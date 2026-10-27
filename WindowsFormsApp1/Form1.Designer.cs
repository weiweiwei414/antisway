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
            this.btnJog1Pos = new System.Windows.Forms.Button();
            this.btnJog1Neg = new System.Windows.Forms.Button();
            this.lblSpeed1 = new System.Windows.Forms.Label();
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
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.lblHome = new System.Windows.Forms.Label();
            this.lblRaw = new System.Windows.Forms.Label();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.lblPpm1 = new System.Windows.Forms.Label();
            this.txtPpm1 = new System.Windows.Forms.TextBox();
            this.lblPpm1Unit = new System.Windows.Forms.Label();
            this.lblPpm2 = new System.Windows.Forms.Label();
            this.txtPpm2 = new System.Windows.Forms.TextBox();
            this.lblPpm2Unit = new System.Windows.Forms.Label();
            this.btnApplyPpm = new System.Windows.Forms.Button();
            this.lblCalState = new System.Windows.Forms.Label();
            this.lblCmd = new System.Windows.Forms.Label();
            this.txtCmd = new System.Windows.Forms.TextBox();
            this.lblAct = new System.Windows.Forms.Label();
            this.txtAct = new System.Windows.Forms.TextBox();
            this.btnCalc1 = new System.Windows.Forms.Button();
            this.btnCalc2 = new System.Windows.Forms.Button();
            this.lblCalTip = new System.Windows.Forms.Label();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.groupBox4.SuspendLayout();
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
            this.groupBox1.Controls.Add(this.btnJog1Pos);
            this.groupBox1.Controls.Add(this.btnJog1Neg);
            this.groupBox1.Controls.Add(this.lblSpeed1);
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
            // lblSpeed1
            // 
            this.lblSpeed1.AutoSize = true;
            this.lblSpeed1.Location = new System.Drawing.Point(12, 112);
            this.lblSpeed1.Name = "lblSpeed1";
            this.lblSpeed1.Size = new System.Drawing.Size(41, 12);
            this.lblSpeed1.TabIndex = 2;
            this.lblSpeed1.Text = "速度:";
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
            this.lblSpeed2.Size = new System.Drawing.Size(41, 12);
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
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.lblHome);
            this.groupBox3.Controls.Add(this.lblRaw);
            this.groupBox3.Location = new System.Drawing.Point(20, 336);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(600, 100);
            this.groupBox3.TabIndex = 6;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "原点";
            // 
            // lblHome
            // 
            this.lblHome.BackColor = System.Drawing.Color.Gray;
            this.lblHome.ForeColor = System.Drawing.Color.White;
            this.lblHome.Location = new System.Drawing.Point(12, 26);
            this.lblHome.Name = "lblHome";
            this.lblHome.Size = new System.Drawing.Size(576, 26);
            this.lblHome.TabIndex = 0;
            this.lblHome.Text = "原点 Home1（常开）：未连接";
            this.lblHome.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblRaw
            // 
            this.lblRaw.ForeColor = System.Drawing.Color.DimGray;
            this.lblRaw.Location = new System.Drawing.Point(12, 62);
            this.lblRaw.Name = "lblRaw";
            this.lblRaw.Size = new System.Drawing.Size(576, 20);
            this.lblRaw.TabIndex = 1;
            this.lblRaw.Text = "Home 原始值 = -";
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.lblPpm1);
            this.groupBox4.Controls.Add(this.txtPpm1);
            this.groupBox4.Controls.Add(this.lblPpm1Unit);
            this.groupBox4.Controls.Add(this.lblPpm2);
            this.groupBox4.Controls.Add(this.txtPpm2);
            this.groupBox4.Controls.Add(this.lblPpm2Unit);
            this.groupBox4.Controls.Add(this.btnApplyPpm);
            this.groupBox4.Controls.Add(this.lblCalState);
            this.groupBox4.Controls.Add(this.lblCmd);
            this.groupBox4.Controls.Add(this.txtCmd);
            this.groupBox4.Controls.Add(this.lblAct);
            this.groupBox4.Controls.Add(this.txtAct);
            this.groupBox4.Controls.Add(this.btnCalc1);
            this.groupBox4.Controls.Add(this.btnCalc2);
            this.groupBox4.Controls.Add(this.lblCalTip);
            this.groupBox4.Location = new System.Drawing.Point(20, 446);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(600, 128);
            this.groupBox4.TabIndex = 7;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "标定 / 位置换算";
            // 
            // lblPpm1
            // 
            this.lblPpm1.AutoSize = true;
            this.lblPpm1.Location = new System.Drawing.Point(12, 28);
            this.lblPpm1.Name = "lblPpm1";
            this.lblPpm1.Size = new System.Drawing.Size(53, 12);
            this.lblPpm1.TabIndex = 0;
            this.lblPpm1.Text = "轴1 当量";
            // 
            // txtPpm1
            // 
            this.txtPpm1.Location = new System.Drawing.Point(70, 24);
            this.txtPpm1.Name = "txtPpm1";
            this.txtPpm1.Size = new System.Drawing.Size(64, 21);
            this.txtPpm1.TabIndex = 1;
            this.txtPpm1.Text = "80";
            this.txtPpm1.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // lblPpm1Unit
            // 
            this.lblPpm1Unit.AutoSize = true;
            this.lblPpm1Unit.Location = new System.Drawing.Point(138, 28);
            this.lblPpm1Unit.Name = "lblPpm1Unit";
            this.lblPpm1Unit.Size = new System.Drawing.Size(53, 12);
            this.lblPpm1Unit.TabIndex = 2;
            this.lblPpm1Unit.Text = "脉冲/mm";
            // 
            // lblPpm2
            // 
            this.lblPpm2.AutoSize = true;
            this.lblPpm2.Location = new System.Drawing.Point(202, 28);
            this.lblPpm2.Name = "lblPpm2";
            this.lblPpm2.Size = new System.Drawing.Size(53, 12);
            this.lblPpm2.TabIndex = 3;
            this.lblPpm2.Text = "轴2 当量";
            // 
            // txtPpm2
            // 
            this.txtPpm2.Location = new System.Drawing.Point(260, 24);
            this.txtPpm2.Name = "txtPpm2";
            this.txtPpm2.Size = new System.Drawing.Size(64, 21);
            this.txtPpm2.TabIndex = 4;
            this.txtPpm2.Text = "255";
            this.txtPpm2.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // lblPpm2Unit
            // 
            this.lblPpm2Unit.AutoSize = true;
            this.lblPpm2Unit.Location = new System.Drawing.Point(328, 28);
            this.lblPpm2Unit.Name = "lblPpm2Unit";
            this.lblPpm2Unit.Size = new System.Drawing.Size(53, 12);
            this.lblPpm2Unit.TabIndex = 5;
            this.lblPpm2Unit.Text = "脉冲/mm";
            // 
            // btnApplyPpm
            // 
            this.btnApplyPpm.Location = new System.Drawing.Point(396, 21);
            this.btnApplyPpm.Name = "btnApplyPpm";
            this.btnApplyPpm.Size = new System.Drawing.Size(72, 26);
            this.btnApplyPpm.TabIndex = 6;
            this.btnApplyPpm.Text = "应用";
            this.btnApplyPpm.UseVisualStyleBackColor = true;
            this.btnApplyPpm.Click += new System.EventHandler(this.btnApplyPpm_Click);
            // 
            // lblCalState
            // 
            this.lblCalState.ForeColor = System.Drawing.Color.DarkOrange;
            this.lblCalState.Location = new System.Drawing.Point(476, 28);
            this.lblCalState.Name = "lblCalState";
            this.lblCalState.Size = new System.Drawing.Size(110, 16);
            this.lblCalState.TabIndex = 7;
            this.lblCalState.Text = "当量未标定";
            // 
            // lblCmd
            // 
            this.lblCmd.AutoSize = true;
            this.lblCmd.Location = new System.Drawing.Point(12, 58);
            this.lblCmd.Name = "lblCmd";
            this.lblCmd.Size = new System.Drawing.Size(65, 12);
            this.lblCmd.TabIndex = 8;
            this.lblCmd.Text = "指令(mm)";
            // 
            // txtCmd
            // 
            this.txtCmd.Location = new System.Drawing.Point(86, 54);
            this.txtCmd.Name = "txtCmd";
            this.txtCmd.Size = new System.Drawing.Size(60, 21);
            this.txtCmd.TabIndex = 9;
            this.txtCmd.Text = "300";
            this.txtCmd.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // lblAct
            // 
            this.lblAct.AutoSize = true;
            this.lblAct.Location = new System.Drawing.Point(154, 58);
            this.lblAct.Name = "lblAct";
            this.lblAct.Size = new System.Drawing.Size(65, 12);
            this.lblAct.TabIndex = 10;
            this.lblAct.Text = "实测(mm)";
            // 
            // txtAct
            // 
            this.txtAct.Location = new System.Drawing.Point(228, 54);
            this.txtAct.Name = "txtAct";
            this.txtAct.Size = new System.Drawing.Size(60, 21);
            this.txtAct.TabIndex = 11;
            this.txtAct.Text = "300";
            this.txtAct.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // btnCalc1
            // 
            this.btnCalc1.Location = new System.Drawing.Point(296, 52);
            this.btnCalc1.Name = "btnCalc1";
            this.btnCalc1.Size = new System.Drawing.Size(100, 26);
            this.btnCalc1.TabIndex = 12;
            this.btnCalc1.Text = "算轴1当量";
            this.btnCalc1.UseVisualStyleBackColor = true;
            this.btnCalc1.Click += new System.EventHandler(this.btnCalc1_Click);
            // 
            // btnCalc2
            // 
            this.btnCalc2.Location = new System.Drawing.Point(404, 52);
            this.btnCalc2.Name = "btnCalc2";
            this.btnCalc2.Size = new System.Drawing.Size(100, 26);
            this.btnCalc2.TabIndex = 13;
            this.btnCalc2.Text = "算轴2当量";
            this.btnCalc2.UseVisualStyleBackColor = true;
            this.btnCalc2.Click += new System.EventHandler(this.btnCalc2_Click);
            // 
            // lblCalTip
            // 
            this.lblCalTip.ForeColor = System.Drawing.Color.DimGray;
            this.lblCalTip.Location = new System.Drawing.Point(12, 88);
            this.lblCalTip.Name = "lblCalTip";
            this.lblCalTip.Size = new System.Drawing.Size(576, 34);
            this.lblCalTip.TabIndex = 14;
            this.lblCalTip.Text = "标定法：让轴实际走一段（点动或连续），把程序认为走了多少、卷尺量到多少填进来，点对应按钮算当量。\r\n行程越长越准，建议 200~300mm；两端各留一点余量；单向走，避免反向间隙。";
            // 
            // timer1
            // 
            this.timer1.Interval = 50;
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(640, 598);
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
            this.Text = "桥式卸船机防摇实验台";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.groupBox4.ResumeLayout(false);
            this.groupBox4.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button BtnConnect;
        private System.Windows.Forms.Button BtnReset;
        private System.Windows.Forms.Button BtnDisConnect;
        private System.Windows.Forms.Label labStatus;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Button btnJog1Pos;
        private System.Windows.Forms.Button btnJog1Neg;
        private System.Windows.Forms.Label lblSpeed1;
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
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.Label lblHome;
        private System.Windows.Forms.Label lblRaw;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.Label lblPpm1;
        private System.Windows.Forms.TextBox txtPpm1;
        private System.Windows.Forms.Label lblPpm1Unit;
        private System.Windows.Forms.Label lblPpm2;
        private System.Windows.Forms.TextBox txtPpm2;
        private System.Windows.Forms.Label lblPpm2Unit;
        private System.Windows.Forms.Button btnApplyPpm;
        private System.Windows.Forms.Label lblCalState;
        private System.Windows.Forms.Label lblCmd;
        private System.Windows.Forms.TextBox txtCmd;
        private System.Windows.Forms.Label lblAct;
        private System.Windows.Forms.TextBox txtAct;
        private System.Windows.Forms.Button btnCalc1;
        private System.Windows.Forms.Button btnCalc2;
        private System.Windows.Forms.Label lblCalTip;
        private System.Windows.Forms.Timer timer1;
    }
}
