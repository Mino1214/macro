"""
Safepal 크롬 확장용 사진 기반 매크로 - 전체 플로우:
- 플러그인 아이콘 클릭
- 비밀번호 로그인
- selection / selection2 에서 지갑/니모닉 가져오기 선택
- mainpage 니모닉 1~12 입력 후 다음(Enter) 진행
- 에러 여부 확인
- 성공 시 잔고(balance) 읽어서 0 초과이면 니모닉 + 잔고 로그 기록
"""
from __future__ import annotations

import csv
import json
import os
import re
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Tuple

import cv2
import numpy as np
import pytesseract

from image_matcher import (
    capture_screen,
    find_on_screen,
    find_on_screen_multiscale,
    find_on_screen_orb,
)

try:
    import pyautogui

    pyautogui.FAILSAFE = True
except ImportError:  # pragma: no cover - 런타임에서만 확인
    pyautogui = None

# Windows에서 Tesseract 경로 지정 (PATH에 없을 때를 대비)
if os.name == "nt":
    default_tess = r"C:\Program Files\Tesseract-OCR\tesseract.exe"
    tess_cmd = os.environ.get("TESSERACT_CMD", default_tess)
    pytesseract.pytesseract.tesseract_cmd = tess_cmd


BASE_DIR = Path(__file__).parent
TEMPLATES_DIR = BASE_DIR / "macros" / "templates"
DATA_DIR = BASE_DIR / "data"
LOGS_DIR = BASE_DIR / "logs"
MNEMONICS_FILE = DATA_DIR / "mnemonics.txt"
RESULTS_FILE = LOGS_DIR / "results.csv"
COORDS_FILE = DATA_DIR / "coords.json"


@dataclass
class FlowResult:
    success: bool
    balance: float
    error: str = ""


def _require_pyautogui() -> None:
    if pyautogui is None:
        raise RuntimeError("pyautogui가 필요합니다. pip install pyautogui")


_coords_cache: Optional[Dict[str, Tuple[int, int]]] = None


def _load_coords() -> Dict[str, Tuple[int, int]]:
    """data/coords.json 에서 좌표 맵을 읽는다. {name: {x, y}}"""
    global _coords_cache
    if _coords_cache is not None:
        return _coords_cache
    if not COORDS_FILE.exists():
        _coords_cache = {}
        return _coords_cache
    try:
        with open(COORDS_FILE, "r", encoding="utf-8") as f:
            raw = json.load(f)
        coords: Dict[str, Tuple[int, int]] = {}
        for key, val in raw.items():
            if not isinstance(val, dict):
                continue
            x = val.get("x")
            y = val.get("y")
            if isinstance(x, int) and isinstance(y, int):
                coords[str(key)] = (x, y)
        _coords_cache = coords
        return coords
    except Exception:
        _coords_cache = {}
        return _coords_cache


def _get_coord_for_template(template_name: str) -> Optional[Tuple[int, int]]:
    """
    템플릿 이름(예: 'safepal.png')에 대응하는 좌표가 있으면 반환.
    - 우선 'safepal.png' 전체 키로 찾고
    - 없으면 확장자 제거한 'safepal' 로 찾는다.
    """
    name = template_name
    coords = _load_coords()
    if name in coords:
        return coords[name]
    base = name.rsplit(".", 1)[0]
    return coords.get(base)


def load_mnemonics(path: Path = MNEMONICS_FILE) -> List[List[str]]:
    """
    data/mnemonics.txt 에서 니모닉 세트들을 읽어온다.
    - 한 줄에 12개 단어, 공백 또는 콤마로 구분
    """
    if not path.exists():
        raise FileNotFoundError(f"니모닉 파일을 찾을 수 없습니다: {path}")

    sets: List[List[str]] = []
    with open(path, "r", encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line or line.startswith("#"):
                continue
            if "," in line:
                parts = [p.strip() for p in line.split(",") if p.strip()]
            else:
                parts = [p for p in line.split() if p]
            if len(parts) != 12:
                # 12개가 아니면 스킵
                continue
            sets.append(parts)
    return sets


def wait_for_template(
    template_name: str,
    timeout: float = 10.0,
    threshold: float = 0.8,
) -> Optional[Tuple[int, int]]:
    """
    템플릿이 화면에 나타날 때까지 기다리고, 중심 좌표를 반환.
    시간 내에 못 찾으면 None.
    """
    template_path = TEMPLATES_DIR / template_name
    end_time = time.time() + timeout
    while time.time() < end_time:
        pos: Optional[Tuple[int, int]] = None

        # 1) 멀티스케일 템플릿 매칭 (흰 여백 크롭 + 스케일 조정)
        try:
            pos = find_on_screen_multiscale(
                template_path,
                threshold=0.7 if template_name == "safepal.png" else threshold,
            )
        except Exception:
            pos = None

        # 2) safepal 아이콘처럼 작고 패턴이 단순한 요소는 ORB 기반 매칭도 추가 시도
        if pos is None and template_name == "safepal.png":
            pos = find_on_screen_orb(template_path)

        # 3) 그래도 못 찾으면 기존 템플릿 매칭으로 한 번 더 시도
        if pos is None:
            pos = find_on_screen(template_path, threshold=threshold)

        if pos is not None:
            return pos
        time.sleep(0.3)
    return None


def wait_and_click(
    template_name: str,
    timeout: float = 10.0,
    threshold: float = 0.8,
) -> Tuple[int, int]:
    """
    템플릿이 나타날 때까지 기다렸다가 해당 위치를 클릭하고 좌표를 반환.
    - data/coords.json 에 좌표가 정의되어 있으면, 이미지 매칭 없이 해당 좌표를 바로 클릭.
    """
    _require_pyautogui()

    # 좌표 기반 모드(이미지 매칭이 너무 불안정할 때 사용)
    coord = _get_coord_for_template(template_name)
    if coord is not None:
        x, y = coord
        pyautogui.click(x, y)
        return x, y

    # 기본: 이미지가 실제로 화면에 나타날 때까지 기다린 뒤 클릭
    pos = wait_for_template(template_name, timeout=timeout, threshold=threshold)
    if pos is None:
        raise TimeoutError(f"템플릿을 찾지 못했습니다: {template_name}")
    x, y = pos
    pyautogui.click(x, y)
    return x, y


def wait_for_exists(
    template_name: str,
    timeout: float = 10.0,
    threshold: float = 0.8,
) -> bool:
    """
    템플릿이 화면에 나타나면 True, 아니면 False.
    """
    return wait_for_template(template_name, timeout=timeout, threshold=threshold) is not None


def type_text(text: str, interval: float = 0.05) -> None:
    """
    현재 포커스된 입력창에 텍스트 입력.
    """
    _require_pyautogui()
    if not text:
        return
    pyautogui.write(text, interval=interval)


def press_key(key: str) -> None:
    _require_pyautogui()
    pyautogui.press(key)


def read_balance_from_template_region(
    template_name: str = "balance.png",
    threshold: float = 0.8,
) -> float:
    """
    balance 템플릿 위치 주변을 캡처해 OCR로 숫자를 읽는다.
    - balance.png 는 잔고 숫자 근처(레이블 또는 심볼 포함 영역)로 캡처해두었다고 가정.
    - 숫자/소수점/쉼표, 달러 기호 등을 제거하고 float 로 변환.
    """
    template_path = TEMPLATES_DIR / template_name
    pos = find_on_screen(template_path, threshold=threshold)
    if pos is None:
        raise RuntimeError("잔고 템플릿(balance.png)을 화면에서 찾지 못했습니다.")

    cx, cy = pos
    # balance 텍스트는 대개 오른쪽에 위치한다고 가정하고 영역을 넓게 잡는다.
    region_width = 260
    region_height = 80
    x = max(0, cx - 30)
    y = max(0, cy - region_height // 2)
    region = (x, y, region_width, region_height)

    img = capture_screen(region)
    gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
    # 간단한 바이너리 스레시홀드로 글자 대비 강화
    _, thresh = cv2.threshold(gray, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)

    text = pytesseract.image_to_string(thresh, config="--psm 7")
    # 숫자/소수점/콤마/달러 기호만 남기고 파싱
    cleaned = re.findall(r"[0-9][0-9,\\.]*", text.replace("$", ""))
    if not cleaned:
        return 0.0
    raw = cleaned[0].replace(",", "")
    try:
        return float(raw)
    except ValueError:
        return 0.0


def do_login(password: str, threshold: float = 0.8) -> None:
    """
    플러그인 아이콘 클릭 → 비밀번호 입력 → 로그인 버튼 클릭

    templates 사용:
    - safepal.png : 확장 아이콘
    - passwordinput.png : 비밀번호 입력칸
    - passwordbutton.png : 로그인/확인 버튼
    """
    # 브라우저 툴바에서 확장 아이콘 클릭
    # safepal 아이콘은 매칭 값이 낮게 나올 수 있어서 threshold를 별도로 더 낮게 사용
    wait_and_click("safepal.png", timeout=10, threshold=0.2)

    # 로그인 비밀번호 입력
    wait_and_click("passwordinput.png", timeout=10, threshold=threshold)
    type_text(password)
    wait_and_click("passwordbutton.png", timeout=10, threshold=threshold)


def go_to_mnemonic_import(threshold: float = 0.8) -> None:
    """
    selection / selection2 화면에서 니모닉 가져오기까지 진입.

    templates 사용:
    - selectionbutton.png : '지갑 가져오기' 버튼
    - selection2button.png : '니모닉 문구 가져오기' 버튼
    """
    wait_and_click("selectionbutton.png", timeout=10, threshold=threshold)
    wait_and_click("selection2button.png", timeout=10, threshold=threshold)


def enter_mnemonic_words(
    words: Iterable[str],
    threshold: float = 0.8,
) -> None:
    """
    mainpage 에서 1~12번 니모닉 입력 후 '다음'으로 진행.

    templates 사용:
    - mainpage.png : 첫 번째 니모닉 입력칸 근처(또는 제목 영역 등, 클릭 시 첫 칸이 포커스 되는 위치)
    - '다음' 버튼은 존재한다고 가정하지 않고, Enter 키로 제출.
    """
    # mainpage.png 위치를 클릭해 첫 번째 입력칸에 포커스 준다고 가정
    wait_and_click("mainpage.png", timeout=10, threshold=threshold)

    for idx, w in enumerate(words):
        type_text(w)
        # 마지막 칸이 아니면 Tab 으로 다음 입력칸 이동
        if idx < 11:
            press_key("tab")

    # 모든 단어 입력 후 Enter 로 제출
    press_key("enter")


def check_mnemonic_error_or_success(
    threshold: float = 0.8,
) -> bool:
    """
    mainpageerror / successpage 템플릿을 기준으로
    - 에러가 보이면 False
    - 성공 페이지로 넘어가면 True
    """
    # 잠시 대기 후 에러 여부 확인
    time.sleep(2.0)

    # 에러 이미지가 보이면 실패
    if wait_for_exists("mainpageerror.png", timeout=3, threshold=threshold):
        return False

    # 성공 페이지가 보이거나, 이후 balance 영역이 나타나면 성공으로 본다.
    if wait_for_exists("successpage.png", timeout=10, threshold=threshold):
        return True

    # successpage 템플릿이 없다면, 이후 balance 템플릿으로 최종 판단
    if wait_for_exists("balance.png", timeout=10, threshold=threshold):
        return True

    # 둘 다 못 찾으면 실패로 간주
    return False


def run_full_flow_for_mnemonic(
    mnemonic_words: List[str],
    password: str,
    threshold: float = 0.8,
) -> FlowResult:
    """
    한 세트의 니모닉에 대해 전체 플로우 수행:
    - 로그인 → 니모닉 가져오기 → 12 단어 입력 → 에러/성공 판단 → 잔고 확인
    """
    try:
        do_login(password=password, threshold=threshold)
        go_to_mnemonic_import(threshold=threshold)
        enter_mnemonic_words(mnemonic_words, threshold=threshold)

        ok = check_mnemonic_error_or_success(threshold=threshold)
        if not ok:
            return FlowResult(success=False, balance=0.0, error="mnemonic_invalid")

        # 잔고 읽기
        # balance 템플릿이 보일 때까지 기다린 후 OCR 수행
        if not wait_for_exists("balance.png", timeout=15, threshold=threshold):
            return FlowResult(success=False, balance=0.0, error="balance_not_visible")

        balance = read_balance_from_template_region("balance.png", threshold=threshold)
        if balance <= 0:
            return FlowResult(success=False, balance=balance, error="balance_zero_or_parse_failed")

        return FlowResult(success=True, balance=balance, error="")
    except TimeoutError as e:
        return FlowResult(success=False, balance=0.0, error=f"timeout:{e}")
    except Exception as e:  # pragma: no cover - 실제 런타임에서만
        return FlowResult(success=False, balance=0.0, error=f"exception:{e}")


def append_result_log(
    mnemonic_words: List[str],
    result: FlowResult,
    results_file: Path = RESULTS_FILE,
) -> None:
    """
    logs/results.csv 에 결과 한 줄 추가.
    - timestamp,status,balance,mnemonic,error
    """
    results_file.parent.mkdir(parents=True, exist_ok=True)
    exists = results_file.exists()
    with open(results_file, "a", newline="", encoding="utf-8") as f:
        writer = csv.writer(f)
        if not exists:
            writer.writerow(["timestamp", "status", "balance", "mnemonic", "error"])
        ts = time.strftime("%Y-%m-%d %H:%M:%S", time.localtime())
        status = "success" if result.success else "fail"
        mnemonic_str = " ".join(mnemonic_words)
        writer.writerow([ts, status, f"{result.balance}", mnemonic_str, result.error])


def run_for_all_mnemonics(
    password: Optional[str] = None,
    threshold: float = 0.8,
) -> None:
    """
    mnemonics.txt 에 정의된 모든 니모닉 세트에 대해 순차적으로 플로우 실행.
    - EXT_PASSWORD 환경변수 또는 인자로 받은 password 사용.
    """
    if password is None:
        password = os.environ.get("EXT_PASSWORD") or os.environ.get("SAFE_PASSWORD")
    if not password:
        # 완전 자동화를 위해서는 환경변수로 넣어두는 것이 좋다.
        password = input("지갑 비밀번호를 입력하세요: ").strip()

    mnemonic_sets = load_mnemonics(MNEMONICS_FILE)
    if not mnemonic_sets:
        print(f"니모닉 세트를 찾지 못했습니다: {MNEMONICS_FILE}")
        return

    print(f"총 {len(mnemonic_sets)}개의 니모닉 세트 실행을 시작합니다.")

    for idx, words in enumerate(mnemonic_sets, start=1):
        print(f"[{idx}/{len(mnemonic_sets)}] 니모닉 실행 중...")
        result = run_full_flow_for_mnemonic(words, password=password, threshold=threshold)
        append_result_log(words, result, RESULTS_FILE)
        if result.success:
            print(f"  -> 성공: balance={result.balance}")
        else:
            print(f"  -> 실패: {result.error}")
        # 각 세트 사이에 약간의 딜레이를 두어 UI가 정리될 시간을 준다.
        time.sleep(2.0)


def main() -> None:
    run_for_all_mnemonics()


if __name__ == "__main__":
    main()

