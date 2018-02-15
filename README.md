# DMS Update Manager

The DMS Update Manager is used to keep automated processing manager software up-to-date.
Although it is part of PRISM (the Proteomics Research Information and Management System),
it is a standalone program and does not contact any databases.

The program compares the files on a central distribution share to files on the local computer,
updating any files that are out of date.  It supports rolling back local files to an older version
and auto-deleting extra files (see the special flag files described below).

## DMSUpdateManagerConsole Syntax

```
DMSUpdateManagerConsole.exe
 [/S:SourceFolderPath [/T:TargetFolderPath]
 [/P:ParameterFilePath] [/L] [/V] [/NM] [/WaitTimeout:minutes]
```

All files present in the source folder will be copied to the target folder if the file size or file modification time are different.
You can either define the source and target folder at the command line, or using the parameter file.  All settings in the parameter file override comm
and line settings.

Use /L to log details of the updated files.

Use /V to preview the files that would be updated.

Use /NM to not use a mutex, allowing multiple instances of this program to run simultaneously with the same parameter file.

Use /WaitTimeout:minutes to specify how long the program should wait for another instance to finish before exiting.

### DLL

The DMSUpdateManager.dll can be referenced via another application to update files in a given folder.

## Special Files

### File Suffix Flags

These file suffix flags affect how files are processed. Append the flags to the source file name to use them.

| Suffix   | Description |
|----------|-------------|
| .rollback  | Rolls back newer target files to match the source |
| .delete    | Deletes the target file |
| .checkjava | Check for a running .jar file; do not overwrite if in use |

### Flag Files 

The following special flag files affect how folders are processed. To use them, create an empty file with the given name in a source folder:

| Filename | Description |
|----------|-------------|
| _PushDir_.txt | Pushes the directory to the parent of the target folder |
| _AMSubDir_.txt | Pushes the directory to the target folder as a subfolder |
| _DeleteSubDir_.txt | Deletes the directory from the parent of the target, but only if the directory is empty |
| _DeleteAMSubDir_.txt | Deletes the directory from below the target, but only if it is empty |


## Example Distribution Folder Layout

* SubFolder: AnalysisToolManager1
  * StartManager1.bat
  * StartUpdateManager.bat
* SubFolder: AnalysisToolManager2
  * StartManager2.bat
  * StartUpdateManager.bat
* SubFolder: MASIC
  * PNNLOmics.dll.delete
  * MSDataFileReader.dll
  * PRISM.dll
  * ThermoRawFileReader.dll
  * MASIC.exe
  * ThermoRawFileReader.dll.rollback
  * Readme.txt
  * RevisionHistory.txt
* SubFolder: x64
  * SQLite.Interop.dll
  * SQLite.Interop.dll.rollback
  * _AMSubDir_.txt
* SubFolder: x86
  * SQLite.Interop.dll
  * SQLite.Interop.dll.rollback
  * _AMSubDir_.txt
* AM_Shared.dll
* AnalysisManagerProg.exe
* DLLVersionInspector_x64.exe
* DLLVersionInspector_x86.exe
* PRISM.dll
* PRISMWin.dll
* Protein_Exporter.dll

### Example Usage

```
DMSUpdateManagerConsole.exe /P:DMSUpdateManagerOptions.xml /L
```

### Example Parameter File

```xml
<?xml version="1.0" encoding="utf-8"?>
<sections>
  <section name="DMSUpdateManager">
    <item key="OverwriteNewerFiles" value="False" />
    <item key="SourceFolderPath" value="\\CentralServer\AnalysisToolManagerDistribution" />
    <item key="TargetFolderPath" value="C:\DMS_Programs\AnalysisToolManager1" />
    <item key="CopySubdirectoriesToParentFolder" value="True" />
    <item key="FilesToIgnore" value="AnalysisManagerProg.exe.config, Utils.pyc, Global.pyc" />
    <item key="LogMessages" value="True" />
    <item key="MinimumRepeatTimeSeconds" value="60" />
    <item key="LogFolderPath" value="C:\DMS_Programs\DMSUpdateManager\Logs" />
  </section>
</sections>
```

Files will be copied from `\\CentralServer\AnalysisToolManagerDistribution` to `C:\DMS_Programs\AnalysisToolManager1`

When processing subdirectories, the files in the subdirectory will be compared to the corresponding 
subdirectory below `C:\DMS_Programs`.  The exception is if the subdirectory contains file `_AMSubDir_.txt` in which case
the folder will be compared to a directory below `C:\DMS_Programs\AnalysisToolManager1`


## Linux Server Updates

The DMS Update Manager also supports pushing new/updated files to a Linux server.
This requires an RSA private key file on the computer running the DMS Update Manager
along with an RSA public key file on the target Linux server.  Specify the path
to the private key file in the XML parameter file provided to the DMS Update Manager.
Also specify the path to a text file that contains the encoded passphrase to decrypt 
the private key. See below for an example XML parameter file that specifies these options.

When pushing files to a remote server, the parameter file must specify the path
to a text file with an encoded passphrase for decoding the RSA private key. The
following commands can be used to convert passwords:

`DMSUpdateManagerConsole.exe PasswordToParse /Encode` \
`DMSUpdateManagerConsole.exe EncodedPassword /Decode`

### Example RSA private key file

```
-----BEGIN RSA PRIVATE KEY-----
Proc-Type: 4,ENCRYPTED
DEK-Info: DES-EDE3-CBC,3A98E0096FAA85F9

BsYgOkem+lhwJ...
...TDRHNoeILQI/K2pCpsQ==
-----END RSA PRIVATE KEY-----
```

### Example Usage

```
DMSUpdateManagerConsole.exe /P:DMSUpdateManagerOptionsRemoteHost.xml /L
```

### Example Parameter File With Remote Host Info

```xml
<?xml version="1.0" encoding="utf-8"?>
<sections>
  <section name="DMSUpdateManager">
    <item key="OverwriteNewerFiles" value="False" />
    <item key="SourceFolderPath" value="\\CentralServer\AnalysisToolManagerDistribution" />
    <item key="TargetFolderPath" value="/opt/DMS_Programs/AnalysisManager" />
    <item key="RemoteHostName" value="prismweb2" />
    <item key="RemoteHostUserName" value="svc-dms" />
    <item key="PrivateKeyFilePath" value="C:\DMS_RemoteInfo\Svc-Dms.key" />
    <item key="PassphraseFilePath" value="C:\DMS_RemoteInfo\Svc-Dms.pass" />
    <item key="CopySubdirectoriesToParentFolder" value="True" />
    <item key="FilesToIgnore" value="AnalysisManagerProg.exe.config, Utils.pyc, Global.pyc" />
    <item key="LogMessages" value="True" />
    <item key="MinimumRepeatTimeSeconds" value="60" />
    <item key="LogFolderPath" value="C:\DMS_Programs\DMSUpdateManager\Logs" />
  </section>
</sections>
```

Files will be copied from `\\CentralServer\AnalysisToolManagerDistribution` to `/opt/DMS_Programs/AnalysisManager`

When processing subdirectories, the files in the subdirectory will be compared to the corresponding 
subdirectory below `/opt/DMS_Programs`.  The exception is if the subdirectory contains file `_AMSubDir_.txt` in which case
the folder will be compared to a directory below `/opt/DMS_Programs/AnalysisManager`
	
## Contacts

Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) \
E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov \
Website: https://panomics.pnl.gov/ or https://omics.pnl.gov

## License

The DMS Update Manager is licensed under the Apache License, Version 2.0; 
you may not use this file except in compliance with the License.  You may obtain 
a copy of the License at https://opensource.org/licenses/Apache-2.0
