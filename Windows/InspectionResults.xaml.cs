using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace LogAnalyzerWPF.Windows
{
    /// <summary>
    /// Interaction logic for InspectionResults.xaml
    /// </summary>
    public partial class InspectionResults : AdonisUI.Controls.AdonisWindow
    {
        #region Events
        internal delegate void GoToLineEventHandler(int lineIndex);
        internal event GoToLineEventHandler GoToLineEvent;

        internal delegate void RepoRangeEventHandler(Repo.LineRange range);
        internal event RepoRangeEventHandler RepoRangeEvent;

        internal delegate void NewRepoTagsEventHandler(Repo newRepo);
        internal event NewRepoTagsEventHandler NewRepoTagsEvent;
        #endregion

        public static RoutedCommand CommandLineAction = new RoutedCommand();

        internal class MainModel
        {
            public class ItemInfo
            {
                public Repo.Inspection.Info Info { get; }
                public string Context { get => Info.Context; }
                public string Message { get => Info.Msg; }

                public bool HasLineIndex { get => Info.LineIndex.HasValue; }
                public bool HasLineRange { get => Info.LineRange.HasValue; }

                public ItemInfo(Repo.Inspection.Info info)
                {
                    Info = info;
                }
            };

            public class ItemWarning
            {
                public Repo.Inspection.Warning Warn { get; }
                public string Context { get => Warn.Context; }
                public string Message { get => Warn.Msg; }

                public bool HasLineIndex { get => Warn.LineIndex.HasValue; }
                public bool HasLineRange { get => Warn.LineRange.HasValue; }

                public ItemWarning(Repo.Inspection.Warning warn)
                {
                    Warn = warn;
                }
            };

            public class ItemExecution
            {
                public Repo.Inspection.Execution Exec { get; }
                public string Context { get; }
                public string Message { get => Exec.Msg; }

                public bool HasLineIndex { get => false; }
                public bool HasLineRange { get => true; }

                public ItemExecution(string context, Repo.Inspection.Execution exec)
                {
                    Context = context;
                    Exec = exec;
                }
            };

            private List<ItemInfo> mInfos;
            public ReadOnlyCollection<ItemInfo> Infos { get => mInfos.AsReadOnly(); }

            private List<ItemWarning> mWarns;
            public ReadOnlyCollection<ItemWarning> Warns { get => mWarns.AsReadOnly(); }

            private List<ItemExecution> mExecs;
            public ReadOnlyCollection<ItemExecution> Execs { get => mExecs.AsReadOnly(); }

            public MainModel(Repo repo, Repo.Inspection inspection)
            {
                mInfos = new List<ItemInfo>(inspection.Infos.Count);
                foreach (var info in inspection.Infos)
                    mInfos.Add(new ItemInfo(info));

                mWarns = new List<ItemWarning>(inspection.Warnings.Count);
                foreach (var warn in inspection.Warnings)
                    mWarns.Add(new ItemWarning(warn));

                mExecs = new List<ItemExecution>(inspection.Executions.Count);
                foreach (var exec in inspection.Executions)
                    mExecs.Add(new ItemExecution(string.Format("[{0}, {1}] - {2} lines", exec.TimestampStart.ToString("yyyy-MM-dd HH:mm:ss.fff"), exec.TimestampFinish.ToString("yyyy-MM-dd HH:mm:ss.fff"), exec.LineRange.Count), exec));
            }
        }

        internal class TreeViewModel : INotifyPropertyChanged
        {
            TreeViewModel(string name, object tag)
            {
                Name = name;
                Tag = tag;
                Children = new List<TreeViewModel>();
            }

            public string Name { get; }
            public object Tag { get; }
            public List<TreeViewModel> Children { get; private set; }
            public bool IsInitiallySelected { get; private set; }

            bool? _isChecked = false;
            TreeViewModel _parent;

            public bool? IsChecked
            {
                get { return _isChecked; }
                set { SetIsChecked(value, true, true); }
            }

            void SetIsChecked(bool? value, bool updateChildren, bool updateParent)
            {
                if (value == _isChecked) return;

                _isChecked = value;

                if (updateChildren && _isChecked.HasValue) Children.ForEach(c => c.SetIsChecked(_isChecked, true, false));

                if (updateParent && _parent != null) _parent.VerifyCheckedState();

                NotifyPropertyChanged("IsChecked");
            }

            void VerifyCheckedState()
            {
                bool? state = null;

                for (int i = 0; i < Children.Count; ++i)
                {
                    bool? current = Children[i].IsChecked;
                    if (i == 0)
                    {
                        state = current;
                    }
                    else if (state != current)
                    {
                        state = null;
                        break;
                    }
                }

                SetIsChecked(state, false, true);
            }

            void Initialize()
            {
                foreach (TreeViewModel child in Children)
                {
                    child._parent = this;
                    child.Initialize();
                }
            }

            public static List<TreeViewModel> SetTree(ReadOnlyCollection<Repo.SummaryInfo.Tag> tags)
            {
                List<TreeViewModel> treeView = new List<TreeViewModel>();
               
                {
                    Action<ReadOnlyCollection<Repo.SummaryInfo.Tag>, List<TreeViewModel>> func = null;
                    func = (ReadOnlyCollection<Repo.SummaryInfo.Tag> tags, List<TreeViewModel> model) =>
                    {
                        foreach (var child in tags)
                        {
                            var childModel = new TreeViewModel(child.Name, child);
                            if (child.HasDescendents)
                                func(child.Descendents, childModel.Children);

                            foreach (var grandChild in childModel.Children)
                                grandChild._parent = childModel;

                            model.Add(childModel);
                        }
                    };

                    func(tags, treeView);
                }

                return treeView;
            }

            public static List<string> GetTree(List<TreeViewModel> tree)
            {
                List<string> targetTags = new List<string>();

                Action<List<TreeViewModel>, string> func = null;
                func = (List<TreeViewModel> tree, string currentName) =>
                {
                    foreach (var child in tree)
                    {
                        var tag = child.Tag as Repo.SummaryInfo.Tag;
                        if (tag == null)
                            continue;

                        if (child.IsChecked.GetValueOrDefault(false))
                        {
                            string newTag;
                            if (string.IsNullOrWhiteSpace(currentName))
                                newTag = $"{tag.Name}";
                            else
                                newTag = $"{currentName}.{tag.Name}";

                            if (child.Children.Count > 0)
                            {
                                newTag += "*";
                                targetTags.Add(newTag);

                                continue; //no point in going further down
                            }

                            targetTags.Add(newTag);
                        }

                        if (string.IsNullOrWhiteSpace(currentName))
                            func(child.Children, $"{tag.Name}");
                        else
                            func(child.Children, $"{currentName}.{tag.Name}");
                    }
                };

                func(tree, "");

                return targetTags;
            }

            void NotifyPropertyChanged(string info)
            {
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(info));
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
        }

        private Repo mRepo;
        private Repo.Inspection mInspection;
        private MainModel mMainModel;

        public InspectionResults(Repo repo, Repo.Inspection inspection)
        {
            InitializeComponent();

            mRepo = repo;
            mInspection = inspection;
            mMainModel = new MainModel(mRepo, mInspection);

            mTagsTree.ItemsSource = TreeViewModel.SetTree(mRepo.Summary.Tags);

            if ((mInspection.Executions.Count <= 0) || ((mInspection.Executions.Count == 1) && (mInspection.Executions[0].LineRange.Start == 0) && (mInspection.Executions[0].LineRange.Count == mRepo.NumLines)))
                mTabControl.Items.Remove(mTabExecutions);

            this.DataContext = mMainModel;
        }

        private void onLineAction(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            var paramInfo = (e.Parameter as MainModel.ItemInfo);
            if (paramInfo != null)
            {
                if (paramInfo.Info.LineIndex.HasValue)
                {
                    var eHandler = GoToLineEvent;
                    eHandler?.Invoke(paramInfo.Info.LineIndex.Value);
                }
                else if (paramInfo.Info.LineRange.HasValue)
                {
                    var eHandler = RepoRangeEvent;
                    eHandler?.Invoke(paramInfo.Info.LineRange.Value);
                }

                return;
            }

            var paramWarn = (e.Parameter as MainModel.ItemWarning);
            if (paramWarn != null)
            {
                if (paramWarn.Warn.LineIndex.HasValue)
                {
                    var eHandler = GoToLineEvent;
                    eHandler?.Invoke(paramWarn.Warn.LineIndex.Value);
                }
                else if (paramWarn.Warn.LineRange.HasValue)
                {
                    var eHandler = RepoRangeEvent;
                    eHandler?.Invoke(paramWarn.Warn.LineRange.Value);
                }

                return;
            }

            var paramExec = (e.Parameter as MainModel.ItemExecution);
            if (paramExec != null)
            {
                var eHandler = RepoRangeEvent;
                eHandler?.Invoke(paramExec.Exec.LineRange);
                return;
            }
        }

        private void mButtonTagsRepo_Click(object sender, RoutedEventArgs e)
        {
            var targetTags = TreeViewModel.GetTree(mTagsTree.ItemsSource.Cast<TreeViewModel>().ToList());
            if (targetTags.Count <= 0)
            {
                AdonisUI.Controls.MessageBox.Show(this, "Please select at least one item in the tag tree.", "No tags selected", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Exclamation);
                return;
            }

            var newRepo = Repo.InitTags(mRepo, targetTags);
            if (newRepo == null)
            {
                AdonisUI.Controls.MessageBox.Show(this, "Unable to create a new repo for the selected tags.", "Error", AdonisUI.Controls.MessageBoxButton.OK, AdonisUI.Controls.MessageBoxImage.Exclamation);
                return;
            }

            var eHandler = NewRepoTagsEvent;
            eHandler?.Invoke(newRepo);
        }
    }
}
