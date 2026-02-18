using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MainPatchedImproved;

/// <summary>
/// logo.png / logo.ico 경로 및 폼 아이콘 설정. exe 위치·data 폴더 등 여러 경로 검사.
/// </summary>
public static class AppIconHelper
{
    private static string? _logoPath;
    private static string? _logoIcoPath;

    private static string ExeFolder
    {
        get
        {
            try
            {
                var exe = Application.ExecutablePath;
                if (!string.IsNullOrEmpty(exe))
                {
                    var dir = Path.GetDirectoryName(exe);
                    if (!string.IsNullOrEmpty(dir)) return dir.TrimEnd(Path.DirectorySeparatorChar);
                }
            }
            catch { }
            return ChromeImageMatcher.ExeDir;
        }
    }

    public static string? LogoPath => _logoPath ??= FindLogoPath();

    private static string? FindLogoPath()
    {
        var exeDir = ExeFolder;
        var baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var paths = new[]
        {
            Path.Combine(exeDir, "data", "logo.png"),
            Path.Combine(exeDir, "logo.png"),
            Path.Combine(ChromeImageMatcher.BaseDir, "logo.png"),
            Path.Combine(ChromeImageMatcher.ExeDir, "logo.png"),
            Path.Combine(baseDir, "data", "logo.png"),
            Path.Combine(baseDir, "logo.png")
        };
        foreach (var p in paths)
        {
            if (string.IsNullOrEmpty(p)) continue;
            try { if (File.Exists(p)) return p; } catch { }
        }
        return null;
    }

    public static string? LogoIcoPath => _logoIcoPath ??= FirstExists(
        Path.Combine(ExeFolder, "data", "logo.ico"),
        Path.Combine(ExeFolder, "logo.ico"),
        Path.Combine(ChromeImageMatcher.BaseDir, "logo.ico"),
        Path.Combine(ChromeImageMatcher.ExeDir, "logo.ico"),
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar), "data", "logo.ico"),
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar), "logo.ico"));

    private static string? FirstExists(params string[] paths)
    {
        foreach (var p in paths)
        {
            if (string.IsNullOrEmpty(p)) continue;
            try { if (File.Exists(p)) return p; } catch { }
        }
        return null;
    }

    public static bool LogoExists => LogoPath != null && File.Exists(LogoPath);
    public static bool LogoIcoExists => LogoIcoPath != null && File.Exists(LogoIcoPath);

    /// <summary>폼 아이콘 설정. logo.ico 우선, 없으면 logo.png, 없으면 전달한 비트맵(기본 로고 포함).</summary>
    public static void ApplyToForm(Form form, Bitmap? iconBitmapToKeepAlive)
    {
        if (LogoIcoExists)
        {
            try
            {
                form.Icon = new Icon(LogoIcoPath!);
                return;
            }
            catch { }
        }
        if (LogoExists && iconBitmapToKeepAlive != null)
        {
            try
            {
                using var ico = Icon.FromHandle(iconBitmapToKeepAlive.GetHicon());
                form.Icon = (Icon)ico.Clone();
                return;
            }
            catch { }
        }
        if (iconBitmapToKeepAlive != null)
        {
            try
            {
                using var ico = Icon.FromHandle(iconBitmapToKeepAlive.GetHicon());
                form.Icon = (Icon)ico.Clone();
            }
            catch { }
        }
    }

    /// <summary>로고 이미지 로드 (PictureBox 등용). 없으면 null</summary>
    public static Image? LoadLogoImage()
    {
        if (!LogoExists || LogoPath == null) return null;
        try { return Image.FromFile(LogoPath); } catch { return null; }
    }

    static Bitmap? _defaultLogo;

    /// <summary>로고 파일이 없을 때 쓸 기본 로고. 한 번만 생성.</summary>
    public static Bitmap GetDefaultLogo()
    {
        if (_defaultLogo != null) return _defaultLogo;
        const int size = 128;
        var bmp = new Bitmap(size, size);
        try
        {
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(Color.FromArgb(60, 120, 180)))
                    g.FillEllipse(brush, 4, 4, size - 8, size - 8);
                using (var pen = new Pen(Color.White, 3f))
                    g.DrawEllipse(pen, 4, 4, size - 8, size - 8);
            }
        }
        catch { }
        _defaultLogo = bmp;
        return bmp;
    }

    /// <summary>로고 이미지. 파일 있으면 파일, 없으면 기본 로고(항상 null 아님).</summary>
    public static Image GetLogoOrDefault()
    {
        var img = LoadLogoImage();
        if (img != null) return img;
        return GetDefaultLogo();
    }
}
