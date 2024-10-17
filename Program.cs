using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.VisualBasic;
using System.Security.Cryptography;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace LXMusicServer
{
    public class Program
    {

        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddAuthorization();

            var dir = builder.Configuration["MusicDir"];
            var domain = builder.Configuration["MusicDomain"];

            var app = builder.Build();

            // Configure the HTTP request pipeline.

            app.UseAuthorization();

            app.MapGet("/play/{fileName}", (string fileName) =>
            {

                bool found = false;
                if (System.IO.File.Exists("index.idx"))
                {
                    var index = System.IO.File.ReadAllLines("index.idx");
                    foreach (var item in index)
                    {
                        if (item.StartsWith(fileName))
                        {
                            fileName = item.Substring(fileName.Length).Trim();
                            found = true;
                            break;
                        }
                    }
                } 
                if (!found)
                {
                    StringBuilder sb = new StringBuilder();
                    foreach (var item in System.IO.Directory.GetFiles(dir))
                    {
                        sb.AppendLine(ComputeMD5Hash(System.IO.Path.GetFileName(item)) + System.IO.Path.GetExtension(item) + "\t" + item);
                    }
                    System.IO.File.WriteAllText("index.idx", sb.ToString());
                }
                var filePath = Path.Combine(dir, fileName); // 指定文件的完整路径

                if (!File.Exists(filePath))
                {
                    return Results.NotFound(); // 如果文件不存在，则返回 404 Not Found
                }

                var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                // 返回文件流，并指定一个可选的文件名
                return Results.File(fileStream, "audio/mpeg", fileName);
            });

            app.MapPost("/musicapi", async (MusicInfo info) =>
            {

                List<string> strings = new List<string>();
                foreach (var item in System.IO.Directory.GetFiles(dir))
                {
                    if (item.IndexOf(info.name) >= 0 && System.IO.Path.GetExtension(item) != ".lrc")
                    {
                        strings.Add(item);
                    }
                }
                if (strings.Count == 0)
                {
                    System.IO.File.AppendAllText("notfound.log", info.name);
                    System.IO.File.AppendAllText("notfound.log", System.Text.Json.JsonSerializer.Serialize(info));
                    return null;
                }
                var ext = strings.FirstOrDefault(x => x.Contains(info.singer));
                if (ext == null)
                {
                    ext = strings.First();
                }

                return new { url = domain + "/play/" + ComputeMD5Hash(System.IO.Path.GetFileName(ext)) + System.IO.Path.GetExtension(ext) };

            });

            app.Run();
        }

        public static string ComputeMD5Hash(string input)
        {
            // 步骤 1: 获取 MD5 的实例
            using (MD5 md5 = MD5.Create())
            {
                // 步骤 2: 计算消息摘要（hash）
                byte[] inputBytes = Encoding.ASCII.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                // 步骤 3: 将字节数组转换为字符串
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }
    }

}
