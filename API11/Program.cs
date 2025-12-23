using API11.Classes;
using API11.Models.Responce;
using API11.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace API11
{
    internal class Program
    {
        // <summary>
        // Клиент 
        // </summary>

        public static string ClientId = "019b46ca-19f5-7dc1-b8d0-c65a3a79add4";

        // <summary>
        // Код авторизации
        // </summary>
        public static string AuthorizationKey = "MDE5YjQ2Y2EtMTlmNS03ZGMxLWI4ZDAtYzY1YTNhNzlhZGQ0Ojk2MTkyM2ViLTFmMjctNDA3My05MzhiLWI1MDYyODQ5YjcyYg==";
        static List<Models.Request.Message> DialogHistory =
       new List<Models.Request.Message>()
       {
           //new Models.Request.Message
           //{
           //    role = "system",
           //    content = "Ты — Василий Кандинский"
           //}
       };
        static async Task Main(string[] args)
        {
            string Token = await GetToken(ClientId, AuthorizationKey);
            if (Token == null)
            {
                Console.WriteLine("Не удалось получить токен");
                return;
            }





            while (true)
            {
                Console.Write("Сообщение: токен получен");
                string message = Console.ReadLine();

                DialogHistory.Add(new Models.Request.Message
                {
                    role = "user",
                    content = message
                });

                ResponseMessage answer = await GetAnswer(Token, DialogHistory);

                string assistantText = answer.choices[0].message.content;
                Console.WriteLine("Ответ: " + assistantText);

                DialogHistory.Add(new Models.Request.Message
                {
                    role = "assistant",
                    content = assistantText
                });

                string fileId = ExtractImageId(assistantText);

                if (!string.IsNullOrEmpty(fileId))
                {
                    byte[] img = await DownloadImage(Token, fileId);
                    string path = SaveImage(img);
                    WallpaperSetter.SetWallpaper(path);
                    Console.WriteLine("Обои успешно установлены!");
                }
                else
                {
                    Console.WriteLine("Ответ не содержит изображения.");
                }

            }


        }

        public static async Task<ResponseMessage> GetAnswer(string token, List<Models.Request.Message> messages)
        {

            ResponseMessage responseMessage = null;

            string Url = "https://gigachat.devices.sberbank.ru/api/v1/chat/completions";

            using (HttpClientHandler Handler = new HttpClientHandler())
            {
                Handler.ServerCertificateCustomValidationCallback = (message, cert, chain, SslPolicyErrors) => true;

                using (HttpClient Client = new HttpClient(Handler))
                {
                    HttpRequestMessage Request = new HttpRequestMessage(HttpMethod.Post, Url);

                    Request.Headers.Add("Accept", "application/json");
                    Request.Headers.Add("Authorization", $"Bearer {token}");

                    Models.Request DataRequest = new Models.Request()
                    {
                        model = "GigaChat:2.0.28.2",
                        messages = messages,
                        function_call = "auto",
                        temperature = 0.3,
                        max_tokens = 1500
                    };


                    string JsonContent = JsonConvert.SerializeObject(DataRequest);
                    Request.Content = new StringContent(JsonContent, Encoding.UTF8, "application/json");

                    HttpResponseMessage Responce = await Client.SendAsync(Request);

                    if (Responce.IsSuccessStatusCode)
                    {
                        string ResponseContent = await Responce.Content.ReadAsStringAsync();
                        responseMessage = JsonConvert.DeserializeObject<ResponseMessage>(ResponseContent);
                    }
                }
            }
            return responseMessage;

        }

        public static string ExtractImageId(string content)
        {
            var start = content.IndexOf("src=\"") + 5;
            var end = content.IndexOf("\"", start);
            return content.Substring(start, end - start);
        }
        public static string SaveImage(byte[] data)
        {
            string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "gigachat_wallpaper.jpg");
            File.WriteAllBytes(filePath, data);
            return filePath;
        }
        public static async Task<byte[]> DownloadImage(string token, string fileId)
        {
            string url = $"https://gigachat.devices.sberbank.ru/api/v1/files/{fileId}/content";

            using (HttpClientHandler handler = new HttpClientHandler())
            {
                handler.ServerCertificateCustomValidationCallback = (msg, cert, chain, errors) => true;

                using (HttpClient client = new HttpClient(handler))
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

                    HttpResponseMessage response = await client.GetAsync(url);

                    response.EnsureSuccessStatusCode();

                    return await response.Content.ReadAsByteArrayAsync();
                }
            }
        }


        public static async Task<string> GetToken(string rqUID, string bearer)
        {
            string ReturnToken = null;
            string Url = "https://ngw.devices.sberbank.ru:9443/api/v2/oauth";

            using (HttpClientHandler Handler = new HttpClientHandler())
            {
                Handler.ServerCertificateCustomValidationCallback = (message, cert, chain, SslPolicyErrors) => true;
                using (HttpClient Client = new HttpClient(Handler))
                {
                    HttpRequestMessage Request = new HttpRequestMessage(HttpMethod.Post, Url);
                    Request.Headers.Add("Accept", "application/json");
                    Request.Headers.Add("RqUID", rqUID);
                    Request.Headers.Add("Authorization", $"Bearer {bearer}");

                    var Data = new List<KeyValuePair<string, string>>
                    {
                        new KeyValuePair<string, string>("scope","GIGACHAT_API_PERS")
                    };

                    Request.Content = new FormUrlEncodedContent(Data);
                    HttpResponseMessage Responce = await Client.SendAsync(Request);

                    if (Responce.IsSuccessStatusCode)
                    {
                        string ResponseContent = await Responce.Content.ReadAsStringAsync();
                        ResponseToken Token = JsonConvert.DeserializeObject<ResponseToken>(ResponseContent);
                        ReturnToken = Token.access_token;
                    }
                }
            }
            return ReturnToken;
        }
    }
}
