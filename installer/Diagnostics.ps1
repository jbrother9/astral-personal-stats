param([string]$GameRoot,[string]$OutputRoot,[switch]$IncludeGameLogs)
. (Join-Path $PSScriptRoot 'Common.ps1')
$game=Select-GameRoot $GameRoot
if(!$game){exit 0}
$paths=Read-Paths (Join-Path $game 'BepInEx\config\local.astral.personalcollector.cfg')
$data=if($paths.ContainsKey('DataDirectory') -and $paths['DataDirectory']){[IO.Path]::GetFullPath($paths['DataDirectory'])}else{Join-Path $env:LOCALAPPDATA 'AstralPersonalStats'}
$destRoot=if($OutputRoot){[IO.Path]::GetFullPath($OutputRoot)}else{Join-Path $data 'diagnostics'}
[IO.Directory]::CreateDirectory($destRoot)|Out-Null
$stage=Join-Path $destRoot ('.diagnostics-'+[Guid]::NewGuid().ToString('N'));$null=Assert-ChildPath $destRoot $stage
[IO.Directory]::CreateDirectory($stage)|Out-Null
$files=New-Object 'System.Collections.Generic.List[string]'
$included=New-Object 'System.Collections.Generic.List[string]'
function Save-Document([string]$Relative,[string]$Text){
 $target=Join-Safe $stage $Relative;[IO.Directory]::CreateDirectory((Split-Path $target -Parent))|Out-Null
 Write-Utf8 $target $Text;$files.Add($Relative)
}
function Redact([string]$Text){
 $text=$Text.Replace($data,'<DATA>').Replace($game,'<GAME>')
 $text=[regex]::Replace($text,'(?i)[A-Z]:[\\/]Users[\\/][^\\/\r\n"'']+','<USER>')
 $text=[regex]::Replace($text,'\b\d{10,24}\b','<ID>')
 $text=[regex]::Replace($text,'(?i)((?:owner|account|player|canonical|candidate|career)[ _-]*(?:id)?\s*[:=]?\s*)\d{5,}','$1<ID>')
 $lines=$text -split "`r?`n"
 return (($lines|ForEach-Object {if($_ -match '(?i)authorization|bearer\s|password|passwd|ticket|secret|(?:session|auth|access|refresh)[_-]?(?:key|token|id)'){ '[sensitive log line omitted]' }else{$_}}) -join "`r`n")
}
function Add-Log([string]$Source,[string]$Relative,[int]$MaxBytes=262144){
 if(!(Test-Path -LiteralPath $Source -PathType Leaf)){return}
 if(((Get-Item -LiteralPath $Source).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){return}
 $stream=$null
 try{
  $stream=[IO.File]::Open($Source,[IO.FileMode]::Open,[IO.FileAccess]::Read,([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
  $length=[int][Math]::Min($stream.Length,$MaxBytes);$null=$stream.Seek(-$length,[IO.SeekOrigin]::End);$bytes=New-Object byte[] $length;$read=$stream.Read($bytes,0,$length)
  Save-Document $Relative (Redact ([Text.Encoding]::UTF8.GetString($bytes,0,$read)));$included.Add($Relative)
 }catch{Save-Document ($Relative+'.unavailable.txt') ('Unable to read log: '+$_.Exception.GetType().Name)}finally{if($stream){$stream.Dispose()}}
}
try{
 foreach($name in @('plugin-runtime.log','plugin-worker.log')){foreach($suffix in @('','.1','.2')){Add-Log (Join-Safe $data ($name+$suffix)) ('runtime/'+$name+$suffix)}}
 $state=@{}
 foreach($source in @('plugin-collection-status.json','plugin-worker-status.json','plugin-ui-state.json','plugin-state.json')){
  $path=Join-Safe $data $source
  if(!(Test-Path -LiteralPath $path) -or (Get-Item -LiteralPath $path).Length -gt 16384){continue}
  try{
   $obj=Get-Content -LiteralPath $path -Raw|ConvertFrom-Json;$safe=@{}
   foreach($key in @('version','heartbeat','kind','label','pending','failed','indexed','status','profile_found','window_showing','window_on_stage','history_status','room_present','room_state')){
    $property=$obj.PSObject.Properties[$key]
    if($property -and ($null -eq $property.Value -or $property.Value -is [bool] -or $property.Value -is [ValueType] -or $property.Value -is [string] -and $property.Value.Length -le 160)){$safe[$key]=$property.Value}
   }
   $state[$source]=$safe
  }catch{}
 }
 Save-Document 'state.json' ($state|ConvertTo-Json -Depth 6)
 $inventory=@();$plugins=Join-Path $game 'BepInEx\plugins'
 if(Test-Path -LiteralPath $plugins){foreach($item in Get-ChildItem -LiteralPath $plugins -Filter '*.dll' -File -Recurse){
  if($item.FullName -match '[\\/]worker[\\/]'){continue}
  if($item.Name -notmatch '^[A-Za-z0-9_.-]{1,128}$'){continue}
  try{$version=[Diagnostics.FileVersionInfo]::GetVersionInfo($item.FullName).FileVersion}catch{$version='unknown'}
  $inventory+=@{file=$item.Name;version=$version}
 }}
 Save-Document 'environment.json' (@{schema=1;created_utc=[DateTime]::UtcNow.ToString('o');os=[Environment]::OSVersion.VersionString;logical_processors=[Environment]::ProcessorCount;plugins=$inventory;game_logs_included=[bool]$IncludeGameLogs;included_logs=@($included.ToArray())}|ConvertTo-Json -Depth 6)
 if($IncludeGameLogs){
  Add-Log (Join-Path $game 'BepInEx\LogOutput.log') 'game/LogOutput.log' 524288
  $unity=Join-Path $env:USERPROFILE 'AppData\LocalLow\feimo\AstralParty_INT'
  foreach($name in @('Player.log','Player-prev.log')){Add-Log (Join-Path $unity $name) ('game/'+$name) 524288}
 }
 Save-Document 'README.txt' "Astral Personal Stats diagnostics`r`nDefault collection includes only mod logs, selected status fields and plugin file versions. No database, replay, settings, account IDs or authentication payloads are collected intentionally.`r`nLog text redaction is best-effort. Review before sharing; use a private channel, especially when game logs were included.`r`nRuntime logs describe this mod's own work. Process memory includes the whole game and other mods. A native crash may terminate before the final log is flushed."
 Add-Type -AssemblyName System.IO.Compression.FileSystem
 $zip=Join-Path $destRoot ('AstralStats-Diagnostics-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,6)+'.zip')
 [IO.Compression.ZipFile]::CreateFromDirectory($stage,$zip)
 Write-Output ('Diagnostics saved: '+$zip)
}finally{
 $null=Assert-ChildPath $destRoot $stage;Remove-OwnedFiles $stage $files;Remove-EmptyTree $stage
}
