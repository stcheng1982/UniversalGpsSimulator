# define constants
Set-Variable -Name "TFGpsSimulatorSetupProgramDownloadUrl" -Value "https://devmulti29.transfinder.com/pfgpssimulator/setup.exe" -Option Constant

Set-Variable -Name "TFGpsSimulatorProcessName" -Value "GpsSimulatorWindowsApp.exe" -Option Constant


# Create a subfolder named "TFGpsSimulator" under the current user's "Downloads" folder
$downloadPath = $PSScriptRoot
if (!(Test-Path -Path $downloadPath)) {
    New-Item -Path $downloadPath -ItemType Directory
}

# Download the setup program to the "TFGpsSimulator" folder with progress reporting in percentage format
$setupProgramPath = "$downloadPath\setup.exe"
Write-Host "Downloading the setup program to $setupProgramPath"
Invoke-WebRequest -Uri $TFGpsSimulatorSetupProgramDownloadUrl -OutFile $setupProgramPath -Verbose

# After setup program is downloaded, unblock it
Write-Host "Unblocking the setup program"
Unblock-File -Path $setupProgramPath -Verbose
Add-MpPreference -ExclusionPath $setupProgramPath -Verbose

# Call Add-MpPreference to exclude the GpsSimulatorWindowsApp.exe from Windows Defender
Write-Host "Excluding the GpsSimulatorWindowsApp.exe from Windows Defender"
Add-MpPreference -ExclusionPath "$env:userprofile\AppData\Local\Apps\2.0" -Verbose
Add-MpPreference -ExclusionProcess $TFGpsSimulatorProcessName -Verbose

# Call Start-Process to run the setup program with the /SILENT switch
Write-Host "Running the setup program with the /SILENT switch"
Start-Process -FilePath $setupProgramPath -ArgumentList "/SILENT" -Wait -Verbose

Write-Host "Removing the setup program folder from Windows Defender exclusion list"
Remove-MpPreference -ExclusionPath $setupProgramPath -Verbose

