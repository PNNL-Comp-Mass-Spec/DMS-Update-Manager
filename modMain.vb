Option Strict On

' This program copies new and updated files from a source folder to a target folder
'
' -------------------------------------------------------------------------------
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Program started January 16, 2009
'
' E-mail: matthew.monroe@pnnl.gov or matt@alchemistmatt.com
' Website: http://ncrr.pnnl.gov/ or http://www.sysbio.org/resources/staff/
' -------------------------------------------------------------------------------
' 

Module modMain
	Public Const PROGRAM_DATE As String = "May 18, 2012"

	' Either mSourceFolderPath and mTargetFolderPath must be specified, or mParameterFilePath needs to be specified
	Private mSourceFolderPath As String			' Option A
	Private mTargetFolderPath As String			' Option A
	Private mParameterFilePath As String		' Option B

	Private mLogMessagesToFile As Boolean
	Private mPreviewMode As Boolean

	Private mDMSUpdateManager As clsDMSUpdateManager

	Public Function Main() As Integer
		' Returns 0 if no error, error code if an error

		Dim intReturnCode As Integer
		Dim objParseCommandLine As New clsParseCommandLine
		Dim blnProceed As Boolean

		intReturnCode = 0
		mSourceFolderPath = String.Empty
		mTargetFolderPath = String.Empty
		mParameterFilePath = String.Empty

		mLogMessagesToFile = False
		mPreviewMode = False

		Try
			blnProceed = False
			If objParseCommandLine.ParseCommandLine Then
				If SetOptionsUsingCommandLineParameters(objParseCommandLine) Then blnProceed = True
			End If

			If Not blnProceed OrElse objParseCommandLine.NeedToShowHelp OrElse objParseCommandLine.ParameterCount = 0 OrElse _
			   Not (mSourceFolderPath.Length > 0 And mTargetFolderPath.Length > 0 Or mParameterFilePath.Length > 0) Then
				ShowProgramHelp()
				intReturnCode = -1
			Else
				mDMSUpdateManager = New clsDMSUpdateManager

				With mDMSUpdateManager

					.PreviewMode = mPreviewMode
					.LogMessagesToFile = mLogMessagesToFile

					' Note: These options will get overridden if defined in the parameter file
					.SourceFolderPath = mSourceFolderPath

				End With

				If mDMSUpdateManager.UpdateFolder(mTargetFolderPath, mParameterFilePath) Then
					intReturnCode = 0
				Else
					intReturnCode = mDMSUpdateManager.ErrorCode
					If intReturnCode <> 0 Then
						Console.WriteLine("Error while processing: " & mDMSUpdateManager.GetErrorMessage())
					End If
                End If

                ' Note: This call will only have an effect if the parameter file defined by mParameterFilePath has an entry like this:
                ' <item key="RebootCommandFolderPath" value="\\gigasax\DMS_Programs\AnalysisToolManagerReboot" />
                mDMSUpdateManager.CheckForRebootOrShutdownFile(mPreviewMode)

			End If

		Catch ex As Exception
            ShowErrorMessage("Error occurred in modMain->Main: " & System.Environment.NewLine & ex.Message)
			intReturnCode = -1
		End Try

		Return intReturnCode

	End Function

    Private Function GetAppVersion() As String
        Return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString() & " (" & PROGRAM_DATE & ")"
    End Function

	Private Function SetOptionsUsingCommandLineParameters(ByVal objParseCommandLine As clsParseCommandLine) As Boolean
		' Returns True if no problems; otherwise, returns false

		Dim strValue As String = String.Empty
		Dim strValidParameters() As String = New String() {"S", "T", "P", "L", "V"}

		Try
			' Make sure no invalid parameters are present
			If objParseCommandLine.InvalidParametersPresent(strValidParameters) Then
				Return False
			Else
				With objParseCommandLine
					' Query objParseCommandLine to see if various parameters are present
					If .RetrieveValueForParameter("S", strValue) Then mSourceFolderPath = strValue
					If .RetrieveValueForParameter("T", strValue) Then mTargetFolderPath = strValue
					If .RetrieveValueForParameter("P", strValue) Then mParameterFilePath = strValue
					If .RetrieveValueForParameter("L", strValue) Then mLogMessagesToFile = True
					If .RetrieveValueForParameter("V", strValue) Then mPreviewMode = True
				End With

				Return True
			End If

        Catch ex As Exception
            ShowErrorMessage("Error parsing the command line parameters: " & System.Environment.NewLine & ex.Message)
        End Try

        Return False

	End Function

    Private Sub ShowErrorMessage(ByVal strMessage As String)
        Dim strSeparator As String = "------------------------------------------------------------------------------"

        Console.WriteLine()
        Console.WriteLine(strSeparator)
        Console.WriteLine(strMessage)
        Console.WriteLine(strSeparator)
        Console.WriteLine()

    End Sub

	Private Sub ShowProgramHelp()

		Try

			Console.WriteLine("This program copies new and updated files from a source folder to a target folder.")
			Console.WriteLine()
            Console.WriteLine("Program syntax:" & ControlChars.NewLine & System.IO.Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location))
            Console.WriteLine(" [/S:SourceFolderPath [/T:TargetFolderPath]")
            Console.WriteLine(" [/P:ParameterFilePath] [/L] [/V]")
            Console.WriteLine()
			Console.WriteLine("All files present in the source folder will be copied to the target folder if the file size or file modification time are different.")
			Console.WriteLine("You can either define the source and target folder at the command line, or using the parameter file.  All settings in the parameter file override command line settings.")
			Console.WriteLine()
            Console.WriteLine("Use /L to log details of the updated files.")
            Console.WriteLine("Use /V to preview the files that would be updated.")
            Console.WriteLine()

            Console.WriteLine("Program written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2009")
            Console.WriteLine("Version: " & GetAppVersion())
            Console.WriteLine()

			Console.WriteLine("E-mail: matthew.monroe@pnnl.gov or matt@alchemistmatt.com")
            Console.WriteLine("Website: http://ncrr.pnl.gov/ or http://omics.pnl.gov")
			Console.WriteLine()

            ' Delay for 750 msec in case the user double clicked this file from within Windows Explorer (or started the program via a shortcut)
            System.Threading.Thread.Sleep(750)

		Catch ex As Exception
			ShowErrorMessage("Error displaying the program syntax: " & ex.Message)
		End Try

	End Sub

End Module
