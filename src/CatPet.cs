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
        private readonly PhotoMotion cat = new PhotoMotion();
        private readonly TextBlock message = new TextBlock();
        private readonly Border bubble = new Border();
        private readonly PetMotion motion = new PetMotion(Environment.TickCount);
        private double lastTick;
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
        private double petSize = 240;
        private int petCount;

        public PetWindow(bool selfTest)
        {
            testing = selfTest;
            Title = "猫咪桌宠 · 动作样品";
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
            cat.SetPhoto(bitmap);
            cat.Cursor = Cursors.Hand;
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
            Point original = cat.SourcePoint(local);
            int x = (int)Math.Floor(original.X);
            int y = (int)Math.Floor(original.Y);
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
            double delta = now - lastTick;
            lastTick = now;
            if (animate) cat.SetPose(motion.Advance(delta));
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
            if (animate) motion.Pet();
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
            lastTick = clock.Elapsed.TotalSeconds;
            timer.Start();
            PlaceAtHome();
            if (testing) { Left = -10000; Top = -10000; }
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
            menu.Items.Add(MenuAction(animate ? "暂停动作" : "恢复动作", delegate
            {
                animate = !animate;
                lastTick = clock.Elapsed.TotalSeconds;
                Tick();
            }));
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
            timer.Stop();
            cat.SetPose(new MotionPose());
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
            cat.SetPose(new MotionPose { Blink = 0.5, Tail = 0.3 });
            animate = false;
            Tick();
            Check(cat.Pose.Blink == 0.5 && cat.Pose.Tail == 0.3, "Pause freezes all local movements", checks);
            cat.SetPose(new MotionPose());
            bubbleUntil = -1;
            Tick();
            Check(bubble.Visibility == Visibility.Collapsed, "Response automatically disappears", checks);
            CreateTray();
            Check(tray.Visible && tray.Icon != null && tray.ContextMenuStrip.Items.Count == 4, "Tray icon and controls created", checks);
            HidePet();
            Check(!IsVisible && !timer.IsEnabled, "Hide stops rendering timer", checks);
            Reveal();
            Check(IsVisible && timer.IsEnabled, "Restore shows pet and resumes timer", checks);
            timer.Stop();
            VerifyMotion(output, checks);
            ApplySize(240, false);
            VerifyLiveTimer(checks);
            cat.SetPose(new MotionPose());
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

        private void VerifyLiveTimer(List<string> checks)
        {
            int ticks = 0;
            double blink = 0;
            EventHandler observe = delegate { ticks++; blink = Math.Max(blink, cat.Pose.Blink); };
            DispatcherFrame loop = new DispatcherFrame();
            DispatcherTimer finish = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.65) };
            finish.Tick += delegate { loop.Continue = false; };
            animate = true;
            motion.Pet();
            lastTick = clock.Elapsed.TotalSeconds;
            timer.Tick += observe;
            timer.Start();
            finish.Start();
            try { Dispatcher.PushFrame(loop); }
            finally { finish.Stop(); timer.Stop(); timer.Tick -= observe; animate = false; }
            Check(ticks >= 10, "Live WPF timer drives motion updates", checks);
            Check(blink > 0.9, "Petting reaches the animated view through the live timer", checks);
        }

        private byte[] SaveMotionFrame(string path)
        {
            scene.UpdateLayout();
            RenderTargetBitmap sceneFrame = new RenderTargetBitmap((int)Math.Ceiling(Width),
                (int)Math.Ceiling(Height), 96, 96, PixelFormats.Pbgra32);
            sceneFrame.Render(scene);
            CroppedBitmap frame = new CroppedBitmap(sceneFrame, new Int32Rect(12, 48,
                (int)Math.Ceiling(cat.Width), (int)Math.Ceiling(cat.Height)));
            byte[] rendered = new byte[frame.PixelWidth * frame.PixelHeight * 4];
            frame.CopyPixels(rendered, frame.PixelWidth * 4, 0);
            int solid = 0, clear = 0;
            for (int i = 3; i < rendered.Length; i += 4)
            {
                if (rendered[i] >= 240) solid++;
                if (rendered[i] == 0) clear++;
            }
            if (solid < rendered.Length / 4 / 5 || clear < rendered.Length / 4 / 20)
                throw new InvalidOperationException("Motion frame lost its cat or transparent background");
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(frame));
            using (Stream file = File.Create(path)) encoder.Save(file);
            return rendered;
        }

        private void VerifyMotion(string output, List<string> checks)
        {
            string motionPath = Path.Combine(output, "motion");
            Directory.CreateDirectory(motionPath);
            bubble.Visibility = Visibility.Collapsed;
            ApplySize(640, false);
            scene.UpdateLayout();
            MotionPose[] poses = {
                new MotionPose(), new MotionPose { Blink = 0.5 }, new MotionPose { Blink = 1 },
                new MotionPose { LeftEar = 1 }, new MotionPose { RightEar = 1 },
                new MotionPose { Tail = -1 }, new MotionPose { Tail = 1 },
                new MotionPose { Blink = 1, LeftEar = 1, Tail = 1, Breath = 1 }
            };
            string[] names = { "rest", "half-blink", "closed", "left-ear", "right-ear", "tail-left", "tail-right", "combined" };
            byte[] restingPixels = null;
            for (int i = 0; i < poses.Length; i++)
            {
                cat.SetPose(poses[i]);
                Check(cat.HasValidSurface(), "No torn or folded surface: " + names[i], checks);
                Point[] planted = { new Point(320, 960), new Point(800, 856), new Point(492, 290), new Point(483, 780) };
                foreach (Point point in planted)
                    if ((PhotoMotion.Map(point, poses[i]) - point).Length > 0.01)
                        throw new InvalidOperationException("Paws, nose or tail root shifted");
                byte[] frame = SaveMotionFrame(Path.Combine(motionPath, names[i] + ".png"));
                if (i == 0) restingPixels = frame;
                else
                    for (int y = 540; y < 650; y++)
                        for (int x = 420 * 4; x < 610 * 4; x++)
                            if (frame[y * 640 * 4 + x] != restingPixels[y * 640 * 4 + x])
                                throw new InvalidOperationException("Stationary paw pixels flickered during " + names[i]);
            }
            checks.Add("PASS: Paws, nose and tail root stay fixed in all extreme poses");
            checks.Add("PASS: Rendered stationary paw pixels remain identical during all gestures");
            // Verify actual moved edge pixels against the inverse triangle lookup.
            foreach (double sway in new double[] { -1, 1 })
            {
                MotionPose pose = new MotionPose { Tail = sway };
                cat.SetPose(pose);
                int changedPixels = 0;
                for (int y = 1120; y < 1280; y += 13)
                    for (int x = 95; x < 430; x += 13)
                    {
                        Point original = new Point(x + 0.4, y + 0.4);
                        Point moved = PhotoMotion.Map(original, pose);
                        Point local = new Point(moved.X * cat.ActualWidth / bitmap.PixelWidth,
                            moved.Y * cat.ActualHeight / bitmap.PixelHeight);
                        Point recovered = cat.SourcePoint(local);
                        if ((recovered - original).Length > 1.0)
                            throw new InvalidOperationException("Moving tail hit-test drifted away from the photo");
                        // Avoid antialiased boundaries where a subpixel difference changes the threshold.
                        byte a = pixels[y * stride + x * 4 + 3];
                        if (a == 0 || a == 255)
                        {
                            bool uniform = true;
                            for (int ny = y - 1; ny <= y + 1; ny++)
                                for (int nx = x - 1; nx <= x + 1; nx++)
                                    if (pixels[ny * stride + nx * 4 + 3] != a) uniform = false;
                            if (uniform && IsCatPixel(local) != (a == 255))
                                throw new InvalidOperationException("Transparent moving tail hit-test failed");
                        }
                        if ((moved - original).Length > 8) changedPixels++;
                    }
                Check(changedPixels > 20, "Mouse follows tail silhouette at sway " + sway, checks);
            }
            PetMotion schedule = new PetMotion(42);
            bool blinked = false, earMoved = false, tailMoved = false, rested = false;
            double largestStep = 0;
            Point previousTip = new Point(180, 1240);
            for (int i = 0; i < 25 * 30; i++)
            {
                MotionPose pose = schedule.Advance(0.04);
                cat.SetPose(pose);
                if (!cat.HasValidSurface()) throw new InvalidOperationException("Surface folded during continuous motion");
                blinked |= pose.Blink > 0.85;
                earMoved |= pose.LeftEar > 0.7 || pose.RightEar > 0.7;
                tailMoved |= Math.Abs(pose.Tail) > 0.4;
                rested |= i > 100 && pose.Blink == 0 && pose.LeftEar == 0 && pose.RightEar == 0 && pose.Tail == 0;
                Point tip = PhotoMotion.Map(new Point(180, 1240), pose);
                largestStep = Math.Max(largestStep, (tip - previousTip).Length);
                previousTip = tip;
            }
            Check(blinked && earMoved && tailMoved && rested, "30-second idle sequence includes blinks, single-ear twitches, tail movement and pauses", checks);
            Check(largestStep < 2, "Tail moves continuously without a jump", checks);
            schedule = new PetMotion(7);
            schedule.Pet();
            double slowBlink = 0;
            MotionPose last = new MotionPose();
            for (int i = 0; i < 41; i++) { last = schedule.Advance(0.04); slowBlink = Math.Max(slowBlink, last.Blink); }
            Check(slowBlink > 0.9 && last.Blink == 0, "Petting produces a slow blink and returns to open eyes", checks);
            schedule = new PetMotion(7);
            schedule.Pet();
            for (int i = 0; i < 12; i++) last = schedule.Advance(0.04);
            schedule.Pet();
            Check(Math.Abs(schedule.Advance(0).Blink - last.Blink) < 0.00001,
                "Repeated petting does not snap closed eyes open", checks);
            ApplySize(400, false);
            scene.UpdateLayout();
            schedule = new PetMotion(42);
            Stopwatch rendering = Stopwatch.StartNew();
            for (int frame = 0; frame < 90; frame++)
            {
                if (frame == 15) schedule.Pet();
                cat.SetPose(schedule.Advance(1.0 / 15));
                SaveMotionFrame(Path.Combine(motionPath, "frame-" + frame.ToString("D3") + ".png"));
            }
            checks.Add("PASS: Rendered 90 WPF motion frames, including petting and idle actions");
            checks.Add("INFO: Mesh vertices = " + cat.VertexCount + "; 90 offscreen frames = " + rendering.Elapsed.TotalSeconds.ToString("F2") + " seconds (not an on-screen FPS measurement)");
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
