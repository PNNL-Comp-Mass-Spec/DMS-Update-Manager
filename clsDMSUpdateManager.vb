Option Strict On

Imports System.IO
Imports System.Reflection
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
        mFileDate = "January 18, 2016"
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

    ' This path defines the folder to examine to look for a file named ComputerName_RebootNow.txt or ComputerName_ShutdownNow.txt
    Protected mRebootCommandFolderPath As String

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

    Public Property RebootCommandFolderPath() As String
        Get
            Return mRebootCommandFolderPath
        End Get
        Set(value As String)
            If Not value Is Nothing Then
                mRebootCommandFolderPath = value
            End If
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

    Public Sub CheckForRebootOrShutdownFile(blnPreview As Boolean)
        CheckForRebootOrShutdownFile(mRebootCommandFolderPath, blnPreview)
    End Sub

    ''' <summary>
    ''' Look for a file named MachineName_RebootNow.txt or MachineName_ShutdownNow.txt in strSourceFolder
    ''' If strSourceFolder is empty, then does not do anything
    ''' </summary>
    ''' <param name="strSourceFolder"></param>
    ''' <remarks></remarks>
    Public Sub CheckForRebootOrShutdownFile(strSourceFolder As String, blnPreview As Boolean)

        Dim blnMatchFound As Boolean

        If strSourceFolder Is Nothing OrElse strSourceFolder.Length = 0 Then
            If blnPreview Then
                Console.WriteLine("SourceFolder to look for the RebootNow.txt file is empty; will not look for the file")
            End If
        Else
            blnMatchFound = CheckForRebootOrShutdownFileWork(strSourceFolder, "_RebootNow.txt", MentalisUtils.RestartOptions.Reboot, blnPreview)

            If Not blnMatchFound Then
                CheckForRebootOrShutdownFileWork(strSourceFolder, "_ShutdownNow.txt", MentalisUtils.RestartOptions.ShutDown, blnPreview)
            End If
        End If

    End Sub

    Protected Function CheckForRebootOrShutdownFileWork(strSourceFolder As String,
        strTargetFileSuffix As String,
        eAction As MentalisUtils.RestartOptions,
        blnPreview As Boolean) As Boolean

        Dim ioFileInfo As FileInfo
        Dim strFilePathToFind As String
        Dim strNewPath As String
        Dim strComputer As String

        Try
            strComputer = Environment.MachineName.ToString()
            strFilePathToFind = Path.Combine(strSourceFolder, strComputer & strTargetFileSuffix)

            Console.WriteLine("Looking for " & strFilePathToFind)
            ioFileInfo = New FileInfo(strFilePathToFind)

            If Not ioFileInfo.Exists Then Return False

            Console.WriteLine("Found file: " & strFilePathToFind)

            If Not blnPreview Then
                strNewPath = strFilePathToFind & ".done"

                Try
                    If File.Exists(strNewPath) Then
                        File.Delete(strNewPath)
                        Thread.Sleep(500)
                    End If
                Catch ex As Exception
                    Console.WriteLine("Error deleting file " & strNewPath & ": " & ex.Message)
                End Try

                Try
                    ioFileInfo.MoveTo(strNewPath)
                    Thread.Sleep(500)
                Catch ex As Exception
                    Console.WriteLine("Error renaming " & Path.GetFileName(strFilePathToFind) & " to " & Path.GetFileName(strNewPath) & ": " & ex.Message)
                End Try

            End If

            If blnPreview Then
                Console.WriteLine("Preview: Call LogoffOrRebootMachine with command " & eAction.ToString)
            Else
                LogoffOrRebootMachine(eAction, True)
            End If

        Catch ex As Exception
            Console.WriteLine("Error in CheckForRebootOrShutdownFileWork: " & ex.Message)

        End Try

        Return False

    End Function

    Protected Sub CopyFile(ByRef objSourceFile As FileInfo, ByRef objTargetFile As FileInfo, ByRef intFileUpdateCount As Integer, strCopyReason As String)

        Dim objCopiedFile As FileInfo

        Dim existingFileInfo As String

        If objTargetFile.Exists Then
            existingFileInfo = "Old: " & objTargetFile.LastWriteTimeUtc.ToString("yyyy-MM-dd hh:mm:ss tt") & " and " & objTargetFile.Length & " bytes"
        Else
            existingFileInfo = String.Empty
        End If

        Dim updatedFileInfo = "New: " & objSourceFile.LastWriteTimeUtc.ToString("yyyy-MM-dd hh:mm:ss tt") & " and " & objSourceFile.Length & " bytes"

        If mPreviewMode Then
            ShowMessage("Preview: Update file: " & objSourceFile.Name & "; " & strCopyReason, False)
            If objTargetFile.Exists Then
                ShowMessage("                      " & existingFileInfo)
            End If
            ShowMessage("                      " & updatedFileInfo)
        Else

            ShowMessage("Update file: " & objSourceFile.Name & "; " & strCopyReason)
            If objTargetFile.Exists Then
                ShowMessage("             " & existingFileInfo)
            End If
            ShowMessage("             " & updatedFileInfo)

            Try
                objCopiedFile = objSourceFile.CopyTo(objTargetFile.FullName, True)

                If objCopiedFile.Length <> objSourceFile.Length Then
                    ShowErrorMessage("Copy of " & objSourceFile.Name & " failed; sizes differ", True)
                ElseIf objCopiedFile.LastWriteTimeUtc <> objSourceFile.LastWriteTimeUtc Then
                    ShowErrorMessage("Copy of " & objSourceFile.Name & " failed; modification times differ", True)
                Else
                    intFileUpdateCount += 1
                End If

            Catch ex As Exception
                ShowErrorMessage("Error copying " & objSourceFile.Name & ": " & ex.Message, True)
            End Try

        End If

    End Sub


    Protected Function CopyFileIfNeeded(ByRef objSourceFile As FileInfo, strTargetFolderPath As String,
       ByRef intFileUpdateCount As Integer, eDateComparisonMode As eDateComparisonModeConstants,
       blnProcessingSubFolder As Boolean) As Boolean

        Dim blnNeedToCopy As Boolean
        Dim strCopyReason As String

        Dim strTargetFilePath As String
        Dim objTargetFile As FileInfo

        strTargetFilePath = Path.Combine(strTargetFolderPath, objSourceFile.Name)
        objTargetFile = New FileInfo(strTargetFilePath)

        strCopyReason = String.Empty
        blnNeedToCopy = False

        If Not objTargetFile.Exists Then
            ' File not present in the target; copy it now
            strCopyReason = "not found in target folder"
            blnNeedToCopy = True
        Else
            ' File is present, see if the file has a different size
            If eDateComparisonMode = eDateComparisonModeConstants.CopyIfSizeOrDateDiffers Then

                If objTargetFile.Length <> objSourceFile.Length Then
                    blnNeedToCopy = True
                    strCopyReason = "sizes are different"
                ElseIf objSourceFile.LastWriteTimeUtc <> objTargetFile.LastWriteTimeUtc Then
                    blnNeedToCopy = True
                    strCopyReason = "dates are different"
                End If

            Else

                If objTargetFile.Length <> objSourceFile.Length Then
                    blnNeedToCopy = True
                    strCopyReason = "sizes are different"
                ElseIf objSourceFile.LastWriteTimeUtc > objTargetFile.LastWriteTimeUtc Then
                    blnNeedToCopy = True
                    strCopyReason = "source file is newer"
                End If

                If blnNeedToCopy AndAlso eDateComparisonMode = eDateComparisonModeConstants.RetainNewerTargetIfDifferentSize Then
                    If objTargetFile.LastWriteTimeUtc > objSourceFile.LastWriteTimeUtc Then
                        ' Target file is newer than the source; do not overwrite

                        Dim strWarning = "Warning: Skipping file " & objSourceFile.Name
                        If blnProcessingSubFolder Then
                            strWarning &= " in " & strTargetFolderPath
                        End If
                        strWarning &= " since a newer version exists in the target; source=" & objSourceFile.LastWriteTimeUtc.ToLocalTime() & ", target=" & objTargetFile.LastWriteTimeUtc.ToLocalTime()

                        ShowMessage(strWarning, intDuplicateHoldoffHours:=24)
                        blnNeedToCopy = False
                    End If
                End If

            End If

        End If

        If blnNeedToCopy Then
            CopyFile(objSourceFile, objTargetFile, intFileUpdateCount, strCopyReason)
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
        mRebootCommandFolderPath = String.Empty

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

        Const OPTIONS_SECTION As String = "DMSUpdateManager"

        Dim objSettingsFile As New XmlSettingsFileAccessor

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
                    mRebootCommandFolderPath = objSettingsFile.GetParam(OPTIONS_SECTION, "RebootCommandFolderPath", mRebootCommandFolderPath)

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

            Dim objSourceFolder = New DirectoryInfo(mSourceFolderPath)
            Dim objTargetFolder = New DirectoryInfo(strTargetFolderPath)

            MyBase.mProgressStepDescription = "Updating " & objTargetFolder.Name & ControlChars.NewLine & " using " & objSourceFolder.FullName
            MyBase.ResetProgress()

            blnSuccess = UpdateFolderWork(objSourceFolder.FullName, objTargetFolder.FullName, blnPushNewSubfolders:=False, blnProcessingSubFolder:=False)

            If mCopySubdirectoriesToParentFolder Then
                blnSuccess = UpdateFolderCopyToParent(objTargetFolder, objSourceFolder)
            End If

        Catch ex As Exception
            HandleException("Error in UpdateFolder", ex)
        End Try

        Return blnSuccess

    End Function

    Protected Function UpdateFolderCopyToParent(objTargetFolder As DirectoryInfo, objSourceFolder As DirectoryInfo) As Boolean

        Dim blnSuccess As Boolean

        For Each objSourceSubFolder In objSourceFolder.GetDirectories()

            ' The target folder is treated as a subdirectory of the parent folder
            Dim strTargetSubFolderPath = Path.Combine(objTargetFolder.Parent.FullName, objSourceSubFolder.Name)

            ' Initially assume we'll process the folder if it exists at the target
            Dim blnProcessSubfolder = Directory.Exists(strTargetSubFolderPath)

            If objSourceSubFolder.GetFiles(DELETE_SUBDIR_FLAG).Length > 0 Then
                ' Remove this subfolder at the target (but only if it's empty)
                Dim objTargetSubFolder = New DirectoryInfo(strTargetSubFolderPath)

                If objTargetSubFolder.Exists AndAlso objTargetSubFolder.GetFiles().Length = 0 Then
                    blnProcessSubfolder = False
                    Try
                        objTargetSubFolder.Delete(False)
                        ShowMessage("Deleted parent subfolder " & objTargetSubFolder.FullName)
                    Catch ex As Exception
                        HandleException("Error removing empty parent subfolder flagged with " & DELETE_SUBDIR_FLAG, ex)
                    End Try
                End If
            End If

            If objSourceSubFolder.GetFiles(DELETE_AM_SUBDIR_FLAG).Length > 0 Then
                ' Remove this subfolder if it is present below objTargetFolder (but only if it's empty)

                Dim strAMSubDirPath = Path.Combine(objTargetFolder.FullName, objSourceSubFolder.Name)
                Dim objTargetSubFolder = New DirectoryInfo(strAMSubDirPath)

                If objTargetSubFolder.Exists AndAlso objTargetSubFolder.GetFiles().Length = 0 Then
                    blnProcessSubfolder = False
                    Try
                        objTargetSubFolder.Delete(False)
                        ShowMessage("Deleted subfolder " & objTargetSubFolder.FullName)
                    Catch ex As Exception
                        HandleException("Error removing empty subfolder flagged with " & DELETE_SUBDIR_FLAG, ex)
                    End Try
                End If
            End If

            If objSourceSubFolder.GetFiles(PUSH_AM_SUBDIR_FLAG).Length > 0 Then
                ' Push this folder as a subdirectory of the current folder, not as a subdirectory of the parent folder
                strTargetSubFolderPath = Path.Combine(objTargetFolder.FullName, objSourceSubFolder.Name)
                blnProcessSubfolder = True
            Else
                If objSourceSubFolder.GetFiles(PUSH_DIR_FLAG).Length > 0 Then
                    blnProcessSubfolder = True
                End If
            End If

            If blnProcessSubfolder Then
                blnSuccess = UpdateFolderWork(objSourceSubFolder.FullName, strTargetSubFolderPath, blnPushNewSubfolders:=True, blnProcessingSubFolder:=True)
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

        ' Populate a SortedList object the with the names of any .delete files in fiFilesInSource
        Dim lstDeleteFiles = New List(Of String)(fiFilesInSource.Length)

        For Each objSourceFile As FileInfo In fiFilesInSource
            If objSourceFile.Name.EndsWith(DELETE_SUFFIX) Then
                lstDeleteFiles.Add(TrimSuffix(objSourceFile.Name, DELETE_SUFFIX).ToLower())
            End If
        Next

        For Each objSourceFile As FileInfo In fiFilesInSource

            Dim retryCount = 2
            Dim errorLogged = False

            While retryCount >= 0

                Try
                    Dim strFileNameLCase = objSourceFile.Name.ToLower()
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

                    ' See if file ends with ROLLBACK_SUFFIX
                    If objSourceFile.Name.EndsWith(ROLLBACK_SUFFIX) Then
                        ' This is a Rollback file
                        ' Do not copy this file
                        ' However, do look for a corresponding file that does not have .rollback and copy it if the target file has a different date or size

                        ProcessRollbackFile(objSourceFile, diTargetFolder.FullName, intFileUpdateCount, blnProcessingSubFolder)

                    ElseIf objSourceFile.Name.EndsWith(DELETE_SUFFIX) Then
                        ' This is a Delete file
                        ' Do not copy this file
                        ' However, do look for a corresponding file that does not have .delete and delete that file in the target folder

                        ProcessDeleteFile(objSourceFile, diTargetFolder.FullName)

                    Else
                        ' Make sure a corresponding .Delete file does not exist in fiFilesInSource
                        If Not lstDeleteFiles.Contains(strFileNameLCase) Then

                            If mOverwriteNewerFiles Then
                                eDateComparisonMode = eDateComparisonModeConstants.OverwriteNewerTargetIfDifferentSize
                            Else
                                eDateComparisonMode = eDateComparisonModeConstants.RetainNewerTargetIfDifferentSize
                            End If

                            CopyFileIfNeeded(objSourceFile, diTargetFolder.FullName, intFileUpdateCount, eDateComparisonMode, blnProcessingSubFolder)

                        End If

                    End If

                    Exit While

                Catch ex As Exception
                    If Not errorLogged Then
                        ShowErrorMessage("Error synchronizing " & objSourceFile.Name & ": " & ex.Message, True)
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
        For Each objSourceSubFolder In diSourceFolder.GetDirectories()
            Dim strTargetSubFolderPath = Path.Combine(diTargetFolder.FullName, objSourceSubFolder.Name)

            ' Initially assume we'll process this folder if it exists at the target
            Dim blnProcessSubfolder As Boolean
            blnProcessSubfolder = Directory.Exists(strTargetSubFolderPath)

            If objSourceSubFolder.GetFiles(DELETE_SUBDIR_FLAG).Length > 0 Then
                ' Remove this subfolder (but only if it's empty)
                If objSourceSubFolder.GetFiles().Length = 1 Then
                    blnProcessSubfolder = False
                    Try
                        objSourceSubFolder.Delete(False)
                    Catch ex As Exception
                        HandleException("Error removing empty directory flagged with " & DELETE_SUBDIR_FLAG, ex)
                    End Try
                End If
            End If

            If blnPushNewSubfolders AndAlso objSourceSubFolder.GetFiles(PUSH_DIR_FLAG).Length > 0 Then
                blnProcessSubfolder = True
            End If

            If blnProcessSubfolder Then
                UpdateFolderWork(objSourceSubFolder.FullName, strTargetSubFolderPath, blnPushNewSubfolders, blnProcessingSubFolder)
            End If
        Next

        Return True
    End Function

    Private Sub LogoffOrRebootMachine(eAction As MentalisUtils.RestartOptions, blnForce As Boolean)

        ' Typical values for eAction are:
        ' RestartOptions.LogOff
        ' RestartOptions.PowerOff
        ' RestartOptions.Reboot
        ' RestartOptions.ShutDown
        ' RestartOptions.Suspend
        ' RestartOptions.Hibernate

        MentalisUtils.WindowsController.ExitWindows(eAction, blnForce)

    End Sub

    Private Sub ProcessDeleteFile(ByRef objDeleteFile As FileInfo, strTargetFolderPath As String)
        Dim objTargetFile As FileInfo
        Dim strTargetFilePath As String

        strTargetFilePath = Path.Combine(strTargetFolderPath, TrimSuffix(objDeleteFile.Name, DELETE_SUFFIX))
        objTargetFile = New FileInfo(strTargetFilePath)

        If objTargetFile.Exists() Then
            objTargetFile.Delete()
            ShowMessage("Deleted file " & objTargetFile.FullName)
        End If

        ' Make sure the .delete is also not in the target folder
        strTargetFilePath = Path.Combine(strTargetFolderPath, objDeleteFile.Name)
        objTargetFile = New FileInfo(strTargetFilePath)

        If objTargetFile.Exists() Then
            objTargetFile.Delete()
            ShowMessage("Deleted file " & objTargetFile.FullName)
        End If
    End Sub

    Private Sub ProcessRollbackFile(ByRef objRollbackFile As FileInfo, strTargetFolderPath As String, ByRef intFileUpdateCount As Integer, blnProcessingSubFolder As Boolean)
        Dim objSourceFile As FileInfo
        Dim strSourceFilePath As String
        Dim blnCopied As Boolean

        strSourceFilePath = TrimSuffix(objRollbackFile.FullName, ROLLBACK_SUFFIX)

        objSourceFile = New FileInfo(strSourceFilePath)

        If objSourceFile.Exists() Then
            blnCopied = CopyFileIfNeeded(objSourceFile, strTargetFolderPath, intFileUpdateCount, eDateComparisonModeConstants.CopyIfSizeOrDateDiffers, blnProcessingSubFolder)
            If blnCopied Then
                ShowMessage("Rolled back file " & objSourceFile.Name & " to version from " & objSourceFile.LastWriteTimeUtc.ToLocalTime() & " with size " & (objSourceFile.Length / 1024.0).ToString("0.0") & " KB")
            End If
        Else
            ShowMessage("Warning: Rollback file is present (" + objRollbackFile.Name + ") but expected source file was not found: " & objSourceFile.Name, intDuplicateHoldoffHours:=24)
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
