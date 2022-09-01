// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Design", "RCS1075:Avoid empty catch clause that catches System.Exception.", Justification = "Ignore errors here", Scope = "member", Target = "~M:DMSUpdateManager.DirectoryContainer.Dispose(System.Boolean)")]
[assembly: SuppressMessage("Design", "RCS1075:Avoid empty catch clause that catches System.Exception.", Justification = "Ignore errors here", Scope = "member", Target = "~M:DMSUpdateManager.DMSUpdateManager.ExtractExePathFromProcessPath(System.String,System.String@)~System.String")]
[assembly: SuppressMessage("Design", "RCS1075:Avoid empty catch clause that catches System.Exception.", Justification = "Ignore errors here", Scope = "member", Target = "~M:DMSUpdateManager.DMSUpdateManager.LoadParameterFileSettings(System.String)~System.Boolean")]
[assembly: SuppressMessage("Design", "RCS1075:Avoid empty catch clause that catches System.Exception.", Justification = "Ignore errors here", Scope = "member", Target = "~M:DMSUpdateManager.RemoteUpdateUtility.DeleteLockFile(Renci.SshNet.Sftp.SftpFile)")]
[assembly: SuppressMessage("Design", "RCS1075:Avoid empty catch clause that catches System.Exception.", Justification = "Ignore errors here", Scope = "member", Target = "~M:DMSUpdateManager.RemoteUpdateUtility.DeleteLockFile(Renci.SshNet.SftpClient,System.String,System.String)")]
[assembly: SuppressMessage("Readability", "RCS1123:Add parentheses when necessary.", Justification = "Parentheses not needed", Scope = "member", Target = "~M:DMSUpdateManager.RemoteUpdateUtility.MoveFiles(System.Collections.Generic.IReadOnlyCollection{System.String},System.String,System.Collections.Generic.List{System.String})~System.Boolean")]
[assembly: SuppressMessage("Roslynator", "RCS1123:Add parentheses when necessary.", Justification = "Parentheses not needed", Scope = "member", Target = "~M:DMSUpdateManager.DMSUpdateManager.UpdateDirectoryWork(System.String,DMSUpdateManager.DirectoryContainer,DMSUpdateManager.FileOrDirectoryInfo,System.Boolean,System.Boolean,System.Boolean)~System.Boolean")]
[assembly: SuppressMessage("Simplification", "RCS1179:Unnecessary assignment.", Justification = "Leave for readability", Scope = "member", Target = "~M:DMSUpdateManager.DMSUpdateManager.UpdateDirectoryRun(System.String,DMSUpdateManager.DirectoryContainer,System.String,System.Boolean)~System.Boolean")]
[assembly: SuppressMessage("Usage", "RCS1246:Use element access.", Justification = "Prefer to use .First()", Scope = "member", Target = "~M:DMSUpdateManager.RemoteUpdateUtility.CopyFilesToRemote(System.Collections.Generic.IEnumerable{System.IO.FileInfo},System.String,System.Boolean,System.String)~System.Boolean")]
