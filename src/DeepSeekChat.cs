using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace PhotoCat
{
    internal sealed class ChatProblem : Exception
    {
        internal ChatProblem(string message) : base(message) { }
    }

    internal sealed class ChatMessage
    {
        public string role { get; set; }
        public string content { get; set; }
        internal ChatMessage(string speaker, string text) { role = speaker; content = text; }
    }

    internal sealed class DeepSeekClient : IDisposable
    {
        internal const string Model = "deepseek-flash";
        internal const string Endpoint = "https://api.deepseek.com/chat/completions";
        private readonly HttpClient http;
        internal DeepSeekClient() : this(new HttpClientHandler { AllowAutoRedirect = false }) { }
        internal DeepSeekClient(HttpMessageHandler handler)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            http = new HttpClient(handler);
            http.Timeout = TimeSpan.FromSeconds(45);
            http.MaxResponseContentBufferSize = 131072;
        }

        internal static void ValidateKey(string key)
        {
            if (String.IsNullOrWhiteSpace(key)) throw new ChatProblem("先在“聊天设置”里填写 DeepSeek API Key，就可以聊天了。");
            if (key.Length > 512 || key.Length < 8) throw new ChatProblem("API Key 长度不正确，请检查是否复制完整。");
            foreach (char c in key)
                if (c < 33 || c > 126) throw new ChatProblem("API Key 中有空格或无效字符，请重新复制。");
        }

        internal async Task<string> ReplyAsync(string key, IList<ChatMessage> history, string userText, CancellationToken token)
        {
            ValidateKey(key);
            List<ChatMessage> messages = new List<ChatMessage>();
            messages.Add(new ChatMessage("system", "你是桌面猫咪的 AI 陪伴助手。用自然简短的中文交流，温和、有一点猫咪的俏皮，少用语气词。用户问问题时认真回答，通常不超过200字。不要声称能看到用户屏幕、读取文件或知道未提供的生活信息。不要催促用户持续聊天。"));
            messages.AddRange(history);
            messages.Add(new ChatMessage("user", userText));
            string json = new JavaScriptSerializer().Serialize(new {
                model = Model, messages = messages, thinking = new { type = "disabled" }, stream = false, max_tokens = 768
            });
            try
            {
                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Endpoint))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                    using (HttpResponseMessage response = await http.SendAsync(request, token).ConfigureAwait(false))
                    {
                        if (!response.IsSuccessStatusCode) throw new ChatProblem(StatusMessage((int)response.StatusCode));
                        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        token.ThrowIfCancellationRequested();
                        try
                        {
                            ChatResponse answer = new JavaScriptSerializer { MaxJsonLength = 131072 }.Deserialize<ChatResponse>(body);
                            if (answer == null || answer.choices == null || answer.choices.Length == 0 || answer.choices[0] == null
                                || answer.choices[0].message == null || String.IsNullOrWhiteSpace(answer.choices[0].message.content))
                                throw new ChatProblem("这次没有收到完整回复，可以再试一次。");
                            string text = answer.choices[0].message.content.Trim();
                            return text.Length <= 4000 ? text : text.Substring(0, 4000) + "…";
                        }
                        catch (ArgumentException) { throw new ChatProblem("收到的回复格式不完整，可以再试一次。"); }
                        catch (InvalidOperationException) { throw new ChatProblem("收到的回复格式不完整，可以再试一次。"); }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                if (token.IsCancellationRequested) throw;
                throw new ChatProblem("等待回复超时了，请检查网络后再试一次。");
            }
            catch (HttpRequestException) { throw new ChatProblem("暂时连不上 DeepSeek，请检查网络后再试一次。"); }
        }

        internal static string StatusMessage(int status)
        {
            if (status == 401 || status == 403) return "API Key 无效或没有访问权限，请检查聊天设置。";
            if (status == 402) return "DeepSeek 账户余额不足，请充值后再试。";
            if (status == 429) return "请求有些频繁，请稍等一会儿再聊。";
            if (status == 400 || status == 404 || status == 422) return "DeepSeek 暂时无法处理这次请求，请稍后重试或更新桌宠。";
            if (status >= 500) return "DeepSeek 服务暂时繁忙，请稍后再试。";
            return "这次未能收到回复，请稍后再试。";
        }

        public sealed class ChatResponse { public Choice[] choices { get; set; } }
        public sealed class Choice { public Answer message { get; set; } }
        public sealed class Answer { public string content { get; set; } }
        public void Dispose() { http.Dispose(); }
    }

    internal sealed class ChatSession : IDisposable
    {
        private readonly DeepSeekClient client;
        private readonly List<ChatMessage> history = new List<ChatMessage>();
        private CancellationTokenSource pending;
        internal bool Busy { get { return pending != null; } }
        internal int MessageCount { get { return history.Count; } }
        internal ChatSession(DeepSeekClient api) { client = api; }

        internal async Task<string> SendAsync(string key, string text)
        {
            if (Busy) throw new ChatProblem("猫咪正在回复，等这句说完再聊吧。");
            DeepSeekClient.ValidateKey(key);
            text = (text ?? "").Trim();
            if (text.Length == 0 || text.Length > 2000) throw new ChatProblem("请输入 1 到 2000 个字的消息。");
            using (CancellationTokenSource request = new CancellationTokenSource())
            {
                pending = request;
                try
                {
                    string answer = await client.ReplyAsync(key, new List<ChatMessage>(history), text, request.Token);
                    request.Token.ThrowIfCancellationRequested();
                    history.Add(new ChatMessage("user", text));
                    history.Add(new ChatMessage("assistant", answer));
                    while (history.Count > 12) history.RemoveRange(0, 2);
                    return answer;
                }
                finally { if (pending == request) pending = null; }
            }
        }
        internal ChatMessage[] Snapshot() { return history.ToArray(); }
        internal void Cancel() { if (pending != null) pending.Cancel(); }
        internal void Clear() { Cancel(); history.Clear(); }
        internal static string GreetingPrompt()
        {
            return "现在是本地时间 " + DateTime.Now.ToString("HH:mm") + "。请用不超过35字的一句中文自然问候我，可以轻轻提醒休息或陪我说句话，不要假装知道我正在做什么。";
        }
        public void Dispose() { Cancel(); client.Dispose(); }
    }
}
