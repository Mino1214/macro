namespace MainPatchedImproved;

/// <summary>
/// GUI 로그 출력 (백그라운드 스레드에서 폼 텍스트박스로 전달)
/// </summary>
public static class AppLog
{
    public static Action<string>? LogLine { get; set; }

    /// <summary>시도한 시드 문구 한 줄 (UI에 최대 100개 쌓기용)</summary>
    public static Action<string>? AttemptedPhrase { get; set; }

    public static void WriteLine(string text = "")
    {
        var line = string.IsNullOrEmpty(text) ? Environment.NewLine : text + Environment.NewLine;
        LogLine?.Invoke(line);
    }

    public static void WriteAttemptedPhrase(string phraseLine)
    {
        AttemptedPhrase?.Invoke(phraseLine);
    }
}
