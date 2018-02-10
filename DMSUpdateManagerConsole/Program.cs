﻿using System;
using System.Collections.Generic;
using System.IO;
using PRISM;
using PRISM.FileProcessor;

namespace DMSUpdateManager
{
    /// <summary>
    /// This program copies new and updated files from a source folder to a target folder
    /// </summary>
    /// <remarks>
    /// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
    /// Program started January 16, 2009
    /// --
    /// E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov
    /// Website: https://omics.pnl.gov/ or https://panomics.pnnl.gov/
    /// </remarks>
    public static class Program
    {
        public const string PROGRAM_DATE = "February 8, 2018";

        // Either mSourceFolderPath and mTargetFolderPath must be specified, or mParameterFilePath needs to be specified

        // Option A
        private static string mSourceFolderPath;

        // Option A
        private static string mTargetFolderPath;

        // Option B
        private static string mParameterFilePath;

        private static bool mForceUpdate;

        private static bool mLogMessagesToFile;

        private static bool mPreviewMode;

        private static bool mNoMutex;
        private static double mWaitTimeoutMinutes;

        public static int Main(string[] args)
        {
            // Returns 0 if no error, error code if an error

            int returnCode;
            var commandLineParser = new clsParseCommandLine();
            var proceed = false;

            mSourceFolderPath = string.Empty;
            mTargetFolderPath = string.Empty;
            mParameterFilePath = string.Empty;

            mForceUpdate = false;
            mLogMessagesToFile = false;
            mPreviewMode = false;
            mNoMutex = false;
            mWaitTimeoutMinutes = 5;

            try
            {
                if (commandLineParser.ParseCommandLine())
                {
                    if (SetOptionsUsingCommandLineParameters(commandLineParser))
                        proceed = true;
                }

                if (!proceed || commandLineParser.NeedToShowHelp || commandLineParser.ParameterCount == 0 ||
                    !(mSourceFolderPath.Length > 0 & mTargetFolderPath.Length > 0 | mParameterFilePath.Length > 0))
                {
                    ShowProgramHelp();
                    returnCode = -1;
                }
                else
                {
                    var dmsUpdateManager = new DMSUpdateManager
                    {
                        ForceUpdate = mForceUpdate,
                        LogMessagesToFile = mLogMessagesToFile,
                        PreviewMode = mPreviewMode,
                        SourceFolderPath = mSourceFolderPath,
                        DoNotUseMutex = mNoMutex,
                        MutexWaitTimeoutMinutes = mWaitTimeoutMinutes,
                        LoggingLevel = ProcessFilesOrFoldersBase.LogLevel.Normal,
                        ProgressOutputLevel = ProcessFilesOrFoldersBase.LogLevel.Suppress,
                        WriteToConsoleIfNoListener = false
                    };

                    RegisterEvents(dmsUpdateManager);

                    if (dmsUpdateManager.UpdateFolder(mTargetFolderPath, mParameterFilePath))
                    {
                        returnCode = 0;
                    }
                    else
                    {
                        returnCode = (int)dmsUpdateManager.ErrorCode;
                        if (returnCode != 0)
                        {
                            ConsoleMsgUtils.ShowError("Error while processing: " + dmsUpdateManager.GetErrorMessage());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error occurred in modMain->Main: " + Environment.NewLine + ex.Message, ex);
                returnCode = -1;
            }

            return returnCode;
        }

        private static bool SetOptionsUsingCommandLineParameters(clsParseCommandLine commandLineParser)
        {
            // Returns True if no problems; otherwise, returns false

            var validParameters = new List<string> { "S", "T", "P", "Force", "L", "V", "Preview", "NM", "WaitTimeout" };

            try
            {
                // Make sure no invalid parameters are present
                if (commandLineParser.InvalidParametersPresent(validParameters))
                {
                    return false;
                }

                // Query commandLineParser to see if various parameters are present
                if (commandLineParser.RetrieveValueForParameter("S", out var sourceFolder))
                    mSourceFolderPath = sourceFolder;

                if (commandLineParser.RetrieveValueForParameter("T", out var targetFolder))
                    mTargetFolderPath = targetFolder;

                if (commandLineParser.RetrieveValueForParameter("P", out var parameterFile))
                    mParameterFilePath = parameterFile;

                mForceUpdate = commandLineParser.IsParameterPresent("Force");

                mLogMessagesToFile = commandLineParser.IsParameterPresent("L");

                mPreviewMode = commandLineParser.IsParameterPresent("V");

                mPreviewMode = mPreviewMode || commandLineParser.IsParameterPresent("Preview");

                mNoMutex = commandLineParser.IsParameterPresent("NM");

                if (commandLineParser.RetrieveValueForParameter("WaitTimeout", out var timeoutMinutes))
                {
                    mWaitTimeoutMinutes = double.Parse(timeoutMinutes);
                }

                return true;
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error parsing the command line parameters: " + Environment.NewLine + ex.Message, ex);
            }

            return false;
        }

        private static void ShowErrorMessage(string message, Exception ex)
        {
            ConsoleMsgUtils.ShowError(message, ex);
        }

        private static void ShowProgramHelp()
        {
            try
            {
                Console.WriteLine("This program copies new and updated files from a source folder to a target folder");
                Console.WriteLine();
                Console.WriteLine("Program syntax:" + "\n" + Path.GetFileName(ProcessFilesOrFoldersBase.GetAppPath()));
                Console.WriteLine(" [/S:SourceFolderPath [/T:TargetFolderPath]");
                Console.WriteLine(" [/P:ParameterFilePath] [/Force] [/L] [/V]");
                Console.WriteLine(" [/NM] [/WaitTimeout:minutes]");
                Console.WriteLine();
                Console.WriteLine("All files present in the source folder will be copied to the target folder if the file size or file modification time are different");
                Console.WriteLine("You can either define the source and target folder at the command line, or using the parameter file.  All settings in the parameter file override command line settings.");
                Console.WriteLine();
                Console.WriteLine("Use /Force to force an update to run, even if the last update ran less than 30 seconds ago (or the time defined by MinimumRepeatTimeSeconds in the parameter file)");
                Console.WriteLine("Use /L to log details of the updated files");
                Console.WriteLine("Use /V to preview the files that would be updated");
                Console.WriteLine();
                Console.WriteLine("Use /NM to not use a mutex, allowing multiple instances of this program to run simultaneously with the same parameter file");
                Console.WriteLine("Use /WaitTimeout:minutes to specify how long the program should wait for another instance to finish before exiting");
                Console.WriteLine();
                Console.WriteLine("These special flags affect how files are processed");
                Console.WriteLine("Append the flags to the source file name to use them");
                Console.WriteLine("  " + DMSUpdateManager.ROLLBACK_SUFFIX + " - Rolls back newer target files to match the source");
                Console.WriteLine("  " + DMSUpdateManager.DELETE_SUFFIX + " - Deletes the target file");
                Console.WriteLine();
                Console.WriteLine("These special flag files affect how folders are processed");
                Console.WriteLine("To use them, create an empty file with the given name in a source folder");
                Console.WriteLine("  " + DMSUpdateManager.PUSH_DIR_FLAG + " - Pushes the directory to the parent of the target folder");
                Console.WriteLine("  " + DMSUpdateManager.PUSH_AM_SUBDIR_FLAG + " - Pushes the directory to the target folder as a subfolder");
                Console.WriteLine("  " + DMSUpdateManager.DELETE_SUBDIR_FLAG + " - Deletes the directory from the parent of the target, but only if the directory is empty");
                Console.WriteLine("  " + DMSUpdateManager.DELETE_AM_SUBDIR_FLAG + " - Deletes the directory from below the target, but only if it is empty");
                Console.WriteLine();
                Console.WriteLine("Program written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2009");
                Console.WriteLine("Version: " + PRISM.FileProcessor.ProcessFilesOrFoldersBase.GetAppVersion(PROGRAM_DATE));
                Console.WriteLine();

                Console.WriteLine("E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov");
                Console.WriteLine("Website: https://omics.pnl.gov/ or https://panomics.pnnl.gov/");
                Console.WriteLine();

                // Delay for 750 msec in case the user double clicked this file from within Windows Explorer (or started the program via a shortcut)
                clsProgRunner.SleepMilliseconds(750);
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error displaying the program syntax: " + ex.Message, ex);
            }
        }

        static void RegisterEvents(clsEventNotifier processor)
        {
            processor.DebugEvent += Processor_DebugEvent;
            processor.ErrorEvent += Processor_ErrorEvent;
            processor.StatusEvent += Processor_StatusEvent;
            processor.WarningEvent += Processor_WarningEvent;
        }

        static void Processor_DebugEvent(string message)
        {
            ConsoleMsgUtils.ShowDebug(message);
        }

        static void Processor_ErrorEvent(string message, Exception ex)
        {
            ShowErrorMessage(message, ex);
        }

        static void Processor_StatusEvent(string message)
        {
            Console.WriteLine(message);
        }

        static void Processor_WarningEvent(string message)
        {
            ConsoleMsgUtils.ShowWarning(message);
        }
    }
}