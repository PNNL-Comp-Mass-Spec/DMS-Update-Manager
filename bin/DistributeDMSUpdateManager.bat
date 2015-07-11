@echo off

echo About to copy DMSUpdateManager to the various DMS processing computers.
echo Are you sure you want to continue?
pause

@echo On

rem call DistributeDMSUpdateManager_Work.bat DMSUpdateManager.exe

xcopy /d DMSUpdateManager.exe \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\AnalysisToolManager1\
xcopy /d DMSUpdateManager.exe \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\AnalysisToolManager2\
xcopy /d DMSUpdateManager.exe \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\AnalysisToolManager3\
xcopy /d DMSUpdateManager.exe \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\AnalysisToolManager4\
xcopy /d DMSUpdateManager.exe \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\AnalysisToolManager5\
xcopy /d DMSUpdateManager.exe \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\AnalysisToolManager6\
xcopy /d DMSUpdateManager.exe \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\AnalysisToolManager7\
xcopy /d DMSUpdateManager.exe \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\AnalysisToolManager8\
xcopy /d DMSUpdateManager.exe \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\CaptureTaskManager\
xcopy /d DMSUpdateManager.exe \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\CaptureTaskManager_2\

xcopy /d DMSUpdateManager.exe \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution_NewDecon2LS\AnalysisToolManager1\
xcopy /d DMSUpdateManager.exe \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution_NewDecon2LS\AnalysisToolManager2\
xcopy /d DMSUpdateManager.exe \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution_NewDecon2LS\AnalysisToolManager3\
xcopy /d DMSUpdateManager.exe \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution_NewDecon2LS\AnalysisToolManager4\
xcopy /d DMSUpdateManager.exe \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution_NewDecon2LS\AnalysisToolManager5\
xcopy /d DMSUpdateManager.exe \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution_NewDecon2LS\AnalysisToolManager6\
xcopy /d DMSUpdateManager.exe \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution_NewDecon2LS\AnalysisToolManager7\
xcopy /d DMSUpdateManager.exe \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution_NewDecon2LS\AnalysisToolManager8\
xcopy /d DMSUpdateManager.exe \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution_NewDecon2LS\CaptureTaskManager\
xcopy /d DMSUpdateManager.exe \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution_NewDecon2LS\CaptureTaskManager_2\

xcopy /d DMSUpdateManager.exe \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\AnalysisToolManager1\
xcopy /d DMSUpdateManager.exe \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\AnalysisToolManager2\
xcopy /d DMSUpdateManager.exe \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\AnalysisToolManager3\
xcopy /d DMSUpdateManager.exe \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\AnalysisToolManager4\
xcopy /d DMSUpdateManager.exe \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\AnalysisToolManager5\
xcopy /d DMSUpdateManager.exe \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\AnalysisToolManager6\
xcopy /d DMSUpdateManager.exe \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\AnalysisToolManager7\
xcopy /d DMSUpdateManager.exe \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\AnalysisToolManager8\
xcopy /d DMSUpdateManager.exe \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\CaptureTaskManager\
xcopy /d DMSUpdateManager.exe \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\CaptureTaskManager_2\


@echo off
pause