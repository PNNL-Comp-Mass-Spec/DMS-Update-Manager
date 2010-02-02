@echo off

echo About to copy DMSUpdateManager to the various DMS processing computers.
echo Are you sure you want to continue?
pause

@echo On

call DistributeDMSUpdateManager_Work.bat DMSUpdateManager.exe

@echo off
pause