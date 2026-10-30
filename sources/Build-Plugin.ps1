param([string]$GameRoot='C:\Program Files (x86)\Steam\steamapps\common\Astral Party\8vJXnINT')
$ErrorActionPreference='Stop'
$taskRefs=New-Object 'System.Collections.Generic.List[string]'
foreach($taskDir in @((Join-Path $GameRoot 'dotnet'),(Join-Path $GameRoot 'BepInEx\core'),(Join-Path $GameRoot 'BepInEx\interop'))) {
 foreach($taskFile in Get-ChildItem -LiteralPath $taskDir -Filter '*.dll') {
  try { $null=[Reflection.AssemblyName]::GetAssemblyName($taskFile.FullName); $taskRefs.Add($taskFile.FullName) } catch {}
 }
}
$taskCompilerRefs=@((Join-Path $PSHOME 'Microsoft.CodeAnalysis.dll'),(Join-Path $PSHOME 'Microsoft.CodeAnalysis.CSharp.dll'),(Join-Path $PSHOME 'System.Collections.Immutable.dll'),(Join-Path $PSHOME 'ref\System.Runtime.dll'),(Join-Path $PSHOME 'ref\System.Collections.dll'),(Join-Path $PSHOME 'ref\System.Collections.Immutable.dll'))
Add-Type -WarningAction SilentlyContinue -IgnoreWarnings -ReferencedAssemblies $taskCompilerRefs -TypeDefinition @"
using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
public static class PersonalPluginCompiler {
 public static string Build(string source,string output,string[] paths) {
  var references=new List<MetadataReference>();
  foreach(string path in paths)references.Add(MetadataReference.CreateFromFile(path));
  var options=new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,optimizationLevel:OptimizationLevel.Release);
  var compilation=CSharpCompilation.Create("AstralPersonalCollector",Array.ConvertAll(Directory.GetFiles(source,"*.cs"), path=>CSharpSyntaxTree.ParseText(File.ReadAllText(path))),references,options);
  var result=compilation.Emit(output);
  if(result.Success)return "Build succeeded";
  var errors=new System.Text.StringBuilder();foreach(var diagnostic in result.Diagnostics)errors.AppendLine(diagnostic.ToString());
  throw new Exception(errors.ToString());
 }
}
"@
[PersonalPluginCompiler]::Build((Join-Path $PSScriptRoot 'plugin'),(Join-Path $PSScriptRoot 'AstralPersonalCollector.dll'),$taskRefs.ToArray())


