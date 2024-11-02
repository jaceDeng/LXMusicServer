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
            // 注册 CORS 服务
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", builder =>
                {
                    builder.AllowAnyOrigin()
                           .AllowAnyMethod()
                           .AllowAnyHeader();
                });
            });
            // 设置最大请求体大小（单位：字节）
            builder.WebHost.UseKestrel(options =>
            {
                options.Limits.MaxRequestBodySize = long.MaxValue; // 设置为无限制
                                                                   // 或者设置一个合理的上限，例如 100MB:
                                                                   // options.Limits.MaxRequestBodySize = 100 * 1024 * 1024;
            });
            var dir = builder.Configuration["MusicDir"];
            var domain = builder.Configuration["MusicDomain"];

            var app = builder.Build();
            app.UseCors("AllowAll");
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
                    var list = File.ReadAllLines("未找到.txt").Distinct();
                    List<string> list2 = new List<string>();
                    foreach (var line in list)
                    {
                        var array = line.Split('|');
                        bool found = false;
                        foreach (var item in System.IO.Directory.GetFiles(dir))
                        {
                            if (item.IndexOf(array[0]) >= 0 && System.IO.Path.GetExtension(item) != ".lrc")
                            {
                                found = true;
                                break;

                            }
                        }
                        if (!found)
                        {
                            list2.Add(line);
                        }
                    }

                    return string.Join("\n", list2);
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
                 return Results.File(fileStream, "audio/mpeg", fileName, enableRangeProcessing: true);
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
            app.MapGet("/upload", async (HttpContext context) =>
            {
                var html = @"<!DOCTYPE html>
<html lang=""zh-CN"">
<head>
    <meta charset=""UTF-8"">
    <title>拖拽上传文件</title>
    <style>
        body {
            font-family: Arial, sans-serif;
            background-color: #f0f0f0;
            display: flex;
            justify-content: center;
            align-items: center;
            height: 100vh;
            margin: 0;
        }
        .container {
            width: 700px;
            padding: 20px;
            border: 2px dashed #ccc;
            border-radius: 8px;
            text-align: center;
            cursor: pointer;
        }
        .container.active {
            border-color: #666;
        }
        .container p {
            margin-top: 20px;
            color: #666;
        }
        .files-list {
            margin-top: 10px;
            color: #0793bf;
        }
        button {
            margin-top: 20px;
            padding: 10px 20px;
            font-size: 16px;
            border: none;
            border-radius: 5px;
            background-color: #007BFF;
            color: white;
            cursor: pointer;
        }
        button:hover {
            background-color: #0056b3;
        }

    </style>
</head>
<body>
    <form id=""upload-form"" enctype=""multipart/form-data"">
        <div class=""container"" id=""drop-zone"">
            <p>将文件拖到这里或点击选择文件</p>
            <input type=""file"" id=""file-input"" name=""file"" multiple style=""display:none;"">
            <div class=""files-list"" id=""files-list""></div>
        </div>
        <button id=""upload-button"">上传文件</button>
    </form>

    <script>
        const dropZone = document.getElementById('drop-zone');
        const fileInput = document.getElementById('file-input');
        const uploadButton = document.getElementById('upload-button');
        const fileList = document.getElementById('files-list');
        const form = document.getElementById('upload-form');

        // 设置拖拽事件
        ['dragenter', 'dragover', 'dragleave', 'drop'].forEach(eventName => {
            dropZone.addEventListener(eventName, preventDefaults, false);
        });

        // 处理拖拽进入和离开
        function preventDefaults(e) {
            e.preventDefault();
            e.stopPropagation();
        }

        // 设置拖拽进入时的样式
        ['dragenter', 'dragover'].forEach(eventName => {
            dropZone.addEventListener(eventName, () => {
                dropZone.classList.add('active');
            }, false);
        });

        // 设置拖拽离开时的样式
        ['dragleave', 'drop'].forEach(eventName => {
            dropZone.addEventListener(eventName, () => {
                dropZone.classList.remove('active');
            }, false);
        });

        // 设置drop事件
        dropZone.addEventListener('drop', handleDrop, false);

        function handleDrop(e) {
            const dt = e.dataTransfer;
            const files = dt.files;
            handleFiles(files);
        }

        // 点击事件
        dropZone.addEventListener('click', () => fileInput.click());

        fileInput.addEventListener('change', (e) => {
            const files = e.target.files;
            handleFiles(files);
        });

        function handleFiles(files) {
            if (files.length > 0) {
                fileInput.files = files;
                form.appendChild(fileInput); // 将文件输入附加到表单中
                let fileListText = '';
                for (let i = 0; i < files.length; i++) {
                    fileListText += `<span>${files[i].name}</span><br>`;
                }
                fileList.innerHTML = fileListText;

                uploadButton.disabled = false;
            } else {
                fileList.innerHTML = ''; // 清空文件列表
                uploadButton.disabled = true;
            }
        }

        uploadButton.addEventListener('click', async () => {
            uploadButton.disabled = true;
            uploadButton.textContent = '正在上传...';

            try {
                const formData = new FormData(form);
                const response = await fetch('/upload', {
                    method: 'POST',
                    body: formData
                });
                if (!response.ok) {
                    throw new Error(`HTTP 错误! 状态码: ${response.status}`);
                }
                const data = await response.json();
                alert(data.message);
            } catch (error) {
                console.error('错误:', error);
                alert('上传文件失败，请检查控制台获取更多信息。');
            } finally {
                uploadButton.textContent = '上传文件';
                uploadButton.disabled = false;
                fileList.innerHTML = ''; // 清空文件列表
            }
        });
    </script>
</body>
</html>";
                return Results.Content(html, "text/html");
            });
            app.MapPost("/upload", async (HttpContext context) =>
            {

                var request = await context.Request.ReadFormAsync();
                foreach (var file in request.Files)
                {
                    if (file.Length > 0)
                    {
                        // 获取文件名
                        string fileName = file.FileName;

                        // 将文件写入磁盘
                        using (var stream = new FileStream(Path.Combine(dir, fileName), FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }
                    }
                }
                return Results.Ok(new { message = "文件上传成功" });
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
