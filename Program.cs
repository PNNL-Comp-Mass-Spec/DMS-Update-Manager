using System;
using System.IO;
using System.Reflection;
using System.Threading;
using PRISM;

namespace DMSUpdateManager
{
    /// <summary>
    /// This program copies new and updated files from a source folder to a target folder
    /// </summary>
    /// <remarks>
    /// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
    /// Program started January 16, 2009
    /// --
    /// E-mail: matthew.monroe@pnnl.gov or matt@alchemistmatt.com
    /// Website: http://omics.pnl.gov/ or http://www.sysbio.org/resources/staff/ or http://panomics.pnnl.gov/
    /// </remarks>
    public static class Program
    {
        public const string PROGRAM_DATE = "November 8, 2017";

        // Either mSourceFolderPath and mTargetFolderPath must be specified, or mParameterFilePath needs to be specified
        // Option A
        private static string mSourceFolderPath;

        // Option A
        private static string mTargetFolderPath;

        // Option B
        private static string mParameterFilePath;

        private static bool mLogMessagesToFile;

        private static bool mPreviewMode;

        private static bool mNoMutex;
        private static double mWaitTimeoutMinutes;

        private static clsDMSUpdateManager mDMSUpdateManager;

        public static int Main(string[] args)
        {
            // Returns 0 if no error, error code if an error

            var intReturnCode = 0;
            clsParseCommandLine objParseCommandLine = new clsParseCommandLine();
            bool blnProceed = false;

            mSourceFolderPath = string.Empty;
            mTargetFolderPath = string.Empty;
            mParameterFilePath = string.Empty;

            mLogMessagesToFile = false;
            mPreviewMode = false;
            mNoMutex = false;
            mWaitTimeoutMinutes = 5;

            try
            {
                if (objParseCommandLine.ParseCommandLine())
                {
                    if (SetOptionsUsingCommandLineParameters(objParseCommandLine))
                        blnProceed = true;
                }

                if (!blnProceed || objParseCommandLine.NeedToShowHelp || objParseCommandLine.ParameterCount == 0 ||
                    !(mSourceFolderPath.Length > 0 & mTargetFolderPath.Length > 0 | mParameterFilePath.Length > 0))
                {
                    ShowProgramHelp();
                    intReturnCode = -1;
                }
                else
                {
                    mDMSUpdateManager = new clsDMSUpdateManager();

                    mDMSUpdateManager.PreviewMode = mPreviewMode;
                    mDMSUpdateManager.LogMessagesToFile = mLogMessagesToFile;

                    // Note: These options will get overridden if defined in the parameter file
                    mDMSUpdateManager.SourceFolderPath = mSourceFolderPath;
                    mDMSUpdateManager.DoNotUseMutex = mNoMutex;
                    mDMSUpdateManager.MutexWaitTimeoutMinutes = mWaitTimeoutMinutes;

                    if (mDMSUpdateManager.UpdateFolder(mTargetFolderPath, mParameterFilePath))
                    {
                        intReturnCode = 0;
                    }
                    else
                    {
                        intReturnCode = (int)mDMSUpdateManager.ErrorCode;
                        if (intReturnCode != 0)
                        {
                            Console.WriteLine("Error while processing: " + mDMSUpdateManager.GetErrorMessage());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error occurred in modMain->Main: " + Environment.NewLine + ex.Message);
                intReturnCode = -1;
            }

            return intReturnCode;
        }

        private static string GetAppVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version.ToString() + " (" + PROGRAM_DATE + ")";
        }

        private static bool SetOptionsUsingCommandLineParameters(clsParseCommandLine objParseCommandLine)
        {
            // Returns True if no problems; otherwise, returns false

            string strValue = string.Empty;
            var strValidParameters = new string[]
            {
                "S",
                "T",
                "P",
                "L",
                "V",
                "Preview",
                "NM",
                "WaitTimeout"
            };

            try
            {
                // Make sure no invalid parameters are present
                if (objParseCommandLine.InvalidParametersPresent(strValidParameters))
                {
                    return false;
                }
                else
                {
                    // Query objParseCommandLine to see if various parameters are present
                    if (objParseCommandLine.RetrieveValueForParameter("S", out strValue))
                        mSourceFolderPath = strValue;
                    if (objParseCommandLine.RetrieveValueForParameter("T", out strValue))
                        mTargetFolderPath = strValue;
                    if (objParseCommandLine.RetrieveValueForParameter("P", out strValue))
                        mParameterFilePath = strValue;
                    if (objParseCommandLine.IsParameterPresent("L"))
                        mLogMessagesToFile = true;
                    if (objParseCommandLine.IsParameterPresent("V"))
                        mPreviewMode = true;
                    if (objParseCommandLine.IsParameterPresent("Preview"))
                        mPreviewMode = true;
                    if (objParseCommandLine.IsParameterPresent("NM"))
                        mNoMutex = true;
                    if (objParseCommandLine.RetrieveValueForParameter("WaitTimeout", out strValue))
                    {
                        mWaitTimeoutMinutes = double.Parse(strValue);
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error parsing the command line parameters: " + Environment.NewLine + ex.Message);
            }

            return false;
        }

        private static void ShowErrorMessage(string strMessage)
        {
            var strSeparator = "------------------------------------------------------------------------------";

            Console.WriteLine();
            Console.WriteLine(strSeparator);
            Console.WriteLine(strMessage);
            Console.WriteLine(strSeparator);
            Console.WriteLine();
        }

        private static void ShowProgramHelp()
        {
            try
            {
                Console.WriteLine("This program copies new and updated files from a source folder to a target folder.");
                Console.WriteLine();
                Console.WriteLine("Program syntax:" + "\n" + Path.GetFileName(Assembly.GetExecutingAssembly().Location));
                Console.WriteLine(" [/S:SourceFolderPath [/T:TargetFolderPath]");
                Console.WriteLine(" [/P:ParameterFilePath] [/L] [/V] [/NM] [/WaitTimeout:minutes]");
                Console.WriteLine();
                Console.WriteLine("All files present in the source folder will be copied to the target folder if the file size or file modification time are different.");
                Console.WriteLine("You can either define the source and target folder at the command line, or using the parameter file.  All settings in the parameter file override command line settings.");
                Console.WriteLine();
                Console.WriteLine("Use /L to log details of the updated files.");
                Console.WriteLine("Use /V to preview the files that would be updated.");
                Console.WriteLine("Use /NM to not use a mutex, allowing multiple instances of this program to run simultaneously with the same parameter file.");
                Console.WriteLine("Use /WaitTimeout:minutes to specify how long the program should wait for another instance to finish before exiting.");
                Console.WriteLine();
                Console.WriteLine("These special flags affect how files are processed");
                Console.WriteLine("Append the flags to the source file name to use them");
                Console.WriteLine("  " + clsDMSUpdateManager.ROLLBACK_SUFFIX + " - Rolls back newer target files to match the source");
                Console.WriteLine("  " + clsDMSUpdateManager.DELETE_SUFFIX + " - Deletes the target file");
                Console.WriteLine();
                Console.WriteLine("These special flag files affect how folders are processed");
                Console.WriteLine("To use them, create an empty file with the given name in a source folder");
                Console.WriteLine("  " + clsDMSUpdateManager.PUSH_DIR_FLAG + " - Pushes the directory to the parent of the target folder");
                Console.WriteLine("  " + clsDMSUpdateManager.PUSH_AM_SUBDIR_FLAG + " - Pushes the directory to the target folder as a subfolder");
                Console.WriteLine("  " + clsDMSUpdateManager.DELETE_SUBDIR_FLAG + " - Deletes the directory from the parent of the target, but only if the directory is empty");
                Console.WriteLine("  " + clsDMSUpdateManager.DELETE_AM_SUBDIR_FLAG + " - Deletes the directory from below the target, but only if it is empty");
                Console.WriteLine();
                Console.WriteLine("Program written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2009");
                Console.WriteLine("Version: " + GetAppVersion());
                Console.WriteLine();

                Console.WriteLine("E-mail: matthew.monroe@pnnl.gov or matt@alchemistmatt.com");
                Console.WriteLine("Website: http://omics.pnl.gov/ or http://panomics.pnnl.gov/");
                Console.WriteLine();

                // Delay for 750 msec in case the user double clicked this file from within Windows Explorer (or started the program via a shortcut)
                Thread.Sleep(750);
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error displaying the program syntax: " + ex.Message);
            }
        }
    }
}
