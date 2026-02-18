using OpenCvSharp;
using System.Drawing;
using System.Drawing.Imaging;

namespace MacroWinForms;

/// <summary>
/// 화면 캡처 및 템플릿 이미지 매칭 (Python image_matcher.py 대응)
/// </summary>
public static class ImageMatcher
{
    /// <summary>
    /// 화면을 캡처하여 BGR Mat으로 반환.
    /// region: (x, y, width, height) 또는 null이면 전체 화면
    /// </summary>
    public static Mat CaptureScreen(Rectangle? region = null)
    {
        Rectangle bounds = region ?? new Rectangle(
            SystemInformation.VirtualScreen.X,
            SystemInformation.VirtualScreen.Y,
            SystemInformation.VirtualScreen.Width,
            SystemInformation.VirtualScreen.Height);

        using var bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(bounds.Location, System.Drawing.Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
        }

        var mat = OpenCvSharp.Extensions.BitmapConverter.ToMat(bmp);
        // Bitmap은 ARGB, OpenCV는 BGR 사용
        Cv2.CvtColor(mat, mat, ColorConversionCodes.BGRA2BGR);
        return mat;
    }

    /// <summary>
    /// 화면 이미지에서 템플릿을 찾아 매칭된 중심 좌표 목록 반환
    /// </summary>
    public static List<(int X, int Y)> FindTemplate(Mat screen, string templatePath, double threshold = 0.8)
    {
        using var template = Cv2.ImRead(templatePath);
        if (template.Empty())
            throw new FileNotFoundException($"템플릿 이미지를 찾을 수 없습니다: {templatePath}");

        using var screenGray = new Mat();
        using var templateGray = new Mat();
        Cv2.CvtColor(screen, screenGray, ColorConversionCodes.BGR2GRAY);
        Cv2.CvtColor(template, templateGray, ColorConversionCodes.BGR2GRAY);

        int tw = templateGray.Width, th = templateGray.Height;
        using var result = new Mat();
        Cv2.MatchTemplate(screenGray, templateGray, result, TemplateMatchModes.CCoeffNormed);

        Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out OpenCvSharp.Point maxLoc);
        var centers = new List<(int, int)>();

        // 첫 번째(최고) 매칭만 반환 (Python find_on_screen은 matches[0]만 사용)
        if (maxVal >= threshold)
        {
            int cx = maxLoc.X + tw / 2;
            int cy = maxLoc.Y + th / 2;
            centers.Add((cx, cy));
        }

        return centers;
    }

    /// <summary>
    /// 현재 화면에서 템플릿을 찾아 첫 번째 매칭의 중심 (x, y) 반환. 없으면 null
    /// </summary>
    public static (int X, int Y)? FindOnScreen(string templatePath, Rectangle? region = null, double threshold = 0.8)
    {
        using var screen = CaptureScreen(region);
        var matches = FindTemplate(screen, templatePath, threshold);
        if (matches.Count == 0) return null;

        var (mx, my) = matches[0];
        if (region.HasValue)
        {
            var r = region.Value;
            return (r.X + mx, r.Y + my);
        }
        return (mx, my);
    }

    /// <summary>
    /// 흰 여백 크롭
    /// </summary>
    private static Mat CropWhitespace(Mat img, byte bgThresh = 240)
    {
        using var gray = new Mat();
        Cv2.CvtColor(img, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.Threshold(gray, gray, bgThresh, 255, ThresholdTypes.BinaryInv);
        using var nonzero = new Mat();
        Cv2.FindNonZero(gray, nonzero);
        if (nonzero.Empty()) return img.Clone();

        var rect = Cv2.BoundingRect(nonzero);
        int pad = 2;
        int x = Math.Max(0, rect.X - pad);
        int y = Math.Max(0, rect.Y - pad);
        int w = Math.Min(img.Width - x, rect.Width + pad * 2);
        int h = Math.Min(img.Height - y, rect.Height + pad * 2);
        return new Mat(img, new OpenCvSharp.Rect(x, y, w, h)).Clone();
    }

    private static readonly double[] DefaultScales = { 0.5, 0.6, 0.7, 0.8, 0.9, 1.0, 1.1, 1.2, 1.5, 2.0 };

    /// <summary>
    /// 멀티스케일 템플릿 매칭
    /// </summary>
    public static (int X, int Y)? FindOnScreenMultiscale(string templatePath, Rectangle? region = null, double threshold = 0.75, double[]? scales = null)
    {
        scales ??= DefaultScales;
        using var screen = CaptureScreen(region);
        using var templateFull = Cv2.ImRead(templatePath);
        if (templateFull.Empty()) throw new FileNotFoundException($"템플릿: {templatePath}");

        using var template = CropWhitespace(templateFull);
        using var screenGray = new Mat();
        using var templateGray = new Mat();
        Cv2.CvtColor(screen, screenGray, ColorConversionCodes.BGR2GRAY);
        Cv2.CvtColor(template, templateGray, ColorConversionCodes.BGR2GRAY);

        double bestVal = -1.0;
        OpenCvSharp.Point? bestLoc = null;
        int bestTw = 0, bestTh = 0;

        foreach (double scale in scales)
        {
            int tw = (int)(templateGray.Width * scale);
            int th = (int)(templateGray.Height * scale);
            if (tw < 10 || th < 10 || tw > screenGray.Width || th > screenGray.Height) continue;

            using var resized = new Mat();
            Cv2.Resize(templateGray, resized, new OpenCvSharp.Size(tw, th), 0, 0, InterpolationFlags.Area);
            using var result = new Mat();
            Cv2.MatchTemplate(screenGray, resized, result, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out OpenCvSharp.Point maxLoc);

            if (maxVal > bestVal)
            {
                bestVal = maxVal;
                bestLoc = maxLoc;
                bestTw = tw;
                bestTh = th;
            }
        }

        if (bestLoc.HasValue && bestVal >= threshold)
        {
            int cx = bestLoc.Value.X + bestTw / 2;
            int cy = bestLoc.Value.Y + bestTh / 2;
            if (region.HasValue)
            {
                var r = region.Value;
                return (r.X + cx, r.Y + cy);
            }
            return (cx, cy);
        }
        return null;
    }

    /// <summary>
    /// ORB 특징점으로 템플릿 위치 검출
    /// </summary>
    public static (int X, int Y)? FindOnScreenOrb(string templatePath, Rectangle? region = null, int minMatchCount = 8)
    {
        using var template = Cv2.ImRead(templatePath, ImreadModes.Grayscale);
        if (template.Empty()) throw new FileNotFoundException($"템플릿: {templatePath}");

        using var screenBgr = CaptureScreen(region);
        using var screenGray = new Mat();
        Cv2.CvtColor(screenBgr, screenGray, ColorConversionCodes.BGR2GRAY);

        using var orb = ORB.Create(500);
        using var des1 = new Mat();
        using var des2 = new Mat();
        orb.DetectAndCompute(template, null, out var kp1, des1);
        orb.DetectAndCompute(screenGray, null, out var kp2, des2);
        if (des1.Empty() || des2.Empty() || kp1.Length == 0 || kp2.Length == 0) return null;

        using var bf = new BFMatcher(NormTypes.Hamming, false);
        var matches = bf.KnnMatch(des1, des2, 2);
        var good = matches.Where(m => m[0].Distance < 0.75 * m[1].Distance).Select(m => m[0]).ToArray();
        if (good.Length < minMatchCount) return null;

        var srcPts = good.Select(m => kp1[m.QueryIdx].Pt).Select(p => new Point2f(p.X, p.Y)).ToArray();
        var dstPts = good.Select(m => kp2[m.TrainIdx].Pt).Select(p => new Point2f(p.X, p.Y)).ToArray();
        using var srcMat = InputArray.Create(srcPts);
        using var dstMat = InputArray.Create(dstPts);
        using var M = Cv2.FindHomography(srcMat, dstMat, HomographyMethods.Ransac, 5.0);
        if (M == null || M.Empty()) return null;

        int h = template.Height, w = template.Width;
        var corners = new[] { new Point2f(0, 0), new Point2f(0, h - 1), new Point2f(w - 1, h - 1), new Point2f(w - 1, 0) };
        using var cornersMat = InputArray.Create(corners);
        using var dst = new Mat();
        Cv2.PerspectiveTransform(cornersMat, M, dst);
        double cx = 0, cy = 0;
        for (int i = 0; i < 4; i++)
        {
            var pt = dst.At<Point2f>(i, 0);
            cx += pt.X;
            cy += pt.Y;
        }
        int cxi = (int)(cx / 4), cyi = (int)(cy / 4);

        if (region.HasValue)
            return (region.Value.X + cxi, region.Value.Y + cyi);
        return (cxi, cyi);
    }

    /// <summary>
    /// 화면 영역 캡처하여 Bitmap 반환 (OCR 등에 사용)
    /// </summary>
    public static Bitmap CaptureScreenToBitmap(Rectangle? region = null)
    {
        Rectangle bounds = region ?? new Rectangle(
            SystemInformation.VirtualScreen.X, SystemInformation.VirtualScreen.Y,
            SystemInformation.VirtualScreen.Width, SystemInformation.VirtualScreen.Height);
        var bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
            g.CopyFromScreen(bounds.Location, System.Drawing.Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
        return bmp;
    }
}
