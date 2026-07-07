using System;
using System.Collections.Generic;

public static class I18n
{
    private static string currentLang = "ru"; // default to Russian

    private static readonly Dictionary<string, Dictionary<string, string>> strings = 
        new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
    {
        { "ru", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            { "App_Title", "GTA MULTI-TOOL (СЕССИЯ И ОПТИМИЗАЦИЯ)" },
            { "Tab_Lobby", "🛡️ Сессия и файрвол" },
            { "Tab_Optimize", "⚡ Оптимизация" },
            { "Tab_Network", "🌐 Сеть и VPN" },
            { "Tab_Settings", "⚙️ Настройки и логи" },
            { "Title_Lobby", "LOBBY (Сессия / Блокировка)" },
            { "Title_Optimize", "OPTIMIZE (Оптимизация)" },
            { "Title_Network", "NETWORK (Сеть / VPN)" },
            { "Title_Settings", "SETTINGS (Настройки / Логи)" },
            { "Lobby_Status_Title", "СТАТУС ПОДКЛЮЧЕНИЯ" },
            { "Lobby_Status_Connected", "ПОДКЛЮЧЕНО" },
            { "Lobby_Status_Blocked", "ЗАБЛОКИРОВАНО" },
            { "Lobby_Peers_GameNotRunning", "Активные пиры: Игра не запущена" },
            { "Lobby_Peers_Active", "Пиров: {0}" },
            { "Btn_Block", "🚫 БЛОКИРОВАТЬ" },
            { "Btn_Allow", "✓ РАЗБЛОКИРОВАТЬ" },
            { "Btn_Blocked", "ЗАБЛОКИРОВАНО" },
            { "Btn_Allowed", "РАЗБЛОКИРОВАНО" },
            { "Btn_Solo", "🎯 SOLO СЕССИЯ ({0}с)" },
            { "Chk_P2POnly", "Только P2P (UDP 6672)" },
            { "Chk_AutoUnblock", "Авто-разблок: {0}с" },
            { "Lbl_UnblockTime", "Авто-разблок: {0}с" },
            { "Chk_AutoSolo", "Авто-Solo при входе в сессию" },
            { "Lbl_SuspendTime", "Длительность: {0}с" },
            { "Btn_Flush", "Очистить DNS" },
            { "Btn_ClearCache", "Очистить кэш игры" },
            
            { "Btn_AnalyzeApply", "⚡ АНАЛИЗИРОВАТЬ ПК И ПРИМЕНИТЬ" },
            { "Lbl_Profile", "Профиль:" },
            { "Profile_Custom", "Custom" },
            { "Profile_MaxPerf", "Max Performance (DX10)" },
            { "Profile_Standard", "Standard Optimization (DX11)" },
            { "Profile_Reset", "Reset Defaults" },
            { "Chk_Cores", "Все ядра CPU" },
            { "Chk_NoQueue", "Уменьшить input lag" },
            { "Chk_LowEnd", "Режим слабого ПК (DX10)" },
            { "Chk_NoMemRestrict", "Игнорировать лимит VRAM" },
            { "Chk_HighPriority", "Высокий приоритет процесса" },
            { "Chk_CompatFlags", "Отключить fullscreen optimizations" },
            { "Chk_GameMode", "Игровой режим Windows" },
            { "Lbl_CustomCmd", "Доп. аргументы commandline.txt (через пробел):" },
            { "Btn_GenerateCmd", "Сохранить commandline" },
            { "Btn_OptimizeSettings", "Оптимизировать settings.xml" },
            { "Btn_CleanRam", "Очистить RAM" },
            { "Btn_SuspendBg", "Приостановить фон" },
            { "Btn_ResumeBg", "Возобновить фон" },
            { "Lbl_Specs", "CPU: {0} ядер | RAM: {1}ГБ | GPU: {2}" },
            
            { "Lbl_Adapter", "Адаптер:" },
            { "Btn_ScanAdapters", "🔄" },
            { "Btn_LaunchVpn", "🎮 ЗАПУСТИТЬ ЧЕРЕЗ АДАПТЕР" },
            { "Btn_CheckIp", "Проверить IP" },
            { "Btn_PingTest", "Проверить пинг" },
            { "Btn_RouteRockstar", "Проверить маршруты" },
            { "Lbl_Dns", "DNS:" },
            { "Btn_ResetDns", "Сбросить" },
            { "Lbl_PingStatus", "Пинг: {0}мс | IP: {1}" },
            
            { "Lbl_Language", "Язык:" },
            { "Chk_Autostart", "Запускать с Windows" },
            { "Chk_GlobalHotkey", "Горячая клавиша Ctrl+F1" },
            { "Chk_EnableOverlay", "Оверлей в игре" },
            { "Chk_LockOverlay", "Оверлей: Click-Through режим" },
            { "Lbl_OverlayShow", "Оверлей показывать:" },
            { "Chk_WidgetPeers", "Пиры" },
            { "Chk_WidgetFps", "FPS" },
            { "Chk_WidgetPing", "Пинг" },
            { "Chk_WidgetCpu", "CPU %" },
            { "Chk_WidgetRam", "RAM %" },
            { "Chk_WidgetGpu", "GPU %" },
            { "Chk_WidgetTemp", "Температура CPU/GPU" },
            { "Btn_OpenLog", "Открыть лог" },
            { "Btn_ClearLog", "Очистить лог" },
            { "Btn_RestoreBackups", "Восстановить бэкапы" },
            { "Btn_FullRollback", "⚠️ ПОЛНЫЙ ОТКАТ НАСТРОЕК" },
            { "Lbl_SelectedExe", "SELECTED EXECUTABLE (GTA5.exe / GTAIV.exe) - Drag & Drop Supported" },
            { "Btn_Browse", "ОБЗОР" },
            { "Msg_RollbackConfirm", "Вы уверены, что хотите выполнить полный откат всех настроек и модификаций?" },
            { "Title_Warning", "Предупреждение" },
            { "Title_Success", "Успех" },
            { "Msg_RollbackSuccess", "Все изменения успешно отменены." }
        }},
        { "en", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            { "App_Title", "GTA MULTI-TOOL (SESSION & OPTIMIZATION)" },
            { "Tab_Lobby", "🛡️ Lobby & Firewall" },
            { "Tab_Optimize", "⚡ Optimize" },
            { "Tab_Network", "🌐 Network & VPN" },
            { "Tab_Settings", "⚙️ Settings & Logs" },
            { "Title_Lobby", "LOBBY (Session / Blocking)" },
            { "Title_Optimize", "OPTIMIZE (Optimization)" },
            { "Title_Network", "NETWORK (Network / VPN)" },
            { "Title_Settings", "SETTINGS (Settings / Logs)" },
            { "Lobby_Status_Title", "CONNECTION STATUS" },
            { "Lobby_Status_Connected", "CONNECTED" },
            { "Lobby_Status_Blocked", "BLOCKED" },
            { "Lobby_Peers_GameNotRunning", "Active Peers: Game not running" },
            { "Lobby_Peers_Active", "Peers: {0}" },
            { "Btn_Block", "🚫 BLOCK" },
            { "Btn_Allow", "✓ ALLOW" },
            { "Btn_Blocked", "BLOCKED" },
            { "Btn_Allowed", "ALLOWED" },
            { "Btn_Solo", "🎯 SOLO SESSION ({0}s)" },
            { "Chk_P2POnly", "P2P Only (UDP 6672)" },
            { "Chk_AutoUnblock", "Auto-Unblock: {0}s" },
            { "Lbl_UnblockTime", "Auto-Unblock: {0}s" },
            { "Chk_AutoSolo", "Auto-Solo on session entry" },
            { "Lbl_SuspendTime", "Duration: {0}s" },
            { "Btn_Flush", "Clear DNS" },
            { "Btn_ClearCache", "Clear game cache" },
            
            { "Btn_AnalyzeApply", "⚡ ANALYZE PC & APPLY" },
            { "Lbl_Profile", "Profile:" },
            { "Profile_Custom", "Custom" },
            { "Profile_MaxPerf", "Max Performance (DX10)" },
            { "Profile_Standard", "Standard Optimization (DX11)" },
            { "Profile_Reset", "Reset Defaults" },
            { "Chk_Cores", "All CPU cores" },
            { "Chk_NoQueue", "Reduce input lag" },
            { "Chk_LowEnd", "Low-End PC mode (DX10)" },
            { "Chk_NoMemRestrict", "Ignore VRAM limits" },
            { "Chk_HighPriority", "High process priority" },
            { "Chk_CompatFlags", "Disable fullscreen optimizations" },
            { "Chk_GameMode", "Windows Game Mode" },
            { "Lbl_CustomCmd", "Extra commandline.txt arguments (space separated):" },
            { "Btn_GenerateCmd", "Save commandline" },
            { "Btn_OptimizeSettings", "Optimize settings.xml" },
            { "Btn_CleanRam", "Clean RAM" },
            { "Btn_SuspendBg", "Suspend background apps" },
            { "Btn_ResumeBg", "Resume background apps" },
            { "Lbl_Specs", "CPU: {0} cores | RAM: {1}GB | GPU: {2}" },
            
            { "Lbl_Adapter", "Adapter:" },
            { "Btn_ScanAdapters", "🔄" },
            { "Btn_LaunchVpn", "🎮 LAUNCH VIA ADAPTER" },
            { "Btn_CheckIp", "Check IP" },
            { "Btn_PingTest", "Check Ping" },
            { "Btn_RouteRockstar", "Check Routes" },
            { "Lbl_Dns", "DNS:" },
            { "Btn_ResetDns", "Reset" },
            { "Lbl_PingStatus", "Ping: {0}ms | IP: {1}" },
            
            { "Lbl_Language", "Language:" },
            { "Chk_Autostart", "Start with Windows" },
            { "Chk_GlobalHotkey", "Hotkey Ctrl+F1" },
            { "Chk_EnableOverlay", "In-game overlay" },
            { "Chk_LockOverlay", "Overlay: Click-Through mode" },
            { "Lbl_OverlayShow", "Overlay show:" },
            { "Chk_WidgetPeers", "Peers" },
            { "Chk_WidgetFps", "FPS" },
            { "Chk_WidgetPing", "Ping" },
            { "Chk_WidgetCpu", "CPU %" },
            { "Chk_WidgetRam", "RAM %" },
            { "Chk_WidgetGpu", "GPU %" },
            { "Chk_WidgetTemp", "CPU/GPU Temperature" },
            { "Btn_OpenLog", "Open Log" },
            { "Btn_ClearLog", "Clear Log" },
            { "Btn_RestoreBackups", "Restore backups" },
            { "Btn_FullRollback", "⚠️ FULL RESET OF ALL SETTINGS" },
            { "Lbl_SelectedExe", "SELECTED EXECUTABLE (GTA5.exe / GTAIV.exe) - Drag & Drop Supported" },
            { "Btn_Browse", "BROWSE" },
            { "Msg_RollbackConfirm", "Are you sure you want to perform a full rollback of all settings and modifications?" },
            { "Title_Warning", "Warning" },
            { "Title_Success", "Success" },
            { "Msg_RollbackSuccess", "All modifications have been successfully rolled back." }
        }}
    };

    public static string T(string key, params object[] args)
    {
        var dict = strings.ContainsKey(currentLang) ? strings[currentLang] : strings["en"];
        string val = dict.ContainsKey(key) ? dict[key] : key;
        return args != null && args.Length > 0 ? string.Format(val, args) : val;
    }

    public static void SetLanguage(string lang)
    {
        if (lang.Equals("ru", StringComparison.OrdinalIgnoreCase) || lang.Equals("en", StringComparison.OrdinalIgnoreCase))
        {
            currentLang = lang.ToLower();
            ConfigManager.Set("Language", currentLang);
        }
    }

    public static string GetLanguage()
    {
        return currentLang;
    }

    public static void LoadFromConfig()
    {
        string lang = ConfigManager.Get("Language", "ru");
        SetLanguage(lang);
    }
}
