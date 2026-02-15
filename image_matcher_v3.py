"""
image_matcher_v3.py
───────────────────
크롬 창 전용 캡처 + 에러 감지 통합 모듈.

변경점 (v2 → v3):
  - capture_screen()이 크롬 창만 캡처
  - coords.json 좌표를 크롬 내부 좌표로 처리
  - Mac + Windows 양쪽 지원
"""
from __future__ import annotations

import cv2
import numpy as np
from pathlib import Path
from typing import Optional

from chrome_capture import (
    capture_chrome,
    get_chrome_region,
    focus_chrome,
    get_click_scale_xy,
    SYSTEM,
)


# ─── 화면 캡처 (크롬 창 전용) ────────────────────────────────

def capture_screen_logical(
    region: tuple[int, int, int, int] | None = None,
) -> tuple[np.ndarray, tuple[float, float]]:
    """
    캡처 후 논리 해상도로 리사이즈 → 매칭 좌표 = 클릭 좌표.
    Returns: (img, origin_pt) — img는 논리 크기, origin_pt는 캡처 영역 좌상단 (pt).
    """
    import time
    import pyautogui

    focus_chrome()
    time.sleep(0.25)

    chrome = get_chrome_region()
    if chrome is None:
        focus_chrome()
        time.sleep(0.3)
        chrome = get_chrome_region()
    if chrome is None:
        shot = pyautogui.screenshot()
        img = cv2.cvtColor(np.array(shot), cv2.COLOR_RGB2BGR)
        scale_x, scale_y = get_click_scale_xy()
        lw, lh = pyautogui.size()
        img = cv2.resize(img, (lw, lh))
        return (img, (0.0, 0.0))

    cx, cy, cw, ch = chrome
    scale_x, scale_y = get_click_scale_xy()

    if region:
        rx, ry, rw, rh = region
        abs_x = cx + rx
        abs_y = max(0, cy + ry)
        shot = pyautogui.screenshot(region=(abs_x, abs_y, rw, rh))
        img = cv2.cvtColor(np.array(shot), cv2.COLOR_RGB2BGR)
        new_w = max(1, int(rw / scale_x))
        new_h = max(1, int(rh / scale_y))
        img = cv2.resize(img, (new_w, new_h))
        origin_pt = (abs_x / scale_x, abs_y / scale_y)
        return (img, origin_pt)
    else:
        shot = pyautogui.screenshot(region=(cx, cy, cw, ch))
        img = cv2.cvtColor(np.array(shot), cv2.COLOR_RGB2BGR)
        new_w = max(1, int(cw / scale_x))
        new_h = max(1, int(ch / scale_y))
        img = cv2.resize(img, (new_w, new_h))
        origin_pt = (cx / scale_x, cy / scale_y)
        return (img, origin_pt)


def capture_screen(region: tuple[int, int, int, int] | None = None) -> np.ndarray:
    """
    크롬 창만 캡처. 캡처 직전에 크롬을 포커스해서 바탕화면이 찍히지 않도록 함.
    
    region: (x, y, w, h) — 크롬 창 내부의 상대 좌표.
            None이면 크롬 창 전체.
    """
    import time
    import pyautogui

    # 캡처 전 크롬을 앞으로 가져옴 → 해당 영역에 크롬이 보이도록
    focus_chrome()
    time.sleep(0.25)

    chrome = get_chrome_region()
    if chrome is None:
        focus_chrome()
        time.sleep(0.3)
        chrome = get_chrome_region()
    if chrome is None:
        print("⚠ 크롬 창 없음. 크롬을 켜고 다시 시도하세요. (전체 화면 캡처 사용)")
        shot = pyautogui.screenshot()
        return cv2.cvtColor(np.array(shot), cv2.COLOR_RGB2BGR)

    cx, cy, cw, ch = chrome

    # macOS: 권한 없으면 최상단 창(크롬)이 아닌 배경화면만 찍힘 → '화면 기록' 권한 필요
    if region:
        rx, ry, rw, rh = region
        abs_x = cx + rx
        abs_y = max(0, cy + ry)
        shot = pyautogui.screenshot(region=(abs_x, abs_y, rw, rh))
    else:
        shot = pyautogui.screenshot(region=(cx, cy, cw, ch))

    return cv2.cvtColor(np.array(shot), cv2.COLOR_RGB2BGR)


# ─── 템플릿 매칭 (멀티스케일) ────────────────────────────────

# 캡처 해상도가 템플릿과 다를 때 사용. 템플릿을 이 크기에서 캡처했다고 가정.
REFERENCE_SIZE = (1440, 900)  # (width, height) - 바꿀 수 있음


def find_on_screen(
    template_path: str | Path,
    threshold: float = 0.7,
    region: tuple[int, int, int, int] | None = None,
) -> Optional[tuple[int, int]]:
    """멀티스케일 템플릿 매칭. 크롬 창 내부 좌표 반환."""
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
            return (cx, cy)
    return None


def find_on_screen_normalized(
    template_path: str | Path,
    threshold: float = 0.7,
    region: tuple[int, int, int, int] | None = None,
    reference_size: tuple[int, int] = REFERENCE_SIZE,
) -> Optional[tuple[int, int]]:
    """
    비율 기반 좌표 계산:
    1) 매칭용으로 캡처를 reference_size로 리사이즈 후 템플릿 매칭
    2) 매칭된 위치의 비율 계산: ratio_x = x / ref_w, ratio_y = y / ref_h
    3) 실제 화면 크기에 비율 적용: actual_x = ratio_x * 실제너비, actual_y = ratio_y * 실제높이
    → 사진(매칭 이미지)에서 얻은 비율 × 실제 화면 크기 = 정확한 클릭 좌표
    """
    template = cv2.imread(str(template_path), cv2.IMREAD_GRAYSCALE)
    if template is None:
        return None

    screen = capture_screen(region)
    orig_h, orig_w = screen.shape[:2]
    ref_w, ref_h = reference_size

    screen_small = cv2.resize(screen, (ref_w, ref_h), interpolation=cv2.INTER_AREA)
    screen_gray = cv2.cvtColor(screen_small, cv2.COLOR_BGR2GRAY)

    for scale in (0.5, 0.75, 0.8, 0.9, 1.0, 1.1, 1.2, 1.25, 1.5, 2.0):
        w = int(template.shape[1] * scale)
        h = int(template.shape[0] * scale)
        if w < 10 or h < 10 or w > screen_gray.shape[1] or h > screen_gray.shape[0]:
            continue
        resized = cv2.resize(template, (w, h), interpolation=cv2.INTER_AREA)
        result = cv2.matchTemplate(screen_gray, resized, cv2.TM_CCOEFF_NORMED)
        _, max_val, _, max_loc = cv2.minMaxLoc(result)
        if max_val >= threshold:
            # ① 매칭 이미지(ref 크기)에서의 중심 좌표
            cx_in_ref = max_loc[0] + w // 2
            cy_in_ref = max_loc[1] + h // 2
            # ② 사진에서 얻은 좌표 비율 (0~1)
            ratio_x = cx_in_ref / ref_w
            ratio_y = cy_in_ref / ref_h
            # ③ 실제 화면 비율 적용 → 최종 좌표
            actual_x = int(ratio_x * orig_w)
            actual_y = int(ratio_y * orig_h)
            return (actual_x, actual_y)
    return None


# ─── 에러 감지 ───────────────────────────────────────────────

def has_error_by_color(
    error_x: int,
    error_y: int,
    sample_width: int = 400,
    sample_height: int = 60,
    min_colored_pixels: int = 30,
    debug: bool = False,
) -> bool:
    """
    크롬 내부 error 좌표 주변에서 채도 있는 색(빨강 등)이 있으면 에러.
    ※ 좌표는 크롬 창 내부 상대 좌표!
    """
    screen = capture_screen()  # 크롬 전체 캡처
    h, w = screen.shape[:2]

    x1 = max(0, error_x - sample_width // 2)
    y1 = max(0, error_y - sample_height // 2)
    x2 = min(w, x1 + sample_width)
    y2 = min(h, y1 + sample_height)

    crop = screen[y1:y2, x1:x2]
    if crop.size == 0:
        if debug:
            print(f"  ⚠ crop 영역이 비어있음: ({x1},{y1})-({x2},{y2}), 화면={w}x{h}")
        return False

    hsv = cv2.cvtColor(crop, cv2.COLOR_BGR2HSV)
    saturation = hsv[:, :, 1]
    value = hsv[:, :, 2]
    colored_mask = (saturation > 50) & (value > 50)
    colored_count = int(np.count_nonzero(colored_mask))

    if debug:
        print(f"  채도 있는 픽셀: {colored_count} (threshold: {min_colored_pixels})")
        cv2.imwrite("/tmp/debug_chrome_crop.png", crop)
        cv2.imwrite("/tmp/debug_chrome_colored_mask.png",
                     colored_mask.astype(np.uint8) * 255)

    return colored_count >= min_colored_pixels


def has_error_by_pixel(
    error_x: int,
    error_y: int,
    color_tolerance: int = 30,
    sample_width: int = 300,
    sample_height: int = 40,
    min_diff_pixels: int = 30,
    debug: bool = False,
) -> bool:
    """배경 대비 픽셀 차이로 에러 감지. 크롬 내부 좌표 사용."""
    screen = capture_screen()
    h, w = screen.shape[:2]

    x1 = max(0, error_x - sample_width // 2)
    y1 = max(0, error_y - sample_height // 2)
    x2 = min(w, x1 + sample_width)
    y2 = min(h, y1 + sample_height)

    crop = screen[y1:y2, x1:x2]
    if crop.size == 0:
        return False

    # 가장자리 픽셀의 중앙값 = 배경색
    edges = np.concatenate([crop[0, :], crop[-1, :], crop[:, 0], crop[:, -1]], axis=0)
    bg_color = np.median(edges, axis=0).astype(np.float32)

    diff = np.linalg.norm(crop.astype(np.float32) - bg_color, axis=2)
    diff_pixels = int(np.count_nonzero(diff > color_tolerance))

    if debug:
        print(f"  배경색(BGR): {bg_color.astype(int).tolist()}")
        print(f"  배경과 다른 픽셀: {diff_pixels} (threshold: {min_diff_pixels})")

    return diff_pixels >= min_diff_pixels


def has_error_v3(
    error_x: int = 564,
    error_y: int = 474,
    template_path: str | Path | None = None,
    debug: bool = False,
) -> bool:
    """
    크롬 창 기반 에러 감지 (최종 버전).

    ★ error_x, error_y는 크롬 창 내부 상대 좌표여야 함!
      - coords.json의 좌표가 스크린 절대 좌표라면,
        get_chrome_region()으로 크롬 위치를 빼서 상대 좌표로 변환 필요.
    """
    if debug:
        chrome = get_chrome_region()
        print(f"─── has_error_v3 (크롬 캡처 모드) ───")
        print(f"  크롬 영역: {chrome}")
        print(f"  error 좌표(크롬 내부): ({error_x}, {error_y})")

    # 1단계: 채도 기반
    if has_error_by_color(error_x, error_y, debug=debug):
        if debug:
            print("  🔴 채도 감지 → 에러!")
        return True

    # 2단계: 배경 대비
    if has_error_by_pixel(error_x, error_y, debug=debug):
        if debug:
            print("  🟡 배경대비 감지 → 에러!")
        return True

    # 3단계: 템플릿
    if template_path and Path(template_path).exists():
        result = find_on_screen(template_path, threshold=0.5)
        if result is not None:
            if debug:
                print("  🖼 템플릿 감지 → 에러!")
            return True

    if debug:
        print("  ✅ 에러 미감지")
    return False
