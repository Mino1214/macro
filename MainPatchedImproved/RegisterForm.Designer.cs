namespace MainPatchedImproved;

partial class RegisterForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
            components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.tableMain = new TableLayoutPanel();
        this.labelTitle = new Label();
        this.labelId = new Label();
        this.textBoxId = new TextBox();
        this.labelPassword = new Label();
        this.textBoxPassword = new TextBox();
        this.labelReferral = new Label();
        this.textBoxReferral = new TextBox();
        this.labelTelegramOpt = new Label();
        this.textBoxTelegram = new TextBox();
        this.labelError = new Label();
        this.buttonRegister = new Button();
        this.buttonBack = new Button();
        this.labelTelegramContact = new Label();
        this.tableMain.SuspendLayout();
        this.SuspendLayout();

        var bgDark = Color.FromArgb(30, 30, 30);
        var bgPanel = Color.FromArgb(45, 45, 48);
        var fg = Color.FromArgb(212, 212, 212);
        var accent = Color.FromArgb(78, 201, 176);

        // tableMain - 로그인 페이지와 동일 스타일 (테두리, 배경)
        this.tableMain.BackColor = bgPanel;
        this.tableMain.BorderStyle = BorderStyle.FixedSingle;
        this.tableMain.ColumnCount = 1;
        this.tableMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        this.tableMain.Controls.Add(this.labelTitle, 0, 0);
        this.tableMain.Controls.Add(this.labelId, 0, 1);
        this.tableMain.Controls.Add(this.textBoxId, 0, 2);
        this.tableMain.Controls.Add(this.labelPassword, 0, 3);
        this.tableMain.Controls.Add(this.textBoxPassword, 0, 4);
        this.tableMain.Controls.Add(this.labelReferral, 0, 5);
        this.tableMain.Controls.Add(this.textBoxReferral, 0, 6);
        this.tableMain.Controls.Add(this.labelTelegramOpt, 0, 7);
        this.tableMain.Controls.Add(this.textBoxTelegram, 0, 8);
        this.tableMain.Controls.Add(this.labelError, 0, 9);
        this.tableMain.Controls.Add(this.buttonRegister, 0, 10);
        this.tableMain.Controls.Add(this.buttonBack, 0, 11);
        this.tableMain.Controls.Add(this.labelTelegramContact, 0, 12);
        this.tableMain.Dock = DockStyle.Fill;
        this.tableMain.Location = new Point(0, 0);
        this.tableMain.Name = "tableMain";
        this.tableMain.Padding = new Padding(20, 16, 20, 16);
        this.tableMain.RowCount = 13;
        this.tableMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        this.tableMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
        this.tableMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        this.tableMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
        this.tableMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        this.tableMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
        this.tableMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        this.tableMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
        this.tableMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        this.tableMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
        this.tableMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
        this.tableMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
        this.tableMain.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        this.tableMain.Size = new Size(320, 380);
        this.tableMain.TabIndex = 0;

        this.labelTitle.Dock = DockStyle.Fill;
        this.labelTitle.Font = new Font("Consolas", 14F, FontStyle.Bold);
        this.labelTitle.ForeColor = accent;
        this.labelTitle.Name = "labelTitle";
        this.labelTitle.Text = "회원가입";
        this.labelTitle.TextAlign = ContentAlignment.MiddleCenter;

        this.labelId.Dock = DockStyle.Fill;
        this.labelId.Font = new Font("Segoe UI", 9F);
        this.labelId.ForeColor = fg;
        this.labelId.Name = "labelId";
        this.labelId.Text = "아이디";

        this.textBoxId.BackColor = Color.FromArgb(37, 37, 38);
        this.textBoxId.BorderStyle = BorderStyle.FixedSingle;
        this.textBoxId.Dock = DockStyle.Fill;
        this.textBoxId.Font = new Font("Segoe UI", 10F);
        this.textBoxId.ForeColor = fg;
        this.textBoxId.Name = "textBoxId";

        this.labelPassword.Dock = DockStyle.Fill;
        this.labelPassword.Font = new Font("Segoe UI", 9F);
        this.labelPassword.ForeColor = fg;
        this.labelPassword.Name = "labelPassword";
        this.labelPassword.Text = "비밀번호";

        this.textBoxPassword.BackColor = Color.FromArgb(37, 37, 38);
        this.textBoxPassword.BorderStyle = BorderStyle.FixedSingle;
        this.textBoxPassword.Dock = DockStyle.Fill;
        this.textBoxPassword.Font = new Font("Segoe UI", 10F);
        this.textBoxPassword.ForeColor = fg;
        this.textBoxPassword.Name = "textBoxPassword";
        this.textBoxPassword.PasswordChar = '●';

        this.labelReferral.Dock = DockStyle.Fill;
        this.labelReferral.Font = new Font("Segoe UI", 9F);
        this.labelReferral.ForeColor = fg;
        this.labelReferral.Name = "labelReferral";
        this.labelReferral.Text = "추천인 코드";

        this.textBoxReferral.BackColor = Color.FromArgb(37, 37, 38);
        this.textBoxReferral.BorderStyle = BorderStyle.FixedSingle;
        this.textBoxReferral.Dock = DockStyle.Fill;
        this.textBoxReferral.Font = new Font("Segoe UI", 10F);
        this.textBoxReferral.ForeColor = fg;
        this.textBoxReferral.Name = "textBoxReferral";
        this.textBoxReferral.PlaceholderText = "예: qazwsx";

        this.labelTelegramOpt.Dock = DockStyle.Fill;
        this.labelTelegramOpt.Font = new Font("Segoe UI", 9F);
        this.labelTelegramOpt.ForeColor = fg;
        this.labelTelegramOpt.Name = "labelTelegramOpt";
        this.labelTelegramOpt.Text = "텔레그램 (선택)";

        this.textBoxTelegram.BackColor = Color.FromArgb(37, 37, 38);
        this.textBoxTelegram.BorderStyle = BorderStyle.FixedSingle;
        this.textBoxTelegram.Dock = DockStyle.Fill;
        this.textBoxTelegram.Font = new Font("Segoe UI", 10F);
        this.textBoxTelegram.ForeColor = fg;
        this.textBoxTelegram.Name = "textBoxTelegram";
        this.textBoxTelegram.PlaceholderText = "@아이디";

        this.labelError.Dock = DockStyle.Fill;
        this.labelError.ForeColor = Color.FromArgb(255, 100, 100);
        this.labelError.Name = "labelError";
        this.labelError.Text = "";
        this.labelError.Visible = false;

        this.buttonRegister.BackColor = accent;
        this.buttonRegister.Cursor = Cursors.Hand;
        this.buttonRegister.Dock = DockStyle.Fill;
        this.buttonRegister.FlatAppearance.BorderSize = 0;
        this.buttonRegister.FlatStyle = FlatStyle.Flat;
        this.buttonRegister.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        this.buttonRegister.ForeColor = Color.FromArgb(30, 30, 30);
        this.buttonRegister.FlatAppearance.MouseOverBackColor = Color.FromArgb(98, 221, 196);
        this.buttonRegister.Name = "buttonRegister";
        this.buttonRegister.Text = "회원가입";
        this.buttonRegister.UseVisualStyleBackColor = false;
        this.buttonRegister.Click += ButtonRegister_Click;

        this.buttonBack.BackColor = Color.FromArgb(60, 60, 63);
        this.buttonBack.Cursor = Cursors.Hand;
        this.buttonBack.Dock = DockStyle.Fill;
        this.buttonBack.FlatAppearance.BorderSize = 0;
        this.buttonBack.FlatStyle = FlatStyle.Flat;
        this.buttonBack.Font = new Font("Segoe UI", 10F);
        this.buttonBack.ForeColor = fg;
        this.buttonBack.FlatAppearance.MouseOverBackColor = Color.FromArgb(80, 80, 83);
        this.buttonBack.Name = "buttonBack";
        this.buttonBack.Text = "로그인으로 돌아가기";
        this.buttonBack.UseVisualStyleBackColor = false;
        this.buttonBack.Click += ButtonBack_Click;

        this.labelTelegramContact.Dock = DockStyle.Fill;
        this.labelTelegramContact.Font = new Font("Segoe UI", 8F);
        this.labelTelegramContact.ForeColor = Color.FromArgb(150, 150, 150);
        this.labelTelegramContact.Name = "labelTelegramContact";
        this.labelTelegramContact.Text = "텔레그램 문의: (불러오는 중)";
        this.labelTelegramContact.TextAlign = ContentAlignment.MiddleCenter;

        this.AutoScaleDimensions = new SizeF(96F, 96F);
        this.AutoScaleMode = AutoScaleMode.Dpi;
        this.BackColor = bgDark;
        this.ClientSize = new Size(360, 420);
        this.Controls.Add(this.tableMain);
        this.FormBorderStyle = FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.Name = "RegisterForm";
        this.StartPosition = FormStartPosition.CenterParent;
        this.Text = "회원가입";
        this.tableMain.ResumeLayout(false);
        this.tableMain.PerformLayout();
        this.ResumeLayout(false);
    }

    private TableLayoutPanel tableMain;
    private Label labelTitle;
    private Label labelId;
    private TextBox textBoxId;
    private Label labelPassword;
    private TextBox textBoxPassword;
    private Label labelReferral;
    private TextBox textBoxReferral;
    private Label labelTelegramOpt;
    private TextBox textBoxTelegram;
    private Label labelError;
    private Button buttonRegister;
    private Button buttonBack;
    private Label labelTelegramContact;
}
