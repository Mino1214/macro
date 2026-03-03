using OpenCvSharp;

namespace MainPatchedImproved;

/// <summary>
/// Trust Wallet 전용 플로우: data/pic/trustwallet/ 템플릿 사용, Chrome 창 캡처.
/// First Set: first → second → (third 탐지까지 대기 2~3초) → third → fourth → fifth → 니모닉 Ctrl+V
/// 이후 failbutton/successbutton 감지 → fail 시 fifth 클릭, Ctrl+A, 새 니모닉 붙여넣기 반복 / success 시 successbutton 클릭 → success.png 클릭 → Ctrl+W → first로 복귀.
/// </summary>
public static class MainLoopTrustWallet
{
    private const double Th = 0.75;
    private const double ThResult = 0.8;
    private const int WaitThirdMs = 3500;
    private const int PollIntervalMs = 200;
    private const int WaitResultAfterPasteMs = 0;
    private const int WaitResultMinBeforeSuccessMs = 2200;
    private const int WalletsToClearPerBatch = 13;

    private static (int X, int Y)? _lastFifthClickPos;
    private static int _successCountThisBatch;
    private static bool _limitDetectedAfterSecond;
    private static bool _passwordDetected;

    /// <summary>시작 전 MainForm 비밀번호 칸에 입력한 값. 지갑 삭제 중 password 창 뜨면 여기 입력.</summary>
    public static string? Password { get; set; }

    public static bool CheckStop() => MainLoop.StopFlag;

    /// <summary>password 이미지 감지 시 RunPasswordFlow 실행 후 true 반환. 모든 검사 지점에서 호출해 브레이크처럼 사용.</summary>
    private static bool CheckPasswordAndHandle()
    {
        if (ChromeImageMatcher.FindImageOnChrome("password", ThResult) == null) return false;
        RunPasswordFlow();
        _passwordDetected = true;
        return true;
    }

    /// <summary>first(또는 생략) → second → third 탐지 대기 → third → fourth → fifth. fromSecond면 first 건너뜀(성공 후 복귀 시).</summary>
    public static bool RunFirstSet(bool fromSecond = false)
    {
        if (CheckStop()) return false;
        if (CheckPasswordAndHandle()) return false;
        if (!fromSecond)
        {
            if (!ClickStep("first")) { LogStepFail("first"); return false; }
            Thread.Sleep(400);
            if (CheckStop() || CheckPasswordAndHandle()) return false;
        }
        if (!ClickStep("second")) { LogStepFail("second"); return false; }
        Thread.Sleep(400);
        if (CheckPasswordAndHandle()) return false;
        if (ChromeImageMatcher.FindImageOnChrome("limit", ThResult) != null)
        {
            _limitDetectedAfterSecond = true;
            return false;
        }
        if (!WaitForThenClick("third", WaitThirdMs)) { LogStepFail("third", timeout: WaitThirdMs); return false; }
        Thread.Sleep(300);
        if (CheckStop() || CheckPasswordAndHandle()) return false;
        if (!ClickStep("fourth")) { LogStepFail("fourth"); return false; }
        Thread.Sleep(300);
        if (CheckStop() || CheckPasswordAndHandle()) return false;
        if (!ClickFifthAndSave()) { LogStepFail("fifth"); return false; }
        Thread.Sleep(400);
        if (CheckPasswordAndHandle()) return false;
        return true;
    }

    private static bool ClickFifthAndSave()
    {
        var pos = ChromeImageMatcher.GetImageClickPosition("fifth", Th);
        if (pos == null) return false;
        _lastFifthClickPos = pos.Value;
        ChromeImageMatcher.ClickAt(pos.Value.X, pos.Value.Y, 0.35);
        return true;
    }

    private static void LogStepFail(string stepName, int timeout = 0)
    {
        try
        {
            var rect = ChromeImageMatcher.GetEdgeCaptureRect();
            string rectStr = rect == null ? "캡처영역 없음(Chrome 미감지)" : $"{rect.Value.Width}x{rect.Value.Height} at ({rect.Value.X},{rect.Value.Y})";
            string msg = timeout > 0
                ? $"[{DateTime.Now:HH:mm:ss}] {stepName}.png 미발견 (대기 {timeout}ms 초과). 캡처={rectStr}"
                : $"[{DateTime.Now:HH:mm:ss}] {stepName}.png 미발견. 캡처={rectStr}";
            var logPath = Path.Combine(ChromeImageMatcher.BaseDir, "trustwallet_detect_log.txt");
            File.AppendAllText(logPath, msg + Environment.NewLine, System.Text.Encoding.UTF8);
            if (rect != null)
            {
                var clamped = ChromeImageMatcher.ClampToScreen(rect.Value);
                using var mat = ChromeImageMatcher.CaptureScreen(clamped);
                var debugPath = Path.Combine(ChromeImageMatcher.BaseDir, "debug", $"trustwallet_failed_{stepName}.png");
                Directory.CreateDirectory(Path.GetDirectoryName(debugPath)!);
                Cv2.ImWrite(debugPath, mat);
            }
        }
        catch { }
    }

    private static bool ClickStep(string name)
    {
        return ChromeImageMatcher.ClickImage(name, Th, 0.35);
    }

    private static bool WaitForThenClick(string name, int timeoutMs)
    {
        int elapsed = 0;
        while (elapsed < timeoutMs && !CheckStop())
        {
            if (CheckPasswordAndHandle()) return false;
            var pos = ChromeImageMatcher.FindImageOnChrome(name, Th);
            if (pos != null)
            {
                ChromeImageMatcher.ClickImage(name, Th, 0.35);
                return true;
            }
            Thread.Sleep(PollIntervalMs);
            elapsed += PollIntervalMs;
        }
        return false;
    }

    /// <summary>기입 후 항상 error.png 검사 → 있으면 fail 처리. 없으면 일정 시간 후 successbutton 클릭. password 감지 시 처리 후 null 반환.</summary>
    /// <remarks>반환: true=성공, false=중지, null=password 감지되어 처리함(first 복귀).</remarks>
    public static bool? RunFailSuccessLoop(
        Func<string[]> getNextMnemonic,
        Action<string[]> onSuccess)
    {
        string[] mnemonic = getNextMnemonic();
        InputHelper.SetClipboardText(string.Join(" ", mnemonic));
        Thread.Sleep(80);
        InputHelper.HotkeyCtrlV();
        Thread.Sleep(WaitResultAfterPasteMs);

        var resultPhaseStart = DateTime.Now;

        while (!CheckStop())
        {
            if (CheckPasswordAndHandle()) return null;

            bool foundError = ChromeImageMatcher.FindImageOnChrome("error", ThResult) != null;

            if (foundError)
            {
                if (_lastFifthClickPos is { } saved)
                {
                    ChromeImageMatcher.ClickAt(saved.X, saved.Y, 0.35);
                }
                else
                {
                    ChromeImageMatcher.ClickImage("fifth", Th, 0.35);
                    var p = ChromeImageMatcher.GetImageClickPosition("fifth", Th);
                    if (p != null) _lastFifthClickPos = p.Value;
                }
                Thread.Sleep(250);
                InputHelper.HotkeyCtrlA();
                Thread.Sleep(60);
                mnemonic = getNextMnemonic();
                InputHelper.SetClipboardText(string.Join(" ", mnemonic));
                Thread.Sleep(80);
                InputHelper.HotkeyCtrlV();
                resultPhaseStart = DateTime.Now;
                continue;
            }

            int elapsed = (int)(DateTime.Now - resultPhaseStart).TotalMilliseconds;
            if (elapsed >= WaitResultMinBeforeSuccessMs)
            {
                ChromeImageMatcher.ClickImage("successbutton", ThResult, 0.4);
                Thread.Sleep(1200);
                if (!WaitForThenClick("success", 5000))
                {
                    if (ChromeImageMatcher.Debug) AppLog.WriteLine("  [TrustWallet] success.png 미발견");
                }
                onSuccess(mnemonic);
                _successCountThisBatch++;
                Thread.Sleep(400);
                InputHelper.HotkeyCtrlW();
                Thread.Sleep(600);
                return true;
            }

            Thread.Sleep(PollIntervalMs);
        }

        return false;
    }

    /// <summary>비밀번호 창 처리: password 클릭 → 비번 입력 → password2 → selection1~4 → first 복귀.</summary>
    private static void RunPasswordFlow()
    {
        if (CheckStop()) return;
        if (!ChromeImageMatcher.ClickImage("password", Th, 0.35)) return;
        Thread.Sleep(300);
        string pwd = Password ?? "";
        if (!string.IsNullOrEmpty(pwd))
            InputHelper.TypeText(pwd, 15);
        Thread.Sleep(200);
        if (!ChromeImageMatcher.ClickImage("password2", Th, 0.35)) return;
        Thread.Sleep(400);
        ChromeImageMatcher.ClickImage("selection1", Th, 0.3);
        Thread.Sleep(250);
        ChromeImageMatcher.ClickImage("selection2", Th, 0.3);
        Thread.Sleep(250);
        ChromeImageMatcher.ClickImage("selection3", Th, 0.3);
        Thread.Sleep(250);
        ChromeImageMatcher.ClickImage("selection4", Th, 0.3);
        Thread.Sleep(500);
    }

    /// <summary>13개 지갑 삭제: manage → (manage2, manage3, manage4) x 13회. 중간에 password 감지 시 RunPasswordFlow 후 복귀.</summary>
    private static void RunManageClear29()
    {
        if (CheckStop()) return;
        if (!ChromeImageMatcher.ClickImage("manage", Th, 0.4)) return;
        Thread.Sleep(800);
        for (int i = 0; i < WalletsToClearPerBatch && !CheckStop(); i++)
        {
            if (ChromeImageMatcher.FindImageOnChrome("password", ThResult) != null)
            {
                RunPasswordFlow();
                return;
            }
            ChromeImageMatcher.ClickImage("manage2", Th, 0.3);
            Thread.Sleep(300);
            ChromeImageMatcher.ClickImage("manage3", Th, 0.3);
            Thread.Sleep(300);
            ChromeImageMatcher.ClickImage("manage4", Th, 0.3);
            Thread.Sleep(400);
        }
        if (CheckStop()) return;
        if (ChromeImageMatcher.FindImageOnChrome("password", ThResult) != null)
        {
            RunPasswordFlow();
            return;
        }
        ChromeImageMatcher.ClickImage("manage5", Th, 0.4);
        Thread.Sleep(600);
    }

    /// <summary>전체 러너: 성공 13회 또는 second 후 limit 감지 시 manage 13회 → first부터.</summary>
    public static void Run(Func<string[]> getNextMnemonic, Action<string[]> onSuccess)
    {
        bool startFromSecond = false;
        while (!CheckStop())
        {
            if (!RunFirstSet(fromSecond: startFromSecond))
            {
                if (CheckStop()) return;
                if (_passwordDetected)
                {
                    _passwordDetected = false;
                    startFromSecond = false;
                    continue;
                }
                if (_limitDetectedAfterSecond)
                {
                    RunManageClear29();
                    _limitDetectedAfterSecond = false;
                    _successCountThisBatch = 0;
                    startFromSecond = false;
                    continue;
                }
                Thread.Sleep(500);
                startFromSecond = false;
                continue;
            }

            bool? loopResult = RunFailSuccessLoop(getNextMnemonic, onSuccess);
            if (loopResult == null)
            {
                _passwordDetected = false;
                startFromSecond = false;
                continue;
            }
            if (loopResult == true)
            {
                if (_successCountThisBatch >= WalletsToClearPerBatch)
                {
                    RunManageClear29();
                    _successCountThisBatch = 0;
                    startFromSecond = false;
                }
                else
                    startFromSecond = true;
            }
            else
                startFromSecond = false;
        }
    }
}
