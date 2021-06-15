using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LogAnalyzerWPF.Controls
{
    /// <summary>
    /// Interaction logic for TabItemNew.xaml
    /// </summary>
    public partial class TabItemNew : UserControl
    {
        internal delegate void RepoCreatedEventHandler(Repo repo, string repoDesc, string rootFolder);
        internal event RepoCreatedEventHandler RepoCreatedEvent;

        public TabItemNew()
        {
            InitializeComponent();

            ApplicationCommands.Paste.CanExecuteChanged += (s, e) =>
            {
                mGroupClipboard.IsEnabled = Clipboard.ContainsText();
            };

            mGroupClipboard.IsEnabled = Clipboard.ContainsText();
        }

        private static Repo initRepoFromClipbard(Repo.FlavorType flavorType)
        {
            if (!Clipboard.ContainsText())
                return null;

            var tempPath = Path.GetTempFileName();

            {
                var textContent = Clipboard.GetText();
                textContent = textContent.Replace("\r\n", "\n");
                File.WriteAllBytes(tempPath, Encoding.UTF8.GetBytes(textContent));
            }

            var newRepo = Repo.InitRepoFile(flavorType, tempPath);
            if ((newRepo == null) || (newRepo.NumLines <= 0))
            {
                newRepo?.Dispose();
                File.Delete(tempPath);
                return null;
            }

            return newRepo;
        }

        private void onComlibFiles(object sender, RoutedEventArgs e)
        {
            var diag = new OpenFileDialog();

            diag.ValidateNames = true;
            diag.Multiselect = false;
            diag.CheckFileExists = true;
            diag.CheckPathExists = true;
            diag.RestoreDirectory = true;
            diag.Filter = "All files (*.*)|*.*";
            diag.Title = "COMLib log file";
            if ((!diag.ShowDialog() ?? false))
                return;

            var newRepo = Repo.InitRepoFile(Repo.FlavorType.WCSComlib, diag.FileName);
            if ((newRepo == null) || (newRepo.NumLines <= 0))
            {
                AdonisUI.Controls.MessageBox.Show(Window.GetWindow(this), "Unable to read COMLib log data from specified file.", "No data available", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Exclamation);
                return;
            }

            var eHandler = RepoCreatedEvent;
            eHandler?.Invoke(newRepo, diag.FileName, null);

        }

        private void onComlibFolder(object sender, RoutedEventArgs e)
        {
            using (var diag = new CommonOpenFileDialog())
            {
                diag.IsFolderPicker = true;
                diag.Multiselect = false;
                diag.EnsurePathExists = true;
                diag.RestoreDirectory = true;
                diag.Title = "Folder with COMLib logs";
                if (diag.ShowDialog() != CommonFileDialogResult.Ok)
                    return;

                var newRepo = Repo.InitRepoFolder(Repo.FlavorType.WCSComlib, diag.FileName);
                if ((newRepo == null) || (newRepo.NumLines <= 0))
                {
                    AdonisUI.Controls.MessageBox.Show(Window.GetWindow(this), "Unable to find any valid files or files aren't of type COMLib.", "No data available", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Exclamation);
                    return;
                }

                var eHandler = RepoCreatedEvent;
                eHandler?.Invoke(newRepo, diag.FileName, diag.FileName);
            }
        }

        private void onCOMLibDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return;

            var droppedFiles = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (droppedFiles.Length <= 0)
                return;
            if (droppedFiles.Length > 1)
            {
                AdonisUI.Controls.MessageBox.Show(Window.GetWindow(this), "Only a single file or folder can be dropped here", "No data available", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Exclamation);
                return;
            }

            var isFile = File.Exists(droppedFiles[0]);
            var isFolder = !isFile && Directory.Exists(droppedFiles[0]);
            if (!isFile && !isFolder)
            {
                AdonisUI.Controls.MessageBox.Show(Window.GetWindow(this), "Unknown typed of dropped object", "No data available", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Exclamation);
                return;
            }

            var newRepo = isFile ? Repo.InitRepoFile(Repo.FlavorType.WCSComlib, droppedFiles[0]) : Repo.InitRepoFolder(Repo.FlavorType.WCSComlib, droppedFiles[0]);
            if ((newRepo == null) || (newRepo.NumLines <= 0))
            {
                AdonisUI.Controls.MessageBox.Show(Window.GetWindow(this), "Unable to find any valid files or files aren't of type COMLib.", "No data available", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Exclamation);
                return;
            }

            var eHandler = RepoCreatedEvent;
            eHandler?.Invoke(newRepo, droppedFiles[0], isFolder ? droppedFiles[0] : null);
        }

        private void onServerFiles(object sender, RoutedEventArgs e)
        {
            var diag = new OpenFileDialog();

            diag.ValidateNames = true;
            diag.Multiselect = false;
            diag.CheckFileExists = true;
            diag.CheckPathExists = true;
            diag.RestoreDirectory = true;
            diag.Filter = "All files (*.*)|*.*";
            diag.Title = "Server log file";
            if ((!diag.ShowDialog() ?? false))
                return;

            var newRepo = Repo.InitRepoFile(Repo.FlavorType.WCSServer, diag.FileName);
            if ((newRepo == null) || (newRepo.NumLines <= 0))
            {
                AdonisUI.Controls.MessageBox.Show(Window.GetWindow(this), "Unable to read server log data from specified file.", "No data available", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Exclamation);
                return;
            }

            var eHandler = RepoCreatedEvent;
            eHandler?.Invoke(newRepo, diag.FileName, null);
        }

        private void onServerFolder(object sender, RoutedEventArgs e)
        {
            using (var diag = new CommonOpenFileDialog())
            {
                diag.IsFolderPicker = true;
                diag.Multiselect = false;
                diag.EnsurePathExists = true;
                diag.RestoreDirectory = true;
                diag.Title = "Folder with server logs";
                if (diag.ShowDialog() != CommonFileDialogResult.Ok)
                    return;

                var newRepo = Repo.InitRepoFolder(Repo.FlavorType.WCSServer, diag.FileName);
                if ((newRepo == null) || (newRepo.NumLines <= 0))
                {
                    AdonisUI.Controls.MessageBox.Show(Window.GetWindow(this), "Unable to find any valid files or files aren't of type server.", "No data available", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Exclamation);
                    return;
                }

                var eHandler = RepoCreatedEvent;
                eHandler?.Invoke(newRepo, diag.FileName, diag.FileName);
            }
        }

        private void onServerDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return;

            var droppedFiles = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (droppedFiles.Length <= 0)
                return;
            if (droppedFiles.Length > 1)
            {
                AdonisUI.Controls.MessageBox.Show(Window.GetWindow(this), "Only a single file or folder can be dropped here", "No data available", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Exclamation);
                return;
            }

            var isFile = File.Exists(droppedFiles[0]);
            var isFolder = !isFile && Directory.Exists(droppedFiles[0]);
            if (!isFile && !isFolder)
            {
                AdonisUI.Controls.MessageBox.Show(Window.GetWindow(this), "Unknown typed of dropped object", "No data available", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Exclamation);
                return;
            }

            var newRepo = isFile ? Repo.InitRepoFile(Repo.FlavorType.WCSServer, droppedFiles[0]) : Repo.InitRepoFolder(Repo.FlavorType.WCSServer, droppedFiles[0]);
            if ((newRepo == null) || (newRepo.NumLines <= 0))
            {
                AdonisUI.Controls.MessageBox.Show(Window.GetWindow(this), "Unable to find any valid files or files aren't of type server.", "No data available", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Exclamation);
                return;
            }

            var eHandler = RepoCreatedEvent;
            eHandler?.Invoke(newRepo, droppedFiles[0], isFolder ? droppedFiles[0] : null);
        }

        private void onComlibClipboard(object sender, RoutedEventArgs e)
        {
            var newRepo = initRepoFromClipbard(Repo.FlavorType.WCSComlib);
            if (newRepo != null)
            {
                var eHandler = RepoCreatedEvent;
                eHandler?.Invoke(newRepo, "COMLib (clipboard)", null);
            }
            else
            {
                AdonisUI.Controls.MessageBox.Show(Window.GetWindow(this), "Unable to process clipboard data as COMLib log file.", "No data available", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Exclamation);
                return;
            }
        }

        private void onAndroidClipboard(object sender, RoutedEventArgs e)
        {
            var newRepo = initRepoFromClipbard(Repo.FlavorType.WCSAndroidLogcat);
            if (newRepo != null)
            {
                var eHandler = RepoCreatedEvent;
                eHandler?.Invoke(newRepo, "Android Logcat (clipboard)", null);
            }
            else
            {
                AdonisUI.Controls.MessageBox.Show(Window.GetWindow(this), "Unable to process clipboard data as Android Logcat log file.", "No data available", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Exclamation);
                return;
            }
        }

        private void onServerClipboard(object sender, RoutedEventArgs e)
        {
            var newRepo = initRepoFromClipbard(Repo.FlavorType.WCSServer);
            if (newRepo != null)
            {
                var eHandler = RepoCreatedEvent;
                eHandler?.Invoke(newRepo, "Server (clipboard)", null);
            }
            else
            {
                AdonisUI.Controls.MessageBox.Show(Window.GetWindow(this), "Unable to process clipboard data as server log file.", "No data available", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Exclamation);
                return;
            }
        }
    }
}
