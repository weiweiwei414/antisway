using System;
using System.Drawing;
using System.Windows.Forms;
using MultiCardCS;

namespace WindowsFormsApp1
{
    /// <summary>
    /// 桥式卸船机防摇实验台 —— 两轴控制台。
    /// 界面在 Form1.Designer.cs，本文件只放逻辑。
    ///
    ///   轴1：直线模组 + 57 闭环步进 + HB808C，原点接 Home1（常开）
    ///   轴2：42 步进 + TB6600，开环，无外部信号
    /// </summary>
    public partial class Form1 : Form
    {
        // ==================================================================
        //  接线配置
        // ==================================================================
        // 原点接在端子 Home1。手册里零位输入编号是 Home1~Home8，
        // 对应的 IO 索引是 0~7，所以 Home1 对应索引 0。
        private const short HOME_INDEX = 0;

        // 回零参数的「方向」取值。
        // 手册写的是 1=正向、0=负向，但实测正好相反：填 0 时小车朝【正转方向】跑。
        // 所以这里按实测结果命名。万一哪天发现又反了，把下面两个值对调即可。
        private const short HomeTowardsFwd = 0;   // 朝正转方向找原点
        private const short HomeTowardsRev = 1;   // 朝反转方向找原点

        private const short AXIS_COUNT = 2;

        // ==================================================================
        //  运行日志
        //
        //  写在 exe 同目录 \日志\yyyy-MM-dd.log，纯文本，记事本直接能看。
        //  记录：连接/断开、每次运动指令、回零全过程、标定变更、异常，
        //        以及连接状态下每秒一条状态快照。
        //  日志自己出任何问题都吞掉，绝不影响主流程。
        // ==================================================================
        private static string LogDir()
        {
            return System.IO.Path.Combine(Application.StartupPath, "日志");
        }

        private static void Log(string msg)
        {
            try
            {
                string dir = LogDir();
                if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
                string f = System.IO.Path.Combine(dir, DateTime.Now.ToString("yyyy-MM-dd") + ".log");
                System.IO.File.AppendAllText(f,
                    DateTime.Now.ToString("HH:mm:ss.fff") + "  " + msg + "\r\n",
                    System.Text.Encoding.UTF8);
            }
            catch { }
        }

        // ==================================================================
        //  字段
        // ==================================================================
        private MultiCardCS.MultiCardCS card;
        private bool isOpen = false;
        private bool[] jogActive = new bool[3];

        private int diHome = 0;         // 零位输入

        private int sts1, sts2;
        private double vel1, vel2;
        private double pos1, pos2;
        private double enc1 = 0;        // 轴1 编码器实际位置（编码器接在驱动器上，这里读不到）
        private bool homing = false;    // 正在回零
        private bool homed = false;     // 轴1 本次连接后是否成功回过零（位置才有绝对含义）
        private bool homed2 = false;    // 轴2 是否设过零点
        private int homeTicks = 0;      // 回零已运行的节拍数（超时保护）
        private int clock1, clock2;
        private int lastLogTickMs = 0;  // 上一次写状态快照的时刻

        public Form1()
        {
            InitializeComponent();

            // 两轴统一绑定（设计器里这些按钮不要再挂事件，否则会触发两次）
            BindAxis(1, btnJog1Pos, btnJog1Neg, btnRun1Pos, btnRun1Neg, btnPause1, txtSpeed1);
            BindAxis(2, btnJog2Pos, btnJog2Neg, btnRun2Pos, btnRun2Neg, btnPause2, txtSpeed2);

            // 读回上次标定的当量
            LoadCalibration();
            txtPpm1.Text = ppm1.ToString("0.###");
            txtPpm2.Text = ppm2.ToString("0.###");
            ShowCalState();

            // 还没连板卡，先把实时读数清空、轴按钮禁掉
            ShowDisconnected();

            Log("—— 程序启动 —— " + Application.StartupPath);
        }

        /// <summary>未连接时：读数全部清空、轴按钮禁用，避免界面显示“假正常”。</summary>
        private void ShowDisconnected()
        {
            homing = false;
            lblHome.Text = "原点 Home1（常开）：未连接"; lblHome.BackColor = Color.Gray;
            lblRaw.Text = "Home 原始值 = -";
            lblPos1.Text = "位置\r\n-";
            lblPos2.Text = "位置\r\n-";
            SetAxisEnabled(false);
        }

        /// <summary>轴操作控件总开关</summary>
        private void SetAxisEnabled(bool on)
        {
            btnJog1Pos.Enabled = on; btnJog1Neg.Enabled = on;
            btnRun1Pos.Enabled = on; btnRun1Neg.Enabled = on;
            btnPause1.Enabled = on; txtSpeed1.Enabled = on; btnHome1.Enabled = on;

            btnJog2Pos.Enabled = on; btnJog2Neg.Enabled = on;
            btnRun2Pos.Enabled = on; btnRun2Neg.Enabled = on;
            btnPause2.Enabled = on; txtSpeed2.Enabled = on; btnZero2.Enabled = on;
        }

        // ==================================================================
        //  脉冲当量（脉冲/mm）
        //
        //  这个数决定“一个脉冲等于多少毫米”，是脉冲位置和实际距离之间
        //  唯一的换算系数。80 / 255 是占位值，只为让程序能跑；
        //  未标定时界面上位置前面会带一个 ≈，提醒这个数不可信。
        //  标定结果存在 exe 同目录的 axis-cal.ini，下次启动自动读回来。
        // ==================================================================
        private double ppm1 = 80.0;
        private double ppm2 = 255.0;
        private bool cal1 = false, cal2 = false;   // 是否已标定

        private double Ppm(short ax) { return ax == 1 ? ppm1 : ppm2; }
        private double Acc(short ax) { return ax == 1 ? 500.0 : 200.0; }  // mm/s²

        /// <summary>
        /// 脉冲 → 实际距离。要两个前提同时成立这个数才有意义：
        ///   1) 建过坐标（轴1 回过零 / 轴2 设过零点）——否则脉冲的起点是随机的
        ///   2) 标过脉冲当量——否则 mm 的换算系数是错的
        /// 缺哪个就在前面标出来，不显示看起来正常的假数。
        /// </summary>
        private string FmtPos(double pulse, short ax)
        {
            bool zeroOk = (ax == 1) ? homed : homed2;
            if (!zeroOk)
                return (ax == 1 ? "未回零" : "未设零点") + "\r\n" + pulse.ToString("0") + " 脉冲";

            bool calOk = (ax == 1) ? cal1 : cal2;
            double mm = pulse / Ppm(ax);
            return (calOk ? "" : "≈") + mm.ToString("0.00") + " mm\r\n"
                 + pulse.ToString("0") + " 脉冲";
        }

        private static string CalFilePath()
        {
            return System.IO.Path.Combine(Application.StartupPath, "axis-cal.ini");
        }

        private void LoadCalibration()
        {
            try
            {
                string p = CalFilePath();
                if (!System.IO.File.Exists(p)) return;
                foreach (string line in System.IO.File.ReadAllLines(p))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim().ToLower();
                    string v = line.Substring(eq + 1).Trim();
                    double d;
                    if (!double.TryParse(v, out d) || d <= 0) continue;
                    if (k == "ppm1") { ppm1 = d; cal1 = true; }
                    else if (k == "ppm2") { ppm2 = d; cal2 = true; }
                }
            }
            catch { }
        }

        private void SaveCalibration()
        {
            try
            {
                System.IO.File.WriteAllText(CalFilePath(),
                    "ppm1=" + ppm1.ToString("0.####") + "\r\n" +
                    "ppm2=" + ppm2.ToString("0.####") + "\r\n");
            }
            catch (Exception ex) { labStatus.Text = "保存标定失败：" + ex.Message; }
        }

        private void ShowCalState()
        {
            if (cal1 && cal2) { lblCalState.Text = "当量已标定"; lblCalState.ForeColor = Color.SeaGreen; }
            else if (cal1 || cal2) { lblCalState.Text = "只有一轴已标定"; lblCalState.ForeColor = Color.DarkOrange; }
            else { lblCalState.Text = "当量未标定"; lblCalState.ForeColor = Color.DarkOrange; }
        }

        /// <summary>手工填当量：已知拨码和导程、算出来直接用的情况。</summary>
        private void btnApplyPpm_Click(object sender, EventArgs e)
        {
            double a, b;
            if (!double.TryParse(txtPpm1.Text.Trim(), out a) || a <= 0 ||
                !double.TryParse(txtPpm2.Text.Trim(), out b) || b <= 0)
            {
                labStatus.Text = "当量必须是大于 0 的数字";
                return;
            }
            ppm1 = a; ppm2 = b; cal1 = true; cal2 = true;
            SaveCalibration();
            ShowCalState();
            labStatus.Text = "当量已应用：轴1 " + a.ToString("0.###")
                           + "、轴2 " + b.ToString("0.###") + " 脉冲/mm";
        }

        private void btnCalc1_Click(object sender, EventArgs e) { CalcPpm(1); }
        private void btnCalc2_Click(object sender, EventArgs e) { CalcPpm(2); }

        /// <summary>
        /// 用“走一段、卷尺量实际距离”反算当量。
        ///   发出去的脉冲 = 指令距离 × 旧当量
        ///   新当量 = 脉冲 ÷ 实测距离 = 旧当量 × 指令 ÷ 实测
        /// </summary>
        private void CalcPpm(short ax)
        {
            double cmd, act;
            if (!double.TryParse(txtCmd.Text.Trim(), out cmd) || cmd <= 0 ||
                !double.TryParse(txtAct.Text.Trim(), out act) || act <= 0)
            {
                labStatus.Text = "指令行程和实测距离都要填大于 0 的数字（单位 mm）";
                return;
            }

            double oldPpm = Ppm(ax);
            double newPpm = oldPpm * cmd / act;
            if (newPpm < 0.001 || newPpm > 100000)
            {
                labStatus.Text = "算出来的当量不合理（" + newPpm.ToString("0.###") + "），检查输入";
                return;
            }

            if (ax == 1) { ppm1 = newPpm; cal1 = true; } else { ppm2 = newPpm; cal2 = true; }
            SaveCalibration();
            txtPpm1.Text = ppm1.ToString("0.###");
            txtPpm2.Text = ppm2.ToString("0.###");
            ShowCalState();

            labStatus.Text = "轴" + ax + " 当量 = " + newPpm.ToString("0.###") + " 脉冲/mm"
                           + "（旧 " + oldPpm.ToString("0.###") + " × 指令 " + cmd.ToString("0.#")
                           + " ÷ 实测 " + act.ToString("0.#") + "）";
        }

        // ==================================================================
        //  连接 / 复位 / 断开
        // ==================================================================
        private void BtnConnect_Click(object sender, EventArgs e)
        {
            if (isOpen) { labStatus.Text = "已经连接，别重复点"; return; }
            try
            {
                card = new MultiCardCS.MultiCardCS();
                int rc = card.GA_Open((short)1, "192.168.0.200", (ushort)60000,
                                      "192.168.0.1", (ushort)60000);
                if (rc != 0) { labStatus.Text = "连接失败 rc=" + rc; card = null; ShowDisconnected(); Log("连接失败 rc=" + rc); return; }

                for (short ax = 1; ax <= AXIS_COUNT; ax++)
                {
                    card.GA_AxisOn(ax);
                    card.GA_SetStopDec(ax, 2.0, 10.0);   // 平滑停 2、急停 10 脉冲/ms²
                }

                isOpen = true;
                homed = false;         // 位置清零过，上一次的坐标不再可信
                homed2 = false;
                ReadInputs();          // 先把输入读一次，避免连上后头 50ms 里用 0 误判
                SetAxisEnabled(true);
                timer1.Start();
                Log("连接成功：卡 192.168.0.1:60000，轴1/轴2 已使能");
                labStatus.Text = "已连接，轴1 / 轴2 已使能。先按【回零】建立坐标";
            }
            catch (Exception ex)
            {
                labStatus.Text = "异常：" + ex.Message;
                Log("连接异常：" + ex);
                card = null;
                ShowDisconnected();
            }
        }

        private void BtnReset_Click(object sender, EventArgs e)
        {
            if (card == null) { labStatus.Text = "请先连接板卡"; return; }
            int rc = card.GA_Reset();
            homed = false;             // 复位后卡内位置清零，坐标作废
            homed2 = false;
            homing = false;
            for (short ax = 1; ax <= AXIS_COUNT; ax++)
            {
                card.GA_AxisOn(ax);
                card.GA_SetStopDec(ax, 2.0, 10.0);
            }
            labStatus.Text = (rc == 0) ? "复位成功，位置已清零。重新回零后再用" : "复位失败 rc=" + rc;
            Log("复位板卡 rc=" + rc);
        }

        private void BtnDisConnect_Click(object sender, EventArgs e)
        {
            timer1.Stop();
            if (card == null) { labStatus.Text = "未连接"; ShowDisconnected(); return; }
            try { card.GA_Stop(0x03, 0x03); } catch { }
            for (short ax = 1; ax <= AXIS_COUNT; ax++) { try { card.GA_AxisOff(ax); } catch { } }
            int rc = card.GA_Close();
            card = null;
            isOpen = false;
            ShowDisconnected();
            labStatus.Text = (rc == 0) ? "已断开" : "关闭失败 rc=" + rc;
            Log("关闭板卡 rc=" + rc);
        }

        // ==================================================================
        //  回零 / 设零点
        // ==================================================================
        /// <summary>
        /// 轴1 回零：方式1（找零位）。卡内部会自己完成
        /// 快速找零位 → 反向离开 → 再找一次 → 走偏移 → 位置清零。
        /// 结束后规划位置就是 0，坐标系就建立了。
        /// </summary>
        private void btnHome1_Click(object sender, EventArgs e)
        {
            if (card == null) { labStatus.Text = "请先连接板卡"; return; }
            if (homing) { labStatus.Text = "正在回零，等它跑完"; return; }

            StartHoming(PickHomeDir());
        }

        /// <summary>
        /// 判断原点在轴的哪一侧，返回应该朝哪个方向找。
        ///   位置 &gt; 0 → 在原点正侧，必须【反转】才能回原点
        ///   位置 &lt; 0 → 在原点负侧，必须【正转】才能回原点
        /// 没回过零时位置不可信，先按默认方向找。
        /// </summary>
        private short PickHomeDir()
        {
            if (homed) return (pos1 >= 0) ? HomeTowardsRev : HomeTowardsFwd;
            return HomeTowardsFwd;
        }

        /// <summary>按给定方向启动一次回零。返回是否启动成功。</summary>
        private bool StartHoming(short dirParam)
        {
            card.GA_AxisOn((short)1);
            card.GA_ClrSts((short)1, 1);

            // 原点开关是常开：读到 1 才是有信号，所以让卡把电平取反
            card.GA_HomeSns(0x0001);

            // 参数：轴1，方式1(找零位)，方向，偏移0，快移20，定位2，Index速度2，加速度1
            int rc = card.GA_HomeSetPrmSingle((short)1, (short)1, dirParam, 0, 20.0, 2.0, 2.0, 1.0);
            if (rc != 0) { labStatus.Text = "设置回零参数失败 rc=" + rc; return false; }

            rc = card.GA_HomeStart((short)1);
            if (rc != 0) { labStatus.Text = "启动回零失败 rc=" + rc; return false; }

            homing = true;
            homeTicks = 0;

            labStatus.Text = "轴1 回零中，朝"
                           + (dirParam == HomeTowardsFwd ? "正转方向" : "反转方向") + "找原点…";
            return true;
        }

        /// <summary>中止回零并停车，同时把原因写到状态栏。</summary>
        private void AbortHoming(string msg)
        {
            homing = false;
            homed = false;                       // 位置不再可信
            try { card.GA_HomeStop((short)1); } catch { }
            try { card.GA_Stop(1 << 0, 0); } catch { }
            labStatus.Text = msg;
        }

        /// <summary>轴2 没有原点开关，用“当前位置设为零点”的办法建立坐标。</summary>
        private void btnZero2_Click(object sender, EventArgs e)
        {
            if (card == null) { labStatus.Text = "请先连接板卡"; return; }
            if (homing) { labStatus.Text = "轴1 正在回零，稍等"; return; }

            int rc = card.GA_ZeroPos((short)2, (short)1);
            if (rc == 0)
            {
                homed2 = true;
                labStatus.Text = "轴2 已设为零点：当前位置 = 0.00 mm";
            }
            else labStatus.Text = "轴2 清零失败 rc=" + rc;
        }

        /// <summary>回零状态轮询：完成就结束，跑太久（60 秒）就中止。</summary>
        private void PollHoming()
        {
            if (!homing) return;

            if (++homeTicks > 1200)
            {
                AbortHoming("回零超时（60 秒）已中止。检查原点开关有没有信号、感应片是否遮得住。");
                return;
            }

            short st = 0;
            int a = 0, b = 0, c = 0;
            card.GA_HomeGetSts((short)1, ref st, ref a, ref b, ref c);

            if (st == 2)
            {
                homing = false;
                homed = true;
                homeTicks = 0;
                card.GA_ClrSts((short)1, 1);
                labStatus.Text = "轴1 回零完成：原点 = 0.00 mm，之后的距离都相对原点";
            }
        }

        // ==================================================================
        //  点动 / 连续 / 暂停
        // ==================================================================
        private void BindAxis(short ax, Button jogPos, Button jogNeg,
                              Button runPos, Button runNeg, Button pause, TextBox speedBox)
        {
            // 点动：按住走、松开停、鼠标滑出也停
            jogPos.MouseDown += (s, e) => { if (e.Button != MouseButtons.Left) return; jogActive[ax] = true; StartMove(ax, +1, speedBox); };
            jogPos.MouseUp += (s, e) => { if (jogActive[ax]) { jogActive[ax] = false; StopAxis(ax); } };
            jogPos.MouseLeave += (s, e) => { if (jogActive[ax]) { jogActive[ax] = false; StopAxis(ax); } };

            jogNeg.MouseDown += (s, e) => { if (e.Button != MouseButtons.Left) return; jogActive[ax] = true; StartMove(ax, -1, speedBox); };
            jogNeg.MouseUp += (s, e) => { if (jogActive[ax]) { jogActive[ax] = false; StopAxis(ax); } };
            jogNeg.MouseLeave += (s, e) => { if (jogActive[ax]) { jogActive[ax] = false; StopAxis(ax); } };

            // 连续：点一下一直走，直到按暂停
            runPos.Click += (s, e) => StartMove(ax, +1, speedBox);
            runNeg.Click += (s, e) => StartMove(ax, -1, speedBox);

            pause.Click += (s, e) => StopAxis(ax);
        }

        private void StartMove(short ax, int dir, TextBox speedBox)
        {
            if (card == null) { labStatus.Text = "请先连接板卡"; return; }
            if (homing) { labStatus.Text = "正在回零，等它跑完"; return; }

            double vMps;
            if (!double.TryParse(speedBox.Text.Trim(), out vMps) || vMps <= 0 || vMps > 1.2)
            {
                labStatus.Text = "速度请填 0 ~ 1.2 之间的数字（单位 m/s）";
                return;
            }

            double ppm = Ppm(ax);
            card.GA_AxisOn(ax);
            card.GA_ClrSts(ax, 1);
            card.GA_PrfJog(ax);                                  // 速度模式
            card.GA_SetStopDec(ax, 2.0, 10.0);

            var jog = new MultiCardCS.MultiCardCS.TJogPrm
            {
                dAcc = Acc(ax) * ppm / 1e6,                      // 脉冲/ms²
                dDec = Acc(ax) * ppm / 1e6,
                dSmooth = 0
            };
            card.GA_SetJogPrm(ax, ref jog);

            // m/s -> 脉冲/ms： v × 1000(mm/s) × ppm / 1000 = v × ppm
            double velPulsePerMs = dir * vMps * ppm;
            card.GA_SetVel(ax, velPulsePerMs);
            card.GA_Update(1 << (ax - 1));

            labStatus.Text = "轴" + ax + " " + (dir > 0 ? "正转 " : "反转 ")
                           + vMps.ToString("0.000") + " m/s";
        }

        /// <summary>让指定轴停下。用 GA_Stop，不用 GA_SetVel(0)——后者在速度模式下不保证停。</summary>
        private void StopAxis(short ax)
        {
            if (card == null) return;
            if (ax == 1 && homing)
            {
                homing = false;
                homed = false;
                card.GA_HomeStop((short)1);
            }
            card.GA_Stop(1 << (ax - 1), 0);     // 第二个参数 0 = 平滑停，1 = 急停
            card.GA_ClrSts(ax, 1);
            labStatus.Text = "轴" + ax + " 已停止";
        }

        // ==================================================================
        //  定时刷新
        // ==================================================================
        private void timer1_Tick(object sender, EventArgs e)
        {
            if (card == null) return;
            try
            {
                ReadInputs();
                ReadAxisStatus();
                PollHoming();
                UpdateUi();
            }
            catch (Exception ex)
            {
                labStatus.Text = "读取失败：" + ex.Message;
            }
        }

        private void ReadInputs()
        {
            card.GA_GetDiRaw(MultiCardCS.MultiCardCS.MC_HOME, ref diHome);   // 零位输入
        }

        private void ReadAxisStatus()
        {
            clock1 = 0;
            card.GA_GetSts((short)1, ref sts1, 1, ref clock1);
            card.GA_GetPrfPos((short)1, ref pos1, 1, ref clock1);
            card.GA_GetPrfVel((short)1, ref vel1, 1, ref clock1);
            card.GA_GetAxisEncPos((short)1, ref enc1, 1, ref clock1);   // 编码器反馈

            clock2 = 0;
            card.GA_GetSts((short)2, ref sts2, 1, ref clock2);
            card.GA_GetPrfPos((short)2, ref pos2, 1, ref clock2);
            card.GA_GetPrfVel((short)2, ref vel2, 1, ref clock2);
        }

        private static bool Bit(int word, int idx)
        {
            return ((word >> idx) & 1) != 0;
        }

        /// <summary>原点开关是常开：感应到读到 1。</summary>
        private bool HomeHit() { return Bit(diHome, HOME_INDEX); }

        // ==================================================================
        //  界面刷新
        // ==================================================================
        private void UpdateUi()
        {
            // 位置换算成实际距离：回零之后规划位置就是“相对原点的脉冲数”，
            // 除以脉冲当量就是毫米。
            lblPos1.Text = "位置\r\n" + FmtPos(pos1, 1);
            lblPos2.Text = "位置\r\n" + FmtPos(pos2, 2);

            bool home = HomeHit();
            bool homeRaw = Bit(diHome, HOME_INDEX);
            lblHome.Text = "原点 Home1（常开）：" + (home ? "有信号" : "无信号")
                         + "　输入=" + (homeRaw ? "1" : "0");
            lblHome.BackColor = home ? Color.LimeGreen : Color.Gray;
            lblHome.ForeColor = home ? Color.Black : Color.White;

            lblRaw.Text = "Home 原始值 = 0x" + (diHome & 0xFFFF).ToString("X4")
                        + "（bit0 = Home1）";
        }

        // ==================================================================
        //  关窗收尾
        // ==================================================================
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try { timer1.Stop(); } catch { }
            if (card != null)
            {
                try { card.GA_Stop(0x03, 0x03); } catch { }
                for (short ax = 1; ax <= AXIS_COUNT; ax++) { try { card.GA_AxisOff(ax); } catch { } }
                try { card.GA_Close(); } catch { }
                card = null;
            }
            base.OnFormClosing(e);
        }
    }
}
