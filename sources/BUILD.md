# Rebuilding

End users do not need Python or a compiler. The supplied binaries are Windows x64.

Worker: use Windows x64 CPython 3.12 and run Build-Worker.ps1. It installs pinned PyInstaller 6.22.3 into that developer environment and produces an onedir build. Only the fifteen Python modules under worker/ are used; no packet capture, SDK login, game credentials or personal records are bundled.

Plugin: after a compatible BepInEx IL2CPP installation has generated interop files, use PowerShell 7 to run Build-Plugin.ps1 -GameRoot <game executable folder>. It uses bundled Roslyn compiler assemblies from PowerShell and references that game's dotnet/BepInEx libraries; generated game libraries are not included in this package.

The installer checks release-manifest.json SHA-256 hashes. A rebuilt distribution must regenerate its manifest and ZIP checksums. Runtime personal settings/database/caches must never be placed into payload/.
