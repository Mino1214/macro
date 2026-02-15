"""
사진 기반 매크로 엔진: 트리거 이미지가 화면에 보이면 액션 실행
"""
from __future__ import annotations

import json
import time
from pathlib import Path
from typing import Any, Optional

from image_matcher import capture_screen, find_template, find_on_screen

try:
    import pyautogui
    pyautogui.FAILSAFE = True
except ImportError:
    pyautogui = None


# 액션 타입
ACTION_CLICK = "click"       # (dx, dy) 오프셋 클릭
ACTION_DOUBLE_CLICK = "double_click"
ACTION_TYPE = "type"         # 문자열 입력
ACTION_WAIT = "wait"         # 초 대기
ACTION_WAIT_IMAGE = "wait_image"  # 이미지 나올 때까지 대기 (별도 이미지)


def run_action(
    action: dict[str, Any],
    base_x: int,
    base_y: int,
    templates_dir: Path,
) -> None:
    """단일 액션 실행. base_x, base_y는 트리거 이미지의 중심 좌표."""
    kind = action.get("action")
    if kind == ACTION_CLICK:
        dx = action.get("dx", 0)
        dy = action.get("dy", 0)
        x, y = base_x + dx, base_y + dy
        if pyautogui:
            pyautogui.click(x, y)
    elif kind == ACTION_DOUBLE_CLICK:
        dx = action.get("dx", 0)
        dy = action.get("dy", 0)
        x, y = base_x + dx, base_y + dy
        if pyautogui:
            pyautogui.doubleClick(x, y)
    elif kind == ACTION_TYPE:
        text = action.get("text", "")
        if pyautogui:
            pyautogui.write(text, interval=0.05)
    elif kind == ACTION_WAIT:
        sec = action.get("seconds", 0)
        time.sleep(sec)
    elif kind == ACTION_WAIT_IMAGE:
        img_name = action.get("image")
        if not img_name:
            return
        path = templates_dir / img_name
        timeout = action.get("timeout", 10)
        t0 = time.time()
        while time.time() - t0 < timeout:
            pos = find_on_screen(path, threshold=action.get("threshold", 0.8))
            if pos is not None:
                break
            time.sleep(0.3)
        else:
            raise TimeoutError(f"이미지 대기 시간 초과: {img_name}")


def run_macro(
    macro_path: str | Path,
    templates_dir: Optional[str | Path] = None,
    screen_region: Optional[tuple[int, int, int, int]] = None,
    threshold: float = 0.8,
) -> bool:
    """
    매크로 JSON 파일을 읽어 실행.
    - 트리거 이미지가 화면에 있으면 해당 위치를 기준으로 액션 수행.
    - 성공 시 True, 트리거를 찾지 못하면 False.
    """
    macro_path = Path(macro_path)
    if not macro_path.exists():
        raise FileNotFoundError(f"매크로 파일 없음: {macro_path}")

    templates_dir = templates_dir or macro_path.parent / "templates"
    templates_dir = Path(templates_dir)
    templates_dir.mkdir(parents=True, exist_ok=True)

    with open(macro_path, "r", encoding="utf-8") as f:
        data = json.load(f)

    trigger_image = data.get("trigger_image")
    if not trigger_image:
        raise ValueError("매크로에 trigger_image가 없습니다.")

    trigger_path = templates_dir / trigger_image
    pos = find_on_screen(trigger_path, region=screen_region, threshold=threshold)
    if pos is None:
        return False

    base_x, base_y = pos
    actions = data.get("actions", [])
    for act in actions:
        run_action(act, base_x, base_y, templates_dir)
    return True


def create_macro_example(macros_dir: Path) -> Path:
    """예제 매크로 JSON 파일 생성"""
    macros_dir = Path(macros_dir)
    macros_dir.mkdir(parents=True, exist_ok=True)
    (macros_dir / "templates").mkdir(exist_ok=True)

    example = {
        "name": "예제 매크로",
        "trigger_image": "trigger.png",
        "actions": [
            {"action": "click", "dx": 0, "dy": 0},
            {"action": "wait", "seconds": 1},
            {"action": "type", "text": "hello"},
            {"action": "wait", "seconds": 0.5},
        ],
    }
    path = macros_dir / "example_macro.json"
    with open(path, "w", encoding="utf-8") as f:
        json.dump(example, f, ensure_ascii=False, indent=2)
    return path
