
namespace DMSUpdateManager
{
    /// <summary>
    /// Connection info for the target remote host
    /// </summary>
    public class RemoteHostConnectionInfo
    {

        /// <summary>
        /// Directory on the remote host where files will be pushed
        /// </summary>
        /// <remarks>
        /// For example, /opt/DMS_Programs
        /// </remarks>
        public string DestinationPath { get; set; }

        /// <summary>
        /// Remote host name
        /// </summary>
        public string HostName { get; set; }

        /// <summary>
        /// Path to the file with the RSA private key for connecting to RemoteHostName as user RemoteHostUser
        /// </summary>
        /// <remarks>
        /// For example, C:\DMS_RemoteInfo\user.key
        /// </remarks>
        public string PrivateKeyFile { get; set; }

        /// <summary>
        /// Path to the file with the passphrase for the RSA private key
        /// </summary>
        /// <remarks>
        /// For example, C:\DMS_RemoteInfo\user.pass
        /// </remarks>
        public string PassphraseFile { get; set; }

        /// <summary>
        /// Remote host username
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="hostName">Host nane</param>
        /// <param name="userName">Username</param>
        /// <param name="privateKeyFilePath">Private key file path</param>
        /// <param name="passphraseFilePath">Passpharse file path</param>
        /// <param name="destinationPath">Destination directory path</param>
        public RemoteHostConnectionInfo(
            string hostName, string userName,
            string privateKeyFilePath, string passphraseFilePath,
            string destinationPath)
        {

            HostName = hostName;
            Username = userName;
            PrivateKeyFile = privateKeyFilePath;
            PassphraseFile = passphraseFilePath;
            DestinationPath = destinationPath;
        }

        /// <summary>
        /// Validate that all of the remote host settings are defined
        /// </summary>
        /// <param name="errorMessage">Error message if a parameter is null or empty</param>
        /// <returns>True if all the settings are defined</returns>
        public bool Validate(out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(HostName))
            {
                errorMessage = "Remote host name not defined";
                return false;
            }

            if (string.IsNullOrWhiteSpace(Username))
            {
                errorMessage = "Remote host username not defined";
                return false;
            }

            if (string.IsNullOrWhiteSpace(PrivateKeyFile))
            {
                errorMessage = "Private key file not defined";
                return false;
            }

            if (string.IsNullOrWhiteSpace(PassphraseFile))
            {
                errorMessage = "Passphrase file not defined";
                return false;
            }

            if (string.IsNullOrWhiteSpace(DestinationPath))
            {
                errorMessage = "Destination directory path not defined";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }
    }
}
