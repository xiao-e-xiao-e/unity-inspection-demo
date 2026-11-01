$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$go=Join-Path $root '.tools\go\bin\go.exe'
if(!(Test-Path $go)){$go=(Get-Command go -ErrorAction Stop).Source}
$run=Join-Path $root ('.tools\integration-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $run -Force | Out-Null
foreach($port in 18083,18084,18085){if(Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue){throw "Port $port is busy. No existing processes will be stopped."}}
$fixture=Join-Path $run 'fixture.exe'
& $go build -o $fixture (Join-Path $PSScriptRoot 'http-fixture.go')
if($LASTEXITCODE -ne 0){throw 'Fixture build failed'}
$backendExe=Join-Path $root 'bin\inspection-server.exe'
$playerExe=Join-Path $root 'bin\unity\InspectionDemo.exe'
$db=Join-Path $run 'test.db'
$playerLog=Join-Path $run 'player.log'
$shot=Join-Path $run 'form.png'
$backend=$null;$fake=$null;$player=$null
function Start-Backend {
 $process=Start-Process -FilePath $backendExe -ArgumentList @('-addr','127.0.0.1:18083','-db',('"{0}"' -f $db)) -PassThru -WindowStyle Hidden
 for($i=0;$i -lt 30;$i++){
  if($process.HasExited){throw 'Backend exited'}
  try{if((Invoke-RestMethod 'http://127.0.0.1:18083/api/health' -TimeoutSec 1).status -eq 'ok'){return $process}}catch{}
  Start-Sleep -Milliseconds 200
 }
 Stop-Process -Id $process.Id;throw 'Backend did not become ready'
}
try {
 $backend=Start-Backend
 $fake=Start-Process -FilePath $fixture -PassThru -WindowStyle Hidden
 # Visible interactive Unity window is required for a valid rendered screenshot.
 $player=Start-Process -FilePath $playerExe -ArgumentList @('-inspectionSmoke','-apiBaseUrl','http://127.0.0.1:18083','-smokeScreenshot',('"{0}"' -f $shot),'-logFile',('"{0}"' -f $playerLog),'-screen-width','1440','-screen-height','900','-screen-fullscreen','0') -PassThru -WindowStyle Normal
 if(!$player.WaitForExit(55000)){throw 'Unity smoke timed out'}
 if($player.ExitCode -ne 0){Get-Content $playerLog -Tail 60;throw 'Unity smoke failed'}
 $records=(Invoke-RestMethod 'http://127.0.0.1:18083/api/issues').items
 if($records.Count -ne 2){throw 'Expected two saved issues'}
 if(!($records | Where-Object {$_.title -eq '网络恢复后重试' -and $_.priority -eq 'high' -and $_.status -eq 'open'})){throw 'Saved values incorrect'}
 $ids=@($records.id | Sort-Object)
 Stop-Process -Id $backend.Id;$backend.WaitForExit();$backend=Start-Backend
 $after=(Invoke-RestMethod 'http://127.0.0.1:18083/api/issues').items
 if((@($after.id | Sort-Object) -join ',') -ne ($ids -join ',')){throw 'Restart lost records'}
 'PASS: Unity integration and backend restart persistence.'
 "Artifacts: $run"
 Select-String -Path $playerLog -Pattern 'INSPECTION_'
} finally {
 foreach($process in @($player,$fake,$backend)){if($process -and !$process.HasExited){Stop-Process -Id $process.Id;$process.WaitForExit()}}
}
