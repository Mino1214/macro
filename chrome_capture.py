"""
chrome_capture.py
─────────────────
크롬 창만 정확히 캡처하는 모듈. Mac + Windows 양쪽 지원.
"""
from __future__ import annotations

import platform
import subprocess
import numpy as np
import cv2
from typing import Optional

SYSTEM = platform.system()


def _mac_scale_factor() -> float:
    """Mac Retina: AppleScript는 포인트, pyautogui.screenshot(region=)은 픽셀. 포인트→픽셀 변환용."""
    if SYSTEM != "Darwin":
        return 1.0
    try:
        from AppKit import NSScreen
        return float(NSScreen.mainScreen().backingScaleFactor())
    except Exception:
        return 2.0  # Retina 기본값


def scale_for_click() -> float:
    """
    클릭 시 픽셀→포인트 변환에 쓸 배율. (하위 호환용)
    신규 코드는 get_click_scale_xy() + px_to_pt() 사용 권장.
    """
    return _mac_scale_factor()


# ─── 캡처(px) ↔ 논리(pt) 변환 ────────────────────────────────────────────────
# 실제 캡처된 해상도와 pyautogui max(x,y) 비율로 계산
# 캡처 좌표 × (논리_max / 캡처_해상도) = 논리 좌표

_capture_size_override: Optional[tuple[int, int]] = None  # (w, h) 실제 캡처 이미지 해상도
_logical_size_cache: Optional[tuple[int, int]] = None  # (logical_w, logical_h)


def set_capture_size(capture_w: int, capture_h: int) -> None:
    """실제 캡처된 이미지 해상도 설정. 캡처 직후 img.shape → (shape[1], shape[0]) 로 호출."""
    global _capture_size_override
    _capture_size_override = (int(capture_w), int(capture_h))


def clear_capture_size() -> None:
    """캡처 크기 오버라이드 해제. px_to_pt 시 전체 화면 스크린샷 기준 비율 사용(화면 픽셀 좌표 변환용)."""
    global _capture_size_override
    _capture_size_override = None


def get_capture_and_logical_size() -> tuple[tuple[int, int], tuple[int, int]]:
    """
    실제 캡처된 해상도와 pyautogui max(x,y) 반환.
    set_capture_size()로 설정된 값이 있으면 그대로 사용, 없으면 전체 화면 스크린샷으로 측정.
    Returns: ((capture_width_px, capture_height_px), (logical_max_x, logical_max_y))
    """
    global _logical_size_cache
    try:
        import pyautogui
        logical_w, logical_h = pyautogui.size()
        if logical_w <= 0 or logical_h <= 0:
            logical_w, logical_h = 1920, 1080
        if _logical_size_cache is None:
            _logical_size_cache = (logical_w, logical_h)
        else:
            logical_w, logical_h = _logical_size_cache
    except Exception:
        logical_w, logical_h = 1920, 1080

    if _capture_size_override is not None:
        return (_capture_size_override, (logical_w, logical_h))
    try:
        import pyautogui
        shot = pyautogui.screenshot()
        capture_w = shot.width
        capture_h = shot.height
        return (((capture_w, capture_h), (logical_w, logical_h)))
    except Exception:
        return (((logical_w, logical_h), (logical_w, logical_h)))


def get_click_scale_xy() -> tuple[float, float]:
    """
    캡처 해상도 / 논리 해상도 비율. (캐시용, 하위 호환)
    pt = px / scale 이므로 scale = capture / logical.
    """
    (capture_w, capture_h), (logical_w, logical_h) = get_capture_and_logical_size()
    if logical_w <= 0 or logical_h <= 0:
        s = _mac_scale_factor()
        return (s, s)
    return (capture_w / logical_w, capture_h / logical_h)


def get_chrome_region_logical() -> Optional[tuple[float, float, float, float]]:
    """크롬 창 (x, y, w, h)를 논리 좌표(pt)로 반환. 캡처 비율=화면 비율 맞출 때 사용."""
    r = get_chrome_region()
    if r is None:
        return None
    cx, cy, cw, ch = r
    scale_x, scale_y = get_click_scale_xy()
    return (cx / scale_x, cy / scale_y, cw / scale_x, ch / scale_y)


def px_to_pt(px_x: float, px_y: float) -> tuple[int, int]:
    """
    캡처 좌표 → 논리 좌표: 캡처 해상도와 실제(max x,y) 비율을 곱해서 변환.
    pt_x = px_x × (logical_max_x / capture_width)
    pt_y = px_y × (logical_max_y / capture_height)
    """
    (capture_w, capture_h), (logical_w, logical_h) = get_capture_and_logical_size()
    if capture_w <= 0 or capture_h <= 0:
        return (int(round(px_x)), int(round(px_y)))
    ratio_x = logical_w / capture_w
    ratio_y = logical_h / capture_h
    pt_x = px_x * ratio_x
    pt_y = px_y * ratio_y
    return (int(round(pt_x)), int(round(pt_y)))


def px_to_pt_with_formula(px_x: float, px_y: float) -> tuple[int, int, str]:
    """
    px_to_pt와 동일하지만 계산법 표기용 문자열도 반환.
    Returns: (pt_x, pt_y, formula_string)
    """
    (capture_w, capture_h), (logical_w, logical_h) = get_capture_and_logical_size()
    if capture_w <= 0 or capture_h <= 0:
        pt_x, pt_y = int(round(px_x)), int(round(px_y))
        return (pt_x, pt_y, f"  [좌표변환] 캡처/논리 미측정 → pt=({pt_x},{pt_y})")
    ratio_x = logical_w / capture_w
    ratio_y = logical_h / capture_h
    pt_x = int(round(px_x * ratio_x))
    pt_y = int(round(px_y * ratio_y))
    formula = (
        f"  [좌표변환] 캡처 해상도=({capture_w}, {capture_h})  pyautogui max(x,y)=({logical_w}, {logical_h})\n"
        f"            비율 ratio_x={logical_w}/{capture_w}={ratio_x:.4f}  ratio_y={logical_h}/{capture_h}={ratio_y:.4f}\n"
        f"            계산: pt_x = px_x × (논리_max_x/캡처_w) = {px_x} × {ratio_x:.4f} = {pt_x}\n"
        f"            계산: pt_y = px_y × (논리_max_y/캡처_h) = {px_y} × {ratio_y:.4f} = {pt_y}\n"
        f"            => pt=({pt_x}, {pt_y})"
    )
    return (pt_x, pt_y, formula)


# ─── 크롬 창 위치/크기 찾기 ──────────────────────────────────

def get_chrome_region() -> Optional[tuple[int, int, int, int]]:
    """크롬 창의 (x, y, width, height)를 반환. Mac에서는 pyautogui 픽셀 좌표로 맞춤."""
    if SYSTEM == "Darwin":
        return _get_chrome_region_mac()
    elif SYSTEM == "Windows":
        return _get_chrome_region_windows()
    else:
        print(f"⚠ 지원하지 않는 OS: {SYSTEM}")
        return None


def _apply_mac_scale(x: int, y: int, w: int, h: int) -> tuple[int, int, int, int]:
    """Mac: 포인트 좌표를 pyautogui 픽셀 좌표로 변환 (Retina 시 배경화면만 찍히는 문제 해결)."""
    s = _mac_scale_factor()
    return (int(x * s), int(y * s), int(w * s), int(h * s))


def _get_chrome_region_mac() -> Optional[tuple[int, int, int, int]]:
    """
    Mac: AppleScript로 크롬 창 위치/크기 가져오기 (포인트).
    반환 시 Retina 스케일을 곱해 pyautogui.screenshot(region=)에 맞는 픽셀 좌표로 변환.
    """
    # 방법 1: bounds를 직접 가져오기
    try:
        result = subprocess.run(
            ["osascript", "-e",
             'tell application "Google Chrome" to get bounds of front window'],
            capture_output=True, text=True, timeout=5
        )
        output = result.stdout.strip()
        if output and output != "NONE":
            parts = [int(x.strip()) for x in output.split(",") if x.strip()]
            if len(parts) == 4:
                x1, y1, x2, y2 = parts
                return _apply_mac_scale(x1, y1, x2 - x1, y2 - y1)
    except Exception:
        pass

    # 방법 2: System Events로 position + size
    try:
        result = subprocess.run(
            ["osascript", "-e",
             'tell application "System Events" to tell process "Google Chrome" '
             'to get {position, size} of front window'],
            capture_output=True, text=True, timeout=5
        )
        output = result.stdout.strip()
        if output:
            nums = [int(x.strip()) for x in output.replace("position:", "").replace("size:", "").split(",") if x.strip().lstrip("-").isdigit()]
            if len(nums) == 4:
                x, y, w, h = nums
                return _apply_mac_scale(x, y, w, h)
    except Exception:
        pass

    # 방법 3: position과 size를 각각 따로 가져오기
    try:
        pos_result = subprocess.run(
            ["osascript", "-e",
             'tell application "System Events" to tell process "Google Chrome" '
             'to get position of front window'],
            capture_output=True, text=True, timeout=5
        )
        size_result = subprocess.run(
            ["osascript", "-e",
             'tell application "System Events" to tell process "Google Chrome" '
             'to get size of front window'],
            capture_output=True, text=True, timeout=5
        )
        pos_str = pos_result.stdout.strip()
        size_str = size_result.stdout.strip()
        if pos_str and size_str:
            px, py = [int(x.strip()) for x in pos_str.split(",")]
            sw, sh = [int(x.strip()) for x in size_str.split(",")]
            return _apply_mac_scale(px, py, sw, sh)
    except Exception:
        pass

    # 방법 4: CGWindowListCopyWindowInfo (이미 픽셀일 수 있음 → 스케일 적용 안 함)
    try:
        import Quartz
        window_list = Quartz.CGWindowListCopyWindowInfo(
            Quartz.kCGWindowListOptionOnScreenOnly | Quartz.kCGWindowListExcludeDesktopElements,
            Quartz.kCGNullWindowID
        )
        for win in window_list:
            owner = win.get("kCGWindowOwnerName", "")
            if "Google Chrome" in owner or "Chrome" in owner:
                bounds = win.get("kCGWindowBounds", {})
                x = int(bounds.get("X", 0))
                y = int(bounds.get("Y", 0))
                w = int(bounds.get("Width", 0))
                h = int(bounds.get("Height", 0))
                if w > 100 and h > 100:
                    # Quartz는 픽셀일 수 있으므로 스케일 적용하지 않음 (또는 적용해볼 수 있음)
                    return (x, y, w, h)
    except ImportError:
        pass

    print("⚠ 모든 Mac 크롬 감지 방법 실패")
    return None


def _get_chrome_region_windows() -> Optional[tuple[int, int, int, int]]:
    """Windows: win32gui로 크롬 창 위치/크기 가져오기"""
    try:
        import win32gui

        results = []
        def callback(hwnd, res):
            if win32gui.IsWindowVisible(hwnd):
                title = win32gui.GetWindowText(hwnd)
                if "Google Chrome" in title or "Chrome" in title:
                    rect = win32gui.GetWindowRect(hwnd)
                    x, y, x2, y2 = rect
                    w, h = x2 - x, y2 - y
                    if w > 100 and h > 100:
                        res.append((x, y, w, h))

        win32gui.EnumWindows(callback, results)
        if results:
            return results[0]
    except ImportError:
        try:
            import pygetwindow as gw
            windows = gw.getWindowsWithTitle("Chrome")
            if not windows:
                windows = gw.getWindowsWithTitle("Google Chrome")
            if windows:
                w = windows[0]
                if w.isMinimized:
                    w.restore()
                return (w.left, w.top, w.width, w.height)
        except ImportError:
            print("⚠ pywin32 또는 pygetwindow 필요")
    return None


# ─── 크롬 창 캡처 ────────────────────────────────────────────

def capture_chrome(debug: bool = False) -> Optional[np.ndarray]:
    """크롬 창 영역만 캡처해서 BGR numpy 배열로 반환."""
    import pyautogui

    region = get_chrome_region()
    if region is None:
        print("⚠ 크롬 창을 찾을 수 없습니다.")
        return None

    x, y, w, h = region
    if debug:
        print(f"  크롬 캡처: x={x}, y={y}, {w}x{h}")

    shot = pyautogui.screenshot(region=(x, y, w, h))
    return cv2.cvtColor(np.array(shot), cv2.COLOR_RGB2BGR)


# ─── 크롬 창 포커스 ──────────────────────────────────────────

def focus_chrome() -> bool:
    """크롬을 최상위로 올리기"""
    if SYSTEM == "Darwin":
        try:
            subprocess.run(
                ["osascript", "-e", 'tell application "Google Chrome" to activate'],
                timeout=5
            )
            return True
        except Exception:
            return False

    elif SYSTEM == "Windows":
        try:
            import win32gui
            def callback(hwnd, _):
                if win32gui.IsWindowVisible(hwnd):
                    title = win32gui.GetWindowText(hwnd)
                    if "Chrome" in title:
                        win32gui.SetForegroundWindow(hwnd)
                        return False
                return True
            win32gui.EnumWindows(callback, None)
            return True
        except Exception:
            try:
                import pygetwindow as gw
                windows = gw.getWindowsWithTitle("Chrome")
                if windows:
                    windows[0].activate()
                    return True
            except Exception:
                pass
    return False
