using System.Drawing;

namespace MainPatchedImproved;

/// <summary>
/// 단계별 테스트: 1) Edge 캡처 → 2) 메인페이지 감지 → 3) 햄버거 메뉴 감지
/// </summary>
public partial class StepTestForm : Form
{
    private System.Windows.Forms.Timer? _mouseTimer;

    public StepTestForm()
    {
        InitializeComponent();
        _mouseTimer = new System.Windows.Forms.Timer { Interval = 100 };
        _mouseTimer.Tick += (_, _) =>
        {
            var p = Cursor.Position;
            labelMouse.Text = $"마우스: ({p.X}, {p.Y})";
        };
        _mouseTimer.Start();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _mouseTimer?.Stop();
        _mouseTimer?.Dispose();
        base.OnFormClosed(e);
    }

    private void ButtonLogCoord_Click(object? sender, EventArgs e)
    {
        var p = Cursor.Position;
        AppendLog($"  📍 마우스 좌표: ({p.X}, {p.Y})");
    }

    /// <summary>원클릭 테스트: 지금 보이는 창(포커스된 창) 기준으로 바로 찾기/클릭. 포커스 안 옮김 → 지갑 안 꺼짐.</summary>
    private void ButtonOneClickTest_Click(object? sender, EventArgs e)
    {
        bool noClick = checkBoxNoClick.Checked;
        // 원클릭은 항상 현 상태(포커스된 창)만 사용. Edge로 포커스 안 뺏음.
        ChromeImageMatcher.UseForegroundWindow = true;
        AppendLog("========== [원클릭 테스트] 현재 창 기준 (포커스 유지) ==========");
        try
        {
            // 1) First Set
            AppendLog("  [1/2] First Set (3→4→5→6→7)...");
            if (noClick)
            {
                foreach (string name in MainLoop.GetFirstSetStepNames())
                {
                    var res = ChromeImageMatcher.FindImageOnChrome(name, 0.7);
                    AppendLog($"    {name}: {(res != null ? "찾음" : "못 찾음")}");
                }
            }
            else
            {
                MainLoop.RunFirstSet();
            }
            System.Threading.Thread.Sleep(800);

            // 2) Second Set
            AppendLog("  [2/2] Second Set (1~12)...");
            var words = MainLoop.LoadWords();
            string[] mnemonic = MainLoop.Random12(words);
            AppendLog($"    니모닉: {string.Join(" ", mnemonic)}");
            if (noClick)
            {
                foreach (string slot in MainLoop.GetSecondSetSlotNames())
                {
                    var res = ChromeImageMatcher.FindImageOnChrome(slot, 0.7);
                    AppendLog($"    슬롯 {slot}: {(res != null ? "찾음" : "못 찾음")}");
                }
            }
            else
            {
                MainLoop.RunSecondSet(mnemonic, useCtrlA: false);
                System.Threading.Thread.Sleep(500);
                if (MainLoop.HasSuccess())
                {
                    AppendLog("    success 발견 → confirm → x 클릭");
                    ChromeImageMatcher.ClickImage("confirm", ChromeImageMatcher.StateMatchThreshold, 0.3);
                    System.Threading.Thread.Sleep(400);
                    ChromeImageMatcher.ClickImage("x", ChromeImageMatcher.StateMatchThreshold, 0.2);
                }
                else
                {
                    AppendLog("    success 미발견 → Ctrl+A 재시도...");
                    MainLoop.RunSecondSet(mnemonic, useCtrlA: true);
                }
            }
            AppendLog("  원클릭 테스트 완료.");
        }
        catch (Exception ex)
        {
            AppendLog($"  ❌ {ex.Message}");
        }
        finally
        {
            ChromeImageMatcher.UseForegroundWindow = false;
        }
        AppendLog("");
    }

    /// <summary>First Set: 3→4→5→6→7. "찾기만"이면 클릭 없이 이미지 찾기만 하고 로그. "포커스 안 함"이면 지갑 창 유지.</summary>
    private void ButtonFirstSet_Click(object? sender, EventArgs e)
    {
        bool noClick = checkBoxNoClick.Checked;
        bool noFocus = checkBoxNoFocus.Checked;
        ChromeImageMatcher.UseForegroundWindow = noFocus;
        AppendLog($"---------- [First Set] 3→4→5→6→7 ({(noClick ? "찾기만" : "클릭 실행")}{(noFocus ? ", 포커스 안 함" : "")}) ----------");
        try
        {
            if (!noFocus)
            {
                EdgeHelper.FocusEdge();
                System.Threading.Thread.Sleep(300);
            }
            string[] steps = MainLoop.GetFirstSetStepNames();
            for (int i = 0; i < steps.Length; i++)
            {
                string name = steps[i];
                if (noClick)
                {
                    var res = ChromeImageMatcher.FindImageOnChrome(name, 0.7);
                    if (res != null)
                        AppendLog($"  {i + 1}/5 {name} 찾음 (신뢰도 {res.Value.Confidence:F2})");
                    else
                        AppendLog($"  {i + 1}/5 {name} 못 찾음");
                }
                else
                {
                    bool ok = ChromeImageMatcher.ClickImage(name, 0.7, 0.15);
                    AppendLog($"  {i + 1}/5 {name} {(ok ? "클릭함" : "못 찾음")}");
                    System.Threading.Thread.Sleep(400);
                }
            }
            AppendLog("  First Set 완료.");
        }
        catch (Exception ex)
        {
            AppendLog($"  ❌ {ex.Message}");
        }
        finally
        {
            ChromeImageMatcher.UseForegroundWindow = false;
        }
        AppendLog("");
    }

    /// <summary>Second Set: 1~12. 루핑=1차 후 success 없으면 Ctrl+A 재시도 1회. "포커스 안 함"이면 지갑 유지.</summary>
    private void ButtonSecondSet_Click(object? sender, EventArgs e)
    {
        bool noClick = checkBoxNoClick.Checked;
        bool noFocus = checkBoxNoFocus.Checked;
        ChromeImageMatcher.UseForegroundWindow = noFocus;
        AppendLog($"---------- [Second Set] 1~12 ({(noClick ? "찾기만" : "클릭+문구+next, 루프재시도")}{(noFocus ? ", 포커스 안 함" : "")}) ----------");
        try
        {
            var words = MainLoop.LoadWords();
            string[] mnemonic = MainLoop.Random12(words);
            string[] slots = MainLoop.GetSecondSetSlotNames();
            int[] wordIdx = MainLoop.GetSecondSetWordIndices();
            AppendLog($"  니모닉 12개: {string.Join(" ", mnemonic)}");
            if (!noFocus)
            {
                EdgeHelper.FocusEdge();
                System.Threading.Thread.Sleep(300);
            }

            if (noClick)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    var res = ChromeImageMatcher.FindImageOnChrome(slots[i], 0.7);
                    AppendLog($"  슬롯 {slots[i]} {(res != null ? "찾음" : "못 찾음")}");
                }
                var nextRes = ChromeImageMatcher.FindImageOnChrome("next", 0.7);
                AppendLog($"  next {(nextRes != null ? "찾음" : "못 찾음")}");
            }
            else
            {
                // 1차: 클릭 → 문구 → next
                AppendLog("  1차: 클릭+문구+next 실행...");
                MainLoop.RunSecondSet(mnemonic, useCtrlA: false);
                System.Threading.Thread.Sleep(500);
                if (MainLoop.HasSuccess())
                {
                    AppendLog("  success 발견 → (실제 자동화에서는 confirm → x 클릭)");
                }
                else
                {
                    AppendLog("  success 미발견 → 2차 재시도 (Ctrl+A 후 문구+next)...");
                    MainLoop.RunSecondSet(mnemonic, useCtrlA: true);
                    System.Threading.Thread.Sleep(300);
                    AppendLog("  2차 완료.");
                }
            }
            AppendLog("  Second Set 완료.");
        }
        catch (Exception ex)
        {
            AppendLog($"  ❌ {ex.Message}");
        }
        finally
        {
            ChromeImageMatcher.UseForegroundWindow = false;
        }
        AppendLog("");
    }

    /// <summary>번호 버튼 클릭 → 해당 이미지 찾아서 클릭 (찾기만/포커스 안 함 적용). 3,4,5,6,7=First Set, 나머지=1~12.png</summary>
    private void ButtonNum_Click(object? sender, EventArgs e)
    {
        if (sender is not Button btn) return;
        string imageName = btn == buttonNum3 ? "mainpage_menu"   // 3번 = 햄버거
            : btn == buttonNum4 ? "mainpage_addwallet"
            : btn == buttonNum5 ? "mainpage_getwallet"
            : btn == buttonNum6 ? "mnemonic_button"
            : btn == buttonNum7 ? "next"
            : (btn.Tag as string) ?? "";
        if (string.IsNullOrEmpty(imageName)) return;

        ChromeImageMatcher.UseForegroundWindow = checkBoxNoFocus.Checked;
        try
        {
        if (checkBoxNoClick.Checked)
        {
            var res = ChromeImageMatcher.FindImageOnChrome(imageName, 0.7);
            if (res != null)
                AppendLog($"  [찾기만] {imageName} 찾음 (신뢰도 {res.Value.Confidence:F2})");
            else
                AppendLog($"  [찾기만] {imageName} 못 찾음");
        }
        else
        {
            bool ok = MainLoop.RunFirstSetStep(imageName);
            if (ok)
                AppendLog($"  [버튼] {imageName} 클릭함");
            else
                AppendLog($"  [버튼] {imageName} 이미지를 찾지 못함");
        }
        }
        finally
        {
            ChromeImageMatcher.UseForegroundWindow = false;
        }
    }

    /// <summary>번호 키 → 해당 이미지 찾아서 클릭. 4=mainpage_addwallet, 5=mainpage_getwallet, 나머지=1~12.png</summary>
    private void StepTestForm_KeyDown(object? sender, KeyEventArgs e)
    {
        // OemPlus는 .NET에 따라 없을 수 있음 → 0xBB(= 키) 사용
        (string keyLabel, string imageName)? map = e.KeyCode switch
        {
            Keys.D1 => ("1", "1"),
            Keys.D2 => ("2", "2"),
            Keys.D3 => ("3", "mainpage_menu"),   // 3번 = 햄버거 메뉴
            Keys.D4 => ("4", "mainpage_addwallet"),   // 4번 (사번)
            Keys.D5 => ("5", "mainpage_getwallet"),   // 5번 (오번)
            Keys.D6 => ("6", "mnemonic_button"),      // 6번 (육번)
            Keys.D7 => ("7", "next"),                 // 7번 (칠번)
            Keys.D8 => ("8", "8"),
            Keys.D9 => ("9", "9"),
            Keys.D0 => ("0", "10"),
            Keys.OemMinus => ("-", "11"),
            (Keys)0xBB => ("=", "12"),   // VK_OEM_PLUS (= 키)
            _ => null
        };
        if (!map.HasValue) return;
        e.Handled = true;
        var (keyLabel, imageName) = map.Value;

        bool ok = ChromeImageMatcher.ClickImage(imageName, 0.7, 0.15);
        if (ok)
            AppendLog($"  [키 {keyLabel}] {imageName} 클릭함");
        else
            AppendLog($"  [키 {keyLabel}] {imageName} 이미지를 찾지 못함");
    }

    private void AppendLog(string text)
    {
        textBoxLog.AppendText(text + Environment.NewLine);
        textBoxLog.ScrollToCaret();
    }

    private void ButtonStep1_Click(object? sender, EventArgs e)
    {
        bool noFocus = checkBoxNoFocus.Checked;
        ChromeImageMatcher.UseForegroundWindow = noFocus;
        AppendLog($"---------- [1] 캡처 ({(noFocus ? "포커스 창" : "Edge")}) ----------");
        try
        {
            if (!noFocus)
            {
                AppendLog("  Edge 포커스 중...");
                EdgeHelper.FocusEdge();
                System.Threading.Thread.Sleep(500);
            }

            // 메인 로직과 동일: GetEdgeCaptureRect() → ClampToScreen → CaptureScreen(화면 복사)
            var rectWithPad = ChromeImageMatcher.GetEdgeCaptureRect();
            if (rectWithPad == null)
            {
                AppendLog(noFocus ? "  ❌ 포커스된 창이 없습니다. 지갑 등을 앞에 두세요." : "  ❌ Edge 창을 찾을 수 없습니다.");
                AppendLog("");
                return;
            }

            var clamped = ChromeImageMatcher.ClampToScreen(rectWithPad.Value);
            AppendLog($"  캡처 영역: x={clamped.X}, y={clamped.Y}, {clamped.Width} x {clamped.Height}");

            using (var mat = ChromeImageMatcher.CaptureScreen(clamped))
            {
                string path = ChromeImageMatcher.SaveToDebugFile(mat, "step1_capture.png");
                AppendLog($"  ✅ 저장: {Path.GetFileName(path)}");
            }

            AppendLog("  → debug 폴더에서 step1_capture.png 확인");
            AppendLog("");
        }
        catch (Exception ex)
        {
            AppendLog($"  ❌ 오류: {ex.Message}");
            AppendLog("");
        }
        finally
        {
            ChromeImageMatcher.UseForegroundWindow = false;
        }
    }

    private void ButtonStep2_Click(object? sender, EventArgs e)
    {
        ChromeImageMatcher.UseForegroundWindow = checkBoxNoFocus.Checked;
        AppendLog("---------- [2] 메인페이지 감지 ----------");
        try
        {
            var rect = ChromeImageMatcher.GetEdgeCaptureRect();
            if (rect == null)
            {
                AppendLog("  ❌ 캡처할 창이 없습니다 (포커스 창 또는 Edge).");
                AppendLog("");
                return;
            }

            var clamped = ChromeImageMatcher.ClampToScreen(rect.Value);
            using var mat = ChromeImageMatcher.CaptureScreen(clamped);
            string templatePath = ChromeImageMatcher.GetTemplatePath("mainpage");

            if (!File.Exists(templatePath))
            {
                AppendLog($"  ❌ 템플릿 없음: pic/kr/mainpage.png");
                ChromeImageMatcher.SaveToDebugFile(mat, "step2_capture_no_template.png");
                AppendLog("");
                return;
            }

            var result = ChromeImageMatcher.FindTemplateMultiscale(mat, templatePath, 0.7);
            string saveName = result != null ? "step2_mainpage_found.png" : "step2_mainpage_not_found.png";
            ChromeImageMatcher.SaveToDebugFile(mat, saveName);

            if (result != null)
            {
                var ((x, y), confidence, scale) = result.Value;
                AppendLog($"  ✅ 메인페이지 감지됨");
                AppendLog($"     위치: ({x}, {y}), 신뢰도: {confidence:F3}, 스케일: {scale:F2}x");
            }
            else
            {
                AppendLog("  ❌ 메인페이지 미감지 (템플릿과 매칭 안 됨)");
            }

            AppendLog($"  캡처 저장: {saveName}");
            AppendLog("");
        }
        catch (Exception ex)
        {
            AppendLog($"  ❌ 오류: {ex.Message}");
            AppendLog("");
        }
        finally
        {
            ChromeImageMatcher.UseForegroundWindow = false;
        }
    }

    private void ButtonStep3_Click(object? sender, EventArgs e)
    {
        ChromeImageMatcher.UseForegroundWindow = checkBoxNoFocus.Checked;
        AppendLog("---------- [3] 햄버거 메뉴 감지 ----------");
        try
        {
            var (mat, _, result) = ChromeImageMatcher.CaptureAndDetectTemplate("mainpage_menu", 0.7);
            if (mat == null)
            {
                AppendLog("  ❌ 캡처할 창이 없습니다 (포커스 창 또는 Edge).");
                AppendLog("");
                return;
            }
            using (mat)
            {
                if (result == null && !File.Exists(ChromeImageMatcher.GetTemplatePath("mainpage_menu")))
                {
                    AppendLog("  ❌ 템플릿 없음: pic/kr/mainpage_menu.png");
                    ChromeImageMatcher.SaveToDebugFile(mat, "step3_capture_no_template.png");
                }
                else
                {
                    string saveName = result != null ? "step3_hamburger_found.png" : "step3_hamburger_not_found.png";
                    ChromeImageMatcher.SaveToDebugFile(mat, saveName);
                    if (result != null)
                    {
                        var ((x, y), confidence, scale) = result.Value;
                        AppendLog($"  ✅ 햄버거 메뉴 감지됨");
                        AppendLog($"     위치: ({x}, {y}), 신뢰도: {confidence:F3}, 스케일: {scale:F2}x");
                    }
                    else
                        AppendLog("  ❌ 햄버거 메뉴 미감지 (mainpage_menu 템플릿과 매칭 안 됨)");
                    AppendLog($"  캡처 저장: {saveName}");
                }
            }
            AppendLog("");
        }
        catch (Exception ex)
        {
            AppendLog($"  ❌ 오류: {ex.Message}");
            AppendLog("");
        }
        finally
        {
            ChromeImageMatcher.UseForegroundWindow = false;
        }
    }
}
