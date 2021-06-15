using Microsoft.Win32;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LogAnalyzerWPF.Controls
{
    /// <summary>
    /// Interaction logic for CommandResults.xaml
    /// </summary>
    public partial class CommandResults : UserControl
    {
        #region Events
        internal delegate void CommandCountEventHandler(int commandCount);
        internal event CommandCountEventHandler CommandCountEvent;

        internal delegate void NewRepoTagsEventHandler(Repo repo, string repoDesc);
        internal event NewRepoTagsEventHandler NewRepoTagsEvent;

        internal delegate void SelectedLineEventHandler(int lineIndex);
        internal event SelectedLineEventHandler SelectedLineEvent;
        #endregion

        public static RoutedCommand CommandNavPrev = new RoutedCommand();
        public static RoutedCommand CommandNavProx = new RoutedCommand();
        public static RoutedCommand CommandExportLines = new RoutedCommand();
        public static RoutedCommand CommandExportNetwork = new RoutedCommand();
        public static RoutedCommand CommandShowExtraInfo = new RoutedCommand();
        public static RoutedCommand CommandClose = new RoutedCommand();

        public class CommandResult : INotifyPropertyChanged
        {
            public string RawJson { get; }
            public string OutputJson { get; }

            public bool IsValid { get; }
            public bool HasLineIndices { get; }
            public bool HasNetworkData { get; }
            public bool HasExtraInfo { get => !string.IsNullOrWhiteSpace(OutputJson); }

            public List<int> Lines { get; } = new List<int>();

            public string CommandTag { get; }
            public string CommandName { get; }
            public string CommandParams { get; }

            public string NavigationStatus { get => (HasLineIndices ? $"{mSelectedLineIndex + 1} / {Lines.Count}" : ""); }

            private bool mDrawBottomSeparator = true;
            public bool DrawBottomSeparator
            {
                get => mDrawBottomSeparator;
                set
                {
                    if (mDrawBottomSeparator == value)
                        return;

                    mDrawBottomSeparator = value;
                    OnPropertyChanged();
                }
            }

            private int mSelectedLineIndex = 0;
            public int SelectedLineIndex
            {
                get => mSelectedLineIndex;
                set
                {
                    if (Lines.Count <= 0)
                        return;

                    if ((mSelectedLineIndex == value) || (value < 0) || (value >= Lines.Count))
                        return;

                    mSelectedLineIndex = value;
                    OnPropertyChanged();
                    OnPropertyChanged("NavigationStatus");
                }
            }

            public CommandResult(string cmdResult)
            {
                RawJson = cmdResult;

                using (var json = JsonDocument.Parse(cmdResult))
                {
                    var jRoot = json.RootElement;

                    {
                        var jCmd = jRoot.GetProperty("command");

                        CommandTag = jCmd.GetProperty("tag").GetString();
                        CommandName = jCmd.GetProperty("name").GetString();

                        var cmdParams = jCmd.GetProperty("params").GetString();
                        if (!string.IsNullOrWhiteSpace(cmdParams))
                            CommandParams = $"(\"{cmdParams}\")";
                    }

                    IsValid = jRoot.GetProperty("executed").GetBoolean();
                    HasLineIndices = (jRoot.GetProperty("linesIndices").ValueKind == JsonValueKind.Array) && (jRoot.GetProperty("linesIndices").GetArrayLength() > 0);
                    HasNetworkData = (jRoot.GetProperty("networkPackets").ValueKind == JsonValueKind.Array) && (jRoot.GetProperty("networkPackets").GetArrayLength() > 0);

                    if (HasLineIndices)
                    {
                        foreach (var jsonIndices in jRoot.GetProperty("linesIndices").EnumerateArray())
                        {
                            foreach (var jsonIndex in jsonIndices.GetProperty("indices").EnumerateArray())
                                Lines.Add(jsonIndex.GetInt32());
                        }
                    }

                    JsonElement jsonOutput;
                    if (jRoot.TryGetProperty("output", out jsonOutput))
                        OutputJson = jsonOutput.GetRawText();
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;

            protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private Repo mRepo;
        private ObservableCollection<CommandResult> mCmdResults = new ObservableCollection<CommandResult>();

        public int CountCommands { get => mCmdResults.Count; }

        public CommandResults(Repo repo)
        {
            InitializeComponent();

            mRepo = repo;
            mListBox.ItemsSource = mCmdResults;

            mListBox.SelectionChanged += (s, e) =>
            {
                var cmdResult = mListBox.SelectedItem as CommandResult;
                if ((cmdResult == null) || !cmdResult.HasLineIndices || (cmdResult.SelectedLineIndex >= cmdResult.Lines.Count))
                    return;

                var eHandler = SelectedLineEvent;
                eHandler?.Invoke(cmdResult.Lines[cmdResult.SelectedLineIndex]);
            };
        }

        public void AddCommandResult(string cmdResult)
        {
            var newCmd = new CommandResult(cmdResult);
            if (!newCmd.HasLineIndices && !newCmd.HasNetworkData)
                return;

            mCmdResults.Add(newCmd);
            for (int i = 0; i < mCmdResults.Count - 1; i++)
                mCmdResults[i].DrawBottomSeparator = true;
            mCmdResults[mCmdResults.Count - 1].DrawBottomSeparator = false;

            var eHandler = CommandCountEvent;
            eHandler?.Invoke(mCmdResults.Count);
        }

        private void onCommandNavPrev(object sender, ExecutedRoutedEventArgs e)
        {
            var cmdResult = (e.Parameter as CommandResult);
            if ((cmdResult == null) || !cmdResult.HasLineIndices)
                return;

            mListBox.SelectedItem = cmdResult;

            cmdResult.SelectedLineIndex--;
            if ((cmdResult == null) || (cmdResult.SelectedLineIndex < 0) || (cmdResult.SelectedLineIndex >= cmdResult.Lines.Count))
                return;

            var eHandler = SelectedLineEvent;
            eHandler?.Invoke(cmdResult.Lines[cmdResult.SelectedLineIndex]);
        }

        private void onCommandNavProx(object sender, ExecutedRoutedEventArgs e)
        {
            var cmdResult = (e.Parameter as CommandResult);
            if ((cmdResult == null) || !cmdResult.HasLineIndices)
                return;

            mListBox.SelectedItem = cmdResult;

            cmdResult.SelectedLineIndex++;
            if ((cmdResult == null) || (cmdResult.SelectedLineIndex < 0) || (cmdResult.SelectedLineIndex >= cmdResult.Lines.Count))
                return;

            var eHandler = SelectedLineEvent;
            eHandler?.Invoke(cmdResult.Lines[cmdResult.SelectedLineIndex]);
        }

        private void onCommandExportLines(object sender, ExecutedRoutedEventArgs e)
        {
            var cmdResult = (e.Parameter as CommandResult);
            if ((cmdResult == null) || (cmdResult.RawJson == ""))
                return;

            var newRepo = Repo.InitRepoCommand(mRepo, cmdResult.RawJson);
            if (newRepo == null)
            {
                AdonisUI.Controls.MessageBox.Show(Window.GetWindow(this), "Failed to create a new repo from command result.", "Init new repo", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Exclamation);
                return;
            }

            var eHandler = NewRepoTagsEvent;
            eHandler?.Invoke(newRepo, string.IsNullOrWhiteSpace(cmdResult.CommandParams) ? $"{cmdResult.CommandTag} - {cmdResult.CommandName}" : $"{cmdResult.CommandTag} - {cmdResult.CommandName} {cmdResult.CommandParams}");
        }

        private void onCommandExportNetwork(object sender, ExecutedRoutedEventArgs e)
        {
            var cmdResult = (e.Parameter as CommandResult);
            if ((cmdResult == null) || (cmdResult.RawJson == ""))
                return;

            SaveFileDialog fileDiag = new SaveFileDialog();
            fileDiag.Title = "Save network data to PCAP file";
            fileDiag.AddExtension = true;
            fileDiag.OverwritePrompt = true;
            fileDiag.Filter = "PCAP file (*.pcap)|*.pcap";
            if (fileDiag.ShowDialog(Window.GetWindow(this)) == false)
                return;

            if (!mRepo.ExportCommandNetworkPackets(fileDiag.FileName, cmdResult.RawJson))
                AdonisUI.Controls.MessageBox.Show(Window.GetWindow(this), "Failed to save command network information.", "Save network information", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Exclamation);
        }

        private void onCommandShowExtraInfo(object sender, ExecutedRoutedEventArgs e)
        {
            var cmdResult = (e.Parameter as CommandResult);
            if ((cmdResult == null) || !cmdResult.HasExtraInfo || string.IsNullOrWhiteSpace(cmdResult.OutputJson))
                return;

            var windowHelp = new Windows.WindowCommandGeneric(cmdResult.OutputJson);
            windowHelp.Owner = Window.GetWindow(this);
            windowHelp.ShowDialog();
        }

        private void onCommandClose(object sender, ExecutedRoutedEventArgs e)
        {
            var cmdResult = (e.Parameter as CommandResult);
            if (cmdResult == null)
                return;

            mCmdResults.Remove(cmdResult);
            if (mCmdResults.Count > 0)
                mCmdResults[mCmdResults.Count - 1].DrawBottomSeparator = false;

            var eHandler = CommandCountEvent;
            eHandler?.Invoke(mCmdResults.Count);
        }
    }
}
