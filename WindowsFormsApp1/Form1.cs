using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MultiCardCS;
using AntiSway.Vision;

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
        //  卡内硬限位
        //
        //  轴1 两端各一个常闭光电，接在通用输入 X0 / X1 上，交给卡自己判断
        //  ——压上就停脉冲，响应是微秒级。软件 50ms 轮询在 0.05m/s 下就要
        //  过冲 2.5mm，高速更没法看，所以不用轮询。
        //
        //  ★ 哪一侧挂 P、哪一侧挂 N，取决于"卡的正方向"实际朝哪边走。
        //    卡内硬限位是分方向的：P 只拦正向运动、N 只拦反向运动，挂反了就会出现
        //    "正转一路走到端点也不停"——因为卡觉得自己在正向走，N 限位管不着。
        //
        //    2026-11-04：驱动器 SW5 原来设 OFF（CCW），按【正转】车朝 X0 走；
        //    后来把 SW5 拨成 ON（CW），方向反过来，现在正转朝 X1。
        //    所以 P 挂 X1、N 挂 X0。**以后只要动了 SW5，这里必须跟着换。**
        //
        //  极性：常闭开关未触发时原始输入读到 1、撞开读到 0，
        //  而卡出厂按"正常低电平、高电平触发"认，所以要取反（lmtNC = true）。
        //  界面上留了勾选，现场对着实车点一下就知道对不对。
        // ==================================================================
        private const short LMT_AXIS = 1;        // 限位挂在轴1
        private const short LMT_TYPE_GPI = 1;    // 0=用零位信号，1=用通用输入
        private const short LMT_CARD = 0;
        private const short LMT_IO_P = 1;        // 卡正向限位 → X1（正转方向撞这侧）
        private const short LMT_IO_N = 0;        // 卡反向限位 → X0（反转方向撞这侧）
        private const int STS_LMT_FWD = 0x20;    // 状态字 bit5：正转端硬限位
        private const int STS_LMT_REV = 0x40;    // 状态字 bit6：反转端硬限位

        // 压到限位后要停住等这么久，才允许反向退出来（用户 2026-11-04 要求 5 秒）。
        // 期间按反方向会被拒绝；到点后按反方向才会临时解掉那一侧再走。
        private const double LMT_HOLD_SEC = 5.0;

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
        private bool linkOn = false;    // 两轴联动是否在跑
        private int linkDir = 0;        // 联动当前方向：+1 正转 / -1 反转
        private bool[] jogActive = new bool[3];

        private int diHome = 0;         // 零位输入
        private int diGpi = 0;          // 通用输入原始值（X0 / X1 接在这里）

        // 硬限位运行状态
        private bool lmtNC = true;                          // 开关是常闭
        private bool lmtFwd = false, lmtRev = false;        // 两路当前是否压着
        private bool lmtFwdOff = false, lmtRevOff = false;  // 为退出来临时关掉的那一侧
        private bool lmtApplied = false;                    // 卡内限位是否设置成功
        private DateTime lmtFwdAt = DateTime.MinValue;      // 正转端压上的时刻（算 5 秒等待）
        private DateTime lmtRevAt = DateTime.MinValue;      // 反转端压上的时刻
        private bool loadingUi = false;                     // 正在回填界面，别触发保存

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

        // ==================================================================
        //  视觉模块（D415 / vs_vision.dll）运行状态
        //
        //  职责边界（2026-11-10 集成时约定）：
        //    视觉只读不控。摆角、角速度、深度只用于【显示】和【写日志】，
        //    绝不用来给控制卡下发速度/位置指令。
        //    原因：vision.ini 里 rope_len_mm=0，摆角目前走 ang_k×(u-u0) 的
        //    占位近似值，不是真实摆角；相机也未必看得到悬点，闭环会误动作。
        //    等现场做完两点法 / 绳长标定，再单独讨论接入防摇控制器。
        // ==================================================================
        private string visionDir = null;        // vision.ini 所在目录
        private string visionIni = null;
        private bool visionInited = false;      // vs_init 是否成功
        private bool visionRunning = false;     // 相机是否开流
        private byte[] visionBuf;               // BGR 预览缓冲，复用
        private Bitmap visionBmp;               // 复用位图
        private int visionTicks = 0;
        private bool lastVisionValid = true;    // 丢检状态变化时才写一条日志
        private StreamWriter visionCsv = null;  // 视觉 + 轴1 对齐记录
        private int visionCsvLines = 0;

        public Form1()
        {
            InitializeComponent();

            // 两轴统一绑定（设计器里这些按钮不要再挂事件，否则会触发两次）
            BindAxis(1, btnJog1Pos, btnJog1Neg, btnRun1Pos, btnRun1Neg, btnPause1, txtSpeed1);
            BindAxis(2, btnJog2Pos, btnJog2Neg, btnRun2Pos, btnRun2Neg, btnPause2, txtSpeed2);

            // 读回上次标定的当量
            LoadCalibration();
            LoadLmtUi();
            Log("当量载入：轴1 " + ppm1.ToString("0.####") + "、轴2 " + ppm2.ToString("0.####")
                + " 脉冲/mm（" + CalFilePath() + "）");

            // 还没连板卡，先把实时读数清空、轴按钮禁掉
            ShowDisconnected();

            Log("—— 程序启动 —— " + Application.StartupPath);
        }

        /// <summary>未连接时：读数全部清空、轴按钮禁用，避免界面显示“假正常”。</summary>
        private void ShowDisconnected()
        {
            homing = false;
            linkOn = false; linkDir = 0;
            lmtFwd = false; lmtRev = false;                 // 重新连接后从"未触发"重新判断
            lmtFwdOff = false; lmtRevOff = false;
            lmtFwdAt = DateTime.MinValue; lmtRevAt = DateTime.MinValue;
            lmtApplied = false;                             // 断开后卡内配置就不作数了
            lblHome.Text = "原点 Home1（常开）：未连接"; lblHome.BackColor = Color.Gray;
            lblLimPos.Text = "正转限位（X1）：-"; lblLimPos.BackColor = Color.Gray; lblLimPos.ForeColor = Color.White;
            lblLimNeg.Text = "反转限位（X0）：-"; lblLimNeg.BackColor = Color.Gray; lblLimNeg.ForeColor = Color.White;
            lblRaw.Text = "原始值 = -";
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
        private double ppm1 = 44.4444;   //脉冲当量
        private double ppm2 = 50.9296;    // 1600 脉冲/转 ÷ (π×10mm 卷筒周长)
        private bool cal1 = false, cal2 = false;   // 是否已标定

        private double Ppm(short ax) { return ax == 1 ? ppm1 : ppm2; }
        private double Acc(short ax) { return ax == 1 ? 500.0 : 200.0; }  // mm/s²

        /// <summary>
        /// 轴方向系数。电机接线或驱动器 SW5 决定的实际转向，未必和界面上的
        /// 【正转】/【反转】一致，这里统一在发脉冲前乘一个符号，界面文案不用动。
        ///
        /// 轴2 历史：
        ///   2026-11-04  按【正转】实际往回走 → 取 -1（对调一次）
        ///   2026-11-09  再对调回来 → +1，现在与轴1 同向
        /// 以后再反一次，就把这里的 1.0 改回 -1.0，只有这一处。
        /// </summary>
        private double DirSign(short ax) { return 1.0; }   // 两轴目前同向；要翻某一轴就写成 ax == 2 ? -1.0 : 1.0

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
                if (!System.IO.File.Exists(p)) return;//跳出try捕捉
                foreach (string line in System.IO.File.ReadAllLines(p))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim().ToLower();
                    string v = line.Substring(eq + 1).Trim();

                    if (k == "lmtnc") { lmtNC = (v == "1"); continue; }

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
                    "ppm2=" + ppm2.ToString("0.####") + "\r\n" +
                    "lmtnc=" + (lmtNC ? "1" : "0") + "\r\n");
            }
            catch (Exception ex) { labStatus.Text = "保存标定失败：" + ex.Message; }
        }

        // ==================================================================
        //  硬限位：设置 / 读取 / 退出
        // ==================================================================

        /// <summary>
        /// 把轴1 两侧硬限位挂到 X1 / X0 并打开。连接和复位之后都必须调一次
        /// —— GA_Reset 会把卡里的限位配置清掉。
        ///
        /// 每个调用都查返回值：这几个函数设错了不抛异常也不报错，
        /// 只会"看起来设过了但根本不管用"，而限位失效是最难查的一类问题。
        /// </summary>
        private void ApplyHardLimits()
        {
            if (card == null) return;
            try
            {
                short inv = (short)(lmtNC ? 1 : 0);      // 常闭要取反

                int r1 = card.GA_SetHardLimP(LMT_AXIS, LMT_TYPE_GPI, LMT_CARD, LMT_IO_P);
                int r2 = card.GA_SetHardLimN(LMT_AXIS, LMT_TYPE_GPI, LMT_CARD, LMT_IO_N);
                int r3 = card.GA_SetLmtSnsSingle(LMT_AXIS, inv, inv);

                // 正负两侧分开打开，不用 -1 通配——万一固件不认这个值，
                // 就是"调用成功但两侧都没开"这种最坏情况。
                int r4 = card.GA_LmtsOn(LMT_AXIS, 0);    // 正转侧
                int r5 = card.GA_LmtsOn(LMT_AXIS, 1);    // 反转侧

                lmtFwdOff = false; lmtRevOff = false;
                lmtApplied = (r1 == 0 && r2 == 0 && r3 == 0 && r4 == 0 && r5 == 0);

                Log("硬限位设置：SetHardLimP=" + r1 + " SetHardLimN=" + r2 + " SetLmtSns=" + r3
                    + " LmtsOn(正)=" + r4 + " LmtsOn(负)=" + r5
                    + "　" + (lmtNC ? "常闭取反" : "常开不取反")
                    + (lmtApplied ? "　全部成功" : "　★有调用返回非 0，限位可能没生效"));

                if (!lmtApplied)
                    labStatus.Text = "硬限位设置没全部成功，看日志里的 rc（0 才算成功）";
            }
            catch (Exception ex)
            {
                lmtApplied = false;
                Log("设置硬限位异常：" + ex.Message);
                labStatus.Text = "设置硬限位异常：" + ex.Message;
            }
        }

        /// <summary>每个节拍解释一次原始输入，并把"退出来后临时关掉的那侧"恢复回来。</summary>
        private void ReadLimits()
        {
            bool f = Trip(diGpi, LMT_IO_P);
            bool r = Trip(diGpi, LMT_IO_N);

            // 回零时压到限位本身就是错的：原点装在中间，压到端点只可能是方向反了。
            // 这一条不依赖卡内限位生不生效——万一卡在回零模式下不管限位，这里也能兜住。
            if (homing && (f || r))
            {
                AbortHoming("轴1 回零中压到" + (f ? "正转方向的 X1 端" : "反转方向的 X0 端")
                            + "，已中止。回零方向反了，或卡内限位没生效。");
                return;
            }

            if (lmtFwdOff && !f)
            {
                try { card.GA_LmtsOn(LMT_AXIS, 0); } catch { }
                lmtFwdOff = false; Log("已离开 X1 端，正向限位恢复");
            }
            if (lmtRevOff && !r)
            {
                try { card.GA_LmtsOn(LMT_AXIS, 1); } catch { }
                lmtRevOff = false; Log("已离开 X0 端，反向限位恢复");
            }

            if (f && !lmtFwd) { lmtFwdAt = DateTime.Now; Log("压到 X1 端（正转方向），硬限位停车，5 秒后可反转退出"); }
            if (r && !lmtRev) { lmtRevAt = DateTime.Now; Log("压到 X0 端（反转方向），硬限位停车，5 秒后可正转退出"); }

            // 联动时轴1 撞了限位：卡只会停轴1，轴2 还在跑，必须把整组停掉
            if (linkOn && (f || r))
            {
                StopLink();
                labStatus.Text = "轴1 到" + (f ? "X1" : "X0") + " 端限位，两轴已停，"
                               + Math.Ceiling(LMT_HOLD_SEC) + " 秒后可反向";
            }

            lmtFwd = f; lmtRev = r;
        }

        /// <summary>常闭开关未触发读到 1、撞开读到 0，所以要取反才是"触发"。</summary>
        private bool Trip(int raw, int idx)
        {
            bool level = Bit(raw, idx);
            return lmtNC ? !level : level;
        }

        /// <summary>
        /// 压着某一侧还往外走 → 不让动。
        /// 往反方向退 → 压上后要等够 LMT_HOLD_SEC（5 秒）才放行；
        /// 放行时先把那一侧限位临时关掉，否则有些卡会把两个方向一起锁死，人退不回来。
        /// 退出后 ReadLimits 检测到开关松开会自动恢复。
        /// </summary>
        private bool EscapeLimit(short ax, int dir)
        {
            if (ax != LMT_AXIS) return true;

            if (dir > 0 && lmtFwd) { labStatus.Text = "正转方向已压到 X1 端，先按【反转】退出来"; return false; }
            if (dir < 0 && lmtRev) { labStatus.Text = "反转方向已压到 X0 端，先按【正转】退出来"; return false; }

            if (dir < 0 && lmtFwd)
            {
                double left = LMT_HOLD_SEC - (DateTime.Now - lmtFwdAt).TotalSeconds;
                if (left > 0)
                {
                    labStatus.Text = "刚压到 X1 端，还要等 " + Math.Ceiling(left) + " 秒才能反转退出";
                    return false;
                }
                try { card.GA_LmtsOff(LMT_AXIS, 0); } catch { }
                lmtFwdOff = true;
                Log("X1 端已等待满 " + LMT_HOLD_SEC + " 秒，解除该侧限位，允许反转退出");
            }
            if (dir > 0 && lmtRev)
            {
                double left = LMT_HOLD_SEC - (DateTime.Now - lmtRevAt).TotalSeconds;
                if (left > 0)
                {
                    labStatus.Text = "刚压到 X0 端，还要等 " + Math.Ceiling(left) + " 秒才能正转退出";
                    return false;
                }
                try { card.GA_LmtsOff(LMT_AXIS, 1); } catch { }
                lmtRevOff = true;
                Log("X0 端已等待满 " + LMT_HOLD_SEC + " 秒，解除该侧限位，允许正转退出");
            }
            return true;
        }

        /// <summary>5 秒等待期间的倒计时提示。</summary>
        private string HoldHint(bool fwd)
        {
            double left = LMT_HOLD_SEC - (DateTime.Now - (fwd ? lmtFwdAt : lmtRevAt)).TotalSeconds;
            return left > 0 ? "（" + Math.Ceiling(left) + "s 后可退）" : "（可退出）";
        }

        private void UpdateLimitLabels()
        {
            lblLimPos.Text = "正转方向（X1）：" + (lmtFwd ? "触发" + HoldHint(true) : "正常");
            lblLimPos.BackColor = lmtFwd ? Color.OrangeRed : Color.Gainsboro;
            lblLimPos.ForeColor = lmtFwd ? Color.White : Color.Black;

            lblLimNeg.Text = "反转方向（X0）：" + (lmtRev ? "触发" + HoldHint(false) : "正常");
            lblLimNeg.BackColor = lmtRev ? Color.OrangeRed : Color.Gainsboro;
            lblLimNeg.ForeColor = lmtRev ? Color.White : Color.Black;
        }

        private void LoadLmtUi()
        {
            loadingUi = true;                 // 回填时别触发 CheckedChanged 去写文件
            chkLmtNC.Checked = lmtNC;
            loadingUi = false;
        }

        private void chkLmtNC_CheckedChanged(object sender, EventArgs e)
        {
            if (loadingUi) return;
            lmtNC = chkLmtNC.Checked;
            SaveCalibration();
            if (isOpen) ApplyHardLimits();
            labStatus.Text = lmtNC
                ? "限位按常闭处理：未触发读到 1，撞开读到 0"
                : "限位按常开处理：未触发读到 0，撞到读到 1";
        }

        // ==================================================================
        //  连接 / 复位 / 断开
        // ==================================================================
        private void BtnConnect_Click(object sender, EventArgs e)
        {
            if (isOpen) { labStatus.Text = "已经连接"; return; }
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
                ApplyHardLimits();     // 使能之后再挂限位
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
            ApplyHardLimits();         // GA_Reset 会把卡里的限位配置清掉，必须重设
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
            if (linkOn) StopLink();      // 回零和联动不能同时跑

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

            if (++homeTicks > 600)
            {
                AbortHoming("回零超时（30秒）已中止。检查原点开关有没有信号、感应片是否遮得住。");
                return;
            }

            short st = 0;
            int a = 0, b = 0, c = 0;
            card.GA_HomeGetSts((short)1, ref st, ref a, ref b, ref c);

            // 卡内限位拦住了 —— 说明回零方向反了。立刻中止，别等 60 秒超时。
            if (Bit(sts1, 5) || Bit(sts1, 6))
            {
                AbortHoming("轴1 回零途中撞到限位（" + (Bit(sts1, 5) ? "X1 端" : "X0 端")
                            + "），已中止。回零方向可能反了，先反向退出来。");
                return;
            }

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

            if (linkOn) StopLink();      // 单轴指令会盖掉联动，先把整组停掉

            // 压着限位还往外走要拦下；往反方向退，先解锁那一侧（否则卡可能不让动）
            if (!EscapeLimit(ax, dir)) return;

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
            // 再乘轴方向系数（DirSign）：目前两轴都为 +1，即按界面方向走；
            // 万一某轴实际转向相反，只改 DirSign 一处即可，这里不用动。
            double velPulsePerMs = dir * DirSign(ax) * vMps * ppm;
            card.GA_SetVel(ax, velPulsePerMs);
            card.GA_Update(1 << (ax - 1));

            // 起转时把卡的状态字和位置记下来。
            // 出现"电机不动但脉冲一直涨"时，这条能分清是卡自己知道有问题（状态字有报警/限位位），
            // 还是卡以为一切正常（那问题就在脉冲→驱动器这一段）。
            Log("轴" + ax + " 起转 " + (dir > 0 ? "正转 " : "反转 ") + vMps.ToString("0.000")
                + " m/s　脉冲/ms=" + velPulsePerMs.ToString("0.###")
                + "　状态字=0x" + ((ax == 1 ? sts1 : sts2) & 0xFFFF).ToString("X4")
                + "　位置=" + (ax == 1 ? pos1 : pos2).ToString("0"));

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
        //  两轴联动（连续转动）
        //
        //  正转/反转都是连续转，速度各取轴1、轴2 上面的速度框；
        //  两轴参数先设好，最后只用一次 GA_Update(0x03) 下发，保证同时起步。
        //  轴1 撞限位 → 两轴一起停 → 等 5 秒才能反向（等待由 EscapeLimit 判）。
        //  轴2 没有限位也没有编码器，所以位移一律以轴1 为参考。
        // ==================================================================
        private void btnLinkFwd_Click(object sender, EventArgs e) { StartLink(+1); }
        private void btnLinkRev_Click(object sender, EventArgs e) { StartLink(-1); }

        private void btnLinkStop_Click(object sender, EventArgs e)
        {
            if (!linkOn) { labStatus.Text = "联动没在跑"; return; }
            StopLink();
            labStatus.Text = "两轴已暂停";
        }

        private void StartLink(int dir)
        {
            if (card == null) { labStatus.Text = "请先连接板卡"; return; }
            if (homing) { labStatus.Text = "正在回零，等它跑完"; return; }

            // 已经在跑：同方向不重复下发；换方向先停稳，别带着速度反接
            if (linkOn)
            {
                if (linkDir == dir) { labStatus.Text = "已经在" + (dir > 0 ? "正转" : "反转") + "了"; return; }
                StopLink();
            }

            double v1, v2;
            if (!double.TryParse(txtSpeed1.Text.Trim(), out v1) || v1 <= 0 || v1 > 1.2 ||
                !double.TryParse(txtSpeed2.Text.Trim(), out v2) || v2 <= 0 || v2 > 1.2)
            {
                labStatus.Text = "两轴速度都要填 0 ~ 1.2 之间（m/s）";
                return;
            }

            // 限位与"压上限位后 5 秒才允许反向"都在这一个函数里判
            if (!EscapeLimit((short)1, dir)) return;

            for (short ax = 1; ax <= AXIS_COUNT; ax++)
            {
                double ppm = Ppm(ax);
                double v = (ax == 1) ? v1 : v2;

                card.GA_AxisOn(ax);
                card.GA_ClrSts(ax, 1);
                card.GA_PrfJog(ax);
                card.GA_SetStopDec(ax, 2.0, 10.0);

                var jog = new MultiCardCS.MultiCardCS.TJogPrm
                {
                    dAcc = Acc(ax) * ppm / 1e6,
                    dDec = Acc(ax) * ppm / 1e6,
                    dSmooth = 0
                };
                card.GA_SetJogPrm(ax, ref jog);
                card.GA_SetVel(ax, dir * DirSign(ax) * v * ppm);
            }

            card.GA_Update(0x03);          // 两轴一次性下发，同时起步

            linkOn = true;
            linkDir = dir;
            labStatus.Text = "两轴联动" + (dir > 0 ? "正转" : "反转")
                           + "：轴1 " + v1.ToString("0.000") + " m/s，轴2 "
                           + v2.ToString("0.000") + " m/s";
            Log("两轴联动" + (dir > 0 ? "正转" : "反转")
                + "：轴1 " + v1.ToString("0.000") + " m/s（" + (v1 * Ppm(1)).ToString("0.###") + " 脉冲/ms）"
                + "，轴2 " + v2.ToString("0.000") + " m/s（" + (v2 * Ppm(2)).ToString("0.###") + " 脉冲/ms）");
        }

        /// <summary>停掉整组。调用方负责写状态栏文案。</summary>
        private void StopLink()
        {
            if (card != null)
            {
                try { card.GA_Stop(0x03, 0); } catch { }        // 两轴一起平滑停
                for (short ax = 1; ax <= AXIS_COUNT; ax++) { try { card.GA_ClrSts(ax, 1); } catch { } }
            }
            if (linkOn) Log("两轴联动停止（之前方向 " + (linkDir > 0 ? "正转" : "反转") + "）");
            linkOn = false;
            linkDir = 0;
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
                ReadLimits();
                PollHoming();      // 里面要看 sts1 的限位位，必须在 ReadAxisStatus 之后
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
            card.GA_GetDiRaw(MultiCardCS.MultiCardCS.MC_GPI, ref diGpi);     // 通用输入（X0 / X1）
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
            lblLinkPos.Text = "联动位移（轴1）\r\n" + FmtPos(pos1, 1);
            lblPos2.Text = "位置\r\n" + FmtPos(pos2, 2);

            bool home = HomeHit();
            bool homeRaw = Bit(diHome, HOME_INDEX);
            lblHome.Text = "原点 Home1（常开）：" + (home ? "有信号" : "无信号")
                         + "　输入=" + (homeRaw ? "1" : "0");
            lblHome.BackColor = home ? Color.LimeGreen : Color.Gray;
            lblHome.ForeColor = home ? Color.Black : Color.White;

            UpdateLimitLabels();

            lblRaw.Text = "原始值　Home=0x" + (diHome & 0xFFFF).ToString("X4")
                        + "　GPI=0x" + (diGpi & 0xFFFF).ToString("X4")
                        + "（bit0=X0　bit1=X1）　卡内限位："
                        + (lmtApplied ? "已启用" : "未启用，看日志 rc");
        }

        // ==================================================================
        //  视觉模块：定位 / 开流 / 刷新 / 记录 / 收尾
        //
        //  和板卡逻辑完全解耦：视觉用独立的 timerVision，不进 timer1_Tick；
        //  视觉没插相机、DLL 缺失、开流失败都只写日志，绝不阻拦板卡操作。
        // ==================================================================

        /// <summary>
        /// 窗体显示后再定位视觉模块。vs_vision.dll 不在 exe 目录，而在工程根的
        /// vision\ 下，所以先让 Windows 去那个目录找它和它的依赖
        /// （realsense2.dll、libopencv_*.dll、libstdc++-6.dll）。
        /// 不拷贝 DLL 到 bin：改完 C++ 重跑 vision\build.bat 就立刻生效，
        /// 也不会出现"拷贝失败但编译成功、跑的还是旧 DLL"。
        /// </summary>
        private void Form1_Shown(object sender, EventArgs e)
        {
            LocateVision();
            TryStartVision(false);      // 自动开流；失败只写日志
        }

        private void LocateVision()
        {
            try
            {
                if (File.Exists(Path.Combine(Application.StartupPath, "vision.ini")))
                    visionDir = Application.StartupPath;

                if (visionDir == null)
                {
                    DirectoryInfo d = new DirectoryInfo(Application.StartupPath);
                    for (int i = 0; i < 8 && d != null; ++i, d = d.Parent)
                    {
                        string p = Path.Combine(d.FullName, "vision");
                        if (File.Exists(Path.Combine(p, "vision.ini"))) { visionDir = p; break; }
                    }
                }

                if (visionDir == null)
                {
                    SetVisionState("未找到 vision.ini", Color.Gainsboro);
                    VisionLog("[ERR] 找不到 vision 目录。视觉不可用，运动控制卡功能不受影响。");
                    return;
                }

                Vision.PrepareDllDir(visionDir);
                visionIni = Path.Combine(visionDir, "vision.ini");
                string ver = Vision.Version;
                lblVisionDll.Text = "vs_vision.dll " + (string.IsNullOrEmpty(ver) ? "?" : ver);
                VisionLog("视觉模块目录：" + visionDir);
                VisionLog("版本 " + ver + "，配置 " + visionIni);
                if (string.IsNullOrEmpty(ver))
                    VisionLog("[ERR] 读不到 DLL 版本，检查 vision 目录下 vs_vision.dll 及依赖是否齐全");
            }
            catch (Exception ex)
            {
                VisionLog("[ERR] 定位视觉模块异常：" + ex.Message);
            }
        }

        private void TryStartVision(bool manual)
        {
            if (visionRunning) { if (manual) VisionLog("视觉已经在运行"); return; }
            if (visionDir == null) { if (manual) VisionLog("[ERR] 没有 vision.ini，无法开流"); return; }

            try
            {
                if (!visionInited)
                {
                    if (!Vision.Init(visionIni))
                    {
                        VisionLog("[ERR] 视觉初始化失败：" + Vision.LastError);
                        SetVisionState("初始化失败", Color.Gainsboro);
                        return;
                    }
                    visionInited = true;
                    VisionLog("配置已载入（vision.ini）");
                }

                if (!Vision.Start())
                {
                    // 没插相机时 DLL 枚举到 0 台设备会立刻返回，不阻塞板卡界面
                    VisionLog("[ERR] 开流失败：" + Vision.LastError);
                    SetVisionState("相机未连接", Color.Gainsboro);
                    return;
                }

                visionRunning = true;
                lastVisionValid = true;
                visionTicks = 0;
                OpenVisionCsv();
                timerVision.Start();
                SetVisionState("运行中", Color.DarkGreen);
                VisionLog("相机已开流：" + Vision.State);
            }
            catch (DllNotFoundException)
            {
                VisionLog("[ERR] 找不到 vs_vision.dll 或依赖，检查 vision 目录，必要时重跑 build.bat");
                SetVisionState("缺少 DLL", Color.Gainsboro);
            }
            catch (BadImageFormatException)
            {
                VisionLog("[ERR] 位数不匹配：本程序必须用 x64 平台生成（视觉 DLL 只有 64 位）");
                SetVisionState("平台不匹配", Color.Gainsboro);
            }
            catch (Exception ex)
            {
                VisionLog("[ERR] 视觉开流异常：" + ex.Message);
                SetVisionState("开流异常", Color.Gainsboro);
            }
        }

        private void SetVisionState(string text, Color back)
        {
            lblVisionState.Text = "视觉：" + text;
            lblVisionState.BackColor = back;
            lblVisionState.ForeColor = back.GetBrightness() < 0.5 ? Color.White : Color.Black;
        }

        private void btnVisionStart_Click(object sender, EventArgs e) { TryStartVision(true); }

        private void btnVisionStop_Click(object sender, EventArgs e)
        {
            if (!visionRunning) { VisionLog("视觉没在运行"); return; }
            timerVision.Stop();
            try { Vision.Stop(); } catch (Exception ex) { VisionLog("[ERR] 停止取流异常：" + ex.Message); }
            visionRunning = false;
            CloseVisionCsv();
            SetVisionState("已停止", Color.Gainsboro);
            lblAngleBig.Text = "— °";
            lblAngleBig.ForeColor = Color.Silver;
            VisionLog("已停止取流");
        }

        private void btnVisionZero_Click(object sender, EventArgs e)
        {
            if (!visionRunning) { VisionLog("请先开流，再定零位"); return; }
            try
            {
                if (!Vision.CalibrateZero()) { VisionLog("[ERR] 定零位失败：" + Vision.LastError); return; }
                bool saved = Vision.SaveCalibration();
                VisionLog("已把当前位置定为摆角 0 位"
                    + (saved ? "，写入 vision_calib.ini" : "（保存标定失败：" + Vision.LastError + "）")
                    + "。吊重必须静止。");
            }
            catch (Exception ex) { VisionLog("[ERR] 定零位异常：" + ex.Message); }
        }

        private void btnVisionSnap_Click(object sender, EventArgs e)
        {
            if (!visionRunning) { VisionLog("请先开流，再存图"); return; }
            string p = Path.Combine(Application.StartupPath,
                                    "snap_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".jpg");
            try
            {
                if (Vision.Snapshot(p))
                    VisionLog("已存图 " + p + "（原始帧 / 带框由 vision.ini 的 [out] snapshot_raw 决定）");
                else
                    VisionLog("[ERR] 存图失败：" + Vision.LastError);
            }
            catch (Exception ex) { VisionLog("[ERR] 存图异常：" + ex.Message); }
        }

        private void timerVision_Tick(object sender, EventArgs e)
        {
            if (!visionRunning) return;
            VsResult r;
            try
            {
                if (!Vision.TryRead(out r)) return;      // 这一拍没有新帧
                UpdateVisionUi(r);
                WriteVisionCsv(r);
                if ((++visionTicks & 1) == 0) RenderVisionPreview();   // 画面减半刷新，省 CPU
            }
            catch (Exception ex)
            {
                timerVision.Stop();
                visionRunning = false;
                VisionLog("[ERR] 视觉刷新异常，已停止取流：" + ex.Message);
                SetVisionState("刷新异常", Color.Gainsboro);
            }
        }

        /// <summary>只更新读数，不下发任何运动指令。</summary>
        private void UpdateVisionUi(VsResult r)
        {
            bool ok = r.valid == 1;

            lblAngleBig.Text = ok ? r.angleDeg.ToString("+0.00;-0.00;0.00") + "°" : "— °";
            lblAngleBig.ForeColor = ok ? Color.Black : Color.Silver;
            lblVisionRate.Text = string.Format("角速度   {0:+0.00;-0.00;0.00} °/s", r.rateDps);
            lblVisionOffset.Text = string.Format("水平偏移 {0:+0.0;-0.0;0.0} mm", r.offXMm);
            lblVisionDepth.Text = string.Format("深度     {0:0} mm", r.zMm);
            lblVisionFps.Text = string.Format("FPS {0:0.0}  帧 {1}  置信 {2:0.00}",
                                              r.fpsX10 / 10.0, r.frameIndex, r.confidence);

            if (ok != lastVisionValid)
            {
                VisionLog(ok ? "检测恢复（帧 " + r.frameIndex + "）"
                             : "丢检，摆角保持上一拍（帧 " + r.frameIndex + "）");
                lastVisionValid = ok;
            }
            SetVisionState(ok ? "检测正常" : "丢检（保持）", ok ? Color.DarkGreen : Color.Firebrick);
        }

        /// <summary>把 DLL 里的 BGR 帧搬进复用的 Bitmap，逐行拷（位图每行可能有对齐填充）。</summary>
        private void RenderVisionPreview()
        {
            int w, h;
            if (!Vision.GetPreviewSize(out w, out h)) return;

            int need = w * h * 3;
            if (visionBuf == null || visionBuf.Length < need) visionBuf = new byte[need];
            if (Vision.CopyPreview(visionBuf) != need) return;

            if (visionBmp == null || visionBmp.Width != w || visionBmp.Height != h)
            {
                if (visionBmp != null) visionBmp.Dispose();
                visionBmp = new Bitmap(w, h, PixelFormat.Format24bppRgb);
            }

            Rectangle rect = new Rectangle(0, 0, w, h);
            BitmapData data = visionBmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
            try
            {
                int rowBytes = w * 3;
                for (int y = 0; y < h; ++y)
                    Marshal.Copy(visionBuf, y * rowBytes, IntPtr.Add(data.Scan0, y * data.Stride), rowBytes);
            }
            finally { visionBmp.UnlockBits(data); }

            picPreview.Image = visionBmp;
            picPreview.Invalidate();
        }

        /// <summary>
        /// 每帧写一行「视觉读数 + 同一时刻轴1 状态」，供事后画
        /// 小车位置/速度 ↔ 摆角 曲线。视觉 DLL 自己那份 logs\vision_*.csv
        /// 只有视觉量和硬件时间戳，这一份是专门做防摇实验对齐用的。
        /// </summary>
        private void WriteVisionCsv(VsResult r)
        {
            if (visionCsv == null) return;
            try
            {
                double mm = homed ? pos1 / Ppm(1) : 0.0;
                visionCsv.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{0:HH:mm:ss.fff},{1},{2},{3:F3},{4:F3},{5:F1},{6:F0},{7:F3},{8},{9:F1},{10},{11:F0},{12:F2},{13:F3},{14},{15}",
                    DateTime.Now, r.frameIndex, r.valid,
                    r.angleDeg, r.rateDps, r.offXMm, r.zMm, r.confidence, r.cls,
                    r.fpsX10 / 10.0,
                    isOpen ? 1 : 0, pos1, mm, vel1, linkDir, homed ? 1 : 0));
                if (++visionCsvLines % 20 == 0) visionCsv.Flush();
            }
            catch (Exception ex)
            {
                VisionLog("[ERR] 写视觉对齐记录失败：" + ex.Message);
                CloseVisionCsv();
            }
        }

        private void OpenVisionCsv()
        {
            try
            {
                string dir = LogDir();
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string f = Path.Combine(dir, "vision_axis_" + DateTime.Now.ToString("yyyy-MM-dd") + ".csv");
                bool exists = File.Exists(f);
                visionCsv = new StreamWriter(f, true, System.Text.Encoding.UTF8);
                if (!exists)
                    visionCsv.WriteLine("time,frame,valid,angle_deg,rate_dps,off_x_mm,z_mm,conf,cls,fps," +
                                        "card_on,axis1_pulse,axis1_mm,axis1_vel,link_dir,homed");
                visionCsvLines = 0;
                VisionLog("视觉/轴1 对齐记录：" + f);
            }
            catch (Exception ex)
            {
                VisionLog("[ERR] 打不开对齐记录文件：" + ex.Message);
                visionCsv = null;
            }
        }

        private void CloseVisionCsv()
        {
            if (visionCsv == null) return;
            try { visionCsv.Flush(); visionCsv.Dispose(); } catch { }
            visionCsv = null;
        }

        /// <summary>视觉事件同时进界面小日志和现有的「日志」目录文本文件。</summary>
        private void VisionLog(string msg)
        {
            Log("[视觉] " + msg);
            try
            {
                txtVisionLog.AppendText(DateTime.Now.ToString("HH:mm:ss.fff") + "  " + msg + Environment.NewLine);
                if (txtVisionLog.Lines.Length > 120)
                {
                    string[] keep = new string[80];
                    Array.Copy(txtVisionLog.Lines, txtVisionLog.Lines.Length - 80, keep, 0, 80);
                    txtVisionLog.Lines = keep;
                }
            }
            catch { }
        }

        // ==================================================================
        //  关窗收尾
        // ==================================================================
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try { timer1.Stop(); } catch { }

            // 视觉：先停节拍再停采集线程，最后刷盘 / 释放位图
            try { timerVision.Stop(); } catch { }
            if (visionRunning || visionInited)
            {
                try { Vision.Stop(); } catch { }
                try { Vision.Release(); } catch { }
            }
            visionRunning = false;
            visionInited = false;
            CloseVisionCsv();
            if (visionBmp != null) { try { visionBmp.Dispose(); } catch { } visionBmp = null; }
            if (picPreview != null) picPreview.Image = null;

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
