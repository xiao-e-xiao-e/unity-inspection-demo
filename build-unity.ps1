param([string]$UnityPath='D:\App\Unity\Editor\2022.3.62f3c1\Editor\Unity.exe')
$ErrorActionPreference='Stop'
if(!(Test-Path -LiteralPath $UnityPath)){throw 'Pass -UnityPath with the Unity 2022.3 editor executable path.'}
$project=Join-Path $PSScriptRoot 'unity-client'
$logs=Join-Path $PSScriptRoot '.tools'
New-Item -ItemType Directory -Path $logs -Force | Out-Null
$log=Join-Path $logs 'unity-build.log'
$process=Start-Process -FilePath $UnityPath -ArgumentList @('-batchmode','-quit','-projectPath',('"{0}"' -f $project),'-executeMethod','Inspection.Editor.InspectionBuilder.BuildAll','-logFile',('"{0}"' -f $log)) -WindowStyle Hidden -PassThru
$process.WaitForExit()
if($process.ExitCode -ne 0){throw "Unity build failed. See $log"}
Write-Output "Client: $PSScriptRoot\bin\unity\InspectionDemo.exe"
Write-Output "Log: $log"
