using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using TruckSimWidgetSetup.Common;
using TruckSimWidgetSetup.FileManager;
using TruckSimWidgetSetup.GameDiscovery;
using TruckSimWidgetSetup.InstallationState;
using TruckSimWidgetSetup.InstallerCore;
using TruckSimWidgetSetup.PluginManager;

namespace TruckSimWidgetSetup.UI.ViewModels;

public enum WizardStep
{
    Welcome,
    Manage,
    TelemetrySelection,
    GameDirectories,
    ConflictResolution,
    Installing,
    Finished,
    UninstallConfirm,
    Uninstalling,
    UninstallFinished
}

public class InstallerViewModel : INotifyPropertyChanged
{
    private WizardStep _currentStep;
    private string _statusMessage = "Ready";
    private double _progressPercentage;
    private bool _isBusy;
    private bool _createDesktopShortcut;
    private bool _launchAppAfter = true;
    private bool _keepUserData = true;
    private string _installedVersion = "None";
    private string _packageVersion = "1.6.4-beta.1";
    private string _errorMessage = string.Empty;

    public InstallOptions Options { get; }
    public InstallationInfo InstallInfo { get; }
    public GameConfig Ets2Config => Options.Ets2Config;
    public GameConfig AtsConfig => Options.AtsConfig;

    public bool HasEts2Conflict => Ets2Config.UserSelected && Ets2Config.ExistingFileFound &&
                                   Ets2Config.OwnershipStatus != PluginOwnershipStatus.Owned;

    public bool HasAtsConflict => AtsConfig.UserSelected && AtsConfig.ExistingFileFound &&
                                 AtsConfig.OwnershipStatus != PluginOwnershipStatus.Owned;

    public bool HasAnyConflict => HasEts2Conflict || HasAtsConflict;

    public WizardStep CurrentStep
    {
        get => _currentStep;
        set { _currentStep = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanGoNext)); OnPropertyChanged(nameof(CanGoBack)); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public double ProgressPercentage
    {
        get => _progressPercentage;
        set { _progressPercentage = value; OnPropertyChanged(); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanGoNext)); OnPropertyChanged(nameof(CanGoBack)); }
    }

    public bool CreateDesktopShortcut
    {
        get => _createDesktopShortcut;
        set { _createDesktopShortcut = value; Options.CreateDesktopShortcut = value; OnPropertyChanged(); }
    }

    public bool LaunchAppAfter
    {
        get => _launchAppAfter;
        set { _launchAppAfter = value; Options.LaunchAppAfter = value; OnPropertyChanged(); }
    }

    public bool KeepUserData
    {
        get => _keepUserData;
        set { _keepUserData = value; Options.RemoveUserDataOnUninstall = !value; OnPropertyChanged(); }
    }

    public string InstalledVersion
    {
        get => _installedVersion;
        set { _installedVersion = value; OnPropertyChanged(); }
    }

    public string PackageVersion
    {
        get => _packageVersion;
        set { _packageVersion = value; OnPropertyChanged(); }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set { _errorMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool CanGoBack => !IsBusy && CurrentStep != WizardStep.Welcome && CurrentStep != WizardStep.Manage &&
                             CurrentStep != WizardStep.Installing && CurrentStep != WizardStep.Finished &&
                             CurrentStep != WizardStep.Uninstalling && CurrentStep != WizardStep.UninstallFinished;

    public bool CanGoNext => !IsBusy;

    public InstallerViewModel(InstallOptions options, InstallationInfo installInfo)
    {
        Options = options;
        InstallInfo = installInfo;

        InstalledVersion = !string.IsNullOrEmpty(installInfo.InstalledVersion)
            ? installInfo.InstalledVersion
            : "None";

        try
        {
            using var payload = new EmbeddedPayloadProvider(options.CustomSourceDir, options.CustomZipPath);
            PackageVersion = payload.Manifest.Version;
        }
        catch { }

        // Multi-stage discovery if not previously configured
        InitializeGameDiscovery();

        // Set initial step
        if (options.IsUninstallMode)
        {
            CurrentStep = WizardStep.UninstallConfirm;
        }
        else if (installInfo.IsInstalled && !options.IsUpdateMode && !options.IsReinstallMode)
        {
            CurrentStep = WizardStep.Manage;
        }
        else
        {
            CurrentStep = WizardStep.Welcome;
        }
    }

    private void InitializeGameDiscovery()
    {
        // 1. Read existing saved state if present
        if (InstallInfo.ExistingState != null)
        {
            if (InstallInfo.ExistingState.Games.TryGetValue(Constants.GameIdEts2, out var ets2State))
            {
                Ets2Config.SelectedPath = ets2State.GamePath;
                Ets2Config.UserSelected = ets2State.Configured;
                Ets2Config.OwnershipStatus = ets2State.OwnershipStatus;
                Ets2Config.ExistingFileHash = ets2State.InstalledPluginHash;
                Ets2Config.BackupPath = ets2State.BackupPath;
            }

            if (InstallInfo.ExistingState.Games.TryGetValue(Constants.GameIdAts, out var atsState))
            {
                AtsConfig.SelectedPath = atsState.GamePath;
                AtsConfig.UserSelected = atsState.Configured;
                AtsConfig.OwnershipStatus = atsState.OwnershipStatus;
                AtsConfig.ExistingFileHash = atsState.InstalledPluginHash;
                AtsConfig.BackupPath = atsState.BackupPath;
            }
        }

        // 2. Discover ETS2 if path not yet set
        if (string.IsNullOrEmpty(Ets2Config.SelectedPath))
        {
            string detected = SteamFinder.DetectGameInstallation(
                Constants.GameIdEts2,
                Constants.SteamAppIdEts2,
                Constants.GameExeEts2,
                Constants.GameDirNameEts2,
                out _);

            if (!string.IsNullOrEmpty(detected))
            {
                Ets2Config.DetectedPath = detected;
                Ets2Config.SelectedPath = detected;
                Ets2Config.UserSelected = true;
            }
        }

        // 3. Discover ATS if path not yet set
        if (string.IsNullOrEmpty(AtsConfig.SelectedPath))
        {
            string detected = SteamFinder.DetectGameInstallation(
                Constants.GameIdAts,
                Constants.SteamAppIdAts,
                Constants.GameExeAts,
                Constants.GameDirNameAts,
                out _);

            if (!string.IsNullOrEmpty(detected))
            {
                AtsConfig.DetectedPath = detected;
                AtsConfig.SelectedPath = detected;
                AtsConfig.UserSelected = true;
            }
        }
    }

    public void MoveNext()
    {
        ErrorMessage = string.Empty;

        switch (CurrentStep)
        {
            case WizardStep.Welcome:
                CurrentStep = WizardStep.TelemetrySelection;
                break;

            case WizardStep.TelemetrySelection:
                if (!Ets2Config.UserSelected && !AtsConfig.UserSelected)
                {
                    // Skip game dirs and conflicts directly to installation
                    _ = StartInstallationAsync();
                }
                else
                {
                    CurrentStep = WizardStep.GameDirectories;
                }
                break;

            case WizardStep.GameDirectories:
                if (!ValidateGamePaths()) return;

                // Evaluate plugin ownership
                EvaluatePluginOwnerships();

                if (HasAnyConflict)
                {
                    CurrentStep = WizardStep.ConflictResolution;
                }
                else
                {
                    _ = StartInstallationAsync();
                }
                break;

            case WizardStep.ConflictResolution:
                _ = StartInstallationAsync();
                break;

            case WizardStep.Manage:
                // User pressed Reinstall or Update from manage view
                break;
        }
    }

    public void MoveBack()
    {
        ErrorMessage = string.Empty;

        switch (CurrentStep)
        {
            case WizardStep.TelemetrySelection:
                CurrentStep = WizardStep.Welcome;
                break;

            case WizardStep.GameDirectories:
                CurrentStep = WizardStep.TelemetrySelection;
                break;

            case WizardStep.ConflictResolution:
                CurrentStep = WizardStep.GameDirectories;
                break;
        }
    }

    private bool ValidateGamePaths()
    {
        if (Ets2Config.UserSelected)
        {
            if (!GameValidator.ValidateGameFolder(Ets2Config.SelectedPath, Constants.GameExeEts2, out string err))
            {
                ErrorMessage = err;
                return false;
            }

            if (!GameValidator.IsDirectoryWritable(Path.Combine(Ets2Config.SelectedPath, Constants.GameRelBinDir)))
            {
                Ets2Config.NeedsElevation = true;
            }
        }

        if (AtsConfig.UserSelected)
        {
            if (!GameValidator.ValidateGameFolder(AtsConfig.SelectedPath, Constants.GameExeAts, out string err))
            {
                ErrorMessage = err;
                return false;
            }

            if (!GameValidator.IsDirectoryWritable(Path.Combine(AtsConfig.SelectedPath, Constants.GameRelBinDir)))
            {
                AtsConfig.NeedsElevation = true;
            }
        }

        return true;
    }

    private void EvaluatePluginOwnerships()
    {
        string bundledHash = string.Empty;
        try
        {
            using var payload = new EmbeddedPayloadProvider(Options.CustomSourceDir, Options.CustomZipPath);
            using var s = payload.OpenTelemetryPluginStream();
            if (s != null)
            {
                using var sha = System.Security.Cryptography.SHA256.Create();
                bundledHash = Convert.ToHexString(sha.ComputeHash(s)).ToLowerInvariant();
            }
        }
        catch { }

        if (Ets2Config.UserSelected)
        {
            TelemetryPluginManager.EvaluatePluginOwnership(Ets2Config, bundledHash);
            if (HasEts2Conflict && Ets2Config.ConflictAction == PluginConflictAction.None)
            {
                Ets2Config.ConflictAction = PluginConflictAction.BackupReplace;
            }
        }

        if (AtsConfig.UserSelected)
        {
            TelemetryPluginManager.EvaluatePluginOwnership(AtsConfig, bundledHash);
            if (HasAtsConflict && AtsConfig.ConflictAction == PluginConflictAction.None)
            {
                AtsConfig.ConflictAction = PluginConflictAction.BackupReplace;
            }
        }

        OnPropertyChanged(nameof(HasEts2Conflict));
        OnPropertyChanged(nameof(HasAtsConflict));
        OnPropertyChanged(nameof(HasAnyConflict));
    }

    public async Task StartInstallationAsync()
    {
        CurrentStep = WizardStep.Installing;
        IsBusy = true;
        ProgressPercentage = 0;

        var prog = new Progress<double>(p => ProgressPercentage = p * 100);
        var status = new Progress<string>(s => StatusMessage = s);

        bool success = await InstallerService.ExecuteInstallOrUpdateAsync(Options, InstallInfo, prog, status);

        IsBusy = false;
        if (success)
        {
            CurrentStep = WizardStep.Finished;
        }
        else
        {
            ErrorMessage = "Installation encountered an error and was rolled back.";
        }
    }

    public async Task StartUninstallAsync()
    {
        CurrentStep = WizardStep.Uninstalling;
        IsBusy = true;
        ProgressPercentage = 0;

        var prog = new Progress<double>(p => ProgressPercentage = p * 100);
        var status = new Progress<string>(s => StatusMessage = s);

        bool success = await InstallerService.ExecuteUninstallAsync(Options.RemoveUserDataOnUninstall, prog, status);

        IsBusy = false;
        if (success)
        {
            CurrentStep = WizardStep.UninstallFinished;
        }
        else
        {
            ErrorMessage = "Uninstallation encountered an error.";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
