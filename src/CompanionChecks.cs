using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PhotoCat
{
    internal sealed partial class PetWindow
    {
        private void VerifyCompanion(string output, List<string> checks)
        {
            string folder = Path.Combine(output, "companion");
            Directory.CreateDirectory(folder);
            Check(looks.Count >= 3, "Multiple original-photo looks are embedded in the standalone EXE", checks);
            Check(preferences.GreetingMinutes == 0 && preferences.ApiKey.Length == 0,
                "A fresh pet makes no API calls and proactive greetings start disabled", checks);
            ApplySize(320, false); scene.UpdateLayout();
            for (int i = 0; i < looks.Count; i++)
            {
                SelectLook(i, false);
                Check(preferences.LookId == looks[i].Id && motion.Activity == CatActivity.Companion,
                    "Choosing a photo immediately displays and remembers " + looks[i].Id, checks);
                if (i > 0)
                {
                    byte[] bytes = new byte[953 * 1347 * 4]; looks[i].Photo.CopyPixels(bytes, 953 * 4, 0);
                    cat.SetPose(new MotionPose { Blink = 1, Tail = 1, LeftEar = 1, Breath = 1 });
                    int hit = 0, clear = 0;
                    for (int y = 0; y < 1347; y += 37)
                        for (int x = 0; x < 953; x += 31)
                        {
                            Point original = new Point(x + 0.25, y + 0.25);
                            Point local = new Point(original.X * cat.ActualWidth / 953, original.Y * cat.ActualHeight / 1347);
                            if ((cat.SourcePoint(local) - original).Length > 0.01)
                                throw new InvalidOperationException("A new photo inherited the original face/ear deformation");
                            bool expected = bytes[(y * 953 + x) * 4 + 3] >= 30;
                            if (cat.IsPhotoPixel(local) != expected) throw new InvalidOperationException("Photo look hit regions mismatched");
                            if (expected) hit++; else clear++;
                        }
                    Check(hit > 20 && clear > 20, "Photo silhouette and transparent clicks match without face distortion: " + looks[i].Id, checks);
                }
                bubble.Visibility = Visibility.Collapsed;
                SaveElement(scene, (int)Math.Ceiling(Width), (int)Math.Ceiling(Height), Path.Combine(folder, "look-" + looks[i].Id + ".png"));
            }
            motion.Sleep(true); SelectLook(1, false);
            Check(!motion.IsSleeping && motion.Automatic, "Changing a photo leaves sleep immediately and preserves automatic actions", checks);
            PetMotion behavior = new PetMotion(11); behavior.SetAutomatic(false); behavior.Walk();
            for (int i = 0; i < 120; i++) cat.SetPose(behavior.Advance(0.1));
            Check(cat.Pose.Posture == CatPosture.Sit && selectedLook == 1, "Walking returns to the selected photo without losing the look", checks);
            for (int i = 0; i < looks.Count; i++) NextLook();
            Check(selectedLook == 1, "Repeated double-click action cycles through every photo and wraps", checks);
            SelectLook(0, false); motion.ReturnToCompanion();
            Check(LooksMenu().Items.Count == looks.Count, "Every embedded photo is reachable from the right-click look menu", checks);
            checks.AddRange(Task.Run(delegate { return VerifyChatCore(folder); }).GetAwaiter().GetResult());
            VerifyChatWindows(folder, checks);
        }

        private static void SaveElement(FrameworkElement element, int width, int height, string path)
        {
            element.UpdateLayout();
            RenderTargetBitmap target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            target.Render(element);
            PngBitmapEncoder encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(target));
            using (Stream file = File.Create(path)) encoder.Save(file);
        }

        private static async Task<List<string>> VerifyChatCore(string folder)
        {
            List<string> checks = new List<string>();
            const string key = "test-only-not-a-real-api-key";
            string path = Path.Combine(folder, "settings-test.json");
            PetPreferences settings = new PetPreferences { LookId = "sweater", ApiKey = key, GreetingMinutes = 30 };
            settings.Save(path); settings.Save(path);
            string notice;
            PetPreferences loaded = PetPreferences.Load(path, out notice);
            Check(notice == null && loaded.ApiKey == key && loaded.LookId == "sweater" && loaded.GreetingMinutes == 30,
                "Encrypted preferences survive an atomic save and reload under this Windows account", checks);
            Check(!File.ReadAllText(path).Contains(key), "Saved settings contain no plaintext API key", checks);
            settings.ApiKey = ""; settings.GreetingMinutes = 0; settings.Save(path);
            Check(PetPreferences.Load(path, out notice).ApiKey.Length == 0, "Removing the API key persists", checks);
            File.WriteAllText(path, "broken json");
            Check(PetPreferences.Load(path, out notice).ApiKey.Length == 0 && notice != null, "Damaged settings recover without crashing", checks);
            File.WriteAllText(path, "{\"LookId\":\"sofa\",\"ProtectedApiKey\":\"broken ciphertext\",\"GreetingMinutes\":30}");
            loaded = PetPreferences.Load(path, out notice);
            Check(loaded.LookId == "sofa" && loaded.ApiKey.Length == 0 && loaded.GreetingMinutes == 0 && notice != null,
                "An unreadable encrypted key preserves the photo and disables paid greetings", checks);
            File.Delete(path);

            FakeChatHandler handler = new FakeChatHandler();
            using (ChatSession session = new ChatSession(new DeepSeekClient(handler)))
            {
                await ExpectChatProblem(delegate { return session.SendAsync("", "你好"); }, "API Key");
                await ExpectChatProblem(delegate { return session.SendAsync(key, new string('a', 2001)); }, "2000");
                Check(handler.Calls == 0, "Missing key and oversized messages never leave the computer", checks);
                string answer = await session.SendAsync(key, "你好，猫咪");
                var payload = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(handler.Body);
                var thinking = (Dictionary<string, object>)payload["thinking"];
                Check(answer == "陪你待一会儿。" && handler.Url == DeepSeekClient.Endpoint && handler.Authorization == "Bearer " + key
                    && (string)payload["model"] == DeepSeekClient.Model && (string)thinking["type"] == "disabled"
                    && !handler.Body.Contains(key) && !handler.Body.Contains("image_url"),
                    "Mock API verifies official HTTPS endpoint, bearer authentication, current model and text-only requests", checks);
                for (int i = 0; i < 9; i++) await session.SendAsync(key, "第" + i + "次聊天");
                Check(session.MessageCount == 12, "Conversation context keeps only the six most recent complete exchanges", checks);
                foreach (int status in new int[] { 401, 402, 429, 503, 302 })
                {
                    int code = status;
                    handler.Respond = delegate { return Task.FromResult(new HttpResponseMessage((HttpStatusCode)code)); };
                    await ExpectChatProblem(delegate { return session.SendAsync(key, "重试"); }, DeepSeekClient.StatusMessage(code));
                    Check(!session.Busy && session.MessageCount == 12, "API error " + code + " leaves no stuck request or false chat history", checks);
                }
                handler.Respond = delegate { return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("not json") }); };
                await ExpectChatProblem(delegate { return session.SendAsync(key, "格式测试"); }, "格式");
                handler.Respond = delegate { return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") }); };
                await ExpectChatProblem(delegate { return session.SendAsync(key, "空回复测试"); }, "完整回复");
                handler.Respond = delegate { throw new HttpRequestException("private upstream detail"); };
                await ExpectChatProblem(delegate { return session.SendAsync(key, "断网测试"); }, "检查网络");
                handler.Respond = delegate { throw new TaskCanceledException(); };
                await ExpectChatProblem(delegate { return session.SendAsync(key, "超时测试"); }, "超时");
                Check(!session.Busy, "Malformed responses, network failures and timeouts recover with clear messages", checks);
                handler.Respond = async delegate(CancellationToken token) { await Task.Delay(10000, token); return FakeChatHandler.GoodResponse(); };
                Task<string> pending = session.SendAsync(key, "等待中的消息");
                Check(session.Busy, "A slow request is asynchronous and exposes its busy state", checks);
                await ExpectChatProblem(delegate { return session.SendAsync(key, "重复发送"); }, "正在回复");
                session.Clear();
                bool cancelled = false;
                try { await pending; } catch (OperationCanceledException) { cancelled = true; }
                Check(cancelled && !session.Busy && session.MessageCount == 0, "Cancel/clear aborts the request and removes in-memory conversation", checks);
                handler.Respond = delegate { return Task.FromResult(FakeChatHandler.GoodResponse()); };
                await session.SendAsync(key, "恢复后继续聊");
                Check(session.MessageCount == 2, "Chat works normally after cancellation", checks);
            }
            checks.Add("NOT RUN: Live DeepSeek completion, because no real API key was supplied; all network tests used an in-process fake handler.");
            return checks;
        }

        private static async Task ExpectChatProblem(Func<Task<string>> action, string expected)
        {
            try { await action(); }
            catch (ChatProblem problem)
            {
                if (problem.Message.Contains(expected)) return;
                throw;
            }
            throw new InvalidOperationException("Expected a recoverable chat error: " + expected);
        }

        private void VerifyChatWindows(string folder, List<string> checks)
        {
            FakeChatHandler handler = new FakeChatHandler();
            using (ChatSession session = new ChatSession(new DeepSeekClient(handler)))
            {
                string spoken = null;
                CatChatWindow window = new CatChatWindow(session, delegate { return "test-only-not-a-real-api-key"; }, delegate { }, delegate(string text) { spoken = text; });
                window.ShowActivated = false; window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Left = -10000; window.Top = -10000; window.Show();
                try
                {
                    window.InputBox.Text = "今天有点累，陪我说句话吧。";
                    PumpTask(window.SendTextAsync(false));
                    Check(window.BubbleCount == 2 && spoken == "陪你待一会儿。" && !window.IsSending && window.InputBox.Text.Length == 0,
                        "Actual chat window sends a message, shows the response and forwards the pet bubble", checks);
                    Check((Native.GetWindowLong(new WindowInteropHelper(window).Handle, -20) & 0x08000000) == 0,
                        "Chat is a focusable window while the pet keeps its no-activate behavior", checks);
                    SaveElement((FrameworkElement)window.Content, 404, 520, Path.Combine(folder, "chat-window.png"));
                    handler.Respond = delegate { return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)); };
                    window.InputBox.Text = "保留这句话";
                    PumpTask(window.SendTextAsync(false));
                    Check(window.StatusText.Contains("API Key") && window.InputBox.Text == "保留这句话" && !window.IsSending,
                        "Chat UI shows an authentication failure and restores the unsent text for retry", checks);
                    handler.Respond = async delegate(CancellationToken token) { await Task.Delay(10000, token); return FakeChatHandler.GoodResponse(); };
                    Task pending = window.SendTextAsync(false);
                    window.Close(); PumpTask(pending);
                    Check(!session.Busy, "Closing the chat window cancels its pending API call", checks);
                }
                finally { window.Close(); }
            }
            string savedKey = null; int savedMinutes = -1;
            ChatSettingsWindow settings = new ChatSettingsWindow(new PetPreferences(), delegate(string key, int minutes) { savedKey = key; savedMinutes = minutes; return null; });
            settings.ShowActivated = false; settings.WindowStartupLocation = WindowStartupLocation.Manual;
            settings.Left = -10000; settings.Top = -10000; settings.Show();
            try
            {
                SaveElement((FrameworkElement)settings.Content, 430, 480, Path.Combine(folder, "chat-settings.png"));
                settings.KeyBox.Password = "short";
                Check(!settings.SaveSettings() && savedKey == null, "Chat settings validate a malformed key without saving", checks);
                settings.KeyBox.Password = "test-only-not-a-real-api-key";
                Check(settings.SaveSettings() && savedKey == "test-only-not-a-real-api-key" && savedMinutes == 0,
                    "Chat settings save a masked key with proactive greeting disabled by default", checks);
            }
            finally { settings.Close(); }
        }

        private static void PumpTask(Task task)
        {
            DispatcherFrame frame = new DispatcherFrame();
            DateTime end = DateTime.UtcNow.AddSeconds(5);
            DispatcherTimer poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
            poll.Tick += delegate { if (task.IsCompleted || DateTime.UtcNow > end) frame.Continue = false; };
            poll.Start();
            try { if (!task.IsCompleted) Dispatcher.PushFrame(frame); }
            finally { poll.Stop(); }
            if (!task.IsCompleted) throw new InvalidOperationException("A UI chat operation did not complete");
            task.GetAwaiter().GetResult();
        }
    }

    internal sealed class FakeChatHandler : HttpMessageHandler
    {
        internal int Calls;
        internal string Body, Url, Authorization;
        internal Func<CancellationToken, Task<HttpResponseMessage>> Respond;
        internal FakeChatHandler() { Respond = delegate { return Task.FromResult(GoodResponse()); }; }
        internal static HttpResponseMessage GoodResponse()
        {
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"陪你待一会儿。\",\"reasoning_content\":\"ignored private reasoning\"}}]}") };
        }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++; Url = request.RequestUri.AbsoluteUri; Authorization = request.Headers.Authorization.ToString();
            Body = await request.Content.ReadAsStringAsync();
            return await Respond(token);
        }
    }
}
