param([Parameter(Mandatory=$true)][string]$GameRoot)
. (Join-Path $PSScriptRoot 'Common.ps1')
$game=Get-GameRoot $GameRoot;Assert-GameClosed $game
$target=Join-Path $game 'BepInEx\plugins\AstralPersonalCollector';$receipt=Join-Path $game 'BepInEx\config\local.astral.personalcollector.install.json';$config=Join-Path $game 'BepInEx\config\local.astral.personalcollector.cfg'
$null=Assert-ChildPath $game $target;$null=Assert-ChildPath $game $receipt;$null=Assert-ChildPath $game $config
if(!(Test-Path -LiteralPath $receipt)){throw 'No managed installation receipt was found. Install this package once before using its uninstaller.'}
$record=Get-Content -LiteralPath $receipt -Raw|ConvertFrom-Json
if($record.schema -ne 1 -or $record.product_id -ne $script:ProductId){throw 'Invalid installation receipt; no files were removed.'}
foreach($file in $record.files){$null=Join-Safe $target $file}
Remove-OwnedFiles $target $record.files
if((Test-Path -LiteralPath $target) -and @(Get-ChildItem -LiteralPath $target -Force).Count -eq 0){Remove-Item -LiteralPath $target}
if(Test-Path -LiteralPath $config){Remove-Item -LiteralPath $config};Remove-Item -LiteralPath $receipt
Write-Output ('Mod removed. Personal records retained: '+$record.data_directory+'. BepInEx and other mods were retained.')
