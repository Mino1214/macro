using System.Drawing;
using System.Drawing.Imaging;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace MainPatchedImproved;

/// <summary>
/// main_patched_improved.py: 크롬 영역 캡처 + 멀티스케일 그레이 매칭 + 클릭
/// </summary>
public static class ChromeImageMatcher
{
    /// <summary>실행 파일(.exe)이 있는 폴더. Single-file 배포 시 Assembly.Location은 빈 문자열이므로 BaseDirectory 사용.</summary>
    public static readonly string ExeDir = (Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory ?? AppDomain.CurrentDomain.BaseDirectory ?? ".").TrimEnd(Path.DirectorySeparatorChar);
    /// <summary>리소스/설정 저장 폴더 (exe 옆 data). wordlist, pic, logo, server_url 등</summary>
    public static readonly string BaseDir = Path.Combine(ExeDir, "data");
    public static readonly string PicsKr = Path.Combine(BaseDir, "pic", "kr");
    public const int ChromeTopPadding = 120;
    public const double StateMatchThreshold = 0.8;

    private static readonly double[] ScaleRange = { 0.6, 0.7, 0.8, 0.9, 1.0, 1.1, 1.2, 1.3, 1.4, 1.5 };

    public static bool Debug { get; set; } = true;
    /// <summary>true면 Edge 대신 현재 포커스된 창(지갑 팝업 등) 기준으로 캡처/클릭. 포커스를 안 뺏어서 지갑이 안 꺼짐.</summary>
    public static bool UseForegroundWindow { get; set; } = false;
    private static int _debugCounter;

    private static string PicPath(string name)
    {
        return name.EndsWith(".png") ? Path.Combine(PicsKr, name) : Path.Combine(PicsKr, name + ".png");
    }

    /// <summary>단계 테스트용: 템플릿 파일 경로</summary>
    public static string GetTemplatePath(string name) => PicPath(name);

    /// <summary>단계 테스트용: Edge 창 그대로 (패딩 없음). null이면 Edge 미감지</summary>
    public static Rectangle? GetEdgeExactRect()
    {
        return EdgeHelper.GetEdgeRegion();
    }

    /// <summary>캡처할 창 영역. UseForegroundWindow면 포커스 창(없으면 우리 창=시작 클릭 직후→Edge로 fallback), 아니면 Edge.</summary>
    public static Rectangle? GetEdgeCaptureRect()
    {
        if (UseForegroundWindow)
        {
            var fg = EdgeHelper.GetForegroundWindowRegion();
            if (fg != null) return fg.Value;
            // 시작 버튼 누르면 우리 창이 포커스돼서 null → Edge 영역으로 캡처 (지갑/Edge 그 위치에 있으면 잡힘)
            var edgeFallback = EdgeHelper.GetEdgeRegion();
            if (edgeFallback != null)
            {
                var rect = edgeFallback.Value;
                int padF = ChromeTopPadding;
                int capYF = Math.Max(0, rect.Y - padF);
                int capHF = (rect.Y >= padF) ? (rect.Height + padF) : (rect.Height + rect.Y);
                return new Rectangle(rect.X, capYF, rect.Width, capHF);
            }
            return null;
        }
        var edge = EdgeHelper.GetEdgeRegion();
        if (edge == null) return null;
        var r = edge.Value;
        int cy = r.Y, ch = r.Height, pad = ChromeTopPadding;
        int capY = Math.Max(0, cy - pad);
        int capH = (cy >= pad) ? (ch + pad) : (ch + cy);
        return new Rectangle(r.X, capY, r.Width, capH);
    }

    /// <summary>화면 밖으로 나가지 않도록 영역 클램프</summary>
    public static Rectangle ClampToScreen(Rectangle r)
    {
        int vx = SystemInformation.VirtualScreen.X;
        int vy = SystemInformation.VirtualScreen.Y;
        int vw = SystemInformation.VirtualScreen.Width;
        int vh = SystemInformation.VirtualScreen.Height;
        int x = Math.Clamp(r.X, vx, vx + vw - 1);
        int y = Math.Clamp(r.Y, vy, vy + vh - 1);
        int w = Math.Max(1, Math.Min(r.Width, vx + vw - x));
        int h = Math.Max(1, Math.Min(r.Height, vy + vh - y));
        return new Rectangle(x, y, w, h);
    }

    /// <summary>
    /// 화면 영역 캡처 → BGR Mat
    /// </summary>
    public static Mat CaptureScreen(Rectangle? region = null)
    {
        var bounds = region ?? new Rectangle(
            SystemInformation.VirtualScreen.X, SystemInformation.VirtualScreen.Y,
            SystemInformation.VirtualScreen.Width, SystemInformation.VirtualScreen.Height);

        using var bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
            g.CopyFromScreen(bounds.Location, System.Drawing.Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);

        var mat = BitmapConverter.ToMat(bmp);
        Cv2.CvtColor(mat, mat, ColorConversionCodes.BGRA2BGR);
        return mat;
    }

    /// <summary>
    /// 멀티스케일 그레이 매칭. Returns (relative x,y), confidence, scale or null
    /// </summary>
    public static ((int X, int Y) Rel, double Confidence, double Scale)? FindTemplateMultiscale(
        Mat screenImg, string templatePath, double threshold = 0.7, double[]? scales = null)
    {
        scales ??= ScaleRange;
        if (!File.Exists(templatePath)) return null;

        using var templateBgr = Cv2.ImRead(templatePath);
        if (templateBgr.Empty()) return null;

        using var screenGray = new Mat();
        using var templateGray = new Mat();
        Cv2.CvtColor(screenImg, screenGray, ColorConversionCodes.BGR2GRAY);
        Cv2.CvtColor(templateBgr, templateGray, ColorConversionCodes.BGR2GRAY);

        int sh = screenGray.Height, sw = screenGray.Width;
        int th0 = templateGray.Height, tw0 = templateGray.Width;

        double bestConfidence = 0.0;
        OpenCvSharp.Point? bestLoc = null;
        double bestScale = 1.0;

        foreach (double scale in scales)
        {
            if (scale <= 0) continue;
            int newW = (int)(tw0 * scale);
            int newH = (int)(th0 * scale);
            if (newW < 8 || newH < 8 || newW > sw || newH > sh) continue;

            using var resized = new Mat();
            Cv2.Resize(templateGray, resized, new OpenCvSharp.Size(newW, newH), 0, 0, InterpolationFlags.Area);
            using var result = new Mat();
            Cv2.MatchTemplate(screenGray, resized, result, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out OpenCvSharp.Point maxLoc);

            if (maxVal > bestConfidence)
            {
                bestConfidence = maxVal;
                bestLoc = maxLoc;
                bestScale = scale;
            }
        }

        if (bestLoc == null || bestConfidence < threshold) return null;
        return ((bestLoc.Value.X, bestLoc.Value.Y), bestConfidence, bestScale);
    }

    /// <summary>
    /// 크롬 영역에서 이미지 찾기. 반환: (절대 픽셀 x,y), confidence, scale
    /// </summary>
    public static ((int X, int Y) Abs, double Confidence, double Scale)? FindImageOnChrome(string name, double threshold = 0.7)
    {
        var path = PicPath(name);
        if (!File.Exists(path))
        {
            if (Debug) AppLog.WriteLine($"  ⚠ 이미지 없음: {path}");
            return null;
        }

        // 3. 햄버거 메뉴 감지와 동일: GetEdgeCaptureRect() → 화면 복사(CopyFromScreen)만 사용. PrintWindow은 Chromium에서 검정 나옴.
        Rectangle? captureRectNullable = GetEdgeCaptureRect();
        if (captureRectNullable == null) return null;
        var captureRect = ClampToScreen(captureRectNullable.Value);
        int capLeft = captureRect.X, capTop = captureRect.Y, capW = captureRect.Width, capHeight = captureRect.Height;
        Mat fullScreen = CaptureScreen(captureRect);

        if (capW <= 10 || capHeight <= 10)
        {
            if (Debug) AppLog.WriteLine($"  ⚠ 캡처 영역이 너무 작음: {capW}x{capHeight}");
            fullScreen?.Dispose();
            return null;
        }

        using (fullScreen)
        {
            if (Debug && name != "success")
                SaveDebugImage(fullScreen, $"edge_crop_{name}");

            var result = FindTemplateMultiscale(fullScreen, path, threshold);
            if (result == null) return null;

            var ((relX, relY), confidence, scale) = result.Value;
            int absX = capLeft + relX;
            int absY = capTop + relY;

            return ((absX, absY), confidence, scale);
        }
    }

    /// <summary>
    /// 이미지 찾아서 클릭 (중심 오프셋 적용)
    /// </summary>
    public static bool ClickImage(string name, double? threshold = null, double delaySec = 0.2, bool offsetCenter = true)
    {
        double th = threshold ?? StateMatchThreshold;
        var res = FindImageOnChrome(name, th);
        if (res == null) return false;

        var (abs, confidence, scale) = res.Value;
        int absX = abs.X, absY = abs.Y;

        if (offsetCenter)
        {
            using var tpl = Cv2.ImRead(PicPath(name));
            if (!tpl.Empty())
            {
                int tw = tpl.Width, th0 = tpl.Height;
                absX += (int)(tw * scale) / 2;
                absY += (int)(th0 * scale) / 2;
            }
        }

        if (Debug)
        {
            AppLog.WriteLine($"  [클릭] {name} → ({absX}, {absY}) 신뢰도: {confidence:F3}, 스케일: {scale:F2}x");
        }

        InputHelper.Click(absX, absY);
        Thread.Sleep((int)(delaySec * 1000));
        return true;
    }

    /// <summary>이미지를 찾아 클릭할 중심 좌표만 반환 (클릭은 안 함). 2회차 위치 기억용.</summary>
    public static (int X, int Y)? GetImageClickPosition(string name, double? threshold = null)
    {
        double th = threshold ?? StateMatchThreshold;
        var res = FindImageOnChrome(name, th);
        if (res == null) return null;
        var (abs, _, scale) = res.Value;
        int absX = abs.X, absY = abs.Y;
        using var tpl = Cv2.ImRead(PicPath(name));
        if (!tpl.Empty())
        {
            absX += (int)(tpl.Width * scale) / 2;
            absY += (int)(tpl.Height * scale) / 2;
        }
        return (absX, absY);
    }

    /// <summary>지정 좌표 클릭 (저장된 슬롯 위치용)</summary>
    public static void ClickAt(int x, int y, double delaySec = 0.2)
    {
        InputHelper.Click(x, y);
        Thread.Sleep((int)(delaySec * 1000));
    }

    public static void SaveDebugImage(Mat img, string label)
    {
        if (!Debug) return;
        _debugCounter++;
        var dir = Path.Combine(BaseDir, "debug");
        Directory.CreateDirectory(dir);
        var safe = label.Replace(" ", "_").Replace("/", "-");
        if (safe.Length > 40) safe = safe[..40];
        var path = Path.Combine(dir, $"{_debugCounter:D3}_{safe}.png");
        try
        {
            Cv2.ImWrite(path, img);
            AppLog.WriteLine($"  [디버그캡처] {Path.GetFileName(path)}");
        }
        catch (Exception ex) { AppLog.WriteLine($"  [디버그캡처] 저장 실패: {ex.Message}"); }
    }

    public static void SaveDebugCapture(string label, (int X, int Y)? coords = null)
    {
        if (!Debug) return;
        using var img = CaptureScreen(null);
        SaveDebugImage(img, coords != null ? $"{label}_{coords.Value.X}_{coords.Value.Y}" : label);
    }

    /// <summary>단계 테스트용: Mat을 debug 폴더에 저장. 경로 반환.</summary>
    public static string SaveToDebugFile(Mat img, string fileName)
    {
        var dir = Path.Combine(BaseDir, "debug");
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, fileName);
        Cv2.ImWrite(path, img);
        return path;
    }

    /// <summary>
    /// [3] 햄버거 메뉴 감지와 동일: GetEdgeCaptureRect → ClampToScreen → CaptureScreen → FindTemplateMultiscale.
    /// 반환: (캡처 Mat, 캡처 영역, 매칭 결과). Mat은 호출자가 using/Dispose.
    /// </summary>
    public static (Mat? Capture, Rectangle? CaptureRect, ((int X, int Y) Rel, double Confidence, double Scale)? Result) CaptureAndDetectTemplate(string templateName, double threshold = 0.7)
    {
        var rect = GetEdgeCaptureRect();
        if (rect == null) return (null, null, null);

        var clamped = ClampToScreen(rect.Value);
        Mat mat = CaptureScreen(clamped);
        string templatePath = GetTemplatePath(templateName);
        if (!File.Exists(templatePath))
            return (mat, clamped, null);

        var result = FindTemplateMultiscale(mat, templatePath, threshold);
        return (mat, clamped, result);
    }

    /// <summary>
    /// CaptureAndDetect 방식으로 템플릿을 찾아 클릭할 절대 좌표만 반환 (클릭 안 함). 1회차 위치 기록·2회차 재사용용.
    /// </summary>
    public static (int X, int Y)? GetCaptureAndDetectClickPosition(string templateName, double threshold = 0.7)
    {
        var (mat, captureRect, result) = CaptureAndDetectTemplate(templateName, threshold);
        using (mat) { }
        if (captureRect == null || result == null) return null;

        var ((relX, relY), confidence, scale) = result.Value;
        int absX = captureRect.Value.X + relX;
        int absY = captureRect.Value.Y + relY;
        using (var tpl = Cv2.ImRead(PicPath(templateName)))
        {
            if (!tpl.Empty())
            {
                absX += (int)(tpl.Width * scale) / 2;
                absY += (int)(tpl.Height * scale) / 2;
            }
        }
        return (absX, absY);
    }

    /// <summary>
    /// [3] 햄버거 메뉴 감지와 완전히 동일한 방식으로 찾고, 찾으면 그 위치 클릭. (CaptureAndDetectTemplate + 클릭)
    /// </summary>
    public static bool ClickFromCaptureAndDetect(string templateName, double threshold = 0.7, double delaySec = 0.15)
    {
        var pos = GetCaptureAndDetectClickPosition(templateName, threshold);
        if (pos == null) return false;
        if (Debug) AppLog.WriteLine($"  [클릭] {templateName} → ({pos.Value.X}, {pos.Value.Y})");
        InputHelper.Click(pos.Value.X, pos.Value.Y);
        Thread.Sleep((int)(delaySec * 1000));
        return true;
    }
}
