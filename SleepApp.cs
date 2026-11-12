using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

public class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        bool dry = false;
        bool simulate = false;
        string shot = null;
        bool shotCount = false;
        string logPath = null;
        // standby = 关屏进入 S0 Modern Standby（默认，外接键盘保持供电）
        // hibernate = 明确进入 S4 休眠；legacy = 旧的 SetSuspendState(false,...) 行为
        string mode = "standby";
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--dry" || args[i] == "/dry") dry = true;
            if (args[i] == "--simulate") simulate = true;
            if (args[i] == "--shot" && i + 1 < args.Length) shot = args[i + 1];
            if (args[i] == "--shotcount") shotCount = true;
            if (args[i] == "--log" && i + 1 < args.Length) logPath = args[i + 1];
            if (args[i] == "--mode" && i + 1 < args.Length) mode = args[i + 1].ToLowerInvariant();
        }
        Application app = new Application();
        app.ShutdownMode = ShutdownMode.OnMainWindowClose;
        SleepWindow w = new SleepWindow(dry, mode);
        if (shot != null) w.EnableShotMode(shot, shotCount);
        if (logPath != null) w.EnableLog(logPath);
        if (simulate) w.EnableSimulate();
        app.Run(w);
    }
}

public class SleepWindow : Window
{
    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    // Modern Standby (S0) 不是 SYSTEM_POWER_STATE，legacy 的 SetSuspendState(false,...)
    // 在只有 S0 + S4 的机器上会被电源管理器降级成 S4 休眠。
    // 「开始菜单 -> 睡眠」在这类机器上的实际语义是关屏，由系统自动进入 S0 低功耗空闲。
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam,
        uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

    private const int HWND_BROADCAST = 0xFFFF;
    private const uint WM_SYSCOMMAND = 0x0112;
    private const int SC_MONITORPOWER = 0xF170;
    private const int MONITOR_OFF = 2;
    private const uint SMTO_ABORTIFHUNG = 0x0002;

    public static readonly DependencyProperty SweepProperty = DependencyProperty.Register(
        "Sweep", typeof(double), typeof(SleepWindow),
        new PropertyMetadata(360.0, new PropertyChangedCallback(OnSweepChanged)));

    public double Sweep
    {
        get { return (double)GetValue(SweepProperty); }
        set { SetValue(SweepProperty, value); }
    }

    private static void OnSweepChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((SleepWindow)d).UpdateArc((double)e.NewValue);
    }

    // ---------- 全局尺寸系数：改这一个数就能整体缩放 ----------
    private const double K = 2.0;

    // ---------- 字段 ----------
    private readonly bool dryRun;
    private readonly string sleepMode;
    private bool simulate;
    private string shotPath;
    private bool shotCountMode;
    private Border card;
    private Border iconTile;
    private TextBlock titleText;
    private TextBlock descText;
    private Grid stateAsk;
    private Grid stateCount;
    private Path arcPath;
    private TextBlock bigNumber;
    private TextBlock unitText;
    private Border cancelBtn;
    private Border okBtn;
    private TextBlock okLabel;
    private TextBlock cancelLabel;
    private DispatcherTimer timer;
    private DateTime countStart;
    private int lastShown;
    private bool counting;
    private bool sleeping;

    private const double CardW = 560 * K;
    private const double RingSize = 148 * K;
    private const double RingThick = 4 * K;

    private static readonly Color AccentA = Color.FromRgb(0xA7, 0x9B, 0xFF);
    private static readonly Color AccentB = Color.FromRgb(0x6C, 0x8C, 0xFF);

    public SleepWindow(bool dry, string mode)
    {
        dryRun = dry;
        sleepMode = (mode == "hibernate" || mode == "legacy") ? mode : "standby";

        Title = "进入睡眠";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        Width = CardW + 64 * K;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI");
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);

        BuildUi();

        Loaded += OnLoaded;
        KeyDown += OnKeyDown;
    }

    // ================= UI =================
    private string logFile;

    public void EnableLog(string path) { logFile = path; }

    // v1.2: 走完整的睡前/唤醒后流程但不发任何电源动作，用于自动化验证
    public void EnableSimulate() { simulate = true; }

    private void Log(string msg)
    {
        if (logFile == null) return;
        try { System.IO.File.AppendAllText(logFile, msg + "\n"); } catch { }
    }

    public void EnableShotMode(string path, bool countdown)
    {
        shotPath = path;
        shotCountMode = countdown;
    }

    private void BuildUi()
    {
        card = new Border();
        card.CornerRadius = new CornerRadius(22 * K);
        card.Background = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x20));
        card.BorderThickness = new Thickness(1);
        card.BorderBrush = new SolidColorBrush(Color.FromArgb(0x1C, 0xFF, 0xFF, 0xFF));
        DropShadowEffect sh = new DropShadowEffect();
        sh.BlurRadius = 30 * K;
        sh.ShadowDepth = 10 * K;
        sh.Direction = 270;
        sh.Opacity = 0.55;
        sh.Color = Colors.Black;
        card.Effect = sh;

        Grid inner = new Grid();

        Border aurora = new Border();
        aurora.CornerRadius = new CornerRadius(22 * K);
        RadialGradientBrush rg = new RadialGradientBrush();
        rg.Center = new Point(0.5, -0.15);
        rg.GradientOrigin = new Point(0.5, -0.15);
        rg.RadiusX = 0.95;
        rg.RadiusY = 1.0;
        rg.GradientStops.Add(new GradientStop(Color.FromArgb(0x3A, 0x8B, 0x7C, 0xF7), 0.0));
        rg.GradientStops.Add(new GradientStop(Color.FromArgb(0x14, 0x5E, 0x7B, 0xEF), 0.45));
        rg.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 0x18, 0x18, 0x20), 0.78));
        aurora.Background = rg;
        aurora.IsHitTestVisible = false;
        inner.Children.Add(aurora);

        Border sheen = new Border();
        sheen.Height = 1.5;
        sheen.VerticalAlignment = VerticalAlignment.Top;
        sheen.Margin = new Thickness(40 * K, 0, 40 * K, 0);
        LinearGradientBrush sgb = new LinearGradientBrush();
        sgb.StartPoint = new Point(0, 0);
        sgb.EndPoint = new Point(1, 0);
        sgb.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0xFF, 0xFF, 0xFF), 0));
        sgb.GradientStops.Add(new GradientStop(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF), 0.5));
        sgb.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0xFF, 0xFF, 0xFF), 1));
        sheen.Background = sgb;
        sheen.IsHitTestVisible = false;
        inner.Children.Add(sheen);

        StackPanel pad = new StackPanel();
        pad.Margin = new Thickness(38 * K, 30 * K, 38 * K, 28 * K);

        // ---------- 视觉区：图标 / 圆环 ----------
        Grid visual = new Grid();
        visual.Height = RingSize + 16;

        stateAsk = new Grid();
        iconTile = new Border();
        iconTile.Width = 108 * K;
        iconTile.Height = 108 * K;
        iconTile.CornerRadius = new CornerRadius(34 * K);
        iconTile.Background = new LinearGradientBrush(Color.FromArgb(0x38, 0x8B, 0x7C, 0xF7), Color.FromArgb(0x1A, 0x5E, 0x7B, 0xEF), 90);
        iconTile.BorderThickness = new Thickness(1.5);
        iconTile.BorderBrush = new SolidColorBrush(Color.FromArgb(0x38, 0xA7, 0x9B, 0xFF));
        iconTile.HorizontalAlignment = HorizontalAlignment.Center;
        iconTile.VerticalAlignment = VerticalAlignment.Center;

        Path moon = new Path();
        moon.Data = MoonGeometry(27 * K, 27 * K, 27 * K);
        moon.Fill = new LinearGradientBrush(Color.FromRgb(0xE4, 0xDF, 0xFF), Color.FromRgb(0x9B, 0x8C, 0xFA), 45);
        moon.Stretch = Stretch.None;
        moon.HorizontalAlignment = HorizontalAlignment.Center;
        moon.VerticalAlignment = VerticalAlignment.Center;
        iconTile.Child = moon;

        Border halo = new Border();
        halo.Width = 160 * K;
        halo.Height = 160 * K;
        halo.CornerRadius = new CornerRadius(54 * K);
        RadialGradientBrush hg = new RadialGradientBrush();
        hg.Center = new Point(0.5, 0.5);
        hg.GradientOrigin = new Point(0.5, 0.5);
        hg.GradientStops.Add(new GradientStop(Color.FromArgb(0x2E, 0x8B, 0x7C, 0xF7), 0));
        hg.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 0x8B, 0x7C, 0xF7), 1));
        halo.Background = hg;
        halo.IsHitTestVisible = false;

        Grid iconStack = new Grid();
        iconStack.HorizontalAlignment = HorizontalAlignment.Center;
        iconStack.VerticalAlignment = VerticalAlignment.Center;
        iconStack.Children.Add(halo);
        iconStack.Children.Add(iconTile);
        stateAsk.Children.Add(iconStack);
        visual.Children.Add(stateAsk);

        stateCount = new Grid();
        stateCount.Opacity = 0;
        stateCount.IsHitTestVisible = false;

        Grid ringBox = new Grid();
        ringBox.Width = RingSize;
        ringBox.Height = RingSize;
        ringBox.HorizontalAlignment = HorizontalAlignment.Center;
        ringBox.VerticalAlignment = VerticalAlignment.Center;

        Ellipse track = new Ellipse();
        track.Width = RingSize;
        track.Height = RingSize;
        track.StrokeThickness = RingThick;
        track.Stroke = new SolidColorBrush(Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF));
        ringBox.Children.Add(track);

        arcPath = new Path();
        arcPath.StrokeThickness = RingThick;
        arcPath.Stroke = new LinearGradientBrush(AccentA, AccentB, 45);
        arcPath.StrokeStartLineCap = PenLineCap.Round;
        arcPath.StrokeEndLineCap = PenLineCap.Round;
        arcPath.HorizontalAlignment = HorizontalAlignment.Left;
        arcPath.VerticalAlignment = VerticalAlignment.Top;
        ringBox.Children.Add(arcPath);
        UpdateArc(360);

        StackPanel numBox = new StackPanel();
        numBox.HorizontalAlignment = HorizontalAlignment.Center;
        numBox.VerticalAlignment = VerticalAlignment.Center;

        bigNumber = new TextBlock();
        bigNumber.Text = "3";
        bigNumber.FontSize = 46 * K;
        bigNumber.FontWeight = FontWeights.Light;
        bigNumber.Foreground = new SolidColorBrush(Color.FromRgb(0xF4, 0xF3, 0xFB));
        bigNumber.HorizontalAlignment = HorizontalAlignment.Center;
        bigNumber.TextAlignment = TextAlignment.Center;
        numBox.Children.Add(bigNumber);

        unitText = new TextBlock();
        unitText.Text = "秒后睡眠";
        unitText.FontSize = 12 * K;
        unitText.Foreground = new SolidColorBrush(Color.FromRgb(0x82, 0x82, 0x9C));
        unitText.HorizontalAlignment = HorizontalAlignment.Center;
        unitText.TextAlignment = TextAlignment.Center;
        unitText.Margin = new Thickness(0, 4 * K, 0, 0);
        numBox.Children.Add(unitText);

        ringBox.Children.Add(numBox);
        stateCount.Children.Add(ringBox);
        visual.Children.Add(stateCount);

        pad.Children.Add(visual);

        // ---------- 文案 ----------
        titleText = new TextBlock();
        titleText.Text = "进入睡眠";
        titleText.FontSize = 25 * K;
        titleText.FontWeight = FontWeights.SemiBold;
        titleText.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xFA));
        titleText.HorizontalAlignment = HorizontalAlignment.Center;
        titleText.TextAlignment = TextAlignment.Center;
        titleText.Margin = new Thickness(0, 6 * K, 0, 0);
        pad.Children.Add(titleText);

        descText = new TextBlock();
        descText.Text = "让这台电脑现在进入睡眠模式？";
        descText.FontSize = 14 * K;
        descText.Foreground = new SolidColorBrush(Color.FromRgb(0x93, 0x93, 0xAA));
        descText.HorizontalAlignment = HorizontalAlignment.Center;
        descText.TextAlignment = TextAlignment.Center;
        descText.TextWrapping = TextWrapping.Wrap;
        descText.Margin = new Thickness(0, 9 * K, 0, 0);
        pad.Children.Add(descText);

        // ---------- 按钮 ----------
        StackPanel btns = new StackPanel();
        btns.Orientation = Orientation.Horizontal;
        btns.HorizontalAlignment = HorizontalAlignment.Center;
        btns.Margin = new Thickness(0, 26 * K, 0, 0);

        cancelBtn = MakeButton("取消", 138 * K, 48 * K, false);
        okBtn = MakeButton("进入睡眠", 176 * K, 48 * K, true);
        cancelLabel = (TextBlock)cancelBtn.Child;
        okLabel = (TextBlock)okBtn.Child;
        cancelBtn.Margin = new Thickness(0, 0, 12 * K, 0);
        cancelBtn.MouseLeftButtonUp += delegate { ExitCancel(); };
        okBtn.MouseLeftButtonUp += delegate { OnOkClick(); };
        btns.Children.Add(cancelBtn);
        btns.Children.Add(okBtn);
        pad.Children.Add(btns);

        // ---------- 快捷键附注 ----------
        StackPanel hintRow = new StackPanel();
        hintRow.Orientation = Orientation.Horizontal;
        hintRow.HorizontalAlignment = HorizontalAlignment.Center;
        hintRow.Margin = new Thickness(0, 18 * K, 0, 0);

        hintRow.Children.Add(KeyChip("Enter"));
        hintRow.Children.Add(HintLabel("确认", 20 * K));
        hintRow.Children.Add(KeyChip("Esc"));
        hintRow.Children.Add(HintLabel("取消", 0));

        pad.Children.Add(hintRow);

        inner.Children.Add(pad);
        card.Child = inner;
        card.MouseLeftButtonDown += OnDrag;

        Grid outer = new Grid();
        outer.Margin = new Thickness(32 * K, 26 * K, 32 * K, 32 * K);
        outer.Children.Add(card);
        Content = outer;

        outer.RenderTransform = new TranslateTransform(0, 14 * K);
        Opacity = 0;
    }

    private static TextBlock HintLabel(string text, double rightMargin)
    {
        TextBlock t = new TextBlock();
        t.Text = text;
        t.FontSize = 11.5 * K;
        t.Foreground = new SolidColorBrush(Color.FromRgb(0x72, 0x72, 0x8A));
        t.VerticalAlignment = VerticalAlignment.Center;
        t.Margin = new Thickness(8 * K, 0, rightMargin, 0);
        return t;
    }

    private static Border KeyChip(string text)
    {
        Border b = new Border();
        b.CornerRadius = new CornerRadius(6 * K);
        b.Padding = new Thickness(8 * K, 3 * K, 8 * K, 4 * K);
        b.Background = new SolidColorBrush(Color.FromArgb(0x0D, 0xFF, 0xFF, 0xFF));
        b.BorderThickness = new Thickness(1);
        b.BorderBrush = new SolidColorBrush(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF));
        TextBlock t = new TextBlock();
        t.Text = text;
        t.FontSize = 11 * K;
        t.Foreground = new SolidColorBrush(Color.FromRgb(0xA8, 0xA8, 0xBE));
        b.Child = t;
        return b;
    }

    private static Geometry MoonGeometry(double cx, double cy, double r)
    {
        EllipseGeometry outer = new EllipseGeometry(new Point(cx, cy), r, r);
        EllipseGeometry cut = new EllipseGeometry(new Point(cx + r * 0.45, cy - r * 0.32), r * 0.88, r * 0.88);
        return Geometry.Combine(outer, cut, GeometryCombineMode.Exclude, null);
    }

    private void UpdateArc(double sweepDeg)
    {
        if (arcPath == null) return;
        double r = (RingSize - RingThick) / 2.0;
        double cx = RingSize / 2.0;
        double cy = RingSize / 2.0;

        if (sweepDeg >= 359.5)
        {
            arcPath.Data = new EllipseGeometry(new Point(cx, cy), r, r);
            return;
        }
        if (sweepDeg <= 0.3)
        {
            arcPath.Data = Geometry.Empty;
            return;
        }

        double start = -90.0;
        double end = start + sweepDeg;
        double rad = Math.PI / 180.0;
        Point p1 = new Point(cx + r * Math.Cos(start * rad), cy + r * Math.Sin(start * rad));
        Point p2 = new Point(cx + r * Math.Cos(end * rad), cy + r * Math.Sin(end * rad));

        PathFigure fig = new PathFigure();
        fig.StartPoint = p1;
        fig.IsClosed = false;
        ArcSegment seg = new ArcSegment();
        seg.Point = p2;
        seg.Size = new Size(r, r);
        seg.IsLargeArc = sweepDeg > 180;
        seg.SweepDirection = SweepDirection.Clockwise;
        seg.IsStroked = true;
        fig.Segments.Add(seg);

        PathGeometry pg = new PathGeometry();
        pg.Figures.Add(fig);
        arcPath.Data = pg;
    }

    private Border MakeButton(string text, double width, double height, bool primary)
    {
        Border b = new Border();
        b.Width = width;
        b.Height = height;
        b.CornerRadius = new CornerRadius(12 * K);
        b.Cursor = Cursors.Hand;

        TextBlock t = new TextBlock();
        t.Text = text;
        t.FontSize = 14 * K;
        t.HorizontalAlignment = HorizontalAlignment.Center;
        t.VerticalAlignment = VerticalAlignment.Center;
        b.Child = t;

        if (primary)
        {
            b.Background = new SolidColorBrush(Color.FromRgb(0xF3, 0xF3, 0xF7));
            t.Foreground = new SolidColorBrush(Color.FromRgb(0x12, 0x12, 0x1A));
            t.FontWeight = FontWeights.SemiBold;
            b.MouseEnter += delegate { b.Background = new SolidColorBrush(Colors.White); };
            b.MouseLeave += delegate { b.Background = new SolidColorBrush(Color.FromRgb(0xF3, 0xF3, 0xF7)); };
        }
        else
        {
            b.Background = new SolidColorBrush(Color.FromArgb(0x09, 0xFF, 0xFF, 0xFF));
            b.BorderThickness = new Thickness(1);
            b.BorderBrush = new SolidColorBrush(Color.FromArgb(0x1C, 0xFF, 0xFF, 0xFF));
            t.Foreground = new SolidColorBrush(Color.FromRgb(0xCD, 0xCD, 0xDC));
            b.MouseEnter += delegate
            {
                b.Background = new SolidColorBrush(Color.FromArgb(0x16, 0xFF, 0xFF, 0xFF));
                b.BorderBrush = new SolidColorBrush(Color.FromArgb(0x2E, 0xFF, 0xFF, 0xFF));
            };
            b.MouseLeave += delegate
            {
                b.Background = new SolidColorBrush(Color.FromArgb(0x09, 0xFF, 0xFF, 0xFF));
                b.BorderBrush = new SolidColorBrush(Color.FromArgb(0x1C, 0xFF, 0xFF, 0xFF));
            };
        }

        b.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) { e.Handled = true; };
        return b;
    }

    // ================= 交互 =================
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Activate();
        Focus();
        Log("LOADED");

        if (shotPath != null)
        {
            if (shotCountMode) StartCountdown();
            DispatcherTimer st = new DispatcherTimer();
            st.Interval = TimeSpan.FromMilliseconds(shotCountMode ? 1300 : 900);
            st.Tick += delegate
            {
                st.Stop();
                try
                {
                    int w = (int)Math.Ceiling(ActualWidth);
                    int h = (int)Math.Ceiling(ActualHeight);
                    RenderTargetBitmap rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                    rtb.Render(this);
                    PngBitmapEncoder enc = new PngBitmapEncoder();
                    enc.Frames.Add(BitmapFrame.Create(rtb));
                    System.IO.FileStream fs = new System.IO.FileStream(shotPath, System.IO.FileMode.Create);
                    enc.Save(fs);
                    fs.Close();
                    System.IO.File.WriteAllText(shotPath + ".txt",
                        string.Format("opacity={0} w={1} h={2}", Opacity, w, h));
                }
                catch (Exception ex)
                {
                    System.IO.File.WriteAllText(shotPath + ".err", ex.ToString());
                }
                Close();
            };
            st.Start();
        }

        DoubleAnimation fade = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(260)));
        fade.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
        BeginAnimation(OpacityProperty, fade);

        TranslateTransform tr = (TranslateTransform)((FrameworkElement)Content).RenderTransform;
        DoubleAnimation ty = new DoubleAnimation(14 * K, 0, new Duration(TimeSpan.FromMilliseconds(320)));
        ty.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
        tr.BeginAnimation(TranslateTransform.YProperty, ty);

        DoubleAnimation glow = new DoubleAnimation(0.75, 1.0, new Duration(TimeSpan.FromMilliseconds(1800)));
        glow.AutoReverse = true;
        glow.RepeatBehavior = RepeatBehavior.Forever;
        glow.EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut };
        iconTile.BeginAnimation(OpacityProperty, glow);
    }

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is Border)
        {
            Border b = (Border)e.OriginalSource;
            if (b.Cursor == Cursors.Hand) return;
        }
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            try { DragMove(); } catch { }
        }
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            ExitCancel();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            OnOk();
            e.Handled = true;
        }
    }

    private void OnOkClick()
    {
        if (sleeping) return;
        if (counting) DoSleep();
        else StartCountdown();
    }

    private void OnOk()
    {
        if (sleeping) return;
        if (counting) return;
        StartCountdown();
    }

    private void StartCountdown()
    {
        counting = true;
        lastShown = 3;

        Log("COUNTDOWN_START");
        titleText.Text = "即将进入睡眠";
        descText.Text = "按 Esc 或「取消」可以中断";
        okLabel.Text = "立即睡眠";

        DoubleAnimation outA = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(160)));
        outA.Completed += delegate { stateAsk.Visibility = Visibility.Collapsed; };
        stateAsk.BeginAnimation(OpacityProperty, outA);

        stateCount.IsHitTestVisible = true;
        DoubleAnimation inB = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(280)));
        inB.BeginTime = TimeSpan.FromMilliseconds(90);
        inB.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
        stateCount.BeginAnimation(OpacityProperty, inB);

        DoubleAnimation sweepAnim = new DoubleAnimation(360, 0, new Duration(TimeSpan.FromSeconds(3)));
        BeginAnimation(SweepProperty, sweepAnim);

        countStart = DateTime.Now;
        timer = new DispatcherTimer();
        timer.Interval = TimeSpan.FromMilliseconds(40);
        timer.Tick += OnTick;
        timer.Start();
    }

    private void OnTick(object sender, EventArgs e)
    {
        double elapsed = (DateTime.Now - countStart).TotalSeconds;
        int remain = (int)Math.Ceiling(3 - elapsed);
        if (remain < 0) remain = 0;
        if (remain != lastShown && remain > 0)
        {
            lastShown = remain;
            bigNumber.Text = remain.ToString();
            Pulse();
        }
        if (elapsed >= 3)
        {
            timer.Stop();
            counting = false;
            DoSleep();
        }
    }

    private void Pulse()
    {
        DoubleAnimation a = new DoubleAnimation(1, 0.45, new Duration(TimeSpan.FromMilliseconds(150)));
        a.AutoReverse = true;
        bigNumber.BeginAnimation(OpacityProperty, a);
    }

    private void DoSleep()
    {
        if (sleeping) return;
        Log("SLEEP_TRIGGERED mode=" + sleepMode + " v=1.2");
        sleeping = true;
        counting = false;
        if (timer != null) timer.Stop();

        // standby 走 S0（关屏）；hibernate / legacy 走 S4，文案要如实说明
        bool deep = (sleepMode != "standby");
        titleText.Text = deep ? "正在进入休眠" : "正在进入睡眠";
        descText.Text = deep ? "正在写入休眠文件…" : "屏幕熄灭后系统会自动进入低功耗待机…";
        okLabel.Text = "请稍候";
        okBtn.IsEnabled = false;
        okBtn.Opacity = 0.55;
        cancelLabel.Text = "关闭";
        unitText.Text = deep ? "即将休眠" : "即将睡眠";

        if (dryRun)
        {
            descText.Text = "[测试模式] 已到触发点，未执行真实动作（模式：" + sleepMode + "）";
            okLabel.Text = "关闭";
            okBtn.IsEnabled = true;
            okBtn.Opacity = 1;
            sleeping = false;
            return;
        }

        // v1.2 修复 1：先从 DWM 合成树撤掉窗口，再发任何电源动作。
        // v1.1 把 Close() 排在关屏广播之后，而关屏会立刻触发 S0 待机（506 的原因就是
        // SC_MONITORPOWER），窗口销毁来不及提交，唤醒后 DWM 恢复的仍是「正在进入睡眠」那一帧。
        StopAnimations();
        Hide();

        Application app = Application.Current;
        if (app != null) app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // v1.2 修复 2：所有阻塞式 P/Invoke 一律离开 Dispatcher 线程。
        // v1.1 在 UI 线程上广播 HWND_BROADCAST（2 秒超时），被 WPS 之类窗口拖住就会被判
        // AppHangXProcB1；SetSuspendState 更要等唤醒才返回，绝不能占着 UI 线程。
        System.Threading.ThreadPool.QueueUserWorkItem(delegate(object state)
        {
            bool ok = true;
            string err = "";
            try
            {
                if (simulate)
                {
                    System.Threading.Thread.Sleep(200);
                    Log("SIMULATE_SKIP_POWER_ACTION");
                }
                else if (!deep)
                {
                    IntPtr result;
                    IntPtr ret = SendMessageTimeout(
                        new IntPtr(HWND_BROADCAST), WM_SYSCOMMAND,
                        new IntPtr(SC_MONITORPOWER), new IntPtr(MONITOR_OFF),
                        SMTO_ABORTIFHUNG, 300, out result);
                    ok = (ret != IntPtr.Zero);
                    Log(ok ? "MONITOR_OFF_OK" : "MONITOR_OFF_FAILED");
                }
                else
                {
                    ok = (sleepMode == "hibernate")
                        ? SetSuspendState(true, true, false)
                        : SetSuspendState(false, false, false);
                    Log(ok ? "SUSPEND_OK" : "SUSPEND_FAILED code=" + Marshal.GetLastWin32Error());
                }
            }
            catch (Exception ex)
            {
                ok = false;
                err = ex.GetType().Name;
                Log("POWER_EXCEPTION " + err);
            }

            if (app == null) return;
            try
            {
                app.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Normal,
                    new Action(delegate
                    {
                        if (!ok)
                        {
                            titleText.Text = "没能进入睡眠";
                            descText.Text = (err.Length > 0)
                                ? "调用电源接口失败：" + err
                                : "系统拒绝了电源请求，未执行任何动作";
                            okLabel.Text = "关闭";
                            okBtn.IsEnabled = true;
                            okBtn.Opacity = 1;
                            unitText.Text = "未执行";
                            sleeping = false;
                            Show();
                            Activate();
                            return;
                        }
                        Log("DONE");
                        Close();
                        app.Shutdown();
                    }));
            }
            catch { }
        });
    }

    // v1.2: 停掉所有动画（尤其是 RepeatBehavior.Forever 的呼吸光晕），
    // 否则 Hide 之后动画时钟仍会驱动 DWM 重绘，容易留下旧帧。
    private void StopAnimations()
    {
        try { if (iconTile != null) iconTile.BeginAnimation(OpacityProperty, null); } catch { }
        try { if (stateAsk != null) stateAsk.BeginAnimation(OpacityProperty, null); } catch { }
        try { if (stateCount != null) stateCount.BeginAnimation(OpacityProperty, null); } catch { }
        try { if (bigNumber != null) bigNumber.BeginAnimation(OpacityProperty, null); } catch { }
        try { BeginAnimation(OpacityProperty, null); } catch { }
        try { BeginAnimation(SweepProperty, null); } catch { }
        try
        {
            TranslateTransform tr = ((FrameworkElement)Content).RenderTransform as TranslateTransform;
            if (tr != null) tr.BeginAnimation(TranslateTransform.YProperty, null);
        }
        catch { }
    }

    private void ExitCancel()
    {
        Log("CANCEL");
        if (timer != null) timer.Stop();
        counting = false;

        DoubleAnimation fade = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(120)));
        BeginAnimation(OpacityProperty, fade);

        DispatcherTimer closer = new DispatcherTimer();
        closer.Interval = TimeSpan.FromMilliseconds(160);
        closer.Tick += delegate
        {
            closer.Stop();
            Close();
        };
        closer.Start();
    }
}
