"""
main_patched.py
───────────────
상태 기반 UI 매크로. 크롬 창 캡처(pic/kr 이미지)로 화면 상태를 판별 후 동작.

★ 핫키: ESC 누르면 즉시 강제 종료
★ 메인페이지 감지 후 클릭 순서: mainpage_menu → mainpage_add → mainpage_getwallet → next (mainpage는 감지만)
★ next 클릭 후 mnemonicpage 진입·감지 → 12단어 입력 → confirm → 에러 시 재시도

필요 파일 (coords.json 사용 안 함, 전부 이미지):
  프로젝트/
  ├── main_patched.py
  ├── chrome_capture.py
  ├── image_matcher_v3.py  (REFERENCE_SIZE = 1440x900, 해상도 다르면 여기서 수정)
  ├── wordlist.txt
  ├── pic/kr/
  │   ├── mainpage.png, mainpage_menu.png, ... next.png, mnemonicpage.png
  │   ├── 1.png ~ 12.png, confirm.png, error.png(선택)
  │   └── ★ 템플릿은 기준 해상도(기본 1440x900)에서 캡처해 두면 실제 화면 해상도가 달라도 좌표 보정됨
  └── macros/templates/mainpageerror.png  (선택)
"""
from __future__ import annotations

import random
import time
import platform
from pathlib import Path

import pyautogui

import cv2
from image_matcher_v3 import capture_screen, find_on_screen_normalized, REFERENCE_SIZE
from chrome_capture import get_chrome_region, focus_chrome, scale_for_click

BASE_DIR = Path(__file__).parent
PICS_KR = BASE_DIR / "pic" / "kr"
TEMPLATES_DIR = BASE_DIR / "macros" / "templates"

WORDLIST_FILE = BASE_DIR / "wordlist.txt"

# 니모닉 슬롯 이미지: pic/kr/1.png ~ 12.png
SLOT_IMAGE_NAMES = [str(i) for i in range(1, 13)]

# 특정 이미지는 화면 일부에서만 검색 (비슷한 UI가 다른 곳에 있으면 잘못 매칭 방지)
# (x비율, y비율, 너비비율, 높이비율) 0~1. 햄버거 메뉴는 보통 좌상단 → 주소창 클릭 방지
SEARCH_REGION_BY_IMAGE: dict[str, tuple[float, float, float, float]] = {
    "mainpage_menu": (0, 0, 0.4, 0.25),   # 좌측 40%, 상단 25% 안에서만 검색
}

# 메인페이지 감지: 이 중 하나라도 보이면 "메인페이지" 상태
MAINPAGE_DETECT = [
    "mainpage",
    "mainpage_menu",
    "mainpage_add",
    "mainpage_getwallet",
    "next",
]
# 메인페이지에서 클릭할 순서 (mainpage는 감지만 하고 클릭 안 함)
MAINPAGE_CLICK_ORDER = [
    "mainpage_menu",
    "mainpage_add",
    "mainpage_getwallet",
    "next",
]

DEBUG = True
SYSTEM = platform.system()

STOP_FLAG = False

# 디버그 캡처 저장용 (행동마다 순번 붙여서 저장)
DEBUG_CAPTURE_DIR = BASE_DIR / "debug"
_debug_action_counter = 0


def _setup_hotkey_pynput() -> bool:
    """pynput으로 ESC 핫키 등록 (Mac에서 sudo 불필요)."""
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
    """ESC 키로 강제 종료. Mac에서는 pynput 우선 (sudo 불필요)."""
    global STOP_FLAG
    # Mac: keyboard는 관리자 권한 필요 → pynput 먼저 사용
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
    # Windows 등: keyboard 시도 후 실패하면 pynput
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
    if STOP_FLAG:
        return True
    return False


# ─── 입력 (전부 이미지 클릭, coords.json 미사용) ─────────────────

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


# ─── 디버그: 행동마다 캡처 저장 ───────────────────────────────

def save_debug_capture(label: str, coords: tuple[int, int] | None = None) -> None:
    """DEBUG일 때 현재 크롬 화면을 debug/ 폴더에 저장. 클릭 좌표 있으면 파일명에 포함."""
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


# ─── 크롬 캡처 기반: 이미지 찾기 & 클릭 (pic/kr) ───────────────

def _pic_path(name: str) -> Path:
    """이미지 이름 → pic/kr/{name}.png (확장자 없으면 .png 붙임)"""
    p = PICS_KR / name if name.endswith(".png") else PICS_KR / f"{name}.png"
    return p


def find_image_on_chrome(name: str, threshold: float = 0.7) -> tuple[int, int] | None:
    """
    크롬 창 캡처에서 pic/kr/{name}.png 를 찾아 절대 좌표 (픽셀) 반환.
    SEARCH_REGION_BY_IMAGE에 있으면 해당 영역(비율) 안에서만 검색 → 주소창 등 다른 요소와 혼동 방지.
    """
    path = _pic_path(name)
    if not path.exists():
        if DEBUG:
            print(f"  ⚠ 이미지 없음: {path}")
        return None
    chrome = get_chrome_region()
    if not chrome:
        return None
    cx, cy, cw, ch = chrome
    region: tuple[int, int, int, int] | None = None
    region_offset_x, region_offset_y = 0, 0
    if name in SEARCH_REGION_BY_IMAGE:
        rx_ratio, ry_ratio, rw_ratio, rh_ratio = SEARCH_REGION_BY_IMAGE[name]
        region_offset_x = int(cw * rx_ratio)
        region_offset_y = int(ch * ry_ratio)
        region = (
            region_offset_x,
            region_offset_y,
            int(cw * rw_ratio),
            int(ch * rh_ratio),
        )
        if DEBUG:
            print(f"  [검색영역] {name}: 좌측상단 {rx_ratio:.0%}x{ry_ratio:.0%}, 크기 {rw_ratio:.0%}x{rh_ratio:.0%}")
    rel = find_on_screen_normalized(path, threshold=threshold, region=region)
    if rel is None:
        return None
    # region 썼을 때 rel은 해당 영역 내 좌표 → 전체 크롬 기준으로 보정
    return (cx + region_offset_x + rel[0], cy + region_offset_y + rel[1])


def _pixel_to_click_point(px: int, py: int) -> tuple[int, int]:
    """캡처/이미지 좌표(픽셀) → pyautogui.click()에 넣을 좌표(포인트). Mac Retina 보정."""
    scale = scale_for_click()
    return (int(px / scale), int(py / scale))


def click_image(name: str, threshold: float | None = None, delay: float = 0.2) -> bool:
    """pic/kr 이미지를 찾아 클릭. 성공 시 True. Mac에서 픽셀→포인트 변환 적용."""
    if threshold is None:
        threshold = STATE_MATCH_THRESHOLD
    abs_pos = find_image_on_chrome(name, threshold=threshold)
    if abs_pos is None:
        return False
    px, py = abs_pos
    click_x, click_y = _pixel_to_click_point(px, py)
    if DEBUG:
        print(f"  [클릭] {name} → 픽셀 ({px}, {py}) → 클릭 좌표 ({click_x}, {click_y})")
        save_debug_capture(f"before_click_{name}", coords=(px, py))
    pyautogui.click(click_x, click_y)
    time.sleep(delay)
    if DEBUG:
        save_debug_capture(f"after_click_{name}")
    return True


# 상태 감지용 매칭 임계값 (낮을수록 느슨하게 매칭)
STATE_MATCH_THRESHOLD = 0.5


def _save_debug_capture_once():
    """디버그: 크롬 캡처를 한 번만 저장 (상태 감지 실패 시 확인용)."""
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
        cx, cy, cw, ch = chrome
        print(f"  디버그: 캡처 저장 → {out.name} (영역 x={cx} y={cy} {cw}x{ch}). 이게 크롬이 아니면 창을 포커스한 뒤 재실행.")
    except Exception as e:
        print(f"  디버그 캡처 저장 실패: {e}")


def detect_state() -> str | None:
    """
    현재 크롬 화면 상태 판별. (크롬 창만 캡처해서 pic/kr 이미지와 매칭)
    "mainpage" | "mnemonicpage" | None
    """
    if check_stop():
        return None
    if DEBUG:
        save_debug_capture("detect_state")
    chrome = get_chrome_region()
    if not chrome:
        return None
    # 니모닉 페이지 먼저 확인 (해상도 정규화 매칭)
    if _pic_path("mnemonicpage").exists():
        if find_on_screen_normalized(_pic_path("mnemonicpage"), threshold=STATE_MATCH_THRESHOLD, region=None) is not None:
            return "mnemonicpage"
    # 메인페이지 감지: MAINPAGE_DETECT 중 하나라도 보이면 메인페이지
    for step in MAINPAGE_DETECT:
        if _pic_path(step).exists() and find_on_screen_normalized(_pic_path(step), threshold=STATE_MATCH_THRESHOLD, region=None) is not None:
            return "mainpage"
    return None


def run_mainpage_sequence(max_rounds: int = 20) -> bool:
    """
    메인페이지인 동안 MAINPAGE_CLICK_ORDER 순서대로 클릭:
    mainpage_menu → mainpage_add → mainpage_getwallet → next
    (mainpage는 감지만 하고 클릭하지 않음)
    해당 이미지가 하나도 안 보일 때까지 반복 후 True → mnemonicpage 진입.
    """
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


# ─── 에러 감지 (이미지: pic/kr/error.png 또는 mainpageerror.png) ─────

def has_error() -> bool:
    """크롬 화면에서 에러 템플릿이 보이면 True (해상도 정규화 매칭)."""
    for path in (PICS_KR / "error.png", TEMPLATES_DIR / "mainpageerror.png"):
        if path.exists() and find_on_screen_normalized(path, threshold=0.5, region=None) is not None:
            if DEBUG:
                print("  🔴 에러 이미지 감지")
            return True
    return False


# ─── 니모닉 슬롯 채우기 (이미지: pic/kr/1.png ~ 12.png) ───────────────

def fill_slots(words12: list[str], is_first: bool) -> None:
    for i, w in enumerate(words12, start=1):
        if check_stop():
            return
        slot_name = SLOT_IMAGE_NAMES[i - 1]  # "1" ~ "12"
        if not click_image(slot_name, threshold=STATE_MATCH_THRESHOLD, delay=0.05):
            if DEBUG:
                print(f"  ⚠ 슬롯 {i} 이미지 못 찾음, 입력만 시도")
        if not is_first:
            pyautogui.hotkey("command" if SYSTEM == "Darwin" else "ctrl", "a")
            time.sleep(0.02)
        type_text(w)
        time.sleep(0.05)


# ─── 메인 루프 ─────────────────────────────────────────────────

def main() -> None:
    pyautogui.FAILSAFE = True
    setup_hotkey()

    print("\n크롬 창 포커스...")
    focus_chrome()
    time.sleep(0.5)

    chrome = get_chrome_region()
    if not chrome:
        print("⚠ 크롬 창을 찾을 수 없습니다! 크롬을 열어주세요.")
        return
    print(f"✅ 크롬 감지: x={chrome[0]}, y={chrome[1]}, {chrome[2]}x{chrome[3]}")
    if DEBUG:
        print(f"  좌표 계산: 사진 비율 × 실제 화면 크기 (기준 {REFERENCE_SIZE[0]}x{REFERENCE_SIZE[1]})")
    if DEBUG and SYSTEM == "Darwin":
        print(f"  (Mac 클릭 보정: 픽셀→포인트 배율 {scale_for_click()})")
    if SYSTEM == "Darwin":
        print("💡 캡처가 배경화면만 나오면: [시스템 설정] → [개인 정보 보호 및 보안] → [화면 기록]")
        print("   → 이 스크립트를 실행하는 앱(터미널/Cursor)을 켜고, 재실행하세요.")

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

        # 상태 불명: 크롬 캡처는 맞는데 템플릿 매칭 실패 (임계값 0.5)
        if DEBUG:
            save_debug_capture("state_unknown_retry")
            if not _debug_capture_saved:
                _save_debug_capture_once()
                _debug_capture_saved = True
                print("  상태 감지 안 됨 (크롬 캡처 기준). debug_capture.png 확인 후 pic/kr 템플릿/해상도 맞는지 보세요.")
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
