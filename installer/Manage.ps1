param([string]$GameRoot)
. (Join-Path $PSScriptRoot 'Common.ps1')
try{
 $game=Select-GameRoot $GameRoot;if(!$game){exit 0}
 $paths=Read-Paths (Join-Path $game 'BepInEx\config\local.astral.personalcollector.cfg')
 $data=if($paths.ContainsKey('DataDirectory')){$paths['DataDirectory']}else{Join-Path $env:LOCALAPPDATA 'AstralPersonalStats'}
 $exe=Join-Path $game 'BepInEx\plugins\AstralPersonalCollector\worker\AstralStatsWorker.exe'
 if(!(Test-Path -LiteralPath $exe)){throw 'Install the distribution package first.'}
 while($true){
  Write-Host "`nAstral Personal Stats";Write-Host '1. Open personal data folder';Write-Host '2. Import a saved replay';Write-Host '3. Change replay retention days';Write-Host '4. Run offline diagnostics';Write-Host '5. Exit';Write-Host '6. Create diagnostics ZIP'
  $choice=Read-Host 'Select'
  if($choice -eq '5'){break}
  try{
   switch($choice){
    '1'{New-Item -ItemType Directory -Path $data -Force|Out-Null;Start-Process explorer.exe -ArgumentList ('"'+$data+'"')}
    '2'{Add-Type -AssemblyName System.Windows.Forms;$dialog=New-Object Windows.Forms.OpenFileDialog;$dialog.Title='Select a saved replay';$dialog.Filter='Replay files|*';if($dialog.ShowDialog() -eq 'OK'){& $exe --import-replay $dialog.FileName --data-dir $data;if($LASTEXITCODE -ne 0){throw 'Replay import failed. Log in to the game once and select a replay from your own account.'}}}
    '3'{
     $days=0;$answer=Read-Host 'Keep downloaded raw replays for how many days? (1-3650)'
     if(![int]::TryParse($answer,[ref]$days) -or $days -lt 1 -or $days -gt 3650){throw 'Enter a value from 1 to 3650.'}
     New-Item -ItemType Directory -Path $data -Force|Out-Null;$file=Join-Path $data 'personal-settings.json';$value=if(Test-Path -LiteralPath $file){Get-Content -LiteralPath $file -Raw|ConvertFrom-Json}else{[pscustomobject]@{player_id=0;cache_days=7;cache_enabled=$true}}
     if($value.PSObject.Properties['cache_days']){$value.cache_days=$days}else{$value|Add-Member -NotePropertyName cache_days -NotePropertyValue $days}
     $tmp=$file+'.part';Write-Utf8 $tmp ($value|ConvertTo-Json -Depth 6);Move-Item -LiteralPath $tmp -Destination $file -Force;Write-Host 'Retention updated. Analysed statistics are retained.'
    }
    '6'{& (Join-Path $PSScriptRoot 'Diagnostics.ps1') -GameRoot $game}
    '4'{& $exe --self-test;if($LASTEXITCODE -ne 0){throw 'Diagnostics failed.'}}
   }
  }catch{Write-Host $_.Exception.Message -ForegroundColor Red}
 }
}catch{Write-Host $_.Exception.Message -ForegroundColor Red;Read-Host 'Press Enter to close'}
