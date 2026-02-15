#!/usr/bin/env python3
"""
capture_helper.py
─────────────────
템플릿 이미지를 정확히 캡처하고 테스트하는 도구

사용법:
  python3 capture_helper.py capture    # 마우스 위치 기반 캡처
  python3 capture_helper.py test       # 기존 템플릿 테스트
  python3 capture_helper.py regions    # 검색 영역 시각화
"""

import sys
import time
from pathlib import Path

import cv2
import numpy as np
import pyautogui

from image_matcher_v3 import capture_screen, find_on_screen_normalized
from chrome_capture import get_chrome_region, focus_chrome, scale_for_click

BASE_DIR = Path(__file__).parent
PICS_KR = BASE_DIR / "pic" / "kr"


def capture_template_interactive():
    """마우스 위치 기반으로 템플릿 캡처"""
    print("\n" + "="*60)
    print("  템플릿 이미지 캡처 도구")
    print("="*60)
    print("\n📍 캡처할 버튼/요소 위에 마우스를 올려주세요.")
    print("   (3초 후 자동으로 캡처합니다)\n")
    
    for i in range(3, 0, -1):
        print(f"   {i}초...")
        time.sleep(1)
    
    # 마우스 위치
    mx, my = pyautogui.position()
    print(f"\n마우스 위치: ({mx}, {my})")
    
    # 화면 캡처
    full_screen = capture_screen()
    
    # 픽셀 스케일 고려 (Mac Retina)
    scale = scale_for_click()
    px, py = int(mx * scale), int(my * scale)
    
    print(f"픽셀 좌표: ({px}, {py}) (스케일: {scale}x)")
    
    # 여러 크기로 저장
    sizes = [40, 60, 80, 100, 120]
    saved_files = []
    
    for size in sizes:
        half = size // 2
        y1, y2 = max(0, py - half), min(full_screen.shape[0], py + half)
        x1, x2 = max(0, px - half), min(full_screen.shape[1], px + half)
        
        cropped = full_screen[y1:y2, x1:x2]
        
        # 중심에 십자선 표시 (확인용)
        h, w = cropped.shape[:2]
        viz = cropped.copy()
        cv2.line(viz, (w//2, 0), (w//2, h), (0, 255, 0), 1)
        cv2.line(viz, (0, h//2), (w, h//2), (0, 255, 0), 1)
        
        filename = f"template_{size}x{size}.png"
        cv2.imwrite(filename, cropped)
        cv2.imwrite(filename.replace(".png", "_preview.png"), viz)
        saved_files.append(filename)
        
        print(f"   ✓ {filename} (중심: 마우스 위치)")
    
    print("\n" + "="*60)
    print("캡처 완료!")
    print("="*60)
    print("\n생성된 파일:")
    for f in saved_files:
        print(f"  - {f}")
        print(f"    {f.replace('.png', '_preview.png')} (십자선 = 중심)")
    
    print("\n다음 단계:")
    print("  1. 이미지 뷰어로 각 파일 확인")
    print("  2. 버튼이 정확히 포함된 크기 선택")
    print("  3. 선택한 파일을 pic/kr/mainpage_menu.png로 복사")
    print(f"\n예: cp template_60x60.png {PICS_KR}/mainpage_menu.png")


def test_template(template_name: str = "mainpage_menu"):
    """기존 템플릿의 매칭 성능 테스트"""
    print("\n" + "="*60)
    print(f"  템플릿 테스트: {template_name}")
    print("="*60)
    
    template_path = PICS_KR / f"{template_name}.png"
    if not template_path.exists():
        print(f"\n❌ 템플릿 없음: {template_path}")
        print(f"   pic/kr/{template_name}.png 파일을 먼저 생성하세요.")
        return
    
    print("\n크롬 창 포커스 중...")
    focus_chrome()
    time.sleep(1)
    
    chrome = get_chrome_region()
    if not chrome:
        print("❌ 크롬 창을 찾을 수 없습니다.")
        return
    
    cx, cy, cw, ch = chrome
    print(f"✅ 크롬 영역: x={cx}, y={cy}, {cw}x{ch}")
    
    # 화면 캡처
    full_screen = capture_screen()
    chrome_img = full_screen[cy:cy+ch, cx:cx+cw]
    
    # 템플릿 로드
    template = cv2.imread(str(template_path))
    th, tw = template.shape[:2]
    print(f"📐 템플릿 크기: {tw}x{th}")
    
    # 여러 임계값으로 테스트
    thresholds = [0.4, 0.5, 0.6, 0.7, 0.8]
    results = []
    
    print("\n매칭 테스트:")
    for threshold in thresholds:
        result = cv2.matchTemplate(chrome_img, template, cv2.TM_CCOEFF_NORMED)
        min_val, max_val, min_loc, max_loc = cv2.minMaxLoc(result)
        
        status = "✓" if max_val >= threshold else "✗"
        results.append((threshold, max_val, max_loc))
        print(f"  임계값 {threshold:.1f}: {status} (점수: {max_val:.3f})")
    
    # 최고 점수 결과 시각화
    _, best_confidence, best_loc = max(results, key=lambda x: x[1])
    
    if best_confidence > 0.3:
        # 결과 이미지에 매칭 위치 표시
        viz = chrome_img.copy()
        x, y = best_loc
        cv2.rectangle(viz, (x, y), (x + tw, y + th), (0, 255, 0), 2)
        
        # 중심점 표시
        cx_center = x + tw // 2
        cy_center = y + th // 2
        cv2.circle(viz, (cx_center, cy_center), 5, (0, 0, 255), -1)
        
        # 정보 텍스트
        cv2.putText(viz, f"Match: {best_confidence:.3f}", 
                    (x, y - 10), cv2.FONT_HERSHEY_SIMPLEX, 
                    0.6, (0, 255, 0), 2)
        
        output_path = f"test_{template_name}_result.png"
        cv2.imwrite(output_path, viz)
        
        print(f"\n결과 저장: {output_path}")
        print(f"  초록 사각형: 매칭 영역")
        print(f"  빨간 점: 클릭될 위치 (중심)")
        print(f"  매칭 점수: {best_confidence:.3f}")
        
        if best_confidence < 0.5:
            print("\n⚠️  점수가 낮습니다! (0.5 미만)")
            print("   권장 조치:")
            print("   1. 템플릿 재캡처 (더 선명하게)")
            print("   2. 버튼 크기를 키워서 캡처")
            print("   3. 임계값을 낮추기 (STATE_MATCH_THRESHOLD)")
    else:
        print("\n❌ 매칭 실패 (점수 0.3 미만)")
        print("   템플릿이 현재 화면에 없거나 너무 다릅니다.")
        print("   화면을 확인하고 템플릿을 재캡처하세요.")
    
    # 템플릿 이미지도 저장
    cv2.imwrite(f"test_{template_name}_template.png", template)
    print(f"\n참고: 템플릿 이미지 → test_{template_name}_template.png")


def visualize_search_regions():
    """검색 영역을 시각적으로 표시"""
    print("\n" + "="*60)
    print("  검색 영역 시각화")
    print("="*60)
    
    focus_chrome()
    time.sleep(1)
    
    chrome = get_chrome_region()
    if not chrome:
        print("❌ 크롬 창을 찾을 수 없습니다.")
        return
    
    cx, cy, cw, ch = chrome
    print(f"✅ 크롬 영역: {cw}x{ch}")
    
    full_screen = capture_screen()
    viz = full_screen.copy()
    
    # 크롬 전체 영역 (초록색)
    cv2.rectangle(viz, (cx, cy), (cx + cw, cy + ch), (0, 255, 0), 3)
    cv2.putText(viz, f"Chrome {cw}x{ch}", (cx + 10, cy + 30),
                cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 255, 0), 2)
    
    # mainpage_menu 검색 영역 (파란색)
    # SEARCH_REGION_BY_IMAGE의 설정 사용
    rx_ratio, ry_ratio, rw_ratio, rh_ratio = 0, 0, 0.25, 0.15
    
    rx = int(cw * rx_ratio)
    ry = int(ch * ry_ratio)
    rw = int(cw * rw_ratio)
    rh = int(ch * rh_ratio)
    
    cv2.rectangle(viz, (cx + rx, cy + ry), 
                  (cx + rx + rw, cy + ry + rh), (255, 0, 0), 2)
    cv2.putText(viz, f"Search Area {rw}x{rh}", 
                (cx + rx + 10, cy + ry + 30),
                cv2.FONT_HERSHEY_SIMPLEX, 0.7, (255, 0, 0), 2)
    
    output_path = "search_regions.png"
    cv2.imwrite(output_path, viz)
    
    print(f"\n저장됨: {output_path}")
    print(f"  초록색 = 크롬 전체 영역")
    print(f"  파란색 = mainpage_menu 검색 영역")
    print(f"           (좌상단 {rw_ratio:.0%}x{rh_ratio:.0%})")
    print("\n이미지를 열어서 햄버거 메뉴가 파란색 영역 안에 있는지 확인하세요.")


def main():
    if len(sys.argv) < 2:
        print("사용법:")
        print("  python3 capture_helper.py capture    # 템플릿 캡처")
        print("  python3 capture_helper.py test       # 템플릿 테스트")
        print("  python3 capture_helper.py test <name>  # 특정 템플릿 테스트")
        print("  python3 capture_helper.py regions    # 검색 영역 확인")
        return
    
    command = sys.argv[1].lower()
    
    if command == "capture":
        capture_template_interactive()
    elif command == "test":
        template_name = sys.argv[2] if len(sys.argv) > 2 else "mainpage_menu"
        test_template(template_name)
    elif command == "regions":
        visualize_search_regions()
    else:
        print(f"알 수 없는 명령: {command}")


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\n\n중단됨.")
