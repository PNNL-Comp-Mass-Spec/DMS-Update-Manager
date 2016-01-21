Option Strict On

Imports System.ComponentModel
Imports System.IO
Imports System.Management
Imports System.Reflection
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
''' Website: http://ncrr.pnnl.gov/ or http://www.sysbio.org/resources/staff/
''' </remarks>
Public Class clsDMSUpdateManager
    Inherits clsProcessFoldersBaseClass

    Public Sub New()
        mFileDate = "January 21, 2016"
        InitializeLocalVariables()
    End Sub


#Region "Constants and Enums"

    ' Error codes specialized for this class
    Public Enum eDMSUpdateManagerErrorCodes As Integer
        NoError = 0
        UnspecifiedError = -1
    End Enum

    Protected Enum eDateComparisonModeConstants
        RetainNewerTargetIfDifferentSize = 0
        OverwriteNewerTargetIfDifferentSize = 2
        CopyIfSizeOrDateDiffers = 3
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
    Protected mPreviewMode As Boolean

    ' If False, then will not overwrite files in the target folder that are newer than files in the source folder
    Protected mOverwriteNewerFiles As Boolean

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

    Protected mCopySubdirectoriesToParentFolder As Boolean

    ' The following is the path that lists the files that will be copied to the target folder
    Protected mSourceFolderPath As String
    Protected mTargetFolderPath As String

    ' List of files that will not be copied
    ' The names must be full filenames (no wildcards)
    Protected mFilesToIgnoreCount As Integer
    Protected mFilesToIgnore() As String

    Protected mLocalErrorCode As eDMSUpdateManagerErrorCodes
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

    Public Sub AddFileToIgnore(strFileName As String)

        If Not strFileName Is Nothing AndAlso strFileName.Length > 0 Then
            If mFilesToIgnoreCount >= mFilesToIgnore.Length Then
                ReDim Preserve mFilesToIgnore(mFilesToIgnore.Length * 2 - 1)
            End If

            mFilesToIgnore(mFilesToIgnoreCount) = strFileName
            mFilesToIgnoreCount += 1
        End If

    End Sub

    Protected Sub CopyFile(fiSourceFile As FileInfo, fiTargetFile As FileInfo, ByRef intFileUpdateCount As Integer, strCopyReason As String)

        Dim existingFileInfo As String

        If fiTargetFile.Exists Then
            existingFileInfo = "Old: " & fiTargetFile.LastWriteTimeUtc.ToString("yyyy-MM-dd hh:mm:ss tt") & " and " & fiTargetFile.Length & " bytes"
        Else
            existingFileInfo = String.Empty
        End If

        Dim updatedFileInfo = "New: " & fiSourceFile.LastWriteTimeUtc.ToString("yyyy-MM-dd hh:mm:ss tt") & " and " & fiSourceFile.Length & " bytes"

        If mPreviewMode Then
            ShowMessage("Preview: Update file: " & fiSourceFile.Name & "; " & strCopyReason, False)
            If fiTargetFile.Exists Then
                ShowMessage("                      " & existingFileInfo)
            End If
            ShowMessage("                      " & updatedFileInfo)
        Else

            ShowMessage("Update file: " & fiSourceFile.Name & "; " & strCopyReason)
            If fiTargetFile.Exists Then
                ShowMessage("             " & existingFileInfo)
            End If
            ShowMessage("             " & updatedFileInfo)

            Try
                Dim fiCopiedFile = fiSourceFile.CopyTo(fiTargetFile.FullName, True)

                If fiCopiedFile.Length <> fiSourceFile.Length Then
                    ShowErrorMessage("Copy of " & fiSourceFile.Name & " failed; sizes differ", True)
                ElseIf fiCopiedFile.LastWriteTimeUtc <> fiSourceFile.LastWriteTimeUtc Then
                    ShowErrorMessage("Copy of " & fiSourceFile.Name & " failed; modification times differ", True)
                Else
                    intFileUpdateCount += 1
                End If

            Catch ex As Exception
                ShowErrorMessage("Error copying " & fiSourceFile.Name & ": " & ex.Message, True)
            End Try

        End If

    End Sub


    Protected Function CopyFileIfNeeded(ByRef fiSourceFile As FileInfo, strTargetFolderPath As String,
       ByRef intFileUpdateCount As Integer, eDateComparisonMode As eDateComparisonModeConstants,
       blnProcessingSubFolder As Boolean) As Boolean

        Dim blnNeedToCopy As Boolean
        Dim strCopyReason As String

        Dim strTargetFilePath As String
        Dim fiTargetFile As FileInfo

        strTargetFilePath = Path.Combine(strTargetFolderPath, fiSourceFile.Name)
        fiTargetFile = New FileInfo(strTargetFilePath)

        strCopyReason = String.Empty
        blnNeedToCopy = False

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
            CopyFile(fiSourceFile, fiTargetFile, intFileUpdateCount, strCopyReason)
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

        mFilesToIgnoreCount = 0
        ReDim mFilesToIgnore(9)

        mLocalErrorCode = eDMSUpdateManagerErrorCodes.NoError

    End Sub

    Public Overrides Function GetErrorMessage() As String
        ' Returns "" if no error

        Dim strErrorMessage As String

        If MyBase.ErrorCode = eProcessFoldersErrorCodes.LocalizedError Or
           MyBase.ErrorCode = eProcessFoldersErrorCodes.NoError Then
            Select Case mLocalErrorCode
                Case eDMSUpdateManagerErrorCodes.NoError
                    strErrorMessage = ""
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

    Private Function LoadParameterFileSettings(strParameterFilePath As String) As Boolean

        Const OPTIONS_SECTION = "DMSUpdateManager"

        Dim objSettingsFile = New XmlSettingsFileAccessor()

        Dim strFilesToIgnore As String
        Dim strIgnoreList() As String

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

                    strFilesToIgnore = objSettingsFile.GetParam(OPTIONS_SECTION, "FilesToIgnore", String.Empty)
                    Try
                        If strFilesToIgnore.Length > 0 Then
                            strIgnoreList = strFilesToIgnore.Split(","c)

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

    Public Overloads Overrides Function ProcessFolder(strInputFolderPath As String, strOutputFolderAlternatePath As String, strParameterFilePath As String, blnResetErrorCode As Boolean) As Boolean
        ' Returns True if success, False if failure
        ' Note: strOutputFolderAlternatePath is ignored in this function

        Return UpdateFolder(strInputFolderPath, strParameterFilePath)
    End Function

    Public Function UpdateFolder(strTargetFolderPath As String, strParameterFilePath As String) As Boolean
        ' Returns True if success, False if failure

        Dim blnSuccess As Boolean

        SetLocalErrorCode(eDMSUpdateManagerErrorCodes.NoError)

        If Not strTargetFolderPath Is Nothing AndAlso strTargetFolderPath.Length > 0 Then
            ' Update mTargetFolderPath using strTargetFolderPath
            ' Note: If TargetFolder is defined in the parameter file, then this value will get overridden
            mTargetFolderPath = String.Copy(strTargetFolderPath)
        End If

        If Not LoadParameterFileSettings(strParameterFilePath) Then
            ShowErrorMessage("Parameter file load error: " & strParameterFilePath)

            If MyBase.ErrorCode = eProcessFoldersErrorCodes.NoError Then
                MyBase.SetBaseClassErrorCode(eProcessFoldersErrorCodes.InvalidParameterFile)
            End If
            Return False
        End If

        Try
            strTargetFolderPath = String.Copy(mTargetFolderPath)

            If mSourceFolderPath Is Nothing OrElse mSourceFolderPath.Length = 0 Then
                ShowMessage("Source folder path is not defined.  Either specify it at the command line or include it in the parameter file.")
                MyBase.SetBaseClassErrorCode(eProcessFoldersErrorCodes.InvalidInputFolderPath)
                Return False
            End If

            If strTargetFolderPath Is Nothing OrElse strTargetFolderPath.Length = 0 Then
                ShowMessage("Target folder path is not defined.  Either specify it at the command line or include it in the parameter file.")
                MyBase.SetBaseClassErrorCode(eProcessFoldersErrorCodes.InvalidInputFolderPath)
                Return False
            End If

            ' Note that CleanupFilePaths() will update mOutputFolderPath, which is used by LogMessage()
            If Not CleanupFolderPaths(strTargetFolderPath, String.Empty) Then
                MyBase.SetBaseClassErrorCode(eProcessFoldersErrorCodes.FilePathError)
                Return False
            End If

            Dim diSourceFolder = New DirectoryInfo(mSourceFolderPath)
            Dim diTargetFolder = New DirectoryInfo(strTargetFolderPath)

            MyBase.mProgressStepDescription = "Updating " & diTargetFolder.Name & ControlChars.NewLine & " using " & diSourceFolder.FullName
            MyBase.ResetProgress()

            blnSuccess = UpdateFolderWork(diSourceFolder.FullName, diTargetFolder.FullName, blnPushNewSubfolders:=False, blnProcessingSubFolder:=False)

            If mCopySubdirectoriesToParentFolder Then
                blnSuccess = UpdateFolderCopyToParent(diTargetFolder, diSourceFolder)
            End If

        Catch ex As Exception
            HandleException("Error in UpdateFolder", ex)
        End Try

        Return blnSuccess

    End Function

    Protected Function UpdateFolderCopyToParent(diTargetFolder As DirectoryInfo, diSourceFolder As DirectoryInfo) As Boolean

        Dim blnSuccess As Boolean

        For Each diSourceSubFolder As DirectoryInfo In diSourceFolder.GetDirectories()

            ' The target folder is treated as a subdirectory of the parent folder
            Dim strTargetSubFolderPath = Path.Combine(diTargetFolder.Parent.FullName, diSourceSubFolder.Name)

            ' Initially assume we'll process the folder if it exists at the target
            Dim blnProcessSubfolder = Directory.Exists(strTargetSubFolderPath)

            If diSourceSubFolder.GetFiles(DELETE_SUBDIR_FLAG).Length > 0 Then
                ' Remove this subfolder at the target (but only if it's empty)
                Dim diTargetSubFolder = New DirectoryInfo(strTargetSubFolderPath)

                If diTargetSubFolder.Exists AndAlso diTargetSubFolder.GetFiles().Length = 0 Then
                    blnProcessSubfolder = False
                    Try
                        diTargetSubFolder.Delete(False)
                        ShowMessage("Deleted parent subfolder " & diTargetSubFolder.FullName)
                    Catch ex As Exception
                        HandleException("Error removing empty parent subfolder flagged with " & DELETE_SUBDIR_FLAG, ex)
                    End Try
                End If
            End If

            If diSourceSubFolder.GetFiles(DELETE_AM_SUBDIR_FLAG).Length > 0 Then
                ' Remove this subfolder if it is present below diTargetFolder (but only if it's empty)

                Dim strAMSubDirPath = Path.Combine(diTargetFolder.FullName, diSourceSubFolder.Name)
                Dim diTargetSubFolder = New DirectoryInfo(strAMSubDirPath)

                If diTargetSubFolder.Exists AndAlso diTargetSubFolder.GetFiles().Length = 0 Then
                    blnProcessSubfolder = False
                    Try
                        diTargetSubFolder.Delete(False)
                        ShowMessage("Deleted subfolder " & diTargetSubFolder.FullName)
                    Catch ex As Exception
                        HandleException("Error removing empty subfolder flagged with " & DELETE_SUBDIR_FLAG, ex)
                    End Try
                End If
            End If

            If diSourceSubFolder.GetFiles(PUSH_AM_SUBDIR_FLAG).Length > 0 Then
                ' Push this folder as a subdirectory of the current folder, not as a subdirectory of the parent folder
                strTargetSubFolderPath = Path.Combine(diTargetFolder.FullName, diSourceSubFolder.Name)
                blnProcessSubfolder = True
            Else
                If diSourceSubFolder.GetFiles(PUSH_DIR_FLAG).Length > 0 Then
                    blnProcessSubfolder = True
                End If
            End If

            If blnProcessSubfolder Then
                blnSuccess = UpdateFolderWork(diSourceSubFolder.FullName, strTargetSubFolderPath, blnPushNewSubfolders:=True, blnProcessingSubFolder:=True)
            End If
        Next

        Return blnSuccess

    End Function

    Protected Function UpdateFolderWork(strSourceFolderPath As String, strTargetFolderPath As String, blnPushNewSubfolders As Boolean, blnProcessingSubFolder As Boolean) As Boolean

        Dim strStatusMessage As String

        Dim intFileUpdateCount As Integer

        Dim eDateComparisonMode As eDateComparisonModeConstants

        MyBase.mProgressStepDescription = "Updating " & strTargetFolderPath & ControlChars.NewLine & " using " & strSourceFolderPath
        ShowMessage(MyBase.mProgressStepDescription, False)

        ' Make sure the target folder exists
        Dim diTargetFolder = New DirectoryInfo(strTargetFolderPath)
        If Not diTargetFolder.Exists Then
            diTargetFolder.Create()
        End If

        ' Obtain a list of files in the source folder
        Dim diSourceFolder = New DirectoryInfo(strSourceFolderPath)

        intFileUpdateCount = 0

        Dim fiFilesInSource = diSourceFolder.GetFiles()

        ' Populate a List object the with the names of any .delete files in fiFilesInSource
        Dim lstDeleteFiles = New List(Of String)(fiFilesInSource.Length)
        lstDeleteFiles.AddRange(From fiSourceFile In fiFilesInSource
                                Where fiSourceFile.Name.EndsWith(DELETE_SUFFIX, StringComparison.InvariantCultureIgnoreCase)
                                Select TrimSuffix(fiSourceFile.Name, DELETE_SUFFIX).ToLower())

        ' Populate a List object the with the names of any .checkjava files in fiFilesInSource
        Dim lstCheckJavaFiles = New List(Of String)(fiFilesInSource.Length)
        lstCheckJavaFiles.AddRange(From fiSourceFile In fiFilesInSource
                                   Where fiSourceFile.Name.EndsWith(CHECK_JAVA_SUFFIX, StringComparison.InvariantCultureIgnoreCase)
                                   Select TrimSuffix(fiSourceFile.Name, CHECK_JAVA_SUFFIX).ToLower())

        For Each fiSourceFile As FileInfo In fiFilesInSource

            Dim retryCount = 2
            Dim errorLogged = False

            While retryCount >= 0

                Try
                    Dim strFileNameLCase = fiSourceFile.Name.ToLower()
                    Dim blnSkipFile = False

                    ' Make sure this file is not in mFilesToIgnore
                    For intIndex = 0 To mFilesToIgnoreCount - 1
                        If strFileNameLCase = mFilesToIgnore(intIndex).ToLower Then
                            ' Skip file
                            blnSkipFile = True
                            Exit For
                        End If
                    Next intIndex

                    If strFileNameLCase = PUSH_DIR_FLAG.ToLower() Then
                        blnSkipFile = True
                    ElseIf strFileNameLCase = PUSH_AM_SUBDIR_FLAG.ToLower() Then
                        blnSkipFile = True
                    ElseIf strFileNameLCase = DELETE_SUBDIR_FLAG.ToLower() Then
                        blnSkipFile = True
                    ElseIf strFileNameLCase = DELETE_AM_SUBDIR_FLAG.ToLower() Then
                        blnSkipFile = True
                    End If

                    If blnSkipFile Then
                        Continue For
                    End If

                    ' See if file ends with one of the special suffix flags
                    If fiSourceFile.Name.EndsWith(ROLLBACK_SUFFIX, StringComparison.InvariantCultureIgnoreCase) Then
                        ' This is a Rollback file
                        ' Do not copy this file
                        ' However, do look for a corresponding file that does not have .rollback and copy it if the target file has a different date or size

                        ProcessRollbackFile(fiSourceFile, diTargetFolder.FullName, intFileUpdateCount, blnProcessingSubFolder)

                    ElseIf fiSourceFile.Name.EndsWith(DELETE_SUFFIX, StringComparison.InvariantCultureIgnoreCase) Then
                        ' This is a Delete file
                        ' Do not copy this file
                        ' However, do look for a corresponding file that does not have .delete and delete that file in the target folder

                        ProcessDeleteFile(fiSourceFile, diTargetFolder.FullName)

                    ElseIf fiSourceFile.Name.EndsWith(CHECK_JAVA_SUFFIX, StringComparison.InvariantCultureIgnoreCase) Then
                        ' This is a .checkjava file
                        ' Do not copy this file

                    Else
                        ' Make sure this file does not match a corresponding .delete file
                        If Not lstDeleteFiles.Contains(strFileNameLCase) Then

                            If mOverwriteNewerFiles Then
                                eDateComparisonMode = eDateComparisonModeConstants.OverwriteNewerTargetIfDifferentSize
                            Else
                                eDateComparisonMode = eDateComparisonModeConstants.RetainNewerTargetIfDifferentSize
                            End If

                            If lstCheckJavaFiles.Contains(strFileNameLCase) Then
                                If JarFileInUseByJava(fiSourceFile) Then
                                    ' Jar file is in use; move on to the next file
                                    Continue For
                                End If
                            End If

                            CopyFileIfNeeded(fiSourceFile, diTargetFolder.FullName, intFileUpdateCount, eDateComparisonMode, blnProcessingSubFolder)

                        End If

                    End If

                    ' File processed; move on to the next file
                    Continue For

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

        If intFileUpdateCount > 0 Then
            strStatusMessage = "Updated " & intFileUpdateCount & " file"
            If intFileUpdateCount > 1 Then strStatusMessage &= "s"

            strStatusMessage &= " using " & diSourceFolder.FullName & "\"

            ShowMessage(strStatusMessage, True, False)
        End If

        ' Process each subdirectory in the source folder
        ' If the folder exists at the target, then copy it
        ' Additionally, if the source folder contains file _PushDir_.txt then it gets copied even if it doesn't exist at the target
        For Each diSourceSubFolder As DirectoryInfo In diSourceFolder.GetDirectories()
            Dim strTargetSubFolderPath = Path.Combine(diTargetFolder.FullName, diSourceSubFolder.Name)

            ' Initially assume we'll process this folder if it exists at the target
            Dim blnProcessSubfolder As Boolean
            blnProcessSubfolder = Directory.Exists(strTargetSubFolderPath)

            If diSourceSubFolder.GetFiles(DELETE_SUBDIR_FLAG).Length > 0 Then
                ' Remove this subfolder (but only if it's empty)
                If diSourceSubFolder.GetFiles().Length = 1 Then
                    blnProcessSubfolder = False
                    Try
                        diSourceSubFolder.Delete(False)
                    Catch ex As Exception
                        HandleException("Error removing empty directory flagged with " & DELETE_SUBDIR_FLAG, ex)
                    End Try
                End If
            End If

            If blnPushNewSubfolders AndAlso diSourceSubFolder.GetFiles(PUSH_DIR_FLAG).Length > 0 Then
                blnProcessSubfolder = True
            End If

            If blnProcessSubfolder Then
                UpdateFolderWork(diSourceSubFolder.FullName, strTargetSubFolderPath, blnPushNewSubfolders, blnProcessingSubFolder)
            End If
        Next

        Return True
    End Function

    Private Function JarFileInUseByJava(fiSourceFile As FileInfo) As Boolean

        Dim INCLUDE_PROGRAM_PATH = False

        Try
            For Each oProcess As Process In Process.GetProcesses()
                Dim processNameLcase = oProcess.ProcessName.ToLower()

                If Not processNameLcase.Contains("java") Then
                    Continue For
                End If

                Try
                    Dim commandLine = GetCommandLine(oProcess, INCLUDE_PROGRAM_PATH)

                    If commandLine.ToLower().Contains(fiSourceFile.Name.ToLower()) Then
                        ShowMessage("Skipping " & fiSourceFile.Name & " because currently in use by Java")
                        Return True
                    End If

                Catch ex As Win32Exception
                    ' Skip the process if permission denied
                    ' Otherwise, re-throw the exception
                    If CUInt(ex.ErrorCode) <> &H80004005UI Then
                        Throw
                    End If
                End Try

            Next

        Catch ex As Exception
            ShowErrorMessage("Error looking for java using " & fiSourceFile.Name & ": " & ex.Message, True)
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

    Private Sub ProcessDeleteFile(fiDeleteFile As FileInfo, strTargetFolderPath As String)

        Dim strTargetFilePath = Path.Combine(strTargetFolderPath, TrimSuffix(fiDeleteFile.Name, DELETE_SUFFIX))
        Dim fiTargetFile = New FileInfo(strTargetFilePath)

        If fiTargetFile.Exists() Then
            fiTargetFile.Delete()
            ShowMessage("Deleted file " & fiTargetFile.FullName)
        End If

        ' Make sure the .delete is also not in the target folder
        Dim strTargetDeleteFilePath = Path.Combine(strTargetFolderPath, fiDeleteFile.Name)
        Dim fiTargetDeleteFile = New FileInfo(strTargetDeleteFilePath)

        If fiTargetDeleteFile.Exists() Then
            fiTargetDeleteFile.Delete()
            ShowMessage("Deleted file " & fiTargetDeleteFile.FullName)
        End If
    End Sub

    Private Sub ProcessRollbackFile(fiRollbackFile As FileInfo, strTargetFolderPath As String, ByRef intFileUpdateCount As Integer, blnProcessingSubFolder As Boolean)
        Dim fiSourceFile As FileInfo
        Dim strSourceFilePath As String
        Dim blnCopied As Boolean

        strSourceFilePath = TrimSuffix(fiRollbackFile.FullName, ROLLBACK_SUFFIX)

        fiSourceFile = New FileInfo(strSourceFilePath)

        If fiSourceFile.Exists() Then
            blnCopied = CopyFileIfNeeded(fiSourceFile, strTargetFolderPath, intFileUpdateCount, eDateComparisonModeConstants.CopyIfSizeOrDateDiffers, blnProcessingSubFolder)
            If blnCopied Then
                ShowMessage("Rolled back file " & fiSourceFile.Name & " to version from " & fiSourceFile.LastWriteTimeUtc.ToLocalTime() & " with size " & (fiSourceFile.Length / 1024.0).ToString("0.0") & " KB")
            End If
        Else
            ShowMessage("Warning: Rollback file is present (" + fiRollbackFile.Name + ") but expected source file was not found: " & fiSourceFile.Name, intDuplicateHoldoffHours:=24)
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
End Class
