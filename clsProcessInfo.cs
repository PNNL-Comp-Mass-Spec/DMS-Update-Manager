Imports System.IO

Public Class clsProcessInfo

    ''' <summary>
    ''' Process ID
    ''' </summary>
    ''' <returns></returns>
    Public ReadOnly Property ProcessID As Long

    ''' <summary>
    ''' Full path to the .exe
    ''' </summary>
    ''' <returns></returns>
    Public ReadOnly Property ExePath As String

    ''' <summary>
    ''' Parent folder of the .exe
    ''' </summary>
    ''' <returns></returns>
    Public ReadOnly Property FolderPath As String

    ''' <summary>
    ''' Command line, including the .exe and any command line arguments
    ''' </summary>
    ''' <returns></returns>
    ''' <remarks>May have absolute path or relative path to the Exe, depending on how the process was started</remarks>
    Public ReadOnly Property CommandLine As String

    ''' <summary>
    ''' Arguments portion of the command line
    ''' </summary>
    ''' <returns></returns>
    Public ReadOnly Property CommandLineArgs As String

    ' ReSharper disable once CollectionNeverUpdated.Global
    ''' <summary>
    ''' FolderPath, split on path separators
    ''' </summary>
    ''' <returns></returns>
    Public ReadOnly Property FolderHierarchy As List(Of String)

    ''' <summary>
    ''' Constructor
    ''' </summary>
    ''' <param name="lngProcessID">Process ID</param>
    ''' <param name="strExePath">Executable path</param>
    ''' <param name="strCommandLine">Command line</param>
    Public Sub New(lngProcessID As Long, strExePath As String, strCommandLine As String)
        ProcessID = lngProcessID
        ExePath = strExePath
        CommandLine = strCommandLine

        FolderPath = Path.GetDirectoryName(strExePath)
        CommandLine = strCommandLine
        FolderHierarchy = GetFolderHierarchy(FolderPath)

        Dim exeName = Path.GetFileName(ExePath)
        Dim exeIndex = CommandLine.IndexOf(exeName, StringComparison.Ordinal)

        If exeIndex >= 0 Then
            CommandLineArgs = CommandLine.Substring(exeIndex + exeName.Length)
        Else
            CommandLineArgs = CommandLine
        End If

    End Sub

    Public Shared Function GetFolderHierarchy(folderPath As String) As List(Of String)
        Dim folderPathHierarchy = folderPath.Split(Path.DirectorySeparatorChar).ToList()
        Return folderPathHierarchy
    End Function


    Public Overrides Function ToString() As String
        If CommandLine.Contains(ExePath) Then
            Return CommandLine
        Else
            Return ExePath
        End If
    End Function

End Class
