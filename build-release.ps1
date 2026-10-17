param([string]$OutputDir,[string]$PayloadDir,[string]$WixBin=$env:WIX_BIN)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
if(!$OutputDir){$OutputDir=Join-Path $root 'release'}
$OutputDir=[IO.Path]::GetFullPath($OutputDir)
[IO.Directory]::CreateDirectory($OutputDir) | Out-Null
$stage=Join-Path ([IO.Path]::GetTempPath()) ('TrayLeaf-package-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($stage) | Out-Null
try {
 if(!$PayloadDir){
  & (Join-Path $root 'build.ps1') -OutputPath (Join-Path $stage 'TrayLeaf.exe')
 }else{
  $PayloadDir=[IO.Path]::GetFullPath($PayloadDir)
  foreach($name in @('TrayLeaf.exe','TrayLeaf.exe.config')){
   Copy-Item -LiteralPath (Join-Path $PayloadDir $name) -Destination (Join-Path $stage $name)
  }
 }
 Copy-Item -LiteralPath (Join-Path $root 'installer\WiX-LICENSE.txt') -Destination (Join-Path $stage 'WiX-LICENSE.txt')
 $version=[Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $stage 'TrayLeaf.exe'))
 $v=$version.ProductVersion
 if($v -notmatch '^\d+\.\d+\.\d+$'){throw "Invalid product version: $v"}
 $folder='TrayLeaf-v'+$v+'-Windows-x64'
 $files=@('TrayLeaf.exe','TrayLeaf.exe.config','WiX-LICENSE.txt')
 Add-Type -AssemblyName System.IO.Compression
 Add-Type -AssemblyName System.IO.Compression.FileSystem
 $temporaryZip=Join-Path $stage 'package.zip'
 $zip=[IO.Compression.ZipFile]::Open($temporaryZip,[IO.Compression.ZipArchiveMode]::Create)
 try {
  foreach($name in $files){
   [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,(Join-Path $stage $name),($folder+'/'+$name),[IO.Compression.CompressionLevel]::Optimal) | Out-Null
  }
 }finally{$zip.Dispose()}
 $destination=Join-Path $OutputDir $folder
 [IO.Directory]::CreateDirectory($destination) | Out-Null
 foreach($name in $files){Copy-Item -LiteralPath (Join-Path $stage $name) -Destination (Join-Path $destination $name) -Force}
 # Remove only the retired documentation files left by earlier packages.
 foreach($name in @('使用说明.txt','更新说明.txt','使用教程.txt','更新日志.txt')){
  $retired=Join-Path $destination $name
  if(Test-Path -LiteralPath $retired -PathType Leaf){Remove-Item -LiteralPath $retired -Force}
 }
 $archive=Join-Path $OutputDir ($folder+'.zip')
 Copy-Item -LiteralPath $temporaryZip -Destination $archive -Force
 $sha=[Security.Cryptography.SHA256]::Create()
 $stream=[IO.File]::OpenRead($archive)
 try {$hash=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','')}finally{$stream.Dispose();$sha.Dispose()}
 [IO.File]::WriteAllText($archive+'.sha256',$hash+'  '+[IO.Path]::GetFileName($archive)+[Environment]::NewLine,[Text.Encoding]::ASCII)
 Write-Output "ARCHIVE=$archive"
 Write-Output "SHA256=$hash"
 if($WixBin){& (Join-Path $root 'installer\build-installer.ps1') -WixBin $WixBin -OutputDir $OutputDir -PayloadDir $stage}
}finally{
 $resolved=[IO.Path]::GetFullPath($stage)
 $temp=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
 if(!$resolved.StartsWith($temp,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($resolved) -notlike 'TrayLeaf-package-*'){throw 'Unexpected staging path; cleanup stopped.'}
 Remove-Item -LiteralPath $resolved -Recurse -Force
}