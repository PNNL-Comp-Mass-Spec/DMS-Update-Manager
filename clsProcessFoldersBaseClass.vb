Option Strict On

' This class can be used as a base class for classes that process a folder or folders
' Note that this class contains simple error codes that
' can be set from any derived classes.  The derived classes can also set their own local error codes
'
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Copyright 2005, Battelle Memorial Institute.  All Rights Reserved.
' Started April 26, 2005

Public MustInherit Class clsProcessFoldersBaseClass

    Public Sub New()
        mFileDate = "January 16, 2009"
        mErrorCode = eProcessFoldersErrorCodes.NoError
        mProgressStepDescription = String.Empty

        mOutputFolderPath = String.Empty
        mLogFolderPath = String.Empty
        mLogFilePath = String.Empty
    End Sub

#Region "Enums and Classwide Variables"
    Public Enum eProcessFoldersErrorCodes
        NoError = 0
        InvalidInputFolderPath = 1
        InvalidOutputFolderPath = 2
        ParameterFileNotFound = 4
        InvalidParameterFile = 8
        FilePathError = 16
        LocalizedError = 32
        UnspecifiedError = -1
    End Enum

	Protected Enum eMessageTypeConstants
		Normal = 0
		ErrorMsg = 1
		Warning = 2
	End Enum

	'' Copy the following to any derived classes
    ''Public Enum eDerivedClassErrorCodes
    ''    NoError = 0
    ''    UnspecifiedError = -1
    ''End Enum

    ''Private mLocalErrorCode As eDerivedClassErrorCodes

    ''Public ReadOnly Property LocalErrorCode() As eDerivedClassErrorCodes
    ''    Get
    ''        Return mLocalErrorCode
    ''    End Get
    ''End Property

    Private mShowMessages As Boolean
    Private mErrorCode As eProcessFoldersErrorCodes

    Protected mFileDate As String
    Protected mAbortProcessing As Boolean

	Protected mLogMessagesToFile As Boolean
	Protected mLogFileUsesDateStamp As Boolean = True

    Protected mLogFilePath As String
    Protected mLogFile As System.IO.StreamWriter

    ' This variable is updated when CleanupFilePaths() is called
    Protected mOutputFolderPath As String
    Protected mLogFolderPath As String          ' If blank, then mOutputFolderPath will be used; if mOutputFolderPath is also blank, then the log is created in the same folder as the executing assembly

    Public Event ProgressReset()
    Public Event ProgressChanged(ByVal taskDescription As String, ByVal percentComplete As Single)     ' PercentComplete ranges from 0 to 100, but can contain decimal percentage values
    Public Event ProgressComplete()

    Protected mProgressStepDescription As String
    Protected mProgressPercentComplete As Single        ' Ranges from 0 to 100, but can contain decimal percentage values

#End Region

#Region "Interface Functions"
    Public Property AbortProcessing() As Boolean
        Get
            Return mAbortProcessing
        End Get
        Set(ByVal Value As Boolean)
            mAbortProcessing = Value
        End Set
    End Property

    Public ReadOnly Property ErrorCode() As eProcessFoldersErrorCodes
        Get
            Return mErrorCode
        End Get
    End Property

    Public ReadOnly Property FileVersion() As String
        Get
            FileVersion = GetVersionForExecutingAssembly()
        End Get
    End Property

    Public ReadOnly Property FileDate() As String
        Get
            FileDate = mFileDate
        End Get
    End Property

    Public ReadOnly Property LogFilePath() As String
        Get
            Return mLogFilePath
        End Get
    End Property

    Public Property LogFolderPath() As String
        Get
            Return mLogFolderPath
        End Get
        Set(ByVal value As String)
            mLogFolderPath = value
        End Set
    End Property

    Public Property LogMessagesToFile() As Boolean
        Get
            Return mLogMessagesToFile
        End Get
        Set(ByVal value As Boolean)
            mLogMessagesToFile = value
        End Set
    End Property

    Public Overridable ReadOnly Property ProgressStepDescription() As String
        Get
            Return mProgressStepDescription
        End Get
    End Property

    ' ProgressPercentComplete ranges from 0 to 100, but can contain decimal percentage values
    Public ReadOnly Property ProgressPercentComplete() As Single
        Get
            Return CType(Math.Round(mProgressPercentComplete, 2), Single)
        End Get
    End Property

    Public Property ShowMessages() As Boolean
        Get
            Return mShowMessages
        End Get
        Set(ByVal Value As Boolean)
            mShowMessages = Value
        End Set
    End Property
#End Region

    Public Overridable Sub AbortProcessingNow()
        mAbortProcessing = True
    End Sub

    Protected Function CleanupFolderPaths(ByRef strInputFolderPath As String, ByRef strOutputFolderPath As String) As Boolean
        ' Validates that strOutputFolderPath and strOutputFolderPath contain valid folder paths
        ' Will ignore strOutputFolderPath if it is Nothing or empty; will create strOutputFolderPath if it does not exist
        '
        ' Returns True if success, False if failure

        Dim ioFolder As System.IO.DirectoryInfo
        Dim blnSuccess As Boolean

        Try
            ' Make sure strInputFolderPath points to a valid folder
            ioFolder = New System.IO.DirectoryInfo(strInputFolderPath)

            If Not ioFolder.Exists() Then
                If Me.ShowMessages Then
                    ShowErrorMessage("Input folder not found: " & strInputFolderPath)
                Else
					LogMessage("Input folder not found: " & strInputFolderPath, eMessageTypeConstants.ErrorMsg)
                End If
                mErrorCode = eProcessFoldersErrorCodes.InvalidInputFolderPath
                blnSuccess = False
            Else
                If strOutputFolderPath Is Nothing OrElse strOutputFolderPath.Length = 0 Then
                    ' Define strOutputFolderPath based on strInputFolderPath
                    strOutputFolderPath = ioFolder.FullName
                End If

                ' Make sure strOutputFolderPath points to a folder
                ioFolder = New System.IO.DirectoryInfo(strOutputFolderPath)

                If Not ioFolder.Exists() Then
                    ' strOutputFolderPath points to a non-existent folder; attempt to create it
                    ioFolder.Create()
                End If

                mOutputFolderPath = String.Copy(strOutputFolderPath)

                blnSuccess = True
            End If

        Catch ex As Exception
            HandleException("Error cleaning up the folder paths", ex)
        End Try

        Return blnSuccess
    End Function

    Protected Function GetBaseClassErrorMessage() As String
        ' Returns String.Empty if no error

        Dim strErrorMessage As String

        Select Case Me.ErrorCode
            Case eProcessFoldersErrorCodes.NoError
                strErrorMessage = String.Empty
            Case eProcessFoldersErrorCodes.InvalidInputFolderPath
                strErrorMessage = "Invalid input folder path"
            Case eProcessFoldersErrorCodes.InvalidOutputFolderPath
                strErrorMessage = "Invalid output folder path"
            Case eProcessFoldersErrorCodes.ParameterFileNotFound
                strErrorMessage = "Parameter file not found"
            Case eProcessFoldersErrorCodes.InvalidParameterFile
                strErrorMessage = "Invalid parameter file"
            Case eProcessFoldersErrorCodes.FilePathError
                strErrorMessage = "General file path error"
            Case eProcessFoldersErrorCodes.LocalizedError
                strErrorMessage = "Localized error"
            Case eProcessFoldersErrorCodes.UnspecifiedError
                strErrorMessage = "Unspecified error"
            Case Else
                ' This shouldn't happen
                strErrorMessage = "Unknown error state"
        End Select

        Return strErrorMessage

    End Function

    Private Function GetVersionForExecutingAssembly() As String

        Dim strVersion As String

        Try
            strVersion = System.Reflection.Assembly.GetExecutingAssembly.GetName.Version.ToString()
        Catch ex As Exception
            strVersion = "??.??.??.??"
        End Try

        Return strVersion

    End Function

    Public MustOverride Function GetErrorMessage() As String

    Protected Sub HandleException(ByVal strBaseMessage As String, ByVal ex As System.Exception)
        If strBaseMessage Is Nothing OrElse strBaseMessage.Length = 0 Then
            strBaseMessage = "Error"
        End If

        If Me.ShowMessages Then
            ' Note that ShowErrorMessage() will call LogMessage()
            ShowErrorMessage(strBaseMessage & ": " & ex.Message, True)
        Else
            LogMessage(strBaseMessage & ": " & ex.Message, eMessageTypeConstants.ErrorMsg)
            Throw New System.Exception(strBaseMessage, ex)
        End If

    End Sub

    Protected Sub LogMessage(ByVal strMessage As String)
        LogMessage(strMessage, eMessageTypeConstants.Normal)
    End Sub

    Protected Sub LogMessage(ByVal strMessage As String, ByVal eMessageType As eMessageTypeConstants)
        ' Note that CleanupFilePaths() will update mOutputFolderPath, which is used here if mLogFolderPath is blank
        ' Thus, be sure to call CleanupFilePaths (or update mLogFolderPath) before the first call to LogMessage

        Dim strMessageType As String
        Dim blnOpeningExistingFile As Boolean = False

        If mLogFile Is Nothing AndAlso mLogMessagesToFile Then
            Try
                mLogFilePath = System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetExecutingAssembly().Location)
				mLogFilePath &= "_log"

				If mLogFileUsesDateStamp Then
					mLogFilePath &= "_" & System.DateTime.Now.ToString("yyyy-MM-dd") & ".txt"
				Else
					mLogFilePath &= ".txt"
				End If


                Try
                    If mLogFolderPath Is Nothing Then mLogFolderPath = String.Empty

                    If mLogFolderPath.Length = 0 Then
                        ' Log folder is undefined; use mOutputFolderPath if it is defined
                        If Not mOutputFolderPath Is Nothing AndAlso mOutputFolderPath.Length > 0 Then
                            mLogFolderPath = String.Copy(mOutputFolderPath)
                        End If
                    End If

                    If mLogFolderPath.Length > 0 Then
                        ' Create the log folder if it doesn't exist
                        If Not System.IO.Directory.Exists(mLogFolderPath) Then
                            System.IO.Directory.CreateDirectory(mLogFolderPath)
                        End If
                    End If
                Catch ex As Exception
                    mLogFolderPath = String.Empty
                End Try

                If mLogFolderPath.Length > 0 Then
                    mLogFilePath = System.IO.Path.Combine(mLogFolderPath, mLogFilePath)
                End If

                blnOpeningExistingFile = System.IO.File.Exists(mLogFilePath)

                mLogFile = New System.IO.StreamWriter(New System.IO.FileStream(mLogFilePath, IO.FileMode.Append, IO.FileAccess.Write, IO.FileShare.Read))
                mLogFile.AutoFlush = True

                If Not blnOpeningExistingFile Then
                    mLogFile.WriteLine("Date" & ControlChars.Tab & _
                                       "Type" & ControlChars.Tab & _
                                       "Message")
                End If

            Catch ex As Exception
                ' Error creating the log file; set mLogMessagesToFile to false so we don't repeatedly try to create it
                mLogMessagesToFile = False
            End Try

        End If

        If Not mLogFile Is Nothing Then
            Select Case eMessageType
                Case eMessageTypeConstants.Normal
                    strMessageType = "Normal"
                Case eMessageTypeConstants.ErrorMsg
                    strMessageType = "Error"
                Case eMessageTypeConstants.Warning
                    strMessageType = "Warning"
                Case Else
                    strMessageType = "Unknown"
            End Select

            mLogFile.WriteLine(System.DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss tt") & ControlChars.Tab & _
                               strMessageType & ControlChars.Tab & _
                               strMessage)
        End If

    End Sub

    Public Function ProcessFoldersWildcard(ByVal strInputFolderPath As String) As Boolean
        Return ProcessFoldersWildcard(strInputFolderPath, String.Empty, String.Empty)
    End Function

    Public Function ProcessFoldersWildcard(ByVal strInputFolderPath As String, ByVal strOutputFolderAlternatePath As String) As Boolean
        Return ProcessFoldersWildcard(strInputFolderPath, strOutputFolderAlternatePath, String.Empty)
    End Function

    Public Function ProcessFoldersWildcard(ByVal strInputFolderPath As String, ByVal strOutputFolderAlternatePath As String, ByVal strParameterFilePath As String) As Boolean
        Return ProcessFoldersWildcard(strInputFolderPath, strOutputFolderAlternatePath, strParameterFilePath, True)
    End Function

    Public Function ProcessFoldersWildcard(ByVal strInputFolderPath As String, ByVal strOutputFolderAlternatePath As String, ByVal strParameterFilePath As String, ByVal blnResetErrorCode As Boolean) As Boolean
        ' Returns True if success, False if failure

        Dim blnSuccess As Boolean
        Dim intMatchCount As Integer

        Dim strCleanPath As String
        Dim strInputFolderToUse As String
        Dim strFolderNameMatchPattern As String

        Dim ioFolderMatch As System.IO.DirectoryInfo
        Dim ioInputFolderInfo As System.IO.DirectoryInfo

        mAbortProcessing = False
        blnSuccess = True
        Try
            ' Possibly reset the error code
            If blnResetErrorCode Then mErrorCode = eProcessFoldersErrorCodes.NoError

			If Not strOutputFolderAlternatePath Is Nothing AndAlso strOutputFolderAlternatePath.Length > 0 Then
				' Update the cached output folder path
				mOutputFolderPath = String.Copy(strOutputFolderAlternatePath)
			End If

            ' See if strInputFolderPath contains a wildcard (* or ?)
            If Not strInputFolderPath Is Nothing AndAlso (strInputFolderPath.IndexOf("*") >= 0 Or strInputFolderPath.IndexOf("?") >= 0) Then
                ' Copy the path into strCleanPath and replace any * or ? characters with _
                strCleanPath = strInputFolderPath.Replace("*", "_")
                strCleanPath = strCleanPath.Replace("?", "_")

                ioInputFolderInfo = New System.IO.DirectoryInfo(strCleanPath)
                If ioInputFolderInfo.Parent.Exists Then
                    strInputFolderToUse = ioInputFolderInfo.Parent.FullName
                Else
                    ' Use the current working directory
                    strInputFolderToUse = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
                End If

                ' Remove any directory information from strInputFolderPath
                strFolderNameMatchPattern = System.IO.Path.GetFileName(strInputFolderPath)

                ' Process any matching folder in this folder
                Try
                    ioInputFolderInfo = New System.IO.DirectoryInfo(strInputFolderToUse)
				Catch ex As Exception
					HandleException("Error in ProcessFoldersWildcard", ex)
					mErrorCode = eProcessFoldersErrorCodes.InvalidInputFolderPath
					Return False
                End Try

                intMatchCount = 0
                For Each ioFolderMatch In ioInputFolderInfo.GetDirectories(strFolderNameMatchPattern)

                    blnSuccess = ProcessFolder(ioFolderMatch.FullName, strOutputFolderAlternatePath, strParameterFilePath, True)

                    If Not blnSuccess Or mAbortProcessing Then Exit For
                    intMatchCount += 1

                    If intMatchCount Mod 1 = 0 Then Console.Write(".")

                Next ioFolderMatch

                If intMatchCount = 0 Then
                    If mErrorCode = eProcessFoldersErrorCodes.NoError Then
                        If Me.ShowMessages Then
                            ShowErrorMessage("No match was found for the input folder path:" & strInputFolderPath)
                        Else
                            LogMessage("No match was found for the input folder path:" & strInputFolderPath, eMessageTypeConstants.ErrorMsg)
                        End If
                    End If
                Else
                    Console.WriteLine()
                End If

            Else
                blnSuccess = ProcessFolder(strInputFolderPath, strOutputFolderAlternatePath, strParameterFilePath, blnResetErrorCode)
            End If

        Catch ex As Exception
            HandleException("Error in ProcessFoldersWildcard", ex)
        End Try

        Return blnSuccess

    End Function

    Public Function ProcessFolder(ByVal strInputFolderPath As String) As Boolean
        Return ProcessFolder(strInputFolderPath, String.Empty, String.Empty, True)
    End Function

    Public Function ProcessFolder(ByVal strInputFolderPath As String, ByVal strOutputFolderAlternatePath As String, ByVal strParameterFilePath As String) As Boolean
        Return ProcessFolder(strInputFolderPath, strOutputFolderAlternatePath, strParameterFilePath, True)
    End Function

    Public MustOverride Function ProcessFolder(ByVal strInputFolderPath As String, ByVal strOutputFolderAlternatePath As String, ByVal strParameterFilePath As String, ByVal blnResetErrorCode As Boolean) As Boolean


    Public Function ProcessAndRecurseFolders(ByVal strInputFolderPath As String) As Boolean
        Return ProcessAndRecurseFolders(strInputFolderPath, String.Empty)
    End Function

    Public Function ProcessAndRecurseFolders(ByVal strInputFolderPath As String, ByVal intRecurseFoldersMaxLevels As Integer) As Boolean
        Return ProcessAndRecurseFolders(strInputFolderPath, String.Empty, String.Empty, intRecurseFoldersMaxLevels)
    End Function

    Public Function ProcessAndRecurseFolders(ByVal strInputFolderPath As String, ByVal strOutputFolderAlternatePath As String) As Boolean
        Return ProcessAndRecurseFolders(strInputFolderPath, strOutputFolderAlternatePath, String.Empty)
    End Function

    Public Function ProcessAndRecurseFolders(ByVal strInputFolderPath As String, ByVal strOutputFolderAlternatePath As String, ByVal strParameterFilePath As String) As Boolean
        Return ProcessAndRecurseFolders(strInputFolderPath, strOutputFolderAlternatePath, strParameterFilePath, 0)
    End Function

    Public Function ProcessAndRecurseFolders(ByVal strInputFolderPath As String, ByVal strOutputFolderAlternatePath As String, ByVal strParameterFilePath As String, ByVal intRecurseFoldersMaxLevels As Integer) As Boolean
        ' Calls ProcessFolders for all matching folders in strInputFolderPath 
        ' If intRecurseFoldersMaxLevels is <=0 then we recurse infinitely

        Dim strCleanPath As String
        Dim strInputFolderToUse As String
        Dim strFolderNameMatchPattern As String

        Dim ioFolderInfo As System.IO.DirectoryInfo

        Dim blnSuccess As Boolean
        Dim intFolderProcessCount, intFolderProcessFailCount As Integer

        ' Examine strInputFolderPath to see if it contains a * or ?
        Try
            If Not strInputFolderPath Is Nothing AndAlso (strInputFolderPath.IndexOf("*") >= 0 Or strInputFolderPath.IndexOf("?") >= 0) Then
                ' Copy the path into strCleanPath and replace any * or ? characters with _
                strCleanPath = strInputFolderPath.Replace("*", "_")
                strCleanPath = strCleanPath.Replace("?", "_")

                ioFolderInfo = New System.IO.DirectoryInfo(strCleanPath)
                If ioFolderInfo.Parent.Exists Then
                    strInputFolderToUse = ioFolderInfo.Parent.FullName
                Else
                    ' Use the current working directory
                    strInputFolderToUse = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
                End If

                ' Remove any directory information from strInputFolderPath
                strFolderNameMatchPattern = System.IO.Path.GetFileName(strInputFolderPath)

            Else
                ioFolderInfo = New System.IO.DirectoryInfo(strInputFolderPath)
                If ioFolderInfo.Exists Then
                    strInputFolderToUse = ioFolderInfo.FullName
                Else
                    ' Use the current working directory
                    strInputFolderToUse = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
                End If
                strFolderNameMatchPattern = "*"
            End If

            If Not strInputFolderToUse Is Nothing AndAlso strInputFolderToUse.Length > 0 Then

                ' Validate the output folder path
                If Not strOutputFolderAlternatePath Is Nothing AndAlso strOutputFolderAlternatePath.Length > 0 Then
                    Try
                        ioFolderInfo = New System.IO.DirectoryInfo(strOutputFolderAlternatePath)
                        If Not ioFolderInfo.Exists Then ioFolderInfo.Create()
					Catch ex As Exception
						HandleException("Error in ProcessAndRecurseFolders", ex)
						mErrorCode = clsProcessFoldersBaseClass.eProcessFoldersErrorCodes.InvalidOutputFolderPath
						Return False
                    End Try
                End If

                ' Initialize some parameters
                mAbortProcessing = False
                intFolderProcessCount = 0
                intFolderProcessFailCount = 0

                ' Call RecurseFoldersWork
                blnSuccess = RecurseFoldersWork(strInputFolderToUse, strFolderNameMatchPattern, strParameterFilePath, strOutputFolderAlternatePath, intFolderProcessCount, intFolderProcessFailCount, 1, intRecurseFoldersMaxLevels)

            Else
                mErrorCode = clsProcessFoldersBaseClass.eProcessFoldersErrorCodes.InvalidInputFolderPath
                Return False
            End If

        Catch ex As Exception
            HandleException("Error in ProcessAndRecurseFolders", ex)
            blnSuccess = False
        End Try

        Return blnSuccess

    End Function

    Private Function RecurseFoldersWork(ByVal strInputFolderPath As String, ByVal strFolderNameMatchPattern As String, ByVal strParameterFilePath As String, ByVal strOutputFolderAlternatePath As String, ByRef intFolderProcessCount As Integer, ByRef intFolderProcessFailCount As Integer, ByVal intRecursionLevel As Integer, ByVal intRecurseFoldersMaxLevels As Integer) As Boolean
        ' If intRecurseFoldersMaxLevels is <=0 then we recurse infinitely

        Dim ioInputFolderInfo As System.IO.DirectoryInfo
        Dim ioSubFolderInfo As System.IO.DirectoryInfo
        Dim ioFolderMatch As System.io.DirectoryInfo

        Dim strOutputFolderPathToUse As String
        Dim blnSuccess As Boolean

        Try
            ioInputFolderInfo = New System.IO.DirectoryInfo(strInputFolderPath)
        Catch ex As Exception
            ' Input folder path error
            HandleException("Error in RecurseFoldersWork", ex)
            mErrorCode = eProcessFoldersErrorCodes.InvalidInputFolderPath
            Return False
        End Try

        Try
            If Not strOutputFolderAlternatePath Is Nothing AndAlso strOutputFolderAlternatePath.Length > 0 Then
                strOutputFolderAlternatePath = System.IO.Path.Combine(strOutputFolderAlternatePath, ioInputFolderInfo.Name)
                strOutputFolderPathToUse = String.Copy(strOutputFolderAlternatePath)
            Else
                strOutputFolderPathToUse = String.Empty
            End If
        Catch ex As Exception
            ' Output file path error
            HandleException("Error in RecurseFoldersWork", ex)
            mErrorCode = eProcessFoldersErrorCodes.InvalidOutputFolderPath
            Return False
        End Try

        Try
            Console.WriteLine("Examining " & strInputFolderPath)

            If intRecursionLevel = 1 And strFolderNameMatchPattern = "*" Then
                ' Need to process the current folder
                blnSuccess = ProcessFolder(ioInputFolderInfo.FullName, strOutputFolderPathToUse, strParameterFilePath, True)
                If Not blnSuccess Then
                    intFolderProcessFailCount += 1
                    blnSuccess = True
                Else
                    intFolderProcessCount += 1
                End If
            End If

            ' Process any matching folder in this folder
            blnSuccess = True
            For Each ioFolderMatch In ioInputFolderInfo.GetDirectories(strFolderNameMatchPattern)
                If mAbortProcessing Then Exit For

                If strOutputFolderPathToUse.Length > 0 Then
                    blnSuccess = ProcessFolder(ioFolderMatch.FullName, System.IO.Path.Combine(strOutputFolderPathToUse, ioFolderMatch.Name), strParameterFilePath, True)
                Else
                    blnSuccess = ProcessFolder(ioFolderMatch.FullName, String.Empty, strParameterFilePath, True)
                End If

                If Not blnSuccess Then
                    intFolderProcessFailCount += 1
                    blnSuccess = True
                Else
                    intFolderProcessCount += 1
                End If

            Next ioFolderMatch

        Catch ex As Exception
            HandleException("Error in RecurseFoldersWork", ex)
            mErrorCode = eProcessFoldersErrorCodes.InvalidInputFolderPath
            Return False
        End Try

        If Not mAbortProcessing Then
            ' If intRecurseFoldersMaxLevels is <=0 then we recurse infinitely
            '  otherwise, compare intRecursionLevel to intRecurseFoldersMaxLevels
            If intRecurseFoldersMaxLevels <= 0 OrElse intRecursionLevel <= intRecurseFoldersMaxLevels Then
                ' Call this function for each of the subfolders of ioInputFolderInfo
                For Each ioSubFolderInfo In ioInputFolderInfo.GetDirectories()
                    blnSuccess = RecurseFoldersWork(ioSubFolderInfo.FullName, strFolderNameMatchPattern, strParameterFilePath, strOutputFolderAlternatePath, intFolderProcessCount, intFolderProcessFailCount, intRecursionLevel + 1, intRecurseFoldersMaxLevels)
                    If Not blnSuccess Then Exit For
                Next ioSubFolderInfo
            End If
        End If

        Return blnSuccess

    End Function

    Protected Sub ResetProgress()
        RaiseEvent ProgressReset()
    End Sub

    Protected Sub ResetProgress(ByVal strProgressStepDescription As String)
        UpdateProgress(strProgressStepDescription, 0)
        RaiseEvent ProgressReset()
    End Sub

    Protected Sub SetBaseClassErrorCode(ByVal eNewErrorCode As eProcessFoldersErrorCodes)
        mErrorCode = eNewErrorCode
    End Sub

    Protected Sub ShowErrorMessage(ByVal strMessage As String)
        ShowErrorMessage(strMessage, True)
    End Sub

    Protected Sub ShowErrorMessage(ByVal strMessage As String, ByVal blnAllowLogToFile As Boolean)
        Dim strSeparator As String = "------------------------------------------------------------------------------"

        Console.WriteLine()
        Console.WriteLine(strSeparator)
        Console.WriteLine(strMessage)
        Console.WriteLine(strSeparator)
        Console.WriteLine()

        If blnAllowLogToFile Then
            LogMessage(strMessage, eMessageTypeConstants.ErrorMsg)
        End If

    End Sub

    Protected Sub ShowMessage(ByVal strMessage As String)
        ShowMessage(strMessage, True, False)
    End Sub

    Protected Sub ShowMessage(ByVal strMessage As String, ByVal blnAllowLogToFile As Boolean)
        ShowMessage(strMessage, blnAllowLogToFile, False)
    End Sub

    Protected Sub ShowMessage(ByVal strMessage As String, ByVal blnAllowLogToFile As Boolean, ByVal blnPrecedeWithNewline As Boolean)

        If blnPrecedeWithNewline Then
            Console.WriteLine()
        End If
        Console.WriteLine(strMessage)

        If blnAllowLogToFile Then
            LogMessage(strMessage, eMessageTypeConstants.Normal)
        End If

    End Sub

    Protected Sub UpdateProgress(ByVal strProgressStepDescription As String)
        UpdateProgress(strProgressStepDescription, mProgressPercentComplete)
    End Sub

    Protected Sub UpdateProgress(ByVal sngPercentComplete As Single)
        UpdateProgress(Me.ProgressStepDescription, sngPercentComplete)
    End Sub

    Protected Sub UpdateProgress(ByVal strProgressStepDescription As String, ByVal sngPercentComplete As Single)
        Dim blnDescriptionChanged As Boolean = False

        If strProgressStepDescription <> mProgressStepDescription Then
            blnDescriptionChanged = True
        End If

        mProgressStepDescription = String.Copy(strProgressStepDescription)
        If sngPercentComplete < 0 Then
            sngPercentComplete = 0
        ElseIf sngPercentComplete > 100 Then
            sngPercentComplete = 100
        End If
        mProgressPercentComplete = sngPercentComplete

        If blnDescriptionChanged Then
            If mProgressPercentComplete = 0 Then
                LogMessage(mProgressStepDescription)
            Else
                LogMessage(mProgressStepDescription & " (" & mProgressPercentComplete.ToString("0.0") & "% complete)")
            End If
        End If

        RaiseEvent ProgressChanged(Me.ProgressStepDescription, Me.ProgressPercentComplete)
    End Sub

    Protected Sub OperationComplete()
        RaiseEvent ProgressComplete()
    End Sub

    '' The following functions should be placed in any derived class
    '' Cannot define as MustOverride since it contains a customized enumerated type (eDerivedClassErrorCodes) in the function declaration

    ''Private Sub SetLocalErrorCode(ByVal eNewErrorCode As eDerivedClassErrorCodes)
    ''    SetLocalErrorCode(eNewErrorCode, False)
    ''End Sub

    ''Private Sub SetLocalErrorCode(ByVal eNewErrorCode As eDerivedClassErrorCodes, ByVal blnLeaveExistingErrorCodeUnchanged As Boolean)
    ''    If blnLeaveExistingErrorCodeUnchanged AndAlso mLocalErrorCode <> eDerivedClassErrorCodes.NoError Then
    ''        ' An error code is already defined; do not change it
    ''    Else
    ''        mLocalErrorCode = eNewErrorCode

    ''        If eNewErrorCode = eDerivedClassErrorCodes.NoError Then
    ''            If MyBase.ErrorCode = Me.eProcessFoldersErrorCodes.LocalizedError Then
    ''                MyBase.SetBaseClassErrorCode(Me.eProcessFoldersErrorCodes.NoError)
    ''            End If
    ''        Else
    ''            MyBase.SetBaseClassErrorCode(clsProcessFoldersBaseClass.eProcessFoldersErrorCodes.LocalizedError)
    ''        End If
    ''    End If

    ''End Sub

End Class
