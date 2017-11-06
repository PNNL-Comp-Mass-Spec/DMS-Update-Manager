using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DMSUpdateManager
{
    public class clsProcessInfo
    {
        /// <summary>
        /// Process ID
        /// </summary>
        /// <returns></returns>
        public long ProcessID { get; }

        /// <summary>
        /// Full path to the .exe
        /// </summary>
        /// <returns></returns>
        public string ExePath { get; }

        /// <summary>
        /// Parent folder of the .exe
        /// </summary>
        /// <returns></returns>
        public string FolderPath { get; }

        /// <summary>
        /// Command line, including the .exe and any command line arguments
        /// </summary>
        /// <returns></returns>
        /// <remarks>May have absolute path or relative path to the Exe, depending on how the process was started</remarks>
        public string CommandLine { get; }

        /// <summary>
        /// Arguments portion of the command line
        /// </summary>
        /// <returns></returns>
        public string CommandLineArgs { get; }

        // ReSharper disable once CollectionNeverUpdated.Global
        /// <summary>
        /// FolderPath, split on path separators
        /// </summary>
        /// <returns></returns>
        public List<string> FolderHierarchy { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="lngProcessID">Process ID</param>
        /// <param name="strExePath">Executable path</param>
        /// <param name="strCommandLine">Command line</param>
        public clsProcessInfo(long lngProcessID, string strExePath, string strCommandLine)
        {
            ProcessID = lngProcessID;
            ExePath = strExePath;
            CommandLine = strCommandLine;

            FolderPath = Path.GetDirectoryName(strExePath);
            CommandLine = strCommandLine;
            FolderHierarchy = GetFolderHierarchy(FolderPath);

            var exeName = Path.GetFileName(ExePath);
            var exeIndex = CommandLine.IndexOf(exeName, StringComparison.Ordinal);

            if (exeIndex >= 0)
            {
                CommandLineArgs = CommandLine.Substring(exeIndex + exeName.Length);
            }
            else
            {
                CommandLineArgs = CommandLine;
            }
        }

        public static List<string> GetFolderHierarchy(string folderPath)
        {
            var folderPathHierarchy = folderPath.Split(Path.DirectorySeparatorChar).ToList();
            return folderPathHierarchy;
        }

        public override string ToString()
        {
            if (CommandLine.Contains(ExePath))
            {
                return CommandLine;
            }
            else
            {
                return ExePath;
            }
        }
    }
}
