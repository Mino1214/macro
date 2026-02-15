"""
image_matcher_pyautogui.py
──────────────────────────
이미지 캡처·인식을 pyautogui만 사용하는 모듈.
OpenCV(cv2) 대신 pyautogui.screenshot + pyautogui.locateOnScreen(confidence=) 사용.

- capture_screen(region=None) → 크롬 창 캡처 (numpy BGR 반환, 기존 코드 호환용)
- find_on_screen(...) → 화면에서 이미지 찾기 (중심 좌표, 크롬 상대)
- find_on_screen_normalized(...) → 비율 보정 좌표 (기존 API 호환)
- find_template_multiscale(...) → locateOnScreen(confidence=) 로 매칭, (left,top), conf, scale 반환

※ confidence 사용 시 opencv-python 필요 (pyautogui가 내부적으로 사용).
"""
from __future__ import annotations

import time
from pathlib import Path
from typing import Optional

import numpy as np
import pyautogui

from chrome_capture import get_chrome_region, focus_chrome


# ─── 화면 캡처 (pyautogui) ────────────────────────────────────

def capture_screen(region: tuple[int, int, int, int] | None = None) -> np.ndarray:
    """
    크롬 창만 캡처. pyautogui.screenshot 사용.
    region=None 이면 크롬 창 전체. 반환은 numpy BGR (기존 cv2 호환).
    """
    focus_chrome()
    time.sleep(0.2)

    chrome = get_chrome_region()
    if chrome is None:
        focus_chrome()
        time.sleep(0.25)
        chrome = get_chrome_region()
    if chrome is None:
        shot = pyautogui.screenshot()
        return np.array(shot)[:, :, ::-1].copy()  # RGB -> BGR

    cx, cy, cw, ch = chrome
    if region:
        rx, ry, rw, rh = region
        shot = pyautogui.screenshot(region=(cx + rx, cy + ry, rw, rh))
    else:
        shot = pyautogui.screenshot(region=(cx, cy, cw, ch))

    return np.array(shot)[:, :, ::-1].copy()  # RGB -> BGR


# ─── 이미지 찾기 (pyautogui.locateOnScreen) ────────────────────

def find_on_screen(
    template_path: str | Path,
    threshold: float = 0.7,
    region: tuple[int, int, int, int] | None = None,
) -> Optional[tuple[int, int]]:
    """
    pyautogui.locateOnScreen(..., confidence=threshold) 로 이미지 찾기.
    반환: (center_x, center_y) 크롬 창 내부 상대 좌표, 없으면 None.
    """
    path = Path(template_path)
    if not path.exists():
        return None

    chrome = get_chrome_region()
    if chrome is None:
        box = pyautogui.locateOnScreen(str(path), confidence=threshold)
    else:
        cx, cy, cw, ch = chrome
        if region:
            rx, ry, rw, rh = region
            search_rect = (cx + rx, cy + ry, rw, rh)
        else:
            search_rect = (cx, cy, cw, ch)
        box = pyautogui.locateOnScreen(str(path), region=search_rect, confidence=threshold)

    if box is None:
        return None
    # box = (left, top, width, height) 화면 절대 좌표
    left, top, w, h = box
    center_screen_x = left + w // 2
    center_screen_y = top + h // 2
    if chrome is not None:
        cx, cy, _, _ = chrome
        return (center_screen_x - cx, center_screen_y - cy)
    return (center_screen_x, center_screen_y)


def find_on_screen_normalized(
    template_path: str | Path,
    threshold: float = 0.7,
    region: tuple[int, int, int, int] | None = None,
    reference_size: tuple[int, int] = (1440, 900),
) -> Optional[tuple[int, int]]:
    """
    find_on_screen과 동일. reference_size는 호환용으로 받기만 함.
    pyautogui는 영역 내 픽셀 좌표를 그대로 쓰므로 비율 재계산 없이 find_on_screen 결과 반환.
    """
    return find_on_screen(template_path, threshold=threshold, region=region)


def find_template_multiscale(
    screen_img: np.ndarray,
    template_path: Path | str,
    threshold: float = 0.7,
    scales: Optional[list[float]] = None,
) -> Optional[tuple[tuple[int, int], float, float]]:
    """
    pyautogui.locateOnScreen(confidence=threshold) 사용. screen_img는 호환용으로 받기만 함.
    반환: ((left, top), confidence, scale) — 크롬 창 내부 상대 좌표.
    scale = 감지된 박스 너비/템플릿 너비 (기존 코드의 w = tw0*scale 호환).
    """
    path = Path(template_path)
    if not path.exists():
        return None

    # 템플릿 크기 (scale 계산용)
    try:
        from PIL import Image
        with Image.open(path) as im:
            tw0, th0 = im.size
    except Exception:
        tw0, th0 = 1, 1

    chrome = get_chrome_region()
    if chrome is None:
        box = pyautogui.locateOnScreen(str(path), confidence=threshold)
        if box is None:
            return None
        left, top, w, h = box
        scale = w / tw0 if tw0 else 1.0
        return ((left, top), threshold, scale)

    cx, cy, cw, ch = chrome
    box = pyautogui.locateOnScreen(str(path), region=(cx, cy, cw, ch), confidence=threshold)
    if box is None:
        return None
    left, top, w, h = box
    left_rel = left - cx
    top_rel = top - cy
    scale = w / tw0 if tw0 else 1.0
    return ((left_rel, top_rel), threshold, scale)


def find_image_on_chrome(
    name: str,
    pics_dir: Path,
    threshold: float = 0.7,
) -> Optional[tuple[tuple[int, int], float, float]]:
    """
    이미지 이름으로 pic 폴더에서 파일 찾아 locateOnScreen.
    반환: ((absolute_pixel_x, absolute_pixel_y), confidence, scale) or None.
    """
    path = pics_dir / name if (pics_dir / name).exists() else pics_dir / f"{name}.png"
    if not path.exists():
        return None

    chrome = get_chrome_region()
    if chrome is None:
        box = pyautogui.locateOnScreen(str(path), confidence=threshold)
        if box is None:
            return None
        left, top, w, h = box
        abs_x = left + w // 2
        abs_y = top + h // 2
        return ((abs_x, abs_y), threshold, 1.0)

    cx, cy, cw, ch = chrome
    box = pyautogui.locateOnScreen(str(path), region=(cx, cy, cw, ch), confidence=threshold)
    if box is None:
        return None
    left, top, w, h = box
    abs_x = left + w // 2
    abs_y = top + h // 2
    return ((abs_x, abs_y), threshold, 1.0)


def find_image_on_chrome_improved(
    name: str,
    threshold: float = 0.7,
    pics_dir: Optional[Path] = None,
) -> Optional[tuple[tuple[int, int], float, float]]:
    """
    find_image_on_chrome_improved(name, threshold) 호환.
    pics_dir 미지정 시 이 파일 기준 pic/kr 사용.
    """
    if pics_dir is None:
        pics_dir = Path(__file__).parent / "pic" / "kr"
    return find_image_on_chrome(name, pics_dir, threshold)
