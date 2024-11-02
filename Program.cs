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
            app.MapGet("/ok", () =>
            {

                return "ok";
            });
            app.MapGet("/not", () =>
            {
                if (File.Exists("未找到.txt"))
                {
                    return string.Join("\n", File.ReadAllLines("未找到.txt").Distinct());
                }
                return "not";
            });

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
            app.MapPost("/musicapi", async (HttpContext context) =>
            {
                StreamReader sr = new StreamReader(context.Request.Body);
                var body = await sr.ReadToEndAsync();
                body = body.Replace("\\\"", "\"").Trim('"');
                // Console.WriteLine(body);
                MusicInfo info = System.Text.Json.JsonSerializer.Deserialize<MusicInfo>(body);
                List<string> strings = new List<string>();
                foreach (var item in System.IO.Directory.GetFiles(dir))
                {
                    if (System.IO.Path.GetExtension(item) != ".lrc" && 
                    (item.IndexOf(info.name) >= 0 || 
                    CalculateJaccardSimilarity(System.IO.Path.GetFileNameWithoutExtension(item), info.name) >= 0.6))
                    {
                        strings.Add(item);
                    }
                }
                if (strings.Count == 0)
                {
                    System.IO.File.AppendAllText("未找到.txt", info.name + "|" + info.singer + "\r\n");
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



        // 计算两个集合的交集
        private static HashSet<char> Intersection(HashSet<char> set1, HashSet<char> set2)
        {
            return new HashSet<char>(set1.Intersect(set2));
        }

        // 计算两个集合的并集
        private static HashSet<char> Union(HashSet<char> set1, HashSet<char> set2)
        {
            return new HashSet<char>(set1.Union(set2));
        }

        // 计算 Jaccard 相似度
        public static double CalculateJaccardSimilarity(string str1, string str2)
        {
            HashSet<char> set1 = new HashSet<char>(str1);
            HashSet<char> set2 = new HashSet<char>(str2);

            HashSet<char> intersection = Intersection(set1, set2);
            HashSet<char> union = Union(set1, set2);

            if (union.Count == 0)
            {
                return 1.0; // 如果两个集合都为空，Jaccard 相似度为 1
            }

            return (double)intersection.Count / union.Count;
        }

        public static string ComputeMD5Hash(string input)
        {
            // 步骤 1: 获取 MD5 的实例
            using (MD5 md5 = MD5.Create())
            {
                // 步骤 2: 计算消息摘要（hash）
                byte[] inputBytes = Encoding.UTF8.GetBytes(input);
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
