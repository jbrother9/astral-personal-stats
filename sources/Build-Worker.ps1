$ErrorActionPreference='Stop'
python -m pip install pyinstaller==6.22.3
if($LASTEXITCODE -ne 0){throw 'PyInstaller installation failed'}
$buildArgs=@('--noconfirm','--clean','--onedir','--console','--noupx','--name','AstralStatsWorker','--distpath',(Join-Path $PSScriptRoot 'build\dist'),'--workpath',(Join-Path $PSScriptRoot 'build\work'),'--specpath',(Join-Path $PSScriptRoot 'build'),'--paths',(Join-Path $PSScriptRoot 'worker'))
# Some Python distributions require an adjacent certificate policy file during SSLContext creation.
# Bundle it unchanged when supplied by that runtime; certificate verification remains enabled.
$certificate=python -c "import ssl,pathlib;print(pathlib.Path(ssl.__file__).parent/'codex-rejected-cas.pem')"
if($LASTEXITCODE -ne 0){throw 'Unable to inspect Python TLS runtime'}
if(Test-Path -LiteralPath $certificate -PathType Leaf){$buildArgs+=@('--add-data',($certificate+';.'))}
$buildArgs+=(Join-Path $PSScriptRoot 'worker\astral_worker_main.py')
python -m PyInstaller @buildArgs
if($LASTEXITCODE -ne 0){throw 'Worker build failed'}
