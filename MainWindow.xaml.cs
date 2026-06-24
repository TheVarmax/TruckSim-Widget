using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
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

        // Auto-update constants
        private const string GitHubApiUrl = "https://api.github.com/repos/TheVarmax/TruckSim-Widget/releases/latest";
        private const string DonateUrl = "https://donate.maksym.uk";
        private const string SupportEmail = "info@maksym.uk";
        public bool _isCheckingUpdate = false;

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
        private bool _trailerWasAttachedBeforeLoading = false; // Был ли прицеплен свой прицеп до загрузки груза
        private int _navZeroTicks = 0; // Для отслеживания сброса GPS-маршрута
        private int _navPositiveTicks = 0; // Для отслеживания возврата GPS-маршрута
        private float _lastValidNavDist = 0; // Для отслеживания внезапного обрыва маршрута
        private bool _forceProfileUnloaded = false; // Жестко глушим телеметрию, если вышли из профиля
        private int _lastDeliveredLogIndex = -1; // Для предотвращения спама DELIVERED из логов
        private HashSet<string> _cancelledJobs = new(); // Хранит отменённые заказы, чтобы багнутый кэш SDK не воскрешал их после рестарта виджета
        private string _lastJobIdEts = ""; // Железобетонный идентификатор заказа (ETS)
        private string _lastJobIdAts = ""; // Железобетонный идентификатор заказа (ATS)
        private string _currentTelemetryJobId = "";
        private string _lastTelemetryJobId = "";
        private string _tbJobIdEts = "";
        private string _tbJobIdAts = "";
        private Dictionary<string, JobState> _jobStates = new();

        private bool _tbLogSaysActive = false;
        private DateTime _lastDeliveredTimestamp = DateTime.MinValue;
        private string _lastDeliveredJobIdEts = "";
        private string _lastDeliveredJobIdAts = "";
        private DateTime _deliveredIndicatorUntil = DateTime.MinValue;
        private DateTime _deliveredFromLogUntil = DateTime.MinValue;

        // Трекинг фантомных данных от игры (когда телеметрия не сбросила старый заказ)
        private bool _lastTbSaysNoJob = false;
        private bool _tbSaysNoJob = false;

        private bool _triggerGhostSnapshot = false;
        private int _cargoLoadedTicks = 0;
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
        private bool _awaitingTbUpload = false;
        private int _speedWarningEts = 0;
        private int _speedWarningAts = 0;

        private const float KmToMiles = 0.621371f;
        private GameType _currentGame = GameType.Unknown;
        private bool _awaitingTelemetryJob = false;

        // Рассинхрон
        private int _desyncSeconds = 0;
        private bool _isDesync = false;

        private string _uiMode = "full";
        private bool _autoHideEnabled = false;
        private DispatcherTimer? _idleTimer;
        private bool _isIdle = false;
        private const int IdleDelaySeconds = 3;
        private const double IdleOpacity = 0.25;
        private int _autoHideQuietMs = 0;
        private const int AutoHideTickMs = 250;

        private enum HealthStatus { Neutral, Healthy, Warning, Error }
        private HealthStatus _currentHealth = HealthStatus.Neutral;
        private double windowOpacity = 0.85;
        private string uiLanguage = "en";
        private readonly Dictionary<string, string> _ets2CityTranslations = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _atsCityTranslations = new(StringComparer.OrdinalIgnoreCase);


        // Settings window
        private SettingsWindow? _settingsWindow;
        private double _savedSettingsLeft = double.NaN;
        private double _savedSettingsTop = double.NaN;

        // UI Scale
        private const double BASE_SCALE = 1.15; // default "100%" is 15% larger than design
        private int _uiScale = 100; // user-selected scale percentage

        private bool _headerOverlayVisible = false;
        private bool _overlayHover = false;
        private HeaderOverlayWindow? _headerOverlay;
        private DispatcherTimer? _overlayHideTimer;

        private class JobState
        {
            public string TelemetryId { get; set; } = "";
            public float DrivenDistance { get; set; }
            public int MaxSpeedKmh { get; set; }
            public bool IsRace { get; set; }
            public bool CargoWasLoaded { get; set; }
            public bool TrailerWasAttachedBeforeLoading { get; set; }
        }

        private class AppState
        {
            public double Left { get; set; }
            public double Top { get; set; }
            public bool ShowDistance { get; set; } = true;
            public bool ShowBottomInfo { get; set; } = true;
            public bool ShowRoute { get; set; } = true;
            public string UIMode { get; set; } = "full";
            public double WindowOpacity { get; set; }
            public string UiLanguage { get; set; } = "en";
            public bool AutoHideEnabled { get; set; } = false;
            public double SettingsLeft { get; set; } = double.NaN;
            public double SettingsTop { get; set; } = double.NaN;
            public int UiScale { get; set; } = 100;
            public List<string> CancelledJobs { get; set; } = new();
        }

        private class GameState
        {
            public string LastJobId { get; set; } = "";
            public string TbJobId { get; set; } = "";
            public string LastDeliveredJobId { get; set; } = "";
            public Dictionary<string, JobState> JobStates { get; set; } = new();
            public int SpeedWarning { get; set; }
        }

        private class CityTranslationEntry
        {
            [JsonPropertyName("english")]
            public string English { get; set; } = "";

            [JsonPropertyName("ukrainian")]
            public string Ukrainian { get; set; } = "";
        }

        private string _currentRouteText = "NOT DEFINED";
        private string RouteText
        {
            get => _currentRouteText;
            set
            {
                _currentRouteText = value;
                Route.Text = value;
                RouteMulti.Text = value;
                
                if (value.Length > 24 && value != "ORDER SUSPENDED" && value != "ЗАМОВЛЕННЯ ПРИЗУПИНЕНО" && value != "NOT DEFINED" && value != "НЕ ВИЗНАЧЕНО")
                {
                    RouteSingleLineGrid.Visibility = Visibility.Collapsed;
                    RouteMultiLineGrid.Visibility = Visibility.Visible;
                }
                else
                {
                    RouteSingleLineGrid.Visibility = Visibility.Visible;
                    RouteMultiLineGrid.Visibility = Visibility.Collapsed;
                }
            }
        }


        private class CityTranslationFile
        {
            [JsonPropertyName("ets2")]
            public List<CityTranslationEntry> Ets2 { get; set; } = new();

            [JsonPropertyName("ats")]
            public List<CityTranslationEntry> Ats { get; set; } = new();
        }

        public MainWindow()
        {
            InitializeComponent();

            MainBorder.Opacity = windowOpacity;

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string folder = Path.Combine(appData, "TruckSimWidget");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            stateFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "state.dat");
            appLogFilePath = Path.Combine(folder, "app_log.txt");

            WriteLog("=== OVERLAY STARTED ===");

            LoadState();
            LoadGameState(GameType.Ets);
            LoadGameState(GameType.Ats);
            LoadCityTranslations();
            ResetStartupJobMemory();
            CheckStatusAndProcesses();
            ApplyLocalization();
            UpdateSpeedWarningText();
            ApplyScale();

            MouseLeftButtonDown += (s, e) => { if (!locked) DragMove(); };
            LocationChanged += (s, e) =>
            {
                SaveState();
                UpdateHeaderOverlayPosition();
            };
            SizeChanged += (s, e) => { UpdateHeaderOverlayPosition(); };

            telemetry = new SCSSdkTelemetry();
            telemetry.Data += Telemetry_Data;
            telemetry.JobStarted += Telemetry_JobStarted;
            telemetry.JobCancelled += Telemetry_JobCancelled;
            telemetry.JobDelivered += Telemetry_JobDelivered;

            tbTimer = new DispatcherTimer();
            tbTimer.Interval = TimeSpan.FromSeconds(1);
            tbTimer.Tick += (s, e) => { CheckStatusAndProcesses(); };
            tbTimer.Start();

            // Idle timer for autohide
            _idleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(AutoHideTickMs) };
            _idleTimer.Tick += (s, e) =>
            {
                EvaluateAutoHideState();
            };

            if (_autoHideEnabled)
            {
                StartIdleTimer();
            }

            // Auto-check for updates on startup (silent mode)
            _ = CheckForUpdatesAsync(silent: true);

            Loaded += async (s, e) =>
            {
                EnsureHeaderOverlay();
                UpdatePinIcon();
                UpdateHeaderOverlayPosition();
                HideHeaderOverlay();

                // 1. Анимация появления виджета + Интро
                MainUI.Opacity = 0;
                IntroOverlay.Visibility = Visibility.Visible;
                IntroOverlay.Opacity = 1;

                // Плавно проявляем главное окно от 0 до windowOpacity
                MainBorder.Opacity = 0;
                var windowFade = new DoubleAnimation(windowOpacity, TimeSpan.FromSeconds(0.4));
                MainBorder.BeginAnimation(OpacityProperty, windowFade);

                // Ждем 1.5 секунды, чтобы пользователь прочитал интро
                await Task.Delay(1500);

                // Кросс-фейд: затухаем интро, проявляем интерфейс
                var fadeOut = new DoubleAnimation(0, TimeSpan.FromSeconds(0.3));
                var fadeIn = new DoubleAnimation(1, TimeSpan.FromSeconds(0.3));

                fadeOut.Completed += (s, ev) => 
                {
                    IntroOverlay.Visibility = Visibility.Collapsed;

                    // Show update success dialog if applicable
                    var args = Environment.GetCommandLineArgs();
                    if (Array.Exists(args, arg => arg == "--updated"))
                    {
                        var successWindow = new UpdateSuccessWindow(uiLanguage);
                        successWindow.Owner = this;
                        successWindow.ShowDialog();
                    }
                };

                IntroOverlay.BeginAnimation(OpacityProperty, fadeOut);
                MainUI.BeginAnimation(OpacityProperty, fadeIn);
            };

            // Восстановление после сворачивания в трей
            StateChanged += (s, e) =>
            {
                if (WindowState == WindowState.Normal)
                {
                    MainBorder.Opacity = 0;
                    MainBorder.BeginAnimation(OpacityProperty, new DoubleAnimation(windowOpacity, TimeSpan.FromSeconds(0.3)));
                    
                    if (_settingsWindow != null && _settingsWindow.IsVisible)
                    {
                        _settingsWindow.Opacity = 0;
                        _settingsWindow.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromSeconds(0.3)));
                    }
                    
                    if (_headerOverlay != null && _headerOverlayVisible)
                    {
                        _headerOverlay.SetOpacity(0);
                        _headerOverlay.AnimateOpacity(1, 0.3);
                    }
                }
            };
        }
        private bool _jobCancelledOrDeliveredFlag = false;

        private void Telemetry_JobStarted(object? sender, EventArgs e)
        {
            WriteLog("[EVENT] Job Started fired by telemetry.");
            _jobCancelledOrDeliveredFlag = false;
        }

        private void Telemetry_JobCancelled(object? sender, EventArgs e)
        {
            WriteLog("[EVENT] Job Cancelled fired by telemetry.");
            _jobCancelledOrDeliveredFlag = true;
        }

        private void Telemetry_JobDelivered(object? sender, EventArgs e)
        {
            WriteLog("[EVENT] Job Delivered fired by telemetry.");
            _jobCancelledOrDeliveredFlag = true;
            Dispatcher.Invoke(() => {
                _deliveredIndicatorUntil = DateTime.Now.AddSeconds(15);
                UpdateStatusUI(LocalizeStatus("DELIVERED"), new SolidColorBrush(Color.FromRgb(82, 193, 79)), true);
            });
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
                    ApplySpeedWarningColor(currentSpeed);

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
                    if (_jobCancelledOrDeliveredFlag) hasJobInfo = false;

                    // Если заказ ранее был принудительно отменён нами из-за багнутого кэша SDK
                    if (_cancelledJobs.Contains(resolvedJobId))
                    {
                        if (data.NavigationValues.NavigationDistance > 0)
                        {
                            _navPositiveTicks++;
                            if (_navPositiveTicks > 5)
                            {
                                // Если навигация стабильно ожила (игрок продолжил заказ) - убираем из отменённых
                                _cancelledJobs.Remove(resolvedJobId);
                                _navPositiveTicks = 0;
                            }
                            else
                            {
                                hasJobInfo = false;
                            }
                        }
                        else
                        {
                            _navPositiveTicks = 0;
                            hasJobInfo = false;
                        }
                    }
                    else
                    {
                        _navPositiveTicks = 0;
                    }

                    if (data.NavigationValues.NavigationDistance == 0) _navZeroTicks++;
                    else
                    {
                        _navZeroTicks = 0;
                        _lastValidNavDist = data.NavigationValues.NavigationDistance;
                    }

                    if (hasJobInfo && _navZeroTicks > 5)
                    {
                        // SCSSdkClient bug workaround: WoT job was cancelled/suspended, but telemetry is stuck.
                        // Navigation distance is 0 for several ticks.
                        if (!_cargoWasLoaded && _cargoLoadedTicks < 3)
                        {
                            // Phase 1 (driving to pickup). We always drop to Free Roam if NavDist becomes 0.
                            // If they just arrived at pickup, it's fine, it will recover when they load.
                            hasJobInfo = false;
                        }
                        else if (_lastValidNavDist > 250f)
                        {
                            // Phase 3 (already loaded). Only drop to Free Roam if the route abruptly vanished
                            // while they were far (>250m) from the destination (i.e. cancelled/suspended).
                            hasJobInfo = false;
                        }

                        if (!hasJobInfo)
                        {
                            // Сохраняем ID отменённого заказа, чтобы он не воскрес при перезапуске виджета
                            _cancelledJobs.Add(resolvedJobId);
                        }
                    }

                    // ЖЁСТКАЯ ПРОВЕРКА ПРИЦЕПА (Игнорируем фейковый CargoLoaded от игры, пока прицепа нет физически на фаркопе)
                    // ПРАВКА: Проверяем ТОЛЬКО первый прицеп (индекс 0). Составные прицепы могут иметь Trailer[1].Attached=true (прицеплен к Trailer[0]), 
                    // даже когда Trailer[0] отцеплен от грузовика!
                    bool isTrailerAttached = data.TrailerValues != null && data.TrailerValues.Length > 0 && data.TrailerValues[0].Attached;
                    bool rawIsCargoLoaded = data.JobValues.CargoLoaded && isTrailerAttached;

                    // Дебаунс: ждём 3 тика подряд, чтобы убедиться, что это не фантомный всплеск при загрузке
                    if (rawIsCargoLoaded) _cargoLoadedTicks++;
                    else _cargoLoadedTicks = 0;

                    bool isCargoLoaded = _cargoLoadedTicks >= 3;

                    // DUMP JOB VALUES FOR DEBUGGING
                    if (data.JobValues != null && !_forceProfileUnloaded)
                    {
                        var jsonDump = System.Text.Json.JsonSerializer.Serialize(data.JobValues, new System.Text.Json.JsonSerializerOptions { WriteIndented = false });
                        // Чтобы не спамить каждый тик, пишем только если изменилась plannedDist или раз в 5 секунд?
                        // Проще просто записать один раз, если есть расхождение
                        if (!isCargoLoaded && plannedDist > 0.5f) {
                             // Just log it every 100 ticks
                             if (DateTime.Now.Second % 5 == 0 && DateTime.Now.Millisecond < 100)
                                WriteLog($"[JOB DATA DUMP] NavDist: {data.NavigationValues.NavigationDistance} | {jsonDump}");
                        }
                    }

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
                            if (_tbHasActiveJob && !string.IsNullOrWhiteSpace(CurrentTbJobId))
                            {
                                // TB активно подтверждает заказ — используем его ID
                                resolvedJobId = CurrentTbJobId;
                            }
                            else if (!_tbSaysNoJob && string.IsNullOrWhiteSpace(CurrentTbJobId))
                            {
                                // TB ещё парсит лог и не вынес вердикт — подождём
                                return;
                            }
                            // Если _tbSaysNoJob=true — используем telemetryJobId (не доверяем истории)
                        }

                        _awaitingTelemetryJob = false;
                        CurrentLastJobId = resolvedJobId;
                        // isNewJob=true если TB говорит «нет активного заказа» —
                        // значит загруженный файл может быть от прошлой сессии
                        LoadOrInitJobState(resolvedJobId, isNewJob: !_tbHasActiveJob);
                    }

                    if (!hasJobInfo)
                    {
                        _telHasJobInfo = false;
                        _telHasActiveJob = false;

                        if (isDelivering || RouteText != (uiLanguage == "uk" ? "ВІЛЬНА ЇЗДА" : "FREE ROAM"))
                        {
                            isDelivering = false;
                            
                            // Мы больше не сбрасываем _cargoWasLoaded здесь, иначе при выходе в меню
                            // виджет забудет, что груз уже был взят, и при перезаходе аннулирует заказ!
                            // _cargoWasLoaded = false;
                            
                            // Также не сбрасываем CurrentLastJobId, чтобы при возобновлении 
                            // приостановленного заказа виджет не считал его новым!
                            // CurrentLastJobId = "";
                            
                            if (jobDrivenDistance > 0) SaveJobState();
                            ClearJobUI();
                        }
                        maxSpeedKmh = 0;
                        MaxSpeedValue.Text = "0";
                        UpdateDeliveryTypeUI(0);
                        _lastTickOdometer = -1;
                    }
                    else
                    {
                        isDelivering = true;

                        // ПЕРЕСТРАХОВКА УБРАНА: _cargoWasLoaded больше не восстанавливается из файла.
                        // Статус груза всегда определяется прямо из телеметрии (isCargoLoaded).

                        // 1. ПЕРВАЯ ПРОВЕРКА: Железобетонный детект нового заказа по ID (города + дистанция)
                        if (CurrentLastJobId != resolvedJobId)
                        {
                            WriteLog($"New job detected! (Job changed from '{CurrentLastJobId}' to '{resolvedJobId}'). Resetting cargo flags.");
                            CurrentLastJobId = resolvedJobId;
                            _lastTickOdometer = -1;
                            _cargoLoadedTicks = 0;
                            _trailerWasAttachedBeforeLoading = false;
                            lastPlannedDistance = plannedDist;
                            LoadOrInitJobState(resolvedJobId, isNewJob: true);
                            SaveJobState();
                        }
                        else if (_lastTelemetryJobId != _currentTelemetryJobId)
                        {
                            LoadOrInitJobState(resolvedJobId);
                        }

                        // Запоминаем, если свой прицеп был подцеплен во время Фазы 1
                        if (isTrailerAttached && !_cargoWasLoaded)
                        {
                            _trailerWasAttachedBeforeLoading = true;
                        }

                        // 2. ВТОРАЯ ПРОВЕРКА: Смотрим, подцеплен ли прицеп физически
                        if (!isCargoLoaded)
                        {
                            _lastTickOdometer = -1; // ЗАМОРАЖИВАЕМ СЧЕТЧИК КИЛОМЕТРОВ! Прицеп отцеплен.

                            if (!_cargoWasLoaded)
                            {
                                if (_trailerWasAttachedBeforeLoading && !isTrailerAttached)
                                {
                                    // ФАЗА 1 (Свой прицеп): Прицеп отцепили по пути на погрузку! Заказ приостановлен.
                                    RouteText = uiLanguage == "uk" ? "ЗАМОВЛЕННЯ ПРИЗУПИНЕНО" : "ORDER SUSPENDED";
                                    DistanceInfo.Text = uiLanguage == "uk" ? "Призупинено" : "Suspended";
                                }
                                else
                                {
                                    // ФАЗА 1: Едем за грузом (ещё не брали)
                                    RouteText = $"{GetLocalizedCity(data.JobValues.CitySource).ToUpper()} -> {GetLocalizedCity(data.JobValues.CityDestination).ToUpper()}";
                                    DistanceInfo.Text = uiLanguage == "uk" ? "За вантажем..." : "To pickup...";
                                }
                            }
                            else
                            {
                                // ФАЗА 3: Прицеп отцепили / Заказ приостановлен
                                RouteText = uiLanguage == "uk" ? "ЗАМОВЛЕННЯ ПРИЗУПИНЕНО" : "ORDER SUSPENDED";
                                DistanceInfo.Text = uiLanguage == "uk" 
                                    ? "Призупинено" 
                                    : "Suspended";
                            }
                        }
                        else
                        {
                            // ФАЗА 2: Груз загружен (реальная сцепка)
                            if (!_cargoWasLoaded)
                            {
                                // ДВОЙНАЯ ПРОВЕРКА: телеметрия говорит груз есть,
                                // но если TB запущен и говорит «нет активного заказа» —
                                // это фантомное/устаревшее состояние игры. Не считаем груз взятым.
                                bool tbAllowsCargo = !_isTbRunning || _tbHasActiveJob;
                                if (tbAllowsCargo)
                                {
                                    _cargoWasLoaded = true;
                                    _lastTickOdometer = currentOdo;
                                    
                                    // РАСШИРЕННЫЙ ЛОГ ДЛЯ ДИАГНОСТИКИ
                                    string trailerLog = data.TrailerValues != null 
                                        ? string.Join(", ", data.TrailerValues.Select(t => $"[Attached:{t.Attached}, Name:{t.Name}, ID:{t.Id}, Body:{t.BodyType}]"))
                                        : "null";
                                        
                                    WriteLog($"Cargo physically loaded! Tracking started. Total Dist: {plannedDist}{GetDistanceUnitShort()}, Dest: {GetLocalizedCity(data.JobValues.CityDestination).ToUpper()}");
                                    WriteLog($"[DIAGNOSTICS] CargoLoaded: {data.JobValues.CargoLoaded}, TrailerValues: {trailerLog}");
                                    
                                    SaveJobState();
                                }
                                else
                                {
                                    // TB говорит нет заказа — телеметрия врёт (ghost data)
                                    WriteLog($"Ghost cargo ignored: telemetry says loaded but TB has no active job.");
                                }
                            }

                            // Считаем пройденный путь и обновляем UI только если груз реально подтверждён
                            if (_cargoWasLoaded)
                            {
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
                                // Используем данные от навигатора (advisor) в реальном времени, чтобы итоговый километраж
                                // обновлялся при перестроении маршрута. Берём пройденное + оставшееся по навигатору.
                                float remaining = (data.NavigationValues.NavigationDistance / 1000f) * distanceFactor;
                                int drivenInt = Math.Max(0, (int)Math.Round(jobDrivenDistance));
                                // totalFromAdvisor — текущее ожидаемое суммарное расстояние (пройдено + оставшееся по навигатору)
                                float totalFromAdvisor = jobDrivenDistance + remaining;
                                // На случай, если PlannedDistance из JobValues актуален и больше (редкие случаи), берём максимум
                                float totalCandidate = Math.Max(totalFromAdvisor, plannedDist);
                                int totalInt = Math.Max(0, (int)Math.Round(totalCandidate));

                                DistanceInfo.Text = uiLanguage == "uk"
                                    ? $"{drivenInt} / {totalInt} {GetDistanceUnitShort()}"
                                    : $"{drivenInt} / {totalInt} {GetDistanceUnitShort()}";

                                if (!string.IsNullOrEmpty(data.JobValues.CitySource))
                                    RouteText = $"{GetLocalizedCity(data.JobValues.CitySource).ToUpper()} -> {GetLocalizedCity(data.JobValues.CityDestination).ToUpper()}";
                            }
                            else
                            {
                                // Груз не подтверждён TB — показываем фазу 1 (едем за грузом)
                                RouteText = $"{GetLocalizedCity(data.JobValues.CitySource).ToUpper()} -> {GetLocalizedCity(data.JobValues.CityDestination).ToUpper()}";
                                DistanceInfo.Text = uiLanguage == "uk" ? "За вантажем..." : "To pickup...";
                            }
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
                    GetTbActiveJobFromFolders(out var folderJobId, out var deliveredInfo);
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
                        if (CurrentLastJobId == CurrentLastDeliveredJobId || _cancelledJobs.Contains(CurrentLastDeliveredJobId))
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
                        // Deleted unused assignment
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
                            int lastProfileLoad = -1, lastProfileLeave = -1;
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
                            bool wasDelivered = false;

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
                                    wasDelivered = line.Contains("- Delivered");
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
                                if (line.Contains("New profile selected")) lastProfileLoad = i;
                                if (line.Contains("Leave profile")) lastProfileLeave = i;
                            }

                            int lastStart = _currentGame == GameType.Ats ? lastStartAts : lastStartEts;
                            int lastFinish = _currentGame == GameType.Ats ? lastFinishAts : lastFinishEts;
                            int lastEmpty = _currentGame == GameType.Ats ? lastEmptyAts : lastEmptyEts;

                            bool awaitingTbUpload = lastDeliveredIdx >= 0
                                && lastDeliveredIdx >= lastStart
                                && lastUploadedIdx < lastDeliveredIdx
                                && lastExitGameIdx < lastDeliveredIdx;
                            _awaitingTbUpload = _isTbRunning && awaitingTbUpload;

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

                            if (lastDeliveredIdx >= 0)
                            {
                                if (_lastDeliveredLogIndex == -1 || lastDeliveredIdx < _lastDeliveredLogIndex)
                                {
                                    _lastDeliveredLogIndex = lastDeliveredIdx;
                                }
                                else if (lastDeliveredIdx > _lastDeliveredLogIndex)
                                {
                                    if (wasDelivered)
                                    {
                                        _deliveredFromLogUntil = DateTime.Now.AddSeconds(10);
                                    }
                                    else
                                    {
                                        _deliveredFromLogUntil = DateTime.MinValue;
                                        _deliveredIndicatorUntil = DateTime.MinValue;
                                        _lastDeliveredTimestamp = DateTime.MinValue;
                                        CurrentLastDeliveredJobId = "";
                                    }
                                    _lastDeliveredLogIndex = lastDeliveredIdx;
                                }
                            }

                            if (!isRecordingBroken && !_awaitingTbUpload && lastDeliveredIdx >= 0 && lastDeliveredIdx > Math.Max(lastDecryptOkIdx, lastUploadedIdx)
                                && lastDeliveredIdx >= lastStart && lastDeliveredIdx >= lastProfileLoad)
                            {
                                isRecordingBroken = true;
                                tbState = LocalizeStatus("TB_ERROR");
                                tbColor = Brushes.Red;
                            }

                            if (!isRecordingBroken && !_awaitingTbUpload && lastFoundFilesIdx > lastDeliveredIdx && lastDeliveredIdx >= 0 && lastDeliveredIdx >= lastStart)
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
                                if (!_cancelledJobs.Contains(CurrentTbJobId))
                                {
                                    CurrentLastJobId = CurrentTbJobId;
                                    LoadOrInitJobState(CurrentLastJobId);
                                    SaveJobState();
                                }
                            }

                            _tbHasActiveJob = logSaysActive && !string.IsNullOrWhiteSpace(CurrentTbJobId);
                            _awaitingTbResponse = _isTbRunning && !_tbHasActiveJob && !_tbSaysNoJob && !_forceProfileUnloaded;

                            if (_tbSaysNoJob && !_telHasJobInfo)
                            {
                                _isRecordingBroken = false;
                                tbState = LocalizeStatus("TB_ONLINE");
                                tbColor = new SolidColorBrush(Color.FromRgb(82, 193, 79));
                            }

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
                            if (_awaitingTbResponse || _awaitingTbUpload || !isProfileLoaded || _forceProfileUnloaded)
                            {
                                _desyncSeconds = 0;
                            }
                            else if (_isTbRunning && !_isRecordingBroken && isGameRunning)
                            {
                                if (!isPaused || (!isProfileLoaded && _tbHasActiveJob) || RouteText == "ORDER SUSPENDED" || RouteText == "ЗАМОВЛЕННЯ ПРИЗУПИНЕНО")
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

            if (_forceProfileUnloaded)
            {
                // Телеметрия перестаёт присылать данные при выходе из профиля.
                // Поэтому принудительно гасим UI из фонового потока, чтобы он не зависал на старых значениях.
                isProfileLoaded = false;
                _telHasJobInfo = false;
                _telHasActiveJob = false;
                isDelivering = false;
                
                GameStatus.Text = LocalizeStatus("GAME_START");
                GameStatus.Foreground = Brushes.Orange;
                
                MaxSpeedValue.Text = "0";
                
                ClearJobUI();
            }

            string oldStatus = StatusValue.Text;

            if (!_isTbRunning && _telHasActiveJob) UpdateStatusUI(LocalizeStatus("TB_CLOSED_NO_REC"), Brushes.Red, false);
            else if (_isRecordingBroken && _telHasActiveJob) UpdateStatusUI(LocalizeStatus("NOT_RECORDING"), Brushes.Red, false);
            else if (_awaitingTbResponse || _forceProfileUnloaded) UpdateStatusUI(LocalizeStatus("PROFILE_MENU"), Brushes.Orange, false);
            else if (_isDesync) UpdateStatusUI(uiLanguage == "uk" ? "Гра ≠ TB" : "Game ≠ TB", Brushes.Red, false);
            else if (_isRecordingBroken && _telHasJobInfo) UpdateStatusUI(LocalizeStatus("TB_ERROR_CHECK"), Brushes.Red, false);
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
            else if (_awaitingTbUpload) UpdateStatusUI(LocalizeStatus("WAIT_TB_UPLOAD"), Brushes.Orange, false);
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

            // Infer health status from color and react for autohide mode
            HealthStatus newStatus = HealthStatus.Neutral;
            if (color is SolidColorBrush sc)
            {
                var c = sc.Color;
                // Green-ish -> Healthy
                if (c.G > 150 && c.R < 150 && c.B < 150) newStatus = HealthStatus.Healthy;
                // Red-ish -> Error
                else if (c.R > 180 && c.G < 140) newStatus = HealthStatus.Error;
                // Orange/Yellow-ish -> Warning
                else if (c.R > 180 && c.G > 120 && c.B < 140) newStatus = HealthStatus.Warning;
                else newStatus = HealthStatus.Neutral;
            }
            else if (color == Brushes.Red) newStatus = HealthStatus.Error;
            else if (color == Brushes.Orange || color == Brushes.Yellow) newStatus = HealthStatus.Warning;
            else if (color == Brushes.White) newStatus = HealthStatus.Healthy;

            OnStatusLevelChanged(newStatus);
        }

        private void ClearAllJobData()
        {
            maxSpeedKmh = 0; isRace = false; lastPlannedDistance = 0; jobDrivenDistance = 0; _lastTickOdometer = -1; _cargoWasLoaded = false;
            _lastJobIdEts = ""; _lastJobIdAts = "";
            _tbJobIdEts = ""; _tbJobIdAts = "";
            _lastDeliveredJobIdEts = ""; _lastDeliveredJobIdAts = "";
            _jobStates.Clear();
            MaxSpeedValue.Text = "0"; RouteText = uiLanguage == "uk" ? "НЕ ВИЗНАЧЕНО" : "NOT DEFINED"; DistanceInfo.Text = GetZeroDistanceText();
            UpdateDeliveryTypeUI(0); SaveState();
        }

        private void ClearJobUI()
        {
            RouteText = uiLanguage == "uk" ? "НЕ ВИЗНАЧЕНО" : "NOT DEFINED";
            DistanceInfo.Text = GetZeroDistanceText();
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

        private void LoadOrInitJobState(string jobId, bool isNewJob = false)
        {
            if (string.IsNullOrWhiteSpace(jobId)) return;

            string stateKey = GetJobStateKey(jobId);

            if (!_jobStates.TryGetValue(stateKey, out var state))
            {
                state = LoadIndividualJobFile(_currentGame, jobId);

                if (state == null)
                {
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

            if (isNewJob)
            {
                state.CargoWasLoaded = false;
                state.DrivenDistance = 0;
                state.MaxSpeedKmh = 0;
                state.IsRace = false;
                state.TrailerWasAttachedBeforeLoading = false;
                WriteLog($"New job — all state reset for: {jobId}");
                // Сбрасываем переменные сразу и сохраняем на диск
                jobDrivenDistance = 0;
                maxSpeedKmh = 0;
                isRace = false;
                _cargoWasLoaded = false;
                _trailerWasAttachedBeforeLoading = false;
                _lastTickOdometer = -1;
                MaxSpeedValue.Text = "0";
                SaveJobState();
                return;
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
            // КРИТИЧЕСКОЕ ПРАВИЛО ОТМЕНЕНО: Теперь мы восстанавливаем _cargoWasLoaded, чтобы при перезапуске виджета во время паузы (с отцепленным прицепом)
            // он помнил, что груз уже БЫЛ взят, и корректно показывал ORDER SUSPENDED, а не откатывался в Фазу 1 (To pickup...)
            _cargoWasLoaded = jobDrivenDistance > 0 || state.CargoWasLoaded;
            _trailerWasAttachedBeforeLoading = state.TrailerWasAttachedBeforeLoading;
            MaxSpeedValue.Text = maxSpeedKmh.ToString();
            _lastTickOdometer = -1;
            // Не вызываем SaveJobState() здесь: не хотим перезаписать CargoWasLoaded=true в файле
            // до того, как телеметрия подтвердит сцепку.
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
                CargoWasLoaded = _cargoWasLoaded,
                TrailerWasAttachedBeforeLoading = _trailerWasAttachedBeforeLoading
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
                    ShowDistance = true,
                    ShowBottomInfo = true,
                    ShowRoute = true,
                    UIMode = _uiMode,
                    WindowOpacity = windowOpacity,
                    UiLanguage = uiLanguage,
                    AutoHideEnabled = _autoHideEnabled,
                    SettingsLeft = _settingsWindow?.Left ?? _savedSettingsLeft,
                    SettingsTop = _settingsWindow?.Top ?? _savedSettingsTop,
                    UiScale = _uiScale,
                    CancelledJobs = _cancelledJobs.ToList()
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
                            _uiMode = state.UIMode ?? "full";
                            windowOpacity = state.WindowOpacity;
                            _autoHideEnabled = state.AutoHideEnabled;
                            uiLanguage = string.IsNullOrWhiteSpace(state.UiLanguage) ? "en" : state.UiLanguage;
                            if (windowOpacity <= 0 || windowOpacity > 1)
                            {
                                windowOpacity = 0.85;
                            }
                            // Restore settings window position if available
                            if (!double.IsNaN(state.SettingsLeft) && !double.IsNaN(state.SettingsTop))
                            {
                                _savedSettingsLeft = state.SettingsLeft;
                                _savedSettingsTop = state.SettingsTop;
                            }
                            if (state.CancelledJobs != null)
                            {
                                _cancelledJobs = new HashSet<string>(state.CancelledJobs);
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
                            // Legacy parsing omitted
                        }
                    }
                }
            }
            catch { }

            MainBorder.Opacity = windowOpacity;
            ApplyDualLayerOpacity();
            ApplyLanguageSelection();
            ApplyLocalization();
            ApplyUIMode(false);
            UpdateSpeedWarningText();
        }

        private void ResetDisplay(bool clearJobState = true)
        {
            SpeedValue.Text = "0"; 
            isDelivering = false; 
            isPaused = false; 
            isProfileLoaded = false;
            GameStatus.Text = LocalizeStatus("GAME_OFFLINE"); 
            GameStatus.Foreground = Brushes.Gray;
            UpdateStatusUI(uiLanguage == "uk" ? "Очікування гри..." : "Wait for game...", Brushes.Gray, false);
            UpdateSimDisplay();

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

        public void BtnSettings_Click(object? sender, RoutedEventArgs e)
        {
            if (_settingsWindow == null)
            {
                _settingsWindow = new SettingsWindow(this);
                _settingsWindow.SetUIMode(_uiMode);
                _settingsWindow.SetOpacity(windowOpacity * 100);
                _settingsWindow.SetLanguage(uiLanguage);
                _settingsWindow.SetSpeedWarningText(Math.Max(0, CurrentSpeedWarning).ToString());
                _settingsWindow.SetVersionText($"v{GetCurrentVersion()}");
                _settingsWindow.SetScale(_uiScale);
                _settingsWindow.SetAutoHideEnabled(_autoHideEnabled);
                _settingsWindow.UpdateLocalization(uiLanguage == "uk");
                // Restore saved position if available
                if (!double.IsNaN(_savedSettingsLeft) && !double.IsNaN(_savedSettingsTop))
                {
                    _settingsWindow.Left = _savedSettingsLeft;
                    _settingsWindow.Top = _savedSettingsTop;
                }
            }

            if (!_settingsWindow.IsVisible)
            {
                _settingsWindow.Opacity = 0;
                _settingsWindow.Show();
                var fadeIn = new DoubleAnimation(1, TimeSpan.FromSeconds(0.2));
                _settingsWindow.BeginAnimation(Window.OpacityProperty, fadeIn);
            }
            _settingsWindow.Activate();
        }

        private void ApplyUIMode(bool animate = true)
        {
            if (_uiMode == "minimal")
            {
                if (animate)
                {
                    var fadeOut = new DoubleAnimation(0, TimeSpan.FromSeconds(0.2));
                    var currentHeight = CollapsibleSection.ActualHeight;
                    CollapsibleSection.Height = currentHeight;
                    var shrink = new DoubleAnimation(currentHeight, 0, TimeSpan.FromSeconds(0.3)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } };

                    shrink.Completed += (s, ev) => 
                    {
                        CollapsibleSection.BeginAnimation(HeightProperty, null);
                        CollapsibleSection.Visibility = Visibility.Collapsed;
                        CollapsibleSection.Height = double.NaN;
                    };

                    CollapsibleSection.BeginAnimation(OpacityProperty, fadeOut);
                    CollapsibleSection.BeginAnimation(HeightProperty, shrink);
                }
                else
                {
                    CollapsibleSection.BeginAnimation(OpacityProperty, null);
                    CollapsibleSection.BeginAnimation(HeightProperty, null);
                    CollapsibleSection.Opacity = 0;
                    CollapsibleSection.Visibility = Visibility.Collapsed;
                    CollapsibleSection.Height = double.NaN;
                }
            }
            else
            {
                CollapsibleSection.Visibility = Visibility.Visible;
                
                if (animate)
                {
                    CollapsibleSection.Height = double.NaN;
                    CollapsibleSection.UpdateLayout();
                    var targetHeight = CollapsibleSection.ActualHeight;

                    CollapsibleSection.Height = 0;
                    CollapsibleSection.Opacity = 0;

                    var fadeIn = new DoubleAnimation(1, TimeSpan.FromSeconds(0.3)) { BeginTime = TimeSpan.FromSeconds(0.1) };
                    var grow = new DoubleAnimation(0, targetHeight, TimeSpan.FromSeconds(0.3)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } };

                    grow.Completed += (s, ev) => 
                    {
                        CollapsibleSection.BeginAnimation(HeightProperty, null);
                        CollapsibleSection.Height = double.NaN;
                    };

                    CollapsibleSection.BeginAnimation(OpacityProperty, fadeIn);
                    CollapsibleSection.BeginAnimation(HeightProperty, grow);
                }
                else
                {
                    CollapsibleSection.BeginAnimation(OpacityProperty, null);
                    CollapsibleSection.BeginAnimation(HeightProperty, null);
                    CollapsibleSection.Opacity = 1;
                    CollapsibleSection.Height = double.NaN;
                }
            }
        }

        private void ApplyScale()
        {
            double effectiveScale = BASE_SCALE * (_uiScale / 100.0);
            UIScaleTransform.ScaleX = effectiveScale;
            UIScaleTransform.ScaleY = effectiveScale;
            _headerOverlay?.SetScale(effectiveScale);
            UpdateHeaderOverlayPosition();
        }

        public void OnScaleChanged(int scalePercent)
        {
            _uiScale = scalePercent;
            ApplyScale();
            SaveState();
        }

        // Called by SettingsWindow when Auto-hide enabled toggles
        public void OnAutoHideEnabledChanged(bool enabled)
        {
            _autoHideEnabled = enabled;
            SaveState();
            if (_autoHideEnabled)
            {
                _autoHideQuietMs = IdleDelaySeconds * 1000;
                StartIdleTimer();
                EvaluateAutoHideState();
            }
            else
            {
                StopIdleTimer();
                ExitIdleState();
            }
        }

        public void SetAutoHideEnabled(bool enabled)
        {
            _autoHideEnabled = enabled;
            if (_settingsWindow != null && _settingsWindow.IsVisible)
            {
                _settingsWindow.SetAutoHideEnabled(enabled);
            }
        }

        // Public methods called by SettingsWindow
        public void OnUIModeChanged(string mode)
        {
            _uiMode = mode;
            ApplyUIMode();
            EnsureAutohideConsistency();
            SaveState();
        }

        public void OnOpacityChanged(double sliderValue)
        {
            windowOpacity = sliderValue / 100.0;
            ApplyDualLayerOpacity();
            SaveState();
        }

        public bool IsLocked => locked;

        /// <summary>
        /// Applies opacity so the overall widget uses windowOpacity, but text elements
        /// use only half of the opacity delta (50% less opacity change for better readability).
        /// E.g. widget at 90% -> text at 95%.
        /// </summary>
        private void ApplyDualLayerOpacity()
        {
            // Remove any active animation that might block local value updates
            MainBorder.BeginAnimation(OpacityProperty, null);
            
            // The background/border gets the full user opacity
            MainBorder.Opacity = windowOpacity;
            
            if (_headerOverlay != null && _headerOverlayVisible)
            {
                _headerOverlay.AnimateOpacity(windowOpacity, 0.1);
            }

            // Text opacity: midpoint between 1.0 and windowOpacity (half as much change)
            double textOpacity = windowOpacity + (1.0 - windowOpacity) * 0.5;
            textOpacity = Math.Max(0.0, Math.Min(1.0, textOpacity));

            // Apply to all named text elements
            TextBlock[] textBlocks = new TextBlock[]
            {
                GameLabel, GameStatus,
                SimLabel, TbStatus,
                StatusLabel, StatusValue,
                DistanceLabel, DistanceInfo,
                RouteLabel, RouteLabelMulti,
                Route, RouteMulti,
                SpeedHeader, SpeedValue, SpeedUnit,
                MaxHeader, MaxSpeedValue, MaxUnit,
                TypeHeader, DeliveryType
            };
            foreach (var tb in textBlocks)
            {
                if (tb != null) tb.Opacity = textOpacity;
            }
        }

        public void OnLanguageChanged(string lang)
        {
            uiLanguage = lang;
            ApplyLocalization();
            SaveState();
        }

        public void OnSpeedWarningChanged(int value)
        {
            value = Math.Max(0, value);
            if (CurrentSpeedWarning != value)
            {
                CurrentSpeedWarning = value;
                SaveGameState(_currentGame);
            }
            ApplySpeedWarningColor();
        }

        public void OnSpeedWarningUp()
        {
            CurrentSpeedWarning = Math.Max(0, CurrentSpeedWarning + 1);
            _settingsWindow?.SetSpeedWarningText(CurrentSpeedWarning.ToString());
            SaveGameState(_currentGame);
            ApplySpeedWarningColor();
        }

        public void OnSpeedWarningDown()
        {
            CurrentSpeedWarning = Math.Max(0, CurrentSpeedWarning - 1);
            _settingsWindow?.SetSpeedWarningText(CurrentSpeedWarning.ToString());
            SaveGameState(_currentGame);
            ApplySpeedWarningColor();
        }

        public void OnCheckUpdate()
        {
            if (_isCheckingUpdate) return;
            _ = CheckForUpdatesAsync(silent: false);
        }

        public void OnDonate()
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = DonateUrl, UseShellExecute = true });
            }
            catch (Exception ex) { WriteLog($"Failed to open donate page: {ex.Message}"); }
        }


        private void ApplyLocalization()
        {
            bool isUk = uiLanguage == "uk";

            // Main window labels
            GameLabel.Text = isUk ? "ГРА" : "GAME";
            SimLabel.Text = isUk ? "TRUCKSBOOK" : "TRUCKSBOOK"; // Always TRUCKSBOOK (card header)
            DistanceLabel.Text = isUk ? "ДИСТАНЦІЯ" : "DISTANCE";
            StatusLabel.Text = isUk ? "СТАТУС" : "STATUS";
            RouteLabel.Text = isUk ? "МАРШРУТ" : "ROUTE";
            RouteLabelMulti.Text = isUk ? "МАРШРУТ" : "ROUTE";
            SpeedHeader.Text = isUk ? "ШВИДКІСТЬ" : "SPEED";
            MaxHeader.Text = isUk ? "МАКС" : "MAX";
            TypeHeader.Text = isUk ? "ТИП" : "TYPE";
            SpeedUnit.Text = GetSpeedUnitText();
            MaxUnit.Text = GetSpeedUnitText();

            // Settings window localization
            _settingsWindow?.UpdateLocalization(isUk);
            ;

            UpdateDeliveryTypeUI(0);
            UpdateDistanceInfoForLanguage();
            UpdateStatusTextForLanguage();
            UpdateRouteForLanguage();
            UpdateSimDisplay();
        }

        private void UpdateRouteForLanguage()
        {
            bool isUk = uiLanguage == "uk";
            RouteLabel.Text = isUk ? "МАРШРУТ" : "ROUTE";
            RouteLabelMulti.Text = isUk ? "МАРШРУТ" : "ROUTE";

            if (string.IsNullOrWhiteSpace(RouteText)) return;

            if (RouteText == "ROUTE: NOT DEFINED" || RouteText == "МАРШРУТ: НЕ ВИЗНАЧЕНО")
            {
                RouteText = uiLanguage == "uk" ? "МАРШРУТ: НЕ ВИЗНАЧЕНО" : "ROUTE: NOT DEFINED";
                return;
            }

            var match = Regex.Match(RouteText, @"^(?<from>.+?)\s*->\s*(?<to>.+)$");
            if (!match.Success) return;

            var from = match.Groups["from"].Value.Trim();
            var to = match.Groups["to"].Value.Trim();
            RouteText = $"{GetLocalizedCity(from).ToUpper()} -> {GetLocalizedCity(to).ToUpper()}";
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
                "GAME_ACTIVE" => isUk ? "АКТИВНА" : "ACTIVE",
                "GAME_START" => isUk ? "СТАРТ" : "START",
                "PAUSE" => isUk ? "ПАУЗА" : "PAUSE",
                "DESYNC" => isUk ? "РОЗСИНХРОН!" : "DESYNC ALERT!",
                "FREE_ROAM" => isUk ? "ВІЛЬНИЙ РЕЖИМ" : "Free Roam",
                "WAIT_TB" => isUk ? "Чекаю TB..." : "Wait for TB...",
                "WAIT_TB_UPLOAD" => isUk ? "Відправка..." : "Uploading...",
                "DRIVING_TO_PICKUP" => isUk ? "За вантажем..." : "To pickup...",
                "TRAILER_DETACHED" => isUk ? "Від'єднано!" : "Detached!",
                "RECORDING_KM" => isUk ? "Запис кілометрів" : "Recording km",
                "TB_CLOSED_NO_REC" => isUk ? "TB ЗАКРИТО" : "TB CLOSED",
                "NOT_RECORDING" => isUk ? "НЕ ЗАПИСУЄ!" : "NO REC!",
                "TB_ERROR_CHECK" => isUk ? "Помилка TB" : "TB Error",
                "PROFILE_MENU" => isUk ? "В меню" : "In Menu",
                "DELIVERED" => isUk ? "ДОСТАВЛЕНО" : "DELIVERED",
                "KM_NOT_REC" => isUk ? "КМ ВТРАЧЕНО (TB)" : "KM LOST (TB)",
                _ => key
            };
        }

        private void UpdateStatusTextForLanguage()
        {
            var status = StatusValue.Text;
            StatusValue.Text = status switch
            {
                "TB CLOSED! NO REC" or "TB CLOSED" or "TB ЗАКРИТО" or "TB ЗАКРИТИЙ! НЕ ЗАП." => LocalizeStatus("TB_CLOSED_NO_REC"),
                "NOT RECORDING!" or "NO REC!" or "НЕ ЗАПИСУЄ!" or "НЕ ЗАПИСУЄТЬСЯ!" => LocalizeStatus("NOT_RECORDING"),
                "Game ≠ TB" or "Гра ≠ TB" => uiLanguage == "uk" ? "Гра ≠ TB" : "Game ≠ TB",
                "TB Error: Check Client" or "TB Error" or "Помилка TB" or "Помилка TB: Перевірте клієнт" => LocalizeStatus("TB_ERROR_CHECK"),
                "Profile Menu" or "In Menu" or "В меню" or "Меню профілю" => LocalizeStatus("PROFILE_MENU"),
                "Free Roam" or "ВІЛЬНИЙ РЕЖИМ" => LocalizeStatus("FREE_ROAM"),
                "Trailer detached!" or "Detached!" or "Від'єднано!" or "Причеп від'єднано!" => LocalizeStatus("TRAILER_DETACHED"),
                "PAUSED (Menu)" or "ПАУЗА (Меню)" => uiLanguage == "uk" ? "ПАУЗА (Меню)" : "PAUSED (Menu)",
                "Recording km" or "Запис кілометрів" => LocalizeStatus("RECORDING_KM"),
                "Waiting for TB..." or "Wait for TB..." or "Чекаю TB..." or "Очікування TB..." => LocalizeStatus("WAIT_TB"),
                "Waiting for order upload..." or "Uploading..." or "Відправка..." or "Очікування відправки замовлення..." => LocalizeStatus("WAIT_TB_UPLOAD"),
                "KM NOT REC (TB)" or "KM LOST (TB)" or "КМ ВТРАЧЕНО (TB)" or "КМ НЕ ЗАП (TB)" => LocalizeStatus("KM_NOT_REC"),
                "DELIVERED" or "ДОСТАВЛЕНО" => LocalizeStatus("DELIVERED"),
                _ => status
            };

            GameStatus.Text = GameStatus.Text switch
            {
                "OFFLINE" => LocalizeStatus("GAME_OFFLINE"),
                "ACTIVE" => LocalizeStatus("GAME_ACTIVE"),
                "START" => LocalizeStatus("GAME_START"),
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

            if (RouteText == "NOT DEFINED" || RouteText == "НЕ ВИЗНАЧЕНО")
            {
                RouteText = uiLanguage == "uk" ? "НЕ ВИЗНАЧЕНО" : "NOT DEFINED";
            }
            else if (RouteText == "ORDER SUSPENDED" || RouteText == "ЗАМОВЛЕННЯ ПРИЗУПИНЕНО")
            {
                RouteText = uiLanguage == "uk" ? "ЗАМОВЛЕННЯ ПРИЗУПИНЕНО" : "ORDER SUSPENDED";
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

            if (DistanceInfo.Text == "To pickup..." || DistanceInfo.Text == "За вантажем...")
            {
                DistanceInfo.Text = uiLanguage == "uk" ? "За вантажем..." : "To pickup...";
                return;
            }

            if (uiLanguage == "uk")
            {
                DistanceInfo.Text = DistanceInfo.Text
                    .Replace(" km (", " км (")
                    .Replace(" mi (", " миль (")
                    .Replace(" km", " км")
                    .Replace(" mi", " миль")
                    .Replace("Detached! ", "Від'єднано! ")
                    .Replace(" done)", " пройдено)");
            }
            else
            {
                DistanceInfo.Text = DistanceInfo.Text
                    .Replace(" км (", " km (")
                    .Replace(" миль (", " mi (")
                    .Replace(" км", " km")
                    .Replace(" миль", " mi")
                    .Replace("Від'єднано! ", "Detached! ")
                    .Replace(" пройдено)", " done)");
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
            UpdateSpeedWarningText();
            UpdateDistanceInfoForLanguage();
            UpdateSimDisplay();
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

        private void LoadCityTranslations()
        {
            try
            {
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "city_translations_uk.json");
                if (!File.Exists(path))
                {
                    WriteLog($"City translations not found: {path}");
                    return;
                }

                var json = File.ReadAllText(path);
                var data = JsonSerializer.Deserialize<CityTranslationFile>(json);
                if (data == null)
                {
                    WriteLog("City translations file is empty or invalid.");
                    return;
                }

                _ets2CityTranslations.Clear();
                _atsCityTranslations.Clear();

                foreach (var entry in data.Ets2)
                {
                    if (!string.IsNullOrWhiteSpace(entry.English) && !string.IsNullOrWhiteSpace(entry.Ukrainian))
                    {
                        _ets2CityTranslations[entry.English] = entry.Ukrainian;
                    }
                }

                foreach (var entry in data.Ats)
                {
                    if (!string.IsNullOrWhiteSpace(entry.English) && !string.IsNullOrWhiteSpace(entry.Ukrainian))
                    {
                        _atsCityTranslations[entry.English] = entry.Ukrainian;
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLog($"Failed to load city translations: {ex.Message}");
            }
        }

        private string GetLocalizedCity(string? city)
        {
            if (string.IsNullOrWhiteSpace(city)) return city ?? string.Empty;

            var translations = _currentGame == GameType.Ats ? _atsCityTranslations : _ets2CityTranslations;

            if (uiLanguage == "uk")
            {
                // Attempt to find the Ukrainian translation for the given English city (case-insensitive key match)
                var pair = translations.FirstOrDefault(x => x.Key.Equals(city, StringComparison.OrdinalIgnoreCase));
                return pair.Value != null ? pair.Value : city;
            }
            else
            {
                // Attempt to find the English original for the given Ukrainian city (case-insensitive value match)
                var pair = translations.FirstOrDefault(x => x.Value.Equals(city, StringComparison.OrdinalIgnoreCase));
                return pair.Key != null ? pair.Key : city;
            }
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
                    SpeedWarning = game == GameType.Ats ? _speedWarningAts : _speedWarningEts,
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
                    _speedWarningAts = Math.Max(0, state.SpeedWarning);
                }
                else
                {
                    _lastJobIdEts = state.LastJobId ?? "";
                    _tbJobIdEts = state.TbJobId ?? "";
                    _lastDeliveredJobIdEts = state.LastDeliveredJobId ?? "";
                    _speedWarningEts = Math.Max(0, state.SpeedWarning);
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

        public async void BtnMinimize_Click(object? sender, RoutedEventArgs e)
        {
            _isManualMinimize = true;
            
            var fadeOut = new DoubleAnimation(0, TimeSpan.FromSeconds(0.2));
            MainBorder.BeginAnimation(OpacityProperty, fadeOut);
            
            if (_settingsWindow != null && _settingsWindow.IsVisible)
            {
                _settingsWindow.BeginAnimation(OpacityProperty, fadeOut);
            }
            
            if (_headerOverlay != null && _headerOverlayVisible)
            {
                _headerOverlay.AnimateOpacity(0, 0.2);
            }
            
            await Task.Delay(200);
            WindowState = WindowState.Minimized;
            
            // Восстанавливаем анимацию, чтобы при StateChanged виджет проявился нормально
            MainBorder.BeginAnimation(OpacityProperty, null);
            MainBorder.Opacity = windowOpacity;
        }

        public void BtnClose_Click(object? sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        public void BtnTopmost_Click(object? sender, RoutedEventArgs e)
        {
            Topmost = !Topmost;
            UpdatePinIcon();
            SaveState();
        }

        private void UpdatePinIcon()
        {
            _headerOverlay?.UpdatePinIcon(Topmost);
        }
        protected override void OnClosed(EventArgs e) { WriteLog("=== OVERLAY CLOSED ==="); SaveState(); telemetry?.Dispose(); base.OnClosed(e); }

        // ==================== AUTO-UPDATE ====================

        /// <summary>
        /// Получает текущую версию приложения из сборки
        /// </summary>
        private static string GetCurrentVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version == null) return "0.0.0";
            return $"{version.Major}.{version.Minor}.{version.Build}";
        }

        /// <summary>
        /// Извлекает номер версии из тега релиза GitHub (например "v1.0.4-stable" -> "1.0.4")
        /// </summary>
        private static string ExtractVersionFromTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return "0.0.0";
            var match = Regex.Match(tag, @"(\d+\.\d+\.\d+)");
            return match.Success ? match.Groups[1].Value : "0.0.0";
        }

        /// <summary>
        /// Сравнивает две версии. Возвращает true, если remote новее current
        /// </summary>
        private static bool IsNewerVersion(string remote, string current)
        {
            try
            {
                var remoteParts = remote.Split('.').Select(int.Parse).ToArray();
                var currentParts = current.Split('.').Select(int.Parse).ToArray();

                for (int i = 0; i < Math.Min(remoteParts.Length, currentParts.Length); i++)
                {
                    if (remoteParts[i] > currentParts[i]) return true;
                    if (remoteParts[i] < currentParts[i]) return false;
                }
                return remoteParts.Length > currentParts.Length;
            }
            catch
            {
                return false;
            }
        }

        // BtnCheckUpdate_Click and BtnDonate_Click are now handled via public OnCheckUpdate() / OnDonate() methods called from SettingsWindow

        private DateTime _lastUpdateCheck = DateTime.MinValue;
        private bool _isCooldownActive = false;

        private async void StartCooldownTimer()
        {
            if (_isCooldownActive) return;
            _isCooldownActive = true;
            
            try
            {
                Dispatcher.Invoke(() => { if (_settingsWindow != null) _settingsWindow.BtnCheckUpdate.IsEnabled = false; });
                
                while (true)
                {
                    double elapsed = (DateTime.Now - _lastUpdateCheck).TotalSeconds;
                    if (elapsed >= 30) break;
                    
                    int remaining = (int)Math.Ceiling(30 - elapsed);
                    Dispatcher.Invoke(() =>
                    {
                        if (_settingsWindow != null)
                            _settingsWindow.BtnCheckUpdate.Content = uiLanguage == "uk" 
                                ? $"⏳ {remaining} сек." 
                                : $"⏳ {remaining} sec";
                    });
                    
                    await Task.Delay(500);
                }
                
                Dispatcher.Invoke(() =>
                {
                    if (_settingsWindow != null)
                    {
                        _settingsWindow.BtnCheckUpdate.IsEnabled = true;
                        _settingsWindow.BtnCheckUpdate.Background = new SolidColorBrush(Color.FromRgb(67, 160, 71));
                        _settingsWindow.BtnCheckUpdate.Content = uiLanguage == "uk" ? "🔄 Перевірити оновлення" : "🔄 Check for updates";
                    }
                });
            }
            catch { }
            finally
            {
                _isCooldownActive = false;
            }
        }

        /// <summary>
        /// Проверяет наличие обновлений через GitHub API
        /// </summary>
        private async Task CheckForUpdatesAsync(bool silent)
        {
            if (_isCheckingUpdate || _isCooldownActive) return;
            
            // Защита от лимитов GitHub API (60 запросов в час)
            // При тихой проверке (старт приложения) проверяем не чаще раз в 15 минут
            if (silent && (DateTime.Now - _lastUpdateCheck).TotalMinutes < 15) return;
            // При ручной проверке обрабатывается через StartCooldownTimer (кнопка заблокирована)

            _isCheckingUpdate = true;
            bool isError = false;
            _lastUpdateCheck = DateTime.Now;

            try
            {
                Dispatcher.Invoke(() =>
                {
                    if (_settingsWindow != null)
                    {
                        _settingsWindow.BtnCheckUpdate.IsEnabled = false;
                        _settingsWindow.BtnCheckUpdate.Content = uiLanguage == "uk" ? "⏳ Перевірка..." : "⏳ Checking...";
                        _settingsWindow.UpdateStatusText.Text = "";
                    }
                });

                WriteLog("Checking for updates...");

                using var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("TruckSimWidget/" + GetCurrentVersion());
                client.Timeout = TimeSpan.FromSeconds(15);

                var response = await client.GetStringAsync(GitHubApiUrl);
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                string tagName = root.GetProperty("tag_name").GetString() ?? "";
                string releaseName = root.GetProperty("name").GetString() ?? tagName;
                string remoteVersion = ExtractVersionFromTag(tagName);
                string currentVersion = GetCurrentVersion();

                WriteLog($"Current version: {currentVersion}, Latest: {remoteVersion} ({tagName})");

                if (IsNewerVersion(remoteVersion, currentVersion))
                {
                    // Ищем ZIP-ассет в релизе
                    string? downloadUrl = null;
                    string? assetName = null;

                    if (root.TryGetProperty("assets", out var assets))
                    {
                        foreach (var asset in assets.EnumerateArray())
                        {
                            string name = asset.GetProperty("name").GetString() ?? "";
                            if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                            {
                                downloadUrl = asset.GetProperty("browser_download_url").GetString();
                                assetName = name;
                                break;
                            }
                        }
                    }

                    if (downloadUrl != null && assetName != null)
                    {
                        WriteLog($"Update available: {releaseName}, asset: {assetName}");

                        Dispatcher.Invoke(() =>
                        {
                            if (_settingsWindow != null)
                            {
                                _settingsWindow.UpdateStatusText.Foreground = new SolidColorBrush(Color.FromRgb(82, 193, 79));
                                _settingsWindow.UpdateStatusText.Text = uiLanguage == "uk"
                                    ? $"🆕 Доступне оновлення: {releaseName}"
                                    : $"🆕 Update available: {releaseName}";
                            }
                        });

                        // Показываем диалог подтверждения
                        ShowUpdateConfirmDialog(releaseName, downloadUrl, assetName);
                    }
                    else
                    {
                        WriteLog("Update found but no ZIP asset available");
                        if (!silent)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                if (_settingsWindow != null)
                                {
                                    _settingsWindow.UpdateStatusText.Foreground = new SolidColorBrush(Color.FromRgb(243, 156, 18));
                                    _settingsWindow.UpdateStatusText.Text = uiLanguage == "uk"
                                        ? "⚠ Оновлення знайдено, але файл недоступний"
                                        : "⚠ Update found but file not available";
                                }
                            });
                        }
                    }
                }
                else
                {
                    WriteLog("No updates available");
                    if (!silent)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            if (_settingsWindow != null)
                            {
                                _settingsWindow.UpdateStatusText.Foreground = new SolidColorBrush(Color.FromRgb(82, 193, 79));
                                _settingsWindow.UpdateStatusText.Text = uiLanguage == "uk"
                                    ? "✅ У вас остання версія"
                                    : "✅ You have the latest version";
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                isError = true;
                WriteLog($"Update check failed: {ex.Message}");
                if (!silent)
                {
                Dispatcher.Invoke(() =>
                {
                    if (_settingsWindow != null)
                    {
                        _settingsWindow.UpdateStatusText.Foreground = Brushes.Red;
                        _settingsWindow.UpdateStatusText.Text = uiLanguage == "uk"
                            ? "❌ Помилка перевірки оновлень"
                            : "❌ Update check failed";
                        _settingsWindow.BtnCheckUpdate.Background = new SolidColorBrush(Color.FromRgb(211, 47, 47));
                    }
                });
                }
            }
            finally
            {
                _isCheckingUpdate = false;
                
                if (!isError)
                {
                    _lastUpdateCheck = DateTime.Now;
                    if (!silent) 
                    {
                        StartCooldownTimer();
                    }
                    else
                    {
                        Dispatcher.Invoke(() =>
                        {
                            if (_settingsWindow != null)
                            {
                                _settingsWindow.BtnCheckUpdate.IsEnabled = true;
                                _settingsWindow.BtnCheckUpdate.Background = new SolidColorBrush(Color.FromRgb(67, 160, 71));
                                _settingsWindow.BtnCheckUpdate.Content = uiLanguage == "uk" ? "🔄 Перевірити оновлення" : "🔄 Check for updates";
                            }
                        });
                    }
                }
                else
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (_settingsWindow != null)
                        {
                            _settingsWindow.BtnCheckUpdate.IsEnabled = true;
                            _settingsWindow.BtnCheckUpdate.Content = uiLanguage == "uk" ? "🔄 Перевірити оновлення" : "🔄 Check for updates";
                        }
                    });
                }
            }
        }

        /// <summary>
        /// Показывает диалог подтверждения обновления
        /// </summary>
        private void ShowUpdateConfirmDialog(string releaseName, string downloadUrl, string assetName)
        {
            Dispatcher.Invoke(() =>
            {
                string title = uiLanguage == "uk" ? "Оновлення доступне" : "Update Available";
                string message = uiLanguage == "uk"
                    ? $"Доступна нова версія: {releaseName}\n\nВстановити оновлення?\n\nПрограма буде закрита для оновлення файлів."
                    : $"A new version is available: {releaseName}\n\nInstall update?\n\nThe application will close to update files.";

                string yesBtn = uiLanguage == "uk" ? "Так" : "Yes";
                string noBtn = uiLanguage == "uk" ? "Ні" : "No";

                var result = CustomMessageBox.Show(this, message, title, yesBtn, noBtn);

                if (result == MessageBoxResult.Yes)
                {
                    LaunchUpdaterAndShutdown(downloadUrl, assetName);
                }
            });
        }

        /// <summary>
        /// Запускает updater.exe, передавая URL для скачивания, и сразу закрывает виджет
        /// </summary>
        private void LaunchUpdaterAndShutdown(string downloadUrl, string assetName)
        {
            try
            {
                // Проверяем, что updater.exe существует
                string updaterPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "updater.exe");
                if (!File.Exists(updaterPath))
                {
                    // Если updater.exe не найден в папке приложения, пробуем рядом с exe
                    string? exeDir = Path.GetDirectoryName(Environment.ProcessPath);
                    if (exeDir != null)
                        updaterPath = Path.Combine(exeDir, "updater.exe");
                }

                if (!File.Exists(updaterPath))
                {
                    throw new FileNotFoundException(
                        uiLanguage == "uk"
                            ? "updater.exe не знайдено. Переконайтеся, що файл знаходиться в папці програми."
                            : "updater.exe not found. Make sure the file is in the application folder.");
                }

                string appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
                string appExe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";
                string logPath = appLogFilePath;

                WriteLog($"Launching updater: {updaterPath}");
                WriteLog($"Args: \"{downloadUrl}\" \"{assetName}\" \"{appDir}\" \"{appExe}\" \"{logPath}\" \"{uiLanguage}\"");

                // Запускаем updater.exe с URL для скачивания
                Process.Start(new ProcessStartInfo
                {
                    FileName = updaterPath,
                    Arguments = $"\"{downloadUrl}\" \"{assetName}\" \"{appDir}\" \"{appExe}\" \"{logPath}\" \"{uiLanguage}\"",
                    UseShellExecute = true
                });

                WriteLog("Updater launched, shutting down for update...");

                // Сохраняем состояние и сразу закрываем приложение
                SaveState();
                telemetry?.Dispose();
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                WriteLog($"Failed to launch updater: {ex.Message}");

                if (_settingsWindow != null)
                {
                    _settingsWindow.UpdateStatusText.Foreground = Brushes.Red;
                    _settingsWindow.UpdateStatusText.Text = uiLanguage == "uk"
                        ? $"❌ Помилка: {ex.Message}"
                        : $"❌ Error: {ex.Message}";
                    _settingsWindow.BtnCheckUpdate.IsEnabled = true;
                    _settingsWindow.BtnCheckUpdate.Content = uiLanguage == "uk" ? "🔄 Перевірити оновлення" : "🔄 Check for updates";
                }

                ShowUpdateErrorDialog(ex.Message);
            }
        }

        /// <summary>
        /// Показывает диалог ошибки обновления с кнопками копирования почты и сохранения лога
        /// </summary>
        private void ShowUpdateErrorDialog(string errorMessage)
        {
            bool isUk = uiLanguage == "uk";
            string title = isUk ? "Помилка оновлення" : "Update Error";
            string body = isUk
                ? $"Під час оновлення виникла помилка:\n{errorMessage}\n\n" +
                  $"Будь ласка, надішліть лог на {SupportEmail}\n\n" +
                  $"Скопіювати email у буфер обміну?"
                : $"An error occurred during update:\n{errorMessage}\n\n" +
                  $"Please send the log to {SupportEmail}\n\n" +
                  $"Copy email to clipboard?";

            string yesBtn = isUk ? "Так" : "Yes";
            string noBtn = isUk ? "Ні" : "No";

            var result = CustomMessageBox.Show(this, body, title, yesBtn, noBtn);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    Clipboard.SetText(SupportEmail);
                    string copied = isUk ? "Email скопійовано!" : "Email copied!";
                    if (_settingsWindow != null) _settingsWindow.UpdateStatusText.Text = $"📋 {copied}";
                }
                catch { }

                // Предлагаем сохранить лог
                try
                {
                    var dialog = new Microsoft.Win32.SaveFileDialog
                    {
                        Filter = isUk ? "Текстові файли (*.txt)|*.txt|Усі файли (*.*)|*.*"
                                      : "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                        FileName = "trucksim_widget_log.txt",
                        Title = isUk ? "Зберегти лог" : "Save log file"
                    };

                    if (dialog.ShowDialog(this) == true)
                    {
                        if (File.Exists(appLogFilePath))
                        {
                            File.Copy(appLogFilePath, dialog.FileName, overwrite: true);
                            string saved = isUk ? "Лог збережено!" : "Log saved!";
                            if (_settingsWindow != null) _settingsWindow.UpdateStatusText.Text = $"💾 {saved}";
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteLog($"Failed to save log copy: {ex.Message}");
                }
            }
        }

        // ==================== NEW METHODS FOR REDESIGNED UI ====================

        private int CurrentSpeedWarning
        {
            get => _currentGame == GameType.Ats ? _speedWarningAts : _speedWarningEts;
            set
            {
                if (_currentGame == GameType.Ats)
                    _speedWarningAts = value;
                else
                    _speedWarningEts = value;
            }
        }

        private void UpdateSpeedWarningText()
        {
            _settingsWindow?.SetSpeedWarningText(Math.Max(0, CurrentSpeedWarning).ToString());
        }

        private void ApplySpeedWarningColor()
        {
            if (int.TryParse(SpeedValue.Text, out var speed))
            {
                ApplySpeedWarningColor(speed);
            }
        }

        private void ApplySpeedWarningColor(int currentSpeed)
        {
            int threshold = Math.Max(0, CurrentSpeedWarning);
            if (threshold > 0 && currentSpeed >= threshold)
            {
                SpeedValue.Foreground = Brushes.Red;
            }
            else
            {
                SpeedValue.Foreground = Brushes.White;
            }
        }

        private void ApplyLanguageSelection()
        {
            _settingsWindow?.SetLanguage(uiLanguage);
        }

        private void UpdateSimDisplay()
        {
            // SimCard now shows TrucksBook status (dot + TbStatus text).
            // The TbStatusDot and TbStatus are updated in CheckStatusAndProcesses.
            // This method is retained as a no-op for backward compatibility with callers.
        }

        // ==================== HEADER AUTO-HIDE ====================

        public bool AutoHideHeader => true;

        private const double HeaderHoverZoneHeight = 30;
        private const double HeaderOverlayOffset = 6;
        private const double HeaderOverlayFadeSeconds = 0.2;

        private void EnsureHeaderOverlay()
        {
            if (_headerOverlay != null) return;
            _headerOverlay = new HeaderOverlayWindow(this);
            _headerOverlay.Owner = this;
            double effectiveScale = BASE_SCALE * (_uiScale / 100.0);
            _headerOverlay.SetScale(effectiveScale);
            _headerOverlay.Opacity = 1;
            _headerOverlay.SetOpacity(0);
            _headerOverlay.IsHitTestVisible = false;
            _headerOverlay.Show();
            _overlayHideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
            _overlayHideTimer.Tick += (s, e) =>
            {
                _overlayHideTimer?.Stop();
                if (!_overlayHover && !IsPointerInHoverZone())
                {
                    HideHeaderOverlay();
                }
            };
        }

        private void UpdateHeaderOverlayPosition()
        {
            if (_headerOverlay == null) return;
            var width = MainBorder.Width > 0 ? MainBorder.Width : (MainBorder.ActualWidth / Math.Max(0.1, UIScaleTransform.ScaleX));
            _headerOverlay.UpdateWidth(width);
            _headerOverlay.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var overlayHeight = _headerOverlay.DesiredSize.Height;
            var borderTopLeft = MainBorder.TranslatePoint(new Point(0, 0), this);
            var headerMargin = 16.0;
            var left = Left + borderTopLeft.X - headerMargin;
            var top = Top + borderTopLeft.Y - overlayHeight + headerMargin - HeaderOverlayOffset;
            _headerOverlay.Left = left;
            _headerOverlay.Top = top;
        }

        private void ShowHeaderOverlay()
        {
            EnsureHeaderOverlay();
            UpdateHeaderOverlayPosition();
            if (_headerOverlay == null) return;
            if (_headerOverlayVisible) return;
            _headerOverlayVisible = true;
            _headerOverlay.IsHitTestVisible = true;
            _headerOverlay.AnimateOpacity(windowOpacity, HeaderOverlayFadeSeconds);
        }

        private void HideHeaderOverlay()
        {
            if (_headerOverlay == null) return;
            if (!_headerOverlayVisible) return;
            _headerOverlayVisible = false;
            _headerOverlay.IsHitTestVisible = false;
            _headerOverlay.AnimateOpacity(0, HeaderOverlayFadeSeconds);
        }

        public void NotifyOverlayHover(bool isHovering)
        {
            _overlayHover = isHovering;
            if (isHovering)
            {
                // cancel autohide while interacting with overlay
                StopIdleTimer();
                ExitIdleState();
            }
            else
            {
                StartOverlayHideTimer();
                if (_autoHideEnabled)
                {
                    StartIdleTimer();
                }
            }
        }

        private void OnStatusLevelChanged(HealthStatus newStatus)
        {
            _currentHealth = newStatus;
            EvaluateAutoHideState();
        }

        private void StartIdleTimer()
        {
            if (_idleTimer == null || _idleTimer.IsEnabled) return;
            _idleTimer.Start();
        }

        private void StopIdleTimer()
        {
            _idleTimer?.Stop();
            _autoHideQuietMs = 0;
        }

        private bool CanAutoHideNow()
        {
            if (!_autoHideEnabled) return false;
            if (_currentHealth == HealthStatus.Error || _currentHealth == HealthStatus.Warning) return false;
            if (_overlayHover) return false;
            if (IsPointerInHoverZone()) return false;
            return true;
        }

        private void EvaluateAutoHideState()
        {
            if (!_autoHideEnabled)
            {
                StopIdleTimer();
                ExitIdleState();
                return;
            }

            if (!CanAutoHideNow())
            {
                _autoHideQuietMs = 0;
                if (_isIdle) ExitIdleState();
                return;
            }

            if (_isIdle) return;

            _autoHideQuietMs += AutoHideTickMs;
            if (_autoHideQuietMs >= IdleDelaySeconds * 1000)
            {
                _autoHideQuietMs = 0;
                EnterIdleState();
            }
        }

        private void EnterIdleState()
        {
            if (_isIdle) return;
            _isIdle = true;

            _autoHideQuietMs = 0;

            MainBorder.BeginAnimation(OpacityProperty, null);
            var idleOpacityAnim = new DoubleAnimation(IdleOpacity, TimeSpan.FromSeconds(0.18));
            MainBorder.BeginAnimation(OpacityProperty, idleOpacityAnim);

            if (_uiMode == "full")
            {
                // collapse the collapsible section (animate similar to ApplyUIMode minimal)
                // Clear any existing animations first to avoid stuck animation state
                CollapsibleSection.BeginAnimation(OpacityProperty, null);
                CollapsibleSection.BeginAnimation(HeightProperty, null);
                if (CollapsibleSection.Visibility == Visibility.Visible)
                {
                    var fadeOut = new DoubleAnimation(0, TimeSpan.FromSeconds(0.15));
                    var currentHeight = CollapsibleSection.ActualHeight;
                    CollapsibleSection.Height = currentHeight;
                    var shrink = new DoubleAnimation(currentHeight, 0, TimeSpan.FromSeconds(0.2)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } };
                    shrink.Completed += (s, ev) => 
                    { 
                        CollapsibleSection.BeginAnimation(HeightProperty, null);
                        CollapsibleSection.Visibility = Visibility.Collapsed; 
                        CollapsibleSection.Height = double.NaN;
                    };
                    CollapsibleSection.BeginAnimation(OpacityProperty, fadeOut);
                    CollapsibleSection.BeginAnimation(HeightProperty, shrink);
                }
            }
            else
            {
                // Keep minimal mode collapsed
                CollapsibleSection.BeginAnimation(OpacityProperty, null);
                CollapsibleSection.BeginAnimation(HeightProperty, null);
                CollapsibleSection.Visibility = Visibility.Collapsed;
                CollapsibleSection.Opacity = 0;
            }
        }

        private void ExitIdleState()
        {
            if (!_isIdle) return;
            _isIdle = false;

            _autoHideQuietMs = 0;

            MainBorder.BeginAnimation(OpacityProperty, null);
            var restoreOpacityAnim = new DoubleAnimation(windowOpacity, TimeSpan.FromSeconds(0.18));
            restoreOpacityAnim.Completed += (s, ev) => ApplyDualLayerOpacity();
            MainBorder.BeginAnimation(OpacityProperty, restoreOpacityAnim);

            if (_uiMode == "full")
            {
                // restore collapsible section
                CollapsibleSection.BeginAnimation(OpacityProperty, null);
                CollapsibleSection.BeginAnimation(HeightProperty, null);
                CollapsibleSection.Visibility = Visibility.Visible;
                CollapsibleSection.Height = double.NaN;
                CollapsibleSection.UpdateLayout();
                var targetHeight = CollapsibleSection.ActualHeight;
                CollapsibleSection.Height = 0;
                CollapsibleSection.Opacity = 0;
                var fadeIn = new DoubleAnimation(1, TimeSpan.FromSeconds(0.18)) { BeginTime = TimeSpan.FromSeconds(0.05) };
                var grow = new DoubleAnimation(0, targetHeight, TimeSpan.FromSeconds(0.2)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } };
                grow.Completed += (s, ev) => { CollapsibleSection.BeginAnimation(HeightProperty, null); CollapsibleSection.Height = double.NaN; };
                CollapsibleSection.BeginAnimation(OpacityProperty, fadeIn);
                CollapsibleSection.BeginAnimation(HeightProperty, grow);
            }
            else
            {
                // Keep minimal mode collapsed
                CollapsibleSection.BeginAnimation(OpacityProperty, null);
                CollapsibleSection.BeginAnimation(HeightProperty, null);
                CollapsibleSection.Visibility = Visibility.Collapsed;
                CollapsibleSection.Opacity = 0;
            }
        }

        private void HeaderHover_Enter(object sender, MouseEventArgs e)
        {
            ShowHeaderOverlay();
        }

        private void MainBorder_MouseEnter(object sender, MouseEventArgs e)
        {
            // Cancel autohide idle when pointer enters
            ExitIdleState();
            StopIdleTimer();
            UpdateOverlayVisibility(e.GetPosition(MainBorder));
        }

        private void MainBorder_MouseMove(object sender, MouseEventArgs e)
        {
            UpdateOverlayVisibility(e.GetPosition(MainBorder));
        }

        private void MainBorder_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_overlayHover) return;
            StartOverlayHideTimer();
            if (_autoHideEnabled)
            {
                StartIdleTimer();
            }
            EvaluateAutoHideState();
        }

        private void UpdateOverlayVisibility(Point position)
        {
            bool inHeaderZone = position.Y >= 0 && position.Y <= HeaderHoverZoneHeight;
            if (inHeaderZone)
            {
                if (!_headerOverlayVisible)
                {
                    ShowHeaderOverlay();
                }
            }
            else if (!_overlayHover)
            {
                StartOverlayHideTimer();
            }
        }

        private bool IsPointerInHoverZone()
        {
            if (!MainBorder.IsMouseOver) return false;
            var position = Mouse.GetPosition(MainBorder);
            return position.Y >= 0 && position.Y <= HeaderHoverZoneHeight;
        }

        private void StartOverlayHideTimer()
        {
            if (_overlayHideTimer == null) return;
            _overlayHideTimer.Stop();
            _overlayHideTimer.Start();
        }

        // Ensure autohide state consistency when applying UI mode
        private void EnsureAutohideConsistency()
        {
            if (_autoHideEnabled)
            {
                // If current status is healthy, start idle timer; otherwise ensure exit idle
                if (_currentHealth == HealthStatus.Healthy && !_overlayHover && !IsPointerInHoverZone())
                {
                    _autoHideQuietMs = 0;
                    StartIdleTimer();
                }
                else
                {
                    StopIdleTimer();
                    ExitIdleState();
                }
            }
            else
            {
                // Not autohide -> ensure no idle state remains
                StopIdleTimer();
                ExitIdleState();
            }
        }


    }
}
