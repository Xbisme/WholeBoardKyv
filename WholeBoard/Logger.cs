using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WholeBoard
{
    public static class Logger
    {
        private static readonly string LogFilePath = "log.txt";

        public static void LogMessage(string message)
        {
            using (var writer = new StreamWriter(LogFilePath, true))
            {
                writer.WriteLine(message);
            }
        }

        public static void LogMethodCall(string methodName)
        {
            LogMessage($"Method {methodName} called at {DateTime.Now}.");
        }

        public static void LogMethodEnd(string methodName)
        {
            LogMessage($"Method {methodName} ended at {DateTime.Now}.");
        }
    }
}
