namespace MainPatchedImproved;

/// <summary>
/// data/mnemonic_attempt_count.txt 에 니모닉문구 시도 횟수 저장·읽기 (한 줄 정수)
/// </summary>
public static class WalletCountFile
{
    private static string FilePath => System.IO.Path.Combine(ChromeImageMatcher.BaseDir, "mnemonic_attempt_count.txt");

    public static int Read()
    {
        try
        {
            if (!File.Exists(FilePath)) return 0;
            var s = File.ReadAllText(FilePath, System.Text.Encoding.UTF8).Trim();
            return int.TryParse(s, out var n) && n >= 0 ? n : 0;
        }
        catch { return 0; }
    }

    public static void Increment()
    {
        try
        {
            var n = Read() + 1;
            File.WriteAllText(FilePath, n.ToString(), System.Text.Encoding.UTF8);
        }
        catch { }
    }
}
