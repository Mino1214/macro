"""
main_patched_ratio.py
─────────────────────
mainpage 이미지를 찾은 뒤, 그 사진의 **정가운데로부터 비율**로 버튼 위치를 계산해 클릭.

- mainpage.png 한 번만 매칭 → (중심 x,y, 너비 w, 높이 h) 획득
- 각 버튼은 "mainpage 중심 + (비율_x * w, 비율_y * h)" 로 클릭
- 해상도가 달라도 비율만 맞으면 동일한 상대 위치 클릭
"""
from __future__ import annotations

import random
import sys
import time
import platform
from pathlib import Path

import pyautogui
import cv2
import numpy as np

from image_matcher_v3 import capture_screen, capture_screen_logical, find_on_screen, find_on_screen_normalized
from chrome_capture import get_chrome_region, focus_chrome, px_to_pt, px_to_pt_with_formula, clear_capture_size

# improved와 동일한 인식·매칭 사용
from main_patched_improved import find_template_multiscale, find_image_on_chrome_improved

BASE_DIR = Path(__file__).parent
PICS_KR = BASE_DIR / "pic" / "kr"
TEMPLATES_DIR = BASE_DIR / "macros" / "templates"
WORDLIST_FILE = BASE_DIR / "wordlist.txt"
# ✅ 첫 정상 실행 때의 크롬 창 크기를 기준으로 오프셋을 비율 스케일링
BASELINE_CHROME_SIZE: tuple[int, int] | None = None

SLOT_IMAGE_NAMES = [str(i) for i in range(1, 13)]

# 테스트 모드: 실행 시 인자로 "테스트" 주면 3세트에서 고정 니모닉으로 로그인 시도 (루프 확인용)
TEST_MODE = len(sys.argv) > 1 and sys.argv[1].strip() == "테스트"
FIXED_TEST_MNEMONIC = [
    "visa", "trick", "federal", "thrive", "laundry", "vanish",
    "novel", "remain", "cancel", "worth", "nut", "suffer",
]

MAINPAGE_DETECT = ["mainpage", "mainpage_menu", "mainpage_add", "mainpage_getwallet", "next"]

# ★ 1세트 작업 순서 (말씀하신 방식)
# 1. 스크린샷 → 2. 메인페이지인지 확인 → 3·4. 같은 스크린샷에서 헤더(햄버거) 찾아 [클릭]
# → 5. 새 스크린샷 → 6. 지갑추가하기 찾아 [클릭] → 7. 지갑가져오기 찾아 [클릭]
MAINPAGE_FLOW = [
    ("ratio", "mainpage_menu"),   # 3·4. 헤더(햄버거) 클릭 (1번 스크린샷에서 찾거나 offset)
    ("image", "mainpage_add"),    # 5·6. 새 스크린샷 → 지갑추가하기 찾아 클릭
    ("image", "mainpage_next"),   # 7. 지갑가져오기 찾아 클릭 (새 스크린샷)
]

# ★ 2세트: selectionpage일 때 두 번 클릭 (문구 → 다음). 감지는 selectionpage.png 로 1번과 동일 방식
SELECTIONPAGE_DETECT = ["selectionpage"]
SELECTIONPAGE_FLOW = [
    ("ratio", "selectionpage_phrase"),  # 첫번째: 문구 클릭 (기준점 x-70, y+205)
    ("ratio", "selectionpage_next"),    # 두번째: 다음 클릭 (기준점 x-70, y+352)
]

# ★ 헤더 메뉴(지갑 이름 옆): 더보기(동그라미) 말고 헤더 햄버거만. mainpage_menu_header.png = 햄버거만 크롭 권장.
HEADER_MENU_REGION = (0.35, 0, 0.65, 0.22)  # x시작, y시작(0=맨위), 너비, 높이 비율
HEADER_TOP_PADDING = 120  # 캡처 시 cy 위쪽 120px 포함 (맨위 잘림 시 150~200으로)
# ★ 앵커 = 전체 화면에서 찾은 네모(템플릿). 구석 하나만 제대로 찾으면 기준으로 쓸 수 있음.
# 기준점: "center" = 네모 중심, "top_left" = 네모 왼쪽 위 꼭짓점 (top_left 쓰면 오프셋을 그 꼭짓점 기준으로 다시 잰 값으로 바꿔야 함)
ANCHOR_REFERENCE = "center"  # "center" | "top_left"
# ★ 중심점(앵커)으로부터의 차이값
# selectionpage: 문구클릭 x-70 y+205 / 다음 x-70 y+352
# mnemonicpage: 12개 입력창 = 1,2세트와 동일 기준점 + 아래 오프셋
BUTTON_OFFSET_FROM_ANCHOR: dict[str, tuple[int, int]] = {
    "mainpage_menu": (92, -157),    # 메뉴: 기준점 x+92, y-157
    "mainpage_add":  (-139, 386),   # add: x-139, y+386
    "mainpage_next": (-139, 355),   # 가져오기: x-139, y-355
    "selectionpage_phrase": (-70, 205),  # 2세트 첫번째: 문구 (1세트와 동일 기준점)
    "selectionpage_next": (-70, 352),    # 2세트 두번째: 다음 (1세트와 동일 기준점)
    # 니모닉 12칸 (기준점으로부터)
    "mnemonic_1": (-183, -22),   "mnemonic_2": (-76, -22),   "mnemonic_3": (36, -22),
    "mnemonic_4": (-183, 23),    "mnemonic_5": (-76, 23),    "mnemonic_6": (36, 23),
    "mnemonic_7": (-183, 68),    "mnemonic_8": (-76, 68),    "mnemonic_9": (36, 68),
    "mnemonic_10": (-183, 118),  "mnemonic_11": (-76, 118),  "mnemonic_12": (36, 118),
    "mnemonic_next": (67, 193),   # 12개 작성 후 다음 버튼 (기준점 x+67, y+193)
    "success_to_set1": (63, 182), # success.png 감지 후 클릭 → 세트 1로 복구
}

DEBUG = True
SYSTEM = platform.system()
STOP_FLAG = False

STATE_MATCH_THRESHOLD = 0.75
STATE_DETECT_THRESHOLD = 0.60
# success.png (민트 원 + 체크 아이콘): 너무 높으면 감지 못함, 너무 낮으면 오탐 (0.65~0.78 권장)
SUCCESS_DETECT_THRESHOLD = 0.70

DEBUG_CAPTURE_DIR = BASE_DIR / "debug"
_debug_action_counter = 0


def _save_anchor_visual(
    screen_img: np.ndarray,
    left: int, top: int, w: int, h: int,
    center_x: int, center_y: int,
    anchor_name: str,
) -> None:
    """
    앵커로 찾은 네모(사각)와 중심점을 화면 이미지에 그려서 저장.
    기준점이 네모 한가운데로 나오는지 확인용.
    """
    DEBUG_CAPTURE_DIR.mkdir(parents=True, exist_ok=True)
    global _debug_action_counter
    _debug_action_counter += 1
    vis = screen_img.copy()
    # 네모 테두리 (초록)
    cv2.rectangle(vis, (left, top), (left + w, top + h), (0, 255, 0), 2)
    # 중심점 (빨간 원 + 십자)
    r = 8
    cv2.circle(vis, (center_x, center_y), r, (0, 0, 255), 2)
    cv2.line(vis, (center_x - r - 2, center_y), (center_x + r + 2, center_y), (0, 0, 255), 1)
    cv2.line(vis, (center_x, center_y - r - 2), (center_x, center_y + r + 2), (0, 0, 255), 1)
    coord_text = f"center=({center_x},{center_y})"
    red_label = f"red=({center_x},{center_y})"
    font = cv2.FONT_HERSHEY_SIMPLEX
    (tw, th), _ = cv2.getTextSize(coord_text, font, 0.6, 2)
    (tw2, th2), _ = cv2.getTextSize(red_label, font, 0.6, 2)
    w_max = max(tw, tw2)
    h_total = th + th2 + 4
    cv2.rectangle(vis, (center_x - 2, center_y + r + 4), (center_x + w_max + 4, center_y + r + 4 + h_total), (0, 0, 0), -1)
    cv2.putText(vis, coord_text, (center_x, center_y + r + th + 4), font, 0.6, (0, 255, 255), 2)
    cv2.putText(vis, red_label, (center_x, center_y + r + th + 4 + th2 + 2), font, 0.6, (0, 0, 255), 2)
    path = DEBUG_CAPTURE_DIR / f"anchor_visual_{_debug_action_counter:03d}_{anchor_name}.png"
    cv2.imwrite(str(path), vis)
    if DEBUG:
        print(f"  [앵커 시각화] {path.name} (네모=초록, 기준점=빨간원) 빨간점좌표=({center_x},{center_y})")


def _pic_path(name: str) -> Path:
    return PICS_KR / name if name.endswith(".png") else PICS_KR / f"{name}.png"


def _click_px_to_pt(px_x: int | float, px_y: int | float) -> tuple[int, int]:
    """캡처 좌표(px) → 논리 좌표(pt): 캡처 해상도와 pyautogui max 비율로 곱해서 변환."""
    px_x, px_y = float(px_x), float(px_y)
    if DEBUG:
        pt_x, pt_y, formula = px_to_pt_with_formula(px_x, px_y)
        print(formula)
        return (pt_x, pt_y)
    return px_to_pt(px_x, px_y)


def _do_click(pt_x: int, pt_y: int, label: str = "") -> bool:
    """pt 좌표로 실제 클릭 실행. 실패 시 로그 후 False."""
    try:
        if DEBUG and label:
            print(f"  [클릭실행] {label} pt=({pt_x},{pt_y})")
        pyautogui.click(pt_x, pt_y)
        return True
    except Exception as e:
        if DEBUG:
            print(f"  ⚠ 클릭 실패 ({pt_x},{pt_y}): {e}")
        return False


# ─── 앵커 영역 찾기: mainpage 우선, 없으면 mainpage_menu로 대체 ───────────


def click_header_menu(delay: float = 0.3) -> bool:
    """지갑 이름 옆 헤더 햄버거만 찾아 클릭 (밑에 동그라미 더보기 제외).
    전체 크롬 캡처 후 상단만 잘라서 검색 -> 맨위 잘림 방지."""
    chrome = get_chrome_region()
    if chrome is None:
        return False
    cx, cy, cw, ch = chrome
    rx_ratio, ry_ratio, rw_ratio, rh_ratio = HEADER_MENU_REGION
    rx = int(cw * rx_ratio)
    ry = 0
    rw = int(cw * rw_ratio)
    rh = min(int(ch * rh_ratio), 180)
    if rw < 20 or rh < 20:
        return False
    top_pad = HEADER_TOP_PADDING
    capture_h = top_pad + rh
    # region은 크롬 기준 상대: (0, -top_pad, cw, capture_h) → 상단 패딩 포함
    chrome_img = capture_screen(region=(0, -top_pad, cw, capture_h))
    if chrome_img is None or chrome_img.size == 0:
        return False
    header_img = chrome_img[0:capture_h, rx:rx + rw]
    DEBUG_CAPTURE_DIR.mkdir(parents=True, exist_ok=True)
    cv2.imwrite(str(DEBUG_CAPTURE_DIR / "header_crop_for_menu.png"), header_img)
    if DEBUG:
        print("  [헤더캡처] debug/header_crop_for_menu.png 저장 -> 맨위 햄버거 보이는지 확인")
    path_menu = _pic_path("mainpage_menu_header") if _pic_path("mainpage_menu_header").exists() else _pic_path("mainpage_menu")
    if not path_menu.exists():
        return False
    result = find_template_multiscale(header_img, path_menu, threshold=STATE_DETECT_THRESHOLD)
    if result is None:
        # 임계값 낮춰서 한 번 더 시도
        result = find_template_multiscale(header_img, path_menu, threshold=0.50)
    if result is None:
        if DEBUG:
            print("  [헤더메뉴] 상단에서 미발견 (debug/header_crop_for_menu.png 확인, mainpage_menu_header.png 또는 mainpage_menu.png 템플릿)")
        return False
    (max_loc, confidence, scale) = result
    template = cv2.imread(str(path_menu))
    if template is None:
        return False
    th0, tw0 = template.shape[:2]
    w_icon = int(tw0 * scale)
    h_icon = int(th0 * scale)
    center_in_header_x = int(max_loc[0]) + w_icon // 2
    center_in_header_y = int(max_loc[1]) + h_icon // 2
    # 헤더(햄버거) 찾은 위치 앵커 시각화 (chrome_img 기준 좌표)
    _save_anchor_visual(
        chrome_img,
        rx + int(max_loc[0]), int(max_loc[1]),
        w_icon, h_icon,
        rx + center_in_header_x, center_in_header_y,
        "header_menu",
    )
    rel_x = rx + center_in_header_x
    abs_x = cx + rel_x
    abs_y = max(cy - top_pad, 0) + center_in_header_y
    screen_px = (int(abs_x), int(abs_y))
    click_x, click_y = _click_px_to_pt(screen_px[0], screen_px[1])
    if DEBUG:
        print(f"  [헤더메뉴] 픽셀=({screen_px[0]},{screen_px[1]}) -> pt=({click_x},{click_y})")
    focus_chrome()
    time.sleep(0.05)
    _do_click(click_x, click_y, "헤더메뉴")
    time.sleep(delay)
    return True

def get_anchor_region() -> tuple[int, int, int, int, int, int, str] | None:
    """
    ✅ 수정:
    - 크롬 영역만 캡처해서 매칭 좌표가 '크롬 내부 좌표'가 되게 통일
    - BASELINE_CHROME_SIZE를 최초 1회 저장(이 크기에서 오프셋이 정확했다고 가정)
    """
    global BASELINE_CHROME_SIZE

    chrome = get_chrome_region()
    if chrome is None:
        return None
    cx, cy, cw, ch = chrome

    # ✅ 최초 1회 기준 크롬 크기 저장 (이 때 오프셋이 "정확했다"는 전제)
    if BASELINE_CHROME_SIZE is None:
        BASELINE_CHROME_SIZE = (cw, ch)
        if DEBUG:
            print(f"  [기준 크롬 크기 저장] BASELINE_CHROME_SIZE={BASELINE_CHROME_SIZE}")

    # ✅ 크롬 전체 캡처 (위쪽 잘림 방지: 상단 패딩 포함)
    top_pad = HEADER_TOP_PADDING
    chrome_img = capture_screen(region=(0, -top_pad, cw, ch + top_pad))
    if chrome_img is None or chrome_img.size == 0:
        return None

    # 1) mainpage.png 먼저 시도
    path_main = _pic_path("mainpage")
    if path_main.exists():
        result = find_template_multiscale(chrome_img, path_main, threshold=STATE_DETECT_THRESHOLD)
        if result is not None:
            (max_loc, confidence, scale) = result
            template = cv2.imread(str(path_main))
            if template is not None:
                th0, tw0 = template.shape[:2]
                w, h = int(tw0 * scale), int(th0 * scale)
                left, top = int(max_loc[0]), int(max_loc[1])
                center_x, center_y_in_img = left + w // 2, top + h // 2
                # 캡처에 상단 패딩이 있으므로 크롬 내부 좌표로 보정
                center_y = center_y_in_img - top_pad
                top_chrome = top - top_pad
                if DEBUG:
                    print(f"  [앵커] mainpage (크롬내부) left,top=({left},{top_chrome}) center=({center_x},{center_y}) size={w}x{h} conf={confidence:.3f}")
                    _save_anchor_visual(chrome_img, left, top, w, h, center_x, center_y_in_img, "mainpage")
                return (center_x, center_y, w, h, left, top_chrome, "mainpage")

    # 2) mainpage_menu 로 앵커 대체 — 캡처를 논리 해상도로 해서 매칭 좌표 = 클릭 좌표
    path_menu = _pic_path("mainpage_menu_header") if _pic_path("mainpage_menu_header").exists() else _pic_path("mainpage_menu")
    if path_menu.exists():
        from chrome_capture import get_click_scale_xy
        rx_ratio, ry_ratio, rw_ratio, rh_ratio = HEADER_MENU_REGION
        rx = int(cw * rx_ratio)
        rw = int(cw * rw_ratio)
        rh = min(int(ch * rh_ratio), 180)
        top_pad = HEADER_TOP_PADDING
        capture_h = top_pad + rh
        header_chrome, origin_pt = capture_screen_logical(region=(0, -top_pad, cw, capture_h))
        if header_chrome is not None and header_chrome.size > 0:
            scale_x, scale_y = get_click_scale_xy()
            rx_pt = rx / scale_x
            rw_pt = rw / scale_x
            y_end = header_chrome.shape[0]
            x_start = int(rx_pt)
            x_end = min(int(rx_pt + rw_pt), header_chrome.shape[1])
            header_img = header_chrome[0:y_end, x_start:x_end]
            if header_img.size == 0:
                return None
            DEBUG_CAPTURE_DIR.mkdir(parents=True, exist_ok=True)
            cv2.imwrite(str(DEBUG_CAPTURE_DIR / "header_crop_for_menu.png"), header_img)
            result = find_template_multiscale(header_img, path_menu, threshold=STATE_DETECT_THRESHOLD)
            if result is not None:
                (max_loc, confidence, scale) = result
                template = cv2.imread(str(path_menu))
                if template is not None:
                    th0, tw0 = template.shape[:2]
                    w_icon, h_icon = int(tw0 * scale), int(th0 * scale)
                    center_in_header_x = int(max_loc[0]) + w_icon // 2
                    center_in_header_y = int(max_loc[1]) + h_icon // 2
                    # 논리 좌표로 저장 → 클릭 시 변환 없이 그대로 클릭
                    click_pt_x = origin_pt[0] + rx_pt + center_in_header_x
                    click_pt_y = origin_pt[1] + center_in_header_y
                    click_point = (round(click_pt_x), round(click_pt_y))
                    center_x = rx + int(center_in_header_x * scale_x)
                    center_y = int(center_in_header_y * scale_y)
                    left = center_x - w_icon // 2
                    top = center_y - h_icon // 2
                    if DEBUG:
                        print(f"  [앵커] mainpage_menu 논리 pt={click_point} (캡처=화면비율)")
                        _save_anchor_visual(header_chrome, x_start + int(max_loc[0]), int(max_loc[1]), w_icon, h_icon, x_start + center_in_header_x, center_in_header_y, "mainpage_menu")
                    return (center_x, center_y, w_icon, h_icon, left, top, "mainpage_menu", click_point)

    return None

def get_anchor_region_selectionpage() -> tuple[int, int, int, int, int, int, str] | None:
    chrome = get_chrome_region()
    if chrome is None:
        return None
    cx, cy, cw, ch = chrome

    chrome_img = capture_screen(region=(0, 0, cw, ch))  # 크롬 전체 (region은 크롬 기준 상대)
    if chrome_img is None or chrome_img.size == 0:
        return None

    path_sel = _pic_path("selectionpage")
    if not path_sel.exists():
        return None

    result = find_template_multiscale(chrome_img, path_sel, threshold=STATE_DETECT_THRESHOLD)
    if result is None:
        return None

    (max_loc, confidence, scale) = result
    template = cv2.imread(str(path_sel))
    if template is None:
        return None

    th0, tw0 = template.shape[:2]
    w, h = int(tw0 * scale), int(th0 * scale)
    left, top = int(max_loc[0]), int(max_loc[1])  # ✅ 크롬 내부
    center_x, center_y = left + w // 2, top + h // 2

    if DEBUG:
        print(f"  [앵커] selectionpage (크롬내부) center=({center_x},{center_y}) size={w}x{h} conf={confidence:.3f}")

    return (center_x, center_y, w, h, left, top, "selectionpage")

def click_button_by_offset(
    button_name: str,
    anchor: tuple[int, int, int, int, int, int, str],
    delay: float = 0.3
) -> bool:
    """
    ✅ 수정 핵심
    1) anchor 좌표는 '크롬 내부 좌표'로 통일됨 → 화면 픽셀로 만들 때만 cx,cy 더함
    2) 오프셋 dx,dy는 픽셀에서 더한 뒤, 마지막에 px_to_pt로 pt 변환 후 클릭
    3) 창 크기 변경 대응: BASELINE_CHROME_SIZE 대비 현재 크롬 크기 비율로 dx,dy를 스케일
    """
    global BASELINE_CHROME_SIZE

    center_x, center_y, w, h, left, top, anchor_name = anchor[0], anchor[1], anchor[2], anchor[3], anchor[4], anchor[5], anchor[6]
    stored_click = anchor[7] if len(anchor) >= 8 else None

    if button_name not in BUTTON_OFFSET_FROM_ANCHOR:
        if DEBUG:
            print(f"  ⚠ 오프셋 정의 없음: {button_name}")
        return False

    # 2번 햄버거 메뉴: 저장 pt 있으면 그대로 클릭, 없으면 이미지로 찾아서 클릭 먼저 시도
    if button_name == "mainpage_menu":
        if stored_click is not None:
            click_x, click_y = int(stored_click[0]), int(stored_click[1])
            if DEBUG:
                print(f"  [메뉴클릭] pt={stored_click} 그대로 클릭")
            focus_chrome()
            time.sleep(0.05)
            _do_click(click_x, click_y, "메뉴저장좌표")
            time.sleep(delay)
            return True
        # mainpage 앵커일 때: 햄버거 이미지 찾아서 클릭 시도 → 실패 시만 오프셋
        if click_header_menu(delay):
            return True
        if DEBUG:
            print("  [메뉴클릭] 헤더에서 미발견 → 오프셋으로 클릭 시도")
        # 아래 오프셋 경로로 진행

    chrome = get_chrome_region()
    if chrome is None:
        if DEBUG:
            print("  ⚠ 크롬 영역 없음 → 클릭 생략")
        return False
    cx, cy, cw, ch = chrome

    if BASELINE_CHROME_SIZE is None:
        BASELINE_CHROME_SIZE = (cw, ch)

    base_w, base_h = BASELINE_CHROME_SIZE
    scale_x = (cw / base_w) if base_w else 1.0
    scale_y = (ch / base_h) if base_h else 1.0

    dx, dy = BUTTON_OFFSET_FROM_ANCHOR[button_name]
    dx_s = int(round(dx * scale_x))
    dy_s = int(round(dy * scale_y))

    # 기준점(크롬 내부)
    if ANCHOR_REFERENCE == "top_left":
        ref_rel_x, ref_rel_y = left, top
    else:
        ref_rel_x, ref_rel_y = center_x, center_y

    # ✅ 크롬 내부 → 화면 픽셀
    ref_abs_x = cx + ref_rel_x
    ref_abs_y = cy + ref_rel_y

    # ✅ 오프셋도 픽셀에서 더하기 (DPI 스케일 적용 전)
    target_abs_x = ref_abs_x + dx_s
    target_abs_y = ref_abs_y + dy_s

    # 화면 절대 px → pt 변환 후 클릭
    click_x, click_y = _click_px_to_pt(target_abs_x, target_abs_y)

    if DEBUG:
        print(f"  [오프셋스케일] chrome {cw}x{ch} / base {base_w}x{base_h} → scale=({scale_x:.3f},{scale_y:.3f})")
        print(f"  [앵커→화면] chrome_origin({cx},{cy}) + ref_rel({ref_rel_x},{ref_rel_y}) = ref_abs({ref_abs_x},{ref_abs_y})")
        print(f"  [오프셋클릭] {button_name} : target_abs({target_abs_x},{target_abs_y}) -> pt=({click_x},{click_y})")

    _do_click(click_x, click_y, button_name)
    time.sleep(delay)
    return True

# ─── 핫키 / 공통 ───────────────────────────────────────────────────────

def setup_hotkey():
    global STOP_FLAG
    try:
        from pynput import keyboard as pynput_kb
        def on_press(key):
            global STOP_FLAG
            if key == pynput_kb.Key.esc:
                STOP_FLAG = True
                print("\n🛑 ESC 감지! 강제 종료")
                return False
        listener = pynput_kb.Listener(on_press=on_press)
        listener.daemon = True
        listener.start()
        print("⌨️  ESC = 강제 종료")
    except ImportError:
        print("⌨️  pip install pynput 권장")

def check_stop() -> bool:
    return STOP_FLAG

def type_text(text: str, delay: float = 0.02) -> None:
    if text:
        pyautogui.write(text, interval=delay)

def load_words() -> list[str]:
    with open(WORDLIST_FILE, "r", encoding="utf-8") as f:
        return [w.strip() for w in f if w.strip() and not w.startswith("#")]

def random_12(words: list[str]) -> list[str]:
    return random.sample(words, 12)


# ─── 상태 감지 (이전 페이지 기록 → 그 다음 세트를 먼저 시도) ─────────

# 이전 페이지 다음 세트: mainpage → selectionpage → mnemonicpage → mainpage
NEXT_SET: dict[str, str] = {
    "mainpage": "selectionpage",
    "selectionpage": "mnemonicpage",
    "mnemonicpage": "mainpage",
}
ALL_SETS = ["mainpage", "selectionpage", "mnemonicpage"]


def _detection_order(previous_page: str | None) -> list[str]:
    """
    이전 페이지가 없으면: mainpage, selectionpage, mnemonicpage 전부 시도.
    이전 페이지가 있으면: 그 다음 세트만 시도 (mainpage 재시도 안 함).
    """
    if previous_page is None:
        return list(ALL_SETS)
    next_only = NEXT_SET.get(previous_page)
    if not next_only:
        return list(ALL_SETS)
    return [next_only]  # 다음 세트만 시도


def _try_detect_set(set_name: str) -> tuple[bool, float] | None:
    """해당 세트가 화면에 있는지 시도. 있으면 (True, 신뢰도), 없으면 None."""
    if set_name == "mnemonicpage":
        r = find_image_on_chrome_improved("mnemonicpage", threshold=STATE_DETECT_THRESHOLD)
        return (True, r[1]) if r is not None else None
    if set_name == "selectionpage":
        for step in SELECTIONPAGE_DETECT:
            r = find_image_on_chrome_improved(step, threshold=STATE_DETECT_THRESHOLD)
            if r is not None:
                return (True, r[1])
        return None
    if set_name == "mainpage":
        for step in MAINPAGE_DETECT:
            r = find_image_on_chrome_improved(step, threshold=STATE_DETECT_THRESHOLD)
            if r is not None:
                return (True, r[1])
        return None
    return None


def detect_state(previous_page: str | None = None) -> str | None:
    """
    현재 화면 세트 감지: mainpage / selectionpage / mnemonicpage.
    previous_page가 있으면 그 다음 세트를 먼저 시도한다.
    """
    if check_stop():
        return None
    if get_chrome_region() is None:
        return None
    for set_name in _detection_order(previous_page):
        result = _try_detect_set(set_name)
        if result is not None:
            _, confidence = result
            if DEBUG:
                print(f"  [세트 감지] {set_name} (신뢰도: {confidence:.3f})")
            return set_name
    return None


def state_to_set_label(state: str | None) -> str:
    """세트 감지 결과(mainpage/selectionpage/mnemonicpage)를 읽기 쉬운 이름으로."""
    if state == "mainpage":
        return "mainpage"
    if state == "selectionpage":
        return "selectionpage"
    if state == "mnemonicpage":
        return "mnemonicpage"
    return "알 수 없음"


# ─── 메인페이지 플로우: 앵커 찾기 → 비율로 메뉴/add/바로위 → 이미지로 니모닉문구/confirm ─

def click_image_like_improved(name: str, threshold: float = 0.75, delay: float = 0.3) -> bool:
    """improved와 동일하게 이미지 찾아서(중심) 클릭."""
    r = find_image_on_chrome_improved(name, threshold=threshold)
    if r is None:
        return False
    (abs_x, abs_y), confidence, scale = r
    path = _pic_path(name)
    if path.exists():
        t = cv2.imread(str(path))
        if t is not None:
            th0, tw0 = t.shape[:2]
            abs_x += int(tw0 * scale) // 2
            abs_y += int(th0 * scale) // 2
    click_x, click_y = _click_px_to_pt(abs_x, abs_y)
    if DEBUG:
        print(f"  [이미지클릭] {name} → px=({abs_x},{abs_y}) pt=({click_x},{click_y}) 신뢰도={confidence:.3f}")
    _do_click(click_x, click_y, name)
    time.sleep(delay)
    return True


def run_mainpage_sequence_ratio() -> tuple[bool, tuple[int, int, int, int, int, int, str] | None]:
    """
    1세트: 1) 스크린샷 2) 메인페이지 확인 3·4) 헤더(햄버거) 클릭
          5) 새 스크린샷 6) 지갑추가하기 찾아 클릭 7) 지갑가져오기 찾아 클릭
    """
    clear_capture_size()  # 클릭 좌표는 화면 픽셀 기준 → 전체 화면 비율로 px→pt 변환
    anchor = get_anchor_region()  # 1. 스크린샷 + 2. 메인페이지인지 확인
    if anchor is None:
        if DEBUG:
            print("  [1세트] 메인페이지 아님 (앵커 없음)")
        return (False, None)

    for step_type, step_name in MAINPAGE_FLOW:
        if check_stop():
            return (False, anchor)
        if step_type == "ratio":
            click_button_by_offset(step_name, anchor, delay=0.4)
        else:
            click_image_like_improved(step_name, threshold=STATE_MATCH_THRESHOLD, delay=0.4)
        time.sleep(0.3)
    return (True, anchor)


def run_selectionpage_sequence_ratio(
    anchor: tuple[int, int, int, int, int, int, str] | None,
) -> bool:
    """
    2세트: 1세트에서 썼던 기준점(앵커)을 그대로 사용 → 문구, 다음 클릭.
    anchor가 None이면 실패 (1세트 기준점 없음).
    """
    clear_capture_size()  # 클릭 좌표는 화면 픽셀 기준
    if anchor is None:
        if DEBUG:
            print("  [2세트] 1세트 기준점 없음 (같은 기준점 유지하려면 1세트 먼저 실행)")
        return False
    for step_type, step_name in SELECTIONPAGE_FLOW:
        if check_stop():
            return False
        click_button_by_offset(step_name, anchor, delay=0.4)
        time.sleep(0.3)
    return True


# ─── 니모닉 / 에러 / 슬롯 (기존 이미지 방식 유지) ─────────────────────────

def find_and_click_image(name: str, threshold: float = 0.75, delay: float = 0.2) -> bool:
    """이미지 한 장 찾아서 클릭 (니모닉 슬롯·confirm 등)."""
    path = _pic_path(name)
    if not path.exists():
        return False
    chrome = get_chrome_region()
    if not chrome:
        return False
    rel = find_on_screen_normalized(path, threshold=threshold, region=None)
    if rel is None:
        return False
    cx, cy, _, _ = chrome
    px, py = cx + rel[0], cy + rel[1]
    click_x, click_y = _click_px_to_pt(px, py)
    _do_click(click_x, click_y, "find_and_click")
    time.sleep(delay)
    return True

def has_error() -> bool:
    """에러 탐지: error.png 등 (find_on_screen_normalized 사용)."""
    for p in (PICS_KR / "error.png", TEMPLATES_DIR / "mainpageerror.png"):
        if p.exists() and find_on_screen_normalized(p, threshold=0.5, region=None) is not None:
            return True
    return False


def has_success() -> bool:
    """success.png 감지. 크롬 멀티스케일 매칭 사용 (아이콘 감지에 유리, improved에서 success 시 디버그 저장 안 함)."""
    return find_image_on_chrome_improved("success", threshold=SUCCESS_DETECT_THRESHOLD) is not None

def fill_slots(
    words12: list[str],
    is_first: bool,
    anchor: tuple[int, int, int, int, int, int, str] | None,
) -> None:
    """
    니모닉 12칸 입력. 1,2세트와 동일한 기준점(anchor)으로부터 오프셋으로 클릭 후 입력.
    anchor가 None이면 이미지 감지 방식으로 폴백.
    """
    for i, w in enumerate(words12, start=1):
        if check_stop():
            return
        slot_name = f"mnemonic_{i}"
        if anchor is not None and slot_name in BUTTON_OFFSET_FROM_ANCHOR:
            click_button_by_offset(slot_name, anchor, delay=0.05)
        else:
            find_and_click_image(SLOT_IMAGE_NAMES[i - 1], threshold=STATE_MATCH_THRESHOLD, delay=0.05)
        if not is_first:
            pyautogui.hotkey("command" if SYSTEM == "Darwin" else "ctrl", "a")
            time.sleep(0.02)
        type_text(w)
        time.sleep(0.05)


# ─── main ───────────────────────────────────────────────────────────────

def main() -> None:
    pyautogui.FAILSAFE = True
    setup_hotkey()
    focus_chrome()
    time.sleep(0.5)

    if not get_chrome_region():
        print("⚠ 크롬 창을 찾을 수 없습니다.")
        return
    if not PICS_KR.exists():
        print("⚠ pic/kr 폴더가 없습니다.")
        return

    if TEST_MODE:
        print("🔧 테스트 모드: 1회차만 고정 니모닉(visa trick federal ...), 2회차부터 랜덤")

    words = load_words()
    attempt = 0
    previous_page: str | None = None  # 이전 페이지 기록 → 다음 세트를 먼저 시도
    saved_anchor: tuple[int, int, int, int, int, int, str] | None = None  # 1세트 기준점 → 2세트에서 그대로 사용

    while not check_stop():
        state = detect_state(previous_page)  # 전페이지 다음 세트를 먼저 시도
        if state == "mainpage":
            print("\n[세트: mainpage] 1세트 실행 (메뉴 → add → 가져오기)...")
            _, used_anchor = run_mainpage_sequence_ratio()
            if used_anchor is not None:
                saved_anchor = used_anchor  # 2세트에서 같은 기준점 쓰기 위해 저장
            time.sleep(1.0)
            previous_page = "mainpage"
            after = detect_state(previous_page)
            print(f"  → 1세트 완료. 현재 세트: {state_to_set_label(after)}")
            continue
        if state == "selectionpage":
            print("\n[세트: selectionpage] 2세트 실행 (문구 → 다음)...")
            run_selectionpage_sequence_ratio(saved_anchor)  # 1세트때 기준점 그대로 사용
            time.sleep(1.0)
            previous_page = "selectionpage"
            after = detect_state(previous_page)
            print(f"  → 2세트 완료. 현재 세트: {state_to_set_label(after)}")
            continue
        if state == "mnemonicpage":
            previous_page = "mnemonicpage"
            attempt += 1
            # 테스트 모드: 1회차만 고정 니모닉, 2회차부터는 랜덤
            mnemonic = FIXED_TEST_MNEMONIC if (TEST_MODE and attempt == 1) else random_12(words)
            if TEST_MODE and attempt == 1:
                print(f"\n[세트: mnemonicpage] 니모닉 입력 (테스트 모드 1회차 고정 니모닉, 시도 #{attempt})...")
            else:
                print(f"\n[세트: mnemonicpage] 니모닉 입력 (시도 #{attempt})...")
            # 1회차: 그냥 클릭 후 입력 / 2회차부터: 각 칸 오프셋클릭 → Ctrl+A → 기입
            is_first_fill = (attempt == 1)
            while True:
                fill_slots(mnemonic, is_first=is_first_fill, anchor=saved_anchor)
                if check_stop():
                    break
                # 12개 작성 후 다음 버튼 클릭
                if saved_anchor is not None and "mnemonic_next" in BUTTON_OFFSET_FROM_ANCHOR:
                    click_button_by_offset("mnemonic_next", saved_anchor, delay=0.2)
                else:
                    find_and_click_image("confirm", threshold=STATE_MATCH_THRESHOLD, delay=0.2)
                time.sleep(0.6)  # 다음 버튼 반응 대기
                if check_stop():
                    break
                # mnemonic_next 누른 뒤 에러 체크 (error.png)
                if has_error():
                    print("  ❌ 에러 감지(error.png) → 재시도 (1번 클릭 후 Ctrl+A, 타이핑)")
                    is_first_fill = False
                    continue
                # 성공 후 한번 더 error.png 체크
                time.sleep(0.3)
                if has_error():
                    print("  ❌ 에러 감지(error.png) → 재시도 (1번 클릭 후 Ctrl+A, 타이핑)")
                    is_first_fill = False
                    continue
                print(f"\n🎉 성공! (시도 #{attempt}회)")
                break
            # 다음 회차 전에 success.png 감지 → 기준점+오프셋 클릭으로 세트 1 복구
            if saved_anchor is not None and "success_to_set1" in BUTTON_OFFSET_FROM_ANCHOR:
                for _ in range(8):  # 최대 약 2.4초 대기 (0.3초 간격)
                    time.sleep(0.3)
                    if check_stop():
                        break
                    if has_success():
                        if DEBUG:
                            print("  [success.png 감지] 기준점+오프셋(63,182) 클릭 → 세트 1로 복구")
                        click_button_by_offset("success_to_set1", saved_anchor, delay=0.3)
                        time.sleep(0.5)
                        click_button_by_offset("mainpage_menu", saved_anchor, delay=0.3)  # success 루프 마지막: 메뉴 클릭
                        time.sleep(0.3)
                        previous_page = "mnemonicpage"  # 다음 루프에서 mainpage 감지 → 1세트
                        break
                else:
                    previous_page = "selectionpage"  # success 미감지 시 3세트로
            else:
                previous_page = "selectionpage"
            continue
        # 이전 페이지가 있었으면 초기화하지 않음 → 다음 세트만 계속 시도 (mainpage 재감지 방지)
        if previous_page is None:
            pass  # 처음부터 미감지
        else:
            # 다음 세트(mainpage) 미감지 시, mnemonicpage 다음이면 success.png 한번 더 감지 → 클릭으로 세트1 복구
            if previous_page == "mnemonicpage" and saved_anchor is not None and has_success():
                if DEBUG:
                    print("  [세트 감지] success.png 감지 → 기준점+오프셋 클릭으로 세트 1 복구")
                click_button_by_offset("success_to_set1", saved_anchor, delay=0.3)
                time.sleep(0.5)
                click_button_by_offset("mainpage_menu", saved_anchor, delay=0.3)
                time.sleep(0.3)
            elif DEBUG:
                print(f"  [세트 감지] 다음 세트({NEXT_SET.get(previous_page, '?')}) 미감지, 재시도...")
        time.sleep(1.0)

    if STOP_FLAG:
        print("\n🛑 ESC로 종료됨")


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\n🛑 Ctrl+C 종료")
