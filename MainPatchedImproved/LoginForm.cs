using System.Drawing;
using System.Runtime.InteropServices;

namespace MainPatchedImproved;

/// <summary>
/// 로그인(시작) 화면 - 진입 후 메인 자동화 폼 표시
/// </summary>
public partial class LoginForm : Form
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_CAPTION_COLOR = 35;

    private Bitmap? _iconBitmap;

    public LoginForm()
    {
        InitializeComponent();
        Load += LoginForm_Load;
        Shown += LoginForm_Shown;
        Load += (_, _) => ShowLoginMode();
    }

    private void ApplyLogoAndIcon()
    {
        try
        {
            var img = AppIconHelper.GetLogoOrDefault();
            _iconBitmap = img is Bitmap b ? b : new Bitmap(img);
            AppIconHelper.ApplyToForm(this, _iconBitmap);
            if (pictureBoxLogo != null)
            {
                pictureBoxLogo.Image = _iconBitmap;
                pictureBoxLogo.Refresh();
            }
        }
        catch
        {
            try
            {
                _iconBitmap = AppIconHelper.GetDefaultLogo();
                AppIconHelper.ApplyToForm(this, _iconBitmap);
                if (pictureBoxLogo != null)
                {
                    pictureBoxLogo.Image = _iconBitmap;
                    pictureBoxLogo.Refresh();
                }
            }
            catch { }
        }
    }

    private void LoginForm_Shown(object? sender, EventArgs e)
    {
        if (_iconBitmap != null)
            AppIconHelper.ApplyToForm(this, _iconBitmap);
    }

    private async void LoginForm_Load(object? sender, EventArgs e)
    {
        int dark = 1;
        DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, 4);
        int captionColor = 0x00302D2D;
        DwmSetWindowAttribute(Handle, DWMWA_CAPTION_COLOR, ref captionColor, 4);

        ApplyLogoAndIcon();

        ServerApi.LoadBaseUrlFromFile();
        if (labelTelegram != null)
        {
            labelTelegram.Text = "서버 로그인 · 텔레그램 문의: (불러오는 중)";
            if (ServerApi.Enabled)
            {
                var nick = await ServerApi.GetTelegramNicknameAsync();
                var telegramPart = string.IsNullOrEmpty(nick) ? "텔레그램 문의: (설정 안 됨)" : "텔레그램 문의: " + nick;
                labelTelegram.Text = "서버 로그인 · " + telegramPart;
            }
        }
    }

    private async void ButtonLogin_Click(object? sender, EventArgs e)
    {
        labelError.Visible = false;
        labelError.Text = "";

        string id = textBoxId.Text.Trim();
        string pw = textBoxPassword.Text.Trim();

        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(pw))
        {
            labelError.ForeColor = Color.FromArgb(255, 100, 100);
            labelError.Text = "아이디와 비밀번호를 입력하세요.";
            labelError.Visible = true;
            return;
        }

        if (!ServerApi.Enabled)
        {
            labelError.ForeColor = Color.FromArgb(255, 100, 100);
            labelError.Text = "서버에 연결할 수 없습니다.";
            labelError.Visible = true;
            return;
        }

        var (ok, token, _) = await ServerApi.LoginAsync(id, pw);
        if (!ok || token == null)
        {
            labelError.ForeColor = Color.FromArgb(255, 100, 100);
            labelError.Text = "아이디 또는 비밀번호가 올바르지 않습니다.";
            labelError.Visible = true;
            return;
        }
        if (!ServerApi.IsApproved())
        {
            labelError.ForeColor = Color.FromArgb(255, 100, 100);
            labelError.Text = "승인 대기 중입니다. 관리자 승인 후 이용 가능합니다.";
            labelError.Visible = true;
            ServerApi.CurrentToken = null;
            ServerApi.CurrentUserId = null;
            return;
        }
        // approved지만 기간이 종료되었으면 로그인 거부
        if (!ServerApi.IsSubscriptionValid())
        {
            labelError.ForeColor = Color.FromArgb(255, 100, 100);
            labelError.Text = "아이디 또는 비밀번호가 올바르지 않습니다.";
            labelError.Visible = true;
            ServerApi.CurrentToken = null;
            ServerApi.CurrentUserId = null;
            return;
        }
        ServerApi.CurrentToken = token;
        ServerApi.CurrentUserId = id;

        OpenMainForm();
    }

    private void ShowLoginMode()
    {
        buttonLogin.Visible = true;
        linkSwitchToRegister.Visible = true;
    }

    private void LinkSwitchToRegister_LinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
    {
        if (e.Link != null) e.Link.Visited = true;
        using var registerForm = new RegisterForm();
        registerForm.SetTelegramContact(labelTelegram?.Text ?? "텔레그램 문의: (설정 안 됨)");
        registerForm.ShowDialog(this);
    }

    private void OpenMainForm()
    {
        var main = new MainForm();
        main.FormClosed += (_, _) => Close();
        main.Show();
        Hide();
    }
}
