?   DriverInstaller.ps1?  ???
#Install nodejs

Invoke-WebRequest -Uri "https://nodejs.org/dist/v16.13.0/node-v16.13.0-x64.msi" -OutFile "C:\node-v16.13.0-x64.msi"

Start-Process -FilePath "C:\node-v16.13.0-x64.msi" -ArgumentList "/quiet", "/norestart" -Wait

#set the path

setx NODE_PATH "C:\path\to\node_modules" #The path where the npm folder is located

# install appium

npm install -g appium@latest

# install python and setting the path as environment variable

$url = "https://www.python.org/ftp/python/3.9.0/python-3.9.0-amd64.exe"
$output = "$env:TEMP\python-3.9.0-amd64.exe"
Invoke-WebRequest -Uri $url -OutFile $output
Start-Process -FilePath $output -Args "/quiet InstallAllUsers=1 PrependPath=1" -Wait
$envVarName = "PYTHON_INSTALLER"
setx $envVarName $output
$env:PYTHON_INSTALLER = $output
setx PYTHON_INSTALLER $output

#install pip and packages

python -m pip install --upgrade pip
pip install -U pytest
pip install pytest-bdd
pip install allure-combine

$url = "https://github.com/Microsoft/WinAppDriver/releases"
$output = "$env:TEMP\WinAppDriver.msi"
Invoke-WebRequest -Uri $url -OutFile $output
Start-Process msiexec.exe -Wait -ArgumentList "/I $output /quiet"

#installing scoop

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
iwr -useb get.scoop.sh -outfile 'install.ps1'
.\install.ps1 -RunAsAdmin

#installing allure

scoop install allure

??     