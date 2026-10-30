Set-StrictMode -Version 2
$ErrorActionPreference='Stop'
$script:ProductId='local.astral.personalcollector'
function Assert-ChildPath([string]$Root,[string]$Path) {
 $r=[IO.Path]::GetFullPath($Root).TrimEnd('\','/');$p=[IO.Path]::GetFullPath($Path)
 if(!$p.StartsWith($r+'\',[StringComparison]::OrdinalIgnoreCase)){throw "Path outside expected folder: $p"}
 $check=$p
 while($check.Length -ge $r.Length){
  if(Test-Path -LiteralPath $check){$item=Get-Item -LiteralPath $check -Force;if(($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw "Linked paths are not supported: $check"}}
  if($check -eq $r){break};$parent=Split-Path -Parent $check;if(!$parent -or $parent -eq $check){break};$check=$parent
 }
 return $p
}
function Join-Safe([string]$Root,[string]$Relative) {
 if([IO.Path]::IsPathRooted($Relative) -or $Relative -match '(^|[\\/])\.\.([\\/]|$)' -or $Relative -match ':'){throw 'Invalid relative package path.'}
 return Assert-ChildPath $Root (Join-Path $Root ($Relative.Replace('/','\')))
}
function Assert-GameClosed([string]$Root) {
 foreach($p in @(Get-Process AstralParty_INT -ErrorAction SilentlyContinue)){
  try{$exe=$p.MainModule.FileName}catch{throw 'Close Astral Party before installing or removing the mod.'}
  if([IO.Path]::GetFullPath($exe) -eq (Join-Path $Root 'AstralParty_INT.exe')){throw 'Close Astral Party before installing or removing the mod.'}
 }
 foreach($p in @(Get-Process AstralStatsWorker -ErrorAction SilentlyContinue)){
  try{$exe=$p.MainModule.FileName}catch{throw 'Wait for the game worker to exit, then retry.'}
  if($exe.StartsWith((Join-Path $Root 'BepInEx\plugins\AstralPersonalCollector')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Wait a few seconds after closing the game, then retry.'}
 }
}
function Get-GameRoot([string]$Candidate) {
 $full=[IO.Path]::GetFullPath($Candidate)
 foreach($path in @($full,(Join-Path $full '8vJXnINT'))){
  if((Test-Path -LiteralPath (Join-Path $path 'AstralParty_INT.exe')) -and (Test-Path -LiteralPath (Join-Path $path 'GameAssembly.dll')) -and (Test-Path -LiteralPath (Join-Path $path 'AstralParty_INT_Data') -PathType Container)){return $path}
 }
 throw 'Select the Astral Party game folder (the folder containing AstralParty_INT.exe).'
}
function Read-Paths([string]$File) {
 $result=@{};if(!(Test-Path -LiteralPath $File)){return $result};$section=''
 foreach($line in Get-Content -LiteralPath $File){if($line -match '^\s*\[([^\]]+)\]'){$section=$Matches[1];continue};if($section -eq 'Paths' -and $line -match '^\s*([^#;=]+?)\s*=\s*(.*)$'){$result[$Matches[1].Trim()]=$Matches[2].Trim()}}
 return $result
}
function Write-Utf8([string]$File,[string]$Text){[IO.File]::WriteAllText($File,$Text,(New-Object Text.UTF8Encoding($true)))}
function Remove-OwnedFiles([string]$Root,$Files) {
 $parents=@{}
 foreach($relative in $Files){$path=Join-Safe $Root $relative;if(Test-Path -LiteralPath $path -PathType Leaf){Remove-Item -LiteralPath $path -Force};$parent=Split-Path -Parent $path;while($parent.Length -gt $Root.Length){$parents[$parent]=$true;$parent=Split-Path -Parent $parent}}
 foreach($directory in @($parents.Keys|Sort-Object Length -Descending)){if((Test-Path -LiteralPath $directory -PathType Container) -and @(Get-ChildItem -LiteralPath $directory -Force).Count -eq 0){$null=Assert-ChildPath $Root $directory;Remove-Item -LiteralPath $directory}}
}
function Assert-Package([string]$PackageRoot){
 $manifest=Get-Content -LiteralPath (Join-Path $PackageRoot 'release-manifest.json') -Raw|ConvertFrom-Json
 if($manifest.schema -ne 1 -or $manifest.product_id -ne $script:ProductId){throw 'Unsupported package manifest.'}
 foreach($file in $manifest.files){$path=Join-Safe $PackageRoot $file.path;if(!(Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Sha256 $path) -ne $file.sha256){throw ('Package file is damaged or missing: '+$file.path)}}
 return $manifest
}
function Expand-CheckedZip([string]$Archive,[string]$Destination){
 Add-Type -AssemblyName System.IO.Compression.FileSystem
 $zip=[IO.Compression.ZipFile]::OpenRead($Archive)
 try{foreach($entry in $zip.Entries){$null=Join-Safe $Destination $entry.FullName}}finally{$zip.Dispose()}
 [IO.Compression.ZipFile]::ExtractToDirectory($Archive,$Destination)
}
function Remove-EmptyTree([string]$Root){
 if(!(Test-Path -LiteralPath $Root -PathType Container)){return}
 foreach($dir in @(Get-ChildItem -LiteralPath $Root -Directory -Force -Recurse|Sort-Object {$_.FullName.Length} -Descending)){$null=Assert-ChildPath $Root $dir.FullName;if(@(Get-ChildItem -LiteralPath $dir.FullName -Force).Count -eq 0){Remove-Item -LiteralPath $dir.FullName}}
 if(@(Get-ChildItem -LiteralPath $Root -Force).Count -eq 0){Remove-Item -LiteralPath $Root}
}
function Select-GameRoot([string]$Candidate){
 if($Candidate){return Get-GameRoot $Candidate}
 $steam=@();try{$steam+=((Get-ItemProperty 'HKCU:\Software\Valve\Steam').SteamPath)}catch{}
 $steam+=@((Join-Path ${env:ProgramFiles(x86)} 'Steam'),(Join-Path $env:ProgramFiles 'Steam'));$libraries=@($steam)
 foreach($path in @($steam)){$vdf=Join-Path $path 'steamapps\libraryfolders.vdf';if(Test-Path -LiteralPath $vdf){foreach($match in [regex]::Matches([IO.File]::ReadAllText($vdf),'"path"\s+"([^"]+)"')){$libraries+=$match.Groups[1].Value.Replace('\\','\')}}}
 foreach($library in @($libraries|Select-Object -Unique)){
  $manifest=Join-Path $library 'steamapps\appmanifest_2622000.acf';$install='Astral Party'
  if(Test-Path -LiteralPath $manifest){$match=[regex]::Match([IO.File]::ReadAllText($manifest),'"installdir"\s+"([^"]+)"');if($match.Success){$install=$match.Groups[1].Value}}
  try{return Get-GameRoot (Join-Path $library ('steamapps\common\'+$install))}catch{}
 }
 Add-Type -AssemblyName System.Windows.Forms;$dialog=New-Object Windows.Forms.FolderBrowserDialog;$dialog.Description='Select Astral Party (Steam > Manage > Browse local files).'
 if($dialog.ShowDialog() -ne 'OK'){return $null};return Get-GameRoot $dialog.SelectedPath
}

function Get-Sha256([string]$File){$stream=[IO.File]::OpenRead($File);$sha=[Security.Cryptography.SHA256]::Create();try{return ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-','')}finally{$sha.Dispose();$stream.Dispose()}}
