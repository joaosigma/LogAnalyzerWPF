using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LogAnalyzerWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : AdonisUI.Controls.AdonisWindow
    {
        public static readonly RoutedCommand CmdEscape = new RoutedCommand();
        public static readonly RoutedCommand CmdCloseTabItem = new RoutedCommand();
        public static readonly RoutedCommand CmdShowPalette = new RoutedCommand();
        public static readonly RoutedCommand CmdShowInspection = new RoutedCommand();
        public static readonly RoutedCommand CmdShowFind = new RoutedCommand();
        public static readonly RoutedCommand CmdGotoParentLine = new RoutedCommand();

        internal class RepoItem : INotifyPropertyChanged
        {
            #region Events
            internal delegate void RepoCreatedEventHandler(Repo newRepo, string repoDesc);
            internal event RepoCreatedEventHandler RepoCreatedEvent;

            internal delegate void GotoParentLineEventHandler(RepoItem parent, int lineId);
            internal event GotoParentLineEventHandler GotoParentLineEvent;
            #endregion

            public RepoItem Parent { get; private set; }

            public bool IsEmpty { get; private set; } = true;
            public string Description { get; private set; }
            public UserControl Content { get; private set; }

            public bool mShowParent;
            public bool ShowParent
            {
                get => mShowParent;
                set
                {
                    if (value.Equals(mShowParent))
                        return;

                    mShowParent = value;
                    OnPropertyChanged();
                }
            }

            public bool mShowChild;
            public bool ShowChild
            {
                get => mShowChild;
                set
                {
                    if (value.Equals(mShowChild))
                        return;

                    mShowChild = value;
                    OnPropertyChanged();
                }
            }

            public RepoItem(RepoItem parent, Repo repo, string description)
            {
                Parent = parent;
                Description = description;
                IsEmpty = false;

                var newTabItemRepo = new Controls.TabItemRepo(repo, parent != null, null);
                newTabItemRepo.RepoCreatedEvent += (repo, repoDesc) =>
                {
                    var eHandler = RepoCreatedEvent;
                    eHandler?.Invoke(repo, repoDesc);
                };
                newTabItemRepo.GotoParentLineEvent += (lineId) =>
                {
                    var eHandler = GotoParentLineEvent;
                    eHandler?.Invoke(parent, lineId);
                };

                Content = newTabItemRepo;
            }

            public RepoItem(string description)
            {
                Description = description;

                var itemPicker = new Controls.TabItemNew();
                itemPicker.RepoCreatedEvent += (repo, repoDesc, rootFolder) =>
                {
                    Description = repoDesc;
                    OnPropertyChanged("Description");

                    var newTabItemRepo = new Controls.TabItemRepo(repo, false, rootFolder);
                    newTabItemRepo.RepoCreatedEvent += (repo, repoDesc) =>
                    {
                        var eHandler = RepoCreatedEvent;
                        eHandler?.Invoke(repo, repoDesc);
                    };

                    Content = newTabItemRepo;
                    OnPropertyChanged("Content");

                    IsEmpty = false;
                };

                Content = itemPicker;
            }

            public void ProcessCommand(Controls.TabItemRepo.CommandType commandType)
            {
                var tabItemRepo = Content as Controls.TabItemRepo;
                if (tabItemRepo != null)
                    tabItemRepo.ProcessCommand(commandType);
            }

            public bool CanProcessCommand(Controls.TabItemRepo.CommandType commandType)
            {
                var tabItemRepo = Content as Controls.TabItemRepo;
                return (tabItemRepo != null) ? tabItemRepo.CanProcessCommand(commandType) : false;
            }

            public void ClearParent()
            {
                Parent = null;
            }

            public event PropertyChangedEventHandler PropertyChanged;

            protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        internal class MainModel : INotifyPropertyChanged
        {
            private ObservableCollection<RepoItem> mRepos = new ObservableCollection<RepoItem>();
            public ObservableCollection<RepoItem> Repos
            {
                get => this.mRepos;
                set
                {
                    if (value.Equals(this.mRepos))
                        return;

                    this.mRepos = value;
                    OnPropertyChanged();
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;

            protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private bool mDarkMode = true;
        private MainModel mMainModel = new MainModel();

        public MainWindow()
        {
            InitializeComponent();

            createRepoItem();

            mMainWindow.DataContext = mMainModel;
            mTabControlMain.SelectedIndex = 0;
        }

        private void createRepoItem()
        {
            var newRepoItem = new RepoItem("New repository");
            newRepoItem.RepoCreatedEvent += (repo, repoDesc) =>
            {
                createRepoItem(newRepoItem, repo, repoDesc);
                mTabControlMain.SelectedIndex = mMainModel.Repos.Count - 1;
            };

            mMainModel.Repos.Add(newRepoItem);
        }

        private void createRepoItem(RepoItem parent, Repo repo, string repoDesc)
        {
            var newRepoItem = new RepoItem(parent, repo, repoDesc);
            newRepoItem.RepoCreatedEvent += (repo, repoDesc) =>
            {
                createRepoItem(newRepoItem, repo, repoDesc);
                mTabControlMain.SelectedIndex = mMainModel.Repos.Count - 1;
            };
            newRepoItem.GotoParentLineEvent += (repoItem, lineId) =>
            {
                if (!mMainModel.Repos.Contains(repoItem))
                    return;

                var tabItem = (repoItem.Content as Controls.TabItemRepo);
                if (tabItem != null)
                {
                    tabItem.SelectLineId(lineId);
                    mTabControlMain.SelectedIndex = mMainModel.Repos.IndexOf(repoItem);
                }
            };

            mMainModel.Repos.Add(newRepoItem);
        }

        private void onLightbulb(object sender, RoutedEventArgs e)
        {
            mDarkMode = !mDarkMode;
            if (mDarkMode)
                AdonisUI.ResourceLocator.SetColorScheme(Application.Current.Resources, AdonisUI.ResourceLocator.DarkColorScheme);
            else
                AdonisUI.ResourceLocator.SetColorScheme(Application.Current.Resources, AdonisUI.ResourceLocator.LightColorScheme);
        }

        private void onHelp(object sender, RoutedEventArgs e)
        {
            var windowHelp = new Windows.WindowHelp();
            windowHelp.Owner = Window.GetWindow(this);
            windowHelp.ShowDialog();
        }

        private void onAddTabItem(object sender, RoutedEventArgs e)
        {
            if (mMainModel.Repos.Count >= 1)
            {
                var lastItem = mMainModel.Repos.Last<RepoItem>();
                if (lastItem.IsEmpty)
                {
                    mTabControlMain.SelectedIndex = mMainModel.Repos.IndexOf(lastItem);
                    return;
                }
            }

            createRepoItem();
            mTabControlMain.SelectedIndex = mMainModel.Repos.Count - 1;
        }

        #region Commands
        private void onCmdEscape(object sender, ExecutedRoutedEventArgs e)
        {
            var repoItem = (mTabControlMain.SelectedItem as RepoItem);
            if (repoItem != null)
                repoItem.ProcessCommand(Controls.TabItemRepo.CommandType.Escape);
        }
        
        private void onCmdCloseTabItem(object sender, ExecutedRoutedEventArgs e)
        {
            var repoItem = (e.Parameter as RepoItem);
            if (repoItem == null)
            {
                repoItem = (mTabControlMain.SelectedItem as RepoItem); //assume it's the currently selected one
                if (repoItem == null)
                    return;
            }

            foreach (var curRepoItem in mMainModel.Repos)
            {
                if (curRepoItem.Parent == repoItem)
                    curRepoItem.ClearParent();
            }

            if (repoItem.IsEmpty)
            {
                if (mMainModel.Repos.Count <= 1)
                    return;

                mMainModel.Repos.Remove(repoItem);
            }
            else
            {
                mMainModel.Repos.Remove(repoItem);
                if (mMainModel.Repos.Count <= 0)
                {
                    createRepoItem();
                    mTabControlMain.SelectedIndex = 0;
                }
            }
        }

        private void onCmdActionShowPalette(object sender, ExecutedRoutedEventArgs e)
        {
            var repoItem = (mTabControlMain.SelectedItem as RepoItem);
            if (repoItem != null)
                repoItem.ProcessCommand(Controls.TabItemRepo.CommandType.ShowCommands);
        }

        private void onCmdActionShowInspection(object sender, ExecutedRoutedEventArgs e)
        {
            var repoItem = (mTabControlMain.SelectedItem as RepoItem);
            if (repoItem != null)
                repoItem.ProcessCommand(Controls.TabItemRepo.CommandType.ShowInspector);
        }

        private void onCmdActionShowFind(object sender, ExecutedRoutedEventArgs e)
        {
            var repoItem = (mTabControlMain.SelectedItem as RepoItem);
            if (repoItem != null)
                repoItem.ProcessCommand(Controls.TabItemRepo.CommandType.ShowFind);
        }

        private void onCmdActionGotoParentLine(object sender, ExecutedRoutedEventArgs e)
        {
            var repoItem = (mTabControlMain.SelectedItem as RepoItem);
            if (repoItem != null)
                repoItem.ProcessCommand(Controls.TabItemRepo.CommandType.GotoParent);
        }

        private void onCmdCanExecuteActionGotoParentLine(object sender, CanExecuteRoutedEventArgs e)
        {
            var repoItem = (mTabControlMain.SelectedItem as RepoItem);
            e.CanExecute = (repoItem == null) ? false : repoItem.CanProcessCommand(Controls.TabItemRepo.CommandType.GotoParent);
        }
        #endregion

        private void mTabControlMain_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var repoItem = (mTabControlMain.SelectedItem as RepoItem);
            if (repoItem == null)
                return;

            foreach (var curRepoItem in mMainModel.Repos)
                curRepoItem.ShowParent = (repoItem.Parent == curRepoItem);
            foreach (var curRepoItem in mMainModel.Repos)
                curRepoItem.ShowChild = (repoItem == curRepoItem.Parent);
        }
    }
}
