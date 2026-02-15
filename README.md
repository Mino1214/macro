# 사진 기반 매크로 (Image-based Macro)

화면에 **특정 이미지(사진)**가 보이면 자동으로 클릭·입력·대기 같은 동작을 실행하는 매크로입니다.

## 설치

```bash
pip install -r requirements.txt
```

## 동작 방식

1. **트리거 이미지**: 매크로가 실행될 “기준”이 되는 화면 일부를 스크린샷으로 저장합니다.
2. **매크로 실행**: 현재 화면에서 그 이미지를 찾고, 찾은 위치를 기준으로 정해진 액션(클릭, 입력 등)을 수행합니다.

## 사용 방법

### 1. 템플릿 이미지 캡처

매크로의 트리거가 될 화면 영역을 이미지로 저장합니다.

```bash
python capture_region.py
```

- 좌표 4개 입력: `x y width height` (예: `100 200 80 30`)
- 또는 Enter만 누르면 **현재 마우스 위치** 기준 100×100 영역 저장
- 저장할 파일명 입력 (예: `trigger.png`)  
→ `macros/templates/` 아래에 저장됩니다.

### 2. 매크로 JSON 작성

`macros/` 폴더에 매크로 정의 JSON을 만듭니다.

**예시 (`macros/my_macro.json`):**

```json
{
  "name": "내 매크로",
  "trigger_image": "trigger.png",
  "actions": [
    { "action": "click", "dx": 0, "dy": 0 },
    { "action": "wait", "seconds": 1 },
    { "action": "type", "text": "hello" },
    { "action": "wait", "seconds": 0.5 }
  ]
}
```

- **trigger_image**: `macros/templates/` 안의 파일명 (캡처한 이미지).
- **actions**: 트리거 이미지 **중심 좌표**를 기준으로 실행되는 액션 목록.

**지원 액션**

| action        | 설명                    | 예시 |
|---------------|-------------------------|------|
| `click`       | (dx, dy) 오프셋 클릭    | `{"action": "click", "dx": 10, "dy": -5}` |
| `double_click`| 더블클릭                | `{"action": "double_click", "dx": 0, "dy": 0}` |
| `type`        | 문자열 입력             | `{"action": "type", "text": "hello"}` |
| `wait`        | N초 대기                | `{"action": "wait", "seconds": 1}` |
| `wait_image`  | 다른 이미지 나올 때까지 대기 | `{"action": "wait_image", "image": "next.png", "timeout": 10}` |

### 3. 매크로 실행

**한 번만 실행 (화면에 트리거가 있을 때):**

```bash
python run_macro.py macros/my_macro.json
```

**트리거 이미지가 나올 때까지 기다렸다가 실행:**

```bash
python run_macro.py macros/my_macro.json --loop
```

**매칭 정확도 조절 (기본 0.8):**

```bash
python run_macro.py macros/my_macro.json --threshold 0.85
```

## 폴더 구조

```
safepal/
├── image_matcher.py   # 화면 캡처, 이미지 찾기
├── macro_engine.py    # 매크로 실행 엔진
├── capture_region.py  # 영역 캡처 도구
├── run_macro.py       # 매크로 실행 CLI
├── requirements.txt
├── README.md
└── macros/
    ├── templates/     # 트리거/참조용 이미지
    │   └── trigger.png
    └── my_macro.json
```

## 참고

- 트리거 이미지는 **화면과 비슷한 크기·해상도**에서 캡처하는 것이 좋습니다.
- 매칭이 잘 안 되면 `--threshold`를 조금 낮추거나 (예: 0.75), 트리거 이미지를 다시 캡처해 보세요.
- macOS에서 화면 캡처 권한이 필요할 수 있습니다: **시스템 설정 → 개인 정보 보호 및 보안 → 화면 기록**에서 터미널/IDE 허용.
