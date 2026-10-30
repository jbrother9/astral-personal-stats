$ErrorActionPreference='Stop'
python -m pip install pyinstaller==6.22.3
python -m PyInstaller --noconfirm --clean --onedir --console --noupx --name AstralStatsWorker --distpath (Join-Path $PSScriptRoot 'build\dist') --workpath (Join-Path $PSScriptRoot 'build\work') --specpath (Join-Path $PSScriptRoot 'build') --paths (Join-Path $PSScriptRoot 'worker') (Join-Path $PSScriptRoot 'worker\astral_worker_main.py')
if($LASTEXITCODE -ne 0){throw 'Worker build failed'}
