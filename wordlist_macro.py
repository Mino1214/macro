"""
coords.json + wordlist.txt 기반 순수 좌표 매크로

플로우(요약):
- (최초 1회) pre1 클릭 -> pre2 클릭
- 이후 루프:
  - wordlist.txt 에서 랜덤으로 12개 단어 선택
  - 처음 한 번은 slot1~slot12 에 '클릭 + 글자 입력'
  - 이후부터는 slot1~slot12 에 '더블클릭 + 글자 입력' (덮어쓰기)
  - confirm 클릭
  - mainpageerror.png 가 화면에 보이면 계속 루프
  - mainpageerror.png 가 안 보이면 성공으로 판단하고 종료 (또는 이후 로직 추가 가능)
"""
from __future__ import annotations

import random
import time
from pathlib import Path

import pyautogui

from image_matcher import find_on_screen

BASE_DIR = Path(__file__).parent
DATA_DIR = BASE_DIR / "data"
TEMPLATES_DIR = BASE_DIR / "macros" / "templates"

COORDS_FILE = DATA_DIR / "coords.json"
WORDLIST_FILE = BASE_DIR / "wordlist.txt"


def load_coords() -> dict:
    import json

    with open(COORDS_FILE, "r", encoding="utf-8") as f:
        raw = json.load(f)

    # 키 이름에 공백/개행 등이 섞여 있어도 안전하게 쓰기 위해 strip 적용
    coords: dict[str, dict] = {}
    for k, v in raw.items():
        if not isinstance(v, dict):
            continue
        key = str(k).strip()
        coords[key] = v
    return coords


COORDS = load_coords()


def pos(name: str) -> tuple[int, int]:
    c = COORDS[name]
    return int(c["x"]), int(c["y"])


def click(name: str, double: bool = False, delay: float = 0.1) -> None:
    x, y = pos(name)
    if double:
        pyautogui.doubleClick(x, y)
    else:
        pyautogui.click(x, y)
    time.sleep(delay)


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


def has_error(threshold: float = 0.8) -> bool:
    """mainpageerror.png 가 보이면 에러로 간주"""
    err_tpl = TEMPLATES_DIR / "mainpageerror.png"
    pos_err = find_on_screen(err_tpl, threshold=threshold)
    return pos_err is not None


def fill_slots(words12: list[str], first_time: bool) -> None:
    """슬롯 1~12에 단어 채우기. 최초 1회는 클릭, 이후는 더블클릭."""
    for i, w in enumerate(words12, start=1):
        name = f"slot{i}"
        click(name, double=not first_time, delay=0.05)
        type_text(w)
        time.sleep(0.05)


def main() -> None:
    pyautogui.FAILSAFE = True

    words = load_words()

    print("최초 1회: pre1, pre2 클릭")
    click("pre1")
    click("pre2")

    first_fill = True  # 이 루프에서 아직 한 번도 안 채운 상태

    while True:
        mnemonic = random_12(words)
        print("새 니모닉 12개:", " ".join(mnemonic))

        fill_slots(mnemonic, first_time=first_fill)
        first_fill = False  # 그 다음부터는 무조건 더블클릭 모드

        # 13. 확인/다음 클릭
        click("confirm", double=False, delay=0.3)

        # 에러 여부 확인
        time.sleep(1.0)  # 화면 업데이트 대기
        if has_error():
            print(" -> 에러 감지됨, 다음 랜덤 12조합으로 계속 시도")
            continue

        print("성공: 에러 없음으로 판단, 루프 종료")
        # TODO: 여기서 니모닉을 파일에 기록하거나, balance 체크를 추가할 수 있음.
        break


if __name__ == "__main__":
    main()

