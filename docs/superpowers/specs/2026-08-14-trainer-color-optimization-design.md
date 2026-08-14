# D&B 修改器配色优化设计

**状态：** 已选定方向，待实施

**目标：** 将 Trainer 从当前低区分度的紫蓝灰界面调整为适合长期操作的石墨黑工作台，并以冷青色建立明确的操作、焦点与连接状态层级。

## 范围

本设计只覆盖 `trainer/src/TrainerApp.cs` 中的颜色 token、控件状态颜色和 ComboBox XAML 模板注入色。

不在范围内：窗口布局、控件尺寸、字体、交互流程、指令逻辑、页面结构、动画和新增依赖。

## 当前问题

当前色板的 `BgMain`、`BgPanel`、`BgInput`、`BgActive`、`BorderC` 均位于相近的紫蓝灰阶：

- 页面背景、面板、输入区和选中态明度差偏小，密集的指令页难以快速扫描。
- `BgActive` 同时承担按钮、导航选中、列表悬停等不同行为，语义不清。
- `Accent` 是高饱和紫色，和绿色/红色状态点没有统一的状态优先级。
- 下拉框选择态直接使用 Accent 背景，而文字仍使用浅色 `TextMain`，需要改为深冷色选中文字以保持对比度。

## 视觉方向

采用“石墨黑 + 冷青操作色”。整体保持桌面运维工具的克制感：中性深灰负责结构层级，低饱和蓝绿负责可操作状态，绿/黄/红仅表达运行状态和风险。

冷青不作为大面积页面背景，也不和紫色并存。它只用于焦点、当前选中、主要操作和连接状态，避免界面重新变成单一色相主题。

## 色彩 Token

| Token | 色值 | 用途 |
|---|---:|---|
| `BgMain` | `#111516` | 窗口主背景、页面滚动区 |
| `BgPanel` | `#192326` | 顶栏、日志区、面板与卡片表面 |
| `BgRaised` | `#213034` | 次级抬升表面、悬停容器 |
| `BgInput` | `#121C1E` | TextBox、ComboBox、预览和历史记录输入面 |
| `BgHover` | `#26373B` | 普通按钮、列表项、下拉选项悬停 |
| `BgSelected` | `#164D53` | 导航/列表当前选中、次级操作背景 |
| `Accent` | `#2AC7C9` | 焦点描边、执行强调、连接状态、选中指示 |
| `AccentHover` | `#54DCDA` | Accent 悬停或按下反馈 |
| `BorderC` | `#34484D` | 默认描边、控件边界、分区轮廓 |
| `BorderFocus` | `#2AC7C9` | 输入焦点、主操作强调描边 |
| `TextMain` | `#EDF6F4` | 标题、主文本、常规控件文字 |
| `TextDim` | `#A9BEC0` | 辅助说明、状态信息、日志文字 |
| `TextMuted` | `#72898C` | 非当前导航、低优先级元数据 |
| `TextOnAccent` | `#061B1D` | 亮 Accent 背景上的文字 |
| `OkGreen` | `#52C985` | 已连接、成功状态 |
| `WarnAmber` | `#E4B458` | 需重启、风险提示、非阻断告警 |
| `ErrRed` | `#E26B70` | 未连接、执行错误、失败状态 |

## 对比度与可读性约束

- `TextMain` 在 `BgMain`、`BgPanel`、`BgInput`、`BgSelected` 上须达到 WCAG AA 正文级可读性目标（至少 4.5:1）。
- `TextDim` 仅用于辅助文字，不能用于主要动作文字或可编辑字段的内容。
- `TextOnAccent` 只在 `Accent` / `AccentHover` 的实底按钮或选中项上使用，禁止继续沿用浅色文字。
- `BorderC` 必须能区分 `BgPanel` 与 `BgInput`，但不应比主要操作色更抢眼。
- 成功、警告、错误不复用 Accent：颜色本身表达语义，文字或图标仍需同时表达状态。

## 组件映射

### 窗口与页面

- `MainWindow.Background`、`ScrollViewer.Background`：`BgMain`。
- 顶栏、日志栏、面板/卡片：`BgPanel`。
- 指令页三列 Border：保持 `BgPanel`，默认边框改为 `BorderC`。
- 维护页、控制台页的留白不变；本轮不调整间距或布局。

### 输入与下拉框

- TextBox、ComboBox、只读预览、控制台历史：`BgInput` + `BorderC` + `TextMain`。
- ComboBox 下拉 Popup：`BgInput` + `BorderC`。
- 下拉项目悬停：`BgHover` + `TextMain`。
- 下拉项目选中：`Accent` + `TextOnAccent`。
- 下拉箭头、未选中辅助文本：`TextDim`。
- 焦点状态：控件描边切换至 `BorderFocus`，不改变整个页面背景。

### 按钮、导航与列表

- 普通顶栏按钮：`BgPanel`；悬停 `BgHover`；边框 `BorderC`。
- 执行/维护操作按钮（`FlatButton`）：默认 `BgSelected` + `TextMain`；悬停 `Accent` + `TextOnAccent`；按下使用 `AccentHover` + `TextOnAccent`。
- 顶栏导航当前项：`BgSelected` + `TextMain`；非当前项：透明 + `TextMuted`；悬停使用 `BgHover` + `TextMain`。
- 指令分组和指令列表：悬停 `BgHover`，选中 `BgSelected`；不要使用亮 Accent 作为大面积列表行背景。

### 状态与日志

- 已连接状态点：`OkGreen`；未连接：`ErrRed`。
- 部署锁定、需重启、新插件待生效等非阻断提示：`WarnAmber`。
- 日志底色：`BgPanel`；常规日志：`TextDim`；执行错误行后续可按 `ErrRed` 着色，但该行为属于可选增强，不能改变日志数据结构。

## 实施边界与最小改动

1. 扩展 `MainWindow` 中的静态 Brush token：增加 `BgRaised`、`BgHover`、`BgSelected`、`BorderFocus`、`TextMuted`、`TextOnAccent`、`WarnAmber`，替换现有 10 个 token 的十六进制值，并移除 `BgActive`。
2. 让当前 `BgActive` 的职责拆分为 `BgHover` 与 `BgSelected`；不保留既有“一个颜色多种语义”的映射。
3. 为 `TopButton`、`FlatButton`、导航按钮、`ListItemStyle()`、`ComboBox.ItemContainerStyle` 添加基于现有 WPF Trigger 的 hover/selected/focus 色彩状态。不得改动 Click、SelectionChanged、命令渲染或后台线程代码。
4. 更新 `ComboDarkTemplate()` 的 `{C_INPUT}`、`{C_BORDER}`、`{C_DIM}` 注入色，继续使用 Replace 而非 `string.Format`，以避免和 XAML Binding 花括号冲突。
5. 不在 XAML 字符串中散落新的硬编码颜色；所有新增颜色从命名 Brush/token 或同名 template placeholder 导出。
6. 不增加第三方主题库，不引入渐变、发光、阴影、装饰性图形或动画。

## 验收标准

1. 主背景、面板、输入区、悬停、选中态在桌面窗口中可一眼区分。
2. 执行按钮、当前导航、当前下拉选项可识别，但亮冷青不占据大面积背景。
3. ComboBox 展开、悬停、选中时没有白底、浅灰字或低对比文字。
4. 已连接、警告、错误具有独立色彩，不依赖紫色 Accent 区分。
5. 现有布局、指令分组、维护页边距、控制台与测试行为不回归。
6. `build.ps1` 成功，`test_ui.ps1` 保持 `17/17`。

## 实施后验证

- 运行 Trainer，检查指令页、控制台页、维护页的静态状态。
- 手动展开 ComboBox，检查普通、悬停、选中和焦点状态。
- 触发一次连接成功、一次无效 EVAL 或断开状态，确认状态色与日志仍可读。
- 用 UIAutomation 回归脚本验证页面、分组、控件数量和命令执行流程没有颜色导致的模板异常。
