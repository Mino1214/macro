"""
image_matcher_v2.py
───────────────────
진단 결과 반영한 실전 버전.

핵심 아이디어: 
  error 좌표(564, 474) 주변 픽셀을 직접 읽어서
  "배경색이 아닌 색"이 있으면 에러 텍스트가 있는 것.

왜 기존 방식이 안 됐는가:
  1. 빨간 텍스트 HSV 감지 → region 계산이 잘못되었거나, 실제 텍스트 색이 순수 빨강이 아닐 수 있음
  2. 템플릿 매칭 → 스크린샷 DPI와 템플릿 DPI가 다르면 스코어 0.3 이하로 떨어짐
  3. 검정 배경 체크 → 배경이 순수 검정(#000)이 아닐 수 있음

해결: 
  - error 좌표 주변 "직접 픽셀 색상" 체크 (가장 확실)
  - 배경색을 먼저 학습하고, 배경과 다른 색이 나타나면 에러로 판단
  - 템플릿은 보조 수단으로만 사용
"""
from __future__ import annotations

import cv2
import numpy as np
import pyautogui
from pathlib import Path
from typing import Optional


def capture_screen(region: tuple[int, int, int, int] | None = None) -> np.ndarray:
    if region:
        x, y, w, h = region
        shot = pyautogui.screenshot(region=(x, y, w, h))
    else:
        shot = pyautogui.screenshot()
    return cv2.cvtColor(np.array(shot), cv2.COLOR_RGB2BGR)


def find_on_screen(
    template_path: str | Path,
    threshold: float = 0.7,
    region: tuple[int, int, int, int] | None = None,
) -> Optional[tuple[int, int]]:
    """기존 호환용 멀티스케일 매칭"""
    template = cv2.imread(str(template_path), cv2.IMREAD_GRAYSCALE)
    if template is None:
        return None

    screen = capture_screen(region)
    screen_gray = cv2.cvtColor(screen, cv2.COLOR_BGR2GRAY)

    for scale in (0.5, 0.75, 0.8, 0.9, 1.0, 1.1, 1.2, 1.25, 1.5, 2.0):
        w = int(template.shape[1] * scale)
        h = int(template.shape[0] * scale)
        if w < 10 or h < 10 or w > screen_gray.shape[1] or h > screen_gray.shape[0]:
            continue
        resized = cv2.resize(template, (w, h), interpolation=cv2.INTER_AREA)
        result = cv2.matchTemplate(screen_gray, resized, cv2.TM_CCOEFF_NORMED)
        _, max_val, _, max_loc = cv2.minMaxLoc(result)
        if max_val >= threshold:
            cx = max_loc[0] + w // 2
            cy = max_loc[1] + h // 2
            if region:
                cx += region[0]
                cy += region[1]
            return (cx, cy)
    return None


# ═══════════════════════════════════════════════════════════════
# 핵심: 픽셀 직접 샘플링 기반 에러 감지
# ═══════════════════════════════════════════════════════════════

def has_error_by_pixel(
    error_x: int,
    error_y: int,
    bg_color: tuple[int, int, int] | None = None,
    color_tolerance: int = 30,
    sample_width: int = 300,
    sample_height: int = 40,
    min_diff_pixels: int = 30,
    debug: bool = False,
) -> bool:
    """
    error 좌표 주변 픽셀을 직접 읽어서 에러 텍스트 존재 여부 판단.
    
    원리:
      에러가 없을 때의 배경색과 비교해서,
      배경과 확연히 다른 색의 픽셀이 일정 수 이상이면 에러 텍스트가 있는 것.
    
    Parameters:
      error_x, error_y: 에러 텍스트가 나타나는 대략적 중심 좌표
      bg_color: 에러가 없을 때의 배경색 (B,G,R). None이면 자동 감지
      color_tolerance: 배경색과의 차이 허용 범위
      sample_width, sample_height: 샘플링 영역 크기
      min_diff_pixels: 이 수 이상 다른 픽셀이 있으면 에러로 판단
      debug: True면 상세 로그 출력
    """
    # 전체 화면 캡처
    screen = capture_screen()
    h, w = screen.shape[:2]

    # 샘플링 영역 계산
    x1 = max(0, error_x - sample_width // 2)
    y1 = max(0, error_y - sample_height // 2)
    x2 = min(w, x1 + sample_width)
    y2 = min(h, y1 + sample_height)

    crop = screen[y1:y2, x1:x2]

    if debug:
        cv2.imwrite("/tmp/debug_error_crop.png", crop)

    # 배경색 자동 감지: 영역의 가장자리 픽셀들의 중앙값을 배경색으로 사용
    if bg_color is None:
        edges = np.concatenate([
            crop[0, :],           # 상단 행
            crop[-1, :],          # 하단 행
            crop[:, 0],           # 좌측 열
            crop[:, -1],          # 우측 열
        ], axis=0)
        bg_color = tuple(int(x) for x in np.median(edges, axis=0))
        if debug:
            print(f"  자동 감지된 배경색(BGR): #{bg_color[2]:02x}{bg_color[1]:02x}{bg_color[0]:02x}")

    # 각 픽셀과 배경색의 차이 계산
    bg = np.array(bg_color, dtype=np.float32)
    diff = np.linalg.norm(crop.astype(np.float32) - bg, axis=2)

    # 배경과 확연히 다른 픽셀 수
    diff_pixels = int(np.count_nonzero(diff > color_tolerance))

    if debug:
        print(f"  샘플 영역: ({x1},{y1})-({x2},{y2})")
        print(f"  배경과 다른 픽셀: {diff_pixels} (threshold: {min_diff_pixels})")
        # 차이 마스크 저장
        mask = (diff > color_tolerance).astype(np.uint8) * 255
        cv2.imwrite("/tmp/debug_diff_mask.png", mask)

    return diff_pixels >= min_diff_pixels


def has_error_by_color_any(
    error_x: int,
    error_y: int,
    sample_width: int = 400,
    sample_height: int = 60,
    min_colored_pixels: int = 30,
    debug: bool = False,
) -> bool:
    """
    error 좌표 주변에서 '채도가 있는 색' (빨강, 주황 등)이 있으면 에러로 판단.
    
    ※ 배경이 무채색(검정/회색/흰색)인 경우에 가장 효과적.
    ※ 에러 텍스트가 빨간색/주황색/노란색 등 채도 있는 색이면 확실히 잡힘.
    """
    screen = capture_screen()
    h, w = screen.shape[:2]

    x1 = max(0, error_x - sample_width // 2)
    y1 = max(0, error_y - sample_height // 2)
    x2 = min(w, x1 + sample_width)
    y2 = min(h, y1 + sample_height)

    crop = screen[y1:y2, x1:x2]
    hsv = cv2.cvtColor(crop, cv2.COLOR_BGR2HSV)

    # 채도(Saturation) > 50 이고 밝기(Value) > 50 이면 "색이 있는" 픽셀
    saturation = hsv[:, :, 1]
    value = hsv[:, :, 2]
    colored_mask = (saturation > 50) & (value > 50)
    colored_count = int(np.count_nonzero(colored_mask))

    if debug:
        print(f"  채도 있는 픽셀: {colored_count} (threshold: {min_colored_pixels})")
        mask_img = colored_mask.astype(np.uint8) * 255
        cv2.imwrite("/tmp/debug_colored_mask.png", mask_img)
        cv2.imwrite("/tmp/debug_error_crop_v2.png", crop)

    return colored_count >= min_colored_pixels


# ═══════════════════════════════════════════════════════════════
# 통합 has_error (실전용)
# ═══════════════════════════════════════════════════════════════

def has_error_v2(
    error_x: int = 564,
    error_y: int = 474,
    template_path: str | Path | None = None,
    debug: bool = False,
) -> bool:
    """
    실전용 에러 감지. 3단계:
    
    1단계: 채도 기반 감지 (에러 텍스트가 색이 있으면 즉시 감지)
    2단계: 배경 대비 픽셀 감지 (배경과 다른 게 있으면 감지)  
    3단계: 템플릿 매칭 (위 두 개 실패 시 fallback)
    """
    if debug:
        print("─── has_error_v2 진단 ───")

    # 1단계: 채도 기반
    if has_error_by_color_any(error_x, error_y, debug=debug):
        if debug:
            print("  🔴 1단계(채도) 감지 → 에러!")
        return True

    # 2단계: 배경 대비
    if has_error_by_pixel(error_x, error_y, debug=debug):
        if debug:
            print("  🟡 2단계(배경대비) 감지 → 에러!")
        return True

    # 3단계: 템플릿 (옵션)
    if template_path and Path(template_path).exists():
        result = find_on_screen(template_path, threshold=0.5)
        if result is not None:
            if debug:
                print("  🖼 3단계(템플릿) 감지 → 에러!")
            return True

    if debug:
        print("  ✅ 에러 미감지")
    return False
