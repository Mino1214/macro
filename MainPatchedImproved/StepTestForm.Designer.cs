namespace MainPatchedImproved;

partial class StepTestForm
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
        this.buttonStep1 = new Button();
        this.buttonStep2 = new Button();
        this.buttonStep3 = new Button();
        this.textBoxLog = new TextBox();
        this.labelTitle = new Label();
        this.SuspendLayout();

        // labelTitle
        this.labelTitle.AutoSize = true;
        this.labelTitle.Location = new Point(12, 12);
        this.labelTitle.Name = "labelTitle";
        this.labelTitle.Size = new Size(280, 15);
        this.labelTitle.Text = "차근차근: 1) Edge 캡처 → 2) 메인페이지 → 3) 햄버거 메뉴";

        // buttonStep1 (원클릭 테스트 옆에)
        this.buttonStep1.Location = new Point(158, 38);
        this.buttonStep1.Name = "buttonStep1";
        this.buttonStep1.Size = new Size(140, 28);
        this.buttonStep1.Text = "1. Edge 캡처 저장";
        this.buttonStep1.UseVisualStyleBackColor = true;
        this.buttonStep1.Click += ButtonStep1_Click;

        // buttonStep2
        this.buttonStep2.Location = new Point(304, 38);
        this.buttonStep2.Name = "buttonStep2";
        this.buttonStep2.Size = new Size(140, 28);
        this.buttonStep2.Text = "2. 메인페이지 감지";
        this.buttonStep2.UseVisualStyleBackColor = true;
        this.buttonStep2.Click += ButtonStep2_Click;

        // buttonStep3
        this.buttonStep3.Location = new Point(450, 38);
        this.buttonStep3.Name = "buttonStep3";
        this.buttonStep3.Size = new Size(122, 28);
        this.buttonStep3.Text = "3. 햄버거 메뉴 감지";
        this.buttonStep3.UseVisualStyleBackColor = true;
        this.buttonStep3.Click += ButtonStep3_Click;

        // 원클릭 테스트: First Set → Second Set 한 사이클
        this.buttonOneClickTest = new Button();
        this.buttonOneClickTest.BackColor = System.Drawing.Color.LightGreen;
        this.buttonOneClickTest.Font = new Font(this.buttonOneClickTest.Font.FontFamily, 9.75F, FontStyle.Bold);
        this.buttonOneClickTest.Location = new Point(12, 38);
        this.buttonOneClickTest.Name = "buttonOneClickTest";
        this.buttonOneClickTest.Size = new Size(140, 28);
        this.buttonOneClickTest.Text = "원클릭 테스트";
        this.buttonOneClickTest.UseVisualStyleBackColor = false;
        this.buttonOneClickTest.Click += ButtonOneClickTest_Click;

        // labelMouse
        this.labelMouse = new Label();
        this.labelMouse.AutoSize = true;
        this.labelMouse.Font = new Font("Consolas", 9.75F);
        this.labelMouse.Location = new Point(12, 74);
        this.labelMouse.Name = "labelMouse";
        this.labelMouse.Size = new Size(180, 15);
        this.labelMouse.Text = "마우스: (0, 0)";

        // labelKeyHint
        this.labelKeyHint = new Label();
        this.labelKeyHint.AutoSize = true;
        this.labelKeyHint.ForeColor = System.Drawing.Color.Gray;
        this.labelKeyHint.Location = new Point(200, 74);
        this.labelKeyHint.Name = "labelKeyHint";
        this.labelKeyHint.Size = new Size(280, 15);
        this.labelKeyHint.Text = "First Set: 3→4→5→6→7 | Second Set: 1~12 (슬롯 이미지)";

        // buttonLogCoord
        this.buttonLogCoord = new Button();
        this.buttonLogCoord.Location = new Point(450, 70);
        this.buttonLogCoord.Name = "buttonLogCoord";
        this.buttonLogCoord.Size = new Size(122, 26);
        this.buttonLogCoord.Text = "현재 좌표 로그에 찍기";
        this.buttonLogCoord.UseVisualStyleBackColor = true;
        this.buttonLogCoord.Click += ButtonLogCoord_Click;

        // 1세트: 삼·사·오·육·칠 (3,4,5,6,7번)
        this.buttonNum3 = new Button();
        this.buttonNum3.Location = new Point(12, 100);
        this.buttonNum3.Name = "buttonNum3";
        this.buttonNum3.Size = new Size(72, 28);
        this.buttonNum3.Text = "3 햄버거";
        this.buttonNum3.UseVisualStyleBackColor = true;
        this.buttonNum3.Click += ButtonNum_Click;

        this.buttonNum4 = new Button();
        this.buttonNum4.Location = new Point(88, 100);
        this.buttonNum4.Name = "buttonNum4";
        this.buttonNum4.Size = new Size(72, 28);
        this.buttonNum4.Text = "4 addwallet";
        this.buttonNum4.UseVisualStyleBackColor = true;
        this.buttonNum4.Click += ButtonNum_Click;

        this.buttonNum5 = new Button();
        this.buttonNum5.Location = new Point(164, 100);
        this.buttonNum5.Name = "buttonNum5";
        this.buttonNum5.Size = new Size(72, 28);
        this.buttonNum5.Text = "5 getwallet";
        this.buttonNum5.UseVisualStyleBackColor = true;
        this.buttonNum5.Click += ButtonNum_Click;

        this.buttonNum6 = new Button();
        this.buttonNum6.Location = new Point(240, 100);
        this.buttonNum6.Name = "buttonNum6";
        this.buttonNum6.Size = new Size(72, 28);
        this.buttonNum6.Text = "6 mnemonic";
        this.buttonNum6.UseVisualStyleBackColor = true;
        this.buttonNum6.Click += ButtonNum_Click;

        this.buttonNum7 = new Button();
        this.buttonNum7.Location = new Point(316, 100);
        this.buttonNum7.Name = "buttonNum7";
        this.buttonNum7.Size = new Size(56, 28);
        this.buttonNum7.Text = "7 next";
        this.buttonNum7.UseVisualStyleBackColor = true;
        this.buttonNum7.Click += ButtonNum_Click;

        // First Set (한 번에 실행)
        this.buttonFirstSet = new Button();
        this.buttonFirstSet.Location = new Point(378, 100);
        this.buttonFirstSet.Name = "buttonFirstSet";
        this.buttonFirstSet.Size = new Size(82, 28);
        this.buttonFirstSet.Text = "First Set";
        this.buttonFirstSet.UseVisualStyleBackColor = true;
        this.buttonFirstSet.Click += ButtonFirstSet_Click;

        // 찾기만 (클릭/입력 안 함)
        this.checkBoxNoClick = new CheckBox();
        this.checkBoxNoClick.AutoSize = true;
        this.checkBoxNoClick.Location = new Point(464, 104);
        this.checkBoxNoClick.Name = "checkBoxNoClick";
        this.checkBoxNoClick.Size = new Size(110, 19);
        this.checkBoxNoClick.Text = "찾기만 (클릭X)";
        this.checkBoxNoClick.UseVisualStyleBackColor = true;

        // 포커스 안 함 = 지갑(팝업) 유지. 체크 시 현재 포커스 창만 사용 → 지갑 안 꺼짐
        this.checkBoxNoFocus = new CheckBox();
        this.checkBoxNoFocus.AutoSize = true;
        this.checkBoxNoFocus.Location = new Point(12, 133);
        this.checkBoxNoFocus.Name = "checkBoxNoFocus";
        this.checkBoxNoFocus.Size = new Size(150, 19);
        this.checkBoxNoFocus.Text = "포커스 안 함 (지갑 유지)";
        this.checkBoxNoFocus.UseVisualStyleBackColor = true;

        // Second Set 행: 실행 버튼 + 1~12
        this.labelSecondSet = new Label();
        this.labelSecondSet.AutoSize = true;
        this.labelSecondSet.Location = new Point(168, 137);
        this.labelSecondSet.Name = "labelSecondSet";
        this.labelSecondSet.Size = new Size(60, 15);
        this.labelSecondSet.Text = "Second Set:";

        this.buttonSecondSet = new Button();
        this.buttonSecondSet.Location = new Point(234, 132);
        this.buttonSecondSet.Name = "buttonSecondSet";
        this.buttonSecondSet.Size = new Size(88, 28);
        this.buttonSecondSet.Text = "Second Set";
        this.buttonSecondSet.UseVisualStyleBackColor = true;
        this.buttonSecondSet.Click += ButtonSecondSet_Click;

        this.flowSecondSet = new FlowLayoutPanel();
        this.flowSecondSet.FlowDirection = FlowDirection.LeftToRight;
        this.flowSecondSet.Location = new Point(328, 132);
        this.flowSecondSet.Size = new Size(288, 32);
        this.flowSecondSet.WrapContents = false;
        string[] secondSlots = { "1", "2", "3", "6", "7", "8", "9", "10", "11", "12" };
        foreach (string s in secondSlots)
        {
            var btn = new Button { Text = s, Size = new Size(28, 26), Tag = s };
            btn.Click += ButtonNum_Click;
            this.flowSecondSet.Controls.Add(btn);
        }

        // textBoxLog
        this.textBoxLog.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        this.textBoxLog.Font = new Font("Consolas", 9F);
        this.textBoxLog.Location = new Point(12, 168);
        this.textBoxLog.Multiline = true;
        this.textBoxLog.Name = "textBoxLog";
        this.textBoxLog.ReadOnly = true;
        this.textBoxLog.ScrollBars = ScrollBars.Vertical;
        this.textBoxLog.Size = new Size(560, 226);
        this.textBoxLog.TabIndex = 4;

        // StepTestForm
        this.AutoScaleDimensions = new SizeF(7F, 15F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.ClientSize = new Size(584, 445);
        this.Controls.Add(this.checkBoxNoFocus);
        this.Controls.Add(this.checkBoxNoClick);
        this.Controls.Add(this.flowSecondSet);
        this.Controls.Add(this.buttonSecondSet);
        this.Controls.Add(this.labelSecondSet);
        this.Controls.Add(this.buttonFirstSet);
        this.Controls.Add(this.buttonNum7);
        this.Controls.Add(this.buttonNum6);
        this.Controls.Add(this.buttonNum5);
        this.Controls.Add(this.buttonNum4);
        this.Controls.Add(this.buttonNum3);
        this.Controls.Add(this.labelKeyHint);
        this.Controls.Add(this.labelMouse);
        this.Controls.Add(this.buttonLogCoord);
        this.Controls.Add(this.labelTitle);
        this.Controls.Add(this.buttonStep1);
        this.Controls.Add(this.buttonStep2);
        this.Controls.Add(this.buttonOneClickTest);
        this.Controls.Add(this.buttonStep3);
        this.Controls.Add(this.textBoxLog);
        this.MinimumSize = new Size(500, 350);
        this.KeyPreview = true;
        this.Name = "StepTestForm";
        this.StartPosition = FormStartPosition.CenterScreen;
        this.Text = "단계별 테스트 (메인페이지 → 햄버거)";
        this.KeyDown += StepTestForm_KeyDown;
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    private Button buttonOneClickTest;
    private Button buttonStep1;
    private Button buttonStep2;
    private Button buttonStep3;
    private Label labelMouse;
    private Label labelKeyHint;
    private Button buttonLogCoord;
    private Button buttonNum3;
    private Button buttonNum4;
    private Button buttonNum5;
    private Button buttonNum6;
    private Button buttonNum7;
    private Button buttonFirstSet;
    private CheckBox checkBoxNoClick;
    private CheckBox checkBoxNoFocus;
    private Label labelSecondSet;
    private Button buttonSecondSet;
    private FlowLayoutPanel flowSecondSet;
    private TextBox textBoxLog;
    private Label labelTitle;
}
