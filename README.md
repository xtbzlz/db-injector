# D&B 指令注入器 (bride-injector)

ダンジョン＆ブライド（Dungeon & Bride）指令注入器：通过 krkrz 引擎插件（TJS 表达式注入）修改游戏对象与状态，全部指令逐条经游戏源码核实，避免非法值导致游戏报错退出或拼接错误。

## 功能

- **图形化指令拼接**：14 个分组 / 80+ 条指令，全部经游戏源码核实
- **双语下拉**：角色/物品/技能/魔法/地图等参数实时从游戏读取，中文（日文）双语显示
- **TJS 控制台**：输入任意 TJS 表达式直接执行（与 MTool krkrConsole 同级）
- **单文件分发**：整个修改器只有一个 exe，插件内嵌，自动部署
- 指令覆盖：等级经验 / 六维属性 / 技能魔法 / 物品 / 时间 / 状态异常 / 结婚 / 队伍 / 地图 / 男主数据 / 后宫数值 / 性记录 / 战斗恢复 / 图鉴

## 使用

1. 下载 `DXCXN.exe`（发行版 Releases 中获取），双击运行
2. 首次运行选择游戏目录（`game64.exe`），程序自动：部署插件 → 启动游戏 → 连接
3. 游戏重启后修改器自动重连；配置保存在 `%APPDATA%\DzbTrainer\config.json`

> 仅支持 Windows 7 SP1+（含 10/11），需 .NET Framework 4.0+（Win8+ 自带 4.8），无需管理员权限。
> 杀软如拦截 exe 或 `tb_bridge.tpm`，请加入白名单（本地工具，无网络行为）。

## 原理

- 命名管道 `\\.\pipe\tbc_bridge` 与游戏内插件通信
- 插件 `tb_bridge.tpm`（krkrz 官方 V2Link 插件机制）在游戏主线程内执行 TJS 代码并返回结果
- **游戏文件零修改**：仅新增 `plugin\tb_bridge.tpm` 一个文件，删除即完全还原
- 存档安全：修改均为运行时内存修改，读档可恢复

## 开发者构建

```bash
# 1) 桥接插件 DLL（zig cc）
powershell -ExecutionPolicy Bypass -File bridge/build.ps1
# 2) 主程序（.NET Framework 4 csc.exe，代码构建 WPF UI，内嵌插件为资源）
powershell -ExecutionPolicy Bypass -File trainer/build.ps1
# 3) UI 回归（UIAutomation，需游戏；期望 17/17）
cd trainer && powershell -ExecutionPolicy Bypass -File test_ui.ps1
```

## 免责声明

- 本工具仅供学习交流，请勿用于商业用途
- 游戏本体及汉化数据版权归原作者/发行方所有，本仓库不含任何游戏资源
- 使用本工具造成的一切后果由使用者自行承担

## License

[MIT](LICENSE)
