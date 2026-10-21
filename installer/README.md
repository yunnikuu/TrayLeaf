# 轻羽归栈 v0.8.2 安装器

使用 WiX v3.14.x candle / light 构建，详见仓库 README。固定 UpgradeCode：{49F5C6BF-41D9-5CF1-8FFA-917E45CCB2DC}，提高版本时须生成新 ProductCode。

当前用户安装，默认 `%LOCALAPPDATA%\Programs\TrayLeaf`。AppSearch 读取 HKCU\Software\TrayLeaf\Installer 的 InstallPath 识别已有位置。升级移除旧版本，保留用户数据库。维护向导修复时选择 REINSTALLMODE=amus，以覆盖同版文件。

三个可选项：桌面快捷方式、开始菜单、开机自启动。配置启动的应用专用入口不打开主界面；管理员计划任务建立需要 Windows 权限确认，取消则保留 Run 入口。卸载会尝试清理自启动，但保留用户偏好和数据库。升级移除旧包时不提前删除正在沿用的计划任务。

Wix ICE91 因明确 per-user 目录设计而抑制；ICE61 因允许同版本替换而抑制。其余 ICE 正常验证。发布载荷只含 TrayLeaf.exe / TrayLeaf.config / WiX-LICENSE.txt。

已验证构建、表属性、升级关系、可选组件条件和行政解包。未在用户系统执行真实安装、升级、卸载或计划任务注册。
