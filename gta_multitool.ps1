Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

# C# Code for Process Suspend/Resume (will compile only when needed)
$csharpCode = @"
using System;
using System.Runtime.InteropServices;
using System.Diagnostics;

public class ProcessUtils
{
    [DllImport("ntdll.dll", PreserveSig = false)]
    public static extern void NtSuspendProcess(IntPtr processHandle);

    [DllImport("ntdll.dll", PreserveSig = false)]
    public static extern void NtResumeProcess(IntPtr processHandle);
    
    public static void SuspendProcess(int pid) {
        Process process = Process.GetProcessById(pid);
        NtSuspendProcess(process.Handle);
    }
    public static void ResumeProcess(int pid) {
        Process process = Process.GetProcessById(pid);
        NtResumeProcess(process.Handle);
    }
}
"@
$global:csharpCompiled = $false


$batchDir = $env:BATCH_DIR
if (-not $batchDir) { $batchDir = $PSScriptRoot }
if (-not $batchDir) { $batchDir = $env:APPDATA }

$configFile = Join-Path $batchDir "gta_blocker_paths.conf"

$global:gta5Path = ""

function Load-Config {
    if (Test-Path $configFile) {
        $lines = Get-Content $configFile -Encoding UTF8
        foreach ($line in $lines) {
            if ($line -match "^GTA5=(.*)$") { $global:gta5Path = $Matches[1].Trim() }
        }
    }
}

function Save-Config {
    try {
        $configContent = "GTA5=$global:gta5Path"
        $configContent | Out-File $configFile -Encoding UTF8 -Force
    } catch {
        $configFile = Join-Path $env:APPDATA "gta_blocker_paths.conf"
        $configContent | Out-File $configFile -Encoding UTF8 -Force
    }
}

function Init-Rules {
    $rules = @(
        @{Name="GTA5_Block_Out"; Dir="out"; Path=$global:gta5Path},
        @{Name="GTA5_Block_In"; Dir="in"; Path=$global:gta5Path}
    )
    foreach ($r in $rules) {
        # Delete old rules using netsh for Windows 7 compatibility
        netsh advfirewall firewall delete rule name=$r.Name 2>$null | Out-Null
        if ($r.Path) {
            # Add new rules using netsh
            netsh advfirewall firewall add rule name=$r.Name dir=$r.Dir action=block program="`"$($r.Path)`"" enable=no profile=any group="GTA_Blocker" | Out-Null
        }
    }
}

function Enable-Block {
    netsh advfirewall firewall set rule group="GTA_Blocker" new enable=yes | Out-Null
}

function Disable-Block {
    netsh advfirewall firewall set rule group="GTA_Blocker" new enable=no | Out-Null
}

Load-Config
if (-not $global:gta5Path) {
    $p = Get-Process | Where-Object { $_.Name -match "GTA5|GTAIV" } -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($p) { $global:gta5Path = $p.Path }
    else {
        $cp = "C:\Program Files (x86)\Steam\steamapps\common\Grand Theft Auto V\GTA5.exe"
        if (Test-Path $cp) { $global:gta5Path = $cp }
    }
}
Save-Config
if ($global:gta5Path) { Init-Rules }

# ----------------- UI DESIGN -----------------

$bgBlack = [System.Drawing.Color]::FromArgb(10, 10, 10)
$bgGray = [System.Drawing.Color]::FromArgb(25, 25, 25)
$rsYellow = [System.Drawing.Color]::FromArgb(252, 165, 36)
$textWhite = [System.Drawing.Color]::White
$wastedRed = [System.Drawing.Color]::FromArgb(200, 30, 30)
$moneyGreen = [System.Drawing.Color]::FromArgb(60, 160, 70)
$btnBorder = [System.Drawing.Color]::FromArgb(50, 50, 50)

$fontTitle = New-Object System.Drawing.Font("Arial Black", 20, [System.Drawing.FontStyle]::Bold)
$fontNormal = New-Object System.Drawing.Font("Impact", 14, [System.Drawing.FontStyle]::Regular)
$fontSmall = New-Object System.Drawing.Font("Trebuchet MS", 10, [System.Drawing.FontStyle]::Bold)
$fontHuge = New-Object System.Drawing.Font("Arial Black", 24, [System.Drawing.FontStyle]::Bold)

$form = New-Object System.Windows.Forms.Form
$form.Text = "GTA MULTI-TOOL (BLOCKER & OPTIMIZER)"
$form.Size = New-Object System.Drawing.Size(650, 550)
$form.StartPosition = 'CenterScreen'
$form.TopMost = $false
$form.FormBorderStyle = 'FixedDialog'
$form.MaximizeBox = $false
$form.BackColor = $bgBlack

$tabControl = New-Object System.Windows.Forms.TabControl
$tabControl.Size = New-Object System.Drawing.Size(610, 380)
$tabControl.Location = New-Object System.Drawing.Point(10, 10)
$tabControl.Font = $fontSmall
$form.Controls.Add($tabControl)

# --- TAB 1: BLOCKER ---
$tabBlocker = New-Object System.Windows.Forms.TabPage
$tabBlocker.Text = "BLOCKER & LOBBY"
$tabBlocker.BackColor = $bgBlack
$tabControl.TabPages.Add($tabBlocker)

$pnlStatus = New-Object System.Windows.Forms.Panel
$pnlStatus.Size = New-Object System.Drawing.Size(580, 100)
$pnlStatus.Location = New-Object System.Drawing.Point(15, 15)
$pnlStatus.BackColor = $bgGray
$pnlStatus.BorderStyle = 'FixedSingle'
$tabBlocker.Controls.Add($pnlStatus)

$lblStatusTitle = New-Object System.Windows.Forms.Label
$lblStatusTitle.Text = "CONNECTION STATUS"
$lblStatusTitle.Font = $fontNormal
$lblStatusTitle.ForeColor = $rsYellow
$lblStatusTitle.Location = New-Object System.Drawing.Point(10, 5)
$lblStatusTitle.AutoSize = $true
$pnlStatus.Controls.Add($lblStatusTitle)

$lblStatus = New-Object System.Windows.Forms.Label
$lblStatus.Text = "CONNECTED"
$lblStatus.Font = $fontHuge
$lblStatus.ForeColor = $moneyGreen
$lblStatus.Location = New-Object System.Drawing.Point(10, 30)
$lblStatus.Size = New-Object System.Drawing.Size(560, 60)
$lblStatus.TextAlign = 'MiddleCenter'
$pnlStatus.Controls.Add($lblStatus)

$btnBlock = New-Object System.Windows.Forms.Button
$btnBlock.Text = "BLOCK"
$btnBlock.Font = $fontTitle
$btnBlock.BackColor = $wastedRed
$btnBlock.ForeColor = $textWhite
$btnBlock.FlatStyle = 'Flat'
$btnBlock.FlatAppearance.BorderColor = $btnBorder
$btnBlock.Location = New-Object System.Drawing.Point(15, 130)
$btnBlock.Size = New-Object System.Drawing.Size(280, 65)
$tabBlocker.Controls.Add($btnBlock)

$btnUnblock = New-Object System.Windows.Forms.Button
$btnUnblock.Text = "ALLOW"
$btnUnblock.Font = $fontTitle
$btnUnblock.BackColor = $bgBlack
$btnUnblock.ForeColor = $moneyGreen
$btnUnblock.FlatStyle = 'Flat'
$btnUnblock.FlatAppearance.BorderColor = $btnBorder
$btnUnblock.Location = New-Object System.Drawing.Point(315, 130)
$btnUnblock.Size = New-Object System.Drawing.Size(280, 65)
$tabBlocker.Controls.Add($btnUnblock)

$btnSolo = New-Object System.Windows.Forms.Button
$btnSolo.Text = "SOLO SESSION (10s SUSPEND)"
$btnSolo.Font = $fontNormal
$btnSolo.BackColor = $rsYellow
$btnSolo.ForeColor = $bgBlack
$btnSolo.FlatStyle = 'Flat'
$btnSolo.FlatAppearance.BorderColor = $btnBorder
$btnSolo.Location = New-Object System.Drawing.Point(15, 210)
$btnSolo.Size = New-Object System.Drawing.Size(580, 50)
$tabBlocker.Controls.Add($btnSolo)

$btnFlush = New-Object System.Windows.Forms.Button
$btnFlush.Text = "FLUSH DNS"
$btnFlush.Font = $fontSmall
$btnFlush.BackColor = $bgGray
$btnFlush.ForeColor = $textWhite
$btnFlush.FlatStyle = 'Flat'
$btnFlush.Location = New-Object System.Drawing.Point(15, 275)
$btnFlush.Size = New-Object System.Drawing.Size(280, 40)
$tabBlocker.Controls.Add($btnFlush)

$btnClearCache = New-Object System.Windows.Forms.Button
$btnClearCache.Text = "CLEAR GAME CACHE"
$btnClearCache.Font = $fontSmall
$btnClearCache.BackColor = $bgGray
$btnClearCache.ForeColor = $textWhite
$btnClearCache.FlatStyle = 'Flat'
$btnClearCache.Location = New-Object System.Drawing.Point(315, 275)
$btnClearCache.Size = New-Object System.Drawing.Size(280, 40)
$tabBlocker.Controls.Add($btnClearCache)

# --- TAB 2: OPTIMIZATION ---
$tabOpt = New-Object System.Windows.Forms.TabPage
$tabOpt.Text = "PC OPTIMIZATION"
$tabOpt.BackColor = $bgBlack
$tabControl.TabPages.Add($tabOpt)

$lblOptInfo = New-Object System.Windows.Forms.Label
$lblOptInfo.Text = "PC SPECS: (Press Analyze button below)"
$lblOptInfo.Font = $fontSmall
$lblOptInfo.ForeColor = $textWhite
$lblOptInfo.Location = New-Object System.Drawing.Point(15, 15)
$lblOptInfo.Size = New-Object System.Drawing.Size(580, 40)
$tabOpt.Controls.Add($lblOptInfo)

$btnAnalyze = New-Object System.Windows.Forms.Button
$btnAnalyze.Text = "ANALYZE PC SPECS"
$btnAnalyze.Font = $fontSmall
$btnAnalyze.BackColor = $bgGray
$btnAnalyze.ForeColor = $rsYellow
$btnAnalyze.FlatStyle = 'Flat'
$btnAnalyze.Location = New-Object System.Drawing.Point(15, 55)
$btnAnalyze.Size = New-Object System.Drawing.Size(180, 25)
$tabOpt.Controls.Add($btnAnalyze)

$chkCores = New-Object System.Windows.Forms.CheckBox
$chkCores.Text = "Use All CPU Cores (-USEALLAVAILABLECORES)"
$chkCores.ForeColor = $textWhite
$chkCores.Location = New-Object System.Drawing.Point(15, 90)
$chkCores.Size = New-Object System.Drawing.Size(400, 24)
$tabOpt.Controls.Add($chkCores)

$chkNoQueue = New-Object System.Windows.Forms.CheckBox
$chkNoQueue.Text = "Reduce Input Lag (-FrameQueueLimit 0)"
$chkNoQueue.ForeColor = $textWhite
$chkNoQueue.Location = New-Object System.Drawing.Point(15, 120)
$chkNoQueue.Size = New-Object System.Drawing.Size(400, 24)
$tabOpt.Controls.Add($chkNoQueue)

$chkLowEnd = New-Object System.Windows.Forms.CheckBox
$chkLowEnd.Text = "Low-End PC Mode (-DX10, -shadowQuality 0)"
$chkLowEnd.ForeColor = $textWhite
$chkLowEnd.Location = New-Object System.Drawing.Point(15, 150)
$chkLowEnd.Size = New-Object System.Drawing.Size(400, 24)
$tabOpt.Controls.Add($chkLowEnd)

$chkNoMemRestrict = New-Object System.Windows.Forms.CheckBox
$chkNoMemRestrict.Text = "Ignore VRAM Limits (-nomemrestrict)"
$chkNoMemRestrict.ForeColor = $textWhite
$chkNoMemRestrict.Location = New-Object System.Drawing.Point(15, 180)
$chkNoMemRestrict.Size = New-Object System.Drawing.Size(400, 24)
$tabOpt.Controls.Add($chkNoMemRestrict)

$lblCustomCmd = New-Object System.Windows.Forms.Label
$lblCustomCmd.Text = "Custom commandline.txt arguments (space separated):"
$lblCustomCmd.ForeColor = $textWhite
$lblCustomCmd.Location = New-Object System.Drawing.Point(15, 210)
$lblCustomCmd.AutoSize = $true
$tabOpt.Controls.Add($lblCustomCmd)

$txtCustomCmd = New-Object System.Windows.Forms.TextBox
$txtCustomCmd.Location = New-Object System.Drawing.Point(15, 235)
$txtCustomCmd.Size = New-Object System.Drawing.Size(560, 23)
$tabOpt.Controls.Add($txtCustomCmd)

$btnApplyOpt = New-Object System.Windows.Forms.Button
$btnApplyOpt.Text = "GENERATE COMMANDLINE.TXT"
$btnApplyOpt.Font = $fontNormal
$btnApplyOpt.BackColor = $rsYellow
$btnApplyOpt.ForeColor = $bgBlack
$btnApplyOpt.FlatStyle = 'Flat'
$btnApplyOpt.Location = New-Object System.Drawing.Point(15, 270)
$btnApplyOpt.Size = New-Object System.Drawing.Size(560, 45)
$tabOpt.Controls.Add($btnApplyOpt)

# --- TAB 3: VPN BYPASS ---
$tabVPN = New-Object System.Windows.Forms.TabPage
$tabVPN.Text = "VPN BYPASS"
$tabVPN.BackColor = $bgBlack
$tabControl.TabPages.Add($tabVPN)

$lblVpnInfo = New-Object System.Windows.Forms.Label
$lblVpnInfo.Text = "Select network adapter to route the game through (bypassing VPN):`nRequires ForceBindIP utility."
$lblVpnInfo.ForeColor = $textWhite
$lblVpnInfo.Location = New-Object System.Drawing.Point(15, 15)
$lblVpnInfo.Size = New-Object System.Drawing.Size(580, 40)
$tabVPN.Controls.Add($lblVpnInfo)

$btnRefreshNet = New-Object System.Windows.Forms.Button
$btnRefreshNet.Text = "SCAN ADAPTERS"
$btnRefreshNet.Font = $fontSmall
$btnRefreshNet.BackColor = $bgGray
$btnRefreshNet.ForeColor = $rsYellow
$btnRefreshNet.FlatStyle = 'Flat'
$btnRefreshNet.Location = New-Object System.Drawing.Point(15, 55)
$btnRefreshNet.Size = New-Object System.Drawing.Size(180, 25)
$tabVPN.Controls.Add($btnRefreshNet)

$cmbAdapters = New-Object System.Windows.Forms.ComboBox
$cmbAdapters.DropDownStyle = 'DropDownList'
$cmbAdapters.Location = New-Object System.Drawing.Point(15, 90)
$cmbAdapters.Size = New-Object System.Drawing.Size(560, 24)
$tabVPN.Controls.Add($cmbAdapters)

$btnLaunchVpn = New-Object System.Windows.Forms.Button
$btnLaunchVpn.Text = "LAUNCH GAME VIA ADAPTER"
$btnLaunchVpn.Font = $fontNormal
$btnLaunchVpn.BackColor = $rsYellow
$btnLaunchVpn.ForeColor = $bgBlack
$btnLaunchVpn.FlatStyle = 'Flat'
$btnLaunchVpn.Location = New-Object System.Drawing.Point(15, 130)
$btnLaunchVpn.Size = New-Object System.Drawing.Size(560, 50)
$tabVPN.Controls.Add($btnLaunchVpn)

# Bottom Settings Panel
$pnlBottom = New-Object System.Windows.Forms.Panel
$pnlBottom.Size = New-Object System.Drawing.Size(610, 80)
$pnlBottom.Location = New-Object System.Drawing.Point(10, 410)
$pnlBottom.BackColor = $bgGray
$pnlBottom.BorderStyle = 'FixedSingle'
$form.Controls.Add($pnlBottom)

$lblBottomTitle = New-Object System.Windows.Forms.Label
$lblBottomTitle.Text = "SELECTED EXECUTABLE (GTA5.exe / GTAIV.exe)"
$lblBottomTitle.Font = $fontNormal
$lblBottomTitle.ForeColor = $rsYellow
$lblBottomTitle.Location = New-Object System.Drawing.Point(10, 10)
$lblBottomTitle.AutoSize = $true
$pnlBottom.Controls.Add($lblBottomTitle)

$txtPath = New-Object System.Windows.Forms.TextBox
$txtPath.Text = $global:gta5Path
$txtPath.Font = $fontSmall
$txtPath.BackColor = $bgBlack
$txtPath.ForeColor = $textWhite
$txtPath.Location = New-Object System.Drawing.Point(15, 40)
$txtPath.Size = New-Object System.Drawing.Size(460, 23)
$txtPath.ReadOnly = $true
$pnlBottom.Controls.Add($txtPath)

$btnBrowse = New-Object System.Windows.Forms.Button
$btnBrowse.Text = "BROWSE"
$btnBrowse.Font = $fontSmall
$btnBrowse.BackColor = $bgBlack
$btnBrowse.ForeColor = $textWhite
$btnBrowse.FlatStyle = 'Flat'
$btnBrowse.FlatAppearance.BorderColor = $btnBorder
$btnBrowse.Location = New-Object System.Drawing.Point(490, 38)
$btnBrowse.Size = New-Object System.Drawing.Size(100, 26)
$pnlBottom.Controls.Add($btnBrowse)

# ----------------- EVENTS & LOGIC -----------------

function Update-UI {
    # Use netsh for Windows 7 compatibility instead of Get-NetFirewallRule
    $ruleStatus = (netsh advfirewall firewall show rule name="GTA5_Block_Out" 2>$null) -join "`n"
    if ($ruleStatus -match "Enabled:\s*Yes") {
        $lblStatus.Text = "WASTED (BLOCKED)"
        $lblStatus.ForeColor = $wastedRed
        $btnBlock.BackColor = $bgBlack; $btnBlock.ForeColor = $wastedRed; $btnBlock.Text = "BLOCKED"
        $btnUnblock.BackColor = $moneyGreen; $btnUnblock.ForeColor = $textWhite; $btnUnblock.Text = "ALLOW"
    } else {
        $lblStatus.Text = "CONNECTED"
        $lblStatus.ForeColor = $moneyGreen
        $btnBlock.BackColor = $wastedRed; $btnBlock.ForeColor = $textWhite; $btnBlock.Text = "BLOCK"
        $btnUnblock.BackColor = $bgBlack; $btnUnblock.ForeColor = $moneyGreen; $btnUnblock.Text = "ALLOWED"
    }
}

function Load-PCSpecs {
    $btnAnalyze.Text = "ANALYZING..."
    $btnAnalyze.Refresh()
    try {
        $cpu = Get-WmiObject Win32_Processor | Select-Object -First 1
        $ram = Get-WmiObject Win32_ComputerSystem | Select-Object -First 1
        $gpu = Get-WmiObject Win32_VideoController | Select-Object -First 1
        
        $cores = $cpu.NumberOfLogicalProcessors
        $ramGB = [math]::Round($ram.TotalPhysicalMemory / 1GB)
        $gpuName = $gpu.Name
        
        $lblOptInfo.Text = "PC SPECS: CPU Cores: $cores | RAM: ${ramGB}GB`nGPU: $gpuName"
        
        # Auto-suggest
        if ($cores -gt 4) { $chkCores.Checked = $true }
        if ($ramGB -lt 8) { $chkLowEnd.Checked = $true; $chkNoMemRestrict.Checked = $true }
        $chkNoQueue.Checked = $true
    } catch {
        $lblOptInfo.Text = "PC SPECS: Could not analyze."
    }
    $btnAnalyze.Text = "ANALYZE PC SPECS"
}

function Load-NetworkAdapters {
    $btnRefreshNet.Text = "SCANNING..."
    $btnRefreshNet.Refresh()
    try {
        $adapters = Get-WmiObject Win32_NetworkAdapterConfiguration -Filter "IPEnabled = 'True'"
        $cmbAdapters.Items.Clear()
        foreach ($a in $adapters) {
            $ip = $a.IPAddress[0]
            $name = $a.Description
            if ($ip) {
                [void]$cmbAdapters.Items.Add("$name ($ip)")
            }
        }
        if ($cmbAdapters.Items.Count -gt 0) { $cmbAdapters.SelectedIndex = 0 }
    } catch { }
    $btnRefreshNet.Text = "SCAN ADAPTERS"
}

$btnAnalyze.Add_Click({ Load-PCSpecs })
$btnRefreshNet.Add_Click({ Load-NetworkAdapters })

$btnBlock.Add_Click({
    if (-not $global:gta5Path) {
        [System.Windows.Forms.MessageBox]::Show("Пожалуйста, укажите путь к игре (BROWSE).", "Ошибка", 'OK', 'Warning')
        return
    }
    Enable-Block
    Update-UI
})

$btnUnblock.Add_Click({
    Disable-Block
    Update-UI
})

$btnSolo.Add_Click({
    if (-not $global:csharpCompiled) {
        $btnSolo.Text = "COMPILING ENGINE..."
        $btnSolo.Refresh()
        Add-Type -TypeDefinition $csharpCode -Language CSharp -ErrorAction SilentlyContinue
        $global:csharpCompiled = $true
    }
    
    $exeName = [System.IO.Path]::GetFileNameWithoutExtension($global:gta5Path)
    if (-not $exeName) { $exeName = "GTA5" }
    
    $p = Get-Process -Name $exeName -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $p) {
        [System.Windows.Forms.MessageBox]::Show("Процесс игры ($exeName) не запущен!", "Ошибка", 'OK', 'Warning')
        return
    }
    
    $btnSolo.Text = "SUSPENDING..."
    $btnSolo.BackColor = $wastedRed
    $btnSolo.Refresh()
    
    try {
        [ProcessUtils]::SuspendProcess($p.Id)
        Start-Sleep -Seconds 10
        [ProcessUtils]::ResumeProcess($p.Id)
        [System.Windows.Forms.MessageBox]::Show("Процесс разморожен. Вы должны быть в соло-сессии.", "Успех", 'OK', 'Information')
    } catch {
        [System.Windows.Forms.MessageBox]::Show("Ошибка: $($_.Exception.Message)", "Ошибка", 'OK', 'Error')
    }
    
    $btnSolo.Text = "SOLO SESSION (10s SUSPEND)"
    $btnSolo.BackColor = $rsYellow
})

$btnFlush.Add_Click({
    Clear-DnsClientCache -ErrorAction SilentlyContinue
    ipconfig /flushdns | Out-Null
    [System.Windows.Forms.MessageBox]::Show("DNS Кэш очищен.", "Успех", 'OK', 'Information')
})

$btnClearCache.Add_Click({
    $path = Join-Path $env:LOCALAPPDATA "Rockstar Games\GTA V"
    if (Test-Path $path) {
        try {
            Remove-Item (Join-Path $path "*") -Recurse -Force -ErrorAction SilentlyContinue
            [System.Windows.Forms.MessageBox]::Show("Кэш игры очищен.", "Успех", 'OK', 'Information')
        } catch { }
    } else {
        [System.Windows.Forms.MessageBox]::Show("Папка кэша не найдена.", "Информация", 'OK', 'Information')
    }
})

$btnApplyOpt.Add_Click({
    if (-not $global:gta5Path) {
        [System.Windows.Forms.MessageBox]::Show("Пожалуйста, укажите путь к игре.", "Ошибка", 'OK', 'Warning')
        return
    }
    $dir = [System.IO.Path]::GetDirectoryName($global:gta5Path)
    $cmdPath = Join-Path $dir "commandline.txt"
    
    $args = @()
    if ($chkCores.Checked) { $args += "-USEALLAVAILABLECORES" }
    if ($chkNoQueue.Checked) { $args += "-FrameQueueLimit 0" }
    if ($chkLowEnd.Checked) { $args += "-DX10 -shadowQuality 0 -grassQuality 0 -reflectionQuality 0" }
    if ($chkNoMemRestrict.Checked) { $args += "-nomemrestrict -norestrictions" }
    if ($txtCustomCmd.Text) { $args += $txtCustomCmd.Text }
    
    if ($args.Count -eq 0) {
        if (Test-Path $cmdPath) { Remove-Item $cmdPath -Force }
        [System.Windows.Forms.MessageBox]::Show("Все настройки сброшены. commandline.txt удален.", "Успех", 'OK', 'Information')
    } else {
        $content = $args -join " "
        $content | Out-File $cmdPath -Encoding ASCII -Force
        [System.Windows.Forms.MessageBox]::Show("Файл commandline.txt успешно создан в папке с игрой!`n`nАргументы: $content", "Успех", 'OK', 'Information')
    }
})

$btnLaunchVpn.Add_Click({
    if (-not $global:gta5Path) {
        [System.Windows.Forms.MessageBox]::Show("Пожалуйста, укажите путь к игре.", "Ошибка", 'OK', 'Warning')
        return
    }
    if ($cmbAdapters.SelectedIndex -lt 0) {
        [System.Windows.Forms.MessageBox]::Show("Выберите адаптер.", "Ошибка", 'OK', 'Warning')
        return
    }
    
    $selected = $cmbAdapters.SelectedItem.ToString()
    if ($selected -match "\(([\d\.]+)\)") {
        $ip = $Matches[1]
        
        $forceBindPath = Join-Path $batchDir "ForceBindIP64.exe"
        if (-not (Test-Path $forceBindPath)) {
            $msg = "Утилита ForceBindIP64.exe не найдена в папке со скриптом.`n`nВам необходимо скачать её с официального сайта (r1ch.net/projects/forcebindip) и положить ForceBindIP64.exe и BindIP64.dll в папку: `n$batchDir"
            [System.Windows.Forms.MessageBox]::Show($msg, "Требуется ForceBindIP", 'OK', 'Warning')
            return
        }
        
        try {
            Start-Process -FilePath $forceBindPath -ArgumentList "$ip `"$global:gta5Path`"" -WorkingDirectory ([System.IO.Path]::GetDirectoryName($global:gta5Path))
            [System.Windows.Forms.MessageBox]::Show("Игра запускается через адаптер $ip...", "Успех", 'OK', 'Information')
        } catch {
            [System.Windows.Forms.MessageBox]::Show("Ошибка запуска: $($_.Exception.Message)", "Ошибка", 'OK', 'Error')
        }
    }
})

$btnBrowse.Add_Click({
    $ofd = New-Object System.Windows.Forms.OpenFileDialog
    $ofd.Filter = "Исполняемые файлы (*.exe)|*.exe|Все файлы (*.*)|*.*"
    if ($ofd.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
        $global:gta5Path = $ofd.FileName
        $txtPath.Text = $global:gta5Path
        Save-Config
        Init-Rules
    }
})

$form.Add_Load({
    Update-UI
})

[void]$form.ShowDialog()
