// Independent Windows photo-pet sample. Uses only Windows .NET Framework assemblies.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace PhotoCat
{
    internal static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            bool selfTest = args.Length > 0 && args[0] == "--self-test";
            bool first;
            using (Mutex instance = new Mutex(true, "Local\\PhotoCatSample_20260916", out first))
            {
                if (!first && !selfTest)
                {
                    MessageBox.Show("猫咪已经在陪你了。\n请在任务栏右侧的托盘中找到猫咪图标，选择“显示猫咪”。", "猫咪桌宠");
                    return 0;
                }
                try
                {
                    Application app = new Application();
                    app.ShutdownMode = ShutdownMode.OnMainWindowClose;
                    PetWindow pet = new PetWindow(selfTest);
                    if (selfTest)
                    {
                        string output = args.Length > 1 ? args[1] : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "qa");
                        pet.ContentRendered += delegate
                        {
                            pet.Dispatcher.BeginInvoke(new Action(delegate
                            {
                                try { pet.VerifyAndRender(output); }
                                catch (Exception ex)
                                {
                                    Directory.CreateDirectory(output);
                                    File.WriteAllText(Path.Combine(output, "FAILED.txt"), ex.ToString());
                                    Environment.ExitCode = 1;
                                }
                                finally { pet.Close(); }
                            }), DispatcherPriority.ApplicationIdle);
                        };
                    }
                    app.Run(pet);
                    return Environment.ExitCode;
                }
                catch (Exception ex)
                {
                    if (!selfTest) MessageBox.Show("猫咪暂时没能启动。\n" + ex.Message, "猫咪桌宠");
                    return 1;
                }
            }
        }
    }

    internal sealed class PetWindow : Window
    {
        private readonly Canvas scene = new Canvas();
        private readonly Image cat = new Image();
        private readonly TextBlock message = new TextBlock();
        private readonly Border bubble = new Border();
        private readonly ScaleTransform breathing = new ScaleTransform(1, 1);
        private readonly TranslateTransform nudge = new TranslateTransform();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly DispatcherTimer timer;
        private readonly BitmapSource bitmap;
        private readonly byte[] pixels;
        private readonly int stride;
        private readonly bool testing;
        private Forms.NotifyIcon tray;
        private IntPtr handle;
        private bool clickThrough;
        private bool moving;
        private bool moved;
        private bool animate = true;
        private bool menuOpen;
        private Point dragStart;
        private double startLeft, startTop;
        private double bubbleUntil;
        private double petAt = -10;
        private double petSize = 240;
        private int petCount;

        public PetWindow(bool selfTest)
        {
            testing = selfTest;
            Title = "猫咪桌宠 · 试用样品";
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            UseLayoutRounding = true;

            using (Stream input = Assembly.GetExecutingAssembly().GetManifestResourceStream("PhotoCat.cat.png"))
            {
                if (input == null) throw new InvalidOperationException("缺少猫咪图片资源。");
                BitmapImage decoded = new BitmapImage();
                decoded.BeginInit();
                decoded.CacheOption = BitmapCacheOption.OnLoad;
                decoded.StreamSource = input;
                decoded.EndInit();
                decoded.Freeze();
                bitmap = new FormatConvertedBitmap(decoded, PixelFormats.Bgra32, null, 0);
                bitmap.Freeze();
            }
            stride = bitmap.PixelWidth * 4;
            pixels = new byte[stride * bitmap.PixelHeight];
            bitmap.CopyPixels(pixels, stride, 0);
            cat.Source = bitmap;
            cat.Stretch = Stretch.Fill;
            cat.Cursor = Cursors.Hand;
            cat.RenderTransformOrigin = new Point(0.5, 0.97);
            TransformGroup transforms = new TransformGroup();
            transforms.Children.Add(breathing);
            transforms.Children.Add(nudge);
            cat.RenderTransform = transforms;
            RenderOptions.SetBitmapScalingMode(cat, BitmapScalingMode.HighQuality);
            cat.MouseLeftButtonDown += BeginDrag;
            cat.MouseMove += ContinueDrag;
            cat.MouseLeftButtonUp += EndDrag;
            cat.LostMouseCapture += delegate { moving = false; };
            cat.MouseRightButtonUp += delegate(object sender, MouseButtonEventArgs e)
            {
                if (IsCatPixel(cat.PointFromScreen(ScreenCursor()))) OpenMenu();
                e.Handled = true;
            };

            message.FontFamily = new FontFamily("Microsoft YaHei UI");
            message.FontSize = 12;
            message.Foreground = new SolidColorBrush(Color.FromRgb(69, 63, 57));
            message.TextAlignment = TextAlignment.Center;
            message.TextWrapping = TextWrapping.Wrap;
            bubble.Background = new SolidColorBrush(Color.FromArgb(246, 255, 253, 247));
            bubble.BorderBrush = new SolidColorBrush(Color.FromRgb(218, 212, 198));
            bubble.BorderThickness = new Thickness(1);
            bubble.CornerRadius = new CornerRadius(12);
            bubble.Padding = new Thickness(12, 7, 12, 7);
            bubble.Child = message;
            bubble.IsHitTestVisible = false;
            bubble.Visibility = Visibility.Collapsed;
            scene.Children.Add(cat);
            scene.Children.Add(bubble);
            Content = scene;
            ApplySize(240, false);

            SourceInitialized += delegate
            {
                handle = new WindowInteropHelper(this).Handle;
                int style = Native.GetWindowLong(handle, -20);
                Native.SetWindowLong(handle, -20, style | 0x08000000 | 0x80);
                HwndSource.FromHwnd(handle).AddHook(WindowMessages);
            };
            Loaded += delegate
            {
                PlaceAtHome();
                if (testing) { Left = -10000; Top = -10000; }
                else
                {
                    CreateTray();
                    Say("拖动我换位置 · 右键打开菜单", 8);
                }
            };
            Closed += delegate
            {
                timer.Stop();
                if (tray != null) { tray.Visible = false; tray.Dispose(); }
            };
            timer = new DispatcherTimer(DispatcherPriority.Background);
            timer.Interval = TimeSpan.FromMilliseconds(40);
            timer.Tick += delegate { Tick(); };
            timer.Start();
        }

        private static Point ScreenCursor()
        {
            Native.POINT point;
            Native.GetCursorPos(out point);
            return new Point(point.X, point.Y);
        }

        private bool IsCatPixel(Point local)
        {
            if (cat.ActualWidth <= 0 || cat.ActualHeight <= 0) return false;
            int x = (int)Math.Floor(local.X / cat.ActualWidth * bitmap.PixelWidth);
            int y = (int)Math.Floor(local.Y / cat.ActualHeight * bitmap.PixelHeight);
            return x >= 0 && y >= 0 && x < bitmap.PixelWidth && y < bitmap.PixelHeight
                && pixels[y * stride + x * 4 + 3] >= 30;
        }

        private void SetClickThrough(bool value)
        {
            if (handle == IntPtr.Zero || clickThrough == value) return;
            int style = Native.GetWindowLong(handle, -20);
            Native.SetWindowLong(handle, -20, value ? style | 0x20 : style & ~0x20);
            clickThrough = value;
        }

        private IntPtr WindowMessages(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == 0x21) { handled = true; return new IntPtr(3); } // Never take typing focus.
            return IntPtr.Zero;
        }

        private void Tick()
        {
            if (!IsVisible) return;
            double now = clock.Elapsed.TotalSeconds;
            double wave = animate ? Math.Sin(now * Math.PI / 2.3) : 0;
            breathing.ScaleY = 1 + wave * 0.003;
            breathing.ScaleX = 1 - wave * 0.001;
            double elapsed = now - petAt;
            nudge.Y = animate && elapsed >= 0 && elapsed < 0.65 ? -3 * Math.Sin(elapsed / 0.65 * Math.PI) : 0;
            if (now > bubbleUntil) bubble.Visibility = Visibility.Collapsed;
            if (!testing && !moving && !menuOpen)
                SetClickThrough(!IsCatPixel(cat.PointFromScreen(ScreenCursor())));
        }

        private void BeginDrag(object sender, MouseButtonEventArgs e)
        {
            if (!IsCatPixel(cat.PointFromScreen(ScreenCursor()))) return;
            SetClickThrough(false);
            dragStart = ScreenCursor();
            startLeft = Left;
            startTop = Top;
            moved = false;
            moving = cat.CaptureMouse();
            e.Handled = true;
        }

        private void ContinueDrag(object sender, MouseEventArgs e)
        {
            if (!moving) return;
            Vector delta = ScreenCursor() - dragStart;
            HwndSource source = HwndSource.FromHwnd(handle);
            if (source != null) delta = source.CompositionTarget.TransformFromDevice.Transform(delta);
            if (Math.Abs(delta.X) + Math.Abs(delta.Y) > 4) moved = true;
            if (moved) { Left = startLeft + delta.X; Top = startTop + delta.Y; }
        }

        private void EndDrag(object sender, MouseButtonEventArgs e)
        {
            if (!moving) return;
            bool wasMoved = moved;
            moving = false;
            cat.ReleaseMouseCapture();
            if (wasMoved) KeepVisible(); else Pet();
            e.Handled = true;
        }

        private void Pet()
        {
            string[] replies = { "喵。", "呼噜呼噜…", "陪你待一会儿。" };
            Say(replies[petCount++ % replies.Length], 2.8);
            petAt = clock.Elapsed.TotalSeconds;
        }

        private void Say(string text, double seconds)
        {
            message.Text = text;
            bubble.Visibility = Visibility.Visible;
            bubbleUntil = clock.Elapsed.TotalSeconds + seconds;
        }

        private void ApplySize(double width, bool keepFeet)
        {
            double foot = Top + Height;
            petSize = width;
            cat.Width = width;
            cat.Height = width * bitmap.PixelHeight / bitmap.PixelWidth;
            Width = width + 24;
            Height = cat.Height + 60;
            scene.Width = Width;
            scene.Height = Height;
            Canvas.SetLeft(cat, 12);
            Canvas.SetTop(cat, 48);
            bubble.Width = width - 2;
            Canvas.SetLeft(bubble, 13);
            Canvas.SetTop(bubble, 4);
            if (keepFeet) { Top = foot - Height; KeepVisible(); }
        }

        private void PlaceAtHome()
        {
            Rect work = SystemParameters.WorkArea;
            Left = work.Right - Width - 22;
            Top = work.Bottom - Height - 8;
        }

        private void KeepVisible()
        {
            Rect visible = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
            Left = Math.Max(visible.Left, Math.Min(Left, visible.Right - Width));
            Top = Math.Max(visible.Top, Math.Min(Top, visible.Bottom - Height));
        }

        private void Reveal()
        {
            Show();
            timer.Start();
            PlaceAtHome();
            Say("我在这里。", 2);
        }

        private void HidePet()
        {
            Hide();
            timer.Stop();
        }

        private MenuItem MenuAction(string title, Action action)
        {
            MenuItem item = new MenuItem { Header = title };
            item.Click += delegate { action(); };
            return item;
        }

        private void OpenMenu()
        {
            SetClickThrough(false);
            menuOpen = true;
            ContextMenu menu = new ContextMenu();
            menu.FontFamily = new FontFamily("Microsoft YaHei UI");
            menu.FontSize = 13;
            menu.Items.Add(MenuAction("摸一摸", Pet));
            MenuItem size = new MenuItem { Header = "猫咪大小" };
            foreach (double value in new double[] { 180, 240, 320 })
            {
                double target = value;
                MenuItem item = MenuAction(value == 180 ? "小" : value == 240 ? "中" : "大", delegate { ApplySize(target, true); });
                item.IsChecked = petSize == value;
                size.Items.Add(item);
            }
            menu.Items.Add(size);
            menu.Items.Add(MenuAction(animate ? "暂停呼吸动作" : "恢复呼吸动作", delegate { animate = !animate; Tick(); }));
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuAction("暂时隐藏（从托盘恢复）", HidePet));
            menu.Items.Add(MenuAction("回到屏幕右下角", PlaceAtHome));
            menu.Items.Add(MenuAction("退出", Close));
            menu.Closed += delegate { menuOpen = false; };
            menu.IsOpen = true;
        }

        private void CreateTray()
        {
            tray = new Forms.NotifyIcon();
            using (Stream input = Assembly.GetExecutingAssembly().GetManifestResourceStream("PhotoCat.cat.png"))
            using (Drawing.Bitmap full = new Drawing.Bitmap(input))
            using (Drawing.Bitmap small = new Drawing.Bitmap(full, new Drawing.Size(32, 32)))
            {
                IntPtr temporary = small.GetHicon();
                using (Drawing.Icon icon = Drawing.Icon.FromHandle(temporary)) tray.Icon = (Drawing.Icon)icon.Clone();
                Native.DestroyIcon(temporary);
            }
            tray.Text = "猫咪桌宠 · 双击显示，右键退出";
            Forms.ContextMenuStrip menu = new Forms.ContextMenuStrip();
            menu.Items.Add("显示猫咪", null, delegate { Dispatcher.Invoke(new Action(Reveal)); });
            menu.Items.Add("暂时隐藏", null, delegate { Dispatcher.Invoke(new Action(HidePet)); });
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("退出", null, delegate { Dispatcher.Invoke(new Action(Close)); });
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { Dispatcher.Invoke(new Action(Reveal)); };
            tray.Visible = true;
        }

        internal void VerifyAndRender(string output)
        {
            Directory.CreateDirectory(output);
            List<string> checks = new List<string>();
            long clear = 0, solid = 0;
            for (int i = 3; i < pixels.Length; i += 4) { if (pixels[i] == 0) clear++; if (pixels[i] >= 240) solid++; }
            long total = pixels.Length / 4;
            Check(clear > total / 20 && solid > total / 5, "PNG contains transparent background and opaque cat", checks);
            Check(Topmost && AllowsTransparency && !ShowInTaskbar, "Transparent always-on-top window", checks);
            Check(handle != IntPtr.Zero, "Native Windows window created", checks);
            SetClickThrough(true);
            Check((Native.GetWindowLong(handle, -20) & 0x20) != 0, "Click-through native style enabled", checks);
            SetClickThrough(false);
            Check((Native.GetWindowLong(handle, -20) & 0x20) == 0, "Click-through native style disabled for interaction", checks);
            Check((Native.GetWindowLong(handle, -20) & 0x08000000) != 0, "No-activate native style preserves typing focus", checks);
            ApplySize(180, false);
            scene.UpdateLayout();
            Check(cat.ActualWidth == 180, "Small size applied", checks);
            ApplySize(320, false);
            scene.UpdateLayout();
            Check(cat.ActualWidth == 320, "Large size applied", checks);
            ApplySize(240, false);
            scene.UpdateLayout();
            int transparentSamples = 0, hitSamples = 0;
            for (int y = 0; y < bitmap.PixelHeight; y += 19)
                for (int x = 0; x < bitmap.PixelWidth; x += 19)
                {
                    bool hit = IsCatPixel(new Point((x + 0.1) * cat.ActualWidth / bitmap.PixelWidth,
                        (y + 0.1) * cat.ActualHeight / bitmap.PixelHeight));
                    bool expected = pixels[y * stride + x * 4 + 3] >= 30;
                    if (hit != expected) throw new InvalidOperationException("Alpha hit-test does not match photo");
                    if (hit) hitSamples++; else transparentSamples++;
                }
            Check(transparentSamples > 0 && hitSamples > 0, "Photo alpha hit-test separates cat from empty background", checks);
            Pet();
            Check(bubble.Visibility == Visibility.Visible && message.Text == "喵。", "Petting displays response", checks);
            animate = false;
            Tick();
            Check(breathing.ScaleY == 1 && nudge.Y == 0, "Pause removes motion", checks);
            bubbleUntil = -1;
            Tick();
            Check(bubble.Visibility == Visibility.Collapsed, "Response automatically disappears", checks);
            CreateTray();
            Check(tray.Visible && tray.Icon != null && tray.ContextMenuStrip.Items.Count == 4, "Tray icon and controls created", checks);
            HidePet();
            Check(!IsVisible && !timer.IsEnabled, "Hide stops rendering timer", checks);
            Reveal();
            Check(IsVisible && timer.IsEnabled, "Restore shows pet and resumes timer", checks);
            Say("喵。陪你待一会儿。", 30);
            scene.UpdateLayout();
            RenderTargetBitmap preview = new RenderTargetBitmap((int)Math.Ceiling(Width), (int)Math.Ceiling(Height), 96, 96, PixelFormats.Pbgra32);
            preview.Render(scene);
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(preview));
            using (Stream file = File.Create(Path.Combine(output, "pet-preview.png"))) encoder.Save(file);
            checks.Add("PASS: Rendered actual WPF pet view to pet-preview.png");
            checks.Add("Not covered: physical mouse input, Windows tray clicks, other Windows computers, mixed-DPI monitors.");
            File.WriteAllLines(Path.Combine(output, "verification.txt"), checks.ToArray());
        }

        private static void Check(bool value, string label, List<string> checks)
        {
            if (!value) throw new InvalidOperationException(label);
            checks.Add("PASS: " + label);
        }
    }

    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential)] internal struct POINT { public int X, Y; }
        [DllImport("user32.dll")] internal static extern bool GetCursorPos(out POINT point);
        [DllImport("user32.dll")] internal static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")] internal static extern int SetWindowLong(IntPtr hwnd, int index, int value);
        [DllImport("user32.dll")] internal static extern bool DestroyIcon(IntPtr icon);
    }
}
