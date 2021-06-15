using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Media;

namespace LogAnalyzerWPF.Controls
{
    /// <summary>
    /// Interaction logic for ContentLineInfo.xaml
    /// </summary>
    public partial class ContentLineInfo : UserControl
    {
        internal delegate void DrawThreadColorEventHandler(int lineIndex, string threadId, Color color);
        internal event DrawThreadColorEventHandler DrawThreadColorEvent;

        public class MainModel : INotifyPropertyChanged
        {
            private string mLineRaw = "";
            public string LineRaw
            {
                get => mLineRaw;
                set
                {
                    if (value.Equals(mLineRaw))
                        return;

                    mLineRaw = value;
                    OnPropertyChanged();
                }
            }

            private LineInfo mLine = null;
            public LineInfo Line
            {
                get => mLine;
                set
                {
                    if (value.Equals(mLine))
                        return;

                    mLine = value;
                    OnPropertyChanged();
                }
            }

            private List<LineParamInfo> mLineParams = new List<LineParamInfo>();
            public List<LineParamInfo> LineParams
            {
                get => mLineParams;
                set
                {
                    if (value.Equals(mLineParams))
                        return;

                    mLineParams = value;
                    OnPropertyChanged();
                }
            }

            public void Clear()
            {
                mLine = null;
                mLineRaw = "";
                mLineParams.Clear();
            }

            public event PropertyChangedEventHandler PropertyChanged;

            protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public sealed class LineInfo
        {
            public string Raw { get; private set; }

            public string Date { get; private set; }
            public Repo.LevelType Level { get; private set; }
            public string Tag { get; private set; }
            public string Thread { get; private set; }
            public string Method { get; private set; }
            public string Message { get; private set; }

            public static LineInfo InitLineInfo(JsonDocument jsonContent)
            {
                var lineInfo = new LineInfo();

                var jsonRoot = jsonContent.RootElement;

                lineInfo.Level = (Repo.LevelType)jsonRoot.GetProperty("level").GetUInt32();
                lineInfo.Tag = jsonRoot.GetProperty("tag").GetString();
                lineInfo.Method = jsonRoot.GetProperty("method").GetString();
                lineInfo.Message = jsonRoot.GetProperty("msg").GetString();

                {
                    long timestamp = jsonRoot.GetProperty("timestamp").GetInt64();
                    lineInfo.Date = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).DateTime.ToString("yyyy-MM-dd HH:mm:ss.fff");
                }

                {
                    string threadId = "", threadName = "";

                    JsonElement jsonThread;
                    if (jsonRoot.TryGetProperty("threadId", out jsonThread))
                        threadId = jsonThread.GetInt32().ToString();
                    if (jsonRoot.TryGetProperty("threadName", out jsonThread))
                        threadName = jsonThread.GetString();

                    if (string.IsNullOrWhiteSpace(threadId))
                        lineInfo.Thread = threadName;
                    else if (string.IsNullOrWhiteSpace(threadName))
                        lineInfo.Thread = threadId;
                    else
                        lineInfo.Thread = string.Format($"{threadId} - {threadName}");
                }

                return lineInfo;
            }
        }

        public sealed class LineParamInfo
        {
            public string Name { get; private set; }
            public string Value { get; private set; }

            public static List<LineParamInfo> InitLineParams(JsonDocument jsonContent)
            {
                var list = new List<LineParamInfo>();

                foreach (var jsonParam in jsonContent.RootElement.GetProperty("params").EnumerateArray())
                {
                    var newParam = new LineParamInfo();
                    newParam.Name = jsonParam.GetProperty("name").GetString();
                    newParam.Value = jsonParam.GetProperty("value").GetString();

                    if (newParam.Value.StartsWith('[') || newParam.Value.StartsWith('{'))
                    {
                        try
                        {
                            var jsonDoc = JsonDocument.Parse(newParam.Value);

                            using (var stream = new MemoryStream())
                            {
                                var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
                                jsonDoc.WriteTo(writer);
                                writer.Flush();

                                newParam.Value = Encoding.UTF8.GetString(stream.ToArray());
                            }
                        }
                        catch(JsonException)
                        { }
                    }

                    list.Add(newParam);
                }

                return list;
            }
        }

        private Repo mRepo;
        private int? mLineIndex = null;
        private MainModel mMainModel = new MainModel();

        public ContentLineInfo(Repo repo)
        {
            InitializeComponent();

            mRepo = repo;
            DataContext = mMainModel;
        }

        public void ClearInfo()
        {
            mLineIndex = null;
            mMainModel.Clear();
        }

        public void ShowLineInfo(int lineIndex)
        {
            mLineIndex = lineIndex;
            mMainModel.LineRaw = mRepo.RetrieveLineContent(lineIndex, Repo.TranslationType.Raw, Repo.TranslationFormat.Line);

            using (var jsonContent = JsonDocument.Parse(mRepo.RetrieveLineContent(lineIndex, Repo.TranslationType.Translated, Repo.TranslationFormat.JsonFull)))
            {
                mMainModel.Line = LineInfo.InitLineInfo(jsonContent);
                mMainModel.LineParams = LineParamInfo.InitLineParams(jsonContent);
            }

            //mDataGridParams.adju

            /*{
                var columns = (mListViewActions.View as GridView).Columns;

                if (double.IsNaN(columns[0].Width))
                    columns[0].Width = 1;
                columns[0].Width = double.NaN;

                mListViewActions.UpdateLayout();

                columns[1].Width = mListViewActions.ActualWidth - columns[0].ActualWidth;
            }*/
        }

        private void onButtonThreadId(object sender, System.Windows.RoutedEventArgs e)
        {
            if ((mLineIndex == null) || (mMainModel.Line == null))
                return;
            if (string.IsNullOrWhiteSpace(mMainModel.Line.Thread))
                return;

            var rand = new Random();
            var targetColor = Color.FromRgb(0, (byte)rand.Next(38, 119), (byte)rand.Next(10, 91));

            //var eHandler = DrawThreadColorEvent;
            //eHandler?.Invoke(mLineIndex.Value, (mMainModel.Line.ThreadId == "") ? mMainModel.Line.ThreadName : mMainModel.Line.ThreadId, targetColor);
        }
    }
}
