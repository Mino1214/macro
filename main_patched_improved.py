"""
main_patched_improved.py
─────────────────────────
개선 사항:
1. (중요) 크롬 영역 좌표계(논리좌표/픽셀좌표) 불일치 수정 → 잘못된 crop/클릭 방지
2. 멀티스케일 매칭을 그레이스케일 기반으로 변경 → 인식률/안정성 개선
3. 검색영역 crop 클램프/빈영역 방지
4. 디버그: 크롬 crop / 검색영역 crop 저장 강화
"""
from __future__ import annotations

import random
import time
import platform
from pathlib import Path

import pyautogui
import cv2
import numpy as np

from image_matcher_v3 import capture_screen, find_on_screen_normalized, REFERENCE_SIZE
from chrome_capture import get_chrome_region, focus_chrome, px_to_pt, get_click_scale_xy

BASE_DIR = Path(__file__).parent
PICS_KR = BASE_DIR / "pic" / "kr"
TEMPLATES_DIR = BASE_DIR / "macros" / "templates"
WORDLIST_FILE = BASE_DIR / "wordlist.txt"

SLOT_IMAGE_NAMES = [str(i) for i in range(1, 13)]

# 검색 영역 (햄버거 메뉴는 좌상단에만)
SEARCH_REGION_BY_IMAGE: dict[str, tuple[float, float, float, float]] = {
    # 우측 상단: x=65%~100%, y=0%~25%
    #"mainpage_menu": (0.75, 0.0, 0.25, 0.20),
}

MAINPAGE_DETECT = [
    "mainpage",
    "mainpage_menu",
    "mainpage_add",
    "mainpage_getwallet",
    "next",
]
MAINPAGE_CLICK_ORDER = [
    "mainpage_menu",
    "mainpage_add",
    "mainpage_getwallet",
    "next",
]

DEBUG = True
SYSTEM = platform.system()
STOP_FLAG = False

DEBUG_CAPTURE_DIR = BASE_DIR / "debug"
_debug_action_counter = 0

# ★ 새로운 멀티스케일 매칭 설정
MULTISCALE_ENABLED = True  # False로 바꾸면 기존 방식
SCALE_RANGE = [0.6, 0.7, 0.8, 0.9, 1.0, 1.1, 1.2, 1.3, 1.4, 1.5]  # 템플릿을 이 배율들로 테스트

STATE_MATCH_THRESHOLD = 0.8

# 크롬 캡처 시 상단 여유(맨 위 메뉴 포함). chrome_crop_* 저장 시 더 위까지 찍힘
CHROME_TOP_PADDING = 120


def _setup_hotkey_pynput() -> bool:
    global STOP_FLAG
    try:
        from pynput import keyboard as pynput_kb

        def on_press(key):
            global STOP_FLAG
            if key == pynput_kb.Key.esc:
                STOP_FLAG = True
                print("\n🛑 ESC 감지! 강제 종료합니다...")
                return False

        listener = pynput_kb.Listener(on_press=on_press)
        listener.daemon = True
        listener.start()
        print("⌨️  핫키 등록: ESC = 강제 종료 (pynput)")
        return True
    except ImportError:
        return False


def setup_hotkey():
    global STOP_FLAG
    if SYSTEM == "Darwin":
        if _setup_hotkey_pynput():
            return
        try:
            import keyboard
            def on_esc():
                global STOP_FLAG
                STOP_FLAG = True
                print("\n🛑 ESC 감지! 강제 종료합니다...")
            keyboard.add_hotkey("esc", on_esc)
            print("⌨️  핫키 등록: ESC = 강제 종료 (keyboard)")
        except (ImportError, OSError):
            print("⌨️  핫키 등록 실패. Ctrl+C 로 종료하세요. (pip install pynput 권장)")
        return
    try:
        import keyboard
        def on_esc():
            global STOP_FLAG
            STOP_FLAG = True
            print("\n🛑 ESC 감지! 강제 종료합니다...")
        keyboard.add_hotkey("esc", on_esc)
        print("⌨️  핫키 등록: ESC = 강제 종료")
    except (ImportError, OSError):
        if not _setup_hotkey_pynput():
            print("⌨️  핫키 라이브러리 없음. Ctrl+C 로 종료하세요.")
            print("    (pip install pynput)")


def check_stop() -> bool:
    return STOP_FLAG


def type_text(text: str, delay: float = 0.02) -> None:
    if not text:
        return
    pyautogui.write(text, interval=delay)


def load_words() -> list[str]:
    if not WORDLIST_FILE.exists():
        raise FileNotFoundError(f"wordlist.txt 를 찾을 수 없습니다: {WORDLIST_FILE}")
    with open(WORDLIST_FILE, "r", encoding="utf-8") as f:
        return [w.strip() for w in f if w.strip() and not w.startswith("#")]


def random_12(words: list[str]) -> list[str]:
    if len(words) < 12:
        raise ValueError("wordlist.txt 에 12개 이상 단어가 필요합니다.")
    return random.sample(words, 12)


def _ensure_int(v: float) -> int:
    return int(round(v))


def _logical_to_pixel_rect(x: int, y: int, w: int, h: int) -> tuple[int, int, int, int]:
    """
    get_chrome_region()이 주는 값이 '클릭(논리) 좌표'인 경우가 많음.
    capture_screen() 이미지는 '픽셀' 기준인 경우가 많아서,
    crop은 반드시 픽셀 좌표로 변환해야 함.
    """
    sx, sy = get_click_scale_xy()
    s = (sx + sy) / 2.0 if (sx and sy) else 1.0
    # s가 1.0이면 그대로, 레티나면 보통 2.0 근처
    px = _ensure_int(x * s)
    py = _ensure_int(y * s)
    pw = _ensure_int(w * s)
    ph = _ensure_int(h * s)
    return px, py, pw, ph


def _clamp_rect(x: int, y: int, w: int, h: int, max_w: int, max_h: int) -> tuple[int, int, int, int]:
    x = max(0, min(x, max_w))
    y = max(0, min(y, max_h))
    w = max(0, min(w, max_w - x))
    h = max(0, min(h, max_h - y))
    return x, y, w, h


def save_debug_capture(label: str, coords: tuple[int, int] | None = None) -> None:
    if not DEBUG:
        return
    global _debug_action_counter
    _debug_action_counter += 1
    chrome = get_chrome_region()
    if not chrome:
        return
    DEBUG_CAPTURE_DIR.mkdir(parents=True, exist_ok=True)
    safe_label = label.replace(" ", "_").replace("/", "-")[:40]
    if coords is not None:
        name = f"{_debug_action_counter:03d}_{safe_label}_{coords[0]}_{coords[1]}.png"
    else:
        name = f"{_debug_action_counter:03d}_{safe_label}.png"
    out = DEBUG_CAPTURE_DIR / name
    try:
        img = capture_screen(region=None)
        cv2.imwrite(str(out), img)
        print(f"  [디버그캡처] {out.name}")
    except Exception as e:
        print(f"  [디버그캡처] 저장 실패: {e}")


def _save_debug_img(img: np.ndarray, label: str) -> None:
    if not DEBUG:
        return
    global _debug_action_counter
    _debug_action_counter += 1
    DEBUG_CAPTURE_DIR.mkdir(parents=True, exist_ok=True)
    safe_label = label.replace(" ", "_").replace("/", "-")[:40]
    out = DEBUG_CAPTURE_DIR / f"{_debug_action_counter:03d}_{safe_label}.png"
    try:
        cv2.imwrite(str(out), img)
        print(f"  [디버그캡처] {out.name}")
    except Exception as e:
        print(f"  [디버그캡처] 저장 실패: {e}")


def _pic_path(name: str) -> Path:
    p = PICS_KR / name if name.endswith(".png") else PICS_KR / f"{name}.png"
    return p


# ★★★ 핵심 개선: 멀티스케일 매칭 (그레이스케일) ★★★
def find_template_multiscale(
    screen_img: np.ndarray,
    template_path: Path,
    threshold: float = 0.7,
    scales: list[float] | None = None
) -> tuple[tuple[int, int], float, float] | None:
    """
    여러 스케일로 템플릿을 리사이즈하여 매칭 시도.
    Returns: ((x, y), confidence, best_scale) or None
    """
    if not template_path.exists():
        return None

    template_bgr = cv2.imread(str(template_path), cv2.IMREAD_COLOR)
    if template_bgr is None:
        return None

    if scales is None:
        scales = SCALE_RANGE

    # (중요) 매칭은 그레이스케일로 (UI 색/밝기 변화에 강함)
    screen_gray = cv2.cvtColor(screen_img, cv2.COLOR_BGR2GRAY)
    template_gray = cv2.cvtColor(template_bgr, cv2.COLOR_BGR2GRAY)

    best_match = None
    best_confidence = 0.0
    best_scale = 1.0

    sh, sw = screen_gray.shape[:2]
    th0, tw0 = template_gray.shape[:2]

    for scale in scales:
        if scale <= 0:
            continue

        new_w = int(tw0 * scale)
        new_h = int(th0 * scale)

        # 너무 작거나 화면보다 큰 템플릿은 제외
        if new_w < 8 or new_h < 8 or new_w > sw or new_h > sh:
            continue

        resized_t = cv2.resize(template_gray, (new_w, new_h), interpolation=cv2.INTER_AREA)

        result = cv2.matchTemplate(screen_gray, resized_t, cv2.TM_CCOEFF_NORMED)
        _, max_val, _, max_loc = cv2.minMaxLoc(result)

        if max_val > best_confidence:
            best_confidence = max_val
            best_match = max_loc
            best_scale = scale

    if best_match is None or best_confidence < threshold:
        return None

    return (best_match, best_confidence, best_scale)


def find_image_on_chrome_improved(
    name: str,
    threshold: float = 0.7
) -> tuple[tuple[int, int], float, float] | None:
    """
    개선된 이미지 찾기.
    Returns: ((absolute_pixel_x, absolute_pixel_y), confidence, scale) or None

    ⚠️ 중요:
    - crop/매칭은 '픽셀 좌표' 기준
    - 클릭은 pyautogui 좌표(논리 좌표) 기준이므로,
      최종 클릭 직전에만 px_to_pt()로 px→pt 변환
    """
    path = _pic_path(name)
    if not path.exists():
        if DEBUG:
            print(f"  ⚠ 이미지 없음: {path}")
        return None

    chrome = get_chrome_region()
    if not chrome:
        return None

    # chrome은 논리좌표일 가능성이 높음 → 픽셀로 변환해서 crop
    lx, ly, lw, lh = chrome
    cx, cy, cw, ch = _logical_to_pixel_rect(lx, ly, lw, lh)

    # 크롬 캡처 (상단 패딩 포함 → 맨 위 메뉴까지 들어가게)
    pad = CHROME_TOP_PADDING
    full_screen = capture_screen(region=(0, -pad, cw, ch + pad))
    H, W = full_screen.shape[:2]

    if cw <= 10 or ch <= 10:
        if DEBUG:
            print(f"  ⚠ 크롬 crop이 너무 작음: {cw}x{ch} (픽셀)")
        return None

    chrome_img = full_screen  # 높이 (ch+pad), 너비 cw

    if DEBUG and name != "success":
        # 크롬 crop 확인용 저장 (success는 폴링 시 매번 저장 방지)
        _save_debug_img(chrome_img, f"chrome_crop_{name}")

    # 검색 영역 제한 (픽셀 기준). 패딩 있으면 콘텐츠는 row [pad:] 부터
    region_offset_x, region_offset_y = 0, 0
    search_img = chrome_img

    if name in SEARCH_REGION_BY_IMAGE:
        rx_ratio, ry_ratio, rw_ratio, rh_ratio = SEARCH_REGION_BY_IMAGE[name]
        region_offset_x = int(cw * rx_ratio)
        region_offset_y = int(ch * ry_ratio)  # 콘텐츠 기준
        region_w = int(cw * rw_ratio)
        region_h = int(ch * rh_ratio)

        # 클램프 + 빈영역 방지
        region_offset_x = max(0, min(region_offset_x, cw - 1))
        region_offset_y = max(0, min(region_offset_y, ch - 1))
        region_w = max(1, min(region_w, cw - region_offset_x))
        region_h = max(1, min(region_h, ch - region_offset_y))

        # 캡처 상단 패딩 때문에 콘텐츠는 row pad 부터
        search_img = chrome_img[
            pad + region_offset_y : pad + region_offset_y + region_h,
            region_offset_x : region_offset_x + region_w
        ]

        if DEBUG:
            print(f"  [검색영역] {name}: {region_w}x{region_h} (크롬 {cw}x{ch} 픽셀 기준)")
            _save_debug_img(search_img, f"search_crop_{name}")

    # 멀티스케일 매칭
    if MULTISCALE_ENABLED:
        result = find_template_multiscale(search_img, path, threshold)
        if result is None:
            return None
        (rel_x, rel_y), confidence, scale = result
    else:
        template = cv2.imread(str(path), cv2.IMREAD_COLOR)
        if template is None:
            return None
        # 단일 스케일도 그레이로 매칭
        search_gray = cv2.cvtColor(search_img, cv2.COLOR_BGR2GRAY)
        template_gray = cv2.cvtColor(template, cv2.COLOR_BGR2GRAY)
        match_result = cv2.matchTemplate(search_gray, template_gray, cv2.TM_CCOEFF_NORMED)
        _, max_val, _, max_loc = cv2.minMaxLoc(match_result)
        if max_val < threshold:
            return None
        rel_x, rel_y = max_loc
        confidence = max_val
        scale = 1.0

    # 절대 픽셀 좌표: 캡처 이미지 상단이 cy-pad 이므로 y = cy - pad + (이미지 내 y)
    if name in SEARCH_REGION_BY_IMAGE:
        y_in_chrome_img = pad + region_offset_y + rel_y
    else:
        y_in_chrome_img = rel_y
    abs_x = cx + region_offset_x + rel_x
    abs_y = cy - CHROME_TOP_PADDING + y_in_chrome_img

    return ((abs_x, abs_y), confidence, scale)


def _pixel_to_click_point(px: int, py: int) -> tuple[int, int]:
    # 캡처 좌표(px) → 논리 좌표(pt) 변환 (Retina/DPI 자동)
    return px_to_pt(float(px), float(py))


def click_image_improved(
    name: str,
    threshold: float | None = None,
    delay: float = 0.2,
    offset_center: bool = True
) -> bool:
    """
    개선된 클릭: 템플릿 중심 오프셋 자동 적용
    """
    if threshold is None:
        threshold = STATE_MATCH_THRESHOLD

    result = find_image_on_chrome_improved(name, threshold=threshold)
    if result is None:
        return False

    (abs_x, abs_y), confidence, scale = result

    # 템플릿 중심으로 클릭 위치 조정 (픽셀 기준)
    if offset_center:
        path = _pic_path(name)
        template = cv2.imread(str(path), cv2.IMREAD_COLOR)
        if template is not None:
            template_h, template_w = template.shape[:2]
            scaled_w = int(template_w * scale)
            scaled_h = int(template_h * scale)
            abs_x += scaled_w // 2
            abs_y += scaled_h // 2

    click_x, click_y = _pixel_to_click_point(abs_x, abs_y)

    if DEBUG:
        print(f"  [클릭] {name} → 픽셀 ({abs_x}, {abs_y}) → 클릭 ({click_x}, {click_y})")
        print(f"        신뢰도: {confidence:.3f}, 스케일: {scale:.2f}x")
        save_debug_capture(f"before_click_{name}", coords=(abs_x, abs_y))

    pyautogui.click(click_x, click_y)
    time.sleep(delay)

    if DEBUG:
        save_debug_capture(f"after_click_{name}")

    return True


click_image = click_image_improved


def _save_debug_capture_once():
    if not DEBUG:
        return
    chrome = get_chrome_region()
    if not chrome:
        print("  디버그: 크롬 영역을 못 찾아 캡처 생략")
        return
    out = BASE_DIR / "debug_capture.png"
    try:
        img = capture_screen(region=None)
        cv2.imwrite(str(out), img)
        lx, ly, lw, lh = chrome
        px, py, pw, ph = _logical_to_pixel_rect(lx, ly, lw, lh)
        print(f"  디버그: 캡처 저장 → {out.name}")
        print(f"        chrome logical x={lx} y={ly} {lw}x{lh}")
        print(f"        chrome pixel   x={px} y={py} {pw}x{ph}")
    except Exception as e:
        print(f"  디버그 캡처 저장 실패: {e}")


def detect_state() -> str | None:
    if check_stop():
        return None
    if DEBUG:
        save_debug_capture("detect_state")

    chrome = get_chrome_region()
    if not chrome:
        return None

    # 니모닉 페이지 먼저 확인
    result = find_image_on_chrome_improved("mnemonicpage", threshold=STATE_MATCH_THRESHOLD)
    
    if result is not None:
        return "mnemonicpage"

    # 메인페이지 감지
    for step in MAINPAGE_DETECT:
        result = find_image_on_chrome_improved(step, threshold=STATE_MATCH_THRESHOLD)
        if result is not None:
            if DEBUG:
                print(f"  [상태감지] {step} 발견 (신뢰도: {result[1]:.3f})")
            return "mainpage"

    return None


def run_mainpage_sequence(max_rounds: int = 20) -> bool:
    if DEBUG:
        save_debug_capture("mainpage_sequence_start")

    for round_num in range(max_rounds):
        if check_stop():
            return False
        if DEBUG:
            save_debug_capture(f"mainpage_round_{round_num+1}")

        clicked_any = False
        for step in MAINPAGE_CLICK_ORDER:
            if check_stop():
                return False
            if click_image(step, threshold=STATE_MATCH_THRESHOLD, delay=0.4):
                if DEBUG:
                    print(f"  [메인페이지] 클릭: {step}")
                clicked_any = True
                break

        if not clicked_any:
            if DEBUG:
                print("  [메인페이지] 더 이상 클릭할 항목 없음 → 니모닉 페이지로 간주")
            return True

        time.sleep(0.6)

    if DEBUG:
        print("  [메인페이지] max_rounds 도달")
    return True


def has_error() -> bool:
    for name in ["error", "mainpageerror"]:
        result = find_image_on_chrome_improved(name, threshold=0.5)
        if result is not None:
            if DEBUG:
                print(f"  🔴 에러 이미지 감지: {name} (신뢰도: {result[1]:.3f})")
            return True
    return False


def fill_slots(words12: list[str], is_first: bool) -> None:
    for i, w in enumerate(words12, start=1):
        if check_stop():
            return
        slot_name = SLOT_IMAGE_NAMES[i - 1]
        if not click_image(slot_name, threshold=STATE_MATCH_THRESHOLD, delay=0.05):
            if DEBUG:
                print(f"  ⚠ 슬롯 {i} 이미지 못 찾음, 입력만 시도")
        if not is_first:
            pyautogui.hotkey("command" if SYSTEM == "Darwin" else "ctrl", "a")
            time.sleep(0.02)
        type_text(w)
        time.sleep(0.05)


def main() -> None:
    pyautogui.FAILSAFE = True
    setup_hotkey()

    print("\n" + "="*60)
    print("  이미지 기반 자동화 (개선 버전)")
    print("  - 멀티스케일 매칭: " + ("활성화" if MULTISCALE_ENABLED else "비활성화"))
    print("  - 스케일 범위:", SCALE_RANGE if MULTISCALE_ENABLED else "1.0 고정")
    print("  - px→pt 스케일 (캡처/논리):", get_click_scale_xy())
    print("="*60 + "\n")

    print("\n크롬 창 포커스...")
    focus_chrome()
    time.sleep(0.5)

    chrome = get_chrome_region()
    if not chrome:
        print("⚠ 크롬 창을 찾을 수 없습니다! 크롬을 열어주세요.")
        return

    lx, ly, lw, lh = chrome
    px, py, pw, ph = _logical_to_pixel_rect(lx, ly, lw, lh)
    print(f"✅ 크롬 감지 (logical): x={lx}, y={ly}, {lw}x{lh}")
    print(f"✅ 크롬 감지 (pixel)  : x={px}, y={py}, {pw}x{ph}")

    if not PICS_KR.exists():
        print(f"⚠ pic/kr 폴더가 없습니다: {PICS_KR}")
        return

    words = load_words()
    attempt = 0
    _debug_capture_saved = False

    while not check_stop():
        state = detect_state()

        if state == "mainpage":
            print("\n[상태: 메인페이지] 메인페이지 플로우 실행...")
            if DEBUG:
                save_debug_capture("state_mainpage")
            run_mainpage_sequence()
            time.sleep(1.0)
            if DEBUG:
                save_debug_capture("mainpage_sequence_done")
            continue

        if state == "mnemonicpage":
            if DEBUG:
                save_debug_capture("state_mnemonicpage")
            attempt += 1
            mnemonic = random_12(words)
            print(f"\n[시도 #{attempt}] 니모닉: {' '.join(mnemonic)}")
            is_first = attempt == 1
            if DEBUG:
                save_debug_capture("before_fill_slots")
            fill_slots(mnemonic, is_first=is_first)
            if check_stop():
                break
            if DEBUG:
                save_debug_capture("before_confirm_click")
            if not click_image("confirm", threshold=STATE_MATCH_THRESHOLD, delay=0.3):
                if DEBUG:
                    print("  ⚠ confirm 이미지 못 찾음")
            time.sleep(1.0)
            if check_stop():
                break
            if DEBUG:
                save_debug_capture("after_confirm_before_error_check")
            if has_error():
                if DEBUG:
                    save_debug_capture("has_error_retry")
                print(f"  ❌ 에러 감지 → 다음 조합 시도")
                continue
            print(f"\n🎉 성공! (시도 #{attempt}회)")
            print(f"   니모닉: {' '.join(mnemonic)}")
            break

        if DEBUG:
            save_debug_capture("state_unknown_retry")
            if not _debug_capture_saved:
                _save_debug_capture_once()
                _debug_capture_saved = True
                print("  상태 감지 안 됨. debug 폴더의 chrome_crop_*/search_crop_* 확인하세요.")
            print("  1초 후 재시도...")
        time.sleep(1.0)

    if STOP_FLAG:
        print(f"\n🛑 ESC로 강제 종료됨 (총 {attempt}회 시도)")


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\n🛑 Ctrl+C 로 종료됨")
    except pyautogui.FailSafeException:
        print("\n🛑 마우스 좌상단 이동으로 안전 종료됨")