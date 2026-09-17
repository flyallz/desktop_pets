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
        internal PhotoCustomization Customization;
        internal bool IsCustom { get { return UserPhotoStore.IsCustom(Id); } }
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
        private UserPhotoStore photoStore;
        private PhotoEditorWindow photoEditor;
        private bool builtinAutomatic = true;
        private bool CustomLook { get { return looks.Count > 0 && looks[selectedLook].IsCustom; } }
        private double nextGreeting;
        private bool companionClosed;
        private string settingsNotice;

        private void InitializeCompanion()
        {
            preferences = testing ? new PetPreferences { LookId = "original" } : PetPreferences.Load(PetPreferences.DefaultPath, out settingsNotice);
            looks.Add(new PhotoLook("original", "原版坐姿", bitmap));
            string[] resources = Assembly.GetExecutingAssembly().GetManifestResourceNames();
            string[,] choices = { { "sweater", "红毛衣" }, { "tilt", "歪头看你" }, { "sofa", "乖乖侧坐" },
                { "lounge", "侧躺休息" }, { "belly", "露肚皮睡觉" }, { "back", "毛茸茸的背影" }, { "desk", "陪你上班" } };
            for (int i = 0; i < choices.GetLength(0); i++)
                if (Array.IndexOf(resources, "PhotoCat.looks." + choices[i, 0] + ".png") >= 0)
                    looks.Add(new PhotoLook(choices[i, 0], choices[i, 1], LoadPhoto("looks." + choices[i, 0] + ".png")));
            cat.LoadSpriteAtlas(LoadPhoto("sprites.cat-spritesheet.png"));
            looks.Add(new PhotoLook("animated", "动作版 · 9组新动作", cat.SpriteIdlePhoto));
            photoStore = new UserPhotoStore(UserPhotoStore.DefaultFolder);
            if (!testing)
            {
                try
                {
                    int skipped; looks.AddRange(photoStore.Load(out skipped));
                    if (skipped > 0) settingsNotice = (settingsNotice ?? "") + " 有 " + skipped + " 张自选造型暂时没能读取，内置造型仍可使用。";
                }
                catch (Exception ex)
                {
                    if (!(ex is IOException || ex is UnauthorizedAccessException)) throw;
                    settingsNotice = (settingsNotice ?? "") + " 照片库暂时没能读取，可以先用内置造型。";
                }
            }
            for (int i = 0; i < looks.Count; i++) if (looks[i].Id == preferences.LookId) selectedLook = i;
            if (CustomLook) motion.SetAutomatic(false);
            ApplyLookPhoto();
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
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuAction("添加自己的照片…", delegate { OpenPhotoEditor(false); }));
            if (CustomLook)
            {
                menu.Items.Add(MenuAction("调整这张照片 / 改名字…", delegate { OpenPhotoEditor(true); }));
                menu.Items.Add(MenuAction("删除这张自选照片", RemoveCurrentPhoto));
            }
            return menu;
        }

        private void SelectLook(int index, bool save)
        {
            bool previousCustom = CustomLook;
            if (!previousCustom) builtinAutomatic = motion.Automatic;
            selectedLook = index;
            motion.ReturnToCompanion();
            if (CustomLook) motion.SetAutomatic(false);
            else if (previousCustom) motion.SetAutomatic(builtinAutomatic);
            pendingWalkingDirection = 0; walkingRemainder = 0;
            ApplyLookPhoto();
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

        private void ApplyLookPhoto()
        {
            PhotoLook look = looks[selectedLook];
            cat.SetSpriteMode(look.Id == "animated");
            motion.SetSpriteActions(look.Id == "animated");
            if (look.IsCustom) CustomMotionSafety.Apply(cat, look.Photo, look.Id, look.Customization ?? new PhotoCustomization());
            else cat.SetIdlePhoto(look.Photo, look.Id);
        }
        private void OpenPhotoEditor(bool edit)
        {
            if (photoEditor != null) { photoEditor.Activate(); return; }
            PhotoLook existing = edit && CustomLook ? looks[selectedLook] : null;
            photoEditor = new PhotoEditorWindow(existing, delegate(BitmapSource image, string name, PhotoCustomization data)
            { return SaveUserPhoto(existing, image, name, data); });
            photoEditor.Owner = this;
            photoEditor.Closed += delegate { photoEditor = null; lastTick = clock.Elapsed.TotalSeconds; };
            photoEditor.ShowDialog();
        }
        private string SaveUserPhoto(PhotoLook existing, BitmapSource image, string name, PhotoCustomization data)
        {
            try
            {
                PhotoLook saved;
                if (existing == null) { saved = photoStore.Add(image, name, data); looks.Add(saved); }
                else
                {
                    saved = new PhotoLook(existing.Id, name, image) { Customization = data };
                    photoStore.Save(saved); looks[looks.FindIndex(delegate(PhotoLook item) { return item.Id == existing.Id; })] = saved;
                }
                SelectLook(looks.IndexOf(saved), true);
                return null;
            }
            catch (Exception ex)
            {
                if (!(ex is IOException || ex is UnauthorizedAccessException)) throw;
                return "这次没能保存照片，请检查电脑是否有可用空间后重试。";
            }
        }
        private void RemoveCurrentPhoto()
        {
            if (!CustomLook) return;
            if (MessageBox.Show(this, "从桌宠中删除“" + looks[selectedLook].Name + "”？\n你电脑上的原照片不会被删除。", "删除自选造型", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            DeleteUserPhoto(looks[selectedLook].Id);
        }
        private bool DeleteUserPhoto(string id)
        {
            try
            {
                int index = looks.FindIndex(delegate(PhotoLook item) { return item.Id == id && item.IsCustom; });
                if (index < 0) return false;
                photoStore.Delete(id);
                if (selectedLook == index) SelectLook(0, true);
                else if (selectedLook > index) selectedLook--;
                looks.RemoveAt(index); return true;
            }
            catch (Exception ex)
            {
                if (!(ex is IOException || ex is UnauthorizedAccessException)) throw;
                Say("这次没能删除照片，请稍后重试。", 4); return false;
            }
        }

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
            if (!animate || moving || menuOpen || motion.IsSleeping || conversation.Busy || settingsWindow != null || photoEditor != null || (chatWindow != null && chatWindow.IsVisible)) return;
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
            if (photoEditor != null) photoEditor.Close();
            if (conversation != null) conversation.Dispose();
            if (chatWindow != null) chatWindow.Close();
            if (settingsWindow != null) settingsWindow.Close();
        }
    }
}
