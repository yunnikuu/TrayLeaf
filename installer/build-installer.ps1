<<<<<<< HEAD
﻿param([string]$WixBin=$env:WIX_BIN,[string]$OutputDir,[string]$PayloadDir)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$workspace=(& (Join-Path $root 'resolve-project.ps1') -Json | ConvertFrom-Json).WorkspaceRoot
if(!$WixBin){throw '请安装/解压 WiX Toolset v3.14.1，并通过 -WixBin 指定 candle.exe 与 light.exe 所在目录。'}
$WixBin=[IO.Path]::GetFullPath($WixBin)
$build=Join-Path $workspace 'work\installer-build'
[IO.Directory]::CreateDirectory($build) | Out-Null
if(!$PayloadDir){
 $PayloadDir=Join-Path $build 'payload'
 [IO.Directory]::CreateDirectory($PayloadDir) | Out-Null
 & (Join-Path $root 'build.ps1') -OutputPath (Join-Path $PayloadDir 'TrayLeaf.exe')
 Copy-Item -LiteralPath (Join-Path $root 'TrayLeaf.config') -Destination $PayloadDir
}
$PayloadDir=[IO.Path]::GetFullPath($PayloadDir)
$v=[Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $PayloadDir 'TrayLeaf.exe'))
if(!$OutputDir){$OutputDir=Join-Path $workspace ('TrayLeaf\releases\github'+$v.ProductVersion+'\release')}
$OutputDir=[IO.Path]::GetFullPath($OutputDir)
[IO.Directory]::CreateDirectory($OutputDir) | Out-Null
[xml]$definition=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'TrayLeaf.wxs') -Raw -Encoding UTF8
$productVersion=[string]$definition.Wix.Product.Version
if($v.ProductVersion -ne $productVersion -or $v.FileVersion -ne ($productVersion+'.0')){throw '安装模板与程序载荷的版本必须一致。'}
$fileName='TrayLeaf-v'+$productVersion+'-Windows-x64-Setup.msi'
$object=Join-Path $build 'TrayLeaf.wixobj';$msi=Join-Path $OutputDir $fileName
$builtMsi=Join-Path $build $fileName
Push-Location $PSScriptRoot
try {
 & (Join-Path $WixBin 'candle.exe') -nologo -arch x64 "-dPayloadDir=$PayloadDir" -out $object 'TrayLeaf.wxs'
 if($LASTEXITCODE -ne 0){throw "candle failed: $LASTEXITCODE"}
 # ICE91: per-user package. ICE61: intentional same-version upgrade/rebuild support.
 & (Join-Path $WixBin 'light.exe') -nologo -ext WixUIExtension -cultures:zh-cn -sice:ICE91 -sice:ICE61 -pdbout (Join-Path $build 'TrayLeaf.wixpdb') -out $builtMsi $object
 if($LASTEXITCODE -ne 0){throw "light failed: $LASTEXITCODE"}
}finally{Pop-Location}
Copy-Item -LiteralPath $builtMsi -Destination $msi -Force
Write-Output "INSTALLER=$msi"
=======
param([string]$WixBin=$env:WIX_BIN,[string]$OutputDir,[string]$PayloadDir)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
if(!$WixBin){throw '请安装/解压 WiX Toolset v3.14.1，并通过 -WixBin 指定 candle.exe 与 light.exe 所在目录。'}
$WixBin=[IO.Path]::GetFullPath($WixBin)
if(!$OutputDir){$OutputDir=Join-Path $root 'release'}
$OutputDir=[IO.Path]::GetFullPath($OutputDir)
$build=Join-Path $OutputDir 'installer-build'
[IO.Directory]::CreateDirectory($build) | Out-Null
if(!$PayloadDir){
 $PayloadDir=Join-Path $build 'payload'
 [IO.Directory]::CreateDirectory($PayloadDir) | Out-Null
 & (Join-Path $root 'build.ps1') -OutputPath (Join-Path $PayloadDir 'TrayLeaf.exe')
 Copy-Item -LiteralPath (Join-Path $root 'TrayLeaf.config') -Destination $PayloadDir
}
$PayloadDir=[IO.Path]::GetFullPath($PayloadDir)
$v=[Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $PayloadDir 'TrayLeaf.exe'))
if($v.FileVersion -ne '0.8.5.0' -or $v.ProductVersion -ne '0.8.5'){throw '安装载荷版本必须为 0.8.5。'}
$object=Join-Path $build 'TrayLeaf.wixobj';$msi=Join-Path $OutputDir 'TrayLeaf-v0.8.5-Windows-x64-Setup.msi'
Push-Location $PSScriptRoot
try {
 & (Join-Path $WixBin 'candle.exe') -nologo -arch x64 "-dPayloadDir=$PayloadDir" -out $object 'TrayLeaf.wxs'
 if($LASTEXITCODE -ne 0){throw "candle failed: $LASTEXITCODE"}
 # ICE91: per-user package. ICE61: intentional same-version upgrade/rebuild support.
 & (Join-Path $WixBin 'light.exe') -nologo -ext WixUIExtension -cultures:zh-cn -sice:ICE91 -sice:ICE61 -out $msi $object
 if($LASTEXITCODE -ne 0){throw "light failed: $LASTEXITCODE"}
}finally{Pop-Location}
$sha=[Security.Cryptography.SHA256]::Create()
 $stream=[IO.File]::OpenRead($msi)
 try {$hash=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','')}finally{$stream.Dispose();$sha.Dispose()}
[IO.File]::WriteAllText($msi+'.sha256',$hash+'  '+[IO.Path]::GetFileName($msi)+[Environment]::NewLine,[Text.Encoding]::ASCII)
Write-Output "INSTALLER=$msi"
Write-Output "SHA256=$hash"
>>>>>>> origin/main
