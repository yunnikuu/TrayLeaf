# TrayLeaf v0.2.1 中文安装器

## 构建

依赖：Windows x64、Windows PowerShell 5.1、.NET Framework 4.8，以及官方 **WiX Toolset v3.14.1** 二进制工具包（`wix314-binaries.zip`，解压即可）。无需安装 .NET SDK 或 NuGet 包。请从 wixtoolset/wix3 官方 GitHub Releases 获取该工具链；不要把工具链提交进本项目。

在仓库根目录运行，以下路径请按本机实际解压位置修改：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\installer\build-installer.ps1 -WixBin C:\Tools\wix314
```

默认先调用项目的 `build.ps1` 编译应用，再生成说明文本与 MSI。输出位于 `release/TrayLeaf-v0.2.1-Windows-x64-Setup.msi`，并生成 `.sha256`；中间产物位于 `release/installer-build`。可用 `-OutputDir` 改输出目录。

已有经核验的 v0.2.1 便携载荷时，可指定 `-PayloadDir C:\Package\TrayLeaf`，其中必须有 `TrayLeaf.exe`、`TrayLeaf.exe.config`、`使用说明.txt`、`更新说明.txt`。此方式不重新编译 EXE。脚本检查文件/产品版本后封装，正式发布采用此方式，保持原 EXE 字节不变。`WiX-LICENSE.txt` 由安装器源码目录提供。

## 安装行为

- 仅当前用户，默认 `%LOCALAPPDATA%\Programs\TrayLeaf`；支持自选安装目录。
- 中文安装与维护向导，开始菜单快捷方式、可选桌面快捷方式；安装结束不自动启动。
- 应用运行仍请求管理员权限；不增加服务、自启动项或后台安装代理。
- 卸载保留应用数据目录 `%LOCALAPPDATA%\TrayLeaf`。
- MSI ProductVersion=`0.2.1`；ProductCode=`{2A9E4A72-61FB-41B1-866B-15EC6D1A7BA0}`；UpgradeCode=`{49F5C6BF-41D9-5CF1-8FFA-917E45CCB2DC}`。
- 当前 ProductCode 仅属于这一版；以后提高版本时必须生成新的 ProductCode，保持 UpgradeCode 稳定。同版包内容变化请先卸载旧包，不支持同版 major upgrade。
- 已记录的安装路径会在修复/卸载时通过 HKCU 读取，避免自选目录卸载时误用默认路径。
- Windows 版本条件从注册表 `CurrentBuildNumber` 读取真实构建号，避免 MSI 的旧兼容版本属性误判。

## 验证与注意事项

构建正常执行 WiX ICE 验证；仅抑制 **ICE91**，因为本包明确为 per-user 且不支持 ALLUSERS，用户目录正是设计目标。没有跳过其余 ICE 验证。

详细测试结果见 `docs/TESTING.md`。包未进行代码签名，构建成功不代表通过安全软件信誉认证；签名私钥不应提交仓库。

WiX UI 组件许可保留在 `WiX-LICENSE.txt` 中，安装时也随包附带。该文件不是 TrayLeaf 项目本身的开源授权；项目许可证仍需维护者自行确定。
