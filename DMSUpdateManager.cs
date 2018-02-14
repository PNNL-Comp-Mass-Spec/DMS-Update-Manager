using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading;
using PRISM;
using PRISM.FileProcessor;
using Renci.SshNet.Common;

namespace DMSUpdateManager
{
    /// <summary>
    /// This program copies new and updated files from a source directory (master file folder)
    /// to a target directory
    /// </summary>
    /// <remarks>
    /// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
    /// Program started January 16, 2009
    /// --
    /// E-mail: matthew.monroe@pnnl.gov or matt@alchemistmatt.com
    /// Website: http://omics.pnl.gov/ or http://panomics.pnnl.gov/")
    /// </remarks>
    public class DMSUpdateManager : ProcessFoldersBase
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public DMSUpdateManager()
        {
            mFileDate = "February 13, 2018";

            mFilesToIgnore = new SortedSet<string>(StringComparer.InvariantCultureIgnoreCase);
            mProcessesDict = new Dictionary<uint, ProcessInfo>();

            mExecutingExePath = GetAppPath();
            mExecutingExeName = Path.GetFileName(mExecutingExePath);

            InitializeLocalVariables();
        }

        #region "Constants and Enums"

        /// <summary>
        /// Error codes specialized for this class
        /// </summary>
        public enum eDMSUpdateManagerErrorCodes
        {
            /// <summary>
            /// No error
            /// </summary>
            NoError = 0,

            /// <summary>
            /// Unspecified error
            /// </summary>
            UnspecifiedError = -1
        }

        private enum eDateComparisonModeConstants
        {
            RetainNewerTargetIfDifferentSize = 0,
            OverwriteNewerTargetIfDifferentSize = 2,
            CopyIfSizeOrDateDiffers = 3
        }

        private enum eItemInUseConstants
        {
            NotInUse = 0,
            ItemInUse = 1,
            DirectoryInUse = 2
        }

        /// <summary>
        /// Rolls back newer target files to match the source
        /// </summary>
        public const string ROLLBACK_SUFFIX = ".rollback";

        /// <summary>
        /// Deletes the target file
        /// </summary>
        public const string DELETE_SUFFIX = ".delete";

        /// <summary>
        /// Check for a running .jar file; do not overwrite if in use
        /// </summary>
        public const string CHECK_JAVA_SUFFIX = ".checkjava";

        /// <summary>
        /// Pushes the directory to the parent of the target directory
        /// </summary>
        public const string PUSH_DIR_FLAG = "_PushDir_.txt";

        /// <summary>
        /// Pushes the directory to the target directory as a subdirectory
        /// </summary>
        public const string PUSH_AM_SUBDIR_FLAG = "_AMSubDir_.txt";

        /// <summary>
        /// Deletes the directory from the parent of the target, but only if the directory is empty
        /// </summary>
        public const string DELETE_SUBDIR_FLAG = "_DeleteSubDir_.txt";

        /// <summary>
        /// Deletes the directory from below the target, but only if it is empty
        /// </summary>
        public const string DELETE_AM_SUBDIR_FLAG = "_DeleteAMSubDir_.txt";

        #endregion

        #region "Classwide Variables"

        private bool mProcessesShown;

        private bool mRemoteHostInfoDefined;

        /// <summary>
        /// Source directory path
        /// </summary>
        private string mSourceFolderPath;

        /// <summary>
        /// Target directory path
        /// </summary>
        /// <remarks>Ignored if updating a remote host</remarks>
        private string mTargetFolderPath;

        /// <summary>
        /// List of files that will not be copied
        /// The names must be full filenames (no wildcards)
        /// </summary>
        private readonly SortedSet<string> mFilesToIgnore;

        private readonly string mExecutingExeName;
        private readonly string mExecutingExePath;

        // Store the results of the WMI query getting running processes with command line data
        // Keys are Process ID
        // Values are clsProcessInfo
        private readonly Dictionary<uint, ProcessInfo> mProcessesDict;

        // Keys are process ID
        // Values are the full command line for the process
        private Dictionary<uint, string> mProcessesMatchingTarget;

        private string mLastFolderProcessesChecked;
        private string mLastFolderRunningProcessPath;
        private uint mLastFolderRunningProcessId;

        private string mTargetFolderPathBase;

        /// <summary>
        /// Minimum time between updates
        /// </summary>
        /// <remarks>Ignored if ForceUpdate is true</remarks>
        private int mMinimumRepeatThresholdSeconds;

        private string mMutexNameSuffix;

        #endregion

        #region "Properties"

        /// <summary>
        /// When mCopySubdirectoriesToParentFolder=True, will copy any subdirectories of the source directory
        /// into a subdirectory off the parent directory the target directory
        /// </summary>
        /// <remarks>
        /// For example:
        ///   The .Exe resides in directory C:\DMS_Programs\AnalysisToolManager\DMSUpdateManager.exe
        ///   mSourceFolderPath = "\\gigasax\DMS_Programs\AnalysisToolManagerDistribution"
        ///   mTargetFolderPath = "."
        ///   Files are synced from "\\gigasax\DMS_Programs\AnalysisToolManagerDistribution" to "C:\DMS_Programs\AnalysisToolManager\"
        ///   Next, directory \\gigasax\DMS_Programs\AnalysisToolManagerDistribution\MASIC\ will get sync'd with ..\MASIC (but only if ..\MASIC exists)
        ///     Note that ..\MASIC is actually C:\DMS_Programs\MASIC\
        ///   When sync'ing the MASIC directories, will recursively sync additional directories that match
        ///   If the source directory contains file _PushDir_.txt or _AMSubDir_.txt then the directory will be copied to the target even if it doesn't exist there
        /// </remarks>
        public bool CopySubdirectoriesToParentFolder { get; set; }

        /// <summary>
        /// Mutex wait timeout (minutes)
        /// </summary>
        public double MutexWaitTimeoutMinutes { get; set; }

        /// <summary>
        /// When true, use a Mutex to assure that multiple copies of the update manager are not started simulatenously
        /// </summary>
        public bool DoNotUseMutex { get; set; }

        /// <summary>
        /// When true, force the update to occur, even if the previous update occurred less than 30 seconds ago
        /// (or the time specified by mMinimumRepeatThresholdSeconds)
        /// </summary>
        public bool ForceUpdate { get; set; }

        /// <summary>
        /// Local error code
        /// </summary>
        public eDMSUpdateManagerErrorCodes LocalErrorCode { get; private set; }

        /// <summary>
        /// If False, then will not overwrite files in the target directory that are newer than files in the source directory
        /// </summary>
        public bool OverwriteNewerFiles { get; set; }

        /// <summary>
        /// When true, then messages will be displayed and logged showing the files that would be copied
        /// </summary>
        public bool PreviewMode { get; set; }

        /// <summary>
        /// Connection info for uploading files to a remote linux host
        /// </summary>
        public RemoteHostConnectionInfo RemoteHostInfo { get; set; }

        /// <summary>
        /// Source directory path
        /// </summary>
        public string SourceFolderPath
        {
            get
            {
                if (mSourceFolderPath == null)
                {
                    return string.Empty;
                }

                return mSourceFolderPath;
            }
            set
            {
                if (value != null)
                {
                    mSourceFolderPath = value;
                }
            }
        }

        #endregion

        /// <summary>
        /// Shorten folderPath if it starts with mTargetFolderPathBase
        /// </summary>
        /// <param name="fileOrFolderPath"></param>
        /// <returns></returns>
        private string AbbreviatePath(string fileOrFolderPath)
        {
            return AbbreviatePath(fileOrFolderPath, mTargetFolderPathBase);
        }

        /// <summary>
        /// Shorten folderPath if it starts with folderPathBase
        /// </summary>
        /// <param name="fileOrFolderPath"></param>
        /// <param name="folderPathBase"></param>
        /// <returns></returns>
        private string AbbreviatePath(string fileOrFolderPath, string folderPathBase)
        {
            if (fileOrFolderPath.StartsWith(folderPathBase))
            {
                if (fileOrFolderPath.Length > folderPathBase.Length)
                {
                    return fileOrFolderPath.Substring(folderPathBase.Length + 1);
                }
                return ".";
            }

            return fileOrFolderPath;
        }

        /// <summary>
        /// Add a file to ignore from processing
        /// </summary>
        /// <param name="fileName">Full filename (no wildcards)</param>
        public void AddFileToIgnore(string fileName)
        {
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                if (!mFilesToIgnore.Contains(fileName))
                {
                    mFilesToIgnore.Add(fileName);
                }
            }
        }

        private string CombinePaths(DirectoryContainer targetFolderInfo, string parentFolder, string folderToAppend)
        {

            if (targetFolderInfo.TrackingRemoteHostDirectory)
                return parentFolder + '/' + folderToAppend;

            return Path.Combine(parentFolder, folderToAppend);

        }

        private string ConstructMutexName(string mutexSuffix)
        {
            var appName = GetEntryOrExecutingAssembly().GetName().Name;
            var parameterFileCleaned = mutexSuffix.Replace("\\", "_").Replace(":", "_").Replace(".", "_").Replace(" ", "_");
            var mutexName = @"Global\" + appName.ToLower() + "_" + parameterFileCleaned.ToLower();
            return mutexName;
        }

        /// <summary>
        /// Copy the file (or preview the copy)
        /// </summary>
        /// <param name="sourceFile">Source file</param>
        /// <param name="targetFolderInfo">Target directory info</param>
        /// <param name="targetFile">Target file</param>
        /// <param name="fileUpdateCount">Total number of files updated (input/output)</param>
        /// <param name="copyReason">Reason for the copy</param>
        private void CopyFile(
            FileInfo sourceFile,
            DirectoryContainer targetFolderInfo,
            FileOrDirectoryInfo targetFile,
            ref int fileUpdateCount, string copyReason)
        {
            string existingFileInfo;

            if (targetFile.Exists)
            {
                existingFileInfo = "Old: " + GetFileDateAndSize(targetFile);
            }
            else
            {
                existingFileInfo = string.Empty;
            }

            var updatedFileInfo = "New: " + GetFileDateAndSize(new FileOrDirectoryInfo(sourceFile));

            if (PreviewMode)
            {
                ShowOldAndNewFileInfo("Preview: Update file: ", targetFile, existingFileInfo, updatedFileInfo, copyReason, true);
            }
            else
            {
                ShowOldAndNewFileInfo("Update file: ", targetFile, existingFileInfo, updatedFileInfo, copyReason, true);

                try
                {
                    var copiedFile = targetFolderInfo.CopyFile(sourceFile, targetFile.FullName);

                    if (copiedFile.Length != sourceFile.Length)
                    {
                        ShowErrorMessage("Copy of " + sourceFile.Name + " failed; sizes differ");
                    }
                    else if (copiedFile.LastWriteTimeUtc != sourceFile.LastWriteTimeUtc)
                    {
                        ShowErrorMessage("Copy of " + sourceFile.Name + " failed; modification times differ");
                    }
                    else
                    {
                        fileUpdateCount += 1;
                    }
                }
                catch (Exception ex)
                {
                    ShowErrorMessage("Error copying " + sourceFile.Name + ": " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Compare the source file to the target file and update it if they differ
        /// </summary>
        /// <param name="sourceFile">Source file</param>
        /// <param name="targetFolderInfo">Target directory info</param>
        /// <param name="targetFolderPath">Target directory path</param>
        /// <param name="fileUpdateCount">Number of files that have been updated (Input/output)</param>
        /// <param name="eDateComparisonMode">Date comparison mode</param>
        /// <param name="itemInUse">Used to track when a file or directory is in use by another process (log a message if the source and target files differ)</param>
        /// <param name="fileUsageMessage">Message to log when the file (or directory) is in use and the source and targets differ</param>
        /// <returns>True if the file was updated, otherwise false</returns>
        /// <remarks></remarks>
        private bool CopyFileIfNeeded(
            FileInfo sourceFile,
            DirectoryContainer targetFolderInfo,
            string targetFolderPath,
            ref int fileUpdateCount,
            eDateComparisonModeConstants eDateComparisonMode,
            eItemInUseConstants itemInUse = eItemInUseConstants.NotInUse,
            string fileUsageMessage = "")
        {

            var targetFilePath = CombinePaths(targetFolderInfo, targetFolderPath, sourceFile.Name);
            var targetFile = targetFolderInfo.GetFileInfo(targetFilePath);

            var copyReason = string.Empty;
            var needToCopy = false;

            if (!targetFile.Exists)
            {
                // File not present in the target; copy it now
                copyReason = "not found in target directory";
                needToCopy = true;
            }
            else
            {
                // File is present, see if the file has a different size
                if (eDateComparisonMode == eDateComparisonModeConstants.CopyIfSizeOrDateDiffers)
                {
                    if (targetFile.Length != sourceFile.Length)
                    {
                        needToCopy = true;
                        copyReason = "sizes are different";
                    }
                    else if (sourceFile.LastWriteTimeUtc != targetFile.LastWriteTimeUtc)
                    {
                        needToCopy = true;
                        copyReason = "dates are different";
                    }
                }
                else
                {
                    if (targetFile.Length != sourceFile.Length)
                    {
                        needToCopy = true;
                        copyReason = "sizes are different";
                    }
                    else if (sourceFile.LastWriteTimeUtc > targetFile.LastWriteTimeUtc)
                    {
                        needToCopy = true;
                        copyReason = "source file is newer";
                    }

                    if (needToCopy && eDateComparisonMode == eDateComparisonModeConstants.RetainNewerTargetIfDifferentSize)
                    {
                        if (targetFile.LastWriteTimeUtc > sourceFile.LastWriteTimeUtc)
                        {
                            // Target file is newer than the source; do not overwrite

                            var strWarning = "Warning: Skipping file " + targetFile.FullName + " since a newer version exists in the target; " +
                                             "source=" + sourceFile.LastWriteTimeUtc.ToLocalTime() + ", target=" + targetFile.LastWriteTimeUtc.ToLocalTime();

                            ShowWarning(strWarning, 24);
                            needToCopy = false;
                        }
                    }
                }
            }

            if (!needToCopy)
                return false;

            if (targetFile.Exists)
            {
                if (string.Equals(targetFile.FullName, mExecutingExePath))
                {
                    ShowMessage("Skipping " + targetFile.FullName + "; cannot update the currently running copy of the DMSUpdateManager");
                    return false;
                }

                if (itemInUse != eItemInUseConstants.NotInUse)
                {
                    if (targetFile.Name == mExecutingExeName)
                    {
                        // Update DMSUpdateManager.exe if it is not in the same directory as the starting directory
                        if (!string.Equals(targetFile.DirectoryName, mOutputFolderPath))
                        {
                            itemInUse = eItemInUseConstants.NotInUse;
                        }
                    }
                }

                if (itemInUse != eItemInUseConstants.NotInUse)
                {
                    // Do not update this file; it is in use (or another file in this directory is in use)
                    if (string.IsNullOrWhiteSpace(fileUsageMessage))
                    {
                        if (itemInUse == eItemInUseConstants.DirectoryInUse)
                        {
                            ShowMessage("Skipping " + sourceFile.Name + " because directory " +
                                        AbbreviatePath(targetFile.DirectoryName) + " is in use (by an unknown process)");
                        }
                        else
                        {
                            ShowMessage("Skipping " + sourceFile.Name + " in directory " +
                                        AbbreviatePath(targetFile.DirectoryName) + " because currently in use (by an unknown process)");
                        }
                    }
                    else
                    {
                        ShowMessage(fileUsageMessage);
                    }

                    return false;
                }
            }

            CopyFile(sourceFile, targetFolderInfo,  targetFile, ref fileUpdateCount, copyReason);
            return true;
        }

        private FileOrDirectoryInfo CreateDirectoryIfMissing(DirectoryContainer targetFolderInfo, string directoryPath)
        {

            var currentTask = "validating";

            try
            {
                // Make sure the target directory exists
                var targetDirectory = targetFolderInfo.GetDirectoryInfo(directoryPath);

                if (targetDirectory.Exists)
                    return targetDirectory;

                currentTask = "creating";
                var newDirectory = targetFolderInfo.CreateDirectoryIfMissing(directoryPath);
                return newDirectory;
            }
            catch (Exception ex)
            {
                ShowErrorMessage(string.Format("Error {0} directory {1}: {2}", currentTask, directoryPath, ex.Message));
                ConsoleMsgUtils.ShowWarning(clsStackTraceFormatter.GetExceptionStackTraceMultiLine(ex));

                var missingDirectory = new FileOrDirectoryInfo(
                    directoryPath,
                    exists: false,
                    lastWrite: DateTime.MinValue,
                    lastWriteUtc: DateTime.MinValue,
                    linuxDirectory: targetFolderInfo.TrackingRemoteHostDirectory);

                return missingDirectory;

            }
        }
       
        private void InitializeLocalVariables()
        {
            ReThrowEvents = false;
            mLogFileUsesDateStamp = true;

            ForceUpdate = false;
            PreviewMode = false;
            OverwriteNewerFiles = false;
            CopySubdirectoriesToParentFolder = false;

            MutexWaitTimeoutMinutes = 5;
            DoNotUseMutex = false;
            mMutexNameSuffix = string.Empty;

            mMinimumRepeatThresholdSeconds = 30;

            mSourceFolderPath = string.Empty;
            mTargetFolderPath = string.Empty;

            RemoteHostInfo = new RemoteHostConnectionInfo();
            mRemoteHostInfoDefined = false;

            mFilesToIgnore.Clear();
            mFilesToIgnore.Add(PUSH_DIR_FLAG);
            mFilesToIgnore.Add(PUSH_AM_SUBDIR_FLAG);
            mFilesToIgnore.Add(DELETE_SUBDIR_FLAG);
            mFilesToIgnore.Add(DELETE_AM_SUBDIR_FLAG);

            LocalErrorCode = eDMSUpdateManagerErrorCodes.NoError;

            // This list tracks processes that appear to be using a directory, but for which we likely can still update files in that directory
            var executablesToIgnore = new SortedSet<string>(StringComparer.OrdinalIgnoreCase) {
                "cmd.exe", "BC2.exe", "BCompare.exe", "BComp.com", "BComp.exe",
                "EditPadLite7.exe", "EditPadPro7.exe", "notepad.exe", "notepad++.exe"
            };

            var runningExeName = mExecutingExeName ?? "UnknownApp.exe";

            executablesToIgnore.Add(Path.ChangeExtension(runningExeName, "vshost.exe"));

            mProcessesDict.Clear();
            var results = new ManagementObjectSearcher("SELECT ProcessId, ExecutablePath, CommandLine FROM Win32_Process");

            foreach (var item in results.Get())
            {
                var processId = (uint)item["ProcessId"];
                var processPath = (string)item["ExecutablePath"];
                var cmd = (string)item["CommandLine"];

                // Only store the processes that have non-empty command lines and are not referring to the current executable
                if (string.IsNullOrWhiteSpace(cmd))
                {
                    continue;
                }

                // If the ExecutablePath is null/empty, replace it with the CommandLine string
                if (string.IsNullOrWhiteSpace(processPath))
                {
                    processPath = cmd;
                }

                // Skip this process if it is unlikely to be locking the target file
                if (processPath.Contains(runningExeName) || executablesToIgnore.Contains(Path.GetFileName(processPath)))
                {
                    continue;
                }

                var newProcess = new ProcessInfo(processId, processPath, cmd);
                if (newProcess.FolderHierarchy.Count < 3)
                {
                    // Process running in the root directory or one below the root directory; ignore it
                    continue;
                }
                mProcessesDict.Add(processId, newProcess);
            }

            mProcessesMatchingTarget = new Dictionary<uint, string>();

            // Ignore checking for running processes in the first directory that we are updating
            mLastFolderProcessesChecked = Path.GetDirectoryName(mExecutingExePath);
            mLastFolderRunningProcessPath = mExecutingExePath;
            mLastFolderRunningProcessId = 0;
        }

        /// <summary>
        /// Get the current error message
        /// </summary>
        /// <returns></returns>
        public override string GetErrorMessage()
        {
            // Returns an empty string if no error

            string strErrorMessage;

            if (ErrorCode == eProcessFoldersErrorCodes.LocalizedError | ErrorCode == eProcessFoldersErrorCodes.NoError)
            {
                switch (LocalErrorCode)
                {
                    case eDMSUpdateManagerErrorCodes.NoError:
                        strErrorMessage = string.Empty;
                        break;
                    case eDMSUpdateManagerErrorCodes.UnspecifiedError:
                        strErrorMessage = "Unspecified localized error";
                        break;
                    default:
                        // This shouldn't happen
                        strErrorMessage = "Unknown error state";
                        break;
                }
            }
            else
            {
                strErrorMessage = GetBaseClassErrorMessage();
            }

            return strErrorMessage;
        }

        private static string GetFileDateAndSize(FileOrDirectoryInfo fileInfo)
        {
            return fileInfo.LastWriteTimeUtc.ToString("yyyy-MM-dd hh:mm:ss tt") + " and " + fileInfo.Length + " bytes";
        }

        private bool GetMutex(string mutexName, bool updatingTargetFolder, out Mutex mutex, out bool doNotUpdateParent)
        {
            doNotUpdateParent = false;

            try
            {
                mutex = new Mutex(false, mutexName);

                bool hasMutexHandle;
                try
                {
                    hasMutexHandle = mutex.WaitOne(0, false);
                    if (!hasMutexHandle)
                    {
                        if (updatingTargetFolder)
                            ConsoleMsgUtils.ShowWarning("Other instance already running on target directory, waiting for finish before continuing...");
                        else
                            ConsoleMsgUtils.ShowWarning("Other instance already running, waiting for finish before continuing...");

                        ConsoleMsgUtils.ShowDebug("Mutex: " + mutexName);

                        // Mutex is held by another application; do another wait for it to be released, with a timeout of 5 minutes
                        hasMutexHandle = mutex.WaitOne(TimeSpan.FromMinutes(MutexWaitTimeoutMinutes), false);

                        Console.WriteLine();
                    }
                }
                catch (AbandonedMutexException)
                {
                    ConsoleMsgUtils.ShowWarning("Detected abandoned mutex, picking it up...");
                    hasMutexHandle = true;
                }

                if (!hasMutexHandle)
                {
                    // If we don't have the mutex handle, don't try to update the parent directory
                    doNotUpdateParent = true;
                }

                return hasMutexHandle;
            }
            catch (UnauthorizedAccessException ex)
            {
                // Access to the path 'Global\dmsupdatemanager_c__dms_programs' is denied.
                OnWarningEvent("Error accessing the mutex: " + ex.Message);
                mutex = null;
                doNotUpdateParent = true;
                return false;
            }
            catch (Exception ex)
            {
                OnWarningEvent("Error creating/monitoring the mutex: " + ex.Message);
                doNotUpdateParent = true;
                mutex = null;
                return false;
            }

        }

        private bool LoadParameterFileSettings(string parameterFilePath)
        {
            const string OPTIONS_SECTION = "DMSUpdateManager";

            var settingsFile = new XmlSettingsFileAccessor();

            try
            {
                if (string.IsNullOrEmpty(parameterFilePath))
                {
                    // No parameter file specified; nothing to load
                    return true;
                }

                if (!File.Exists(parameterFilePath))
                {
                    // See if parameterFilePath points to a file in the same directory as the application
                    var exeFolder = Path.GetDirectoryName(mExecutingExePath);
                    if (exeFolder == null)
                        parameterFilePath = Path.GetFileName(parameterFilePath);
                    else
                        parameterFilePath = Path.Combine(exeFolder, Path.GetFileName(parameterFilePath));

                    if (!File.Exists(parameterFilePath))
                    {
                        OnErrorEvent("Parameter file not found: " + parameterFilePath);
                        SetBaseClassErrorCode(eProcessFoldersErrorCodes.ParameterFileNotFound);
                        return false;
                    }
                }

                if (settingsFile.LoadSettings(parameterFilePath))
                {
                    if (!settingsFile.SectionPresent(OPTIONS_SECTION))
                    {
                        ShowErrorMessage("The node '<section name=\"" + OPTIONS_SECTION + "\"> was not found in the parameter file: " + parameterFilePath);
                        SetBaseClassErrorCode(eProcessFoldersErrorCodes.InvalidParameterFile);
                        return false;
                    }

                    if (settingsFile.GetParam(OPTIONS_SECTION, "LogMessages", false))
                    {
                        LogMessagesToFile = true;
                    }

                    OverwriteNewerFiles = settingsFile.GetParam(OPTIONS_SECTION, "OverwriteNewerFiles", OverwriteNewerFiles);
                    CopySubdirectoriesToParentFolder = settingsFile.GetParam(OPTIONS_SECTION, "CopySubdirectoriesToParentFolder", CopySubdirectoriesToParentFolder);

                    mSourceFolderPath = settingsFile.GetParam(OPTIONS_SECTION, "SourceFolderPath", mSourceFolderPath);
                    mTargetFolderPath = settingsFile.GetParam(OPTIONS_SECTION, "TargetFolderPath", mTargetFolderPath);

                    RemoteHostInfo.HostName = settingsFile.GetParam(OPTIONS_SECTION, "RemoteHostName", RemoteHostInfo.HostName);
                    RemoteHostInfo.Username = settingsFile.GetParam(OPTIONS_SECTION, "RemoteHostUserName", RemoteHostInfo.Username);
                    RemoteHostInfo.PrivateKeyFile = settingsFile.GetParam(OPTIONS_SECTION, "PrivateKeyFilePath", RemoteHostInfo.PrivateKeyFile);
                    RemoteHostInfo.PassphraseFile = settingsFile.GetParam(OPTIONS_SECTION, "PassphraseFilePath", RemoteHostInfo.PassphraseFile);

                    if (!string.IsNullOrWhiteSpace(RemoteHostInfo.HostName) ||
                        !string.IsNullOrWhiteSpace(RemoteHostInfo.HostName))
                    {
                        RemoteHostInfo.DestinationPath = mTargetFolderPath;
                        mRemoteHostInfoDefined = true;
                    }

                    mMutexNameSuffix = settingsFile.GetParam(OPTIONS_SECTION, "MutexNameSuffix", string.Empty);

                    var logFolderPath = settingsFile.GetParam(OPTIONS_SECTION, "LogFolderPath", "Logs");
                    mMinimumRepeatThresholdSeconds = settingsFile.GetParam(OPTIONS_SECTION, "MinimumRepeatTimeSeconds", 30);
                    var logLevel = settingsFile.GetParam(OPTIONS_SECTION, "LoggingLevel", string.Empty);

                    if (Enum.TryParse(logLevel, false, out LogLevel level))
                    {
                        LoggingLevel = level;
                    }

                    if (!string.IsNullOrWhiteSpace(logFolderPath))
                    {
                        CloseLogFileNow();
                        LogFolderPath = logFolderPath;
                        LogFilePath = string.Empty;
                        ConfigureLogFilePath();
                        ConsoleMsgUtils.ShowDebug("Logging to " + LogFilePath);
                        Console.WriteLine();
                        ArchiveOldLogFilesNow();
                    }

                    var filesToIgnore = settingsFile.GetParam(OPTIONS_SECTION, "FilesToIgnore", string.Empty);
                    try
                    {
                        if (filesToIgnore.Length > 0)
                        {
                            var ignoreList = filesToIgnore.Split(',');

                            foreach (var strFile in ignoreList)
                            {
                                AddFileToIgnore(strFile.Trim());
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Ignore errors here
                    }
                }
            }
            catch (Exception ex)
            {
                HandleException("Error in LoadParameterFileSettings", ex);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Update files in directory inputFolderPath
        /// </summary>
        /// <param name="inputFolderPath">Target directory to update</param>
        /// <param name="outputFolderAlternatePath">Ignored by this method</param>
        /// <param name="parameterFilePath">Parameter file defining the source directory path and other options</param>
        /// <param name="resetErrorCode">Ignored by this method</param>
        /// <returns>True if success, False if failure</returns>
        /// <remarks>If TargetFolder is defined in the parameter file, inputFolderPath will be ignored</remarks>
        public override bool ProcessFolder(string inputFolderPath, string outputFolderAlternatePath, string parameterFilePath, bool resetErrorCode)
        {
            return UpdateFolder(inputFolderPath, parameterFilePath);
        }

        private void ShowOldAndNewFileInfo(
            string messagePrefix,
            FileOrDirectoryInfo targetFile,
            string existingFileInfo,
            string updatedFileInfo,
            string copyReason,
            bool logToFile)
        {
            var spacePad = new string(' ', messagePrefix.Length);

            ShowMessage(messagePrefix + targetFile.FullName + "; " + copyReason, logToFile);

            if (targetFile.Exists)
            {
                ShowMessage(spacePad + existingFileInfo);
            }
            ShowMessage(spacePad + updatedFileInfo);
        }

        /// <summary>
        /// Update files in directory targetFolderPath
        /// </summary>
        /// <param name="targetFolderPath">Target directory to update</param>
        /// <param name="parameterFilePath">Parameter file defining the source directory path and other options</param>
        /// <returns>True if success, False if failure</returns>
        /// <remarks>If TargetFolder is defined in the parameter file, targetFolderPath will be ignored</remarks>
        public bool UpdateFolder(string targetFolderPath, string parameterFilePath)
        {
            SetLocalErrorCode(eDMSUpdateManagerErrorCodes.NoError);

            if (!string.IsNullOrEmpty(targetFolderPath))
            {
                // Update mTargetFolderPath using targetFolderPath
                // Note: If TargetFolder is defined in the parameter file, this value will get overridden
                mTargetFolderPath = string.Copy(targetFolderPath);
            }

            if (!LoadParameterFileSettings(parameterFilePath))
            {
                ShowErrorMessage("Parameter file load error: " + parameterFilePath);

                if (ErrorCode == eProcessFoldersErrorCodes.NoError)
                {
                    SetBaseClassErrorCode(eProcessFoldersErrorCodes.InvalidParameterFile);
                }
                return false;
            }

            try
            {
                targetFolderPath = string.Copy(mTargetFolderPath);

                if (string.IsNullOrEmpty(mSourceFolderPath))
                {
                    ShowWarning("Source directory path is not defined; either specify it at the command line or include it in the parameter file");
                    SetBaseClassErrorCode(eProcessFoldersErrorCodes.InvalidInputFolderPath);
                    return false;
                }

                if (string.IsNullOrWhiteSpace(targetFolderPath))
                {
                    ShowWarning("Target directory path is not defined; either specify it at the command line or include it in the parameter file");
                    SetBaseClassErrorCode(eProcessFoldersErrorCodes.InvalidInputFolderPath);
                    return false;
                }

                // Note that CleanupFilePaths() will update mOutputFolderPath, which is used by LogMessage()
                // Since we're updating files on the local computer, use the target directory path for parameter inputFolderPath of CleanupFolderPaths
                var tempStr = string.Empty;
                if (!CleanupFolderPaths(ref targetFolderPath, ref tempStr))
                {
                    SetBaseClassErrorCode(eProcessFoldersErrorCodes.FilePathError);
                    return false;
                }

                var targetFolderInfo = new DirectoryContainer(targetFolderPath);

                if (DoNotUseMutex)
                {
                    return UpdateFolderRun(targetFolderInfo, parameterFilePath);
                }

                return UpdateFolderMutexWrapped(targetFolderInfo, parameterFilePath);
            }
            catch (Exception ex)
            {
                HandleException("Error in UpdateFolder", ex);
                return false;
            }
        }

        /// <summary>
        /// Update files on a remote Linux host
        /// </summary>
        /// <param name="targetHostInfo"></param>
        /// <param name="parameterFilePath"></param>
        /// <returns></returns>
        public bool UpdateRemoteHost(RemoteHostConnectionInfo targetHostInfo, string parameterFilePath)
        {
            SetLocalErrorCode(eDMSUpdateManagerErrorCodes.NoError);

            if (!LoadParameterFileSettings(parameterFilePath))
            {
                ShowErrorMessage("Parameter file load error: " + parameterFilePath);

                if (ErrorCode == eProcessFoldersErrorCodes.NoError)
                {
                    SetBaseClassErrorCode(eProcessFoldersErrorCodes.InvalidParameterFile);
                }
                return false;
            }

            try
            {
                if (string.IsNullOrEmpty(mSourceFolderPath))
                {
                    ShowWarning("Source directory path is not defined; either specify it at the command line or include it in the parameter file");
                    SetBaseClassErrorCode(eProcessFoldersErrorCodes.InvalidInputFolderPath);
                    return false;
                }

                var validOptions = targetHostInfo.Validate(out var errorMessage);
                if (!validOptions)
                {
                    ShowWarning(errorMessage + " in targetHostInfo");
                    SetBaseClassErrorCode(eProcessFoldersErrorCodes.InvalidInputFolderPath);
                    return false;
                }

                // Note that CleanupFilePaths() will update mOutputFolderPath, which is used by LogMessage()
                // Since we're updating a files on a remote host, use the entry assembly's path for parameter inputFolderPath of CleanupFolderPaths
                var appFolderPath = GetAppFolderPath();
                var tempStr = string.Empty;
                if (!CleanupFolderPaths(ref appFolderPath, ref tempStr))
                {
                    SetBaseClassErrorCode(eProcessFoldersErrorCodes.FilePathError);
                    return false;
                }

                var targetFolderInfo = new DirectoryContainer(targetHostInfo);

                if (DoNotUseMutex)
                {
                    return UpdateFolderRun(targetFolderInfo, parameterFilePath);
                }

                return UpdateFolderMutexWrapped(targetFolderInfo, parameterFilePath);
            }
            catch (Exception ex)
            {
                HandleException("Error in UpdateFolder", ex);
                return false;
            }
        }

        /// <summary>
        /// Check a global mutex keyed on the parameter file path; if it returns false, exit
        /// </summary>
        /// <param name="targetFolderInfo"></param>
        /// <param name="parameterFilePath"></param>
        /// <returns></returns>
        private bool UpdateFolderMutexWrapped(DirectoryContainer targetFolderInfo, string parameterFilePath)
        {
            Mutex mutex = null;
            var hasMutexHandle = false;

            try
            {
                var doNotUpdateParent = false;
                if (!string.IsNullOrWhiteSpace(parameterFilePath))
                {
                    string mutexName;
                    if (string.IsNullOrWhiteSpace(mMutexNameSuffix))
                        mutexName = ConstructMutexName(parameterFilePath);
                    else
                        mutexName = ConstructMutexName(mMutexNameSuffix);

                    hasMutexHandle = GetMutex(mutexName, false, out mutex, out doNotUpdateParent);
                }

                return UpdateFolderRun(targetFolderInfo, parameterFilePath, doNotUpdateParent);
            }
            finally
            {
                if (hasMutexHandle)
                {
                    mutex.ReleaseMutex();
                }

                mutex?.Dispose();
            }
        }

        /// <summary>
        /// Check a global mutex keyed on the parameter file path; if it returns false, exit
        /// </summary>
        /// <param name="diSourceFolder"></param>
        /// <param name="targetFolderInfo"></param>
        /// <param name="parameterFilePath"></param>
        /// <returns></returns>
        private bool UpdateFolderCopyToParentMutexWrapped(
            DirectoryInfo diSourceFolder,
            DirectoryContainer targetFolderInfo,
            string parameterFilePath)
        {
            Mutex mutex = null;
            var hasMutexHandle = false;

            try
            {
                var doNotUpdateParent = false;
                var targetFolderParent = targetFolderInfo.ParentPath;
                if (!string.IsNullOrWhiteSpace(targetFolderParent))
                {
                    var mutexName = ConstructMutexName(targetFolderParent);

                    hasMutexHandle = GetMutex(mutexName, true, out mutex, out doNotUpdateParent);
                }

                if (!doNotUpdateParent)
                {
                    return UpdateFolderCopyToParentRun(diSourceFolder, targetFolderInfo, parameterFilePath);
                }
                return true;
            }
            finally
            {
                if (hasMutexHandle)
                {
                    mutex.ReleaseMutex();
                }
                mutex?.Dispose();
            }
        }

        private bool UpdateFolderRun(DirectoryContainer targetFolderInfo, string parameterFilePath, bool doNotUpdateParent = false)
        {
            var sourceFolder = new DirectoryInfo(mSourceFolderPath);

            if (sourceFolder.Parent == null)
            {
                OnErrorEvent("Unable to determine the parent directory of the source directory: " + sourceFolder.FullName);
                return false;
            }

            if (targetFolderInfo.TrackingRemoteHostDirectory)
            {
                mTargetFolderPathBase = targetFolderInfo.ParentPath;
            }
            else
            {

                var targetFolder = new DirectoryInfo(targetFolderInfo.DirectoryPath);
                if (targetFolder.Parent == null)
                {
                    OnErrorEvent("Unable to determine the parent directory of the target directory: " + targetFolder.FullName);
                    return false;
                }

                mTargetFolderPathBase = targetFolder.Parent.FullName;
            }

            ResetProgress();

            bool success;

            if (targetFolderInfo.TrackingRemoteHostDirectory)
                success = UpdateFolderWork(sourceFolder.FullName, targetFolderInfo, targetFolderInfo.RemoteHostInfo.DestinationPath, pushNewSubfolders: false);
            else
                success = UpdateFolderWork(sourceFolder.FullName, targetFolderInfo, targetFolderInfo.DirectoryPath, pushNewSubfolders: false);

            if (!CopySubdirectoriesToParentFolder || doNotUpdateParent)
                return success;

            if (DoNotUseMutex)
            {
                success = UpdateFolderCopyToParentRun(sourceFolder, targetFolderInfo, parameterFilePath);
            }
            else
            {
                success = UpdateFolderCopyToParentMutexWrapped(sourceFolder, targetFolderInfo, parameterFilePath);
            }

            return success;
        }

        private bool UpdateFolderCopyToParentRun(DirectoryInfo sourceFolder, DirectoryContainer targetFolderInfo, string parameterFilePath)
        {
            var success = true;
            var skipShared = false;
            var checkFilePath = string.Empty;

            // Check the repeat time threshold; must be checked before any writes to the log; make sure anything added above only logs on error
            if (!string.IsNullOrWhiteSpace(LogFilePath))
            {
                var logFileInfo = new FileInfo(LogFilePath);
                var logFileFolder = logFileInfo.DirectoryName;

                if (logFileFolder == null)
                {
                    OnErrorEvent("Unable to determine the parent directory of the log file: " + logFileInfo.FullName);
                    return false;
                }

                checkFilePath = Path.Combine(logFileFolder, Path.GetFileNameWithoutExtension(parameterFilePath) + "_parentCheck.txt");
                var checkFileInfo = new FileInfo(checkFilePath);
                if (checkFileInfo.Exists)
                {
                    var lastUpdate = checkFileInfo.LastWriteTimeUtc;
                    var nextAllowedUpdate = lastUpdate.AddSeconds(mMinimumRepeatThresholdSeconds);

                    var currentTime = DateTime.UtcNow;

                    if (currentTime < nextAllowedUpdate)
                    {
                        var lastUpdateSeconds = currentTime.Subtract(lastUpdate).TotalSeconds;

                        if (ForceUpdate)
                        {
                            OnStatusEvent(
                                string.Format("Last update ran {0:N0} seconds ago; forcing update due to /Force swich", lastUpdateSeconds));
                            Console.WriteLine();
                        }
                        else
                        {

                            // Reduce hits on the source: not enough time has passed since the last update
                            // Delay the output so that important log messages about bad parameters will be output regardless of this
                            OnWarningEvent(
                                string.Format("Exiting update since last update ran {0:N0} seconds ago; update is allowed in {1:N0} seconds",
                                              lastUpdateSeconds,
                                              nextAllowedUpdate.Subtract(currentTime).TotalSeconds));

                            skipShared = true;
                        }
                    }
                }
            }

            if (!skipShared)
            {
                // Update the check file's date
                TouchCheckFile(checkFilePath);

                success = UpdateFolderCopyToParent(sourceFolder, targetFolderInfo);

                // Update the check file's date one more itme
                TouchCheckFile(checkFilePath);
            }

            return success;
        }

        /// <summary>
        /// Update the last write time on the log file
        /// </summary>
        private void TouchCheckFile(string checkFilePath)
        {
            if (!string.IsNullOrWhiteSpace(checkFilePath))
            {
                if (!File.Exists(checkFilePath))
                {
                    // Create an empty file; since a FileStream is returned, also call Dispose().
                    File.Create(checkFilePath).Dispose();
                }
                File.SetLastWriteTimeUtc(checkFilePath, DateTime.UtcNow);
            }
        }

        private bool UpdateFolderCopyToParent(DirectoryInfo sourceFolder, DirectoryContainer targetFolderInfo)
        {
            var successOverall = true;

            var parentFolder = targetFolderInfo.ParentPath;
            if (parentFolder == null)
            {
                OnWarningEvent("Unable to determine the parent directory of " + targetFolderInfo.DirectoryPath);
                return false;
            }

            foreach (var sourceSubFolder in sourceFolder.GetDirectories())
            {

                // The target directory is treated as a subdirectory of the parent directory
                var targetSubFolderPath = CombinePaths(targetFolderInfo, parentFolder, sourceSubFolder.Name);

                // Initially assume we'll process this directory if it exists at the target
                var targetSubFolder = targetFolderInfo.GetDirectoryInfo(targetSubFolderPath);

                var processSubfolder = targetSubFolder.Exists;

                if (processSubfolder && sourceSubFolder.GetFiles(DELETE_SUBDIR_FLAG).Length > 0)
                {
                    // Remove this subfolder (but only if it's empty)
                    var folderDeleted = DeleteSubFolder(sourceSubFolder, targetFolderInfo, targetSubFolder, "parent subfolder", DELETE_SUBDIR_FLAG);
                    if (folderDeleted)
                        processSubfolder = false;
                }

                if (sourceSubFolder.GetFiles(DELETE_AM_SUBDIR_FLAG).Length > 0)
                {
                    // Remove this subfolder (but only if it's empty)
                    var analysisMgrSubDirPath = CombinePaths(targetFolderInfo, targetFolderInfo.DirectoryPath, sourceSubFolder.Name);
                    var analysisMgrSubDir = targetFolderInfo.GetDirectoryInfo(analysisMgrSubDirPath);

                    var folderDeleted = DeleteSubFolder(sourceSubFolder, targetFolderInfo, analysisMgrSubDir, "subfolder", DELETE_AM_SUBDIR_FLAG);
                    if (folderDeleted)
                        processSubfolder = false;
                }

                if (sourceSubFolder.GetFiles(PUSH_AM_SUBDIR_FLAG).Length > 0)
                {
                    // Push this directory as a subdirectory of the target directory, not as a subdirectory of the parent directory
                    targetSubFolderPath = CombinePaths(targetFolderInfo, targetFolderInfo.DirectoryPath, sourceSubFolder.Name);
                    processSubfolder = true;
                }
                else
                {
                    if (sourceSubFolder.GetFiles(PUSH_DIR_FLAG).Length > 0)
                    {
                        processSubfolder = true;
                    }
                }

                if (processSubfolder)
                {
                    var success = UpdateFolderWork(sourceSubFolder.FullName, targetFolderInfo, targetSubFolderPath, pushNewSubfolders: true);
                    if (!success)
                        successOverall = false;
                }
            }

            return successOverall;
        }

        private bool DeleteSubFolder(
            FileSystemInfo sourceSubFolder,
            DirectoryContainer targetFolderInfo,
            FileOrDirectoryInfo targetSubFolder,
            string folderDescription,
            string deleteFlag)
        {
            if (string.IsNullOrWhiteSpace(folderDescription))
            {
                folderDescription = "directory";
            }

            if (!targetSubFolder.Exists)
                return false;

            var fileCount = targetFolderInfo.GetFiles(targetSubFolder).Count;
            var folders = targetFolderInfo.GetDirectories(targetSubFolder);

            if (fileCount > 0)
            {
                ShowWarning(
                    "Folder flagged for deletion, but it is not empty (File Count  = " + fileCount + "): " +
                    AbbreviatePath(targetSubFolder.FullName));
                return false;
            }

            if (folders.Count > 0)
            {
                // Check each sub directory for file _DeleteSubDir_.txt
                foreach (var folder in folders)
                {
                    var newSourceSubDir = new DirectoryInfo(Path.Combine(sourceSubFolder.FullName, folder.Name));
                    var deleteSubDirFile = new FileInfo(Path.Combine(newSourceSubDir.FullName, deleteFlag));

                    if (deleteSubDirFile.Exists)
                    {
                        // Recursively call this method
                        DeleteSubFolder(newSourceSubDir, targetFolderInfo, folder, folderDescription, deleteFlag);
                    }
                }

                // Refresh the subdirectories
                var folderCount = targetFolderInfo.GetDirectories(targetSubFolder, true).Count;

                if (folderCount > 0)
                {
                    ShowWarning(
                        "Directory flagged for deletion, but it is not empty (Directory Count = " + folderCount + "): " +
                        AbbreviatePath(targetSubFolder.FullName));
                    return false;
                }
            }

            try
            {
                if (PreviewMode)
                {
                    ShowMessage("Preview " + folderDescription + " delete: " + targetSubFolder.FullName);
                }
                else
                {
                    targetFolderInfo.DeleteFileOrDirectory(targetSubFolder);
                    ShowMessage("Deleted " + folderDescription + " " + targetSubFolder.FullName);
                }

                return true;
            }
            catch (Exception ex)
            {
                HandleException("Error removing empty " + folderDescription + " flagged with " + deleteFlag + " at " + targetSubFolder.FullName, ex);
                return false;
            }

        }

        private bool UpdateFolderWork(string sourceFolderPath, DirectoryContainer targetFolderInfo, string targetFolderPath, bool pushNewSubfolders)
        {

            ShowMessage("Updating " + AbbreviatePath(targetFolderPath), false, eMessageType: eMessageTypeConstants.Debug);

            var targetFolder = CreateDirectoryIfMissing(targetFolderInfo, targetFolderPath);
            if (!targetFolder.Exists)
                return false;

            // Obtain a list of files in the source directory
            var sourceFolder = new DirectoryInfo(sourceFolderPath);

            var fileUpdateCount = 0;

            var filesInSource = sourceFolder.GetFiles();

            // Populate a SortedSet with the names of any .delete files in fiFilesInSource
            var deleteFiles = new SortedSet<string>(StringComparer.InvariantCultureIgnoreCase);
            var filesToDelete = (from sourceFile in filesInSource
                                 where sourceFile.Name.EndsWith(DELETE_SUFFIX, StringComparison.InvariantCultureIgnoreCase)
                                 select TrimSuffix(sourceFile.Name, DELETE_SUFFIX).ToLower());
            foreach (var item in filesToDelete)
            {
                deleteFiles.Add(item);
            }

            // Populate a SortedSet with the names of any .checkjava files in fiFilesInSource
            var checkJavaFiles = new SortedSet<string>(StringComparer.InvariantCultureIgnoreCase);
            var javaFilesToCheck = (from sourceFile in filesInSource
                                    where sourceFile.Name.EndsWith(CHECK_JAVA_SUFFIX, StringComparison.InvariantCultureIgnoreCase)
                                    select TrimSuffix(sourceFile.Name, CHECK_JAVA_SUFFIX).ToLower());
            foreach (var item in javaFilesToCheck)
            {
                checkJavaFiles.Add(item);
            }

            foreach (var sourceFile in filesInSource)
            {
                var retryCount = 2;
                var errorLogged = false;

                while (retryCount >= 0)
                {
                    try
                    {
                        var fileNameLCase = sourceFile.Name.ToLower();

                        // Make sure this file is not in mFilesToIgnore
                        // Note that mFilesToIgnore contains several flag files:
                        //   PUSH_DIR_FLAG, PUSH_AM_SUBDIR_FLAG,
                        //   DELETE_SUBDIR_FLAG, DELETE_AM_SUBDIR_FLAG
                        var skipFile = mFilesToIgnore.Contains(fileNameLCase);

                        if (skipFile)
                        {
                            break; // Break out of the while, continue the for loop
                        }

                        var itemInUse = eItemInUseConstants.NotInUse;
                        string fileUsageMessage;

                        // See if file ends with one of the special suffix flags
                        if (sourceFile.Name.EndsWith(ROLLBACK_SUFFIX, StringComparison.InvariantCultureIgnoreCase))
                        {
                            // This is a Rollback file
                            // Do not copy this file
                            // However, do look for a corresponding file that does not have .rollback and copy it if the target file has a different date or size

                            var targetFileName = TrimSuffix(fileNameLCase, ROLLBACK_SUFFIX);
                            if (checkJavaFiles.Contains(targetFileName))
                            {
                                if (JarFileInUseByJava(sourceFile, out fileUsageMessage))
                                {
                                    itemInUse = eItemInUseConstants.ItemInUse;
                                }
                            }
                            else
                            {
                                if (!targetFolderInfo.TrackingRemoteHostDirectory &&
                                    TargetFolderInUseByProcess(targetFolder.FullName, targetFileName, out fileUsageMessage))
                                {
                                    // The directory is in use
                                    // Allow new files to be copied, but do not overwrite existing files
                                    itemInUse = eItemInUseConstants.DirectoryInUse;
                                }
                                else
                                {
                                    fileUsageMessage = string.Empty;
                                }
                            }

                            ProcessRollbackFile(sourceFile, targetFolderInfo, targetFolderPath, ref fileUpdateCount, itemInUse, fileUsageMessage);
                            break; // Break out of the while, continue the for loop
                        }

                        if (sourceFile.Name.EndsWith(DELETE_SUFFIX, StringComparison.InvariantCultureIgnoreCase))
                        {
                            // This is a Delete file
                            // Do not copy this file
                            // However, do look for a corresponding file that does not have .delete and delete that file in the target directory

                            ProcessDeleteFile(sourceFile, targetFolderInfo, targetFolderPath);
                            break; // Break out of the while, continue the for loop
                        }

                        if (sourceFile.Name.EndsWith(CHECK_JAVA_SUFFIX, StringComparison.InvariantCultureIgnoreCase))
                        {
                            // This is a .checkjava file
                            // Do not copy this file
                            break; // Break out of the while, continue the for loop
                        }

                        // Make sure this file does not match a corresponding .delete file
                        if (deleteFiles.Contains(fileNameLCase))
                        {
                            break; // Break out of the while, continue the for loop
                        }

                        eDateComparisonModeConstants eDateComparisonMode;

                        if (OverwriteNewerFiles)
                        {
                            eDateComparisonMode = eDateComparisonModeConstants.OverwriteNewerTargetIfDifferentSize;
                        }
                        else
                        {
                            eDateComparisonMode = eDateComparisonModeConstants.RetainNewerTargetIfDifferentSize;
                        }

                        if (!targetFolderInfo.TrackingRemoteHostDirectory &&
                            checkJavaFiles.Contains(fileNameLCase))
                        {
                            if (JarFileInUseByJava(sourceFile, out fileUsageMessage))
                            {
                                itemInUse = eItemInUseConstants.ItemInUse;
                            }
                        }
                        else
                        {
                            if (!targetFolderInfo.TrackingRemoteHostDirectory &&
                                TargetFolderInUseByProcess(targetFolder.FullName, sourceFile.Name, out fileUsageMessage))
                            {
                                // The directory is in use
                                // Allow new files to be copied, but do not overwrite existing files
                                itemInUse = eItemInUseConstants.DirectoryInUse;
                            }
                            else
                            {
                                fileUsageMessage = string.Empty;
                            }
                        }

                        CopyFileIfNeeded(sourceFile, targetFolderInfo, targetFolderPath, ref fileUpdateCount, eDateComparisonMode, itemInUse, fileUsageMessage);

                        // File processed; move on to the next file
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (!errorLogged)
                        {
                            ShowErrorMessage("Error synchronizing " + sourceFile.Name + ": " + ex.Message);
                            errorLogged = true;
                        }

                        retryCount -= 1;
                        clsProgRunner.SleepMilliseconds(100);
                    }
                }
            }

            if (fileUpdateCount > 0)
            {
                // Example message
                // Updated 1 file using \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\DMSUpdateManager\
                var statusMessage = "Updated " + fileUpdateCount + " file";
                if (fileUpdateCount > 1)
                    statusMessage += "s";

                statusMessage += " using " + sourceFolder.FullName + "\\";

                ShowMessage(statusMessage);
            }

            // Process each subdirectory in the source directory
            // If the directory exists at the target, copy it
            // Additionally, if the source directory contains file _PushDir_.txt, it gets copied even if it doesn't exist at the target
            foreach (var sourceSubFolder in sourceFolder.GetDirectories())
            {
                FileOrDirectoryInfo targetSubFolder;

                if (targetFolderInfo.TrackingRemoteHostDirectory)
                {
                    var targetSubFolderPath = targetFolderPath + '/' + sourceSubFolder.Name;
                    targetSubFolder = targetFolderInfo.GetDirectoryInfo(targetSubFolderPath);
                }
                else
                {
                    var targetSubFolderPath = Path.Combine(targetFolder.FullName, sourceSubFolder.Name);

                    var folderInfo = new DirectoryInfo(targetSubFolderPath);

                    targetSubFolder = new FileOrDirectoryInfo(
                        folderInfo.FullName,
                        folderInfo.Exists,
                        lastWrite: folderInfo.LastWriteTime,
                        lastWriteUtc: folderInfo.LastWriteTimeUtc,
                        linuxDirectory: false);
                }

                // Initially assume we'll process this directory if it exists at the target
                var processSubfolder = targetSubFolder.Exists;
                if (processSubfolder && sourceSubFolder.GetFiles(DELETE_SUBDIR_FLAG).Length > 0)
                {
                    // Remove this subfolder (but only if it's empty)
                    var folderDeleted = DeleteSubFolder(sourceSubFolder, targetFolderInfo, targetSubFolder, "subfolder", DELETE_SUBDIR_FLAG);
                    if (folderDeleted)
                        processSubfolder = false;
                }

                if (pushNewSubfolders && sourceSubFolder.GetFiles(PUSH_DIR_FLAG).Length > 0)
                {
                    processSubfolder = true;
                }

                if (processSubfolder)
                {
                    UpdateFolderWork(sourceSubFolder.FullName, targetFolderInfo, targetSubFolder.FullName, pushNewSubfolders);
                }
            }

            return true;
        }

        private bool JarFileInUseByJava(FileSystemInfo sourceFile, out string jarFileUsageMessage)
        {
            const bool INCLUDE_PROGRAM_PATH = false;
            jarFileUsageMessage = string.Empty;

            try
            {
                var processes = Process.GetProcesses().ToList();
                processes.Sort(new ProcessNameComparer());

                if (PreviewMode & !mProcessesShown)
                {
                    Console.WriteLine();
                    ShowMessage("Examining running processes for Java", false);
                }

                var lastProcess = string.Empty;

                foreach (var process in processes)
                {
                    if (PreviewMode & !mProcessesShown)
                    {
                        if (process.ProcessName != lastProcess)
                        {
                            Console.WriteLine(process.ProcessName);
                        }
                        lastProcess = process.ProcessName;
                    }

                    if (!process.ProcessName.StartsWith("java", StringComparison.InvariantCultureIgnoreCase))
                    {
                        continue;
                    }

                    try
                    {
                        var commandLine = GetCommandLine(process, INCLUDE_PROGRAM_PATH);

                        if (PreviewMode & !mProcessesShown)
                        {
                            Console.WriteLine("  " + commandLine);
                        }

                        if (commandLine.ToLower().Contains(sourceFile.Name.ToLower()))
                        {
                            jarFileUsageMessage = "Skipping " + sourceFile.Name + " because currently in use by Java";
                            return true;
                        }

                        if (string.IsNullOrWhiteSpace(commandLine))
                        {
                            jarFileUsageMessage = "Skipping " + sourceFile.Name + " because empty Java command line (permissions issue?)";
                            return true;
                        }

                        // Uncomment to debug:
                        // ShowMessage("Command line for java process ID " & oProcess.Id & ": " & commandLine)
                    }
                    catch (Exception ex)
                    {
                        // Skip the process; possibly permission denied

                        jarFileUsageMessage = "Skipping " + sourceFile.Name + " because exception: " + ex.Message;
                        return true;
                    }
                }

                if (PreviewMode & !mProcessesShown)
                {
                    Console.WriteLine();
                    mProcessesShown = true;
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error looking for Java using " + sourceFile.Name + ": " + ex.Message);
            }

            return false;
        }

        private string GetCommandLine(Process process, bool includeProgramPath)
        {
            var commandLine = new StringBuilder();

            if (includeProgramPath)
            {
                commandLine.Append(process.MainModule.FileName);
                commandLine.Append(" ");
            }

            var result = new ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE ProcessId = " + process.Id);

            foreach (var item in result.Get())
            {
                commandLine.Append(item["CommandLine"]);
            }

            return commandLine.ToString();
        }

        private bool TargetFolderInUseByProcess(string targetFolderPath, string targetFileName, out string folderUsageMessage)
        {
            folderUsageMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(targetFolderPath))
            {
                OnWarningEvent("Empty target directory path passed to TargetFolderInUseByProcess");
                return false;
            }

            try
            {
                var processCount = GetNumTargetFolderProcesses(targetFolderPath, out var firstProcessPath, out var firstProcessId);

                if (processCount <= 0)
                {
                    return false;
                }

                // Example log messages:
                // Skipping UIMFLibrary.dll because directory DeconTools is in use by process DeconConsole.exe (PID 343243)
                // Skipping DeconConsole.exe because directory DeconTools is in use by 2 processes on this system, including DeconConsole.exe (PID 343243)

                folderUsageMessage = "Skipping " + targetFileName + " because directory " + AbbreviatePath(targetFolderPath) + " is in use by ";

                string processPathToShow;

                if (string.IsNullOrWhiteSpace(firstProcessPath))
                {
                    processPathToShow = "an unknown process";
                }
                else
                {
                    var processFile = new FileInfo(firstProcessPath);
                    var processIdAppend = " (PID " + firstProcessId + ")";

                    if (processFile.DirectoryName == null)
                    {
                        OnWarningEvent("Unable to determine the parent directory of " + processFile.FullName);
                        return false;
                    }

                    if (processFile.DirectoryName == targetFolderPath)
                    {
                        processPathToShow = Path.GetFileName(firstProcessPath) + processIdAppend;
                    }
                    else if (targetFolderPath.StartsWith(processFile.DirectoryName))
                    {
                        if (processFile.Directory?.Parent == null)
                        {
                            OnWarningEvent("Unable to determine the parent directory of " + processFile.FullName);
                            return false;
                        }

                        var relativePath = processFile.Directory.Parent.FullName;
                        string pathPart;
                        if (processFile.DirectoryName.Length > relativePath.Length)
                        {
                            pathPart = processFile.DirectoryName.Substring(relativePath.Length + 1);
                        }
                        else
                        {
                            pathPart = processFile.DirectoryName;
                        }

                        processPathToShow = Path.Combine(pathPart, Path.GetFileName(firstProcessPath)) + processIdAppend;
                    }
                    else
                    {
                        processPathToShow = firstProcessPath + processIdAppend;
                    }
                }

                if (processCount == 1)
                {
                    folderUsageMessage += "process " + processPathToShow;
                }
                else
                {
                    folderUsageMessage += processCount + " processes on this system, including " + processPathToShow;
                }

                return true;
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error looking for processes using files in " + targetFolderPath + ": " + ex.Message);
                return false;
            }

        }

        /// <summary>
        /// Determine the number of processes using files in the given directory
        /// </summary>
        /// <param name="targetFolderPath">Directory to examine</param>
        /// <param name="firstProcessPath">Output parameter: first process using files in this directory; empty string if no processes</param>
        /// <param name="firstProcessId">Output parameter: Process ID of first process using files in this directory</param>
        /// <returns>Count of processes using this directory</returns>
        private int GetNumTargetFolderProcesses(string targetFolderPath, out string firstProcessPath, out uint firstProcessId)
        {
            firstProcessPath = string.Empty;
            firstProcessId = 0;

            // Filter the queried results for each call to this method

            var targetFolderPathHierarchy = ProcessInfo.GetFolderHierarchy(targetFolderPath);

            if (string.Equals(targetFolderPath, mLastFolderProcessesChecked, StringComparison.InvariantCultureIgnoreCase))
            {
                firstProcessPath = mLastFolderRunningProcessPath;
                firstProcessId = mLastFolderRunningProcessId;
                return mProcessesMatchingTarget.Count;
            }

            mProcessesMatchingTarget.Clear();
            mLastFolderProcessesChecked = targetFolderPath;
            mLastFolderRunningProcessPath = string.Empty;
            mLastFolderRunningProcessId = 0;

            // Look for running processes with a .exe in the target directory (or in a parent of the target directory)
            // Ignore cmd.exe
            foreach (var item in mProcessesDict)
            {
                var exeFolderHierarchy = item.Value.FolderHierarchy;

                if (exeFolderHierarchy.Count > targetFolderPathHierarchy.Count)
                {
                    continue;
                }

                var treesMatch = true;
                for (var index = 0; index <= exeFolderHierarchy.Count - 1; index++)
                {
                    if (targetFolderPathHierarchy[index] != exeFolderHierarchy[index])
                    {
                        treesMatch = false;
                        break;
                    }
                }

                if (!treesMatch)
                    continue;

                mProcessesMatchingTarget.Add(item.Key, item.Value.CommandLine);
                if (string.IsNullOrWhiteSpace(firstProcessPath))
                {
                    firstProcessPath = item.Value.ExePath;
                    firstProcessId = item.Key;
                    if (string.IsNullOrWhiteSpace(firstProcessPath))
                    {
                        firstProcessPath = item.Value.CommandLine;
                    }
                }
            }

            // Next examine the command line arguments of running processes
            // This can help check for .jar files that don't have a .checkjava flag file
            foreach (var item in mProcessesDict)
            {
                if (item.Value.CommandLineArgs.IndexOf(targetFolderPath, StringComparison.InvariantCultureIgnoreCase) < 0)
                {
                    continue;
                }

                if (mProcessesMatchingTarget.ContainsKey(item.Key))
                    continue;

                mProcessesMatchingTarget.Add(item.Key, item.Value.CommandLine);
                if (string.IsNullOrWhiteSpace(firstProcessPath))
                {
                    firstProcessPath = item.Value.ExePath;
                    firstProcessId = item.Key;
                    if (string.IsNullOrWhiteSpace(firstProcessPath))
                    {
                        firstProcessPath = item.Value.CommandLine;
                    }
                }
            }

            if (mProcessesMatchingTarget.Count > 0)
            {
                mLastFolderRunningProcessPath = firstProcessPath;
                mLastFolderRunningProcessId = firstProcessId;
            }

            return mProcessesMatchingTarget.Count;
        }

        private void ProcessDeleteFile(FileSystemInfo deleteFile, DirectoryContainer targetFolderInfo, string targetFolderPath)
        {
            var targetFilePath = CombinePaths(targetFolderInfo, targetFolderPath, TrimSuffix(deleteFile.Name, DELETE_SUFFIX));

            var targetFile = targetFolderInfo.GetFileInfo(targetFilePath);

           if (targetFile.Exists)
            {
                if (PreviewMode)
                {
                    ShowMessage("Preview delete: " + targetFile.FullName);
                }
                else
                {
                    targetFolderInfo.DeleteFileOrDirectory(targetFile);
                    ShowMessage("Deleted file " + targetFile.FullName);
                }
            }

            // Make sure the .delete is also not in the target directory
            var targetDeleteFilePath = Path.Combine(targetFolderPath, deleteFile.Name);
            var targetDeleteFile = targetFolderInfo.GetFileInfo(targetDeleteFilePath);

            if (targetDeleteFile.Exists)
            {
                if (PreviewMode)
                {
                    ShowMessage("Preview delete: " + targetDeleteFile.FullName);
                }
                else
                {
                    targetFolderInfo.DeleteFileOrDirectory(targetDeleteFile);
                    ShowMessage("Deleted file " + targetDeleteFile.FullName);
                }
            }
        }

        /// <summary>
        /// Rollback the target file if it differs from the source
        /// </summary>
        /// <param name="rollbackFile">Rollback file path</param>
        /// <param name="targetFolderInfo">Target directory info</param>
        /// <param name="targetFolderPath">Target directory path</param>
        /// <param name="fileUpdateCount">Number of files that have been updated (Input/output)</param>
        /// <param name="itemInUse">Used to track when a file or directory is in use by another process (log a message if the source and target files differ)</param>
        /// <param name="fileUsageMessage">Message to log when the file (or directory) is in use and the source and targets differ</param>
        private void ProcessRollbackFile(
            FileSystemInfo rollbackFile,
            DirectoryContainer targetFolderInfo,
            string targetFolderPath,
            ref int fileUpdateCount,
            eItemInUseConstants itemInUse = eItemInUseConstants.NotInUse,
            string fileUsageMessage = "")
        {

            var sourceFilePath = TrimSuffix(rollbackFile.FullName, ROLLBACK_SUFFIX);

            var sourceFile = new FileInfo(sourceFilePath);

            if (sourceFile.Exists)
            {
                var copied = CopyFileIfNeeded(sourceFile, targetFolderInfo, targetFolderPath, ref fileUpdateCount, eDateComparisonModeConstants.CopyIfSizeOrDateDiffers, itemInUse, fileUsageMessage);
                if (copied)
                {
                    string prefix;

                    if (PreviewMode)
                    {
                        prefix = "Preview rollback of file ";
                    }
                    else
                    {
                        prefix = "Rolled back file ";
                    }

                    ShowMessage(prefix + sourceFile.Name + " to version from " + sourceFile.LastWriteTimeUtc.ToLocalTime() + " with size " + (sourceFile.Length / 1024.0).ToString("0.0") + " KB");
                }
            }
            else
            {
                ShowWarning(
                    "Warning: Rollback file is present (" + rollbackFile.Name + ") but expected source file was not found: " +
                    sourceFile.Name, 24);
            }
        }

        private void SetLocalErrorCode(eDMSUpdateManagerErrorCodes eNewErrorCode, bool leaveExistingErrorCodeUnchanged = false)
        {
            if (leaveExistingErrorCodeUnchanged && LocalErrorCode != eDMSUpdateManagerErrorCodes.NoError)
            {
                // An error code is already defined; do not change it
            }
            else
            {
                LocalErrorCode = eNewErrorCode;

                if (eNewErrorCode == eDMSUpdateManagerErrorCodes.NoError)
                {
                    if (ErrorCode == eProcessFoldersErrorCodes.LocalizedError)
                    {
                        SetBaseClassErrorCode(eProcessFoldersErrorCodes.NoError);
                    }
                }
                else
                {
                    SetBaseClassErrorCode(eProcessFoldersErrorCodes.LocalizedError);
                }
            }
        }

        private string TrimSuffix(string text, string suffix)
        {
            if (text.Length >= suffix.Length)
            {
                return text.Substring(0, text.Length - suffix.Length);
            }
            return text;
        }

        private class ProcessNameComparer : IComparer<Process>
        {
            public int Compare(Process x, Process y)
            {
                return string.Compare(x?.ProcessName, y?.ProcessName, StringComparison.InvariantCultureIgnoreCase);
            }
        }
    }
}
