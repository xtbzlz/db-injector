// TrainerApp.cs — D&B Trainer 重构版：图形化指令拼接与执行工具
// 架构：数据驱动指令库 + 动态参数表单 + 双语(运行时名/日文标识符)下拉 + 管道桥执行
// 依赖：PipeClient.cs（管道协议），插件 tb_bridge.tpm（LIST/EVAL）
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace DzbTrainer
{
    public static class AppMain
    {
        [STAThread]
        public static int Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) => CrashLog("AppDomain", e.ExceptionObject);
            var app = new Application();
            app.DispatcherUnhandledException += (s, e) =>
            {
                CrashLog("Dispatcher", e.Exception);
                e.Handled = false;
            };
            app.Run(new MainWindow());
            return 0;
        }

        static void CrashLog(string where, object ex)
        {
            try
            {
                string msg = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + where + ": " + ex;
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"),
                    msg + "\r\n");
            }
            catch { }
        }
    }

    // ============================================================
    // 指令库数据模型
    // ============================================================
    public class ParamSpec
    {
        public string Key;      // 占位符名
        public string Label;    // 中文标签
        public string Type;     // chara|o|item|skill|magic|status|num|arr9|bool|strip|preg|guy|acts
        public string Def;      // 默认值
        public string[] Options; // 预置选项
        public string Hint;     // 提示
    }

    public class CmdDef
    {
        public string Group;    // 分组
        public string Name;     // 中文名
        public string Template; // TJS 模板（{key} 占位）
        public ParamSpec[] Params;
        public string CountParam; // 执行次数参数（物品数量等），null 表示单次
        public override string ToString() { return Name; }
    }

    // ============================================================
    // 指令库（按用户清单全量录入）
    // ============================================================
    public static class CmdLib
    {
        static ParamSpec P(string key, string label, string type, string def = "", string[] opts = null, string hint = "")
        {
            return new ParamSpec { Key = key, Label = label, Type = type, Def = def, Options = opts, Hint = hint };
        }

        static readonly string[] Guys = { "theo", "max", "linus", "blue", "alex" };
        static readonly string[] Strips = { "通常", "下着", "パンツ", "裸", "汗だく", "妊娠", "ボテ腹" };
        static readonly string[] StripsSub = { "通常", "裸", "汗だく" };
        static readonly string[] Statuses = { "麻痺", "毒", "猛毒", "睡眠", "混乱", "魅了", "呪い", "石化", "恐怖", "沉默", "衰弱", "疫病" };
        static readonly string[] Skills = { "偵察", "反撃", "格闘", "聖拳", "回避", "鑑定", "修理", "探索", "警戒", "習得", "契約", "憑依術", "浄化", "全員かばう", "擊倒" };
        static readonly string[] Mercs = { "ブレイズマン", "ソードダンサー", "チャンピオン", "エンジェルナイト", "アコライト", "ハイプリースト", "ミスティック", "メイガス", "スナイパー", "マスターアーチャー" };
        // 可操控主力（有背包，party 出入队有意义）
        static readonly string[] PartyKeys = { "テオ", "マックス", "リーゼル" };
        // 支援角色（GuestObject，无背包，guest.entry 有效）
        public static readonly string[] GuestKeys = { "ポラリス", "サンドラ", "マリア", "ブルー", "ライナス", "ミレディ" };
        // o 引用角色名单（键=日文标识符, 显示名中文）
        public static readonly string[] ORoleKeys = { "テオ", "マックス", "ブルー", "リーゼル", "サンドラ", "マリア", "ポラリス", "リム", "クレア", "フレデリカ", "ミューズ", "マルエット", "linus", "alex" };

        public static CmdDef[] All = new CmdDef[] {
            // ---- 等级经验 ----
            Cmd("等级经验", "升级到指定等级", "{chara}.levelUp({num})",
                P("chara","角色","chara"), P("num","目标等级","num","99")),
            Cmd("等级经验", "经验拉满升至最高(绿字时)", "{chara}.levelUpToTheLast()",
                P("chara","角色","chara")),
            Cmd("等级经验", "获得经验", "{chara}.exp = {num}",
                P("chara","角色","chara"), P("num","经验值","num","1000")),
            Cmd("等级经验", "队伍升级(需经验足够)", "game.hotelView.checkLevelUp()"),
            // ---- 六维属性 ----
            Cmd("六维属性", "武艺", "{chara}.power = {num}", P("chara","角色","chara"), P("num","数值","num","99")),
            Cmd("六维属性", "智慧", "{chara}.intellect = {num}", P("chara","角色","chara"), P("num","数值","num","99")),
            Cmd("六维属性", "信仰", "{chara}.piety = {num}", P("chara","角色","chara"), P("num","数值","num","99")),
            Cmd("六维属性", "生命力", "{chara}.vitality = {num}", P("chara","角色","chara"), P("num","数值","num","99")),
            Cmd("六维属性", "敏捷", "{chara}.speed = {num}", P("chara","角色","chara"), P("num","数值","num","99")),
            Cmd("六维属性", "运", "{chara}.luck = {num}", P("chara","角色","chara"), P("num","数值","num","99")),
            // ---- 魔法上限 ----
            Cmd("魔法上限", "精灵魔法上限", "{chara}.mmagicMax = {arr9}", P("chara","角色","chara"), P("arr9","数值数组","arr9","[99,99,99,99,99,99,99,99,99]")),
            Cmd("魔法上限", "神圣魔法上限", "{chara}.pmagicMax = {arr9}", P("chara","角色","chara"), P("arr9","数值数组","arr9","[99,99,99,99,99,99,99,99,99]")),
            Cmd("魔法上限", "炼金魔法上限", "{chara}.amagicMax = {arr9}", P("chara","角色","chara"), P("arr9","数值数组","arr9","[99,99,99,99,99,99,99,99,99]")),
            Cmd("魔法上限", "召唤魔法上限", "{chara}.smagicMax = {arr9}", P("chara","角色","chara"), P("arr9","数值数组","arr9","[99,99,99,99,99,99,99,99,99]")),
            // ---- 物品 ----
            Cmd("物品", "设置金钱(所持金)", "game.party.gold = {num}", P("num","金钱","num","999999")),
            Cmd("物品", "增加金钱(所持金)", "game.party.gold += {num}", P("num","金钱","num","100000")),
            Cmd("物品", "发放物品(目标角色)", "{chara}.insertItem({item})",
                new ParamSpec[] { P("chara","目标角色","chara"), P("item","物品","item") }, "item"),
            Cmd("物品", "发放物品(队伍)", "game.party.insertItem({item})",
                new ParamSpec[] { P("item","物品","item") }, "item"),
            Cmd("物品", "发放高能炸弹", "game.chara[0].insertItem(o.ハイパーボム)"),
            // ---- 时间 ----
            Cmd("时间", "时间推进", "game.elapse({num})", P("num","天数","num","30")),
            // ---- 状态异常 ----
            Cmd("状态异常", "设置异常状态", "{chara}.setStatus({status}, {bool})",
                P("chara","角色","chara"), P("status","状态","status","麻痺"), P("bool","开启","bool","true")),
            // ---- 结婚 ----
            Cmd("结婚", "与角色结婚(四步,不触发主线)", "(game.wife = {chara}, {o}.married = true, game.wife.married = true, game.wife.events = [13,17,14,15,16,30,50,70])",
                P("chara","角色索引","chara"), P("o","角色o键","o","テオ")),
            // ---- 队伍 ----
            Cmd("队伍", "加入主力(主角テオ)", "game.party.entry(o.テオ)"),
            Cmd("队伍", "加入主力(基友マックス)", "game.party.entry(o.マックス)"),
            Cmd("队伍", "加入主力(洁丽尔リーゼル)", "game.party.entry(o.リーゼル)"),
            Cmd("队伍", "移除主力成员", "game.party.removeMember({o})", P("o","成员","o","テオ", PartyKeys)),
            Cmd("队伍", "加入支援队伍(右侧)", "game.guest.entry({o})", P("o","角色","o","ポラリス", GuestKeys)),
            Cmd("队伍", "移除支援成员", "game.guest.removeMember({o})", P("o","成员","o","ポラリス", GuestKeys)),
            Cmd("队伍", "加佣兵(右侧那排)", "game.hiring.add({o})", P("o","佣兵","o","ブレイズマン", Mercs)),
            Cmd("队伍", "取消基友特殊地位", "o.マックス.regular = false"),
            // ---- 地图 ----
            Cmd("地图", "全区域地图开启", "game.map.showAllArea = true"),
            Cmd("地图", "遇怪概率(0不遇怪/1每步遇怪)", "game.map.battleRate = {num}", P("num","概率","num","0")),
            // ---- 技能 ----
            Cmd("技能", "添加技能", "{chara}.skill.add({skill})",
                P("chara","角色","chara"), P("skill","技能","skill","偵察")),
            Cmd("技能", "删除技能", "{chara}.skill.remove({skill})",
                P("chara","角色","chara"), P("skill","技能","skill","擊倒")),
            Cmd("技能", "学会魔法", "{chara}.learnSkill({magic})",
                P("chara","角色","chara","2"), P("magic","魔法","magic","ファイア")),
            // ---- 男主 ----
            Cmd("男主数据", "性技巧", "game.chara[0].technic = {num}", P("num","数值","num","99")),
            Cmd("男主数据", "阴茎长度", "game.chara[0].penis = {num}", P("num","数值","num","30")),
            Cmd("男主数据", "性经验人数", "{chara}.loversCount = {num}", P("chara","角色","chara"), P("num","人数","num","10")),
            Cmd("男主数据", "男主性爱次数", "game.chara[0].sexCount.self = {num}", P("num","次数","num","10")),
            // ---- 后宫数值 ----
            Cmd("后宫数值", "好感度", "{o}.love.{guy} = {num}",
                P("o","角色","o","サンドラ"), P("guy","对象","guy","theo", Guys), P("num","数值","num","100")),
            Cmd("后宫数值", "性爱次数", "{o}.sexCount.{guy} = {num}",
                P("o","角色","o","サンドラ"), P("guy","对象","guy","theo", Guys), P("num","次数","num","10")),
            Cmd("后宫数值", "立绘状态", "{o}.strip.{guy} = {strip}",
                P("o","角色","o","サンドラ"), P("guy","对象","guy","theo", Guys), P("strip","立绘","strip","裸", Strips)),
            Cmd("后宫数值", "开发度", "{o}.develop.{guy} = {num}",
                P("o","角色","o","サンドラ"), P("guy","对象","guy","theo", Guys), P("num","数值","num","100")),
            Cmd("后宫数值", "警戒度", "{o}.guard.{guy} = {num}",
                P("o","角色","o","サンドラ"), P("guy","对象","guy","theo", Guys), P("num","数值","num","0")),
            Cmd("后宫数值", "性欲度", "{o}.desire = {num}", P("o","角色","o","サンドラ"), P("num","数值","num","10")),
            Cmd("后宫数值", "贞洁度", "{o}.moral = {num}", P("o","角色","o","サンドラ"), P("num","数值","num","100")),
            Cmd("后宫数值", "暴露度", "{o}.expose = {num}", P("o","角色","o","サンドラ"), P("num","数值","num","0")),
            Cmd("后宫数值", "敏感度", "{o}.feel = {num}", P("o","角色","o","サンドラ"), P("num","数值","num","10")),
            Cmd("后宫数值", "卖春数", "{o}.sellCount = {num}", P("o","角色","o","サンドラ"), P("num","数值","num","0")),
            Cmd("后宫数值", "出轨数", "{o}.affairCount = {num}", P("o","角色","o","サンドラ"), P("num","数值","num","0")),
            Cmd("后宫数值", "中出数", "{o}.pourCount = {num}", P("o","角色","o","サンドラ"), P("num","数值","num","0")),
            Cmd("后宫数值", "怀孕程度", "{o}.pregnant = {preg}",
                P("o","角色","o","サンドラ"), P("preg","怀孕程度","preg","NO_PREGNANCY")),
            Cmd("后宫数值", "怀孕数", "{o}.pregnantCount = {num}", P("o","角色","o","サンドラ"), P("num","数值","num","0")),
            Cmd("后宫数值", "性欲上升间隔", "{o}.heatCounter = {num}", P("o","角色","o","サンドラ"), P("num","数值","num","0")),
            Cmd("后宫数值", "囚禁时间", "{o}.slaveTerm = {num}", P("o","角色","o","サンドラ"), P("num","数值","num","0")),
            Cmd("后宫数值", "初夜对象", "{o}.firstPartnerKey = \"{guy}\"",
                P("o","角色","o","サンドラ"), P("guy","对象","guy","theo", Guys)),
            Cmd("后宫数值", "清空性爱日记", "{o}.diary = %[]", P("o","角色","o","サンドラ")),
            Cmd("后宫数值", "强行扒衣(次要人物)", "{o}.equipLife = {strip}",
                P("o","角色","o","サンドラ"), P("strip","立绘","strip","裸", StripsSub)),
            // ---- 性记录 ----
            Cmd("性记录", "添加性爱记录", "{chara}.addSexRecordAndChangeHeart(%[date: game.date, guyKey: \"{guy}\", sexCount: {num}, satisfy: {num}, orgasmCount: {num}, acts: [{acts}]])",
                P("chara","角色","chara","2"), P("guy","对象","guy","theo", Guys),
                P("num","次数","num","5"), P("num2","满足","num","5"), P("num3","高潮","num","1"),
                P("acts","体位列表(逗号分隔)","acts","正常位,背面座位")),
        };

        static CmdDef Cmd(string group, string name, string template, params ParamSpec[] ps)
        {
            return new CmdDef { Group = group, Name = name, Template = template, Params = ps };
        }
        static CmdDef Cmd(string group, string name, string template, ParamSpec[] ps, string countParam)
        {
            return new CmdDef { Group = group, Name = name, Template = template, Params = ps, CountParam = countParam };
        }

        public static string[] Groups
        {
            get { return All.Select(c => c.Group).Distinct().ToArray(); }
        }

        // 运行时数据源（由 MainWindow 填充）
        public static Dictionary<string, string> CharaNames = new Dictionary<string, string>(); // index -> name
        public static Dictionary<string, string> MagicNames = new Dictionary<string, string>(); // 系列+index -> name
    }

    // ============================================================
    // MainWindow
    // ============================================================
    public class MainWindow : Window
    {
        static readonly Brush BgMain = B("#1E1E2E");
        static readonly Brush BgPanel = B("#2A2A3E");
        static readonly Brush BgInput = B("#232334");
        static readonly Brush BgActive = B("#4A4A68");
        static readonly Brush BorderC = B("#4A4A68");
        static readonly Brush Accent = B("#7C5CFF");
        static readonly Brush TextMain = B("#E8E8F0");
        static readonly Brush TextDim = B("#9A9AB0");
        static readonly Brush OkGreen = B("#4CAF50");
        static readonly Brush ErrRed = B("#E05C5C");

        static Brush B(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }

        PipeClient pipe = new PipeClient();
        DispatcherTimer statusTimer;
        Grid pageHost;
        Dictionary<string, FrameworkElement> pages = new Dictionary<string, FrameworkElement>();
        List<Button> navButtons = new List<Button>();
        TextBlock statusDot, statusText, statusInfo;
        ListBox logBox;

        // 指令页控件
        ListBox groupList, cmdList;
        StackPanel formPanel;
        TextBox previewBox, descBox;
        Button runButton;

        // 数据
        Dictionary<string, string> listData = new Dictionary<string, string>(); // o键 -> "name\tid"
        Dictionary<string, string> listNames = new Dictionary<string, string>(); // o键 -> name
        Dictionary<string, string> listIds = new Dictionary<string, string>();   // o键 -> id
        List<KeyValuePair<int, string>> itemList = new List<KeyValuePair<int, string>>(); // id -> 显示名
        Dictionary<string, string> itemIdToKey = new Dictionary<string, string>(); // id -> o键
        Dictionary<string, string> itemKeyToId = new Dictionary<string, string>(); // o键 -> id
        Dictionary<string, string> itemJpName = new Dictionary<string, string>();   // id -> 日文标准名(wiki优先,o键兜底)
        Dictionary<string, string> charaJpName = new Dictionary<string, string>();  // chara索引 -> o键(日文)
        Dictionary<string, string> oKeyDisplay = new Dictionary<string, string>();  // o键 -> displayName(中文)
        List<string> oRoleKeys = new List<string>();
        string[] magicSeries = { "mmagic", "pmagic", "amagic", "smagic" };
        Dictionary<string, string> magicDisplay = new Dictionary<string, string>(); // "系列|名" -> o键

        CmdDef selectedCmd;
        Dictionary<string, Control> paramControls = new Dictionary<string, Control>();
        Dictionary<string, TextBox> searchBoxes = new Dictionary<string, TextBox>();
        List<string> oKeys = new List<string>();

        public MainWindow()
        {
            Title = "D&B 修改器 - ダンジョン＆ブライド";
            Width = 1220; Height = 700; MinWidth = 1020; MinHeight = 560;
            BuildUi();
            ShowPage("指令");
            statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            statusTimer.Tick += (s, e) => RefreshStatus();
            statusTimer.Start();
            RefreshStatus();
            RefreshStatusInfo();
        }

        // ============ 通用帮助 ============
        ScrollViewer Page()
        {
            var sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = BgMain };
            sv.Content = new StackPanel { Margin = new Thickness(12) };
            return sv;
        }

        StackPanel Card()
        {
            return new StackPanel { Background = BgPanel, Margin = new Thickness(0, 0, 0, 10) };
        }

        TextBlock Lbl(string text, bool dim = false, double size = 13)
        {
            return new TextBlock { Text = text, Foreground = dim ? TextDim : TextMain, FontSize = size, Margin = new Thickness(0, 2, 0, 2) };
        }

        TextBox MakeInput(string text = "", double width = 140)
        {
            return new TextBox { Text = text, Width = width, Padding = new Thickness(4), Background = BgInput, Foreground = TextMain, BorderBrush = BorderC, BorderThickness = new Thickness(1), FontSize = 12 };
        }

        static ControlTemplate comboDarkTemplate;

        static ControlTemplate ComboDarkTemplate()
        {
            if (comboDarkTemplate != null) return comboDarkTemplate;
            const string xaml =
                "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' " +
                "xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ComboBox'>" +
                "<Grid>" +
                "  <ToggleButton x:Name='ToggleButton' ClickMode='Press' Focusable='false' " +
                "    IsChecked='{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}' Background='Transparent'>" +
                "    <ToggleButton.Template>" +
                "      <ControlTemplate TargetType='ToggleButton'>" +
                "        <Border Background='#232334' BorderBrush='#4A4A68' BorderThickness='1' CornerRadius='2'>" +
                "          <Grid>" +
                "            <Grid.ColumnDefinitions><ColumnDefinition/><ColumnDefinition Width='18'/></Grid.ColumnDefinitions>" +
                "            <Path Grid.Column='1' Data='M 0 0 L 4 4 L 8 0 Z' Fill='#9A9AB0' HorizontalAlignment='Center' VerticalAlignment='Center'/>" +
                "          </Grid>" +
                "        </Border>" +
                "      </ControlTemplate>" +
                "    </ToggleButton.Template>" +
                "  </ToggleButton>" +
                "  <ContentPresenter Margin='5,2,22,2' VerticalAlignment='Center' HorizontalAlignment='Left' IsHitTestVisible='False'" +
                "    Content='{TemplateBinding SelectionBoxItem}' ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}'" +
                "    ContentStringFormat='{TemplateBinding SelectionBoxItemStringFormat}'/>" +
                "  <Popup x:Name='PART_Popup' Placement='Bottom' IsOpen='{TemplateBinding IsDropDownOpen}'" +
                "    AllowsTransparency='True' Focusable='False' PopupAnimation='Slide'>" +
                "    <Border Background='#232334' BorderBrush='#4A4A68' BorderThickness='1' CornerRadius='2'" +
                "      MinWidth='{TemplateBinding ActualWidth}' MaxHeight='{TemplateBinding MaxDropDownHeight}'>" +
                "      <ScrollViewer VerticalScrollBarVisibility='Auto'>" +
                "        <ItemsPresenter/>" +
                "      </ScrollViewer>" +
                "    </Border>" +
                "  </Popup>" +
                "</Grid></ControlTemplate>";
            comboDarkTemplate = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(xaml);
            return comboDarkTemplate;
        }

        static Style listItemStyle;

        static Style ListItemStyle()
        {
            if (listItemStyle != null) return listItemStyle;
            var st = new Style(typeof(ListBoxItem));
            st.Setters.Add(new Setter(ListBoxItem.PaddingProperty, new Thickness(6, 5, 6, 5)));
            st.Setters.Add(new Setter(ListBoxItem.MinHeightProperty, 28.0));
            listItemStyle = st;
            return st;
        }

        ComboBox MakeCombo(double width = 180)
        {
            var cb = new ComboBox { Width = width, Padding = new Thickness(2), Background = BgInput, Foreground = TextMain, BorderBrush = BorderC, FontSize = 12 };
            cb.Template = ComboDarkTemplate();
            // 下拉列表项深色样式（默认是白底+继承的灰白字，对比度差）
            var st = new Style(typeof(ListBoxItem));
            st.Setters.Add(new Setter(Control.BackgroundProperty, BgInput));
            st.Setters.Add(new Setter(Control.ForegroundProperty, TextMain));
            st.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(4, 2, 4, 2)));
            var trig = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
            trig.Setters.Add(new Setter(Control.BackgroundProperty, Accent));
            trig.Setters.Add(new Setter(Control.ForegroundProperty, TextMain));
            st.Triggers.Add(trig);
            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Control.BackgroundProperty, BgActive));
            st.Triggers.Add(hover);
            cb.ItemContainerStyle = st;
            return cb;
        }

        Button TopButton(string text, Action onClick)
        {
            var b = new Button { Content = text, Height = 30, MinWidth = 80, Padding = new Thickness(12, 4, 12, 4), Background = BgPanel, Foreground = TextMain, BorderBrush = BorderC, BorderThickness = new Thickness(1), FontSize = 12, VerticalContentAlignment = VerticalAlignment.Center, Cursor = System.Windows.Input.Cursors.Hand };
            b.Click += (s, e) => onClick();
            return b;
        }

        Button FlatButton(string text, Action onClick)
        {
            var b = new Button { Content = text, Height = 30, MinWidth = 80, Padding = new Thickness(12, 4, 12, 4), Background = BgActive, Foreground = TextMain, BorderBrush = BorderC, BorderThickness = new Thickness(1), FontSize = 12, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 6, 4), Cursor = System.Windows.Input.Cursors.Hand };
            b.Click += (s, e) => onClick();
            return b;
        }

        void AddNav(StackPanel nav, string name)
        {
            var b = new Button
            {
                Content = name,
                Tag = name,
                Height = 30,
                MinWidth = 76,
                Margin = new Thickness(0, 4, 6, 4),
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = Brushes.Transparent,
                Foreground = TextDim,
                BorderBrush = BorderC,
                BorderThickness = new Thickness(0),
                FontSize = 13,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(12, 0, 12, 0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            b.Click += (s, e) => ShowPage(name);
            nav.Children.Add(b);
            navButtons.Add(b);
        }

        void BuildUi()
        {
            Background = BgMain;
            var root = new Grid();
            Content = root;
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(110) });

            // ---- 顶栏 ----
            var top = new Grid { Background = BgPanel };
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
            top.ColumnDefinitions.Add(new ColumnDefinition());
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });
            var title = new TextBlock { Text = "D&B 修改器 - 指令拼接", Foreground = TextMain, FontSize = 15, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0), FontWeight = FontWeights.Bold, TextTrimming = TextTrimming.CharacterEllipsis };
            top.Children.Add(title);
            var nav = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            AddNav(nav, "指令");
            AddNav(nav, "控制台");
            AddNav(nav, "维护");
            Grid.SetColumn(nav, 1);
            top.Children.Add(nav);
            var status = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            statusDot = new TextBlock { Text = "●", FontSize = 14, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) };
            statusText = new TextBlock { Foreground = TextDim, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            statusInfo = new TextBlock { Foreground = TextDim, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
            status.Children.Add(statusDot);
            status.Children.Add(statusText);
            status.Children.Add(statusInfo);
            status.Children.Add(TopButton("启动游戏", LaunchGame));
            Grid.SetColumn(status, 2);
            top.Children.Add(status);
            root.Children.Add(top);

            // ---- 页面区 ----
            pageHost = new Grid();
            pageHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(pageHost, 1);
            root.Children.Add(pageHost);

            // ---- 日志区 ----
            var logPanel = new DockPanel { Background = BgPanel, Margin = new Thickness(0, 0, 0, 0) };
            Grid.SetRow(logPanel, 2);
            root.Children.Add(logPanel);
            logBox = new ListBox { Background = BgPanel, Foreground = TextDim, FontSize = 12, BorderThickness = new Thickness(0) };
            ScrollViewer.SetVerticalScrollBarVisibility(logBox, ScrollBarVisibility.Auto);
            DockPanel.SetDock(logBox, Dock.Top);
            logPanel.Children.Add(logBox);
        }

        void ShowPage(string name)
        {
            FrameworkElement pg;
            if (!pages.TryGetValue(name, out pg))
            {
                pg = BuildPage(name);
                pages[name] = pg;
                pageHost.Children.Add(pg);
            }
            foreach (var b in navButtons)
            {
                bool sel = (string)b.Tag == name;
                b.Background = sel ? BgActive : Brushes.Transparent;
                b.Foreground = sel ? TextMain : TextDim;
                b.BorderThickness = sel ? new Thickness(1) : new Thickness(0);
            }
            foreach (FrameworkElement f in pageHost.Children)
                f.Visibility = Visibility.Collapsed;
            pg.Visibility = Visibility.Visible;
        }

        FrameworkElement BuildPage(string name)
        {
            switch (name)
            {
                case "指令": return BuildCmdPage();
                case "控制台": return BuildConsolePage();
                case "维护": return BuildMaintPage();
            }
            return new Grid();
        }

        // ============ 日志/执行 ============
        void Log(string msg)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                logBox.Items.Add("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg);
                if (logBox.Items.Count > 500) logBox.Items.RemoveAt(0);
                logBox.ScrollIntoView(logBox.Items[logBox.Items.Count - 1]);
            }));
        }

        void BackgroundLoad(Action work)
        {
            var t = new Thread(() =>
            {
                try { work(); }
                catch (Exception ex) { Log("错误: " + ex.Message); }
            });
            t.IsBackground = true;
            t.Start();
        }

        void RefreshStatus()
        {
            // 后台执行：游戏未启动时 Ping 可能耗时（连接失败/重试），不能阻塞 UI 线程
            BackgroundLoad(() =>
            {
                bool ok = pipe.Ping() == "PONG";
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    statusDot.Foreground = ok ? OkGreen : ErrRed;
                    statusText.Text = ok ? "已连接" : "未连接";
                }));
            });
        }

        void RefreshStatusInfo()
        {
            BackgroundLoad(() =>
            {
                try
                {
                    int cnt = int.Parse(EvalSafe("game.items.count"));
                    int cc = int.Parse(EvalSafe("game.chara.count"));
                    Dispatcher.Invoke(new Action(() => statusInfo.Text = "物品 " + cnt + " / 角色 " + cc));
                }
                catch { }
            });
        }

        string EvalSafe(string code)
        {
            var r = pipe.Eval(code);
            if (r.StartsWith("ERR")) throw new Exception(r.Substring(4));
            return r;
        }

        // 双语显示：中文（日文）；两者相同则只显示一个
        static string FormatBi(string zh, string jp)
        {
            if (zh == null) zh = "";
            if (jp == null) jp = "";
            if (jp.Length == 0 || jp == zh) return zh;
            return zh + "（" + jp + "）";
        }

        // 技能中文映射（来自玩家整理）
        static readonly Dictionary<string, string> SkillZh = new Dictionary<string, string> {
            { "偵察", "侦察" }, { "反撃", "反击" }, { "格闘", "格斗" }, { "聖拳", "圣拳" },
            { "回避", "回避" }, { "鑑定", "鉴定" }, { "修理", "修理" }, { "探索", "探索" },
            { "警戒", "警戒" }, { "習得", "习得" }, { "契約", "契约" }, { "憑依術", "凭依术" },
            { "浄化", "净化" }, { "全員かばう", "全员庇护" }, { "擊倒", "击倒" }, { "かばう", "庇护" },
        };

        static readonly string[] MercKeys = { "ブレイズマン", "ソードダンサー", "チャンピオン", "エンジェルナイト", "アコライト", "ハイプリースト", "ミスティック", "メイガス", "スナイパー", "マスターアーチャー" };

        long Parse(string s)
        {
            long v;
            if (!long.TryParse(s.Trim(), out v)) throw new Exception("无效数字: " + s);
            return v;
        }

        void LaunchGame()
        {
            try
            {
                var baseDir = @"C:\Users\1\Desktop\CR\ダンジョン＆ブライド";
                if (!System.IO.Directory.Exists(baseDir)) { Log("找不到游戏目录: " + baseDir); return; }
                Process.Start(baseDir + @"\game64.exe");
                Log("已启动游戏");
            }
            catch (Exception ex) { Log("启动失败: " + ex.Message); }
        }

        // ============ 数据加载 ============
        void LoadRegistry()
        {
            // LIST：o 注册表（键=日文标识符, name=运行时名, id=物品ID）
            var raw = pipe.List();
            if (raw.StartsWith("ERR")) throw new Exception(raw.Substring(4));
            listNames.Clear();
            listIds.Clear();
            itemList.Clear();
            itemIdToKey.Clear();
            itemKeyToId.Clear();
            itemJpName.Clear();
            charaJpName.Clear();
            oKeys.Clear();
            foreach (var line in raw.Split('\n'))
            {
                var t = line.Split('\t');
                if (t.Length < 3 || t[0].Length == 0) continue;
                string key = t[0], nm = t[1], id = t[2];
                oKeys.Add(key);
                if (nm.Length > 0) listNames[key] = nm;
                if (id.Length > 0)
                {
                    listIds[key] = id;
                    int iv;
                    if (int.TryParse(id, out iv))
                    {
                        if (!itemIdToKey.ContainsKey(id)) itemIdToKey[id] = key;
                        if (!itemKeyToId.ContainsKey(key)) itemKeyToId[key] = id;
                    }
                }
            }
            // 物品源：game.items 数组（569 个，索引=id）——LIST 的带 id 对象含地图/事件等非物品
            // 双语：中文=运行时 displayName（汉化版），日文=运行时 name
            itemList.Clear();
            {
                string itemsRaw = EvalSafe("(function(){ var a=game.items; var r=''; for(var i=0;i<a.count;i++){ r += i + '\\t' + a[i].name + '\\t' + a[i].displayName + '\\n'; } return r; })()");
                foreach (var line in itemsRaw.Split('\n'))
                {
                    var t = line.Split('\t');
                    if (t.Length < 2) continue;
                    int id;
                    if (!int.TryParse(t[0].Trim(), out id)) continue;
                    string nm = t[1].Trim();   // 日文原名
                    string cn = t.Length >= 3 ? t[2].Trim() : "";
                    if (cn.Length == 0) { string mc; if (!CnItemMap.Map.TryGetValue(nm, out mc)) mc = nm; cn = mc; }
                    if (nm.Length == 0) nm = "?";
                    itemJpName[id.ToString()] = nm;
                    itemList.Add(new KeyValuePair<int, string>(id, FormatBi(cn, nm)));
                }
                itemList.Sort((a, b) => a.Key.CompareTo(b.Key));
            }
            // 角色名（双语：运行时名 + o键日文名）
            CmdLib.CharaNames.Clear();
            {
                string names = EvalSafe("(function(){ var r=''; for(var i=0;i<game.chara.count;i++){ r += i + '\\t' + game.chara[i].name + '\\n'; } return r; })()");
                foreach (var line in names.Split('\n'))
                {
                    var t = line.Split('\t');
                    if (t.Length < 2) continue;
                    string idx = t[0].Trim(), nm = t[1].Trim();
                    if (nm.Length == 0) continue;
                    // 日文名：先查 chara.tsv 映射（中文名→日文原名），再查标准角色名单，最后 LIST
                    string jp = "";
                    string jpFromMap;
                    if (CharaMap.Jp.TryGetValue(nm, out jpFromMap)) jp = jpFromMap;
                    if (jp.Length == 0)
                        foreach (var ok in CmdLib.ORoleKeys)
                            if (listNames.ContainsKey(ok) && listNames[ok] == nm) { jp = ok; break; }
                    if (jp.Length == 0)
                        foreach (var kv in listNames)
                            if (kv.Value == nm) { jp = kv.Key; break; }
                    charaJpName[idx] = jp;
                    CmdLib.CharaNames[idx] = FormatBi(nm, jp);
                }
            }
            // 魔法名（4 系列，中文=displayName，日文=name）；值=cn\tjp
            CmdLib.MagicNames.Clear();
            magicDisplay.Clear();
            for (int s = 0; s < magicSeries.Length; s++)
            {
                string mn = EvalSafe("(function(){ var a=game." + magicSeries[s] + "; var r=''; for(var i=0;i<a.count;i++){ r += (i>0?',':'') + a[i].name + '\\t' + a[i].displayName; } return r; })()");
                foreach (var pair in mn.Split(','))
                {
                    var p2 = pair.Split('\t');
                    if (p2.Length < 1) continue;
                    string jp = p2[0].Trim();
                    string cn = p2.Length >= 2 ? p2[1].Trim() : jp;
                    if (jp.Length == 0) continue;
                    if (cn.Length == 0) cn = jp;
                    magicDisplay[magicSeries[s] + "|" + jp] = cn + "\t" + jp;
                }
            }
            // 技能/状态/佣兵/角色 o 键的 displayName（中文），用于下拉「中文（日文）」
            oKeyDisplay.Clear();
            {
                var probe = new List<string>();
                probe.AddRange(CmdLib.ORoleKeys);
                probe.AddRange(CmdLib.GuestKeys);
                probe.AddRange(SkillsFromRegistry());
                probe.AddRange(StatusesFromRegistry());
                probe.AddRange(MercKeys);
                string pk = string.Join(",", probe.ToArray());
                string dn = EvalSafe("(function(){ var ks=['" + pk.Replace(",", "','") + "']; var r=''; for(var i=0;i<ks.length;i++){ try { r += ks[i] + '\\t' + o[ks[i]].displayName + '\\n'; } catch(e){} } return r; })()");
                foreach (var line in dn.Split('\n'))
                {
                    var t = line.Split('\t');
                    if (t.Length < 2) continue;
                    string k = t[0].Trim(), v = t[1].Trim();
                    if (k.Length > 0) oKeyDisplay[k] = v;
                }
            }
            Dispatcher.Invoke(new Action(() =>
            {
                statusInfo.Text = "注册表 " + oKeys.Count + " 项 / 物品 " + itemList.Count + " / 角色 " + CmdLib.CharaNames.Count;
                Log("已加载注册表 " + oKeys.Count + " 项，物品 " + itemList.Count + " 个，角色 " + CmdLib.CharaNames.Count + " 名");
                if (selectedCmd != null) BuildParamForm(selectedCmd); // 数据就绪后重建表单
            }));
        }

        // ============ 指令页 ============
        FrameworkElement BuildCmdPage()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.RowDefinitions.Add(new RowDefinition()); // 默认 Height=*，使 grid 撑满整页高

            // 左：分组
            var gPanel = new Border { Background = BgPanel, BorderBrush = BorderC, BorderThickness = new Thickness(1), Padding = new Thickness(4) };
            var gGrid = new Grid();
            gPanel.Child = gGrid;
            gGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            gGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            gGrid.Children.Add(Lbl("分组", true, 11));
            groupList = new ListBox { Background = BgPanel, Foreground = TextMain, BorderThickness = new Thickness(0), FontSize = 12, ItemContainerStyle = ListItemStyle() };
            Grid.SetRow(groupList, 1);
            gGrid.Children.Add(groupList);
            grid.Children.Add(gPanel);

            // 中：指令 + 描述面板（填满列高，消除空白）
            var cPanel = new Border { Background = BgPanel, BorderBrush = BorderC, BorderThickness = new Thickness(1), Padding = new Thickness(4), Margin = new Thickness(6, 0, 0, 0) };
            var cGrid = new Grid();
            cPanel.Child = cGrid;
            cGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            cGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.2, GridUnitType.Star) });
            cGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0.8, GridUnitType.Star) });
            cGrid.Children.Add(Lbl("指令", true, 11));
            cmdList = new ListBox { Background = BgPanel, Foreground = TextMain, BorderThickness = new Thickness(0), FontSize = 12, ItemContainerStyle = ListItemStyle() };
            Grid.SetRow(cmdList, 1);
            cGrid.Children.Add(cmdList);
            descBox = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = BgInput, Foreground = TextDim, BorderThickness = new Thickness(0), FontSize = 12, Margin = new Thickness(0, 6, 0, 0), Padding = new Thickness(6) };
            Grid.SetRow(descBox, 2);
            cGrid.Children.Add(descBox);
            Grid.SetColumn(cPanel, 1);
            grid.Children.Add(cPanel);

            // 右：参数表单 + 预览（*行填满）+ 执行按钮
            var fPanel = new Border { Background = BgPanel, BorderBrush = BorderC, BorderThickness = new Thickness(1), Padding = new Thickness(10), Margin = new Thickness(6, 0, 0, 0) };
            var fGrid = new Grid();
            fPanel.Child = fGrid;
            fGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            fGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            fGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            fGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            formPanel = new StackPanel { MaxWidth = 760, HorizontalAlignment = HorizontalAlignment.Left };
            fGrid.Children.Add(formPanel);
            var pvLabel = Lbl("指令预览（TJS）", true, 11);
            pvLabel.Margin = new Thickness(0, 8, 0, 0);
            Grid.SetRow(pvLabel, 1);
            fGrid.Children.Add(pvLabel);
            previewBox = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = BgInput, Foreground = TextMain, BorderBrush = BorderC, BorderThickness = new Thickness(1), FontSize = 12, Margin = new Thickness(0, 2, 0, 0), Padding = new Thickness(6, 4, 6, 4), MinHeight = 60 };
            Grid.SetRow(previewBox, 2);
            fGrid.Children.Add(previewBox);
            runButton = FlatButton("执行", () => RunSelectedCmd());
            runButton.FontSize = 14;
            runButton.Height = 40;
            runButton.MinWidth = 120;
            runButton.Padding = new Thickness(20, 6, 20, 6);
            runButton.HorizontalAlignment = HorizontalAlignment.Left;
            runButton.Margin = new Thickness(0, 6, 0, 0);
            Grid.SetRow(runButton, 3);
            fGrid.Children.Add(runButton);
            Grid.SetColumn(fPanel, 2);
            grid.Children.Add(fPanel);

            // 填充分组
            foreach (var g in CmdLib.Groups) groupList.Items.Add(g);
            groupList.SelectionChanged += (s, e) =>
            {
                if (groupList.SelectedItem == null) return;
                var g = groupList.SelectedItem.ToString();
                cmdList.Items.Clear();
                foreach (var c in CmdLib.All)
                    if (c.Group == g) cmdList.Items.Add(c);
                if (cmdList.Items.Count > 0) cmdList.SelectedIndex = 0;
            };
            cmdList.SelectionChanged += (s, e) =>
            {
                if (cmdList.SelectedItem == null) return;
                selectedCmd = (CmdDef)cmdList.SelectedItem;
                descBox.Text = "【" + selectedCmd.Name + "】\n" + selectedCmd.Template;
                BuildParamForm(selectedCmd);
            };

            // 初始选中第一个分组（触发指令列表填充）
            groupList.SelectedIndex = 0;

            BackgroundLoad(() =>
            {
                try { LoadRegistry(); }
                catch (Exception ex) { Log("数据加载失败: " + ex.Message); }
            });
            return grid;
        }

        // ---- 动态参数表单 ----
        void BuildParamForm(CmdDef cmd)
        {
            formPanel.Children.Clear();
            paramControls.Clear();
            searchBoxes.Clear();
            if (cmd == null) return;
            formPanel.Children.Add(Lbl(cmd.Name, false, 15));
            formPanel.Children.Add(Lbl(cmd.Template, true, 12));

            foreach (var p in cmd.Params)
            {
                var row = new Grid { Margin = new Thickness(0, 6, 0, 0) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition());
                var label = Lbl(p.Label + ": ", true, 12);
                label.Width = 110;
                label.HorizontalAlignment = HorizontalAlignment.Right;
                label.VerticalAlignment = VerticalAlignment.Center;
                label.TextWrapping = TextWrapping.Wrap;
                row.Children.Add(label);
                Control ctl = null;
                string note = null;
                switch (p.Type)
                {
                    case "chara": ctl = MakeCharaCombo(p.Def); break;
                    case "o": ctl = MakeOCombo(p.Options, p.Def); break;
                    case "item": ctl = MakeItemCombo(p.Def); break;
                    case "skill": ctl = MakeKeyCombo(SkillsFromRegistry(), p.Def); break;
                    case "magic": ctl = MakeMagicCombo(p.Def); break;
                    case "status": ctl = MakeKeyCombo(StatusesFromRegistry(), p.Def); break;
                    case "num": ctl = MakeInput(p.Def); break;
                    case "arr9":
                        ctl = MakeInput(p.Def, 260);
                        ctl.IsEnabled = false;
                        note = "（9 元素数组）";
                        break;
                    case "bool": ctl = MakeBoolCombo(p.Def); break;
                    case "strip": ctl = MakeStripCombo(p.Options, p.Def); break;
                    case "preg": ctl = MakeKeyCombo(new string[] { "NO_PREGNANCY" }, p.Def, true); break;
                    case "guy": ctl = MakeGuyCombo(p.Options, p.Def); break;
                    case "acts": ctl = MakeInput(p.Def, 260); break;
                }
                if (ctl == null) continue;
                ctl.Tag = p.Key;
                paramControls[p.Key] = ctl;
                var ctlWrap = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
                if (note != null)
                {
                    var n = Lbl(note, true, 11);
                    n.VerticalAlignment = VerticalAlignment.Center;
                    ctlWrap.Children.Add(n);
                }
                ctlWrap.Children.Add(ctl);
                if (p.Hint.Length > 0)
                {
                    var hint = Lbl(p.Hint, true, 12);
                    hint.Margin = new Thickness(8, 0, 0, 0);
                    hint.VerticalAlignment = VerticalAlignment.Center;
                    ctlWrap.Children.Add(hint);
                }
                Grid.SetColumn(ctlWrap, 1);
                row.Children.Add(ctlWrap);
                formPanel.Children.Add(row);
            }

            // 执行次数
            if (cmd.CountParam != null)
            {
                var row = new Grid { Margin = new Thickness(0, 6, 0, 0) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition());
                var label = Lbl("发放数量: ", true, 12);
                label.Width = 110;
                label.HorizontalAlignment = HorizontalAlignment.Right;
                label.VerticalAlignment = VerticalAlignment.Center;
                row.Children.Add(label);
                var cnt = MakeInput("1", 60);
                cnt.Tag = "count";
                paramControls["count"] = cnt;
                cnt.HorizontalAlignment = HorizontalAlignment.Left;
                Grid.SetColumn(cnt, 1);
                row.Children.Add(cnt);
                formPanel.Children.Add(row);
            }

            // 参数变化 → 更新预览
            foreach (var c in paramControls.Values)
            {
                if (c is TextBox) ((TextBox)c).TextChanged += (s, e) => UpdatePreview();
                else if (c is ComboBox) ((ComboBox)c).SelectionChanged += (s, e) => UpdatePreview();
            }
            UpdatePreview();
        }

        // ---- 各类控件 ----
        ComboBox MakeCharaCombo(string defIndex)
        {
            var cb = MakeCombo(240);
            foreach (var kv in CmdLib.CharaNames)
                cb.Items.Add(new ComboItem("[" + kv.Key + "] " + kv.Value, kv.Key));
            if (CmdLib.CharaNames.Count > 0)
            {
                int di;
                cb.SelectedIndex = int.TryParse(defIndex, out di) && di < CmdLib.CharaNames.Count ? di : 0;
            }
            return cb;
        }

        ComboBox MakeOCombo(string[] fixedOpts, string defKey)
        {
            var cb = MakeCombo(240);
            var seen = new HashSet<string>();
            var keys = fixedOpts != null && fixedOpts.Length > 0 ? fixedOpts : CmdLib.ORoleKeys;
            foreach (var k in keys)
            {
                string nm = listNames.ContainsKey(k) ? listNames[k] : "";
                string zh = oKeyDisplay.ContainsKey(k) ? oKeyDisplay[k]
                        : (nm.Length > 0 && nm != k ? nm : k);
                cb.Items.Add(new ComboItem(FormatBi(zh, k), k));
                seen.Add(k);
            }
            int sel = 0;
            for (int i = 0; i < keys.Length; i++)
                if (keys[i] == defKey) sel = i;
            cb.SelectedIndex = Math.Min(sel, cb.Items.Count - 1);
            return cb;
        }

        ComboBox MakeItemCombo(string defKey)
        {
            var cb = MakeCombo(340);
            foreach (var kv in itemList)
            {
                cb.Items.Add(new ComboItem("[" + kv.Key + "] " + kv.Value, kv.Key.ToString()));
            }
            if (cb.Items.Count > 0) cb.SelectedIndex = 0;
            return cb;
        }

        ComboBox MakeKeyCombo(string[] keys, string defKey, bool raw = false)
        {
            var cb = MakeCombo(220);
            for (int i = 0; i < keys.Length; i++)
            {
                string k = keys[i];
                string nm = listNames.ContainsKey(k) ? listNames[k] : "";
                string zh = oKeyDisplay.ContainsKey(k) ? oKeyDisplay[k]
                        : (SkillZh.ContainsKey(k) ? SkillZh[k]
                        : (nm.Length > 0 && nm != k ? nm : k));
                cb.Items.Add(new ComboItem(FormatBi(zh, k), k));
            }
            int sel = 0;
            for (int i = 0; i < keys.Length; i++)
                if (keys[i] == defKey) sel = i;
            cb.SelectedIndex = Math.Min(sel, cb.Items.Count - 1);
            return cb;
        }

        ComboBox MakeMagicCombo(string defKey)
        {
            var cb = MakeCombo(340);
            int sel = 0;
            int idx = 0;
            string curSeries = "";
            foreach (var e in MagicMap.All)
            {
                if (e.Series != curSeries)
                {
                    curSeries = e.Series;
                    var sep = new ComboItem("—— " + SeriesName(e.Series) + " ——", "");
                    cb.Items.Add(sep);
                }
                cb.Items.Add(new ComboItem("LV" + e.Lv + " " + e.Cn + "（" + e.Key + "）", e.Key));
                if (e.Key == defKey) sel = cb.Items.Count - 1;
                idx++;
            }
            if (cb.Items.Count > 0) cb.SelectedIndex = sel;
            return cb;
        }

        static string SeriesName(string s)
        {
            switch (s)
            {
                case "mmagic": return "精灵魔法";
                case "pmagic": return "神圣魔法";
                case "amagic": return "炼金魔法";
                case "smagic": return "召唤魔法";
            }
            return s;
        }

        ComboBox MakeBoolCombo(string def)
        {
            var cb = MakeCombo(90);
            cb.Items.Add(new ComboItem("true", "true"));
            cb.Items.Add(new ComboItem("false", "false"));
            cb.SelectedIndex = def == "false" ? 1 : 0;
            return cb;
        }

        ComboBox MakeStripCombo(string[] opts, string def)
        {
            var cb = MakeCombo(120);
            for (int i = 0; i < opts.Length; i++)
                cb.Items.Add(new ComboItem(opts[i], "\"" + opts[i] + "\""));
            int sel = 0;
            for (int i = 0; i < opts.Length; i++)
                if (opts[i] == def) sel = i;
            cb.SelectedIndex = sel;
            return cb;
        }

        ComboBox MakeGuyCombo(string[] opts, string def)
        {
            var cb = MakeCombo(120);
            for (int i = 0; i < opts.Length; i++)
                cb.Items.Add(new ComboItem(opts[i], opts[i]));
            int sel = 0;
            for (int i = 0; i < opts.Length; i++)
                if (opts[i] == def) sel = i;
            cb.SelectedIndex = sel;
            return cb;
        }

        string[] SkillsFromRegistry()
        {
            var fixedSkills = new string[] { "偵察", "反撃", "格闘", "聖拳", "回避", "鑑定", "修理", "探索", "警戒", "習得", "契約", "憑依術", "浄化", "全員かばう", "擊倒" };
            var res = new List<string>();
            foreach (var k in fixedSkills) if (oKeys.Contains(k) || true) res.Add(k);
            return res.ToArray();
        }

        string[] StatusesFromRegistry()
        {
            var fixedSt = new string[] { "麻痺", "毒", "猛毒", "睡眠", "混乱", "魅了", "呪い", "石化", "恐怖", "沉默", "衰弱", "疫病" };
            return fixedSt;
        }

        // ---- 拼接 ----
        string GetVal(string key)
        {
            Control c;
            if (!paramControls.TryGetValue(key, out c)) return "";
            if (c is TextBox) return ((TextBox)c).Text.Trim();
            if (c is ComboBox)
            {
                var ci = ((ComboBox)c).SelectedItem as ComboItem;
                return ci != null ? ci.Val : "";
            }
            return "";
        }

        string RenderTemplate(CmdDef cmd)
        {
            if (cmd == null) return "";
            string t = cmd.Template;
            foreach (var p in cmd.Params)
            {
                string v = GetVal(p.Key);
                switch (p.Type)
                {
                    case "chara": t = t.Replace("{" + p.Key + "}", "game.chara[" + v + "]"); break;
                    case "o": t = t.Replace("{" + p.Key + "}", "o." + v); break;
                    case "item": t = t.Replace("{" + p.Key + "}", "game.items[" + v + "]"); break;
                    case "skill": t = t.Replace("{" + p.Key + "}", "o." + v); break;
                    case "magic": t = t.Replace("{" + p.Key + "}", "o." + v); break;
                    case "status": t = t.Replace("{" + p.Key + "}", "o." + v); break;
                    case "strip": t = t.Replace("{" + p.Key + "}", v); break;
                    case "preg": t = t.Replace("{" + p.Key + "}", v); break;
                    case "guy": t = t.Replace("{" + p.Key + "}", v); break;
                    case "num": t = t.Replace("{" + p.Key + "}", v); break;
                    case "arr9": t = t.Replace("{" + p.Key + "}", v); break;
                    case "bool": t = t.Replace("{" + p.Key + "}", v); break;
                    case "acts":
                        {
                            var parts = v.Split(',').Select(x => "\"" + x.Trim() + "\"");
                            t = t.Replace("{" + p.Key + "}", string.Join(",", parts));
                            break;
                        }
                    default: t = t.Replace("{" + p.Key + "}", v); break;
                }
            }
            return t;
        }

        void UpdatePreview()
        {
            if (previewBox == null || selectedCmd == null) return;
            previewBox.Text = RenderTemplate(selectedCmd);
        }

        void RunSelectedCmd()
        {
            if (selectedCmd == null) return;
            string expr = RenderTemplate(selectedCmd);
            long count = 1;
            if (selectedCmd.CountParam != null)
            {
                try { count = Parse(GetVal("count")); }
                catch { count = 1; }
            }
            if (count < 1) count = 1;
            if (count > 100) count = 100;
            BackgroundLoad(() =>
            {
                try
                {
                    string last = "";
                    for (long i = 0; i < count; i++)
                    {
                        var r = pipe.Eval(expr);
                        if (r.StartsWith("ERR")) { Log("错误: " + r.Substring(4) + "  |  " + expr); return; }
                        last = r;
                    }
                    Log((count > 1 ? "已执行 " + count + " 次" : "已执行") + " → " + expr + (count == 1 ? "  结果: " + last : ""));
                }
                catch (Exception ex) { Log("错误: " + ex.Message); }
            });
        }

        // ============ 控制台页 ============
        TextBox consoleInput, consoleHistory;

        FrameworkElement BuildConsolePage()
        {
            return BuildConsolePageInner();
        }

        FrameworkElement BuildConsolePageInner()
        {
            var grid = new Grid { Margin = new Thickness(12) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var c1 = Card();
            c1.Children.Add(Lbl("TJS 表达式控制台（与 MTool krkrConsole 同级）", false, 14));
            c1.Children.Add(Lbl("提示：表达式模式不支持 for/if 顶层语句，可用 (function(){...})() 匿名函数包裹；多命令用逗号表达式。", true, 11));
            var row = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            consoleInput = MakeInput("", 640);
            consoleInput.Height = 30;
            consoleInput.VerticalAlignment = VerticalAlignment.Center;
            consoleInput.KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Enter) RunConsole(); };
            var execBtn = new Button { Content = "执行", Height = 30, MinWidth = 80, Padding = new Thickness(12, 4, 12, 4), Background = BgActive, Foreground = TextMain, BorderBrush = BorderC, BorderThickness = new Thickness(1), FontSize = 12, VerticalContentAlignment = VerticalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0), Cursor = System.Windows.Input.Cursors.Hand };
            execBtn.Click += (s, e) => RunConsole();
            row.Children.Add(consoleInput);
            row.Children.Add(execBtn);
            c1.Children.Add(row);
            Grid.SetRow(c1, 0);
            grid.Children.Add(c1);
            var c2 = new Grid { Background = BgPanel, Margin = new Thickness(0, 0, 0, 10) };
            c2.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            c2.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            c2.Children.Add(Lbl("执行历史", true, 11));
            consoleHistory = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = BgInput, Foreground = TextDim, BorderThickness = new Thickness(0), FontSize = 12, Margin = new Thickness(6, 2, 6, 6), Padding = new Thickness(4) };
            Grid.SetRow(consoleHistory, 1);
            c2.Children.Add(consoleHistory);
            Grid.SetRow(c2, 1);
            grid.Children.Add(c2);
            return grid;
        }

        void RunConsole()
        {
            string code = consoleInput.Text.Trim();
            if (code.Length == 0) return;
            BackgroundLoad(() =>
            {
                try
                {
                    var r = pipe.Eval(code);
                    Log("执行: " + code);
                    Log("结果: " + r);
                    Dispatcher.Invoke(new Action(() =>
                    {
                        consoleHistory.Text = "> " + code + "\r\n= " + r + "\r\n\r\n" + consoleHistory.Text;
                    }));
                }
                catch (Exception ex) { Log("错误: " + ex.Message); }
            });
        }

        // ============ 维护页 ============
        FrameworkElement BuildMaintPage()
        {
            var sv = Page();
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            sv.Content = grid;
            var left = new Grid();
            left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var c1 = Card();
            c1.Children.Add(Lbl("维护", false, 14));
            var row = new WrapPanel();
            var b1 = FlatButton("启动游戏", LaunchGame);
            b1.Width = 170;
            row.Children.Add(b1);
            var b2 = FlatButton("重新加载注册表", () => BackgroundLoad(() => { try { LoadRegistry(); Log("注册表已重载"); } catch (Exception ex) { Log("错误: " + ex.Message); } }));
            b2.Width = 170;
            row.Children.Add(b2);
            var b3 = FlatButton("清空全员背包", ClearAllBags);
            b3.Width = 170;
            row.Children.Add(b3);
            c1.Children.Add(row);
            left.Children.Add(c1);
            var c2 = Card();
            c2.Children.Add(Lbl("角色 o 键对照表（指令参数用，中文（日文））", true, 12));
            var refs = new StringBuilder();
            string[] roleNames = { "主角", "基友", "蓝毛", "洁丽尔", "珊朵拉", "玛丽亚", "北极星女神", "人类", "精灵", "矮人", "诺姆", "风精灵", "肥宅", "牛牛老大" };
            for (int i = 0; i < roleNames.Length; i++)
            {
                string key = CmdLib.ORoleKeys[i];
                string zh = oKeyDisplay.ContainsKey(key) ? oKeyDisplay[key] : "";
                if (zh.Length == 0) zh = roleNames[i];
                refs.AppendLine("· " + FormatBi(zh, key) + "   ←  o." + key);
            }
            var tb = new TextBlock { Text = refs.ToString(), Foreground = TextDim, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
            c2.Children.Add(tb);
            Grid.SetRow(c2, 1);
            left.Children.Add(c2);
            grid.Children.Add(left);
            var right = new Grid { Margin = new Thickness(10, 0, 0, 0) };
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var c3 = Card();
            c3.Children.Add(Lbl("佣兵名单", true, 12));
            var mercLbl = Lbl("ブレイズマン / ソードダンサー(高级曲刀兵) / チャンピオン / エンジェルナイト(天使女武神) / アコライト(术士) / ハイプリースト(高等术士) / ミスティック(召唤师) / メイガス(魔法使) / スナイパー(神箭手) / マスターアーチャー(高级神箭手)", true, 12);
            mercLbl.TextWrapping = TextWrapping.Wrap;
            c3.Children.Add(mercLbl);
            right.Children.Add(c3);
            var c4 = Card();
            c4.Children.Add(Lbl("注意事项", true, 12));
            var noteLbl = Lbl("· 修改器与游戏必须同为运行状态；游戏重启后 Trainer 自动重连\n· 建议修改前先存档；修改均为运行时内存，读档可恢复\n· 杀软如拦截 tb_bridge.tpm / Trainer.exe，请加入白名单\n· 清空背包为数据层重置，游戏内重新打开背包即刷新", true, 12);
            noteLbl.TextWrapping = TextWrapping.Wrap;
            c4.Children.Add(noteLbl);
            Grid.SetRow(c4, 1);
            right.Children.Add(c4);
            Grid.SetColumn(right, 1);
            grid.Children.Add(right);
            return sv;
        }

        void ClearAllBags()
        {
            var r = MessageBox.Show(this, "将清空全部队员背包（bag/bagStat/bagMap 重置为空）。\n建议先存档！确认执行？", "清空背包", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (r != MessageBoxResult.Yes) return;
            BackgroundLoad(() =>
            {
                try
                {
                    int cnt = int.Parse(EvalSafe("game.party.members.count"));
                    for (int i = 0; i < cnt; i++)
                    {
                        EvalSafe("game.party.members[" + i + "].bag = []");
                        EvalSafe("game.party.members[" + i + "].bagStat = []");
                        EvalSafe("game.party.members[" + i + "].bagMap = []");
                    }
                    Log("已清空 " + cnt + " 名队员背包（数据层）");
                    Log("提示：若游戏界面未刷新，重新打开背包即可（setInventory 会从 bag 重建）");
                }
                catch (Exception ex) { Log("错误: " + ex.Message); }
            });
        }
    }

    public class ComboItem
    {
        public string Text;
        public string Val;
        public ComboItem(string text, string val) { Text = text; Val = val; }
        public override string ToString() { return Text; }
    }
}
