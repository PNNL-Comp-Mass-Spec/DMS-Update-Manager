@echo off

echo This file is obsolete
echo Instead use DistributeDMSUpdateManager.bat which copies the .exe to paths like
echo \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\AnalysisToolManager1\
echo.
echo This allows the managers to update their fellow managers

Goto Done

echo Sequest clusters
rem copy %1 \\SeqCluster1\DMS_Programs\AnalysisToolManager\
rem copy %1 \\SeqCluster2\DMS_Programs\AnalysisToolManager\
rem copy %1 \\SeqCluster3\DMS_Programs\AnalysisToolManager\
rem copy %1 \\SeqCluster4\DMS_Programs\AnalysisToolManager\
rem copy %1 \\SeqCluster5\DMS_Programs\AnalysisToolManager\

echo Pub-24 through Pub-29
copy %1 \\Pub-24\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-25\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-26\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-27\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-28\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-29\DMS_Programs\AnalysisToolManager1\

echo Pub-30 through Pub-49
copy %1 \\Pub-30\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-31\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-32\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-33\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-34\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-35\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-36\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-37\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-38\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-39\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-40\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-41\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-42\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-43\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-44\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-45\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-46\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-47\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-48\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-49\DMS_Programs\AnalysisToolManager1\

echo Pub-50 through Pub-69
copy %1 \\Pub-50\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-51\DMS_Programs\AnalysisToolManager1\
echo WARNING: Skipping Pub-52 since was offline in June 2014
rem Offline: copy %1 \\Pub-52\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-53\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-54\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-55\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-56\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-57\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-58\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-59\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-60\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-61\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-62\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-63\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-64\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-65\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-66\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-67\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-68\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-69\DMS_Programs\AnalysisToolManager1\

echo Pub-70 through Pub-93, plus mallard
copy %1 \\Pub-70\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-71\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-72\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-73\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-74\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-75\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-76\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-77\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-78\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-79\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-80\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-81\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-82\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-83\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-84\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-85\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-86\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-87\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-88\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-89\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-90\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-91\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-92\DMS_Programs\AnalysisToolManager1\
copy %1 \\Pub-93\DMS_Programs\AnalysisToolManager1\

copy %1 \\mallard\DMS_Programs\AnalysisToolManager1\

echo Mash-01 through Mash-06
rem Decommissioned: copy %1 \\mash-01\DMS_Programs\AnalysisToolManager\
rem Decommissioned: copy %1 \\mash-02\DMS_Programs\AnalysisToolManager\
rem Decommissioned: copy %1 \\mash-03\DMS_Programs\AnalysisToolManager\
rem Decommissioned: copy %1 \\mash-04\DMS_Programs\AnalysisToolManager\
rem Decommissioned: copy %1 \\mash-05\DMS_Programs\AnalysisToolManager\
rem Decommissioned: copy %1 \\mash-06\DMS_Programs\AnalysisToolManager\

echo Proto-3 through Proto-10
copy %1 \\Proto-3\DMS_Programs\AnalysisToolManager1\
copy %1 \\Proto-4\DMS_Programs\AnalysisToolManager1\
copy %1 \\Proto-5\DMS_Programs\AnalysisToolManager1\
copy %1 \\Proto-6\DMS_Programs\AnalysisToolManager1\
copy %1 \\Proto-7\DMS_Programs\AnalysisToolManager1\
copy %1 \\Proto-8\DMS_Programs\AnalysisToolManager1\
copy %1 \\Proto-9\DMS_Programs\AnalysisToolManager1\
copy %1 \\Proto-10\DMS_Programs\AnalysisToolManager1\




echo Pub-11 through Pub-29

copy %1 \\Pub-24\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-26\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-27\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-28\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-29\DMS_Programs\AnalysisToolManager2\

echo Pub-30 through Pub-49
copy %1 \\Pub-30\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-31\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-32\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-33\DMS_Programs\AnalysisToolManager2\
rem Decommissioned: copy %1 \\Pub-34\DMS_Programs\AnalysisToolManager2\
rem Decommissioned: copy %1 \\Pub-35\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-36\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-37\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-38\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-39\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-40\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-41\DMS_Programs\AnalysisToolManager2\
rem Decommissioned: copy %1 \\Pub-42\DMS_Programs\AnalysisToolManager2\
rem Decommissioned: copy %1 \\Pub-43\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-44\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-45\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-46\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-47\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-48\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-49\DMS_Programs\AnalysisToolManager2\

echo Pub-50 through Pub-69
copy %1 \\Pub-50\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-51\DMS_Programs\AnalysisToolManager2\
echo WARNING: Skipping Pub-52 since was offline in June 2014
rem Offline: copy %1 \\Pub-52\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-53\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-54\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-55\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-56\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-57\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-58\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-59\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-60\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-61\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-62\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-63\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-64\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-65\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-66\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-67\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-68\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-69\DMS_Programs\AnalysisToolManager2\

echo Pub-70 through Pub-93, plus mallard
copy %1 \\Pub-70\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-71\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-72\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-73\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-74\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-75\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-76\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-77\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-78\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-79\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-80\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-81\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-82\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-83\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-84\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-85\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-86\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-87\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-88\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-89\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-90\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-91\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-92\DMS_Programs\AnalysisToolManager2\
copy %1 \\Pub-93\DMS_Programs\AnalysisToolManager2\
copy %1 \\mallard\DMS_Programs\AnalysisToolManager2\

echo Proto-3 through Proto-10
copy %1 \\Proto-3\DMS_Programs\AnalysisToolManager2\
copy %1 \\Proto-4\DMS_Programs\AnalysisToolManager2\
copy %1 \\Proto-5\DMS_Programs\AnalysisToolManager2\
copy %1 \\Proto-6\DMS_Programs\AnalysisToolManager2\
copy %1 \\Proto-7\DMS_Programs\AnalysisToolManager2\
copy %1 \\Proto-8\DMS_Programs\AnalysisToolManager2\
copy %1 \\Proto-9\DMS_Programs\AnalysisToolManager2\
copy %1 \\Proto-10\DMS_Programs\AnalysisToolManager2\


copy %1 \\Pub-32\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-33\DMS_Programs\AnalysisToolManager3\
rem Offline: copy %1 \\Pub-34\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-35\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-36\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-37\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-38\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-39\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-40\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-41\DMS_Programs\AnalysisToolManager3\
rem Decommissioned: copy %1 \\Pub-42\DMS_Programs\AnalysisToolManager3\
rem Decommissioned: copy %1 \\Pub-43\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-44\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-45\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-46\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-47\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-48\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-49\DMS_Programs\AnalysisToolManager3\

echo Pub-50 through Pub-69
copy %1 \\Pub-50\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-51\DMS_Programs\AnalysisToolManager3\
echo WARNING: Skipping Pub-52 since was offline in June 2014
rem Offline: copy %1 \\Pub-52\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-53\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-54\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-55\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-56\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-57\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-58\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-59\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-60\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-61\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-62\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-63\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-64\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-65\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-66\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-67\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-68\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-69\DMS_Programs\AnalysisToolManager3\

echo Pub-70 through Pub-93, plus mallard
copy %1 \\Pub-70\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-71\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-72\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-73\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-74\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-75\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-76\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-77\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-78\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-79\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-80\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-81\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-82\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-83\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-84\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-85\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-86\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-87\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-88\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-89\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-90\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-91\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-92\DMS_Programs\AnalysisToolManager3\
copy %1 \\Pub-93\DMS_Programs\AnalysisToolManager3\

copy %1 \\mallard\DMS_Programs\AnalysisToolManager3\


copy %1 \\Pub-32\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-33\DMS_Programs\AnalysisToolManager4\
rem Offline: copy %1 \\Pub-34\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-35\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-36\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-37\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-38\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-39\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-40\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-41\DMS_Programs\AnalysisToolManager4\
rem Decommissioned: copy %1 \\Pub-42\DMS_Programs\AnalysisToolManager4\
rem Decommissioned: copy %1 \\Pub-43\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-44\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-45\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-46\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-47\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-48\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-49\DMS_Programs\AnalysisToolManager4\

echo Pub-50 through Pub-69
copy %1 \\Pub-50\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-51\DMS_Programs\AnalysisToolManager4\
echo WARNING: Skipping Pub-52 since was offline in June 2014
rem Offline: copy %1 \\Pub-52\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-53\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-54\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-55\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-56\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-57\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-58\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-59\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-60\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-61\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-62\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-63\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-64\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-65\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-66\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-67\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-68\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-69\DMS_Programs\AnalysisToolManager4\

echo Pub-70 through Pub-93, plus mallard
copy %1 \\Pub-70\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-71\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-72\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-73\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-74\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-75\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-76\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-77\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-78\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-79\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-80\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-81\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-82\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-83\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-84\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-85\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-86\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-87\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-88\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-89\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-90\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-91\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-92\DMS_Programs\AnalysisToolManager4\
copy %1 \\Pub-93\DMS_Programs\AnalysisToolManager4\

copy %1 \\mallard\DMS_Programs\AnalysisToolManager4\


copy %1 \\Pub-36\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-37\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-38\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-39\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-44\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-45\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-46\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-47\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-48\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-49\DMS_Programs\AnalysisToolManager5\

echo Pub-50 through Pub-69
copy %1 \\Pub-50\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-51\DMS_Programs\AnalysisToolManager5\
echo WARNING: Skipping Pub-52 since was offline in June 2014
rem Offline: copy %1 \\Pub-52\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-53\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-54\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-55\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-56\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-57\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-58\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-59\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-60\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-61\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-62\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-63\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-64\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-65\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-66\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-67\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-68\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-69\DMS_Programs\AnalysisToolManager5\

echo Pub-70 through Pub-93, plus mallard
copy %1 \\Pub-70\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-71\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-72\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-73\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-74\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-75\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-76\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-77\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-78\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-79\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-80\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-81\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-82\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-83\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-84\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-85\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-86\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-87\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-88\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-89\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-90\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-91\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-92\DMS_Programs\AnalysisToolManager5\
copy %1 \\Pub-93\DMS_Programs\AnalysisToolManager5\
copy %1 \\mallard\DMS_Programs\AnalysisToolManager5\


copy %1 \\Pub-36\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-37\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-38\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-39\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-44\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-45\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-46\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-47\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-48\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-49\DMS_Programs\AnalysisToolManager6\

echo Pub-50 through Pub-69
copy %1 \\Pub-50\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-51\DMS_Programs\AnalysisToolManager6\
echo WARNING: Skipping Pub-52 since was offline in June 2014
rem Offline: copy %1 \\Pub-52\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-53\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-54\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-55\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-56\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-57\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-58\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-59\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-60\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-61\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-62\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-63\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-64\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-65\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-66\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-67\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-68\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-69\DMS_Programs\AnalysisToolManager6\

echo Pub-70 through Pub-93, plus mallard
copy %1 \\Pub-70\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-71\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-72\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-73\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-74\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-75\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-76\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-77\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-78\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-79\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-80\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-81\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-82\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-83\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-84\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-85\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-86\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-87\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-88\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-89\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-90\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-91\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-92\DMS_Programs\AnalysisToolManager6\
copy %1 \\Pub-93\DMS_Programs\AnalysisToolManager6\

copy %1 \\mallard\DMS_Programs\AnalysisToolManager6\


copy %1 \\Pub-36\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-37\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-38\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-39\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-44\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-45\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-46\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-47\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-48\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-49\DMS_Programs\AnalysisToolManager7\

echo Pub-50 through Pub-69
copy %1 \\Pub-50\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-51\DMS_Programs\AnalysisToolManager7\
echo WARNING: Skipping Pub-52 since was offline in June 2014
rem Offline: copy %1 \\Pub-52\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-53\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-54\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-55\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-56\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-57\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-58\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-59\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-60\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-61\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-62\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-63\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-64\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-65\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-66\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-67\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-68\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-69\DMS_Programs\AnalysisToolManager7\

echo Pub-70 through Pub-93, plus mallard
copy %1 \\Pub-70\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-71\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-72\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-73\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-74\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-75\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-76\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-77\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-78\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-79\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-80\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-81\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-82\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-83\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-84\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-85\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-86\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-87\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-88\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-89\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-90\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-91\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-92\DMS_Programs\AnalysisToolManager7\
copy %1 \\Pub-93\DMS_Programs\AnalysisToolManager7\

copy %1 \\mallard\DMS_Programs\AnalysisToolManager7\


copy %1 \\Pub-36\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-37\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-38\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-39\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-44\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-45\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-46\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-47\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-48\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-49\DMS_Programs\AnalysisToolManager8\

echo Pub-50 through Pub-69
copy %1 \\Pub-50\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-51\DMS_Programs\AnalysisToolManager8\
echo WARNING: Skipping Pub-52 since was offline in June 2014
rem Offline: copy %1 \\Pub-52\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-53\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-54\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-55\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-56\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-57\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-58\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-59\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-60\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-61\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-62\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-63\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-64\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-65\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-66\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-67\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-68\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-69\DMS_Programs\AnalysisToolManager8\

echo Pub-70 through Pub-93, plus mallard
copy %1 \\Pub-70\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-71\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-72\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-73\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-74\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-75\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-76\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-77\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-78\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-79\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-80\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-81\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-82\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-83\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-84\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-85\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-86\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-87\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-88\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-89\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-90\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-91\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-92\DMS_Programs\AnalysisToolManager8\
copy %1 \\Pub-93\DMS_Programs\AnalysisToolManager8\

copy %1 \\mallard\DMS_Programs\AnalysisToolManager8\


:CTM
echo Mash-01 through Mash-06, CaptureTaskManager
rem Decommissioned: copy %1 \\mash-01\DMS_Programs\CaptureTaskManager\
rem Decommissioned: copy %1 \\mash-02\DMS_Programs\CaptureTaskManager\
rem Decommissioned: copy %1 \\mash-03\DMS_Programs\CaptureTaskManager\
rem Decommissioned: copy %1 \\mash-04\DMS_Programs\CaptureTaskManager\
rem Decommissioned: copy %1 \\mash-05\DMS_Programs\CaptureTaskManager\
rem Decommissioned: copy %1 \\mash-06\DMS_Programs\CaptureTaskManager\


echo Proto-3 through Proto-10, CaptureTaskManager
copy %1 \\proto-3\DMS_Programs\CaptureTaskManager\
copy %1 \\proto-4\DMS_Programs\CaptureTaskManager\
copy %1 \\proto-5\DMS_Programs\CaptureTaskManager\
copy %1 \\proto-7\DMS_Programs\CaptureTaskManager\
copy %1 \\proto-8\DMS_Programs\CaptureTaskManager\
copy %1 \\proto-9\DMS_Programs\CaptureTaskManager\
copy %1 \\proto-10\DMS_Programs\CaptureTaskManager\

echo Proto-3 through Proto-10, CaptureTaskManager2
copy %1 \\proto-3\DMS_Programs\CaptureTaskManager_2\
copy %1 \\proto-5\DMS_Programs\CaptureTaskManager_2\
copy %1 \\proto-7\DMS_Programs\CaptureTaskManager_2\
copy %1 \\proto-9\DMS_Programs\CaptureTaskManager_2\
copy %1 \\proto-10\DMS_Programs\CaptureTaskManager_2\

echo Pub-50 through Pub-69, CaptureTaskManager
copy %1 \\Pub-50\DMS_Programs\CaptureTaskManager\
copy %1 \\pub-51\DMS_Programs\CaptureTaskManager\
echo WARNING: Skipping Pub-52 since was offline in June 2014
rem Offline: copy %1 \\pub-52\DMS_Programs\CaptureTaskManager\
copy %1 \\pub-53\DMS_Programs\CaptureTaskManager\
copy %1 \\pub-54\DMS_Programs\CaptureTaskManager\
copy %1 \\pub-55\DMS_Programs\CaptureTaskManager\
copy %1 \\pub-56\DMS_Programs\CaptureTaskManager\
copy %1 \\pub-57\DMS_Programs\CaptureTaskManager\
copy %1 \\pub-58\DMS_Programs\CaptureTaskManager\
copy %1 \\pub-59\DMS_Programs\CaptureTaskManager\
copy %1 \\pub-60\DMS_Programs\CaptureTaskManager\
copy %1 \\pub-61\DMS_Programs\CaptureTaskManager\
copy %1 \\pub-62\DMS_Programs\CaptureTaskManager\
copy %1 \\pub-63\DMS_Programs\CaptureTaskManager\
copy %1 \\pub-64\DMS_Programs\CaptureTaskManager\
copy %1 \\pub-65\DMS_Programs\CaptureTaskManager\
copy %1 \\pub-66\DMS_Programs\CaptureTaskManager\
copy %1 \\pub-67\DMS_Programs\CaptureTaskManager\
copy %1 \\pub-68\DMS_Programs\CaptureTaskManager\
copy %1 \\pub-69\DMS_Programs\CaptureTaskManager\

echo Pub-50 through Pub-69, CaptureTaskManager_2
copy %1 \\Pub-50\DMS_Programs\CaptureTaskManager_2\
copy %1 \\pub-51\DMS_Programs\CaptureTaskManager_2\
echo WARNING: Skipping Pub-52 since was offline in June 2014
rem Offline: copy %1 \\pub-52\DMS_Programs\CaptureTaskManager_2\
copy %1 \\pub-53\DMS_Programs\CaptureTaskManager_2\
copy %1 \\pub-54\DMS_Programs\CaptureTaskManager_2\
copy %1 \\pub-55\DMS_Programs\CaptureTaskManager_2\
copy %1 \\pub-56\DMS_Programs\CaptureTaskManager_2\
copy %1 \\pub-57\DMS_Programs\CaptureTaskManager_2\
copy %1 \\pub-58\DMS_Programs\CaptureTaskManager_2\
copy %1 \\pub-59\DMS_Programs\CaptureTaskManager_2\
copy %1 \\pub-60\DMS_Programs\CaptureTaskManager_2\
copy %1 \\pub-61\DMS_Programs\CaptureTaskManager_2\
copy %1 \\pub-62\DMS_Programs\CaptureTaskManager_2\
copy %1 \\pub-63\DMS_Programs\CaptureTaskManager_2\
copy %1 \\pub-64\DMS_Programs\CaptureTaskManager_2\
copy %1 \\pub-65\DMS_Programs\CaptureTaskManager_2\
copy %1 \\pub-66\DMS_Programs\CaptureTaskManager_2\
copy %1 \\pub-67\DMS_Programs\CaptureTaskManager_2\
copy %1 \\pub-68\DMS_Programs\CaptureTaskManager_2\
copy %1 \\pub-69\DMS_Programs\CaptureTaskManager_2\

:Done