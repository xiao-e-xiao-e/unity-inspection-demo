param([string]$UnityPath='D:\App\Unity\Editor\2022.3.62f3c1\Editor\Unity.exe')
$ErrorActionPreference='Stop'
if (!(Test-Path -LiteralPath $UnityPath)) { throw 'Unity not found. Pass -UnityPath with the Unity 2022.3 editor executable path.' }
$project=Join-Path $PSScriptRoot 'unity-client'
Start-Process -FilePath $UnityPath -ArgumentList @('-projectPath',('"{0}"' -f $project)) -WindowStyle Hidden
