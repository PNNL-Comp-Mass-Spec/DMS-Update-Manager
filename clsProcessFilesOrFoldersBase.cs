﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Reflection;
using System.Text.RegularExpressions;
using PRISM;

namespace DMSUpdateManager
{
    /// <summary>
    /// This class contains functions used by both clsProcessFilesBaseClass and clsProcessFoldersBaseClass
    /// </summary>
    /// <remarks>
    /// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
    /// Created in October 2013
    /// Last updated in October 2015
    /// </remarks>
    public abstract class clsProcessFilesOrFoldersBase : clsEventNotifier
    {
        #region "Constants and Enums"

        protected enum eMessageTypeConstants
        {
            Normal = 0,
            ErrorMsg = 1,
            Warning = 2
        }

        #endregion

        #region "Classwide Variables"

        protected string mFileDate;

        protected bool mLogFileUsesDateStamp = true;
        protected string mLogFilePath;
        protected StreamWriter mLogFile;

        // This variable is updated when CleanupFilePaths() is called
        protected string mOutputFolderPath;

        private string mLastMessage = "";
        private DateTime mLastReportTime = DateTime.UtcNow;
        private DateTime mLastErrorShown = DateTime.MinValue;

        public event ProgressResetEventHandler ProgressReset;
        public delegate void ProgressResetEventHandler();

        public event ProgressCompleteEventHandler ProgressComplete;
        public delegate void ProgressCompleteEventHandler();

        protected string mProgressStepDescription;

        /// <summary>
        /// Percent complete, value between 0 and 100, but can contain decimal percentage values
        /// </summary>
        protected float mProgressPercentComplete;

        /// <summary>
        /// Keys in this dictionary are the log type and message (separated by an underscore), values are the most recent time the string was logged
        /// </summary>
        /// <remarks></remarks>
        private readonly Dictionary<string, DateTime> mLogDataCache;

        private const int MAX_LOGDATA_CACHE_SIZE = 100000;

        #endregion

        #region "Interface Functions"

        public bool AbortProcessing { get; set; }

        public string FileVersion => GetVersionForExecutingAssembly();

        public string FileDate => mFileDate;

        public string LogFilePath
        {
            get => mLogFilePath;
            set
            {
                if (value == null)
                    value = string.Empty;
                mLogFilePath = value;
            }
        }

        /// <summary>
        /// Log folder path (ignored if LogFilePath is rooted)
        /// </summary>
        /// <remarks>
        /// If blank, mOutputFolderPath will be used; if mOutputFolderPath is also blank, the log is created in the same folder as the executing assembly
        /// </remarks>
        public string LogFolderPath { get; set; }

        public bool LogMessagesToFile { get; set; }

        public virtual string ProgressStepDescription => mProgressStepDescription;

        /// <summary>
        /// Percent complete, value between 0 and 100, but can contain decimal percentage values
        /// </summary>
        public float ProgressPercentComplete => Convert.ToSingle(Math.Round(mProgressPercentComplete, 2));

        public bool ShowMessages { get; set; } = true;

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        /// <remarks></remarks>
        protected clsProcessFilesOrFoldersBase()
        {
            mProgressStepDescription = string.Empty;

            mOutputFolderPath = string.Empty;
            LogFolderPath = string.Empty;
            mLogFilePath = string.Empty;

            mLogDataCache = new Dictionary<string, DateTime>();
        }

        public virtual void AbortProcessingNow()
        {
            AbortProcessing = true;
        }

        protected abstract void CleanupPaths(ref string inputFileOrFolderPath, ref string outputFolderPath);

        public void CloseLogFileNow()
        {
            if (mLogFile != null)
            {
                mLogFile.Close();
                mLogFile = null;

                GarbageCollectNow();
                Thread.Sleep(100);
            }
        }

        /// <summary>
        /// Verifies that the specified .XML settings file exists in the user's local settings folder
        /// </summary>
        /// <param name="applicationName">Application name</param>
        /// <param name="settingsFileName">Settings file name</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static bool CreateSettingsFileIfMissing(string applicationName, string settingsFileName)
        {
            var settingsFilePathLocal = GetSettingsFilePathLocal(applicationName, settingsFileName);

            return CreateSettingsFileIfMissing(settingsFilePathLocal);
        }

        /// <summary>
        /// Verifies that the specified .XML settings file exists in the user's local settings folder
        /// </summary>
        /// <param name="settingsFilePathLocal">Full path to the local settings file, for example C:\Users\username\AppData\Roaming\AppName\SettingsFileName.xml</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static bool CreateSettingsFileIfMissing(string settingsFilePathLocal)
        {
            try
            {
                if (!File.Exists(settingsFilePathLocal))
                {
                    var masterSettingsFile = new FileInfo(Path.Combine(GetAppFolderPath(), Path.GetFileName(settingsFilePathLocal)));

                    if (masterSettingsFile.Exists)
                    {
                        masterSettingsFile.CopyTo(settingsFilePathLocal);
                    }
                }
            }
            catch (Exception)
            {
                // Ignore errors, but return false
                return false;
            }

            return true;
        }

        /// <summary>
        /// Perform garbage collection
        /// </summary>
        /// <remarks></remarks>
        public static void GarbageCollectNow()
        {
            const int maxWaitTimeMSec = 1000;
            GarbageCollectNow(maxWaitTimeMSec);
        }

        /// <summary>
        /// Perform garbage collection
        /// </summary>
        /// <param name="maxWaitTimeMSec"></param>
        /// <remarks></remarks>
        public static void GarbageCollectNow(int maxWaitTimeMSec)
        {
            const int THREAD_SLEEP_TIME_MSEC = 100;

            if (maxWaitTimeMSec < 100)
                maxWaitTimeMSec = 100;
            if (maxWaitTimeMSec > 5000)
                maxWaitTimeMSec = 5000;

            Thread.Sleep(100);

            try
            {
                var gcThread = new Thread(GarbageCollectWaitForGC);
                gcThread.Start();

                var totalThreadWaitTimeMsec = 0;
                while (gcThread.IsAlive && totalThreadWaitTimeMsec < maxWaitTimeMSec)
                {
                    Thread.Sleep(THREAD_SLEEP_TIME_MSEC);
                    totalThreadWaitTimeMsec += THREAD_SLEEP_TIME_MSEC;
                }
                if (gcThread.IsAlive)
                    gcThread.Abort();
            }
            catch (Exception)
            {
                // Ignore errors here
            }
        }

        protected static void GarbageCollectWaitForGC()
        {
            clsProgRunner.GarbageCollectNow();
        }

        /// <summary>
        /// Returns the full path to the folder into which this application should read/write settings file information
        /// </summary>
        /// <param name="appName"></param>
        /// <returns></returns>
        /// <remarks>For example, C:\Users\username\AppData\Roaming\AppName</remarks>
        public static string GetAppDataFolderPath(string appName)
        {
            string appDataFolder;

            if (string.IsNullOrWhiteSpace(appName))
            {
                appName = string.Empty;
            }

            try
            {
                appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appName);
                if (!Directory.Exists(appDataFolder))
                {
                    Directory.CreateDirectory(appDataFolder);
                }
            }
            catch (Exception)
            {
                // Error creating the folder, revert to using the system Temp folder
                appDataFolder = Path.GetTempPath();
            }

            return appDataFolder;
        }

        /// <summary>
        /// Returns the full path to the folder that contains the currently executing .Exe or .Dll
        /// </summary>
        /// <returns></returns>
        /// <remarks></remarks>
        public static string GetAppFolderPath()
        {
            // Could use Application.StartupPath, but .GetExecutingAssembly is better
            return Path.GetDirectoryName(GetAppPath());
        }

        /// <summary>
        /// Returns the full path to the executing .Exe or .Dll
        /// </summary>
        /// <returns>File path</returns>
        /// <remarks></remarks>
        public static string GetAppPath()
        {
            return Assembly.GetExecutingAssembly().Location;
        }

        /// <summary>
        /// Returns the .NET assembly version followed by the program date
        /// </summary>
        /// <param name="programDate"></param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static string GetAppVersion(string programDate)
        {
            return Assembly.GetExecutingAssembly().GetName().Version + " (" + programDate + ")";
        }

        public abstract string GetErrorMessage();

        private string GetVersionForExecutingAssembly()
        {
            string version;

            try
            {
                version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            }
            catch (Exception)
            {
                version = "??.??.??.??";
            }

            return version;
        }

        /// <summary>
        /// Returns the full path to this application's local settings file
        /// </summary>
        /// <param name="applicationName"></param>
        /// <param name="settingsFileName"></param>
        /// <returns></returns>
        /// <remarks>For example, C:\Users\username\AppData\Roaming\AppName\SettingsFileName.xml</remarks>
        public static string GetSettingsFilePathLocal(string applicationName, string settingsFileName)
        {
            return Path.Combine(GetAppDataFolderPath(applicationName), settingsFileName);
        }

        protected void HandleException(string baseMessage, Exception ex)
        {
            if (string.IsNullOrWhiteSpace(baseMessage))
            {
                baseMessage = "Error";
            }

            if (ShowMessages)
            {
                // Note that ShowErrorMessage() will call LogMessage()
                ShowErrorMessage(baseMessage + ": " + ex.Message);
            }
            else
            {
                LogMessage(baseMessage + ": " + ex.Message, eMessageTypeConstants.ErrorMsg);
                throw new Exception(baseMessage, ex);
            }
        }

        /// <summary>
        /// Sets the log file path (<see cref="mLogFilePath"/>), according to data in <see cref="mLogFilePath"/>, <see cref="mLogFileUsesDateStamp"/>, and <see cref="LogFolderPath"/>
        /// </summary>
        protected void ConfigureLogFilePath()
        {
            if (string.IsNullOrWhiteSpace(mLogFilePath))
            {
                // Auto-name the log file
                mLogFilePath = Path.GetFileNameWithoutExtension(GetAppPath());
                mLogFilePath += "_log";

                if (mLogFileUsesDateStamp)
                {
                    mLogFilePath += "_" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt";
                }
                else
                {
                    mLogFilePath += ".txt";
                }
            }

            try
            {
                if (LogFolderPath == null)
                    LogFolderPath = string.Empty;

                if (string.IsNullOrWhiteSpace(LogFolderPath))
                {
                    // Log folder is undefined; use mOutputFolderPath if it is defined
                    if (!string.IsNullOrWhiteSpace(mOutputFolderPath))
                    {
                        LogFolderPath = string.Copy(mOutputFolderPath);
                    }
                }

                if (LogFolderPath.Length > 0)
                {
                    // Create the log folder if it doesn't exist
                    if (!Directory.Exists(LogFolderPath))
                    {
                        Directory.CreateDirectory(LogFolderPath);
                    }
                }
            }
            catch (Exception)
            {
                LogFolderPath = string.Empty;
            }

            if (!Path.IsPathRooted(mLogFilePath) && LogFolderPath.Length > 0 && !mLogFilePath.StartsWith(LogFolderPath))
            {
                mLogFilePath = Path.Combine(LogFolderPath, mLogFilePath);
            }
        }

        private void InitializeLogFile(int duplicateHoldoffHours)
        {
            try
            {
                ConfigureLogFilePath();

                var openingExistingFile = File.Exists(mLogFilePath);

                if (openingExistingFile & mLogDataCache.Count == 0)
                {
                    UpdateLogDataCache(mLogFilePath, DateTime.UtcNow.AddHours(-duplicateHoldoffHours));
                }

                mLogFile = new StreamWriter(new FileStream(mLogFilePath, FileMode.Append, FileAccess.Write, FileShare.Read))
                {
                    AutoFlush = true
                };

                if (!openingExistingFile)
                {
                    mLogFile.WriteLine("Date\tType\tMessage");
                }
            }
            catch (Exception ex)
            {
                // Error creating the log file; set mLogMessagesToFile to false so we don't repeatedly try to create it
                LogMessagesToFile = false;
                HandleException("Error opening log file", ex);
                // Note: do not exit this function if an exception occurs
            }
        }

        /// <summary>
        /// Log a message then raise a Status, Warning, or Error event
        /// </summary>
        /// <param name="message"></param>
        /// <param name="eMessageType"></param>
        /// <param name="duplicateHoldoffHours"></param>
        /// <remarks>
        /// Note that CleanupPaths() will update mOutputFolderPath, which is used here if mLogFolderPath is blank
        /// Thus, be sure to call CleanupPaths (or update mLogFolderPath) before the first call to LogMessage
        /// </remarks>
        protected void LogMessage(string message, eMessageTypeConstants eMessageType = eMessageTypeConstants.Normal, int duplicateHoldoffHours = 0)
        {

            if (mLogFile == null && LogMessagesToFile)
            {
                InitializeLogFile(duplicateHoldoffHours);
            }

            if (mLogFile != null)
            {
                WriteToLogFile(message, eMessageType, duplicateHoldoffHours);
            }

            RaiseMessageEvent(message, eMessageType);
        }

        private void RaiseMessageEvent(string message, eMessageTypeConstants eMessageType)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            if (string.Equals(message, mLastMessage) && DateTime.UtcNow.Subtract(mLastReportTime).TotalSeconds < 0.5)
            {
                // Duplicate message; do not raise any events
            }
            else
            {
                mLastReportTime = DateTime.UtcNow;
                mLastMessage = string.Copy(message);

                switch (eMessageType)
                {
                    case eMessageTypeConstants.Normal:
                        OnStatusEvent(message);
                        break;
                    case eMessageTypeConstants.Warning:
                        OnWarningEvent(message);

                        break;
                    case eMessageTypeConstants.ErrorMsg:
                        OnErrorEvent(message);

                        break;
                    default:
                        OnStatusEvent(message);
                        break;
                }
            }
        }

        protected void ResetProgress()
        {
            mProgressPercentComplete = 0;
            ProgressReset?.Invoke();
        }

        protected void ResetProgress(string progressStepDescription)
        {
            UpdateProgress(progressStepDescription, 0);
            ProgressReset?.Invoke();
        }

        protected void ShowErrorMessage(string message, int duplicateHoldoffHours)
        {
            ShowErrorMessage(message, allowLogToFile: true, duplicateHoldoffHours: duplicateHoldoffHours);
        }

        protected void ShowErrorMessage(string message, bool allowLogToFile = true, int duplicateHoldoffHours = 0)
        {
            if (allowLogToFile)
            {
                // Note that LogMessage will call RaiseMessageEvent
                LogMessage(message, eMessageTypeConstants.ErrorMsg, duplicateHoldoffHours);
            }
            else
            {
                RaiseMessageEvent(message, eMessageTypeConstants.ErrorMsg);
            }
        }

        protected void ShowMessage(string message, int duplicateHoldoffHours)
        {
            ShowMessage(message, allowLogToFile: true, duplicateHoldoffHours: duplicateHoldoffHours);
        }

        protected void ShowMessage(
            string message,
            bool allowLogToFile = true,
            int duplicateHoldoffHours = 0,
            eMessageTypeConstants eMessageType = eMessageTypeConstants.Normal)
        {
            if (allowLogToFile)
            {
                // Note that LogMessage will call RaiseMessageEvent
                LogMessage(message, eMessageType, duplicateHoldoffHours);
            }
            else
            {
                RaiseMessageEvent(message, eMessageType);
            }
        }

        protected void ShowWarning(string message, int duplicateHoldoffHours = 0)
        {
            ShowMessage(message, allowLogToFile: true, duplicateHoldoffHours: duplicateHoldoffHours, eMessageType: eMessageTypeConstants.Warning);
        }

        protected void ShowWarning(string message, bool allowLogToFile)
        {
            ShowMessage(message, allowLogToFile, duplicateHoldoffHours: 0, eMessageType: eMessageTypeConstants.Warning);
        }

        private void TrimLogDataCache()
        {
            if (mLogDataCache.Count < MAX_LOGDATA_CACHE_SIZE)
                return;

            try
            {
                // Remove entries from mLogDataCache so that the list count is 80% of MAX_LOGDATA_CACHE_SIZE

                // First construct a list of dates that we can sort to determine the datetime threshold for removal
                var lstDates = (from entry in mLogDataCache select entry.Value).ToList();

                // Sort by date
                lstDates.Sort();

                var thresholdIndex = Convert.ToInt32(Math.Floor(mLogDataCache.Count - MAX_LOGDATA_CACHE_SIZE * 0.8));
                if (thresholdIndex < 0)
                    thresholdIndex = 0;

                var threshold = lstDates[thresholdIndex];

                // Construct a list of keys to be removed
                var lstKeys = (from entry in mLogDataCache where entry.Value <= threshold select entry.Key).ToList();

                // Remove each of the keys
                foreach (var key in lstKeys)
                {
                    mLogDataCache.Remove(key);
                }
            }
            catch (Exception)
            {
                // Ignore errors here
            }
        }

        private void UpdateLogDataCache(string logFilePath, DateTime dateThresholdToStore)
        {
            var reParseLine = new Regex(@"^([^\t]+)\t([^\t]+)\t(.+)", RegexOptions.Compiled);

            try
            {
                mLogDataCache.Clear();

                using (var srLogFile = new StreamReader(new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                {
                    while (!srLogFile.EndOfStream)
                    {
                        var lineIn = srLogFile.ReadLine();
                        if (string.IsNullOrEmpty(lineIn))
                            continue;

                        var reMatch = reParseLine.Match(lineIn);

                        if (!reMatch.Success)
                            continue;

                        if (DateTime.TryParse(reMatch.Groups[1].Value, out var logTime))
                        {
                            logTime = logTime.ToUniversalTime();
                            if (logTime >= dateThresholdToStore)
                            {
                                var key = reMatch.Groups[2].Value + "_" + reMatch.Groups[3].Value;

                                try
                                {
                                    if (mLogDataCache.ContainsKey(key))
                                    {
                                        mLogDataCache[key] = logTime;
                                    }
                                    else
                                    {
                                        mLogDataCache.Add(key, logTime);
                                    }
                                }
                                catch (Exception)
                                {
                                    // Ignore errors here
                                }
                            }
                        }
                    }
                }

                if (mLogDataCache.Count > MAX_LOGDATA_CACHE_SIZE)
                {
                    TrimLogDataCache();
                }
            }
            catch (Exception ex)
            {
                if (DateTime.UtcNow.Subtract(mLastErrorShown).TotalSeconds > 10)
                {
                    mLastErrorShown = DateTime.UtcNow;
                    Console.WriteLine("Error caching the log file: " + ex.Message);
                }
            }
        }

        protected void UpdateProgress(string progressStepDescription)
        {
            UpdateProgress(progressStepDescription, mProgressPercentComplete);
        }

        protected void UpdateProgress(float sngPercentComplete)
        {
            UpdateProgress(ProgressStepDescription, sngPercentComplete);
        }

        protected void UpdateProgress(string progressStepDescription, float sngPercentComplete)
        {
            var descriptionChanged = !string.Equals(progressStepDescription, mProgressStepDescription);

            mProgressStepDescription = string.Copy(progressStepDescription);
            if (sngPercentComplete < 0)
            {
                sngPercentComplete = 0;
            }
            else if (sngPercentComplete > 100)
            {
                sngPercentComplete = 100;
            }
            mProgressPercentComplete = sngPercentComplete;

            if (descriptionChanged)
            {
                if (mProgressPercentComplete < float.Epsilon)
                {
                    LogMessage(mProgressStepDescription.Replace(Environment.NewLine, "; "));
                }
                else
                {
                    LogMessage(mProgressStepDescription + " (" + mProgressPercentComplete.ToString("0.0") + "% complete)".Replace(Environment.NewLine, "; "));
                }
            }

            OnProgressUpdate(ProgressStepDescription, ProgressPercentComplete);
        }

        private void WriteToLogFile(string message, eMessageTypeConstants eMessageType, int duplicateHoldoffHours)
        {
            string messageType;

            switch (eMessageType)
            {
                case eMessageTypeConstants.Normal:
                    messageType = "Normal";
                    break;
                case eMessageTypeConstants.ErrorMsg:
                    messageType = "Error";
                    break;
                case eMessageTypeConstants.Warning:
                    messageType = "Warning";
                    break;
                default:
                    messageType = "Unknown";
                    break;
            }

            var writeToLog = true;

            var logKey = messageType + "_" + message;
            bool messageCached;

            if (mLogDataCache.TryGetValue(logKey, out var lastLogTime))
            {
                messageCached = true;
            }
            else
            {
                messageCached = false;
                lastLogTime = DateTime.UtcNow.AddHours(-(duplicateHoldoffHours + 1));
            }

            if (duplicateHoldoffHours > 0 && DateTime.UtcNow.Subtract(lastLogTime).TotalHours < duplicateHoldoffHours)
            {
                writeToLog = false;
            }

            if (!writeToLog)
                return;

            mLogFile.WriteLine(DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss tt") + "\t" +
                               messageType + "\t" +
                               message);

            if (messageCached)
            {
                mLogDataCache[logKey] = DateTime.UtcNow;
            }
            else
            {
                try
                {
                    mLogDataCache.Add(logKey, DateTime.UtcNow);

                    if (mLogDataCache.Count > MAX_LOGDATA_CACHE_SIZE)
                    {
                        TrimLogDataCache();
                    }
                }
                catch (Exception)
                {
                    // Ignore errors here
                }
            }
        }

        protected void OperationComplete()
        {
            ProgressComplete?.Invoke();
        }
    }
}

