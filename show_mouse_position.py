"""
마우스 위치 실시간 표시 (pyautogui.displayMousePosition() 대신)
좌표 확인 후 알려주실 때 사용하세요. Ctrl+C 로 종료.
"""
import pyautogui
import time

print("마우스를 움직이면 좌표가 출력됩니다. (Ctrl+C 로 종료)\n")

try:
    while True:
        x, y = pyautogui.position()
        print(f"\r  x={x}  y={y}  ", end="", flush=True)
        time.sleep(0.05)
except KeyboardInterrupt:
    print("\n\n종료됨.")
