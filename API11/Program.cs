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
        // Client ID для доступа к GigaChat API
        // ВНИМАНИЕ: Нужно заменить на свои данные после регистрации на developers.sber.ru
        // </summary>
        public static string ClientId = "019b46ca-19f5-7dc1-b8d0-c65a3a79add4";

        // <summary>
        // Ключ авторизации в формате Base64 (ClientId:ClientSecret)
        // ВНИМАНИЕ: Это конфиденциальные данные! Не публикуйте в открытом доступе
        // </summary>
        public static string AuthorizationKey = "MDE5YjQ2Y2EtMTlmNS03ZGMxLWI4ZDAtYzY1YTNhNzlhZGQ0Ojk2MTkyM2ViLTFmMjctNDA3My05MzhiLWI1MDYyODQ5YjcyYg==";

        // История диалога для поддержания контекста разговора с ИИ
        static List<Models.Request.Message> DialogHistory = new List<Models.Request.Message>()
    {
        // Системный промпт, который задает роль ИИ
        new Models.Request.Message
        {
            role = "system",
            content = "Ты помощник, который генерирует изображения. Когда пользователь просит изображение, ты создаешь его и возвращаешь file_id в формате <img src=\"file_id\">. Ты умеешь создавать любые изображения: картины, пейзажи, портреты, абстракции."
        }
    };
        // Главный метод программы (точка входа)
        static async Task Main(string[] args)
        {
            // Получаем токен доступа по ClientId и AuthorizationKey
            string Token = await GetToken(ClientId, AuthorizationKey);
            if (Token == null)
            {
                Console.WriteLine("Не удалось получить токен");
                return;
            }

            Console.WriteLine("Токен успешно получен!");
            Console.WriteLine("Теперь вы можете запросить генерацию изображений.");
            Console.WriteLine("Примеры запросов:");
            Console.WriteLine("- Нарисуй красную машину на фоне заката");
            Console.WriteLine("- Создай абстрактное изображение с геометрическими фигурами");
            Console.WriteLine("- Сгенерируй пейзаж горного озера");
            Console.WriteLine();

            while (true)
            {
                Console.Write("Введите запрос для генерации изображения: ");
                string message = Console.ReadLine();
                // Добавляем сообщение пользователя в историю диалога
                DialogHistory.Add(new Models.Request.Message
                {
                    role = "user",      // Роль отправителя - пользователь
                    content = message   // Текст сообщения
                });

                // Получаем ответ от GigaChat API
                ResponseMessage answer = await GetAnswer(Token, DialogHistory);

                // Извлекаем текст ответа от ассистента (ИИ)
                string assistantText = answer.choices[0].message.content;
                Console.WriteLine("Ответ: " + assistantText);

                // Добавляем ответ ассистента в историю диалога
                DialogHistory.Add(new Models.Request.Message
                {
                    role = "assistant", // Роль отправителя - ассистент (ИИ)
                    content = assistantText // Текст ответа ИИ
                });

                // Пытаемся извлечь ID изображения из ответа
                // (GigaChat может возвращать изображения в виде ссылок с fileId)
                string fileId = ExtractImageId(assistantText);

                if (!string.IsNullOrEmpty(fileId))
                {
                    // Загружаем и устанавливаем обои
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

        // <summary>
        // Метод для отправки запроса к GigaChat API и получения ответа
        // </summary>
        public static async Task<ResponseMessage> GetAnswer(string token, List<Models.Request.Message> messages)
        {
            ResponseMessage responseMessage = null;

            // URL для запроса к API чата
            string Url = "https://gigachat.devices.sberbank.ru/api/v1/chat/completions";

            using (HttpClientHandler Handler = new HttpClientHandler())
            {
                // Отключает проверку SSL-сертификата (для разработки)
                Handler.ServerCertificateCustomValidationCallback = (message, cert, chain, SslPolicyErrors) => true;

                using (HttpClient Client = new HttpClient(Handler))
                {
                    // Создаем POST-запрос
                    HttpRequestMessage Request = new HttpRequestMessage(HttpMethod.Post, Url);

                    // Добавляем заголовки запроса
                    Request.Headers.Add("Accept", "application/json");
                    Request.Headers.Add("Authorization", $"Bearer {token}");

                    // Формируем тело запроса с параметрами
                    Models.Request DataRequest = new Models.Request()
                    {
                        model = "GigaChat:2.0.28.2", // Версия модели
                        messages = messages,         // История сообщений
                        function_call = "auto",      // Автоматический вызов функций
                        temperature = 0.3,           // Уровень "творчества" (0.0-1.0)
                        max_tokens = 1500            // Максимальная длина ответа
                    };

                    // Сериализуем данные в JSON и отправляем запрос
                    string JsonContent = JsonConvert.SerializeObject(DataRequest);
                    Request.Content = new StringContent(JsonContent, Encoding.UTF8, "application/json");

                    HttpResponseMessage Responce = await Client.SendAsync(Request);

                    // Если запрос успешен, десериализуем ответ
                    if (Responce.IsSuccessStatusCode)
                    {
                        string ResponseContent = await Responce.Content.ReadAsStringAsync();
                        responseMessage = JsonConvert.DeserializeObject<ResponseMessage>(ResponseContent);
                    }
                }
            }
            return responseMessage;
        }

        // <summary>
        // Извлекает ID изображения из HTML-кода
        // Ищет атрибут src="..." в тексте ответа
        // </summary>
        public static string ExtractImageId(string content)
        {
            try
            {
                // Проверяем, что строка не пустая
                if (string.IsNullOrEmpty(content))
                    return null;

                // Ищем начало атрибута src="
                var start = content.IndexOf("src=\"");

                // Если не нашли тег src
                if (start == -1)
                    return null;

                // Сдвигаем на длину "src=\"" (5 символов)
                start += 5;

                // Проверяем, что start не выходит за границы строки
                if (start >= content.Length)
                    return null;

                // Ищем закрывающую кавычку
                var end = content.IndexOf("\"", start);

                // Если не нашли закрывающую кавычку
                if (end == -1)
                    return null;

                // Извлекаем и возвращаем ID изображения
                return content.Substring(start, end - start);
            }
            catch (Exception ex)
            {
                // Логируем ошибку (для отладки)
                Console.WriteLine($"Ошибка при извлечении ID изображения: {ex.Message}");
                return null;
            }
        }

        // <summary>
        // Сохраняет массив байтов (изображение) в файл на рабочем столе
        // </summary>
        public static string SaveImage(byte[] data)
        {
            // Формируем путь к файлу на рабочем столе
            string filePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "gigachat_wallpaper.jpg");

            // Записываем данные в файл
            File.WriteAllBytes(filePath, data);
            return filePath;
        }

        // <summary>
        // Скачивает изображение по его fileId из GigaChat API
        // </summary>
        public static async Task<byte[]> DownloadImage(string token, string fileId)
        {
            // Формируем URL для загрузки изображения
            string url = $"https://gigachat.devices.sberbank.ru/api/v1/files/{fileId}/content";

            using (HttpClientHandler handler = new HttpClientHandler())
            {
                // Отключает проверку SSL-сертификата
                handler.ServerCertificateCustomValidationCallback = (msg, cert, chain, errors) => true;

                using (HttpClient client = new HttpClient(handler))
                {
                    // Добавляем токен авторизации
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

                    // Отправляем GET-запрос
                    HttpResponseMessage response = await client.GetAsync(url);

                    // Проверяем успешность запроса
                    response.EnsureSuccessStatusCode();

                    // Читаем ответ как массив байтов
                    return await response.Content.ReadAsByteArrayAsync();
                }
            }
        }

        // <summary>
        // Получает токен доступа для работы с GigaChat API
        // </summary>
        public static async Task<string> GetToken(string rqUID, string bearer)
        {
            string ReturnToken = null;
            // URL для получения OAuth-токена
            string Url = "https://ngw.devices.sberbank.ru:9443/api/v2/oauth";

            using (HttpClientHandler Handler = new HttpClientHandler())
            {
                // Отключает проверку SSL-сертификата
                Handler.ServerCertificateCustomValidationCallback = (message, cert, chain, SslPolicyErrors) => true;

                using (HttpClient Client = new HttpClient(Handler))
                {
                    // Создаем POST-запрос для получения токена
                    HttpRequestMessage Request = new HttpRequestMessage(HttpMethod.Post, Url);

                    // Добавляем необходимые заголовки
                    Request.Headers.Add("Accept", "application/json");
                    Request.Headers.Add("RqUID", rqUID);
                    Request.Headers.Add("Authorization", $"Bearer {bearer}");

                    // Формируем данные для OAuth-авторизации
                    var Data = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("scope","GIGACHAT_API_PERS")
                };

                    Request.Content = new FormUrlEncodedContent(Data);
                    HttpResponseMessage Responce = await Client.SendAsync(Request);

                    // Если запрос успешен, извлекаем токен из ответа
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
