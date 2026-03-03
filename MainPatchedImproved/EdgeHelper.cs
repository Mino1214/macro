using System.Runtime.InteropServices;
using System.Drawing;

namespace MainPatchedImproved;

/// <summary>
/// Windows: Microsoft Edge 창 위치/포커스
/// </summary>
public static class EdgeHelper
{
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(nint hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PrintWindow(nint hwnd, nint hdcBlt, uint nFlags);

    private const int SW_RESTORE = 9;
    private const uint PW_CLIENTONLY = 0x1;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    private static nint _edgeHwnd;
    private static nint _chromeHwnd;

    private static string GetTitle(nint hWnd)
    {
        var sb = new System.Text.StringBuilder(512);
        GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static string GetClass(nint hWnd)
    {
        var sb = new System.Text.StringBuilder(256);
        GetClassName(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static bool IsChromeLikeWindow(nint hWnd, uint ourPid)
    {
        if (!IsWindowVisible(hWnd)) return false;

        GetWindowThreadProcessId(hWnd, out uint pid);
        if (pid == 0 || pid == ourPid) return false;

        // 1) 윈도우 클래스 기반 (Process 접근 실패 대비)
        var cls = GetClass(hWnd);
        if (cls.StartsWith("Chrome_WidgetWin_", StringComparison.OrdinalIgnoreCase))
            return true;

        // 2) 타이틀 기반 (클래스가 비정상인 케이스 대비)
        var title = GetTitle(hWnd);
        if (title.Contains("Google Chrome", StringComparison.OrdinalIgnoreCase) ||
            title.Contains(" - Chrome", StringComparison.OrdinalIgnoreCase))
            return true;

        // 3) 기존 방식: 프로세스명 기반 (가능할 때만)
        try
        {
            using var proc = System.Diagnostics.Process.GetProcessById((int)pid);
            if (proc.ProcessName.Equals("chrome", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        catch
        {
            // 프로세스 접근 실패는 "아니다"로 단정하지 않고, 위의 class/title로 걸러짐
        }

        return false;
    }

    /// <summary>
    /// Chrome 창 (x, y, width, height) 픽셀 좌표. 없으면 null. Trust Wallet용 캡처 대상.
    /// 감지 실패 시 열거된 창 목록을 data/chrome_detect_log.txt 에 기록.
    /// </summary>
    public static Rectangle? GetChromeRegion()
    {
        _chromeHwnd = nint.Zero;
        uint ourPid = (uint)Environment.ProcessId;
        var logEntries = new List<string>();

        nint bestHwnd = nint.Zero;
        long bestArea = 0;

        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd)) return true;
            GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == 0 || pid == ourPid) return true;
            if (!GetWindowRect(hWnd, out var r)) return true;

            int w = r.Right - r.Left;
            int h = r.Bottom - r.Top;
            if (w <= 50 || h <= 50) return true;

            string processName = "";
            try
            {
                using var proc = System.Diagnostics.Process.GetProcessById((int)pid);
                processName = proc.ProcessName ?? "";
            }
            catch { processName = "(접근실패)"; }

            string cls = GetClass(hWnd);
            string title = GetTitle(hWnd);
            if (title.Length > 80) title = title.Substring(0, 77) + "...";

            logEntries.Add($"  PID={pid} Process={processName} Class={cls} Title={title} Size={w}x{h}");

            if (!IsChromeLikeWindow(hWnd, ourPid)) return true;

            long area = (long)w * h;
            if (area > bestArea)
            {
                bestArea = area;
                bestHwnd = hWnd;
            }

            return true;
        }, nint.Zero);

        _chromeHwnd = bestHwnd;
        if (_chromeHwnd == nint.Zero && logEntries.Count > 0)
        {
            try
            {
                var logPath = Path.Combine(ChromeImageMatcher.BaseDir, "chrome_detect_log.txt");
                var lines = new List<string>
                {
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] GetChromeRegion: Chrome 미감지 (우리 PID={ourPid})",
                    "열거된 창 (PID, 프로세스명, 클래스, 제목, 크기):",
                    ""
                };
                lines.AddRange(logEntries);
                File.WriteAllLines(logPath, lines, System.Text.Encoding.UTF8);
            }
            catch { }
        }

        if (_chromeHwnd == nint.Zero) return null;
        if (!GetWindowRect(_chromeHwnd, out var rect)) return null;

        return new Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

    /// <summary>
    /// Edge 창 (x, y, width, height) 픽셀 좌표. 없으면 null
    /// </summary>
    public static Rectangle? GetEdgeRegion()
    {
        _edgeHwnd = nint.Zero;
        uint ourPid = (uint)Environment.ProcessId;
        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd)) return true;
            GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == ourPid) return true; // 우리 프로그램 창 제외
            var sb = new System.Text.StringBuilder(256);
            GetWindowText(hWnd, sb, sb.Capacity);
            var title = sb.ToString();
            // "Microsoft Edge" 또는 "Edge" 포함 (우리 프로세스는 위에서 이미 제외됨)
            if (!title.Contains("Microsoft Edge", StringComparison.OrdinalIgnoreCase) &&
                !title.Contains("Edge", StringComparison.OrdinalIgnoreCase))
                return true;
            if (GetWindowRect(hWnd, out var r))
            {
                int w = r.Right - r.Left;
                int h = r.Bottom - r.Top;
                if (w > 100 && h > 100)
                {
                    _edgeHwnd = hWnd;
                    return false; // stop enum
                }
            }
            return true;
        }, nint.Zero);

        if (_edgeHwnd == nint.Zero) return null;
        if (!GetWindowRect(_edgeHwnd, out var rect)) return null;
        return new Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

    /// <summary>GetEdgeRegion() 호출 후 Edge 창 핸들. 없으면 nint.Zero.</summary>
    public static nint GetEdgeHwnd()
    {
        if (_edgeHwnd == nint.Zero) GetEdgeRegion();
        return _edgeHwnd;
    }

    /// <summary>
    /// 포커스를 옮기지 않고 창 내용만 캡처 (PrintWindow). 지갑이 꺼지지 않음.
    /// 반환: (캡처 비트맵, 창 화면 좌표). 실패 시 (null, null).
    /// </summary>
    public static (Bitmap? Bmp, Rectangle? WindowRect) CaptureWindowWithoutFocus(nint hwnd)
    {
        if (hwnd == nint.Zero || !GetWindowRect(hwnd, out var r)) return (null, null);
        int w = r.Right - r.Left;
        int h = r.Bottom - r.Top;
        if (w <= 0 || h <= 0) return (null, null);
        try
        {
            var bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                nint hdc = g.GetHdc();
                try
                {
                    if (!PrintWindow(hwnd, hdc, 0))
                        return (null, null);
                }
                finally
                {
                    g.ReleaseHdc(hdc);
                }
            }
            return (bmp, new Rectangle(r.Left, r.Top, w, h));
        }
        catch
        {
            return (null, null);
        }
    }

    /// <summary>
    /// 현재 포커스된 창의 영역 (지갑 팝업 등). 포커스 창이 없거나 우리 프로세스 창이면 null.
    /// 지갑이 팝업일 때 FocusEdge() 대신 이 영역으로 캡처하면 포커스를 뺏지 않아 창이 안 닫힘.
    /// </summary>
    public static Rectangle? GetForegroundWindowRegion()
    {
        nint h = GetForegroundWindow();
        if (h == nint.Zero) return null;
        GetWindowThreadProcessId(h, out uint pid);
        if (pid == (uint)Environment.ProcessId) return null; // 우리 창이면 사용 안 함
        if (!IsWindowVisible(h)) return null;
        if (!GetWindowRect(h, out var r)) return null;
        int w = r.Right - r.Left, hgt = r.Bottom - r.Top;
        if (w < 50 || hgt < 50) return null;
        return new Rectangle(r.Left, r.Top, w, hgt);
    }

    /// <summary>
    /// Edge를 최상위로 포커스 (최소화돼 있으면 복원)
    /// </summary>
    public static bool FocusEdge()
    {
        var r = GetEdgeRegion();
        if (r == null) return false;
        if (IsIconic(_edgeHwnd))
            ShowWindow(_edgeHwnd, SW_RESTORE);
        System.Threading.Thread.Sleep(100);
        return SetForegroundWindow(_edgeHwnd);
    }
}