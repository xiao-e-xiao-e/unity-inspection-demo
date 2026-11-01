$ErrorActionPreference='Stop'
$client=Join-Path $PSScriptRoot 'bin\unity\InspectionDemo.exe'
if(!(Test-Path -LiteralPath $client)) {
    throw 'Client not built. Open unity-client in Unity, then use Inspection > Build Windows Client, or run the InspectionRoom scene in Editor.'
}
Start-Process -FilePath $client -WorkingDirectory (Split-Path $client -Parent) -WindowStyle Normal
