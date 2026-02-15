"""
main_patched_ratio_pyautogui.py
────────────────────────────────
main_patched_ratio와 동일한 플로우를 사용하되,
이미지 인식만 pyautogui (image_matcher_pyautogui) 로 수행하는 진입 스크립트.

사용: python main_patched_ratio_pyautogui.py
      python main_patched_ratio_pyautogui.py 테스트
"""
from __future__ import annotations

import sys
from pathlib import Path

# 이 스크립트에서 사용할 이미지 인식 모듈
import image_matcher_pyautogui as _pyauto_img

# main_patched_ratio 를 임포트하면서, 그 모듈의 캡처/인식 함수를 pyautogui 버전으로 교체
import main_patched_ratio as _m

_m.capture_screen = _pyauto_img.capture_screen
_m.find_on_screen_normalized = _pyauto_img.find_on_screen_normalized
_m.find_template_multiscale = _pyauto_img.find_template_multiscale
_m.find_image_on_chrome_improved = _pyauto_img.find_image_on_chrome_improved

if __name__ == "__main__":
    _m.main()
