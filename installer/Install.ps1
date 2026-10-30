param([Parameter(Mandatory=$true)][string]$GameRoot,[string]$UserDataRoot=(Join-Path $env:LOCALAPPDATA 'AstralPersonalStats'))
. (Join-Path $PSScriptRoot 'Common.ps1')
$packageRoot=Split-Path $PSScriptRoot -Parent
$manifest=Assert-Package $packageRoot
$game=Get-GameRoot $GameRoot;Assert-GameClosed $game
if(![Environment]::Is64BitOperatingSystem){throw 'Windows x64 is required.'}
$bep=Join-Path $game 'BepInEx';$target=Join-Path $bep 'plugins\AstralPersonalCollector';$config=Join-Path $bep 'config\local.astral.personalcollector.cfg';$receipt=Join-Path $bep 'config\local.astral.personalcollector.install.json'
$null=Assert-ChildPath $game $target;$null=Assert-ChildPath $game $config;$null=Assert-ChildPath $game $receipt
$oldPaths=Read-Paths $config
$dataRoot=if($oldPaths.ContainsKey('DataDirectory') -and $oldPaths['DataDirectory']){[IO.Path]::GetFullPath($oldPaths['DataDirectory'])}else{[IO.Path]::GetFullPath($UserDataRoot)}
if((Test-Path -LiteralPath $dataRoot) -and !(Test-Path -LiteralPath $dataRoot -PathType Container)){throw 'The personal data path is a file. Choose a data folder.'}
$stage=Join-Path $bep ('.astral-personal-stage-'+[Guid]::NewGuid().ToString('N'));$null=Assert-ChildPath $game $stage
$backup=Join-Path $bep ('.astral-personal-backups\'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N'))
$null=Assert-ChildPath $game $backup
$payload=@($manifest.files|Where-Object {$_.path.StartsWith('payload/')});$relativeFiles=@($payload|ForEach-Object {$_.path.Substring(8)})
$loaderCreated=@();$oldMoved=$false;$newMoved=$false;$cfgExisted=Test-Path -LiteralPath $config;$receiptExisted=Test-Path -LiteralPath $receipt
$cfgBefore=if($cfgExisted){[IO.File]::ReadAllText($config)}else{$null};$receiptBefore=if($receiptExisted){[IO.File]::ReadAllText($receipt)}else{$null}
try{
 New-Item -ItemType Directory -Path $stage -Force|Out-Null
 foreach($file in $payload){$dest=Join-Safe $stage $file.path.Substring(8);New-Item -ItemType Directory -Path (Split-Path $dest -Parent) -Force|Out-Null;Copy-Item -LiteralPath (Join-Safe $packageRoot $file.path) -Destination $dest;if((Get-Sha256 $dest) -ne $file.sha256){throw 'Staged file hash mismatch.'}}
 $core=Join-Path $bep 'core\BepInEx.Unity.IL2CPP.dll'
 if(!(Test-Path -LiteralPath $core)){
  if((Test-Path -LiteralPath (Join-Path $game 'winhttp.dll')) -or (Test-Path -LiteralPath (Join-Path $game 'dotnet')) -or (Test-Path -LiteralPath (Join-Path $bep 'core'))){throw 'An incomplete or different loader exists. Repair BepInEx first; existing loader files were not overwritten.'}
  $loaderStage=Join-Path $stage '.loader';New-Item -ItemType Directory -Path $loaderStage|Out-Null
  Expand-CheckedZip (Join-Safe $packageRoot $manifest.loader.path) $loaderStage
  foreach($item in Get-ChildItem -LiteralPath $loaderStage -File -Recurse){$relative=$item.FullName.Substring($loaderStage.Length+1);$dest=Join-Safe $game $relative;if(Test-Path -LiteralPath $dest){throw ('Loader destination already exists: '+$relative)};New-Item -ItemType Directory -Path (Split-Path $dest -Parent) -Force|Out-Null;Copy-Item -LiteralPath $item.FullName -Destination $dest;$loaderCreated+=@{path=$relative;hash=(Get-Sha256 $dest)}}
  $loaderRel=@(Get-ChildItem -LiteralPath $loaderStage -File -Recurse|ForEach-Object {$_.FullName.Substring($loaderStage.Length+1)});Remove-OwnedFiles $loaderStage $loaderRel;Remove-EmptyTree $loaderStage
 }else{
  foreach($required in @('core\Il2CppInterop.Runtime.dll','core\0Harmony.dll')){if(!(Test-Path -LiteralPath (Join-Path $bep $required))){throw 'Existing BepInEx IL2CPP installation is incomplete.'}}
 }
 Assert-GameClosed $game
 if(Test-Path -LiteralPath $target){
  foreach($item in Get-ChildItem -LiteralPath $target -Force -Recurse){if(($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Linked files in the existing plugin folder are not supported.'}}
  New-Item -ItemType Directory -Path $backup -Force|Out-Null
  if($cfgExisted){Write-Utf8 (Join-Path $backup 'previous.cfg') $cfgBefore};if($receiptExisted){Write-Utf8 (Join-Path $backup 'previous-install.json') $receiptBefore}
  Move-Item -LiteralPath $target -Destination (Join-Path $backup 'plugin');$oldMoved=$true
 }
 New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force|Out-Null
 Move-Item -LiteralPath $stage -Destination $target;$newMoved=$true
 New-Item -ItemType Directory -Path (Split-Path $config -Parent) -Force|Out-Null
 [IO.Directory]::CreateDirectory($dataRoot)|Out-Null
 $oldSettings=if($oldPaths.ContainsKey('BackendDirectory')){Join-Path $oldPaths['BackendDirectory'] 'personal-settings.json'}else{$null}
 if($oldSettings -and (Test-Path -LiteralPath $oldSettings) -and !(Test-Path -LiteralPath (Join-Path $dataRoot 'personal-settings.json'))){Copy-Item -LiteralPath $oldSettings -Destination (Join-Path $dataRoot 'personal-settings.json')}
 $worker=Join-Path $target 'worker\AstralStatsWorker.exe'
 Write-Utf8 $config ((@('[Paths]',('BackendDirectory = '+(Join-Path $target 'worker')),('WorkerExecutable = '+$worker),'PythonExecutable =',('DataDirectory = '+$dataRoot))) -join "`r`n")
 $record=@{schema=1;product_id=$script:ProductId;version=$manifest.version;data_directory=$dataRoot;files=$relativeFiles;loader_installed=($loaderCreated.Count -gt 0)}
 Write-Utf8 $receipt ($record|ConvertTo-Json -Depth 6)
 Write-Output ('Installed '+$manifest.version+'. Data: '+$dataRoot)
}catch{
 if($newMoved -and (Test-Path -LiteralPath $target)){Remove-OwnedFiles $target $relativeFiles;if(@(Get-ChildItem -LiteralPath $target -Force).Count -eq 0){$null=Assert-ChildPath $game $target;Remove-Item -LiteralPath $target}}
 if($oldMoved){$null=Assert-ChildPath $game $target;$null=Assert-ChildPath $game (Join-Path $backup 'plugin');Move-Item -LiteralPath (Join-Path $backup 'plugin') -Destination $target}
 if($cfgExisted){Write-Utf8 $config $cfgBefore}elseif(Test-Path -LiteralPath $config){Remove-Item -LiteralPath $config}
 if($receiptExisted){Write-Utf8 $receipt $receiptBefore}elseif(Test-Path -LiteralPath $receipt){Remove-Item -LiteralPath $receipt}
 foreach($created in $loaderCreated){$path=Join-Safe $game $created.path;if((Test-Path -LiteralPath $path) -and (Get-Sha256 $path) -eq $created.hash){Remove-Item -LiteralPath $path}}
 if(Test-Path -LiteralPath $stage){Remove-OwnedFiles $stage $relativeFiles;if(@(Get-ChildItem -LiteralPath $stage -Force).Count -eq 0){$null=Assert-ChildPath $game $stage;Remove-Item -LiteralPath $stage}}
 throw
}
