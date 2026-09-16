using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PhotoCat
{
    internal static class LocalPhotoCutout
    {
        internal static bool Available { get { return Assembly.GetExecutingAssembly().GetManifestResourceInfo("PhotoCat.photo-tools.zip") != null; } }
        internal static string CacheRoot { get { return Path.Combine(Path.GetDirectoryName(PetPreferences.DefaultPath), "tools"); } }
        private static string EnsureTools(string cache, CancellationToken token, IProgress<string> progress)
        {
            Assembly assembly=Assembly.GetExecutingAssembly();
            string stamp;
            using(Stream stream=assembly.GetManifestResourceStream("PhotoCat.photo-tools.sha256"))
            {
                if(stream==null) throw new InvalidDataException("这个开发版本没有内置抠图工具，请使用完整的成品 EXE。");
                using(StreamReader reader=new StreamReader(stream)) stamp=reader.ReadToEnd().Trim();
            }
            if(stamp.Length!=64) throw new InvalidDataException("抠图工具校验信息不完整，请重新下载成品。");
            string target=Path.Combine(cache,"offline-"+stamp.Substring(0,16));
            if(File.Exists(Path.Combine(target,"ready.txt")) && File.ReadAllText(Path.Combine(target,"ready.txt"))==stamp
                && File.Exists(Path.Combine(target,"PhotoCutout.exe")) && File.Exists(Path.Combine(target,"isnet-general-use.onnx"))) return target;
            progress.Report("首次准备照片工具，请稍候……");
            cache=Path.GetFullPath(cache).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
            Directory.CreateDirectory(cache);
            string pending=Path.Combine(cache,".p-"+Guid.NewGuid().ToString("N").Substring(0,12));
            Directory.CreateDirectory(pending);
            try
            {
                using(Stream embedded=assembly.GetManifestResourceStream("PhotoCat.photo-tools.zip"))
                {
                    if(embedded==null) throw new InvalidDataException("抠图工具不完整，请重新下载成品。");
                    using(SHA256 sha=SHA256.Create())
                    {
                        string actual=BitConverter.ToString(sha.ComputeHash(embedded)).Replace("-","").ToLowerInvariant();
                        if(actual!=stamp) throw new InvalidDataException("抠图工具校验失败，请重新下载成品。");
                    }
                    embedded.Position=0;
                    using(ZipArchive zip=new ZipArchive(embedded,ZipArchiveMode.Read))
                    foreach(ZipArchiveEntry entry in zip.Entries)
                    {
                        token.ThrowIfCancellationRequested();
                        string destination=Path.GetFullPath(Path.Combine(pending,entry.FullName));
                        if(!destination.StartsWith(pending+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("抠图工具路径不正确。");
                        if(entry.FullName.EndsWith("/")) { Directory.CreateDirectory(destination); continue; }
                        Directory.CreateDirectory(Path.GetDirectoryName(destination));
                        using(Stream input=entry.Open()) using(Stream output=File.Create(destination)) input.CopyTo(output);
                    }
                }
                token.ThrowIfCancellationRequested();
                File.WriteAllText(Path.Combine(pending,"ready.txt"),stamp,Encoding.ASCII);
                // A damaged cache is preserved, never recursively replaced.
                if(Directory.Exists(target)) target += "-"+Guid.NewGuid().ToString("N").Substring(0,8);
                Directory.Move(pending,target); return target;
            }
            finally
            {
                if(Path.GetFullPath(pending).StartsWith(cache+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)
                    && Path.GetFileName(pending).StartsWith(".p-",StringComparison.Ordinal) && Directory.Exists(pending))
                {
                    try { Directory.Delete(pending,true); } catch(IOException) { } catch(UnauthorizedAccessException) { }
                }
            }
        }
        internal static Task<byte[]> RunAsync(byte[] png, string cache, CancellationToken token, IProgress<string> progress)
        {
            return Task.Run(async delegate
            {
                string folder=EnsureTools(cache,token,progress);
                token.ThrowIfCancellationRequested();
                progress.Report("正在去掉背景，照片只在这台电脑处理……");
                ProcessStartInfo start=new ProcessStartInfo(Path.Combine(folder,"PhotoCutout.exe"),"\""+Path.Combine(folder,"isnet-general-use.onnx")+"\"");
                start.UseShellExecute=false; start.CreateNoWindow=true; start.WindowStyle=ProcessWindowStyle.Hidden;
                start.WorkingDirectory=folder; start.RedirectStandardInput=true; start.RedirectStandardOutput=true; start.RedirectStandardError=true;
                using(Process process=new Process { StartInfo=start })
                using(CancellationTokenSource deadline=CancellationTokenSource.CreateLinkedTokenSource(token))
                using(MemoryStream output=new MemoryStream())
                {
                    deadline.CancelAfter(TimeSpan.FromMinutes(3));
                    process.Start();
                    using(CancellationTokenRegistration stop=deadline.Token.Register(delegate { try { if(!process.HasExited) process.Kill(); } catch(InvalidOperationException) { } catch(System.ComponentModel.Win32Exception) { } }))
                    {
                        Task read=ReadBounded(process.StandardOutput.BaseStream,output,deadline.Token);
                        Task<string> errors=process.StandardError.ReadToEndAsync();
                        try
                        {
                            await process.StandardInput.BaseStream.WriteAsync(png,0,png.Length,deadline.Token).ConfigureAwait(false);
                            process.StandardInput.Close();
                            await read.ConfigureAwait(false);
                            process.WaitForExit(); await errors.ConfigureAwait(false);
                            deadline.Token.ThrowIfCancellationRequested();
                            if(process.ExitCode!=0 || output.Length<32) throw new InvalidDataException("这张照片暂时没处理成功。请换一张主体清晰的照片，或直接使用透明 PNG。");
                            return output.ToArray();
                        }
                        catch(Exception ex)
                        {
                            try { if(!process.HasExited) process.Kill(); } catch(InvalidOperationException) { } catch(System.ComponentModel.Win32Exception) { }
                            try { read.GetAwaiter().GetResult(); } catch { }
                            if(token.IsCancellationRequested) throw new OperationCanceledException(token);
                            if(deadline.IsCancellationRequested) throw new InvalidDataException("这次处理超过了 3 分钟，请换一张较小的照片再试。");
                            if(ex is IOException) throw new InvalidDataException("照片工具中断了，请重试或换一张照片。",ex);
                            throw;
                        }
                    }
                }
            },token);
        }
        private static async Task ReadBounded(Stream input,Stream output,CancellationToken token)
        {
            byte[] buffer=new byte[65536];int count;
            while((count=await input.ReadAsync(buffer,0,buffer.Length,token).ConfigureAwait(false))>0)
            {
                if(output.Length+count>20*1024*1024) throw new InvalidDataException("处理结果太大，请换一张较小的照片。");
                output.Write(buffer,0,count);
            }
        }
    }
}
