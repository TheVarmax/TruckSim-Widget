using System.Windows;
using System.Windows.Input;

namespace ETSOverlay
{
    public partial class CloudSyncConflictDialog : Window
    {
        public enum ConflictResult
        {
            Download,
            Overwrite,
            Cancel
        }

        public ConflictResult Result { get; private set; } = ConflictResult.Cancel;

        public CloudSyncConflictDialog()
        {
            InitializeComponent();
            MouseLeftButtonDown += (s, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
        }

        private void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            Result = ConflictResult.Download;
            DialogResult = true;
            Close();
        }

        private void BtnOverwrite_Click(object sender, RoutedEventArgs e)
        {
            Result = ConflictResult.Overwrite;
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Result = ConflictResult.Cancel;
            DialogResult = false;
            Close();
        }
    }
}
