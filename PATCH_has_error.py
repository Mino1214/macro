"""
★ main.py 에서 has_error() 함수만 아래로 교체하세요 ★

그리고 상단 import에 추가:
  from image_matcher_v2 import has_error_v2
"""


def has_error() -> bool:
    """
    v2: 픽셀 직접 샘플링 방식.
    error 좌표 주변에서 '색이 있는 텍스트'가 있으면 에러로 판단.
    """
    err_tpl = TEMPLATES_DIR / "mainpageerror.png"

    try:
        ex, ey = pos("error")
    except KeyError:
        ex, ey = 564, 474  # fallback 좌표

    return has_error_v2(
        error_x=ex,
        error_y=ey,
        template_path=err_tpl if err_tpl.exists() else None,
        debug=True,  # 안정화 후 False로 변경
    )
