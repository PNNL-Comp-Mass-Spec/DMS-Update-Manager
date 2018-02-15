using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PRISM;
using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;

namespace DMSUpdateManager
{
    /// <summary>
    /// Methods for copying new/updated files from a Windows share to a Linux host
    /// Uses sftp for file listings
    /// Uses scp for file transfers
    /// </summary>
    public class RemoteUpdateUtility : clsEventNotifier
    {
        #region "Constants"

        /// <summary>
        /// Default date/time format
        /// </summary>
        public const string DATE_TIME_FORMAT = "yyyy-MM-dd hh:mm:ss tt";

        /// <summary>
        /// Lock file suffix
        /// </summary>
        public const string LOCK_FILE_EXTENSION = ".lock";

        #endregion

        #region "Module variables"

        private bool mParametersValidated;

        private PrivateKeyFile mPrivateKeyFile;

        #endregion

        #region "Properties"

        /// <summary>
        /// Remote host name
        /// </summary>
        public RemoteHostConnectionInfo TargetHostOptions { get; }

        #endregion

        #region "Methods"

        /// <summary>
        /// Constructor
        /// </summary>
        public RemoteUpdateUtility(RemoteHostConnectionInfo targetHostOptions)
        {
            TargetHostOptions = targetHostOptions;
            mParametersValidated = false;

        /// <summary>
        /// Look for a lock file named dataFileName + ".lock" in directory remoteDirectoryPath
        /// If found, and if less than maxWaitTimeMinutes old, waits for it to be deleted by another process or to age
        /// </summary>
        /// <param name="sftp">Sftp client (to avoid connecting / disconnecting repeatedly)</param>
        /// <param name="remoteDirectoryPath">Target directory on the remote server (use Linux-style forward slashes)</param>
        /// <param name="dataFileName">Data file name (without .lock)</param>
        /// <param name="lockFileWasFound">
        /// Output: true if a lock file was found and we waited for it to be deleted or age
        /// </param>
        /// <param name="lockFileWasAged">
        /// Output: true if either an aged lock file was found and deleted,
        /// or if a current lock file was found but we waited more than maxWaitTimeMinutes and it still existed
        /// </param>
        /// <param name="maxWaitTimeMinutes">Maximum age of the lock file</param>
        /// <param name="logIntervalMinutes"></param>
        /// <remarks>
        /// Typical steps for using lock files to assure that only one manager is creating a specific file
        /// 1. Call CheckForRemoteLockFile() to check for a lock file; wait for it to age
        /// 2. Once CheckForRemoteLockFile() exits, check for the required data file; exit the function if the desired file is found
        /// 3. If the file was not found, create a new lock file by calling CreateRemoteLockFile()
        /// 4. Copy the file to the remote server
        /// 5. Delete the lock file by calling DeleteLockFile()
        /// </remarks>
        /// <remarks>This method is similar to CheckForLockFile in clsAnalysisResources but it uses sftp or file lookups and scp for file creation</remarks>
        private void CheckForRemoteLockFile(
            SftpClient sftp,
            string remoteDirectoryPath,
            string dataFileName,
            out bool lockFileWasFound,
            out bool lockFileWasAged,
            int maxWaitTimeMinutes = 120,
            int logIntervalMinutes = 5)
        {

            if (dataFileName.EndsWith(LOCK_FILE_EXTENSION, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("dataFileName may not end in .lock", nameof(dataFileName));

            lockFileWasFound = false;
            lockFileWasAged = false;

            var waitingForLockFile = false;
            var dtLockFileCreated = DateTime.UtcNow;

            // Look for a recent .lock file on the remote server
            var lockFileName = dataFileName + LOCK_FILE_EXTENSION;

            var fiLockFile = FindLockFile(sftp, remoteDirectoryPath, lockFileName);
            if (fiLockFile != null)
            {
                if (DateTime.UtcNow.Subtract(fiLockFile.LastWriteTimeUtc).TotalMinutes < maxWaitTimeMinutes)
                {
                    waitingForLockFile = true;
                    dtLockFileCreated = fiLockFile.LastWriteTimeUtc;

                    var debugMessage = "Lock file found, will wait for it to be deleted or age; " +
                        fiLockFile.Name + " created " + fiLockFile.LastWriteTime.ToString(DATE_TIME_FORMAT);
                    OnDebugEvent(debugMessage);
                }
                else
                {
                    // Lock file has aged; delete it
                    fiLockFile.Delete();
                    lockFileWasAged = true;
                }
            }

            if (!waitingForLockFile)
                return;

            lockFileWasFound = true;

            var dtLastProgressTime = DateTime.UtcNow;
            if (logIntervalMinutes < 1)
                logIntervalMinutes = 1;

            // Initially wait 30 seconds before checking for the lock file again
            // After that, add 30 seconds onto the wait time for every iteration
            var waitTimeSeconds = 30;

            while (waitingForLockFile)
            {
                // Wait for a period of time, the check on the status of the lock file
                SleepSeconds(waitTimeSeconds);

                fiLockFile = FindLockFile(sftp, remoteDirectoryPath, lockFileName);

                if (fiLockFile == null)
                {
                    // Lock file no longer exists
                    waitingForLockFile = false;
                }
                else if (DateTime.UtcNow.Subtract(dtLockFileCreated).TotalMinutes > maxWaitTimeMinutes)
                {
                    // We have waited too long
                    waitingForLockFile = false;
                    lockFileWasAged = true;
                }
                else
                {
                    if (DateTime.UtcNow.Subtract(dtLastProgressTime).TotalMinutes >= logIntervalMinutes)
                    {
                        OnDebugEvent("Waiting for lock file " + fiLockFile.Name);
                        dtLastProgressTime = DateTime.UtcNow;
                    }
                }

                // Increase the wait time by 30 seconds
                waitTimeSeconds += 30;
            }

            // Check for the lock file one more time
            fiLockFile = FindLockFile(sftp, remoteDirectoryPath, lockFileName);
            if (fiLockFile != null)
            {
                // Lock file is over 2 hours old; delete it
                DeleteLockFile(fiLockFile);
            }
        }

        /// <summary>
        /// Connect to the remote host specified by TargetHostOptions
        /// </summary>
        /// <returns>sFtp client</returns>
        public SftpClient ConnectToRemoteHost()
        {
            UpdateParameters();

            var sftpClient = new SftpClient(TargetHostOptions.HostName, TargetHostOptions.Username, mPrivateKeyFile);
            sftpClient.Connect();
            return sftpClient;
        }

        /// <summary>
        /// Copy files from the remote host to a local directory
        /// </summary>
        /// <param name="sourceDirectoryPath">Source directory</param>
        /// <param name="sourceFileNames">Source file names; wildcards are not allowed</param>
        /// <param name="localDirectoryPath">Local target directory</param>
        /// <param name="warnIfMissing">Log warnings if any files are missing.  When false, logs debug messages instead</param>
        /// <returns>
        /// True on success, false if an error
        /// Returns False if if any files were missing, even if warnIfMissing is false
        /// </returns>
        public bool CopyFilesFromRemote(
            string sourceDirectoryPath,
            IReadOnlyCollection<string> sourceFileNames,
            string localDirectoryPath,
            bool warnIfMissing = true)
        {
            // Use scp to retrieve the files
            // scp is faster than sftp, but it has the downside that we can't check for the existence of a file before retrieving it

            var successCount = 0;
            var failCount = 0;

            if (!ParametersValidated)
            {
                // Validate that the required parameters are present and load the private key and passphrase from disk
                // This throws an exception if any parameters are missing
                UpdateParameters();
            }

            try
            {
                if (sourceFileNames.Count == 1)
                    OnDebugEvent(string.Format("Retrieving file {0} from {1} on host {2}", sourceFileNames.First(), sourceDirectoryPath, TargetHostOptions.HostName));
                else
                    OnDebugEvent(string.Format("Retrieving {0} files from {1} on host {2}", sourceFileNames.Count, sourceDirectoryPath, TargetHostOptions.HostName));

                using (var scp = new ScpClient(TargetHostOptions.HostName, TargetHostOptions.Username, mPrivateKeyFile))
                {
                    scp.Connect();

                    foreach (var sourceFileName in sourceFileNames)
                    {
                        var remoteFilePath = clsPathUtils.CombineLinuxPaths(sourceDirectoryPath, sourceFileName);
                        var targetFile = new FileInfo(clsPathUtils.CombinePathsLocalSepChar(localDirectoryPath, sourceFileName));

                        try
                        {
                            scp.Download(remoteFilePath, targetFile);

                            targetFile.Refresh();
                            if (targetFile.Exists)
                                successCount++;
                            else
                                failCount++;
                        }
                        catch (Exception ex)
                        {
                            failCount++;

                            if (ex.Message.ToLower().Contains("no such file"))
                            {
                                if (warnIfMissing)
                                    OnWarningEvent(string.Format("Remote file not found: {0}", remoteFilePath));
                                else
                                    OnDebugEvent(string.Format("Remote file not found: {0}", remoteFilePath));
                            }
                            else
                            {
                                OnWarningEvent(string.Format("Error copying {0}: {1}", remoteFilePath, ex.Message));
                            }
                        }

                    }

                    scp.Disconnect();

                }

                if (successCount > 0 && failCount == 0)
                    return true;

                if (warnIfMissing)
                    OnWarningEvent(string.Format("Error retrieving {0} of {1} files from {2}", failCount, sourceFileNames.Count, sourceDirectoryPath));

                return false;
            }
            catch (Exception ex)
            {
                OnErrorEvent(string.Format("Error copying files from {0}: {1}", sourceDirectoryPath, ex.Message), ex);
                return false;
            }

        }

        /// <summary>
        /// Copy a single file from a local directory to the remote host
        /// </summary>
        /// <param name="sourceFilePath">Source file path</param>
        /// <param name="remoteDirectoryPath">Remote target directory</param>
        /// <returns>True on success, false if an error</returns>
        /// <remarks>Calls UpdateParameters if necessary; that method will throw an exception if there are missing parameters or configuration issues</remarks>
        public bool CopyFileToRemote(string sourceFilePath, string remoteDirectoryPath)
        {
            try
            {
                var sourceFile = new FileInfo(sourceFilePath);
                if (!sourceFile.Exists)
                {
                    OnErrorEvent("Cannot copy file to remote; source file not found: " + sourceFilePath);
                    return false;
                }

                var sourceFileNames = new List<string> { sourceFile.Name };

                var success = CopyFilesToRemote(sourceFile.DirectoryName, sourceFileNames, remoteDirectoryPath);
                return success;
            }
            catch (Exception ex)
            {
                var errMsg = string.Format("Error copying file {0} to {1}: {2}", Path.GetFileName(sourceFilePath), remoteDirectoryPath, ex.Message);
                OnErrorEvent(errMsg, ex);
                return false;
            }
        }

        /// <summary>
        /// Copy files from a local directory to the remote host
        /// </summary>
        /// <param name="sourceDirectoryPath">Source directory</param>
        /// <param name="sourceFileNames">Source file names; wildcards are allowed</param>
        /// <param name="remoteDirectoryPath">Remote target directory</param>
        /// <param name="useLockFile">True to use a lock file when the destination directory might be accessed via multiple managers simultaneously</param>
        /// <param name="managerName">Manager name; stored in the lock file when useLockFile is true</param>
        /// <returns>True on success, false if an error</returns>
        public bool CopyFilesToRemote(
            string sourceDirectoryPath,
            IEnumerable<string> sourceFileNames,
            string remoteDirectoryPath,
            bool useLockFile = false,
            string managerName = "Unknown")
        {
            if (string.IsNullOrWhiteSpace(sourceDirectoryPath))
            {
                OnErrorEvent("Cannot copy files to remote; source directory is empty");
                return false;
            }

            if (!mParametersValidated)
            {
                // Validate that the required parameters are present and load the private key and passphrase from disk
                // This throws an exception if any parameters are missing
                UpdateParameters();
            }

            try
            {
                var sourceDirectory = new DirectoryInfo(sourceDirectoryPath);
                if (!sourceDirectory.Exists)
                {
                    OnErrorEvent("Cannot copy files to remote; source directory not found: " + sourceDirectoryPath);
                    return false;
                }

                var sourceDirectoryFiles = sourceDirectory.GetFiles();
                var filesToCopy = new List<FileInfo>();

                foreach (var sourceFileName in sourceFileNames)
                {
                    if (sourceFileName.Contains("*") || sourceFileName.Contains("?"))
                    {
                        // Filename has a wildcard
                        var matchingFiles = sourceDirectory.GetFiles(sourceFileName);
                        filesToCopy.AddRange(matchingFiles);
                        continue;
                    }

                    var matchFound = false;
                    foreach (var candidateFile in sourceDirectoryFiles)
                    {
                        if (!string.Equals(sourceFileName, candidateFile.Name, StringComparison.OrdinalIgnoreCase))
                            continue;

                        filesToCopy.Add(candidateFile);
                        matchFound = true;
                        break;
                    }

                    if (!matchFound)
                    {
                        OnWarningEvent(string.Format("Source file not found; cannot copy {0} to {1}",
                            Path.Combine(sourceDirectory.FullName, sourceFileName), remoteDirectoryPath));
                    }
                }

                return CopyFilesToRemote(filesToCopy, remoteDirectoryPath, useLockFile, managerName);

            }
            catch (Exception ex)
            {
                var errMsg = string.Format("Error copying files to {0}: {1}", remoteDirectoryPath, ex.Message);
                OnErrorEvent(errMsg, ex);
                return false;
            }
        }

        /// <summary>
        /// Copy files from a local directory to the remote host
        /// </summary>
        /// <param name="sourceFiles">Source files</param>
        /// <param name="remoteDirectoryPath">Remote target directory</param>
        /// <param name="useLockFile">True to use a lock file when the destination directory might be accessed via multiple managers simultaneously</param>
        /// <param name="managerName">Manager name; stored in the lock file when useLockFile is true</param>
        /// <returns>True on success, false if an error</returns>
        public bool CopyFilesToRemote(
            IEnumerable<FileInfo> sourceFiles,
            string remoteDirectoryPath,
            bool useLockFile = false,
            string managerName = "Unknown")
        {

            if (!mParametersValidated)
            {
                // Validate that the required parameters are present and load the private key and passphrase from disk
                // This throws an exception if any parameters are missing
                UpdateParameters();
            }

            try
            {

                var uniqueFiles = GetUniqueFileList(sourceFiles).ToList();
                if (uniqueFiles.Count == 0)
                {
                    OnErrorEvent(string.Format("Cannot copy files to {0}; sourceFiles list is empty", TargetHostOptions.HostName));
                    return false;
                }

                var success = false;

                if (uniqueFiles.Count == 1)
                    OnDebugEvent(string.Format("Copying {0} to {1} on {2}", uniqueFiles.First().Name, remoteDirectoryPath, TargetHostOptions.HostName));
                else
                    OnDebugEvent(string.Format("Copying {0} files to {1} on {2}", uniqueFiles.Count, remoteDirectoryPath, TargetHostOptions.HostName));

                SftpClient sftp;

                if (useLockFile)
                {
                    sftp = new SftpClient(TargetHostOptions.HostName, TargetHostOptions.Username, mPrivateKeyFile);
                    sftp.Connect();
                }
                else
                {
                    sftp = null;
                }

                using (var scp = new ScpClient(TargetHostOptions.HostName, TargetHostOptions.Username, mPrivateKeyFile))
                {
                    scp.Connect();

                    var lockFileDefined = false;
                    var lockFileDataFile = string.Empty;

                    foreach (var sourceFile in uniqueFiles)
                    {
                        if (!sourceFile.Exists)
                        {
                            OnWarningEvent(string.Format("Source file not found; cannot copy {0} to {1} on {2}",
                                                         sourceFile.FullName, remoteDirectoryPath, TargetHostOptions.HostName));
                            continue;
                        }

                        if (useLockFile && !lockFileDefined)
                        {
                            // Only create a lock file for the first file we copy
                            // That lock file will not be deleted until after all files have been copied

                            CheckForRemoteLockFile(sftp, remoteDirectoryPath, sourceFile.Name, out var lockFileWasFound, out var lockFileWasAged);

                            if (lockFileWasFound && !lockFileWasAged)
                            {
                                // Another manager was copying this file
                                // If the file length now matches the source file length, assume the file is up-to-date remotely
                                var matchingFiles = GetRemoteFileListing(remoteDirectoryPath, sourceFile.Name);

                                if (matchingFiles.Count > 0)
                                {
                                    var remoteFile = matchingFiles.First().Value;
                                    if (remoteFile.Length == sourceFile.Length)
                                    {
                                        OnStatusEvent(string.Format("Remote file was being copied via locks; " +
                                                                    "file length is now {0:N0} bytes so assuming up-to-date: {1}",
                                                                    remoteFile.Length, remoteFile.FullName
                                                                    ));

                                        success = true;
                                        continue;
                                    }
                                }

                            }

                            var remoteLockFilePath = CreateRemoteLockFile(sftp, remoteDirectoryPath, sourceFile.Name, managerName);
                            if (string.IsNullOrWhiteSpace(remoteLockFilePath))
                            {
                                // Problem creating the lock file; abort the copy
                                OnStatusEvent("Error creating lock file at: " + remoteLockFilePath + "; aborting copy to remote");

                                return false;
                            }

                            lockFileDefined = true;
                            lockFileDataFile = sourceFile.Name;
                        }

                        OnDebugEvent("  Copying " + sourceFile.FullName);

                        var targetFilePath = clsPathUtils.CombineLinuxPaths(remoteDirectoryPath, sourceFile.Name);
                        scp.Upload(sourceFile, targetFilePath);

                        success = true;
                    }

                    if (useLockFile && !string.IsNullOrWhiteSpace(lockFileDataFile))
                    {
                        DeleteLockFile(sftp, remoteDirectoryPath, lockFileDataFile);
                    }

                    scp.Disconnect();

                }

                if (useLockFile)
                {
                    sftp.Disconnect();
                }

                if (success)
                {
                    return true;
                }

                OnErrorEvent(string.Format("Cannot copy files to {0}; all of the files in sourceFiles are missing", TargetHostOptions.HostName));
                return false;

            }
            catch (Exception ex)
            {
                OnErrorEvent(string.Format("Error copying files to {0} on {1}: {2}", remoteDirectoryPath, TargetHostOptions.HostName, ex.Message), ex);
                return false;
            }

        }

        /// <summary>
        /// Validates that the remote directory exists, creating it if missing
        /// </summary>
        /// <param name="remoteDirectoryPath"></param>
        /// <returns>True on success, otherwise false</returns>
        /// <remarks>The parent directory of remoteDirectoryPath must already exist</remarks>
        public bool CreateRemoteDirectory(string remoteDirectoryPath)
        {
            if (!ParametersValidated)
                throw new Exception("Call UpdateParameters before calling CreateRemoteDirectory");

            return CreateRemoteDirectories(new List<string> { remoteDirectoryPath });
        }

        /// <summary>
        /// Validates that the remote directories exists, creating any that are missing
        /// </summary>
        /// <param name="remoteDirectories"></param>
        /// <returns>True on success, otherwise false</returns>
        /// <remarks>The parent directory of each item in remoteDirectories must already exist</remarks>
        public bool CreateRemoteDirectories(IReadOnlyCollection<string> remoteDirectories)
        {

            if (!ParametersValidated)
                throw new Exception("Call UpdateParameters before calling CreateRemoteDirectories");

            try
            {
                if (remoteDirectories.Count == 0)
                    return true;

                // Keys in this dictionary are parent directory paths; values are subdirectories to find in each
                var parentDirectories = new Dictionary<string, SortedSet<string>>();
                foreach (var remoteDirectory in remoteDirectories)
                {
                    var parentPath = clsPathUtils.GetParentDirectoryPath(remoteDirectory, out var directoryName);
                    if (string.IsNullOrWhiteSpace(parentPath))
                        continue;

                    if (!parentDirectories.TryGetValue(parentPath, out var subDirectories))
                    {
                        subDirectories = new SortedSet<string>();
                        parentDirectories.Add(parentPath, subDirectories);
                    }

                    if (!subDirectories.Contains(directoryName))
                        subDirectories.Add(directoryName);

                }

                OnDebugEvent("Verifying directories on host " + TargetHostOptions.HostName);

                using (var sftp = new SftpClient(TargetHostOptions.HostName, TargetHostOptions.Username, mPrivateKeyFile))
                {
                    sftp.Connect();
                    foreach (var parentDirectory in parentDirectories)
                    {
                        var remoteDirectoryPath = parentDirectory.Key;
                        OnDebugEvent("  checking " + remoteDirectoryPath);

                        var filesAndFolders = sftp.ListDirectory(remoteDirectoryPath);
                        var remoteSubdirectories = new SortedSet<string>();

                        foreach (var item in filesAndFolders)
                        {
                            if (!item.IsDirectory || item.Name == "." || item.Name == "..")
                            {
                                continue;
                            }

                            if (!remoteSubdirectories.Contains(item.Name))
                                remoteSubdirectories.Add(item.Name);
                        }

                        foreach (var directoryToVerify in parentDirectory.Value)
                        {
                            if (remoteSubdirectories.Contains(directoryToVerify))
                            {
                                OnDebugEvent("    found " + directoryToVerify);
                                continue;
                            }
                            var directoryPathToCreate = clsPathUtils.CombineLinuxPaths(remoteDirectoryPath, directoryToVerify);

                            OnDebugEvent("  creating " + directoryPathToCreate);
                            sftp.CreateDirectory(directoryPathToCreate);
                        }

                    }
                    sftp.Disconnect();
                }

                return true;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error creating remote directories", ex);
                return false;
            }
        }

        /// <summary>
        /// Create a new lock file named dataFileName + ".lock" in directory remoteDirectoryPath
        /// </summary>
        /// <param name="sftp">Sftp client</param>
        /// <param name="remoteDirectoryPath">Target directory on the remote server (use Linux-style forward slashes)</param>
        /// <param name="dataFileName">Data file name (without .lock)</param>
        /// <param name="managerName">Manager name, stored in the lock file </param>
        /// <returns>Full path to the lock file; empty string if a problem</returns>
        private string CreateRemoteLockFile(SftpClient sftp, string remoteDirectoryPath, string dataFileName, string managerName)
        {

            var lockFileContents = new[]
            {
                "Date: " + DateTime.Now.ToString(DATE_TIME_FORMAT),
                "Manager: " + managerName
            };

            var lockFileData = new StringBuilder();
            foreach (var dataLine in lockFileContents)
            {
                lockFileData.AppendLine(dataLine);
            }

            var remoteLockFilePath = clsPathUtils.CombineLinuxPaths(remoteDirectoryPath, dataFileName + LOCK_FILE_EXTENSION);

            OnDebugEvent("  creating lock file at " + remoteLockFilePath);

            using (var lockFileWriter = sftp.Create(remoteLockFilePath))
            {
                var buffer = Encoding.ASCII.GetBytes(lockFileData.ToString());
                lockFileWriter.Write(buffer, 0, buffer.Length);
            }

            // Wait 2 to 5 seconds, then re-open the file to make sure it was created by this manager
            var oRandom = new Random();
            SleepSeconds(oRandom.Next(2, 5));

            var lockFileContentsNew = sftp.ReadAllLines(remoteLockFilePath, Encoding.ASCII);

            if (!LockFilesMatch(remoteLockFilePath, lockFileContents, lockFileContentsNew, out var errorMessage))
            {
                // Lock file content doesn't match the expected value
                OnWarningEvent(errorMessage);
                return string.Empty;
            }

            return remoteLockFilePath;

        }

        /// <summary>
        /// Decode a password
        /// </summary>
        /// <param name="encodedPwd">Encoded password</param>
        /// <returns>Clear text password</returns>
        public static string DecodePassword(string encodedPwd)
        {
            return EncryptDecrypt(encodedPwd, false);
        }

        /// <summary>
        /// Delete the lock file from the remote host
        /// </summary>
        /// <param name="lockFile"></param>
        /// <remarks>Requires an active sftp session</remarks>
        private void DeleteLockFile(SftpFile lockFile)
        {
            try
            {
                lockFile.Delete();
            }
            catch (Exception)
            {
                // Ignore errors here
            }
        }

        /// <summary>
        /// Delete the lock file from the remote host
        /// </summary>
        /// <param name="sftp"></param>
        /// <param name="remoteDirectoryPath"></param>
        /// <param name="dataFileName"></param>
        private void DeleteLockFile(SftpClient sftp, string remoteDirectoryPath, string dataFileName)
        {
            try
            {
                var lockFileName = dataFileName + LOCK_FILE_EXTENSION;
                var fiLockFile = FindLockFile(sftp, remoteDirectoryPath, lockFileName);
                fiLockFile?.Delete();
            }
            catch (Exception)
            {
                // Ignore errors here
            }
        }

        /// <summary>
        /// Delete a directory on a remote host (must already be empty)
        /// </summary>
        /// <param name="sftpClient"></param>
        /// <param name="directoryPath"></param>
        public void DeleteDirectory(SftpClient sftpClient, string directoryPath)
        {
            sftpClient.DeleteDirectory(directoryPath);
        }

        /// <summary>
        /// Delete a remote directory, including all files and subdirectories
        /// </summary>
        /// <param name="directoryPath"></param>
        public void DeleteDirectoryAndContents(string directoryPath)
        {

            if (!ParametersValidated)
                throw new Exception("Call UpdateParameters before calling DeleteDirectoryAndContents");

            try
            {
                if (string.IsNullOrEmpty(directoryPath))
                    throw new Exception("directoryPath parameter is empty; nothing to delete");

                OnDebugEvent("Delete directory on host " + TargetHostOptions.HostName + ": " + directoryPath);

                using (var sftp = new SftpClient(TargetHostOptions.HostName, TargetHostOptions.Username, mPrivateKeyFile))
                {
                    sftp.Connect();

                    const int MAX_DEPTH = -1;

                    // Keys are filenames, values are SftpFile objects
                    var filesAndDirectories = new Dictionary<string, SftpFile>();
                    var directoriesToDelete = new SortedSet<string>();

                    GetRemoteFilesAndDirectories(sftp, directoryPath, true, MAX_DEPTH, filesAndDirectories);

                    foreach (var workDirFile in filesAndDirectories)
                    {
                        if (workDirFile.Value.IsDirectory)
                        {
                            if (workDirFile.Value.Name == "." || workDirFile.Value.Name == "..")
                                continue;

                            if (!directoriesToDelete.Contains(workDirFile.Key))
                                directoriesToDelete.Add(workDirFile.Key);

                            continue;
                        }

                        try
                        {
                            OnDebugEvent("  deleting " + workDirFile.Key);
                            workDirFile.Value.Delete();

                            var parentPath = clsPathUtils.GetParentDirectoryPath(workDirFile.Value.FullName, out _);

                            if (!directoriesToDelete.Contains(parentPath))
                                directoriesToDelete.Add(parentPath);

                        }
                        catch (Exception ex)
                        {
                            OnErrorEvent(string.Format("Error deleting file {0}: {1}", workDirFile.Value.Name, ex.Message));
                        }

                    }

                    foreach (var directoryToDelete in (from item in directoriesToDelete orderby item descending select item))
                    {
                        try
                        {
                            OnDebugEvent("  deleting directory " + directoryToDelete);
                            sftp.DeleteDirectory(directoryToDelete);
                        }
                        catch (Exception ex)
                        {
                            OnErrorEvent(string.Format("Error deleting directory {0}: {1}", directoryToDelete, ex.Message));
                        }
                    }

                    sftp.Disconnect();
                }

            }
            catch (Exception ex)
            {
                OnErrorEvent("Error deleting remote files", ex);
            }

        }

        /// <summary>
        /// Delete a file on a remote host
        /// </summary>
        /// <param name="sftpClient"></param>
        /// <param name="filePath"></param>
        public void DeleteFile(SftpClient sftpClient, string filePath)
        {
            sftpClient.Delete(filePath);
        }

        /// <summary>
        /// Encode a password
        /// </summary>
        /// <param name="password">Clear text password</param>
        /// <returns>Encoded password</returns>
        public static string EncodePassword(string password)
        {
            return EncryptDecrypt(password, true);
        }

        /// <summary>
        /// Encode or decode a password
        /// </summary>
        /// <param name="password">Password</param>
        /// <param name="encrypt">True to encode the password; false to decode the password</param>
        /// <returns>Encoded password</returns>
        private static string EncryptDecrypt(string password, bool encrypt)
        {
            // Convert the password string to a character array
            var pwdChars = password.ToCharArray();
            var pwdCharsAdj = new List<char>();

            var pwdBytes = pwdChars.Select(t => (byte)t).ToList();

            var modTest = encrypt ? 1 : 0;

            // Modify the byte array by shifting alternating bytes up or down and converting back to char
            for (var index = 0; index < pwdBytes.Count; index++)
            {
                if (index % 2 == modTest)
                    pwdBytes[index] += 1;
                else
                    pwdBytes[index] -= 1;

                pwdCharsAdj.Add((char)pwdBytes[index]);
            }

            return string.Join("", pwdCharsAdj);
        }

        /// <summary>
        /// Look for the lock file in the given remote directory
        /// </summary>
        /// <param name="sftp">Sftp client</param>
        /// <param name="remoteDirectoryPath"></param>
        /// <param name="lockFileName"></param>
        /// <returns>SftpFile info if found, otherwise null</returns>
        private SftpFile FindLockFile(SftpClient sftp, string remoteDirectoryPath, string lockFileName)
        {
            const bool recurse = false;

            var matchingFiles = new Dictionary<string, SftpFile>();

            GetRemoteFileListing(sftp, new List<string> { remoteDirectoryPath }, lockFileName, recurse, matchingFiles);

            return matchingFiles.Any() ? matchingFiles.First().Value : null;
        }

        /// <summary>
        /// Retrieve a listing of files in the remoteDirectoryPath directory on the remote host
        /// </summary>
        /// <param name="remoteDirectoryPath">Remote target directory</param>
        /// <param name="fileMatchSpec">Filename to find, or files to find if wildcards are used</param>
        /// <param name="recurse">True to find files in subdirectories</param>
        /// <returns>Dictionary of matching files, where keys are full file paths and values are instances of SFtpFile</returns>
        public Dictionary<string, SftpFile> GetRemoteFileListing(string remoteDirectoryPath, string fileMatchSpec, bool recurse = false)
        {
            var matchingFiles = new Dictionary<string, SftpFile>();

            if (!ParametersValidated)
                throw new Exception("Call UpdateParameters before calling GetRemoteFileListing");

            try
            {
                if (string.IsNullOrWhiteSpace(remoteDirectoryPath))
                    throw new ArgumentException("Remote directory path cannot be empty", nameof(remoteDirectoryPath));

                if (string.IsNullOrWhiteSpace(fileMatchSpec))
                    fileMatchSpec = "*";

                OnDebugEvent(string.Format("Getting file listing for {0} on host {1}", remoteDirectoryPath, TargetHostOptions.HostName));

                using (var sftp = new SftpClient(TargetHostOptions.HostName, TargetHostOptions.Username, mPrivateKeyFile))
                {
                    sftp.Connect();
                    GetRemoteFileListing(sftp, new List<string> { remoteDirectoryPath }, fileMatchSpec, recurse, matchingFiles);
                    sftp.Disconnect();
                }

                return matchingFiles;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error retrieving remote file listing", ex);
                return matchingFiles;
            }
        }

        /// <summary>
        /// Retrieve a listing of files in the specified remote directories on the remote host
        /// </summary>
        /// <param name="sftp">sftp client</param>
        /// <param name="remoteDirectoryPaths">Paths to check</param>
        /// <param name="fileMatchSpec">Filename to find, or files to find if wildcards are used</param>
        /// <param name="recurse">True to find files in subdirectories</param>
        /// <param name="matchingFiles">Dictionary of matching files, where keys are full file paths and values are instances of SFtpFile</param>
        private void GetRemoteFileListing(
            SftpClient sftp,
            IEnumerable<string> remoteDirectoryPaths,
            string fileMatchSpec,
            bool recurse,
            IDictionary<string, SftpFile> matchingFiles)
        {
            foreach (var remoteDirectory in remoteDirectoryPaths)
            {
                if (string.IsNullOrWhiteSpace(remoteDirectory))
                {
                    OnWarningEvent("Ignoring empty remote directory name from remoteDirectoryPaths in GetRemoteFileListing");
                    continue;
                }

                var filesAndFolders = sftp.ListDirectory(remoteDirectory);
                var subdirectoryPaths = new List<string>();

                foreach (var item in filesAndFolders)
                {
                    if (item.IsDirectory)
                    {
                        if (item.Name == "." || item.Name == "..")
                            continue;

                        subdirectoryPaths.Add(item.FullName);
                        continue;
                    }

                    if (fileMatchSpec == "*" || clsPathUtils.FitsMask(item.Name, fileMatchSpec))
                    {
                        try
                        {
                            matchingFiles.Add(item.FullName, item);
                        }
                        catch (ArgumentException)
                        {
                            OnWarningEvent("Skipping duplicate filename: " + item.FullName);
                        }

                    }
                }

                if (recurse && subdirectoryPaths.Count > 0)
                {
                    // Recursively call this function
                    GetRemoteFileListing(sftp, subdirectoryPaths, fileMatchSpec, true, matchingFiles);
                }
            }

        }

        /// <summary>
        /// Retrieve a listing of all files and directories below a given directory on the remote host
        /// </summary>
        /// <param name="remoteDirectoryPath">Directory to check</param>
        /// <param name="recurse">True to find files and directories in subdirectories</param>
        /// <param name="maxDepth">Maximum depth to recurse (-1 for infinite)</param>
        /// <returns>Dictionary of matching files and directories, where keys are full paths and values are instances of SFtpFile</returns>
        public IDictionary<string, SftpFile> GetRemoteFilesAndDirectories(string remoteDirectoryPath, bool recurse = false, int maxDepth = -1)
        {

            var filesAndDirectories = new Dictionary<string, SftpFile>();

            if (!mParametersValidated)
                throw new Exception("Call UpdateParameters before calling GetRemoteFilesAndDirectories");

            try
            {
                if (string.IsNullOrWhiteSpace(remoteDirectoryPath))
                    throw new ArgumentException("Remote directory path cannot be empty", nameof(remoteDirectoryPath));

                OnDebugEvent(string.Format("Getting file/directory listing for {0} on host {1}", remoteDirectoryPath, TargetHostOptions.HostName));

                using (var sftp = new SftpClient(TargetHostOptions.HostName, TargetHostOptions.Username, mPrivateKeyFile))
                {
                    sftp.Connect();
                    GetRemoteFilesAndDirectories(sftp, remoteDirectoryPath, recurse, maxDepth, filesAndDirectories);
                    sftp.Disconnect();
                }

                return filesAndDirectories;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error retrieving remote file/directory listing", ex);
                return filesAndDirectories;
            }

        }

        /// <summary>
        /// Retrieve a listing of all files and directories below a given directory on the remote host
        /// </summary>
        /// <param name="sftp">sftp client</param>
        /// <param name="remoteDirectoryPath">Directory to check</param>
        /// <param name="recurse">True to find files and directories in subdirectories</param>
        /// <param name="maxDepth">Maximum depth to recurse (-1 for infinite)</param>
        /// <param name="filesAndDirectories">Dictionary of matching files and directories, where keys are full paths and values are instances of SFtpFile</param>
        public void GetRemoteFilesAndDirectories(
            SftpClient sftp,
            string remoteDirectoryPath,
            bool recurse,
            int maxDepth,
            IDictionary<string, SftpFile> filesAndDirectories)
        {

            if (string.IsNullOrWhiteSpace(remoteDirectoryPath))
            {
                throw new ArgumentException("Remote directory path cannot be empty", nameof(remoteDirectoryPath));
            }

            var subdirectoryPaths = new List<string>();

            List<SftpFile> remoteFilesAndDirectories;

            try
            {
                remoteFilesAndDirectories = sftp.ListDirectory(remoteDirectoryPath).ToList();
            }
            catch (SftpPathNotFoundException)
            {
                OnDebugEvent("Directory does not exist: " + remoteDirectoryPath);
                return;
            }

            foreach (var item in remoteFilesAndDirectories)
            {
                if (item.IsDirectory)
                {
                    if (item.Name == "." || item.Name == "..")
                        continue;

                    subdirectoryPaths.Add(item.FullName);
                }

                try
                {
                    filesAndDirectories.Add(item.FullName, item);
                }
                catch (ArgumentException)
                {
                    OnWarningEvent("Skipping duplicate file or directory: " + item.FullName);
                }
            }

            if (recurse && subdirectoryPaths.Count > 0 && maxDepth != 0)
            {
                int newDepth;
                if (maxDepth < 0)
                {
                    newDepth = -1;
                }
                else
                {
                    newDepth = maxDepth > 0 ? maxDepth - 1 : 0;
                }

                // Recursively call this function
                foreach (var subDirectoryPath in subdirectoryPaths)
                    GetRemoteFilesAndDirectories(sftp, subDirectoryPath, true, newDepth, filesAndDirectories);
            }

        }

        /// <summary>
        /// Retrieve a listing of all files and directories below TargetHostOptions.DestinationPath
        /// </summary>
        /// <returns>Dictionary of matching files and directories, where keys are full paths and values are instances of SFtpFile</returns>
        public IDictionary<string, SftpFile> GetTargetHostFilesAndDirectories()
        {
            try
            {
                return GetRemoteFilesAndDirectories(TargetHostOptions.DestinationPath, true);
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error retrieving remote file/directory listing", ex);
                return new Dictionary<string, SftpFile>();
            }

        }

        /// <summary>
        /// Return a listing of files where there are no duplicate files (based on full file path)
        /// </summary>
        /// <param name="files">File list to check</param>
        /// <param name="ignoreCase">True to ignore file case</param>
        /// <returns></returns>
        private static IEnumerable<FileInfo> GetUniqueFileList(IEnumerable<FileInfo> files, bool ignoreCase = true)
        {
            var comparer = ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            var uniqueFiles = new Dictionary<string, FileInfo>(comparer);

            foreach (var file in files)
            {
                if (uniqueFiles.ContainsKey(file.FullName))
                    continue;

                uniqueFiles.Add(file.FullName, file);
            }

            return uniqueFiles.Values;
        }

        private void LoadRSAPrivateKey()
        {
            OnDebugEvent("Loading RSA private key file");

            var keyFile = new FileInfo(TargetHostOptions.PrivateKeyFile);
            if (!keyFile.Exists)
            {
                throw new FileNotFoundException("Private key file not found: " + keyFile.FullName);
            }

            var passPhraseFile = new FileInfo(TargetHostOptions.PassphraseFile);
            if (!passPhraseFile.Exists)
            {
                throw new FileNotFoundException("Passpharse file not found: " + passPhraseFile.FullName);
            }

            MemoryStream keyFileStream;
            string passphraseEncoded;

            try
            {
                OnDebugEvent("  reading " + keyFile.FullName);
                using (var reader = new StreamReader(new FileStream(keyFile.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                {
                    keyFileStream = new MemoryStream(Encoding.ASCII.GetBytes(reader.ReadToEnd()));
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error reading the private key file: " + ex.Message, ex);
            }

            try
            {
                OnDebugEvent("  reading " + passPhraseFile.FullName);
                using (var reader = new StreamReader(new FileStream(passPhraseFile.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                {
                    passphraseEncoded = reader.ReadLine();
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error reading the passpharse file: " + ex.Message, ex);
            }

            try
            {
                mPrivateKeyFile = new PrivateKeyFile(keyFileStream, DecodePassword(passphraseEncoded));
            }
            catch (InvalidOperationException ex)
            {
                if (ex.Message.Contains("Invalid data type") || ex.Message.Contains("cannot be more than 4 bytes"))
                    throw new Exception("Invalid passphrase for the private key file; see manager params RemoteHostPrivateKeyFile and RemoteHostPassphraseFile: " + ex.Message, ex);

                throw new Exception("Error instantiating the private key " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Compare the original contents of a lock file with new contents
        /// </summary>
        /// <param name="lockFilePath">Lock file path (could be Windows or Linux-based; only used for error messages)</param>
        /// <param name="lockFileContents">Original lock file contents</param>
        /// <param name="lockFileContentsNew">Current lock file contents</param>
        /// <param name="errorMessage">Output: error message</param>
        /// <returns>True if the contents match, otherwise false</returns>
        public static bool LockFilesMatch(
            string lockFilePath,
            IReadOnlyList<string> lockFileContents,
            IReadOnlyList<string> lockFileContentsNew,
            out string errorMessage)
        {

            if (lockFileContentsNew.Count < lockFileContents.Count)
            {
                // Remote lock file is shorter than we expected
                errorMessage = "Lock file does have the expected content: " + lockFilePath;
                return false;
            }

            for (var i = 0; i < lockFileContentsNew.Count; i++)
            {
                if (i >= lockFileContents.Count)
                {
                    // Lock file now has more rows than we expected; that's ok
                    break;
                }

                if (string.Equals(lockFileContents[i], lockFileContentsNew[i]))
                    continue;

                // Lock file content doesn't match the expected value
                errorMessage = "Another manager replaced the lock file that this manager created at " + lockFilePath;
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        /// <summary>
        /// Move the specified files to targetRemoteDirectory, optionally deleting files in filesToDelete
        /// </summary>
        /// <param name="sourceFilePaths"></param>
        /// <param name="targetRemoteDirectory"></param>
        /// <param name="filesToDelete">File names or paths in sourceFilePaths to delete instead of moving</param>
        /// <returns></returns>
        public bool MoveFiles(
            IReadOnlyCollection<string> sourceFilePaths,
            string targetRemoteDirectory,
            List<string> filesToDelete)
        {
            if (!ParametersValidated)
                throw new Exception("Call UpdateParameters before calling MoveFiles");

            try
            {
                if (sourceFilePaths.Count == 0)
                    return true;

                var fileNamesToDelete = new SortedSet<string>();
                var filePathsToDelete = new SortedSet<string>();

                foreach (var fileToDelete in filesToDelete)
                {
                    if (fileToDelete.StartsWith("/") || fileToDelete.StartsWith("\\"))
                    {
                        // Path is rooted
                        if (!filePathsToDelete.Contains(fileToDelete))
                            filePathsToDelete.Add(fileToDelete);
                        continue;
                    }

                    // Note that Path.GetFileName handles both Windows and Linux file paths
                    var fileName = Path.GetFileName(fileToDelete);

                    if (!fileNamesToDelete.Contains(fileName))
                        fileNamesToDelete.Add(fileName);
                }

                OnDebugEvent("Moving files on host " + TargetHostOptions.HostName + " to " + targetRemoteDirectory);

                using (var sftp = new SftpClient(TargetHostOptions.HostName, TargetHostOptions.Username, mPrivateKeyFile))
                {
                    sftp.Connect();
                    foreach (var remoteFilePath in sourceFilePaths)
                    {

                        var fileName = Path.GetFileName(remoteFilePath);

                        if (fileName != null && fileNamesToDelete.Contains(fileName) ||
                            filePathsToDelete.Contains(remoteFilePath))
                        {
                            try
                            {
                                // Delete this file instead of moving it
                                OnDebugEvent("  deleting " + remoteFilePath);
                                sftp.Delete(remoteFilePath);
                            }
                            catch (Exception ex)
                            {
                                OnErrorEvent(string.Format("Error deleting {0}: {1}", remoteFilePath, ex.Message));
                            }

                            continue;
                        }

                        var newFilePath = clsPathUtils.CombineLinuxPaths(targetRemoteDirectory, fileName);

                        try
                        {
                            // Move the file; if it already exists in the destination, an exception will occur (with message "Failure")
                            OnDebugEvent("  moving " + remoteFilePath);
                            sftp.RenameFile(remoteFilePath, newFilePath);
                        }
                        catch (Exception ex)
                        {
                            OnErrorEvent(string.Format("Error moving {0}: {1}", remoteFilePath, ex.Message));
                        }
                    }

                    sftp.Disconnect();
                }

                return true;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error moving files", ex);
                return false;
            }
        }

        /// <summary>
        /// Sleep for the given number of seconds
        /// </summary>
        /// <param name="waitTimeSeconds"></param>
        private void SleepSeconds(int waitTimeSeconds)
        {
            clsProgRunner.SleepMilliseconds(waitTimeSeconds * 1000);
        }

        /// <summary>
        /// Validate settings in TargetHostOption, then load the private key information
        /// from RemoteHostPrivateKeyFile and RemoteHostPassphraseFile
        /// </summary>
        /// <remarks>
        /// Throws an exception if any parameters are missing or empty
        /// Also throws an exception if there is an error reading the private key information
        /// </remarks>
        public void UpdateParameters()
        {

            // Use settings defined for this manager
            OnDebugEvent("Updating remote transfer settings using manager defaults");

            if (string.IsNullOrWhiteSpace(TargetHostOptions.HostName))
                throw new Exception("Remote HostName parameter is empty; check the manager parameters");

            if (string.IsNullOrWhiteSpace(TargetHostOptions.Username))
                throw new Exception("Remote Username parameter is empty; check the manager parameters");

            if (string.IsNullOrWhiteSpace(TargetHostOptions.PrivateKeyFile))
                throw new Exception("Remote PrivateKeyFile parameter is empty; check the manager parameters");

            if (string.IsNullOrWhiteSpace(TargetHostOptions.PassphraseFile))
                throw new Exception("Remote PassphraseFile parameter is empty; check the manager parameters");

            if (string.IsNullOrWhiteSpace(TargetHostOptions.DestinationPath))
                throw new Exception("Remote DestinationFolderPath parameter is empty; check the manager parameters");

            // Load the RSA private key info
            LoadRSAPrivateKey();

            mParametersValidated = true;
        }

        #endregion

    }
}
