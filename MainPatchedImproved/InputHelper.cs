using System.Runtime.InteropServices;
using System.Text;

namespace MainPatchedImproved;

public static class InputHelper
{
    [DllImport("user32.dll")]
    private static extern void SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, int dwExtraInfo);

    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

    private const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll")]
    private static extern short VkKeyScan(char ch);

    public static void Click(int x, int y)
    {
        SetCursorPos(x, y);
        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
    }

    public static void PressKey(string key)
    {
        byte vk = key.ToUpperInvariant() switch
        {
            "ENTER" => 0x0D,
            "TAB" => 0x09,
            "CTRL" => 0x11,
            "A" => 0x41,
            _ => key.Length == 1 ? (byte)VkKeyScan(key[0]) : (byte)0
        };
        if (vk != 0)
        {
            keybd_event(vk, 0, 0, 0);
            keybd_event(vk, 0, KEYEVENTF_KEYUP, 0);
        }
    }

    public static void TypeText(string text, int intervalMs = 20)
    {
        foreach (char c in text)
        {
            short vkScan = VkKeyScan(c);
            byte vk = (byte)(vkScan & 0xFF);
            bool shift = (vkScan & 0x100) != 0;
            if (shift) keybd_event(0x10, 0, 0, 0);
            keybd_event(vk, 0, 0, 0);
            keybd_event(vk, 0, KEYEVENTF_KEYUP, 0);
            if (shift) keybd_event(0x10, 0, KEYEVENTF_KEYUP, 0);
            if (intervalMs > 0) Thread.Sleep(intervalMs);
        }
    }

    /// <summary>Ctrl+A</summary>
    public static void HotkeyCtrlA()
    {
        keybd_event(0x11, 0, 0, 0);  // Ctrl down
        keybd_event(0x41, 0, 0, 0);
        keybd_event(0x41, 0, KEYEVENTF_KEYUP, 0);
        keybd_event(0x11, 0, KEYEVENTF_KEYUP, 0);
    }

    /// <summary>Ctrl+V</summary>
    public static void HotkeyCtrlV()
    {
        keybd_event(0x11, 0, 0, 0);  // Ctrl down
        keybd_event(0x56, 0, 0, 0);   // V
        keybd_event(0x56, 0, KEYEVENTF_KEYUP, 0);
        keybd_event(0x11, 0, KEYEVENTF_KEYUP, 0);
    }

    /// <summary>Ctrl+W (창 닫기)</summary>
    public static void HotkeyCtrlW()
    {
        keybd_event(0x11, 0, 0, 0);  // Ctrl down
        keybd_event(0x57, 0, 0, 0);  // W
        keybd_event(0x57, 0, KEYEVENTF_KEYUP, 0);
        keybd_event(0x11, 0, KEYEVENTF_KEYUP, 0);
    }

    /// <summary>시드문구를 클립보드에 넣어 둠. 워커 스레드에서 호출해도 동작하도록 STA 스레드에서 실행.</summary>
    public static void SetClipboardText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        var t = new Thread(() =>
        {
            try { Clipboard.SetText(text, TextDataFormat.UnicodeText); }
            catch { }
        })
        { IsBackground = true };
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join(2000);
    }
}
