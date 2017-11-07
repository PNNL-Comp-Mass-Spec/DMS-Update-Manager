using System;
using System.IO;
using System.Reflection;

namespace DMSUpdateManager
{
    /// <summary>
    /// This class can be used as a base class for classes that process a folder or folders
    /// Note that this class contains simple error codes that
    /// can be set from any derived classes.  The derived classes can also set their own local error codes
    /// </summary>
    /// <remarks>
    /// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
    /// Copyright 2005, Battelle Memorial Institute.  All Rights Reserved.
    /// Started April 26, 2005
    /// </remarks>
    public abstract class clsProcessFoldersBaseClass : clsProcessFilesOrFoldersBase
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <remarks></remarks>
        public clsProcessFoldersBaseClass()
        {
            mFileDate = "November 20, 2013";
            mErrorCode = eProcessFoldersErrorCodes.NoError;
        }

        #region "Constants and Enums"

        public enum eProcessFoldersErrorCodes
        {
            NoError = 0,
            InvalidInputFolderPath = 1,
            InvalidOutputFolderPath = 2,
            ParameterFileNotFound = 4,
            InvalidParameterFile = 8,
            FilePathError = 16,
            LocalizedError = 32,
            UnspecifiedError = -1
        }

        // Copy the following to any derived classes
        //enum eDerivedClassErrorCodes
        //{
        //    NoError = 0,
        //    UnspecifiedError = -1
        //}

        #endregion

        #region "Classwide Variables"

        // Copy the following to any derived classes
        //
        //private eDerivedClassErrorCodes mLocalErrorCode;

        //public eDerivedClassErrorCodes LocalErrorCode
        //{
        //    get
        //    {
        //        return mLocalErrorCode;
        //    }
        //}

        private eProcessFoldersErrorCodes mErrorCode;

        #endregion

        #region "Interface Functions"

        public eProcessFoldersErrorCodes ErrorCode
        {
            get { return mErrorCode; }
        }

        #endregion

        protected override void CleanupPaths(ref string strInputFileOrFolderPath, ref string strOutputFolderPath)
        {
            CleanupFolderPaths(ref strInputFileOrFolderPath, ref strOutputFolderPath);
        }

        protected bool CleanupFolderPaths(ref string strInputFolderPath, ref string strOutputFolderPath)
        {
            // Validates that strInputFolderPath and strOutputFolderPath contain valid folder paths
            // Will ignore strOutputFolderPath if it is Nothing or empty; will create strOutputFolderPath if it does not exist
            //
            // Returns True if success, False if failure

            bool blnSuccess;

            try
            {
                // Make sure strInputFolderPath points to a valid folder
                var diFolder = new DirectoryInfo(strInputFolderPath);

                if (!diFolder.Exists)
                {
                    if (ShowMessages)
                    {
                        ShowErrorMessage("Input folder not found: " + strInputFolderPath);
                    }
                    else
                    {
                        LogMessage("Input folder not found: " + strInputFolderPath, eMessageTypeConstants.ErrorMsg);
                    }
                    mErrorCode = eProcessFoldersErrorCodes.InvalidInputFolderPath;
                    blnSuccess = false;
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(strOutputFolderPath))
                    {
                        // Define strOutputFolderPath based on strInputFolderPath
                        strOutputFolderPath = diFolder.FullName;
                    }

                    // Make sure strOutputFolderPath points to a folder
                    diFolder = new DirectoryInfo(strOutputFolderPath);

                    if (!diFolder.Exists)
                    {
                        // strOutputFolderPath points to a non-existent folder; attempt to create it
                        diFolder.Create();
                    }

                    mOutputFolderPath = string.Copy(diFolder.FullName);

                    blnSuccess = true;
                }
            }
            catch (Exception ex)
            {
                HandleException("Error cleaning up the folder paths", ex);
                return false;
            }

            return blnSuccess;
        }

        protected string GetBaseClassErrorMessage()
        {
            // Returns String.Empty if no error

            string strErrorMessage;

            switch (ErrorCode)
            {
                case eProcessFoldersErrorCodes.NoError:
                    strErrorMessage = string.Empty;
                    break;
                case eProcessFoldersErrorCodes.InvalidInputFolderPath:
                    strErrorMessage = "Invalid input folder path";
                    break;
                case eProcessFoldersErrorCodes.InvalidOutputFolderPath:
                    strErrorMessage = "Invalid output folder path";
                    break;
                case eProcessFoldersErrorCodes.ParameterFileNotFound:
                    strErrorMessage = "Parameter file not found";
                    break;
                case eProcessFoldersErrorCodes.InvalidParameterFile:
                    strErrorMessage = "Invalid parameter file";
                    break;
                case eProcessFoldersErrorCodes.FilePathError:
                    strErrorMessage = "General file path error";
                    break;
                case eProcessFoldersErrorCodes.LocalizedError:
                    strErrorMessage = "Localized error";
                    break;
                case eProcessFoldersErrorCodes.UnspecifiedError:
                    strErrorMessage = "Unspecified error";
                    break;
                default:
                    // This shouldn't happen
                    strErrorMessage = "Unknown error state";
                    break;
            }

            return strErrorMessage;
        }

        public bool ProcessFoldersWildcard(string strInputFolderPath)
        {
            return ProcessFoldersWildcard(strInputFolderPath, string.Empty, string.Empty);
        }

        public bool ProcessFoldersWildcard(string strInputFolderPath, string strOutputFolderAlternatePath)
        {
            return ProcessFoldersWildcard(strInputFolderPath, strOutputFolderAlternatePath, string.Empty);
        }

        public bool ProcessFoldersWildcard(string strInputFolderPath, string strOutputFolderAlternatePath, string strParameterFilePath)
        {
            return ProcessFoldersWildcard(strInputFolderPath, strOutputFolderAlternatePath, strParameterFilePath, true);
        }

        public bool ProcessFoldersWildcard(string strInputFolderPath, string strOutputFolderAlternatePath, string strParameterFilePath,
            bool blnResetErrorCode)
        {
            // Returns True if success, False if failure

            AbortProcessing = false;
            var blnSuccess = true;
            try
            {
                // Possibly reset the error code
                if (blnResetErrorCode)
                    mErrorCode = eProcessFoldersErrorCodes.NoError;

                if (!string.IsNullOrWhiteSpace(strOutputFolderAlternatePath))
                {
                    // Update the cached output folder path
                    mOutputFolderPath = string.Copy(strOutputFolderAlternatePath);
                }

                // See if strInputFolderPath contains a wildcard (* or ?)
                if ((strInputFolderPath != null) && (strInputFolderPath.Contains("*") | strInputFolderPath.Contains("?")))
                {
                    // Copy the path into strCleanPath and replace any * or ? characters with _
                    var strCleanPath = strInputFolderPath.Replace("*", "_");
                    strCleanPath = strCleanPath.Replace("?", "_");

                    var diInputFolderInfo = new DirectoryInfo(strCleanPath);
                    string strInputFolderToUse;
                    if (diInputFolderInfo.Parent.Exists)
                    {
                        strInputFolderToUse = diInputFolderInfo.Parent.FullName;
                    }
                    else
                    {
                        // Use the current working directory
                        strInputFolderToUse = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    }

                    // Remove any directory information from strInputFolderPath
                    var strFolderNameMatchPattern = Path.GetFileName(strInputFolderPath);

                    // Process any matching folder in this folder
                    try
                    {
                        if (string.IsNullOrEmpty(strInputFolderToUse))
                            throw new ArgumentNullException(strInputFolderPath);

                        diInputFolderInfo = new DirectoryInfo(strInputFolderToUse);
                    }
                    catch (Exception ex)
                    {
                        HandleException("Error in ProcessFoldersWildcard", ex);
                        mErrorCode = eProcessFoldersErrorCodes.InvalidInputFolderPath;
                        return false;
                    }

                    var intMatchCount = 0;

                    foreach (var diFolder in diInputFolderInfo.GetDirectories(strFolderNameMatchPattern))
                    {
                        blnSuccess = ProcessFolder(diFolder.FullName, strOutputFolderAlternatePath, strParameterFilePath, true);

                        if (!blnSuccess | AbortProcessing)
                            break;
                        intMatchCount += 1;

                        if (intMatchCount % 1 == 0)
                            Console.Write(".");
                    }

                    if (intMatchCount == 0)
                    {
                        if (mErrorCode == eProcessFoldersErrorCodes.NoError)
                        {
                            if (ShowMessages)
                            {
                                ShowErrorMessage("No match was found for the input folder path:" + strInputFolderPath);
                            }
                            else
                            {
                                LogMessage("No match was found for the input folder path:" + strInputFolderPath, eMessageTypeConstants.ErrorMsg);
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine();
                    }
                }
                else
                {
                    blnSuccess = ProcessFolder(strInputFolderPath, strOutputFolderAlternatePath, strParameterFilePath, blnResetErrorCode);
                }
            }
            catch (Exception ex)
            {
                HandleException("Error in ProcessFoldersWildcard", ex);
                return false;
            }

            return blnSuccess;
        }

        public bool ProcessFolder(string strInputFolderPath)
        {
            return ProcessFolder(strInputFolderPath, string.Empty, string.Empty, true);
        }

        public bool ProcessFolder(string strInputFolderPath, string strOutputFolderAlternatePath, string strParameterFilePath)
        {
            return ProcessFolder(strInputFolderPath, strOutputFolderAlternatePath, strParameterFilePath, true);
        }

        public abstract bool ProcessFolder(string strInputFolderPath, string strOutputFolderAlternatePath, string strParameterFilePath,
            bool blnResetErrorCode);

        public bool ProcessAndRecurseFolders(string strInputFolderPath)
        {
            return ProcessAndRecurseFolders(strInputFolderPath, string.Empty);
        }

        public bool ProcessAndRecurseFolders(string strInputFolderPath, int intRecurseFoldersMaxLevels)
        {
            return ProcessAndRecurseFolders(strInputFolderPath, string.Empty, string.Empty, intRecurseFoldersMaxLevels);
        }

        public bool ProcessAndRecurseFolders(string strInputFolderPath, string strOutputFolderAlternatePath)
        {
            return ProcessAndRecurseFolders(strInputFolderPath, strOutputFolderAlternatePath, string.Empty);
        }

        public bool ProcessAndRecurseFolders(string strInputFolderPath, string strOutputFolderAlternatePath, string strParameterFilePath)
        {
            return ProcessAndRecurseFolders(strInputFolderPath, strOutputFolderAlternatePath, strParameterFilePath, 0);
        }

        public bool ProcessAndRecurseFolders(string strInputFolderPath, string strOutputFolderAlternatePath, string strParameterFilePath,
            int intRecurseFoldersMaxLevels)
        {
            // Calls ProcessFolders for all matching folders in strInputFolderPath
            // If intRecurseFoldersMaxLevels is <=0 then we recurse infinitely

            bool blnSuccess;

            // Examine strInputFolderPath to see if it contains a * or ?
            try
            {
                DirectoryInfo diFolderInfo;
                string strInputFolderToUse;
                string strFolderNameMatchPattern;
                if ((strInputFolderPath != null) && (strInputFolderPath.Contains("*") | strInputFolderPath.Contains("?")))
                {
                    // Copy the path into strCleanPath and replace any * or ? characters with _
                    var strCleanPath = strInputFolderPath.Replace("*", "_");
                    strCleanPath = strCleanPath.Replace("?", "_");

                    diFolderInfo = new DirectoryInfo(strCleanPath);
                    if (diFolderInfo.Parent != null && diFolderInfo.Parent.Exists)
                    {
                        strInputFolderToUse = diFolderInfo.Parent.FullName;
                    }
                    else
                    {
                        // Use the current working directory
                        strInputFolderToUse = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    }

                    // Remove any directory information from strInputFolderPath
                    strFolderNameMatchPattern = Path.GetFileName(strInputFolderPath);
                }
                else
                {
                    if (string.IsNullOrEmpty(strInputFolderPath))
                        throw new ArgumentNullException(strInputFolderPath);

                    diFolderInfo = new DirectoryInfo(strInputFolderPath);
                    if (diFolderInfo.Exists)
                    {
                        strInputFolderToUse = diFolderInfo.FullName;
                    }
                    else
                    {
                        // Use the current working directory
                        strInputFolderToUse = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    }
                    strFolderNameMatchPattern = "*";
                }

                if (!string.IsNullOrWhiteSpace(strInputFolderToUse))
                {
                    // Validate the output folder path
                    if (!string.IsNullOrWhiteSpace(strOutputFolderAlternatePath))
                    {
                        try
                        {
                            diFolderInfo = new DirectoryInfo(strOutputFolderAlternatePath);
                            if (!diFolderInfo.Exists)
                                diFolderInfo.Create();
                        }
                        catch (Exception ex)
                        {
                            HandleException("Error in ProcessAndRecurseFolders", ex);
                            mErrorCode = eProcessFoldersErrorCodes.InvalidOutputFolderPath;
                            return false;
                        }
                    }

                    // Initialize some parameters
                    AbortProcessing = false;
                    var intFolderProcessCount = 0;
                    var intFolderProcessFailCount = 0;

                    // Call RecurseFoldersWork
                    blnSuccess = RecurseFoldersWork(strInputFolderToUse, strFolderNameMatchPattern, strParameterFilePath,
                        strOutputFolderAlternatePath, ref intFolderProcessCount, ref intFolderProcessFailCount, 1, intRecurseFoldersMaxLevels);
                }
                else
                {
                    mErrorCode = eProcessFoldersErrorCodes.InvalidInputFolderPath;
                    return false;
                }
            }
            catch (Exception ex)
            {
                HandleException("Error in ProcessAndRecurseFolders", ex);
                return false;
            }

            return blnSuccess;
        }

        private bool RecurseFoldersWork(string strInputFolderPath, string strFolderNameMatchPattern, string strParameterFilePath,
            string strOutputFolderAlternatePath, ref int intFolderProcessCount, ref int intFolderProcessFailCount, int intRecursionLevel,
            int intRecurseFoldersMaxLevels)
        {
            // If intRecurseFoldersMaxLevels is <=0 then we recurse infinitely

            DirectoryInfo diInputFolderInfo;

            string strOutputFolderPathToUse;
            bool blnSuccess;

            try
            {
                diInputFolderInfo = new DirectoryInfo(strInputFolderPath);
            }
            catch (Exception ex)
            {
                // Input folder path error
                HandleException("Error in RecurseFoldersWork", ex);
                mErrorCode = eProcessFoldersErrorCodes.InvalidInputFolderPath;
                return false;
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(strOutputFolderAlternatePath))
                {
                    strOutputFolderAlternatePath = Path.Combine(strOutputFolderAlternatePath, diInputFolderInfo.Name);
                    strOutputFolderPathToUse = string.Copy(strOutputFolderAlternatePath);
                }
                else
                {
                    strOutputFolderPathToUse = string.Empty;
                }
            }
            catch (Exception ex)
            {
                // Output file path error
                HandleException("Error in RecurseFoldersWork", ex);
                mErrorCode = eProcessFoldersErrorCodes.InvalidOutputFolderPath;
                return false;
            }

            try
            {
                ShowMessage("Examining " + strInputFolderPath);

                if (intRecursionLevel == 1 & strFolderNameMatchPattern == "*")
                {
                    // Need to process the current folder
                    blnSuccess = ProcessFolder(diInputFolderInfo.FullName, strOutputFolderPathToUse, strParameterFilePath, true);
                    if (!blnSuccess)
                    {
                        intFolderProcessFailCount += 1;
                    }
                    else
                    {
                        intFolderProcessCount += 1;
                    }
                }

                // Process any matching folder in this folder
                blnSuccess = true;
                foreach (var diFolderMatch in diInputFolderInfo.GetDirectories(strFolderNameMatchPattern))
                {
                    if (AbortProcessing)
                        break;

                    if (strOutputFolderPathToUse.Length > 0)
                    {
                        blnSuccess = ProcessFolder(diFolderMatch.FullName, Path.Combine(strOutputFolderPathToUse, diFolderMatch.Name),
                            strParameterFilePath, true);
                    }
                    else
                    {
                        blnSuccess = ProcessFolder(diFolderMatch.FullName, string.Empty, strParameterFilePath, true);
                    }

                    if (!blnSuccess)
                    {
                        intFolderProcessFailCount += 1;
                        blnSuccess = true;
                    }
                    else
                    {
                        intFolderProcessCount += 1;
                    }
                }
            }
            catch (Exception ex)
            {
                HandleException("Error in RecurseFoldersWork", ex);
                mErrorCode = eProcessFoldersErrorCodes.InvalidInputFolderPath;
                return false;
            }

            if (!AbortProcessing)
            {
                // If intRecurseFoldersMaxLevels is <=0 then we recurse infinitely
                //  otherwise, compare intRecursionLevel to intRecurseFoldersMaxLevels
                if (intRecurseFoldersMaxLevels <= 0 || intRecursionLevel <= intRecurseFoldersMaxLevels)
                {
                    // Call this function for each of the subfolders of ioInputFolderInfo
                    foreach (var diSubFolderInfo in diInputFolderInfo.GetDirectories())
                    {
                        blnSuccess = RecurseFoldersWork(diSubFolderInfo.FullName, strFolderNameMatchPattern, strParameterFilePath,
                            strOutputFolderAlternatePath, ref intFolderProcessCount, ref intFolderProcessFailCount, intRecursionLevel + 1,
                            intRecurseFoldersMaxLevels);
                        if (!blnSuccess)
                            break;
                    }
                }
            }

            return blnSuccess;
        }

        protected void SetBaseClassErrorCode(eProcessFoldersErrorCodes eNewErrorCode)
        {
            mErrorCode = eNewErrorCode;
        }

        // The following functions should be placed in any derived class
        // Cannot define as MustOverride since it contains a customized enumerated type (eDerivedClassErrorCodes) in the function declaration
        //
        //private void SetLocalErrorCode(eDerivedClassErrorCodes eNewErrorCode)
        //{
        //    SetLocalErrorCode(eNewErrorCode, false);
        //}

        //private void SetLocalErrorCode(eDerivedClassErrorCodes eNewErrorCode, bool blnLeaveExistingErrorCodeUnchanged)
        //{
        //    if (blnLeaveExistingErrorCodeUnchanged && mLocalErrorCode != eDerivedClassErrorCodes.NoError)
        //    {
        //        // An error code is already defined; do not change it
        //    }
        //    else
        //    {
        //        mLocalErrorCode = eNewErrorCode;

        //        if (eNewErrorCode == eDerivedClassErrorCodes.NoError)
        //        {
        //            if (base.ErrorCode == clsProcessFoldersBaseClass.eProcessFoldersErrorCodes.LocalizedError)
        //            {
        //                base.SetBaseClassErrorCode(clsProcessFoldersBaseClass.eProcessFoldersErrorCodes.NoError);
        //            }
        //        }
        //        else
        //        {
        //            base.SetBaseClassErrorCode(clsProcessFoldersBaseClass.eProcessFoldersErrorCodes.LocalizedError);
        //        }
        //    }
        //}
    }
}
