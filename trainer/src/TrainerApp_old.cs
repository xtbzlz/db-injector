// TrainerApp.cs — D&B Trainer (WPF, code-only UI, MAA-style dark theme)
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
        ListBox memberList;
        WrapPanel memberFields;
        TextBox consoleInput, consoleHistory;
        List<string> memberNames = new List<string>();
        List<string> allItems = new List<string>();
        List<string> allMagic = new List<string>();
        List<string> charaList = new List<string>();
        ListBox itemList;
        ComboBox itemTargetBox, magicTargetBox;
        string curMagicArray = "mmagic";

        public MainWindow()
        {
            Title = "D&B 修改器 - ダンジョン＆ブライド";
            Width = 1180; Height = 780; MinWidth = 1000; MinHeight = 660;
            Background = BgMain;
            FontFamily = new FontFamily("Microsoft YaHei");
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            BuildUi();
            statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            statusTimer.Tick += (s, e) => RefreshStatus();
            statusTimer.Start();
            RefreshStatus();
            Log("修改器已启动。游戏运行并加载插件后自动连接。");
        }

        void BuildUi()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var top = new DockPanel { Background = BgPanel, Margin = new Thickness(0) };
            top.Children.Add(statusDot = new TextBlock { Text = "\u25CF", Foreground = ErrRed, FontSize = 14, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 6, 0) });
            top.Children.Add(statusText = new TextBlock { Text = "未连接", Foreground = TextMain, VerticalAlignment = VerticalAlignment.Center });
            top.Children.Add(statusInfo = new TextBlock { Text = "", Foreground = TextDim, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) });
            var btnConnect = TopButton("立即连接", () => { pipe.Close(); if (pipe.Connect()) { Log("已连接"); RefreshStatus(); } else Log("连接失败：游戏未运行或插件未加载"); });
            var btnLaunch = TopButton("启动游戏", LaunchGame);
            top.Children.Add(btnConnect);
            top.Children.Add(btnLaunch);
            DockPanel.SetDock(btnConnect, Dock.Right);
            DockPanel.SetDock(btnLaunch, Dock.Right);
            root.Children.Add(top);

            var main = new Grid();
            main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var nav = new StackPanel { Background = BgPanel };
            AddNav(nav, "概览");
            AddNav(nav, "数值修改");
            AddNav(nav, "物品发放");
            AddNav(nav, "魔法学习");
            AddNav(nav, "队伍管理");
            AddNav(nav, "控制台");
            main.Children.Add(nav);
            pageHost = new Grid { Background = BgMain };
            pageHost.SetValue(Grid.ColumnProperty, 1);
            main.Children.Add(pageHost);
            main.SetValue(Grid.RowProperty, 1);
            root.Children.Add(main);

            logBox = new ListBox { Background = BgPanel, Foreground = TextDim, Height = 100, FontSize = 11, Padding = new Thickness(8, 4, 8, 4), BorderThickness = new Thickness(0, 1, 0, 0), BorderBrush = BorderC };
            root.Children.Add(logBox);
            logBox.SetValue(Grid.RowProperty, 2);

            Content = root;
            ShowPage("概览");
        }

        Button TopButton(string text, Action onClick)
        {
            var b = new Button
            {
                Content = text,
                Background = Accent,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(14, 6, 14, 6),
                Margin = new Thickness(6, 0, 0, 0),
                FontSize = 12,
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            b.Click += (s, e) => onClick();
            return b;
        }

        void AddNav(StackPanel nav, string name)
        {
            var btn = new Button
            {
                Content = name,
                Background = BgPanel,
                Foreground = TextMain,
                BorderThickness = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(12, 9, 12, 9),
                Margin = new Thickness(8, 2, 8, 2),
                FontSize = 13,
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            btn.Click += (s, e) => ShowPage(name);
            nav.Children.Add(btn);
            navButtons.Add(btn);
        }

        Button FlatButton(string text, Action onClick)
        {
            var b = new Button
            {
                Content = text,
                Background = BgInput,
                Foreground = TextMain,
                BorderBrush = BorderC,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0, 0, 8, 8),
                FontSize = 12,
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            b.Click += (s, e) => onClick();
            return b;
        }

        TextBox MakeInput(string text = "", double width = 130)
        {
            return new TextBox
            {
                Text = text,
                Background = BgInput,
                Foreground = TextMain,
                BorderBrush = BorderC,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 4, 6, 4),
                Width = width,
                FontSize = 12,
                CaretBrush = TextMain,
            };
        }

        TextBlock Lbl(string text, bool dim = false)
        {
            return new TextBlock { Text = text, Foreground = dim ? TextDim : TextMain, FontSize = 13, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
        }

        StackPanel Card()
        {
            return new StackPanel { Background = BgPanel, Margin = new Thickness(0, 0, 0, 12) };
        }

        ScrollViewer Page()
        {
            var sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(18) };
            sv.Content = new StackPanel();
            return sv;
        }

        void ShowPage(string name)
        {
            foreach (var b in navButtons) b.Background = BgPanel;
            var btn = navButtons.FirstOrDefault(x => (string)x.Content == name);
            if (btn != null) btn.Background = BgActive;
            if (!pages.ContainsKey(name)) pages[name] = BuildPage(name);
            if (pageHost.Children.Count == 0 || pageHost.Children[0] != pages[name])
            {
                pageHost.Children.Clear();
                pageHost.Children.Add(pages[name]);
            }
            foreach (var kv in pages) kv.Value.Visibility = Visibility.Collapsed;
            pages[name].Visibility = Visibility.Visible;
        }

        FrameworkElement BuildPage(string name)
        {
            switch (name)
            {
                case "数值修改": return BuildStatsPage();
                case "物品发放": return BuildItemsPage();
                case "魔法学习": return BuildMagicPage();
                case "队伍管理": return BuildPartyPage();
                case "控制台": return BuildConsolePage();
                default: return BuildOverviewPage();
            }
        }

        // ============ shared helpers ============
        void Log(string msg)
        {
            if (logBox == null) return;
            logBox.Dispatcher.BeginInvoke(new Action(() =>
            {
                var tb = new TextBlock { Text = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg, Foreground = TextDim, TextWrapping = TextWrapping.Wrap };
                logBox.Items.Add(tb);
                while (logBox.Items.Count > 400) logBox.Items.RemoveAt(0);
                logBox.ScrollIntoView(tb);
            }));
        }

        void BackgroundLoad(Action work)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try { work(); }
                catch (Exception ex) { Log("错误: " + ex.Message); }
            });
        }

        void RefreshStatus()
        {
            if (statusDot == null) return;
            bool connected = false;
            try { connected = pipe.Ping() == "PONG"; }
            catch { connected = false; }
            if (!connected) pipe.Close();
            statusDot.Foreground = connected ? OkGreen : ErrRed;
            statusText.Text = connected ? "已连接游戏" : "未连接";
            if (connected)
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        string gold = pipe.Eval("game.party.gold");
                        string cnt = pipe.Eval("game.party.members.count");
                        string info = "";
                        if (!gold.StartsWith("ERR") && !cnt.StartsWith("ERR"))
                            info = "金币:" + gold + "  队员:" + cnt;
                        Dispatcher.BeginInvoke(new Action(() => statusInfo.Text = info));
                    }
                    catch { }
                });
            }
            else statusInfo.Text = "";
        }

        string EvalSafe(string code)
        {
            var r = pipe.Eval(code);
            if (r.StartsWith("ERR")) throw new Exception(r.Substring(4));
            return r;
        }

        void RunCmd(string expr, string okMsg)
        {
            try { var r = EvalSafe(expr); Log(okMsg + " → " + r); }
            catch (Exception ex) { Log("错误: " + ex.Message); }
        }

        // batch operations: build one TJS command per member and eval each
        void RunBatch(string exprTemplate, string okMsg)
        {
            BackgroundLoad(() =>
            {
                int cnt = int.Parse(EvalSafe("game.party.members.count"));
                for (int i = 0; i < cnt; i++)
                    EvalSafe(exprTemplate.Replace("%i%", i.ToString()));
                Log(okMsg);
            });
        }

        long Parse(string s)
        {
            long v;
            if (!long.TryParse(s.Trim(), out v)) throw new Exception("无效数字: " + s);
            return v;
        }

        // ============ pages ============
        FrameworkElement BuildOverviewPage()
        {
            var sv = Page();
            var sp = (StackPanel)sv.Content;
            var c1 = Card();
            c1.Children.Add(Lbl("快速操作"));
            var row = new WrapPanel();
            row.Children.Add(FlatButton("满金 99999999", () => RunCmd("game.party.gold = 99999999", "已设为满金")));
            row.Children.Add(FlatButton("全员满血", () => RunBatch("game.party.members[%i%]._life = game.party.members[%i%]._maxLife", "已全员回满")));
            row.Children.Add(FlatButton("全员满级(99)", () => RunBatch("game.party.members[%i%].level = 99", "已设置等级99")));
            row.Children.Add(FlatButton("全员属性MAX", () => RunBatch("(game.party.members[%i%]._power=999, game.party.members[%i%]._intellect=999, game.party.members[%i%]._piety=999, game.party.members[%i%]._vitality=999, game.party.members[%i%]._speed=999, game.party.members[%i%]._luck=999)", "属性已提升")));
            c1.Children.Add(row);
            sp.Children.Add(c1);
            var c2 = Card();
            c2.Children.Add(Lbl("使用说明", true));
            c2.Children.Add(new TextBlock
            {
                Text = "· 修改器通过插件桥直接调用游戏 TJS 引擎（与 MTool krkrConsole 同等级）。\n" +
                       "· 启动游戏后自动连接；顶栏可一键启动游戏。\n" +
                       "· 物品/魔法/角色清单全部实时从游戏内读取。\n" +
                       "· 控制台支持任意 TJS 表达式，帖子中的 game.* / o.* 命令可直接使用。\n" +
                       "· 批量操作通过逗号表达式展开执行（TJS 顶层无控制流）。\n" +
                       "· 修改后如发现问题，重新读档即可恢复。",
                Foreground = TextDim,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0),
            });
            sp.Children.Add(c2);
            return sv;
        }

        FrameworkElement BuildStatsPage()
        {
            var sv = Page();
            var sp = (StackPanel)sv.Content;
            var c1 = Card();
            c1.Children.Add(Lbl("金钱"));
            var row = new WrapPanel();
            var goldBox = MakeInput("10000", 150);
            row.Children.Add(goldBox);
            row.Children.Add(FlatButton("读取", () => { try { goldBox.Text = EvalSafe("game.party.gold"); } catch (Exception ex) { Log("错误: " + ex.Message); } }));
            row.Children.Add(FlatButton("写入", () =>
            {
                try { long v = Parse(goldBox.Text); EvalSafe("game.party.gold = " + v); Log("金币已设置为 " + v); }
                catch (Exception ex) { Log("错误: " + ex.Message); }
            }));
            row.Children.Add(FlatButton("满金", () =>
            {
                try { EvalSafe("game.party.gold = 99999999"); goldBox.Text = "99999999"; Log("金币已设为 99999999"); }
                catch (Exception ex) { Log("错误: " + ex.Message); }
            }));
            c1.Children.Add(row);
            sp.Children.Add(c1);

            var c2 = Card();
            c2.Children.Add(Lbl("队伍成员"));
            c2.Children.Add(FlatButton("刷新列表", RefreshMemberList));
            memberList = new ListBox { Background = BgInput, Foreground = TextMain, Height = 210, BorderBrush = BorderC, BorderThickness = new Thickness(1), FontSize = 12 };
            memberList.SelectionChanged += (s, e) => LoadMemberFields();
            c2.Children.Add(memberList);
            memberFields = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            c2.Children.Add(memberFields);
            sp.Children.Add(c2);
            return sv;
        }

        void RefreshMemberList()
        {
            BackgroundLoad(() =>
            {
                int cnt = int.Parse(EvalSafe("game.party.members.count"));
                var items = new List<string>();
                var names = new List<string>();
                for (int i = 0; i < cnt; i++)
                {
                    string name = EvalSafe("game.party.members[" + i + "].name");
                    string lv = EvalSafe("game.party.members[" + i + "].level");
                    string hp = EvalSafe("game.party.members[" + i + "]._life") + "/" + EvalSafe("game.party.members[" + i + "]._maxLife");
                    names.Add(name);
                    items.Add(i + "  " + name + "  Lv" + lv + "  HP " + hp);
                }
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    memberNames = names;
                    memberList.ItemsSource = items;
                }));
                Log("队员 " + cnt + " 人");
            });
        }

        void LoadMemberFields()
        {
            memberFields.Children.Clear();
            int idx = memberList.SelectedIndex;
            if (idx < 0 || idx >= memberNames.Count) return;
            int i = idx;
            try
            {
                var f = new WrapPanel();
                f.Children.Add(Lbl(memberNames[i] + " Lv"));
                var lvBox = MakeInput(EvalSafe("game.party.members[" + i + "].level"), 60);
                var hpBox = MakeInput(EvalSafe("game.party.members[" + i + "]._life"), 70);
                var mhpBox = MakeInput(EvalSafe("game.party.members[" + i + "]._maxLife"), 70);
                var expBox = MakeInput(EvalSafe("game.party.members[" + i + "].exp"), 80);
                f.Children.Add(lvBox);
                f.Children.Add(Lbl("HP"));
                f.Children.Add(hpBox);
                f.Children.Add(Lbl("最大HP"));
                f.Children.Add(mhpBox);
                f.Children.Add(Lbl("EXP"));
                f.Children.Add(expBox);
                f.Children.Add(FlatButton("应用", () =>
                {
                    try
                    {
                        EvalSafe("game.party.members[" + i + "].level = " + Parse(lvBox.Text));
                        EvalSafe("game.party.members[" + i + "]._life = " + Parse(hpBox.Text));
                        EvalSafe("game.party.members[" + i + "]._maxLife = " + Parse(mhpBox.Text));
                        EvalSafe("game.party.members[" + i + "].exp = " + Parse(expBox.Text));
                        Log(memberNames[i] + " 已更新");
                    }
                    catch (Exception ex) { Log("错误: " + ex.Message); }
                }));
                f.Children.Add(FlatButton("满血", () =>
                {
                    try
                    {
                        EvalSafe("game.party.members[" + i + "]._life = game.party.members[" + i + "]._maxLife");
                        Log(memberNames[i] + " 已回满");
                    }
                    catch (Exception ex) { Log("错误: " + ex.Message); }
                }));
                f.Children.Add(FlatButton("属性MAX", () =>
                {
                    try
                    {
                        var m = "game.party.members[" + i + "]";
                        EvalSafe(m + "._power=999");
                        EvalSafe(m + "._intellect=999");
                        EvalSafe(m + "._piety=999");
                        EvalSafe(m + "._vitality=999");
                        EvalSafe(m + "._speed=999");
                        EvalSafe(m + "._luck=999");
                        Log(memberNames[i] + " 属性已提升");
                    }
                    catch (Exception ex) { Log("错误: " + ex.Message); }
                }));
                memberFields.Children.Add(f);
            }
            catch (Exception ex) { Log("错误: " + ex.Message); }
        }

        FrameworkElement BuildItemsPage()
        {
            var sv = Page();
            var sp = (StackPanel)sv.Content;
            var c1 = Card();
            c1.Children.Add(Lbl("物品发放（游戏内物品模板，insertItem 安全入包）"));
            var row = new WrapPanel();
            var searchBox = MakeInput("", 200);
            searchBox.TextChanged += (s, e) => FilterItems(searchBox.Text);
            row.Children.Add(Lbl("搜索"));
            row.Children.Add(searchBox);
            row.Children.Add(FlatButton("载入全部物品", LoadItems));
            c1.Children.Add(row);
            itemList = new ListBox { Background = BgInput, Foreground = TextMain, Height = 320, BorderBrush = BorderC, BorderThickness = new Thickness(1), FontSize = 12 };
            c1.Children.Add(itemList);
            var row2 = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            var countBox = MakeInput("1", 60);
            itemTargetBox = MakeCombo(220);
            row2.Children.Add(Lbl("目标角色"));
            row2.Children.Add(itemTargetBox);
            row2.Children.Add(FlatButton("刷新目标", () => RefreshTargetCombo(itemTargetBox)));
            row2.Children.Add(Lbl("数量"));
            row2.Children.Add(countBox);
            row2.Children.Add(FlatButton("发放", () => GiveSelectedItem(countBox)));
            c1.Children.Add(row2);
            sp.Children.Add(c1);
            return sv;
        }

        ComboBox MakeCombo(double width)
        {
            return new ComboBox
            {
                Width = width,
                Background = BgInput,
                Foreground = TextMain,
                BorderBrush = BorderC,
                Margin = new Thickness(0, 0, 8, 8),
                FontSize = 12,
            };
        }

        void RefreshTargetCombo(ComboBox box)
        {
            BackgroundLoad(() =>
            {
                var loaded = new List<string>();
                try
                {
                    int cnt = int.Parse(EvalSafe("game.party.members.count"));
                    for (int i = 0; i < cnt; i++)
                    {
                        string name = EvalSafe("game.party.members[" + i + "].name");
                        loaded.Add(i + "  " + name);
                    }
                }
                catch (Exception ex) { Log("错误: " + ex.Message); return; }
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    box.ItemsSource = loaded;
                    box.SelectedIndex = loaded.Count > 0 ? 0 : -1;
                }));
                Log("目标角色 " + loaded.Count + " 人");
            });
        }

        void LoadItems()
        {
            BackgroundLoad(() =>
            {
                var loaded = new List<string>();
                int cnt = int.Parse(EvalSafe("game.items.count"));
                int batch = 60;
                for (int start = 0; start < cnt; start += batch)
                {
                    int end = Math.Min(start + batch, cnt);
                    var parts = new List<string>();
                    for (int i = start; i < end; i++) parts.Add("game.items[" + i + "].name");
                    string expr = "[" + string.Join(",", parts) + "].join('\\t')";
                    string r = EvalSafe(expr);
                    var names = r.Split('\t');
                    for (int i = 0; i < names.Length && start + i < cnt; i++)
                        loaded.Add(start + i + "\t" + names[i]);
                }
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    allItems = loaded;
                    itemList.ItemsSource = allItems.ToList();
                }));
                Log("已载入 " + cnt + " 种物品");
            });
        }

        void FilterItems(string keyword)
        {
            if (allItems.Count == 0 || itemList == null) return;
            var k = keyword.Trim();
            if (k == "") { itemList.ItemsSource = allItems.ToList(); return; }
            itemList.ItemsSource = allItems.Where(x => x.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        }

        void GiveSelectedItem(TextBox countBox)
        {
            if (itemList.SelectedIndex < 0) { Log("请先选择物品"); return; }
            // read UI values on the UI thread
            string sel = (string)itemList.SelectedItem;
            int itemId = int.Parse(sel.Split('\t')[0]);
            long n;
            try { n = Parse(countBox.Text); }
            catch (Exception ex) { Log("错误: " + ex.Message); return; }
            if (n < 1) n = 1;
            if (n > 10) { Log("数量上限 10（每件物品占一个背包格子，避免背包爆满）"); n = 10; }
            int target = itemTargetBox.SelectedIndex; // -1 = 主角队伍
            BackgroundLoad(() =>
            {
                try
                {
                    for (long k = 0; k < n; k++)
                    {
                        string cmd = target >= 0
                            ? "game.party.members[" + target + "].insertItem(game.items[" + itemId + "])"
                            : "game.party.insertItem(game.items[" + itemId + "])";
                        string r = EvalSafe(cmd);
                        if (r.StartsWith("ERR")) { Log("发放失败: " + r); return; }
                    }
                    Log("已发放: " + sel + " x" + n);
                }
                catch (Exception ex) { Log("错误: " + ex.Message); }
            });
        }

        FrameworkElement BuildMagicPage()
        {
            var sv = Page();
            var sp = (StackPanel)sv.Content;
            var c1 = Card();
            c1.Children.Add(Lbl("魔法学习（选择目标角色后学习）"));
            var row = new WrapPanel();
            var magicList = new ListBox { Background = BgInput, Foreground = TextMain, Height = 300, BorderBrush = BorderC, BorderThickness = new Thickness(1), FontSize = 12 };
            magicList.ItemsSource = new[] { "点击上方按钮载入魔法列表" };
            magicTargetBox = MakeCombo(220);
            row.Children.Add(Lbl("目标角色"));
            row.Children.Add(magicTargetBox);
            row.Children.Add(FlatButton("刷新目标", () => RefreshTargetCombo(magicTargetBox)));
            row.Children.Add(FlatButton("载入精灵魔法", () => LoadMagic(magicList, "mmagic")));
            row.Children.Add(FlatButton("载入神圣魔法", () => LoadMagic(magicList, "pmagic")));
            row.Children.Add(FlatButton("载入炼金魔法", () => LoadMagic(magicList, "amagic")));
            row.Children.Add(FlatButton("载入召唤魔法", () => LoadMagic(magicList, "smagic")));
            row.Children.Add(FlatButton("学会所选", () => LearnSelected(magicList)));
            row.Children.Add(FlatButton("该系上限全9", () => SetMagicMax()));
            c1.Children.Add(row);
            c1.Children.Add(magicList);
            sp.Children.Add(c1);
            return sv;
        }

        void LoadMagic(ListBox list, string arr)
        {
            curMagicArray = arr;
            BackgroundLoad(() =>
            {
                var loaded = new List<string>();
                int cnt = int.Parse(EvalSafe("game." + arr + ".count"));
                var parts = new List<string>();
                for (int i = 0; i < cnt; i++) parts.Add("game." + arr + "[" + i + "].name");
                string expr = "[" + string.Join(",", parts) + "].join('\\t')";
                string r = EvalSafe(expr);
                var names = r.Split('\t');
                for (int i = 0; i < names.Length && i < cnt; i++)
                    loaded.Add(i + "\t" + names[i]);
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    allMagic = loaded;
                    list.ItemsSource = allMagic.ToList();
                }));
                Log(arr + " 载入 " + cnt + " 种");
            });
        }

        void LearnSelected(ListBox list)
        {
            if (list.SelectedIndex < 0) { Log("请先选择魔法"); return; }
            // read UI values on the UI thread
            int magicId = int.Parse(((string)list.SelectedItem).Split('\t')[0]);
            string itemText = (string)list.SelectedItem;
            int target = magicTargetBox.SelectedIndex; // -1 = 队伍第1位
            BackgroundLoad(() =>
            {
                try
                {
                    if (target >= 0)
                        EvalSafe("game.party.members[" + target + "].learnSkill(game." + curMagicArray + "[" + magicId + "])");
                    else
                        EvalSafe("game.party.members[0].learnSkill(game." + curMagicArray + "[" + magicId + "])");
                    Log("已学习: " + itemText);
                }
                catch (Exception ex) { Log("错误: " + ex.Message); }
            });
        }

        void SetMagicMax()
        {
            int target = magicTargetBox.SelectedIndex;
            BackgroundLoad(() =>
            {
                try
                {
                    int cnt = int.Parse(EvalSafe("game." + curMagicArray + ".count"));
                    var m = "game.party.members[" + (target >= 0 ? target.ToString() : "0") + "]." + curMagicArray + "Max";
                    EvalSafe(m + " = [" + string.Join(",", Enumerable.Repeat("9", cnt)) + "]");
                    Log(curMagicArray + "Max 全部设为 9");
                }
                catch (Exception ex) { Log("错误: " + ex.Message); }
            });
        }

        FrameworkElement BuildPartyPage()
        {
            var sv = Page();
            var sp = (StackPanel)sv.Content;
            var c1 = Card();
            c1.Children.Add(Lbl("角色列表（game.chara 全角色模板）"));
            var row = new WrapPanel();
            var charList = new ListBox { Background = BgInput, Foreground = TextMain, Height = 280, BorderBrush = BorderC, BorderThickness = new Thickness(1), FontSize = 12 };
            row.Children.Add(FlatButton("载入角色", () => LoadCharaList(charList)));
            row.Children.Add(FlatButton("加入主力队伍(party.entry)", () => EntrySelected(charList, true)));
            row.Children.Add(FlatButton("加入支援队伍(guest.entry)", () => EntrySelected(charList, false)));
            row.Children.Add(FlatButton("清空全员背包", ClearAllBags));
            c1.Children.Add(row);
            c1.Children.Add(charList);
            sp.Children.Add(c1);
            return sv;
        }

        void LoadCharaList(ListBox list)
        {
            BackgroundLoad(() =>
            {
                var loaded = new List<string>();
                int cnt = int.Parse(EvalSafe("game.chara.count"));
                var parts = new List<string>();
                for (int i = 0; i < cnt; i++) parts.Add("game.chara[" + i + "].name");
                string expr = "[" + string.Join(",", parts) + "].join('\\t')";
                string r = EvalSafe(expr);
                var names = r.Split('\t');
                for (int i = 0; i < names.Length && i < cnt; i++)
                    loaded.Add(i + "\t" + names[i]);
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    charaList = loaded;
                    list.ItemsSource = charaList.ToList();
                }));
                Log("角色 " + cnt + " 人");
            });
        }

        void EntrySelected(ListBox list, bool party)
        {
            if (list.SelectedIndex < 0) { Log("请先选择角色"); return; }
            int charId = int.Parse(((string)list.SelectedItem).Split('\t')[0]);
            string itemText = (string)list.SelectedItem;
            BackgroundLoad(() =>
            {
                try
                {
                    string cmd = party ? "game.party.entry(game.chara[" + charId + "])" : "game.guest.entry(game.chara[" + charId + "])";
                    EvalSafe(cmd);
                    Log("已加入" + (party ? "主力队伍" : "支援队伍") + ": " + itemText);
                }
                catch (Exception ex) { Log("错误: " + ex.Message); }
            });
        }

        FrameworkElement BuildConsolePage()
        {
            var sv = Page();
            var sp = (StackPanel)sv.Content;
            var c1 = Card();
            c1.Children.Add(Lbl("TJS 控制台（单表达式，game.* / o.* 命令直接可用）"));
            var row = new WrapPanel();
            consoleInput = MakeInput("game.party.gold", 480);
            consoleInput.KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Enter) RunConsole(); };
            row.Children.Add(consoleInput);
            row.Children.Add(FlatButton("执行", RunConsole));
            c1.Children.Add(row);
            consoleHistory = new TextBox
            {
                Background = BgInput,
                Foreground = TextMain,
                BorderBrush = BorderC,
                BorderThickness = new Thickness(1),
                FontSize = 12,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Height = 320,
                Margin = new Thickness(0, 10, 0, 0),
            };
            c1.Children.Add(consoleHistory);
            sp.Children.Add(c1);
            return sv;
        }

        void RunConsole()
        {
            string code = consoleInput.Text.Trim();
            if (code == "") return;
            BackgroundLoad(() =>
            {
                try
                {
                    string r = EvalSafe(code);
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        consoleHistory.Text = "> " + code + "\n= " + r + "\n\n" + consoleHistory.Text;
                        if (consoleHistory.Text.Length > 30000) consoleHistory.Text = consoleHistory.Text.Substring(0, 30000);
                    }));
                    Log("控制台: " + code);
                }
                catch (Exception ex)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        consoleHistory.Text = "> " + code + "\n! " + ex.Message + "\n\n" + consoleHistory.Text;
                    }));
                    Log("控制台错误: " + ex.Message);
                }
            });
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

        void LaunchGame()
        {
            try
            {
                var root = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\CR";
                string gameExe = null;
                if (System.IO.Directory.Exists(root))
                {
                    foreach (var d in System.IO.Directory.GetDirectories(root))
                    {
                        var exe = System.IO.Path.Combine(d, "game64.exe");
                        if (System.IO.File.Exists(exe)) { gameExe = exe; break; }
                    }
                }
                if (gameExe == null) { Log("未找到 game64.exe，请手动启动游戏"); return; }
                Process.Start(gameExe);
                Log("已启动: " + gameExe);
            }
            catch (Exception ex) { Log("启动失败: " + ex.Message); }
        }
    }
}
