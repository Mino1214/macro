#!/usr/bin/env python3
"""
현재 마우스 좌표를 쉽게 확인하기 위한 도구.

사용법:
  python print_mouse_pos.py
  -> 마우스를 원하는 위치에 올려두고 Enter
"""
from __future__ import annotations

import sys
from pathlib import Path

try:
    import pyautogui
except ImportError:
    print("pyautogui 필요: pip install pyautogui")
    sys.exit(1)


def main() -> None:
    base = Path(__file__).parent
    print("마우스를 원하는 위치에 올려두고 Enter 를 누르세요.")
    print("(좌표를 여러 번 측정하고 싶으면 이 스크립트를 다시 실행하면 됩니다.)")
    input("> ")
    x, y = pyautogui.position()
    print(f"현재 마우스 좌표: x={x}, y={y}")
    print("이 값을 data/coords.json 에 넣어 사용하면 됩니다.")


if __name__ == "__main__":
    main()

