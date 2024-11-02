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
                if (File.Exists("δ�ҵ�.txt"))
                {
                    return string.Join("\n", File.ReadAllLines("δ�ҵ�.txt").Distinct());
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
                 var filePath = Path.Combine(dir, fileName); // ָ���ļ�������·��

                 if (!File.Exists(filePath))
                 {
                     return Results.NotFound(); // ����ļ������ڣ��򷵻� 404 Not Found
                 }

                 var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                 // �����ļ�������ָ��һ����ѡ���ļ���
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
                    System.IO.File.AppendAllText("δ�ҵ�.txt", info.name + "|" + info.singer + "\r\n");
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



        // �����������ϵĽ���
        private static HashSet<char> Intersection(HashSet<char> set1, HashSet<char> set2)
        {
            return new HashSet<char>(set1.Intersect(set2));
        }

        // �����������ϵĲ���
        private static HashSet<char> Union(HashSet<char> set1, HashSet<char> set2)
        {
            return new HashSet<char>(set1.Union(set2));
        }

        // ���� Jaccard ���ƶ�
        public static double CalculateJaccardSimilarity(string str1, string str2)
        {
            HashSet<char> set1 = new HashSet<char>(str1);
            HashSet<char> set2 = new HashSet<char>(str2);

            HashSet<char> intersection = Intersection(set1, set2);
            HashSet<char> union = Union(set1, set2);

            if (union.Count == 0)
            {
                return 1.0; // ����������϶�Ϊ�գ�Jaccard ���ƶ�Ϊ 1
            }

            return (double)intersection.Count / union.Count;
        }

        public static string ComputeMD5Hash(string input)
        {
            // ���� 1: ��ȡ MD5 ��ʵ��
            using (MD5 md5 = MD5.Create())
            {
                // ���� 2: ������ϢժҪ��hash��
                byte[] inputBytes = Encoding.UTF8.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                // ���� 3: ���ֽ�����ת��Ϊ�ַ���
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
