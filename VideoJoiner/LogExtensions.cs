using System;

namespace VideoJoiner
{
    static class LogExtensions
    {
        public static void Log(string contents, MessageType messageType = MessageType.INFO, bool withTitle = true, bool onlyTitleColor = true, WriteMode writeMode = WriteMode.NewLine)
        {
            switch (messageType)
            {
                case MessageType.INFO: Console.ForegroundColor = ConsoleColor.Green; break;
                case MessageType.WARN: Console.ForegroundColor = ConsoleColor.Yellow; break;
                case MessageType.ERROR: Console.ForegroundColor = ConsoleColor.Red; break;
                case MessageType.TIME: Console.ForegroundColor = ConsoleColor.Blue; break;
                case MessageType.PATH: Console.ForegroundColor = ConsoleColor.DarkMagenta; break;
                default: Console.ResetColor(); break;
            }
            if (withTitle) Console.Write($"{ messageType.ToString().PadRight(6, ' ') }");

            if (onlyTitleColor) Console.ResetColor();
            Console.Write(contents);

            Console.ResetColor();
            if (writeMode == WriteMode.NewLine) Console.WriteLine();
        }

        public enum MessageType
        {
            INFO,
            WARN,
            ERROR,
            TIME,
            NONE,
            PATH
        }

        public enum WriteMode
        {
            Append,
            NewLine
        }
    }
}
