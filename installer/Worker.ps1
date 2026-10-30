param([ValidateSet('Install','Uninstall')][string]$Mode='Install',[Parameter(Mandatory=$true)][string]$GameRoot,[Parameter(Mandatory=$true)][string]$UserDataRoot,[Parameter(Mandatory=$true)][string]$ResultPath)
$ErrorActionPreference='Stop'
try{
 if($Mode -eq 'Install'){$message=& (Join-Path $PSScriptRoot 'Install.ps1') -GameRoot $GameRoot -UserDataRoot $UserDataRoot}else{$message=& (Join-Path $PSScriptRoot 'Uninstall.ps1') -GameRoot $GameRoot}
 @{ok=$true;message=($message -join "`r`n")}|ConvertTo-Json|Set-Content -LiteralPath $ResultPath -Encoding utf8;exit 0
}catch{@{ok=$false;message=$_.Exception.Message}|ConvertTo-Json|Set-Content -LiteralPath $ResultPath -Encoding utf8;exit 1}
