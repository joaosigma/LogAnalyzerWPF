using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace LogAnalyzerWPF.Controls
{
    /// <summary>
    /// Interaction logic for ContentLines.xaml
    /// </summary>
    public partial class ContentLines : UserControl
    {
        #region Events
        internal delegate void SelectedLineEventHandler(int? lineIndex);
        internal event SelectedLineEventHandler SelectedLineEvent;

        internal delegate void ExecutedCommandEventHandler(string cmdTag, string cmdName, string cmdParams);
        internal event ExecutedCommandEventHandler ExecutedCommandEvent;

        internal delegate void GotoParentLineEventHandler(int lineIndex, int lineId);
        internal event GotoParentLineEventHandler GotoParentLineEvent; 
        #endregion

        public class MainModel : INotifyPropertyChanged
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

            private IList<LineItemWrapper> mLines = new List<LineItemWrapper>();
            public IList<LineItemWrapper> Lines
            {
                get => mLines;
                set
                {
                    if (value.Equals(mLines))
                        return;

                    mLines = value;
                    OnPropertyChanged();
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;

            protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public class LineItemWrapper : INotifyPropertyChanged
        {
            private IList<LineItem> mLinesSource;

            public int Index { get; }

            public LineItem Line
            {
                get
                {
                    if ((Index < 0) || (Index >= mLinesSource.Count))
                        return null;
                    return mLinesSource[Index];
                }
            }

            public SolidColorBrush mThreadBrush;
            public SolidColorBrush ThreadBrush
            {
                get => mThreadBrush;
                set
                {
                    if ((value == null) && (mThreadBrush == null))
                        return;
                    if ((value != null) && value.Equals(mThreadBrush))
                        return;

                    mThreadBrush = value;
                    OnPropertyChanged();
                }
            }

            public LineItemWrapper(IList<LineItem> linesSource, int lineIndex)
            {
                mLinesSource = linesSource;
                Index = lineIndex;
            }

            public event PropertyChangedEventHandler PropertyChanged;

            protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public class LineItem
        {
            public int Id { get; private set; } = 0;
            public int Index { get; private set; } = 0;
            public Repo.LevelType Level { get; private set; }
            public string Date { get; private set; }
            public string Tag { get; private set; }
            public string ThreadId { get; private set; }
            public string Method { get; private set; }
            public string Message { get; private set; }
            public string XParams { get; private set; }

            public bool ShowMessageParamsSeparator { get; private set; } = true;

            public static LineItem CreateLineItem(int lineIndex, Repo.LevelType level)
            {
                var newLine = new LineItem();

                newLine.Index = lineIndex;
                newLine.Level = level;
                return newLine;
            }

            public static LineItem CreateLineItem(int lineIndex, JsonElement jsonRoot, string dateFormat)
            {
                var newLine = new LineItem();

                newLine.Index = lineIndex;
                newLine.Id = jsonRoot.GetProperty("id").GetInt32();
                newLine.Level = (Repo.LevelType)jsonRoot.GetProperty("level").GetUInt32();
                newLine.Tag = jsonRoot.GetProperty("tag").GetString();
                newLine.Method = jsonRoot.GetProperty("method").GetString();
                newLine.Message = jsonRoot.GetProperty("msg").GetString();
                newLine.XParams = jsonRoot.GetProperty("params").GetString();

                newLine.Date = DateTimeOffset.FromUnixTimeMilliseconds(jsonRoot.GetProperty("timestamp").GetInt64()).DateTime.ToString(dateFormat);

                {
                    JsonElement jsonThread;
                    if (jsonRoot.TryGetProperty("threadId", out jsonThread))
                        newLine.ThreadId = jsonThread.GetInt32().ToString();
                    else if (jsonRoot.TryGetProperty("threadName", out jsonThread))
                        newLine.ThreadId = jsonThread.GetString();
                }

                newLine.ShowMessageParamsSeparator = (newLine.Message.Length > 0) && (newLine.XParams.Length > 0);

                return newLine;
            }

            public override bool Equals(object obj)
            {
                return this.Equals(obj as LineItem);
            }

            public bool Equals(LineItem line)
            {
                if (Object.ReferenceEquals(line, null))
                    return false;

                return (Id == line.Id);
            }

            public override int GetHashCode()
            {
                return Id;
            }

            public static bool operator ==(LineItem lhs, LineItem rhs)
            {
                if (lhs is null)
                {
                    if (rhs is null)
                        return true; //null == null
                    return false;
                }

                return lhs.Equals(rhs);
            }

            public static bool operator !=(LineItem lhs, LineItem rhs)
            {
                return !(lhs == rhs);
            }
        }

        private Repo mRepo;
        private bool mHasParent;
        private bool mCanSelect = false;
        private IList<LineItem> mLines;
        private IList<LineItemWrapper> mLineWrappers;
        private MainModel mMainModel = new MainModel();
        private Dictionary<Color, SolidColorBrush> mThreadPalette = new Dictionary<Color, SolidColorBrush>();

        public bool ShowMethod
        {
            get { return mMainModel.ShowMethod; }
            set { mMainModel.ShowMethod = value; }
        }

        public int? SelectedLineIndex
        {
            get
            {
                var lineWrapper = (mListBox.SelectedItem as LineItemWrapper);
                if (lineWrapper == null)
                    return null;
                return lineWrapper.Line.Index;
            }
        }

        public int? SelectedLineId
        {
            get
            {
                var lineWrapper = (mListBox.SelectedItem as LineItemWrapper);
                if (lineWrapper == null)
                    return null;
                return lineWrapper.Line.Id;
            }
        }

        public ContentLines(Repo repo, bool hasParent)
        {
            InitializeComponent();

            mRepo = repo;
            mHasParent = hasParent;

            if (mRepo.NumLines > 0)
            {
                string dateFormat = (mRepo.Summary.DateStart.Year == mRepo.Summary.DateEnd.Year) ? "MM-dd HH:mm:ss.fff" : "yyyy-MM-dd HH:mm:ss.fff";

                mLines = BFF.DataVirtualizingCollection.DataVirtualizingCollection.DataVirtualizingCollectionBuilder
                    .Build<LineItem>(200, new System.Reactive.Concurrency.EventLoopScheduler())
                    .NonPreloading()
                    .LeastRecentlyUsed(6)
                    .NonTaskBasedFetchers(pageFetcher: (offset, pageSize) =>
                    {
                        var newItems = new LineItem[pageSize];
                        Parallel.For(0, pageSize, index =>
                        {
                            var realIndex = index + offset;

                            var contentJson = mRepo.RetrieveLineContent(realIndex, Repo.TranslationType.Raw, Repo.TranslationFormat.JsonSingleParams);
                            if (contentJson == "")
                            {
                                newItems[index] = LineItem.CreateLineItem(realIndex, Repo.LevelType.Error);
                                return;
                            }

                            var newLine = LineItem.CreateLineItem(realIndex, JsonDocument.Parse(contentJson).RootElement, dateFormat);
                            newItems[index] = newLine;
                        });

                        return newItems;
                    }, countFetcher: () => mRepo.NumLines)
                    .SyncIndexAccess();

                mLineWrappers = new List<LineItemWrapper>(repo.NumLines);
                for (int i = 0; i < repo.NumLines; i++)
                    mLineWrappers.Add(new LineItemWrapper(mLines, i));

                mCanSelect = true;
            }

            mMainModel.Lines = mLineWrappers;
            DataContext = mMainModel;
        }

        public ContentLines(Repo repo, IList<int> lineIndices)
        {
            InitializeComponent();

            mRepo = repo;

            if (lineIndices.Count > 0)
            {
                string dateFormat = (mRepo.Summary.DateStart.Year == mRepo.Summary.DateEnd.Year) ? "MM-dd HH:mm:ss.fff" : "yyyy-MM-dd HH:mm:ss.fff";

                mLines = BFF.DataVirtualizingCollection.DataVirtualizingCollection.DataVirtualizingCollectionBuilder
                    .Build<LineItem>(200, new System.Reactive.Concurrency.EventLoopScheduler())
                    .NonPreloading()
                    .LeastRecentlyUsed(6)
                    .NonTaskBasedFetchers(pageFetcher: (offset, pageSize) =>
                    {
                        var newItems = new LineItem[pageSize];
                        for (int i = 0; i < pageSize; i++)
                        {
                            var realIndex = lineIndices[i + offset];

                            var contentJson = mRepo.RetrieveLineContent(realIndex, Repo.TranslationType.Raw, Repo.TranslationFormat.JsonSingleParams);
                            if (string.IsNullOrWhiteSpace(contentJson))
                            {
                                newItems[i] = LineItem.CreateLineItem(realIndex, Repo.LevelType.Error);
                                continue;
                            }

                            var newLine = LineItem.CreateLineItem(realIndex, JsonDocument.Parse(contentJson).RootElement, dateFormat);
                            newItems[i] = newLine;
                        }

                        return newItems;
                    }, countFetcher: () => lineIndices.Count)
                    .SyncIndexAccess();

                mLineWrappers = new List<LineItemWrapper>(lineIndices.Count);
                for (int i = 0; i < lineIndices.Count; i++)
                    mLineWrappers.Add(new LineItemWrapper(mLines, lineIndices[i]));
            }

            mMainModel.Lines = mLineWrappers;
            DataContext = mMainModel;
        }

        public void SelectLine(int lineIndex)
        {
            if (!mCanSelect)
                return;
            if ((lineIndex < 0) || (lineIndex >= mRepo.NumLines))
                return;

            mListBox.SelectedIndex = lineIndex;
            mListBox.ScrollIntoView(mListBox.SelectedItem);
        }

        public void SelectLineId(int lineId)
        {
            if (!mCanSelect)
                return;

            var lineIndex = mRepo.GetLineIndex(lineId);
            if (lineIndex.HasValue)
                SelectLine(lineIndex.Value);
        }

        public void ClearDrawnThreads()
        {
            foreach (var line in mLineWrappers)
                line.ThreadBrush = null;
        }

        public void DrawThread(string threadId, Color targetColor)
        {
            SolidColorBrush targetBrush;
            if (!mThreadPalette.TryGetValue(targetColor, out targetBrush))
            {
                targetBrush = new SolidColorBrush(targetColor);
                mThreadPalette.Add(targetColor, targetBrush);
            }

            foreach (var line in mLineWrappers)
            {
                if (line.Line.ThreadId == threadId)
                    line.ThreadBrush = new SolidColorBrush(targetColor);
            }
        }
        
        private void mListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var lineItem = (mListBox.SelectedItem as LineItemWrapper);

            var eHandler = SelectedLineEvent;
            eHandler?.Invoke((lineItem == null) ? null : (int?)lineItem.Index);
        }

        private void mListBoxContextMenu_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            mListBoxContextMenu.Items.Clear();

            //go to parent repo
            {
                var item = new MenuItem();
                item.Header = "Goto parent line";
                item.IsEnabled = mHasParent;
                item.Click += (s, e) =>
                {
                    var lineItem = (mListBox.SelectedItem as LineItemWrapper);
                    if (lineItem == null)
                        return;

                    var eHandler = GotoParentLineEvent;
                    eHandler?.Invoke(lineItem.Index, lineItem.Line.Id);
                };
                mListBoxContextMenu.Items.Add(item);
            }

            //mark thread
            {
                var item = new MenuItem();
                item.Header = "Mark thread";
                item.Click += (s, e) =>
                {
                    var lineItem = (mListBox.SelectedItem as LineItemWrapper);
                    if (lineItem == null)
                        return;

                    DrawThread(lineItem.Line.ThreadId, Color.FromRgb(100, 0, 0));
                };
                mListBoxContextMenu.Items.Add(item);
            }

            foreach (var cmdTag in mRepo.CommandTags)
            {
                var itemParent = new MenuItem();
                itemParent.Header = cmdTag.Name;

                foreach (var cmd in cmdTag.Cmds)
                {
                    if (!cmd.SupportLineExecution)
                        continue;

                    var item = new MenuItem();
                    item.Header = cmd.Name;
                    item.Click += (s, e) =>
                    {
                        var lineItem = (mListBox.SelectedItem as LineItemWrapper);
                        if (lineItem == null)
                            return;

                        var eHandler = ExecutedCommandEvent;
                        eHandler?.Invoke(cmdTag.Name, cmd.Name, string.Format(":{0}", lineItem.Line.Index));
                    };
                    itemParent.Items.Add(item);
                }

                if (itemParent.Items.Count > 0)
                    mListBoxContextMenu.Items.Add(itemParent);
            }
        }
    }
}
