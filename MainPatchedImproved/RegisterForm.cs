using System.Drawing;
using System.Runtime.InteropServices;

namespace MainPatchedImproved;

/// <summary>회원가입 팝업 - 로그인 페이지와 동일 UI 스타일</summary>
public partial class RegisterForm : Form
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_CAPTION_COLOR = 35;

    private string _telegramText = "텔레그램 문의: (불러오는 중)";

    public RegisterForm()
    {
        InitializeComponent();
        Load += RegisterForm_Load;
    }

    public void SetTelegramContact(string text)
    {
        _telegramText = text;
        if (labelTelegramContact != null)
            labelTelegramContact.Text = text;
    }

    private async void RegisterForm_Load(object? sender, EventArgs e)
    {
        int dark = 1;
        DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, 4);
        int captionColor = 0x00302D2D;
        DwmSetWindowAttribute(Handle, DWMWA_CAPTION_COLOR, ref captionColor, 4);

        if (ServerApi.Enabled)
        {
            var nick = await ServerApi.GetTelegramNicknameAsync();
            var text = string.IsNullOrEmpty(nick) ? "텔레그램 문의: (설정 안 됨)" : "텔레그램 문의: " + nick;
            SetTelegramContact(text);
        }
    }

    private void ButtonBack_Click(object? sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private async void ButtonRegister_Click(object? sender, EventArgs e)
    {
        labelError.Visible = false;
        labelError.Text = "";

        string id = textBoxId.Text.Trim();
        string pw = textBoxPassword.Text.Trim();
        string referral = textBoxReferral.Text.Trim();
        string telegram = textBoxTelegram.Text.Trim();

        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(pw))
        {
            labelError.Text = "아이디와 비밀번호를 입력하세요.";
            labelError.ForeColor = Color.FromArgb(255, 100, 100);
            labelError.Visible = true;
            return;
        }
        if (string.IsNullOrEmpty(referral))
        {
            labelError.Text = "추천인 코드(매니저 아이디)를 입력하세요.";
            labelError.ForeColor = Color.FromArgb(255, 100, 100);
            labelError.Visible = true;
            return;
        }

        var (success, message) = await ServerApi.RegisterAsync(id, pw, referral, string.IsNullOrEmpty(telegram) ? null : telegram);
        labelError.Text = message ?? (success ? "가입 요청이 완료되었습니다." : "오류가 발생했습니다.");
        labelError.ForeColor = success ? Color.FromArgb(78, 201, 176) : Color.FromArgb(255, 100, 100);
        labelError.Visible = true;
        if (success)
            textBoxId.Clear();
        textBoxPassword.Clear();
        textBoxReferral.Clear();
        textBoxTelegram.Clear();
    }
}
