"""
test_mainpage_pyautogui.py
──────────────────────────
mainpage / mainpage_menu 를 pyautogui.locateOnScreen 으로만 인식 테스트.
창 크기(보통/전체화면) 바꿔 가며 좌표가 정확한지 확인용.

사용: python test_mainpage_pyautogui.py
      python test_mainpage_pyautogui.py --click   # 감지된 중심에 마우스 이동 + 클릭
"""
from __future__ import annotations

import argparse
import time
from pathlib import Path

import pyautogui

from chrome_capture import get_chrome_region, focus_chrome, scale_for_click

BASE_DIR = Path(__file__).parent
PICS_KR = BASE_DIR / "pic" / "kr"


def test_mainpage(confidence: float = 0.6, do_click: bool = False) -> None:
    focus_chrome()
    time.sleep(0.3)

    chrome = get_chrome_region()
    if chrome is None:
        print("⚠ 크롬 창 없음. 크롬을 켜고 다시 시도하세요.")
        return
    cx, cy, cw, ch = chrome
    print(f"  [크롬 영역] get_chrome_region() = (x={cx}, y={cy}, w={cw}, h={ch})")
    print()

    scale = scale_for_click()
    print(f"  [클릭 스케일] scale_for_click() = {scale} (Mac Retina 등)")
    print()

    for name in ("mainpage", "mainpage_menu"):
        path = PICS_KR / name if (PICS_KR / name).exists() else PICS_KR / f"{name}.png"
        if not path.exists():
            print(f"  [skip] {name}: 파일 없음 {path}")
            continue

        # 1) 크롬 영역 안에서만 찾기 (region=크롬)
        box = pyautogui.locateOnScreen(str(path), region=(cx, cy, cw, ch), confidence=confidence)
        if box is None:
            # 2) 크롬 영역 실패 시 전체 화면에서 재시도
            box = pyautogui.locateOnScreen(str(path), confidence=confidence)
            if box is None:
                print(f"  [실패] {name}: 미감지 (confidence={confidence})")
                continue
            print(f"  [참고] {name}: 크롬 영역에서는 미감지, 전체 화면에서는 감지됨")
            left, top, w, h = box
            center_screen_x = left + w // 2
            center_screen_y = top + h // 2
            rel_x = center_screen_x - cx
            rel_y = center_screen_y - cy
        else:
            left, top, w, h = box
            center_screen_x = left + w // 2
            center_screen_y = top + h // 2
            rel_x = center_screen_x - cx
            rel_y = center_screen_y - cy

        click_x = int(center_screen_x / scale)
        click_y = int(center_screen_y / scale)

        print(f"  [pyautogui] {name}")
        print(f"      화면(픽셀) box = (left={left}, top={top}, w={w}, h={h})")
        print(f"      화면(픽셀) 중심 = ({center_screen_x}, {center_screen_y})")
        print(f"      크롬 내부 상대   = ({rel_x}, {rel_y})  [기준: 크롬원점 ({cx},{cy})]")
        print(f"      클릭 좌표(scale 적용) = ({click_x}, {click_y})")
        print()

        if do_click:
            pyautogui.moveTo(click_x, click_y, duration=0.2)
            time.sleep(0.15)
            pyautogui.click()
            print(f"      → 클릭 실행 ({click_x}, {click_y})")
            time.sleep(0.5)

    print("  (다른 창 크기로 바꾼 뒤 다시 실행해 보세요.)")


if __name__ == "__main__":
    ap = argparse.ArgumentParser(description="mainpage 인식 pyautogui 테스트")
    ap.add_argument("--confidence", "-c", type=float, default=0.6, help="매칭 신뢰도 (기본 0.6)")
    ap.add_argument("--click", action="store_true", help="감지된 위치에 마우스 이동 후 클릭")
    args = ap.parse_args()

    print("─── mainpage 인식 테스트 (pyautogui만 사용) ───")
    test_mainpage(confidence=args.confidence, do_click=args.click)
