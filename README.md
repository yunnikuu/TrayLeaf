# 托盘管家 TrayLeaf

**游戏前关闭选定后台，结束后按需重新启动。**

Windows 托盘工具，采用 C#、WPF 与系统 SQLite；默认请求管理员权限。没有浏览器内核，不持续轮询进程。

![主界面（示例数据）](docs/images/main.png)

## 功能

- 默认识别托盘 / 任务栏可见程序，也可切换全部进程。
- 勾选后批量关闭；记录程序名称、路径、启动参数，按需重新启动。
- 每个程序可单独选择恢复时是否提权。
- 支持手动添加 EXE、鼠标定位目标窗口和识别进程。
- 支持移除不想显示的程序，并在设置中恢复显示。
- 常驻托盘；默认 **Ctrl+Alt+1** 关闭、**Ctrl+Alt+2** 恢复，可自定义或设为同一组合键。
- 系统/服务进程风险识别与确认提示，包括 svchost；可在设置中关闭风险提醒，但不会绕过系统保护。
- SQLite 保存设置与恢复记录；默认微软雅黑，可选择字体；支持 Per-Monitor V2 显示缩放。
- 统一蓝色叶片图标用于 EXE、窗口/任务栏、托盘和界面标识。

## 下载与使用

在本仓库的 **Releases** 页面下载 `TrayLeaf-v0.1.0-Windows-x64.zip`，完整解压后运行 `TrayLeaf.exe`，保留同目录 `TrayLeaf.exe.config`。这是免安装压缩包，不是安装程序；Windows UAC 会请求管理员权限。

- Windows 10/11 **x64**。
- .NET Framework **4.8 或更高的 4.x 就地版本**，以及系统提供的 `winsqlite3.dll`。
- 正式 EXE 未进行代码签名；若出现系统安全提示，请审阅源码并自行判断，不要关闭系统防护。

> **先保存工作。** 关闭进程可能丢失未保存数据；恢复仅重新启动程序，不恢复文档、登录会话、窗口位置或内存状态。误关系统进程可能导致注销、黑屏或系统崩溃。不建议关闭系统/服务进程。

详细使用与行为边界：[使用说明](docs/USAGE.md)。

## 从源码构建

仓库使用 Windows 自带的 .NET Framework C# 编译器；当前没有 `.sln` / `.csproj`，不需要安装 .NET SDK 或恢复 NuGet 包。代码兼容 C# 5。

在仓库根目录打开 **Windows PowerShell**：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

生成 `TrayLeaf.exe`，与仓库中的 `TrayLeaf.exe.config` 放在同一目录即可运行。构建会嵌入图标与 XAML；运行时不依赖旁置图片。请不要把生成的 EXE 或数据库提交到源码仓库。

## 目录

```text
src/                     C# 源码与 WPF XAML
assets/                  多尺寸 ICO 与透明 PNG
tests/                   受控测试与 PowerShell runner
docs/                    使用说明、测试说明与示例截图
build.ps1                构建脚本
app.manifest             管理员权限与 DPI 声明
TrayLeaf.exe.config      .NET 运行时配置
CHANGELOG.md             版本说明
```

## 测试与已知边界

发布前已从此源码副本成功构建，并执行七组检查：**137 项通过，0 项失败**。测试命令与验证边界见 [测试说明](docs/TESTING.md)。

- 测试不代表全部第三方托盘实现兼容。
- 没有真实 2K / 4K 多屏硬件切换验收，也未验证 UAC 同意/取消等所有权限场景。
- 不承诺固定内存占用或固定可释放内存；不自动清理内存或修改系统服务配置。
- 数据位于 `%LOCALAPPDATA%\TrayLeaf\trayleaf.db`，**未加密**，可能含敏感启动参数；不要上传个人数据库或错误日志。

![设置（示例数据）](docs/images/settings.png)
