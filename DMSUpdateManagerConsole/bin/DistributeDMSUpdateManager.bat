@echo off

echo About to schedule DMSUpdateManager for distribution
echo by copying to \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution
echo Are you sure you want to continue?
if not "%1"=="NoPause" pause

@echo On

call DistributeDMSUpdateManager_Work.bat \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\DMSUpdateManager_CTM\

call DistributeDMSUpdateManager_Work.bat \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\DMSUpdateManager\

call DistributeDMSUpdateManager_Work.bat \\protoapps\DMS_Programs\DMSUpdateManager

xcopy /d /y DMSUpdateManager.dll ..\..\..\Analysis_Manager\AM_Common\
xcopy /d /y DMSUpdateManager.pdb ..\..\..\Analysis_Manager\AM_Common\

@echo off
if not "%1"=="NoPause" pause
