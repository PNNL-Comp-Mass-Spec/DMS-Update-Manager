The DMS Update Manager program synchronizes the files in Analysis Manager folders and related folders using a master source.

Special files:
	_PushDir_.txt
	_DeleteSubDir_.txt
	_DeleteAMSubDir_.txt

Special file suffixes:
	.rollback
	.delete


The DMSUpdateManager.exe should be placed in the same folder as AnalysisManagerProg.exe or CaptureTaskManager.exe and then
started using a command like this:
DMSUpdateManager.exe /P:DMSUpdateManagerOptions.xml

where might look like DMSUpdateManagerOptions.xml

<?xml version="1.0" encoding="utf-8"?>
<sections>
  <section name="DMSUpdateManager">
    <item key="OverwriteNewerFiles" value="False" />
    <item key="SourceFolderPath" value="\\gigasax\DMS_Programs\AnalysisToolManagerDistribution_NewDecon2LS" />
    <item key="TargetFolderPath" value="." />
    <item key="CopySubdirectoriesToParentFolder" value="True" />
    <item key="RebootCommandFolderPath" value="\\gigasax\DMS_Programs\AnalysisToolManagerReboot" />
    <item key="FilesToIgnore" value="AnalysisManagerProg.exe.config, Utils.pyc, Global.pyc, PValues.txt, PTMods.txt" />
    <item key="LogMessages" value="True" />
  </section>
</sections>


The DMS Update Manager will look for files that are present in the SourceFolder and 
not present in the target folder, or are newer in the SourceFolder than present in 
the target folder.  If new/updated files are found, it will copy them from the source folder
to the target folder.  Use setting FilesToIgnore in the XML file to define file names that 
should not be updated

Setting CopySubdirectoriesToParentFolder instructs the program to compare each of the subdirectories
in the source folder with directories present one level above the target folder.  For example, 
if DMSUpdateManager.exe is started in folder AnalysisToolManager1 in this folder tree:

-\AnalysisToolManager1
-\AnalysisToolManager2
-\AnalysisToolManager3
-\DeconTools
---\v1.0.4212
---\v1.0.4232
---\v1.0.4259
-\DtaRefinery
-\inspect
-\MASIC
-\MSGF
---\v6393
---\v6432
-\MultiAlign
-\XTandem

And if the source folder has this structure:

-\AnalysisToolManagerDistribution_NewDecon2LS
----\DeconTools
------\v1.0.4259
----\DtaRefinery
----\MSGF
------\v6393
------\v6432
----\MultiAlign


Then the DMS Update Manager will compare files in folders DeconTools, 
DtaRefinery, MSGF, and MultiAlign since those folders are present in the source 
folder as subdirectories, and are also present as folders one level above.

To specify that a file should be deleted instead of being copied, append ".delete"
to the filename.  For example, to delete file PHRP.pdb you would put file PHRP.pdb.delete
in the source folder.

To specify that a file should be updated even if a newer version exists in the target folder,
create a file named Filename.ext.rollback in the source folder.  For example, 
if the source file contained these two files:
	MSGF.jar
	MSGF.jar.rollback

Then file MSGF.jar will get copied to the target even if the target folder has a 
newer version of MSGF.jar

Typically, if a subfolder exists in the source but does not exist in the target, 
that subfolder will not be processed.  To force the subfolder to be copied to the target,
place file _PushDir_.txt in the source folder.  The program will then copy the 
subfolder and all files to the target computer.

Conversely, to delete a subfolder, place file _DeleteSubDir_.txt or file
_DeleteAMSubDir_.txt in the subfolder.  Note that the folder will only be deleted
if it is empty.  To assure it is empty, rename all of the files in the source folder
to end in .delete
