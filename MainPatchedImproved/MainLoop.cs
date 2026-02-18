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
    /// 3 햄버거 / 4,5,6,7: [3] 햄버거 메뉴 감지와 동일한 방식(CaptureAndDetectTemplate)으로 찾고 클릭.
    /// </summary>
    public static bool RunFirstSetStep(string imageName)
    {
        return ChromeImageMatcher.ClickFromCaptureAndDetect(imageName, 0.7, 0.15);
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
            Thread.Sleep(400);
        }
    }

    /// <summary>
    /// Second Set: 1~12 각 슬롯 클릭 → (선택) Ctrl+A → 문구 기입. 모두 채운 뒤 마지막에 next 한 번.
    /// 1회차 때 슬롯 위치를 SlotPositions에 저장해 두고, 2회차부터는 그 좌표로 클릭.
    /// </summary>
    public static void RunSecondSet(string[] words12, bool useCtrlA)
    {
        if (words12.Length < 12) return;
        for (int i = 0; i < SecondSetSlots.Length; i++)
        {
            if (CheckStop()) return;
            string slot = SecondSetSlots[i];
            int wordIdx = SecondSetWordIndices[i];
            string word = words12[wordIdx];
            var pos = ChromeImageMatcher.GetImageClickPosition(slot, ChromeImageMatcher.StateMatchThreshold);
            if (pos != null)
            {
                SlotPositions[i] = pos.Value;
                ChromeImageMatcher.ClickAt(pos.Value.X, pos.Value.Y, 0.1);
            }
            else if (SlotPositions[i] is { } savedPos)
            {
                // 이미지 못 찾아도 이전에 기억한 좌표로 클릭 (한 번 저장하면 유지)
                ChromeImageMatcher.ClickAt(savedPos.X, savedPos.Y, 0.1);
            }
            else
            {
                if (ChromeImageMatcher.ClickImage(slot, ChromeImageMatcher.StateMatchThreshold, 0.1))
                {
                    var again = ChromeImageMatcher.GetImageClickPosition(slot, ChromeImageMatcher.StateMatchThreshold);
                    if (again != null) SlotPositions[i] = again.Value;
                }
                else if (ChromeImageMatcher.Debug) AppLog.WriteLine($"  ⚠ Second Set 슬롯 {slot} 이미지 못 찾음 (좌표도 없음)");
            }
            if (useCtrlA)
            {
                InputHelper.HotkeyCtrlA();
                Thread.Sleep(30);
            }
            InputHelper.TypeText(word, 20);
            Thread.Sleep(80);
        }
        if (CheckStop()) return;
        ChromeImageMatcher.ClickImage("next", ChromeImageMatcher.StateMatchThreshold, 0.2);
        Thread.Sleep(300);
        if (ChromeImageMatcher.Debug) AppLog.WriteLine($"  [Second Set] 완료 (useCtrlA={useCtrlA})");
    }

    /// <summary>
    /// 2회차용: 1회차 때 저장한 SlotPositions로 1~12 전부 채운 뒤 마지막에 next 한 번 (1회차와 동일한 방식).
    /// </summary>
    public static bool RunSecondSetRetry(string[] words12)
    {
        if (words12.Length < 12) return false;
        for (int i = 0; i < SecondSetSlots.Length; i++)
        {
            if (CheckStop()) return false;
            if (SlotPositions[i] is not { } pos)
            {
                if (ChromeImageMatcher.Debug) AppLog.WriteLine($"  ⚠ 2회차 슬롯 {SecondSetSlots[i]} 위치 없음(1회차에서 못 찾음) → 생략");
                continue;
            }
            string word = words12[SecondSetWordIndices[i]];
            ChromeImageMatcher.ClickAt(pos.X, pos.Y, 0.1);
            Thread.Sleep(200); // 입력란 포커스 대기
            InputHelper.HotkeyCtrlA();
            Thread.Sleep(30);
            InputHelper.TypeText(word, 20);
            Thread.Sleep(80);
        }
        if (CheckStop()) return false;
        ChromeImageMatcher.ClickImage("next", ChromeImageMatcher.StateMatchThreshold, 0.2);
        Thread.Sleep(3000); // success 화면 뜰 때까지 3초 대기 후 체크
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
