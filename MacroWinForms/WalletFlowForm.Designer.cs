namespace MacroWinForms;

partial class WalletFlowForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
            components.Dispose();
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
        this.labelMnemonic = new Label();
        this.textBoxMnemonicPath = new TextBox();
        this.buttonBrowse = new Button();
        this.labelPassword = new Label();
        this.textBoxPassword = new TextBox();
        this.labelThreshold = new Label();
        this.textBoxThreshold = new TextBox();
        this.buttonStart = new Button();
        this.buttonStop = new Button();
        this.textBoxLog = new TextBox();
        this.labelInfo = new Label();
        this.SuspendLayout();

        // labelMnemonic
        this.labelMnemonic.AutoSize = true;
        this.labelMnemonic.Location = new Point(12, 15);
        this.labelMnemonic.Name = "labelMnemonic";
        this.labelMnemonic.Size = new Size(76, 15);
        this.labelMnemonic.Text = "니모닉 파일:";

        // textBoxMnemonicPath
        this.textBoxMnemonicPath.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        this.textBoxMnemonicPath.Location = new Point(110, 12);
        this.textBoxMnemonicPath.Name = "textBoxMnemonicPath";
        this.textBoxMnemonicPath.Size = new Size(420, 23);
        this.textBoxMnemonicPath.TabIndex = 1;

        // buttonBrowse
        this.buttonBrowse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        this.buttonBrowse.Location = new Point(536, 11);
        this.buttonBrowse.Name = "buttonBrowse";
        this.buttonBrowse.Size = new Size(75, 25);
        this.buttonBrowse.Text = "찾기...";
        this.buttonBrowse.UseVisualStyleBackColor = true;
        this.buttonBrowse.Click += ButtonBrowse_Click;

        // labelPassword
        this.labelPassword.AutoSize = true;
        this.labelPassword.Location = new Point(12, 48);
        this.labelPassword.Name = "labelPassword";
        this.labelPassword.Size = new Size(92, 15);
        this.labelPassword.Text = "지갑 비밀번호:";

        // textBoxPassword
        this.textBoxPassword.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        this.textBoxPassword.Location = new Point(110, 45);
        this.textBoxPassword.Name = "textBoxPassword";
        this.textBoxPassword.PasswordChar = '*';
        this.textBoxPassword.Size = new Size(501, 23);
        this.textBoxPassword.TabIndex = 2;

        // labelThreshold
        this.labelThreshold.AutoSize = true;
        this.labelThreshold.Location = new Point(12, 81);
        this.labelThreshold.Name = "labelThreshold";
        this.labelThreshold.Size = new Size(158, 15);
        this.labelThreshold.Text = "이미지 매칭 threshold (0~1):";

        // textBoxThreshold
        this.textBoxThreshold.Location = new Point(176, 78);
        this.textBoxThreshold.Name = "textBoxThreshold";
        this.textBoxThreshold.Size = new Size(80, 23);
        this.textBoxThreshold.TabIndex = 3;

        // buttonStart
        this.buttonStart.Location = new Point(12, 112);
        this.buttonStart.Name = "buttonStart";
        this.buttonStart.Size = new Size(75, 28);
        this.buttonStart.Text = "시작";
        this.buttonStart.UseVisualStyleBackColor = true;
        this.buttonStart.Click += ButtonStart_Click;

        // buttonStop
        this.buttonStop.Enabled = false;
        this.buttonStop.Location = new Point(93, 112);
        this.buttonStop.Name = "buttonStop";
        this.buttonStop.Size = new Size(75, 28);
        this.buttonStop.Text = "중지";
        this.buttonStop.UseVisualStyleBackColor = true;
        this.buttonStop.Click += ButtonStop_Click;

        // textBoxLog
        this.textBoxLog.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        this.textBoxLog.Location = new Point(12, 150);
        this.textBoxLog.Multiline = true;
        this.textBoxLog.Name = "textBoxLog";
        this.textBoxLog.ReadOnly = true;
        this.textBoxLog.ScrollBars = ScrollBars.Vertical;
        this.textBoxLog.Size = new Size(616, 298);
        this.textBoxLog.TabIndex = 4;

        // labelInfo
        this.labelInfo.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        this.labelInfo.AutoEllipsis = true;
        this.labelInfo.Location = new Point(12, 456);
        this.labelInfo.Name = "labelInfo";
        this.labelInfo.Size = new Size(616, 18);
        this.labelInfo.Text = $"니모닉: {WalletFlowMacro.MnemonicsFile} / 결과 로그: {WalletFlowMacro.ResultsFile}";

        // WalletFlowForm
        this.AutoScaleDimensions = new SizeF(7F, 15F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.ClientSize = new Size(640, 480);
        this.Controls.Add(this.labelInfo);
        this.Controls.Add(this.textBoxLog);
        this.Controls.Add(this.buttonStop);
        this.Controls.Add(this.buttonStart);
        this.Controls.Add(this.textBoxThreshold);
        this.Controls.Add(this.labelThreshold);
        this.Controls.Add(this.textBoxPassword);
        this.Controls.Add(this.labelPassword);
        this.Controls.Add(this.buttonBrowse);
        this.Controls.Add(this.textBoxMnemonicPath);
        this.Controls.Add(this.labelMnemonic);
        this.MinimumSize = new Size(500, 400);
        this.Name = "WalletFlowForm";
        this.StartPosition = FormStartPosition.CenterScreen;
        this.Text = "Safepal 이미지 매크로 GUI";
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    #endregion

    private Label labelMnemonic;
    private TextBox textBoxMnemonicPath;
    private Button buttonBrowse;
    private Label labelPassword;
    private TextBox textBoxPassword;
    private Label labelThreshold;
    private TextBox textBoxThreshold;
    private Button buttonStart;
    private Button buttonStop;
    private TextBox textBoxLog;
    private Label labelInfo;
}
