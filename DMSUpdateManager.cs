using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using PRISM;
using PRISM.FileProcessor;
using Renci.SshNet.Common;

namespace DMSUpdateManager
{
    /// <summary>
    /// This program copies new and updated files from a source directory
    /// to a target directory
    /// </summary>
    /// <remarks>
    /// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
    /// Program started January 16, 2009
    /// --
    /// E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov
    /// Website: https://omics.pnl.gov/ or https://panomics.pnnl.gov/")
    /// </remarks>
    public class DMSUpdateManager : ProcessFoldersBase
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public DMSUpdateManager()
        {
            mFileDate = "March 15, 2018";

            mFilesToIgnore = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
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
        private string mSourceDirectoryPath;

        /// <summary>
        /// Target directory path
        /// </summary>
        /// <remarks>Ignored if updating a remote host</remarks>
        private string mTargetDirectoryPath;

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

        private string mLastDirectoryProcessesChecked;
        private string mLastDirectoryRunningProcessPath;
        private uint mLastDirectoryRunningProcessId;

        private string mTargetDirectoryPathBase;

        /// <summary>
        /// Minimum time between updates
        /// </summary>
        /// <remarks>Ignored if ForceUpdate is true</remarks>
        private int mMinimumRepeatThresholdSeconds;

        /// <summary>
        /// Suffix applied to the mutex name when checking for other running copies of the DMSUpdateManager
        /// </summary>
        private string mMutexNameSuffix;

        #endregion

        #region "Properties"

        /// <summary>
        /// When mCopySubdirectoriesToParentDirectory=True, will copy any subdirectories of the source directory
        /// into a subdirectory off the parent directory the target directory
        /// </summary>
        /// <remarks>
        /// For example:
        ///   The .Exe resides in directory C:\DMS_Programs\AnalysisToolManager\DMSUpdateManager.exe
        ///   mSourceDirectoryPath = "\\gigasax\DMS_Programs\AnalysisToolManagerDistribution"
        ///   mTargetDirectoryPath = "."
        ///   Files are synced from "\\gigasax\DMS_Programs\AnalysisToolManagerDistribution" to "C:\DMS_Programs\AnalysisToolManager\"
        ///   Next, directory \\gigasax\DMS_Programs\AnalysisToolManagerDistribution\MASIC\ will get sync'd with ..\MASIC (but only if ..\MASIC exists)
        ///     Note that ..\MASIC is actually C:\DMS_Programs\MASIC\
        ///   When sync'ing the MASIC directories, will recursively sync additional directories that match
        ///   If the source directory contains file _PushDir_.txt or _AMSubDir_.txt then the directory will be copied to the target even if it doesn't exist there
        /// </remarks>
        public bool CopySubdirectoriesToParentDirectory { get; set; }

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
        public string SourceDirectoryPath
        {
            get
            {
                if (mSourceDirectoryPath == null)
                {
                    return string.Empty;
                }

                return mSourceDirectoryPath;
            }
            set
            {
                if (value != null)
                {
                    mSourceDirectoryPath = value;
                }
            }
        }

        #endregion

        /// <summary>
        /// Shorten the file or directory path if it starts with mTargetDirectoryPathBase
        /// </summary>
        /// <param name="fileOrDirectoryPath"></param>
        /// <returns></returns>
        private string AbbreviatePath(string fileOrDirectoryPath)
        {
            return AbbreviatePath(fileOrDirectoryPath, mTargetDirectoryPathBase);
        }

        /// <summary>
        /// Shorten the file or directory path if it starts with directoryPathBase
        /// </summary>
        /// <param name="fileOrDirectoryPath"></param>
        /// <param name="directoryPathBase"></param>
        /// <returns></returns>
        private string AbbreviatePath(string fileOrDirectoryPath, string directoryPathBase)
        {
            if (fileOrDirectoryPath.StartsWith(directoryPathBase))
            {
                if (fileOrDirectoryPath.Length > directoryPathBase.Length)
                {
                    return fileOrDirectoryPath.Substring(directoryPathBase.Length + 1);
                }
                return ".";
            }

            return fileOrDirectoryPath;
        }

        /// <summary>
        /// Add a file to ignore from processing
        /// </summary>
        /// <param name="fileName">Full filename (no wildcards)</param>
        public void AddFileToIgnore(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return;

            if (!mFilesToIgnore.Contains(fileName))
            {
                mFilesToIgnore.Add(fileName);
            }
        }

        private string CombinePaths(DirectoryContainer targetDirectoryInfo, string parentDirectory, string directoryToAppend)
        {
            if (targetDirectoryInfo.TrackingRemoteHostDirectory)
                return parentDirectory + '/' + directoryToAppend;

            return Path.Combine(parentDirectory, directoryToAppend);
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
        /// <param name="targetDirectoryInfo">Target directory info</param>
        /// <param name="targetFile">Target file</param>
        /// <param name="fileUpdateCount">Total number of files updated (input/output)</param>
        /// <param name="copyReason">Reason for the copy</param>
        private void CopyFile(
            FileInfo sourceFile,
            DirectoryContainer targetDirectoryInfo,
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
                ShowOldAndNewFileInfo("Preview: Update file: ", targetFile, existingFileInfo, updatedFileInfo, copyReason, false);
            }
            else
            {
                ShowOldAndNewFileInfo("Update file: ", targetFile, existingFileInfo, updatedFileInfo, copyReason, true);

                try
                {
                    var copiedFile = targetDirectoryInfo.CopyFile(sourceFile, targetFile.FullName);

                    if (copiedFile.Length != sourceFile.Length)
                    {
                        ShowErrorMessage("Copy of " + sourceFile.Name + " failed; sizes differ");
                    }
                    else if (!TimesMatch(copiedFile.LastWriteTimeUtc, sourceFile.LastWriteTimeUtc))
                    {
                        ShowErrorMessage("Copy of " + sourceFile.Name + " failed; modification times differ");
                    }
                    else
                    {
                        fileUpdateCount += 1;
                    }
                }
                catch (SftpPermissionDeniedException ex)
                {
                    ShowErrorMessage(string.Format("Error copying {0} to {1}: {2} for user {3}",
                                                   sourceFile.Name, targetFile.FullName, ex.Message, RemoteHostInfo.Username));
                }
                catch (Exception ex)
                {
                    string msg;
                    if (targetDirectoryInfo.TrackingRemoteHostDirectory)
                        msg = string.Format("Error copying {0} to {1}: {2} for user {3}",
                                            sourceFile.Name, targetFile.FullName, ex.Message, RemoteHostInfo.Username);
                    else
                        msg = string.Format("Error copying {0} to {1}: {2}",
                                            sourceFile.Name, targetFile.FullName, ex.Message);

                    ShowErrorMessage(msg);
                }
            }
        }

        /// <summary>
        /// Compare the source file to the target file and update it if they differ
        /// </summary>
        /// <param name="sourceFile">Source file</param>
        /// <param name="targetDirectoryInfo">Target directory info</param>
        /// <param name="targetDirectoryPath">Target directory path</param>
        /// <param name="fileUpdateCount">Number of files that have been updated (Input/output)</param>
        /// <param name="eDateComparisonMode">Date comparison mode</param>
        /// <param name="itemInUse">Used to track when a file or directory is in use by another process (log a message if the source and target files differ)</param>
        /// <param name="fileUsageMessage">Message to log when the file (or directory) is in use and the source and targets differ</param>
        /// <returns>True if the file was updated, otherwise false</returns>
        /// <remarks></remarks>
        private bool CopyFileIfNeeded(
            FileInfo sourceFile,
            DirectoryContainer targetDirectoryInfo,
            string targetDirectoryPath,
            ref int fileUpdateCount,
            eDateComparisonModeConstants eDateComparisonMode,
            eItemInUseConstants itemInUse = eItemInUseConstants.NotInUse,
            string fileUsageMessage = "")
        {

            var targetFilePath = CombinePaths(targetDirectoryInfo, targetDirectoryPath, sourceFile.Name);
            var targetFile = targetDirectoryInfo.GetFileInfo(targetFilePath);

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
                    else if (!TimesMatch(sourceFile.LastWriteTimeUtc, targetFile.LastWriteTimeUtc))
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
                    else if (TimeIsNewer(sourceFile.LastWriteTimeUtc, targetFile.LastWriteTimeUtc))
                    {
                        needToCopy = true;
                        copyReason = "source file is newer";
                    }

                    if (needToCopy && eDateComparisonMode == eDateComparisonModeConstants.RetainNewerTargetIfDifferentSize)
                    {
                        if (TimeIsNewer(targetFile.LastWriteTimeUtc, sourceFile.LastWriteTimeUtc))
                        {
                            // Target file is newer than the source; do not overwrite
                            // Check for a .rollback file
                            var rollbackFile = new FileInfo(sourceFile.FullName + ROLLBACK_SUFFIX);

                            if (!rollbackFile.Exists)
                            {
                                // No .rollback file; log a warning every 24 hours
                                var strWarning = "Warning: Skipping file " + targetFile.FullName + " since a newer version exists in the target; " +
                                                 "source=" + sourceFile.LastWriteTimeUtc.ToLocalTime() + ", target=" +
                                                 targetFile.LastWriteTimeUtc.ToLocalTime();

                                ShowWarning(strWarning, 24);
                            }

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
                    ShowMessage("Skipping " + targetFile.FullName + "; cannot update the currently running copy of the DMSUpdateManager", false, 4);
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
                                        AbbreviatePath(targetFile.DirectoryName) + " is in use (by an unknown process)", !PreviewMode, 4);
                        }
                        else
                        {
                            ShowMessage("Skipping " + sourceFile.Name + " in directory " +
                                        AbbreviatePath(targetFile.DirectoryName) + " because currently in use (by an unknown process)", !PreviewMode, 4);
                        }
                    }
                    else
                    {
                        ShowMessage(fileUsageMessage, !PreviewMode, 4);
                    }

                    return false;
                }
            }

            CopyFile(sourceFile, targetDirectoryInfo, targetFile, ref fileUpdateCount, copyReason);
            return true;
        }

        private FileOrDirectoryInfo CreateDirectoryIfMissing(DirectoryContainer targetDirectoryInfo, string directoryPath)
        {

            var currentTask = "validating";
            var isLinuxDir = targetDirectoryInfo.TrackingRemoteHostDirectory;

            try
            {
                // Make sure the target directory exists
                var targetDirectory = targetDirectoryInfo.GetDirectoryInfo(directoryPath);

                if (targetDirectory.Exists)
                    return targetDirectory;

                if (PreviewMode)
                {
                    ShowMessage("Preview: Create directory " + directoryPath, false);
                    return FileOrDirectoryInfo.InitializeMissingDirectoryInfo(directoryPath, isLinuxDir);
                }

                currentTask = "creating";
                return targetDirectoryInfo.CreateDirectoryIfMissing(directoryPath);
            }
            catch (Exception ex)
            {
                ShowErrorMessage(string.Format("Error {0} directory {1}: {2}", currentTask, directoryPath, ex.Message));
                ConsoleMsgUtils.ShowWarning(clsStackTraceFormatter.GetExceptionStackTraceMultiLine(ex));

                return FileOrDirectoryInfo.InitializeMissingDirectoryInfo(directoryPath, isLinuxDir);
            }
        }

        /// <summary>
        /// Examine the process path to determine the executable path
        /// </summary>
        /// <param name="processPath"></param>
        /// <param name="exeName">Executable name</param>
        /// <returns>Executable path, or an empty string if cannot be determined</returns>
        private string ExtractExePathFromProcessPath(string processPath, out string exeName)
        {
            try
            {
                // First try using Path.GetFileName
                exeName = Path.GetFileName(processPath);
                return processPath;
            }
            catch (Exception)
            {
                // Error; likely the processPath has invalid characters
                // For example, this path leads to an exception:
                // "C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\Common7\ServiceHub\Hosts\ServiceHub.Host.Node.x86\ServiceHub.Host.Node.x86.exe" "./ServiceHub/controller/hubController.all.js" "3394f971a1ed456c51c2f32d0c4071f0c7c1a655cecbcb4babba32efd0f844fd"

                try
                {

                    // Look for a filename embedded in double quotes
                    var exeMatcher = new Regex("\"(?<ExePath>[^\"]+)\"");
                    var match = exeMatcher.Match(processPath);
                    if (match.Success)
                        return ExtractExePathFromProcessPath(match.Groups["ExePath"].Value, out exeName);

                    var spaceIndex = processPath.IndexOf(' ');
                    if (spaceIndex > 0)
                    {
                        var startOfPath = processPath.Substring(0, spaceIndex);
                        if (startOfPath.Length > 1 && startOfPath.StartsWith("\""))
                            return ExtractExePathFromProcessPath(startOfPath.Substring(1), out exeName);

                        return ExtractExePathFromProcessPath(startOfPath, out exeName);
                    }
                }
                catch (Exception)
                {
                    // Ignore errors here
                }

                exeName = string.Empty;
                return string.Empty;
            }
        }

        private void InitializeLocalVariables()
        {
            ReThrowEvents = false;
            mLogFileUsesDateStamp = true;

            ForceUpdate = false;
            PreviewMode = false;
            OverwriteNewerFiles = false;
            CopySubdirectoriesToParentDirectory = false;

            MutexWaitTimeoutMinutes = 5;
            DoNotUseMutex = false;
            mMutexNameSuffix = string.Empty;

            mMinimumRepeatThresholdSeconds = 30;

            mSourceDirectoryPath = string.Empty;
            mTargetDirectoryPath = string.Empty;

            RemoteHostInfo = new RemoteHostConnectionInfo();
            mRemoteHostInfoDefined = false;

            ResetFilesToIgnore();

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
                var exePath = ExtractExePathFromProcessPath(processPath, out var exeName);
                if (string.IsNullOrWhiteSpace(exePath))
                    continue;

                if (processPath.Contains(runningExeName) || executablesToIgnore.Contains(exeName))
                {
                    continue;
                }

                var newProcess = new ProcessInfo(processId, exePath, cmd);
                if (newProcess.DirectoryHierarchy.Count < 3)
                {
                    // Process running in the root directory or one below the root directory; ignore it
                    continue;
                }
                mProcessesDict.Add(processId, newProcess);
            }

            mProcessesMatchingTarget = new Dictionary<uint, string>();

            // Ignore checking for running processes in the first directory that we are updating
            mLastDirectoryProcessesChecked = Path.GetDirectoryName(mExecutingExePath);
            mLastDirectoryRunningProcessPath = mExecutingExePath;
            mLastDirectoryRunningProcessId = 0;
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
            return fileInfo.LastWriteTime.ToString("yyyy-MM-dd hh:mm:ss tt") + " and " + fileInfo.Length + " bytes";
        }

        private bool GetMutex(string mutexName, bool updatingTargetDirectory, out Mutex mutex, out bool doNotUpdateParent)
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
                        if (updatingTargetDirectory)
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
                    var exeDirectory = Path.GetDirectoryName(mExecutingExePath);
                    if (exeDirectory == null)
                        parameterFilePath = Path.GetFileName(parameterFilePath);
                    else
                        parameterFilePath = Path.Combine(exeDirectory, Path.GetFileName(parameterFilePath));

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

                    CopySubdirectoriesToParentDirectory = settingsFile.GetParam(OPTIONS_SECTION, "CopySubdirectoriesToParentFolder", CopySubdirectoriesToParentDirectory);

                    mSourceDirectoryPath = settingsFile.GetParam(OPTIONS_SECTION, "SourceFolderPath", mSourceDirectoryPath);
                    mTargetDirectoryPath = settingsFile.GetParam(OPTIONS_SECTION, "TargetFolderPath", mTargetDirectoryPath);

                    RemoteHostInfo.HostName = settingsFile.GetParam(OPTIONS_SECTION, "RemoteHostName", RemoteHostInfo.HostName);
                    RemoteHostInfo.Username = settingsFile.GetParam(OPTIONS_SECTION, "RemoteHostUserName", RemoteHostInfo.Username);
                    RemoteHostInfo.PrivateKeyFile = settingsFile.GetParam(OPTIONS_SECTION, "PrivateKeyFilePath", RemoteHostInfo.PrivateKeyFile);
                    RemoteHostInfo.PassphraseFile = settingsFile.GetParam(OPTIONS_SECTION, "PassphraseFilePath", RemoteHostInfo.PassphraseFile);

                    if (!string.IsNullOrWhiteSpace(RemoteHostInfo.HostName) ||
                        !string.IsNullOrWhiteSpace(RemoteHostInfo.Username))
                    {
                        RemoteHostInfo.BaseDirectoryPath = mTargetDirectoryPath;
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

                    if (LogMessagesToFile)
                    {
                        UpdateAutoDefinedLogFilePath(logFolderPath, "DMSUpdateManager");
                    }

                    var filesToIgnore = settingsFile.GetParam(OPTIONS_SECTION, "FilesToIgnore", string.Empty);
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(filesToIgnore))
                        {
                            var ignoreList = filesToIgnore.Split(',');

                            ResetFilesToIgnore();
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
        /// Update files in directory inputDirectoryPath
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

        private void ResetFilesToIgnore()
        {
            mFilesToIgnore.Clear();
            AddFileToIgnore(PUSH_DIR_FLAG);
            AddFileToIgnore(PUSH_AM_SUBDIR_FLAG);
            AddFileToIgnore(DELETE_SUBDIR_FLAG);
            AddFileToIgnore(DELETE_AM_SUBDIR_FLAG);
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
                ShowMessage(spacePad + existingFileInfo, logToFile);
            }
            ShowMessage(spacePad + updatedFileInfo, logToFile);
        }

        /// <summary>
        /// Compare two DateTime values, returning true if the first value is newer than the second, with the given tolerance
        /// </summary>
        /// <param name="time1"></param>
        /// <param name="time2"></param>
        /// <param name="toleranceMilliseconds"></param>
        /// <returns>True if time1 is newer than time2, within the given tolerance</returns>
        private bool TimeIsNewer(DateTime time1, DateTime time2, int toleranceMilliseconds = 1000)
        {
            var timeDiffMilliseconds = time1.Subtract(time2).TotalMilliseconds;
            return timeDiffMilliseconds > toleranceMilliseconds;
        }

        /// <summary>
        /// Compare two DateTime values, returning true if the times are the same, with the given tolerance
        /// </summary>
        /// <param name="time1"></param>
        /// <param name="time2"></param>
        /// <param name="toleranceMilliseconds"></param>
        /// <returns>True if time1 equals time2, within the given tolerance</returns>
        private bool TimesMatch(DateTime time1, DateTime time2, int toleranceMilliseconds = 1000)
        {
            var timeDiffMilliseconds = time1.Subtract(time2).TotalMilliseconds;
            return Math.Abs(timeDiffMilliseconds) <= toleranceMilliseconds;
        }

        /// <summary>
        /// Update files in directory targetDirectoryPath
        /// </summary>
        /// <param name="targetDirectoryPath">Target directory to update</param>
        /// <param name="parameterFilePath">Parameter file defining the source directory path and other options</param>
        /// <returns>True if success, false if an error</returns>
        /// <remarks>If TargetFolder is defined in the parameter file, targetDirectoryPath will be ignored</remarks>
        public bool UpdateFolder(string targetDirectoryPath, string parameterFilePath)
        {
            SetLocalErrorCode(eDMSUpdateManagerErrorCodes.NoError);

            if (!string.IsNullOrEmpty(targetDirectoryPath))
            {
                // Update mtargetDirectoryPath using targetDirectoryPath
                // Note: If TargetFolder is defined in the parameter file, this value will get overridden
                mTargetDirectoryPath = string.Copy(targetDirectoryPath);
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
                if (mRemoteHostInfoDefined)
                {
                    return UpdateRemoteHostWork(RemoteHostInfo, parameterFilePath);
                }

                targetDirectoryPath = string.Copy(mTargetDirectoryPath);

                if (string.IsNullOrEmpty(mSourceDirectoryPath))
                {
                    ShowWarning("Source directory path is not defined; either specify it at the command line or include it in the parameter file");
                    SetBaseClassErrorCode(eProcessFoldersErrorCodes.InvalidInputFolderPath);
                    return false;
                }

                if (string.IsNullOrWhiteSpace(targetDirectoryPath))
                {
                    ShowWarning("Target directory path is not defined; either specify it at the command line or include it in the parameter file");
                    SetBaseClassErrorCode(eProcessFoldersErrorCodes.InvalidInputFolderPath);
                    return false;
                }

                // Note that CleanupFilePaths() will update mOutputDirectoryPath, which is used by LogMessage()
                // Since we're updating files on the local computer, use the target directory path for parameter inputFolderPath of CleanupFolderPaths
                var tempStr = string.Empty;
                if (!CleanupFolderPaths(ref targetDirectoryPath, ref tempStr))
                {
                    SetBaseClassErrorCode(eProcessFoldersErrorCodes.FilePathError);
                    return false;
                }

                var targetDirectoryInfo = new DirectoryContainer(targetDirectoryPath);

                if (DoNotUseMutex)
                {
                    return UpdateDirectoryRun(mSourceDirectoryPath, targetDirectoryInfo, parameterFilePath);
                }

                return UpdateDirectoryMutexWrapped(mSourceDirectoryPath, targetDirectoryInfo, parameterFilePath);
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
        /// <param name="targetHostInfo">Target host info</param>
        /// <param name="parameterFilePath">Parameter file defining the source directory path and other options</param>
        /// <returns>True if success, false if an error</returns>
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

            return UpdateRemoteHostWork(RemoteHostInfo, parameterFilePath);
        }

        /// <summary>
        /// Update files on a remote Linux host
        /// </summary>
        /// <param name="remoteHostInfo">Remote host info</param>
        /// <param name="sourceDirectoryPath">Source directory (typically a Windows share)</param>
        /// <param name="targetDirectoryPath">
        /// Target directory on the remote host, for example /opt/DMS_Programs
        /// Ignored if remoteHostInfo.DirectoryPath is defined
        /// </param>
        /// <param name="overwriteNewerFiles">If False, then will not overwrite files in the target directory that are newer than files in the source directory</param>
        /// <param name="ignoreList">List of files that will not be copied</param>
        /// <param name="copySubdirectoriesToParentDirectory">When True, copy any subdirectories of the source directory into a subdirectory off the parent directory the target directory</param>
        /// <param name="mutexNameSuffix">Suffix applied to the mutex name when checking for other running copies of the DMSUpdateManager</param>
        /// <param name="minimumRepeatTimeSeconds">Minimum time between updates</param>
        /// <returns></returns>
        public bool UpdateRemoteHost(
            RemoteHostConnectionInfo remoteHostInfo,
            string sourceDirectoryPath,
            string targetDirectoryPath,
            List<string> ignoreList,
            bool overwriteNewerFiles = false,
            bool copySubdirectoriesToParentDirectory = true,
            string mutexNameSuffix = "UpdateRemoteHost",
            int minimumRepeatTimeSeconds = 30)
        {
            SetLocalErrorCode(eDMSUpdateManagerErrorCodes.NoError);

            RemoteHostInfo = remoteHostInfo;

            mSourceDirectoryPath = sourceDirectoryPath;
            mTargetDirectoryPath = targetDirectoryPath;

            if (string.IsNullOrWhiteSpace(remoteHostInfo.BaseDirectoryPath) && !string.IsNullOrWhiteSpace(targetDirectoryPath))
            {
                remoteHostInfo.BaseDirectoryPath = targetDirectoryPath;
            }
            else if (!string.IsNullOrWhiteSpace(remoteHostInfo.BaseDirectoryPath))
            {
                mTargetDirectoryPath = remoteHostInfo.BaseDirectoryPath;
            }

            OverwriteNewerFiles = overwriteNewerFiles;

            ResetFilesToIgnore();
            foreach (var item in ignoreList)
            {
                AddFileToIgnore(item.Trim());
            }

            CopySubdirectoriesToParentDirectory = copySubdirectoriesToParentDirectory;

            mMutexNameSuffix = mutexNameSuffix;

            mMinimumRepeatThresholdSeconds = minimumRepeatTimeSeconds;

            LogMessagesToFile = false;

            LoggingLevel = LogLevel.Normal;

            return UpdateRemoteHostWork(RemoteHostInfo, "UpdateRemoteHost.xml");
        }

        /// <summary>
        /// Update files on a remote Linux host
        /// </summary>
        /// <param name="targetHostInfo">Target host info</param>
        /// <param name="parameterFilePath">Parameter file path</param>
        /// <returns>True if success, false if an error</returns>
        /// <remarks>
        /// Parameter file path is used to determine the checkfile path, which is used to asssure
        /// that a minimum amount of time elapses between sequential runs of the DMS Update Manager
        /// </remarks>
        private bool UpdateRemoteHostWork(RemoteHostConnectionInfo targetHostInfo, string parameterFilePath)
        {

            try
            {
                if (string.IsNullOrEmpty(mSourceDirectoryPath))
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

                var msg = string.Format("Updating files on remote host {0}; connecting as user {1}", targetHostInfo.HostName, targetHostInfo.Username);
                ShowMessage(msg, false);

                // Note that CleanupFilePaths() will update mOutputDirectoryPath, which is used by LogMessage()
                // Since we're updating a files on a remote host, use the entry assembly's path for parameter inputFolderPath of CleanupFolderPaths
                var appFolderPath = GetAppFolderPath();
                var tempStr = string.Empty;
                if (!CleanupFolderPaths(ref appFolderPath, ref tempStr))
                {
                    SetBaseClassErrorCode(eProcessFoldersErrorCodes.FilePathError);
                    return false;
                }

                var targetDirectoryInfo = new DirectoryContainer(targetHostInfo);

                if (DoNotUseMutex)
                {
                    return UpdateDirectoryRun(mSourceDirectoryPath, targetDirectoryInfo, parameterFilePath);
                }

                return UpdateDirectoryMutexWrapped(mSourceDirectoryPath, targetDirectoryInfo, parameterFilePath);
            }
            catch (Exception ex)
            {
                HandleException("Error in UpdateRemoteHostWork", ex);
                return false;
            }
        }

        /// <summary>
        /// Check a global mutex keyed on the parameter file path; if it returns false, exit
        /// </summary>
        /// <param name="sourceDirectoryPath">Source directory path</param>
        /// <param name="targetDirectoryInfo">Target directory info</param>
        /// <param name="parameterFilePath">Parameter file defining the source directory path and other options</param>
        /// <returns></returns>
        private bool UpdateDirectoryMutexWrapped(string sourceDirectoryPath, DirectoryContainer targetDirectoryInfo, string parameterFilePath)
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

                return UpdateDirectoryRun(sourceDirectoryPath, targetDirectoryInfo, parameterFilePath, doNotUpdateParent);
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
        /// <param name="sourceDirectory">Source directory info</param>
        /// <param name="targetDirectoryInfo">Target directory info</param>
        /// <param name="parameterFilePath">Parameter file defining the source directory path and other options</param>
        /// <returns></returns>
        private bool UpdateDirectoryCopyToParentMutexWrapped(
            DirectoryInfo sourceDirectory,
            DirectoryContainer targetDirectoryInfo,
            string parameterFilePath)
        {
            Mutex mutex = null;
            var hasMutexHandle = false;

            try
            {
                var doNotUpdateParent = false;
                var targetDirectoryParent = targetDirectoryInfo.ParentPath;
                if (!string.IsNullOrWhiteSpace(targetDirectoryParent))
                {
                    var mutexName = ConstructMutexName(targetDirectoryParent);

                    hasMutexHandle = GetMutex(mutexName, true, out mutex, out doNotUpdateParent);
                }

                if (!doNotUpdateParent)
                {
                    return UpdateDirectoryCopyToParentRun(sourceDirectory, targetDirectoryInfo, parameterFilePath);
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

        /// <summary>
        /// Entry method for updating the target Directory
        /// </summary>
        /// <param name="sourceDirectoryPath">Source directory path</param>
        /// <param name="targetDirectoryInfo">Target directory info</param>
        /// <param name="parameterFilePath">Parameter file path</param>
        /// <param name="doNotUpdateParent"></param>
        /// <returns></returns>
        /// <remarks>
        /// Parameter file path is used to determine the checkfile path, which is used to asssure
        /// that a minimum amount of time elapses between sequential runs of the DMS Update Manager
        /// </remarks>
        private bool UpdateDirectoryRun(string sourceDirectoryPath, DirectoryContainer targetDirectoryInfo, string parameterFilePath, bool doNotUpdateParent = false)
        {
            var sourceDirectory = new DirectoryInfo(sourceDirectoryPath);

            if (sourceDirectory.Parent == null)
            {
                OnErrorEvent("Unable to determine the parent directory of the source directory: " + sourceDirectory.FullName);
                return false;
            }

            if (targetDirectoryInfo.TrackingRemoteHostDirectory)
            {
                mTargetDirectoryPathBase = targetDirectoryInfo.ParentPath;
                Console.WriteLine();
            }
            else
            {

                var directoryToCheck = new DirectoryInfo(targetDirectoryInfo.DirectoryPath);
                if (directoryToCheck.Parent == null)
                {
                    OnErrorEvent("Unable to determine the parent directory of the target directory: " + directoryToCheck.FullName);
                    return false;
                }

                mTargetDirectoryPathBase = directoryToCheck.Parent.FullName;
            }

            ResetProgress();

            var targetDirectory = targetDirectoryInfo.GetDirectoryInfo(targetDirectoryInfo.DirectoryPath);
            var success = UpdateDirectoryWork(sourceDirectory.FullName, targetDirectoryInfo, targetDirectory, pushNewSubdirectories: false);

            if (!CopySubdirectoriesToParentDirectory || doNotUpdateParent)
                return success;

            if (DoNotUseMutex)
            {
                success = UpdateDirectoryCopyToParentRun(sourceDirectory, targetDirectoryInfo, parameterFilePath);
            }
            else
            {
                success = UpdateDirectoryCopyToParentMutexWrapped(sourceDirectory, targetDirectoryInfo, parameterFilePath);
            }

            return success;
        }

        /// <summary>
        /// Update directory and copy subdirectories to the parent directory
        /// </summary>
        /// <param name="sourceDirectory">Source directory info</param>
        /// <param name="targetDirectoryInfo">Target directory info</param>
        /// <param name="parameterFilePath">Parameter file path</param>
        /// <returns></returns>
        /// <remarks>
        /// Parameter file path is used to determine the checkfile path, which is used to asssure
        /// that a minimum amount of time elapses between sequential runs of the DMS Update Manager
        /// </remarks>
        private bool UpdateDirectoryCopyToParentRun(DirectoryInfo sourceDirectory, DirectoryContainer targetDirectoryInfo, string parameterFilePath)
        {
            var success = true;
            var skipShared = false;
            var checkFilePath = string.Empty;

            // Check the repeat time threshold; must be checked before any writes to the log; make sure anything added above only logs on error
            if (!string.IsNullOrWhiteSpace(LogFilePath))
            {
                var logFileInfo = new FileInfo(LogFilePath);
                var logFileDirectory = logFileInfo.DirectoryName;

                if (logFileDirectory == null)
                {
                    OnErrorEvent("Unable to determine the parent directory of the log file: " + logFileInfo.FullName);
                    return false;
                }

                checkFilePath = Path.Combine(logFileDirectory, Path.GetFileNameWithoutExtension(parameterFilePath) + "_parentCheck.txt");
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
                            var msg = string.Format("Last update ran {0:N0} seconds ago; forcing update due to /Force switch", lastUpdateSeconds);
                            ShowMessage(msg, false);
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

                success = UpdateDirectoryCopyToParent(sourceDirectory, targetDirectoryInfo);

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

        private bool UpdateDirectoryCopyToParent(DirectoryInfo sourceDirectory, DirectoryContainer targetDirectoryInfo)
        {
            var successOverall = true;

            var parentDirectory = targetDirectoryInfo.ParentPath;
            if (parentDirectory == null)
            {
                OnWarningEvent("Unable to determine the parent directory of " + targetDirectoryInfo.DirectoryPath);
                return false;
            }

            foreach (var sourceSubdirectory in sourceDirectory.GetDirectories())
            {

                // The target directory is treated as a subdirectory of the parent directory
                var targetSubdirectoryPath = CombinePaths(targetDirectoryInfo, parentDirectory, sourceSubdirectory.Name);

                // Initially assume we'll process this directory if it exists at the target
                var targetSubdirectory = targetDirectoryInfo.GetDirectoryInfo(targetSubdirectoryPath);

                var processSubdirectory = targetSubdirectory.Exists;

                if (processSubdirectory && sourceSubdirectory.GetFiles(DELETE_SUBDIR_FLAG).Length > 0)
                {
                    // Remove this subDirectory (but only if it's empty)
                    var directoryDeleted = DeleteSubdirectory(sourceSubdirectory, targetDirectoryInfo, targetSubdirectory, "parent subdirectory", DELETE_SUBDIR_FLAG);
                    if (directoryDeleted)
                        processSubdirectory = false;
                }

                if (sourceSubdirectory.GetFiles(DELETE_AM_SUBDIR_FLAG).Length > 0)
                {
                    // Remove this subdirectory (but only if it's empty)
                    var analysisMgrSubDirPath = CombinePaths(targetDirectoryInfo, targetDirectoryInfo.DirectoryPath, sourceSubdirectory.Name);
                    var analysisMgrSubDir = targetDirectoryInfo.GetDirectoryInfo(analysisMgrSubDirPath);

                    var directoryDeleted = DeleteSubdirectory(sourceSubdirectory, targetDirectoryInfo, analysisMgrSubDir, "subdirectory", DELETE_AM_SUBDIR_FLAG);
                    if (directoryDeleted)
                        processSubdirectory = false;
                }

                if (sourceSubdirectory.GetFiles(PUSH_AM_SUBDIR_FLAG).Length > 0)
                {
                    // Push this directory as a subdirectory of the target directory, not as a subdirectory of the parent directory
                    targetSubdirectoryPath = CombinePaths(targetDirectoryInfo, targetDirectoryInfo.DirectoryPath, sourceSubdirectory.Name);
                    processSubdirectory = true;
                }
                else
                {
                    if (sourceSubdirectory.GetFiles(PUSH_DIR_FLAG).Length > 0)
                    {
                        processSubdirectory = true;
                    }
                }

                if (processSubdirectory)
                {
                    var verifiedSubdirectory = CreateDirectoryIfMissing(targetDirectoryInfo, targetSubdirectoryPath);
                    var success = UpdateDirectoryWork(sourceSubdirectory.FullName, targetDirectoryInfo, verifiedSubdirectory, pushNewSubdirectories: true);
                    if (!success)
                        successOverall = false;
                }
            }

            return successOverall;
        }

        private bool DeleteSubdirectory(
            FileSystemInfo sourceSubdirectory,
            DirectoryContainer targetDirectoryInfo,
            FileOrDirectoryInfo targetSubdirectory,
            string directoryDescription,
            string deleteFlag)
        {
            if (string.IsNullOrWhiteSpace(directoryDescription))
            {
                directoryDescription = "directory";
            }

            if (!targetSubdirectory.Exists)
                return false;

            var fileCount = targetDirectoryInfo.GetFiles(targetSubdirectory).Count;
            var directories = targetDirectoryInfo.GetDirectories(targetSubdirectory);

            if (fileCount > 0)
            {
                ShowWarning(
                    "Folder flagged for deletion, but it is not empty (File Count = " + fileCount + "): " +
                    AbbreviatePath(targetSubdirectory.FullName));
                return false;
            }

            if (directories.Count > 0)
            {
                // Check each sub directory for file _DeleteSubDir_.txt
                foreach (var subDir in directories)
                {
                    var newSourceSubDir = new DirectoryInfo(Path.Combine(sourceSubdirectory.FullName, subDir.Name));
                    var deleteSubDirFile = new FileInfo(Path.Combine(newSourceSubDir.FullName, deleteFlag));

                    if (deleteSubDirFile.Exists)
                    {
                        // Recursively call this method
                        DeleteSubdirectory(newSourceSubDir, targetDirectoryInfo, subDir, directoryDescription, deleteFlag);
                    }
                }

                // Refresh the subdirectories
                var dirCount = targetDirectoryInfo.GetDirectories(targetSubdirectory, true).Count;

                if (dirCount > 0)
                {
                    ShowWarning(
                        "Directory flagged for deletion, but it is not empty (Directory Count = " + dirCount + "): " +
                        AbbreviatePath(targetSubdirectory.FullName));
                    return false;
                }
            }

            try
            {
                if (PreviewMode)
                {
                    ShowMessage("Preview " + directoryDescription + " delete: " + targetSubdirectory.FullName, false);
                }
                else
                {
                    targetDirectoryInfo.DeleteFileOrDirectory(targetSubdirectory);
                    ShowMessage("Deleted " + directoryDescription + " " + targetSubdirectory.FullName);
                }

                return true;
            }
            catch (Exception ex)
            {
                HandleException("Error removing empty " + directoryDescription + " flagged with " + deleteFlag + " at " + targetSubdirectory.FullName, ex);
                return false;
            }

        }

        private bool UpdateDirectoryWork(
            string sourceDirectoryPath,
            DirectoryContainer targetDirectoryInfo,
            FileOrDirectoryInfo targetDirectory,
            bool pushNewSubdirectories)
        {

            if (!targetDirectory.Exists && !PreviewMode)
            {
                ShowMessage("Skipping non-existent directory: " + AbbreviatePath(targetDirectory.FullName), false, eMessageType: eMessageTypeConstants.Debug);
                return false;
            }

            ShowMessage("Updating " + AbbreviatePath(targetDirectory.FullName), false, eMessageType: eMessageTypeConstants.Debug);


            // Obtain a list of files in the source directory
            var sourceDirectory = new DirectoryInfo(sourceDirectoryPath);

            var fileUpdateCount = 0;

            var filesInSource = sourceDirectory.GetFiles();

            // Populate a SortedSet with the names of any .delete files in fiFilesInSource
            var deleteFiles = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            var filesToDelete = (from sourceFile in filesInSource
                                 where sourceFile.Name.EndsWith(DELETE_SUFFIX, StringComparison.OrdinalIgnoreCase)
                                 select TrimSuffix(sourceFile.Name, DELETE_SUFFIX).ToLower());
            foreach (var item in filesToDelete)
            {
                deleteFiles.Add(item);
            }

            // Populate a SortedSet with the names of any .checkjava files in fiFilesInSource
            var checkJavaFiles = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            var javaFilesToCheck = (from sourceFile in filesInSource
                                    where sourceFile.Name.EndsWith(CHECK_JAVA_SUFFIX, StringComparison.OrdinalIgnoreCase)
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
                        if (sourceFile.Name.EndsWith(ROLLBACK_SUFFIX, StringComparison.OrdinalIgnoreCase))
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
                                if (!targetDirectoryInfo.TrackingRemoteHostDirectory &&
                                    TargetDirectoryInUseByProcess(targetDirectory.FullName, targetFileName, out fileUsageMessage))
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

                            ProcessRollbackFile(sourceFile, targetDirectoryInfo, targetDirectory.FullName, ref fileUpdateCount, itemInUse, fileUsageMessage);
                            break; // Break out of the while, continue the for loop
                        }

                        if (sourceFile.Name.EndsWith(DELETE_SUFFIX, StringComparison.OrdinalIgnoreCase))
                        {
                            // This is a Delete file
                            // Do not copy this file
                            // However, do look for a corresponding file that does not have .delete and delete that file in the target directory

                            ProcessDeleteFile(sourceFile, targetDirectoryInfo, targetDirectory.FullName);
                            break; // Break out of the while, continue the for loop
                        }

                        if (sourceFile.Name.EndsWith(CHECK_JAVA_SUFFIX, StringComparison.OrdinalIgnoreCase))
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

                        if (!targetDirectoryInfo.TrackingRemoteHostDirectory &&
                            checkJavaFiles.Contains(fileNameLCase))
                        {
                            if (JarFileInUseByJava(sourceFile, out fileUsageMessage))
                            {
                                itemInUse = eItemInUseConstants.ItemInUse;
                            }
                        }
                        else
                        {
                            if (!targetDirectoryInfo.TrackingRemoteHostDirectory &&
                                TargetDirectoryInUseByProcess(targetDirectory.FullName, sourceFile.Name, out fileUsageMessage))
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

                        CopyFileIfNeeded(sourceFile, targetDirectoryInfo, targetDirectory.FullName, ref fileUpdateCount, eDateComparisonMode, itemInUse, fileUsageMessage);

                        // File processed; move on to the next file
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (!errorLogged)
                        {
                            ShowErrorMessage("Error synchronizing " + sourceFile.Name + ": " + ex.Message);
                            ConsoleMsgUtils.ShowWarning(clsStackTraceFormatter.GetExceptionStackTraceMultiLine(ex));
                            errorLogged = true;
                        }

                        retryCount -= 1;
                        ConsoleMsgUtils.SleepSeconds(0.1);
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

                statusMessage += " using " + sourceDirectory.FullName + "\\";

                ShowMessage(statusMessage);
            }

            // Process each subdirectory in the source directory
            // If the directory exists at the target, copy it
            // Additionally, if the source directory contains file _PushDir_.txt, it gets copied even if it doesn't exist at the target
            foreach (var sourceSubdirectory in sourceDirectory.GetDirectories())
            {
                var targetSubdirectoryPath = CombinePaths(targetDirectoryInfo, targetDirectory.FullName, sourceSubdirectory.Name);
                var targetSubdirectory = targetDirectoryInfo.GetDirectoryInfo(targetSubdirectoryPath);

                // Initially assume we'll process this directory if it exists at the target
                var processSubdirectory = targetSubdirectory.Exists;
                if (processSubdirectory && sourceSubdirectory.GetFiles(DELETE_SUBDIR_FLAG).Length > 0)
                {
                    // Remove this subdirectory (but only if it's empty)
                    var directoryDeleted = DeleteSubdirectory(sourceSubdirectory, targetDirectoryInfo, targetSubdirectory, "subdirectory", DELETE_SUBDIR_FLAG);
                    if (directoryDeleted)
                        processSubdirectory = false;
                }

                if (pushNewSubdirectories && sourceSubdirectory.GetFiles(PUSH_DIR_FLAG).Length > 0)
                {
                    processSubdirectory = true;
                }

                if (processSubdirectory)
                {
                    var verifiedSubdirectory = CreateDirectoryIfMissing(targetDirectoryInfo, targetSubdirectory.FullName);
                    UpdateDirectoryWork(sourceSubdirectory.FullName, targetDirectoryInfo, verifiedSubdirectory, pushNewSubdirectories);
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
                            ConsoleMsgUtils.ShowDebug(process.ProcessName);
                        }
                        lastProcess = process.ProcessName;
                    }

                    if (!process.ProcessName.StartsWith("java", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    try
                    {
                        var commandLine = GetCommandLine(process, INCLUDE_PROGRAM_PATH);

                        if (PreviewMode & !mProcessesShown)
                        {
                            ConsoleMsgUtils.ShowDebug("  " + commandLine);
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

        private bool TargetDirectoryInUseByProcess(string targetDirectoryPath, string targetFileName, out string directoryUsageMessage)
        {
            directoryUsageMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(targetDirectoryPath))
            {
                OnWarningEvent("Empty target directory path passed to TargetDirectoryInUseByProcess");
                return false;
            }

            try
            {
                var processCount = GetNumTargetDirectoryProcesses(targetDirectoryPath, out var firstProcessPath, out var firstProcessId);

                if (processCount <= 0)
                {
                    return false;
                }

                // Example log messages:
                // Skipping UIMFLibrary.dll because directory DeconTools is in use by process DeconConsole.exe (PID 343243)
                // Skipping DeconConsole.exe because directory DeconTools is in use by 2 processes on this system, including DeconConsole.exe (PID 343243)

                directoryUsageMessage = "Skipping " + targetFileName + " because directory " + AbbreviatePath(targetDirectoryPath) + " is in use by ";

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

                    if (processFile.DirectoryName == targetDirectoryPath)
                    {
                        processPathToShow = Path.GetFileName(firstProcessPath) + processIdAppend;
                    }
                    else if (targetDirectoryPath.StartsWith(processFile.DirectoryName))
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
                    directoryUsageMessage += "process " + processPathToShow;
                }
                else
                {
                    directoryUsageMessage += processCount + " processes on this system, including " + processPathToShow;
                }

                return true;
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error looking for processes using files in " + targetDirectoryPath + ": " + ex.Message);
                return false;
            }

        }

        /// <summary>
        /// Determine the number of processes using files in the given directory
        /// </summary>
        /// <param name="targetDirectoryPath">Directory to examine</param>
        /// <param name="firstProcessPath">Output parameter: first process using files in this directory; empty string if no processes</param>
        /// <param name="firstProcessId">Output parameter: Process ID of first process using files in this directory</param>
        /// <returns>Count of processes using this directory</returns>
        private int GetNumTargetDirectoryProcesses(string targetDirectoryPath, out string firstProcessPath, out uint firstProcessId)
        {
            firstProcessPath = string.Empty;
            firstProcessId = 0;

            // Filter the queried results for each call to this method

            var targetDirectoryPathHierarchy = ProcessInfo.GetDirectoryHierarchy(targetDirectoryPath);

            if (string.Equals(targetDirectoryPath, mLastDirectoryProcessesChecked, StringComparison.OrdinalIgnoreCase))
            {
                firstProcessPath = mLastDirectoryRunningProcessPath;
                firstProcessId = mLastDirectoryRunningProcessId;
                return mProcessesMatchingTarget.Count;
            }

            mProcessesMatchingTarget.Clear();
            mLastDirectoryProcessesChecked = targetDirectoryPath;
            mLastDirectoryRunningProcessPath = string.Empty;
            mLastDirectoryRunningProcessId = 0;

            // Look for running processes with a .exe in the target directory (or in a parent of the target directory)
            // Ignore cmd.exe
            foreach (var item in mProcessesDict)
            {
                var exeFolderHierarchy = item.Value.DirectoryHierarchy;

                if (exeFolderHierarchy.Count > targetDirectoryPathHierarchy.Count)
                {
                    continue;
                }

                var treesMatch = true;
                for (var index = 0; index <= exeFolderHierarchy.Count - 1; index++)
                {
                    if (targetDirectoryPathHierarchy[index] != exeFolderHierarchy[index])
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
                if (item.Value.CommandLineArgs.IndexOf(targetDirectoryPath, StringComparison.OrdinalIgnoreCase) < 0)
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
                mLastDirectoryRunningProcessPath = firstProcessPath;
                mLastDirectoryRunningProcessId = firstProcessId;
            }

            return mProcessesMatchingTarget.Count;
        }

        private void ProcessDeleteFile(FileSystemInfo deleteFile, DirectoryContainer targetDirectoryInfo, string targetDirectoryPath)
        {
            var targetFilePath = CombinePaths(targetDirectoryInfo, targetDirectoryPath, TrimSuffix(deleteFile.Name, DELETE_SUFFIX));

            var targetFile = targetDirectoryInfo.GetFileInfo(targetFilePath);

            if (targetFile.Exists)
            {
                if (PreviewMode)
                {
                    ShowMessage("Preview delete: " + targetFile.FullName, false);
                }
                else
                {
                    targetDirectoryInfo.DeleteFileOrDirectory(targetFile);
                    ShowMessage("Deleted file " + targetFile.FullName);
                }
            }

            // Make sure the .delete file is also not in the target directory
            var targetDeleteFilePath = CombinePaths(targetDirectoryInfo, targetDirectoryPath, deleteFile.Name);
            var targetDeleteFile = targetDirectoryInfo.GetFileInfo(targetDeleteFilePath);

            if (targetDeleteFile.Exists)
            {
                if (PreviewMode)
                {
                    ShowMessage("Preview delete: " + targetDeleteFile.FullName, false);
                }
                else
                {
                    targetDirectoryInfo.DeleteFileOrDirectory(targetDeleteFile);
                    ShowMessage("Deleted file " + targetDeleteFile.FullName);
                }
            }
        }

        /// <summary>
        /// Rollback the target file if it differs from the source
        /// </summary>
        /// <param name="rollbackFile">Rollback file path</param>
        /// <param name="targetDirectoryInfo">Target directory info</param>
        /// <param name="targetDirectoryPath">Target directory path</param>
        /// <param name="fileUpdateCount">Number of files that have been updated (Input/output)</param>
        /// <param name="itemInUse">Used to track when a file or directory is in use by another process (log a message if the source and target files differ)</param>
        /// <param name="fileUsageMessage">Message to log when the file (or directory) is in use and the source and targets differ</param>
        private void ProcessRollbackFile(
            FileSystemInfo rollbackFile,
            DirectoryContainer targetDirectoryInfo,
            string targetDirectoryPath,
            ref int fileUpdateCount,
            eItemInUseConstants itemInUse = eItemInUseConstants.NotInUse,
            string fileUsageMessage = "")
        {

            var sourceFilePath = TrimSuffix(rollbackFile.FullName, ROLLBACK_SUFFIX);

            var sourceFile = new FileInfo(sourceFilePath);

            if (sourceFile.Exists)
            {
                var copied = CopyFileIfNeeded(sourceFile, targetDirectoryInfo, targetDirectoryPath, ref fileUpdateCount, eDateComparisonModeConstants.CopyIfSizeOrDateDiffers, itemInUse, fileUsageMessage);
                if (copied)
                {
                    var prefix = PreviewMode ? "Preview rollback of file " : "Rolled back file ";

                    var msg = string.Format("{0} {1} to version from {2} with size {3:0.0} KB",
                                            prefix, sourceFile.Name, sourceFile.LastWriteTime, sourceFile.Length / 1024.0);

                    ShowMessage(msg, !PreviewMode);
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
                return string.Compare(x?.ProcessName, y?.ProcessName, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
