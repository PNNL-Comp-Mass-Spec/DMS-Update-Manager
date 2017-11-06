﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Text;
using System.Threading;

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
    public class clsDMSUpdateManager : clsProcessFoldersBaseClass
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public clsDMSUpdateManager()
        {
            mFileDate = "April 7, 2017";

            mFilesToIgnore = new SortedSet<string>(StringComparer.InvariantCultureIgnoreCase);
            mProcessesDict = new Dictionary<UInt32, clsProcessInfo>();

            mExecutingExePath = Assembly.GetExecutingAssembly().Location;
            mExecutingExeName = Path.GetFileName(mExecutingExePath);

            InitializeLocalVariables();
        }

        #region "Constants and Enums"

        // Error codes specialized for this class
        public enum eDMSUpdateManagerErrorCodes : int
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
            itemInUse = 1,
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

        #region "Structures"

        #endregion

        #region "Classwide Variables"

        // When true, then messages will be displayed and logged showing the files that would be copied

        private bool mPreviewMode;

        private bool mProcessesShown = false;
        // If False, then will not overwrite files in the target folder that are newer than files in the source folder

        private bool mOverwriteNewerFiles;
        // When mCopySubdirectoriesToParentFolder=True, then will copy any subdirectories of the source folder into a subdirectory off the parent folder of the target folder
        // For example:
        //   The .Exe resides at folder C:\DMS_Programs\AnalysisToolManager\DMSUpdateManager.exe
        //   mSourceFolderPath = "\\gigasax\DMS_Programs\AnalysisToolManagerDistribution"
        //   mTargetFolderPath = "."
        //   Files are synced from "\\gigasax\DMS_Programs\AnalysisToolManagerDistribution" to "C:\DMS_Programs\AnalysisToolManager\"
        //   Next, folder \\gigasax\DMS_Programs\AnalysisToolManagerDistribution\MASIC\ will get sync'd with ..\MASIC (but only if ..\MASIC exists)
        //     Note that ..\MASIC is actually C:\DMS_Programs\MASIC\
        //   When sync'ing the MASIC folders, will recursively sync additional folders that match
        //   If the source folder contains file _PushDir_.txt or _AMSubDir_.txt then the directory will be copied to the target even if it doesn't exist there

        private bool mCopySubdirectoriesToParentFolder;
        // The following is the path that lists the files that will be copied to the target folder
        private string mSourceFolderPath;

        private string mTargetFolderPath;
        // List of files that will not be copied
        // The names must be full filenames (no wildcards)

        private readonly SortedSet<string> mFilesToIgnore;

        private eDMSUpdateManagerErrorCodes mLocalErrorCode;
        private readonly string mExecutingExeName;

        private readonly string mExecutingExePath;
        // Store the results of the WMI query getting running processes with command line data
        // Keys are Process ID
        // Values are clsProcessInfo

        private readonly Dictionary<UInt32, clsProcessInfo> mProcessesDict;
        // Keys are process ID
        // Values are the full command line for the process

        private Dictionary<UInt32, string> mProcessesMatchingTarget;
        private string mLastFolderProcessesChecked;
        private string mLastFolderRunningProcessPath;

        private UInt32 mLastFolderRunningProcessId;
        private string mSourceFolderPathBase;

        private string mTargetFolderPathBase;
        #endregion

        #region "Properties"

        public bool CopySubdirectoriesToParentFolder
        {
            get { return mCopySubdirectoriesToParentFolder; }
            set { mCopySubdirectoriesToParentFolder = value; }
        }

        public eDMSUpdateManagerErrorCodes LocalErrorCode
        {
            get { return mLocalErrorCode; }
        }

        public bool OverwriteNewerFiles
        {
            get { return mOverwriteNewerFiles; }
            set { mOverwriteNewerFiles = value; }
        }

        public bool PreviewMode
        {
            get { return mPreviewMode; }
            set { mPreviewMode = value; }
        }

        public string SourceFolderPath
        {
            get
            {
                if (mSourceFolderPath == null)
                {
                    return string.Empty;
                }
                else
                {
                    return mSourceFolderPath;
                }
            }
            set
            {
                if ((value != null))
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
                else
                {
                    return ".";
                }
            }

            return fileOrFolderPath;
        }

        /// <summary>
        /// Add a file to ignore from processing
        /// </summary>
        /// <param name="strFileName">Full filename (no wildcards)</param>
        public void AddFileToIgnore(string strFileName)
        {
            if (!string.IsNullOrWhiteSpace(strFileName))
            {
                if (!mFilesToIgnore.Contains(strFileName))
                {
                    mFilesToIgnore.Add(strFileName);
                }
            }
        }

        /// <summary>
        /// Copy the file (or preview the copy)
        /// </summary>
        /// <param name="fiSourceFile">Source file</param>
        /// <param name="fiTargetFile">Target file</param>
        /// <param name="fileUpdateCount">Total number of files updated (input/output)</param>
        /// <param name="strCopyReason">Reason for the copy</param>
        private void CopyFile(FileInfo fiSourceFile, FileInfo fiTargetFile, ref int fileUpdateCount, string strCopyReason)
        {
            string existingFileInfo = null;

            if (fiTargetFile.Exists)
            {
                existingFileInfo = "Old: " + GetFileDateAndSize(fiTargetFile);
            }
            else
            {
                existingFileInfo = string.Empty;
            }

            var updatedFileInfo = "New: " + GetFileDateAndSize(fiSourceFile);

            if (mPreviewMode)
            {
                ShowOldAndNewFileInfo("Preview: Update file: ", fiSourceFile, fiTargetFile, existingFileInfo, updatedFileInfo, strCopyReason, true);
            }
            else
            {
                ShowOldAndNewFileInfo("Update file: ", fiSourceFile, fiTargetFile, existingFileInfo, updatedFileInfo, strCopyReason, true);

                try
                {
                    var fiCopiedFile = fiSourceFile.CopyTo(fiTargetFile.FullName, true);

                    if (fiCopiedFile.Length != fiSourceFile.Length)
                    {
                        ShowErrorMessage("Copy of " + fiSourceFile.Name + " failed; sizes differ", true);
                    }
                    else if (fiCopiedFile.LastWriteTimeUtc != fiSourceFile.LastWriteTimeUtc)
                    {
                        ShowErrorMessage("Copy of " + fiSourceFile.Name + " failed; modification times differ", true);
                    }
                    else
                    {
                        fileUpdateCount += 1;
                    }
                }
                catch (Exception ex)
                {
                    ShowErrorMessage("Error copying " + fiSourceFile.Name + ": " + ex.Message, true);
                }
            }
        }

        /// <summary>
        /// Compare the source file to the target file and update it if they differ
        /// </summary>
        /// <param name="fiSourceFile">Source file</param>
        /// <param name="strTargetFolderPath">Target folder</param>
        /// <param name="fileUpdateCount">Number of files that have been updated (Input/output)</param>
        /// <param name="eDateComparisonMode">Date comparison mode</param>
        /// <param name="blnProcessingSubFolder">True if processing a subfolder</param>
        /// <param name="itemInUse">Used to track when a file or folder is in use by another process (log a message if the source and target files differ)</param>
        /// <param name="fileUsageMessage">Message to log when the file (or folder) is in use and the source and targets differ</param>
        /// <returns>True if the file was updated, otherwise false</returns>
        /// <remarks></remarks>
        private bool CopyFileIfNeeded(FileInfo fiSourceFile, string strTargetFolderPath, ref int fileUpdateCount, eDateComparisonModeConstants eDateComparisonMode, bool blnProcessingSubFolder, eItemInUseConstants itemInUse = eItemInUseConstants.NotInUse, string fileUsageMessage = "")
        {
            var strTargetFilePath = Path.Combine(strTargetFolderPath, fiSourceFile.Name);
            var fiTargetFile = new FileInfo(strTargetFilePath);

            var strCopyReason = string.Empty;
            var blnNeedToCopy = false;

            if (!fiTargetFile.Exists)
            {
                // File not present in the target; copy it now
                strCopyReason = "not found in target folder";
                blnNeedToCopy = true;
            }
            else
            {
                // File is present, see if the file has a different size

                if (eDateComparisonMode == eDateComparisonModeConstants.CopyIfSizeOrDateDiffers)
                {
                    if (fiTargetFile.Length != fiSourceFile.Length)
                    {
                        blnNeedToCopy = true;
                        strCopyReason = "sizes are different";
                    }
                    else if (fiSourceFile.LastWriteTimeUtc != fiTargetFile.LastWriteTimeUtc)
                    {
                        blnNeedToCopy = true;
                        strCopyReason = "dates are different";
                    }
                }
                else
                {
                    if (fiTargetFile.Length != fiSourceFile.Length)
                    {
                        blnNeedToCopy = true;
                        strCopyReason = "sizes are different";
                    }
                    else if (fiSourceFile.LastWriteTimeUtc > fiTargetFile.LastWriteTimeUtc)
                    {
                        blnNeedToCopy = true;
                        strCopyReason = "source file is newer";
                    }

                    if (blnNeedToCopy && eDateComparisonMode == eDateComparisonModeConstants.RetainNewerTargetIfDifferentSize)
                    {
                        if (fiTargetFile.LastWriteTimeUtc > fiSourceFile.LastWriteTimeUtc)
                        {
                            // Target file is newer than the source; do not overwrite

                            var strWarning = "Warning: Skipping file " + fiSourceFile.Name;
                            if (blnProcessingSubFolder)
                            {
                                strWarning += " in " + strTargetFolderPath;
                            }
                            strWarning += " since a newer version exists in the target; source=" + fiSourceFile.LastWriteTimeUtc.ToLocalTime() + ", target=" + fiTargetFile.LastWriteTimeUtc.ToLocalTime();

                            ShowMessage(strWarning, intDuplicateHoldoffHours: 24);
                            blnNeedToCopy = false;
                        }
                    }
                }
            }

            if (blnNeedToCopy)
            {
                if (fiTargetFile.Exists)
                {
                    if (string.Equals(fiTargetFile.FullName, mExecutingExePath))
                    {
                        ShowMessage("Skipping " + fiTargetFile.FullName + "; cannot update the currently running copy of the DMSUpdateManager");
                        return false;
                    }

                    if (itemInUse != eItemInUseConstants.NotInUse)
                    {
                        if (fiTargetFile.Name == mExecutingExeName)
                        {
                            // Update DMSUpdateManager.exe if it is not in the same folder as the starting folder
                            if (!string.Equals(fiTargetFile.DirectoryName, mOutputFolderPath))
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
                                ShowMessage("Skipping " + fiSourceFile.Name + " because folder " + AbbreviatePath(fiTargetFile.DirectoryName) + " is in use (by an unknown process)");
                            }
                            else
                            {
                                ShowMessage("Skipping " + fiSourceFile.Name + " in folder " + AbbreviatePath(fiTargetFile.DirectoryName) + " because currently in use (by an unknown process)");
                            }
                        }
                        else
                        {
                            ShowMessage(fileUsageMessage);
                        }

                        return false;
                    }
                }

                CopyFile(fiSourceFile, fiTargetFile, ref fileUpdateCount, strCopyReason);
                return true;
            }
            else
            {
                return false;
            }
        }

        private void InitializeLocalVariables()
        {
            base.ShowMessages = false;
            base.mLogFileUsesDateStamp = false;

            mPreviewMode = false;
            mOverwriteNewerFiles = false;
            mCopySubdirectoriesToParentFolder = false;

            mSourceFolderPath = string.Empty;
            mTargetFolderPath = string.Empty;

            mFilesToIgnore.Clear();
            mFilesToIgnore.Add(PUSH_DIR_FLAG);
            mFilesToIgnore.Add(PUSH_AM_SUBDIR_FLAG);
            mFilesToIgnore.Add(DELETE_SUBDIR_FLAG);
            mFilesToIgnore.Add(DELETE_AM_SUBDIR_FLAG);

            mLocalErrorCode = eDMSUpdateManagerErrorCodes.NoError;

            string executingExePath = Assembly.GetExecutingAssembly().Location;
            var vsHostName = Path.ChangeExtension(mExecutingExeName, "vshost.exe").ToLower();

            mProcessesDict.Clear();
            var results = new ManagementObjectSearcher("SELECT ProcessId, ExecutablePath, CommandLine FROM Win32_Process");

            foreach (var item in results.Get())
            {
                var processId = (UInt32) item["ProcessId"];
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

                var newProcess = new clsProcessInfo(processId, processPath, cmd);
                if (newProcess.FolderHierarchy.Count < 3)
                {
                    // Process running in the root folder or one below the root folder; ignore it
                    continue;
                }
                mProcessesDict.Add(processId, newProcess);
            }

            mProcessesMatchingTarget = new Dictionary<UInt32, string>();

            // Ignore checking for running processes in the first folder that we are updating
            mLastFolderProcessesChecked = Path.GetDirectoryName(executingExePath);
            mLastFolderRunningProcessPath = Path.GetFileName(executingExePath);
            mLastFolderRunningProcessId = 0;
        }

        public override string GetErrorMessage()
        {
            // Returns an empty string if no error

            string strErrorMessage = null;

            if (base.ErrorCode == eProcessFoldersErrorCodes.LocalizedError | base.ErrorCode == eProcessFoldersErrorCodes.NoError)
            {
                switch (mLocalErrorCode)
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
                strErrorMessage = base.GetBaseClassErrorMessage();
            }

            return strErrorMessage;
        }

        private static string GetFileDateAndSize(FileInfo fiFileInfo)
        {
            return fiFileInfo.LastWriteTimeUtc.ToString("yyyy-MM-dd hh:mm:ss tt") + " and " + fiFileInfo.Length + " bytes";
        }

        private bool LoadParameterFileSettings(string strParameterFilePath)
        {
            const string OPTIONS_SECTION = "DMSUpdateManager";

            var objSettingsFile = new XmlSettingsFileAccessor();

            try
            {
                if (strParameterFilePath == null || strParameterFilePath.Length == 0)
                {
                    // No parameter file specified; nothing to load
                    return true;
                }

                if (!File.Exists(strParameterFilePath))
                {
                    // See if strParameterFilePath points to a file in the same directory as the application
                    strParameterFilePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), Path.GetFileName(strParameterFilePath));
                    if (!File.Exists(strParameterFilePath))
                    {
                        base.SetBaseClassErrorCode(eProcessFoldersErrorCodes.ParameterFileNotFound);
                        return false;
                    }
                }

                if (objSettingsFile.LoadSettings(strParameterFilePath))
                {
                    if (!objSettingsFile.SectionPresent(OPTIONS_SECTION))
                    {
                        ShowErrorMessage("The node '<section name=\"" + OPTIONS_SECTION + "\"> was not found in the parameter file: " + strParameterFilePath);
                        base.SetBaseClassErrorCode(eProcessFoldersErrorCodes.InvalidParameterFile);
                        return false;
                    }
                    else
                    {
                        if (objSettingsFile.GetParam(OPTIONS_SECTION, "LogMessages", false))
                        {
                            base.LogMessagesToFile = true;
                        }

                        mOverwriteNewerFiles = objSettingsFile.GetParam(OPTIONS_SECTION, "OverwriteNewerFiles", mOverwriteNewerFiles);
                        mCopySubdirectoriesToParentFolder = objSettingsFile.GetParam(OPTIONS_SECTION, "CopySubdirectoriesToParentFolder", mCopySubdirectoriesToParentFolder);

                        mSourceFolderPath = objSettingsFile.GetParam(OPTIONS_SECTION, "SourceFolderPath", mSourceFolderPath);
                        mTargetFolderPath = objSettingsFile.GetParam(OPTIONS_SECTION, "TargetFolderPath", mTargetFolderPath);

                        var strFilesToIgnore = objSettingsFile.GetParam(OPTIONS_SECTION, "FilesToIgnore", string.Empty);
                        try
                        {
                            if (strFilesToIgnore.Length > 0)
                            {
                                var strIgnoreList = strFilesToIgnore.Split(',');

                                foreach (string strFile in strIgnoreList)
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
        /// <param name="strInputFolderPath">Target folder to update</param>
        /// <param name="strOutputFolderAlternatePath">Ignored by this function</param>
        /// <param name="strParameterFilePath">Parameter file defining the source folder path and other options</param>
        /// <param name="blnResetErrorCode">Ignored by this function</param>
        /// <returns>True if success, False if failure</returns>
        /// <remarks>If TargetFolder is defined in the parameter file, strInputFolderPath will be ignored</remarks>
        public override bool ProcessFolder(string strInputFolderPath, string strOutputFolderAlternatePath, string strParameterFilePath, bool blnResetErrorCode)
        {
            return UpdateFolder(strInputFolderPath, strParameterFilePath);
        }

        private void ShowOldAndNewFileInfo(string messagePrefix, FileInfo fiSourceFile, FileInfo fiTargetFile, string existingFileInfo, string updatedFileInfo, string strCopyReason, bool logToFile)
        {
            var spacePad = new string(' ', messagePrefix.Length);

            ShowMessage(messagePrefix + fiSourceFile.Name + "; " + strCopyReason, logToFile);
            if (fiTargetFile.Exists)
            {
                ShowMessage(spacePad + existingFileInfo);
            }
            ShowMessage(spacePad + updatedFileInfo);
        }

        /// <summary>
        /// Update files in folder targetFolderPath
        /// </summary>
        /// <param name="targetFolderPath">Target folder to update</param>
        /// <param name="strParameterFilePath">Parameter file defining the source folder path and other options</param>
        /// <returns>True if success, False if failure</returns>
        /// <remarks>If TargetFolder is defined in the parameter file, targetFolderPath will be ignored</remarks>
        public bool UpdateFolder(string targetFolderPath, string strParameterFilePath)
        {
            SetLocalErrorCode(eDMSUpdateManagerErrorCodes.NoError);

            if ((targetFolderPath != null) && targetFolderPath.Length > 0)
            {
                // Update mTargetFolderPath using targetFolderPath
                // Note: If TargetFolder is defined in the parameter file, this value will get overridden
                mTargetFolderPath = string.Copy(targetFolderPath);
            }

            if (!LoadParameterFileSettings(strParameterFilePath))
            {
                ShowErrorMessage("Parameter file load error: " + strParameterFilePath);

                if (base.ErrorCode == eProcessFoldersErrorCodes.NoError)
                {
                    base.SetBaseClassErrorCode(eProcessFoldersErrorCodes.InvalidParameterFile);
                }
                return false;
            }

            try
            {
                targetFolderPath = string.Copy(mTargetFolderPath);

                if (mSourceFolderPath == null || mSourceFolderPath.Length == 0)
                {
                    ShowMessage("Source folder path is not defined.  Either specify it at the command line or include it in the parameter file.");
                    base.SetBaseClassErrorCode(eProcessFoldersErrorCodes.InvalidInputFolderPath);
                    return false;
                }

                if (string.IsNullOrWhiteSpace(targetFolderPath))
                {
                    ShowMessage("Target folder path is not defined.  Either specify it at the command line or include it in the parameter file.");
                    base.SetBaseClassErrorCode(eProcessFoldersErrorCodes.InvalidInputFolderPath);
                    return false;
                }

                // Note that CleanupFilePaths() will update mOutputFolderPath, which is used by LogMessage()
                var tempstr = string.Empty;
                if (!CleanupFolderPaths(ref targetFolderPath, ref tempstr))
                {
                    base.SetBaseClassErrorCode(eProcessFoldersErrorCodes.FilePathError);
                    return false;
                }

                var diSourceFolder = new DirectoryInfo(mSourceFolderPath);
                var diTargetFolder = new DirectoryInfo(targetFolderPath);

                mSourceFolderPathBase = diSourceFolder.Parent.FullName;
                mTargetFolderPathBase = diTargetFolder.Parent.FullName;

                base.mProgressStepDescription = "Updating " + diTargetFolder.Name + "\n" + " using " + diSourceFolder.FullName;
                base.ResetProgress();

                ShowMessage("Updating " + diTargetFolder.FullName + "\n" + " using " + diSourceFolder.FullName, false);

                var success = UpdateFolderWork(diSourceFolder.FullName, diTargetFolder.FullName, blnPushNewSubfolders: false, blnProcessingSubFolder: false);

                if (mCopySubdirectoriesToParentFolder)
                {
                    success = UpdateFolderCopyToParent(diTargetFolder, diSourceFolder);
                }

                return success;
            }
            catch (Exception ex)
            {
                HandleException("Error in UpdateFolder: " + ex.Message, ex);
                return false;
            }
        }

        private bool UpdateFolderCopyToParent(DirectoryInfo diTargetFolder, DirectoryInfo diSourceFolder)
        {
            var successOverall = true;

            foreach (DirectoryInfo diSourceSubFolder in diSourceFolder.GetDirectories())
            {
                // The target folder is treated as a subdirectory of the parent folder
                var strTargetSubFolderPath = Path.Combine(diTargetFolder.Parent.FullName, diSourceSubFolder.Name);

                // Initially assume we'll process this folder if it exists at the target
                var blnProcessSubfolder = Directory.Exists(strTargetSubFolderPath);

                if (blnProcessSubfolder && diSourceSubFolder.GetFiles(DELETE_SUBDIR_FLAG).Length > 0)
                {
                    // Remove this subfolder (but only if it's empty)
                    var folderDeleted = DeleteSubFolder(strTargetSubFolderPath, "parent subfolder", DELETE_SUBDIR_FLAG);
                    if (folderDeleted)
                        blnProcessSubfolder = false;
                }

                if (diSourceSubFolder.GetFiles(DELETE_AM_SUBDIR_FLAG).Length > 0)
                {
                    // Remove this subfolder (but only if it's empty)
                    var strAMSubDirPath = Path.Combine(diTargetFolder.FullName, diSourceSubFolder.Name);
                    var folderDeleted = DeleteSubFolder(strAMSubDirPath, "subfolder", DELETE_AM_SUBDIR_FLAG);
                    if (folderDeleted)
                        blnProcessSubfolder = false;
                }

                if (diSourceSubFolder.GetFiles(PUSH_AM_SUBDIR_FLAG).Length > 0)
                {
                    // Push this folder as a subdirectory of the target folder, not as a subdirectory of the parent folder
                    strTargetSubFolderPath = Path.Combine(diTargetFolder.FullName, diSourceSubFolder.Name);
                    blnProcessSubfolder = true;
                }
                else
                {
                    if (diSourceSubFolder.GetFiles(PUSH_DIR_FLAG).Length > 0)
                    {
                        blnProcessSubfolder = true;
                    }
                }

                if (blnProcessSubfolder)
                {
                    var success = UpdateFolderWork(diSourceSubFolder.FullName, strTargetSubFolderPath, blnPushNewSubfolders: true, blnProcessingSubFolder: true);
                    if (!success)
                        successOverall = false;
                }
            }

            return successOverall;
        }

        private bool DeleteSubFolder(string targetSubFolderPath, string folderDescription, string deleteFlag)
        {
            var diTargetSubFolder = new DirectoryInfo(targetSubFolderPath);

            if (string.IsNullOrWhiteSpace(folderDescription))
            {
                folderDescription = "folder";
            }

            if (diTargetSubFolder.Exists)
            {
                var fileCount = diTargetSubFolder.GetFiles().Length;
                var folderCount = diTargetSubFolder.GetDirectories().Length;

                if (fileCount > 0)
                {
                    ShowMessage("Folder flagged for deletion, but it is not empty (File Count  = " + fileCount + "): " + AbbreviatePath(diTargetSubFolder.FullName));
                }
                else if (folderCount > 0)
                {
                    ShowMessage("Folder flagged for deletion, but it is not empty (Folder Count = " + folderCount + "): " + AbbreviatePath(diTargetSubFolder.FullName));
                }
                else
                {
                    try
                    {
                        if (mPreviewMode)
                        {
                            ShowMessage("Preview " + folderDescription + " delete: " + diTargetSubFolder.FullName);
                        }
                        else
                        {
                            diTargetSubFolder.Delete(false);
                            ShowMessage("Deleted " + folderDescription + " " + diTargetSubFolder.FullName);
                        }

                        return true;
                    }
                    catch (Exception ex)
                    {
                        HandleException("Error removing empty " + folderDescription + " flagged with " + deleteFlag + " at " + diTargetSubFolder.FullName, ex);
                    }
                }
            }

            return false;
        }

        private bool UpdateFolderWork(string strSourceFolderPath, string strTargetFolderPath, bool blnPushNewSubfolders, bool blnProcessingSubFolder)
        {
            base.mProgressStepDescription = "Updating " + AbbreviatePath(strTargetFolderPath) + "\n" + " using " + AbbreviatePath(strSourceFolderPath, mSourceFolderPathBase);

            ShowMessage(base.mProgressStepDescription, false);

            // Make sure the target folder exists
            var diTargetFolder = new DirectoryInfo(strTargetFolderPath);
            if (!diTargetFolder.Exists)
            {
                diTargetFolder.Create();
            }

            // Obtain a list of files in the source folder
            var diSourceFolder = new DirectoryInfo(strSourceFolderPath);

            var fileUpdateCount = 0;

            var fiFilesInSource = diSourceFolder.GetFiles();

            // Populate a List object the with the names of any .delete files in fiFilesInSource
            var lstDeleteFiles = new SortedSet<string>(StringComparer.InvariantCultureIgnoreCase);
            var filesToDelete = (from fiSourceFile in fiFilesInSource where fiSourceFile.Name.EndsWith(DELETE_SUFFIX, StringComparison.InvariantCultureIgnoreCase) select TrimSuffix(fiSourceFile.Name, DELETE_SUFFIX).ToLower());
            foreach (var item in filesToDelete)
            {
                lstDeleteFiles.Add(item);
            }

            // Populate a List object the with the names of any .checkjava files in fiFilesInSource
            var lstCheckJavaFiles = new SortedSet<string>(StringComparer.InvariantCultureIgnoreCase);
            var javaFilesToCheck = (from fiSourceFile in fiFilesInSource where fiSourceFile.Name.EndsWith(CHECK_JAVA_SUFFIX, StringComparison.InvariantCultureIgnoreCase) select TrimSuffix(fiSourceFile.Name, CHECK_JAVA_SUFFIX).ToLower());
            foreach (var item in javaFilesToCheck)
            {
                lstCheckJavaFiles.Add(item);
            }

            foreach (FileInfo fiSourceFile in fiFilesInSource)
            {
                var retryCount = 2;
                var errorLogged = false;

                while (retryCount >= 0)
                {
                    try
                    {
                        var strFileNameLCase = fiSourceFile.Name.ToLower();

                        // Make sure this file is not in mFilesToIgnore
                        // Note that mFilesToIgnore contains several flag files:
                        //   PUSH_DIR_FLAG, PUSH_AM_SUBDIR_FLAG,
                        //   DELETE_SUBDIR_FLAG, DELETE_AM_SUBDIR_FLAG
                        var blnSkipFile = mFilesToIgnore.Contains(strFileNameLCase);

                        if (blnSkipFile)
                        {
                            continue;
                        }

                        var itemInUse = eItemInUseConstants.NotInUse;
                        string fileUsageMessage = string.Empty;

                        // See if file ends with one of the special suffix flags
                        if (fiSourceFile.Name.EndsWith(ROLLBACK_SUFFIX, StringComparison.InvariantCultureIgnoreCase))
                        {
                            // This is a Rollback file
                            // Do not copy this file
                            // However, do look for a corresponding file that does not have .rollback and copy it if the target file has a different date or size

                            var targetFileName = TrimSuffix(strFileNameLCase, ROLLBACK_SUFFIX);
                            if (lstCheckJavaFiles.Contains(targetFileName))
                            {
                                if (JarFileInUseByJava(fiSourceFile, out fileUsageMessage))
                                {
                                    itemInUse = eItemInUseConstants.itemInUse;
                                }
                            }
                            else
                            {
                                if (TargetFolderInUseByProcess(diTargetFolder.FullName, targetFileName, out fileUsageMessage))
                                {
                                    // The folder is in use
                                    // Allow new files to be copied, but do not overwrite existing files
                                    itemInUse = eItemInUseConstants.FolderInUse;
                                }
                            }

                            ProcessRollbackFile(fiSourceFile, diTargetFolder.FullName, ref fileUpdateCount, blnProcessingSubFolder, itemInUse, fileUsageMessage);
                            continue;
                        }
                        else if (fiSourceFile.Name.EndsWith(DELETE_SUFFIX, StringComparison.InvariantCultureIgnoreCase))
                        {
                            // This is a Delete file
                            // Do not copy this file
                            // However, do look for a corresponding file that does not have .delete and delete that file in the target folder

                            ProcessDeleteFile(fiSourceFile, diTargetFolder.FullName);
                            continue;
                        }
                        else if (fiSourceFile.Name.EndsWith(CHECK_JAVA_SUFFIX, StringComparison.InvariantCultureIgnoreCase))
                        {
                            // This is a .checkjava file
                            // Do not copy this file
                            continue;
                        }

                        // Make sure this file does not match a corresponding .delete file
                        if (lstDeleteFiles.Contains(strFileNameLCase))
                        {
                            continue;
                        }

                        eDateComparisonModeConstants eDateComparisonMode = default(eDateComparisonModeConstants);

                        if (mOverwriteNewerFiles)
                        {
                            eDateComparisonMode = eDateComparisonModeConstants.OverwriteNewerTargetIfDifferentSize;
                        }
                        else
                        {
                            eDateComparisonMode = eDateComparisonModeConstants.RetainNewerTargetIfDifferentSize;
                        }

                        if (lstCheckJavaFiles.Contains(strFileNameLCase))
                        {
                            if (JarFileInUseByJava(fiSourceFile, out fileUsageMessage))
                            {
                                itemInUse = eItemInUseConstants.itemInUse;
                            }
                        }
                        else
                        {
                            if (TargetFolderInUseByProcess(diTargetFolder.FullName, fiSourceFile.Name, out fileUsageMessage))
                            {
                                // The folder is in use
                                // Allow new files to be copied, but do not overwrite existing files
                                itemInUse = eItemInUseConstants.FolderInUse;
                            }
                        }

                        CopyFileIfNeeded(fiSourceFile, diTargetFolder.FullName, ref fileUpdateCount, eDateComparisonMode, blnProcessingSubFolder, itemInUse, fileUsageMessage);

                        // File processed; move on to the next file
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (!errorLogged)
                        {
                            ShowErrorMessage("Error synchronizing " + fiSourceFile.Name + ": " + ex.Message, true);
                            errorLogged = true;
                        }

                        retryCount -= 1;
                        Thread.Sleep(100);
                    }
                }
            }

            if (fileUpdateCount > 0)
            {
                var statusMessage = "Updated " + fileUpdateCount + " file";
                if (fileUpdateCount > 1)
                    statusMessage += "s";

                statusMessage += " using " + diSourceFolder.FullName + "\\";

                ShowMessage(statusMessage, true, false);
            }

            // Process each subdirectory in the source folder
            // If the folder exists at the target, copy it
            // Additionally, if the source folder contains file _PushDir_.txt, it gets copied even if it doesn't exist at the target
            foreach (DirectoryInfo diSourceSubFolder in diSourceFolder.GetDirectories())
            {
                var strTargetSubFolderPath = Path.Combine(diTargetFolder.FullName, diSourceSubFolder.Name);

                // Initially assume we'll process this folder if it exists at the target
                var diTargetSubFolder = new DirectoryInfo(strTargetSubFolderPath);
                var blnProcessSubfolder = diTargetSubFolder.Exists;

                if (blnProcessSubfolder && diSourceSubFolder.GetFiles(DELETE_SUBDIR_FLAG).Length > 0)
                {
                    // Remove this subfolder (but only if it's empty)
                    var folderDeleted = DeleteSubFolder(strTargetSubFolderPath, "subfolder", DELETE_SUBDIR_FLAG);
                    if (folderDeleted)
                        blnProcessSubfolder = false;
                }

                if (blnPushNewSubfolders && diSourceSubFolder.GetFiles(PUSH_DIR_FLAG).Length > 0)
                {
                    blnProcessSubfolder = true;
                }

                if (blnProcessSubfolder)
                {
                    UpdateFolderWork(diSourceSubFolder.FullName, diTargetSubFolder.FullName, blnPushNewSubfolders, blnProcessingSubFolder);
                }
            }

            return true;
        }

        private bool JarFileInUseByJava(FileInfo fiSourceFile, out string jarFileUsageMessage)
        {
            const bool INCLUDE_PROGRAM_PATH = false;
            jarFileUsageMessage = string.Empty;

            try
            {
                var processes = Process.GetProcesses().ToList();
                processes.Sort(new ProcessNameComparer());

                if (mPreviewMode & !mProcessesShown)
                {
                    Console.WriteLine();
                    ShowMessage("Examining running processes for Java", false);
                }

                var lastProcess = string.Empty;

                foreach (Process oProcess in processes)
                {
                    if (mPreviewMode & !mProcessesShown)
                    {
                        if (oProcess.ProcessName != lastProcess)
                        {
                            Console.WriteLine(oProcess.ProcessName);
                        }
                        lastProcess = oProcess.ProcessName;
                    }

                    if (!oProcess.ProcessName.StartsWith("java", StringComparison.InvariantCultureIgnoreCase))
                    {
                        continue;
                    }

                    try
                    {
                        var commandLine = GetCommandLine(oProcess, INCLUDE_PROGRAM_PATH);

                        if (mPreviewMode & !mProcessesShown)
                        {
                            Console.WriteLine("  " + commandLine);
                        }

                        if (commandLine.ToLower().Contains(fiSourceFile.Name.ToLower()))
                        {
                            jarFileUsageMessage = "Skipping " + fiSourceFile.Name + " because currently in use by Java";
                            return true;
                        }
                        else
                        {
                            if ((string.IsNullOrWhiteSpace(commandLine)))
                            {
                                jarFileUsageMessage = "Skipping " + fiSourceFile.Name + " because empty Java command line (permissions issue?)";
                                return true;
                            }

                            // Uncomment to debug:
                            // ShowMessage("Command line for java process ID " & oProcess.Id & ": " & commandLine)
                        }
                    }
                    catch (Exception ex)
                    {
                        // Skip the process; possibly permission denied

                        jarFileUsageMessage = "Skipping " + fiSourceFile.Name + " because exception: " + ex.Message;
                        return true;
                    }
                }

                if (mPreviewMode & !mProcessesShown)
                {
                    Console.WriteLine();
                    mProcessesShown = true;
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error looking for Java using " + fiSourceFile.Name + ": " + ex.Message, true);
            }

            return false;
        }

        private string GetCommandLine(Process oProcess, bool includeProgramPath)
        {
            var commandLine = new StringBuilder();

            if (includeProgramPath)
            {
                commandLine.Append(oProcess.MainModule.FileName);
                commandLine.Append(" ");
            }

            var result = new ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE ProcessId = " + oProcess.Id);

            foreach (var item in result.Get())
            {
                commandLine.Append(item["CommandLine"]);
            }

            return commandLine.ToString();
        }

        private bool TargetFolderInUseByProcess(string strTargetFolderPath, string targetFileName, out string folderUsageMessage)
        {
            folderUsageMessage = string.Empty;

            try
            {
                string firstProcessPath = string.Empty;
                UInt32 firstProcessId = default(UInt32);
                var processCount = GetNumTargetFolderProcesses(strTargetFolderPath, out firstProcessPath, out firstProcessId);

                if (processCount > 0)
                {
                    // Example log messages:
                    // Skipping UIMFLibrary.dll because folder DeconTools is in use by process DeconConsole.exe (PID 343243)
                    // Skipping DeconConsole.exe because folder DeconTools is in use by 2 processes on this system, including DeconConsole.exe (PID 343243)

                    folderUsageMessage = "Skipping " + targetFileName + " because folder " + AbbreviatePath(strTargetFolderPath) + " is in use by ";

                    string processPathToShow = null;

                    if (string.IsNullOrWhiteSpace(firstProcessPath))
                    {
                        processPathToShow = "an unknown process";
                    }
                    else
                    {
                        var diProcessFile = new FileInfo(firstProcessPath);
                        var processIdAppend = " (" + " PID " + firstProcessId + ")";

                        if (diProcessFile.DirectoryName == strTargetFolderPath)
                        {
                            processPathToShow = Path.GetFileName(firstProcessPath) + processIdAppend;
                        }
                        else if (strTargetFolderPath.StartsWith(diProcessFile.DirectoryName))
                        {
                            var relativePath = diProcessFile.Directory.Parent.FullName;
                            string pathPart = null;
                            if (diProcessFile.DirectoryName.Length > relativePath.Length)
                            {
                                pathPart = diProcessFile.DirectoryName.Substring(relativePath.Length + 1);
                            }
                            else
                            {
                                pathPart = diProcessFile.DirectoryName;
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
                ShowErrorMessage("Error looking for processes using files in " + strTargetFolderPath + ": " + ex.Message, true);
            }

            return false;
        }

        /// <summary>
        /// Determine the number of processes using files in the given folder
        /// </summary>
        /// <param name="strTargetFolderPath">Folder to examine</param>
        /// <param name="firstProcessPath">Output parameter: first process using files in this folder; empty string if no processes</param>
        /// <param name="firstProcessId">Output parameter: Process ID of first process using files in this folder</param>
        /// <returns>Count of processes using this folder</returns>
        private int GetNumTargetFolderProcesses(string strTargetFolderPath, out string firstProcessPath, out UInt32 firstProcessId)
        {
            firstProcessPath = string.Empty;
            firstProcessId = 0;

            // Filter the queried results for each call to this function.

            var targetFolderPathHierarchy = clsProcessInfo.GetFolderHierarchy(strTargetFolderPath);

            if (string.Equals(strTargetFolderPath, mLastFolderProcessesChecked, StringComparison.InvariantCultureIgnoreCase))
            {
                firstProcessPath = mLastFolderRunningProcessPath;
                firstProcessId = mLastFolderRunningProcessId;
                return mProcessesMatchingTarget.Count;
            }

            mProcessesMatchingTarget.Clear();
            mLastFolderProcessesChecked = strTargetFolderPath;
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
                if (item.Value.CommandLineArgs.IndexOf(strTargetFolderPath, StringComparison.InvariantCultureIgnoreCase) < 0)
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

        private void ProcessDeleteFile(FileInfo fiDeleteFile, string strTargetFolderPath)
        {
            var strTargetFilePath = Path.Combine(strTargetFolderPath, TrimSuffix(fiDeleteFile.Name, DELETE_SUFFIX));
            var fiTargetFile = new FileInfo(strTargetFilePath);

            if (fiTargetFile.Exists)
            {
                if (mPreviewMode)
                {
                    ShowMessage("Preview delete: " + fiTargetFile.FullName);
                }
                else
                {
                    fiTargetFile.Delete();
                    ShowMessage("Deleted file " + fiTargetFile.FullName);
                }
            }

            // Make sure the .delete is also not in the target folder
            var strTargetDeleteFilePath = Path.Combine(strTargetFolderPath, fiDeleteFile.Name);
            var fiTargetDeleteFile = new FileInfo(strTargetDeleteFilePath);

            if (fiTargetDeleteFile.Exists)
            {
                if (mPreviewMode)
                {
                    ShowMessage("Preview delete: " + fiTargetDeleteFile.FullName);
                }
                else
                {
                    fiTargetDeleteFile.Delete();
                    ShowMessage("Deleted file " + fiTargetDeleteFile.FullName);
                }
            }
        }

        /// <summary>
        /// Rollback the target file if it differs from the source
        /// </summary>
        /// <param name="fiRollbackFile">Rollback file path</param>
        /// <param name="strTargetFolderPath">Target folder</param>
        /// <param name="fileUpdateCount">Number of files that have been updated (Input/output)</param>
        /// <param name="blnProcessingSubFolder">True if processing a subfolder</param>
        /// <param name="itemInUse">Used to track when a file or folder is in use by another process (log a message if the source and target files differ)</param>
        /// <param name="fileUsageMessage">Message to log when the file (or folder) is in use and the source and targets differ</param>
        private void ProcessRollbackFile(FileInfo fiRollbackFile, string strTargetFolderPath, ref int fileUpdateCount, bool blnProcessingSubFolder, eItemInUseConstants itemInUse = eItemInUseConstants.NotInUse, string fileUsageMessage = "")
        {
            var strSourceFilePath = TrimSuffix(fiRollbackFile.FullName, ROLLBACK_SUFFIX);

            var fiSourceFile = new FileInfo(strSourceFilePath);

            if (fiSourceFile.Exists)
            {
                var copied = CopyFileIfNeeded(fiSourceFile, strTargetFolderPath, ref fileUpdateCount, eDateComparisonModeConstants.CopyIfSizeOrDateDiffers, blnProcessingSubFolder, itemInUse, fileUsageMessage);
                if (copied)
                {
                    string prefix = null;

                    if (mPreviewMode)
                    {
                        prefix = "Preview rollback of file ";
                    }
                    else
                    {
                        prefix = "Rolled back file ";
                    }

                    ShowMessage(prefix + fiSourceFile.Name + " to version from " + fiSourceFile.LastWriteTimeUtc.ToLocalTime() + " with size " + (fiSourceFile.Length / 1024.0).ToString("0.0") + " KB");
                }
            }
            else
            {
                ShowMessage("Warning: Rollback file is present (" + fiRollbackFile.Name + ") but expected source file was not found: " + fiSourceFile.Name, intDuplicateHoldoffHours: 24);
            }
        }

        private void SetLocalErrorCode(eDMSUpdateManagerErrorCodes eNewErrorCode)
        {
            SetLocalErrorCode(eNewErrorCode, false);
        }

        private void SetLocalErrorCode(eDMSUpdateManagerErrorCodes eNewErrorCode, bool blnLeaveExistingErrorCodeUnchanged)
        {
            if (blnLeaveExistingErrorCodeUnchanged && mLocalErrorCode != eDMSUpdateManagerErrorCodes.NoError)
            {
                // An error code is already defined; do not change it
            }
            else
            {
                mLocalErrorCode = eNewErrorCode;

                if (eNewErrorCode == eDMSUpdateManagerErrorCodes.NoError)
                {
                    if (base.ErrorCode == eProcessFoldersErrorCodes.LocalizedError)
                    {
                        base.SetBaseClassErrorCode(eProcessFoldersErrorCodes.NoError);
                    }
                }
                else
                {
                    base.SetBaseClassErrorCode(eProcessFoldersErrorCodes.LocalizedError);
                }
            }
        }

        private string TrimSuffix(string strText, string strSuffix)
        {
            if (strText.Length >= strSuffix.Length)
            {
                return strText.Substring(0, strText.Length - strSuffix.Length);
            }
            else
            {
                return strText;
            }
        }

        private class ProcessNameComparer : IComparer<Process>
        {
            public int Compare(Process x, Process y)
            {
                return string.Compare(x.ProcessName, y.ProcessName, StringComparison.InvariantCultureIgnoreCase);
            }
        }
    }
}
