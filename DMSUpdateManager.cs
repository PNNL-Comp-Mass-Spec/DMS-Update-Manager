using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Text;
using System.Threading;
using PRISM;
using PRISM.FileProcessor;

namespace DMSUpdateManager
{
    /// <summary>
    /// This program copies new and updated files from a source folder (master file folder)
    /// to a target folder
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
            mFileDate = "November 13, 2017";

            mFilesToIgnore = new SortedSet<string>(StringComparer.InvariantCultureIgnoreCase);
            mProcessesDict = new Dictionary<uint, ProcessInfo>();

            mExecutingExePath = Assembly.GetExecutingAssembly().Location;
            mExecutingExeName = Path.GetFileName(mExecutingExePath);

            InitializeLocalVariables();
        }

        #region "Constants and Enums"

        // Error codes specialized for this class
        public enum eDMSUpdateManagerErrorCodes
        {
            NoError = 0,
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
            FolderInUse = 2
        }

        public const string ROLLBACK_SUFFIX = ".rollback";
        public const string DELETE_SUFFIX = ".delete";
        public const string CHECK_JAVA_SUFFIX = ".checkjava";

        public const string PUSH_DIR_FLAG = "_PushDir_.txt";
        public const string PUSH_AM_SUBDIR_FLAG = "_AMSubDir_.txt";

        public const string DELETE_SUBDIR_FLAG = "_DeleteSubDir_.txt";
        public const string DELETE_AM_SUBDIR_FLAG = "_DeleteAMSubDir_.txt";

        #endregion

        #region "Classwide Variables"

        private bool mProcessesShown;

        /// <summary>
        /// Source folder path
        /// </summary>
        private string mSourceFolderPath;

        /// <summary>
        /// Target folder path
        /// </summary>
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

        private int mMinimumRepeatThresholdSeconds;
        private string mMutexNameSuffix;

        #endregion

        #region "Properties"

        /// <summary>
        /// When mCopySubdirectoriesToParentFolder=True, then will copy any subdirectories of the source folder into a subdirectory off the parent folder of the target folder
        /// </summary>
        /// <remarks>
        /// For example:
        ///   The .Exe resides at folder C:\DMS_Programs\AnalysisToolManager\DMSUpdateManager.exe
        ///   mSourceFolderPath = "\\gigasax\DMS_Programs\AnalysisToolManagerDistribution"
        ///   mTargetFolderPath = "."
        ///   Files are synced from "\\gigasax\DMS_Programs\AnalysisToolManagerDistribution" to "C:\DMS_Programs\AnalysisToolManager\"
        ///   Next, folder \\gigasax\DMS_Programs\AnalysisToolManagerDistribution\MASIC\ will get sync'd with ..\MASIC (but only if ..\MASIC exists)
        ///     Note that ..\MASIC is actually C:\DMS_Programs\MASIC\
        ///   When sync'ing the MASIC folders, will recursively sync additional folders that match
        ///   If the source folder contains file _PushDir_.txt or _AMSubDir_.txt then the directory will be copied to the target even if it doesn't exist there
        /// </remarks>
        public bool CopySubdirectoriesToParentFolder { get; set; }

        public double MutexWaitTimeoutMinutes { get; set; }

        public bool DoNotUseMutex { get; set; }

        public eDMSUpdateManagerErrorCodes LocalErrorCode { get; private set; }

        /// <summary>
        /// If False, then will not overwrite files in the target folder that are newer than files in the source folder
        /// </summary>
        public bool OverwriteNewerFiles { get; set; }

        /// <summary>
        /// When true, then messages will be displayed and logged showing the files that would be copied
        /// </summary>
        public bool PreviewMode { get; set; }

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

        private string ConstructMutexName(string mutexSuffix)
        {
            var appName = GetEntryOrExecutingAssembly().GetName().Name.ToLower();
            var parameterFileCleaned = mutexSuffix.Replace("\\", "_").Replace(":", "_").Replace(".", "_").Replace(" ", "_");
            var mutexName = @"Global\" + appName.ToLower() + "_" + parameterFileCleaned.ToLower();
            return mutexName;
        }

        /// <summary>
        /// Copy the file (or preview the copy)
        /// </summary>
        /// <param name="sourceFile">Source file</param>
        /// <param name="targetFile">Target file</param>
        /// <param name="fileUpdateCount">Total number of files updated (input/output)</param>
        /// <param name="copyReason">Reason for the copy</param>
        private void CopyFile(FileInfo sourceFile, FileInfo targetFile, ref int fileUpdateCount, string copyReason)
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

            var updatedFileInfo = "New: " + GetFileDateAndSize(sourceFile);

            if (PreviewMode)
            {
                ShowOldAndNewFileInfo("Preview: Update file: ", targetFile, existingFileInfo, updatedFileInfo, copyReason, true);
            }
            else
            {
                ShowOldAndNewFileInfo("Update file: ", targetFile, existingFileInfo, updatedFileInfo, copyReason, true);

                try
                {
                    var copiedFile = sourceFile.CopyTo(targetFile.FullName, true);

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
        /// <param name="targetFolderPath">Target folder</param>
        /// <param name="fileUpdateCount">Number of files that have been updated (Input/output)</param>
        /// <param name="eDateComparisonMode">Date comparison mode</param>
        /// <param name="itemInUse">Used to track when a file or folder is in use by another process (log a message if the source and target files differ)</param>
        /// <param name="fileUsageMessage">Message to log when the file (or folder) is in use and the source and targets differ</param>
        /// <returns>True if the file was updated, otherwise false</returns>
        /// <remarks></remarks>
        private bool CopyFileIfNeeded(
            FileInfo sourceFile,
            string targetFolderPath,
            ref int fileUpdateCount,
            eDateComparisonModeConstants eDateComparisonMode,
            eItemInUseConstants itemInUse = eItemInUseConstants.NotInUse,
            string fileUsageMessage = "")
        {
            var targetFilePath = Path.Combine(targetFolderPath, sourceFile.Name);
            var targetFile = new FileInfo(targetFilePath);

            var copyReason = string.Empty;
            var needToCopy = false;

            if (!targetFile.Exists)
            {
                // File not present in the target; copy it now
                copyReason = "not found in target folder";
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
                        // Update DMSUpdateManager.exe if it is not in the same folder as the starting folder
                        if (!string.Equals(targetFile.DirectoryName, mOutputFolderPath))
                        {
                            itemInUse = eItemInUseConstants.NotInUse;
                        }
                    }
                }

                if (itemInUse != eItemInUseConstants.NotInUse)
                {
                    // Do not update this file; it is in use (or another file in this folder is in use)
                    if (string.IsNullOrWhiteSpace(fileUsageMessage))
                    {
                        if (itemInUse == eItemInUseConstants.FolderInUse)
                        {
                            ShowMessage("Skipping " + sourceFile.Name + " because folder " + AbbreviatePath(targetFile.DirectoryName) + " is in use (by an unknown process)");
                        }
                        else
                        {
                            ShowMessage("Skipping " + sourceFile.Name + " in folder " + AbbreviatePath(targetFile.DirectoryName) + " because currently in use (by an unknown process)");
                        }
                    }
                    else
                    {
                        ShowMessage(fileUsageMessage);
                    }

                    return false;
                }
            }

            CopyFile(sourceFile, targetFile, ref fileUpdateCount, copyReason);
            return true;
        }

        private void InitializeLocalVariables()
        {
            ReThrowEvents = false;
            mLogFileUsesDateStamp = true;

            PreviewMode = false;
            OverwriteNewerFiles = false;
            CopySubdirectoriesToParentFolder = false;

            MutexWaitTimeoutMinutes = 5;
            DoNotUseMutex = false;
            mMutexNameSuffix = string.Empty;

            mMinimumRepeatThresholdSeconds = 30;

            mSourceFolderPath = string.Empty;
            mTargetFolderPath = string.Empty;

            mFilesToIgnore.Clear();
            mFilesToIgnore.Add(PUSH_DIR_FLAG);
            mFilesToIgnore.Add(PUSH_AM_SUBDIR_FLAG);
            mFilesToIgnore.Add(DELETE_SUBDIR_FLAG);
            mFilesToIgnore.Add(DELETE_AM_SUBDIR_FLAG);

            LocalErrorCode = eDMSUpdateManagerErrorCodes.NoError;

            var executingExePath = Assembly.GetExecutingAssembly().Location;
            var vsHostName = Path.ChangeExtension(mExecutingExeName, "vshost.exe").ToLower();

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

                // If the ExecutablePath is null/empty, then replace it with the CommandLine string
                // Otherwise, the next check with throw an exception when the ExecutablePath is null (at the .ToLower() part of line)
                // We could also set it to an empty string, but using the CommandLine for pessimistic purposes
                if (string.IsNullOrWhiteSpace(processPath))
                {
                    processPath = cmd;
                }

                // Skip this process if it is the active DMSUpdateManager, or DMSUpdateManager.vshost.exe or cmd.exe
                var exeLCase = Path.GetFileName(processPath).ToLower();
                if (processPath.Contains(executingExePath) || exeLCase == vsHostName || exeLCase == "cmd.exe")
                {
                    continue;
                }

                var newProcess = new ProcessInfo(processId, processPath, cmd);
                if (newProcess.FolderHierarchy.Count < 3)
                {
                    // Process running in the root folder or one below the root folder; ignore it
                    continue;
                }
                mProcessesDict.Add(processId, newProcess);
            }

            mProcessesMatchingTarget = new Dictionary<uint, string>();

            // Ignore checking for running processes in the first folder that we are updating
            mLastFolderProcessesChecked = Path.GetDirectoryName(executingExePath);
            mLastFolderRunningProcessPath = Path.GetFileName(executingExePath);
            mLastFolderRunningProcessId = 0;
        }

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

        private static string GetFileDateAndSize(FileInfo fileInfo)
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
                            ConsoleMsgUtils.ShowWarning("Other instance already running on target folder, waiting for finish before continuing...");
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
                    // If we don't have the mutex handle, don't try to update the parent folder
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
        /// Update files in folder strInputFolderPath
        /// </summary>
        /// <param name="inputFolderPath">Target folder to update</param>
        /// <param name="outputFolderAlternatePath">Ignored by this method</param>
        /// <param name="parameterFilePath">Parameter file defining the source folder path and other options</param>
        /// <param name="resetErrorCode">Ignored by this method</param>
        /// <returns>True if success, False if failure</returns>
        /// <remarks>If TargetFolder is defined in the parameter file, strInputFolderPath will be ignored</remarks>
        public override bool ProcessFolder(string inputFolderPath, string outputFolderAlternatePath, string parameterFilePath, bool resetErrorCode)
        {
            return UpdateFolder(inputFolderPath, parameterFilePath);
        }

        private void ShowOldAndNewFileInfo(
            string messagePrefix,
            FileSystemInfo targetFile,
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
        /// Update files in folder targetFolderPath
        /// </summary>
        /// <param name="targetFolderPath">Target folder to update</param>
        /// <param name="parameterFilePath">Parameter file defining the source folder path and other options</param>
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
                    ShowWarning("Source folder path is not defined; either specify it at the command line or include it in the parameter file");
                    SetBaseClassErrorCode(eProcessFoldersErrorCodes.InvalidInputFolderPath);
                    return false;
                }

                if (string.IsNullOrWhiteSpace(targetFolderPath))
                {
                    ShowWarning("Target folder path is not defined; either specify it at the command line or include it in the parameter file");
                    SetBaseClassErrorCode(eProcessFoldersErrorCodes.InvalidInputFolderPath);
                    return false;
                }

                // Note that CleanupFilePaths() will update mOutputFolderPath, which is used by LogMessage()
                var tempStr = string.Empty;
                if (!CleanupFolderPaths(ref targetFolderPath, ref tempStr))
                {
                    SetBaseClassErrorCode(eProcessFoldersErrorCodes.FilePathError);
                    return false;
                }

                if (DoNotUseMutex)
                {
                    return UpdateFolderRun(targetFolderPath, parameterFilePath);
                }

                return UpdateFolderMutexWrapped(targetFolderPath, parameterFilePath);
            }
            catch (Exception ex)
            {
                HandleException("Error in UpdateFolder: " + ex.Message, ex);
                return false;
            }
        }

        private bool UpdateFolderMutexWrapped(string targetFolderPath, string parameterFilePath)
        {
            // Check a global mutex keyed on the parameter file path; if it returns false, exit
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

                return UpdateFolderRun(targetFolderPath, parameterFilePath, doNotUpdateParent);
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

        private bool UpdateFolderCopyToParentMutexWrapped(DirectoryInfo diTargetFolder, DirectoryInfo diSourceFolder, string parameterFilePath)
        {
            // Check a global mutex keyed on the parameter file path; if it returns false, exit
            Mutex mutex = null;
            var hasMutexHandle = false;

            try
            {
                var doNotUpdateParent = false;
                var targetFolderParent = diTargetFolder.Parent?.FullName;
                if (!string.IsNullOrWhiteSpace(targetFolderParent))
                {
                    var mutexName = ConstructMutexName(targetFolderParent);

                    hasMutexHandle = GetMutex(mutexName, true, out mutex, out doNotUpdateParent);
                }

                if (!doNotUpdateParent)
                {
                    return UpdateFolderCopyToParentRun(diTargetFolder, diSourceFolder, parameterFilePath);
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

        private bool UpdateFolderRun(string targetFolderPath, string parameterFilePath, bool doNotUpdateParent = false)
        {
            var sourceFolder = new DirectoryInfo(mSourceFolderPath);
            var targetFolder = new DirectoryInfo(targetFolderPath);

            if (sourceFolder.Parent == null)
            {
                OnErrorEvent("Unable to determine the parent directory of the source folder: " + sourceFolder.FullName);
                return false;
            }

            if (targetFolder.Parent == null)
            {
                OnErrorEvent("Unable to determine the parent directory of the target folder: " + targetFolder.FullName);
                return false;
            }

            mTargetFolderPathBase = targetFolder.Parent.FullName;

            ResetProgress();

            var success = UpdateFolderWork(sourceFolder.FullName, targetFolder.FullName, pushNewSubfolders: false);

            if (!CopySubdirectoriesToParentFolder || doNotUpdateParent)
                return success;

            if (DoNotUseMutex)
            {
                success = UpdateFolderCopyToParentRun(targetFolder, sourceFolder, parameterFilePath);
            }
            else
            {
                success = UpdateFolderCopyToParentMutexWrapped(targetFolder, sourceFolder, parameterFilePath);
            }

            return success;
        }

        private bool UpdateFolderCopyToParentRun(DirectoryInfo targetFolder, DirectoryInfo sourceFolder, string parameterFilePath)
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
                        // Reduce hits on the source: not enough time has passed since the last update
                        // Delay the output so that important log messages about bad parameters will be output regardless of this
                        OnWarningEvent(
                            string.Format("Exiting update since last update ran {0:N0} seconds ago; update is allowed in {1:N0} seconds",
                                          currentTime.Subtract(lastUpdate).TotalSeconds,
                                          nextAllowedUpdate.Subtract(currentTime).TotalSeconds));

                        skipShared = true;
                    }
                }
            }

            if (!skipShared)
            {
                // Update the check file's date
                TouchCheckFile(checkFilePath);

                success = UpdateFolderCopyToParent(targetFolder, sourceFolder);

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

        private bool UpdateFolderCopyToParent(DirectoryInfo targetFolder, DirectoryInfo sourceFolder)
        {
            var successOverall = true;

            var parentFolder = targetFolder.Parent;
            if (parentFolder == null)
            {
                OnWarningEvent("Unable to determine the parent directory of " + targetFolder.FullName);
                return false;
            }

            foreach (var sourceSubFolder in sourceFolder.GetDirectories())
            {

                // The target folder is treated as a subdirectory of the parent folder
                var targetSubFolderPath = Path.Combine(parentFolder.FullName, sourceSubFolder.Name);

                // Initially assume we'll process this folder if it exists at the target
                var targetSubFolder = new DirectoryInfo(targetSubFolderPath);
                var processSubfolder = targetSubFolder.Exists;

                if (processSubfolder && sourceSubFolder.GetFiles(DELETE_SUBDIR_FLAG).Length > 0)
                {
                    // Remove this subfolder (but only if it's empty)
                    var folderDeleted = DeleteSubFolder(sourceSubFolder, targetSubFolder, "parent subfolder", DELETE_SUBDIR_FLAG);
                    if (folderDeleted)
                        processSubfolder = false;
                }

                if (sourceSubFolder.GetFiles(DELETE_AM_SUBDIR_FLAG).Length > 0)
                {
                    // Remove this subfolder (but only if it's empty)
                    var analysisMgrSubDir = new DirectoryInfo(Path.Combine(targetFolder.FullName, sourceSubFolder.Name));
                    var folderDeleted = DeleteSubFolder(sourceSubFolder, analysisMgrSubDir, "subfolder", DELETE_AM_SUBDIR_FLAG);
                    if (folderDeleted)
                        processSubfolder = false;
                }

                if (sourceSubFolder.GetFiles(PUSH_AM_SUBDIR_FLAG).Length > 0)
                {
                    // Push this folder as a subdirectory of the target folder, not as a subdirectory of the parent folder
                    targetSubFolderPath = Path.Combine(targetFolder.FullName, sourceSubFolder.Name);
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
                    var success = UpdateFolderWork(sourceSubFolder.FullName, targetSubFolderPath, pushNewSubfolders: true);
                    if (!success)
                        successOverall = false;
                }
            }

            return successOverall;
        }

        private bool DeleteSubFolder(FileSystemInfo sourceSubFolder, DirectoryInfo targetSubFolder, string folderDescription, string deleteFlag)
        {
            if (string.IsNullOrWhiteSpace(folderDescription))
            {
                folderDescription = "folder";
            }

            if (!targetSubFolder.Exists)
                return false;

            var fileCount = targetSubFolder.GetFiles().Length;
            var folders = targetSubFolder.GetDirectories().ToList();

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
                        DeleteSubFolder(newSourceSubDir, folder, folderDescription, deleteFlag);
                    }
                }

                // Refresh the subdirectories
                var folderCount = targetSubFolder.GetDirectories().Length;

                if (folderCount > 0)
                {
                    ShowWarning(
                        "Folder flagged for deletion, but it is not empty (Folder Count = " + folderCount + "): " +
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
                    targetSubFolder.Delete(false);
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

        private bool UpdateFolderWork(string sourceFolderPath, string targetFolderPath, bool pushNewSubfolders)
        {

            ShowMessage("Updating " + AbbreviatePath(targetFolderPath), false, eMessageType: eMessageTypeConstants.Debug);

            // Make sure the target folder exists
            var targetFolder = new DirectoryInfo(targetFolderPath);
            if (!targetFolder.Exists)
            {
                targetFolder.Create();
            }

            // Obtain a list of files in the source folder
            var sourceFolder = new DirectoryInfo(sourceFolderPath);

            var fileUpdateCount = 0;

            var filesInSource = sourceFolder.GetFiles();

            // Populate a List object the with the names of any .delete files in fiFilesInSource
            var deleteFiles = new SortedSet<string>(StringComparer.InvariantCultureIgnoreCase);
            var filesToDelete = (from sourceFile in filesInSource where sourceFile.Name.EndsWith(DELETE_SUFFIX, StringComparison.InvariantCultureIgnoreCase) select TrimSuffix(sourceFile.Name, DELETE_SUFFIX).ToLower());
            foreach (var item in filesToDelete)
            {
                deleteFiles.Add(item);
            }

            // Populate a List object the with the names of any .checkjava files in fiFilesInSource
            var checkJavaFiles = new SortedSet<string>(StringComparer.InvariantCultureIgnoreCase);
            var javaFilesToCheck = (from sourceFile in filesInSource where sourceFile.Name.EndsWith(CHECK_JAVA_SUFFIX, StringComparison.InvariantCultureIgnoreCase) select TrimSuffix(sourceFile.Name, CHECK_JAVA_SUFFIX).ToLower());
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
                                if (TargetFolderInUseByProcess(targetFolder.FullName, targetFileName, out fileUsageMessage))
                                {
                                    // The folder is in use
                                    // Allow new files to be copied, but do not overwrite existing files
                                    itemInUse = eItemInUseConstants.FolderInUse;
                                }
                            }

                            ProcessRollbackFile(sourceFile, targetFolder.FullName, ref fileUpdateCount, itemInUse, fileUsageMessage);
                            break; // Break out of the while, continue the for loop
                        }

                        if (sourceFile.Name.EndsWith(DELETE_SUFFIX, StringComparison.InvariantCultureIgnoreCase))
                        {
                            // This is a Delete file
                            // Do not copy this file
                            // However, do look for a corresponding file that does not have .delete and delete that file in the target folder

                            ProcessDeleteFile(sourceFile, targetFolder.FullName);
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

                        if (checkJavaFiles.Contains(fileNameLCase))
                        {
                            if (JarFileInUseByJava(sourceFile, out fileUsageMessage))
                            {
                                itemInUse = eItemInUseConstants.ItemInUse;
                            }
                        }
                        else
                        {
                            if (TargetFolderInUseByProcess(targetFolder.FullName, sourceFile.Name, out fileUsageMessage))
                            {
                                // The folder is in use
                                // Allow new files to be copied, but do not overwrite existing files
                                itemInUse = eItemInUseConstants.FolderInUse;
                            }
                        }

                        CopyFileIfNeeded(sourceFile, targetFolder.FullName, ref fileUpdateCount, eDateComparisonMode, itemInUse, fileUsageMessage);

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
                        Thread.Sleep(100);
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

            // Process each subdirectory in the source folder
            // If the folder exists at the target, copy it
            // Additionally, if the source folder contains file _PushDir_.txt, it gets copied even if it doesn't exist at the target
            foreach (var sourceSubFolder in sourceFolder.GetDirectories())
            {
                var targetSubFolderPath = Path.Combine(targetFolder.FullName, sourceSubFolder.Name);

                // Initially assume we'll process this folder if it exists at the target
                var targetSubFolder = new DirectoryInfo(targetSubFolderPath);
                var processSubfolder = targetSubFolder.Exists;

                if (processSubfolder && sourceSubFolder.GetFiles(DELETE_SUBDIR_FLAG).Length > 0)
                {
                    // Remove this subfolder (but only if it's empty)
                    var folderDeleted = DeleteSubFolder(sourceSubFolder, targetSubFolder, "subfolder", DELETE_SUBDIR_FLAG);
                    if (folderDeleted)
                        processSubfolder = false;
                }

                if (pushNewSubfolders && sourceSubFolder.GetFiles(PUSH_DIR_FLAG).Length > 0)
                {
                    processSubfolder = true;
                }

                if (processSubfolder)
                {
                    UpdateFolderWork(sourceSubFolder.FullName, targetSubFolder.FullName, pushNewSubfolders);
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
                OnWarningEvent("Empty target folder path passed to TargetFolderInUseByProcess");
                return false;
            }

            try
            {
                var processCount = GetNumTargetFolderProcesses(targetFolderPath, out var firstProcessPath, out var firstProcessId);

                if (processCount > 0)
                {
                    // Example log messages:
                    // Skipping UIMFLibrary.dll because folder DeconTools is in use by process DeconConsole.exe (PID 343243)
                    // Skipping DeconConsole.exe because folder DeconTools is in use by 2 processes on this system, including DeconConsole.exe (PID 343243)

                    folderUsageMessage = "Skipping " + targetFileName + " because folder " + AbbreviatePath(targetFolderPath) + " is in use by ";

                    string processPathToShow;

                    if (string.IsNullOrWhiteSpace(firstProcessPath))
                    {
                        processPathToShow = "an unknown process";
                    }
                    else
                    {
                        var processFile = new FileInfo(firstProcessPath);
                        var processIdAppend = " (" + " PID " + firstProcessId + ")";

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
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error looking for processes using files in " + targetFolderPath + ": " + ex.Message);
            }

            return false;
        }

        /// <summary>
        /// Determine the number of processes using files in the given folder
        /// </summary>
        /// <param name="targetFolderPath">Folder to examine</param>
        /// <param name="firstProcessPath">Output parameter: first process using files in this folder; empty string if no processes</param>
        /// <param name="firstProcessId">Output parameter: Process ID of first process using files in this folder</param>
        /// <returns>Count of processes using this folder</returns>
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

            // Look for running processes with a .exe in the target folder (or in a parent of the target folder)
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

        private void ProcessDeleteFile(FileSystemInfo deleteFile, string targetFolderPath)
        {
            var targetFilePath = Path.Combine(targetFolderPath, TrimSuffix(deleteFile.Name, DELETE_SUFFIX));
            var targetFile = new FileInfo(targetFilePath);

            if (targetFile.Exists)
            {
                if (PreviewMode)
                {
                    ShowMessage("Preview delete: " + targetFile.FullName);
                }
                else
                {
                    targetFile.Delete();
                    ShowMessage("Deleted file " + targetFile.FullName);
                }
            }

            // Make sure the .delete is also not in the target folder
            var targetDeleteFilePath = Path.Combine(targetFolderPath, deleteFile.Name);
            var targetDeleteFile = new FileInfo(targetDeleteFilePath);

            if (targetDeleteFile.Exists)
            {
                if (PreviewMode)
                {
                    ShowMessage("Preview delete: " + targetDeleteFile.FullName);
                }
                else
                {
                    targetDeleteFile.Delete();
                    ShowMessage("Deleted file " + targetDeleteFile.FullName);
                }
            }
        }

        /// <summary>
        /// Rollback the target file if it differs from the source
        /// </summary>
        /// <param name="rollbackFile">Rollback file path</param>
        /// <param name="targetFolderPath">Target folder</param>
        /// <param name="fileUpdateCount">Number of files that have been updated (Input/output)</param>
        /// <param name="itemInUse">Used to track when a file or folder is in use by another process (log a message if the source and target files differ)</param>
        /// <param name="fileUsageMessage">Message to log when the file (or folder) is in use and the source and targets differ</param>
        private void ProcessRollbackFile(
            FileSystemInfo rollbackFile,
            string targetFolderPath,
            ref int fileUpdateCount,
            eItemInUseConstants itemInUse = eItemInUseConstants.NotInUse,
            string fileUsageMessage = "")
        {
            var sourceFilePath = TrimSuffix(rollbackFile.FullName, ROLLBACK_SUFFIX);

            var sourceFile = new FileInfo(sourceFilePath);

            if (sourceFile.Exists)
            {
                var copied = CopyFileIfNeeded(sourceFile, targetFolderPath, ref fileUpdateCount, eDateComparisonModeConstants.CopyIfSizeOrDateDiffers, itemInUse, fileUsageMessage);
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
