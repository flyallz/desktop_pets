using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace PhotoCat
{
    internal sealed class PetPreferences
    {
        internal string LookId = "original";
        internal string ApiKey = "";
        internal int GreetingMinutes;
        internal static string DefaultPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PhotoCat", "settings.json"); }
        }

        public sealed class Stored
        {
            public string LookId { get; set; }
            public string ProtectedApiKey { get; set; }
            public int GreetingMinutes { get; set; }
        }

        internal static PetPreferences Load(string path, out string notice)
        {
            notice = null;
            PetPreferences result = new PetPreferences();
            try
            {
                if (!File.Exists(path)) return result;
                if (new FileInfo(path).Length > 32768) throw new InvalidDataException();
                Stored saved = new JavaScriptSerializer().Deserialize<Stored>(File.ReadAllText(path, Encoding.UTF8));
                if (saved == null) throw new InvalidDataException();
                result.LookId = String.IsNullOrEmpty(saved.LookId) ? "original" : saved.LookId;
                result.GreetingMinutes = ValidInterval(saved.GreetingMinutes) ? saved.GreetingMinutes : 0;
                if (!String.IsNullOrEmpty(saved.ProtectedApiKey))
                {
                    byte[] plain = null;
                    try
                    {
                        plain = ProtectedData.Unprotect(Convert.FromBase64String(saved.ProtectedApiKey), null, DataProtectionScope.CurrentUser);
                        result.ApiKey = Encoding.UTF8.GetString(plain);
                    }
                    catch (CryptographicException) { notice = "本机无法解密原来的 API Key，请在聊天设置里重新填写。"; }
                    catch (FormatException) { notice = "原来的 API Key 保存不完整，请在聊天设置里重新填写。"; }
                    finally { if (plain != null) Array.Clear(plain, 0, plain.Length); }
                }
                if (String.IsNullOrWhiteSpace(result.ApiKey)) result.GreetingMinutes = 0;
            }
            catch (Exception ex)
            {
                if (!(ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is InvalidOperationException)) throw;
                notice = "暂时无法读取设置，已使用默认值。";
            }
            return result;
        }

        internal static bool ValidInterval(int minutes) { return minutes == 0 || minutes == 15 || minutes == 30 || minutes == 60 || minutes == 120; }

        internal void Save(string path)
        {
            byte[] plain = Encoding.UTF8.GetBytes(ApiKey ?? "");
            string protectedKey;
            try
            {
                protectedKey = plain.Length == 0 ? "" : Convert.ToBase64String(ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser));
            }
            finally { Array.Clear(plain, 0, plain.Length); }
            Stored saved = new Stored { LookId = LookId, ProtectedApiKey = protectedKey, GreetingMinutes = GreetingMinutes };
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, new JavaScriptSerializer().Serialize(saved), new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
