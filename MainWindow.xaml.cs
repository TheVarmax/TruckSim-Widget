using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        private bool locked = false;
        private DispatcherTimer? tbTimer;
        private string logFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"TrucksBook\log.txt");
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
        private string _lastJobId = ""; // Железобетонный идентификатор заказа

        // Трекинг фантомных данных от игры (когда телеметрия не сбросила старый заказ)
        private bool _lastTbSaysNoJob = false;
        private bool _triggerGhostSnapshot = false;
        private float _ghostDistance = -1f;
        private string _ghostDestination = ""; // Для защиты, если километраж совпадет
        private bool _isGhostData = false;

        private DateTime lastTelemetryUpdate = DateTime.Now;
        private bool _isManualMinimize = false;

        // Состояния для сверки
        private bool _telHasActiveJob = false;
        private bool _tbHasActiveJob = false;
        private bool _isTbRunning = false;
        private bool _isRecordingBroken = false;

        // Рассинхрон
        private int _desyncSeconds = 0;
        private bool _isDesync = false;

        private bool showDistance = true;
        private bool showBottomInfo = true;

        public MainWindow()
        {
            InitializeComponent();

            SpeedValue.Width = double.NaN;
            DeliveryType.Margin = new Thickness(0);
            DeliveryType.HorizontalAlignment = HorizontalAlignment.Right;

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string folder = Path.Combine(appData, "ETSOverlay");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            stateFilePath = Path.Combine(folder, "state.dat");
            appLogFilePath = Path.Combine(folder, "app_log.txt");

            WriteLog("=== OVERLAY STARTED ===");

            LoadState();
            UpdatePinIcon();
            CheckStatusAndProcesses();

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
                    int currentSpeedKmh = isPaused ? 0 : (int)Math.Round(Math.Abs(rawSpeed) * 3.6f);
                    SpeedValue.Text = currentSpeedKmh.ToString();

                    float plannedDistKm = data.JobValues.PlannedDistanceKm;
                    string currentJobId = $"{data.JobValues.CitySource}_{data.JobValues.CityDestination}_{(int)plannedDistKm}";

                    // --- ЛОГИКА ФАНТОМНЫХ ДАННЫХ (GHOST DATA) ---
                    if (_triggerGhostSnapshot)
                    {
                        _ghostDistance = plannedDistKm;
                        _ghostDestination = data.JobValues.CityDestination ?? "";
                        _isGhostData = true;
                        _triggerGhostSnapshot = false;
                        WriteLog($"Ghost snapshot taken. Dist: {_ghostDistance}, Dest: {_ghostDestination}");
                    }

                    if (_isGhostData)
                    {
                        string currentDest = data.JobValues.CityDestination ?? "";
                        if (Math.Abs(plannedDistKm - _ghostDistance) > 1.0f || currentDest != _ghostDestination)
                        {
                            _isGhostData = false;
                            WriteLog("Telemetry updated (new route detected). Ghost data cleared.");
                        }
                    }

                    bool hasJobInfo = plannedDistKm > 0.5f && !string.IsNullOrWhiteSpace(data.JobValues.CityDestination);

                    // ЖЁСТКАЯ ПРОВЕРКА ПРИЦЕПА (Игнорируем фейковый CargoLoaded от игры, пока прицепа нет физически на фаркопе)
                    bool isTrailerAttached = data.TrailerValues?.Any(t => t.Attached) ?? false;
                    bool isCargoLoaded = data.JobValues.CargoLoaded && isTrailerAttached;

                    float currentOdo = data.TruckValues.CurrentValues.DashboardValues.Odometer;

                    if (_isGhostData || _forceProfileUnloaded)
                    {
                        hasJobInfo = false;
                        isCargoLoaded = false;
                    }

                    _telHasActiveJob = hasJobInfo && isCargoLoaded;

                    if (!hasJobInfo)
                    {
                        // Нет заказа вообще
                        if (isDelivering)
                        {
                            isDelivering = false;
                            _cargoWasLoaded = false;
                            _lastJobId = ""; // ЖЕСТКО СБРАСЫВАЕМ ID ЗАКАЗА ПРИ ЕГО КОНЦЕ
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
                        if (_lastJobId != currentJobId)
                        {
                            WriteLog($"New job detected! (Job changed from '{_lastJobId}' to '{currentJobId}'). Resetting cargo flags.");
                            _lastJobId = currentJobId;
                            lastPlannedDistance = plannedDistKm;
                            jobDrivenDistance = 0;
                            maxSpeedKmh = 0;
                            MaxSpeedValue.Text = "0";
                            isRace = false;
                            _cargoWasLoaded = false; // <-- ТОТ САМЫЙ ПРАВИЛЬНЫЙ СБРОС ФЛАГА!
                            _lastTickOdometer = -1;
                            SaveState();
                        }

                        // 2. ВТОРАЯ ПРОВЕРКА: Смотрим, подцеплен ли прицеп физически
                        if (!isCargoLoaded)
                        {
                            _lastTickOdometer = -1; // ЗАМОРАЖИВАЕМ СЧЕТЧИК КИЛОМЕТРОВ! Прицеп отцеплен.

                            if (!_cargoWasLoaded)
                            {
                                // ФАЗА 1: Едем за грузом (ещё не брали)
                                Route.Text = $"{data.JobValues.CitySource.ToUpper()} -> {data.JobValues.CityDestination.ToUpper()}";
                                DistanceInfo.Text = "Driving to pickup...";
                                JobProgressBar.Value = 0;
                            }
                            else
                            {
                                // ФАЗА 3: Прицеп отцепили / Заказ приостановлен
                                Route.Text = "ORDER SUSPENDED";
                                int drivenInt = Math.Max(0, (int)Math.Round(jobDrivenDistance));
                                DistanceInfo.Text = $"Trailer detached! ({drivenInt} km done)";
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
                                WriteLog($"Cargo physically loaded! Tracking started. Total Dist: {plannedDistKm}km, Dest: {data.JobValues.CityDestination.ToUpper()}");
                                SaveState();
                            }

                            // Считаем пройденный путь дельтами (только пока едем с грузом!)
                            if (_lastTickOdometer > 0 && currentOdo > _lastTickOdometer)
                            {
                                float delta = currentOdo - _lastTickOdometer;
                                if (delta < 500) // Защита от телеметрийных глюков (прыжков)
                                {
                                    jobDrivenDistance += delta;
                                }
                            }
                            _lastTickOdometer = currentOdo;

                            if (currentSpeedKmh > maxSpeedKmh && !isPaused)
                            {
                                maxSpeedKmh = currentSpeedKmh;
                                MaxSpeedValue.Text = maxSpeedKmh.ToString();
                                if (maxSpeedKmh > 100)
                                {
                                    if (!isRace) WriteLog("Speed exceeded 100km/h - marked as RACING");
                                    isRace = true;
                                }
                                SaveState();
                            }
                            UpdateDeliveryTypeUI(currentSpeedKmh);

                            // Отрисовка прогресса
                            float remainingKm = data.NavigationValues.NavigationDistance / 1000f;
                            int drivenInt = Math.Max(0, (int)Math.Round(jobDrivenDistance));
                            int totalInt = (int)Math.Round(plannedDistKm);

                            DistanceInfo.Text = $"{drivenInt} / {totalInt} km ({(int)Math.Round(remainingKm)} left)";
                            JobProgressBar.Value = Math.Max(0, Math.Min(1.0, (double)drivenInt / (totalInt > 0 ? totalInt : 1)));

                            if (!string.IsNullOrEmpty(data.JobValues.CitySource))
                                Route.Text = $"{data.JobValues.CitySource.ToUpper()} -> {data.JobValues.CityDestination.ToUpper()}";
                        }
                    }

                    // Обновление верхнего текста статуса игры
                    if (_isDesync)
                    {
                        GameStatus.Text = "DESYNC ALERT!";
                        GameStatus.Foreground = Brushes.Red;
                    }
                    else if (!isProfileLoaded)
                    {
                        GameStatus.Text = "GAME: START";
                        GameStatus.Foreground = Brushes.Orange;
                    }
                    else if (isPaused)
                    {
                        GameStatus.Text = "PAUSE";
                        GameStatus.Foreground = Brushes.Yellow;
                    }
                    else
                    {
                        GameStatus.Text = "GAME: ACTIVE";
                        GameStatus.Foreground = new SolidColorBrush(Color.FromRgb(122, 197, 205));
                    }
                }
            });
        }

        private void CheckStatusAndProcesses()
        {
            var gameProcesses = Process.GetProcessesByName("eurotrucks2").Concat(Process.GetProcessesByName("amtrucks"));
            bool isGameRunning = gameProcesses.Any();

            if (isGameRunning && (DateTime.Now - lastTelemetryUpdate).TotalSeconds > 5)
            {
                try { telemetry?.Dispose(); telemetry = new SCSSdkTelemetry(); telemetry.Data += Telemetry_Data; lastTelemetryUpdate = DateTime.Now; WriteLog("Reinitialized telemetry connection"); } catch { }
            }

            if (!isGameRunning)
            {
                if (isGameOnline || GameStatus.Text != "OFFLINE")
                {
                    WriteLog("Game closed or went offline");
                    isGameOnline = false;
                    ResetDisplay();
                }
            }

            var tbProcesses = Process.GetProcessesByName("TB Client");
            bool wasTbRunning = _isTbRunning;
            _isTbRunning = tbProcesses.Length > 0;

            if (_isTbRunning != wasTbRunning)
            {
                WriteLog($"TrucksBook running status changed to: {_isTbRunning}");
            }

            if (!_isTbRunning)
            {
                TbStatus.Text = "TB • OFFLINE";
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
                    if (File.Exists(logFilePath))
                    {
                        using var fs = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        using var sr = new StreamReader(fs);
                        string content = sr.ReadToEnd();
                        string[] lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                        if (lines.Length > 0)
                        {
                            string tbState = "TB • ONLINE";
                            Brush tbColor = new SolidColorBrush(Color.FromRgb(82, 193, 79));
                            bool isRecordingBroken = false;
                            bool kmLostWarning = false;

                            int lastStart = -1, lastFinish = -1, lastEmpty = -1;
                            int lastProfileLoad = -1, lastProfileLeave = -1;

                            int lastEventIdx = Array.FindLastIndex(lines, l => l.Contains("Launched game") || l.Contains("Connected to telemetry") || l.Contains("TrucksBook Client launch"));
                            if (lastEventIdx == -1) lastEventIdx = 0;

                            for (int i = lastEventIdx; i < lines.Length; i++)
                            {
                                string line = lines[i];
                                if (line.Contains("Drive without Client")) kmLostWarning = true;
                                if (line.Contains("Login failed") || line.Contains("could not be resolved")) { tbState = "TB • OFFLINE (NET)"; tbColor = Brushes.Red; isRecordingBroken = true; }
                                else if (line.Contains("Problem with processes thread") || line.Contains("turn off client") || line.Contains("Disconnected from telemetry")) { tbState = "TB • ERROR"; tbColor = Brushes.Red; isRecordingBroken = true; }

                                // Отслеживаем старты и стопы заказов
                                if (line.Contains("- Starting delivery") || line.Contains("- Delivery exist")) lastStart = i;
                                if (line.Contains("- Delivered") || line.Contains("- Cancelled")) lastFinish = i;
                                if (line.Contains("Found owner files: 0") || line.Contains("Found files: 0")) lastEmpty = i;

                                // Отслеживаем состояние профиля!
                                if (line.Contains("New profile selected")) lastProfileLoad = i;
                                if (line.Contains("Leave profile")) lastProfileLeave = i;
                            }

                            _isRecordingBroken = isRecordingBroken;

                            if (!_isRecordingBroken && kmLostWarning)
                            {
                                tbState = "TB • KM LOST WARNING";
                                tbColor = Brushes.Orange;
                            }

                            // Вычисляем, есть ли сейчас работа по версии Тракбука
                            int maxEnd = Math.Max(lastFinish, lastEmpty);
                            maxEnd = Math.Max(maxEnd, lastProfileLeave);
                            maxEnd = Math.Max(maxEnd, lastProfileLoad); // Если загрузили профиль и не было старта

                            bool tbSaysNoJob = (maxEnd > lastStart) || (lastStart == -1);

                            // Триггерим снимок фантомных данных, если ТБ перешел в статус "без заказа"
                            if (tbSaysNoJob && !_lastTbSaysNoJob)
                            {
                                _triggerGhostSnapshot = true;
                                WriteLog("TB reports no active job. Triggering ghost data snapshot.");
                            }
                            _lastTbSaysNoJob = tbSaysNoJob;

                            // Если последним действием был ВЫХОД из профиля, жестко глушим заказ
                            _forceProfileUnloaded = (lastProfileLeave > lastProfileLoad);

                            _tbHasActiveJob = (lastStart > maxEnd);
                            if (_forceProfileUnloaded) _tbHasActiveJob = false;

                            // Проверяем рассинхрон только если мы в профиле, иначе нам всё равно
                            if (_isTbRunning && !_isRecordingBroken && isGameRunning && !_forceProfileUnloaded)
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
                }
                catch (Exception ex)
                {
                    WriteLog($"Error reading TB log: {ex.Message}");
                }
            }

            if (!isGameRunning) return;

            string oldStatus = StatusValue.Text;

            if (!_isTbRunning && _telHasActiveJob) UpdateStatusUI("TB CLOSED! NO REC", Brushes.Red, false);
            else if (_isRecordingBroken && _telHasActiveJob) UpdateStatusUI("NOT RECORDING!", Brushes.Red, false);
            else if (_isDesync) UpdateStatusUI("DESYNC: GAME ≠ TB", Brushes.Red, false);
            else if (_isRecordingBroken) UpdateStatusUI("TB Error: Check Client", Brushes.Red, false);
            else if (_forceProfileUnloaded) UpdateStatusUI("Profile Menu", Brushes.Orange, false); // Показываем, что мы вышли из профиля
            else if (!isDelivering) UpdateStatusUI("Free Roam", Brushes.White, false);
            else if (isDelivering && !_telHasActiveJob)
            {
                if (_cargoWasLoaded) UpdateStatusUI("Trailer detached!", Brushes.Orange, false);
                else UpdateStatusUI("Driving to pickup...", new SolidColorBrush(Color.FromRgb(122, 197, 205)), false);
            }
            else if (isPaused && _telHasActiveJob) UpdateStatusUI("PAUSED (Menu)", Brushes.Yellow, false);
            else if (_tbHasActiveJob) UpdateStatusUI("Recording km", new SolidColorBrush(Color.FromRgb(82, 193, 79)), true);
            else UpdateStatusUI("Waiting for TB...", Brushes.Orange, false);

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
            _lastJobId = ""; // Очищаем ID тоже
            MaxSpeedValue.Text = "0"; Route.Text = "ROUTE: NOT DEFINED"; DistanceInfo.Text = "0 / 0 km"; JobProgressBar.Value = 0;
            UpdateDeliveryTypeUI(0); SaveState();
        }

        private void ClearJobUI()
        {
            Route.Text = "ROUTE: NOT DEFINED";
            DistanceInfo.Text = "0 / 0 km";
            JobProgressBar.Value = 0;
        }

        private void SaveState()
        {
            try
            {
                // Добавили _lastJobId в самый конец сейв-файла
                File.WriteAllText(stateFilePath, $"{lastPlannedDistance}|{jobDrivenDistance}|{maxSpeedKmh}|{isRace}|{Left}|{Top}|{showDistance}|{showBottomInfo}|{_cargoWasLoaded}|{_lastJobId}");
            }
            catch { }
        }

        private void LoadState()
        {
            try
            {
                if (File.Exists(stateFilePath))
                {
                    string[] parts = File.ReadAllText(stateFilePath).Split('|');

                    if (parts.Length >= 9) { bool.TryParse(parts[8], out _cargoWasLoaded); }
                    if (parts.Length >= 10) { _lastJobId = parts[9]; } // Загружаем наш ID

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
                    if (parts.Length >= 6) { double l, t; if (double.TryParse(parts[4], out l)) Left = l; if (double.TryParse(parts[5], out t)) Top = t; }
                    if (parts.Length >= 8) { bool.TryParse(parts[6], out showDistance); bool.TryParse(parts[7], out showBottomInfo); }
                }
            }
            catch { }

            ChkShowDistance.IsChecked = showDistance; ChkShowBottomInfo.IsChecked = showBottomInfo;
            ApplyVisibilitySettings();
        }

        private void ResetDisplay()
        {
            SpeedValue.Text = "0"; isDelivering = false; isPaused = false; isProfileLoaded = false;
            GameStatus.Text = "OFFLINE"; GameStatus.Foreground = Brushes.Gray;
            UpdateStatusUI("Waiting for game...", Brushes.Gray, false);
            maxSpeedKmh = 0; MaxSpeedValue.Text = "0"; isRace = false; _cargoWasLoaded = false; _lastTickOdometer = -1;
            _forceProfileUnloaded = false;
            _isGhostData = false;
            _ghostDistance = -1f;
            _ghostDestination = "";
            _triggerGhostSnapshot = false;
            _lastJobId = ""; // Сбрасываем и здесь на всякий
            UpdateDeliveryTypeUI(0);

            ClearJobUI();
            _telHasActiveJob = false;
        }

        private void UpdateDeliveryTypeUI(int currentSpeed)
        {
            if (isGameOnline && _telHasActiveJob)
            {
                DeliveryType.Text = isRace ? "RACING" : "REAL";
                DeliveryType.Foreground = isRace ? Brushes.Red : new SolidColorBrush(Color.FromRgb(82, 193, 79));
            }
            else { DeliveryType.Text = "N/A"; DeliveryType.Foreground = Brushes.Gray; }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e) => SettingsPanel.Visibility = Visibility.Visible;
        private void BtnCloseSettings_Click(object sender, RoutedEventArgs e) { SettingsPanel.Visibility = Visibility.Collapsed; SaveState(); }
        private void Settings_Changed(object sender, RoutedEventArgs e) { if (!IsLoaded) return; showDistance = ChkShowDistance.IsChecked == true; showBottomInfo = ChkShowBottomInfo.IsChecked == true; ApplyVisibilitySettings(); }
        private void ApplyVisibilitySettings() { DistanceInfo.Visibility = showDistance ? Visibility.Visible : Visibility.Collapsed; BottomInfoGrid.Visibility = showBottomInfo ? Visibility.Visible : Visibility.Collapsed; }
        private void BtnMinimize_Click(object sender, RoutedEventArgs e) { _isManualMinimize = true; WindowState = WindowState.Minimized; }
        private void BtnClose_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
        private void BtnTopmost_Click(object sender, RoutedEventArgs e) { Topmost = !Topmost; UpdatePinIcon(); }
        private void UpdatePinIcon() => BtnTopmost.Opacity = Topmost ? 1.0 : 0.4;
        protected override void OnClosed(EventArgs e) { WriteLog("=== OVERLAY CLOSED ==="); SaveState(); telemetry?.Dispose(); base.OnClosed(e); }
    }
}