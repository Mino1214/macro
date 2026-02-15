"""
diagnose_error.py
─────────────────
에러 감지가 왜 실패하는지 진단하는 스크립트.
실행하면 /tmp/ 에 디버그 이미지들이 저장됨.

사용법:
  1. 에러가 표시된 상태에서 이 스크립트 실행
  2. /tmp/diag_*.png 파일들을 확인
  3. 결과를 보고 has_error() 파라미터 조정
"""
import cv2
import numpy as np
import pyautogui
import json
from pathlib import Path

BASE_DIR = Path(__file__).parent
TEMPLATES_DIR = BASE_DIR / "macros" / "templates"
COORDS_FILE = BASE_DIR / "data" / "coords.json"

OUT = Path("/tmp")


def capture_full() -> np.ndarray:
    shot = pyautogui.screenshot()
    return cv2.cvtColor(np.array(shot), cv2.COLOR_RGB2BGR)


def main():
    print("=" * 60)
    print("에러 감지 진단 시작")
    print("=" * 60)

    # ── 1. 전체 화면 캡처 ──
    screen = capture_full()
    h, w = screen.shape[:2]
    print(f"\n[1] 화면 해상도: {w} x {h}")
    cv2.imwrite(str(OUT / "diag_fullscreen.png"), screen)
    print(f"    → 전체 화면 저장: {OUT / 'diag_fullscreen.png'}")

    # ── 2. error 좌표 주변 캡처 ──
    with open(COORDS_FILE, "r", encoding="utf-8") as f:
        coords = json.load(f)

    ex = int(coords.get("error", {}).get("x", 564))
    ey = int(coords.get("error", {}).get("y", 474))
    print(f"\n[2] error 좌표: ({ex}, {ey})")

    # 여러 크기의 영역을 캡처해서 비교
    for box_w, box_h in [(200, 60), (400, 80), (600, 120), (800, 200)]:
        x1 = max(0, ex - box_w // 2)
        y1 = max(0, ey - box_h // 2)
        x2 = min(w, x1 + box_w)
        y2 = min(h, y1 + box_h)
        crop = screen[y1:y2, x1:x2]
        fname = f"diag_region_{box_w}x{box_h}.png"
        cv2.imwrite(str(OUT / fname), crop)
        print(f"    → 영역({box_w}x{box_h}) 저장: {OUT / fname}")

    # ── 3. 에러 좌표 주변 색상 분석 ──
    print(f"\n[3] error 좌표 주변 색상 분석")
    region_big = screen[max(0, ey - 100):min(h, ey + 100), max(0, ex - 300):min(w, ex + 300)]
    hsv = cv2.cvtColor(region_big, cv2.COLOR_BGR2HSV)

    # 빨간색 감지 (넓은 범위)
    lower_red1 = np.array([0, 50, 50])
    upper_red1 = np.array([15, 255, 255])
    lower_red2 = np.array([160, 50, 50])
    upper_red2 = np.array([180, 255, 255])
    mask_r = cv2.inRange(hsv, lower_red1, upper_red1) | cv2.inRange(hsv, lower_red2, upper_red2)
    red_count = int(np.count_nonzero(mask_r))
    cv2.imwrite(str(OUT / "diag_red_mask.png"), mask_r)
    print(f"    빨간 픽셀 수 (넓은 범위): {red_count}")

    # 실제 어떤 색상이 있는지 분석
    # 고유 색상 상위 20개
    bgr_flat = region_big.reshape(-1, 3)
    unique_colors, counts = np.unique(bgr_flat, axis=0, return_counts=True)
    top_idx = np.argsort(-counts)[:20]
    print(f"    상위 20 색상 (BGR):")
    for i in top_idx:
        b, g, r = unique_colors[i]
        print(f"      #{r:02x}{g:02x}{b:02x} (R={r}, G={g}, B={b}) — {counts[i]}px")

    # ── 4. 에러 텍스트 색상 직접 샘플링 ──
    print(f"\n[4] error 좌표 정확한 픽셀 색상")
    for dy in range(-5, 6, 2):
        for dx in range(-20, 21, 10):
            py, px = min(h - 1, max(0, ey + dy)), min(w - 1, max(0, ex + dx))
            b, g, r = screen[py, px]
            print(f"    ({px},{py}): #{r:02x}{g:02x}{b:02x}  R={r} G={g} B={b}")

    # ── 5. 템플릿 분석 ──
    tpl_path = TEMPLATES_DIR / "mainpageerror.png"
    print(f"\n[5] 템플릿 분석: {tpl_path}")
    if tpl_path.exists():
        tpl = cv2.imread(str(tpl_path))
        th, tw = tpl.shape[:2]
        print(f"    템플릿 크기: {tw} x {th}")
        print(f"    화면 대비 비율: {tw/w:.3f} x {th/h:.3f}")

        # 템플릿 색상 분석
        tpl_flat = tpl.reshape(-1, 3)
        tpl_unique, tpl_counts = np.unique(tpl_flat, axis=0, return_counts=True)
        top_tpl = np.argsort(-tpl_counts)[:10]
        print(f"    템플릿 상위 색상 (BGR):")
        for i in top_tpl:
            b, g, r = tpl_unique[i]
            print(f"      #{r:02x}{g:02x}{b:02x} (R={r}, G={g}, B={b}) — {tpl_counts[i]}px")

        # 템플릿과 화면 error 영역 직접 비교
        crop_for_tpl = screen[max(0, ey - th // 2):max(0, ey - th // 2) + th,
                              max(0, ex - tw // 2):max(0, ex - tw // 2) + tw]
        if crop_for_tpl.shape[:2] == tpl.shape[:2]:
            diff = cv2.absdiff(crop_for_tpl, tpl)
            avg_diff = float(np.mean(diff))
            print(f"    템플릿 vs 화면 평균 차이: {avg_diff:.1f} (0=동일, >50=매우 다름)")
            cv2.imwrite(str(OUT / "diag_tpl_diff.png"), diff * 3)  # 차이 강조
        else:
            print(f"    ⚠ 크기 불일치로 직접 비교 불가: 화면crop={crop_for_tpl.shape[:2]} vs 템플릿={tpl.shape[:2]}")

        cv2.imwrite(str(OUT / "diag_template.png"), tpl)
    else:
        print("    ⚠ 템플릿 파일 없음!")

    # ── 6. 새 템플릿 자동 생성 제안 ──
    print(f"\n[6] 새 템플릿 자동 캡처")
    # error 좌표 중심으로 적절한 크기로 잘라서 새 템플릿으로 저장
    new_tpl_h, new_tpl_w = 40, 350
    y1 = max(0, ey - new_tpl_h // 2)
    x1 = max(0, ex - new_tpl_w // 2)
    new_tpl = screen[y1:y1 + new_tpl_h, x1:x1 + new_tpl_w]
    new_tpl_path = OUT / "diag_new_template.png"
    cv2.imwrite(str(new_tpl_path), new_tpl)
    print(f"    → 새 템플릿 후보 저장: {new_tpl_path}")
    print(f"    → 이 이미지가 에러 텍스트를 잘 담고 있으면,")
    print(f"       {TEMPLATES_DIR / 'mainpageerror.png'} 에 덮어쓰세요!")

    print("\n" + "=" * 60)
    print("진단 완료! /tmp/diag_*.png 파일들을 확인하세요.")
    print("=" * 60)


if __name__ == "__main__":
    main()
