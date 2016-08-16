Option Strict On

Imports System.IO
Imports System.Management
Imports System.Reflection
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Threading

''' <summary>
'''  This program copies new and updated files from a source folder (master file folder) 
''' to a target folder
''' </summary>
''' <remarks>
''' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
''' Program started January 16, 2009
''' --
''' E-mail: matthew.monroe@pnnl.gov or matt@alchemistmatt.com
''' Website: http://omics.pnl.gov/ or http://panomics.pnnl.gov/")
''' </remarks>
Public Class clsDMSUpdateManager
    Inherits clsProcessFoldersBaseClass

    ''' <summary>
    ''' Constructor
    ''' </summary>
    Public Sub New()
        mFileDate = "August 4, 2016"

        mFilesToIgnore = New SortedSet(Of String)(StringComparer.InvariantCultureIgnoreCase)
        mProcessesDict = New Dictionary(Of UInt32, clsProcessInfo)

        mExecutingExePath = Assembly.GetExecutingAssembly().Location
        mExecutingExeName = Path.GetFileName(mExecutingExePath)

        InitializeLocalVariables()
    End Sub


#Region "Constants and Enums"

    ' Error codes specialized for this class
    Public Enum eDMSUpdateManagerErrorCodes As Integer
        NoError = 0
        UnspecifiedError = -1
    End Enum

    Private Enum eDateComparisonModeConstants
        RetainNewerTargetIfDifferentSize = 0
        OverwriteNewerTargetIfDifferentSize = 2
        CopyIfSizeOrDateDiffers = 3
    End Enum

    Private Enum eItemInUseConstants
        NotInUse = 0
        itemInUse = 1
        FolderInUse = 2
    End Enum

    Public Const ROLLBACK_SUFFIX As String = ".rollback"
    Public Const DELETE_SUFFIX As String = ".delete"
    Public Const CHECK_JAVA_SUFFIX As String = ".checkjava"

    Public Const PUSH_DIR_FLAG As String = "_PushDir_.txt"
    Public Const PUSH_AM_SUBDIR_FLAG As String = "_AMSubDir_.txt"

    Public Const DELETE_SUBDIR_FLAG As String = "_DeleteSubDir_.txt"
    Public Const DELETE_AM_SUBDIR_FLAG As String = "_DeleteAMSubDir_.txt"

#End Region

#Region "Structures"

#End Region

#Region "Classwide Variables"

    ' When true, then messages will be displayed and logged showing the files that would be copied
    Private mPreviewMode As Boolean

    Private mProcessesShown As Boolean = False

    ' If False, then will not overwrite files in the target folder that are newer than files in the source folder
    Private mOverwriteNewerFiles As Boolean

    ' When mCopySubdirectoriesToParentFolder=True, then will copy any subdirectories of the source folder into a subdirectory off the parent folder of the target folder
    ' For example:
    '   The .Exe resides at folder C:\DMS_Programs\AnalysisToolManager\DMSUpdateManager.exe
    '   mSourceFolderPath = "\\gigasax\DMS_Programs\AnalysisToolManagerDistribution"
    '   mTargetFolderPath = "."
    '   Files are synced from "\\gigasax\DMS_Programs\AnalysisToolManagerDistribution" to "C:\DMS_Programs\AnalysisToolManager\"
    '   Next, folder \\gigasax\DMS_Programs\AnalysisToolManagerDistribution\MASIC\ will get sync'd with ..\MASIC (but only if ..\MASIC exists)
    '     Note that ..\MASIC is actually C:\DMS_Programs\MASIC\ 
    '   When sync'ing the MASIC folders, will recursively sync additional folders that match
    '   If the source folder contains file _PushDir_.txt or _AMSubDir_.txt then the directory will be copied to the target even if it doesn't exist there

    Private mCopySubdirectoriesToParentFolder As Boolean

    ' The following is the path that lists the files that will be copied to the target folder
    Private mSourceFolderPath As String
    Private mTargetFolderPath As String

    ' List of files that will not be copied
    ' The names must be full filenames (no wildcards)
    Private ReadOnly mFilesToIgnore As SortedSet(Of String)

    Private mLocalErrorCode As eDMSUpdateManagerErrorCodes

    Private ReadOnly mExecutingExeName As String
    Private ReadOnly mExecutingExePath As String

    ' Store the results of the WMI query getting running processes with command line data
    ' Keys are Process ID
    ' Values are clsProcessInfo
    Private ReadOnly mProcessesDict As Dictionary(Of UInt32, clsProcessInfo)

    ' Keys are process ID
    ' Values are the full command line for the process
    Private mProcessesMatchingTarget As Dictionary(Of UInt32, String)

    Private mLastFolderProcessesChecked As String
    Private mLastFolderRunningProcessPath As String
    Private mLastFolderRunningProcessId As UInt32

    Private mSourceFolderPathBase As String
    Private mTargetFolderPathBase As String

#End Region

#Region "Properties"

    Public Property CopySubdirectoriesToParentFolder() As Boolean
        Get
            Return mCopySubdirectoriesToParentFolder
        End Get
        Set(value As Boolean)
            mCopySubdirectoriesToParentFolder = value
        End Set
    End Property

    Public ReadOnly Property LocalErrorCode() As eDMSUpdateManagerErrorCodes
        Get
            Return mLocalErrorCode
        End Get
    End Property

    Public Property OverwriteNewerFiles() As Boolean
        Get
            Return mOverwriteNewerFiles
        End Get
        Set(value As Boolean)
            mOverwriteNewerFiles = value
        End Set
    End Property

    Public Property PreviewMode() As Boolean
        Get
            Return mPreviewMode
        End Get
        Set(value As Boolean)
            mPreviewMode = value
        End Set
    End Property

    Public Property SourceFolderPath() As String
        Get
            If mSourceFolderPath Is Nothing Then
                Return String.Empty
            Else
                Return mSourceFolderPath
            End If
        End Get
        Set(value As String)
            If Not value Is Nothing Then
                mSourceFolderPath = value
            End If
        End Set
    End Property

#End Region

    ''' <summary>
    ''' Shorten folderPath if it starts with mTargetFolderPathBase
    ''' </summary>
    ''' <param name="fileOrFolderPath"></param>
    ''' <returns></returns>
    Private Function AbbreviatePath(fileOrFolderPath As String) As String
        Return AbbreviatePath(fileOrFolderPath, mTargetFolderPathBase)
    End Function

    ''' <summary>
    ''' Shorten folderPath if it starts with folderPathBase
    ''' </summary>
    ''' <param name="fileOrFolderPath"></param>
    ''' <returns></returns>
    Private Function AbbreviatePath(fileOrFolderPath As String, folderPathBase As String) As String
        If fileOrFolderPath.StartsWith(folderPathBase) Then
            If fileOrFolderPath.Length > folderPathBase.Length Then
                Return fileOrFolderPath.Substring(folderPathBase.Length + 1)
            Else
                Return "."
            End If

        End If

        Return fileOrFolderPath
    End Function


    ''' <summary>
    ''' Add a file to ignore from processing
    ''' </summary>
    ''' <param name="strFileName">Full filename (no wildcards)</param>
    Public Sub AddFileToIgnore(strFileName As String)

        If Not String.IsNullOrWhiteSpace(strFileName) Then
            If Not mFilesToIgnore.Contains(strFileName) Then
                mFilesToIgnore.Add(strFileName)
            End If
        End If

    End Sub

    ''' <summary>
    ''' Copy the file (or preview the copy)
    ''' </summary>
    ''' <param name="fiSourceFile">Source file</param>
    ''' <param name="fiTargetFile">Target file</param>
    ''' <param name="fileUpdateCount">Total number of files updated (input/output)</param>
    ''' <param name="strCopyReason">Reason for the copy</param>
    Private Sub CopyFile(fiSourceFile As FileInfo, fiTargetFile As FileInfo, ByRef fileUpdateCount As Integer, strCopyReason As String)

        Dim existingFileInfo As String

        If fiTargetFile.Exists Then
            existingFileInfo = "Old: " & GetFileDateAndSize(fiTargetFile)
        Else
            existingFileInfo = String.Empty
        End If

        Dim updatedFileInfo = "New: " & GetFileDateAndSize(fiSourceFile)

        If mPreviewMode Then
            ShowOldAndNewFileInfo("Preview: Update file: ", fiSourceFile, fiTargetFile, existingFileInfo, updatedFileInfo, strCopyReason, True)
        Else
            ShowOldAndNewFileInfo("Update file: ", fiSourceFile, fiTargetFile, existingFileInfo, updatedFileInfo, strCopyReason, True)

            Try
                Dim fiCopiedFile = fiSourceFile.CopyTo(fiTargetFile.FullName, True)

                If fiCopiedFile.Length <> fiSourceFile.Length Then
                    ShowErrorMessage("Copy of " & fiSourceFile.Name & " failed; sizes differ", True)
                ElseIf fiCopiedFile.LastWriteTimeUtc <> fiSourceFile.LastWriteTimeUtc Then
                    ShowErrorMessage("Copy of " & fiSourceFile.Name & " failed; modification times differ", True)
                Else
                    fileUpdateCount += 1
                End If

            Catch ex As Exception
                ShowErrorMessage("Error copying " & fiSourceFile.Name & ": " & ex.Message, True)
            End Try

        End If

    End Sub

    ''' <summary>
    ''' Compare the source file to the target file and update it if they differ
    ''' </summary>
    ''' <param name="fiSourceFile">Source file</param>
    ''' <param name="strTargetFolderPath">Target folder</param>
    ''' <param name="fileUpdateCount">Number of files that have been updated (Input/output)</param>
    ''' <param name="eDateComparisonMode">Date comparison mode</param>
    ''' <param name="blnProcessingSubFolder">True if processing a subfolder</param>
    ''' <param name="itemInUse">Used to track when a file or folder is in use by another process (log a message if the source and target files differ)</param>
    ''' <param name="fileUsageMessage">Message to log when the file (or folder) is in use and the source and targets differ</param>
    ''' <returns>True if the file was updated, otherwise false</returns>
    ''' <remarks></remarks>
    Private Function CopyFileIfNeeded(
       fiSourceFile As FileInfo,
       strTargetFolderPath As String,
       ByRef fileUpdateCount As Integer,
       eDateComparisonMode As eDateComparisonModeConstants,
       blnProcessingSubFolder As Boolean,
       Optional itemInUse As eItemInUseConstants = eItemInUseConstants.NotInUse,
       Optional fileUsageMessage As String = "") As Boolean

        Dim strTargetFilePath = Path.Combine(strTargetFolderPath, fiSourceFile.Name)
        Dim fiTargetFile = New FileInfo(strTargetFilePath)

        Dim strCopyReason = String.Empty
        Dim blnNeedToCopy = False

        If Not fiTargetFile.Exists Then
            ' File not present in the target; copy it now
            strCopyReason = "not found in target folder"
            blnNeedToCopy = True
        Else
            ' File is present, see if the file has a different size
            If eDateComparisonMode = eDateComparisonModeConstants.CopyIfSizeOrDateDiffers Then

                If fiTargetFile.Length <> fiSourceFile.Length Then
                    blnNeedToCopy = True
                    strCopyReason = "sizes are different"
                ElseIf fiSourceFile.LastWriteTimeUtc <> fiTargetFile.LastWriteTimeUtc Then
                    blnNeedToCopy = True
                    strCopyReason = "dates are different"
                End If

            Else

                If fiTargetFile.Length <> fiSourceFile.Length Then
                    blnNeedToCopy = True
                    strCopyReason = "sizes are different"
                ElseIf fiSourceFile.LastWriteTimeUtc > fiTargetFile.LastWriteTimeUtc Then
                    blnNeedToCopy = True
                    strCopyReason = "source file is newer"
                End If

                If blnNeedToCopy AndAlso eDateComparisonMode = eDateComparisonModeConstants.RetainNewerTargetIfDifferentSize Then
                    If fiTargetFile.LastWriteTimeUtc > fiSourceFile.LastWriteTimeUtc Then
                        ' Target file is newer than the source; do not overwrite

                        Dim strWarning = "Warning: Skipping file " & fiSourceFile.Name
                        If blnProcessingSubFolder Then
                            strWarning &= " in " & strTargetFolderPath
                        End If
                        strWarning &= " since a newer version exists in the target; source=" & fiSourceFile.LastWriteTimeUtc.ToLocalTime() & ", target=" & fiTargetFile.LastWriteTimeUtc.ToLocalTime()

                        ShowMessage(strWarning, intDuplicateHoldoffHours:=24)
                        blnNeedToCopy = False
                    End If
                End If

            End If

        End If

        If blnNeedToCopy Then
            If fiTargetFile.Exists Then
                If String.Equals(fiTargetFile.FullName, mExecutingExePath) Then
                    ShowMessage("Skipping " & fiTargetFile.FullName & "; cannot update the currently running copy of the DMSUpdateManager")
                    Return False
                End If

                If itemInUse <> eItemInUseConstants.NotInUse Then
                    If fiTargetFile.Name = mExecutingExeName Then
                        ' Update DMSUpdateManager.exe if it is not in the same folder as the starting folder
                        If Not String.Equals(fiTargetFile.DirectoryName, mOutputFolderPath) Then
                            itemInUse = eItemInUseConstants.NotInUse
                        End If
                    End If
                End If

                If itemInUse <> eItemInUseConstants.NotInUse Then
                    ' Do not update this file; it is in use (or another file in this folder is in use)
                    If String.IsNullOrWhiteSpace(fileUsageMessage) Then
                        If itemInUse = eItemInUseConstants.FolderInUse Then
                            ShowMessage("Skipping " & fiSourceFile.Name & " because folder " &
                                AbbreviatePath(fiTargetFile.DirectoryName) & " is in use (by an unknown process)")
                        Else
                            ShowMessage("Skipping " & fiSourceFile.Name & " in folder " &
                                AbbreviatePath(fiTargetFile.DirectoryName) & " because currently in use (by an unknown process)")
                        End If
                    Else
                        ShowMessage(fileUsageMessage)
                    End If

                    Return False
                End If
            End If

            CopyFile(fiSourceFile, fiTargetFile, fileUpdateCount, strCopyReason)
            Return True
        Else
            Return False
        End If

    End Function

    Private Sub InitializeLocalVariables()
        MyBase.ShowMessages = False
        MyBase.mLogFileUsesDateStamp = False

        mPreviewMode = False
        mOverwriteNewerFiles = False
        mCopySubdirectoriesToParentFolder = False

        mSourceFolderPath = String.Empty
        mTargetFolderPath = String.Empty

        mFilesToIgnore.Clear()
        mFilesToIgnore.Add(PUSH_DIR_FLAG)
        mFilesToIgnore.Add(PUSH_AM_SUBDIR_FLAG)
        mFilesToIgnore.Add(DELETE_SUBDIR_FLAG)
        mFilesToIgnore.Add(DELETE_AM_SUBDIR_FLAG)

        mLocalErrorCode = eDMSUpdateManagerErrorCodes.NoError

        Dim executingExePath As String = Assembly.GetExecutingAssembly().Location
        Dim vsHostName = Path.ChangeExtension(mExecutingExeName, "vshost.exe").ToLower()

        mProcessesDict.Clear()
        Dim results = New ManagementObjectSearcher("SELECT ProcessId, ExecutablePath, CommandLine FROM Win32_Process")

        For Each item In results.Get()
            Dim processId = DirectCast(item("ProcessId"), UInt32)
            Dim processPath = DirectCast(item("ExecutablePath"), String)
            Dim cmd = DirectCast(item("CommandLine"), String)

            ' Only store the processes that have non-empty command lines and are not referring to the current executable
            If String.IsNullOrWhiteSpace(cmd) Then
                Continue For
            End If

            ' If the ExecutablePath is null/empty, then replace it with the CommandLine string
            ' Otherwise, the next check with throw an exception when the ExecutablePath is null (at the .ToLower() part of line)
            ' We could also set it to an empty string, but using the CommandLine for pessimistic purposes
            If String.IsNullOrWhiteSpace(processPath) Then
                processPath = cmd
            End If

            ' Skip this process if it is the active DMSUpdateManager, or DMSUpdateManager.vshost.exe or cmd.exe
            Dim exeLCase = Path.GetFileName(processPath).ToLower()
            If processPath.Contains(executingExePath) OrElse exeLCase = vsHostName OrElse exeLCase = "cmd.exe" Then
                Continue For
            End If

            Dim newProcess = New clsProcessInfo(processId, processPath, cmd)
            If newProcess.FolderHierarchy.Count < 3 Then
                ' Process running in the root folder or one below the root folder; ignore it
                Continue For
            End If
            mProcessesDict.Add(processId, newProcess)

        Next

        mProcessesMatchingTarget = New Dictionary(Of UInt32, String)

        ' Ignore checking for running processes in the first folder that we are updating
        mLastFolderProcessesChecked = Path.GetDirectoryName(executingExePath)
        mLastFolderRunningProcessPath = Path.GetFileName(executingExePath)
        mLastFolderRunningProcessId = 0

    End Sub

    Public Overrides Function GetErrorMessage() As String
        ' Returns an empty string if no error

        Dim strErrorMessage As String

        If MyBase.ErrorCode = eProcessFoldersErrorCodes.LocalizedError Or
           MyBase.ErrorCode = eProcessFoldersErrorCodes.NoError Then
            Select Case mLocalErrorCode
                Case eDMSUpdateManagerErrorCodes.NoError
                    strErrorMessage = String.Empty
                Case eDMSUpdateManagerErrorCodes.UnspecifiedError
                    strErrorMessage = "Unspecified localized error"
                Case Else
                    ' This shouldn't happen
                    strErrorMessage = "Unknown error state"
            End Select
        Else
            strErrorMessage = MyBase.GetBaseClassErrorMessage()
        End If

        Return strErrorMessage
    End Function

    Private Shared Function GetFileDateAndSize(fiFileInfo As FileInfo) As String
        Return fiFileInfo.LastWriteTimeUtc.ToString("yyyy-MM-dd hh:mm:ss tt") & " and " & fiFileInfo.Length & " bytes"
    End Function

    Private Function LoadParameterFileSettings(strParameterFilePath As String) As Boolean

        Const OPTIONS_SECTION = "DMSUpdateManager"

        Dim objSettingsFile = New XmlSettingsFileAccessor()

        Try

            If strParameterFilePath Is Nothing OrElse strParameterFilePath.Length = 0 Then
                ' No parameter file specified; nothing to load
                Return True
            End If

            If Not File.Exists(strParameterFilePath) Then
                ' See if strParameterFilePath points to a file in the same directory as the application
                strParameterFilePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), Path.GetFileName(strParameterFilePath))
                If Not File.Exists(strParameterFilePath) Then
                    MyBase.SetBaseClassErrorCode(eProcessFoldersErrorCodes.ParameterFileNotFound)
                    Return False
                End If
            End If

            If objSettingsFile.LoadSettings(strParameterFilePath) Then
                If Not objSettingsFile.SectionPresent(OPTIONS_SECTION) Then
                    ShowErrorMessage("The node '<section name=""" & OPTIONS_SECTION & """> was not found in the parameter file: " & strParameterFilePath)
                    MyBase.SetBaseClassErrorCode(eProcessFoldersErrorCodes.InvalidParameterFile)
                    Return False
                Else
                    If objSettingsFile.GetParam(OPTIONS_SECTION, "LogMessages", False) Then
                        MyBase.LogMessagesToFile = True
                    End If

                    mOverwriteNewerFiles = objSettingsFile.GetParam(OPTIONS_SECTION, "OverwriteNewerFiles", mOverwriteNewerFiles)
                    mCopySubdirectoriesToParentFolder = objSettingsFile.GetParam(OPTIONS_SECTION, "CopySubdirectoriesToParentFolder", mCopySubdirectoriesToParentFolder)

                    mSourceFolderPath = objSettingsFile.GetParam(OPTIONS_SECTION, "SourceFolderPath", mSourceFolderPath)
                    mTargetFolderPath = objSettingsFile.GetParam(OPTIONS_SECTION, "TargetFolderPath", mTargetFolderPath)

                    Dim strFilesToIgnore = objSettingsFile.GetParam(OPTIONS_SECTION, "FilesToIgnore", String.Empty)
                    Try
                        If strFilesToIgnore.Length > 0 Then
                            Dim strIgnoreList = strFilesToIgnore.Split(","c)

                            For Each strFile As String In strIgnoreList
                                AddFileToIgnore(strFile.Trim)
                            Next
                        End If
                    Catch ex As Exception
                        ' Ignore errors here
                    End Try
                End If
            End If

        Catch ex As Exception
            HandleException("Error in LoadParameterFileSettings", ex)
            Return False
        End Try

        Return True

    End Function

    ''' <summary>
    ''' Update files in folder strInputFolderPath
    ''' </summary>
    ''' <param name="strInputFolderPath">Target folder to update</param>
    ''' <param name="strOutputFolderAlternatePath">Ignored by this function</param>
    ''' <param name="strParameterFilePath">Parameter file defining the source folder path and other options</param>
    ''' <param name="blnResetErrorCode">Ignored by this function</param>
    ''' <returns>True if success, False if failure</returns>
    ''' <remarks>If TargetFolder is defined in the parameter file, strInputFolderPath will be ignored</remarks>
    Public Overloads Overrides Function ProcessFolder(
      strInputFolderPath As String,
      strOutputFolderAlternatePath As String,
      strParameterFilePath As String,
      blnResetErrorCode As Boolean) As Boolean

        Return UpdateFolder(strInputFolderPath, strParameterFilePath)

    End Function

    Private Sub ShowOldAndNewFileInfo(
      messagePrefix As String,
      fiSourceFile As FileInfo,
      fiTargetFile As FileInfo,
      existingFileInfo As String,
      updatedFileInfo As String,
      strCopyReason As String,
      logToFile As Boolean)

        Dim spacePad = New String(" "c, messagePrefix.Length)

        ShowMessage(messagePrefix & fiSourceFile.Name & "; " & strCopyReason, logToFile)
        If fiTargetFile.Exists Then
            ShowMessage(spacePad & existingFileInfo)
        End If
        ShowMessage(spacePad & updatedFileInfo)

    End Sub

    ''' <summary>
    ''' Update files in folder targetFolderPath
    ''' </summary>
    ''' <param name="targetFolderPath">Target folder to update</param>
    ''' <param name="strParameterFilePath">Parameter file defining the source folder path and other options</param>
    ''' <returns>True if success, False if failure</returns>
    ''' <remarks>If TargetFolder is defined in the parameter file, targetFolderPath will be ignored</remarks>
    Public Function UpdateFolder(targetFolderPath As String, strParameterFilePath As String) As Boolean

        SetLocalErrorCode(eDMSUpdateManagerErrorCodes.NoError)

        If Not targetFolderPath Is Nothing AndAlso targetFolderPath.Length > 0 Then
            ' Update mTargetFolderPath using targetFolderPath
            ' Note: If TargetFolder is defined in the parameter file, this value will get overridden
            mTargetFolderPath = String.Copy(targetFolderPath)
        End If

        If Not LoadParameterFileSettings(strParameterFilePath) Then
            ShowErrorMessage("Parameter file load error: " & strParameterFilePath)

            If MyBase.ErrorCode = eProcessFoldersErrorCodes.NoError Then
                MyBase.SetBaseClassErrorCode(eProcessFoldersErrorCodes.InvalidParameterFile)
            End If
            Return False
        End If

        Try
            targetFolderPath = String.Copy(mTargetFolderPath)

            If mSourceFolderPath Is Nothing OrElse mSourceFolderPath.Length = 0 Then
                ShowMessage("Source folder path is not defined.  Either specify it at the command line or include it in the parameter file.")
                MyBase.SetBaseClassErrorCode(eProcessFoldersErrorCodes.InvalidInputFolderPath)
                Return False
            End If

            If String.IsNullOrWhiteSpace(targetFolderPath) Then
                ShowMessage("Target folder path is not defined.  Either specify it at the command line or include it in the parameter file.")
                MyBase.SetBaseClassErrorCode(eProcessFoldersErrorCodes.InvalidInputFolderPath)
                Return False
            End If

            ' Note that CleanupFilePaths() will update mOutputFolderPath, which is used by LogMessage()
            If Not CleanupFolderPaths(targetFolderPath, String.Empty) Then
                MyBase.SetBaseClassErrorCode(eProcessFoldersErrorCodes.FilePathError)
                Return False
            End If

            Dim diSourceFolder = New DirectoryInfo(mSourceFolderPath)
            Dim diTargetFolder = New DirectoryInfo(targetFolderPath)

            mSourceFolderPathBase = diSourceFolder.Parent.FullName
            mTargetFolderPathBase = diTargetFolder.Parent.FullName

            MyBase.mProgressStepDescription = "Updating " & diTargetFolder.Name & ControlChars.NewLine & " using " & diSourceFolder.FullName
            MyBase.ResetProgress()

            ShowMessage("Updating " & diTargetFolder.FullName & ControlChars.NewLine & " using " & diSourceFolder.FullName, False)

            Dim success = UpdateFolderWork(diSourceFolder.FullName, diTargetFolder.FullName, blnPushNewSubfolders:=False, blnProcessingSubFolder:=False)

            If mCopySubdirectoriesToParentFolder Then
                success = UpdateFolderCopyToParent(diTargetFolder, diSourceFolder)
            End If

            Return success

        Catch ex As Exception
            HandleException("Error in UpdateFolder: " & ex.Message, ex)
            Return False
        End Try

    End Function

    Private Function UpdateFolderCopyToParent(diTargetFolder As DirectoryInfo, diSourceFolder As DirectoryInfo) As Boolean

        Dim successOverall = True

        For Each diSourceSubFolder As DirectoryInfo In diSourceFolder.GetDirectories()

            ' The target folder is treated as a subdirectory of the parent folder
            Dim strTargetSubFolderPath = Path.Combine(diTargetFolder.Parent.FullName, diSourceSubFolder.Name)

            ' Initially assume we'll process this folder if it exists at the target
            Dim blnProcessSubfolder = Directory.Exists(strTargetSubFolderPath)

            If blnProcessSubfolder AndAlso diSourceSubFolder.GetFiles(DELETE_SUBDIR_FLAG).Length > 0 Then
                ' Remove this subfolder (but only if it's empty)
                Dim folderDeleted = DeleteSubFolder(strTargetSubFolderPath, "parent subfolder", DELETE_SUBDIR_FLAG)
                If folderDeleted Then blnProcessSubfolder = False
            End If

            If diSourceSubFolder.GetFiles(DELETE_AM_SUBDIR_FLAG).Length > 0 Then
                ' Remove this subfolder (but only if it's empty)
                Dim strAMSubDirPath = Path.Combine(diTargetFolder.FullName, diSourceSubFolder.Name)
                Dim folderDeleted = DeleteSubFolder(strAMSubDirPath, "subfolder", DELETE_AM_SUBDIR_FLAG)
                If folderDeleted Then blnProcessSubfolder = False
            End If

            If diSourceSubFolder.GetFiles(PUSH_AM_SUBDIR_FLAG).Length > 0 Then
                ' Push this folder as a subdirectory of the target folder, not as a subdirectory of the parent folder
                strTargetSubFolderPath = Path.Combine(diTargetFolder.FullName, diSourceSubFolder.Name)
                blnProcessSubfolder = True
            Else
                If diSourceSubFolder.GetFiles(PUSH_DIR_FLAG).Length > 0 Then
                    blnProcessSubfolder = True
                End If
            End If

            If blnProcessSubfolder Then
                Dim success = UpdateFolderWork(diSourceSubFolder.FullName, strTargetSubFolderPath, blnPushNewSubfolders:=True, blnProcessingSubFolder:=True)
                If Not success Then successOverall = False
            End If
        Next

        Return successOverall

    End Function

    Private Function DeleteSubFolder(targetSubFolderPath As String, folderDescription As String, deleteFlag As String) As Boolean

        Dim diTargetSubFolder = New DirectoryInfo(targetSubFolderPath)

        If String.IsNullOrWhiteSpace(folderDescription) Then
            folderDescription = "folder"
        End If

        If diTargetSubFolder.Exists Then
            Dim fileCount = diTargetSubFolder.GetFiles().Length
            Dim folderCount = diTargetSubFolder.GetDirectories().Length

            If fileCount > 0 Then
                ShowMessage("Folder flagged for deletion, but it is not empty (File Count  = " & fileCount & "): " & AbbreviatePath(diTargetSubFolder.FullName))
            ElseIf folderCount > 0 Then
                ShowMessage("Folder flagged for deletion, but it is not empty (Folder Count = " & folderCount & "): " & AbbreviatePath(diTargetSubFolder.FullName))
            Else
                Try
                    If mPreviewMode Then
                        ShowMessage("Preview " & folderDescription & " delete: " & diTargetSubFolder.FullName)
                    Else
                        diTargetSubFolder.Delete(False)
                        ShowMessage("Deleted " & folderDescription & " " & diTargetSubFolder.FullName)
                    End If

                    Return True
                Catch ex As Exception
                    HandleException("Error removing empty " & folderDescription & " flagged with " & deleteFlag & " at " & diTargetSubFolder.FullName, ex)
                End Try
            End If
        End If

        Return False

    End Function

    Private Function UpdateFolderWork(
      strSourceFolderPath As String,
      strTargetFolderPath As String,
      blnPushNewSubfolders As Boolean,
      blnProcessingSubFolder As Boolean) As Boolean

        MyBase.mProgressStepDescription = "Updating " & AbbreviatePath(strTargetFolderPath) & ControlChars.NewLine &
            " using " & AbbreviatePath(strSourceFolderPath, mSourceFolderPathBase)

        ShowMessage(MyBase.mProgressStepDescription, False)

        ' Make sure the target folder exists
        Dim diTargetFolder = New DirectoryInfo(strTargetFolderPath)
        If Not diTargetFolder.Exists Then
            diTargetFolder.Create()
        End If

        ' Obtain a list of files in the source folder
        Dim diSourceFolder = New DirectoryInfo(strSourceFolderPath)

        Dim fileUpdateCount = 0

        Dim fiFilesInSource = diSourceFolder.GetFiles()

        ' Populate a List object the with the names of any .delete files in fiFilesInSource
        Dim lstDeleteFiles = New SortedSet(Of String)(StringComparer.InvariantCultureIgnoreCase)
        Dim filesToDelete = (From fiSourceFile In fiFilesInSource
                             Where fiSourceFile.Name.EndsWith(DELETE_SUFFIX, StringComparison.InvariantCultureIgnoreCase)
                             Select TrimSuffix(fiSourceFile.Name, DELETE_SUFFIX).ToLower())
        For Each item In filesToDelete
            lstDeleteFiles.Add(item)
        Next

        ' Populate a List object the with the names of any .checkjava files in fiFilesInSource
        Dim lstCheckJavaFiles = New SortedSet(Of String)(StringComparer.InvariantCultureIgnoreCase)
        Dim javaFilesToCheck = (From fiSourceFile In fiFilesInSource
                                Where fiSourceFile.Name.EndsWith(CHECK_JAVA_SUFFIX, StringComparison.InvariantCultureIgnoreCase)
                                Select TrimSuffix(fiSourceFile.Name, CHECK_JAVA_SUFFIX).ToLower())
        For Each item In javaFilesToCheck
            lstCheckJavaFiles.Add(item)
        Next

        For Each fiSourceFile As FileInfo In fiFilesInSource

            Dim retryCount = 2
            Dim errorLogged = False

            While retryCount >= 0

                Try
                    Dim strFileNameLCase = fiSourceFile.Name.ToLower()

                    ' Make sure this file is not in mFilesToIgnore
                    ' Note that mFilesToIgnore contains several flag files:
                    '   PUSH_DIR_FLAG, PUSH_AM_SUBDIR_FLAG, 
                    '   DELETE_SUBDIR_FLAG, DELETE_AM_SUBDIR_FLAG
                    Dim blnSkipFile = mFilesToIgnore.Contains(strFileNameLCase)

                    If blnSkipFile Then
                        Continue For
                    End If

                    Dim itemInUse = eItemInUseConstants.NotInUse
                    Dim fileUsageMessage As String = String.Empty

                    ' See if file ends with one of the special suffix flags
                    If fiSourceFile.Name.EndsWith(ROLLBACK_SUFFIX, StringComparison.InvariantCultureIgnoreCase) Then
                        ' This is a Rollback file
                        ' Do not copy this file
                        ' However, do look for a corresponding file that does not have .rollback and copy it if the target file has a different date or size

                        Dim targetFileName = TrimSuffix(strFileNameLCase, ROLLBACK_SUFFIX)
                        If lstCheckJavaFiles.Contains(targetFileName) Then
                            If JarFileInUseByJava(fiSourceFile, fileUsageMessage) Then
                                itemInUse = eItemInUseConstants.itemInUse
                            End If
                        Else
                            If TargetFolderInUseByProcess(diTargetFolder.FullName, targetFileName, fileUsageMessage) Then
                                ' The folder is in use
                                ' Allow new files to be copied, but do not overwrite existing files
                                itemInUse = eItemInUseConstants.FolderInUse
                            End If
                        End If

                        ProcessRollbackFile(fiSourceFile, diTargetFolder.FullName, fileUpdateCount, blnProcessingSubFolder, itemInUse, fileUsageMessage)
                        Continue For

                    ElseIf fiSourceFile.Name.EndsWith(DELETE_SUFFIX, StringComparison.InvariantCultureIgnoreCase) Then
                        ' This is a Delete file
                        ' Do not copy this file
                        ' However, do look for a corresponding file that does not have .delete and delete that file in the target folder

                        ProcessDeleteFile(fiSourceFile, diTargetFolder.FullName)
                        Continue For

                    ElseIf fiSourceFile.Name.EndsWith(CHECK_JAVA_SUFFIX, StringComparison.InvariantCultureIgnoreCase) Then
                        ' This is a .checkjava file
                        ' Do not copy this file
                        Continue For

                    End If

                    ' Make sure this file does not match a corresponding .delete file
                    If lstDeleteFiles.Contains(strFileNameLCase) Then
                        Continue For
                    End If

                    Dim eDateComparisonMode As eDateComparisonModeConstants

                    If mOverwriteNewerFiles Then
                        eDateComparisonMode = eDateComparisonModeConstants.OverwriteNewerTargetIfDifferentSize
                    Else
                        eDateComparisonMode = eDateComparisonModeConstants.RetainNewerTargetIfDifferentSize
                    End If

                    If lstCheckJavaFiles.Contains(strFileNameLCase) Then
                        If JarFileInUseByJava(fiSourceFile, fileUsageMessage) Then
                            itemInUse = eItemInUseConstants.itemInUse
                        End If
                    Else
                        If TargetFolderInUseByProcess(diTargetFolder.FullName, fiSourceFile.Name, fileUsageMessage) Then
                            ' The folder is in use
                            ' Allow new files to be copied, but do not overwrite existing files
                            itemInUse = eItemInUseConstants.FolderInUse
                        End If
                    End If

                    CopyFileIfNeeded(fiSourceFile, diTargetFolder.FullName, fileUpdateCount,
                                     eDateComparisonMode, blnProcessingSubFolder, itemInUse, fileUsageMessage)

                    ' File processed; move on to the next file
                    Exit While

                Catch ex As Exception
                    If Not errorLogged Then
                        ShowErrorMessage("Error synchronizing " & fiSourceFile.Name & ": " & ex.Message, True)
                        errorLogged = True
                    End If

                    retryCount -= 1
                    Thread.Sleep(100)
                End Try

            End While
        Next

        If fileUpdateCount > 0 Then
            Dim statusMessage = "Updated " & fileUpdateCount & " file"
            If fileUpdateCount > 1 Then statusMessage &= "s"

            statusMessage &= " using " & diSourceFolder.FullName & "\"

            ShowMessage(statusMessage, True, False)
        End If

        ' Process each subdirectory in the source folder
        ' If the folder exists at the target, copy it
        ' Additionally, if the source folder contains file _PushDir_.txt, it gets copied even if it doesn't exist at the target
        For Each diSourceSubFolder As DirectoryInfo In diSourceFolder.GetDirectories()
            Dim strTargetSubFolderPath = Path.Combine(diTargetFolder.FullName, diSourceSubFolder.Name)

            ' Initially assume we'll process this folder if it exists at the target
            Dim diTargetSubFolder = New DirectoryInfo(strTargetSubFolderPath)
            Dim blnProcessSubfolder = diTargetSubFolder.Exists()

            If blnProcessSubfolder AndAlso diSourceSubFolder.GetFiles(DELETE_SUBDIR_FLAG).Length > 0 Then
                ' Remove this subfolder (but only if it's empty)
                Dim folderDeleted = DeleteSubFolder(strTargetSubFolderPath, "subfolder", DELETE_SUBDIR_FLAG)
                If folderDeleted Then blnProcessSubfolder = False
            End If

            If blnPushNewSubfolders AndAlso diSourceSubFolder.GetFiles(PUSH_DIR_FLAG).Length > 0 Then
                blnProcessSubfolder = True
            End If

            If blnProcessSubfolder Then
                UpdateFolderWork(diSourceSubFolder.FullName, diTargetSubFolder.FullName, blnPushNewSubfolders, blnProcessingSubFolder)
            End If
        Next

        Return True
    End Function

    Private Function JarFileInUseByJava(fiSourceFile As FileInfo, <Out()> ByRef jarFileUsageMessage As String) As Boolean

        Const INCLUDE_PROGRAM_PATH = False
        jarFileUsageMessage = String.Empty

        Try
            Dim processes = Process.GetProcesses().ToList()
            processes.Sort(New ProcessNameComparer)

            If mPreviewMode And Not mProcessesShown Then
                Console.WriteLine()
                ShowMessage("Examining running processes for Java", False)
            End If

            Dim lastProcess = String.Empty

            For Each oProcess As Process In processes

                If mPreviewMode And Not mProcessesShown Then
                    If oProcess.ProcessName <> lastProcess Then
                        Console.WriteLine(oProcess.ProcessName)
                    End If
                    lastProcess = oProcess.ProcessName
                End If

                If Not oProcess.ProcessName.StartsWith("java", StringComparison.InvariantCultureIgnoreCase) Then
                    Continue For
                End If

                Try
                    Dim commandLine = GetCommandLine(oProcess, INCLUDE_PROGRAM_PATH)

                    If mPreviewMode And Not mProcessesShown Then
                        Console.WriteLine("  " & commandLine)
                    End If

                    If commandLine.ToLower().Contains(fiSourceFile.Name.ToLower()) Then
                        jarFileUsageMessage = "Skipping " & fiSourceFile.Name & " because currently in use by Java"
                        Return True
                    Else
                        If (String.IsNullOrWhiteSpace(commandLine)) Then
                            jarFileUsageMessage = "Skipping " & fiSourceFile.Name & " because empty Java command line (permissions issue?)"
                            Return True
                        End If

                        ' Uncomment to debug:
                        ' ShowMessage("Command line for java process ID " & oProcess.Id & ": " & commandLine)
                    End If

                Catch ex As Exception
                    ' Skip the process; possibly permission denied

                    jarFileUsageMessage = "Skipping " & fiSourceFile.Name & " because exception: " & ex.Message
                    Return True

                End Try

            Next

            If mPreviewMode And Not mProcessesShown Then
                Console.WriteLine()
                mProcessesShown = True
            End If

        Catch ex As Exception
            ShowErrorMessage("Error looking for Java using " & fiSourceFile.Name & ": " & ex.Message, True)
        End Try

        Return False

    End Function

    Private Function GetCommandLine(oProcess As Process, includeProgramPath As Boolean) As String
        Dim commandLine = New StringBuilder()

        If includeProgramPath Then
            commandLine.Append(oProcess.MainModule.FileName)
            commandLine.Append(" ")
        End If

        Dim result = New ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE ProcessId = " & oProcess.Id)

        For Each item In result.Get()
            commandLine.Append(item("CommandLine"))
        Next

        Return commandLine.ToString()
    End Function

    Private Function TargetFolderInUseByProcess(strTargetFolderPath As String, targetFileName As String, <Out()> ByRef folderUsageMessage As String) As Boolean

        folderUsageMessage = String.Empty

        Try
            Dim firstProcessPath As String = String.Empty
            Dim firstProcessId As UInt32
            Dim processCount = GetNumTargetFolderProcesses(strTargetFolderPath, firstProcessPath, firstProcessId)

            If processCount > 0 Then
                ' Example log messages:
                ' Skipping UIMFLibrary.dll because folder DeconTools is in use by process DeconConsole.exe (PID 343243)
                ' Skipping DeconConsole.exe because folder DeconTools is in use by 2 processes on this system, including DeconConsole.exe (PID 343243)

                folderUsageMessage = "Skipping " & targetFileName & " because folder " & AbbreviatePath(strTargetFolderPath) & " is in use by "

                Dim processPathToShow As String

                If String.IsNullOrWhiteSpace(firstProcessPath) Then
                    processPathToShow = "an unknown process"
                Else
                    Dim diProcessFile = New FileInfo(firstProcessPath)
                    Dim processIdAppend = " (" & " PID " & firstProcessId & ")"

                    If diProcessFile.DirectoryName = strTargetFolderPath Then
                        processPathToShow = Path.GetFileName(firstProcessPath) & processIdAppend

                    ElseIf strTargetFolderPath.StartsWith(diProcessFile.DirectoryName) Then
                        Dim relativePath = diProcessFile.Directory.Parent.FullName
                        Dim pathPart As String
                        If diProcessFile.DirectoryName.Length > relativePath.Length Then
                            pathPart = diProcessFile.DirectoryName.Substring(relativePath.Length + 1)
                        Else
                            pathPart = diProcessFile.DirectoryName
                        End If

                        processPathToShow = Path.Combine(pathPart, Path.GetFileName(firstProcessPath)) & processIdAppend
                    Else
                        processPathToShow = firstProcessPath & processIdAppend
                    End If

                End If

                If processCount = 1 Then
                    folderUsageMessage &= "process " & processPathToShow
                Else
                    folderUsageMessage &= processCount & " processes on this system, including " & processPathToShow
                End If

                Return True
            End If

        Catch ex As Exception
            ShowErrorMessage("Error looking for processes using files in " & strTargetFolderPath & ": " & ex.Message, True)
        End Try

        Return False
    End Function

    ''' <summary>
    ''' Determine the number of processes using files in the given folder
    ''' </summary>
    ''' <param name="strTargetFolderPath">Folder to examine</param>
    ''' <param name="firstProcessPath">Output parameter: first process using files in this folder; empty string if no processes</param>
    ''' <param name="firstProcessId">Output parameter: Process ID of first process using files in this folder</param>
    ''' <returns>Count of processes using this folder</returns>
    Private Function GetNumTargetFolderProcesses(
      strTargetFolderPath As String,
      <Out()> ByRef firstProcessPath As String,
      <Out()> ByRef firstProcessId As UInt32) As Integer

        firstProcessPath = String.Empty
        firstProcessId = 0

        ' Filter the queried results for each call to this function.

        Dim targetFolderPathHierarchy = clsProcessInfo.GetFolderHierarchy(strTargetFolderPath)

        If String.Equals(strTargetFolderPath, mLastFolderProcessesChecked, StringComparison.InvariantCultureIgnoreCase) Then
            firstProcessPath = mLastFolderRunningProcessPath
            firstProcessId = mLastFolderRunningProcessId
            Return mProcessesMatchingTarget.Count
        End If

        mProcessesMatchingTarget.Clear()
        mLastFolderProcessesChecked = strTargetFolderPath
        mLastFolderRunningProcessPath = String.Empty
        mLastFolderRunningProcessId = 0

        ' Look for running processes with a .exe in the target folder (or in a parent of the target folder)
        ' Ignore cmd.exe
        For Each item In mProcessesDict
            Dim exeFolderHierarchy = item.Value.FolderHierarchy

            If exeFolderHierarchy.Count > targetFolderPathHierarchy.Count Then
                Continue For
            End If

            Dim treesMatch = True
            For index = 0 To exeFolderHierarchy.Count - 1
                If targetFolderPathHierarchy(index) <> exeFolderHierarchy(index) Then
                    treesMatch = False
                    Exit For
                End If
            Next
            If Not treesMatch Then Continue For

            mProcessesMatchingTarget.Add(item.Key, item.Value.CommandLine)
            If String.IsNullOrWhiteSpace(firstProcessPath) Then
                firstProcessPath = item.Value.ExePath
                firstProcessId = item.Key
                If String.IsNullOrWhiteSpace(firstProcessPath) Then
                    firstProcessPath = item.Value.CommandLine
                End If
            End If

        Next

        ' Next examine the command line arguments of running processes
        ' This can help check for .jar files that don't have a .checkjava flag file
        For Each item In mProcessesDict

            If item.Value.CommandLineArgs.IndexOf(strTargetFolderPath, StringComparison.InvariantCultureIgnoreCase) < 0 Then
                Continue For
            End If

            If mProcessesMatchingTarget.ContainsKey(item.Key) Then Continue For

            mProcessesMatchingTarget.Add(item.Key, item.Value.CommandLine)
            If String.IsNullOrWhiteSpace(firstProcessPath) Then
                firstProcessPath = item.Value.ExePath
                firstProcessId = item.Key
                If String.IsNullOrWhiteSpace(firstProcessPath) Then
                    firstProcessPath = item.Value.CommandLine
                End If
            End If

        Next

        If mProcessesMatchingTarget.Count > 0 Then
            mLastFolderRunningProcessPath = firstProcessPath
            mLastFolderRunningProcessId = firstProcessId
        End If

        Return mProcessesMatchingTarget.Count

    End Function

    Private Sub ProcessDeleteFile(fiDeleteFile As FileInfo, strTargetFolderPath As String)

        Dim strTargetFilePath = Path.Combine(strTargetFolderPath, TrimSuffix(fiDeleteFile.Name, DELETE_SUFFIX))
        Dim fiTargetFile = New FileInfo(strTargetFilePath)

        If fiTargetFile.Exists() Then
            If mPreviewMode Then
                ShowMessage("Preview delete: " & fiTargetFile.FullName)
            Else
                fiTargetFile.Delete()
                ShowMessage("Deleted file " & fiTargetFile.FullName)
            End If
        End If

        ' Make sure the .delete is also not in the target folder
        Dim strTargetDeleteFilePath = Path.Combine(strTargetFolderPath, fiDeleteFile.Name)
        Dim fiTargetDeleteFile = New FileInfo(strTargetDeleteFilePath)

        If fiTargetDeleteFile.Exists() Then
            If mPreviewMode Then
                ShowMessage("Preview delete: " & fiTargetDeleteFile.FullName)
            Else
                fiTargetDeleteFile.Delete()
                ShowMessage("Deleted file " & fiTargetDeleteFile.FullName)
            End If
        End If
    End Sub

    ''' <summary>
    ''' Rollback the target file if it differs from the source
    ''' </summary>
    ''' <param name="fiRollbackFile">Rollback file path</param>
    ''' <param name="strTargetFolderPath">Target folder</param>
    ''' <param name="fileUpdateCount">Number of files that have been updated (Input/output)</param>
    ''' <param name="blnProcessingSubFolder">True if processing a subfolder</param>
    ''' <param name="itemInUse">Used to track when a file or folder is in use by another process (log a message if the source and target files differ)</param>
    ''' <param name="fileUsageMessage">Message to log when the file (or folder) is in use and the source and targets differ</param>
    Private Sub ProcessRollbackFile(
      fiRollbackFile As FileInfo,
      strTargetFolderPath As String,
      ByRef fileUpdateCount As Integer,
      blnProcessingSubFolder As Boolean,
      Optional itemInUse As eItemInUseConstants = eItemInUseConstants.NotInUse,
      Optional fileUsageMessage As String = "")

        Dim strSourceFilePath = TrimSuffix(fiRollbackFile.FullName, ROLLBACK_SUFFIX)

        Dim fiSourceFile = New FileInfo(strSourceFilePath)

        If fiSourceFile.Exists() Then
            Dim copied = CopyFileIfNeeded(fiSourceFile, strTargetFolderPath, fileUpdateCount,
                                          eDateComparisonModeConstants.CopyIfSizeOrDateDiffers,
                                          blnProcessingSubFolder, itemInUse, fileUsageMessage)
            If copied Then
                Dim prefix As String

                If mPreviewMode Then
                    prefix = "Preview rollback of file "
                Else
                    prefix = "Rolled back file "
                End If

                ShowMessage(prefix & fiSourceFile.Name &
                            " to version from " & fiSourceFile.LastWriteTimeUtc.ToLocalTime() &
                            " with size " & (fiSourceFile.Length / 1024.0).ToString("0.0") & " KB")
            End If
        Else
            ShowMessage("Warning: Rollback file is present (" + fiRollbackFile.Name + ") but expected source file was not found: " &
                        fiSourceFile.Name, intDuplicateHoldoffHours:=24)
        End If

    End Sub

    Private Sub SetLocalErrorCode(eNewErrorCode As eDMSUpdateManagerErrorCodes)
        SetLocalErrorCode(eNewErrorCode, False)
    End Sub

    Private Sub SetLocalErrorCode(eNewErrorCode As eDMSUpdateManagerErrorCodes, blnLeaveExistingErrorCodeUnchanged As Boolean)

        If blnLeaveExistingErrorCodeUnchanged AndAlso mLocalErrorCode <> eDMSUpdateManagerErrorCodes.NoError Then
            ' An error code is already defined; do not change it
        Else
            mLocalErrorCode = eNewErrorCode

            If eNewErrorCode = eDMSUpdateManagerErrorCodes.NoError Then
                If MyBase.ErrorCode = eProcessFoldersErrorCodes.LocalizedError Then
                    MyBase.SetBaseClassErrorCode(eProcessFoldersErrorCodes.NoError)
                End If
            Else
                MyBase.SetBaseClassErrorCode(eProcessFoldersErrorCodes.LocalizedError)
            End If
        End If

    End Sub

    Private Function TrimSuffix(strText As String, strSuffix As String) As String
        If strText.Length >= strSuffix.Length Then
            Return strText.Substring(0, strText.Length - strSuffix.Length)
        Else
            Return strText
        End If
    End Function

    Private Class ProcessNameComparer
        Implements IComparer(Of Process)

        Public Function Compare(x As Process, y As Process) As Integer Implements IComparer(Of Process).Compare
            Return String.Compare(x.ProcessName, y.ProcessName, StringComparison.InvariantCultureIgnoreCase)
        End Function
    End Class

End Class
