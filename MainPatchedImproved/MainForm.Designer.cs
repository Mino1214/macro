namespace MainPatchedImproved;

partial class MainForm
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
        this.splitContainer = new SplitContainer();
        this.textBoxLog = new TextBox();
        this.panelTop = new Panel();
        this.buttonStart = new Button();
        this.buttonStop = new Button();
        this.labelTitle = new Label();
        this.panelPhrases = new Panel();
        this.labelPhrases = new Label();
        this.listBoxPhrases = new ListBox();
        ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).BeginInit();
        this.splitContainer.Panel1.SuspendLayout();
        this.splitContainer.Panel2.SuspendLayout();
        this.splitContainer.SuspendLayout();
        this.panelTop.SuspendLayout();
        this.panelPhrases.SuspendLayout();
        this.SuspendLayout();

        // Colors - terminal modern dark
        var bgDark = Color.FromArgb(30, 30, 30);
        var bgPanel = Color.FromArgb(45, 45, 48);
        var fg = Color.FromArgb(212, 212, 212);
        var accent = Color.FromArgb(78, 201, 176);

        // splitContainer
        this.splitContainer.BackColor = bgDark;
        this.splitContainer.Dock = DockStyle.Fill;
        this.splitContainer.FixedPanel = FixedPanel.Panel2;
        this.splitContainer.Location = new Point(0, 0);
        this.splitContainer.Name = "splitContainer";
        // Panel1 = 로그
        this.splitContainer.Panel1.BackColor = bgDark;
        this.splitContainer.Panel1.Controls.Add(this.textBoxLog);
        this.splitContainer.Panel1.Padding = new Padding(8);
        // Panel2 = 시도한 문구
        this.splitContainer.Panel2.BackColor = bgPanel;
        this.splitContainer.Panel2.Controls.Add(this.panelPhrases);
        this.splitContainer.Panel2MinSize = 280;
        this.splitContainer.Size = new Size(884, 461);
        this.splitContainer.SplitterDistance = 560;
        this.splitContainer.SplitterWidth = 6;
        this.splitContainer.TabIndex = 0;

        // textBoxLog - 터미널 로그 (스크롤바 항상 표시)
        this.textBoxLog.BackColor = Color.FromArgb(28, 28, 28);
        this.textBoxLog.BorderStyle = BorderStyle.None;
        this.textBoxLog.Dock = DockStyle.Fill;
        this.textBoxLog.Font = new Font("Consolas", 10F);
        this.textBoxLog.ForeColor = accent;
        this.textBoxLog.Multiline = true;
        this.textBoxLog.Name = "textBoxLog";
        this.textBoxLog.ReadOnly = true;
        this.textBoxLog.ScrollBars = ScrollBars.None;
        this.textBoxLog.WordWrap = true;
        this.textBoxLog.Size = new Size(544, 445);
        this.textBoxLog.TabIndex = 0;

        // panelTop
        this.panelTop.BackColor = bgPanel;
        this.panelTop.Controls.Add(this.labelTitle);
        this.panelTop.Controls.Add(this.buttonStart);
        this.panelTop.Controls.Add(this.buttonStop);
        this.panelTop.Dock = DockStyle.Top;
        this.panelTop.Height = 52;
        this.panelTop.Padding = new Padding(12, 8, 12, 8);
        this.panelTop.Name = "panelTop";
        this.panelTop.Size = new Size(884, 52);
        this.panelTop.TabIndex = 1;

        // buttonStart
        this.buttonStart.BackColor = accent;
        this.buttonStart.Cursor = Cursors.Hand;
        this.buttonStart.FlatAppearance.BorderSize = 0;
        this.buttonStart.FlatStyle = FlatStyle.Flat;
        this.buttonStart.Font = new Font("Segoe UI", 9.5F);
        this.buttonStart.ForeColor = Color.FromArgb(30, 30, 30);
        this.buttonStart.Location = new Point(12, 10);
        this.buttonStart.Name = "buttonStart";
        this.buttonStart.Size = new Size(100, 32);
        this.buttonStart.Text = "시작";
        this.buttonStart.UseVisualStyleBackColor = false;
        this.buttonStart.Click += ButtonStart_Click;

        // buttonStop
        this.buttonStop.BackColor = Color.FromArgb(80, 80, 80);
        this.buttonStop.Enabled = false;
        this.buttonStop.FlatAppearance.BorderSize = 0;
        this.buttonStop.FlatStyle = FlatStyle.Flat;
        this.buttonStop.Font = new Font("Segoe UI", 9.5F);
        this.buttonStop.ForeColor = fg;
        this.buttonStop.Location = new Point(118, 10);
        this.buttonStop.Name = "buttonStop";
        this.buttonStop.Size = new Size(100, 32);
        this.buttonStop.Text = "중지";
        this.buttonStop.UseVisualStyleBackColor = false;
        this.buttonStop.Click += ButtonStop_Click;

        // labelTitle
        this.labelTitle.AutoSize = true;
        this.labelTitle.Font = new Font("Consolas", 9F);
        this.labelTitle.ForeColor = Color.FromArgb(150, 150, 150);
        this.labelTitle.Location = new Point(355, 18);
        this.labelTitle.Name = "labelTitle";
        this.labelTitle.Text = "⌨ ESC = 중지  |  터미널 로그";

        // panelPhrases
        this.panelPhrases.BackColor = bgPanel;
        this.panelPhrases.Dock = DockStyle.Fill;
        this.panelPhrases.Padding = new Padding(10, 8, 10, 10);
        this.panelPhrases.Controls.Add(this.labelPhrases);
        this.panelPhrases.Controls.Add(this.listBoxPhrases);
        this.panelPhrases.Name = "panelPhrases";

        // labelPhrases
        this.labelPhrases.Dock = DockStyle.Top;
        this.labelPhrases.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        this.labelPhrases.ForeColor = accent;
        this.labelPhrases.Location = new Point(10, 8);
        this.labelPhrases.Height = 24;
        this.labelPhrases.Name = "labelPhrases";
        this.labelPhrases.Text = "시도한 문구 (최근 100개)";
        this.labelPhrases.AutoSize = false;

        // listBoxPhrases (스크롤바 항상 표시)
        this.listBoxPhrases.BackColor = Color.FromArgb(37, 37, 38);
        this.listBoxPhrases.BorderStyle = BorderStyle.None;
        this.listBoxPhrases.Dock = DockStyle.Fill;
        this.listBoxPhrases.Font = new Font("Consolas", 9F);
        this.listBoxPhrases.ForeColor = fg;
        this.listBoxPhrases.FormattingEnabled = true;
        this.listBoxPhrases.IntegralHeight = false;
        this.listBoxPhrases.ItemHeight = 18;
        this.listBoxPhrases.Name = "listBoxPhrases";
        this.listBoxPhrases.Size = new Size(298, 409);
        this.listBoxPhrases.TabIndex = 0;

        // MainForm
        this.AutoScaleDimensions = new SizeF(7F, 15F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.BackColor = bgDark;
        this.ClientSize = new Size(884, 513);
        this.Controls.Add(this.splitContainer);
        this.Controls.Add(this.panelTop);
        this.MinimumSize = new Size(700, 400);
        this.Name = "MainForm";
        this.StartPosition = FormStartPosition.CenterScreen;
        this.Text = "Nexus v1.0.0 (Edge)";
        this.splitContainer.Panel1.ResumeLayout(false);
        this.splitContainer.Panel1.PerformLayout();
        this.splitContainer.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).EndInit();
        this.splitContainer.ResumeLayout(false);
        this.panelTop.ResumeLayout(false);
        this.panelTop.PerformLayout();
        this.panelPhrases.ResumeLayout(false);
        this.ResumeLayout(false);
    }

    #endregion

    private SplitContainer splitContainer;
    private Panel panelTop;
    private Panel panelPhrases;
    private Button buttonStart;
    private Button buttonStop;
    private Label labelTitle;
    private Label labelPhrases;
    private TextBox textBoxLog;
    private ListBox listBoxPhrases;
}
