namespace MainPatchedImproved;

partial class LoginForm
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
        this.panelMain = new Panel();
        this.pictureBoxLogo = new PictureBox();
        this.labelTitle = new Label();
        this.labelId = new Label();
        this.textBoxId = new TextBox();
        this.labelPassword = new Label();
        this.textBoxPassword = new TextBox();
        this.buttonLogin = new Button();
        this.labelError = new Label();
        this.labelTelegram = new Label();
        this.panelMain.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)(this.pictureBoxLogo)).BeginInit();
        this.SuspendLayout();

        var bgDark = Color.FromArgb(30, 30, 30);
        var bgPanel = Color.FromArgb(45, 45, 48);
        var fg = Color.FromArgb(212, 212, 212);
        var accent = Color.FromArgb(78, 201, 176);

        // panelMain
        this.panelMain.BackColor = bgPanel;
        this.panelMain.BorderStyle = BorderStyle.FixedSingle;
        this.panelMain.Controls.Add(this.pictureBoxLogo);
        this.panelMain.Controls.Add(this.labelTitle);
        this.panelMain.Controls.Add(this.labelId);
        this.panelMain.Controls.Add(this.textBoxId);
        this.panelMain.Controls.Add(this.labelPassword);
        this.panelMain.Controls.Add(this.textBoxPassword);
        this.panelMain.Controls.Add(this.buttonLogin);
        this.panelMain.Controls.Add(this.labelError);
        this.panelMain.Controls.Add(this.labelTelegram);
        this.panelMain.Location = new Point(40, 40);
        this.panelMain.Name = "panelMain";
        this.panelMain.Size = new Size(320, 360);
        this.panelMain.TabIndex = 0;

        // pictureBoxLogo (배경을 패널과 맞춰서 이미지가 확실히 그려지도록)
        this.pictureBoxLogo.BackColor = bgPanel;
        this.pictureBoxLogo.Location = new Point(110, 12);
        this.pictureBoxLogo.Name = "pictureBoxLogo";
        this.pictureBoxLogo.Size = new Size(100, 100);
        this.pictureBoxLogo.SizeMode = PictureBoxSizeMode.Zoom;
        this.pictureBoxLogo.TabIndex = 0;
        this.pictureBoxLogo.TabStop = false;

        // labelTitle
        this.labelTitle.Font = new Font("Consolas", 14F, FontStyle.Bold);
        this.labelTitle.ForeColor = accent;
        this.labelTitle.Location = new Point(20, 118);
        this.labelTitle.Name = "labelTitle";
        this.labelTitle.Size = new Size(280, 28);
        this.labelTitle.Text = "Nexus v1.0.0";
        this.labelTitle.TextAlign = ContentAlignment.MiddleCenter;

        // labelId
        this.labelId.Font = new Font("Segoe UI", 9F);
        this.labelId.ForeColor = fg;
        this.labelId.Location = new Point(24, 154);
        this.labelId.Name = "labelId";
        this.labelId.Size = new Size(272, 20);
        this.labelId.Text = "아이디";

        // textBoxId
        this.textBoxId.BackColor = Color.FromArgb(37, 37, 38);
        this.textBoxId.BorderStyle = BorderStyle.FixedSingle;
        this.textBoxId.Font = new Font("Segoe UI", 10F);
        this.textBoxId.ForeColor = fg;
        this.textBoxId.Location = new Point(24, 176);
        this.textBoxId.Name = "textBoxId";
        this.textBoxId.Size = new Size(272, 25);
        this.textBoxId.TabIndex = 0;

        // labelPassword
        this.labelPassword.Font = new Font("Segoe UI", 9F);
        this.labelPassword.ForeColor = fg;
        this.labelPassword.Location = new Point(24, 206);
        this.labelPassword.Name = "labelPassword";
        this.labelPassword.Size = new Size(272, 20);
        this.labelPassword.Text = "비밀번호";

        // textBoxPassword
        this.textBoxPassword.BackColor = Color.FromArgb(37, 37, 38);
        this.textBoxPassword.BorderStyle = BorderStyle.FixedSingle;
        this.textBoxPassword.Font = new Font("Segoe UI", 10F);
        this.textBoxPassword.ForeColor = fg;
        this.textBoxPassword.Location = new Point(24, 228);
        this.textBoxPassword.Name = "textBoxPassword";
        this.textBoxPassword.PasswordChar = '●';
        this.textBoxPassword.Size = new Size(272, 25);
        this.textBoxPassword.TabIndex = 1;

        // buttonLogin
        this.buttonLogin.BackColor = accent;
        this.buttonLogin.Cursor = Cursors.Hand;
        this.buttonLogin.FlatAppearance.BorderSize = 0;
        this.buttonLogin.FlatStyle = FlatStyle.Flat;
        this.buttonLogin.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        this.buttonLogin.ForeColor = Color.FromArgb(30, 30, 30);
        this.buttonLogin.Location = new Point(24, 278);
        this.buttonLogin.Name = "buttonLogin";
        this.buttonLogin.Size = new Size(272, 44);
        this.buttonLogin.Text = "로그인";
        this.buttonLogin.UseVisualStyleBackColor = false;
        this.buttonLogin.Click += ButtonLogin_Click;

        // labelError (로그인 실패 시 버튼 위에 표시)
        this.labelError.AutoSize = true;
        this.labelError.ForeColor = Color.FromArgb(255, 100, 100);
        this.labelError.Location = new Point(24, 256);
        this.labelError.Name = "labelError";
        this.labelError.Size = new Size(0, 15);
        this.labelError.Visible = false;

        // labelTelegram (텔레그램 문의 - 서버에서 가져옴)
        this.labelTelegram.Font = new Font("Segoe UI", 8F);
        this.labelTelegram.ForeColor = Color.FromArgb(150, 150, 150);
        this.labelTelegram.Location = new Point(24, 328);
        this.labelTelegram.Name = "labelTelegram";
        this.labelTelegram.Size = new Size(272, 32);
        this.labelTelegram.Text = "서버 로그인 · 텔레그램 문의: (불러오는 중)";
        this.labelTelegram.TextAlign = ContentAlignment.TopCenter;

        // LoginForm
        this.AutoScaleDimensions = new SizeF(7F, 15F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.BackColor = bgDark;
        this.ClientSize = new Size(400, 440);
        this.Controls.Add(this.panelMain);
        this.FormBorderStyle = FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;
        this.Name = "LoginForm";
        this.StartPosition = FormStartPosition.CenterScreen;
        this.Text = "Nexus";
        this.panelMain.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)(this.pictureBoxLogo)).EndInit();
        this.ResumeLayout(false);
    }

    private Panel panelMain;
    private PictureBox pictureBoxLogo;
    private Label labelTitle;
    private Label labelId;
    private TextBox textBoxId;
    private Label labelPassword;
    private TextBox textBoxPassword;
    private Button buttonLogin;
    private Label labelError;
    private Label labelTelegram;
}
