using System.Windows;
using System.Windows.Input;
using TruckSimWidgetSetup.PluginManager;
using TruckSimWidgetSetup.UI.ViewModels;

namespace TruckSimWidgetSetup;

public partial class MainWindow : Window
{
    public InstallerViewModel ViewModel { get; }

    public MainWindow(InstallerViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = ViewModel;

        ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ViewModel.CurrentStep))
            {
                UpdateViewVisibility();
            }
        };

        UpdateViewVisibility();
    }

    private void UpdateViewVisibility()
    {
        WelcomePanel.Visibility = ViewModel.CurrentStep == WizardStep.Welcome ? Visibility.Visible : Visibility.Collapsed;
        ManagePanel.Visibility = ViewModel.CurrentStep == WizardStep.Manage ? Visibility.Visible : Visibility.Collapsed;
        TelemetrySelectionPanel.Visibility = ViewModel.CurrentStep == WizardStep.TelemetrySelection ? Visibility.Visible : Visibility.Collapsed;
        GameDirectoriesPanel.Visibility = ViewModel.CurrentStep == WizardStep.GameDirectories ? Visibility.Visible : Visibility.Collapsed;
        ConflictResolutionPanel.Visibility = ViewModel.CurrentStep == WizardStep.ConflictResolution ? Visibility.Visible : Visibility.Collapsed;
        InstallingPanel.Visibility = ViewModel.CurrentStep == WizardStep.Installing ? Visibility.Visible : Visibility.Collapsed;
        FinishedPanel.Visibility = ViewModel.CurrentStep == WizardStep.Finished ? Visibility.Visible : Visibility.Collapsed;
        UninstallConfirmPanel.Visibility = ViewModel.CurrentStep == WizardStep.UninstallConfirm ? Visibility.Visible : Visibility.Collapsed;
        UninstallingPanel.Visibility = ViewModel.CurrentStep == WizardStep.Uninstalling ? Visibility.Visible : Visibility.Collapsed;
        UninstallFinishedPanel.Visibility = ViewModel.CurrentStep == WizardStep.UninstallFinished ? Visibility.Visible : Visibility.Collapsed;

        // Button texts & visibility
        BtnBack.Visibility = (ViewModel.CurrentStep == WizardStep.Welcome ||
                              ViewModel.CurrentStep == WizardStep.Manage ||
                              ViewModel.CurrentStep == WizardStep.Installing ||
                              ViewModel.CurrentStep == WizardStep.Finished ||
                              ViewModel.CurrentStep == WizardStep.UninstallConfirm ||
                              ViewModel.CurrentStep == WizardStep.Uninstalling ||
                              ViewModel.CurrentStep == WizardStep.UninstallFinished)
            ? Visibility.Collapsed
            : Visibility.Visible;

        if (ViewModel.CurrentStep == WizardStep.Finished || ViewModel.CurrentStep == WizardStep.UninstallFinished)
        {
            BtnNext.Content = "Finish";
            BtnCancel.Visibility = Visibility.Collapsed;
        }
        else if (ViewModel.CurrentStep == WizardStep.UninstallConfirm)
        {
            BtnNext.Content = "Uninstall";
            BtnCancel.Visibility = Visibility.Visible;
        }
        else if (ViewModel.CurrentStep == WizardStep.GameDirectories && !ViewModel.HasAnyConflict)
        {
            BtnNext.Content = "Install";
            BtnCancel.Visibility = Visibility.Visible;
        }
        else if (ViewModel.CurrentStep == WizardStep.ConflictResolution)
        {
            BtnNext.Content = "Install";
            BtnCancel.Visibility = Visibility.Visible;
        }
        else if (ViewModel.CurrentStep == WizardStep.Installing || ViewModel.CurrentStep == WizardStep.Uninstalling)
        {
            BtnNext.Visibility = Visibility.Collapsed;
            BtnCancel.Visibility = Visibility.Collapsed;
        }
        else if (ViewModel.CurrentStep == WizardStep.Manage)
        {
            BtnNext.Visibility = Visibility.Collapsed;
            BtnCancel.Content = "Close";
            BtnCancel.Visibility = Visibility.Visible;
        }
        else
        {
            BtnNext.Content = "Next";
            BtnNext.Visibility = Visibility.Visible;
            BtnCancel.Visibility = Visibility.Visible;
        }
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.MoveBack();
    }

    private void BtnNext_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CurrentStep == WizardStep.Finished || ViewModel.CurrentStep == WizardStep.UninstallFinished)
        {
            Close();
            return;
        }

        if (ViewModel.CurrentStep == WizardStep.UninstallConfirm)
        {
            _ = ViewModel.StartUninstallAsync();
            return;
        }

        ViewModel.MoveNext();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void BtnReinstall_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.Options.IsReinstallMode = true;
        _ = ViewModel.StartInstallationAsync();
    }

    private void BtnUpdate_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.Options.IsUpdateMode = true;
        _ = ViewModel.StartInstallationAsync();
    }

    private void BtnUninstall_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CurrentStep = WizardStep.UninstallConfirm;
    }

    private void BtnBrowseEts2_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select Euro Truck Simulator 2 Installation Folder",
            UseDescriptionForTitle = true,
            InitialDirectory = ViewModel.Ets2Config.SelectedPath
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            ViewModel.Ets2Config.SelectedPath = dialog.SelectedPath;
        }
    }

    private void BtnBrowseAts_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select American Truck Simulator Installation Folder",
            UseDescriptionForTitle = true,
            InitialDirectory = ViewModel.AtsConfig.SelectedPath
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            ViewModel.AtsConfig.SelectedPath = dialog.SelectedPath;
        }
    }

    private void RadioConflictBackup_Checked(object sender, RoutedEventArgs e)
    {
        if (ViewModel.HasEts2Conflict) ViewModel.Ets2Config.ConflictAction = PluginConflictAction.BackupReplace;
        if (ViewModel.HasAtsConflict) ViewModel.AtsConfig.ConflictAction = PluginConflictAction.BackupReplace;
    }

    private void RadioConflictKeep_Checked(object sender, RoutedEventArgs e)
    {
        if (ViewModel.HasEts2Conflict) ViewModel.Ets2Config.ConflictAction = PluginConflictAction.KeepExisting;
        if (ViewModel.HasAtsConflict) ViewModel.AtsConfig.ConflictAction = PluginConflictAction.KeepExisting;
    }

    private void RadioConflictOverwrite_Checked(object sender, RoutedEventArgs e)
    {
        if (ViewModel.HasEts2Conflict) ViewModel.Ets2Config.ConflictAction = PluginConflictAction.Overwrite;
        if (ViewModel.HasAtsConflict) ViewModel.AtsConfig.ConflictAction = PluginConflictAction.Overwrite;
    }
}
