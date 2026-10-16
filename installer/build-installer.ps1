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
 Copy-Item -LiteralPath (Join-Path $root 'TrayLeaf.exe.config') -Destination $PayloadDir
 Copy-Item -LiteralPath (Join-Path $root 'docs\USAGE.md') -Destination (Join-Path $PayloadDir '使用说明.txt')
 Copy-Item -LiteralPath (Join-Path $root 'CHANGELOG.md') -Destination (Join-Path $PayloadDir '更新说明.txt')
}
$PayloadDir=[IO.Path]::GetFullPath($PayloadDir)
$v=[Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $PayloadDir 'TrayLeaf.exe'))
if($v.FileVersion -ne '0.2.1.0' -or $v.ProductVersion -ne '0.2.1'){throw '安装载荷版本必须为 0.2.1。'}
$object=Join-Path $build 'TrayLeaf.wixobj';$msi=Join-Path $OutputDir 'TrayLeaf-v0.2.1-Windows-x64-Setup.msi'
Push-Location $PSScriptRoot
try {
 & (Join-Path $WixBin 'candle.exe') -nologo -arch x64 "-dPayloadDir=$PayloadDir" -out $object 'TrayLeaf.wxs'
 if($LASTEXITCODE -ne 0){throw "candle failed: $LASTEXITCODE"}
 # ICE91 is intentionally suppressed: this is exclusively a per-user package, never an all-users install.
 & (Join-Path $WixBin 'light.exe') -nologo -ext WixUIExtension -cultures:zh-cn -sice:ICE91 -out $msi $object
 if($LASTEXITCODE -ne 0){throw "light failed: $LASTEXITCODE"}
}finally{Pop-Location}
$hash=(Get-FileHash -LiteralPath $msi -Algorithm SHA256).Hash
[IO.File]::WriteAllText($msi+'.sha256',$hash+'  '+[IO.Path]::GetFileName($msi)+[Environment]::NewLine,[Text.Encoding]::ASCII)
Write-Output "INSTALLER=$msi"
Write-Output "SHA256=$hash"
