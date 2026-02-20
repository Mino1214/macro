namespace MainPatchedImproved;

public static class MainLoop
{
    public static volatile bool StopFlag;

    private static readonly string[] MainpageDetect = { "mainpage", "mainpage_menu", "mainpage_add", "mainpage_addwallet", "mainpage_getwallet", "mnemonic_button", "next" };
    /// <summary>First Set: 3→4→5→6→7 한 세트 (햄버거→addwallet→getwallet→mnemonic→next)</summary>
    private static readonly string[] FirstSetClickOrder = { "mainpage_menu", "mainpage_addwallet", "mainpage_getwallet", "mnemonic_button", "next" };
    private static readonly string[] MainpageClickOrder = { "mainpage_menu", "mainpage_add", "mainpage_getwallet", "next" };
    private static readonly string[] SlotImageNames = Enumerable.Range(1, 12).Select(i => i.ToString()).ToArray();
    /// <summary>Second Set: 1~12 전부 (사번·오번 포함)</summary>
    private static readonly string[] SecondSetSlots = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12" };
    private static readonly int[] SecondSetWordIndices = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };

    /// <summary>1회차 때 찾은 슬롯 1~12 클릭 좌표. 2회차부터 이 위치로 기입.</summary>
    private static readonly (int X, int Y)?[] SlotPositions = new (int X, int Y)?[12];

    /// <summary>1회차 때 찾은 First Set(햄버거 등) 클릭 좌표. 2회차부터는 인식 없이 이 좌표로 클릭.</summary>
    private static readonly (int X, int Y)?[] FirstSetPositions = new (int X, int Y)?[5];

    /// <summary>단계별 테스트용: First Set 단계 이름 목록</summary>
    public static string[] GetFirstSetStepNames() => (string[])FirstSetClickOrder.Clone();
    /// <summary>단계별 테스트용: Second Set 슬롯 이름 목록</summary>
    public static string[] GetSecondSetSlotNames() => (string[])SecondSetSlots.Clone();
    /// <summary>단계별 테스트용: Second Set 슬롯별 words12 인덱스</summary>
    public static int[] GetSecondSetWordIndices() => (int[])SecondSetWordIndices.Clone();

    public static bool CheckStop() => StopFlag;

    /// <summary>
    /// 상태 감지: mnemonicpage / mainpage / null
    /// 캡처 대상: UseForegroundWindow면 포커스 창, 아니면 Edge. 그 창에서 이미지로 판별.
    /// </summary>
    public static string? DetectState()
    {
        if (CheckStop()) return null;
        if (ChromeImageMatcher.Debug) ChromeImageMatcher.SaveDebugCapture("detect_state");

        // 캡처할 창이 없으면 상태 판별 불가 (포커스 창 모드면 포커스 창, 아니면 Edge)
        if (ChromeImageMatcher.UseForegroundWindow)
        {
            if (EdgeHelper.GetForegroundWindowRegion() == null) return null;
        }
        else
        {
            if (EdgeHelper.GetEdgeRegion() == null) return null;
        }

        // 1) 니모닉 입력 화면이면 mnemonicpage
        var mnemonic = ChromeImageMatcher.FindImageOnChrome("mnemonicpage", ChromeImageMatcher.StateMatchThreshold);
        if (mnemonic != null) return "mnemonicpage";

        // 2) 메인/메뉴/버튼 등이 보이면 mainpage
        foreach (var step in MainpageDetect)
        {
            var res = ChromeImageMatcher.FindImageOnChrome(step, ChromeImageMatcher.StateMatchThreshold);
            if (res != null)
            {
                if (ChromeImageMatcher.Debug) AppLog.WriteLine($"  [상태감지] {step} 발견 (신뢰도: {res.Value.Confidence:F3})");
                return "mainpage";
            }
        }
        if (ChromeImageMatcher.Debug) AppLog.WriteLine("  [상태감지] mnemonicpage·mainpage 모두 미발견 (pic/kr/ 템플릿과 화면 확인)");
        return null;
    }

    /// <summary>
    /// 메인페이지에서 메뉴→추가→지갑가져오기→다음 순서로 클릭 (최대 maxRounds)
    /// </summary>
    public static bool RunMainpageSequence(int maxRounds = 20)
    {
        if (ChromeImageMatcher.Debug) ChromeImageMatcher.SaveDebugCapture("mainpage_sequence_start");

        for (int round = 0; round < maxRounds; round++)
        {
            if (CheckStop()) return false;
            if (ChromeImageMatcher.Debug) ChromeImageMatcher.SaveDebugCapture($"mainpage_round_{round + 1}");

            bool clickedAny = false;
            foreach (var step in MainpageClickOrder)
            {
                if (CheckStop()) return false;
                if (ChromeImageMatcher.ClickImage(step, ChromeImageMatcher.StateMatchThreshold, 0.4))
                {
                    if (ChromeImageMatcher.Debug) AppLog.WriteLine($"  [메인페이지] 클릭: {step}");
                    clickedAny = true;
                    break;
                }
            }

            if (!clickedAny)
            {
                if (ChromeImageMatcher.Debug) AppLog.WriteLine("  [메인페이지] 더 이상 클릭할 항목 없음 → 니모닉 페이지로 간주");
                return true;
            }
            Thread.Sleep(600);
        }
        if (ChromeImageMatcher.Debug) AppLog.WriteLine("  [메인페이지] max_rounds 도달");
        return true;
    }

    /// <summary>
    /// 3 햄버거 / 4,5,6,7: 1회차에는 인식한 위치를 기록하고, 2회차부터는 기록한 좌표로만 클릭 (간헐적 인식 오류 방지).
    /// </summary>
    public static bool RunFirstSetStep(string imageName)
    {
        int idx = Array.IndexOf(FirstSetClickOrder, imageName);
        if (idx < 0) return false;

        if (FirstSetPositions[idx] is { } saved)
        {
            Thread.Sleep(200); // UI 안정 대기
            ChromeImageMatcher.ClickAt(saved.X, saved.Y, 0.4);
            if (ChromeImageMatcher.Debug) AppLog.WriteLine($"  [First Set] {imageName} 캐시 좌표로 클릭 ({saved.X}, {saved.Y})");
            return true;
        }

        var pos = ChromeImageMatcher.GetCaptureAndDetectClickPosition(imageName, 0.7);
        if (pos == null) return false;
        FirstSetPositions[idx] = pos.Value;
        Thread.Sleep(200);
        ChromeImageMatcher.ClickAt(pos.Value.X, pos.Value.Y, 0.4);
        if (ChromeImageMatcher.Debug) AppLog.WriteLine($"  [First Set] {imageName} 인식 후 좌표 기록·클릭 ({pos.Value.X}, {pos.Value.Y})");
        return true;
    }

    /// <summary>
    /// First Set: 3→4→5→6→7. RunFirstSetStep()만 연속 호출 (버튼이 쓰는 메소드 그대로).
    /// </summary>
    public static void RunFirstSet()
    {
        int n = FirstSetClickOrder.Length; 
        for (int i = 0; i < n; i++)
        {
            if (CheckStop()) return;
            string step = FirstSetClickOrder[i];
            bool ok = RunFirstSetStep(step);
            if (ChromeImageMatcher.Debug) AppLog.WriteLine(ok ? $"  {i + 1}/{n} {step} 클릭함" : $"  {i + 1}/{n} {step} 못 찾음");
            Thread.Sleep(650); // 단계마다 화면 전환·UI 반영 대기
        }
    }

    /// <summary>
    /// Second Set 1회차: 시드문구를 클립보드에 넣고, 1번 슬롯만 인식·클릭 후 Ctrl+V → next. (2회차부터는 RunSecondSetRetry에서 1번만 사용)
    /// </summary>
    public static void RunSecondSet(string[] words12, bool useCtrlA)
    {
        if (words12.Length < 12) return;
        string phrase = string.Join(" ", words12);
        InputHelper.SetClipboardText(phrase);
        Thread.Sleep(150); // 클립보드 설정 완료 대기

        if (CheckStop()) return;
        Thread.Sleep(300); // 니모닉 화면 안정 대기
        // 1번 슬롯만 찾아서 위치 기록 후 클릭
        var pos = ChromeImageMatcher.GetImageClickPosition("1", ChromeImageMatcher.StateMatchThreshold);
        if (pos != null)
        {
            SlotPositions[0] = pos.Value;
            ChromeImageMatcher.ClickAt(pos.Value.X, pos.Value.Y, 0.4);
        }
        else if (SlotPositions[0] is { } savedPos)
        {
            ChromeImageMatcher.ClickAt(savedPos.X, savedPos.Y, 0.4);
        }
        else
        {
            if (ChromeImageMatcher.ClickImage("1", ChromeImageMatcher.StateMatchThreshold, 0.4))
            {
                var again = ChromeImageMatcher.GetImageClickPosition("1", ChromeImageMatcher.StateMatchThreshold);
                if (again != null) SlotPositions[0] = again.Value;
            }
            else if (ChromeImageMatcher.Debug) AppLog.WriteLine("  ⚠ Second Set 슬롯 1 이미지 못 찾음");
        }
        Thread.Sleep(550); // 입력란 포커스 대기 (클릭 후 입력란 활성화 시간)
        InputHelper.HotkeyCtrlV();
        Thread.Sleep(400); // 붙여넣기 완료 대기
        if (CheckStop()) return;
        ChromeImageMatcher.ClickImage("next", ChromeImageMatcher.StateMatchThreshold, 0.45);
        Thread.Sleep(600);
        if (ChromeImageMatcher.Debug) AppLog.WriteLine("  [Second Set] 1회차 완료 (1번 클릭 + Ctrl+V)");
    }

    /// <summary>
    /// 2회차용: 1번만 캐시 좌표로 클릭 → Ctrl+A → Ctrl+V → 시드문구 클립보드에 복사 → next. (타이핑 없이 붙여넣기로 빠르게)
    /// </summary>
    public static bool RunSecondSetRetry(string[] words12)
    {
        if (words12.Length < 12) return false;
        if (SlotPositions[0] is not { } pos)
        {
            if (ChromeImageMatcher.Debug) AppLog.WriteLine("  ⚠ 2회차 슬롯 1 위치 없음(1회차에서 못 찾음)");
            return false;
        }
        if (CheckStop()) return false;
        string phrase = string.Join(" ", words12);
        InputHelper.SetClipboardText(phrase); // 먼저 클립보드 설정
        Thread.Sleep(150); // 클립보드 설정 완료 대기
        Thread.Sleep(250); // 재시도 전 화면 안정 대기
        ChromeImageMatcher.ClickAt(pos.X, pos.Y, 0.4);
        Thread.Sleep(550); // 입력란 포커스 대기
        InputHelper.HotkeyCtrlA();
        Thread.Sleep(120);
        InputHelper.HotkeyCtrlV();
        Thread.Sleep(400); // 붙여넣기 완료 대기
        if (CheckStop()) return false;
        ChromeImageMatcher.ClickImage("next", ChromeImageMatcher.StateMatchThreshold, 0.45);
        Thread.Sleep(3000);
        return HasSuccess();
    }

    /// <summary>success 이미지 존재 여부</summary>
    public static bool HasSuccess()
    {
        var res = ChromeImageMatcher.FindImageOnChrome("success", ChromeImageMatcher.StateMatchThreshold);
        return res != null;
    }

    public static bool HasError()
    {
        foreach (var name in new[] { "error", "mainpageerror" })
        {
            var res = ChromeImageMatcher.FindImageOnChrome(name, 0.5);
            if (res != null)
            {
                if (ChromeImageMatcher.Debug) AppLog.WriteLine($"  🔴 에러 이미지 감지: {name} (신뢰도: {res.Value.Confidence:F3})");
                return true;
            }
        }
        return false;
    }

    public static void FillSlots(string[] words12, bool isFirst)
    {
        for (int i = 0; i < words12.Length; i++)
        {
            if (CheckStop()) return;
            string slotName = SlotImageNames[i];
            if (!ChromeImageMatcher.ClickImage(slotName, ChromeImageMatcher.StateMatchThreshold, 0.05))
            {
                if (ChromeImageMatcher.Debug) AppLog.WriteLine($"  ⚠ 슬롯 {i + 1} 이미지 못 찾음, 입력만 시도");
            }
            if (!isFirst)
            {
                InputHelper.HotkeyCtrlA();
                Thread.Sleep(20);
            }
            InputHelper.TypeText(words12[i], 20);
            Thread.Sleep(50);
        }
    }

    public static List<string> LoadWords()
    {
        var path = Path.Combine(ChromeImageMatcher.BaseDir, "wordlist.txt");
        if (!File.Exists(path)) throw new FileNotFoundException($"wordlist.txt 를 찾을 수 없습니다: {path}");
        return File.ReadAllLines(path, System.Text.Encoding.UTF8)
            .Select(w => w.Trim())
            .Where(w => w.Length > 0 && !w.StartsWith("#"))
            .ToList();
    }

    private static int _random12CallCount;

    public static string[] Random12(List<string> words)
    {
        if (words.Count < 12) throw new InvalidOperationException("wordlist.txt 에 12개 이상 단어가 필요합니다.");
        // 호출마다 반드시 다른 시드 (카운터 + 고해상도 시간) → 매 사이클 다른 니모닉
        int seed = unchecked(Environment.TickCount + (int)System.Diagnostics.Stopwatch.GetTimestamp() + (++_random12CallCount));
        var rng = new Random(seed);
        return words.OrderBy(_ => rng.Next()).Take(12).ToArray();
    }
}
