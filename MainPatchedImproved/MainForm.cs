using System.Drawing;
using System.Runtime.InteropServices;

namespace MainPatchedImproved;

public partial class MainForm : Form
{
    private Thread? _workerThread;
    private volatile bool _running;
    private System.Windows.Forms.Timer? _escCheckTimer;
    private System.Windows.Forms.Timer? _sessionCheckTimer;
    private readonly List<string> _attemptedPhrases = new();
    private const int MaxPhrases = 100;
    private Bitmap? _iconBitmap;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    private const int VK_ESCAPE = 0x1B;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_CAPTION_COLOR = 35;

    private static readonly Color LogAccent = Color.FromArgb(78, 201, 176);
    private static readonly Color LogRed = Color.FromArgb(255, 100, 100);

    public MainForm()
    {
        InitializeComponent();
        AppLog.LogLine = AppendLog;
        AppLog.LogLineRed = AppendLogRed;
        AppLog.AttemptedPhrase = AddAttemptedPhrase;
        FormClosing += MainForm_FormClosing;
        Load += MainForm_Load;
    }

    private void MainForm_Load(object? sender, EventArgs e)
    {
        ApplyDarkTitleBar();
        try
        {
            var img = AppIconHelper.GetLogoOrDefault();
            _iconBitmap = img is Bitmap b ? b : new Bitmap(img);
            AppIconHelper.ApplyToForm(this, _iconBitmap);
        }
        catch { }

        RefreshExpiryLabel();
        RefreshWalletCount();
        if (!ServerApi.IsSubscriptionValid())
            buttonStart.Enabled = false;

        if (ServerApi.Enabled && !string.IsNullOrEmpty(ServerApi.CurrentToken))
        {
            _sessionCheckTimer = new System.Windows.Forms.Timer { Interval = 15000 };
            _sessionCheckTimer.Tick += async (_, _) =>
            {
                if (string.IsNullOrEmpty(ServerApi.CurrentToken)) return;
                var valid = await ServerApi.ValidateSessionAsync(ServerApi.CurrentToken);
                if (valid) return;
                _sessionCheckTimer?.Stop();
                if (InvokeRequired)
                    BeginInvoke(() =>
                    {
                        AppLog.WriteLine("세션 만료. 프로그램을 종료합니다.");
                        Application.Exit();
                    });
                else
                {
                    AppLog.WriteLine("세션 만료. 프로그램을 종료합니다.");
                    Application.Exit();
                }
            };
            _sessionCheckTimer.Start();
        }
    }

    private void ApplyDarkTitleBar()
    {
        if (!IsHandleCreated) return;
        int dark = 1;
        DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, 4);
        // 타이틀바 색 = 패널 색 (45,45,48) BGR
        int captionColor = 0x00302D2D;
        DwmSetWindowAttribute(Handle, DWMWA_CAPTION_COLOR, ref captionColor, 4);
    }

    private void AddAttemptedPhrase(string phraseLine)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => AddAttemptedPhrase(phraseLine));
            return;
        }
        _attemptedPhrases.Add(phraseLine);
        while (_attemptedPhrases.Count > MaxPhrases)
            _attemptedPhrases.RemoveAt(0);
        listBoxPhrases.Items.Clear();
        for (int i = _attemptedPhrases.Count - 1; i >= 0; i--)
            listBoxPhrases.Items.Add(_attemptedPhrases[i]);
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        _escCheckTimer?.Stop();
        _escCheckTimer?.Dispose();
        _sessionCheckTimer?.Stop();
        _sessionCheckTimer?.Dispose();
    }

    private void AppendLog(string text)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => AppendLog(text));
            return;
        }
        richTextBoxLog.SelectionStart = richTextBoxLog.TextLength;
        richTextBoxLog.SelectionLength = 0;
        richTextBoxLog.SelectionColor = LogAccent;
        richTextBoxLog.AppendText(text);
        richTextBoxLog.ScrollToCaret();
    }

    private void AppendLogRed(string text)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => AppendLogRed(text));
            return;
        }
        richTextBoxLog.SelectionStart = richTextBoxLog.TextLength;
        richTextBoxLog.SelectionLength = 0;
        richTextBoxLog.SelectionColor = LogRed;
        richTextBoxLog.AppendText(text);
        richTextBoxLog.ScrollToCaret();
    }

    private void RefreshExpiryLabel()
    {
        var exp = ServerApi.SubscriptionExpiry;
        if (!exp.HasValue)
        {
            labelExpiry.Text = "사용기간: 없음";
            labelExpiry.ForeColor = LogRed;
            return;
        }
        var local = exp.Value.ToLocalTime();
        labelExpiry.Text = "만료일: " + local.ToString("yyyy-MM-dd");
        labelExpiry.ForeColor = ServerApi.IsSubscriptionValid() ? LogAccent : LogRed;
    }

    private void RefreshWalletCount()
    {
        var n = WalletCountFile.Read();
        labelWalletCount.Text = "지금까지 찾은 지갑수: " + n;
    }

    private void ButtonStart_Click(object? sender, EventArgs e)
    {
        if (_running) return;

        if (!ServerApi.IsSubscriptionValid())
        {
            MessageBox.Show(this, "이용기간이 없거나 만료되었습니다. 프로그램을 종료합니다.", "이용기간", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            Application.Exit();
            return;
        }

        _running = true;
        MainLoop.StopFlag = false;
        buttonStart.Enabled = false;
        buttonStop.Enabled = true;
        richTextBoxLog.Clear();

        // ESC 폴링: 핫키가 안 먹을 때 대비해 200ms마다 ESC 키 감지
        _escCheckTimer?.Stop();
        _escCheckTimer = new System.Windows.Forms.Timer { Interval = 200 };
        _escCheckTimer.Tick += (_, _) =>
        {
            if (!_running) return;
            if ((GetAsyncKeyState(VK_ESCAPE) & 0x8000) != 0)
            {
                MainLoop.StopFlag = true;
                AppLog.WriteLine("중지");
            }
        };
        _escCheckTimer.Start();

        _workerThread = new Thread(() =>
        {
            try
            {
                AutomationRunner.Run();
            }
            catch (Exception ex)
            {
                AppLog.WriteLine("오류 발생");
                try { File.WriteAllText(Path.Combine(ChromeImageMatcher.BaseDir, "error_log.txt"), ex.ToString()); } catch { }
            }
            finally
            {
                if (InvokeRequired)
                    BeginInvoke(OnWorkerFinished);
                else
                    OnWorkerFinished();
            }
        })
        { IsBackground = true };
        _workerThread.Start();
    }

    private void OnWorkerFinished()
    {
        _running = false;
        _escCheckTimer?.Stop();
        buttonStart.Enabled = ServerApi.IsSubscriptionValid();
        buttonStop.Enabled = false;
        RefreshWalletCount();
    }

    private void ButtonStop_Click(object? sender, EventArgs e)
    {
        if (!_running) return;
        AppLog.WriteLine("중지");
        MainLoop.StopFlag = true;
    }

}
