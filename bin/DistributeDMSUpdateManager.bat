@echo off

echo About to schedule DMSUpdateManager for distribution
echo by copying to \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution
echo Are you sure you want to continue?
pause

@echo On

rem call DistributeDMSUpdateManager_Work.bat DMSUpdateManager.exe

xcopy /d /y DMSUpdateManager.exe \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\DMSUpdateManager_CTM\
xcopy /d /y PRISM.dll            \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\DMSUpdateManager_CTM\

xcopy /d /y DMSUpdateManager.exe \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\DMSUpdateManager\
xcopy /d /y PRISM.dll            \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\DMSUpdateManager\

xcopy /d /y DMSUpdateManager.exe \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution_x86
xcopy /d /y PRISM.dll            \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution_x86

xcopy /d /y DMSUpdateManager.exe \\protoapps\DMS_Programs\DataPackage_Archive_Manager
xcopy /d /y PRISM.dll            \\protoapps\DMS_Programs\DataPackage_Archive_Manager

@echo off
pause