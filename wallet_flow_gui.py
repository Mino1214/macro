"""
Safepal 사진 기반 매크로 - 간단 GUI 런처 (윈도우/맥 공용)

- 비밀번호 입력
- 니모닉 파일 선택
- 시작 버튼 / 로그 창

윈도우에서 .exe 로 빌드할 때 이 파일을 메인 스크립트로 쓰면 됩니다.
"""
from __future__ import annotations

import os
import threading
import tkinter as tk
from pathlib import Path
from tkinter import filedialog, messagebox, scrolledtext

from wallet_flow_macro import (
    DATA_DIR,
    LOGS_DIR,
    MNEMONICS_FILE,
    RESULTS_FILE,
    append_result_log,
    load_mnemonics,
    run_full_flow_for_mnemonic,
)


BASE_DIR = Path(__file__).parent


class WalletFlowGUI(tk.Tk):
    def __init__(self) -> None:
        super().__init__()
        self.title("Safepal 이미지 매크로 GUI")
        self.geometry("640x480")

        # 상태
        self._running = False
        self._worker_thread: threading.Thread | None = None

        # 위젯 구성
        self._build_widgets()

    # ---------------- UI ---------------- #
    def _build_widgets(self) -> None:
        # 상단 프레임: 파일/비밀번호/옵션
        top = tk.Frame(self)
        top.pack(fill=tk.X, padx=10, pady=10)

        # 니모닉 파일
        tk.Label(top, text="니모닉 파일:").grid(row=0, column=0, sticky="w")
        self.mnemo_var = tk.StringVar(value=str(MNEMONICS_FILE))
        self.mnemo_entry = tk.Entry(top, textvariable=self.mnemo_var, width=50)
        self.mnemo_entry.grid(row=0, column=1, sticky="we", padx=5)
        tk.Button(top, text="찾기...", command=self._choose_mnemonic_file).grid(
            row=0, column=2, padx=5
        )

        # 비밀번호
        tk.Label(top, text="지갑 비밀번호:").grid(row=1, column=0, sticky="w", pady=(5, 0))
        self.password_var = tk.StringVar(value=os.environ.get("EXT_PASSWORD", ""))
        self.password_entry = tk.Entry(top, textvariable=self.password_var, show="*")
        self.password_entry.grid(row=1, column=1, sticky="we", padx=5, pady=(5, 0))

        # threshold
        tk.Label(top, text="이미지 매칭 threshold (0~1):").grid(
            row=2, column=0, sticky="w", pady=(5, 0)
        )
        self.threshold_var = tk.StringVar(value="0.8")
        self.threshold_entry = tk.Entry(top, textvariable=self.threshold_var, width=10)
        self.threshold_entry.grid(row=2, column=1, sticky="w", padx=5, pady=(5, 0))

        top.columnconfigure(1, weight=1)

        # 버튼 영역
        btn_frame = tk.Frame(self)
        btn_frame.pack(fill=tk.X, padx=10, pady=(0, 5))

        self.start_btn = tk.Button(btn_frame, text="시작", command=self._on_start)
        self.start_btn.pack(side=tk.LEFT)

        self.stop_btn = tk.Button(btn_frame, text="중지", command=self._on_stop, state=tk.DISABLED)
        self.stop_btn.pack(side=tk.LEFT, padx=(5, 0))

        # 로그 영역
        self.log = scrolledtext.ScrolledText(self, state=tk.DISABLED)
        self.log.pack(fill=tk.BOTH, expand=True, padx=10, pady=(0, 10))

        # 하단 정보
        bottom = tk.Frame(self)
        bottom.pack(fill=tk.X, padx=10, pady=(0, 5))
        tk.Label(
            bottom,
            text=f"니모닉: {MNEMONICS_FILE} / 결과 로그: {RESULTS_FILE}",
            anchor="w",
        ).pack(fill=tk.X)

    # -------------- 이벤트 핸들러 -------------- #
    def _choose_mnemonic_file(self) -> None:
        initial_dir = DATA_DIR if DATA_DIR.exists() else BASE_DIR
        path = filedialog.askopenfilename(
            parent=self,
            title="니모닉 파일 선택",
            initialdir=initial_dir,
            filetypes=[("Text files", "*.txt"), ("All files", "*.*")],
        )
        if path:
            self.mnemo_var.set(path)

    def _on_start(self) -> None:
        if self._running:
            return

        mnemo_path = Path(self.mnemo_var.get()).expanduser()
        password = self.password_var.get().strip()
        threshold_str = self.threshold_var.get().strip()

        if not mnemo_path.exists():
            messagebox.showerror("오류", f"니모닉 파일을 찾을 수 없습니다:\n{mnemo_path}")
            return

        if not password:
            if not messagebox.askyesno("확인", "비밀번호가 비어 있습니다. 계속 진행할까요?"):
                return

        try:
            threshold = float(threshold_str)
        except ValueError:
            messagebox.showerror("오류", "threshold는 0~1 사이의 숫자여야 합니다.")
            return

        if not (0.0 < threshold <= 1.0):
            messagebox.showerror("오류", "threshold는 0보다 크고 1 이하의 값이어야 합니다.")
            return

        # 상태 업데이트
        self._running = True
        self.start_btn.config(state=tk.DISABLED)
        self.stop_btn.config(state=tk.NORMAL)
        self._append_log(f"시작: 파일={mnemo_path}, threshold={threshold}\n")

        # 워커 스레드 시작
        self._worker_thread = threading.Thread(
            target=self._worker_run,
            args=(mnemo_path, password, threshold),
            daemon=True,
        )
        self._worker_thread.start()

    def _on_stop(self) -> None:
        if not self._running:
            return
        self._append_log("중지 요청...\n")
        self._running = False

    # -------------- 워커 로직 -------------- #
    def _worker_run(
        self,
        mnemo_path: Path,
        password: str,
        threshold: float,
    ) -> None:
        try:
            sets = load_mnemonics(mnemo_path)
        except Exception as e:  # pragma: no cover
            self._append_log(f"니모닉 파일 읽기 오류: {e}\n")
            self._finish()
            return

        if not sets:
            self._append_log("니모닉 세트를 찾지 못했습니다.\n")
            self._finish()
            return

        self._append_log(f"총 {len(sets)}개의 니모닉 세트 실행을 시작합니다.\n")

        for idx, words in enumerate(sets, start=1):
            if not self._running:
                self._append_log("사용자에 의해 중지되었습니다.\n")
                break

            self._append_log(f"[{idx}/{len(sets)}] 니모닉 실행 중...\n")
            try:
                result = run_full_flow_for_mnemonic(words, password=password, threshold=threshold)
                append_result_log(words, result, RESULTS_FILE)
                if result.success:
                    self._append_log(f"  -> 성공: balance={result.balance}\n")
                else:
                    self._append_log(f"  -> 실패: {result.error}\n")
            except Exception as e:  # pragma: no cover
                self._append_log(f"  -> 예외 발생: {e}\n")

        self._finish()

    def _finish(self) -> None:
        def _update_ui() -> None:
            self._running = False
            self.start_btn.config(state=tk.NORMAL)
            self.stop_btn.config(state=tk.DISABLED)
            self._append_log("작업이 종료되었습니다.\n")

        # UI 업데이트는 메인스레드에서
        self.after(0, _update_ui)

    # -------------- 로그 출력 -------------- #
    def _append_log(self, text: str) -> None:
        def _do() -> None:
            self.log.config(state=tk.NORMAL)
            self.log.insert(tk.END, text)
            self.log.see(tk.END)
            self.log.config(state=tk.DISABLED)

        # 워커스레드에서도 호출될 수 있으므로 after 로 메인스레드에서 실행
        self.after(0, _do)


def main() -> None:
    # 기본 디렉터리 생성 보장
    DATA_DIR.mkdir(parents=True, exist_ok=True)
    LOGS_DIR.mkdir(parents=True, exist_ok=True)

    app = WalletFlowGUI()
    app.mainloop()


if __name__ == "__main__":
    main()

