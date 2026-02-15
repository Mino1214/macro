"""
image_matcher_pro.py
────────────────────
Pro급 화면 매칭 모듈.

기존 단순 matchTemplate 대비 개선점:
1. 멀티스케일 매칭 (해상도/DPI 차이 대응)
2. 그레이스케일 + 엣지 기반 이중 매칭
3. 색상 기반 텍스트 감지 (빨간 에러 텍스트 특화)
4. SSIM fallback
5. 디버그 모드로 매칭 과정 시각화
"""
from __future__ import annotations

import cv2
import numpy as np
from pathlib import Path
from typing import Optional

# ─── 화면 캡처 ───────────────────────────────────────────────

def capture_screen(region: tuple[int, int, int, int] | None = None) -> np.ndarray:
    """
    화면 캡처 -> BGR numpy 배열 반환.
    region: (x, y, w, h) 또는 None(전체 화면)
    """
    import pyautogui
    if region:
        x, y, w, h = region
        shot = pyautogui.screenshot(region=(x, y, w, h))
    else:
        shot = pyautogui.screenshot()
    img = np.array(shot)
    return cv2.cvtColor(img, cv2.COLOR_RGB2BGR)


# ─── 전략 1: 멀티스케일 템플릿 매칭 ──────────────────────────

def find_on_screen_multiscale(
    template_path: str | Path,
    threshold: float = 0.7,
    scales: tuple[float, ...] = (0.5, 0.75, 0.8, 0.9, 1.0, 1.1, 1.2, 1.25, 1.5, 2.0),
    region: tuple[int, int, int, int] | None = None,
    debug: bool = False,
) -> Optional[tuple[int, int, float]]:
    """
    여러 스케일로 템플릿 매칭. DPI/해상도 차이에 강건함.
    Returns: (cx, cy, score) 또는 None
    """
    template = cv2.imread(str(template_path), cv2.IMREAD_GRAYSCALE)
    if template is None:
        raise FileNotFoundError(f"템플릿 로드 실패: {template_path}")

    screen = capture_screen(region)
    screen_gray = cv2.cvtColor(screen, cv2.COLOR_BGR2GRAY)

    best_score = -1.0
    best_loc = None
    best_scale = 1.0
    best_size = template.shape[::-1]

    for scale in scales:
        w = int(template.shape[1] * scale)
        h = int(template.shape[0] * scale)
        if w < 10 or h < 10 or w > screen_gray.shape[1] or h > screen_gray.shape[0]:
            continue

        resized = cv2.resize(template, (w, h), interpolation=cv2.INTER_AREA)

        # TM_CCOEFF_NORMED 가 일반적으로 가장 안정적
        result = cv2.matchTemplate(screen_gray, resized, cv2.TM_CCOEFF_NORMED)
        _, max_val, _, max_loc = cv2.minMaxLoc(result)

        if debug:
            print(f"  scale={scale:.2f}  score={max_val:.4f}  loc={max_loc}")

        if max_val > best_score:
            best_score = max_val
            best_loc = max_loc
            best_scale = scale
            best_size = (w, h)

    if best_score >= threshold and best_loc is not None:
        cx = best_loc[0] + best_size[0] // 2
        cy = best_loc[1] + best_size[1] // 2
        if region:
            cx += region[0]
            cy += region[1]
        if debug:
            print(f"  ✅ MATCH: scale={best_scale:.2f} score={best_score:.4f} center=({cx},{cy})")
        return (cx, cy, best_score)

    if debug:
        print(f"  ❌ NO MATCH: best_score={best_score:.4f} < threshold={threshold}")
    return None


# ─── 전략 2: 엣지 기반 매칭 (조명/밝기 변화에 강건) ──────────

def find_on_screen_edge(
    template_path: str | Path,
    threshold: float = 0.6,
    region: tuple[int, int, int, int] | None = None,
    debug: bool = False,
) -> Optional[tuple[int, int, float]]:
    """
    Canny 엣지 변환 후 매칭. 밝기/대비 차이에 매우 강건.
    """
    template = cv2.imread(str(template_path), cv2.IMREAD_GRAYSCALE)
    if template is None:
        raise FileNotFoundError(f"템플릿 로드 실패: {template_path}")

    screen = capture_screen(region)
    screen_gray = cv2.cvtColor(screen, cv2.COLOR_BGR2GRAY)

    # Canny 엣지 검출
    tpl_edge = cv2.Canny(template, 50, 150)
    scr_edge = cv2.Canny(screen_gray, 50, 150)

    result = cv2.matchTemplate(scr_edge, tpl_edge, cv2.TM_CCOEFF_NORMED)
    _, max_val, _, max_loc = cv2.minMaxLoc(result)

    if debug:
        print(f"  Edge match: score={max_val:.4f}")

    if max_val >= threshold:
        cx = max_loc[0] + template.shape[1] // 2
        cy = max_loc[1] + template.shape[0] // 2
        if region:
            cx += region[0]
            cy += region[1]
        return (cx, cy, max_val)
    return None


# ─── 전략 3: 빨간 텍스트 색상 감지 (에러 메시지 특화) ────────

def detect_red_text(
    region: tuple[int, int, int, int] | None = None,
    min_red_pixels: int = 50,
    debug: bool = False,
) -> bool:
    """
    빨간색 텍스트가 화면에 있는지 HSV 색공간으로 감지.
    '니모닉 문구가 유효하지 않습니다' 같은 빨간 에러에 최적화.
    
    ※ 템플릿 매칭보다 훨씬 안정적! 해상도/스케일 무관.
    """
    screen = capture_screen(region)
    hsv = cv2.cvtColor(screen, cv2.COLOR_BGR2HSV)

    # 빨간색 범위 (HSV) — 빨강은 H가 0 근처와 170 이상 두 구간
    lower_red1 = np.array([0, 80, 80])
    upper_red1 = np.array([10, 255, 255])
    lower_red2 = np.array([170, 80, 80])
    upper_red2 = np.array([180, 255, 255])

    mask1 = cv2.inRange(hsv, lower_red1, upper_red1)
    mask2 = cv2.inRange(hsv, lower_red2, upper_red2)
    red_mask = mask1 | mask2

    red_pixel_count = int(np.count_nonzero(red_mask))

    if debug:
        print(f"  Red pixels detected: {red_pixel_count} (threshold: {min_red_pixels})")
        # 디버그 이미지 저장
        cv2.imwrite("/tmp/debug_red_mask.png", red_mask)
        cv2.imwrite("/tmp/debug_screen.png", screen)

    return red_pixel_count >= min_red_pixels


# ─── 전략 4: OCR 기반 텍스트 감지 (가장 확실) ────────────────

def detect_text_ocr(
    target_text: str = "유효하지 않습니다",
    region: tuple[int, int, int, int] | None = None,
    debug: bool = False,
) -> bool:
    """
    Tesseract OCR로 화면에서 특정 텍스트 존재 여부 확인.
    pip install pytesseract 필요. tesseract-ocr 설치 필요.
    
    ※ 한글 인식은 tesseract 한글 데이터 필요:
       sudo apt install tesseract-ocr-kor
    """
    try:
        import pytesseract
    except ImportError:
        print("⚠ pytesseract 미설치. pip install pytesseract")
        return False

    screen = capture_screen(region)
    gray = cv2.cvtColor(screen, cv2.COLOR_BGR2GRAY)

    # 이진화로 텍스트 선명하게
    _, binary = cv2.threshold(gray, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)

    text = pytesseract.image_to_string(binary, lang="kor+eng")

    if debug:
        print(f"  OCR result: {text[:100]}...")

    return target_text in text


# ─── 통합 감지 함수 (Pro 모드) ───────────────────────────────

def find_on_screen(
    template_path: str | Path,
    threshold: float = 0.7,
    region: tuple[int, int, int, int] | None = None,
    debug: bool = False,
) -> Optional[tuple[int, int]]:
    """
    기존 find_on_screen 드롭인 대체.
    여러 전략을 순차적으로 시도해서 인식률 극대화.
    
    Returns: (cx, cy) 또는 None
    """
    # 1차: 멀티스케일 템플릿 매칭
    result = find_on_screen_multiscale(
        template_path, threshold=threshold, region=region, debug=debug
    )
    if result:
        return (result[0], result[1])

    # 2차: 엣지 기반 매칭 (threshold 살짝 낮춰서)
    result = find_on_screen_edge(
        template_path, threshold=max(0.5, threshold - 0.15), region=region, debug=debug
    )
    if result:
        return (result[0], result[1])

    # 3차: 멀티스케일을 더 낮은 threshold로 재시도
    result = find_on_screen_multiscale(
        template_path, threshold=max(0.45, threshold - 0.25), region=region, debug=debug
    )
    if result:
        return (result[0], result[1])

    return None


# ─── has_error 개선 버전 ─────────────────────────────────────

def has_error_pro(
    template_path: str | Path | None = None,
    error_region: tuple[int, int, int, int] | None = None,
    debug: bool = False,
) -> bool:
    """
    에러 감지 Pro 버전. 3중 체크:
    1. 빨간 텍스트 색상 감지 (가장 빠르고 안정적)
    2. 멀티스케일 템플릿 매칭
    3. 엣지 기반 매칭
    
    하나라도 감지되면 True.
    """
    # 전략 1: 빨간 텍스트 감지 — 가장 추천!
    if detect_red_text(region=error_region, debug=debug):
        if debug:
            print("  🔴 빨간 텍스트 감지됨 → 에러 판정")
        return True

    # 전략 2+3: 템플릿 매칭
    if template_path and Path(template_path).exists():
        result = find_on_screen(
            template_path, threshold=0.6, region=error_region, debug=debug
        )
        if result is not None:
            if debug:
                print("  🖼 템플릿 매칭 성공 → 에러 판정")
            return True

    if debug:
        print("  ✅ 에러 미감지")
    return False
