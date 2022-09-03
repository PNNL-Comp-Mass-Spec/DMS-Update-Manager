using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Renci.SshNet;
using Renci.SshNet.Sftp;

namespace DMSUpdateManager
{
    /// <summary>
    /// This class tracks information about a directory
    /// The directory can be on the local computer, on a remote Windows share,
    /// or could be one we'll access using SSH and SFTP
    /// </summary>
    public class DirectoryContainer : IDisposable
    {
        // Ignore Spelling: SFtpFile

        private readonly RemoteUpdateUtility mUpdateUtility;

        private readonly SftpClient mSftpClient;

        /// <summary>
        /// Directory path if updating a local directory
        /// RemoteHostInfo.DirectoryPath if updating a remote host
        /// </summary>
        public string DirectoryPath { get; }

        /// <summary>
        /// Tracks directories on the remote host
        /// Keys are full paths to the directory (Linux slashes); values are the subdirectories of the given directory
        /// </summary>
        private IDictionary<string, List<SftpFile>> RemoteHostDirectories { get; }

        /// <summary>
        /// Tracks files on the remote host
        /// Keys are full paths to the directory (Linux slashes); values are the files in the given directory
        /// </summary>
        private IDictionary<string, List<SftpFile>> RemoteHostFiles { get; }

        /// <summary>
        /// Tracks files and directories on the remote host
        /// Keys are full paths to each file or directory; values are instances of SFtpFile
        /// </summary>
        private IDictionary<string, SftpFile> RemoteHostFilesAndDirectories { get; }

        /// <summary>
        /// Ignored if updating a local directory (or Windows share)
        /// Remote host info if updating a remote host
        /// </summary>
        public RemoteHostConnectionInfo RemoteHostInfo { get; }

        /// <summary>
        /// True if tracking files and directories on a remote host
        /// </summary>
        public bool TrackingRemoteHostDirectory { get; }

        /// <summary>
        /// Full path to the parent directory of either DirectoryPath or RemoteHostInfo.DirectoryPath
        /// </summary>
        public string ParentPath { get; }

        /// <summary>
        /// Constructor for a local directory (or Windows share)
        /// </summary>
        public DirectoryContainer(string directoryPath) :
            this(directoryPath, new RemoteHostConnectionInfo(), false)
        {
        }

        /// <summary>
        /// Constructor for updating a remote host
        /// </summary>
        public DirectoryContainer(RemoteHostConnectionInfo remoteHostInfo) :
            this(string.Empty, remoteHostInfo, true)
        {
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        private DirectoryContainer(string directoryPath, RemoteHostConnectionInfo remoteHostInfo, bool trackingRemoteHostDirectory)
        {
            mUpdateUtility = new RemoteUpdateUtility(remoteHostInfo);

            RemoteHostDirectories = new Dictionary<string, List<SftpFile>>();
            RemoteHostFiles = new Dictionary<string, List<SftpFile>>();
            RemoteHostFilesAndDirectories = new Dictionary<string, SftpFile>();
            RemoteHostInfo = remoteHostInfo;
            TrackingRemoteHostDirectory = trackingRemoteHostDirectory;

            if (trackingRemoteHostDirectory)
            {
                DirectoryPath = remoteHostInfo.BaseDirectoryPath;
                ParentPath = GetRemoteDirectoryParent(DirectoryPath);

                mSftpClient = mUpdateUtility.ConnectToRemoteHost();
                if (!mSftpClient.IsConnected)
                {
                    throw new Exception(string.Format("Unable to connect to remote host {0} as user {1}; unknown error",
                                                      RemoteHostInfo.HostName, RemoteHostInfo.Username));
                }
            }
            else
            {
                DirectoryPath = directoryPath;

                var parentDirectory = new DirectoryInfo(directoryPath).Parent;
                ParentPath = parentDirectory?.FullName ?? string.Empty;
            }
        }

        /// <summary>
        /// If RemoteHostDirectories does not contain remoteDirectory,
        /// retrieve the files and directories in that remote directory
        /// </summary>
        /// <param name="remoteDirectory"></param>
        /// <param name="refreshRemoteData">When true, refresh the files and subdirectories of directoryPath</param>
        private void AddUpdateRemoteHostFilesAndDirectories(string remoteDirectory, bool refreshRemoteData)
        {
            var directoryInfoDefined = RemoteHostDirectories.ContainsKey(remoteDirectory);

            if (directoryInfoDefined && !refreshRemoteData)
                return;

            // When processing a new directory, recurse with maxDepth = 1
            // When processing a directory that was previously processed, do not recurse
            var recurse = !directoryInfoDefined;

            const int MAX_DEPTH = 1;

            var filesAndDirectories = new Dictionary<string, SftpFile>();

            // Get files on the remote host
            mUpdateUtility.GetRemoteFilesAndDirectories(mSftpClient, remoteDirectory, recurse, MAX_DEPTH, filesAndDirectories);

            var directories = new List<SftpFile>();
            var files = new List<SftpFile>();

            foreach (var item in filesAndDirectories)
            {
                if (RemoteHostFilesAndDirectories.ContainsKey(item.Key))
                    RemoteHostFilesAndDirectories[item.Key] = item.Value;
                else
                    RemoteHostFilesAndDirectories.Add(item.Key, item.Value);

                if (item.Value.IsDirectory)
                {
                    directories.Add(item.Value);
                }
                else
                {
                    files.Add(item.Value);
                }
            }

            if (RemoteHostDirectories.ContainsKey(remoteDirectory))
                RemoteHostDirectories[remoteDirectory] = directories;
            else
                RemoteHostDirectories.Add(remoteDirectory, directories);

            if (RemoteHostFiles.ContainsKey(remoteDirectory))
                RemoteHostFiles[remoteDirectory] = files;
            else
                RemoteHostFiles.Add(remoteDirectory, files);
        }

        /// <summary>
        /// Copy a file to the directory tracked by DirectoryPath
        /// </summary>
        /// <param name="sourceFile"></param>
        /// <param name="targetFilePath"></param>
        public FileOrDirectoryInfo CopyFile(FileInfo sourceFile, string targetFilePath)
        {
            if (!TrackingRemoteHostDirectory)
            {
                var newFile = sourceFile.CopyTo(targetFilePath, true);
                return new FileOrDirectoryInfo(newFile);
            }

            using (var source = new FileStream(sourceFile.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                mSftpClient.UploadFile(source, targetFilePath);
            }

            var copiedFile = mSftpClient.Get(targetFilePath);
            copiedFile.LastWriteTimeUtc = sourceFile.LastWriteTimeUtc;

            mSftpClient.SetAttributes(targetFilePath, copiedFile.Attributes);

            return new FileOrDirectoryInfo(copiedFile);
        }

        /// <summary>
        /// Check whether the directory exists; create it if missing
        /// </summary>
        /// <param name="directoryPath"></param>
        /// <returns>Instance of FileOrDirectoryInfo for the directory</returns>
        public FileOrDirectoryInfo CreateDirectoryIfMissing(string directoryPath)
        {
            if (!TrackingRemoteHostDirectory)
            {
                var dirInfo = new DirectoryInfo(directoryPath);
                if (!dirInfo.Exists)
                {
                    dirInfo.Create();
                    dirInfo.Refresh();
                }

                return new FileOrDirectoryInfo(dirInfo);
            }

            var remoteDirectory = GetDirectoryInfo(directoryPath);
            if (remoteDirectory.Exists)
                return remoteDirectory;

            mSftpClient.CreateDirectory(directoryPath);
            var newDirectory = GetDirectoryInfo(directoryPath, true);
            return newDirectory;
        }

        /// <summary>
        /// Delete a file or directory
        /// </summary>
        /// <param name="fileOrDirectory"></param>
        public void DeleteFileOrDirectory(FileOrDirectoryInfo fileOrDirectory)
        {
            if (!TrackingRemoteHostDirectory)
            {
                if (fileOrDirectory.IsDirectory)
                {
                    var item = new DirectoryInfo(fileOrDirectory.FullName);
                    // Delete the directory, but do not recurse and do not delete it if not empty
                    item.Delete(false);
                }
                else
                {
                    var item = new FileInfo(fileOrDirectory.FullName);
                    item.Delete();
                }
                return;
            }

            if (fileOrDirectory.IsDirectory)
            {
                var item = GetDirectoryInfo(fileOrDirectory.FullName);
                if (item.Exists)
                    mUpdateUtility.DeleteDirectory(mSftpClient, item.FullName);
            }
            else
            {
                var item = GetFileInfo(fileOrDirectory.FullName);
                if (item.Exists)
                    mUpdateUtility.DeleteFile(mSftpClient, item.FullName);
            }
        }

        /// <summary>
        /// Get information about a directory
        /// </summary>
        /// <remarks>Use Windows slashes for local directories and Linux slashes for remote directories</remarks>
        /// <param name="directoryPath">Directory path to find</param>
        /// <param name="refreshRemoteData">When true, refresh the files and subdirectories of directoryPath (only valid if TrackingRemoteHostDirectory is true)</param>
        /// <returns>Directory info</returns>
        public FileOrDirectoryInfo GetDirectoryInfo(string directoryPath, bool refreshRemoteData = false)
        {
            if (!TrackingRemoteHostDirectory)
            {
                var dirInfo = new DirectoryInfo(directoryPath);
                return new FileOrDirectoryInfo(dirInfo);
            }

            var parentPath = GetRemoteDirectoryParent(directoryPath);
            AddUpdateRemoteHostFilesAndDirectories(parentPath, refreshRemoteData);

            if (RemoteHostFilesAndDirectories.TryGetValue(directoryPath, out var remoteDirectoryInfo))
            {
                if (remoteDirectoryInfo.IsDirectory)
                {
                    return new FileOrDirectoryInfo(remoteDirectoryInfo);
                }
            }

            var missingDirectory = new FileOrDirectoryInfo(
                directoryPath,
                exists: false,
                lastWrite: DateTime.MinValue,
                lastWriteUtc: DateTime.MinValue,
                linuxDirectory: true);

            return missingDirectory;
        }

        /// <summary>
        /// Get a list of all of the subdirectories below the given directory
        /// </summary>
        /// <param name="directoryPath"></param>
        /// <param name="refreshRemoteData">When true, refresh the files and subdirectories of directoryPath (only valid if TrackingRemoteHostDirectory is true)</param>
        /// <returns>List of directories</returns>
        public List<FileOrDirectoryInfo> GetDirectories(FileOrDirectoryInfo directoryPath, bool refreshRemoteData = false)
        {
            var subDirectories = new List<FileOrDirectoryInfo>();

            if (!TrackingRemoteHostDirectory)
            {
                var dirInfo = new DirectoryInfo(directoryPath.FullName);
                subDirectories.AddRange(dirInfo.GetDirectories().Select(item => new FileOrDirectoryInfo(item)));

                return subDirectories;
            }

            var remoteDirectory = GetDirectoryInfo(directoryPath.FullName, refreshRemoteData);
            if (!remoteDirectory.Exists || !RemoteHostDirectories.TryGetValue(directoryPath.FullName, out var remoteDirectoryInfo))
                return subDirectories;

            foreach (var item in remoteDirectoryInfo)
            {
                subDirectories.Add(new FileOrDirectoryInfo(item));
            }

            return subDirectories;
        }

        /// <summary>
        /// Get information about a file
        /// </summary>
        /// <remarks>Use Windows slashes for local files and Linux slashes for remote files</remarks>
        /// <param name="filePath">File path to find</param>
        /// <param name="refreshRemoteData">When true, refresh the files and subdirectories of the file's parent directory (only valid if TrackingRemoteHostDirectory is true)</param>
        /// <returns>File info</returns>
        public FileOrDirectoryInfo GetFileInfo(string filePath, bool refreshRemoteData = false)
        {
            if (!TrackingRemoteHostDirectory)
            {
                var localFile = new FileInfo(filePath);
                return new FileOrDirectoryInfo(localFile);
            }

            var remoteDirectory = GetRemoteDirectoryParent(filePath);

            AddUpdateRemoteHostFilesAndDirectories(remoteDirectory, refreshRemoteData);

            if (RemoteHostFilesAndDirectories.TryGetValue(filePath, out var remoteFileInfo))
            {
                if (!remoteFileInfo.IsDirectory)
                {
                    return new FileOrDirectoryInfo(remoteFileInfo);
                }
            }

            var missingFile = new FileOrDirectoryInfo(filePath, linuxFile: true);

            return missingFile;
        }

        /// <summary>
        /// Get a list of all of the files in the given directory
        /// </summary>
        /// <param name="directoryPath"></param>
        /// <param name="refreshRemoteData">When true, refresh the files and subdirectories of directoryPath (only valid if TrackingRemoteHostDirectory is true)</param>
        /// <returns>List of files</returns>
        public List<FileOrDirectoryInfo> GetFiles(FileOrDirectoryInfo directoryPath, bool refreshRemoteData = false)
        {
            var files = new List<FileOrDirectoryInfo>();

            if (!TrackingRemoteHostDirectory)
            {
                var dirInfo = new DirectoryInfo(directoryPath.FullName);
                files.AddRange(dirInfo.GetFiles().Select(item => new FileOrDirectoryInfo(item)));

                return files;
            }

            var remoteDirectory = GetDirectoryInfo(directoryPath.FullName, refreshRemoteData);
            if (!remoteDirectory.Exists || !RemoteHostFiles.TryGetValue(directoryPath.FullName, out var remoteDirectoryInfo))
                return files;

            foreach (var item in remoteDirectoryInfo)
            {
                files.Add(new FileOrDirectoryInfo(item));
            }

            return files;
        }

        /// <summary>
        /// Determine the parent directory of a given file or directory
        /// </summary>
        /// <remarks>Assumes the path separator character is a forward slash</remarks>
        /// <param name="fileOrDirectoryPath"></param>
        /// <returns>Parent directory path</returns>
        public static string GetRemoteDirectoryParent(string fileOrDirectoryPath)
        {
            if (string.IsNullOrWhiteSpace(fileOrDirectoryPath))
                return "/";

            if (fileOrDirectoryPath.EndsWith("/") && fileOrDirectoryPath.Length > 1)
                fileOrDirectoryPath = fileOrDirectoryPath.TrimEnd('/');

            var lastSlash = fileOrDirectoryPath.LastIndexOf('/');
            var remoteDirectory = lastSlash < 1 ? "/" : fileOrDirectoryPath.Substring(0, lastSlash);
            return remoteDirectory;
        }

        /// <summary>
        /// Public destructor
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected destructor
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    mSftpClient?.Disconnect();
                    mSftpClient?.Dispose();
                }
                catch (Exception)
                {
                    // Ignore errors
                }
            }
        }

        /// <summary>
        /// Target directory path
        /// </summary>
        public override string ToString()
        {
            return DirectoryPath;
        }
    }
}
