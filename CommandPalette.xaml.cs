using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Input;

namespace LogAnalyzerWPF
{
    /// <summary>
    /// Interaction logic for CommandPalette.xaml
    /// </summary>
    public partial class CommandPalette : AdonisUI.Controls.AdonisWindow
    {
        private static int LongestCommonSubsequence(string s1, string s2)
        {
            int i, j;
            int s1Len = s1.Length;
            int s2Len = s2.Length;
            int[] z = new int[(s1Len + 1) * (s2Len + 1)];
            int[,] c = new int[(s1Len + 1), (s2Len + 1)];

            for (i = 0; i <= s1Len; ++i)
                c[i, 0] = z[i * (s2Len + 1)];

            for (i = 1; i <= s1Len; ++i)
            {
                for (j = 1; j <= s2Len; ++j)
                {
                    if (s1[i - 1] == s2[j - 1])
                        c[i, j] = c[i - 1, j - 1] + 1;
                    else
                        c[i, j] = Math.Max(c[i - 1, j], c[i, j - 1]);
                }
            }

            return c[s1Len, s2Len];
        }

        private static int LongestCommonSubsequence(string s1, string s2, out string output)
        {
            int i, j, k, t;
            int s1Len = s1.Length;
            int s2Len = s2.Length;
            int[] z = new int[(s1Len + 1) * (s2Len + 1)];
            int[,] c = new int[(s1Len + 1), (s2Len + 1)];

            for (i = 0; i <= s1Len; ++i)
                c[i, 0] = z[i * (s2Len + 1)];

            for (i = 1; i <= s1Len; ++i)
            {
                for (j = 1; j <= s2Len; ++j)
                {
                    if (s1[i - 1] == s2[j - 1])
                        c[i, j] = c[i - 1, j - 1] + 1;
                    else
                        c[i, j] = Math.Max(c[i - 1, j], c[i, j - 1]);
                }
            }

            t = c[s1Len, s2Len];
            char[] outputSB = new char[t];

            for (i = s1Len, j = s2Len, k = t - 1; k >= 0;)
            {
                if (s1[i - 1] == s2[j - 1])
                {
                    outputSB[k] = s1[i - 1];
                    --i;
                    --j;
                    --k;
                }
                else if (c[i, j - 1] > c[i - 1, j])
                    --j;
                else
                    --i;
            }

            output = new string(outputSB);

            return t;
        }

        public class MainModel : INotifyPropertyChanged
        {
            private bool mShowCommands = true;
            public bool ShowCommands
            {
                get => mShowCommands;
                set
                {
                    if (value.Equals(mShowCommands))
                        return;

                    mShowCommands = value;
                    OnPropertyChanged();
                }
            }

            private bool mShowCommandsFiltered = true;
            public bool ShowCommandsFiltered
            {
                get => mShowCommandsFiltered;
                set
                {
                    if (value.Equals(mShowCommandsFiltered))
                        return;

                    mShowCommandsFiltered = value;
                    OnPropertyChanged();
                }
            }

            private bool mShowWait = false;
            public bool ShowWait
            {
                get => mShowWait;
                set
                {
                    if (value.Equals(mShowWait))
                        return;

                    mShowWait = value;
                    OnPropertyChanged();
                }
            }

            private List<Command> mCmds = new List<Command>();
            public List<Command> Cmds
            {
                get => mCmds;
                set
                {
                    if (value.Equals(mCmds))
                        return;

                    mCmds = value;
                    OnPropertyChanged();
                }
            }

            private List<Command> mCmdsFiltered = new List<Command>();
            public List<Command> CmdsFiltered
            {
                get => mCmdsFiltered;
                set
                {
                    if (value.Equals(mCmdsFiltered))
                        return;

                    mCmdsFiltered = value;
                    OnPropertyChanged();
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;

            protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public sealed class Command
        {
            public enum CommandType { RepoCommand, Action, Separator }

            public CommandType Type { get; }

            public string Tag { get; }
            public string Name { get; }
            public string Desc { get; }
            public string ParamsDesc { get; }

            public bool ShowDesc { get => !string.IsNullOrWhiteSpace(Desc); }
            public bool HasParams { get => !string.IsNullOrWhiteSpace(ParamsDesc); }

            public Command(CommandType type, string name)
            {
                Type = type;
                Name = name;
            }

            public Command(CommandType type, string name, string desc)
                 : this(type, name)
            {
                Desc = desc;
            }

            public Command(CommandType type, string tag, string name, string desc)
                : this(type, name, desc)
            {
                Tag = tag;
            }

            public Command(CommandType type, string tag, string name, string desc, string paramsDesc)
                : this(type, tag, name, desc)
            {
                ParamsDesc = paramsDesc;
            }
        }

        private readonly Repo mRepo;
        private Timer mTimer;
        private string mCurrentFilter;
        private Command mSelectedCommand;
        private MainModel mMainModel = new MainModel();
        private List<Command> mCmds = new List<Command>();

        public string CommandJsonResult { get; private set; }

        public CommandPalette(Repo repo)
        {
            InitializeComponent();

            //generate all commands
            {
                mCmds.Add(new Command(Command.CommandType.Separator, "Misc"));
                mCmds.Add(new Command(Command.CommandType.Action, "Misc", "Find all...", "Find every match of text", "text to search for"));
                mCmds.Add(new Command(Command.CommandType.Action, "Misc", "Find all... (regex)", "Find every match of regex", "regex expression to match text with"));
                mCmds.Add(new Command(Command.CommandType.Action, "Misc", "Warnings", "Gather all warning lines"));
                mCmds.Add(new Command(Command.CommandType.Action, "Misc", "Errors", "Gather all error lines"));

                foreach (var cmdTag in repo.CommandTags)
                {
                    mCmds.Add(new Command(Command.CommandType.Separator, cmdTag.Name));

                    foreach (var cmd in cmdTag.Cmds)
                        mCmds.Add(new Command(Command.CommandType.RepoCommand, cmdTag.Name, cmd.Name, cmd.Desc, cmd.ParamsDesc));
                }
            }

            {
                mTimer = new System.Timers.Timer(250);
                mTimer.Elapsed += (s, e) =>
                {
                    mTimer.Stop();
                    filterCommands(mCurrentFilter);
                };
            }

            mRepo = repo;
            mMainModel.Cmds = mCmds; //no filter applied
            DataContext = mMainModel;

            PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape) Close(); }; //shortcut to close the window

            mListCommands.PreviewKeyDown += (s, e) =>
            {
                if ((e.Key != Key.Enter) || !(mListCommands.SelectedItem is Command))
                    return;

                var cmd = mListCommands.SelectedItem as Command;

                switch (cmd.Type)
                {
                    case Command.CommandType.Action:
                    case Command.CommandType.RepoCommand:
                        if (string.IsNullOrWhiteSpace(cmd.ParamsDesc))
                        {
                            if (cmd.Type == Command.CommandType.Action)
                                executeAction(cmd.Name);
                            else
                                executeCommand(cmd.Tag, cmd.Name);
                        }
                        else
                        {
                            mTextInput.Focus();
                            mTextInput.Text = "";
                            mSelectedCommand = cmd;
                            mMainModel.Cmds = new List<Command>();

                            AdonisUI.Extensions.WatermarkExtension.SetWatermark(mTextInput, cmd.ParamsDesc);
                        }
                        break;
                    case Command.CommandType.Separator:
                    default:
                        return;
                }
            };

            mTextInput.Focus();
            mTextInput.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Down)
                {
                    mListCommands.Focus();
                    return;
                }

                if (e.Key == Key.Enter)
                {
                    if (mSelectedCommand == null)
                        return;

                    switch (mSelectedCommand.Type)
                    {
                        case Command.CommandType.Action:
                            executeAction(mSelectedCommand.Name, mTextInput.Text);
                            return;
                        case Command.CommandType.RepoCommand:
                            executeCommand(mSelectedCommand.Tag, mSelectedCommand.Name, mTextInput.Text);
                            break;
                        case Command.CommandType.Separator:
                        default:
                            return;
                    }
                }

                if (!(mSelectedCommand is null))
                    return;

                mCurrentFilter = mTextInput.Text;

                mTimer.Stop();
                mTimer.Start();
            };
        }

        private async void executeCommand(string cmdTag, string cmdName)
        {
            mTextInput.IsEnabled = false;
            mMainModel.ShowCommands = false;
            mMainModel.ShowWait = true;

            var cmdResult = await Task<string>.Run(() =>
            {
                System.Threading.Thread.Sleep(500);
                return mRepo.ExecuteCommand(cmdTag, cmdName);
            });

            CommandJsonResult = cmdResult;
            Close();
        }

        private async void executeCommand(string cmdTag, string cmdName, string cmdParams)
        {
            mTextInput.IsEnabled = false;
            mMainModel.ShowCommands = false;
            mMainModel.ShowWait = true;

            var cmdResult = await Task<string>.Run(() =>
            {
                System.Threading.Thread.Sleep(500);
                return mRepo.ExecuteCommand(cmdTag, cmdName, cmdParams);
            });

            CommandJsonResult = cmdResult;
            Close();
        }

        private static string convertLineIndicesToJson(string cmdTag, string cmdName, IEnumerable<int> lineIndices)
        {
            using (var stream = new System.IO.MemoryStream())
            {
                using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                {
                    writer.WriteStartObject();
                    {
                        writer.WriteStartObject("command");
                        writer.WriteString("name", cmdName);
                        writer.WriteString("params", "");
                        writer.WriteString("tag", cmdTag);
                        writer.WriteEndObject();
                    }
                    writer.WriteBoolean("executed", true);
                    {
                        writer.WriteStartArray("linesIndices");
                        writer.WriteStartObject();
                        writer.WriteStartArray("indices");

                        foreach (var lineIndex in lineIndices)
                            writer.WriteNumberValue(lineIndex);

                        writer.WriteEndArray();
                        writer.WriteEndObject();
                        writer.WriteEndArray();
                    }
                    {
                        writer.WriteStartArray("networkPackets");
                        writer.WriteEndArray();
                    }
                    writer.WriteEndObject();
                }

                return System.Text.Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private async void executeAction(string action)
        {
            if ((action == "Warnings") || (action == "Errors"))
            {
                var cmdResult = await Task<string>.Run(() =>
                {
                    System.Threading.Thread.Sleep(500);

                    if (action == "Warnings")
                        return convertLineIndicesToJson("General", "All warnings", mRepo.Summary.WarningLineIndices);
                    return convertLineIndicesToJson("General", "All errors", mRepo.Summary.ErrorLineIndices);
                });

                CommandJsonResult = cmdResult;
                Close();

                return;
            }
        }

        private async void executeAction(string action, string actionParams)
        {
            if ((action == "Find all...") || (action == "Find all... (regex)"))
            {
                mTextInput.IsEnabled = false;
                mMainModel.ShowCommands = false;
                mMainModel.ShowWait = true;

                var cmdResult = await Task<string>.Run(() =>
                {
                    System.Threading.Thread.Sleep(500);

                    var searchResult = (action == "Find all...") ? mRepo.FindAll(actionParams, Repo.CaseSensitivity.None) : mRepo.FindAllRegex(actionParams, Repo.CaseSensitivity.None);

                    var lineIndices = new List<int>();
                    foreach (var jElem in JsonDocument.Parse(searchResult).RootElement.EnumerateArray())
                        lineIndices.Add(jElem.GetProperty("index").GetInt32());

                    return convertLineIndicesToJson("General", $"Find ({actionParams})", lineIndices);
                });

                CommandJsonResult = cmdResult;
                Close();

                return;
            }
        }

        private void filterCommands(string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                if (mMainModel.Cmds.Count != mCmds.Count)
                    mMainModel.Cmds = mCmds;
                return;
            }

            filter = filter.ToLower();

            var sorted = new List<Tuple<int, Command>>();
            foreach (var cmd in mCmds)
            {
                if (cmd.Type == Command.CommandType.Separator)
                    continue;

                var testString = cmd.Name.ToLower();
                if (testString.Contains(filter))
                {
                    sorted.Add(new Tuple<int, Command>(int.MaxValue, cmd));
                    continue;
                }

                var maxSequence = LongestCommonSubsequence(filter, testString);
                if (!string.IsNullOrWhiteSpace(cmd.Desc))
                {
                    testString = cmd.Desc.ToLower();
                    if (testString.Contains(filter))
                    {
                        sorted.Add(new Tuple<int, Command>(int.MaxValue - 1, cmd));
                        continue;
                    }

                    var maxSequence2 = LongestCommonSubsequence(filter, testString);
                    maxSequence = Math.Max(maxSequence, maxSequence2);
                }

                sorted.Add(new Tuple<int, Command>(maxSequence, cmd));
            }

            mMainModel.Cmds = (from item in sorted orderby item.Item1 descending, item.Item2.Name ascending select item.Item2).ToList();
        }

    }
}
