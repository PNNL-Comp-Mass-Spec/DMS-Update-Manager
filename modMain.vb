Option Strict On

' This program copies new and updated files from a source folder to a target folder
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

Module modMain
	Public Const PROGRAM_DATE As String = "January 16, 2009"

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
			End If

		Catch ex As Exception
			Console.WriteLine("Error occurred in modMain->Main: " & ControlChars.NewLine & ex.Message)
			intReturnCode = -1
		End Try

		Return intReturnCode

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
			Console.WriteLine("Error parsing the command line parameters: " & ControlChars.NewLine & ex.Message)
		End Try

	End Function

	Private Sub ShowProgramHelp()

		Try

			Console.WriteLine("This program copies new and updated files from a source folder to a target folder.")
			Console.WriteLine()
			Console.WriteLine("Program syntax:" & ControlChars.NewLine & System.IO.Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location) & _
			   " [/S:SourceFolderPath [/T:TargetFolderPath] [/P:ParameterFilePath] [/L] [/V]")
			Console.WriteLine(" ")
			Console.WriteLine()
			Console.WriteLine("All files present in the source folder will be copied to the target folder if the file size or file modification time are different.")
			Console.WriteLine("You can either define the source and target folder at the command line, or using the parameter file.  All settings in the parameter file override command line settings.")
			Console.WriteLine()
			Console.WriteLine("Use /L to log details of the updated files.  Use /V to preview the files that would be updated.")

			Console.WriteLine("Program written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2009")
			Console.WriteLine()

			Console.WriteLine("This is version " & System.Windows.Forms.Application.ProductVersion & " (" & PROGRAM_DATE & ")")
			Console.WriteLine()

			Console.WriteLine("E-mail: matthew.monroe@pnl.gov or matt@alchemistmatt.com")
			Console.WriteLine("Website: http://ncrr.pnl.gov/ or http://www.sysbio.org/resources/staff/")
			Console.WriteLine()

			Console.WriteLine("Licensed under the Apache License, Version 2.0; you may not use this file except in compliance with the License.  " & _
							  "You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0")
			Console.WriteLine()

			Console.WriteLine("Notice: This computer software was prepared by Battelle Memorial Institute, " & _
							  "hereinafter the Contractor, under Contract No. DE-AC05-76RL0 1830 with the " & _
							  "Department of Energy (DOE).  All rights in the computer software are reserved " & _
							  "by DOE on behalf of the United States Government and the Contractor as " & _
							  "provided in the Contract.  NEITHER THE GOVERNMENT NOR THE CONTRACTOR MAKES ANY " & _
							  "WARRANTY, EXPRESS OR IMPLIED, OR ASSUMES ANY LIABILITY FOR THE USE OF THIS " & _
							  "SOFTWARE.  This notice including this sentence must appear on any copies of " & _
							  "this computer software.")

            ' Delay for 750 msec in case the user double clicked this file from within Windows Explorer (or started the program via a shortcut)
            System.Threading.Thread.Sleep(750)

		Catch ex As Exception
			Console.WriteLine("Error displaying the program syntax: " & ex.Message)
		End Try

	End Sub

End Module
