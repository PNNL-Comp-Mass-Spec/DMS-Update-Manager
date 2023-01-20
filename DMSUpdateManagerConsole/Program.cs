using System;
using System.Collections.Generic;
using System.IO;
using UpdateMgr = DMSUpdateManager;
using PRISM;
using PRISM.FileProcessor;
using PRISM.Logging;

namespace DMSUpdateManagerConsole
{
    /// <summary>
    /// This program copies new and updated files from a source directory to a target directory
    /// </summary>
    /// <remarks>
    /// <para>
    /// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
    /// Program started January 16, 2009
    /// </para>
    /// <para>
    /// E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov
    /// Website: https://github.com/PNNL-Comp-Mass-Spec/ or https://panomics.pnnl.gov/ or https://www.pnnl.gov/integrative-omics
    /// </para>
    /// </remarks>
    public static class Program
    {
        // Ignore Spelling: mutex, passphrase, Readme

        /// <summary>
        /// Program date
        /// </summary>
        public const string PROGRAM_DATE = "November 22, 2022";

        // Either mSourceDirectoryPath and mTargetDirectoryPath must be specified, or mParameterFilePath needs to be specified

        // Option A
        private static string mSourceDirectoryPath;

        // Option A
        private static string mTargetDirectoryPath;

        // Option B
        private static string mParameterFilePath;

        private static bool mForceUpdate;

        private static bool mLogMessagesToFile;

        private static bool mPreviewMode;

        private static bool mCopySubdirectoriesToParentDirectory;

        private static bool mNoMutex;
        private static double mWaitTimeoutMinutes;

        /// <summary>
        /// Password to encode or decode if command line switch /E or /D or /Encode or /Decode is present
        /// </summary>
        private static string mPassword;

        private static bool mEncodePassword;

        private static bool mDecodePassword;

        /// <summary>
        /// Main entry point
        /// </summary>
        /// <returns>0 if no error, error code if an error</returns>
        public static int Main()
        {
            var commandLineParser = new clsParseCommandLine();
            var proceed = false;

            mSourceDirectoryPath = string.Empty;
            mTargetDirectoryPath = string.Empty;
            mParameterFilePath = string.Empty;

            mForceUpdate = false;
            mLogMessagesToFile = false;
            mPreviewMode = false;

            mCopySubdirectoriesToParentDirectory = true;

            mNoMutex = false;
            mWaitTimeoutMinutes = 5;

            mPassword = string.Empty;
            mEncodePassword = false;
            mDecodePassword = false;

            try
            {
                if (commandLineParser.ParseCommandLine())
                {
                    if (SetOptionsUsingCommandLineParameters(commandLineParser))
                        proceed = true;
                }

                if (mEncodePassword || mDecodePassword)
                {
                    var success = ShowConvertedPassword();
                    return success ? 0 : -1;
                }

                var requiredParametersDefined = mSourceDirectoryPath.Length > 0 && mTargetDirectoryPath.Length > 0 || mParameterFilePath.Length > 0;

                if (!proceed ||
                    commandLineParser.NeedToShowHelp ||
                    commandLineParser.ParameterCount == 0 ||
                    !requiredParametersDefined)
                {
                    ShowProgramHelp();
                    return -1;
                }

                var updateManager = new UpdateMgr.DMSUpdateManager
                {
                    CopySubdirectoriesToParentDirectory = mCopySubdirectoriesToParentDirectory,
                    ForceUpdate = mForceUpdate,
                    LogMessagesToFile = mLogMessagesToFile,
                    PreviewMode = mPreviewMode,
                    SourceDirectoryPath = mSourceDirectoryPath,
                    DoNotUseMutex = mNoMutex,
                    MutexWaitTimeoutMinutes = mWaitTimeoutMinutes,
                    LoggingLevel = ProcessFilesOrDirectoriesBase.LogLevel.Normal,
                    ProgressOutputLevel = ProcessFilesOrDirectoriesBase.LogLevel.Suppress,
                    WriteToConsoleIfNoListener = false
                };

                RegisterEvents(updateManager);

                if (updateManager.UpdateDirectory(mTargetDirectoryPath, mParameterFilePath))
                {
                    return 0;
                }

                var returnCode = (int)updateManager.ErrorCode;
                if (returnCode != 0)
                {
                    ConsoleMsgUtils.ShowError("Error while processing: " + updateManager.GetErrorMessage());
                }

                return returnCode;
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error occurred in modMain->Main: " + Environment.NewLine + ex.Message, ex);
                return -1;
            }
        }

        private static string GetExeName()
        {
            return Path.GetFileName(AppUtils.GetAppPath());
        }

        /// <summary>
        /// Set command line options
        /// </summary>
        /// <param name="commandLineParser"></param>
        /// <returns>True if success, false if an error</returns>
        private static bool SetOptionsUsingCommandLineParameters(clsParseCommandLine commandLineParser)
        {
            var validParameters = new List<string> {
                "S", "T", "P", "Force", "L", "V",
                "Preview", "NoParent", "NoParents",
                "NM", "WaitTimeout",
                "E", "Encode", "D", "Decode"};

            try
            {
                // Make sure no invalid parameters are present
                if (commandLineParser.InvalidParametersPresent(validParameters))
                {
                    return false;
                }

                // Query commandLineParser to see if various parameters are present
                if (commandLineParser.RetrieveValueForParameter("S", out var sourceDirectory))
                    mSourceDirectoryPath = sourceDirectory;

                if (commandLineParser.RetrieveValueForParameter("T", out var targetDirectory))
                    mTargetDirectoryPath = targetDirectory;

                if (commandLineParser.RetrieveValueForParameter("P", out var parameterFile))
                    mParameterFilePath = parameterFile;

                mForceUpdate = commandLineParser.IsParameterPresent("Force");

                mLogMessagesToFile = commandLineParser.IsParameterPresent("L");

                mPreviewMode = commandLineParser.IsParameterPresent("V");

                mPreviewMode = mPreviewMode || commandLineParser.IsParameterPresent("Preview");

                if (commandLineParser.IsParameterPresent("NoParent") || commandLineParser.IsParameterPresent("NoParents"))
                    mCopySubdirectoriesToParentDirectory = false;

                mNoMutex = commandLineParser.IsParameterPresent("NM");

                if (commandLineParser.RetrieveValueForParameter("WaitTimeout", out var timeoutMinutes))
                {
                    mWaitTimeoutMinutes = double.Parse(timeoutMinutes);
                }

                if (commandLineParser.NonSwitchParameterCount > 0)
                {
                    mPassword = commandLineParser.RetrieveNonSwitchParameter(0);
                }

                mEncodePassword = commandLineParser.IsParameterPresent("Encode");
                mEncodePassword = mEncodePassword || commandLineParser.IsParameterPresent("E");

                mDecodePassword = commandLineParser.IsParameterPresent("Decode");
                mDecodePassword = mDecodePassword || commandLineParser.IsParameterPresent("D");

                return true;
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error parsing the command line parameters: " + Environment.NewLine + ex.Message, ex);
            }

            return false;
        }

        private static bool ShowConvertedPassword()
        {
            if (string.IsNullOrWhiteSpace(mPassword))
            {
                ShowWarning("Please provide the password to encode or decode, plus switch /Encode or /Decode");
                ShowProgramHelp(true);
                return false;
            }

            mPassword = mPassword.Replace("&quot;", "\"").Replace("&quote;", "\"");

            if (mEncodePassword)
                Console.WriteLine(mPassword + " encodes to " + AppUtils.EncodeShiftCipher(mPassword));
            else
                Console.WriteLine(mPassword + " decodes to " + AppUtils.DecodeShiftCipher(mPassword));

            return true;
        }

        private static void ShowErrorMessage(string message, Exception ex)
        {
            ConsoleMsgUtils.ShowError(message, ex);
        }

        private static void ShowProgramHelp(bool limitToPasswordOptions = false)
        {
            try
            {
                Console.WriteLine();
                Console.WriteLine("This program copies new and updated files from a source directory to a target directory");

                if (!limitToPasswordOptions)
                {
                    Console.WriteLine();
                    Console.WriteLine("Program syntax:");
                    Console.WriteLine(GetExeName());
                    Console.WriteLine(" [/S:SourceDirectoryPath [/T:TargetDirectoryPath]");
                    Console.WriteLine(" [/P:ParameterFilePath] [/Force] [/L] [/V]");
                    Console.WriteLine(" [/NoParent] [/NM] [/WaitTimeout:minutes]");
                    Console.WriteLine();

                    Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                                          "All files present in the source directory will be copied to the target directory " +
                                          "if the file size or file modification time are different. " +
                                          "You can either define the source and target directory at the command line, " +
                                          "or using the parameter file. All settings in the parameter file override command line settings."));
                    Console.WriteLine();
                    Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                                          "Use /Force to force an update to run, even if the last update ran less than 30 seconds ago " +
                                          "(or MinimumRepeatTimeSeconds defined in the parameter file)"));
                    Console.WriteLine("Use /L to log details of the updated files");
                    Console.WriteLine("Use /V to preview the files that would be updated");
                    Console.WriteLine();
                    Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                                          "Use /NoParent to only update the target directory, and not copy subdirectories of the source directory " +
                                          "into the subdirectories of the parent directory of the target directory"));
                    Console.WriteLine();
                    Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                                          "Use /NM to not use a mutex, allowing multiple instances of this program " +
                                          "to run simultaneously with the same parameter file"));
                    Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                                          "Use /WaitTimeout:minutes to specify how long the program " +
                                          "should wait for another instance to finish before exiting"));
                    Console.WriteLine();
                    Console.WriteLine("These special flags affect how files are processed");
                    Console.WriteLine("Append the flags to the source file name to use them");
                    Console.WriteLine("  " + UpdateMgr.DMSUpdateManager.ROLLBACK_SUFFIX + " - Rolls back newer target files to match the source");
                    Console.WriteLine("  " + UpdateMgr.DMSUpdateManager.DELETE_SUFFIX + " - Deletes the target file");
                    Console.WriteLine();
                    Console.WriteLine("These special flag files affect how directories are processed");
                    Console.WriteLine("To use them, create an empty file with the given name in a source directory");
                    Console.WriteLine("  " + UpdateMgr.DMSUpdateManager.PUSH_DIR_FLAG + " - Pushes the directory to the parent of the target directory");
                    Console.WriteLine("  " + UpdateMgr.DMSUpdateManager.PUSH_AM_SUBDIR_FLAG +
                                      " - Pushes the directory to the target directory as a subdirectory");
                    Console.WriteLine("  " + UpdateMgr.DMSUpdateManager.DELETE_SUBDIR_FLAG +
                                      " - Deletes the directory from the parent of the target, but only if the directory is empty");
                    Console.WriteLine("  " + UpdateMgr.DMSUpdateManager.DELETE_AM_SUBDIR_FLAG +
                                      " - Deletes the directory from below the target, but only if it is empty");
                    Console.WriteLine();

                    Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                                          "The DMS Update Manager also supports pushing new/updated files to a Linux server. " +
                                          "This requires an RSA private key file be on the computer running the DMS Update Manager " +
                                          "along with an RSA public key file on the target Linux server. For more info, see the Readme file."));
                }

                Console.WriteLine();
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                                      "When pushing files to a remote server, the parameter file must specify the path to a text file " +
                                      "with an encoded passphrase for decoding the RSA private key. " +
                                      "The following commands can be used to convert passwords:"));
                Console.WriteLine(GetExeName() + " PasswordToParse /Encode");
                Console.WriteLine(GetExeName() + " EncodedPassword /Decode");
                Console.WriteLine();
                Console.WriteLine("Program written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)");
                Console.WriteLine("Version: " + AppUtils.GetAppVersion(PROGRAM_DATE));
                Console.WriteLine();

                Console.WriteLine("E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov");
                Console.WriteLine("Website: https://github.com/PNNL-Comp-Mass-Spec/ or https://panomics.pnnl.gov/ or https://www.pnnl.gov/integrative-omics");
                Console.WriteLine();

                // Delay for 1 second in case the user double clicked this file from within Windows Explorer (or started the program via a shortcut)
                ConsoleMsgUtils.SleepSeconds(1);
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error displaying the program syntax: " + ex.Message, ex);
            }
        }

        private static void ShowWarning(string message)
        {
            ConsoleMsgUtils.ShowWarning(message);
        }

        private static void RegisterEvents(IEventNotifier processor)
        {
            processor.DebugEvent += Processor_DebugEvent;
            processor.ErrorEvent += Processor_ErrorEvent;
            processor.StatusEvent += Processor_StatusEvent;
            processor.WarningEvent += Processor_WarningEvent;
        }

        private static void Processor_DebugEvent(string message)
        {
            ConsoleMsgUtils.ShowDebug(message);
        }

        private static void Processor_ErrorEvent(string message, Exception ex)
        {
            ShowErrorMessage(message, ex);
        }

        private static void Processor_StatusEvent(string message)
        {
            Console.WriteLine(message);
        }

        private static void Processor_WarningEvent(string message)
        {
            ShowWarning(message);
        }
    }
}
