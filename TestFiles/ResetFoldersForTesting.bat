@echo off

xcopy AMExtraFolderTest\* ..\bin\AnalysisToolManagerDistribution\AMExtraFolderTest\ /D /y
xcopy AMSubDirPushTest\* ..\bin\AnalysisToolManagerDistribution\AMSubDirPushTest\ /D /y
xcopy DeconTools\* ..\bin\AnalysisToolManagerDistribution\DeconTools\ /D /y
xcopy DtaRefinery\* ..\bin\AnalysisToolManagerDistribution\DtaRefinery\ /D /y
xcopy MSFileInfoScanner\* ..\bin\AnalysisToolManagerDistribution\MSFileInfoScanner\ /D /y
xcopy ProMex\* ..\bin\AnalysisToolManagerDistribution\ProMex\ /D /y
xcopy ProMex\HelloWorld.jar ..\bin\DMS_Programs\ProMex\ /D /Y

if not exist "..\bin\DMS_Programs\MSDeconv\0.6" mkdir "..\bin\DMS_Programs\MSDeconv\0.6"
echo Delete me > "..\bin\DMS_Programs\MSDeconv\0.6\MsDeconvConsole.jar"


if exist "..\bin\DMS_Programs\AScore\x64" del "..\bin\DMS_Programs\AScore\x64\*.*" /q
if exist "..\bin\DMS_Programs\AScore\x64" rmdir "..\bin\DMS_Programs\AScore\x64"

if exist "..\bin\DMS_Programs\AnalysisToolManager1\AMSubDirPushTest\" del "..\bin\DMS_Programs\AnalysisToolManager1\AMSubDirPushTest\Test File.txt" /q
if exist "..\bin\DMS_Programs\AnalysisToolManager1\AMSubDirPushTest\" rmdir "..\bin\DMS_Programs\AnalysisToolManager1\AMSubDirPushTest"

if exist ..\bin\DMS_Programs\ProMex\Missing_File.txt del ..\bin\DMS_Programs\ProMex\Missing_File.txt /q

if not exist "..\bin\DMS_Programs\AnalysisToolManager1\AMExtraFolderTest" mkdir "..\bin\DMS_Programs\AnalysisToolManager1\AMExtraFolderTest"
echo This is an extra file > "..\bin\DMS_Programs\AnalysisToolManager1\AMExtraFolderTest\Extra test file.txt"

copy UIMFLibrary_Rollback\* "..\bin\AnalysisToolManagerDistribution\MSFileInfoScanner" /y

copy msvcp100.dll "..\bin\DMS_Programs\DeconTools\" /y

rem Simulate a running Java program
rem Must use a full path here due to logic in function GetNumTargetFolderProcesses
start /MIN C:\ProgramData\Oracle\Java\javapath\java.exe -jar "F:\My Documents\Projects\DataMining\DMS_Managers\DMS_Update_Manager\bin\DMS_Programs\ProMex\HelloWorld.jar"

rem Simulate having a file in use
echo This file is updated > "..\bin\AnalysisToolManagerDistribution\DtaRefinery\Another new file.txt"

cd

pushd ..\bin\DMS_Programs\DtaRefinery\
call SleepTwoMinutes.bat
popd
