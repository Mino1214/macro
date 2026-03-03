namespace MainPatchedImproved;

/// <summary>
/// 콘솔(터미널) 전용 엔트리. 현재는 니모닉 생성까지 제공.
/// (Tron 잔고조회 로직은 필요 시 여기로 확장)
/// </summary>
public static class TronCli
{
    public static void Run(string[] args)
    {
        try { Directory.CreateDirectory(ChromeImageMatcher.BaseDir); } catch { }

        int count = 1;
        if (TryGetIntArg(args, "--count", out var c) && c > 0) count = c;

        if (HasFlag(args, "--help") || HasFlag(args, "-h"))
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  MainPatchedImproved.exe --count 10");
            Console.WriteLine();
            Console.WriteLine($"BaseDir: {ChromeImageMatcher.BaseDir}");
            return;
        }

        List<string> words;
        try
        {
            words = MainLoop.LoadWords();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("wordlist 로드 실패: " + ex.Message);
            Console.Error.WriteLine("BaseDir: " + ChromeImageMatcher.BaseDir);
            return;
        }

        for (int i = 0; i < count; i++)
        {
            var mnemonic = MainLoop.Random12(words);
            Console.WriteLine(string.Join(" ", mnemonic));
        }
    }

    private static bool HasFlag(string[] args, string flag)
        => args.Any(a => string.Equals(a, flag, StringComparison.OrdinalIgnoreCase));

    private static bool TryGetIntArg(string[] args, string name, out int value)
    {
        value = 0;
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (!string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) continue;
            return int.TryParse(args[i + 1], out value);
        }
        return false;
    }
}

