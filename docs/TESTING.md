# 测试说明

在 Windows x64 的仓库根目录，用 Windows PowerShell 执行：

```powershell
.\tests\run-policy-tests.ps1
.\tests\run-tests.ps1
.\tests\run-ui-tests.ps1
.\tests\run-appearance-tests.ps1
.\tests\run-lifecycle-tests.ps1
.\tests\run-revision-tests.ps1
.\tests\run-icon-tests.ps1 -ProjectRoot $PWD.Path -WorkPath (Join-Path $env:TEMP ("TrayLeaf-icon-" + [guid]::NewGuid())) -ExpectedIco (Join-Path $PWD.Path 'assets\TrayLeaf.ico') -ExpectedPng (Join-Path $PWD.Path 'assets\TrayLeaf.png')
```

| 组别 | 检查数 |
|---|---:|
| 策略 | 11 |
| 受控集成 | 31 |
| 界面 | 16 |
| 字体 / DPI | 27 |
| 托盘生命周期 | 7 |
| 界面与程序移除 | 27 |
| 图标 | 18 |
| 合计 | 137 |

脚本默认使用新的临时目录；图标测试显式指定临时目录。集成测试只结束自己创建的 ControlledFixture，不选择或结束用户真实进程；UI/字体/移除/图标使用示例数据库。生命周期测试会只读扫描真实进程，不执行实际关闭/恢复。

测试以普通权限运行，与正式 EXE 的管理员清单隔离。通过测试不等于完成真实 UAC 提权、所有第三方托盘兼容或真实高分辨率多屏验收。

2026-10-16 发布检查：源码构建成功；以上七组共 **137 项通过，0 项失败**。

本版另核对界面显示、程序集版本、EXE 文件版本、产品版本与清单版本，共 5 项通过；已在隔离副本验证版本修改的精确回滚与重新应用。
