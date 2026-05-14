using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SCSSdkClient;
using SCSSdkClient.Object;

namespace ETSOverlay
{
    public partial class MainWindow : Window
    {
        private enum GameType
        {
            Unknown,
            Ets,
            Ats
        }

        private bool locked = false;
        private DispatcherTimer? tbTimer;
        private string logFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"TrucksBook\log.txt");
        private string deliveriesFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"TrucksBook\deliveries");
        private string deliveredFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"TrucksBook\delivered");
        private string stateFilePath;
        private string appLogFilePath; // Наш новый файл логов

        private SCSSdkTelemetry? telemetry;

        private bool isRace = false;
        private bool isDelivering = false;
        private bool isGameOnline = false;
        private bool isPaused = false;
        private bool isProfileLoaded = false;
        private int maxSpeedKmh = 0;

        private float lastPlannedDistance = 0;
        private float jobDrivenDistance = 0; // Теперь считаем реально пройденный путь с грузом
        private float _lastTickOdometer = -1; // Предыдущее значение одометра для дельты
        private bool _cargoWasLoaded = false; // Флаг, чтобы засекать одометр именно в момент сцепки
        private bool _forceProfileUnloaded = false; // Жестко глушим телеметрию, если вышли из профиля
        private string _lastJobIdEts = ""; // Железобетонный идентификатор заказа (ETS)
        private string _lastJobIdAts = ""; // Железобетонный идентификатор заказа (ATS)
        private string _currentTelemetryJobId = "";
        private string _lastTelemetryJobId = "";
        private string _tbJobIdEts = "";
        private string _tbJobIdAts = "";
        private Dictionary<string, JobState> _jobStates = new();
        private bool _tbActiveFromFolders = false;
        private bool _tbLogSaysActive = false;
        private DateTime _lastDeliveredTimestamp = DateTime.MinValue;
        private string _lastDeliveredJobIdEts = "";
        private string _lastDeliveredJobIdAts = "";
        private DateTime _deliveredIndicatorUntil = DateTime.MinValue;
        private DateTime _deliveredFromLogUntil = DateTime.MinValue;

        // Трекинг фантомных данных от игры (когда телеметрия не сбросила старый заказ)
        private bool _lastTbSaysNoJob = false;
        private bool _tbSaysNoJob = false;
        private bool _tbFoldersChecked = false;
        private bool _triggerGhostSnapshot = false;
        private float _ghostDistance = -1f;
        private string _ghostDestination = ""; // Для защиты, если километраж совпадет
        private bool _isGhostData = false;

        private DateTime lastTelemetryUpdate = DateTime.Now;
        private bool _isManualMinimize = false;

        // Состояния для сверки
        private bool _telHasActiveJob = false;
        private bool _telHasJobInfo = false;
        private bool _tbHasActiveJob = false;
        private bool _isTbRunning = false;
        private bool _isRecordingBroken = false;
        private bool _awaitingTbResponse = false;

        private const float KmToMiles = 0.621371f;
        private GameType _currentGame = GameType.Unknown;
        private bool _awaitingTelemetryJob = false;

        // Рассинхрон
        private int _desyncSeconds = 0;
        private bool _isDesync = false;

        private bool showDistance = true;
        private bool showBottomInfo = true;
        private bool showRoute = true;
        private bool showProgress = true;
        private double windowOpacity = 0.85;
        private string uiLanguage = "en";

        private class JobState
        {
            public string TelemetryId { get; set; } = "";
            public float DrivenDistance { get; set; }
            public int MaxSpeedKmh { get; set; }
            public bool IsRace { get; set; }
            public bool CargoWasLoaded { get; set; }
        }

        private class AppState
        {
            public double Left { get; set; }
            public double Top { get; set; }
            public bool ShowDistance { get; set; }
            public bool ShowBottomInfo { get; set; }
            public bool ShowRoute { get; set; }
            public bool ShowProgress { get; set; }
            public double WindowOpacity { get; set; }
            public string UiLanguage { get; set; } = "en";
        }

        private class GameState
        {
            public string LastJobId { get; set; } = "";
            public string TbJobId { get; set; } = "";
            public string LastDeliveredJobId { get; set; } = "";
            public Dictionary<string, JobState> JobStates { get; set; } = new();
        }

        public MainWindow()
        {
            InitializeComponent();

            SpeedValue.Width = double.NaN;
            DeliveryType.Margin = new Thickness(0);
            DeliveryType.HorizontalAlignment = HorizontalAlignment.Right;
            MainBorder.Opacity = windowOpacity;

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string folder = Path.Combine(appData, "TruckSimWidget");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            stateFilePath = Path.Combine(Environment.CurrentDirectory, "state.dat");
            appLogFilePath = Path.Combine(folder, "app_log.txt");

            WriteLog("=== OVERLAY STARTED ===");

            LoadState();
            ResetStartupJobMemory();
            UpdatePinIcon();
            CheckStatusAndProcesses();
            ApplyLocalization();

            MouseLeftButtonDown += (s, e) => { if (!locked) DragMove(); };
            LocationChanged += (s, e) => { SaveState(); };

            telemetry = new SCSSdkTelemetry();
            telemetry.Data += Telemetry_Data;

            tbTimer = new DispatcherTimer();
            tbTimer.Interval = TimeSpan.FromSeconds(1);
            tbTimer.Tick += (s, e) => { CheckStatusAndProcesses(); };
            tbTimer.Start();
        }

        // Метод для записи логов
        private void WriteLog(string message)
        {
            try
            {
                if (File.Exists(appLogFilePath) && new FileInfo(appLogFilePath).Length > 5 * 1024 * 1024)
                {
                    File.WriteAllText(appLogFilePath, "");
                }

                File.AppendAllText(appLogFilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
            }
            catch { }
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized && Topmost && !_isManualMinimize)
            {
                Dispatcher.BeginInvoke(new Action(() => { WindowState = WindowState.Normal; }), DispatcherPriority.ApplicationIdle);
            }
            if (WindowState == WindowState.Normal) _isManualMinimize = false;
            base.OnStateChanged(e);
        }

        private void Telemetry_Data(SCSTelemetry data, bool updated)
        {
            lastTelemetryUpdate = DateTime.Now;
            if (!updated) return;

            Dispatcher.Invoke(() =>
            {
                if (!isGameOnline && data.SdkActive) WriteLog("Game telemetry CONNECTED");
                isGameOnline = data.SdkActive;
                isPaused = data.Paused;

                if (isGameOnline && data.TruckValues.CurrentValues.DashboardValues.Odometer > 0)
                {
                    if (!_forceProfileUnloaded) isProfileLoaded = true;
                    else isProfileLoaded = false; // Тракбук сказал, что мы вышли из профиля
                }

                if (isGameOnline)
                {
                    float rawSpeed = data.TruckValues.CurrentValues.DashboardValues.Speed.Value;
                    int currentSpeed = isPaused ? 0 : (int)Math.Round(Math.Abs(rawSpeed) * (UseMiles ? 2.236936f : 3.6f));
                    SpeedValue.Text = currentSpeed.ToString();

                    float distanceFactor = UseMiles ? KmToMiles : 1f;
                    float plannedDistKm = data.JobValues.PlannedDistanceKm;
                    float plannedDist = plannedDistKm * distanceFactor;
                    string telemetryJobId = $"{data.JobValues.CitySource}_{data.JobValues.CityDestination}_{(int)plannedDistKm}";
                    // ПРИОРИТЕТ: сначала ищем уникальный ID из логов TrucksBook, если нет - используем телеметрию
                    string tbJobId = _tbHasActiveJob ? CurrentTbJobId : "";
                    string resolvedJobId = !string.IsNullOrWhiteSpace(tbJobId) ? tbJobId : telemetryJobId;
                    _lastTelemetryJobId = _currentTelemetryJobId;
                    _currentTelemetryJobId = telemetryJobId;

                    // --- ЛОГИКА ФАНТОМНЫХ ДАННЫХ (GHOST DATA) ---
                    if (_triggerGhostSnapshot)
                    {
                        _ghostDistance = plannedDist;
                        _ghostDestination = data.JobValues.CityDestination ?? "";
                        _isGhostData = true;
                        _triggerGhostSnapshot = false;
                        WriteLog($"Ghost snapshot taken. Dist: {_ghostDistance}, Dest: {_ghostDestination}");
                    }

                    if (_isGhostData)
                    {
                        string currentDest = data.JobValues.CityDestination ?? "";
                        if (Math.Abs(plannedDist - _ghostDistance) > 1.0f || currentDest != _ghostDestination)
                        {
                            _isGhostData = false;
                            WriteLog("Telemetry updated (new route detected). Ghost data cleared.");
                        }
                    }

                    bool hasJobInfo = plannedDist > 0.5f && !string.IsNullOrWhiteSpace(data.JobValues.CityDestination);

                    // ЖЁСТКАЯ ПРОВЕРКА ПРИЦЕПА (Игнорируем фейковый CargoLoaded от игры, пока прицепа нет физически на фаркопе)
                    bool isTrailerAttached = data.TrailerValues?.Any(t => t.Attached) ?? false;
                    bool isCargoLoaded = data.JobValues.CargoLoaded && isTrailerAttached;

                    float currentOdo = data.TruckValues.CurrentValues.DashboardValues.Odometer;

                    if (_isGhostData || _forceProfileUnloaded)
                    {
                        hasJobInfo = false;
                        isCargoLoaded = false;
                    }

                    _telHasJobInfo = hasJobInfo;
                    _telHasActiveJob = hasJobInfo && isCargoLoaded;

                    if (_awaitingTelemetryJob && hasJobInfo)
                    {
                        if (_isTbRunning)
                        {
                            if (string.IsNullOrWhiteSpace(CurrentTbJobId))
                            {
                                return;
                            }
                            resolvedJobId = CurrentTbJobId;
                        }

                        _awaitingTelemetryJob = false;
                        CurrentLastJobId = resolvedJobId;
                        LoadOrInitJobState(resolvedJobId);
                    }

                    if (!hasJobInfo)
                    {
                        // Нет заказа вообще
                        if (isDelivering)
                        {
                            isDelivering = false;
                            _cargoWasLoaded = false;
                            CurrentLastJobId = ""; // ЖЕСТКО СБРАСЫВАЕМ ID ЗАКАЗА ПРИ ЕГО КОНЦЕ
                            WriteLog("Job finished, cancelled or Profile Left. Switched to Free Roam / Menu.");
                            ClearJobUI();
                        }
                        maxSpeedKmh = 0;
                        MaxSpeedValue.Text = "0";
                        UpdateDeliveryTypeUI(0);
                        _lastTickOdometer = -1; // Сбрасываем трекинг одометра
                    }
                    else
                    {
                        isDelivering = true;

                        // 1. ПЕРВАЯ ПРОВЕРКА: Железобетонный детект нового заказа по ID (города + дистанция)
                        if (CurrentLastJobId != resolvedJobId)
                        {
                            WriteLog($"New job detected! (Job changed from '{CurrentLastJobId}' to '{resolvedJobId}'). Resetting cargo flags.");
                            CurrentLastJobId = resolvedJobId;
                            _lastTickOdometer = -1;
                            lastPlannedDistance = plannedDist;
                            LoadOrInitJobState(resolvedJobId);
                            SaveJobState();
                        }
                        else if (_lastTelemetryJobId != _currentTelemetryJobId)
                        {
                            LoadOrInitJobState(resolvedJobId);
                        }

                        // 2. ВТОРАЯ ПРОВЕРКА: Смотрим, подцеплен ли прицеп физически
                        if (!isCargoLoaded)
                        {
                            _lastTickOdometer = -1; // ЗАМОРАЖИВАЕМ СЧЕТЧИК КИЛОМЕТРОВ! Прицеп отцеплен.

                            if (!_cargoWasLoaded)
                            {
                                if (jobDrivenDistance > 0 || maxSpeedKmh > 0 || isRace)
                                {
                                    jobDrivenDistance = 0;
                                    maxSpeedKmh = 0;
                                    isRace = false;
                                    MaxSpeedValue.Text = "0";
                                    SaveJobState();
                                }
                                // ФАЗА 1: Едем за грузом (ещё не брали)
                                Route.Text = $"{data.JobValues.CitySource.ToUpper()} -> {data.JobValues.CityDestination.ToUpper()}";
                                DistanceInfo.Text = uiLanguage == "uk" ? "Їду за вантажем..." : "Driving to pickup...";
                                JobProgressBar.Value = 0;
                            }
                            else
                            {
                                // ФАЗА 3: Прицеп отцепили / Заказ приостановлен
                                Route.Text = "ORDER SUSPENDED";
                                int drivenInt = Math.Max(0, (int)Math.Round(jobDrivenDistance));
                                DistanceInfo.Text = $"Trailer detached! ({drivenInt} {GetDistanceUnitShort()} done)";
                            }
                        }
                        else
                        {
                            // ФАЗА 2: Груз загружен (реальная сцепка)
                            if (!_cargoWasLoaded)
                            {
                                // Флаг взводится только один раз при физическом контакте с прицепом
                                _cargoWasLoaded = true;
                                _lastTickOdometer = currentOdo; // Фиксируем одометр для старта дельты
                                WriteLog($"Cargo physically loaded! Tracking started. Total Dist: {plannedDist}{GetDistanceUnitShort()}, Dest: {data.JobValues.CityDestination.ToUpper()}");
                                SaveJobState();
                            }

                            // Считаем пройденный путь дельтами (только пока едем с грузом!)
                            if (_lastTickOdometer > 0 && currentOdo > _lastTickOdometer)
                            {
                                float delta = currentOdo - _lastTickOdometer;
                                if (delta < 500) // Защита от телеметрийных глюков (прыжков)
                                {
                                    jobDrivenDistance += delta * distanceFactor;
                                    if (delta > 0)
                                    {
                                        SaveJobState();
                                    }
                                }
                            }
                            _lastTickOdometer = currentOdo;

                            if (currentSpeed > maxSpeedKmh && !isPaused)
                            {
                                maxSpeedKmh = currentSpeed;
                                MaxSpeedValue.Text = maxSpeedKmh.ToString();
                                int raceThreshold = UseMiles ? 81 : 100;
                                if (maxSpeedKmh > raceThreshold)
                                {
                                    if (!isRace) WriteLog($"Speed exceeded {raceThreshold}{GetSpeedUnitSuffix()} - marked as RACING");
                                    isRace = true;
                                }
                                SaveJobState();
                            }
                            UpdateDeliveryTypeUI(currentSpeed);

                            // Отрисовка прогресса
                            float remaining = (data.NavigationValues.NavigationDistance / 1000f) * distanceFactor;
                            int drivenInt = Math.Max(0, (int)Math.Round(jobDrivenDistance));
                            int totalInt = (int)Math.Round(plannedDist);
                            int remainingInt = Math.Max(0, (int)Math.Floor(remaining));

                    DistanceInfo.Text = uiLanguage == "uk"
                        ? $"{drivenInt} / {totalInt} {GetDistanceUnitShort()} ({remainingInt} залишилось)"
                        : $"{drivenInt} / {totalInt} {GetDistanceUnitShort()} ({remainingInt} left)";
                            JobProgressBar.Value = Math.Max(0, Math.Min(1.0, (double)drivenInt / (totalInt > 0 ? totalInt : 1)));

                            if (!string.IsNullOrEmpty(data.JobValues.CitySource))
                                Route.Text = $"{data.JobValues.CitySource.ToUpper()} -> {data.JobValues.CityDestination.ToUpper()}";
                        }
                    }

                    // Обновление верхнего текста статуса игры
                    if (_isDesync)
                    {
                        GameStatus.Text = LocalizeStatus("DESYNC");
                        GameStatus.Foreground = Brushes.Red;
                    }
                    else if (!isProfileLoaded)
                    {
                        GameStatus.Text = LocalizeStatus("GAME_START");
                        GameStatus.Foreground = Brushes.Orange;
                    }
                    else if (isPaused)
                    {
                        GameStatus.Text = LocalizeStatus("PAUSE");
                        GameStatus.Foreground = Brushes.Yellow;
                    }
                    else
                    {
                        _deliveredIndicatorUntil = DateTime.MinValue;
                        _deliveredFromLogUntil = DateTime.MinValue;
                        GameStatus.Text = LocalizeStatus("GAME_ACTIVE");
                        GameStatus.Foreground = new SolidColorBrush(Color.FromRgb(122, 197, 205));
                    }
                }
            });
        }

        private void CheckStatusAndProcesses()
        {
            bool isEtsRunning = Process.GetProcessesByName("eurotrucks2").Any();
            bool isAtsRunning = Process.GetProcessesByName("amtrucks").Any();
            bool isGameRunning = isEtsRunning || isAtsRunning;

            if (isAtsRunning)
            {
                SetCurrentGame(GameType.Ats);
            }
            else if (isEtsRunning)
            {
                SetCurrentGame(GameType.Ets);
            }
            else
            {
                _currentGame = GameType.Unknown;
            }

            if (isGameRunning && (DateTime.Now - lastTelemetryUpdate).TotalSeconds > 5)
            {
                try { telemetry?.Dispose(); telemetry = new SCSSdkTelemetry(); telemetry.Data += Telemetry_Data; lastTelemetryUpdate = DateTime.Now; WriteLog("Reinitialized telemetry connection"); } catch { }
            }

            var tbProcesses = Process.GetProcessesByName("TB Client");
            bool wasTbRunning = _isTbRunning;
            _isTbRunning = tbProcesses.Length > 0;

            if (_isTbRunning != wasTbRunning)
            {
                WriteLog($"TrucksBook running status changed to: {_isTbRunning}");
            }

            if (!isGameRunning)
            {
                if (isGameOnline || GameStatus.Text != "OFFLINE")
                {
                    WriteLog("Game closed or went offline");
                    WriteLog($"Resetting display and game state");
                    isGameOnline = false;
                    ResetDisplay(true); // Полная очистка интерфейса и состояния
                }
                TbStatus.Text = _isTbRunning ? LocalizeStatus("TB_ONLINE") : LocalizeStatus("TB_OFFLINE");
                TbStatus.Foreground = _isTbRunning ? new SolidColorBrush(Color.FromRgb(82, 193, 79)) : Brushes.Red;
                _tbHasActiveJob = false;
                _isRecordingBroken = false;
                _awaitingTbResponse = false;
                _desyncSeconds = 0;
                _isDesync = false;
                return;
            }

            if (!_isTbRunning)
            {
                TbStatus.Text = LocalizeStatus("TB_OFFLINE");
                TbStatus.Foreground = Brushes.Red;
                _tbHasActiveJob = false;
                _isRecordingBroken = true;
                _desyncSeconds = 0;
                _isDesync = false;
            }
            else
            {
                try
                {
                    _tbHasActiveJob = false;
                    _tbActiveFromFolders = false;
                    _ = GetTbActiveJobFromFolders(out var folderJobId, out var deliveredInfo);
                    _tbFoldersChecked = true;
                    if (deliveredInfo.HasValue)
                    {
                        var (deliveredId, deliveredTime) = deliveredInfo.Value;
                        if (deliveredTime > _lastDeliveredTimestamp)
                        {
                            _lastDeliveredTimestamp = deliveredTime;
                            if (deliveredId == _tbJobIdEts) _lastDeliveredJobIdEts = deliveredId;
                            if (deliveredId == _tbJobIdAts) _lastDeliveredJobIdAts = deliveredId;
                            if (deliveredId == CurrentTbJobId)
                            {
                                CurrentLastDeliveredJobId = deliveredId;
                                _deliveredIndicatorUntil = DateTime.Now.AddSeconds(10);
                                WriteLog($"TB delivered detected: {CurrentLastDeliveredJobId}");
                            }
                        }
                    }

                    if (!_tbHasActiveJob && !string.IsNullOrWhiteSpace(CurrentLastDeliveredJobId))
                    {
                        if (CurrentLastJobId == CurrentLastDeliveredJobId)
                        {
                            // Удаляем доставленный заказ из памяти и из файлов
                            string deliveredKey = GetJobStateKey(CurrentLastDeliveredJobId);
                            if (_jobStates.ContainsKey(deliveredKey))
                            {
                                _jobStates.Remove(deliveredKey);
                                WriteLog($"Delivered job removed from memory: {CurrentLastDeliveredJobId}");
                            }
                            // Удаляем файл доставленного заказа из папки игры
                            DeleteIndividualJobFile(_currentGame, CurrentLastDeliveredJobId);
                            ClearCurrentGameJobData();
                            CurrentLastDeliveredJobId = "";
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(folderJobId))
                    {
                        _tbActiveFromFolders = true;
                    }

                    if (File.Exists(logFilePath))
                    {
                        var logInfo = new FileInfo(logFilePath);
                        using var fs = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        using var sr = new StreamReader(fs, System.Text.Encoding.UTF8, true);
                        string content = sr.ReadToEnd();
                        string[] lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                        if (lines.Length > 0)
                        {
                            string tbState = LocalizeStatus("TB_ONLINE");
                            Brush tbColor = new SolidColorBrush(Color.FromRgb(82, 193, 79));
                            bool isRecordingBroken = false;
                            bool kmLostWarning = false;

                            int lastStartEts = -1, lastFinishEts = -1, lastEmptyEts = -1;
                            int lastStartAts = -1, lastFinishAts = -1, lastEmptyAts = -1;
                            int lastProfileLoadEts = -1, lastProfileLeaveEts = -1;
                            int lastProfileLoadAts = -1, lastProfileLeaveAts = -1;
                            string? latestDeliveryIdEts = null;
                            string? latestDeliveryIdAts = null;
                            GameType logGame = _currentGame;
                            int lastTbErrorIdx = -1;
                            int lastTbConnectedIdx = -1;
                            int lastDisconnectIdx = -1;
                            int lastTelemetryActivityIdx = -1;
                            int lastDeliveredIdx = -1;
                            int lastDecryptOkIdx = -1;
                            int lastUploadedIdx = -1;
                            int lastFoundFilesIdx = -1;
                            int lastDeliveryNotStartedIdx = -1;
                            int lastExitGameIdx = -1;
                            int disconnectsAfterDelivery = 0;

                            int lastAtsLaunch = Array.FindLastIndex(lines, l => l.Contains("Launched game (ats)", StringComparison.OrdinalIgnoreCase));
                            int lastEtsLaunch = Array.FindLastIndex(lines, l => l.Contains("Launched game (ets)", StringComparison.OrdinalIgnoreCase));
                            if (lastAtsLaunch > lastEtsLaunch)
                            {
                                SetCurrentGame(GameType.Ats);
                                logGame = GameType.Ats;
                            }
                            else if (lastEtsLaunch > lastAtsLaunch)
                            {
                                SetCurrentGame(GameType.Ets);
                                logGame = GameType.Ets;
                            }

                            int logScopeStart = _currentGame == GameType.Ats ? lastAtsLaunch : lastEtsLaunch;
                            if (logScopeStart < 0) logScopeStart = 0;

                            for (int i = 0; i < lines.Length; i++)
                            {
                                string line = lines[i];
                                bool isInScope = i >= logScopeStart;
                                if (line.Contains("Launched game (ats)", StringComparison.OrdinalIgnoreCase)) logGame = GameType.Ats;
                                else if (line.Contains("Launched game (ets)", StringComparison.OrdinalIgnoreCase)) logGame = GameType.Ets;
                                else if (line.Contains("American Truck Simulator", StringComparison.OrdinalIgnoreCase)) logGame = GameType.Ats;
                                else if (line.Contains("Euro Truck Simulator", StringComparison.OrdinalIgnoreCase)) logGame = GameType.Ets;

                                if (isInScope)
                                {
                                    if (line.Contains("Drive without Client")) kmLostWarning = true;
                                    if (Regex.IsMatch(line, "Connected\\s+to\\s+telemetry", RegexOptions.IgnoreCase))
                                    {
                                        lastTbConnectedIdx = i;
                                        lastTelemetryActivityIdx = i;
                                    }
                                    if (line.Contains("Delivery not started with client", StringComparison.OrdinalIgnoreCase))
                                    {
                                        lastDeliveryNotStartedIdx = i;
                                    }
                                    if (line.Contains("Login failed") || line.Contains("could not be resolved")) { tbState = LocalizeStatus("TB_OFFLINE_NET"); tbColor = Brushes.Red; isRecordingBroken = true; lastTbErrorIdx = i; }
                                    else if (line.Contains("Problem with processes thread", StringComparison.OrdinalIgnoreCase)
                                        || line.Contains("turn off client", StringComparison.OrdinalIgnoreCase)
                                        || Regex.IsMatch(line, "Disconnected\\s+from\\s+telemetry", RegexOptions.IgnoreCase))
                                    {
                                        lastDisconnectIdx = i;
                                    }
                                    if (line.Contains("Exit game", StringComparison.OrdinalIgnoreCase) || line.Contains("Exit the game", StringComparison.OrdinalIgnoreCase))
                                    {
                                        lastExitGameIdx = i;
                                    }
                                }

                                // Отслеживаем старты и стопы заказов
                                // Убедитесь, что парсинг логов является авторитетным источником для активного ID заказа
                                if (line.Contains("- Starting delivery") || line.Contains("- Delivery exist"))
                                {
                                    lastTelemetryActivityIdx = i;
                                    if (logGame == GameType.Ats) lastStartAts = i;
                                    else lastStartEts = i;
                                    var extracted = TryExtractTbJobId(line);
                                    if (!string.IsNullOrWhiteSpace(extracted))
                                    {
                                        if (logGame == GameType.Ats) latestDeliveryIdAts = extracted;
                                        else latestDeliveryIdEts = extracted;
                                    }
                                }
                                if (line.Contains("File (", StringComparison.OrdinalIgnoreCase) && line.Contains(") is yours", StringComparison.OrdinalIgnoreCase))
                                {
                                    var extracted = TryExtractTbJobId(line);
                                    if (!string.IsNullOrWhiteSpace(extracted))
                                    {
                                        if (logGame == GameType.Ats) latestDeliveryIdAts = extracted;
                                        else latestDeliveryIdEts = extracted;
                                    }
                                }
                                if (line.Contains("- Delivered") || line.Contains("- Cancelled"))
                                {
                                    lastDeliveredIdx = i;
                                    lastTelemetryActivityIdx = i;
                                    if (logGame == GameType.Ats) lastFinishAts = i;
                                    else lastFinishEts = i;
                                    if (line.Contains("- Delivered"))
                                    {
                                        if (logGame == _currentGame)
                                        {
                                            _deliveredFromLogUntil = DateTime.Now.AddSeconds(10);
                                        }
                                    }
                                    else
                                    {
                                        if (logGame == _currentGame)
                                        {
                                            _deliveredFromLogUntil = DateTime.MinValue;
                                            _deliveredIndicatorUntil = DateTime.MinValue;
                                            _lastDeliveredTimestamp = DateTime.MinValue;
                                            CurrentLastDeliveredJobId = "";
                                        }
                                    }
                                }
                                if (line.Contains("Found owner files: 0") || line.Contains("Found files: 0"))
                                {
                                    if (logGame == GameType.Ats) lastEmptyAts = i;
                                    else lastEmptyEts = i;
                                }

                                if (line.Contains("Found files: 1", StringComparison.OrdinalIgnoreCase))
                                {
                                    lastFoundFilesIdx = i;
                                }

                                if (line.Contains("Decrypt delivered ok", StringComparison.OrdinalIgnoreCase))
                                {
                                    lastDecryptOkIdx = i;
                                }

                                if (line.Contains("File is uploaded", StringComparison.OrdinalIgnoreCase))
                                {
                                    lastUploadedIdx = i;
                                }

                                if (line.Contains("Tollgate", StringComparison.OrdinalIgnoreCase)
                                    || line.Contains("Ferry", StringComparison.OrdinalIgnoreCase))
                                {
                                    lastTelemetryActivityIdx = i;
                                }

                                if (lastDeliveredIdx >= 0 && Regex.IsMatch(line, "Disconnected\\s+from\\s+telemetry", RegexOptions.IgnoreCase) && i > lastDeliveredIdx)
                                {
                                    disconnectsAfterDelivery++;
                                }

                                // Отслеживаем состояние профиля!
                                if (line.Contains("New profile selected"))
                                {
                                    if (logGame == GameType.Ats) lastProfileLoadAts = i;
                                    else lastProfileLoadEts = i;
                                }
                                if (line.Contains("Leave profile"))
                                {
                                    if (logGame == GameType.Ats) lastProfileLeaveAts = i;
                                    else lastProfileLeaveEts = i;
                                }
                            }

                            int lastStart = _currentGame == GameType.Ats ? lastStartAts : lastStartEts;
                            int lastFinish = _currentGame == GameType.Ats ? lastFinishAts : lastFinishEts;
                            int lastEmpty = _currentGame == GameType.Ats ? lastEmptyAts : lastEmptyEts;
                            int lastProfileLoad = _currentGame == GameType.Ats ? lastProfileLoadAts : lastProfileLoadEts;
                            int lastProfileLeave = _currentGame == GameType.Ats ? lastProfileLeaveAts : lastProfileLeaveEts;

                            if (lastTbConnectedIdx > Math.Max(lastTbErrorIdx, lastDisconnectIdx))
                            {
                                isRecordingBroken = false;
                                if (tbState != LocalizeStatus("TB_OFFLINE_NET"))
                                {
                                    tbState = LocalizeStatus("TB_ONLINE");
                                    tbColor = new SolidColorBrush(Color.FromRgb(82, 193, 79));
                                }
                            }

                            if (!isRecordingBroken && lastDisconnectIdx > Math.Max(lastTelemetryActivityIdx, lastTbConnectedIdx))
                            {
                                isRecordingBroken = true;
                                tbState = LocalizeStatus("TB_ERROR");
                                tbColor = Brushes.Red;
                            }

                            if (!isRecordingBroken && lastDeliveryNotStartedIdx > Math.Max(lastTelemetryActivityIdx, lastTbConnectedIdx))
                            {
                                isRecordingBroken = true;
                                tbState = LocalizeStatus("TB_ERROR");
                                tbColor = Brushes.Red;
                            }

                            if (!isRecordingBroken && lastDeliveredIdx >= 0 && lastDeliveredIdx > Math.Max(lastDecryptOkIdx, lastUploadedIdx)
                                && lastDeliveredIdx >= lastStart && lastDeliveredIdx >= lastProfileLoad)
                            {
                                isRecordingBroken = true;
                                tbState = LocalizeStatus("TB_ERROR");
                                tbColor = Brushes.Red;
                            }

                            if (!isRecordingBroken && lastFoundFilesIdx > lastDeliveredIdx && lastDeliveredIdx >= 0 && lastDeliveredIdx >= lastStart)
                            {
                                isRecordingBroken = true;
                                tbState = LocalizeStatus("TB_ERROR");
                                tbColor = Brushes.Red;
                            }

                            if (!isRecordingBroken && lastExitGameIdx > lastDeliveredIdx && lastDeliveredIdx >= 0 && lastDeliveredIdx >= lastStart
                                && lastUploadedIdx < lastDeliveredIdx && lastDecryptOkIdx < lastDeliveredIdx
                                && disconnectsAfterDelivery >= 2)
                            {
                                isRecordingBroken = true;
                                tbState = LocalizeStatus("TB_ERROR");
                                tbColor = Brushes.Red;
                            }

                            _isRecordingBroken = isRecordingBroken;

                            if (!_isRecordingBroken && kmLostWarning)
                            {
                                tbState = LocalizeStatus("TB_KM_LOST");
                                tbColor = Brushes.Orange;
                            }

                            // Вычисляем, есть ли сейчас работа по версии Тракбука
                            int maxEnd = Math.Max(lastFinish, lastEmpty);
                            maxEnd = Math.Max(maxEnd, lastProfileLeave);
                            maxEnd = Math.Max(maxEnd, lastProfileLoad); // Если загрузили профиль и не было старта

                            bool tbSaysNoJob = (maxEnd > lastStart) || (lastStart == -1);
                            _tbSaysNoJob = tbSaysNoJob;

                            // Триггерим снимок фантомных данных, если ТБ перешел в статус "без заказа"
                            if (tbSaysNoJob && !_lastTbSaysNoJob)
                            {
                                _triggerGhostSnapshot = true;
                                WriteLog("TB reports no active job. Triggering ghost data snapshot.");
                            }
                            _lastTbSaysNoJob = tbSaysNoJob;

                            // Если последним действием был ВЫХОД из профиля, жестко глушим заказ
                            _forceProfileUnloaded = (lastProfileLeave > lastProfileLoad);

                            bool logSaysActive = (lastStart > maxEnd);
                            if (_forceProfileUnloaded) logSaysActive = false;
                            _tbLogSaysActive = logSaysActive;

                            if (!string.IsNullOrWhiteSpace(latestDeliveryIdEts)) _tbJobIdEts = latestDeliveryIdEts;
                            if (!string.IsNullOrWhiteSpace(latestDeliveryIdAts)) _tbJobIdAts = latestDeliveryIdAts;

                            if (_currentGame == GameType.Ats && !string.IsNullOrWhiteSpace(latestDeliveryIdAts))
                            {
                                CurrentTbJobId = latestDeliveryIdAts;
                            }
                            else if (_currentGame == GameType.Ets && !string.IsNullOrWhiteSpace(latestDeliveryIdEts))
                            {
                                CurrentTbJobId = latestDeliveryIdEts;
                            }

                            if (!string.IsNullOrWhiteSpace(CurrentTbJobId) && CurrentLastJobId != CurrentTbJobId && logSaysActive)
                            {
                                CurrentLastJobId = CurrentTbJobId;
                                LoadOrInitJobState(CurrentLastJobId);
                                SaveJobState();
                            }

                            _tbHasActiveJob = logSaysActive && !string.IsNullOrWhiteSpace(CurrentTbJobId);
                            _awaitingTbResponse = _isTbRunning && !_tbHasActiveJob && !_tbSaysNoJob && !_forceProfileUnloaded;

                            if (_tbHasActiveJob)
                            {
                                if (!_isRecordingBroken)
                                {
                                    tbState = LocalizeStatus("TB_ONLINE");
                                    tbColor = new SolidColorBrush(Color.FromRgb(82, 193, 79));
                                }
                            }
                            else if (_tbHasActiveJob && !logSaysActive && _deliveredFromLogUntil <= DateTime.Now)
                            {
                                tbState = LocalizeStatus("TB_NOT_RECORDING");
                                tbColor = Brushes.Red;
                                _isRecordingBroken = true;
                            }

                            if (_tbHasActiveJob && _isGhostData)
                            {
                                _isGhostData = false;
                                _ghostDistance = -1f;
                                _ghostDestination = "";
                                _triggerGhostSnapshot = false;
                                WriteLog("TB reports active job. Ghost data cleared.");
                            }

                            // Проверяем рассинхрон только если мы в профиле, иначе нам всё равно
                            if (_awaitingTbResponse || !isProfileLoaded || _forceProfileUnloaded)
                            {
                                _desyncSeconds = 0;
                            }
                            else if (_isTbRunning && !_isRecordingBroken && isGameRunning)
                            {
                                if (isDelivering && !_telHasActiveJob)
                                {
                                    if (_cargoWasLoaded)
                                    {
                                        _desyncSeconds++;
                                    }
                                    else
                                    {
                                        _desyncSeconds = 0; // Едем за грузом, рассинхрона нет
                                    }
                                }
                                else if (_telHasActiveJob != _tbHasActiveJob)
                                {
                                    _desyncSeconds++;
                                }
                                else
                                {
                                    _desyncSeconds = 0;
                                }
                            }
                            else { _desyncSeconds = 0; }

                            _isDesync = _desyncSeconds >= 5; // Даем 5 секунд, чтобы игра точно успела прогрузить телеметрию

                            TbStatus.Text = tbState;
                            TbStatus.Foreground = tbColor;
                        }
                    }
                    else
                    {
                        WriteLog($"TB log not found: {logFilePath}");
                        TbStatus.Text = LocalizeStatus("TB_OFFLINE");
                        TbStatus.Foreground = Brushes.Red;
                    }
                }
                catch (Exception ex)
                {
                    WriteLog($"Error reading TB log: {ex.Message}");
                }
            }

            if (!isGameRunning) return;

            string oldStatus = StatusValue.Text;

            if (!_isTbRunning && _telHasActiveJob) UpdateStatusUI(LocalizeStatus("TB_CLOSED_NO_REC"), Brushes.Red, false);
            else if (_isRecordingBroken && _telHasActiveJob) UpdateStatusUI(LocalizeStatus("NOT_RECORDING"), Brushes.Red, false);
            else if (_awaitingTbResponse || (!isProfileLoaded && !_telHasActiveJob)) UpdateStatusUI(LocalizeStatus("PROFILE_MENU"), Brushes.Orange, false);
            else if (_isDesync) UpdateStatusUI(uiLanguage == "uk" ? "РОЗСИНХРОН: ГРА ≠ TB" : "DESYNC: GAME ≠ TB", Brushes.Red, false);
            else if (_isRecordingBroken) UpdateStatusUI(LocalizeStatus("TB_ERROR_CHECK"), Brushes.Red, false);
            else if (_forceProfileUnloaded) UpdateStatusUI(LocalizeStatus("PROFILE_MENU"), Brushes.Orange, false); // Показываем, что мы вышли из профиля
            else if ((_deliveredIndicatorUntil > DateTime.Now || _deliveredFromLogUntil > DateTime.Now) && isPaused && !_telHasActiveJob) UpdateStatusUI(LocalizeStatus("DELIVERED"), new SolidColorBrush(Color.FromRgb(82, 193, 79)), false);
            else if (_telHasActiveJob && !_tbHasActiveJob && _tbSaysNoJob) UpdateStatusUI(LocalizeStatus("KM_NOT_REC"), Brushes.Red, false);
            else if (!isDelivering) UpdateStatusUI(LocalizeStatus("FREE_ROAM"), Brushes.White, false);
            else if (isDelivering && !_telHasActiveJob)
            {
                if (_cargoWasLoaded) UpdateStatusUI(LocalizeStatus("TRAILER_DETACHED"), Brushes.Orange, false);
                else if (_telHasJobInfo) UpdateStatusUI(LocalizeStatus("DRIVING_TO_PICKUP"), new SolidColorBrush(Color.FromRgb(122, 197, 205)), false);
                else UpdateStatusUI(LocalizeStatus("WAIT_TB"), Brushes.Orange, false);
            }
            else if (isPaused && _telHasActiveJob) UpdateStatusUI(uiLanguage == "uk" ? "ПАУЗА (Меню)" : "PAUSED (Menu)", Brushes.Yellow, false);
            else if (_tbHasActiveJob) UpdateStatusUI(LocalizeStatus("RECORDING_KM"), new SolidColorBrush(Color.FromRgb(82, 193, 79)), true);
            else UpdateStatusUI(LocalizeStatus("WAIT_TB"), Brushes.Orange, false);

            if (StatusValue.Text != oldStatus)
            {
                WriteLog($"Central UI Status changed to: {StatusValue.Text}");
            }
        }

        private void UpdateStatusUI(string text, Brush? color, bool showCheck)
        {
            StatusValue.Text = text;
            StatusValue.Foreground = color ?? Brushes.White;
            StatusCheckIconContainer.Visibility = showCheck ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ClearAllJobData()
        {
            maxSpeedKmh = 0; isRace = false; lastPlannedDistance = 0; jobDrivenDistance = 0; _lastTickOdometer = -1; _cargoWasLoaded = false;
            _lastJobIdEts = ""; _lastJobIdAts = ""; // Очищаем ID тоже
            _tbJobIdEts = ""; _tbJobIdAts = "";
            _lastDeliveredJobIdEts = ""; _lastDeliveredJobIdAts = "";
            _jobStates.Clear();
            MaxSpeedValue.Text = "0"; Route.Text = uiLanguage == "uk" ? "МАРШРУТ: НЕ ВИЗНАЧЕНО" : "ROUTE: NOT DEFINED"; DistanceInfo.Text = GetZeroDistanceText(); JobProgressBar.Value = 0;
            UpdateDeliveryTypeUI(0); SaveState();
        }

        private void ClearJobUI()
        {
            Route.Text = uiLanguage == "uk" ? "МАРШРУТ: НЕ ВИЗНАЧЕНО" : "ROUTE: NOT DEFINED";
            DistanceInfo.Text = GetZeroDistanceText();
            JobProgressBar.Value = 0;
        }

        private void ClearCurrentGameJobData()
        {
            maxSpeedKmh = 0; isRace = false; lastPlannedDistance = 0; jobDrivenDistance = 0; _lastTickOdometer = -1; _cargoWasLoaded = false;
            CurrentLastJobId = "";
            MaxSpeedValue.Text = "0";
            UpdateDeliveryTypeUI(0);
            ClearJobUI();
            SaveState();
        }

        private void ResetCurrentGameSessionState()
        {
            jobDrivenDistance = 0;
            maxSpeedKmh = 0;
            isRace = false;
            _cargoWasLoaded = false;
            _lastTickOdometer = -1;
            MaxSpeedValue.Text = "0";
            UpdateDeliveryTypeUI(0);
            ClearJobUI();
        }

        private void LoadOrInitJobState(string jobId)
        {
            if (string.IsNullOrWhiteSpace(jobId)) return;

            string stateKey = GetJobStateKey(jobId);

            if (!_jobStates.TryGetValue(stateKey, out var state))
            {
                // Сначала пытаемся загрузить из отдельного файла заказа
                state = LoadIndividualJobFile(_currentGame, jobId);

                if (state == null)
                {
                    // Если файла нет, инициализируем новый заказ
                    state = new JobState
                    {
                        TelemetryId = jobId,
                        DrivenDistance = 0,
                        MaxSpeedKmh = 0,
                        IsRace = false,
                        CargoWasLoaded = false
                    };
                }
                _jobStates[stateKey] = state;
            }

            jobDrivenDistance = state.DrivenDistance;
            // ВАЖНО: Максимальная скорость загружается ТОЛЬКО если груз уже был загружен в этом заказе
            if (state.CargoWasLoaded)
            {
                maxSpeedKmh = state.MaxSpeedKmh;
                isRace = state.IsRace;
            }
            else
            {
                maxSpeedKmh = 0;
                isRace = false;
            }
            _cargoWasLoaded = state.CargoWasLoaded;
            MaxSpeedValue.Text = maxSpeedKmh.ToString();
            _lastTickOdometer = -1;
            SaveJobState();
        }

        private void LoadCurrentGameJobState()
        {
            if (_tbHasActiveJob && !string.IsNullOrWhiteSpace(CurrentTbJobId))
            {
                if (CurrentLastJobId != CurrentTbJobId)
                {
                    CurrentLastJobId = CurrentTbJobId;
                }
                WriteLog($"Loading job state from TB ID: {CurrentLastJobId}");
                LoadOrInitJobState(CurrentLastJobId);
                return;
            }

            WriteLog("No active TB job to load, clearing current job data");
            ClearCurrentGameJobData();
        }

        private void SaveJobState()
        {
            if (!string.IsNullOrWhiteSpace(CurrentTbJobId) && _currentTelemetryJobId == CurrentLastJobId)
            {
                CurrentLastJobId = CurrentTbJobId;
            }

            if (string.IsNullOrWhiteSpace(CurrentLastJobId))
            {
                SaveState();
                return;
            }

            string stateKey = GetJobStateKey(CurrentLastJobId);
            var jobState = new JobState
            {
                TelemetryId = CurrentLastJobId,
                DrivenDistance = jobDrivenDistance,
                MaxSpeedKmh = maxSpeedKmh,
                IsRace = isRace,
                CargoWasLoaded = _cargoWasLoaded
            };
            _jobStates[stateKey] = jobState;

            // Сохраняем отдельный файл заказа в папке игры
            SaveIndividualJobFile(_currentGame, CurrentLastJobId, jobState);

            SaveState();
        }
        private string? TryExtractTbJobId(string line)
        {
            // Формат: "- Delivery exist, continue 1778553643 (roma > roma)"
            var match = Regex.Match(line, "Delivery exist,\\s*continue\\s+(?<id>\\d+)", RegexOptions.IgnoreCase);
            if (match.Success) return match.Groups["id"].Value.Trim();

            // Формат: "- Starting delivery 1778553643 (roma > roma)"
            match = Regex.Match(line, "Starting delivery\\s+(?<id>\\d+)", RegexOptions.IgnoreCase);
            if (match.Success) return match.Groups["id"].Value.Trim();

            // Формат JSON: "delivery_id": "1778553643"
            match = Regex.Match(line, "\"delivery_id\"\\s*:\\s*\"(?<id>[^\"]+)\"", RegexOptions.IgnoreCase);
            if (match.Success) return match.Groups["id"].Value;

            // Формат с : или =
            match = Regex.Match(line, "delivery_id\\s*[:=]\\s*(?<id>[^\\s]+)", RegexOptions.IgnoreCase);
            if (match.Success) return match.Groups["id"].Value.Trim();

            // Формат: "File (1778553643) is yours"
            match = Regex.Match(line, "File\\s*\\((?<id>\\d+)\\)\\s*is\\s*yours", RegexOptions.IgnoreCase);
            if (match.Success) return match.Groups["id"].Value.Trim();

            return null;
        }

        private bool GetTbActiveJobFromFolders(out string? jobId, out (string jobId, DateTime lastWriteTime)? deliveredInfo)
        {
            jobId = null;
            deliveredInfo = null;
            try
            {
                if (Directory.Exists(deliveriesFolderPath))
                {
                    var file = Directory.GetFiles(deliveriesFolderPath, "*", SearchOption.TopDirectoryOnly)
                        .Select(Path.GetFileName)
                        .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));

                    if (!string.IsNullOrWhiteSpace(file))
                    {
                        jobId = Path.GetFileNameWithoutExtension(file);
                        return true;
                    }
                }

                if (Directory.Exists(deliveredFolderPath))
                {
                    var deliveredFile = Directory.GetFiles(deliveredFolderPath, "*", SearchOption.TopDirectoryOnly)
                        .Select(path => new FileInfo(path))
                        .OrderByDescending(info => info.LastWriteTimeUtc)
                        .FirstOrDefault();

                    if (deliveredFile != null)
                    {
                        deliveredInfo = (Path.GetFileNameWithoutExtension(deliveredFile.Name), deliveredFile.LastWriteTimeUtc.ToLocalTime());
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLog($"Error reading TB deliveries folder: {ex.Message}");
            }

            return false;
        }

        private void SaveState()
        {
            try
            {
                var state = new AppState
                {
                    Left = Left,
                    Top = Top,
                    ShowDistance = showDistance,
                    ShowBottomInfo = showBottomInfo,
                    ShowRoute = showRoute,
                    ShowProgress = showProgress,
                    WindowOpacity = windowOpacity,
                    UiLanguage = uiLanguage
                };

                var json = JsonSerializer.Serialize(state);
                File.WriteAllText(stateFilePath, json);

                SaveGameState(GameType.Ets);
                SaveGameState(GameType.Ats);
            }
            catch { }
        }

        private void LoadState()
        {
            try
            {
                if (File.Exists(stateFilePath))
                {
                    string content = File.ReadAllText(stateFilePath);
                    if (content.TrimStart().StartsWith("{", StringComparison.Ordinal))
                    {
                        var state = JsonSerializer.Deserialize<AppState>(content);
                        if (state != null)
                        {
                            Left = state.Left;
                            Top = state.Top;
                            showDistance = state.ShowDistance;
                            showBottomInfo = state.ShowBottomInfo;
                            showRoute = state.ShowRoute;
                            showProgress = state.ShowProgress;
                            windowOpacity = state.WindowOpacity;
                            uiLanguage = string.IsNullOrWhiteSpace(state.UiLanguage) ? "en" : state.UiLanguage;
                            if (windowOpacity <= 0 || windowOpacity > 1)
                            {
                                windowOpacity = 0.85;
                            }
                            // При старте не загружаем сохранённые заказы, только настройки интерфейса.
                        }
                    }
                    else
                    {
                        string[] parts = content.Split('|');

                        if (parts.Length >= 9) { bool.TryParse(parts[8], out _cargoWasLoaded); }
                        if (parts.Length >= 10)
                        {
                            _lastJobIdEts = parts[9];
                            _lastJobIdAts = parts[9];
                            _tbJobIdEts = parts[9];
                            _tbJobIdAts = parts[9];
                            _lastDeliveredJobIdEts = "";
                            _lastDeliveredJobIdAts = "";
                        }

                        if (parts.Length >= 4)
                        {
                            float.TryParse(parts[0], out lastPlannedDistance);
                            float.TryParse(parts[1], out jobDrivenDistance);
                            if (jobDrivenDistance > 10000) jobDrivenDistance = 0;

                            if (_cargoWasLoaded)
                            {
                                int.TryParse(parts[2], out maxSpeedKmh);
                                MaxSpeedValue.Text = maxSpeedKmh.ToString();
                                bool.TryParse(parts[3], out isRace);
                            }
                            else
                            {
                                maxSpeedKmh = 0;
                                MaxSpeedValue.Text = "0";
                                isRace = false;
                            }
                        }
                        if (parts.Length >= 6)
                        {
                            if (double.TryParse(parts[4], out var l)) Left = l;
                            if (double.TryParse(parts[5], out var t)) Top = t;
                        }
                        if (parts.Length >= 8)
                        {
                            bool.TryParse(parts[6], out showDistance);
                            bool.TryParse(parts[7], out showBottomInfo);
                        }
                    }
                }
            }
            catch { }

            ChkShowDistance.IsChecked = showDistance; ChkShowBottomInfo.IsChecked = showBottomInfo;
            ChkShowRoute.IsChecked = showRoute;
            ChkShowProgress.IsChecked = showProgress;
            OpacitySlider.Value = windowOpacity * 100;
            OpacityValue.Text = $"{(int)Math.Round(windowOpacity * 100)}%";
            MainBorder.Opacity = windowOpacity;
            ApplyLanguageSelection();
            ApplyLocalization();
            ApplyVisibilitySettings();
        }

        private void ResetDisplay(bool clearJobState = true)
        {
            SpeedValue.Text = "0"; 
            isDelivering = false; 
            isPaused = false; 
            isProfileLoaded = false;
            GameStatus.Text = LocalizeStatus("GAME_OFFLINE"); 
            GameStatus.Foreground = Brushes.Gray;
            UpdateStatusUI(uiLanguage == "uk" ? "Очікування гри..." : "Waiting for game...", Brushes.Gray, false);

            // ВСЕГДА сбрасываем максимальную скорость при выходе из игры
            maxSpeedKmh = 0; 
            MaxSpeedValue.Text = "0"; 
            isRace = false; 
            _lastTickOdometer = -1;

            _forceProfileUnloaded = false;
            _isGhostData = false;
            _ghostDistance = -1f;
            _ghostDestination = "";
            _triggerGhostSnapshot = false;
            UpdateDeliveryTypeUI(0);

            if (clearJobState)
            {
                _cargoWasLoaded = false;
                _lastJobIdEts = ""; 
                _lastJobIdAts = ""; 
                _tbJobIdEts = ""; 
                _tbJobIdAts = "";
                _lastDeliveredJobIdEts = ""; 
                _lastDeliveredJobIdAts = "";
                ClearJobUI();
            }

            _telHasActiveJob = false;
            _telHasJobInfo = false;
        }

        private void UpdateDeliveryTypeUI(int currentSpeed)
        {
            if (isGameOnline && _telHasActiveJob)
            {
                if (isRace)
                {
                    DeliveryType.Text = uiLanguage == "uk" ? "ПЕРЕГОНИ" : "RACING";
                    DeliveryType.Foreground = Brushes.Red;
                }
                else
                {
                    DeliveryType.Text = uiLanguage == "uk" ? "РЕАЛ" : "REAL";
                    DeliveryType.Foreground = new SolidColorBrush(Color.FromRgb(82, 193, 79));
                }
            }
            else
            {
                DeliveryType.Text = uiLanguage == "uk" ? "Н/Д" : "N/A";
                DeliveryType.Foreground = Brushes.Gray;
            }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e) => SettingsPanel.Visibility = Visibility.Visible;
        private void BtnCloseSettings_Click(object sender, RoutedEventArgs e) { SettingsPanel.Visibility = Visibility.Collapsed; SaveState(); }
        private void Settings_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            showDistance = ChkShowDistance.IsChecked == true;
            showBottomInfo = ChkShowBottomInfo.IsChecked == true;
            showRoute = ChkShowRoute.IsChecked == true;
            showProgress = ChkShowProgress.IsChecked == true;
            ApplyVisibilitySettings();
        }

        private void ApplyVisibilitySettings()
        {
            DistanceInfo.Visibility = showDistance ? Visibility.Visible : Visibility.Collapsed;
            BottomInfoGrid.Visibility = showBottomInfo ? Visibility.Visible : Visibility.Collapsed;
            Route.Visibility = showRoute ? Visibility.Visible : Visibility.Collapsed;
            ProgressBorder.Visibility = showProgress ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded) return;
            windowOpacity = OpacitySlider.Value / 100.0; // Keep opacity mapping linear
            MainBorder.Opacity = windowOpacity; // 100% means fully opaque
            OpacityValue.Text = $"{(int)Math.Round(OpacitySlider.Value)}%";
            SaveState();
        }

        private void LanguageSelector_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            if (LanguageSelector.SelectedItem is System.Windows.Controls.ComboBoxItem item && item.Tag is string tag)
            {
                uiLanguage = tag;
                ApplyLocalization();
                SaveState();
            }
        }

        private void ApplyLanguageSelection()
        {
            foreach (var item in LanguageSelector.Items)
            {
                if (item is System.Windows.Controls.ComboBoxItem comboItem && comboItem.Tag is string tag)
                {
                    if (tag == uiLanguage)
                    {
                        LanguageSelector.SelectedItem = comboItem;
                        break;
                    }
                }
            }
        }

        private void ApplyLocalization()
        {
            bool isUk = uiLanguage == "uk";

            SettingsTitle.Text = isUk ? "НАЛАШТУВАННЯ" : "SETTINGS";
            SettingsDoneButton.Content = isUk ? "Готово" : "Done";
            LanguageLabel.Text = isUk ? "Мова" : "Language";
            OpacityLabel.Text = isUk ? "Прозорість" : "Opacity";
            ChkShowDistance.Content = isUk ? "Показувати пройдену дистанцію" : "Show driving distance";
            ChkShowBottomInfo.Content = isUk ? "Показувати швидкість і тип доставки" : "Show speed and delivery type";
            ChkShowRoute.Content = isUk ? "Показувати маршрут" : "Show route";
            ChkShowProgress.Content = isUk ? "Показувати прогрес-бар" : "Show progress bar";

            StatusLabel.Text = isUk ? "Статус: " : "Status: ";
            SpeedHeader.Text = isUk ? "ШВИДКІСТЬ" : "SPEED";
            MaxHeader.Text = isUk ? "МАКС" : "MAX";
            TypeHeader.Text = isUk ? "ТИП" : "TYPE";
            SpeedUnit.Text = GetSpeedUnitText();
            MaxUnit.Text = GetSpeedUnitText();

            UpdateDeliveryTypeUI(0);
            UpdateDistanceInfoForLanguage();
            UpdateStatusTextForLanguage();
        }

        private string LocalizeStatus(string key)
        {
            bool isUk = uiLanguage == "uk";
            return key switch
            {
                "TB_OFFLINE" => isUk ? "TB • ОФЛАЙН" : "TB • OFFLINE",
                "TB_OFFLINE_NET" => isUk ? "TB • ОФЛАЙН (МЕРЕЖА)" : "TB • OFFLINE (NET)",
                "TB_ERROR" => isUk ? "TB • ПОМИЛКА" : "TB • ERROR",
                "TB_KM_LOST" => isUk ? "TB • ВТРАТА КМ" : "TB • KM LOST WARNING",
                "TB_NOT_RECORDING" => isUk ? "TB • НЕ ЗАПИСУЄ" : "TB • NOT RECORDING",
                "TB_ONLINE" => isUk ? "TB • ОНЛАЙН" : "TB • ONLINE",
                "GAME_OFFLINE" => isUk ? "ОФЛАЙН" : "OFFLINE",
                "GAME_ACTIVE" => isUk ? "ГРА: АКТИВНА" : "GAME: ACTIVE",
                "GAME_START" => isUk ? "ГРА: СТАРТ" : "GAME: START",
                "PAUSE" => isUk ? "ПАУЗА" : "PAUSE",
                "DESYNC" => isUk ? "РОЗСИНХРОН!" : "DESYNC ALERT!",
                "FREE_ROAM" => isUk ? "ВІЛЬНИЙ РЕЖИМ" : "Free Roam",
                "WAIT_TB" => isUk ? "Очікування TB..." : "Waiting for TB...",
                "DRIVING_TO_PICKUP" => isUk ? "Їду за вантажем..." : "Driving to pickup...",
                "TRAILER_DETACHED" => isUk ? "Причеп від'єднано!" : "Trailer detached!",
                "RECORDING_KM" => isUk ? "Запис кілометрів" : "Recording km",
                "TB_CLOSED_NO_REC" => isUk ? "TB ЗАКРИТИЙ! НЕ ЗАП." : "TB CLOSED! NO REC",
                "NOT_RECORDING" => isUk ? "НЕ ЗАПИСУЄТЬСЯ!" : "NOT RECORDING!",
                "TB_ERROR_CHECK" => isUk ? "Помилка TB: Перевірте клієнт" : "TB Error: Check Client",
                "PROFILE_MENU" => isUk ? "Меню профілю" : "Profile Menu",
                "DELIVERED" => isUk ? "ДОСТАВЛЕНО" : "DELIVERED",
                "KM_NOT_REC" => isUk ? "КМ НЕ ЗАП (TB)" : "KM NOT REC (TB)",
                _ => key
            };
        }

        private void UpdateStatusTextForLanguage()
        {
            var status = StatusValue.Text;
            StatusValue.Text = status switch
            {
                "TB CLOSED! NO REC" => LocalizeStatus("TB_CLOSED_NO_REC"),
                "NOT RECORDING!" => LocalizeStatus("NOT_RECORDING"),
                "DESYNC: GAME ≠ TB" => uiLanguage == "uk" ? "РОЗСИНХРОН: ГРА ≠ TB" : status,
                "TB Error: Check Client" => LocalizeStatus("TB_ERROR_CHECK"),
                "Profile Menu" => LocalizeStatus("PROFILE_MENU"),
                "Free Roam" => LocalizeStatus("FREE_ROAM"),
                "Trailer detached!" => LocalizeStatus("TRAILER_DETACHED"),
                "Driving to pickup..." => LocalizeStatus("DRIVING_TO_PICKUP"),
                "PAUSED (Menu)" => uiLanguage == "uk" ? "ПАУЗА (Меню)" : status,
                "Recording km" => LocalizeStatus("RECORDING_KM"),
                "Waiting for TB..." => LocalizeStatus("WAIT_TB"),
                "KM NOT REC (TB)" => LocalizeStatus("KM_NOT_REC"),
                "DELIVERED" => LocalizeStatus("DELIVERED"),
                _ => status
            };

            GameStatus.Text = GameStatus.Text switch
            {
                "OFFLINE" => LocalizeStatus("GAME_OFFLINE"),
                "GAME: ACTIVE" => LocalizeStatus("GAME_ACTIVE"),
                "GAME: START" => LocalizeStatus("GAME_START"),
                "PAUSE" => LocalizeStatus("PAUSE"),
                "DESYNC ALERT!" => LocalizeStatus("DESYNC"),
                _ => GameStatus.Text
            };

            TbStatus.Text = TbStatus.Text switch
            {
                "TB • OFFLINE" => LocalizeStatus("TB_OFFLINE"),
                "TB • OFFLINE (NET)" => LocalizeStatus("TB_OFFLINE_NET"),
                "TB • ERROR" => LocalizeStatus("TB_ERROR"),
                "TB • KM LOST WARNING" => LocalizeStatus("TB_KM_LOST"),
                "TB • NOT RECORDING" => LocalizeStatus("TB_NOT_RECORDING"),
                "TB • ONLINE" => LocalizeStatus("TB_ONLINE"),
                _ => TbStatus.Text
            };

            if (Route.Text == "ROUTE: NOT DEFINED" || Route.Text == "МАРШРУТ: НЕ ВИЗНАЧЕНО")
            {
                Route.Text = uiLanguage == "uk" ? "МАРШРУТ: НЕ ВИЗНАЧЕНО" : "ROUTE: NOT DEFINED";
            }

            if (DistanceInfo.Text == "0 / 0 km" || DistanceInfo.Text == "0 / 0 км")
            {
                DistanceInfo.Text = uiLanguage == "uk" ? "0 / 0 км" : "0 / 0 km";
            }
        }

        private void UpdateDistanceInfoForLanguage()
        {
            if (string.IsNullOrWhiteSpace(DistanceInfo.Text)) return;

            if (DistanceInfo.Text == "0 / 0 km" || DistanceInfo.Text == "0 / 0 км" || DistanceInfo.Text == "0 / 0 mi" || DistanceInfo.Text == "0 / 0 миль")
            {
                DistanceInfo.Text = GetZeroDistanceText();
                return;
            }

            if (uiLanguage == "uk")
            {
                DistanceInfo.Text = DistanceInfo.Text
                    .Replace(" km (", " км (")
                    .Replace(" mi (", " миль (")
                    .Replace(" km ", " км ")
                    .Replace(" mi ", " миль ")
                    .Replace(" left)", " залишилось)");
            }
            else
            {
                DistanceInfo.Text = DistanceInfo.Text
                    .Replace(" км (", " km (")
                    .Replace(" миль (", " mi (")
                    .Replace(" км ", " km ")
                    .Replace(" миль ", " mi ")
                    .Replace(" залишилось)", " left)");
            }
        }

        private bool UseMiles => _currentGame == GameType.Ats;
        private string CurrentLastJobId
        {
            get => _currentGame == GameType.Ats ? _lastJobIdAts : _lastJobIdEts;
            set
            {
                if (_currentGame == GameType.Ats)
                {
                    _lastJobIdAts = value;
                }
                else
                {
                    _lastJobIdEts = value;
                }
            }
        }

        private string CurrentTbJobId
        {
            get => _currentGame == GameType.Ats ? _tbJobIdAts : _tbJobIdEts;
            set
            {
                if (_currentGame == GameType.Ats)
                {
                    _tbJobIdAts = value;
                }
                else
                {
                    _tbJobIdEts = value;
                }
            }
        }

        private string CurrentLastDeliveredJobId
        {
            get => _currentGame == GameType.Ats ? _lastDeliveredJobIdAts : _lastDeliveredJobIdEts;
            set
            {
                if (_currentGame == GameType.Ats)
                {
                    _lastDeliveredJobIdAts = value;
                }
                else
                {
                    _lastDeliveredJobIdEts = value;
                }
            }
        }

        private void SetCurrentGame(GameType game)
        {
            if (_currentGame == game || game == GameType.Unknown) return;

            if (_currentGame != GameType.Unknown)
            {
                SaveGameState(_currentGame);
            }

            // СНАЧАЛА: сбрасываем состояние СТАРОЙ игры
            ResetCurrentGameSessionState();

            _currentGame = game;

            SpeedUnit.Text = GetSpeedUnitText();
            MaxUnit.Text = GetSpeedUnitText();
            UpdateDistanceInfoForLanguage();
            _awaitingTelemetryJob = true;
            LoadCurrentGameJobState();
            WriteLog($"Detected game: {_currentGame}");
        }

        private void ResetStartupJobMemory()
        {
            _lastJobIdEts = "";
            _lastJobIdAts = "";
            _tbJobIdEts = "";
            _tbJobIdAts = "";
            _lastDeliveredJobIdEts = "";
            _lastDeliveredJobIdAts = "";
            _lastDeliveredTimestamp = DateTime.MinValue;
            _deliveredIndicatorUntil = DateTime.MinValue;
            _deliveredFromLogUntil = DateTime.MinValue;
            _jobStates.Clear();
        }

        private string GetSpeedUnitText()
        {
            return UseMiles ? (uiLanguage == "uk" ? "м/год" : "mph") : (uiLanguage == "uk" ? "км/год" : "km/h");
        }

        private string GetSpeedUnitSuffix()
        {
            return UseMiles ? "mph" : "km/h";
        }

        private string GetDistanceUnitShort()
        {
            return UseMiles ? (uiLanguage == "uk" ? "миль" : "mi") : (uiLanguage == "uk" ? "км" : "km");
        }

        private string GetZeroDistanceText()
        {
            return $"0 / 0 {GetDistanceUnitShort()}";
        }

        private string GetJobStateKey(string jobId)
        {
            if (string.IsNullOrWhiteSpace(jobId)) return jobId;
            string prefix = _currentGame == GameType.Ats ? "ats" : "ets";
            return $"{prefix}:{jobId}";
        }

        private string GetGameStateFolder(GameType game)
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string folder = Path.Combine(appData, "TruckSimWidget", game == GameType.Ats ? "ATS" : "ETS");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            return folder;
        }

        private string GetGameStatePath(GameType game)
        {
            return Path.Combine(GetGameStateFolder(game), "state.json");
        }

        private void SaveGameState(GameType game)
        {
            try
            {
                var state = new GameState
                {
                    LastJobId = game == GameType.Ats ? _lastJobIdAts : _lastJobIdEts,
                    TbJobId = game == GameType.Ats ? _tbJobIdAts : _tbJobIdEts,
                    LastDeliveredJobId = game == GameType.Ats ? _lastDeliveredJobIdAts : _lastDeliveredJobIdEts,
                    // ВАЖНО: Сохраняем только активные заказы (с загруженным грузом)
                    JobStates = _jobStates
                        .Where(kvp => kvp.Key.StartsWith(game == GameType.Ats ? "ats:" : "ets:", StringComparison.Ordinal) && kvp.Value.CargoWasLoaded)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                };

                var json = JsonSerializer.Serialize(state);
                File.WriteAllText(GetGameStatePath(game), json);
            }
            catch { }
        }

        private void LoadGameState(GameType game)
        {
            try
            {
                string path = GetGameStatePath(game);
                if (!File.Exists(path)) return;

                string content = File.ReadAllText(path);
                if (!content.TrimStart().StartsWith("{", StringComparison.Ordinal)) return;

                var state = JsonSerializer.Deserialize<GameState>(content);
                if (state == null) return;

                if (game == GameType.Ats)
                {
                    _lastJobIdAts = state.LastJobId ?? "";
                    _tbJobIdAts = state.TbJobId ?? "";
                    _lastDeliveredJobIdAts = state.LastDeliveredJobId ?? "";
                }
                else
                {
                    _lastJobIdEts = state.LastJobId ?? "";
                    _tbJobIdEts = state.TbJobId ?? "";
                    _lastDeliveredJobIdEts = state.LastDeliveredJobId ?? "";
                }

                if (state.JobStates != null)
                {
                    // ВАЖНО: Загружаем только активные заказы
                    foreach (var kvp in state.JobStates)
                    {
                        if (kvp.Value.CargoWasLoaded)
                        {
                            _jobStates[kvp.Key] = kvp.Value;
                        }
                    }
                }

                // Дополнительно загружаем отдельные файлы заказов из папки игры
                LoadIndividualJobFiles(game);
            }
            catch { }
        }

        // Загружает все отдельные файлы заказов из папки игры
        private void LoadIndividualJobFiles(GameType game)
        {
            try
            {
                string folder = GetGameStateFolder(game);
                if (!Directory.Exists(folder)) return;

                var jobFiles = Directory.GetFiles(folder, "job_*.json");
                foreach (var jobFile in jobFiles)
                {
                    try
                    {
                        string content = File.ReadAllText(jobFile);
                        var jobState = JsonSerializer.Deserialize<JobState>(content);
                        if (jobState != null && jobState.CargoWasLoaded)
                        {
                            string stateKey = GetJobStateKey(jobState.TelemetryId);
                            if (!_jobStates.ContainsKey(stateKey))
                            {
                                _jobStates[stateKey] = jobState;
                                WriteLog($"Loaded job from individual file: {jobState.TelemetryId}");
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        // Сохраняет отдельный заказ в файл JSON в папке игры
        private void SaveIndividualJobFile(GameType game, string jobId, JobState jobState)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(jobId) || jobState == null) return;

                string folder = GetGameStateFolder(game);
                // Используем jobId как имя файла для TrucksBook ID или генерируем безопасное имя
                string safeJobId = Regex.Replace(jobId, "[^a-zA-Z0-9_-]", "_");
                string filePath = Path.Combine(folder, $"job_{safeJobId}.json");

                var json = JsonSerializer.Serialize(jobState, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
                WriteLog($"Job state saved to file: {safeJobId}");
            }
            catch (Exception ex)
            {
                WriteLog($"Error saving individual job file: {ex.Message}");
            }
        }

        // Загружает отдельный заказ из файла JSON
        private JobState? LoadIndividualJobFile(GameType game, string jobId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(jobId)) return null;

                string folder = GetGameStateFolder(game);
                string safeJobId = Regex.Replace(jobId, "[^a-zA-Z0-9_-]", "_");
                string filePath = Path.Combine(folder, $"job_{safeJobId}.json");

                if (!File.Exists(filePath)) return null;

                string content = File.ReadAllText(filePath);
                var jobState = JsonSerializer.Deserialize<JobState>(content);
                if (jobState != null)
                {
                    WriteLog($"Job state loaded from file: {safeJobId}");
                }
                return jobState;
            }
            catch (Exception ex)
            {
                WriteLog($"Error loading individual job file: {ex.Message}");
                return null;
            }
        }

        // Удаляет файл заказа при доставке
        private void DeleteIndividualJobFile(GameType game, string jobId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(jobId)) return;

                string folder = GetGameStateFolder(game);
                string safeJobId = Regex.Replace(jobId, "[^a-zA-Z0-9_-]", "_");
                string filePath = Path.Combine(folder, $"job_{safeJobId}.json");

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    WriteLog($"Job file deleted: {safeJobId}");
                }
            }
            catch (Exception ex)
            {
                WriteLog($"Error deleting job file: {ex.Message}");
            }
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) { _isManualMinimize = true; WindowState = WindowState.Minimized; }
        private void BtnClose_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
        private void BtnTopmost_Click(object sender, RoutedEventArgs e) { Topmost = !Topmost; UpdatePinIcon(); }
        private void UpdatePinIcon() => BtnTopmost.Opacity = Topmost ? 1.0 : 0.4;
        protected override void OnClosed(EventArgs e) { WriteLog("=== OVERLAY CLOSED ==="); SaveState(); telemetry?.Dispose(); base.OnClosed(e); }
    }
}