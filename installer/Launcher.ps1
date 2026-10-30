param([ValidateSet('Install','Uninstall')][string]$Mode='Install',[string]$GameRoot)
. (Join-Path $PSScriptRoot 'Common.ps1')
try{
 $GameRoot=Select-GameRoot $GameRoot;if(!$GameRoot){exit 0}
 $dataRoot=Join-Path $env:LOCALAPPDATA 'AstralPersonalStats';$result=[IO.Path]::GetTempFileName()
 $worker=Join-Path $PSScriptRoot 'Worker.ps1';$q=[char]34
 $args='-NoProfile -STA -ExecutionPolicy Bypass -File '+$q+$worker+$q+' -Mode '+$Mode+' -GameRoot '+$q+$GameRoot+$q+' -UserDataRoot '+$q+$dataRoot+$q+' -ResultPath '+$q+$result+$q
 $shell=if([Environment]::Is64BitOperatingSystem -and ![Environment]::Is64BitProcess){Join-Path $env:SystemRoot 'Sysnative\WindowsPowerShell\v1.0\powershell.exe'}else{Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'}
 $process=Start-Process -FilePath $shell -Verb RunAs -WindowStyle Hidden -ArgumentList $args -Wait -PassThru
 $record=Get-Content -LiteralPath $result -Raw|ConvertFrom-Json;Remove-Item -LiteralPath $result
 if(!$record.ok){throw $record.message};Write-Host $record.message -ForegroundColor Green
}catch{Write-Host $_.Exception.Message -ForegroundColor Red}
Read-Host 'Press Enter to close'
