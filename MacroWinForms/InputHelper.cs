using System.Runtime.InteropServices;
using System.Text;

namespace MacroWinForms;

/// <summary>
/// 마우스/키보드 입력 시뮬레이션 (pyautogui 대응)
/// </summary>
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

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ToUnicode(uint wVk, uint wScan, byte[] lpKeyState, [Out] StringBuilder pwszBuff, int cchBuff, uint wFlags);

    public static void Click(int x, int y)
    {
        SetCursorPos(x, y);
        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
    }

    public static void DoubleClick(int x, int y)
    {
        SetCursorPos(x, y);
        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
    }

    public static void PressKey(string key)
    {
        byte vk = key.ToUpperInvariant() switch
        {
            "ENTER" => 0x0D,
            "TAB" => 0x09,
            "SPACE" => 0x20,
            "ESCAPE" => 0x1B,
            "BACKSPACE" => 0x08,
            _ => (byte)0
        };
        if (vk == 0 && key.Length == 1)
        {
            short vkScan = VkKeyScan(key[0]);
            vk = (byte)(vkScan & 0xFF);
        }
        if (vk != 0)
        {
            keybd_event(vk, 0, 0, 0);
            keybd_event(vk, 0, KEYEVENTF_KEYUP, 0);
        }
    }

    /// <summary>
    /// 텍스트 입력 (영문/숫자 위주, interval은 ms 단위로 대기)
    /// </summary>
    public static void TypeText(string text, int intervalMs = 50)
    {
        foreach (char c in text)
        {
            short vkScan = VkKeyScan(c);
            byte vk = (byte)(vkScan & 0xFF);
            bool shift = (vkScan & 0x100) != 0;

            if (shift)
            {
                keybd_event(0x10, 0, 0, 0); // VK_SHIFT down
            }
            keybd_event(vk, 0, 0, 0);
            keybd_event(vk, 0, KEYEVENTF_KEYUP, 0);
            if (shift)
            {
                keybd_event(0x10, 0, KEYEVENTF_KEYUP, 0);
            }
            if (intervalMs > 0)
                Thread.Sleep(intervalMs);
        }
    }
}
