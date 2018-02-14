@echo off

xcopy AMExtraFolderTest\* ..\DMSUpdateManagerConsole\bin\AnalysisToolManagerDistribution\AMExtraFolderTest\ /D /y
xcopy AMSubDirPushTest\* ..\DMSUpdateManagerConsole\bin\AnalysisToolManagerDistribution\AMSubDirPushTest\ /D /y
xcopy DeconTools\* ..\DMSUpdateManagerConsole\bin\AnalysisToolManagerDistribution\DeconTools\ /D /y
xcopy DtaRefinery\* ..\DMSUpdateManagerConsole\bin\AnalysisToolManagerDistribution\DtaRefinery\ /D /y
xcopy MSFileInfoScanner\* ..\DMSUpdateManagerConsole\bin\AnalysisToolManagerDistribution\MSFileInfoScanner\ /D /y
xcopy ProMex\* ..\DMSUpdateManagerConsole\bin\AnalysisToolManagerDistribution\ProMex\ /D /y
xcopy ProMex\HelloWorld.jar ..\DMSUpdateManagerConsole\bin\DMS_Programs\ProMex\ /D /Y

if not exist "..\DMSUpdateManagerConsole\bin\DMS_Programs\MSDeconv\0.6" mkdir "..\DMSUpdateManagerConsole\bin\DMS_Programs\MSDeconv\0.6"
echo Delete me > "..\DMSUpdateManagerConsole\bin\DMS_Programs\MSDeconv\0.6\MsDeconvConsole.jar"


if exist "..\DMSUpdateManagerConsole\bin\DMS_Programs\AScore\x64" del "..\DMSUpdateManagerConsole\bin\DMS_Programs\AScore\x64\*.*" /q
if exist "..\DMSUpdateManagerConsole\bin\DMS_Programs\AScore\x64" rmdir "..\DMSUpdateManagerConsole\bin\DMS_Programs\AScore\x64"

if exist "..\DMSUpdateManagerConsole\bin\DMS_Programs\AnalysisToolManager1\AMSubDirPushTest\" del "..\DMSUpdateManagerConsole\bin\DMS_Programs\AnalysisToolManager1\AMSubDirPushTest\Test File.txt" /q
if exist "..\DMSUpdateManagerConsole\bin\DMS_Programs\AnalysisToolManager1\AMSubDirPushTest\" rmdir "..\DMSUpdateManagerConsole\bin\DMS_Programs\AnalysisToolManager1\AMSubDirPushTest"

if exist ..\DMSUpdateManagerConsole\bin\DMS_Programs\ProMex\Missing_File.txt del ..\DMSUpdateManagerConsole\bin\DMS_Programs\ProMex\Missing_File.txt /q

if not exist "..\DMSUpdateManagerConsole\bin\DMS_Programs\AnalysisToolManager1\AMExtraFolderTest" mkdir "..\DMSUpdateManagerConsole\bin\DMS_Programs\AnalysisToolManager1\AMExtraFolderTest"
echo This is an extra file > "..\DMSUpdateManagerConsole\bin\DMS_Programs\AnalysisToolManager1\AMExtraFolderTest\Extra test file.txt"

copy UIMFLibrary_Rollback\* "..\DMSUpdateManagerConsole\bin\AnalysisToolManagerDistribution\MSFileInfoScanner" /y

copy msvcp100.dll "..\DMSUpdateManagerConsole\bin\DMS_Programs\DeconTools\" /y

rem Simulate a running Java program
rem Must use a full path here due to logic in function GetNumTargetFolderProcesses
start /MIN C:\ProgramData\Oracle\Java\javapath\java.exe -jar "F:\My Documents\Projects\DataMining\DMS_Managers\DMS_Update_Manager\DMSUpdateManagerConsole\bin\DMS_Programs\ProMex\HelloWorld.jar"

rem Simulate having a file in use
echo This file is updated > "..\DMSUpdateManagerConsole\bin\AnalysisToolManagerDistribution\DtaRefinery\Another new file.txt"

cd

echo.
echo Run the DMSUpdateManagerConsole with commandline:
echo /S:AnalysisToolManagerDistribution /T:DMS_Programs\AnalysisToolManager1 /L /Force /Preview
echo or
echo /P:DMSUpdateManagerOptions.xml /L /Force /Preview

pushd ..\DMSUpdateManagerConsole\bin\DMS_Programs\DtaRefinery\
call SleepTwoMinutes.bat
popd
