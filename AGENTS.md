# AGENTS.md — DXCXN 项目记忆（D&B 指令注入器）

ダンジョン＆ブライド 指令注入器：经 krkrz 引擎插件（TJS 表达式）修改游戏对象与状态，逐条经游戏源码核实，避免非法值导致游戏报错退出或拼接错误。

## 架构

- **仓库**：本仓库根目录；**游戏目录**：game64.exe 所在目录（需在引导向导中选择）。
- **通信**：命名管道 `\\.\pipe\tbc_bridge`。插件 `tb_bridge.tpm`（= `bridge\bin\tb_bridge.dll`，krkrz 官方 V2Link 插件机制）由 DXCXN 部署到游戏 `plugin\` 目录，游戏主线程内执行 TJS 并返回结果。与 MTool krkrConsole 同级。
- **执行通道**：`TVPExecuteExpression` 执行任意 TJS **单表达式**；多语句用 `(function(){ ... })()` 匿名函数包裹（不支持顶层 for/if）。
- **双语下拉**：插件 `LIST` 命令用 C++ `EnumMembers` 枚举注册表 `o`（1447 项：键=日文标识符，name=汉化运行时名，id=物品ID）。
- **全局对象**：`game`=GameMaster；`o`=全局注册表；`sf`=跨存档系统标志（存 datasu.ksd，收集组写它）；`o.<角色>` 是 CharaReference（对活体实例透明代理）。
- **游戏文件零修改**：仅新增 `plugin\tb_bridge.tpm`，删除即还原。

## 连接就绪（关键坑，已修复 e5f9a18）

- **PING 就绪 ≠ 引擎就绪**：插件管道线程在 V2Link 时就绪（PING 立即回 PONG），但 LIST/EVAL 需游戏脚本初始化完成（`o` 注册表、`game` 对象存在）。冷启动过早 LIST 会报 `global 'o' not found`，且游戏启动期会 V2Link/V2Unlink 自重启、管道瞬断。
- **就绪标志**：`game.items.count > 0`（与 test_ui.ps1 / `RefreshStatusInfo` 一致）。`TrainerApp.cs` 里 `EngineReady()` 以此判定；`InitGameFlow` 等 `PING && EngineReady()` 才加载注册表，`RefreshStatus` 对"已连接但注册表为空"做 5s 节流自动补载。
- **调试首选**：游戏目录 `tbc_bridge.log`（插件写入每个 PING/LIST/EVAL 及错误原文），冷启动/连接问题先看它，再配合 `%APPDATA%\DzbTrainer\debug.log`（DXCXN 调试日志，需 config.debug=true）。

## 构建 / 测试（顺序重要）

```bash
# 1) 桥接 DLL（zig cc，先于 DXCXN，否则报 "run bridge/build.ps1 first"）
powershell -ExecutionPolicy Bypass -File bridge/build.ps1
# 2) DXCXN.exe（.NET Framework 4 的 csc.exe 编译 7 个 .cs，代码构建 WPF UI，内嵌 tb_bridge.dll 为资源）
powershell -ExecutionPolicy Bypass -File trainer/build.ps1   # 输出 OK: DXCXN.exe <bytes> bytes (plugin embedded)
# 3) 回归（UIAutomation 驱动 UI，自启游戏；17 个断言）
cd trainer && powershell -ExecutionPolicy Bypass -File test_ui.ps1 2>&1 | iconv -f GBK -t UTF-8 | grep -E "FAIL|汇总"   # 期望 === 汇总: 17/17 通过 ===
```

- **真机验证**：`tools\TbcCli.exe "<tjs表达式>"`，需游戏在运行（未连接报 `cannot open pipe (err 2)`）。
- **重启游戏**：`%TEMP%\opencode\restart_game_bom.ps1`（本地开发辅助脚本），等 `game.chara.count` 返回数值即就绪。

## 编码 / 换行坑

- Windows 控制台输出是 **GBK**，读中文日志必须 `iconv -f GBK -t UTF-8`；仓库文本 UTF-8；游戏脚本 `unpacked\patch\*.ks` 是 **UTF-16 LE**（读前转换）。
- git 提交报 LF→CRLF 警告属正常，忽略。

## 关键源码事实

- **角色**（`model.tsv` 明文）：リム(人間Ａ)/クレア(エルフＡ)/フレデリカ(ドワーフＡ)/ミューズ(ノームＡ)/マルエット(シルフＡ)/リーゼル(人間Ｂ,女性)/テオ(人間Ｃ)/マックス(エルフＢ)。
- **GuestObject（次要人物，无背包）**：ポラリス/サンドラ/マリア/ブルー/ライナス/ミレディ，经 `game.guest.entry(o.xxx)` 入队。
- **ORoleKeys 是日文键**：`ライナス`/`アレックス`（不是小写 `linus`/`alex`，`o.linus` 不存在）。
- **主力可操控（有背包）**：テオ/マックス/リーゼル（PartyKeys）。
- **dressInfo**：`o.XXX.dressInfo[...]`=model 的 dressInfo；`o["<key>"]=%[...]`=全局通用立绘 spec。GuestObject 默认 dressInfo 键：`通常/戦闘/水着/バスタオル/上半身裸/下着/裸/汗だく`（部分 img 无 face）。
- `sf.usedJobsByCreating` 用数组字面量 `[]`（不是 `%[]`）；该引擎无 `Debug.console` 成员。

## 立绘状态（strip）模块 — 已修复（04a6d26）

两条渲染路径决定"合法值"：

1. **CharaObject** `createStrippedFigure`：`strip[key]` → `model.dressInfo[key]` → 回退 `o[key]`；`info.img` 存在走 `createFigure(info.face,...)`，否则 `info.clothes.split(",")` 拼装。**崩溃点**：`o[key]` 为 `ItemObject`（有 `img` 无 `face` 成员）时取 `face` 抛 `Member 'face' does not exist`。
2. **GuestObject** `loadHelperStrippedFigure`：只查自身 `dressInfo[key]`，`face` 可缺省。

**"只有头"错误**：`large:true` 的「・大」立绘（`lg_` 大图）走正常尺寸管线，只落入头部区域。

**合法值（游戏实际写入）**：

| 类型 | 角色 | 合法立绘状态值 |
|---|---|---|
| 女主 CharaObject | リム/クレア/フレデリカ/ミューズ/マルエット | 通常、下着、パンツ、裸、裸・幸せ、裸・不満、裸・恥辱、汗だく、妊娠、ボテ腹 |
| 女主 CharaObject | リーゼル | 上述 + パンツ・幸せ |
| GuestObject | サンドラ/マリア/ポラリス/ミレディ 等 | 通常、下着、裸、汗だく、戦闘、水着、バスタオル、上半身裸（8 键） |
| 男主 CharaObject | テオ/マックス | 通常、上半身裸、下着（游戏从不写男主 strip） |

**实现要点**：`StripCandidates` 收窄为 15 个真实值（女主 11 + 客人 8 并集）；`QueryValidStrips` 女主路径排除 `ItemObject` 与 `large` 规格、客人路径只认自身 `dressInfo` 键。类型判别用 `instanceof "CharaObject"` / `"GuestObject"`。

## 界面配色（中性灰底 + 灰紫按钮，用户选定）

token 常量在 `TrainerApp.cs`（BgMain=#333333 / Accent=#785E5E / TextMain=#EDEDED 等）。XAML 模板注入 `{C_INPUT}/{C_BORDER}/{C_DIM}` 用 `.Replace()`（**不能**用 string.Format，会误替换 `{0}`）。导航选中态：局部设 Background/Border，未选中 `ClearValue` 回退样式。状态色 OkGreen/WarnAmber/ErrRed 为语义色，勿改。

## 约定

- **提交**：中文消息，前缀 `fix:/feat:/refactor:/docs:/style:/audit:/chore:`，分原子提交，每批构建+17/17 回归后再提交。**不主动 commit/push**（除非明确要求）。
- **配置**：`%APPDATA%\DzbTrainer\config.json`（gameDir/debug/autoLaunchGame），删除即重新引导。
- **存档安全**：修改均为运行时内存/对象修改，读档恢复；「图鉴」组写 `sf` 跨存档持久，全奖杯会开二周目。
- **残留**：`.omo/`（旧 drafts/evidence）长期未处理，`git status` 注意；`ref/`、`shots*/` 已 gitignore。
- 权威源码参考在 `%TEMP%\opencode\p66_*.txt`（patch66 UTF-8 转换）与游戏 `unpacked\patch\`。
