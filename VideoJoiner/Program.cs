using NReco.VideoConverter;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using static VideoJoiner.FFMpegSettings;
using static VideoJoiner.LogExtensions;

namespace VideoJoiner
{
    class Program
    {
        static string SourcePath { get; set; } = Environment.CurrentDirectory;
        static string InputsType { get; set; } = "mov";
        static string OutputType { get; set; } = "mp4";
        static int Intervals { get; set; } = 3;

        static Dictionary<string, List<Video>> Videos { get; set; }
        static List<string[]> PassVideos { get; set; }

        static FFMpegConverter FFMpegConverter { get; set; }
        static FFMpegSettings FFMpegSettings { get; set; } = new FFMpegSettings();
        static string JoinVideoName { get; set; }
        static string PresetRecord = string.Empty;

        static void Main(string[] args)
        {
            _handler += new EventHandler(Handler);
            SetConsoleCtrlHandler(_handler, true);
            
            try
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i].Substring(0, 2) != "--")
                        switch (args[i])
                        {
                            case "-p": SourcePath = args[++i].TrimEnd('\\'); break;
                            case "-it": InputsType = args[++i].TrimStart('.').ToLower(); break;
                            case "-i": Intervals = int.TryParse(args[++i], out int intervals) ? intervals : 3; break;
                        }
                    else
                    {
                        string param = args[i].Remove(args[i].IndexOf('=') + 1);
                        string value = args[i].Substring(args[i].IndexOf('=') + 1);
                        switch (param)
                        {
                            case "--path": SourcePath = value.TrimEnd('\\'); break;
                            case "--inputs_type": InputsType = value.TrimStart('.').ToLower(); break;
                            case "--output_type": OutputType = value.TrimStart('.').ToLower(); break;
                            case "--interval": Intervals = int.TryParse(value, out int intervals) ? intervals : 3; break;
                            case "--preset":
                                if (Enum.GetNames(typeof(PresetType)).Any(type => type.ToLower() == args[i + 1].ToLower()))
                                    FFMpegSettings.Preset = (PresetType)Enum.Parse(typeof(PresetType), value, true);
                                else
                                    PresetRecord = value;
                                break;
                            case "--crf":
                                if (int.TryParse(value, out int crfScale))
                                    FFMpegSettings.CRFScale = crfScale;
                                else
                                    FFMpegSettings.CRFScale = -1;
                                break;
                            case "--aq":
                                if (decimal.TryParse(args[i + 1], out decimal audioQuality))
                                    FFMpegSettings.AudioQuality = Math.Round(audioQuality, 1, MidpointRounding.AwayFromZero);
                                else
                                    FFMpegSettings.AudioQuality = -1;
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log(contents: "Error input", messageType: MessageType.ERROR);
                Log(contents: ex.Message, messageType: MessageType.ERROR);
                Console.WriteLine();
                return;
            }

            Log(contents: "Start processing");
            long beginTick = DateTime.Now.Ticks;
            if (!Directory.Exists(SourcePath))
                Log(contents: $@"Can NOT match the source folder ""{ SourcePath }""", messageType: MessageType.ERROR);
            else if (!typeof(Format).GetFields(BindingFlags.Public | BindingFlags.Static).Any(format => format.Name.ToLower() == InputsType))
                Log(contents: $@"""{ InputsType.ToUpper() }"" is NOT support for input", messageType: MessageType.ERROR);
            else if (!typeof(Format).GetFields(BindingFlags.Public | BindingFlags.Static).Any(format => format.Name.ToLower() == OutputType))
                Log(contents: $@"""{ OutputType.ToUpper() }"" is NOT support for output", messageType: MessageType.ERROR);
            else if (!string.IsNullOrEmpty(PresetRecord))
                Log(contents: $@"""{ PresetRecord.ToUpper() }"" is NOT support for preset type", messageType: MessageType.ERROR);
            else if (51 < FFMpegSettings.CRFScale || FFMpegSettings.CRFScale < 0)
                Log(contents: $@"""{ FFMpegSettings.CRFScale }"" is out of CRF scale range (the value should be 0-51)", messageType: MessageType.ERROR);
            else if (10m < FFMpegSettings.AudioQuality || FFMpegSettings.AudioQuality < 0.1m)
                Log(contents: $@"""{ FFMpegSettings.AudioQuality }"" is out of audio quality range (the value should be 1-10)", messageType: MessageType.ERROR);
            else
            {
                try
                {
                    Log(contents: "Parse the folder structure", messageType: MessageType.INFO);
                    Videos = new Dictionary<string, List<Video>>();
                    PassVideos = new List<string[]>();
                    foreach (var videoPath in Directory.GetFiles(SourcePath, $"*.{ InputsType }").ToList())
                    {
                        string videoName = videoPath;
                        videoName = videoName.Remove(0, videoName.LastIndexOf('\\') + 1);
                        videoName = videoName.Remove(videoName.LastIndexOf('.'));


                        if (Videos.Count <= 0)
                            Videos.Add(videoName, new List<Video>() {
                                new Video()
                                {
                                    Name = videoName,
                                    Path = videoPath,
                                    Type = InputsType
                                } });
                        else
                        {
                            int length = DateTime.Now.ToString("yyyyMMdd_HHmmss").Length;
                            if (videoName.Length != length)
                                PassVideos.Add(new string[] { videoName, $"Error name format (length != { length })" });
                            else if (!Regex.IsMatch(videoName, @"^\d+_\d+$"))
                                PassVideos.Add(new string[] { videoName, $"Error name format (is not all numbers)" });
                            else
                            {
                                DateTime lastTime = DateTime.ParseExact(Videos.Last().Value.Last().Name, "yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                                DateTime thisTime = DateTime.ParseExact(videoName, "yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);

                                if ((thisTime - lastTime).TotalMinutes <= Intervals)
                                    Videos.Last().Value.Add(new Video()
                                    {
                                        Name = videoName,
                                        Path = videoPath,
                                        Type = InputsType
                                    });
                                else
                                    Videos.Add(videoName, new List<Video>() {
                                        new Video()
                                        {
                                            Name = videoName,
                                            Path = videoPath,
                                            Type = InputsType
                                        } });
                            }
                        }
                    }
                    DisplayVideos();

                    // Init FFMpegConverter
                    FFMpegConverter = new FFMpegConverter();
                    FFMpegConverter.ConvertProgress += FFMpegConverter_ConvertProgress;
                    var format = typeof(Format)
                        .GetFields(BindingFlags.Public | BindingFlags.Static)
                        .First(f => f.Name.ToLower() == OutputType).Name;

                    // Join videos
                    foreach (var pair in Videos)
                    {
                        try
                        {
                            string folderPath = $@"{ SourcePath }\Joins";
                            if (!Directory.Exists(folderPath))
                                Directory.CreateDirectory(folderPath);

                            JoinVideoName = pair.Key;

                            Log(contents: $"Preparing to join video in { JoinVideoName }.{ format.ToString() }",
                                messageType: MessageType.INFO);

                            FFMpegConverter.ConcatMedia(
                                inputFiles: pair.Value.Select(video => video.Path).ToArray(),
                                outputFile: $@"{ folderPath }\{ pair.Key }.{ format.ToString() }",
                                outputFormat: Format.mov.ToString(),
                                settings: new ConcatSettings() { CustomOutputArgs = FFMpegSettings.GetArgsLine() });
                            
                            ClearCurrentConsoleLine();
                            Log(contents: $"Join video in { JoinVideoName }.{ format.ToString() }",
                                messageType: MessageType.INFO,
                                writeMode: WriteMode.Append);
                            Log(contents: " take", messageType: MessageType.INFO, withTitle: false, writeMode: WriteMode.Append);
                            Log(contents:
                                $" { new TimeSpan(DateTime.Now.Ticks - beginTick).Days.ToString().PadLeft(2, ' ') } d" +
                                $" { new TimeSpan(DateTime.Now.Ticks - beginTick).Hours.ToString().PadLeft(2, ' ') } h" +
                                $" { new TimeSpan(DateTime.Now.Ticks - beginTick).Minutes.ToString().PadLeft(2, ' ') } m" +
                                $" { new TimeSpan(DateTime.Now.Ticks - beginTick).Seconds.ToString().PadLeft(2, ' ') } s",
                                messageType: MessageType.TIME, withTitle: false, onlyTitleColor: false);
                        }
                        catch (Exception ex)
                        {
                            Log(contents: $"Join video in { JoinVideoName }.{ format.ToString() } ({ ex.Message })",
                                messageType: MessageType.ERROR);
                        }
                    }

                    // Summary
                    Log(contents: $"Process exited in",
                        messageType: MessageType.INFO, withTitle: true, writeMode: WriteMode.Append);
                    Log(contents:
                        $" { new TimeSpan(DateTime.Now.Ticks - beginTick).Days.ToString() } d" +
                        $" { new TimeSpan(DateTime.Now.Ticks - beginTick).Hours.ToString() } h" +
                        $" { new TimeSpan(DateTime.Now.Ticks - beginTick).Minutes.ToString() } m" +
                        $" { new TimeSpan(DateTime.Now.Ticks - beginTick).Seconds.ToString() } s",
                        messageType: MessageType.TIME, withTitle: false, onlyTitleColor: false, writeMode: WriteMode.Append);
                }
                catch (Exception ex) { Log(contents: ex.Message, messageType: MessageType.ERROR); }
            }
            Console.WriteLine();
        }

        static void FFMpegConverter_ConvertProgress(object sender, ConvertProgressEventArgs e)
        {
            Console.SetCursorPosition(left: 0, top: Console.CursorTop);

            Log(contents: "", messageType: MessageType.INFO, writeMode: WriteMode.Append);

            Console.ResetColor();
            Console.Write($"{ JoinVideoName }.{ OutputType } |");

            double percent = (e.Processed.TotalMilliseconds / e.TotalDuration.TotalMilliseconds) * 100;

            Console.BackgroundColor = ConsoleColor.DarkYellow;
            for (int i = 2; i <= (int)percent; i += 2)
                Console.Write(" ");
            Console.BackgroundColor = ConsoleColor.Gray;
            for (int i = (int)percent + (2 - ((int)percent % 2)); i <= 100; i += 2)
                Console.Write(" ");

            Console.ResetColor();
            Console.Write($"|{ percent.ToString("  0.00") }%");
        }

        static void DisplayVideos()
        {
            Log(contents: $"{ SourcePath } ({ Videos.Sum(pair => pair.Value.Count()) })", messageType: MessageType.INFO);
            foreach (var pair in Videos)
            {
                if (pair.Equals(Videos.Last()))
                    Log(contents: $"\t└─{ pair.Key } ({ pair.Value.Count })", messageType: MessageType.NONE, withTitle: false, onlyTitleColor: true);
                else
                    Log(contents: $"\t├─{ pair.Key } ({ pair.Value.Count })", messageType: MessageType.NONE, withTitle: false, onlyTitleColor: true);
                foreach (var video in pair.Value)
                {
                    if (pair.Equals(Videos.Last()))
                        Log(contents: $"\t ", messageType: MessageType.NONE, withTitle: false, onlyTitleColor: true, writeMode: WriteMode.Append);
                    else
                        Log(contents: $"\t│", messageType: MessageType.NONE, withTitle: false, onlyTitleColor: true, writeMode: WriteMode.Append);
                    if (video.Equals(pair.Value.Last()))
                        Log(contents: $"\t└─", messageType: MessageType.NONE, withTitle: false, onlyTitleColor: true, writeMode: WriteMode.Append);
                    else
                        Log(contents: $"\t├─", messageType: MessageType.NONE, withTitle: false, onlyTitleColor: true, writeMode: WriteMode.Append);
                    Log(contents: $"{ video.Name }.{ video.Type }", messageType: MessageType.PATH, withTitle: false, onlyTitleColor: false);
                }
            }
        }

        static void ClearCurrentConsoleLine()
        {
            Console.SetCursorPosition(0, Console.CursorTop - 1);
            Console.Write(new string(' ', Console.WindowWidth));
            Console.SetCursorPosition(0, Console.CursorTop - 0);
            Console.Write(new string(' ', Console.WindowWidth));
            Console.SetCursorPosition(0, Console.CursorTop - 2);
        }

        #region Exit Console App

        [DllImport("Kernel32")]
        static extern bool SetConsoleCtrlHandler(EventHandler handler, bool add);

        delegate bool EventHandler(CtrlType sig);
        static EventHandler _handler;

        enum CtrlType
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }

        static bool Handler(CtrlType sig)
        {
            switch (sig)
            {
                case CtrlType.CTRL_C_EVENT:
                case CtrlType.CTRL_LOGOFF_EVENT:
                case CtrlType.CTRL_SHUTDOWN_EVENT:
                case CtrlType.CTRL_CLOSE_EVENT:
                default:
                    Console.WriteLine();
                    try
                    {
                        foreach (var process in Process.GetProcessesByName("ffmpeg"))
                            process.Kill();
                    }
                    catch (Exception) { }
                    try
                    {
                        if (File.Exists($@"{ SourcePath }\ffmpeg.exe"))
                            File.Delete($@"{ SourcePath }\ffmpeg.exe");
                    }
                    catch (Exception) { }
                    Environment.Exit(0);
                    return false;
            }
        }

        #endregion
    }
}
