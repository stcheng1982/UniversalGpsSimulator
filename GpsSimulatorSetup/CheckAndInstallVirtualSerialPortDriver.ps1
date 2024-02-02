
Add-Type -AssemblyName System.Windows.Forms

$com0comInstallFolderPath = "${env:ProgramFiles(x86)}\com0com"
$com0comCliSetupToolPath = "$com0comInstallFolderPath\setupc.exe"

# Check if the com0com is already installed (if the com0com install folder exists and contains the setup program)
if ((Test-Path $com0comInstallFolderPath) -and (Test-Path $com0comCliSetupToolPath)) {
    Write-Host "COM0COM virtual serial port driver is already installed"
    return 0
}

# Ask user if they want to install COM0COM virtual serial port driver
$confirmButtons = [System.Windows.Forms.MessageBoxButtons]::YesNo
$result = [System.Windows.Forms.MessageBox]::Show("Do you want to install COM0COM virtual serial port driver? This is necessary for serial port based Device GPS simulation.", "Install COM0COM", $confirmButtons, [System.Windows.Forms.MessageBoxIcon]::Question)

if ($result -eq [System.Windows.Forms.DialogResult]::Yes) {
    Write-Host "Installing COM0COM virtual serial port driver"
}
else {
    Write-Host "Not installing COM0COM virtual serial port driver"
    return 0
}

$com0comZipArchivePath = "$PSScriptRoot\Dependencies\com0com-2.2.2.0-x64-fre-signed.zip"
$com0comSetupExtractPath = "$PSScriptRoot\Dependencies" 

# Extract the com0com zip archive to the current folder
Expand-Archive -Path $com0comZipArchivePath -DestinationPath $com0comSetupExtractPath -Force -Verbose
$com0comSetupProgramPath = "$com0comSetupExtractPath\com0com-2.2.2.0-x64-fre-signed\setup.exe"

if (!(Test-Path $com0comSetupProgramPath)) {
    Write-Error "Could not find the com0com setup program at $com0comSetupProgramPath"
    return 1
}

# Unblock the com0com setup program
Unblock-File -Path $com0comSetupProgramPath -Verbose
# Run the com0com setup program with the /SILENT switch
Start-Process -FilePath $com0comSetupProgramPath -ArgumentList "/S /D=$com0comInstallFolderPath" -Wait -Verbose

Write-Host "Waiting 3 seconds for the com0com driver to be installed"
Start-Sleep -Seconds 3

# Check if the com0com is already installed (if the com0com install folder exists and contains the setup program)
if (!(Test-Path $com0comCliSetupToolPath)) {
    Write-Error "Could not find the com0com cli setup tool at $com0comCliSetupToolPath"
    return 1
}

Write-Host "Creating initial virtual serial port pair"

# try finding larget COM port number that already exists (starts with COMx, where x is a number)
$existingPortNames = [System.IO.Ports.SerialPort]::getportnames() | Where-Object { $_ -like "COM*" }
$maxPortNumber = 0
foreach ($portName in $existingPortNames) {
    $portNumber = [int]$portName.Substring(3)
    if ($portNumber -gt $maxPortNumber) {
        $maxPortNumber = $portNumber
    }
}

# Prepare the virtual serial port name pair
$comPortNumber = $maxPortNumber + 1
$comPortName1 = "COM$comPortNumber"
$comPortName2 = "COM$($comPortNumber + 1)"

# invoke the com0com cli setup tool to create a virtual serial port pair
Write-Host "Gonna creating virtual serial port pair $comPortName1 and $comPortName2"
CD $com0comInstallFolderPath
# Start-Process -FilePath $com0comCliSetupToolPath -ArgumentList "install PortName=$comPortName1,EmuBR=yes PortName=$comPortName2,EmuBR=yes" -Wait -Verbose
& .\setupc.exe install PortName=$comPortName1,EmuBR=yes PortName=$comPortName2,EmuBR=yes 

Write-Host "Waiting 2 seconds for the port pairs to be installed"
Start-Sleep -Seconds 2

$existingPortNames = [System.IO.Ports.SerialPort]::getportnames() | Where-Object { $_ -like "COM*" }
# Check if the virtual serial port pair was created successfully
if (($existingPortNames -contains $comPortName1) -and ($existingPortNames -contains $comPortName2)) {
    Write-Host "Virtual serial port pair $comPortName1 and $comPortName2 created successfully"
}
else {
    Write-Error "Virtual serial port pair $comPortName1 and $comPortName2 was not created successfully"
    return 1
}

Write-Host "COM0COM virtual serial port driver installed successfully"