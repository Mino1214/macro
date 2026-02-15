#!/usr/bin/env python3
"""
사진 기반 매크로 실행 스크립트
사용법:
  python run_macro.py macros/example_macro.json
  python run_macro.py macros/example_macro.json --loop   # 트리거 나올 때까지 대기 후 실행
  python run_macro.py macros/example_macro.json --threshold 0.85
"""
import argparse
from pathlib import Path
import time

from macro_engine import run_macro


def main():
    parser = argparse.ArgumentParser(description="이미지 기반 매크로 실행")
    parser.add_argument("macro", type=str, help="매크로 JSON 파일 경로")
    parser.add_argument("--loop", action="store_true", help="트리거 이미지가 나올 때까지 대기 후 실행")
    parser.add_argument("--interval", type=float, default=1.0, help="--loop일 때 확인 간격(초)")
    parser.add_argument("--threshold", type=float, default=0.8, help="이미지 매칭 임계값 (0~1)")
    parser.add_argument("--templates", type=str, default=None, help="템플릿 이미지 폴더 (기본: 매크로폴더/templates)")
    args = parser.parse_args()

    macro_path = Path(args.macro)
    if not macro_path.exists():
        print(f"파일 없음: {macro_path}")
        return 1

    templates = Path(args.templates) if args.templates else None

    if args.loop:
        print("트리거 이미지 대기 중... (Ctrl+C로 종료)")
        try:
            while True:
                if run_macro(macro_path, templates_dir=templates, threshold=args.threshold):
                    print("매크로 실행 완료.")
                    break
                time.sleep(args.interval)
        except KeyboardInterrupt:
            print("\n종료")
            return 0
    else:
        ok = run_macro(macro_path, templates_dir=templates, threshold=args.threshold)
        if ok:
            print("매크로 실행 완료.")
        else:
            print("화면에서 트리거 이미지를 찾지 못했습니다. threshold를 낮추거나 이미지를 확인하세요.")
        return 0 if ok else 1


if __name__ == "__main__":
    exit(main())
