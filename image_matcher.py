"""
사진(이미지) 기반 매크로 - 화면에서 템플릿 이미지를 찾는 모듈
"""
from __future__ import annotations

import cv2
import numpy as np
from pathlib import Path
from typing import Optional

try:
    import mss
    import mss.tools
except ImportError:
    mss = None

try:
    import pyautogui
except ImportError:
    pyautogui = None


def capture_screen(region: Optional[tuple[int, int, int, int]] = None) -> np.ndarray:
    """
    화면을 캡처하여 BGR numpy 배열로 반환.
    region: (x, y, width, height) 또는 None이면 전체 화면
    """
    if mss:
        with mss.mss() as sct:
            if region:
                x, y, w, h = region
                monitor = {"left": x, "top": y, "width": w, "height": h}
            else:
                monitor = sct.monitors[0]
            img = np.array(sct.grab(monitor))
            return cv2.cvtColor(img, cv2.COLOR_BGRA2BGR)
    if pyautogui:
        if region:
            x, y, w, h = region
            img = pyautogui.screenshot(region=(x, y, w, h))
        else:
            img = pyautogui.screenshot()
        return cv2.cvtColor(np.array(img), cv2.COLOR_RGB2BGR)
    raise RuntimeError("mss 또는 pyautogui가 필요합니다. pip install mss pyautogui")


def find_template(
    screen: np.ndarray,
    template_path: str | Path,
    threshold: float = 0.8,
    method: int = cv2.TM_CCOEFF_NORMED,
) -> list[tuple[int, int]]:
    """
    화면 이미지에서 템플릿 이미지를 찾아 매칭된 좌표 목록 반환.
    반환: [(center_x, center_y), ...] - 템플릿 중심 좌표
    """
    template = cv2.imread(str(template_path))
    if template is None:
        raise FileNotFoundError(f"템플릿 이미지를 찾을 수 없습니다: {template_path}")

    screen_gray = cv2.cvtColor(screen, cv2.COLOR_BGR2GRAY)
    template_gray = cv2.cvtColor(template, cv2.COLOR_BGR2GRAY)
    th, tw = template_gray.shape[:2]

    result = cv2.matchTemplate(screen_gray, template_gray, method)
    if method in (cv2.TM_SQDIFF, cv2.TM_SQDIFF_NORMED):
        locations = np.where(result <= 1 - threshold)
    else:
        locations = np.where(result >= threshold)

    centers = []
    for pt in zip(*locations[::-1]):
        cx = pt[0] + tw // 2
        cy = pt[1] + th // 2
        centers.append((cx, cy))
    return centers


def find_on_screen(
    template_path: str | Path,
    region: Optional[tuple[int, int, int, int]] = None,
    threshold: float = 0.8,
) -> Optional[tuple[int, int]]:
    """
    현재 화면에서 템플릿을 찾아 첫 번째 매칭의 중심 (x, y) 반환.
    없으면 None.
    """
    screen = capture_screen(region)
    matches = find_template(screen, template_path, threshold=threshold)
    if not matches:
        return None
    if region:
        rx, ry, _, _ = region
        mx, my = matches[0]
        return (rx + mx, ry + my)
    return matches[0]


def save_region_as_template(
    region: tuple[int, int, int, int],
    save_path: str | Path,
) -> None:
    """화면의 지정 영역을 캡처해 템플릿 이미지로 저장"""
    img = capture_screen(region)
    cv2.imwrite(str(save_path), img)


def find_on_screen_orb(
    template_path: str | Path,
    region: Optional[tuple[int, int, int, int]] = None,
    min_match_count: int = 8,
) -> Optional[tuple[int, int]]:
    """
    ORB 특징점을 이용해 더 강력하게 템플릿 위치를 찾는다.
    - 크기/밝기/약간의 회전에 더 강함.
    - 매칭이 불충분하면 None 반환.
    """
    template_path = Path(template_path)
    template = cv2.imread(str(template_path), cv2.IMREAD_GRAYSCALE)
    if template is None:
        raise FileNotFoundError(f"템플릿 이미지를 찾을 수 없습니다: {template_path}")

    screen_bgr = capture_screen(region)
    screen_gray = cv2.cvtColor(screen_bgr, cv2.COLOR_BGR2GRAY)

    # ORB 특징점 검출
    orb = cv2.ORB_create(nfeatures=500)
    kp1, des1 = orb.detectAndCompute(template, None)
    kp2, des2 = orb.detectAndCompute(screen_gray, None)
    if des1 is None or des2 is None or len(kp1) == 0 or len(kp2) == 0:
        return None

    # BFMatcher + Lowe ratio test
    bf = cv2.BFMatcher(cv2.NORM_HAMMING, crossCheck=False)
    matches = bf.knnMatch(des1, des2, k=2)

    good = []
    for m, n in matches:
        if m.distance < 0.75 * n.distance:
            good.append(m)

    if len(good) < min_match_count:
        return None

    src_pts = np.float32([kp1[m.queryIdx].pt for m in good]).reshape(-1, 1, 2)
    dst_pts = np.float32([kp2[m.trainIdx].pt for m in good]).reshape(-1, 1, 2)

    # 호모그래피로 위치 추정
    M, mask = cv2.findHomography(src_pts, dst_pts, cv2.RANSAC, 5.0)
    if M is None:
        return None

    h, w = template.shape
    corners = np.float32(
        [[0, 0], [0, h - 1], [w - 1, h - 1], [w - 1, 0]]
    ).reshape(-1, 1, 2)
    dst = cv2.perspectiveTransform(corners, M)

    cx = int(dst[:, 0, 0].mean())
    cy = int(dst[:, 0, 1].mean())

    if region:
        rx, ry, _, _ = region
        return rx + cx, ry + cy
    return cx, cy


def _crop_whitespace(img: np.ndarray, bg_thresh: int = 240) -> np.ndarray:
    """흰색/밝은 여백을 자동으로 잘라냄"""
    gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
    # 밝은 배경이 아닌 부분 찾기
    mask = gray < bg_thresh
    coords = cv2.findNonZero(mask.astype(np.uint8))
    if coords is None:
        return img
    x, y, w, h = cv2.boundingRect(coords)
    # 약간의 패딩 추가
    pad = 2
    x = max(0, x - pad)
    y = max(0, y - pad)
    w = min(img.shape[1] - x, w + pad * 2)
    h = min(img.shape[0] - y, h + pad * 2)
    return img[y : y + h, x : x + w]


def find_template_multiscale(
    screen: np.ndarray,
    template_path: str | Path,
    threshold: float = 0.75,
    scales: list[float] | None = None,
) -> Optional[tuple[int, int]]:
    """
    멀티스케일 + 크롭된 템플릿으로 매칭.
    - 템플릿 크기가 실제 화면 아이콘과 조금 달라도 더 잘 찾도록 보완.
    """
    template = cv2.imread(str(template_path))
    if template is None:
        raise FileNotFoundError(f"템플릿: {template_path}")

    # ① 흰 여백 자동 크롭
    template = _crop_whitespace(template)

    screen_gray = cv2.cvtColor(screen, cv2.COLOR_BGR2GRAY)
    template_gray = cv2.cvtColor(template, cv2.COLOR_BGR2GRAY)

    if scales is None:
        scales = [0.5, 0.6, 0.7, 0.8, 0.9, 1.0, 1.1, 1.2, 1.5, 2.0]

    best_val = -1.0
    best_loc: Optional[tuple[int, int]] = None
    best_tw, best_th = 0, 0

    for scale in scales:
        tw = int(template_gray.shape[1] * scale)
        th = int(template_gray.shape[0] * scale)
        if (
            tw < 10
            or th < 10
            or tw > screen_gray.shape[1]
            or th > screen_gray.shape[0]
        ):
            continue

        resized = cv2.resize(template_gray, (tw, th), interpolation=cv2.INTER_AREA)
        result = cv2.matchTemplate(screen_gray, resized, cv2.TM_CCOEFF_NORMED)
        _, max_val, _, max_loc = cv2.minMaxLoc(result)

        if max_val > best_val:
            best_val = max_val
            best_loc = max_loc
            best_tw, best_th = tw, th

    if best_loc is not None and best_val >= threshold:
        cx = best_loc[0] + best_tw // 2
        cy = best_loc[1] + best_th // 2
        return (cx, cy)
    return None


def find_on_screen_multiscale(
    template_path: str | Path,
    region: Optional[tuple[int, int, int, int]] = None,
    threshold: float = 0.75,
    scales: list[float] | None = None,
) -> Optional[tuple[int, int]]:
    """화면 전체(or 영역)에서 멀티스케일 템플릿 매칭으로 중심 좌표를 찾는다."""
    screen = capture_screen(region)
    pos = find_template_multiscale(screen, template_path, threshold=threshold, scales=scales)
    if pos is None:
        return None
    cx, cy = pos
    if region:
        rx, ry, _, _ = region
        return rx + cx, ry + cy
    return cx, cy