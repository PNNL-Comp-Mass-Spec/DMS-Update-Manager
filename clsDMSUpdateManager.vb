﻿Option Strict On

' This program copies new and updated files from a source folder (master file folder) 
' to a target folder
'
' -------------------------------------------------------------------------------
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Program started January 16, 2009
'
' E-mail: matthew.monroe@pnl.gov or matt@alchemistmatt.com
' Website: http://ncrr.pnl.gov/ or http://www.sysbio.org/resources/staff/
' -------------------------------------------------------------------------------
' 
' Licensed under the Apache License, Version 2.0; you may not use this file except
' in compliance with the License.  You may obtain a copy of the License at 
' http://www.apache.org/licenses/LICENSE-2.0
'
' Notice: This computer software was prepared by Battelle Memorial Institute, 
' hereinafter the Contractor, under Contract No. DE-AC05-76RL0 1830 with the 
' Department of Energy (DOE).  All rights in the computer software are reserved 
' by DOE on behalf of the United States Government and the Contractor as 
' provided in the Contract.  NEITHER THE GOVERNMENT NOR THE CONTRACTOR MAKES ANY 
' WARRANTY, EXPRESS OR IMPLIED, OR ASSUMES ANY LIABILITY FOR THE USE OF THIS 
' SOFTWARE.  This notice including this sentence must appear on any copies of 
' this computer software.

Public Class clsDMSUpdateManager
	Inherits clsProcessFoldersBaseClass

	Public Sub New()
        MyBase.mFileDate = "February 1, 2010"
		InitializeLocalVariables()
	End Sub


#Region "Constants and Enums"

	' Error codes specialized for this class
	Public Enum eDMSUpdateManagerErrorCodes As Integer
		NoError = 0
		UnspecifiedError = -1
	End Enum

#End Region

#Region "Structures"

#End Region

#Region "Classwide Variables"

	' When true, then messages will be displayed and logged showing the files that would be copied
	Protected mPreviewMode As Boolean

	' If False, then will not overwrite files in the target folder that are newer than files in the source folder
	Protected mOverwriteNewerFiles As Boolean

    ' When True, then will copy any subdirectories of the source folder into a subdirectory off the parent folder of the target folder
    ' For example:
    '   The .Exe resides at folder C:\DMS_Programs\AnalysisToolManager\DMSUpdateManager.exe
    '   mSourceFolderPath = "\\gigasax\DMS_Programs\AnalysisToolManagerDistribution"
    '   mTargetFolderPath = "."
    '   Files are synced from "\\gigasax\DMS_Programs\AnalysisToolManagerDistribution" to "C:\DMS_Programs\AnalysisToolManager\"
    '   Next, folder \\gigasax\DMS_Programs\AnalysisToolManagerDistribution\MASIC\ will get sync'd with ..\MASIC (but only if ..\MASIC exists)
    '     Note that ..\MASIC is actually C:\DMS_Programs\MASIC\ 
    '   When sync'ing the MASIC folders, will recursively sync additional folders that match

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
        Set(ByVal value As Boolean)
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
		Set(ByVal value As Boolean)
			mOverwriteNewerFiles = value
		End Set
	End Property

	Public Property PreviewMode() As Boolean
		Get
			Return mPreviewMode
		End Get
		Set(ByVal value As Boolean)
			mPreviewMode = value
		End Set
	End Property

    Public Property RebootCommandFolderPath() As String
        Get
            Return mRebootCommandFolderPath
        End Get
        Set(ByVal value As String)
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
		Set(ByVal value As String)
			If Not value Is Nothing Then
				mSourceFolderPath = value
			End If
		End Set
	End Property

#End Region

	Public Sub AddFileToIgnore(ByVal strFileName As String)

		If Not strFileName Is Nothing AndAlso strFileName.Length > 0 Then
			If mFilesToIgnoreCount >= mFilesToIgnore.Length Then
				ReDim Preserve mFilesToIgnore(mFilesToIgnore.Length * 2 - 1)
			End If

			mFilesToIgnore(mFilesToIgnoreCount) = strFileName
			mFilesToIgnoreCount += 1
		End If

	End Sub

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

		If MyBase.ErrorCode = clsProcessFoldersBaseClass.eProcessFoldersErrorCodes.LocalizedError Or _
		   MyBase.ErrorCode = clsProcessFoldersBaseClass.eProcessFoldersErrorCodes.NoError Then
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

	Private Function LoadParameterFileSettings(ByVal strParameterFilePath As String) As Boolean

		Const OPTIONS_SECTION As String = "DMSUpdateManager"

		Dim objSettingsFile As New XmlSettingsFileAccessor

		Dim strFilesToIgnore As String
		Dim strIgnoreList() As String

		Try

			If strParameterFilePath Is Nothing OrElse strParameterFilePath.Length = 0 Then
				' No parameter file specified; nothing to load
				Return True
			End If

			If Not System.IO.File.Exists(strParameterFilePath) Then
				' See if strParameterFilePath points to a file in the same directory as the application
				strParameterFilePath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), System.IO.Path.GetFileName(strParameterFilePath))
				If Not System.IO.File.Exists(strParameterFilePath) Then
					MyBase.SetBaseClassErrorCode(clsProcessFoldersBaseClass.eProcessFoldersErrorCodes.ParameterFileNotFound)
					Return False
				End If
			End If

			If objSettingsFile.LoadSettings(strParameterFilePath) Then
				If Not objSettingsFile.SectionPresent(OPTIONS_SECTION) Then
					ShowErrorMessage("The node '<section name=""" & OPTIONS_SECTION & """> was not found in the parameter file: " & strParameterFilePath)
					MyBase.SetBaseClassErrorCode(clsProcessFoldersBaseClass.eProcessFoldersErrorCodes.InvalidParameterFile)
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
								AddFileToIgnore(strFile)
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

	Public Overloads Overrides Function ProcessFolder(ByVal strInputFolderPath As String, ByVal strOutputFolderAlternatePath As String, ByVal strParameterFilePath As String, ByVal blnResetErrorCode As Boolean) As Boolean
		' Returns True if success, False if failure
		' Note: strOutputFolderAlternatePath is ignored in this function

		Return UpdateFolder(strInputFolderPath, strParameterFilePath)
	End Function

	Public Function UpdateFolder(ByVal strTargetFolderPath As String, ByVal strParameterFilePath As String) As Boolean
		' Returns True if success, False if failure

		Dim blnSuccess As Boolean
        Dim objSourceFolder As System.IO.DirectoryInfo
        Dim objTargetFolder As System.IO.DirectoryInfo
        Dim objSourceSubFolder As System.IO.DirectoryInfo

        Dim strTargetSubFolderPath As String

		SetLocalErrorCode(eDMSUpdateManagerErrorCodes.NoError)

		If Not strTargetFolderPath Is Nothing AndAlso strTargetFolderPath.Length > 0 Then
			' Update mTargetFolderPath using strTargetFolderPath
			' Note: If TargetFolder is defined in the parameter file, then this value will get overridden
			mTargetFolderPath = String.Copy(strTargetFolderPath)
		End If

		If Not LoadParameterFileSettings(strParameterFilePath) Then
			ShowErrorMessage("Parameter file load error: " & strParameterFilePath)

			If MyBase.ErrorCode = clsProcessFoldersBaseClass.eProcessFoldersErrorCodes.NoError Then
				MyBase.SetBaseClassErrorCode(clsProcessFoldersBaseClass.eProcessFoldersErrorCodes.InvalidParameterFile)
			End If
			Return False
		End If

		Try
			strTargetFolderPath = String.Copy(mTargetFolderPath)

			If mSourceFolderPath Is Nothing OrElse mSourceFolderPath.Length = 0 Then
				ShowMessage("Source folder path is not defined.  Either specify it at the command line or include it in the parameter file.")
				MyBase.SetBaseClassErrorCode(clsProcessFoldersBaseClass.eProcessFoldersErrorCodes.InvalidInputFolderPath)
				Return False
			End If

			If strTargetFolderPath Is Nothing OrElse strTargetFolderPath.Length = 0 Then
				ShowMessage("Target folder path is not defined.  Either specify it at the command line or include it in the parameter file.")
				MyBase.SetBaseClassErrorCode(clsProcessFoldersBaseClass.eProcessFoldersErrorCodes.InvalidInputFolderPath)
				Return False
			End If

			' Note that CleanupFilePaths() will update mOutputFolderPath, which is used by LogMessage()
			If Not CleanupFolderPaths(strTargetFolderPath, String.Empty) Then
				MyBase.SetBaseClassErrorCode(clsProcessFoldersBaseClass.eProcessFoldersErrorCodes.FilePathError)
            Else
                objSourceFolder = New System.IO.DirectoryInfo(mSourceFolderPath)
                objTargetFolder = New System.IO.DirectoryInfo(strTargetFolderPath)
                strTargetFolderPath = objTargetFolder.FullName

                MyBase.mProgressStepDescription = "Updating " & objTargetFolder.Name & ControlChars.NewLine & " using " & objSourceFolder.FullName
                MyBase.ResetProgress()

                blnSuccess = UpdateFolderWork(objSourceFolder.FullName, objTargetFolder.FullName)

                If mCopySubdirectoriesToParentFolder Then

                    For Each objSourceSubFolder In objSourceFolder.GetDirectories()

                        strTargetSubFolderPath = System.IO.Path.Combine(objTargetFolder.Parent.FullName, objSourceSubFolder.Name)

                        If System.IO.Directory.Exists(strTargetSubFolderPath) Then
                            blnSuccess = UpdateFolderWork(objSourceSubFolder.FullName, strTargetSubFolderPath)
                        End If
                    Next

                End If
			End If
		Catch ex As Exception
			HandleException("Error in UpdateFolder", ex)
		End Try

		Return blnSuccess

	End Function

    Protected Function UpdateFolderWork(ByVal strSourceFolderPath As String, ByVal strTargetFolderPath As String) As Boolean

        Dim strStatusMessage As String

        Dim objSourceFolder As System.IO.DirectoryInfo
        Dim objSourceFile As System.IO.FileInfo
        Dim objTargetFile As System.IO.FileInfo
        Dim objCopiedFile As System.IO.FileInfo

        Dim objSourceSubFolder As System.IO.DirectoryInfo
        Dim strTargetSubFolderPath As String
        Dim blnSuccess As Boolean

        Dim intIndex As Integer
        Dim strFileNameLCase As String
        Dim strTargetFilePath As String
        Dim strCopyReason As String

        Dim blnSkipFile As Boolean
        Dim blnNeedToCopy As Boolean
        Dim intFileUpdateCount As Integer

        MyBase.mProgressStepDescription = "Updating " & System.IO.Path.GetDirectoryName(strTargetFolderPath) & ControlChars.NewLine & " using " & strSourceFolderPath
        ShowMessage(MyBase.mProgressStepDescription, False)
        
        ' Obtain a list of files in the source folder
        objSourceFolder = New System.IO.DirectoryInfo(strSourceFolderPath)

        intFileUpdateCount = 0

        For Each objSourceFile In objSourceFolder.GetFiles()

            Try
                strFileNameLCase = objSourceFile.Name.ToLower

                ' Make sure this file is not in mFilesToIgnore
                For intIndex = 0 To mFilesToIgnoreCount - 1
                    If strFileNameLCase = mFilesToIgnore(intIndex).ToLower Then
                        ' Skip file
                        blnSkipFile = True
                        Exit For
                    End If
                Next intIndex

                If Not blnSkipFile Then
                    strTargetFilePath = System.IO.Path.Combine(strTargetFolderPath, objSourceFile.Name)
                    objTargetFile = New System.IO.FileInfo(strTargetFilePath)

                    strCopyReason = String.Empty
                    blnNeedToCopy = False

                    If Not objTargetFile.Exists Then
                        ' File not present in the target; copy it now
                        strCopyReason = "not found in target folder"
                        blnNeedToCopy = True
                    Else
                        ' File is present, see if the file has a different size
                        If objTargetFile.Length <> objSourceFile.Length Then
                            blnNeedToCopy = True
                            strCopyReason = "sizes are different"
                        ElseIf objSourceFile.LastWriteTime > objTargetFile.LastWriteTime Then
                            blnNeedToCopy = True
                            strCopyReason = "source file is newer"
                        End If

                        If blnNeedToCopy AndAlso Not mOverwriteNewerFiles Then
                            If objTargetFile.LastWriteTime > objSourceFile.LastWriteTime Then
                                ' Target file is newer than the source; do not overwrite
                                ShowMessage("Warning: Skipping file " & objSourceFile.Name & " since a newer version exists in the target; source=" & objSourceFile.LastWriteTime & ", target=" & objTargetFile.LastWriteTime)
                                blnNeedToCopy = False
                            End If
                        End If
                    End If

                    If blnNeedToCopy Then
                        If mPreviewMode Then
                            ShowMessage("Preview: Update file: " & objSourceFile.Name & "; " & strCopyReason, False)
                        Else
                            ShowMessage("Update file: " & objSourceFile.Name & "; " & strCopyReason)

                            Try
                                objCopiedFile = objSourceFile.CopyTo(objTargetFile.FullName, True)

                                If objCopiedFile.Length <> objSourceFile.Length Then
                                    ShowErrorMessage("Copy of " & objSourceFile.Name & " failed; sizes differ", True)
                                ElseIf objCopiedFile.LastWriteTime <> objSourceFile.LastWriteTime Then
                                    ShowErrorMessage("Copy of " & objSourceFile.Name & " failed; modification times differ", True)
                                Else
                                    intFileUpdateCount += 1
                                End If

                            Catch ex As Exception
                                ShowErrorMessage("Error copying " & objSourceFile.Name & ": " & ex.Message, True)
                            End Try

                        End If
                    End If
                End If

            Catch ex As Exception
                ShowErrorMessage("Error synchronizing " & objSourceFile.Name & ": " & ex.Message, True)
            End Try

        Next

        If intFileUpdateCount > 0 Then
            strStatusMessage = "Updated " & intFileUpdateCount & " file"
            If intFileUpdateCount > 1 Then strStatusMessage &= "s"

            strStatusMessage &= " using " & objSourceFolder.FullName & "\"

            ShowMessage(strStatusMessage, True, False)
        End If

        ' Process each subdirectory in the source folder
        ' If the folder exists at the target, then copy it
        For Each objSourceSubFolder In objSourceFolder.GetDirectories()
            strTargetSubFolderPath = System.IO.Path.Combine(strTargetFolderPath, objSourceSubFolder.Name)

            If System.IO.Directory.Exists(strTargetSubFolderPath) Then
                blnSuccess = UpdateFolderWork(objSourceSubFolder.FullName, strTargetSubFolderPath)
            End If
        Next

        Return True
    End Function

    Public Sub CheckForRebootOrShutdownFile(ByVal blnPreview As Boolean)
        CheckForRebootOrShutdownFile(mRebootCommandFolderPath, blnPreview)
    End Sub

    ''' <summary>
    ''' Look for a file named MachineName_RebootNow.txt or MachineName_ShutdownNow.txt in strSourceFolder
    ''' If strSourceFolder is empty, then does not do anything
    ''' </summary>
    ''' <param name="strSourceFolder"></param>
    ''' <remarks></remarks>
    Public Sub CheckForRebootOrShutdownFile(ByVal strSourceFolder As String, ByVal blnPreview As Boolean)

        Dim blnMatchFound As Boolean

        If strSourceFolder Is Nothing OrElse strSourceFolder.Length = 0 Then
            If blnPreview Then
                Console.WriteLine("SourceFolder to look for the RebootNow.txt file is empty; will not look for the file")
            End If
        Else
            blnMatchFound = CheckForRebootOrShutdownFileWork(strSourceFolder, "_RebootNow.txt", MentalisUtils.RestartOptions.Reboot, blnPreview)

            If Not blnMatchFound Then
                blnMatchFound = CheckForRebootOrShutdownFileWork(strSourceFolder, "_ShutdownNow.txt", MentalisUtils.RestartOptions.ShutDown, blnPreview)
            End If
        End If

    End Sub

    Protected Function CheckForRebootOrShutdownFileWork(ByVal strSourceFolder As String, _
                                                        ByVal strTargetFileSuffix As String, _
                                                        ByVal eAction As MentalisUtils.RestartOptions, _
                                                        ByVal blnPreview As Boolean) As Boolean

        Dim ioFileInfo As System.IO.FileInfo
        Dim strFilePathToFind As String
        Dim strNewPath As String
        Dim strComputer As String

        Dim blnMatchFound As Boolean = False

        Try
            strComputer = System.Environment.MachineName.ToString()
            strFilePathToFind = System.IO.Path.Combine(strSourceFolder, strComputer & strTargetFileSuffix)

            Console.WriteLine("Looking for " & strFilePathToFind)
            ioFileInfo = New System.IO.FileInfo(strFilePathToFind)

            If ioFileInfo.Exists Then
                Console.WriteLine("Found file: " & strFilePathToFind)

                If Not blnPreview Then
                    strNewPath = strFilePathToFind & ".done"

                    Try
                        If System.IO.File.Exists(strNewPath) Then
                            System.IO.File.Delete(strNewPath)
                            System.Threading.Thread.Sleep(500)
                        End If
                    Catch ex As Exception
                        Console.WriteLine("Error deleting file " & strNewPath & ": " & ex.Message)
                    End Try

                    Try
                        ioFileInfo.MoveTo(strNewPath)
                        System.Threading.Thread.Sleep(500)
                    Catch ex As Exception
                        Console.WriteLine("Error renaming " & System.IO.Path.GetFileName(strFilePathToFind) & " to " & System.IO.Path.GetFileName(strNewPath) & ": " & ex.Message)
                    End Try

                End If

                If blnPreview Then
                    Console.WriteLine("Preview: Call LogoffOrRebootMachine with command " & eAction.ToString)
                Else
                    LogoffOrRebootMachine(eAction, True)
                End If

            End If

        Catch ex As Exception
            Console.WriteLine("Error in CheckForRebootOrShutdownFileWork: " & ex.Message)

        End Try
        
    End Function

    Private Sub LogoffOrRebootMachine(ByVal eAction As MentalisUtils.RestartOptions, ByVal blnForce As Boolean)

        ' Typical values for eAction are:
        ' RestartOptions.LogOff
        ' RestartOptions.PowerOff
        ' RestartOptions.Reboot
        ' RestartOptions.ShutDown
        ' RestartOptions.Suspend
        ' RestartOptions.Hibernate

        MentalisUtils.WindowsController.ExitWindows(eAction, blnForce)

    End Sub

    Private Sub SetLocalErrorCode(ByVal eNewErrorCode As eDMSUpdateManagerErrorCodes)
        SetLocalErrorCode(eNewErrorCode, False)
    End Sub

    Private Sub SetLocalErrorCode(ByVal eNewErrorCode As eDMSUpdateManagerErrorCodes, ByVal blnLeaveExistingErrorCodeUnchanged As Boolean)

        If blnLeaveExistingErrorCodeUnchanged AndAlso mLocalErrorCode <> eDMSUpdateManagerErrorCodes.NoError Then
            ' An error code is already defined; do not change it
        Else
            mLocalErrorCode = eNewErrorCode

            If eNewErrorCode = eDMSUpdateManagerErrorCodes.NoError Then
                If MyBase.ErrorCode = clsProcessFoldersBaseClass.eProcessFoldersErrorCodes.LocalizedError Then
                    MyBase.SetBaseClassErrorCode(clsProcessFoldersBaseClass.eProcessFoldersErrorCodes.NoError)
                End If
            Else
                MyBase.SetBaseClassErrorCode(clsProcessFoldersBaseClass.eProcessFoldersErrorCodes.LocalizedError)
            End If
        End If

    End Sub
End Class
