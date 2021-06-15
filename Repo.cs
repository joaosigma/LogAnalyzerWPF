using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace LogAnalyzerWPF
{
    public sealed class Repo : IDisposable
    {
        private static class Unmanaged
        {
            private class WrapperCString : IDisposable
            {
                private bool disposedValue;

                protected virtual void Dispose(bool disposing)
                {
                    if (disposedValue)
                        return;
                    disposedValue = true;

                    Marshal.FreeHGlobal(CString);
                    CString = IntPtr.Zero;
                }

                ~WrapperCString()
                {
                    Dispose(disposing: false);
                }

                public void Dispose()
                {
                    Dispose(disposing: true);
                    GC.SuppressFinalize(this);
                }

                public IntPtr CString { get; private set; } = IntPtr.Zero;

                public WrapperCString(string value)
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(value);

                    CString = Marshal.AllocHGlobal(bytes.Length + 1);
                    Marshal.Copy(bytes, 0, CString, bytes.Length);
                    Marshal.WriteByte(CString, bytes.Length, 0);
                }
            }

            private class WrapperUTF8String : IDisposable
            {
                private bool disposedValue;

                protected virtual void Dispose(bool disposing)
                {
                    if (disposedValue)
                        return;
                    disposedValue = true;

                    la_str_destroy(ref mNativeStr);
                }

                ~WrapperUTF8String()
                {
                    Dispose(disposing: false);
                }

                public void Dispose()
                {
                    Dispose(disposing: true);
                    GC.SuppressFinalize(this);
                }

                private laStrUTF8 mNativeStr;

                public WrapperUTF8String(laStrUTF8 value)
                {
                    mNativeStr = value;
                }

                public string AsString()
                {
                    if ((mNativeStr.data == IntPtr.Zero) || (mNativeStr.size <= 0))
                        return "";

                    return Marshal.PtrToStringUTF8(mNativeStr.data, mNativeStr.size);
                }
            }

            public enum laFlavorType : int
            {
                Unknown = 0,
                WCSComlib = 1,
                WCSServer = 2,
                WCSAndroidLogcat = 3
            }

            public enum laTranslatorType : int
            {
                Raw = 0,
                Translated = 1
            }

            public enum laTranslatorFormat : int
            {
                Line = 0,
                JsonFull = 1,
                JsonSingleParams = 2
            }

            [StructLayout(LayoutKind.Sequential, Pack = 0)]
            public struct laStrUTF8
            {
                public IntPtr data;
                public int size;
            }

            [StructLayout(LayoutKind.Sequential, Pack = 0)]
            public struct laStrFixedUTF8
            {
                public IntPtr data;
                public int size;
            }

            public enum laFindOptionsCaseSensitivity : int
            {
                None = 0,
                CaseSensitive = 1
            }

            [StructLayout(LayoutKind.Sequential, Pack = 0)]
            public struct laFindOptions
            {
                public laFindOptionsCaseSensitivity caseSensitivity;
                public int startLine;
                public int startLineOffset;
            }

            [StructLayout(LayoutKind.Sequential, Pack = 0)]
            public struct laExportOptions
            {
                [MarshalAs(UnmanagedType.U1)]
                public bool appendToFile;
                public laStrFixedUTF8 filePath;
                public laTranslatorType translationType;
                public laTranslatorFormat translationFormat;
            }

            private class laStrFixedUTF8List : IDisposable
            {
                private bool disposedValue;

                protected virtual void Dispose(bool disposing)
                {
                    if (disposedValue)
                        return;
                    disposedValue = true;

                    Marshal.FreeHGlobal(StringList);
                    StringList = IntPtr.Zero;
                }

                ~laStrFixedUTF8List()
                {
                    Dispose(disposing: false);
                }

                public void Dispose()
                {
                    Dispose(disposing: true);
                    GC.SuppressFinalize(this);
                }

                private List<WrapperCString> mNativeStrings;

                public int Size { get; private set; } = 0;
                public IntPtr StringList { get; private set; } = IntPtr.Zero;

                public laStrFixedUTF8List(IList<string> tags)
                {
                    if (tags.Count <= 0)
                        return;

                    mNativeStrings = new List<WrapperCString>(tags.Count);
                    foreach (var tag in tags)
                        mNativeStrings.Add(new WrapperCString(tag));

                    Size = mNativeStrings.Count;
                    StringList = Marshal.AllocHGlobal(Marshal.SizeOf<laStrFixedUTF8>() * tags.Count);
                    for (int i = 0; i < mNativeStrings.Count; i++)
                    {
                        var curStr = StringList + Marshal.SizeOf<laStrFixedUTF8>() * i;
                        var nativeStr = la_str_fixed_init_cstr(mNativeStrings[i].CString);
                        Marshal.WriteIntPtr(curStr, (int)Marshal.OffsetOf<laStrFixedUTF8>("data"), nativeStr.data);
                        Marshal.WriteInt32(curStr, (int)Marshal.OffsetOf<laStrFixedUTF8>("size"), nativeStr.size);
                    }
                }
            }

            [DllImport("loganalyzer.x64.dll")]
            private static extern laStrUTF8 la_str_init();
            [DllImport("loganalyzer.x64.dll")]
            private static extern laStrUTF8 la_str_init_copy(IntPtr cstr);
            [DllImport("loganalyzer.x64.dll")]
            private static extern laStrUTF8 la_str_init_length(int size);
            [DllImport("loganalyzer.x64.dll")]
            private static extern void la_str_destroy(ref laStrUTF8 str);

            [DllImport("loganalyzer.x64.dll")]
            private static extern laStrFixedUTF8 la_str_fixed_init();
            [DllImport("loganalyzer.x64.dll")]
            private static extern laStrFixedUTF8 la_str_fixed_init_str(laStrUTF8 str);
            [DllImport("loganalyzer.x64.dll")]
            private static extern laStrFixedUTF8 la_str_fixed_init_cstr(IntPtr cstr);

            [DllImport("loganalyzer.x64.dll")]
            private static extern int la_find_ctx_valid(IntPtr findCtx);
            [DllImport("loganalyzer.x64.dll")]
            private static extern laStrUTF8 la_find_ctx_query(IntPtr findCtx);
            [DllImport("loganalyzer.x64.dll")]
            private static extern int la_find_ctx_line_position(IntPtr findCtx, ref int lineOffset);

            [DllImport("loganalyzer.x64.dll")]
            private static extern IntPtr la_init_repo_file(laFlavorType flavor, laStrFixedUTF8 filePath);
            [DllImport("loganalyzer.x64.dll")]
            private static extern IntPtr la_init_repo_folder(laFlavorType flavor, laStrFixedUTF8 folderPath);
            [DllImport("loganalyzer.x64.dll")]
            private static extern IntPtr la_init_repo_folder_filter(laFlavorType flavor, laStrFixedUTF8 folderPath, laStrFixedUTF8 fileNameFilterRegex);

            [DllImport("loganalyzer.x64.dll")]
            private static extern IntPtr la_init_repo_command(IntPtr repo, laStrFixedUTF8 commandResult);
            [DllImport("loganalyzer.x64.dll")]
            private static extern IntPtr la_init_repo_line_range(IntPtr repo, int indexStart, int count);
            [DllImport("loganalyzer.x64.dll")]
            private static extern IntPtr la_init_repo_tags(IntPtr repo, IntPtr tags, int tagsSize);

            [DllImport("loganalyzer.x64.dll")]
            private static extern void la_repo_destroy(IntPtr repo);

            [DllImport("loganalyzer.x64.dll")]
            private static extern int la_repo_num_files(IntPtr repo);
            [DllImport("loganalyzer.x64.dll")]
            private static extern int la_repo_num_lines(IntPtr repo);
            [DllImport("loganalyzer.x64.dll")]
            private static extern laFlavorType la_repo_flavor(IntPtr repo);

            [DllImport("loganalyzer.x64.dll")]
            private static extern IntPtr la_repo_search_text(IntPtr repo, laStrFixedUTF8 query, ref laFindOptions findOptions);
            [DllImport("loganalyzer.x64.dll")]
            private static extern IntPtr la_repo_search_text_regex(IntPtr repo, laStrFixedUTF8 query, ref laFindOptions findOptions);
            [DllImport("loganalyzer.x64.dll")]
            private static extern void la_repo_search_next(IntPtr repo, IntPtr findCtx);
            [DllImport("loganalyzer.x64.dll")]
            private static extern void la_repo_search_destroy(IntPtr findCtx);

            [DllImport("loganalyzer.x64.dll")]
            private static extern laStrUTF8 la_repo_find_all(IntPtr repo, laStrFixedUTF8 query, laFindOptionsCaseSensitivity caseSensitivity);
            [DllImport("loganalyzer.x64.dll")]
            private static extern laStrUTF8 la_repo_find_all_regex(IntPtr repo, laStrFixedUTF8 query, laFindOptionsCaseSensitivity caseSensitivity);

            [DllImport("loganalyzer.x64.dll")]
            private static extern laStrUTF8 la_repo_retrieve_line_content(IntPtr repo, int lineIndex, laTranslatorType translatorType, laTranslatorFormat translatorFormat);

            [DllImport("loganalyzer.x64.dll")]
            private static extern int la_repo_get_lineIndex(IntPtr repo, int lineId, ref int lineIndex);
            [DllImport("loganalyzer.x64.dll")]
            private static extern laStrUTF8 la_repo_get_summary(IntPtr repo);
            [DllImport("loganalyzer.x64.dll")]
            private static extern laStrUTF8 la_repo_get_available_commands(IntPtr repo);

            [DllImport("loganalyzer.x64.dll")]
            private static extern laStrUTF8 la_repo_execute_inspection(IntPtr repo);
            [DllImport("loganalyzer.x64.dll")]
            private static extern laStrUTF8 la_repo_execute_command(IntPtr repo, laStrFixedUTF8 tag, laStrFixedUTF8 cmdName);
            [DllImport("loganalyzer.x64.dll")]
            private static extern laStrUTF8 la_repo_execute_command_params(IntPtr repo, laStrFixedUTF8 tag, laStrFixedUTF8 cmdName, laStrFixedUTF8 cmdParams);

            [DllImport("loganalyzer.x64.dll")]
            private static extern int la_repo_export_lines(IntPtr repo, ref laExportOptions options, int indexStart, int count);
            [DllImport("loganalyzer.x64.dll")]
            private static extern int la_repo_export_command_lines(IntPtr repo, ref laExportOptions options, laStrFixedUTF8 commandResult);
            [DllImport("loganalyzer.x64.dll")]
            private static extern int la_repo_export_command_network_packets(IntPtr repo, ref laExportOptions options, laStrFixedUTF8 commandResult);

            #region Repo
            public static Repo RepoInitFile(laFlavorType flavor, string filePath)
            {
                using (var cFilePath = new WrapperCString(filePath))
                {
                    var repoPtr = la_init_repo_file(flavor, la_str_fixed_init_cstr(cFilePath.CString));
                    return ((repoPtr == IntPtr.Zero) ? null : new Repo(repoPtr));
                }
            }

            public static Repo RepoInitFolder(laFlavorType flavor, string folderPath)
            {
                using (var cFolderPath = new WrapperCString(folderPath))
                {
                    var repoPtr = la_init_repo_folder(flavor, la_str_fixed_init_cstr(cFolderPath.CString));
                    return ((repoPtr == IntPtr.Zero) ? null : new Repo(repoPtr));
                }
            }

            public static Repo RepoInitCommand(Repo repo, string commandResult)
            {
                using (var cCommandResult = new WrapperCString(commandResult))
                {
                    var repoPtr = la_init_repo_command(repo.mNativeRepo, la_str_fixed_init_cstr(cCommandResult.CString));
                    return ((repoPtr == IntPtr.Zero) ? null : new Repo(repoPtr));
                }
            }

            public static Repo RepoInitLineRange(Repo repo, int indexStart, int count)
            {
                var repoPtr = la_init_repo_line_range(repo.mNativeRepo, indexStart, count);
                return ((repoPtr == IntPtr.Zero) ? null : new Repo(repoPtr));
            }

            public static Repo RepoInitTags(Repo repo, IList<string> tags)
            {
                if (tags.Count <= 0)
                    return null;

                using (var cTagsList = new laStrFixedUTF8List(tags))
                {
                    var repoPtr = la_init_repo_tags(repo.mNativeRepo, cTagsList.StringList, cTagsList.Size);
                    return ((repoPtr == IntPtr.Zero) ? null : new Repo(repoPtr));
                }
            }

            public static void RepoDestroy(Repo repo)
            {
                la_repo_destroy(repo.mNativeRepo);
            }

            public static int RepoNumFiles(Repo repo)
            {
                return la_repo_num_files(repo.mNativeRepo);
            }

            public static int RepoNumLines(Repo repo)
            {
                return la_repo_num_lines(repo.mNativeRepo);
            }

            public static laFlavorType RepoFlavor(Repo repo)
            {
                return la_repo_flavor(repo.mNativeRepo);
            }

            public static string RepoFindAll(Repo repo, string query, laFindOptionsCaseSensitivity caseSensitivity)
            {
                using (var cQuery = new WrapperCString(query))
                using (var res = new WrapperUTF8String(la_repo_find_all(repo.mNativeRepo, la_str_fixed_init_cstr(cQuery.CString), caseSensitivity)))
                {
                    return res.AsString();
                }
            }

            public static string RepoFindAllRegex(Repo repo, string query, laFindOptionsCaseSensitivity caseSensitivity)
            {
                using (var cQuery = new WrapperCString(query))
                using (var res = new WrapperUTF8String(la_repo_find_all_regex(repo.mNativeRepo, la_str_fixed_init_cstr(cQuery.CString), caseSensitivity)))
                {
                    return res.AsString();
                }
            }

            public static string RepoRetrieveLineContent(Repo repo, int lineIndex, laTranslatorType translatorType, laTranslatorFormat translatorFormat)
            {
                using (var res = new WrapperUTF8String(la_repo_retrieve_line_content(repo.mNativeRepo, lineIndex, translatorType, translatorFormat)))
                {
                    return res.AsString();
                }
            }

            public static int? RepoGetLineIndex(Repo repo, int lineId)
            {
                int lineIndex = 0;
                if (la_repo_get_lineIndex(repo.mNativeRepo, lineId, ref lineIndex) == 0)
                    return null;
                return lineIndex;
            }

            public static string RepoGetSummary(Repo repo)
            {
                using (var res = new WrapperUTF8String(la_repo_get_summary(repo.mNativeRepo)))
                {
                    return res.AsString();
                }
            }

            public static string RepoGetAvailableCommands(Repo repo)
            {
                using (var res = new WrapperUTF8String(la_repo_get_available_commands(repo.mNativeRepo)))
                {
                    return res.AsString();
                }
            }

            public static string RepoExecuteInspection(Repo repo)
            {
                using (var res = new WrapperUTF8String(la_repo_execute_inspection(repo.mNativeRepo)))
                {
                    return res.AsString();
                }
            }


            public static string RepoExecuteCommand(Repo repo, string cmdTag, string cmdName)
            {
                using (var cCmdTag = new WrapperCString(cmdTag))
                using (var cCmdName = new WrapperCString(cmdName))
                using (var res = new WrapperUTF8String(la_repo_execute_command(repo.mNativeRepo, la_str_fixed_init_cstr(cCmdTag.CString), la_str_fixed_init_cstr(cCmdName.CString))))
                {
                    return res.AsString();
                }
            }

            public static string RepoExecuteCommand(Repo repo, string cmdTag, string cmdName, string cmdParams)
            {
                using (var cCmdTag = new WrapperCString(cmdTag))
                using (var cCmdName = new WrapperCString(cmdName))
                using (var cCmdParams = new WrapperCString(cmdParams))
                using (var res = new WrapperUTF8String(la_repo_execute_command_params(repo.mNativeRepo, la_str_fixed_init_cstr(cCmdTag.CString), la_str_fixed_init_cstr(cCmdName.CString), la_str_fixed_init_cstr(cCmdParams.CString))))
                {
                    return res.AsString();
                }
            }

            public static bool RepoExportLines(Repo repo, string filePath, int indexStart, int count)
            {
                using (var cFilePath = new WrapperCString(filePath))
                {
                    Unmanaged.laExportOptions options;
                    options.appendToFile = false;
                    options.filePath = la_str_fixed_init_cstr(cFilePath.CString);
                    options.translationType = laTranslatorType.Translated;
                    options.translationFormat = laTranslatorFormat.Line;

                    return (la_repo_export_lines(repo.mNativeRepo, ref options, indexStart, count) != 0);
                }
            }

            public static bool RepoExportCommandLines(Repo repo, string filePath, string commandResult)
            {
                using (var cFilePath = new WrapperCString(filePath))
                using (var cCommandResult = new WrapperCString(commandResult))
                {
                    Unmanaged.laExportOptions options;
                    options.appendToFile = false;
                    options.filePath = la_str_fixed_init_cstr(cFilePath.CString);
                    options.translationType = laTranslatorType.Raw;
                    options.translationFormat = laTranslatorFormat.Line;

                    return (la_repo_export_command_lines(repo.mNativeRepo, ref options, la_str_fixed_init_cstr(cCommandResult.CString)) != 0);
                }
            }

            public static bool RepoExportCommandNetworkPackets(Repo repo, string filePath, string commandResult)
            {
                using (var cFilePath = new WrapperCString(filePath))
                using (var cCommandResult = new WrapperCString(commandResult))
                {
                    Unmanaged.laExportOptions options;
                    options.appendToFile = false;
                    options.filePath = la_str_fixed_init_cstr(cFilePath.CString);
                    options.translationType = laTranslatorType.Raw;
                    options.translationFormat = laTranslatorFormat.Line;

                    return (la_repo_export_command_network_packets(repo.mNativeRepo, ref options, la_str_fixed_init_cstr(cCommandResult.CString)) != 0);
                }
            }
            #endregion

            #region Repo searches
            public static bool RepoSearchValid(IntPtr searchCtx)
            {
                return la_find_ctx_valid(searchCtx) != 0;
            }

            public static string RepoSearchQuery(IntPtr searchCtx)
            {
                using (var res = new WrapperUTF8String(la_find_ctx_query(searchCtx)))
                {
                    return res.AsString();
                }
            }

            public static (int index, int offset) RepoSearchLinePosition(IntPtr searchCtx)
            {
                int lineOffset = 0;
                var lineIndex = la_find_ctx_line_position(searchCtx, ref lineOffset);

                return (lineIndex, lineOffset);
            }

            public static IntPtr RepoSearchStart(Repo repo, string query, laFindOptionsCaseSensitivity caseSensitivity, int startLine, int startLineOffset)
            {
                using (var cQuery = new WrapperCString(query))
                {
                    Unmanaged.laFindOptions options;
                    options.caseSensitivity = caseSensitivity;
                    options.startLine = startLine;
                    options.startLineOffset = startLineOffset;

                    return la_repo_search_text(repo.mNativeRepo, la_str_fixed_init_cstr(cQuery.CString), ref options);
                }
            }

            public static IntPtr RepoSearchStartRegex(Repo repo, string query, laFindOptionsCaseSensitivity caseSensitivity, int startLine, int startLineOffset)
            {
                using (var cQuery = new WrapperCString(query))
                {
                    Unmanaged.laFindOptions options;
                    options.caseSensitivity = caseSensitivity;
                    options.startLine = startLine;
                    options.startLineOffset = startLineOffset;

                    return la_repo_search_text_regex(repo.mNativeRepo, la_str_fixed_init_cstr(cQuery.CString), ref options);
                }
            }

            public static void RepoSearchNext(Repo repo, IntPtr searchCtx)
            {
                la_repo_search_next(repo.mNativeRepo, searchCtx);
            }

            public static void RepoSearchDestroy(IntPtr searchCtx)
            {
                la_repo_search_destroy(searchCtx);
            }
            #endregion
        }

        public enum FlavorType { Unknown, WCSComlib, WCSServer, WCSAndroidLogcat }
        public enum TranslationType { Raw, Translated }
        public enum TranslationFormat { Line, JsonFull, JsonSingleParams }
        public enum CaseSensitivity { None, CaseSensitive }
        public enum LevelType { Trace = 0, Debug = 1, Info = 2, Warn = 3, Error = 4, Fatal = 5 }

        public sealed class Search : IDisposable
        {
            private Repo mRepo;
            private IntPtr mNativeSearch;
            private bool disposed = false;

            public bool IsValid { get => Unmanaged.RepoSearchValid(mNativeSearch); }
            public string OriginalQuery { get => Unmanaged.RepoSearchQuery(mNativeSearch); }
            public (int lineIndex, int lineOffset) LinePosition { get => Unmanaged.RepoSearchLinePosition(mNativeSearch); }

            public Search(Repo repo, IntPtr nativeSearch)
            {
                mRepo = repo;
                mNativeSearch = nativeSearch;
            }

            public void Next()
            {
                Unmanaged.RepoSearchNext(mRepo, mNativeSearch);
            }

            ~Search()
            {
                Dispose(disposing: false);
            }

            private void Dispose(bool disposing)
            {
                if (disposed)
                    return;
                disposed = true;

                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                Unmanaged.RepoSearchDestroy(mNativeSearch);
            }

            public void Dispose()
            {
                Dispose(disposing: true);
                GC.SuppressFinalize(this);
            }
        }

        public readonly struct LineRange
        {
            public int Start { get; }
            public int End { get; }

            public int Count { get => End - Start; }
            public bool IsEmpty { get => Count <= 0; }

            public LineRange(int start, int end)
            {
                Start = start;
                End = end;
            }

            public override string ToString() => $"({Start}, {End})";
        }

        public sealed class SummaryInfo
        {
            public class Tag
            {
                private List<Tag> mDescendents = new List<Tag>();

                public string Name { get; }
                public int Count { get; }

                public bool HasDescendents { get => (mDescendents.Count > 0); }
                public ReadOnlyCollection<Tag> Descendents { get => mDescendents.AsReadOnly(); }

                public Tag(JsonElement jsonTag)
                {
                    Name = jsonTag.GetProperty("name").GetString();
                    Count = jsonTag.GetProperty("count").GetInt32();

                    JsonElement jsonDescendents;
                    if (!jsonTag.TryGetProperty("descendents", out jsonDescendents))
                        return;

                    foreach (var jsonDescendent in jsonDescendents.EnumerateArray())
                        mDescendents.Add(new Tag(jsonDescendent));
                }
            }

            private List<int> mThreadIds = new List<int>();
            private List<Tag> mTags = new List<Tag>();
            private List<string> mThreadNames = new List<string>();
            private List<int> mWarningLineIndices = new List<int>();
            private List<int> mErrorLineIndices = new List<int>();

            public DateTime DateStart { get; }
            public DateTime DateEnd { get; }

            public ReadOnlyCollection<int> ThreadIds
            {
                get { return mThreadIds.AsReadOnly(); }
            }

            public ReadOnlyCollection<Tag> Tags
            {
                get { return mTags.AsReadOnly(); }
            }

            public ReadOnlyCollection<string> ThreadNames
            {
                get { return mThreadNames.AsReadOnly(); }
            }

            public ReadOnlyCollection<int> WarningLineIndices
            {
                get { return mWarningLineIndices.AsReadOnly(); }
            }

            public ReadOnlyCollection<int> ErrorLineIndices
            {
                get { return mErrorLineIndices.AsReadOnly(); }
            }

            public SummaryInfo(string jsonContent)
            {
                using (var json = JsonDocument.Parse(jsonContent))
                {
                    var jRoot = json.RootElement;

                    foreach (var jsonId in jRoot.GetProperty("threadIds").EnumerateArray())
                        mThreadIds.Add(jsonId.GetInt32());
                    foreach (var jsonName in jRoot.GetProperty("threadNames").EnumerateArray())
                        mThreadNames.Add(jsonName.GetString());

                    {
                        var jTimestamps = jRoot.GetProperty("timeRange");
                        if (jTimestamps.GetArrayLength() == 2)
                        {
                            DateStart = DateTimeOffset.FromUnixTimeMilliseconds(jTimestamps[0].GetInt64()).DateTime;
                            DateEnd = DateTimeOffset.FromUnixTimeMilliseconds(jTimestamps[1].GetInt64()).DateTime;
                        }
                    }

                    foreach (var jsonTag in jRoot.GetProperty("tags").EnumerateArray())
                        mTags.Add(new Tag(jsonTag));

                    foreach (var jsonIndex in jRoot.GetProperty("warningsLinesIndex").EnumerateArray())
                        mWarningLineIndices.Add(jsonIndex.GetInt32());
                    foreach (var jsonIndex in jRoot.GetProperty("errorsLinesIndex").EnumerateArray())
                        mErrorLineIndices.Add(jsonIndex.GetInt32());
                }
            }
        }

        public sealed class Command
        {
            public string Name { get; }
            public string Desc { get; }
            public string ParamsDesc { get; }
            public bool SupportLineExecution { get; }

            public Command(string name, string desc, string paramsDesc, bool supportLineExecution)
            {
                Name = name;
                Desc = desc;
                ParamsDesc = paramsDesc;
                SupportLineExecution = supportLineExecution;
            }
        }

        public sealed class CommandTag
        {
            public string Name { get; }

            private List<Command> mCmds = new List<Command>();

            public ReadOnlyCollection<Command> Cmds
            {
                get { return mCmds.AsReadOnly(); }
            }

            private CommandTag(string name)
            {
                Name = name;
            }

            public static List<CommandTag> ParseCommandTags(string jsonContent)
            {
                List<CommandTag> cmds = new List<CommandTag>();

                using (var json = JsonDocument.Parse(jsonContent))
                {
                    var jRoot = json.RootElement;
                    foreach (var jsonTag in jRoot.EnumerateArray())
                    {
                        var newCmdTag = new CommandTag(jsonTag.GetProperty("name").GetString());

                        foreach (var jsonCmd in jsonTag.GetProperty("cmds").EnumerateArray())
                        {
                            JsonElement jsonParamsHelp;
                            var hasParamsHelp = jsonCmd.TryGetProperty("paramsHelp", out jsonParamsHelp);
                            JsonElement jsonSupportLineExecution;
                            var hasSupportLineExecution = jsonCmd.TryGetProperty("supportLineExecution", out jsonSupportLineExecution);

                            newCmdTag.mCmds.Add(new Command(
                                jsonCmd.GetProperty("name").GetString(),
                                jsonCmd.GetProperty("help").GetString(),
                                hasParamsHelp ? jsonParamsHelp.GetString() : "",
                                hasSupportLineExecution ? jsonSupportLineExecution.GetBoolean() : false));
                        }

                        cmds.Add(newCmdTag);
                    }
                }

                return cmds;
            }
        }

        public sealed class Inspection
        {
            public sealed class Info
            {
                public string Context { get; }
                public string Msg { get; }
                public int? LineIndex { get; }
                public LineRange? LineRange { get; }

                public Info(string ctx, string msg)
                {
                    Context = ctx;
                    Msg = msg;
                    LineIndex = null;
                    LineRange = null;
                }

                public Info(string ctx, string msg, int lineIndex)
                {
                    Context = ctx;
                    Msg = msg;
                    LineIndex = lineIndex;
                    LineRange = null;
                }

                public Info(string ctx, string msg, LineRange lineRange)
                {
                    Context = ctx;
                    Msg = msg;
                    LineIndex = null;
                    LineRange = lineRange;
                }
            }

            public sealed class Warning
            {
                public string Context { get; }
                public string Msg { get; }
                public int? LineIndex { get; }
                public LineRange? LineRange { get; }

                public Warning(string ctx, string msg)
                {
                    Context = ctx;
                    Msg = msg;
                    LineIndex = null;
                    LineRange = null;
                }

                public Warning(string ctx, string msg, int lineIndex)
                {
                    Context = ctx;
                    Msg = msg;
                    LineIndex = lineIndex;
                    LineRange = null;
                }

                public Warning(string ctx, string msg, LineRange lineRange)
                {
                    Context = ctx;
                    Msg = msg;
                    LineIndex = null;
                    LineRange = lineRange;
                }
            }

            public sealed class Execution
            {
                public string Msg { get; }
                public DateTime TimestampStart { get; }
                public DateTime TimestampFinish { get; }
                public LineRange LineRange { get; }

                public Execution(string msg, DateTime timestampStart, DateTime timestampFinish, LineRange lineRange)
                {
                    Msg = msg;
                    TimestampStart = timestampStart;
                    TimestampFinish = timestampFinish;
                    LineRange = lineRange;
                }
            }

            private List<Info> mInfos = new List<Info>();
            public ReadOnlyCollection<Info> Infos
            {
                get { return mInfos.AsReadOnly(); }
            }

            private List<Warning> mWarns = new List<Warning>();
            public ReadOnlyCollection<Warning> Warnings
            {
                get { return mWarns.AsReadOnly(); }
            }

            private List<Execution> mExecutions = new List<Execution>();
            public ReadOnlyCollection<Execution> Executions
            {
                get { return mExecutions.AsReadOnly(); }
            }

            public bool HasWarnings { get => (mWarns.Count > 0); }

            public Inspection(string jsonContent)
            {
                using (var json = JsonDocument.Parse(jsonContent))
                {
                    JsonElement jsonExecs;
                    if (json.RootElement.TryGetProperty("executions", out jsonExecs))
                    {
                        foreach (var jsonExec in jsonExecs.EnumerateArray())
                        {
                            JsonElement jsonTimestamp, jsonRange;
                            if (!jsonExec.TryGetProperty("timestamp", out jsonTimestamp) || !jsonExec.TryGetProperty("lineRange", out jsonRange))
                                continue;

                            var timestampStart = DateTimeOffset.FromUnixTimeMilliseconds(jsonTimestamp.GetProperty("start").GetInt64()).DateTime;
                            var timestampFinish = DateTimeOffset.FromUnixTimeMilliseconds(jsonTimestamp.GetProperty("finish").GetInt64()).DateTime;

                            mExecutions.Add(new Execution(jsonExec.GetProperty("msg").GetString(), timestampStart, timestampFinish, new LineRange(jsonRange.GetProperty("start").GetInt32(), jsonRange.GetProperty("end").GetInt32())));
                        }
                    }

                    JsonElement jsonInfos;
                    if (json.RootElement.TryGetProperty("infos", out jsonInfos))
                    {
                        foreach (var jsonInfo in jsonInfos.EnumerateArray())
                        {
                            JsonElement jsonLine;
                            if (jsonInfo.TryGetProperty("lineIndex", out jsonLine))
                                mInfos.Add(new Info(jsonInfo.GetProperty("ctx").GetString(), jsonInfo.GetProperty("msg").GetString(), jsonLine.GetInt32()));
                            else if (jsonInfo.TryGetProperty("lineRange", out jsonLine))
                                mInfos.Add(new Info(jsonInfo.GetProperty("ctx").GetString(), jsonInfo.GetProperty("msg").GetString(), new LineRange(jsonLine.GetProperty("start").GetInt32(), jsonLine.GetProperty("end").GetInt32())));
                            else
                                mInfos.Add(new Info(jsonInfo.GetProperty("ctx").GetString(), jsonInfo.GetProperty("msg").GetString()));
                        }
                    }

                    JsonElement jsonWarns;
                    if (json.RootElement.TryGetProperty("warns", out jsonWarns))
                    {
                        foreach (var jsonWarn in jsonWarns.EnumerateArray())
                        {
                            JsonElement jsonLine;
                            if (jsonWarn.TryGetProperty("lineIndex", out jsonLine))
                                mWarns.Add(new Warning(jsonWarn.GetProperty("ctx").GetString(), jsonWarn.GetProperty("msg").GetString(), jsonLine.GetInt32()));
                            else if (jsonWarn.TryGetProperty("lineRange", out jsonLine))
                                mWarns.Add(new Warning(jsonWarn.GetProperty("ctx").GetString(), jsonWarn.GetProperty("msg").GetString(), new LineRange(jsonLine.GetProperty("start").GetInt32(), jsonLine.GetProperty("end").GetInt32())));
                            else
                                mWarns.Add(new Warning(jsonWarn.GetProperty("ctx").GetString(), jsonWarn.GetProperty("msg").GetString()));
                        }
                    }
                }
            }
        }

        private IntPtr mNativeRepo;
        private bool disposed = false;
        private List<CommandTag> mCmdTags = new List<CommandTag>();

        public int NumFiles { get; } = 0;
        public int NumLines { get; } = 0;
        public SummaryInfo Summary { get; }

        public ReadOnlyCollection<CommandTag> CommandTags
        {
            get { return mCmdTags.AsReadOnly(); }
        }

        public static Repo InitRepoFile(FlavorType flavor, string filePath)
        {
            switch (flavor)
            {
                case FlavorType.WCSComlib:
                    return Unmanaged.RepoInitFile(Unmanaged.laFlavorType.WCSComlib, filePath);
                case FlavorType.WCSAndroidLogcat:
                    return Unmanaged.RepoInitFile(Unmanaged.laFlavorType.WCSAndroidLogcat, filePath);
                case FlavorType.WCSServer:
                    return Unmanaged.RepoInitFile(Unmanaged.laFlavorType.WCSServer, filePath);
                default:
                    break;
            }

            return null;
        }

        public static Repo InitRepoFolder(FlavorType flavor, string folderPath)
        {
            switch (flavor)
            {
                case FlavorType.WCSComlib:
                    return Unmanaged.RepoInitFolder(Unmanaged.laFlavorType.WCSComlib, folderPath);
                case FlavorType.WCSAndroidLogcat:
                    return Unmanaged.RepoInitFolder(Unmanaged.laFlavorType.WCSAndroidLogcat, folderPath);
                case FlavorType.WCSServer:
                    return Unmanaged.RepoInitFolder(Unmanaged.laFlavorType.WCSServer, folderPath);
                default:
                    break;
            }

            return null;
        }

        public static Repo InitRepoCommand(Repo sourceRepo, string cmdResult)
        {
            return Unmanaged.RepoInitCommand(sourceRepo, cmdResult);
        }

        public static Repo InitLineRange(Repo sourceRepo, int indexStart, int count)
        {
            return Unmanaged.RepoInitLineRange(sourceRepo, indexStart, count);
        }

        public static Repo InitTags(Repo sourceRepo, IList<string> tags)
        {
            return Unmanaged.RepoInitTags(sourceRepo, tags);
        }

        private Repo(IntPtr nativeRepo)
        {
            mNativeRepo = nativeRepo;

            NumFiles = Unmanaged.RepoNumFiles(this);
            NumLines = Unmanaged.RepoNumLines(this);
            if (NumLines <= 0)
                return;

            Summary = new SummaryInfo(Unmanaged.RepoGetSummary(this));
            mCmdTags = CommandTag.ParseCommandTags(Unmanaged.RepoGetAvailableCommands(this));
        }

        public IEnumerable<Command> GetCommands(string commandTag)
        {
            foreach(var cmdTag in mCmdTags)
            {
                if (cmdTag.Name == commandTag)
                    return cmdTag.Cmds;
            }

            return Array.Empty<Command>();
        }

        public int? GetLineIndex(int lineId)
        {
            return Unmanaged.RepoGetLineIndex(this, lineId);
        }

        public Search SearchText(string query, CaseSensitivity caseSensitivity, int startLine, int startLineOffset)
        {
            IntPtr searchCtx;
            switch (caseSensitivity)
            {
                case CaseSensitivity.CaseSensitive:
                    searchCtx = Unmanaged.RepoSearchStart(this, query, Unmanaged.laFindOptionsCaseSensitivity.CaseSensitive, startLine, startLineOffset);
                    break;
                case CaseSensitivity.None:
                default:
                    searchCtx = Unmanaged.RepoSearchStart(this, query, Unmanaged.laFindOptionsCaseSensitivity.None, startLine, startLineOffset);
                    break;
            }

            if (searchCtx == IntPtr.Zero)
                return null;

            return new Search(this, searchCtx);
        }

        public Search SearchRegex(string query, CaseSensitivity caseSensitivity, int startLine, int startLineOffset)
        {
            IntPtr searchCtx;
            switch (caseSensitivity)
            {
                case CaseSensitivity.CaseSensitive:
                    searchCtx = Unmanaged.RepoSearchStartRegex(this, query, Unmanaged.laFindOptionsCaseSensitivity.CaseSensitive, startLine, startLineOffset);
                    break;
                case CaseSensitivity.None:
                default:
                    searchCtx = Unmanaged.RepoSearchStartRegex(this, query, Unmanaged.laFindOptionsCaseSensitivity.None, startLine, startLineOffset);
                    break;
            }

            if (searchCtx == IntPtr.Zero)
                return null;

            return new Search(this, searchCtx);
        }

        public string FindAll(string query, CaseSensitivity caseSensitivity)
        {
            switch (caseSensitivity)
            {
                case CaseSensitivity.CaseSensitive:
                    return Unmanaged.RepoFindAll(this, query, Unmanaged.laFindOptionsCaseSensitivity.CaseSensitive);
                case CaseSensitivity.None:
                default:
                    return Unmanaged.RepoFindAll(this, query, Unmanaged.laFindOptionsCaseSensitivity.None);
            }
        }

        public string FindAllRegex(string query, CaseSensitivity caseSensitivity)
        {
            switch (caseSensitivity)
            {
                case CaseSensitivity.CaseSensitive:
                    return Unmanaged.RepoFindAllRegex(this, query, Unmanaged.laFindOptionsCaseSensitivity.CaseSensitive);
                case CaseSensitivity.None:
                default:
                    return Unmanaged.RepoFindAllRegex(this, query, Unmanaged.laFindOptionsCaseSensitivity.None);
            }
        }

        public string RetrieveLineContent(int lineIndex, TranslationType translationType, TranslationFormat translationFormat)
        {
            Unmanaged.laTranslatorType type;
            Unmanaged.laTranslatorFormat format;

            switch (translationType)
            {
                case TranslationType.Translated:
                    type = Unmanaged.laTranslatorType.Translated;
                    break;
                case TranslationType.Raw:
                default:
                    type = Unmanaged.laTranslatorType.Raw;
                    break;
            }

            switch (translationFormat)
            {
                case TranslationFormat.JsonFull:
                    format = Unmanaged.laTranslatorFormat.JsonFull;
                    break;
                case TranslationFormat.JsonSingleParams:
                    format = Unmanaged.laTranslatorFormat.JsonSingleParams;
                    break;
                case TranslationFormat.Line:
                default:
                    format = Unmanaged.laTranslatorFormat.Line;
                    break;
            }

            return Unmanaged.RepoRetrieveLineContent(this, lineIndex, type, format);
        }

        public string ExecuteInspection()
        {
            return Unmanaged.RepoExecuteInspection(this);
        }

        public string ExecuteCommand(string cmdTag, string cmdName)
        {
            return Unmanaged.RepoExecuteCommand(this, cmdTag, cmdName);
        }

        public string ExecuteCommand(string cmdTag, string cmdName, string cmdParams)
        {
            return Unmanaged.RepoExecuteCommand(this, cmdTag, cmdName, cmdParams);
        }

        public bool ExportLines(string filePath, int indexStart, int count)
        {
            return Unmanaged.RepoExportLines(this, filePath, indexStart, count);
        }

        public bool ExportCommandLines(string filePath, string cmdResult)
        {
            return Unmanaged.RepoExportCommandLines(this, filePath, cmdResult);
        }

        public bool ExportCommandNetworkPackets(string filePath, string cmdResult)
        {
            return Unmanaged.RepoExportCommandNetworkPackets(this, filePath, cmdResult);
        }

        ~Repo()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposed)
                return;
            disposed = true;

            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
            }

            Unmanaged.RepoDestroy(this);
            mNativeRepo = IntPtr.Zero;
        }
    }
}
