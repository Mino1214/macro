using System.Drawing;
using System.IO;

namespace MacroWinForms;

public partial class WalletFlowForm : Form
{
    private volatile bool _running;
    private Thread? _workerThread;
    private readonly object _logLock = new();

    public WalletFlowForm()
    {
        InitializeComponent();
        textBoxMnemonicPath.Text = WalletFlowMacro.MnemonicsFile;
        textBoxPassword.Text = Environment.GetEnvironmentVariable("EXT_PASSWORD") ?? "";
        textBoxThreshold.Text = "0.8";
    }

    private void ButtonBrowse_Click(object? sender, EventArgs e)
    {
        var initialDir = Directory.Exists(WalletFlowMacro.DataDir) ? WalletFlowMacro.DataDir : WalletFlowMacro.BaseDir;
        using var dlg = new OpenFileDialog
        {
            Title = "니모닉 파일 선택",
            InitialDirectory = initialDir,
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*"
        };
        if (dlg.ShowDialog() == DialogResult.OK)
            textBoxMnemonicPath.Text = dlg.FileName;
    }

    private void ButtonStart_Click(object? sender, EventArgs e)
    {
        if (_running) return;

        var mnemoPath = textBoxMnemonicPath.Text.Trim();
        if (string.IsNullOrEmpty(mnemoPath)) mnemoPath = WalletFlowMacro.MnemonicsFile;
        var password = textBoxPassword.Text.Trim();
        if (!double.TryParse(textBoxThreshold.Text.Trim(), out var threshold) || threshold <= 0 || threshold > 1)
        {
            MessageBox.Show("threshold는 0보다 크고 1 이하의 숫자여야 합니다.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (!File.Exists(mnemoPath))
        {
            MessageBox.Show($"니모닉 파일을 찾을 수 없습니다:\n{mnemoPath}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            if (MessageBox.Show("비밀번호가 비어 있습니다. 계속 진행할까요?", "확인", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
        }

        _running = true;
        buttonStart.Enabled = false;
        buttonStop.Enabled = true;
        AppendLog($"시작: 파일={mnemoPath}, threshold={threshold}\r\n");

        _workerThread = new Thread(() => WorkerRun(mnemoPath, password, threshold))
        {
            IsBackground = true
        };
        _workerThread.Start();
    }

    private void ButtonStop_Click(object? sender, EventArgs e)
    {
        if (!_running) return;
        AppendLog("중지 요청...\r\n");
        _running = false;
    }

    private void WorkerRun(string mnemoPath, string password, double threshold)
    {
        try
        {
            var sets = WalletFlowMacro.LoadMnemonics(mnemoPath);
            if (sets.Count == 0)
            {
                AppendLog("니모닉 세트를 찾지 못했습니다.\r\n");
                Finish();
                return;
            }

            AppendLog($"총 {sets.Count}개의 니모닉 세트 실행을 시작합니다.\r\n");

            for (int idx = 1; idx <= sets.Count; idx++)
            {
                if (!_running)
                {
                    AppendLog("사용자에 의해 중지되었습니다.\r\n");
                    break;
                }

                AppendLog($"[{idx}/{sets.Count}] 니모닉 실행 중...\r\n");
                try
                {
                    var result = WalletFlowMacro.RunFullFlowForMnemonic(sets[idx - 1], password, threshold);
                    WalletFlowMacro.AppendResultLog(sets[idx - 1], result, WalletFlowMacro.ResultsFile);
                    if (result.Success)
                        AppendLog($"  -> 성공: balance={result.Balance}\r\n");
                    else
                        AppendLog($"  -> 실패: {result.Error}\r\n");
                }
                catch (Exception ex)
                {
                    AppendLog($"  -> 예외 발생: {ex.Message}\r\n");
                }
            }
        }
        catch (Exception ex)
        {
            AppendLog($"니모닉 파일 읽기 오류: {ex.Message}\r\n");
        }

        Finish();
    }

    private void Finish()
    {
        if (InvokeRequired)
        {
            BeginInvoke(Finish);
            return;
        }
        _running = false;
        buttonStart.Enabled = true;
        buttonStop.Enabled = false;
        AppendLog("작업이 종료되었습니다.\r\n");
    }

    public void AppendLog(string text)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => AppendLog(text));
            return;
        }
        lock (_logLock)
        {
            textBoxLog.AppendText(text);
        }
    }
}
