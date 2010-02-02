Option Explicit On 
Option Strict On

Imports Org.Mentalis.Utilities
Imports Microsoft.VisualBasic
Imports System

Public Class TestApp
	Public Shared Sub Main()
		Console.WriteLine("Restart Program\r\n--------------\r\n\r\n Select the restart mode:\r\n   l - LogOff\r\n   p - PowerOff\r\n   r - Reboot\r\n   s - Shutdown\r\n   u - Suspend\r\n   h - Hibernate\r\n   c - Exit the program\r\n\r\n Make your choice: ".Replace("\r\n", ControlChars.CrLf))
		Dim input As String = Console.ReadLine()
		Select Case input.ToLower()
			Case "l"
				WindowsController.ExitWindows(RestartOptions.LogOff, False)
			Case "p"
				WindowsController.ExitWindows(RestartOptions.PowerOff, False)
			Case "r"
				WindowsController.ExitWindows(RestartOptions.Reboot, False)
			Case "s"
				WindowsController.ExitWindows(RestartOptions.ShutDown, False)
			Case "u"
				WindowsController.ExitWindows(RestartOptions.Suspend, False)
			Case "h"
				WindowsController.ExitWindows(RestartOptions.Hibernate, False)
			Case "c"
				Return
			Case Else
				Console.WriteLine("Invalid input...")
		End Select
	End Sub
End Class
