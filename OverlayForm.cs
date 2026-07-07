using System;
using System.Drawing;
using System.Windows.Forms;

public class OverlayForm : Form
{
    private Panel pnlCard;
    private Panel pnlTitleBar;
    private Label lblTitle;
    private Label btnPin;
    private Label btnClose;

    // Action buttons panel
    private FlowLayoutPanel pnlActions;
    private Button btnSolo;
    private Button btnBlock;
    private Button btnAllow;

    // Metrics panel (TableLayout for structured grid)
    private TableLayoutPanel tblMetrics;
    private Label lblPeers;
    private Label lblFps;
    private Label lblPing;
    private Label lblCpu;
    private Label lblRam;
    private Label lblGpu;

    private bool drag;
    private Point startPoint = new Point(0, 0);
    private MainForm mainForm;
    private Timer metricsTimer;
    private bool clickThrough = false;

    // State cache
    private int peerCount = 0;
    private bool netBlocked = false;
    private long? lastPing = null;
    private int pingTicker = 0;

    public OverlayForm(MainForm parent)
    {
        this.mainForm = parent;
        this.FormBorderStyle = FormBorderStyle.None;
        this.ShowInTaskbar = false;
        this.TopMost = true;
        this.Size = new Size(340, 130);
        this.BackColor = Color.Lime;
        this.TransparencyKey = Color.Lime;
        this.StartPosition = FormStartPosition.Manual;

        int x = int.Parse(ConfigManager.Get("OverlayX", "100"));
        int y = int.Parse(ConfigManager.Get("OverlayY", "100"));
        this.Location = new Point(x, y);

        // Card Panel (background)
        pnlCard = new Panel();
        pnlCard.Dock = DockStyle.Fill;
        pnlCard.BackColor = Theme.BgGray;
        pnlCard.Paint += PnlCard_Paint;
        this.Controls.Add(pnlCard);

        // Titlebar Panel
        pnlTitleBar = new Panel();
        pnlTitleBar.Height = 22;
        pnlTitleBar.Dock = DockStyle.Top;
        pnlTitleBar.BackColor = Color.FromArgb(16, 16, 16);
        pnlTitleBar.MouseDown += Drag_MouseDown;
        pnlTitleBar.MouseMove += Drag_MouseMove;
        pnlTitleBar.MouseUp += Drag_MouseUp;
        pnlCard.Controls.Add(pnlTitleBar);

        lblTitle = new Label();
        lblTitle.Text = "GTA Multi-Tool Overlay";
        lblTitle.Font = new Font("Segoe UI", 8, FontStyle.Bold);
        lblTitle.ForeColor = Theme.RsYellow;
        lblTitle.Location = new Point(6, 4);
        lblTitle.AutoSize = true;
        lblTitle.MouseDown += Drag_MouseDown;
        lblTitle.MouseMove += Drag_MouseMove;
        lblTitle.MouseUp += Drag_MouseUp;
        pnlTitleBar.Controls.Add(lblTitle);

        btnClose = new Label();
        btnClose.Text = "✕";
        btnClose.Font = new Font("Segoe UI", 9, FontStyle.Bold);
        btnClose.ForeColor = Color.DarkGray;
        btnClose.Size = new Size(18, 18);
        btnClose.Location = new Point(this.Width - 22, 2);
        btnClose.Cursor = Cursors.Hand;
        btnClose.Click += (s, e) => { this.Close(); };
        btnClose.MouseEnter += (s, e) => btnClose.ForeColor = Color.White;
        btnClose.MouseLeave += (s, e) => btnClose.ForeColor = Color.DarkGray;
        pnlTitleBar.Controls.Add(btnClose);

        btnPin = new Label();
        btnPin.Text = "📌";
        btnPin.Font = new Font("Segoe UI", 8);
        btnPin.ForeColor = Color.DarkGray;
        btnPin.Size = new Size(18, 18);
        btnPin.Location = new Point(this.Width - 42, 2);
        btnPin.Cursor = Cursors.Hand;
        btnPin.Click += BtnPin_Click;
        pnlTitleBar.Controls.Add(btnPin);

        // Quick Actions Row
        pnlActions = new FlowLayoutPanel();
        pnlActions.Height = 28;
        pnlActions.Location = new Point(6, 26);
        pnlActions.Width = this.Width - 12;
        pnlActions.FlowDirection = FlowDirection.LeftToRight;
        pnlActions.BackColor = Color.Transparent;
        pnlCard.Controls.Add(pnlActions);

        btnSolo = CreateActionButton("🎯 SOLO", Theme.RsYellow, Color.Black, BtnSolo_Click);
        btnBlock = CreateActionButton("🚫 BLOCK", Theme.WastedRed, Color.White, BtnBlock_Click);
        btnAllow = CreateActionButton("✓ ALLOW", Theme.MoneyGreen, Color.White, BtnAllow_Click);
        pnlActions.Controls.Add(btnSolo);
        pnlActions.Controls.Add(btnBlock);
        pnlActions.Controls.Add(btnAllow);

        // Grid Metrics Table
        tblMetrics = new TableLayoutPanel();
        tblMetrics.Location = new Point(6, 58);
        tblMetrics.Size = new Size(this.Width - 12, 66);
        tblMetrics.ColumnCount = 3;
        tblMetrics.RowCount = 2;
        tblMetrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        tblMetrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        tblMetrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        tblMetrics.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        tblMetrics.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        pnlCard.Controls.Add(tblMetrics);

        lblPeers = CreateMetricLabel();
        lblFps = CreateMetricLabel();
        lblPing = CreateMetricLabel();
        lblCpu = CreateMetricLabel();
        lblRam = CreateMetricLabel();
        lblGpu = CreateMetricLabel();

        tblMetrics.Controls.Add(lblPeers, 0, 0);
        tblMetrics.Controls.Add(lblFps, 1, 0);
        tblMetrics.Controls.Add(lblPing, 2, 0);
        tblMetrics.Controls.Add(lblCpu, 0, 1);
        tblMetrics.Controls.Add(lblRam, 1, 1);
        tblMetrics.Controls.Add(lblGpu, 2, 1);

        // Context menu for right click
        pnlCard.MouseClick += (s, e) => { if (e.Button == MouseButtons.Right) ShowContextMenu(Cursor.Position); };
        pnlTitleBar.MouseClick += (s, e) => { if (e.Button == MouseButtons.Right) ShowContextMenu(Cursor.Position); };

        // Metrics Timer
        metricsTimer = new Timer();
        metricsTimer.Interval = 1000;
        metricsTimer.Tick += MetricsTimer_Tick;
        metricsTimer.Start();
    }

    private Button CreateActionButton(string text, Color backColor, Color foreColor, EventHandler clickHandler)
    {
        var btn = new Button();
        btn.Text = text;
        btn.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
        btn.BackColor = backColor;
        btn.ForeColor = foreColor;
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 0;
        btn.Size = new Size(100, 22);
        btn.Cursor = Cursors.Hand;
        btn.Click += clickHandler;
        return btn;
    }

    private Label CreateMetricLabel()
    {
        var lbl = new Label();
        lbl.Font = new Font("Segoe UI", 8, FontStyle.Regular);
        lbl.ForeColor = Color.White;
        lbl.Dock = DockStyle.Fill;
        lbl.TextAlign = ContentAlignment.MiddleLeft;
        lbl.AutoSize = false;
        return lbl;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        // Verify overlay position is visible on any screen, else reset to (100, 100)
        bool visibleOnAnyScreen = false;
        var myRect = new Rectangle(this.Location, this.Size);
        foreach (var screen in Screen.AllScreens)
        {
            if (screen.WorkingArea.IntersectsWith(myRect))
            {
                visibleOnAnyScreen = true;
                break;
            }
        }
        if (!visibleOnAnyScreen)
        {
            this.Location = new Point(100, 100);
            ConfigManager.Set("OverlayX", "100");
            ConfigManager.Set("OverlayY", "100");
        }

        ApplyLocalization();
        ApplyWidgetVisibility();
        bool lockOverlay = ConfigManager.Get("LockOverlay", "false") == "true";
        SetClickThrough(lockOverlay);
    }

    public void ApplyLocalization()
    {
        lblTitle.Text = I18n.T("App_Title");
        btnSolo.Text = I18n.T("Btn_Solo", mainForm.GetSuspendDuration());
        btnBlock.Text = I18n.T("Btn_Block");
        btnAllow.Text = I18n.T("Btn_Allow");
        UpdatePeersAndStatusText();
    }

    public void ApplyWidgetVisibility()
    {
        lblPeers.Visible = ConfigManager.Get("ShowWidget_Peers", "true") == "true";
        lblFps.Visible = ConfigManager.Get("ShowWidget_Fps", "true") == "true";
        lblPing.Visible = ConfigManager.Get("ShowWidget_Ping", "true") == "true";
        lblCpu.Visible = ConfigManager.Get("ShowWidget_Cpu", "true") == "true";
        lblRam.Visible = ConfigManager.Get("ShowWidget_Ram", "true") == "true";
        lblGpu.Visible = ConfigManager.Get("ShowWidget_Gpu", "true") == "true";
    }

    public void SetClickThrough(bool clickThrough)
    {
        this.clickThrough = clickThrough;
        if (this.IsHandleCreated)
        {
            IntPtr hwnd = this.Handle;
            int exStyle = (int)NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
            if (clickThrough)
            {
                exStyle |= NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_LAYERED;
                lblTitle.Text = I18n.T("App_Title") + " (Locked)";
                pnlActions.Visible = false;
                pnlTitleBar.Visible = false;
                tblMetrics.Location = new Point(6, 6);
                tblMetrics.Height = this.Height - 12;
                btnPin.Text = "🔒";
            }
            else
            {
                exStyle &= ~NativeMethods.WS_EX_TRANSPARENT;
                lblTitle.Text = I18n.T("App_Title");
                pnlActions.Visible = true;
                pnlTitleBar.Visible = true;
                tblMetrics.Location = new Point(6, 58);
                tblMetrics.Height = 66;
                btnPin.Text = "📌";
            }
            NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(exStyle));
        }
    }

    private void BtnPin_Click(object sender, EventArgs e)
    {
        clickThrough = !clickThrough;
        SetClickThrough(clickThrough);
        ConfigManager.Set("LockOverlay", clickThrough ? "true" : "false");
        mainForm.SyncLockOverlayCheckbox(clickThrough);
    }

    private void ShowContextMenu(Point screenLocation)
    {
        ContextMenuStrip menu = new ContextMenuStrip();
        menu.Items.Add(I18n.T("Btn_Block"), null, (s, ev) => mainForm.InvokeBlock());
        menu.Items.Add(I18n.T("Btn_Allow"), null, (s, ev) => mainForm.InvokeAllow());
        menu.Items.Add(I18n.T("Btn_Solo", mainForm.GetSuspendDuration()), null, (s, ev) => mainForm.InvokeSolo());
        menu.Items.Add(clickThrough ? "Unlock Position" : "Lock Position", null, (s, ev) => BtnPin_Click(this, EventArgs.Empty));
        menu.Items.Add("Close Overlay", null, (s, ev) => this.Close());
        menu.Show(screenLocation);
    }

    private void PnlCard_Paint(object sender, PaintEventArgs e)
    {
        using (Pen pen = new Pen(Theme.RsYellow, 1))
        {
            e.Graphics.DrawRectangle(pen, 0, 0, pnlCard.Width - 1, pnlCard.Height - 1);
        }
    }

    private void Drag_MouseDown(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            drag = true;
            startPoint = new Point(e.X, e.Y);
        }
    }

    private void Drag_MouseMove(object sender, MouseEventArgs e)
    {
        if (drag)
        {
            Point p = PointToScreen(e.Location);
            this.Location = new Point(p.X - startPoint.X, p.Y - startPoint.Y);
        }
    }

    private void Drag_MouseUp(object sender, MouseEventArgs e)
    {
        drag = false;
        ConfigManager.Set("OverlayX", this.Location.X.ToString());
        ConfigManager.Set("OverlayY", this.Location.Y.ToString());
    }

    private async void BtnSolo_Click(object sender, EventArgs e)
    {
        btnSolo.Enabled = false;
        btnSolo.Text = "WAIT";
        try
        {
            await mainForm.TriggerSoloSessionFromOverlay();
        }
        catch {}
        btnSolo.Enabled = true;
        btnSolo.Text = I18n.T("Btn_Solo", mainForm.GetSuspendDuration());
    }

    private void BtnBlock_Click(object sender, EventArgs e)
    {
        mainForm.InvokeBlock();
    }

    private void BtnAllow_Click(object sender, EventArgs e)
    {
        mainForm.InvokeAllow();
    }

    public void UpdateInfo(int peers, bool isBlocked)
    {
        this.peerCount = peers;
        this.netBlocked = isBlocked;
        UpdatePeersAndStatusText();
    }

    private void UpdatePeersAndStatusText()
    {
        if (lblPeers.InvokeRequired)
        {
            this.Invoke(new Action(UpdatePeersAndStatusText));
            return;
        }

        lblPeers.Text = string.Format("Peers: {0}", peerCount);
        if (netBlocked)
        {
            lblPeers.ForeColor = Theme.WastedRed;
        }
        else
        {
            lblPeers.ForeColor = Color.White;
        }
    }

    private async void MetricsTimer_Tick(object sender, EventArgs e)
    {
        // 1. CPU Usage
        float cpu = SystemMetrics.GetCpuUsage();
        lblCpu.Text = string.Format("CPU: {0:0}%", cpu);
        
        // 2. RAM Usage
        float ram = SystemMetrics.GetRamUsagePercent();
        lblRam.Text = string.Format("RAM: {0:0}%", ram);

        // 3. GPU Info
        float gpuLoad = 0, gpuTemp = 0;
        string gpuStr = SystemMetrics.GetGpuInfo(out gpuLoad, out gpuTemp);
        if (gpuStr == "N/A")
        {
            lblGpu.Text = "GPU: N/A";
        }
        else
        {
            lblGpu.Text = string.Format("GPU: {0:0}% {1:0}°C", gpuLoad, gpuTemp);
        }

        // 4. FPS (Show N/A to be anti-cheat safe)
        lblFps.Text = "FPS: N/A";
        lblFps.ForeColor = Color.Gray;

        // 5. Ping Test (every 10 ticks = 10 seconds)
        pingTicker++;
        if (pingTicker >= 10 || lastPing == null)
        {
            pingTicker = 0;
            try
            {
                // Retrieve adapter IP and ping Rockstar
                var localIp = mainForm.GetSelectedAdapterIp();
                if (!string.IsNullOrEmpty(localIp))
                {
                    lastPing = await NetworkTools.PingHostAsync("prs-gta5.ros.rockstargames.com", localIp);
                }
            }
            catch {}
        }

        if (lastPing.HasValue)
        {
            lblPing.Text = string.Format("Ping: {0}ms", lastPing.Value);
            lblPing.ForeColor = Theme.MoneyGreen;
        }
        else
        {
            lblPing.Text = "Ping: Timeout";
            lblPing.ForeColor = Color.Gray;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (metricsTimer != null)
            {
                metricsTimer.Stop();
                metricsTimer.Dispose();
            }
        }
        base.Dispose(disposing);
    }
}
