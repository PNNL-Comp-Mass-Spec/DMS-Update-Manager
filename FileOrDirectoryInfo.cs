using System;
using System.IO;
using Renci.SshNet.Sftp;

namespace DMSUpdateManager
{
    /// <summary>
    /// Tracks information on a local or remote file or directory
    /// </summary>
    internal class FileOrDirectoryInfo
    {
        #region "Properties"

        /// <summary>
        /// Full path of the parent directory
        /// </summary>
        public string DirectoryName { get; }

        /// <summary>
        /// True if the file or directory exists
        /// </summary>
        public bool Exists { get; set; }

        /// <summary>
        /// Full path to the file or directory
        /// </summary>
        public string FullName { get; }

        /// <summary>
        /// Full path to the file or directory, using Linux style paths
        /// </summary>
        public string FullPathLinux
        {
            get
            {
                if (string.IsNullOrWhiteSpace(FullName))
                    return string.Empty;

                return LinuxPath ? FullName : FullName.Replace('\\', '/');
            }
        }

        /// <summary>
        /// Full path to the file or directory, using Windows style paths
        /// </summary>
        public string FullPathWindows
        {
            get
            {
                if (string.IsNullOrWhiteSpace(FullName))
                    return string.Empty;

                return LinuxPath ? FullName.Replace('/', '\\') : FullName;
            }
        }

        /// <summary>
        /// True if tracking a directory; false if a file
        /// </summary>
        public bool IsDirectory { get; }


        /// <summary>
        /// Last write time of the file
        /// DateTime.Now if a directory
        /// </summary>
        public DateTime LastWriteTime { get; set; }

        /// <summary>
        /// Last write time of the directory
        /// DateTime.Now if a directory
        /// </summary>
        public DateTime LastWriteTimeUtc { get; set; }

        /// <summary>
        /// File size, in bytes
        /// 0 if a directory
        /// </summary>
        public long Length { get; set; }

        /// <summary>
        /// True if a Linux file or directory
        /// </summary>
        private bool LinuxPath { get; }

        /// <summary>
        /// Name of the file or directory
        /// </summary>
        public string Name { get; }

        #endregion

        /// <summary>
        /// Constructor for a DirectoryInfo instance (assumed to be a Windows directory)
        /// </summary>
        // ReSharper disable once SuggestBaseTypeForParameter
        public FileOrDirectoryInfo(DirectoryInfo dirInfo, bool linuxDirectory = false) :
            this(true, dirInfo.FullName, dirInfo.Exists, 0, dirInfo.LastWriteTime, dirInfo.LastWriteTimeUtc, linuxDirectory)
        {
        }

        /// <summary>
        /// Constructor for a directory
        /// </summary>
        /// <param name="directoryPath"></param>
        /// <param name="exists"></param>
        /// <param name="lastWrite"></param>
        /// <param name="lastWriteUtc"></param>
        /// <param name="linuxDirectory"></param>
        public FileOrDirectoryInfo(string directoryPath, bool exists, DateTime lastWrite, DateTime lastWriteUtc, bool linuxDirectory) :
            this(true, directoryPath, exists, 0, lastWrite, lastWriteUtc, linuxDirectory)
        {
        }

        /// <summary>
        /// Constructor for an FileInfo instance (assumed to be a Windows file)
        /// </summary>
        public FileOrDirectoryInfo(FileInfo localFile) :
            this(false, localFile.FullName, localFile.Exists, 0, localFile.LastWriteTime, localFile.LastWriteTimeUtc, false)
        {
            if (!localFile.Exists) return;
            Length = localFile.Length;
        }

        /// <summary>
        /// Constructor for an SftpFile (which can be a file or a directory)
        /// </summary>
        public FileOrDirectoryInfo(SftpFile remoteFileInfo) :
            this(remoteFileInfo.IsDirectory, remoteFileInfo.FullName, true, remoteFileInfo.Length,
                 remoteFileInfo.LastWriteTime, remoteFileInfo.LastWriteTimeUtc, true)
        {
        }

        /// <summary>
        /// Constructor for a missing file
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="linuxFile"></param>
        public FileOrDirectoryInfo(string filePath, bool linuxFile) :
            this(false, filePath, exists: false, length: 0, lastWrite: DateTime.MinValue, lastWriteUtc: DateTime.MinValue, linuxPath: linuxFile)
        {
        }

        /// <summary>
        /// Constructor for a file
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="exists"></param>
        /// <param name="length"></param>
        /// <param name="lastWrite"></param>
        /// <param name="lastWriteUtc"></param>
        /// <param name="linuxFile"></param>
        public FileOrDirectoryInfo(
            string filePath, bool exists, long length,
            DateTime lastWrite, DateTime lastWriteUtc, bool linuxFile) :
            this(false, filePath, exists, length, lastWrite, lastWriteUtc, linuxFile)
        {
        }

        /// <summary>
        /// Private constructor
        /// </summary>
        /// <param name="isDirectory"></param>
        /// <param name="fileOrDirectoryPath"></param>
        /// <param name="exists"></param>
        /// <param name="length"></param>
        /// <param name="lastWrite"></param>
        /// <param name="lastWriteUtc"></param>
        /// <param name="linuxPath"></param>
        private FileOrDirectoryInfo(
            bool isDirectory,
            string fileOrDirectoryPath,
            bool exists,
            long length,
            DateTime lastWrite,
            DateTime lastWriteUtc,
            bool linuxPath)
        {
            IsDirectory = isDirectory;

            FullName = fileOrDirectoryPath;
            LinuxPath = linuxPath || FullName.Contains("/");

            if (linuxPath)
                Name = Path.GetFileName(FullPathWindows);
            else
                Name = Path.GetFileName(fileOrDirectoryPath);

            Exists = exists;

            Length = length;

            LastWriteTime = lastWrite;

            LastWriteTimeUtc = lastWriteUtc;

            if (linuxPath)
            {
                DirectoryName = DirectoryContainer.GetRemoteDirectoryParent(fileOrDirectoryPath);
            }
            else
            {
                if (isDirectory)
                {
                    var directoryInfo = new DirectoryInfo(fileOrDirectoryPath);
                    DirectoryName = directoryInfo.Parent == null ? string.Empty : directoryInfo.Parent?.FullName;
                }
                else
                {
                    var fileInfo = new FileInfo(fileOrDirectoryPath);
                    DirectoryName = fileInfo.DirectoryName;
                }
            }
        }

        /// <summary>
        /// File or directory path
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return FullName;
        }
    }
}
