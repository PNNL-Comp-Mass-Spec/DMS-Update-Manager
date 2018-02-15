﻿using System;
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
        /// <returns>True on success, false if an error</returns>
        public bool CopyFilesToRemote(
            string sourceDirectoryPath,
            IEnumerable<string> sourceFileNames,
            string remoteDirectoryPath)
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

                return CopyFilesToRemote(filesToCopy, remoteDirectoryPath);

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
        /// <returns>True on success, false if an error</returns>
        public bool CopyFilesToRemote(IEnumerable<FileInfo> sourceFiles, string remoteDirectoryPath)
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

                using (var scp = new ScpClient(TargetHostOptions.HostName, TargetHostOptions.Username, mPrivateKeyFile))
                {
                    scp.Connect();

                    foreach (var sourceFile in uniqueFiles)
                    {
                        if (!sourceFile.Exists)
                        {
                            OnWarningEvent(string.Format("Source file not found; cannot copy {0} to {1} on {2}",
                                                         sourceFile.FullName, remoteDirectoryPath, TargetHostOptions.HostName));
                            continue;
                        }

                        OnDebugEvent("  Copying " + sourceFile.FullName);

                        var targetFilePath = clsPathUtils.CombineLinuxPaths(remoteDirectoryPath, sourceFile.Name);
                        scp.Upload(sourceFile, targetFilePath);

                        success = true;
                    }

                    scp.Disconnect();

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
        /// Decode a password
        /// </summary>
        /// <param name="encodedPwd">Encoded password</param>
        /// <returns>Clear text password</returns>
        public static string DecodePassword(string encodedPwd)
        {
            return EncryptDecrypt(encodedPwd, false);
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
        /// Delete a directory on a remote host
        /// </summary>
        /// <param name="sftpClient"></param>
        /// <param name="directoryPath"></param>
        public void DeleteDirectory(SftpClient sftpClient, string directoryPath)
        {
            sftpClient.DeleteDirectory(directoryPath);
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
        /// Update cached parameters using MgrParams and JobParams
        /// In addition, loads the private key information from RemoteHostPrivateKeyFile and RemoteHostPassphraseFile
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
