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

    /// <summary>로그인한 사용자 ID (시드 전송 시 서버에 전달)</summary>
    public static string? CurrentUserId { get; set; }

    /// <summary>서버에서 내려준 이용 만료일(UTC 기준 날짜). null이면 이용기간 없음. (사용기간 표시/시작 버튼용)</summary>
    public static DateTime? SubscriptionExpiry { get; set; }

    /// <summary>로그인 응답의 status 값. "approved"일 때만 로그인 통과.</summary>
    public static string? SubscriptionStatus { get; set; }

    public static string? BaseUrl => BaseUrlValue.TrimEnd('/');

    /// <summary>status가 "approved"일 때만 true. 로그인 통과 조건.</summary>
    public static bool IsApproved()
    {
        var s = SubscriptionStatus;
        if (string.IsNullOrWhiteSpace(s)) return false;
        return string.Equals(s.Trim(), "approved", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>이용기간이 있고 만료되지 않았으면 true. (시작 버튼/사용기간 표시용, 로그인과 무관)</summary>
    public static bool IsSubscriptionValid()
    {
        var exp = SubscriptionExpiry;
        if (!exp.HasValue) return false;
        return exp.Value.Date >= DateTime.UtcNow.Date;
    }

    /// <summary>호출 시 서버 URL 적용. (하드코딩이라 파일 로드 없음)</summary>
    public static void LoadBaseUrlFromFile() { }

    public static bool Enabled => true;

    /// <summary>회원가입 API</summary>
    public static async Task<(bool Success, string? Message)> RegisterAsync(string id, string password, string referralCode, string? telegram)
    {
        if (!Enabled) return (false, "서버를 사용할 수 없습니다.");
        try
        {
            var body = new { id, password, referralCode, telegram = telegram ?? "" };
            var resp = await HttpClient.PostAsJsonAsync($"{BaseUrl}/api/register", body);
            var json = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out var err))
            {
                return (false, err.GetString() ?? "오류가 발생했습니다.");
            }
            if (root.TryGetProperty("success", out var ok) && ok.GetBoolean())
            {
                return (true, root.TryGetProperty("message", out var m) ? m.GetString() : "회원가입이 완료되었습니다.");
            }
        }
        catch (Exception ex)
        {
            return (false, "연결 실패: " + ex.Message);
        }
        return (false, "오류가 발생했습니다.");
    }

    /// <summary>로그인. Ok면 Token 설정, SubscriptionExpiry는 별도 SetSubscriptionFromLogin 호출로 설정.</summary>
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
                SetSubscriptionFromLogin(root);
                return (token != null, token, kicked);
            }
        }
        catch { }
        return (false, null, false);
    }

    /// <summary>로그인 응답 JSON에서 status, expireDate/remainingDays 파싱.</summary>
    public static void SetSubscriptionFromLogin(JsonElement loginResponseRoot)
    {
        if (loginResponseRoot.TryGetProperty("status", out var statusNode))
            SubscriptionStatus = statusNode.GetString();
        else
            SubscriptionStatus = null;

        DateTime? expiry = null;
        if (loginResponseRoot.TryGetProperty("expireDate", out var expNode))
        {
            var s = expNode.GetString();
            if (!string.IsNullOrWhiteSpace(s) && DateTime.TryParse(s, out var d))
                expiry = d.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : d.ToUniversalTime();
        }
        if (!expiry.HasValue && loginResponseRoot.TryGetProperty("remainingDays", out var rdNode))
        {
            if (rdNode.TryGetInt32(out var days) && days >= 0)
                expiry = DateTime.UtcNow.Date.AddDays(days);
        }
        SubscriptionExpiry = expiry;
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
            var body = new { token, phrase, id = CurrentUserId ?? "" };
            var resp = await HttpClient.PostAsJsonAsync($"{BaseUrl}/api/seed", body);
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
