using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DMSUpdateManager
{
    /// <summary>
    /// Track information about a process
    /// </summary>
    public class ProcessInfo
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
        /// Parent directory of the .exe
        /// </summary>
        /// <returns></returns>
        public string DirectoryPath { get; }

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

        /// <summary>
        /// DirectoryPath, split on path separators
        /// </summary>
        /// <returns></returns>
        public List<string> DirectoryHierarchy { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="processID">Process ID</param>
        /// <param name="exePath">Executable path</param>
        /// <param name="commandLine">Command line</param>
        public ProcessInfo(long processID, string exePath, string commandLine)
        {
            ProcessID = processID;
            ExePath = exePath;
            CommandLine = commandLine;

            DirectoryPath = Path.GetDirectoryName(exePath);
            DirectoryHierarchy = GetDirectoryHierarchy(DirectoryPath);

            var exeName = Path.GetFileName(ExePath);

            if (string.IsNullOrWhiteSpace(exeName))
            {
                CommandLineArgs = CommandLine;
                return;
            }

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

        /// <summary>
        /// Split the given path on the system directory separator character
        /// </summary>
        /// <param name="directoryPath"></param>
        /// <returns>List of directories</returns>
        public static List<string> GetDirectoryHierarchy(string directoryPath)
        {
            var directoryPathHierarchy = directoryPath.Split(Path.DirectorySeparatorChar).ToList();
            return directoryPathHierarchy;
        }

        /// <summary>
        /// Show the command line or the executable path
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            if (CommandLine.Contains(ExePath))
            {
                return CommandLine;
            }
            return ExePath;
        }
    }
}
