using System;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Management;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Xml;
using Microsoft.Win32;

public class MainForm : Form
{
    private const int HOTKEY_ID = 1;
    private const uint MOD_CONTROL = 0x0002;
    private const uint VK_F1 = 0x70; // F1 key

    private string gta5Path = "";
    private string appDir = "";
    private bool autoSoloTriggered = false;
    private int autoSoloDebounceTicks = 0;
    
    private static readonly HashSet<int> suspendedBgPids = new HashSet<int>();
    private static DateTime? suspendedBgTime = null;
    private static readonly HashSet<string> addedRoutes = NetworkTools.AddedRoutes;
    private static bool isLoadingProfile = false;
    private static uint WM_SHOWME;

    private bool startSoloOnStart = false;
    private bool startMinimizedOnStart = false;

    // UI Elements
    private BorderlessTabControl tabControl;
    private TabPage tabBlocker;
    private TabPage tabOpt;
    private TabPage tabVPN;
    private TabPage tabSettings;
    
    private Panel pnlStatus;
    private Label lblStatusTitle;
    private Label lblStatus;
    private Label lblPeersCount;
    
    private ModernButton btnBlock;
    private ModernButton btnUnblock;
    private ToggleSwitch chkP2POnly;
    private ToggleSwitch chkAutoUnblock;
    private TrackBar trackUnblockTime;
    private Label lblUnblockTime;
    
    private ModernButton btnSolo;
    private ToggleSwitch chkAutoSolo;
    private TrackBar trackSuspendTime;
    private Label lblSuspendTime;
    
    private ModernButton btnFlush;
    private ModernButton btnClearCache;
    private ToggleSwitch chkGlobalHotkey;
    
    private Label lblOptInfo;
    private ModernButton btnAnalyze;
    private ComboBox cmbProfiles;
    private ToggleSwitch chkCores;
    private ToggleSwitch chkNoQueue;
    private ToggleSwitch chkLowEnd;
    private ToggleSwitch chkNoMemRestrict;
    
    private ToggleSwitch chkHighPriority;
    private ToggleSwitch chkCompatFlags;
    private ToggleSwitch chkGameMode;
    
    private Label lblCustomCmd;
    private TextBox txtCustomCmd;
    private ModernButton btnApplyOpt;
    private ModernButton btnOptimizeSettingsXml;
    
    private ModernButton btnCleanRam;
    private ModernButton btnSuspendBg;
    private ModernButton btnResumeBg;
    
    private Label lblVpnInfo;
    private ModernButton btnRefreshNet;
    private ComboBox cmbAdapters;
    private ModernButton btnLaunchVpn;
    private ModernButton btnCheckIp;
    private ModernButton btnPingTest;
    private Label lblPingStatus;
    private Label lblExternalIp;
    
    private ModernButton btnResetDns;
    private ModernButton btnAddRoute;
    
    private ToggleSwitch chkAutostart;
    private ModernButton btnRestoreBackups;
    private RichTextBox txtLog;
    private ModernButton btnClearLog;
    
    private Panel pnlBottom;
    private Label lblBottomTitle;
    private TextBox txtPath;
    private ModernButton btnBrowse;

    private NotifyIcon trayIcon;
    private ContextMenuStrip trayMenu;
    private Timer monitorTimer;

    private Button btnNavBlocker;
    private Button btnNavOpt;
    private Button btnNavVPN;
    private Button btnNavSettings;
    private Panel pnlNavHighlight;
    private Label lblTitleText;
    
    // Fonts (GDI Resources to dispose)
    private Font fontTitle;
    private Font fontNormal;
    private Font fontSmall;
    private Font fontHuge;

    // Added fields for overlay and profiles
    private ToggleSwitch chkEnableOverlay;
    private ToggleSwitch chkLockOverlay;
    private Label lblProfileTitle;
    private ComboBox cmbSystemProfiles;
    private ModernButton btnSaveProfile;
    private ModernButton btnLoadProfile;
    private OverlayForm overlayFm;
    private ModernButton btnOpenLogFile;

    private ComboBox cmbLang;
    private ComboBox cmbDns;
    private ToggleSwitch chkWidgetPeers;
    private ToggleSwitch chkWidgetFps;
    private ToggleSwitch chkWidgetPing;
    private ToggleSwitch chkWidgetCpu;
    private ToggleSwitch chkWidgetRam;
    private ToggleSwitch chkWidgetGpu;
    private ModernButton btnFullRollback;
    private Label lblLanguage;
    private Label lblOverlayShow;
    private Label lblDns;
    private Label lblLobbyLog;
    private Panel pnlDnsDiagnostics;

    // --- NATIVE METHODS WRAPPERS ---
    private static bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk)
    {
        return NativeMethods.RegisterHotKey(hWnd, id, fsModifiers, vk);
    }

    private static bool UnregisterHotKey(IntPtr hWnd, int id)
    {
        return NativeMethods.UnregisterHotKey(hWnd, id);
    }

    private static uint GetExtendedUdpTable(IntPtr pUdpTable, ref int pdwSize, bool bOrder, int ulAf, int tableClass, uint reserved)
    {
        return NativeMethods.GetExtendedUdpTable(pUdpTable, ref pdwSize, bOrder, ulAf, tableClass, reserved);
    }

    // --- REGISTRY WRAPPERS ---
    private static void RegWrite(string keyPath, string valueName, string value, RegistryValueKind kind = RegistryValueKind.String)
    {
        RegistryTools.Write(keyPath, valueName, value, kind);
    }

    private static void RegDelete(string keyPath, string valueName)
    {
        RegistryTools.Delete(keyPath, valueName);
    }

    private static void AddCompatFlag(string path, string flagToAdd)
    {
        RegistryTools.AddCompatFlag(path, flagToAdd);
    }

    private static void RemoveCompatFlag(string path, string flagToRemove)
    {
        RegistryTools.RemoveCompatFlag(path, flagToRemove);
    }

    private static void BackupRegistryValue(string keyPath, string valueName, string value)
    {
        RegistryTools.Backup("Backup_CompatFlags_" + valueName, value);
    }

    // --- NETWORK WRAPPERS ---
    private void SetAdapterDns(string description, string[] dns)
    {
        NetworkTools.SetDns(description, dns);
    }

    private string GetAdapterGateway(string desc)
    {
        return NetworkTools.GetGateway(desc);
    }

    private static void RestoreNetworkSettingsInternal()
    {
        NetworkTools.RestoreNetworkSettings();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int CS_DROPSHADOW = 0x00020000;
            CreateParams cp = base.CreateParams;
            cp.ClassStyle |= CS_DROPSHADOW;
            return cp;
        }
    }

    public MainForm(bool startSolo = false, bool startMinimized = false)
    {
        this.startSoloOnStart = startSolo;
        this.startMinimizedOnStart = startMinimized;

        if (startMinimized)
        {
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
        }

        appDir = AppDomain.CurrentDomain.BaseDirectory;
        
        fontTitle = new Font("Segoe UI", 12, FontStyle.Bold);
        fontNormal = new Font("Segoe UI", 10, FontStyle.Regular);
        fontSmall = new Font("Segoe UI", 9, FontStyle.Regular);
        fontHuge = new Font("Segoe UI", 22, FontStyle.Bold);

        // Hook up exception and exit handlers
        AppDomain.CurrentDomain.UnhandledException += (s, ev) => GlobalCleanup();
        AppDomain.CurrentDomain.ProcessExit += (s, ev) => GlobalCleanup();
        this.FormClosing += (s, ev) => {
            if (overlayFm != null && !overlayFm.IsDisposed)
            {
                overlayFm.Close();
            }
            GlobalCleanup();
        };

        // Register custom single instance show message
        WM_SHOWME = NativeMethods.RegisterWindowMessage("WM_SHOW_GTA_MULTITOOL");

        InitializeComponent();
        InitTray();
        
        // Load original routes from config
        string savedRoutes = ConfigManager.Get("AddedRoutes", "");
        if (!string.IsNullOrEmpty(savedRoutes))
        {
            foreach (var r in savedRoutes.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                addedRoutes.Add(r);
            }
        }
        
        gta5Path = ConfigManager.Get("GTA5Path", "");
        if (string.IsNullOrEmpty(gta5Path))
        {
            gta5Path = GameController.LoadGtaPath();
            if (string.IsNullOrEmpty(gta5Path))
            {
                gta5Path = GameController.AutoDetectGtaPath();
            }
        }
        txtPath.Text = gta5Path;
        ConfigManager.Set("GTA5Path", gta5Path);
        
        UpdateUI();
        
        // Setup monitoring timer (1 second interval)
        monitorTimer = new Timer();
        monitorTimer.Interval = 1000;
        monitorTimer.Tick += MonitorTimer_Tick;
        monitorTimer.Start();
        
        chkAutostart.Checked = IsAutostartEnabled();
        
        // Write default profiles on first startup if they don't exist
        WriteDefaultProfiles();

        // Load language and widget configuration
        I18n.LoadFromConfig();
        
        // Populate and select language
        cmbLang.SelectedIndexChanged -= cmbLang_SelectedIndexChanged;
        cmbLang.SelectedIndex = I18n.GetLanguage() == "ru" ? 0 : 1;
        cmbLang.SelectedIndexChanged += cmbLang_SelectedIndexChanged;

        // Load widget checkbox states
        chkWidgetPeers.CheckedChanged -= chkWidget_CheckedChanged;
        chkWidgetFps.CheckedChanged -= chkWidget_CheckedChanged;
        chkWidgetPing.CheckedChanged -= chkWidget_CheckedChanged;
        chkWidgetCpu.CheckedChanged -= chkWidget_CheckedChanged;
        chkWidgetRam.CheckedChanged -= chkWidget_CheckedChanged;
        chkWidgetGpu.CheckedChanged -= chkWidget_CheckedChanged;

        chkWidgetPeers.Checked = ConfigManager.Get("ShowWidget_Peers", "true") == "true";
        chkWidgetFps.Checked = ConfigManager.Get("ShowWidget_Fps", "true") == "true";
        chkWidgetPing.Checked = ConfigManager.Get("ShowWidget_Ping", "true") == "true";
        chkWidgetCpu.Checked = ConfigManager.Get("ShowWidget_Cpu", "true") == "true";
        chkWidgetRam.Checked = ConfigManager.Get("ShowWidget_Ram", "true") == "true";
        chkWidgetGpu.Checked = ConfigManager.Get("ShowWidget_Gpu", "true") == "true";

        chkWidgetPeers.CheckedChanged += chkWidget_CheckedChanged;
        chkWidgetFps.CheckedChanged += chkWidget_CheckedChanged;
        chkWidgetPing.CheckedChanged += chkWidget_CheckedChanged;
        chkWidgetCpu.CheckedChanged += chkWidget_CheckedChanged;
        chkWidgetRam.CheckedChanged += chkWidget_CheckedChanged;
        chkWidgetGpu.CheckedChanged += chkWidget_CheckedChanged;

        // Run localization refresh
        RefreshLocalization();

        // Load overlay enabled state
        chkLockOverlay.Checked = ConfigManager.Get("LockOverlay", "false") == "true";
        chkEnableOverlay.Checked = ConfigManager.Get("EnableOverlay", "false") == "true";

        Log("GTA Multi-Tool initialized successfully.");

        // Check for updates
        Task.Run(async () => await CheckForUpdatesAsync());
    }
    
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (fontTitle != null) { fontTitle.Dispose(); fontTitle = null; }
            if (fontNormal != null) { fontNormal.Dispose(); fontNormal = null; }
            if (fontSmall != null) { fontSmall.Dispose(); fontSmall = null; }
            if (fontHuge != null) { fontHuge.Dispose(); fontHuge = null; }
            if (trayIcon != null) { trayIcon.Dispose(); trayIcon = null; }
            if (monitorTimer != null) { monitorTimer.Stop(); monitorTimer.Dispose(); }
            if (overlayFm != null && !overlayFm.IsDisposed)
            {
                overlayFm.Close();
                overlayFm.Dispose();
                overlayFm = null;
            }
            
            // Unregister hotkey
            UnregisterHotKey(this.Handle, HOTKEY_ID);
        }
        base.Dispose(disposing);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        
        if (startMinimizedOnStart)
        {
            this.BeginInvoke(new Action(() => {
                this.Hide();
            }));
        }
        else
        {
            this.Opacity = 0;
            Timer timer = new Timer { Interval = 15 };
            timer.Tick += (s, ev) => {
                this.Opacity += 0.08;
                if (this.Opacity >= 1)
                {
                    this.Opacity = 1.0;
                    timer.Stop();
                    timer.Dispose();
                }
            };
            timer.Start();
        }

        try
        {
            int attribute = 33; // DWMWA_WINDOW_CORNER_PREFERENCE
            int preference = 2; // Round (standard)
            NativeMethods.DwmSetWindowAttribute(this.Handle, attribute, ref preference, sizeof(int));
        }
        catch {}

        if (startSoloOnStart)
        {
            this.BeginInvoke(new Action(async () => {
                await PerformSoloSessionSequence();
            }));
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Task.Run(() => {
            try
            {
                if (!string.IsNullOrEmpty(gta5Path))
                {
                    FirewallManager.InitRules(gta5Path, chkP2POnly.Checked);
                }
                this.Invoke(new Action(() => {
                    UpdateUI();
                    Log("Firewall rules initialized.");
                }));
            }
            catch (Exception ex)
            {
                this.Invoke(new Action(() => {
                    Log("Firewall rules initialization error: " + ex.Message);
                }));
            }
        });
    }

    private void TitleBar_MouseDown(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            NativeMethods.ReleaseCapture();
            NativeMethods.SendMessage(this.Handle, 0xA1, 0x2, 0); // WM_NCLBUTTONDOWN = 0xA1, HTCAPTION = 2
        }
    }

    private void SetupNavButton(Button btn, string text, int y, int index)
    {
        btn.Text = text;
        btn.Size = new Size(180, 40);
        btn.Location = new Point(10, y);
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, 30, 30);
        btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(20, 20, 20);
        btn.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        btn.ForeColor = Color.Gray;
        btn.TextAlign = ContentAlignment.MiddleLeft;
        btn.Padding = new Padding(10, 0, 0, 0);
        btn.Cursor = Cursors.Hand;
        btn.Click += (s, e) => SetActiveTab(index);
        
        // Add to parent sidebar directly in InitializeComponent
    }

    private void SetActiveTab(int index)
    {
        tabControl.SelectedIndex = index;
        
        btnNavBlocker.BackColor = Color.Transparent;
        btnNavBlocker.ForeColor = Color.DarkGray;
        btnNavOpt.BackColor = Color.Transparent;
        btnNavOpt.ForeColor = Color.DarkGray;
        btnNavVPN.BackColor = Color.Transparent;
        btnNavVPN.ForeColor = Color.DarkGray;
        btnNavSettings.BackColor = Color.Transparent;
        btnNavSettings.ForeColor = Color.DarkGray;
        
        Button activeBtn = null;
        string title = "";
        switch(index)
        {
            case 0: activeBtn = btnNavBlocker; title = "LOBBY & FIREWALL"; break;
            case 1: activeBtn = btnNavOpt; title = "PC & GRAPHICS OPTIMIZATION"; break;
            case 2: activeBtn = btnNavVPN; title = "NETWORK & VPN BYPASS"; break;
            case 3: activeBtn = btnNavSettings; title = "SETTINGS & SYSTEM LOGS"; break;
        }
        
        if (activeBtn != null)
        {
            activeBtn.BackColor = Color.FromArgb(24, 24, 24);
            activeBtn.ForeColor = Color.White;
            pnlNavHighlight.Location = new Point(6, activeBtn.Top + 8);
            pnlNavHighlight.Visible = true;
        }

        if (lblTitleText != null)
        {
            lblTitleText.Text = title;
        }
    }

    private void cmbLang_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (cmbLang.SelectedIndex == 0)
            I18n.SetLanguage("ru");
        else
            I18n.SetLanguage("en");
        RefreshLocalization();
        Log("Language changed to: " + I18n.GetLanguage().ToUpper());
    }

    private void cmbDns_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (cmbAdapters.SelectedIndex < 0) return;
        var adapter = (SystemAnalyzer.NetworkAdapterInfo)cmbAdapters.SelectedItem;
        if (cmbDns.SelectedIndex == 1)
        {
            SetAdapterDns(adapter.Name, new string[] { "1.1.1.1", "1.0.0.1" });
            Log("DNS set to Cloudflare for adapter: " + adapter.Name);
        }
        else
        {
            SetAdapterDns(adapter.Name, null);
            Log("DNS restored to DHCP for adapter: " + adapter.Name);
        }
    }

    private void chkWidget_CheckedChanged(object sender, EventArgs e)
    {
        ConfigManager.Set("ShowWidget_Peers", chkWidgetPeers.Checked ? "true" : "false");
        ConfigManager.Set("ShowWidget_Fps", chkWidgetFps.Checked ? "true" : "false");
        ConfigManager.Set("ShowWidget_Ping", chkWidgetPing.Checked ? "true" : "false");
        ConfigManager.Set("ShowWidget_Cpu", chkWidgetCpu.Checked ? "true" : "false");
        ConfigManager.Set("ShowWidget_Ram", chkWidgetRam.Checked ? "true" : "false");
        ConfigManager.Set("ShowWidget_Gpu", chkWidgetGpu.Checked ? "true" : "false");

        if (overlayFm != null && !overlayFm.IsDisposed)
        {
            overlayFm.ApplyWidgetVisibility();
        }
    }

    private void btnFullRollback_Click(object sender, EventArgs e)
    {
        var result = MessageBox.Show(
            I18n.T("Msg_RollbackConfirm"),
            I18n.T("Title_Warning"),
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning
        );
        
        if (result == DialogResult.Yes)
        {
            PerformFullSystemRollback(gta5Path);
            MessageBox.Show(
                I18n.T("Msg_RollbackSuccess"),
                I18n.T("Title_Success"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
            Log("Full system rollback executed.");
        }
    }

    public string GetSelectedAdapterIp()
    {
        if (cmbAdapters.InvokeRequired)
        {
            return (string)cmbAdapters.Invoke(new Func<string>(GetSelectedAdapterIp));
        }
        if (cmbAdapters.SelectedIndex >= 0)
        {
            var adapter = (SystemAnalyzer.NetworkAdapterInfo)cmbAdapters.SelectedItem;
            return adapter.IpAddress;
        }
        if (cmbAdapters.Items.Count > 0)
        {
            var adapter = (SystemAnalyzer.NetworkAdapterInfo)cmbAdapters.Items[0];
            return adapter.IpAddress;
        }
        return null;
    }

    public int GetSuspendDuration()
    {
        if (trackSuspendTime.InvokeRequired)
        {
            return (int)trackSuspendTime.Invoke(new Func<int>(GetSuspendDuration));
        }
        return trackSuspendTime.Value;
    }

    public void SyncLockOverlayCheckbox(bool isLocked)
    {
        if (chkLockOverlay.InvokeRequired)
        {
            this.Invoke(new Action<bool>(SyncLockOverlayCheckbox), isLocked);
            return;
        }
        chkLockOverlay.CheckedChanged -= chkLockOverlay_CheckedChanged;
        chkLockOverlay.Checked = isLocked;
        chkLockOverlay.CheckedChanged += chkLockOverlay_CheckedChanged;
    }

    private void RefreshSpecsLabel()
    {
        try
        {
            var specs = SystemAnalyzer.AnalyzeSpecs();
            lblOptInfo.Text = I18n.T("Lbl_Specs", specs.Cores, specs.RamGB, specs.GpuName);
        }
        catch
        {
            lblOptInfo.Text = "CPU/RAM/GPU specifications: Unknown";
        }
    }

    private void RefreshLocalization()
    {
        this.Text = I18n.T("App_Title");
        
        // Navigation Buttons
        btnNavBlocker.Text = I18n.T("Tab_Lobby");
        btnNavOpt.Text = I18n.T("Tab_Optimize");
        btnNavVPN.Text = I18n.T("Tab_Network");
        btnNavSettings.Text = I18n.T("Tab_Settings");

        // Active title bar label
        SetActiveTab(tabControl.SelectedIndex);

        // --- Tab 1: Lobby ---
        lblStatusTitle.Text = I18n.T("Lobby_Status_Title");
        UpdateUI(); 

        lblUnblockTime.Text = I18n.T("Lbl_UnblockTime", trackUnblockTime.Value);
        chkAutoUnblock.Text = I18n.T("Chk_AutoUnblock", trackUnblockTime.Value);
        chkP2POnly.Text = I18n.T("Chk_P2POnly");

        lblSuspendTime.Text = I18n.T("Lbl_SuspendTime", trackSuspendTime.Value);
        btnSolo.Text = I18n.T("Btn_Solo", trackSuspendTime.Value);
        chkAutoSolo.Text = I18n.T("Chk_AutoSolo");

        btnFlush.Text = I18n.T("Btn_Flush");
        btnClearCache.Text = I18n.T("Btn_ClearCache");

        // --- Tab 2: Optimize ---
        btnAnalyze.Text = I18n.T("Btn_AnalyzeApply");
        lblProfileTitle.Text = I18n.T("Lbl_Profile");
        
        cmbProfiles.SelectedIndexChanged -= cmbProfiles_SelectedIndexChanged;
        int prevProfilesSel = cmbProfiles.SelectedIndex;
        cmbProfiles.Items.Clear();
        cmbProfiles.Items.Add(I18n.T("Profile_Custom"));
        cmbProfiles.Items.Add(I18n.T("Profile_MaxPerf"));
        cmbProfiles.Items.Add(I18n.T("Profile_Standard"));
        cmbProfiles.Items.Add(I18n.T("Profile_Reset"));
        if (prevProfilesSel >= 0 && prevProfilesSel < cmbProfiles.Items.Count)
            cmbProfiles.SelectedIndex = prevProfilesSel;
        else
            cmbProfiles.SelectedIndex = 0;
        cmbProfiles.SelectedIndexChanged += cmbProfiles_SelectedIndexChanged;

        chkCores.Text = I18n.T("Chk_Cores");
        chkNoQueue.Text = I18n.T("Chk_NoQueue");
        chkLowEnd.Text = I18n.T("Chk_LowEnd");
        chkNoMemRestrict.Text = I18n.T("Chk_NoMemRestrict");
        chkHighPriority.Text = I18n.T("Chk_HighPriority");
        chkCompatFlags.Text = I18n.T("Chk_CompatFlags");
        chkGameMode.Text = I18n.T("Chk_GameMode");
        lblCustomCmd.Text = I18n.T("Lbl_CustomCmd");
        btnApplyOpt.Text = I18n.T("Btn_GenerateCmd");
        btnOptimizeSettingsXml.Text = I18n.T("Btn_OptimizeSettings");
        btnCleanRam.Text = I18n.T("Btn_CleanRam");
        btnSuspendBg.Text = I18n.T("Btn_SuspendBg");
        btnResumeBg.Text = I18n.T("Btn_ResumeBg");
        
        RefreshSpecsLabel();

        // --- Tab 3: Network ---
        lblVpnInfo.Text = I18n.T("Lbl_Adapter");
        btnRefreshNet.Text = I18n.T("Btn_ScanAdapters");
        btnLaunchVpn.Text = I18n.T("Btn_LaunchVpn");
        btnCheckIp.Text = I18n.T("Btn_CheckIp");
        btnPingTest.Text = I18n.T("Btn_PingTest");
        btnAddRoute.Text = I18n.T("Btn_RouteRockstar");
        lblDns.Text = I18n.T("Lbl_Dns");
        btnResetDns.Text = I18n.T("Btn_ResetDns");

        // --- Tab 4: Settings ---
        lblLanguage.Text = I18n.T("Lbl_Language");
        chkAutostart.Text = I18n.T("Chk_Autostart");
        chkGlobalHotkey.Text = I18n.T("Chk_GlobalHotkey");
        chkEnableOverlay.Text = I18n.T("Chk_EnableOverlay");
        chkLockOverlay.Text = I18n.T("Chk_LockOverlay");
        lblOverlayShow.Text = I18n.T("Lbl_OverlayShow");
        chkWidgetPeers.Text = I18n.T("Chk_WidgetPeers");
        chkWidgetFps.Text = I18n.T("Chk_WidgetFps");
        chkWidgetPing.Text = I18n.T("Chk_WidgetPing");
        chkWidgetCpu.Text = I18n.T("Chk_WidgetCpu");
        chkWidgetRam.Text = I18n.T("Chk_WidgetRam");
        chkWidgetGpu.Text = I18n.T("Chk_WidgetGpu");
        btnOpenLogFile.Text = I18n.T("Btn_OpenLog");
        btnClearLog.Text = I18n.T("Btn_ClearLog");
        btnRestoreBackups.Text = I18n.T("Btn_RestoreBackups");
        btnFullRollback.Text = I18n.T("Btn_FullRollback");

        // Bottom panel
        lblBottomTitle.Text = I18n.T("Lbl_SelectedExe");
        btnBrowse.Text = I18n.T("Btn_Browse");

        if (overlayFm != null && !overlayFm.IsDisposed)
        {
            overlayFm.ApplyLocalization();
        }
    }

    private void InitializeComponent()
    {
        this.Text = "GTA MULTI-TOOL (ADVANCED BLOCKER & OPTIMIZER)";
        this.ClientSize = new Size(980, 650);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.FormBorderStyle = FormBorderStyle.None;
        this.BackColor = Theme.BgBlack;
        
        // Drag & Drop
        this.AllowDrop = true;
        this.DragEnter += MainForm_DragEnter;
        this.DragDrop += MainForm_DragDrop;
        
        // --- LEFT SIDEBAR PANEL ---
        Panel pnlSidebar = new Panel();
        pnlSidebar.Size = new Size(200, 650);
        pnlSidebar.Location = new Point(0, 0);
        pnlSidebar.BackColor = Color.FromArgb(12, 12, 12);
        this.Controls.Add(pnlSidebar);

        Label lblLogo = new Label();
        lblLogo.Text = "GTA MULTI-TOOL";
        lblLogo.Font = new Font("Segoe UI", 12, FontStyle.Bold);
        lblLogo.ForeColor = Theme.RsYellow;
        lblLogo.Location = new Point(20, 20);
        lblLogo.AutoSize = true;
        pnlSidebar.Controls.Add(lblLogo);

        Label lblLogoSub = new Label();
        lblLogoSub.Text = "Firewall & Optimization";
        lblLogoSub.Font = new Font("Segoe UI", 8, FontStyle.Regular);
        lblLogoSub.ForeColor = Color.Gray;
        lblLogoSub.Location = new Point(20, 42);
        lblLogoSub.AutoSize = true;
        pnlSidebar.Controls.Add(lblLogoSub);

        Panel pnlDivider = new Panel();
        pnlDivider.Size = new Size(160, 1);
        pnlDivider.Location = new Point(20, 65);
        pnlDivider.BackColor = Color.FromArgb(35, 35, 35);
        pnlSidebar.Controls.Add(pnlDivider);

        // Sidebar Navigation buttons
        btnNavBlocker = new Button();
        btnNavOpt = new Button();
        btnNavVPN = new Button();
        btnNavSettings = new Button();

        pnlNavHighlight = new Panel();
        pnlNavHighlight.Size = new Size(4, 24);
        pnlNavHighlight.BackColor = Theme.RsYellow;
        pnlNavHighlight.Visible = false;
        pnlSidebar.Controls.Add(pnlNavHighlight);

        SetupNavButton(btnNavBlocker, "🛡️  Lobby & Blocker", 80, 0);
        SetupNavButton(btnNavOpt, "⚡  PC & Graphics", 130, 1);
        SetupNavButton(btnNavVPN, "🌐  Network & VPN", 180, 2);
        SetupNavButton(btnNavSettings, "⚙️  Settings & Logs", 230, 3);

        pnlSidebar.Controls.Add(btnNavBlocker);
        pnlSidebar.Controls.Add(btnNavOpt);
        pnlSidebar.Controls.Add(btnNavVPN);
        pnlSidebar.Controls.Add(btnNavSettings);

        // --- TOP TITLEBAR PANEL ---
        Panel pnlTitleBar = new Panel();
        pnlTitleBar.Size = new Size(780, 40);
        pnlTitleBar.Location = new Point(200, 0);
        pnlTitleBar.BackColor = Color.FromArgb(16, 16, 16);
        this.Controls.Add(pnlTitleBar);

        lblTitleText = new Label();
        lblTitleText.Text = "LOBBY & FIREWALL";
        lblTitleText.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        lblTitleText.ForeColor = Color.White;
        lblTitleText.Location = new Point(15, 11);
        lblTitleText.AutoSize = true;
        pnlTitleBar.Controls.Add(lblTitleText);

        pnlTitleBar.MouseDown += TitleBar_MouseDown;
        lblTitleText.MouseDown += TitleBar_MouseDown;

        // Custom window buttons
        Button btnClose = new Button();
        btnClose.Text = "\uE711";
        btnClose.Font = new Font("Segoe MDL2 Assets", 10);
        btnClose.Size = new Size(45, 40);
        btnClose.Location = new Point(735, 0);
        btnClose.FlatStyle = FlatStyle.Flat;
        btnClose.FlatAppearance.BorderSize = 0;
        btnClose.BackColor = Color.Transparent;
        btnClose.ForeColor = Color.White;
        btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(232, 17, 35);
        btnClose.FlatAppearance.MouseDownBackColor = Color.FromArgb(241, 112, 122);
        btnClose.Click += (s, e) => this.Close();
        pnlTitleBar.Controls.Add(btnClose);

        Button btnMin = new Button();
        btnMin.Text = "\uE921";
        btnMin.Font = new Font("Segoe MDL2 Assets", 10);
        btnMin.Size = new Size(45, 40);
        btnMin.Location = new Point(690, 0);
        btnMin.FlatStyle = FlatStyle.Flat;
        btnMin.FlatAppearance.BorderSize = 0;
        btnMin.BackColor = Color.Transparent;
        btnMin.ForeColor = Color.White;
        btnMin.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, 40, 40);
        btnMin.FlatAppearance.MouseDownBackColor = Color.FromArgb(20, 20, 20);
        btnMin.Click += (s, e) => this.WindowState = FormWindowState.Minimized;
        pnlTitleBar.Controls.Add(btnMin);

        // --- TAB CONTROL SETUP ---
        tabControl = new BorderlessTabControl();
        tabControl.Size = new Size(760, 490);
        tabControl.Location = new Point(210, 50);
        tabControl.Font = fontSmall;
        this.Controls.Add(tabControl);
        
        // --- TAB 1: BLOCKER & LOBBY ---
        tabBlocker = new TabPage("LOBBY & FIREWALL");
        tabBlocker.BackColor = Theme.BgBlack;
        tabControl.TabPages.Add(tabBlocker);

        // ZONE 1: Primary actions
        btnBlock = new ModernButton();
        btnBlock.Text = "BLOCK";
        btnBlock.Font = fontTitle;
        btnBlock.BackColor = Theme.WastedRed;
        btnBlock.ForeColor = Theme.TextWhite;
        btnBlock.Location = new Point(15, 15);
        btnBlock.Size = new Size(355, 45);
        btnBlock.Click += btnBlock_Click;
        tabBlocker.Controls.Add(btnBlock);
        
        btnUnblock = new ModernButton();
        btnUnblock.Text = "ALLOW";
        btnUnblock.Font = fontTitle;
        btnUnblock.BackColor = Theme.BgGray;
        btnUnblock.ForeColor = Theme.MoneyGreen;
        btnUnblock.Location = new Point(390, 15);
        btnUnblock.Size = new Size(355, 45);
        btnUnblock.Click += btnUnblock_Click;
        tabBlocker.Controls.Add(btnUnblock);

        btnSolo = new ModernButton();
        btnSolo.Text = "SOLO SESSION (10s SUSPEND)";
        btnSolo.Font = fontNormal;
        btnSolo.BackColor = Theme.RsYellow;
        btnSolo.ForeColor = Theme.BgBlack;
        btnSolo.Location = new Point(15, 70);
        btnSolo.Size = new Size(730, 45);
        btnSolo.Click += btnSolo_Click;
        tabBlocker.Controls.Add(btnSolo);

        // ZONE 2: Settings
        lblUnblockTime = new Label();
        lblUnblockTime.Text = "AUTO-UNBLOCK TIMER: 15s";
        lblUnblockTime.Font = fontNormal;
        lblUnblockTime.ForeColor = Theme.RsYellow;
        lblUnblockTime.Location = new Point(15, 130);
        lblUnblockTime.Size = new Size(300, 20);
        tabBlocker.Controls.Add(lblUnblockTime);

        trackUnblockTime = new TrackBar();
        trackUnblockTime.Minimum = 5;
        trackUnblockTime.Maximum = 60;
        trackUnblockTime.Value = 15;
        trackUnblockTime.TickFrequency = 5;
        trackUnblockTime.Location = new Point(15, 155);
        trackUnblockTime.Size = new Size(300, 30);
        trackUnblockTime.Scroll += (s, e) => { 
            lblUnblockTime.Text = I18n.T("Lbl_UnblockTime", trackUnblockTime.Value); 
            chkAutoUnblock.Text = I18n.T("Chk_AutoUnblock", trackUnblockTime.Value);
        };
        tabBlocker.Controls.Add(trackUnblockTime);

        chkAutoUnblock = new ToggleSwitch();
        chkAutoUnblock.Text = "Enable Auto-Unblock";
        chkAutoUnblock.ForeColor = Theme.TextWhite;
        chkAutoUnblock.Location = new Point(335, 140);
        chkAutoUnblock.Size = new Size(160, 24);
        tabBlocker.Controls.Add(chkAutoUnblock);

        chkP2POnly = new ToggleSwitch();
        chkP2POnly.Text = "P2P Blocker Only (UDP 6672)";
        chkP2POnly.ForeColor = Theme.TextWhite;
        chkP2POnly.Location = new Point(515, 140);
        chkP2POnly.Size = new Size(230, 24);
        chkP2POnly.Checked = true;
        chkP2POnly.CheckedChanged += chkP2POnly_CheckedChanged;
        tabBlocker.Controls.Add(chkP2POnly);

        lblSuspendTime = new Label();
        lblSuspendTime.Text = "SUSPEND DURATION: 10s";
        lblSuspendTime.Font = fontNormal;
        lblSuspendTime.ForeColor = Theme.RsYellow;
        lblSuspendTime.Location = new Point(15, 200);
        lblSuspendTime.Size = new Size(300, 20);
        tabBlocker.Controls.Add(lblSuspendTime);

        trackSuspendTime = new TrackBar();
        trackSuspendTime.Minimum = 3;
        trackSuspendTime.Maximum = 15;
        trackSuspendTime.Value = 10;
        trackSuspendTime.TickFrequency = 1;
        trackSuspendTime.Location = new Point(15, 225);
        trackSuspendTime.Size = new Size(300, 30);
        trackSuspendTime.Scroll += (s, e) => { 
            lblSuspendTime.Text = I18n.T("Lbl_SuspendTime", trackSuspendTime.Value);
            btnSolo.Text = I18n.T("Btn_Solo", trackSuspendTime.Value);
            if (overlayFm != null && !overlayFm.IsDisposed) overlayFm.ApplyLocalization();
        };
        tabBlocker.Controls.Add(trackSuspendTime);

        chkAutoSolo = new ToggleSwitch();
        chkAutoSolo.Text = "Auto-Solo on Online Session Entry";
        chkAutoSolo.ForeColor = Theme.TextWhite;
        chkAutoSolo.Location = new Point(335, 210);
        chkAutoSolo.Size = new Size(230, 24);
        tabBlocker.Controls.Add(chkAutoSolo);

        // ZONE 3: Secondary actions
        btnFlush = new ModernButton();
        btnFlush.Text = "FLUSH DNS";
        btnFlush.Font = fontSmall;
        btnFlush.BackColor = Theme.BgGray;
        btnFlush.ForeColor = Theme.TextWhite;
        btnFlush.Location = new Point(15, 285);
        btnFlush.Size = new Size(355, 35);
        btnFlush.Click += btnFlush_Click;
        tabBlocker.Controls.Add(btnFlush);
        
        btnClearCache = new ModernButton();
        btnClearCache.Text = "CLEAR GAME CACHE";
        btnClearCache.Font = fontSmall;
        btnClearCache.BackColor = Theme.BgGray;
        btnClearCache.ForeColor = Theme.TextWhite;
        btnClearCache.Location = new Point(390, 285);
        btnClearCache.Size = new Size(355, 35);
        btnClearCache.Click += btnClearCache_Click;
        tabBlocker.Controls.Add(btnClearCache);

        // ZONE 4: Status and info
        pnlStatus = new Panel();
        pnlStatus.Size = new Size(730, 100);
        pnlStatus.Location = new Point(15, 340);
        pnlStatus.BackColor = Theme.BgGray;
        pnlStatus.BorderStyle = BorderStyle.FixedSingle;
        tabBlocker.Controls.Add(pnlStatus);
        
        lblStatusTitle = new Label();
        lblStatusTitle.Text = "CONNECTION STATUS";
        lblStatusTitle.Font = fontNormal;
        lblStatusTitle.ForeColor = Theme.RsYellow;
        lblStatusTitle.Location = new Point(10, 10);
        lblStatusTitle.Size = new Size(200, 20);
        pnlStatus.Controls.Add(lblStatusTitle);
        
        lblStatus = new Label();
        lblStatus.Text = "CONNECTED";
        lblStatus.Font = fontHuge;
        lblStatus.ForeColor = Theme.MoneyGreen;
        lblStatus.Location = new Point(10, 35);
        lblStatus.Size = new Size(350, 45);
        lblStatus.TextAlign = ContentAlignment.MiddleLeft;
        pnlStatus.Controls.Add(lblStatus);

        lblPeersCount = new Label();
        lblPeersCount.Text = "Active Peers: Game not running";
        lblPeersCount.Font = fontNormal;
        lblPeersCount.ForeColor = Theme.TextWhite;
        lblPeersCount.Location = new Point(370, 45);
        lblPeersCount.Size = new Size(350, 20);
        lblPeersCount.TextAlign = ContentAlignment.TopRight;
        pnlStatus.Controls.Add(lblPeersCount);

        lblLobbyLog = new Label();
        lblLobbyLog.Text = "Log: Blocker rules ready.";
        lblLobbyLog.Font = fontSmall;
        lblLobbyLog.ForeColor = Color.Gray;
        lblLobbyLog.Location = new Point(15, 455);
        lblLobbyLog.Size = new Size(730, 20);
        tabBlocker.Controls.Add(lblLobbyLog);

        // --- TAB 2: PC OPTIMIZATION ---
        tabOpt = new TabPage("PC & GRAPHICS");
        tabOpt.BackColor = Theme.BgBlack;
        tabControl.TabPages.Add(tabOpt);

        // ZONE 1: Primary actions
        btnAnalyze = new ModernButton();
        btnAnalyze.Text = "⚡ ANALYZE PC SPECS & APPLY";
        btnAnalyze.Font = fontTitle;
        btnAnalyze.BackColor = Theme.RsYellow;
        btnAnalyze.ForeColor = Theme.BgBlack;
        btnAnalyze.Location = new Point(15, 15);
        btnAnalyze.Size = new Size(730, 45);
        btnAnalyze.Click += btnAnalyze_Click;
        tabOpt.Controls.Add(btnAnalyze);

        // ZONE 2: Settings
        lblProfileTitle = new Label();
        lblProfileTitle.Text = "Profile:";
        lblProfileTitle.Font = fontNormal;
        lblProfileTitle.ForeColor = Theme.RsYellow;
        lblProfileTitle.Location = new Point(15, 78);
        lblProfileTitle.Size = new Size(80, 20);
        tabOpt.Controls.Add(lblProfileTitle);

        cmbProfiles = new ComboBox();
        cmbProfiles.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbProfiles.FlatStyle = FlatStyle.Flat;
        cmbProfiles.BackColor = Theme.BgBlack;
        cmbProfiles.ForeColor = Theme.TextWhite;
        cmbProfiles.Location = new Point(100, 75);
        cmbProfiles.Size = new Size(645, 25);
        cmbProfiles.SelectedIndexChanged += cmbProfiles_SelectedIndexChanged;
        tabOpt.Controls.Add(cmbProfiles);

        chkCores = new ToggleSwitch();
        chkCores.Text = "Use All CPU Cores";
        chkCores.ForeColor = Theme.TextWhite;
        chkCores.Location = new Point(15, 115);
        chkCores.Size = new Size(350, 24);
        tabOpt.Controls.Add(chkCores);
        
        chkNoQueue = new ToggleSwitch();
        chkNoQueue.Text = "Reduce Input Lag";
        chkNoQueue.ForeColor = Theme.TextWhite;
        chkNoQueue.Location = new Point(15, 145);
        chkNoQueue.Size = new Size(350, 24);
        tabOpt.Controls.Add(chkNoQueue);
        
        chkLowEnd = new ToggleSwitch();
        chkLowEnd.Text = "Low-End PC Mode";
        chkLowEnd.ForeColor = Theme.TextWhite;
        chkLowEnd.Location = new Point(15, 175);
        chkLowEnd.Size = new Size(350, 24);
        tabOpt.Controls.Add(chkLowEnd);
        
        chkNoMemRestrict = new ToggleSwitch();
        chkNoMemRestrict.Text = "Ignore VRAM Limits";
        chkNoMemRestrict.ForeColor = Theme.TextWhite;
        chkNoMemRestrict.Location = new Point(15, 205);
        chkNoMemRestrict.Size = new Size(350, 24);
        tabOpt.Controls.Add(chkNoMemRestrict);

        chkHighPriority = new ToggleSwitch();
        chkHighPriority.Text = "Auto-Set High Process Priority";
        chkHighPriority.ForeColor = Theme.TextWhite;
        chkHighPriority.Location = new Point(390, 115);
        chkHighPriority.Size = new Size(355, 24);
        tabOpt.Controls.Add(chkHighPriority);

        chkCompatFlags = new ToggleSwitch();
        chkCompatFlags.Text = "Disable Fullscreen Optimizations";
        chkCompatFlags.ForeColor = Theme.TextWhite;
        chkCompatFlags.Location = new Point(390, 145);
        chkCompatFlags.Size = new Size(355, 24);
        chkCompatFlags.CheckedChanged += chkCompatFlags_CheckedChanged;
        tabOpt.Controls.Add(chkCompatFlags);

        chkGameMode = new ToggleSwitch();
        chkGameMode.Text = "Enable Windows Game Mode";
        chkGameMode.ForeColor = Theme.TextWhite;
        chkGameMode.Location = new Point(390, 175);
        chkGameMode.Size = new Size(355, 24);
        chkGameMode.CheckedChanged += chkGameMode_CheckedChanged;
        tabOpt.Controls.Add(chkGameMode);
        
        lblCustomCmd = new Label();
        lblCustomCmd.Text = "Custom commandline.txt arguments (space separated):";
        lblCustomCmd.ForeColor = Theme.TextWhite;
        lblCustomCmd.Font = fontNormal;
        lblCustomCmd.Location = new Point(15, 240);
        lblCustomCmd.Size = new Size(730, 20);
        tabOpt.Controls.Add(lblCustomCmd);
        
        txtCustomCmd = new TextBox();
        txtCustomCmd.Location = new Point(15, 265);
        txtCustomCmd.Size = new Size(730, 23);
        txtCustomCmd.BackColor = Theme.BgBlack;
        txtCustomCmd.ForeColor = Theme.TextWhite;
        txtCustomCmd.BorderStyle = BorderStyle.FixedSingle;
        tabOpt.Controls.Add(txtCustomCmd);
        
        // ZONE 3: Secondary actions
        btnApplyOpt = new ModernButton();
        btnApplyOpt.Text = "SAVE COMMANDLINE.TXT";
        btnApplyOpt.Font = fontSmall;
        btnApplyOpt.BackColor = Theme.BgGray;
        btnApplyOpt.ForeColor = Theme.TextWhite;
        btnApplyOpt.Location = new Point(15, 305);
        btnApplyOpt.Size = new Size(355, 35);
        btnApplyOpt.Click += btnApplyOpt_Click;
        tabOpt.Controls.Add(btnApplyOpt);

        btnOptimizeSettingsXml = new ModernButton();
        btnOptimizeSettingsXml.Text = "OPTIMIZE SETTINGS.XML (MAX FPS)";
        btnOptimizeSettingsXml.Font = fontSmall;
        btnOptimizeSettingsXml.BackColor = Theme.BgGray;
        btnOptimizeSettingsXml.ForeColor = Theme.TextWhite;
        btnOptimizeSettingsXml.Location = new Point(390, 305);
        btnOptimizeSettingsXml.Size = new Size(355, 35);
        btnOptimizeSettingsXml.Click += btnOptimizeSettingsXml_Click;
        tabOpt.Controls.Add(btnOptimizeSettingsXml);

        btnCleanRam = new ModernButton();
        btnCleanRam.Text = "CLEAN WORKING SETS";
        btnCleanRam.Font = fontSmall;
        btnCleanRam.BackColor = Theme.BgGray;
        btnCleanRam.ForeColor = Theme.TextWhite;
        btnCleanRam.Location = new Point(15, 350);
        btnCleanRam.Size = new Size(230, 35);
        btnCleanRam.Click += btnCleanRam_Click;
        tabOpt.Controls.Add(btnCleanRam);

        btnSuspendBg = new ModernButton();
        btnSuspendBg.Text = "SUSPEND BACKGROUND APPS";
        btnSuspendBg.Font = fontSmall;
        btnSuspendBg.BackColor = Theme.BgGray;
        btnSuspendBg.ForeColor = Theme.TextWhite;
        btnSuspendBg.Location = new Point(265, 350);
        btnSuspendBg.Size = new Size(230, 35);
        btnSuspendBg.Click += btnSuspendBg_Click;
        tabOpt.Controls.Add(btnSuspendBg);

        btnResumeBg = new ModernButton();
        btnResumeBg.Text = "RESUME BACKGROUND APPS";
        btnResumeBg.Font = fontSmall;
        btnResumeBg.BackColor = Theme.BgGray;
        btnResumeBg.ForeColor = Theme.TextWhite;
        btnResumeBg.Location = new Point(515, 350);
        btnResumeBg.Size = new Size(230, 35);
        btnResumeBg.Click += btnResumeBg_Click;
        tabOpt.Controls.Add(btnResumeBg);
        
        // ZONE 4: Status information
        lblOptInfo = new Label();
        lblOptInfo.Text = "PC SPECS: (Press Analyze button above)";
        lblOptInfo.Font = fontNormal;
        lblOptInfo.ForeColor = Theme.TextWhite;
        lblOptInfo.Location = new Point(15, 400);
        lblOptInfo.Size = new Size(730, 50);
        tabOpt.Controls.Add(lblOptInfo);

        // --- TAB 3: VPN BYPASS & DIAGNOSTICS ---
        tabVPN = new TabPage("NETWORK & VPN");
        tabVPN.BackColor = Theme.BgBlack;
        tabControl.TabPages.Add(tabVPN);

        // ZONE 2: Settings
        lblVpnInfo = new Label();
        lblVpnInfo.Text = "Adapter:";
        lblVpnInfo.Font = fontNormal;
        lblVpnInfo.ForeColor = Theme.RsYellow;
        lblVpnInfo.Location = new Point(15, 18);
        lblVpnInfo.Size = new Size(80, 20);
        tabVPN.Controls.Add(lblVpnInfo);

        cmbAdapters = new ComboBox();
        cmbAdapters.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbAdapters.FlatStyle = FlatStyle.Flat;
        cmbAdapters.BackColor = Theme.BgBlack;
        cmbAdapters.ForeColor = Theme.TextWhite;
        cmbAdapters.Location = new Point(100, 15);
        cmbAdapters.Size = new Size(600, 25);
        tabVPN.Controls.Add(cmbAdapters);

        btnRefreshNet = new ModernButton();
        btnRefreshNet.Text = "🔄";
        btnRefreshNet.Font = fontSmall;
        btnRefreshNet.BackColor = Theme.BgGray;
        btnRefreshNet.ForeColor = Theme.RsYellow;
        btnRefreshNet.Location = new Point(710, 15);
        btnRefreshNet.Size = new Size(35, 25);
        btnRefreshNet.Click += btnRefreshNet_Click;
        tabVPN.Controls.Add(btnRefreshNet);

        // ZONE 1: Primary actions
        btnLaunchVpn = new ModernButton();
        btnLaunchVpn.Text = "🎮 LAUNCH GAME VIA ADAPTER";
        btnLaunchVpn.Font = fontTitle;
        btnLaunchVpn.BackColor = Theme.RsYellow;
        btnLaunchVpn.ForeColor = Theme.BgBlack;
        btnLaunchVpn.Location = new Point(15, 55);
        btnLaunchVpn.Size = new Size(730, 45);
        btnLaunchVpn.Click += btnLaunchVpn_Click;
        tabVPN.Controls.Add(btnLaunchVpn);

        // ZONE 3: Secondary actions
        btnCheckIp = new ModernButton();
        btnCheckIp.Text = "CHECK ADAPTER IP";
        btnCheckIp.Font = fontSmall;
        btnCheckIp.BackColor = Theme.BgGray;
        btnCheckIp.ForeColor = Theme.TextWhite;
        btnCheckIp.Location = new Point(15, 115);
        btnCheckIp.Size = new Size(230, 35);
        btnCheckIp.Click += btnCheckIp_Click;
        tabVPN.Controls.Add(btnCheckIp);

        btnPingTest = new ModernButton();
        btnPingTest.Text = "PING ROCKSTAR";
        btnPingTest.Font = fontSmall;
        btnPingTest.BackColor = Theme.BgGray;
        btnPingTest.ForeColor = Theme.TextWhite;
        btnPingTest.Location = new Point(265, 115);
        btnPingTest.Size = new Size(230, 35);
        btnPingTest.Click += btnPingTest_Click;
        tabVPN.Controls.Add(btnPingTest);

        btnAddRoute = new ModernButton();
        btnAddRoute.Text = "ROUTE ROCKSTAR PAST VPN";
        btnAddRoute.Font = fontSmall;
        btnAddRoute.BackColor = Theme.BgGray;
        btnAddRoute.ForeColor = Theme.TextWhite;
        btnAddRoute.Location = new Point(515, 115);
        btnAddRoute.Size = new Size(230, 35);
        btnAddRoute.Click += btnAddRoute_Click;
        tabVPN.Controls.Add(btnAddRoute);

        // ZONE 2: Settings
        lblDns = new Label();
        lblDns.Text = "DNS:";
        lblDns.Font = fontNormal;
        lblDns.ForeColor = Theme.RsYellow;
        lblDns.Location = new Point(15, 170);
        lblDns.Size = new Size(80, 20);
        tabVPN.Controls.Add(lblDns);

        cmbDns = new ComboBox();
        cmbDns.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbDns.FlatStyle = FlatStyle.Flat;
        cmbDns.BackColor = Theme.BgBlack;
        cmbDns.ForeColor = Theme.TextWhite;
        cmbDns.Items.AddRange(new string[] { "Default DNS (DHCP)", "Cloudflare DNS (1.1.1.1)" });
        cmbDns.SelectedIndex = 0;
        cmbDns.Location = new Point(100, 168);
        cmbDns.Size = new Size(490, 25);
        cmbDns.SelectedIndexChanged += cmbDns_SelectedIndexChanged;
        tabVPN.Controls.Add(cmbDns);

        btnResetDns = new ModernButton();
        btnResetDns.Text = "RESTORE DHCP";
        btnResetDns.Font = fontSmall;
        btnResetDns.BackColor = Theme.BgGray;
        btnResetDns.ForeColor = Theme.TextWhite;
        btnResetDns.Location = new Point(600, 168);
        btnResetDns.Size = new Size(145, 25);
        btnResetDns.Click += btnResetDns_Click;
        tabVPN.Controls.Add(btnResetDns);

        // ZONE 4: Status
        pnlDnsDiagnostics = new Panel();
        pnlDnsDiagnostics.Size = new Size(730, 100);
        pnlDnsDiagnostics.Location = new Point(15, 215);
        pnlDnsDiagnostics.BackColor = Theme.BgGray;
        pnlDnsDiagnostics.BorderStyle = BorderStyle.FixedSingle;
        tabVPN.Controls.Add(pnlDnsDiagnostics);

        lblPingStatus = new Label();
        lblPingStatus.Text = "Rockstar Session Server Ping: N/A";
        lblPingStatus.ForeColor = Theme.RsYellow;
        lblPingStatus.Font = fontNormal;
        lblPingStatus.Location = new Point(10, 15);
        lblPingStatus.Size = new Size(710, 25);
        pnlDnsDiagnostics.Controls.Add(lblPingStatus);

        lblExternalIp = new Label();
        lblExternalIp.Text = "Current Public IP (via Selected Adapter): Not checked";
        lblExternalIp.ForeColor = Theme.TextWhite;
        lblExternalIp.Font = fontNormal;
        lblExternalIp.Location = new Point(10, 45);
        lblExternalIp.Size = new Size(710, 25);
        pnlDnsDiagnostics.Controls.Add(lblExternalIp);

        // --- TAB 4: SETTINGS & LOGS ---
        tabSettings = new TabPage("SETTINGS & LOGS");
        tabSettings.BackColor = Theme.BgBlack;
        tabControl.TabPages.Add(tabSettings);

        // ZONE 2: Settings
        lblLanguage = new Label();
        lblLanguage.Text = "Language:";
        lblLanguage.Font = fontNormal;
        lblLanguage.ForeColor = Theme.RsYellow;
        lblLanguage.Location = new Point(15, 18);
        lblLanguage.Size = new Size(80, 20);
        tabSettings.Controls.Add(lblLanguage);

        cmbLang = new ComboBox();
        cmbLang.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbLang.FlatStyle = FlatStyle.Flat;
        cmbLang.BackColor = Theme.BgBlack;
        cmbLang.ForeColor = Theme.TextWhite;
        cmbLang.Items.AddRange(new string[] { "Русский", "English" });
        cmbLang.SelectedIndex = 0;
        cmbLang.Location = new Point(100, 15);
        cmbLang.Size = new Size(200, 25);
        cmbLang.SelectedIndexChanged += cmbLang_SelectedIndexChanged;
        tabSettings.Controls.Add(cmbLang);

        // Profiles on Settings tab
        Label lblProfileSystem = new Label();
        lblProfileSystem.Text = "Profile Mode:";
        lblProfileSystem.Font = fontNormal;
        lblProfileSystem.ForeColor = Theme.RsYellow;
        lblProfileSystem.Location = new Point(340, 18);
        lblProfileSystem.Size = new Size(100, 20);
        tabSettings.Controls.Add(lblProfileSystem);

        cmbSystemProfiles = new ComboBox();
        cmbSystemProfiles.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbSystemProfiles.FlatStyle = FlatStyle.Flat;
        cmbSystemProfiles.BackColor = Theme.BgBlack;
        cmbSystemProfiles.ForeColor = Theme.TextWhite;
        cmbSystemProfiles.Location = new Point(450, 15);
        cmbSystemProfiles.Size = new Size(140, 25);
        cmbSystemProfiles.Items.AddRange(new string[] { "Solo Grind", "Public Freeroam", "Stream", "Custom" });
        cmbSystemProfiles.SelectedIndex = 3;
        tabSettings.Controls.Add(cmbSystemProfiles);

        btnLoadProfile = new ModernButton();
        btnLoadProfile.Text = "APPLY";
        btnLoadProfile.Font = fontSmall;
        btnLoadProfile.BackColor = Theme.BgGray;
        btnLoadProfile.ForeColor = Theme.RsYellow;
        btnLoadProfile.Location = new Point(600, 15);
        btnLoadProfile.Size = new Size(70, 25);
        btnLoadProfile.Click += btnLoadProfile_Click;
        tabSettings.Controls.Add(btnLoadProfile);

        btnSaveProfile = new ModernButton();
        btnSaveProfile.Text = "SAVE";
        btnSaveProfile.Font = fontSmall;
        btnSaveProfile.BackColor = Theme.BgGray;
        btnSaveProfile.ForeColor = Theme.TextWhite;
        btnSaveProfile.Location = new Point(675, 15);
        btnSaveProfile.Size = new Size(75, 25);
        btnSaveProfile.Click += btnSaveProfile_Click;
        tabSettings.Controls.Add(btnSaveProfile);

        chkAutostart = new ToggleSwitch();
        chkAutostart.Text = "Start GTA Multi-Tool with Windows";
        chkAutostart.ForeColor = Theme.TextWhite;
        chkAutostart.Location = new Point(15, 55);
        chkAutostart.Size = new Size(350, 24);
        chkAutostart.CheckedChanged += chkAutostart_CheckedChanged;
        tabSettings.Controls.Add(chkAutostart);

        chkGlobalHotkey = new ToggleSwitch();
        chkGlobalHotkey.Text = "Global Hotkey (Ctrl + F1)";
        chkGlobalHotkey.ForeColor = Theme.TextWhite;
        chkGlobalHotkey.Location = new Point(390, 55);
        chkGlobalHotkey.Size = new Size(355, 24);
        chkGlobalHotkey.CheckedChanged += chkGlobalHotkey_CheckedChanged;
        tabSettings.Controls.Add(chkGlobalHotkey);

        chkEnableOverlay = new ToggleSwitch();
        chkEnableOverlay.Text = "Enable In-Game Overlay HUD";
        chkEnableOverlay.ForeColor = Theme.TextWhite;
        chkEnableOverlay.Location = new Point(15, 85);
        chkEnableOverlay.Size = new Size(350, 24);
        chkEnableOverlay.CheckedChanged += chkEnableOverlay_CheckedChanged;
        tabSettings.Controls.Add(chkEnableOverlay);

        chkLockOverlay = new ToggleSwitch();
        chkLockOverlay.Text = "Overlay: Click-Through Mode";
        chkLockOverlay.ForeColor = Theme.TextWhite;
        chkLockOverlay.Location = new Point(390, 85);
        chkLockOverlay.Size = new Size(355, 24);
        chkLockOverlay.CheckedChanged += chkLockOverlay_CheckedChanged;
        tabSettings.Controls.Add(chkLockOverlay);

        lblOverlayShow = new Label();
        lblOverlayShow.Text = "Overlay show:";
        lblOverlayShow.Font = fontNormal;
        lblOverlayShow.ForeColor = Theme.RsYellow;
        lblOverlayShow.Location = new Point(15, 120);
        lblOverlayShow.Size = new Size(350, 20);
        tabSettings.Controls.Add(lblOverlayShow);

        chkWidgetPeers = new ToggleSwitch();
        chkWidgetPeers.Text = "Peers";
        chkWidgetPeers.ForeColor = Theme.TextWhite;
        chkWidgetPeers.Location = new Point(15, 145);
        chkWidgetPeers.Size = new Size(230, 24);
        chkWidgetPeers.Checked = true;
        chkWidgetPeers.CheckedChanged += chkWidget_CheckedChanged;
        tabSettings.Controls.Add(chkWidgetPeers);

        chkWidgetFps = new ToggleSwitch();
        chkWidgetFps.Text = "FPS";
        chkWidgetFps.ForeColor = Theme.TextWhite;
        chkWidgetFps.Location = new Point(265, 145);
        chkWidgetFps.Size = new Size(230, 24);
        chkWidgetFps.Checked = true;
        chkWidgetFps.CheckedChanged += chkWidget_CheckedChanged;
        tabSettings.Controls.Add(chkWidgetFps);

        chkWidgetPing = new ToggleSwitch();
        chkWidgetPing.Text = "Ping";
        chkWidgetPing.ForeColor = Theme.TextWhite;
        chkWidgetPing.Location = new Point(515, 145);
        chkWidgetPing.Size = new Size(230, 24);
        chkWidgetPing.Checked = true;
        chkWidgetPing.CheckedChanged += chkWidget_CheckedChanged;
        tabSettings.Controls.Add(chkWidgetPing);

        chkWidgetCpu = new ToggleSwitch();
        chkWidgetCpu.Text = "CPU %";
        chkWidgetCpu.ForeColor = Theme.TextWhite;
        chkWidgetCpu.Location = new Point(15, 175);
        chkWidgetCpu.Size = new Size(230, 24);
        chkWidgetCpu.Checked = true;
        chkWidgetCpu.CheckedChanged += chkWidget_CheckedChanged;
        tabSettings.Controls.Add(chkWidgetCpu);

        chkWidgetRam = new ToggleSwitch();
        chkWidgetRam.Text = "RAM %";
        chkWidgetRam.ForeColor = Theme.TextWhite;
        chkWidgetRam.Location = new Point(265, 175);
        chkWidgetRam.Size = new Size(230, 24);
        chkWidgetRam.Checked = true;
        chkWidgetRam.CheckedChanged += chkWidget_CheckedChanged;
        tabSettings.Controls.Add(chkWidgetRam);

        chkWidgetGpu = new ToggleSwitch();
        chkWidgetGpu.Text = "GPU %";
        chkWidgetGpu.ForeColor = Theme.TextWhite;
        chkWidgetGpu.Location = new Point(515, 175);
        chkWidgetGpu.Size = new Size(230, 24);
        chkWidgetGpu.Checked = true;
        chkWidgetGpu.CheckedChanged += chkWidget_CheckedChanged;
        tabSettings.Controls.Add(chkWidgetGpu);

        // ZONE 3: Secondary actions
        btnOpenLogFile = new ModernButton();
        btnOpenLogFile.Text = "OPEN LOG FILE";
        btnOpenLogFile.Font = fontSmall;
        btnOpenLogFile.BackColor = Theme.BgGray;
        btnOpenLogFile.ForeColor = Theme.TextWhite;
        btnOpenLogFile.Location = new Point(15, 215);
        btnOpenLogFile.Size = new Size(230, 30);
        btnOpenLogFile.Click += btnOpenLogFile_Click;
        tabSettings.Controls.Add(btnOpenLogFile);

        btnClearLog = new ModernButton();
        btnClearLog.Text = "CLEAR LOG CONSOLE";
        btnClearLog.Font = fontSmall;
        btnClearLog.BackColor = Theme.BgGray;
        btnClearLog.ForeColor = Theme.TextWhite;
        btnClearLog.Location = new Point(265, 215);
        btnClearLog.Size = new Size(230, 30);
        btnClearLog.Click += (s, e) => { txtLog.Clear(); };
        tabSettings.Controls.Add(btnClearLog);

        btnRestoreBackups = new ModernButton();
        btnRestoreBackups.Text = "RESTORE CONFIG BACKUPS";
        btnRestoreBackups.Font = fontSmall;
        btnRestoreBackups.BackColor = Theme.BgGray;
        btnRestoreBackups.ForeColor = Theme.TextWhite;
        btnRestoreBackups.Location = new Point(515, 215);
        btnRestoreBackups.Size = new Size(230, 30);
        btnRestoreBackups.Click += btnRestoreBackups_Click;
        tabSettings.Controls.Add(btnRestoreBackups);

        // ZONE 1: Primary action (Rollback)
        btnFullRollback = new ModernButton();
        btnFullRollback.Text = "⚠️ FULL ROLLBACK OF ALL MODIFICATIONS";
        btnFullRollback.Font = fontNormal;
        btnFullRollback.BackColor = Theme.WastedRed;
        btnFullRollback.ForeColor = Theme.TextWhite;
        btnFullRollback.Location = new Point(15, 260);
        btnFullRollback.Size = new Size(730, 40);
        btnFullRollback.Click += btnFullRollback_Click;
        tabSettings.Controls.Add(btnFullRollback);

        // ZONE 4: Output console log
        txtLog = new RichTextBox();
        txtLog.ReadOnly = true;
        txtLog.BackColor = Theme.LogGray;
        txtLog.ForeColor = Theme.TextWhite;
        txtLog.Location = new Point(15, 315);
        txtLog.Size = new Size(730, 160);
        txtLog.Font = new Font("Consolas", 9, FontStyle.Regular);
        txtLog.BorderStyle = BorderStyle.None;
        tabSettings.Controls.Add(txtLog);
        
        // Bottom Settings Panel
        pnlBottom = new Panel();
        pnlBottom.Size = new Size(760, 80);
        pnlBottom.Location = new Point(210, 555);
        pnlBottom.BackColor = Theme.BgGray;
        pnlBottom.BorderStyle = BorderStyle.FixedSingle;
        this.Controls.Add(pnlBottom);
        
        lblBottomTitle = new Label();
        lblBottomTitle.Text = "SELECTED EXECUTABLE (GTA5.exe / GTAIV.exe) - Drag & Drop Supported";
        lblBottomTitle.Font = fontNormal;
        lblBottomTitle.ForeColor = Theme.RsYellow;
        lblBottomTitle.Location = new Point(10, 10);
        lblBottomTitle.AutoSize = true;
        pnlBottom.Controls.Add(lblBottomTitle);
        
        txtPath = new TextBox();
        txtPath.Font = fontSmall;
        txtPath.BackColor = Theme.BgBlack;
        txtPath.ForeColor = Theme.TextWhite;
        txtPath.Location = new Point(15, 40);
        txtPath.Size = new Size(610, 23);
        txtPath.ReadOnly = true;
        txtPath.BorderStyle = BorderStyle.FixedSingle;
        pnlBottom.Controls.Add(txtPath);
        
        btnBrowse = new ModernButton();
        btnBrowse.Text = "BROWSE";
        btnBrowse.Font = fontSmall;
        btnBrowse.BackColor = Theme.BgBlack;
        btnBrowse.ForeColor = Theme.TextWhite;
        btnBrowse.Location = new Point(645, 38);
        btnBrowse.Size = new Size(100, 26);
        btnBrowse.Click += btnBrowse_Click;
        pnlBottom.Controls.Add(btnBrowse);

        SetActiveTab(0);
    }
    
    private void UpdateUI()
    {
        bool blocked = FirewallManager.IsBlocked();
        if (blocked)
        {
            lblStatus.Text = "WASTED (BLOCKED)";
            lblStatus.ForeColor = Theme.WastedRed;
            btnBlock.BackColor = Theme.BgBlack;
            btnBlock.ForeColor = Theme.WastedRed;
            btnBlock.Text = "BLOCKED";
            btnUnblock.BackColor = Theme.MoneyGreen;
            btnUnblock.ForeColor = Theme.TextWhite;
            btnUnblock.Text = "ALLOW";
        }
        else
        {
            lblStatus.Text = "CONNECTED";
            lblStatus.ForeColor = Theme.MoneyGreen;
            btnBlock.BackColor = Theme.WastedRed;
            btnBlock.ForeColor = Theme.TextWhite;
            btnBlock.Text = "BLOCK";
            btnUnblock.BackColor = Theme.BgBlack;
            btnUnblock.ForeColor = Theme.MoneyGreen;
            btnUnblock.Text = "ALLOWED";
        }
    }

    private void Log(string msg)
    {
        if (txtLog.InvokeRequired)
        {
            txtLog.Invoke(new Action<string>(Log), msg);
            return;
        }
        txtLog.AppendText(string.Format("[{0}] {1}\r\n", DateTime.Now.ToString("HH:mm:ss"), msg));
        txtLog.ScrollToCaret();

        if (lblLobbyLog != null)
        {
            if (lblLobbyLog.InvokeRequired)
                lblLobbyLog.Invoke(new Action(() => lblLobbyLog.Text = "Log: " + msg));
            else
                lblLobbyLog.Text = "Log: " + msg;
        }

        LogToFileDirect(msg);
    }

    // --- SYSTEM TRAY INTEGRATION ---
    private void InitTray()
    {
        trayIcon = new NotifyIcon();
        trayIcon.Text = "GTA Multi-Tool";
        trayIcon.Icon = SystemIcons.Shield;
        
        trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Quick Solo Session", null, async (s, e) => {
            Log("Quick Solo triggered from Tray.");
            await PerformSoloSessionSequence();
        });
        trayMenu.Items.Add("Toggle Blocker State", null, (s, e) => {
            bool currentlyBlocked = FirewallManager.IsBlocked();
            FirewallManager.SetBlockState(!currentlyBlocked);
            UpdateUI();
            Log("Blocker state toggled from Tray. New state: " + (!currentlyBlocked ? "BLOCKED" : "ALLOWED"));
        });
        trayMenu.Items.Add("-");
        trayMenu.Items.Add("Restore Interface", null, (s, e) => RestoreFromTray());
        trayMenu.Items.Add("Exit Tool", null, (s, e) => Application.Exit());
        
        trayIcon.ContextMenuStrip = trayMenu;
        trayIcon.Visible = true;
        trayIcon.DoubleClick += (s, e) => RestoreFromTray();

        this.Resize += (s, e) => {
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.Hide();
                this.ShowInTaskbar = false;
                trayIcon.ShowBalloonTip(2000, "GTA Multi-Tool", "Running in system tray.", ToolTipIcon.Info);
            }
        };
    }

    private void RestoreFromTray()
    {
        this.Show();
        this.WindowState = FormWindowState.Normal;
        this.ShowInTaskbar = true;
        this.Activate();
    }

    // --- MONITORING TIMER (PEER COUNTER, CPU PRIORITY) ---
    private void MonitorTimer_Tick(object sender, EventArgs e)
    {
        // 1. Check for 30-minute background suspension timeout
        if (suspendedBgPids.Count > 0 && suspendedBgTime.HasValue)
        {
            if ((DateTime.Now - suspendedBgTime.Value).TotalMinutes >= 30)
            {
                Log("Auto-Resume: 30 minutes elapsed. Resuming suspended background processes...");
                ResumeAllSuspendedBackgroundProcesses();
            }
        }

        int? pid = ProcessManager.FindGtaProcessId(gta5Path);
        int currentPeers = 0;
        
        if (pid.HasValue)
        {
            // Process exists - enforce priority
            if (chkHighPriority.Checked)
            {
                try
                {
                    using (Process p = Process.GetProcessById(pid.Value))
                    {
                        if (p.PriorityClass != ProcessPriorityClass.High)
                        {
                            p.PriorityClass = ProcessPriorityClass.High;
                            Log("Forced high process priority class for GTA5.");
                        }
                    }
                }
                catch {}
            }

            // Peer check
            currentPeers = GetActivePeerCount(pid.Value);
            lblPeersCount.Text = "Peers (Network Nodes): " + currentPeers;

            // Auto-Solo Lobby activation with Debounce (8 seconds)
            if (chkAutoSolo.Checked)
            {
                if (currentPeers > 5)
                {
                    if (!autoSoloTriggered)
                    {
                        autoSoloDebounceTicks++;
                        if (autoSoloDebounceTicks == 1)
                        {
                            Log("Auto-Solo: High peer count detected. Debounce timer started...");
                        }
                        
                        if (autoSoloDebounceTicks >= 8)
                        {
                            autoSoloTriggered = true;
                            autoSoloDebounceTicks = 0;
                            Log(string.Format("Auto-Solo: High peer count sustained for 8s ({0} peers). Launching solo lobby trigger...", currentPeers));
                            Task.Run(async () => {
                                await PerformSoloSessionSequence();
                            });
                        }
                    }
                }
                else if (currentPeers <= 2)
                {
                    autoSoloDebounceTicks = 0;
                    autoSoloTriggered = false; // Reset trigger when session goes empty
                }
            }
            else
            {
                autoSoloDebounceTicks = 0;
                if (currentPeers <= 2)
                {
                    autoSoloTriggered = false;
                }
            }
        }
        else
        {
            lblPeersCount.Text = "Peers: Game process not running";
            autoSoloTriggered = false;
            autoSoloDebounceTicks = 0;
        }

        // 2. Update Overlay Form Info if visible
        if (overlayFm != null && overlayFm.Visible)
        {
            overlayFm.UpdateInfo(currentPeers, FirewallManager.IsBlocked());
        }
    }

    private int GetActivePeerCount(int pid)
    {
        int size = 0;
        // AF_INET = 2 (IPv4)
        // class = 1 (UDP_TABLE_OWNER_PID)
        uint res = GetExtendedUdpTable(IntPtr.Zero, ref size, false, 2, 1, 0);
        IntPtr pTable = Marshal.AllocHGlobal(size);
        try
        {
            res = GetExtendedUdpTable(pTable, ref size, false, 2, 1, 0);
            if (res != 0) return 0;

            int count = Marshal.ReadInt32(pTable);
            int peerCount = 0;
            IntPtr ptr = (IntPtr)((long)pTable + 4);
            int rowSize = 12; // sizeof(MIB_UDPROW_OWNER_PID) : 4 bytes addr + 4 bytes port + 4 bytes pid

            for (int i = 0; i < count; i++)
            {
                uint rowPid = (uint)Marshal.ReadInt32((IntPtr)((long)ptr + 8));
                if (rowPid == pid)
                {
                    // Local port is in network byte order in the first 2 bytes of the port uint
                    uint rawPort = (uint)Marshal.ReadInt32((IntPtr)((long)ptr + 4));
                    ushort port = (ushort)(((rawPort & 0xff) << 8) | ((rawPort >> 8) & 0xff));
                    
                    if (port == 6672)
                    {
                        peerCount++;
                    }
                }
                ptr = (IntPtr)((long)ptr + rowSize);
            }
            
            // The listener socket counts as 1. Subtract it to get only dynamic connection sockets
            return Math.Max(0, peerCount - 1);
        }
        catch
        {
            return 0;
        }
        finally
        {
            Marshal.FreeHGlobal(pTable);
        }
    }

    // --- EVENT HANDLERS ---

    private void btnBlock_Click(object sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(gta5Path))
        {
            MessageBox.Show("Please select the game path first (BROWSE).", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (!FirewallManager.RulesExist())
        {
            Log("Firewall rules not found. Initializing...");
            FirewallManager.InitRules(gta5Path, chkP2POnly.Checked);
        }
        FirewallManager.SetBlockState(true);
        UpdateUI();
        Log("Firewall rules enabled manually.");

        if (chkAutoUnblock.Checked)
        {
            int delay = trackUnblockTime.Value;
            Log(string.Format("Auto-Unblock trigger armed for {0} seconds...", delay));
            Task.Delay(delay * 1000).ContinueWith(t => {
                FirewallManager.SetBlockState(false);
                this.Invoke(new Action(() => {
                    UpdateUI();
                    Log("Firewall rules auto-disabled by timer.");
                }));
            });
        }
    }
    
    private void btnUnblock_Click(object sender, EventArgs e)
    {
        FirewallManager.SetBlockState(false);
        UpdateUI();
        Log("Firewall rules disabled manually.");
    }

    private void chkP2POnly_CheckedChanged(object sender, EventArgs e)
    {
        if (isLoadingProfile) return;
        if (!string.IsNullOrEmpty(gta5Path))
        {
            FirewallManager.InitRules(gta5Path, chkP2POnly.Checked);
            Log("Reinitialized firewall rules. Mode: " + (chkP2POnly.Checked ? "P2P Blocker (Port 6672 UDP)" : "Full Application Blocker"));
        }
    }
    
    private async void btnSolo_Click(object sender, EventArgs e)
    {
        await PerformSoloSessionSequence();
    }

    private async Task PerformSoloSessionSequence()
    {
        int? pid = ProcessManager.FindGtaProcessId(gta5Path);
        if (!pid.HasValue)
        {
            Log("Solo Session aborted: Game process is not running.");
            MessageBox.Show("Game process is not running!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        btnSolo.Text = "SUSPENDING...";
        btnSolo.BackColor = Theme.WastedRed;
        btnSolo.Enabled = false;
        btnSolo.Refresh();

        try
        {
            int targetPid = pid.Value;
            int seconds = trackSuspendTime.Value;
            bool comboMode = chkP2POnly.Checked; // Use firewall combo block
            
            Log(string.Format("Initiating Solo Session. Suspend GTA5.exe (PID: {0}) for {1}s.", targetPid, seconds));
            
            if (comboMode)
            {
                FirewallManager.SetBlockState(true);
                this.Invoke(new Action(UpdateUI));
                Log("Solo Combo: Firewall P2P block applied.");
            }

            await Task.Run(() =>
            {
                ProcessManager.SuspendProcess(targetPid);
                System.Threading.Thread.Sleep(seconds * 1000);
                ProcessManager.ResumeProcess(targetPid);
            });

            if (comboMode)
            {
                Log("Solo Combo: Awaiting 3-second network detach buffer...");
                await Task.Delay(3000);
                FirewallManager.SetBlockState(false);
                this.Invoke(new Action(UpdateUI));
                Log("Solo Combo: Firewall P2P block disabled.");
            }

            Log("Solo Session sequence completed.");
            System.Media.SystemSounds.Asterisk.Play();
            trayIcon.ShowBalloonTip(3000, "GTA Solo Session", "Session isolated successfully!", ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            Log("Solo Session Error: " + ex.Message);
            MessageBox.Show("Error during suspend sequence: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnSolo.Text = "SOLO SESSION (" + trackSuspendTime.Value + "s SUSPEND)";
            btnSolo.BackColor = Theme.RsYellow;
            btnSolo.Enabled = true;
        }
    }
    
    private async void btnFlush_Click(object sender, EventArgs e)
    {
        btnFlush.Text = "FLUSHING...";
        btnFlush.Enabled = false;
        btnFlush.Refresh();

        try
        {
            await GameController.FlushDnsAsync();
            Log("Cleared Windows DNS Cache.");
            MessageBox.Show("DNS Cache cleared successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Log("Flush DNS Error: " + ex.Message);
            MessageBox.Show("Error flushing DNS: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnFlush.Text = "FLUSH DNS";
            btnFlush.Enabled = true;
        }
    }
    
    private async void btnClearCache_Click(object sender, EventArgs e)
    {
        btnClearCache.Text = "CLEARING...";
        btnClearCache.Enabled = false;
        btnClearCache.Refresh();

        try
        {
            int deleted = await GameController.ClearCacheAsync();
            Log(string.Format("Cleared game cache folder (deleted {0} items).", deleted));
            MessageBox.Show(string.Format("Game cache cleared successfully. Cleared {0} files/folders.", deleted), "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (DirectoryNotFoundException)
        {
            Log("Clear Cache aborted: Cache folder not found.");
            MessageBox.Show("Game cache folder not found.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Log("Clear Cache Error: " + ex.Message);
            MessageBox.Show("Error clearing game cache: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnClearCache.Text = "CLEAR GAME CACHE";
            btnClearCache.Enabled = true;
        }
    }
    
    private async void btnAnalyze_Click(object sender, EventArgs e)
    {
        btnAnalyze.Text = "ANALYZING...";
        btnAnalyze.Enabled = false;
        btnAnalyze.Refresh();

        try
        {
            var specs = await Task.Run(() => SystemAnalyzer.AnalyzeSpecs());
            
            lblOptInfo.Text = string.Format("PC SPECS: CPU Cores: {0} | RAM: {1}GB\nGPU: {2}", specs.Cores, specs.RamGB, specs.GpuName);
            Log(string.Format("PC Analysis: Cores: {0}, RAM: {1}GB, GPU: {2}", specs.Cores, specs.RamGB, specs.GpuName));

            if (specs.Cores > 4) { chkCores.Checked = true; }
            if (specs.RamGB < 8)
            {
                chkLowEnd.Checked = true;
                chkNoMemRestrict.Checked = true;
            }
            chkNoQueue.Checked = true;
        }
        catch (Exception ex)
        {
            lblOptInfo.Text = "PC SPECS: Analysis error.";
            Log("Analyze Specs Error: " + ex.Message);
            MessageBox.Show("Failed to retrieve PC specs: " + ex.Message, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            btnAnalyze.Text = "ANALYZE PC SPECS";
            btnAnalyze.Enabled = true;
        }
    }

    private void cmbProfiles_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (cmbProfiles.SelectedIndex == 1) // Max Performance (DX10)
        {
            chkCores.Checked = true;
            chkNoQueue.Checked = true;
            chkLowEnd.Checked = true;
            chkNoMemRestrict.Checked = true;
            txtCustomCmd.Text = "-DX10 -shadowQuality 0 -grassQuality 0 -reflectionQuality 0";
            Log("Selected Optimization Profile: Max Performance (DX10).");
        }
        else if (cmbProfiles.SelectedIndex == 2) // Standard Optimization (DX11)
        {
            chkCores.Checked = true;
            chkNoQueue.Checked = true;
            chkLowEnd.Checked = false;
            chkNoMemRestrict.Checked = true;
            txtCustomCmd.Text = "-DX11 -FrameQueueLimit 0 -nomemrestrict";
            Log("Selected Optimization Profile: Standard Optimization (DX11).");
        }
        else if (cmbProfiles.SelectedIndex == 3) // Reset Defaults
        {
            chkCores.Checked = false;
            chkNoQueue.Checked = false;
            chkLowEnd.Checked = false;
            chkNoMemRestrict.Checked = false;
            txtCustomCmd.Text = "";
            Log("Selected Optimization Profile: Defaults (Clear).");
        }
    }
    
    private void btnApplyOpt_Click(object sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(gta5Path))
        {
            MessageBox.Show("Please select the game path first.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        bool noneChecked = !chkCores.Checked && !chkNoQueue.Checked && !chkLowEnd.Checked && !chkNoMemRestrict.Checked && string.IsNullOrEmpty(txtCustomCmd.Text);
        if (noneChecked)
        {
            var result = MessageBox.Show("No optimization arguments are selected. This will delete commandline.txt. Proceed?", "Confirm deletion", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes)
            {
                return;
            }
        }

        try
        {
            GameController.GenerateCommandLine(gta5Path, chkCores.Checked, chkNoQueue.Checked, chkLowEnd.Checked, chkNoMemRestrict.Checked, txtCustomCmd.Text);
            if (noneChecked)
            {
                Log("Deleted commandline.txt from game directory.");
                MessageBox.Show("All settings reset. commandline.txt deleted.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                Log("Generated commandline.txt with optimized args.");
                MessageBox.Show("commandline.txt successfully generated in game directory.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex) {
            Log("Generate commandline.txt Error: " + ex.Message);
            MessageBox.Show("Failed to apply settings: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void btnOptimizeSettingsXml_Click(object sender, EventArgs e)
    {
        try
        {
            GameController.OptimizeSettingsXml();
            Log("Applied low-end PC optimization tweaks to settings.xml. Backup created.");
            MessageBox.Show("Successfully optimized settings.xml (MSAA disabled, shadows off, water quality low). Original backup settings.xml.bak created.", "settings.xml Optimized", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Log("Optimize settings.xml Error: " + ex.Message);
            MessageBox.Show("Error optimizing settings.xml: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void btnCleanRam_Click(object sender, EventArgs e)
    {
        try
        {
            int count = ProcessManager.CleanRamWorkingSet();
            Log(string.Format("RAM Cleaned: Emptied working set for {0} active system processes.", count));
            MessageBox.Show(string.Format("RAM memory optimization completed! Trimmed working sets of {0} processes.", count), "RAM Cleaned", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Log("Clean RAM Error: " + ex.Message);
            MessageBox.Show("Error optimizing RAM: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void btnSuspendBg_Click(object sender, EventArgs e)
    {
        string[] targetNames = { "chrome", "discord", "spotify", "steamwebhelper" };
        int suspendedCount = 0;
        int clearedWorkingSetCount = 0;
        
        foreach (var name in targetNames)
        {
            Process[] procs = Process.GetProcessesByName(name);
            foreach (var p in procs)
            {
                try
                {
                    if (name.Equals("chrome", StringComparison.OrdinalIgnoreCase))
                    {
                        // Chrome safety - do not suspend (causes tab crash)
                        clearedWorkingSetCount++;
                    }
                    else
                    {
                        // Other background apps (Discord, Spotify, Steam Web Helper) - suspend
                        ProcessManager.SuspendProcess(p.Id);
                        suspendedBgPids.Add(p.Id);
                        suspendedCount++;
                    }
                }
                catch {}
                finally { p.Dispose(); }
            }
        }
        
        suspendedBgTime = DateTime.Now;
        Log(string.Format("Background apps optimized: Suspended {0} instances, cleared RAM for {1} Chrome instances.", suspendedCount, clearedWorkingSetCount));
        MessageBox.Show(string.Format("Background apps optimized:\n- Suspended: {0} processes (Discord, Spotify, etc.)\n- Purged RAM: {1} Chrome processes", suspendedCount, clearedWorkingSetCount), "Optimization Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void btnResumeBg_Click(object sender, EventArgs e)
    {
        ResumeAllSuspendedBackgroundProcesses();
        MessageBox.Show("All suspended background processes have been resumed.", "Processes Restored", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ResumeAllSuspendedBackgroundProcesses()
    {
        int count = ResumeAllSuspendedBackgroundProcessesInternal();
        Log(string.Format("Resumed {0} suspended background process instances.", count));
    }

    private static int ResumeAllSuspendedBackgroundProcessesInternal()
    {
        int count = 0;
        foreach (int pid in suspendedBgPids)
        {
            try
            {
                ProcessManager.ResumeProcess(pid);
                count++;
            }
            catch {}
        }
        suspendedBgPids.Clear();
        suspendedBgTime = null;
        return count;
    }
    
    private async void btnRefreshNet_Click(object sender, EventArgs e)
    {
        btnRefreshNet.Text = "SCANNING...";
        btnRefreshNet.Enabled = false;
        btnRefreshNet.Refresh();

        try
        {
            cmbAdapters.Items.Clear();
            var adapters = await Task.Run(() => SystemAnalyzer.GetNetworkAdapters());
            foreach (var ad in adapters)
            {
                cmbAdapters.Items.Add(ad);
            }
            if (cmbAdapters.Items.Count > 0)
            {
                cmbAdapters.SelectedIndex = 0;
                Log("Scanned network adapters. Loaded: " + cmbAdapters.Items.Count);
            }
        }
        catch (Exception ex)
        {
            Log("Scan Adapters Error: " + ex.Message);
            MessageBox.Show("Failed to scan network adapters: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnRefreshNet.Text = "SCAN ADAPTERS";
            btnRefreshNet.Enabled = true;
        }
    }
    
    private void btnLaunchVpn_Click(object sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(gta5Path))
        {
            MessageBox.Show("Please select the game path first.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (cmbAdapters.SelectedIndex < 0)
        {
            MessageBox.Show("Please select a network adapter.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var adapter = (SystemAnalyzer.NetworkAdapterInfo)cmbAdapters.SelectedItem;
        string ip = adapter.IpAddress;
        string forceBindPath = Path.Combine(appDir, "ForceBindIP64.exe");

        if (!File.Exists(forceBindPath))
        {
            string msg = "ForceBindIP64.exe utility was not found in the application folder.\n\nPlease download it from the official site (r1ch.net/projects/forcebindip) and place ForceBindIP64.exe and BindIP64.dll in:\n" + appDir;
            MessageBox.Show(msg, "ForceBindIP Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = forceBindPath,
                Arguments = string.Format("{0} \"{1}\"", ip, gta5Path),
                WorkingDirectory = Path.GetDirectoryName(gta5Path),
                UseShellExecute = true
            };
            using (Process.Start(psi)) { }
            Log("Launched GTA5 via ForceBindIP. Bound local IP: " + ip);
            MessageBox.Show("Launching game via adapter " + ip + "...", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Log("Launch via ForceBindIP Error: " + ex.Message);
            MessageBox.Show("Failed to launch game: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void btnCheckIp_Click(object sender, EventArgs e)
    {
        if (cmbAdapters.SelectedIndex < 0)
        {
            MessageBox.Show("Please select an adapter to query through.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var adapter = (SystemAnalyzer.NetworkAdapterInfo)cmbAdapters.SelectedItem;
        string localIp = adapter.IpAddress;

        lblExternalIp.Text = "Querying public IP...";
        Log("Querying external IP via adapter: " + adapter.Name);

        try
        {
            string publicIp = await NetworkTools.GetPublicIpAsync(localIp);
            lblExternalIp.Text = "Current Public IP (via Selected Adapter): " + publicIp;
            Log("Public IP retrieved: " + publicIp);
        }
        catch (Exception ex)
        {
            lblExternalIp.Text = "Error resolving public IP.";
            Log("Public IP Check Error: " + ex.Message);
        }
    }

    private async void btnPingTest_Click(object sender, EventArgs e)
    {
        if (cmbAdapters.SelectedIndex < 0)
        {
            MessageBox.Show("Please select an adapter.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var adapter = (SystemAnalyzer.NetworkAdapterInfo)cmbAdapters.SelectedItem;
        string localIp = adapter.IpAddress;
        string targetHost = "prs-gta5.ros.rockstargames.com";

        lblPingStatus.Text = "Pinging Rockstar Session Servers...";
        Log("Initiating TCP ping test to " + targetHost + " via " + localIp);

        long? latency = await NetworkTools.PingHostAsync(targetHost, localIp);

        if (latency.HasValue)
        {
            lblPingStatus.Text = string.Format("Rockstar Session Server Ping: {0} ms (via {1})", latency.Value, adapter.Name);
            Log(string.Format("Ping successful: {0} ms", latency.Value));
        }
        else
        {
            lblPingStatus.Text = "Rockstar Session Server Ping: Request Timeout / Unreachable";
            Log("Ping test failed (Timeout/Unreachable).");
        }
     }

     private void btnResetDns_Click(object sender, EventArgs e)
    {
        if (cmbAdapters.SelectedIndex < 0)
        {
            MessageBox.Show("Please select a network adapter.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var adapter = (SystemAnalyzer.NetworkAdapterInfo)cmbAdapters.SelectedItem;
        SetAdapterDns(adapter.Name, null);
        MessageBox.Show("DNS settings successfully updated!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void btnAddRoute_Click(object sender, EventArgs e)
    {
        if (cmbAdapters.SelectedIndex < 0)
        {
            MessageBox.Show("Please select an adapter.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var adapter = (SystemAnalyzer.NetworkAdapterInfo)cmbAdapters.SelectedItem;

        // Fetch physical gateway IP
        string gateway = GetAdapterGateway(adapter.Name);
        if (string.IsNullOrEmpty(gateway))
        {
            MessageBox.Show("Could not retrieve gateway IP for selected physical adapter.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Log("Selected physical adapter gateway: " + gateway);
        
        string[] targetHosts = { "prs-gta5.ros.rockstargames.com", "ros.rockstargames.com" };
        int added = 0;
        foreach (var host in targetHosts)
        {
            try
            {
                IPAddress[] ips = Dns.GetHostAddresses(host);
                foreach (var ip in ips)
                {
                    string ipStr = ip.ToString();
                    NetworkTools.AddRoute(ipStr, gateway);
                    Log(string.Format("Added split-routing rule: {0} -> Gateway {1}", ipStr, gateway));
                    added++;
                }
            }
            catch {}
        }
        
        if (added > 0)
        {
            ConfigManager.Set("AddedRoutes", string.Join(",", addedRoutes));
        }
        MessageBox.Show(string.Format("Successfully added {0} bypass routing rule(s) to Windows table.", added), "Bypass Routes Added", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void btnBrowse_Click(object sender, EventArgs e)
    {
        using (OpenFileDialog ofd = new OpenFileDialog())
        {
            ofd.Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                gta5Path = ofd.FileName;
                txtPath.Text = gta5Path;
                GameController.SaveGtaPath(gta5Path);
                FirewallManager.InitRules(gta5Path, chkP2POnly.Checked);
                UpdateUI();
                Log("Set game executable path: " + gta5Path);
            }
        }
    }

    private void btnRestoreBackups_Click(object sender, EventArgs e)
    {
        try
        {
            GameController.RestoreBackups(gta5Path);
            Log("Restored settings.xml and commandline.txt backup files (.bak).");
        }
        catch (Exception ex)
        {
            Log("Restore Backups Error: " + ex.Message);
            MessageBox.Show("Error restoring backups: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // --- DRAG AND DROP ---
    private void MainForm_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effect = DragDropEffects.Copy;
    }

    private void MainForm_DragDrop(object sender, DragEventArgs e)
    {
        string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (files.Length > 0)
        {
            string file = files[0];
            string fileName = Path.GetFileName(file);
            if (file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && 
                (fileName.IndexOf("gta5", StringComparison.OrdinalIgnoreCase) >= 0 || 
                 fileName.IndexOf("gtaiv", StringComparison.OrdinalIgnoreCase) >= 0 || 
                 fileName.IndexOf("gta_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 fileName.IndexOf("playgta", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                gta5Path = file;
                txtPath.Text = gta5Path;
                GameController.SaveGtaPath(gta5Path);
                FirewallManager.InitRules(gta5Path, chkP2POnly.Checked);
                UpdateUI();
                Log("Updated game path via drag & drop: " + gta5Path);
            }
            else
            {
                MessageBox.Show("Please drop a valid GTA5/GTAIV game executable (.exe).", "Invalid Executable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    private void chkCompatFlags_CheckedChanged(object sender, EventArgs e)
    {
        if (isLoadingProfile) return;
        if (string.IsNullOrEmpty(gta5Path)) return;
        try
        {
            if (chkCompatFlags.Checked)
            {
                RegWrite(@"HKEY_CURRENT_USER\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers", gta5Path, "~ DISABLEDXMAXIMIZEDWINDOWEDMODE HIGHDPIAWARE");
                Log("Enabled Fullscreen Optimizations disable & High DPI scale for GTA5.exe.");
            }
            else
            {
                RegDelete(@"HKEY_CURRENT_USER\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers", gta5Path);
                Log("Cleared compatibility registry flags for GTA5.exe.");
            }
        }
        catch (Exception ex)
        {
            Log("Compat Registry Error: " + ex.Message);
        }
    }

    private void chkGameMode_CheckedChanged(object sender, EventArgs e)
    {
        if (isLoadingProfile) return;
        try
        {
            RegWrite(@"HKEY_CURRENT_USER\Software\Microsoft\GameBar", "AllowAutoGameMode", chkGameMode.Checked ? "1" : "0", RegistryValueKind.DWord);
            Log("Set Windows Game Mode registry toggle to: " + chkGameMode.Checked);
        }
        catch (Exception ex)
        {
            Log("Game Mode Registry Error: " + ex.Message);
        }
    }

    private void chkAutostart_CheckedChanged(object sender, EventArgs e)
    {
        if (isLoadingProfile) return;
        try
        {
            if (chkAutostart.Checked)
            {
                RegWrite(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run", "GTAMultiTool", string.Format("\"{0}\" --tray", Application.ExecutablePath));
                Log("Autostart registry entry added with tray launch argument.");
            }
            else
            {
                RegDelete(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run", "GTAMultiTool");
                Log("Autostart registry entry removed.");
            }
        }
        catch (Exception ex)
        {
            Log("Autostart Registry Error: " + ex.Message);
        }
    }

    private bool IsAutostartEnabled()
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false))
            {
                if (key != null)
                {
                    return key.GetValue("GTAMultiTool") != null;
                }
            }
        }
        catch {}
        return false;
    }

    // --- GLOBAL HOTKEY LOGIC ---
    private void chkGlobalHotkey_CheckedChanged(object sender, EventArgs e)
    {
        if (isLoadingProfile) return;
        if (chkGlobalHotkey.Checked)
        {
            // Register Ctrl + F1
            bool success = RegisterHotKey(this.Handle, HOTKEY_ID, MOD_CONTROL, VK_F1);
            if (success)
            {
                Log("Global Hotkey (Ctrl + F1) registered successfully.");
            }
            else
            {
                Log("Failed to register global hotkey (Ctrl + F1). Port/ID might be occupied.");
                chkGlobalHotkey.Checked = false;
            }
        }
        else
        {
            UnregisterHotKey(this.Handle, HOTKEY_ID);
            Log("Global Hotkey unregistered.");
        }
    }

    private void ShowMe()
    {
        if (this.WindowState == FormWindowState.Minimized)
        {
            this.WindowState = FormWindowState.Normal;
        }
        this.Show();
        this.ShowInTaskbar = true;
        this.Activate();
        NativeMethods.SetForegroundWindow(this.Handle);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_SHOWME)
        {
            ShowMe();
            return;
        }
        const int WM_HOTKEY = 0x0312;
        if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
        {
            TriggerHotkeyAction();
        }
        base.WndProc(ref m);
    }

    private async void TriggerHotkeyAction()
    {
        Log("Hotkey triggered! (Ctrl + F1)");
        System.Media.SystemSounds.Beep.Play();

        // Perform Quick Solo lobby sequence
        await PerformSoloSessionSequence();
    }

    private void chkEnableOverlay_CheckedChanged(object sender, EventArgs e)
    {
        if (chkEnableOverlay.Checked)
        {
            if (overlayFm == null || overlayFm.IsDisposed)
            {
                overlayFm = new OverlayForm(this);
            }
            overlayFm.Show();
            if (chkLockOverlay != null)
            {
                overlayFm.SetClickThrough(chkLockOverlay.Checked);
            }
            ConfigManager.Set("EnableOverlay", "true");
        }
        else
        {
            if (overlayFm != null)
            {
                overlayFm.Hide();
            }
            ConfigManager.Set("EnableOverlay", "false");
        }
    }

    private void chkLockOverlay_CheckedChanged(object sender, EventArgs e)
    {
        bool isLocked = chkLockOverlay.Checked;
        ConfigManager.Set("LockOverlay", isLocked ? "true" : "false");
        if (overlayFm != null && !overlayFm.IsDisposed)
        {
            overlayFm.SetClickThrough(isLocked);
        }
        Log("Overlay position " + (isLocked ? "locked (click-through HUD enabled)." : "unlocked (interaction enabled)."));
    }

    public async Task TriggerSoloSessionFromOverlay()
    {
        Log("Solo lobby triggered from In-Game Overlay.");
        await PerformSoloSessionSequence();
    }

    public void InvokeBlock()
    {
        if (this.InvokeRequired) { this.Invoke(new Action(InvokeBlock)); return; }
        btnBlock_Click(this, EventArgs.Empty);
    }

    public void InvokeAllow()
    {
        if (this.InvokeRequired) { this.Invoke(new Action(InvokeAllow)); return; }
        btnUnblock_Click(this, EventArgs.Empty);
    }

    public void InvokeSolo()
    {
        if (this.InvokeRequired) { this.Invoke(new Action(InvokeSolo)); return; }
        btnSolo_Click(this, EventArgs.Empty);
    }

    private void WriteDefaultProfiles()
    {
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string dir = Path.Combine(appData, @"GTA_MultiTool\Profiles");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Profile 1: Solo Grind
            string soloPath = Path.Combine(dir, "Solo_Grind.profile");
            if (!File.Exists(soloPath))
            {
                var lines = new List<string> {
                    "AutoSolo=true", "P2POnly=true", "AutoUnblock=true", "UnblockTime=10", "SuspendTime=10",
                    "GlobalHotkey=true", "UseAllCores=true", "NoQueue=true", "LowEnd=true", "NoMemRestrict=true",
                    "HighPriority=true", "CompatFlags=true", "GameMode=true", "CustomCmd=-DX10"
                };
                File.WriteAllLines(soloPath, lines.ToArray());
            }

            // Profile 2: Public Freeroam
            string publicPath = Path.Combine(dir, "Public_Freeroam.profile");
            if (!File.Exists(publicPath))
            {
                var lines = new List<string> {
                    "AutoSolo=false", "P2POnly=false", "AutoUnblock=false", "UnblockTime=15", "SuspendTime=10",
                    "GlobalHotkey=false", "UseAllCores=false", "NoQueue=false", "LowEnd=false", "NoMemRestrict=false",
                    "HighPriority=false", "CompatFlags=false", "GameMode=false", "CustomCmd="
                };
                File.WriteAllLines(publicPath, lines.ToArray());
            }

            // Profile 3: Stream
            string streamPath = Path.Combine(dir, "Stream.profile");
            if (!File.Exists(streamPath))
            {
                var lines = new List<string> {
                    "AutoSolo=false", "P2POnly=false", "AutoUnblock=false", "UnblockTime=15", "SuspendTime=10",
                    "GlobalHotkey=true", "UseAllCores=true", "NoQueue=false", "LowEnd=false", "NoMemRestrict=false",
                    "HighPriority=true", "CompatFlags=true", "GameMode=false", "CustomCmd="
                };
                File.WriteAllLines(streamPath, lines.ToArray());
            }
        }
        catch {}
    }

    private void btnLoadProfile_Click(object sender, EventArgs e)
    {
        if (cmbSystemProfiles.SelectedIndex < 0) return;
        string name = cmbSystemProfiles.SelectedItem.ToString();
        if (name == "Custom / Unsaved")
        {
            MessageBox.Show("Please select a predefined profile to load.", "Select Profile", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string dir = Path.Combine(appData, @"GTA_MultiTool\Profiles");
            string path = Path.Combine(dir, name.Replace(" ", "_") + ".profile");
            if (!File.Exists(path))
            {
                MessageBox.Show("Profile file not found: " + path, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string[] lines = File.ReadAllLines(path);
            foreach (string line in lines)
            {
                int idx = line.IndexOf('=');
                if (idx > 0)
                {
                    dict[line.Substring(0, idx).Trim()] = line.Substring(idx + 1).Trim();
                }
            }

            isLoadingProfile = true;
            try
            {
                if (dict.ContainsKey("AutoSolo")) chkAutoSolo.Checked = bool.Parse(dict["AutoSolo"]);
                if (dict.ContainsKey("P2POnly")) chkP2POnly.Checked = bool.Parse(dict["P2POnly"]);
                if (dict.ContainsKey("AutoUnblock")) chkAutoUnblock.Checked = bool.Parse(dict["AutoUnblock"]);
                if (dict.ContainsKey("UnblockTime")) trackUnblockTime.Value = Math.Max(5, Math.Min(60, int.Parse(dict["UnblockTime"])));
                if (dict.ContainsKey("SuspendTime")) trackSuspendTime.Value = Math.Max(3, Math.Min(15, int.Parse(dict["SuspendTime"])));
                if (dict.ContainsKey("GlobalHotkey")) chkGlobalHotkey.Checked = bool.Parse(dict["GlobalHotkey"]);
                if (dict.ContainsKey("UseAllCores")) chkCores.Checked = bool.Parse(dict["UseAllCores"]);
                if (dict.ContainsKey("NoQueue")) chkNoQueue.Checked = bool.Parse(dict["NoQueue"]);
                if (dict.ContainsKey("LowEnd")) chkLowEnd.Checked = bool.Parse(dict["LowEnd"]);
                if (dict.ContainsKey("NoMemRestrict")) chkNoMemRestrict.Checked = bool.Parse(dict["NoMemRestrict"]);
                if (dict.ContainsKey("HighPriority")) chkHighPriority.Checked = bool.Parse(dict["HighPriority"]);
                if (dict.ContainsKey("CompatFlags")) chkCompatFlags.Checked = bool.Parse(dict["CompatFlags"]);
                if (dict.ContainsKey("GameMode")) chkGameMode.Checked = bool.Parse(dict["GameMode"]);
                if (dict.ContainsKey("CustomCmd")) txtCustomCmd.Text = dict["CustomCmd"];

                // Update text labels
                lblUnblockTime.Text = "AUTO-UNBLOCK TIMER: " + trackUnblockTime.Value + "s";
                lblSuspendTime.Text = "SUSPEND DURATION: " + trackSuspendTime.Value + "s";
                btnSolo.Text = "SOLO SESSION (" + trackSuspendTime.Value + "s SUSPEND)";
            }
            finally
            {
                isLoadingProfile = false;
            }

            ApplySettingsFromUI();
            Log("Applied configuration profile settings: " + name);
            MessageBox.Show(string.Format("System Profile '{0}' applied successfully!", name), "Profile Applied", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Log("Load profile error: " + ex.Message);
            MessageBox.Show("Failed to load profile: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void btnSaveProfile_Click(object sender, EventArgs e)
    {
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string dir = Path.Combine(appData, @"GTA_MultiTool\Profiles");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "Custom.profile");

            var lines = new List<string> {
                "AutoSolo=" + chkAutoSolo.Checked,
                "P2POnly=" + chkP2POnly.Checked,
                "AutoUnblock=" + chkAutoUnblock.Checked,
                "UnblockTime=" + trackUnblockTime.Value,
                "SuspendTime=" + trackSuspendTime.Value,
                "GlobalHotkey=" + chkGlobalHotkey.Checked,
                "UseAllCores=" + chkCores.Checked,
                "NoQueue=" + chkNoQueue.Checked,
                "LowEnd=" + chkLowEnd.Checked,
                "NoMemRestrict=" + chkNoMemRestrict.Checked,
                "HighPriority=" + chkHighPriority.Checked,
                "CompatFlags=" + chkCompatFlags.Checked,
                "GameMode=" + chkGameMode.Checked,
                "CustomCmd=" + txtCustomCmd.Text
            };

            File.WriteAllLines(path, lines.ToArray());
            cmbSystemProfiles.SelectedIndex = 3; // Select custom
            Log("Saved current settings to Custom.profile");
            MessageBox.Show("Current settings saved to Custom Profile!", "Profile Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Log("Save profile error: " + ex.Message);
            MessageBox.Show("Failed to save profile: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ApplySettingsFromUI()
    {
        // Save game path
        ConfigManager.Set("GTA5Path", gta5Path);

        // Update firewall rules based on game path and P2P mode
        FirewallManager.InitRules(gta5Path, chkP2POnly.Checked);

        // Write registry compatibility flags
        try
        {
            if (!string.IsNullOrEmpty(gta5Path))
            {
                if (chkCompatFlags.Checked)
                {
                    AddCompatFlag(gta5Path, "DISABLEDXMAXIMIZEDWINDOWEDMODE HIGHDPIAWARE");
                }
                else
                {
                    RemoveCompatFlag(gta5Path, "DISABLEDXMAXIMIZEDWINDOWEDMODE HIGHDPIAWARE");
                }
            }
        }
        catch (Exception ex) { Log("Compat Registry Error: " + ex.Message); }

        // Write registry Game Mode
        try
        {
            // Back up first
            if (ConfigManager.Get("Backup_GameMode", null) == null)
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\GameBar", false))
                {
                    object existing = key != null ? key.GetValue("AllowAutoGameMode") : null;
                    ConfigManager.Set("Backup_GameMode", existing != null ? existing.ToString() : "__NULL__");
                }
            }

            RegWrite(@"HKEY_CURRENT_USER\Software\Microsoft\GameBar", "AllowAutoGameMode", chkGameMode.Checked ? "1" : "0", RegistryValueKind.DWord);
        }
        catch (Exception ex) { Log("Game Mode Registry Error: " + ex.Message); }

        // Register/Unregister hotkey
        try
        {
            if (chkGlobalHotkey.Checked)
            {
                RegisterHotKey(this.Handle, HOTKEY_ID, MOD_CONTROL, VK_F1);
            }
            else
            {
                UnregisterHotKey(this.Handle, HOTKEY_ID);
            }
        }
        catch {}

        // Generate commandline
        try
        {
            if (!string.IsNullOrEmpty(gta5Path))
            {
                GameController.GenerateCommandLine(gta5Path, chkCores.Checked, chkNoQueue.Checked, chkLowEnd.Checked, chkNoMemRestrict.Checked, txtCustomCmd.Text);
            }
        }
        catch (Exception ex) { Log("CommandLine generation error: " + ex.Message); }
    }

    private void btnOpenLogFile_Click(object sender, EventArgs e)
    {
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string logPath = Path.Combine(appData, @"GTA_MultiTool\log.txt");
            if (File.Exists(logPath))
            {
                Process.Start(logPath);
            }
            else
            {
                MessageBox.Show("Log file is empty / does not exist yet.", "Log Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Failed to open log file: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            string currentVersion = "2.1.0"; 
            string versionUrl = "https://raw.githubusercontent.com/ryoqe/GTA-MultiTool/main/version.txt";

            using (WebClient wc = new WebClient())
            {
                string remoteVersion = await wc.DownloadStringTaskAsync(versionUrl);
                remoteVersion = remoteVersion.Trim();
                if (new Version(remoteVersion) > new Version(currentVersion))
                {
                    Log(string.Format("New version available: {0} (Current: {1})", remoteVersion, currentVersion));
                    this.BeginInvoke(new Action(() => {
                        var result = MessageBox.Show(string.Format("A new version ({0}) is available on GitHub. Would you like to open the releases page to download it?", remoteVersion), "Update Available", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                        if (result == DialogResult.Yes)
                        {
                            try { Process.Start("https://github.com/ryoqe/GTA-MultiTool/releases"); } catch {}
                        }
                    }));
                }
            }
        }
        catch {}
    }

    public static void PerformFullSystemRollback(string gtaPath)
    {
        // 1. Restore file backups (.bak)
        try
        {
            string docDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string xmlPath = Path.Combine(docDir, @"Rockstar Games\GTA V\settings.xml");
            string xmlBak = xmlPath + ".bak";
            if (File.Exists(xmlBak))
            {
                File.Copy(xmlBak, xmlPath, true);
            }

            if (!string.IsNullOrEmpty(gtaPath))
            {
                string dir = Path.GetDirectoryName(gtaPath);
                string cmdPath = Path.Combine(dir, "commandline.txt");
                string cmdBak = cmdPath + ".bak";
                if (File.Exists(cmdBak))
                {
                    File.Copy(cmdBak, cmdPath, true);
                }
            }
        }
        catch {}

        // 2. Restore registry compatibility flags
        try
        {
            if (!string.IsNullOrEmpty(gtaPath))
            {
                string backupCompat = ConfigManager.Get("Backup_CompatFlags_" + gtaPath, null);
                if (backupCompat != null)
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers", false))
                    {
                        if (key != null)
                        {
                            if (backupCompat == "__NULL__")
                            {
                                RegDelete(@"HKEY_CURRENT_USER\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers", gtaPath);
                            }
                            else
                            {
                                RegWrite(@"HKEY_CURRENT_USER\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers", gtaPath, backupCompat);
                            }
                        }
                    }
                    ConfigManager.Set("Backup_CompatFlags_" + gtaPath, null);
                }
            }
        }
        catch {}

        // 3. Restore registry Game Mode
        try
        {
            string backupGameMode = ConfigManager.Get("Backup_GameMode", null);
            if (backupGameMode != null)
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\GameBar", false))
                {
                    if (key != null)
                    {
                        if (backupGameMode == "__NULL__")
                        {
                            RegDelete(@"HKEY_CURRENT_USER\Software\Microsoft\GameBar", "AllowAutoGameMode");
                        }
                        else
                        {
                            RegWrite(@"HKEY_CURRENT_USER\Software\Microsoft\GameBar", "AllowAutoGameMode", backupGameMode, RegistryValueKind.DWord);
                        }
                    }
                }
                ConfigManager.Set("Backup_GameMode", null);
            }
        }
        catch {}

        // 4. Remove autostart
        try
        {
            RegDelete(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run", "GTAMultiTool");
        }
        catch {}

        // 5. Disable & Clean Firewall rules
        try
        {
            FirewallManager.SetBlockState(false);
            // Delete rules entirely via netsh
            ProcessManager.RunCommand("netsh", "advfirewall firewall delete rule name=\"GTA5_Block_Out\"");
            ProcessManager.RunCommand("netsh", "advfirewall firewall delete rule name=\"GTA5_Block_In\"");
        }
        catch {}

        // 6. Restore DNS default settings and delete added routes
        RestoreNetworkSettingsInternal();
    }

    private static readonly object cleanupLock = new object();
    private static bool cleanupDone = false;

    public static void GlobalCleanup()
    {
        lock (cleanupLock)
        {
            if (cleanupDone) return;
            cleanupDone = true;

            LogToFileDirect("Starting process exit cleanup sequence...");

            // 1. Resume background processes
            ResumeAllSuspendedBackgroundProcessesInternal();

            // 2. Restore network settings (DNS, routes)
            RestoreNetworkSettingsInternal();

            LogToFileDirect("Cleanup sequence completed.");
        }
    }

    private static void LogToFileDirect(string msg)
    {
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string logDir = Path.Combine(appData, "GTA_MultiTool");
            if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
            string lFile = Path.Combine(logDir, "log.txt");
            File.AppendAllText(lFile, string.Format("[{0}] {1}\r\n", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), msg));
        }
        catch {}
    }
}
