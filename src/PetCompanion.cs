using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace PhotoCat
{
    internal sealed class PhotoLook
    {
        internal string Id, Name;
        internal BitmapSource Photo;
        internal PhotoLook(string id, string name, BitmapSource photo) { Id = id; Name = name; Photo = photo; }
    }

    internal sealed partial class PetWindow
    {
        private readonly List<PhotoLook> looks = new List<PhotoLook>();
        private PetPreferences preferences;
        private ChatSession conversation;
        private CatChatWindow chatWindow;
        private ChatSettingsWindow settingsWindow;
        private int selectedLook;
        private double nextGreeting;
        private bool companionClosed;
        private string settingsNotice;

        private void InitializeCompanion()
        {
            preferences = testing ? new PetPreferences() : PetPreferences.Load(PetPreferences.DefaultPath, out settingsNotice);
            looks.Add(new PhotoLook("original", "原版坐姿", bitmap));
            string[] resources = Assembly.GetExecutingAssembly().GetManifestResourceNames();
            string[,] choices = { { "sweater", "红毛衣" }, { "tilt", "歪头看你" }, { "sofa", "乖乖侧坐" },
                { "lounge", "侧躺休息" }, { "belly", "露肚皮睡觉" }, { "back", "毛茸茸的背影" }, { "desk", "陪你上班" } };
            for (int i = 0; i < choices.GetLength(0); i++)
                if (Array.IndexOf(resources, "PhotoCat.looks." + choices[i, 0] + ".png") >= 0)
                    looks.Add(new PhotoLook(choices[i, 0], choices[i, 1], LoadPhoto("looks." + choices[i, 0] + ".png")));
            for (int i = 0; i < looks.Count; i++) if (looks[i].Id == preferences.LookId) selectedLook = i;
            cat.SetIdlePhoto(looks[selectedLook].Photo, selectedLook == 0);
            conversation = new ChatSession(new DeepSeekClient());
            ScheduleGreeting();
        }

        private MenuItem LooksMenu()
        {
            MenuItem menu = new MenuItem { Header = "换造型（双击猫咪也可以）" };
            for (int i = 0; i < looks.Count; i++)
            {
                int index = i;
                MenuItem item = MenuAction(looks[i].Name, delegate { SelectLook(index, true); });
                item.IsChecked = i == selectedLook;
                item.Icon = new Image { Source = looks[i].Photo, Width = 32, Height = 40 };
                menu.Items.Add(item);
            }
            return menu;
        }

        private void SelectLook(int index, bool save)
        {
            selectedLook = index;
            motion.ReturnToCompanion();
            pendingWalkingDirection = 0; walkingRemainder = 0;
            cat.SetIdlePhoto(looks[index].Photo, index == 0);
            cat.SetPose(motion.Advance(0));
            preferences.LookId = looks[index].Id;
            Say("换成“" + looks[index].Name + "”啦。", 3);
            if (save && !testing)
            {
                try { preferences.Save(PetPreferences.DefaultPath); }
                catch (Exception ex)
                {
                    if (!(ex is IOException || ex is UnauthorizedAccessException || ex is System.Security.Cryptography.CryptographicException)) throw;
                    Say("造型已切换，但这次没能保存设置。", 4);
                }
            }
        }
        private void NextLook() { SelectLook((selectedLook + 1) % looks.Count, true); }

        private void OpenChat()
        {
            if (chatWindow == null)
            {
                chatWindow = new CatChatWindow(conversation, delegate { return preferences.ApiKey; }, OpenChatSettings, SayReply);
                chatWindow.Owner = this;
                chatWindow.Closed += delegate { chatWindow = null; };
            }
            chatWindow.Show();
            chatWindow.Activate();
        }

        private void OpenChatSettings()
        {
            if (settingsWindow != null) { settingsWindow.Activate(); return; }
            settingsWindow = new ChatSettingsWindow(preferences, delegate(string key, int minutes)
            {
                PetPreferences updated = new PetPreferences { ApiKey = key, GreetingMinutes = minutes, LookId = preferences.LookId };
                try { if (!testing) updated.Save(PetPreferences.DefaultPath); }
                catch (Exception ex)
                {
                    if (!(ex is IOException || ex is UnauthorizedAccessException || ex is System.Security.Cryptography.CryptographicException)) throw;
                    return "设置没能保存，请检查本机文件夹权限后重试。";
                }
                bool changedKey = preferences.ApiKey != key;
                preferences = updated;
                if (changedKey)
                {
                    conversation.Clear();
                    if (chatWindow != null) chatWindow.ClearDisplayedConversation();
                }
                ScheduleGreeting();
                return null;
            });
            settingsWindow.Owner = chatWindow != null ? (Window)chatWindow : this;
            settingsWindow.Closed += delegate { settingsWindow = null; };
            settingsWindow.ShowDialog();
        }

        private void SayReply(string text)
        {
            if (companionClosed) return;
            string shortText = text.Length <= 80 ? text : text.Substring(0, 77) + "…";
            Say(shortText, 12);
        }
        private void ScheduleGreeting() { nextGreeting = clock.Elapsed.TotalSeconds + Math.Max(1, preferences.GreetingMinutes) * 60; }
        private void TickCompanion()
        {
            if (testing || companionClosed || preferences.GreetingMinutes == 0 || clock.Elapsed.TotalSeconds < nextGreeting) return;
            if (!animate || moving || menuOpen || motion.IsSleeping || conversation.Busy || settingsWindow != null || (chatWindow != null && chatWindow.IsVisible)) return;
            ScheduleGreeting();
            GreetPet(false);
        }
        private async void GreetPet(bool manual)
        {
            if (String.IsNullOrWhiteSpace(preferences.ApiKey))
            {
                if (manual) OpenChatSettings();
                if (String.IsNullOrWhiteSpace(preferences.ApiKey)) return;
            }
            if (conversation.Busy) { if (manual) Say("等我说完这句，再和你打招呼。", 3); return; }
            if (manual && chatWindow != null && chatWindow.IsVisible)
            {
                await chatWindow.SendTextAsync(true);
                return;
            }
            try
            {
                if (manual) Say("猫咪正在想……", 45);
                string reply = await conversation.SendAsync(preferences.ApiKey, ChatSession.GreetingPrompt());
                if (!companionClosed && IsVisible) SayReply(reply);
            }
            catch (ChatProblem problem) { if (!companionClosed && IsVisible) Say(problem.Message, 6); }
            catch (OperationCanceledException) { }
        }
        private void CloseCompanion()
        {
            companionClosed = true;
            if (conversation != null) conversation.Dispose();
            if (chatWindow != null) chatWindow.Close();
            if (settingsWindow != null) settingsWindow.Close();
        }
    }
}
