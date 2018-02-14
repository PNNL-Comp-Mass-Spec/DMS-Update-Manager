@echo off

xcopy /d /y DMSUpdateManagerConsole.exe %1
xcopy /d /y DMSUpdateManager.dll %1
xcopy /d /y PRISM.dll %1
xcopy /d /y Renci.SshNet.dll %1
xcopy /d /y DMSUpdateManager.pdb %1
xcopy /d /y DMSUpdateManagerConsole.pdb %1

@echo on
