#!/usr/bin/env python3
"""
화면 영역을 지정해 템플릿 이미지로 저장하는 도구.
사용법: python capture_region.py
- 마우스로 드래그해 영역 선택 후 저장 (또는 좌표 입력)
"""
from pathlib import Path
import sys

try:
    import pyautogui
except ImportError:
    print("pyautogui 필요: pip install pyautogui")
    sys.exit(1)

try:
    from PIL import ImageGrab
except ImportError:
    try:
        import pyscreenshot as ImageGrab
    except ImportError:
        print("PIL 또는 pyscreenshot 필요")
        sys.exit(1)


def capture_region_interactive(save_dir: Path) -> None:
    """간단한 영역 캡처: 터미널에 좌표 입력받아 해당 영역 저장"""
    print("캡처할 영역의 좌표를 입력하세요 (x y width height)")
    print("예: 100 200 300 150")
    print("또는 Enter만 누르면 현재 마우스 위치 주변 100x100 영역 캡처")
    line = input("> ").strip()

    if not line:
        # 현재 마우스 기준 100x100 박스를 잡되, 화면 밖으로 안 나가게 보정
        x, y = pyautogui.position()
        w, h = 100, 100
        left = max(0, x - w // 2)
        top = max(0, y - h // 2)
        right = left + w
        bottom = top + h
        bbox = (left, top, right, bottom)
    else:
        parts = line.split()
        if len(parts) != 4:
            print("x y width height 네 개의 숫자를 입력하세요")
            return
        try:
            x, y, w, h = [int(p) for p in parts]
        except ValueError:
            print("숫자만 입력하세요")
            return
        if w <= 0 or h <= 0:
            print("width, height 는 0보다 커야 합니다")
            return
        # PIL.ImageGrab.grab 에서는 bbox=(left, top, right, bottom) 형식 사용
        bbox = (x, y, x + w, y + h)

    name = input("저장할 파일명 (예: trigger.png): ").strip() or "capture.png"
    if not name.endswith((".png", ".jpg")):
        name += ".png"
    save_dir.mkdir(parents=True, exist_ok=True)
    path = save_dir / name
    img = ImageGrab.grab(bbox=bbox)
    img.save(str(path))
    print(f"저장됨: {path}")


if __name__ == "__main__":
    base = Path(__file__).parent
    templates = base / "macros" / "templates"
    print(f"템플릿 저장 경로: {templates}")
    capture_region_interactive(templates)
