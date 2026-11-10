namespace WindowsFormsApp1
{
    partial class Form1
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.BtnConnect = new System.Windows.Forms.Button();
            this.BtnReset = new System.Windows.Forms.Button();
            this.BtnDisConnect = new System.Windows.Forms.Button();
            this.labStatus = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.label1 = new System.Windows.Forms.Label();
            this.btnJog1Pos = new System.Windows.Forms.Button();
            this.btnJog1Neg = new System.Windows.Forms.Button();
            this.txtSpeed1 = new System.Windows.Forms.TextBox();
            this.btnRun1Pos = new System.Windows.Forms.Button();
            this.btnRun1Neg = new System.Windows.Forms.Button();
            this.btnPause1 = new System.Windows.Forms.Button();
            this.lblPos1 = new System.Windows.Forms.Label();
            this.btnHome1 = new System.Windows.Forms.Button();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.btnJog2Pos = new System.Windows.Forms.Button();
            this.btnJog2Neg = new System.Windows.Forms.Button();
            this.lblSpeed2 = new System.Windows.Forms.Label();
            this.txtSpeed2 = new System.Windows.Forms.TextBox();
            this.btnRun2Pos = new System.Windows.Forms.Button();
            this.btnRun2Neg = new System.Windows.Forms.Button();
            this.btnPause2 = new System.Windows.Forms.Button();
            this.lblPos2 = new System.Windows.Forms.Label();
            this.btnZero2 = new System.Windows.Forms.Button();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.btnLinkFwd = new System.Windows.Forms.Button();
            this.btnLinkRev = new System.Windows.Forms.Button();
            this.btnLinkStop = new System.Windows.Forms.Button();
            this.lblLinkPos = new System.Windows.Forms.Label();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.lblHome = new System.Windows.Forms.Label();
            this.lblLimPos = new System.Windows.Forms.Label();
            this.lblLimNeg = new System.Windows.Forms.Label();
            this.chkLmtNC = new System.Windows.Forms.CheckBox();
            this.lblRaw = new System.Windows.Forms.Label();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.backgroundWorker1 = new System.ComponentModel.BackgroundWorker();
            this.grpVision = new System.Windows.Forms.GroupBox();
            this.picPreview = new System.Windows.Forms.PictureBox();
            this.lblAngleBig = new System.Windows.Forms.Label();
            this.lblVisionRate = new System.Windows.Forms.Label();
            this.lblVisionOffset = new System.Windows.Forms.Label();
            this.lblVisionDepth = new System.Windows.Forms.Label();
            this.lblVisionFps = new System.Windows.Forms.Label();
            this.lblVisionState = new System.Windows.Forms.Label();
            this.lblVisionHint = new System.Windows.Forms.Label();
            this.btnVisionStart = new System.Windows.Forms.Button();
            this.btnVisionStop = new System.Windows.Forms.Button();
            this.btnVisionZero = new System.Windows.Forms.Button();
            this.btnVisionSnap = new System.Windows.Forms.Button();
            this.lblVisionDll = new System.Windows.Forms.Label();
            this.txtVisionLog = new System.Windows.Forms.TextBox();
            this.timerVision = new System.Windows.Forms.Timer(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.picPreview)).BeginInit();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox4.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.grpVision.SuspendLayout();
            this.SuspendLayout();
            // 
            // BtnConnect
            // 
            this.BtnConnect.Location = new System.Drawing.Point(20, 14);
            this.BtnConnect.Name = "BtnConnect";
            this.BtnConnect.Size = new System.Drawing.Size(100, 30);
            this.BtnConnect.TabIndex = 0;
            this.BtnConnect.Text = "连接板卡";
            this.BtnConnect.UseVisualStyleBackColor = true;
            this.BtnConnect.Click += new System.EventHandler(this.BtnConnect_Click);
            // 
            // BtnReset
            // 
            this.BtnReset.Location = new System.Drawing.Point(128, 14);
            this.BtnReset.Name = "BtnReset";
            this.BtnReset.Size = new System.Drawing.Size(100, 30);
            this.BtnReset.TabIndex = 1;
            this.BtnReset.Text = "复位板卡";
            this.BtnReset.UseVisualStyleBackColor = true;
            this.BtnReset.Click += new System.EventHandler(this.BtnReset_Click);
            // 
            // BtnDisConnect
            // 
            this.BtnDisConnect.Location = new System.Drawing.Point(236, 14);
            this.BtnDisConnect.Name = "BtnDisConnect";
            this.BtnDisConnect.Size = new System.Drawing.Size(100, 30);
            this.BtnDisConnect.TabIndex = 2;
            this.BtnDisConnect.Text = "关闭板卡";
            this.BtnDisConnect.UseVisualStyleBackColor = true;
            this.BtnDisConnect.Click += new System.EventHandler(this.BtnDisConnect_Click);
            // 
            // labStatus
            // 
            this.labStatus.AutoEllipsis = true;
            this.labStatus.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.labStatus.Location = new System.Drawing.Point(20, 54);
            this.labStatus.Name = "labStatus";
            this.labStatus.Size = new System.Drawing.Size(600, 28);
            this.labStatus.TabIndex = 3;
            this.labStatus.Text = "状态栏";
            this.labStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Controls.Add(this.btnJog1Pos);
            this.groupBox1.Controls.Add(this.btnJog1Neg);
            this.groupBox1.Controls.Add(this.txtSpeed1);
            this.groupBox1.Controls.Add(this.btnRun1Pos);
            this.groupBox1.Controls.Add(this.btnRun1Neg);
            this.groupBox1.Controls.Add(this.btnPause1);
            this.groupBox1.Controls.Add(this.lblPos1);
            this.groupBox1.Controls.Add(this.btnHome1);
            this.groupBox1.Location = new System.Drawing.Point(20, 92);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(290, 230);
            this.groupBox1.TabIndex = 4;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "轴1";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(15, 111);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(35, 12);
            this.label1.TabIndex = 9;
            this.label1.Text = "速度:";
            // 
            // btnJog1Pos
            // 
            this.btnJog1Pos.Location = new System.Drawing.Point(12, 24);
            this.btnJog1Pos.Name = "btnJog1Pos";
            this.btnJog1Pos.Size = new System.Drawing.Size(120, 32);
            this.btnJog1Pos.TabIndex = 0;
            this.btnJog1Pos.Text = "正转";
            this.btnJog1Pos.UseVisualStyleBackColor = true;
            // 
            // btnJog1Neg
            // 
            this.btnJog1Neg.Location = new System.Drawing.Point(12, 64);
            this.btnJog1Neg.Name = "btnJog1Neg";
            this.btnJog1Neg.Size = new System.Drawing.Size(120, 32);
            this.btnJog1Neg.TabIndex = 1;
            this.btnJog1Neg.Text = "反转";
            this.btnJog1Neg.UseVisualStyleBackColor = true;
            // 
            // txtSpeed1
            // 
            this.txtSpeed1.Location = new System.Drawing.Point(56, 108);
            this.txtSpeed1.Name = "txtSpeed1";
            this.txtSpeed1.Size = new System.Drawing.Size(76, 21);
            this.txtSpeed1.TabIndex = 3;
            this.txtSpeed1.Text = "0.05";
            this.txtSpeed1.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // btnRun1Pos
            // 
            this.btnRun1Pos.Location = new System.Drawing.Point(12, 142);
            this.btnRun1Pos.Name = "btnRun1Pos";
            this.btnRun1Pos.Size = new System.Drawing.Size(120, 30);
            this.btnRun1Pos.TabIndex = 4;
            this.btnRun1Pos.Text = "连续正转";
            this.btnRun1Pos.UseVisualStyleBackColor = true;
            // 
            // btnRun1Neg
            // 
            this.btnRun1Neg.Location = new System.Drawing.Point(12, 176);
            this.btnRun1Neg.Name = "btnRun1Neg";
            this.btnRun1Neg.Size = new System.Drawing.Size(120, 30);
            this.btnRun1Neg.TabIndex = 5;
            this.btnRun1Neg.Text = "连续反转";
            this.btnRun1Neg.UseVisualStyleBackColor = true;
            // 
            // btnPause1
            // 
            this.btnPause1.Location = new System.Drawing.Point(144, 24);
            this.btnPause1.Name = "btnPause1";
            this.btnPause1.Size = new System.Drawing.Size(130, 72);
            this.btnPause1.TabIndex = 6;
            this.btnPause1.Text = "暂停";
            this.btnPause1.UseVisualStyleBackColor = true;
            // 
            // lblPos1
            // 
            this.lblPos1.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.lblPos1.Location = new System.Drawing.Point(144, 142);
            this.lblPos1.Name = "lblPos1";
            this.lblPos1.Size = new System.Drawing.Size(130, 64);
            this.lblPos1.TabIndex = 7;
            this.lblPos1.Text = "位置: -";
            this.lblPos1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // btnHome1
            // 
            this.btnHome1.Location = new System.Drawing.Point(144, 100);
            this.btnHome1.Name = "btnHome1";
            this.btnHome1.Size = new System.Drawing.Size(130, 30);
            this.btnHome1.TabIndex = 8;
            this.btnHome1.Text = "回零";
            this.btnHome1.UseVisualStyleBackColor = true;
            this.btnHome1.Click += new System.EventHandler(this.btnHome1_Click);
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.btnJog2Pos);
            this.groupBox2.Controls.Add(this.btnJog2Neg);
            this.groupBox2.Controls.Add(this.lblSpeed2);
            this.groupBox2.Controls.Add(this.txtSpeed2);
            this.groupBox2.Controls.Add(this.btnRun2Pos);
            this.groupBox2.Controls.Add(this.btnRun2Neg);
            this.groupBox2.Controls.Add(this.btnPause2);
            this.groupBox2.Controls.Add(this.lblPos2);
            this.groupBox2.Controls.Add(this.btnZero2);
            this.groupBox2.Location = new System.Drawing.Point(330, 92);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(290, 230);
            this.groupBox2.TabIndex = 5;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "轴2";
            // 
            // btnJog2Pos
            // 
            this.btnJog2Pos.Location = new System.Drawing.Point(12, 24);
            this.btnJog2Pos.Name = "btnJog2Pos";
            this.btnJog2Pos.Size = new System.Drawing.Size(120, 32);
            this.btnJog2Pos.TabIndex = 0;
            this.btnJog2Pos.Text = "正转";
            this.btnJog2Pos.UseVisualStyleBackColor = true;
            // 
            // btnJog2Neg
            // 
            this.btnJog2Neg.Location = new System.Drawing.Point(12, 64);
            this.btnJog2Neg.Name = "btnJog2Neg";
            this.btnJog2Neg.Size = new System.Drawing.Size(120, 32);
            this.btnJog2Neg.TabIndex = 1;
            this.btnJog2Neg.Text = "反转";
            this.btnJog2Neg.UseVisualStyleBackColor = true;
            // 
            // lblSpeed2
            // 
            this.lblSpeed2.AutoSize = true;
            this.lblSpeed2.Location = new System.Drawing.Point(12, 112);
            this.lblSpeed2.Name = "lblSpeed2";
            this.lblSpeed2.Size = new System.Drawing.Size(35, 12);
            this.lblSpeed2.TabIndex = 2;
            this.lblSpeed2.Text = "速度:";
            // 
            // txtSpeed2
            // 
            this.txtSpeed2.Location = new System.Drawing.Point(56, 108);
            this.txtSpeed2.Name = "txtSpeed2";
            this.txtSpeed2.Size = new System.Drawing.Size(76, 21);
            this.txtSpeed2.TabIndex = 3;
            this.txtSpeed2.Text = "0.05";
            this.txtSpeed2.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // btnRun2Pos
            // 
            this.btnRun2Pos.Location = new System.Drawing.Point(12, 142);
            this.btnRun2Pos.Name = "btnRun2Pos";
            this.btnRun2Pos.Size = new System.Drawing.Size(120, 30);
            this.btnRun2Pos.TabIndex = 4;
            this.btnRun2Pos.Text = "连续正转";
            this.btnRun2Pos.UseVisualStyleBackColor = true;
            // 
            // btnRun2Neg
            // 
            this.btnRun2Neg.Location = new System.Drawing.Point(12, 176);
            this.btnRun2Neg.Name = "btnRun2Neg";
            this.btnRun2Neg.Size = new System.Drawing.Size(120, 30);
            this.btnRun2Neg.TabIndex = 5;
            this.btnRun2Neg.Text = "连续反转";
            this.btnRun2Neg.UseVisualStyleBackColor = true;
            // 
            // btnPause2
            // 
            this.btnPause2.Location = new System.Drawing.Point(144, 24);
            this.btnPause2.Name = "btnPause2";
            this.btnPause2.Size = new System.Drawing.Size(130, 72);
            this.btnPause2.TabIndex = 6;
            this.btnPause2.Text = "暂停";
            this.btnPause2.UseVisualStyleBackColor = true;
            // 
            // lblPos2
            // 
            this.lblPos2.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.lblPos2.Location = new System.Drawing.Point(144, 142);
            this.lblPos2.Name = "lblPos2";
            this.lblPos2.Size = new System.Drawing.Size(130, 64);
            this.lblPos2.TabIndex = 7;
            this.lblPos2.Text = "位置: -";
            this.lblPos2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // btnZero2
            // 
            this.btnZero2.Location = new System.Drawing.Point(144, 100);
            this.btnZero2.Name = "btnZero2";
            this.btnZero2.Size = new System.Drawing.Size(130, 30);
            this.btnZero2.TabIndex = 8;
            this.btnZero2.Text = "设为零点";
            this.btnZero2.UseVisualStyleBackColor = true;
            this.btnZero2.Click += new System.EventHandler(this.btnZero2_Click);
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.btnLinkFwd);
            this.groupBox4.Controls.Add(this.btnLinkRev);
            this.groupBox4.Controls.Add(this.btnLinkStop);
            this.groupBox4.Controls.Add(this.lblLinkPos);
            this.groupBox4.Location = new System.Drawing.Point(20, 330);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(600, 72);
            this.groupBox4.TabIndex = 7;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "两轴联动";
            // 
            // btnLinkFwd
            // 
            this.btnLinkFwd.Location = new System.Drawing.Point(12, 22);
            this.btnLinkFwd.Name = "btnLinkFwd";
            this.btnLinkFwd.Size = new System.Drawing.Size(100, 30);
            this.btnLinkFwd.TabIndex = 0;
            this.btnLinkFwd.Text = "正转";
            this.btnLinkFwd.UseVisualStyleBackColor = true;
            this.btnLinkFwd.Click += new System.EventHandler(this.btnLinkFwd_Click);
            // 
            // btnLinkRev
            // 
            this.btnLinkRev.Location = new System.Drawing.Point(120, 22);
            this.btnLinkRev.Name = "btnLinkRev";
            this.btnLinkRev.Size = new System.Drawing.Size(100, 30);
            this.btnLinkRev.TabIndex = 1;
            this.btnLinkRev.Text = "反转";
            this.btnLinkRev.UseVisualStyleBackColor = true;
            this.btnLinkRev.Click += new System.EventHandler(this.btnLinkRev_Click);
            // 
            // btnLinkStop
            // 
            this.btnLinkStop.Location = new System.Drawing.Point(228, 22);
            this.btnLinkStop.Name = "btnLinkStop";
            this.btnLinkStop.Size = new System.Drawing.Size(100, 30);
            this.btnLinkStop.TabIndex = 2;
            this.btnLinkStop.Text = "暂停";
            this.btnLinkStop.UseVisualStyleBackColor = true;
            this.btnLinkStop.Click += new System.EventHandler(this.btnLinkStop_Click);
            // 
            // lblLinkPos
            // 
            this.lblLinkPos.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.lblLinkPos.Location = new System.Drawing.Point(348, 12);
            this.lblLinkPos.Name = "lblLinkPos";
            this.lblLinkPos.Size = new System.Drawing.Size(240, 50);
            this.lblLinkPos.TabIndex = 3;
            this.lblLinkPos.Text = "联动位移";
            this.lblLinkPos.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.lblHome);
            this.groupBox3.Controls.Add(this.lblLimPos);
            this.groupBox3.Controls.Add(this.lblLimNeg);
            this.groupBox3.Controls.Add(this.chkLmtNC);
            this.groupBox3.Controls.Add(this.lblRaw);
            this.groupBox3.Location = new System.Drawing.Point(20, 410);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(600, 126);
            this.groupBox3.TabIndex = 6;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "原点 / 限位";
            // 
            // lblHome
            // 
            this.lblHome.BackColor = System.Drawing.Color.Gray;
            this.lblHome.ForeColor = System.Drawing.Color.White;
            this.lblHome.Location = new System.Drawing.Point(12, 22);
            this.lblHome.Name = "lblHome";
            this.lblHome.Size = new System.Drawing.Size(576, 24);
            this.lblHome.TabIndex = 0;
            this.lblHome.Text = "原点 Home1（常开）：未连接";
            this.lblHome.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblLimPos
            // 
            this.lblLimPos.BackColor = System.Drawing.Color.Gray;
            this.lblLimPos.ForeColor = System.Drawing.Color.White;
            this.lblLimPos.Location = new System.Drawing.Point(12, 54);
            this.lblLimPos.Name = "lblLimPos";
            this.lblLimPos.Size = new System.Drawing.Size(236, 26);
            this.lblLimPos.TabIndex = 2;
            this.lblLimPos.Text = "正转方向（X1）：-";
            this.lblLimPos.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lblLimNeg
            // 
            this.lblLimNeg.BackColor = System.Drawing.Color.Gray;
            this.lblLimNeg.ForeColor = System.Drawing.Color.White;
            this.lblLimNeg.Location = new System.Drawing.Point(254, 54);
            this.lblLimNeg.Name = "lblLimNeg";
            this.lblLimNeg.Size = new System.Drawing.Size(236, 26);
            this.lblLimNeg.TabIndex = 3;
            this.lblLimNeg.Text = "反转方向（X0）：-";
            this.lblLimNeg.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // chkLmtNC
            // 
            this.chkLmtNC.AutoSize = true;
            this.chkLmtNC.Checked = true;
            this.chkLmtNC.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkLmtNC.Location = new System.Drawing.Point(506, 58);
            this.chkLmtNC.Name = "chkLmtNC";
            this.chkLmtNC.Size = new System.Drawing.Size(72, 16);
            this.chkLmtNC.TabIndex = 4;
            this.chkLmtNC.Text = "常闭开关";
            this.chkLmtNC.UseVisualStyleBackColor = true;
            this.chkLmtNC.CheckedChanged += new System.EventHandler(this.chkLmtNC_CheckedChanged);
            // 
            // lblRaw
            // 
            this.lblRaw.ForeColor = System.Drawing.Color.DimGray;
            this.lblRaw.Location = new System.Drawing.Point(12, 88);
            this.lblRaw.Name = "lblRaw";
            this.lblRaw.Size = new System.Drawing.Size(576, 18);
            this.lblRaw.TabIndex = 1;
            this.lblRaw.Text = "Home 原始值 = -";
            // 
            // timer1
            // 
            this.timer1.Interval = 50;
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // timerVision —— 视觉读数独立节拍，和板卡状态刷新互不阻塞
            // 
            this.timerVision.Interval = 50;
            this.timerVision.Tick += new System.EventHandler(this.timerVision_Tick);
            // 
            // grpVision —— 右侧视觉面板（D415 实时画面 + 摆角读数）
            // 
            this.grpVision.Controls.Add(this.picPreview);
            this.grpVision.Controls.Add(this.lblAngleBig);
            this.grpVision.Controls.Add(this.lblVisionRate);
            this.grpVision.Controls.Add(this.lblVisionOffset);
            this.grpVision.Controls.Add(this.lblVisionDepth);
            this.grpVision.Controls.Add(this.lblVisionFps);
            this.grpVision.Controls.Add(this.lblVisionState);
            this.grpVision.Controls.Add(this.lblVisionHint);
            this.grpVision.Controls.Add(this.btnVisionStart);
            this.grpVision.Controls.Add(this.btnVisionStop);
            this.grpVision.Controls.Add(this.btnVisionZero);
            this.grpVision.Controls.Add(this.btnVisionSnap);
            this.grpVision.Controls.Add(this.lblVisionDll);
            this.grpVision.Location = new System.Drawing.Point(650, 14);
            this.grpVision.Name = "grpVision";
            this.grpVision.Size = new System.Drawing.Size(630, 600);
            this.grpVision.TabIndex = 8;
            this.grpVision.TabStop = false;
            this.grpVision.Text = "视觉引导防摇 · RealSense D415";
            // 
            // picPreview
            // 
            this.picPreview.BackColor = System.Drawing.Color.Black;
            this.picPreview.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.picPreview.Location = new System.Drawing.Point(12, 18);
            this.picPreview.Name = "picPreview";
            this.picPreview.Size = new System.Drawing.Size(600, 450);
            this.picPreview.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.picPreview.TabIndex = 0;
            this.picPreview.TabStop = false;
            // 
            // lblAngleBig
            // 
            this.lblAngleBig.Font = new System.Drawing.Font("微软雅黑", 24F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblAngleBig.Location = new System.Drawing.Point(12, 472);
            this.lblAngleBig.Name = "lblAngleBig";
            this.lblAngleBig.Size = new System.Drawing.Size(200, 56);
            this.lblAngleBig.TabIndex = 1;
            this.lblAngleBig.Text = "— °";
            this.lblAngleBig.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblVisionRate
            // 
            this.lblVisionRate.Location = new System.Drawing.Point(220, 472);
            this.lblVisionRate.Name = "lblVisionRate";
            this.lblVisionRate.Size = new System.Drawing.Size(190, 24);
            this.lblVisionRate.TabIndex = 2;
            this.lblVisionRate.Text = "角速度   — °/s";
            this.lblVisionRate.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblVisionOffset
            // 
            this.lblVisionOffset.Location = new System.Drawing.Point(220, 496);
            this.lblVisionOffset.Name = "lblVisionOffset";
            this.lblVisionOffset.Size = new System.Drawing.Size(190, 24);
            this.lblVisionOffset.TabIndex = 3;
            this.lblVisionOffset.Text = "水平偏移 — mm";
            this.lblVisionOffset.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblVisionDepth
            // 
            this.lblVisionDepth.Location = new System.Drawing.Point(220, 520);
            this.lblVisionDepth.Name = "lblVisionDepth";
            this.lblVisionDepth.Size = new System.Drawing.Size(190, 24);
            this.lblVisionDepth.TabIndex = 4;
            this.lblVisionDepth.Text = "深度     — mm";
            this.lblVisionDepth.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblVisionFps
            // 
            this.lblVisionFps.Location = new System.Drawing.Point(418, 472);
            this.lblVisionFps.Name = "lblVisionFps";
            this.lblVisionFps.Size = new System.Drawing.Size(200, 24);
            this.lblVisionFps.TabIndex = 5;
            this.lblVisionFps.Text = "FPS —   帧 —   置信 —";
            this.lblVisionFps.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblVisionState
            // 
            this.lblVisionState.BackColor = System.Drawing.Color.Gainsboro;
            this.lblVisionState.Location = new System.Drawing.Point(418, 496);
            this.lblVisionState.Name = "lblVisionState";
            this.lblVisionState.Size = new System.Drawing.Size(200, 24);
            this.lblVisionState.TabIndex = 6;
            this.lblVisionState.Text = "视觉：未启动";
            this.lblVisionState.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lblVisionHint
            // 
            this.lblVisionHint.ForeColor = System.Drawing.Color.DimGray;
            this.lblVisionHint.Location = new System.Drawing.Point(418, 520);
            this.lblVisionHint.Name = "lblVisionHint";
            this.lblVisionHint.Size = new System.Drawing.Size(200, 24);
            this.lblVisionHint.TabIndex = 7;
            this.lblVisionHint.Text = "摆角仅供显示/记录，不参与控制";
            this.lblVisionHint.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // btnVisionStart
            // 
            this.btnVisionStart.Location = new System.Drawing.Point(12, 546);
            this.btnVisionStart.Name = "btnVisionStart";
            this.btnVisionStart.Size = new System.Drawing.Size(100, 30);
            this.btnVisionStart.TabIndex = 8;
            this.btnVisionStart.Text = "开流";
            this.btnVisionStart.UseVisualStyleBackColor = true;
            this.btnVisionStart.Click += new System.EventHandler(this.btnVisionStart_Click);
            // 
            // btnVisionStop
            // 
            this.btnVisionStop.Location = new System.Drawing.Point(120, 546);
            this.btnVisionStop.Name = "btnVisionStop";
            this.btnVisionStop.Size = new System.Drawing.Size(100, 30);
            this.btnVisionStop.TabIndex = 9;
            this.btnVisionStop.Text = "停止";
            this.btnVisionStop.UseVisualStyleBackColor = true;
            this.btnVisionStop.Click += new System.EventHandler(this.btnVisionStop_Click);
            // 
            // btnVisionZero
            // 
            this.btnVisionZero.Location = new System.Drawing.Point(228, 546);
            this.btnVisionZero.Name = "btnVisionZero";
            this.btnVisionZero.Size = new System.Drawing.Size(100, 30);
            this.btnVisionZero.TabIndex = 10;
            this.btnVisionZero.Text = "定零位";
            this.btnVisionZero.UseVisualStyleBackColor = true;
            this.btnVisionZero.Click += new System.EventHandler(this.btnVisionZero_Click);
            // 
            // btnVisionSnap
            // 
            this.btnVisionSnap.Location = new System.Drawing.Point(336, 546);
            this.btnVisionSnap.Name = "btnVisionSnap";
            this.btnVisionSnap.Size = new System.Drawing.Size(100, 30);
            this.btnVisionSnap.TabIndex = 11;
            this.btnVisionSnap.Text = "存图";
            this.btnVisionSnap.UseVisualStyleBackColor = true;
            this.btnVisionSnap.Click += new System.EventHandler(this.btnVisionSnap_Click);
            // 
            // lblVisionDll
            // 
            this.lblVisionDll.ForeColor = System.Drawing.Color.DimGray;
            this.lblVisionDll.Location = new System.Drawing.Point(444, 546);
            this.lblVisionDll.Name = "lblVisionDll";
            this.lblVisionDll.Size = new System.Drawing.Size(174, 30);
            this.lblVisionDll.TabIndex = 12;
            this.lblVisionDll.Text = "vs_vision.dll —";
            this.lblVisionDll.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // txtVisionLog —— 左下角视觉事件日志（文本日志同时落盘到「日志」目录）
            // 
            this.txtVisionLog.BackColor = System.Drawing.Color.White;
            this.txtVisionLog.Location = new System.Drawing.Point(20, 546);
            this.txtVisionLog.Multiline = true;
            this.txtVisionLog.Name = "txtVisionLog";
            this.txtVisionLog.ReadOnly = true;
            this.txtVisionLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtVisionLog.Size = new System.Drawing.Size(600, 70);
            this.txtVisionLog.TabIndex = 9;
            this.txtVisionLog.Text = "";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1300, 630);
            this.Controls.Add(this.grpVision);
            this.Controls.Add(this.txtVisionLog);
            this.Controls.Add(this.groupBox4);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.labStatus);
            this.Controls.Add(this.BtnDisConnect);
            this.Controls.Add(this.BtnReset);
            this.Controls.Add(this.BtnConnect);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "Form1";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "桥式卸船机防摇实验台 · 视觉 + 运动控制";
            // 视觉模块在窗体显示之后再初始化：加载 ONNX 要几百毫秒，
            // 放在构造函数里会让窗口迟迟不出现，也拖慢板卡操作界面可用时间。
            this.Shown += new System.EventHandler(this.Form1_Shown);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.groupBox4.ResumeLayout(false);
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.grpVision.ResumeLayout(false);
            this.grpVision.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picPreview)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button BtnConnect;
        private System.Windows.Forms.Button BtnReset;
        private System.Windows.Forms.Button BtnDisConnect;
        private System.Windows.Forms.Label labStatus;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Button btnJog1Pos;
        private System.Windows.Forms.Button btnJog1Neg;
        private System.Windows.Forms.TextBox txtSpeed1;
        private System.Windows.Forms.Button btnRun1Pos;
        private System.Windows.Forms.Button btnRun1Neg;
        private System.Windows.Forms.Button btnPause1;
        private System.Windows.Forms.Label lblPos1;
        private System.Windows.Forms.Button btnHome1;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Button btnJog2Pos;
        private System.Windows.Forms.Button btnJog2Neg;
        private System.Windows.Forms.Label lblSpeed2;
        private System.Windows.Forms.TextBox txtSpeed2;
        private System.Windows.Forms.Button btnRun2Pos;
        private System.Windows.Forms.Button btnRun2Neg;
        private System.Windows.Forms.Button btnPause2;
        private System.Windows.Forms.Label lblPos2;
        private System.Windows.Forms.Button btnZero2;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.Button btnLinkFwd;
        private System.Windows.Forms.Button btnLinkRev;
        private System.Windows.Forms.Button btnLinkStop;
        private System.Windows.Forms.Label lblLinkPos;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.Label lblHome;
        private System.Windows.Forms.Label lblRaw;
        private System.Windows.Forms.Label lblLimPos;
        private System.Windows.Forms.Label lblLimNeg;
        private System.Windows.Forms.CheckBox chkLmtNC;
        private System.Windows.Forms.Timer timer1;
        private System.ComponentModel.BackgroundWorker backgroundWorker1;
        private System.Windows.Forms.Label label1;

        // ---- 视觉面板（D415 / vs_vision.dll）----
        private System.Windows.Forms.GroupBox grpVision;
        private System.Windows.Forms.PictureBox picPreview;
        private System.Windows.Forms.Label lblAngleBig;
        private System.Windows.Forms.Label lblVisionRate;
        private System.Windows.Forms.Label lblVisionOffset;
        private System.Windows.Forms.Label lblVisionDepth;
        private System.Windows.Forms.Label lblVisionFps;
        private System.Windows.Forms.Label lblVisionState;
        private System.Windows.Forms.Label lblVisionHint;
        private System.Windows.Forms.Button btnVisionStart;
        private System.Windows.Forms.Button btnVisionStop;
        private System.Windows.Forms.Button btnVisionZero;
        private System.Windows.Forms.Button btnVisionSnap;
        private System.Windows.Forms.Label lblVisionDll;
        private System.Windows.Forms.TextBox txtVisionLog;
        private System.Windows.Forms.Timer timerVision;
    }
}
