using LogAnalyzerWPF.Windows;
using Microsoft.Win32;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace LogAnalyzerWPF.Controls
{
    /// <summary>
    /// Interaction logic for TabItemRepo.xaml
    /// </summary>
    public partial class TabItemRepo : UserControl
    {
        #region Events
        internal delegate void RepoCreatedEventHandler(Repo repo, string repoDesc);
        internal event RepoCreatedEventHandler RepoCreatedEvent;

        internal delegate void GotoParentLineEventHandler(int lineId);
        internal event GotoParentLineEventHandler GotoParentLineEvent;
        #endregion

        public static readonly RoutedCommand CmdSearchNext = new RoutedCommand();

        public class MainViewOptions : INotifyPropertyChanged
        {
            private bool mShowMethod = false;
            public bool ShowMethod
            {
                get => mShowMethod;
                set
                {
                    if (value.Equals(mShowMethod))
                        return;

                    mShowMethod = value;
                    OnPropertyChanged();
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;

            protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        
        private Repo mRepo;
        private Repo.Search mRepoSearch;

        private bool mHasParent;
        private string mRootFolder;
        private Repo.Inspection mRepoInspection;
        private bool mInspectionShown = false;
        private ContentLines mContentLines;
        private ContentLineInfo mContentLineInfo;
        private CommandResults mCommandResults;
        private MainViewOptions mMainViewOptions;

        public TabItemRepo(Repo repo, bool hasParent, string rootFolder)
        {
            InitializeComponent();

            mRepo = repo;
            mHasParent = hasParent;
            mRootFolder = rootFolder;

            mButtonInspection.IsEnabled = false; //by default
            mBorderCommands.Visibility = Visibility.Collapsed; //we don't have any commands
            mBorderFind.Visibility = Visibility.Collapsed; //we aren't searching anything

            mMainViewOptions = new MainViewOptions();
            this.DataContext = mMainViewOptions;

            mContentLines = new ContentLines(mRepo, hasParent);
            Grid.SetRow(mContentLines, 1);
            mLinesGrid.Children.Add(mContentLines);
            mContentLines.SelectedLineEvent += (lineIndex) =>
            {
                if (lineIndex.HasValue)
                    mContentLineInfo.ShowLineInfo(lineIndex.Value);
                else
                    mContentLineInfo.ClearInfo();
            };
            mContentLines.ExecutedCommandEvent += (cmdTag, cmdName, cmdParams) =>
            {
                var cmdResult = mRepo.ExecuteCommand(cmdTag, cmdName, cmdParams);
                if (string.IsNullOrWhiteSpace(cmdResult))
                    return;

                mCommandResults.AddCommandResult(cmdResult);
            };
            mContentLines.GotoParentLineEvent += (int lineIndex, int lineId) =>
            {
                var eHandler = GotoParentLineEvent;
                eHandler?.Invoke(lineId);
            };

            mContentLineInfo = new ContentLineInfo(mRepo);
            Grid.SetColumn(mContentLineInfo, 3);
            mMainGrid.Children.Add(mContentLineInfo);
            mContentLineInfo.DrawThreadColorEvent += (int lineIndex, string threadId, Color color) =>
            {
                mContentLines.DrawThread(threadId, color);
            };

            mCommandResults = new CommandResults(mRepo);
            mBorderCommands.Child = mCommandResults;
            mCommandResults.CommandCountEvent += (int commandCount) =>
            {
                mBorderCommands.Visibility = (commandCount > 0) ? Visibility.Visible : Visibility.Collapsed;
            };
            mCommandResults.NewRepoTagsEvent += (Repo repo, string repoDesc) =>
            {
                var eHandler = RepoCreatedEvent;
                eHandler?.Invoke(repo, repoDesc);
            };
            mCommandResults.SelectedLineEvent += (int lineIndex) =>
            {
                mContentLines.SelectLine(lineIndex);
            };

            {
                mFindButtonPrev.IsEnabled = mFindButtonProx.IsEnabled = false;

                mFindInput.TextChanged += (s, e) =>
                {
                    mFindButtonPrev.IsEnabled = mFindButtonProx.IsEnabled = !string.IsNullOrWhiteSpace(mFindInput.Text);
                };
                mFindInput.PreviewKeyDown += (s, e) =>
                {
                    if (e.Key != Key.Enter)
                        return;

                    if (CmdSearchNext.CanExecute(null, null))
                        CmdSearchNext.Execute(null, null);
                };
            }

            this.Loaded += (s, e) => onLoad();
        }

        #region Public methods
        public void SelectLine(int lineIndex)
        {
            mContentLines.SelectLine(lineIndex);
            mContentLines.Focus();
        }

        public void SelectLineId(int lineId)
        {
            mContentLines.SelectLineId(lineId);
            mContentLines.Focus();
        }

        public enum CommandType { Escape, ShowCommands, ShowInspector, ShowFind, GotoParent };
        public void ProcessCommand(CommandType commandType)
        {
            switch (commandType)
            {
                case CommandType.Escape:
                    {
                        if (mBorderFind.Visibility != Visibility.Collapsed)
                            mBorderFind.Visibility = Visibility.Collapsed;
                    }
                    break;

                case CommandType.ShowCommands:
                    {
                        var cmdPalette = new CommandPalette(mRepo);
                        cmdPalette.Owner = Window.GetWindow(this);
                        cmdPalette.ShowDialog();

                        if (string.IsNullOrWhiteSpace(cmdPalette.CommandJsonResult))
                            return;

                        mCommandResults.AddCommandResult(cmdPalette.CommandJsonResult);
                    }
                    break;

                case CommandType.ShowInspector:
                    {
                        if (mRepoInspection == null)
                            return;

                        if (mRepoInspection.HasWarnings && !mInspectionShown)
                        {
                            (this.Resources["ActionButtonAnimationNotice"] as Storyboard).Stop();
                            mButtonInspectionPath.Fill = (this.Resources["ActionButtonForegroundBrush"] as SolidColorBrush);
                        }

                        mInspectionShown = true;

                        var inspectionWindow = new InspectionResults(mRepo, mRepoInspection);
                        inspectionWindow.GoToLineEvent += (int lineIndex) => SelectLine(lineIndex);
                        inspectionWindow.RepoRangeEvent += (Repo.LineRange lineRange) =>
                        {
                            var newRepo = Repo.InitLineRange(mRepo, lineRange.Start, lineRange.Count);
                            if (newRepo == null)
                                return;

                            var eHandler = RepoCreatedEvent;
                            eHandler?.Invoke(newRepo, "exec");
                        };
                        inspectionWindow.NewRepoTagsEvent += (Repo newRepo) =>
                        {
                            if (newRepo == null)
                                return;

                            var eHandler = RepoCreatedEvent;
                            eHandler?.Invoke(newRepo, "tags");
                        };

                        inspectionWindow.Owner = Window.GetWindow(this);
                        inspectionWindow.ShowDialog();
                    }
                    break;

                case CommandType.ShowFind:
                    {
                        if (mBorderFind.Visibility != Visibility.Visible)
                            mBorderFind.Visibility = Visibility.Visible;

                        mFindInput.Focus();
                    }
                    break;

                case CommandType.GotoParent:
                    {
                        if (!mHasParent || (mContentLines == null) || !mContentLines.SelectedLineId.HasValue)
                            return;

                        var eHandler = GotoParentLineEvent;
                        eHandler?.Invoke(mContentLines.SelectedLineId.Value);
                    }
                    break;
            }
        }

        public bool CanProcessCommand(CommandType commandType)
        {
            switch (commandType)
            {
                case CommandType.GotoParent:
                    return mHasParent && (mContentLines != null) && mContentLines.SelectedLineId.HasValue;

                default:
                    break;
            }

            return true;
        } 
        #endregion

        private async void onLoad()
        {
            var result = await Task<string>.Run(() =>
            {
                return mRepo.ExecuteInspection();
            });

            if (string.IsNullOrWhiteSpace(result))
                return;

            mRepoInspection = new Repo.Inspection(result);
            if (mRepoInspection.HasWarnings)
                (this.Resources["ActionButtonAnimationNotice"] as Storyboard).Begin(this, true);
            else
                mButtonInspectionPath.Fill = (this.Resources["ActionButtonForegroundBrush"] as SolidColorBrush);

            mButtonInspection.IsEnabled = true;
        }

        private void onButtonCommands(object sender, RoutedEventArgs e)
        {
            ProcessCommand(CommandType.ShowCommands);
        }

        private void onButtonInspection(object sender, RoutedEventArgs e)
        {
            ProcessCommand(CommandType.ShowInspector);
        }

        private void onButtonMethod(object sender, RoutedEventArgs e)
        {
            mContentLines.ShowMethod = !mContentLines.ShowMethod;
        }

        private void onButtonClear(object sender, RoutedEventArgs e)
        {
            mContentLines.ClearDrawnThreads();
        }

        private void onButtonSave(object sender, RoutedEventArgs e)
        {
            SaveFileDialog fileDiag = new SaveFileDialog();
            fileDiag.Title = "Save logs";
            fileDiag.AddExtension = true;
            fileDiag.OverwritePrompt = true;
            fileDiag.Filter = "Log file (*.log)|*.log";
            if (!string.IsNullOrWhiteSpace(mRootFolder) && System.IO.Directory.Exists(mRootFolder))
                fileDiag.InitialDirectory = mRootFolder;

            if (fileDiag.ShowDialog(Window.GetWindow(this)) == false)
                return;

            if (!mRepo.ExportLines(fileDiag.FileName, 0, mRepo.NumLines))
                AdonisUI.Controls.MessageBox.Show(Window.GetWindow(this), "Failed to export lines to specified file.", "Export lines", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Exclamation);
        }

        private void onCmdSearchNext(object sender, ExecutedRoutedEventArgs e)
        {
            var query = mFindInput.Text;
            if (string.IsNullOrWhiteSpace(query))
                return;

            if ((mRepoSearch != null) && mRepoSearch.IsValid && (mRepoSearch.OriginalQuery == query))
                mRepoSearch.Next();
            else
                mRepoSearch = (mFindIsRegex.IsChecked ?? false) ? mRepo.SearchRegex(query, Repo.CaseSensitivity.None, mContentLines.SelectedLineIndex ?? 0, 0) : mRepo.SearchText(query, Repo.CaseSensitivity.None, mContentLines.SelectedLineIndex ?? 0, 0);

            if ((mRepoSearch != null) && mRepoSearch.IsValid)
            {
                (var lineIndex, var lineOffset) = mRepoSearch.LinePosition;
                mContentLines.SelectLine(lineIndex);
            }
        }
    }
}
