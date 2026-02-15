"""
★★★ main.py 적용 가이드 ★★★

1. 파일 배치:
   프로젝트/
   ├── chrome_capture.py      ← 새로 추가
   ├── image_matcher_v3.py    ← 새로 추가 (기존 image_matcher.py 대체)
   ├── main.py                ← 아래 내용으로 수정
   ├── data/coords.json
   ├── wordlist.txt
   └── macros/templates/mainpageerror.png

2. main.py 수정사항 (3군데만 바꾸면 됨):
"""

# ─── 수정 1: import 변경 ──────────────────────────────────────
# 기존:
#   from image_matcher import capture_screen, find_on_screen
# 변경:
from image_matcher_v3 import capture_screen, find_on_screen, has_error_v3
from chrome_capture import get_chrome_region, focus_chrome


# ─── 수정 2: has_error() 교체 ─────────────────────────────────
def has_error() -> bool:
    err_tpl = TEMPLATES_DIR / "mainpageerror.png"

    try:
        ex, ey = pos("error")
    except KeyError:
        ex, ey = 564, 474

    # ★ 핵심: coords.json 좌표가 스크린 절대좌표라면 크롬 상대좌표로 변환
    chrome = get_chrome_region()
    if chrome:
        cx, cy, _, _ = chrome
        # 절대 좌표 → 크롬 내부 상대 좌표
        rel_x = ex - cx
        rel_y = ey - cy
    else:
        rel_x, rel_y = ex, ey

    return has_error_v3(
        error_x=rel_x,
        error_y=rel_y,
        template_path=err_tpl if err_tpl.exists() else None,
        debug=True,
    )


# ─── 수정 3: main() 시작 부분에 크롬 포커스 추가 ─────────────
def main_start_addition():
    """main() 함수 시작 부분에 이거 추가"""
    # 크롬을 앞으로 가져오기
    focus_chrome()
    import time
    time.sleep(0.5)

    # 크롬 영역 확인
    chrome = get_chrome_region()
    if chrome:
        print(f"크롬 창 감지: x={chrome[0]}, y={chrome[1]}, w={chrome[2]}, h={chrome[3]}")
    else:
        print("⚠ 크롬 창을 찾을 수 없습니다!")
        return


# ─── 필요한 패키지 ────────────────────────────────────────────
"""
Mac:
  pip install pyautogui opencv-python numpy

Windows:
  pip install pyautogui opencv-python numpy pywin32
  # 또는
  pip install pyautogui opencv-python numpy pygetwindow
"""
