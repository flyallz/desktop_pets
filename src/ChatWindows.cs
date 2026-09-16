using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PhotoCat
{
    internal static class ChatUi
    {
        internal static TextBlock Text(string text, double size)
        {
            return new TextBlock { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10), Foreground = new SolidColorBrush(Color.FromRgb(61, 58, 54)) };
        }
        internal static Button Button(string text, RoutedEventHandler action)
        {
            Button button = new Button { Content = text, Padding = new Thickness(12, 7, 12, 7),
                Margin = new Thickness(0, 0, 8, 0), MinWidth = 66 };
            button.Click += action;
            return button;
        }
        internal static void WindowStyle(Window window, string title, double width, double height)
        {
            window.Title = title; window.Width = width; window.Height = height;
            window.FontFamily = new FontFamily("Microsoft YaHei UI"); window.FontSize = 13;
            window.Background = new SolidColorBrush(Color.FromRgb(250, 248, 243));
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
    }

    internal sealed class ChatSettingsWindow : Window
    {
        internal readonly PasswordBox KeyBox = new PasswordBox { MaxLength = 512, Padding = new Thickness(8), Height = 36 };
        private readonly CheckBox automatic = new CheckBox { Content = "让猫咪主动问候我", Margin = new Thickness(0, 18, 0, 12) };
        private readonly ComboBox interval = new ComboBox { Width = 120, Padding = new Thickness(6), HorizontalAlignment = HorizontalAlignment.Left };
        private readonly TextBlock status = ChatUi.Text("", 12);
        private readonly Func<string, int, string> save;
        internal ChatSettingsWindow(PetPreferences preferences, Func<string, int, string> onSave)
        {
            save = onSave;
            ChatUi.WindowStyle(this, "猫咪桌宠 · 聊天设置", 430, 520);
            ResizeMode = ResizeMode.NoResize;
            StackPanel content = new StackPanel { Margin = new Thickness(24) };
            content.Children.Add(ChatUi.Text("让猫咪陪你聊一会儿", 20));
            content.Children.Add(ChatUi.Text("DeepSeek API Key", 13));
            KeyBox.Password = preferences.ApiKey;
            content.Children.Add(KeyBox);
            TextBlock explanation = ChatUi.Text("密钥加密保存在本机 Windows 账户下。\n聊天文字会发送给 DeepSeek，猫咪照片不会上传。", 12);
            explanation.Margin = new Thickness(0, 10, 0, 0);
            content.Children.Add(explanation);
            content.Children.Add(automatic);
            foreach (int value in new int[] { 15, 30, 60, 120 })
                interval.Items.Add(new ComboBoxItem { Content = "每 " + value + " 分钟", Tag = value });
            interval.SelectedIndex = preferences.GreetingMinutes == 15 ? 0 : preferences.GreetingMinutes == 60 ? 2 : preferences.GreetingMinutes == 120 ? 3 : 1;
            automatic.IsChecked = preferences.GreetingMinutes > 0;
            interval.IsEnabled = automatic.IsChecked == true;
            automatic.Checked += delegate { interval.IsEnabled = true; };
            automatic.Unchecked += delegate { interval.IsEnabled = false; };
            content.Children.Add(interval);
            TextBlock cost = ChatUi.Text("主动问候会按间隔调用 DeepSeek，费用由你的 DeepSeek 账户承担；随时可以关闭。", 12);
            cost.Margin = new Thickness(0, 10, 0, 10);
            content.Children.Add(cost);
            status.Foreground = Brushes.Firebrick;
            status.MinHeight = 34;
            content.Children.Add(status);
            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal };
            buttons.Children.Add(ChatUi.Button("保存", delegate { SaveSettings(); }));
            buttons.Children.Add(ChatUi.Button("清除密钥", delegate { KeyBox.Clear(); automatic.IsChecked = false; }));
            buttons.Children.Add(ChatUi.Button("取消", delegate { Close(); }));
            content.Children.Add(buttons);
            Content = content;
        }
        internal bool SaveSettings()
        {
            string key = KeyBox.Password.Trim();
            int minutes = automatic.IsChecked == true ? (int)((ComboBoxItem)interval.SelectedItem).Tag : 0;
            try
            {
                if (key.Length > 0 || minutes > 0) DeepSeekClient.ValidateKey(key);
                string error = save(key, minutes);
                if (error != null) { status.Text = error; return false; }
                Close();
                return true;
            }
            catch (ChatProblem problem) { status.Text = problem.Message; return false; }
        }
    }

    internal sealed class CatChatWindow : Window
    {
        private readonly ChatSession session;
        private readonly Func<string> getKey;
        private readonly Action<string> spoken;
        private readonly StackPanel messages = new StackPanel();
        private readonly ScrollViewer scroll;
        internal readonly TextBox InputBox = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            MaxLength = 2000, MinHeight = 65, MaxHeight = 120, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(8) };
        private readonly TextBlock status = ChatUi.Text("Enter 发送 · Shift + Enter 换行", 12);
        private readonly Button send, greet, clear, cancel;
        private bool closed, sending;
        internal bool IsSending { get { return sending; } }
        internal string StatusText { get { return status.Text; } }
        internal int BubbleCount { get { return messages.Children.Count; } }

        internal CatChatWindow(ChatSession conversation, Func<string> apiKey, Action openSettings, Action<string> onReply)
        {
            session = conversation; getKey = apiKey; spoken = onReply;
            ChatUi.WindowStyle(this, "猫咪桌宠 · 聊天", 440, 580);
            MinWidth = 385; MinHeight = 450;
            DockPanel page = new DockPanel { Margin = new Thickness(18) };
            StackPanel header = new StackPanel();
            header.Children.Add(ChatUi.Text("和猫咪聊一会儿", 20));
            StackPanel actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            greet = ChatUi.Button("打个招呼", async delegate { await SendTextAsync(true); });
            clear = ChatUi.Button("清空对话", delegate { session.Clear(); messages.Children.Clear(); status.Text = "对话已清空。"; });
            actions.Children.Add(greet); actions.Children.Add(clear);
            actions.Children.Add(ChatUi.Button("聊天设置", delegate { openSettings(); }));
            header.Children.Add(actions);
            DockPanel.SetDock(header, Dock.Top); page.Children.Add(header);
            StackPanel composer = new StackPanel();
            composer.Children.Add(InputBox);
            StackPanel bottom = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 9, 0, 8) };
            send = ChatUi.Button("发送", async delegate { await SendTextAsync(false); });
            cancel = ChatUi.Button("停止回复", delegate { session.Cancel(); });
            cancel.IsEnabled = false;
            bottom.Children.Add(send); bottom.Children.Add(cancel);
            composer.Children.Add(bottom); composer.Children.Add(status);
            composer.Children.Add(ChatUi.Text("由 DeepSeek 回复 · 对话仅保留在本次运行中", 11));
            DockPanel.SetDock(composer, Dock.Bottom); page.Children.Add(composer);
            scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = messages, Margin = new Thickness(0, 0, 0, 12) };
            page.Children.Add(scroll); Content = page;
            foreach (ChatMessage item in session.Snapshot())
                AddMessage(item.role == "user" ? "你" : "猫咪", item.content.StartsWith("现在是本地时间 ", StringComparison.Ordinal) ? "请猫咪打个招呼。" : item.content);
            if (String.IsNullOrWhiteSpace(getKey())) status.Text = "先点“聊天设置”，填写 DeepSeek API Key。";
            InputBox.PreviewKeyDown += async delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
                {
                    e.Handled = true;
                    await SendTextAsync(false);
                }
            };
            Loaded += delegate { InputBox.Focus(); };
            Closed += delegate { closed = true; if (sending) session.Cancel(); };
        }

        internal void ClearDisplayedConversation()
        {
            session.Clear(); messages.Children.Clear();
            status.Text = "设置已更新，可以开始新的对话。";
        }
        private void AddMessage(string speaker, string text)
        {
            StackPanel content = new StackPanel();
            content.Children.Add(ChatUi.Text(speaker, 11));
            content.Children.Add(new TextBox { Text = text, IsReadOnly = true, TextWrapping = TextWrapping.Wrap,
                BorderThickness = new Thickness(0), Background = Brushes.Transparent, Padding = new Thickness(0),
                Foreground = new SolidColorBrush(Color.FromRgb(50, 47, 44)) });
            messages.Children.Add(new Border { Child = content, CornerRadius = new CornerRadius(10), Padding = new Thickness(12),
                Margin = new Thickness(speaker == "你" ? 30 : 0, 0, speaker == "你" ? 0 : 30, 10),
                Background = new SolidColorBrush(speaker == "你" ? Color.FromRgb(228, 237, 220) : Colors.White) });
            while (messages.Children.Count > 40) messages.Children.RemoveAt(0);
            scroll.ScrollToBottom();
        }
        internal async Task SendTextAsync(bool greeting)
        {
            if (closed || sending) return;
            string text = greeting ? ChatSession.GreetingPrompt() : InputBox.Text.Trim();
            if (text.Length == 0) return;
            try
            {
                DeepSeekClient.ValidateKey(getKey());
                if (session.Busy) throw new ChatProblem("猫咪正在回复，稍等一下。");
                SetSending(true); status.Text = "猫咪正在想……";
                AddMessage("你", greeting ? "请猫咪打个招呼。" : text);
                if (!greeting) InputBox.Clear();
                string answer = await session.SendAsync(getKey(), text);
                if (closed) return;
                AddMessage("猫咪", answer); spoken(answer);
                status.Text = "Enter 发送 · Shift + Enter 换行";
            }
            catch (ChatProblem problem)
            {
                if (!closed)
                {
                    status.Text = problem.Message;
                    if (!greeting && InputBox.Text.Length == 0) InputBox.Text = text;
                }
            }
            catch (OperationCanceledException)
            {
                if (!closed)
                {
                    status.Text = "已停止回复。";
                    if (!greeting && InputBox.Text.Length == 0) InputBox.Text = text;
                }
            }
            finally { if (!closed) { SetSending(false); InputBox.Focus(); } }
        }
        private void SetSending(bool value)
        {
            sending = value; send.IsEnabled = !value; greet.IsEnabled = !value; clear.IsEnabled = !value;
            cancel.IsEnabled = value;
        }
    }
}
