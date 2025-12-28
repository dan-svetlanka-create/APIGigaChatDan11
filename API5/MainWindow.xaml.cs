
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace API5
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private const string CLIENT_ID = "019b46ca-19f5-7dc1-b8d0-c65a3a79add4";
        private const string AUTH_KEY = "MDE5YjQ2Y2EtMTlmNS03ZGMxLWI4ZDAtYzY1YTNhNzlhZGQ0Ojk2MTkyM2ViLTFmMjctNDA3My05MzhiLWI1MDYyODQ5YjcyYg==";

        private string token;

        public MainWindow()
        {
            InitializeComponent();
            chkHoliday.Checked += (s, e) => cmbHoliday.IsEnabled = true;
            chkHoliday.Unchecked += (s, e) => cmbHoliday.IsEnabled = false;
        }

        private async void btnGenerate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string prompt = txtPrompt.Text.Trim();
                if (string.IsNullOrEmpty(prompt))
                {
                    MessageBox.Show("Введите описание изображения", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Блокируем кнопку
                btnGenerate.IsEnabled = false;
                btnGenerate.Content = "Генерация...";

                // Формируем промпт с параметрами
                string fullPrompt = CreatePrompt(prompt);

                // Получаем токен
                token = await GetAccessToken();
                if (string.IsNullOrEmpty(token))
                {
                    MessageBox.Show("Не удалось получить токен доступа", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    ResetButton();
                    return;
                }

                // ✅ ИСПРАВЛЕНО: Генерируем изображение с правильными классами
                var result = await CreateImage(fullPrompt);
                if (result == null || result.choices == null || result.choices.Count == 0)
                {
                    MessageBox.Show("Ошибка при генерации изображения", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    ResetButton();
                    return;
                }

                // ✅ ИСПРАВЛЕНО: Правильный путь к content
                string fileId = GetImageId(result.choices[0].message.content);

                // ДЕБАГ ВЫВОД
                Console.WriteLine("Получен ответ от GigaChat:");
                Console.WriteLine(result.choices[0].message.content);
                Console.WriteLine($"Найден ID файла: {fileId}");

                if (string.IsNullOrEmpty(fileId))
                {
                    MessageBox.Show("В ответе не найдено изображение. Возможно, GigaChat не сгенерировал изображение.", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    ResetButton();
                    return;
                }

                // Скачиваем изображение
                byte[] imageData = await GetImageData(fileId);
                if (imageData == null || imageData.Length == 0)
                {
                    MessageBox.Show("Не удалось скачать изображение", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    ResetButton();
                    return;
                }

                // Сохраняем изображение
                string filePath = SaveImageFile(imageData, prompt);
                if (string.IsNullOrEmpty(filePath))
                {
                    MessageBox.Show("Не удалось сохранить изображение", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    ResetButton();
                    return;
                }

                // Устанавливаем обои
                try
                {
                    SetDesktopWallpaper(filePath);
                    MessageBox.Show($"Обои успешно установлены!\n\nФайл: {System.IO.Path.GetFileName(filePath)}\nРазмер: {imageData.Length / 1024} КБ",
                        "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при установке обоев: {ex.Message}\n\nФайл сохранен: {filePath}",
                        "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                ResetButton();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Произошла ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                ResetButton();
            }
        }

        private void ResetButton()
        {
            btnGenerate.IsEnabled = true;
            btnGenerate.Content = "Создать обои";
        }

        private string CreatePrompt(string basePrompt)
        {
            var sb = new StringBuilder();

            sb.AppendLine("Создай изображение для обоев рабочего стола.");
            sb.AppendLine("Обязательно сгенерируй и верни изображение.");
            sb.AppendLine($"Тема: {basePrompt}");
            sb.AppendLine();

            sb.AppendLine("Параметры изображения:");

            // Стиль
            string style = (cmbStyle.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Реализм";
            sb.AppendLine($"- Стиль: {style}");

            // Цветовая палитра
            string palette = (cmbPalette.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Тёплые цвета";
            sb.AppendLine($"- Цветовая палитра: {palette}");

            // Соотношение сторон
            string aspect = (cmbAspect.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "16:9";
            sb.AppendLine($"- Соотношение сторон: {aspect}");

            // Праздничная тема
            if (chkHoliday.IsChecked == true)
            {
                string holiday = (cmbHoliday.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Новый год";
                sb.AppendLine($"- Праздничная тема: {holiday}");
                sb.AppendLine($"- Добавь праздничные элементы и атмосферу");
            }

            sb.AppendLine();
            sb.AppendLine("Требования к изображению:");
            sb.AppendLine("- Высокое качество для обоев рабочего стола");
            sb.AppendLine("- Без текста и водяных знаков");
            sb.AppendLine("- Гармоничная композиция");
            sb.AppendLine("- Возвращай только изображение с ID файла");

            return sb.ToString();
        }

        private async Task<string> GetAccessToken()
        {
            try
            {
                using (var handler = new HttpClientHandler())
                {
                    handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;

                    using (var client = new HttpClient(handler))
                    {
                        var request = new HttpRequestMessage(HttpMethod.Post, "https://ngw.devices.sberbank.ru:9443/api/v2/oauth");
                        request.Headers.Add("Accept", "application/json");
                        request.Headers.Add("RqUID", CLIENT_ID);
                        request.Headers.Add("Authorization", $"Bearer {AUTH_KEY}");

                        var content = new FormUrlEncodedContent(new[]
                        {
                            new KeyValuePair<string, string>("scope", "GIGACHAT_API_PERS")
                        });

                        request.Content = content;
                        var response = await client.SendAsync(request);

                        if (response.IsSuccessStatusCode)
                        {
                            var responseContent = await response.Content.ReadAsStringAsync();
                            var tokenResponse = JsonConvert.DeserializeObject<ResponseToken>(responseContent);
                            return tokenResponse?.access_token;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка получения токена: {ex.Message}");
            }
            return null;
        }

        private async Task<ResponseMessage> CreateImage(string prompt)
        {
            try
            {
                using (var handler = new HttpClientHandler())
                {
                    handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;

                    using (var client = new HttpClient(handler))
                    {
                        client.Timeout = TimeSpan.FromSeconds(120);

                        var request = new HttpRequestMessage(HttpMethod.Post, "https://gigachat.devices.sberbank.ru/api/v1/chat/completions");
                        request.Headers.Add("Accept", "application/json");
                        request.Headers.Add("Authorization", $"Bearer {token}");

                        var messages = new List<Message>
                        {
                            new Message { role = "user", content = prompt }
                        };

                        var dataRequest = new Request
                        {
                            model = "GigaChat",
                            messages = messages,
                            function_call = "auto",
                            temperature = 0.7,
                            max_tokens = 1500
                        };

                        string jsonContent = JsonConvert.SerializeObject(dataRequest);
                        request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                        var response = await client.SendAsync(request);

                        if (response.IsSuccessStatusCode)
                        {
                            var responseContent = await response.Content.ReadAsStringAsync();
                            Console.WriteLine($"Ответ API: {responseContent}");
                            return JsonConvert.DeserializeObject<ResponseMessage>(responseContent);
                        }
                        else
                        {
                            var errorContent = await response.Content.ReadAsStringAsync();
                            Console.WriteLine($"Ошибка API: {errorContent} (Status: {response.StatusCode})");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка CreateImage: {ex.Message}");
            }
            return null;
        }

        private string GetImageId(string content)
        {
            if (string.IsNullOrEmpty(content)) return null;

            try
            {
                var srcPattern = @"src=[""']([^""']+)[""']";
                var srcMatch = Regex.Match(content, srcPattern, RegexOptions.IgnoreCase);

                if (srcMatch.Success && srcMatch.Groups.Count > 1)
                {
                    string srcValue = srcMatch.Groups[1].Value;

                    if (srcValue.Contains("/files/"))
                    {
                        var parts = srcValue.Split(new[] { "/files/", "/content" }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2)
                        {
                            return parts[1].TrimEnd('/');
                        }
                    }
                    return srcValue;
                }


                var uuidPattern = @"[a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12}";
                var uuidMatch = Regex.Match(content, uuidPattern);
                if (uuidMatch.Success) return uuidMatch.Value;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка GetImageId: {ex.Message}");
            }

            return null;
        }

        private async Task<byte[]> GetImageData(string fileId)
        {
            try
            {
                using (var handler = new HttpClientHandler())
                {
                    handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;

                    using (var client = new HttpClient(handler))
                    {
                        client.Timeout = TimeSpan.FromSeconds(60);
                        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

                        var response = await client.GetAsync($"https://gigachat.devices.sberbank.ru/api/v1/files/{fileId}/content");

                        if (response.IsSuccessStatusCode)
                        {
                            return await response.Content.ReadAsByteArrayAsync();
                        }
                        else
                        {
                            var errorContent = await response.Content.ReadAsStringAsync();
                            Console.WriteLine($"Ошибка скачивания: {errorContent}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка GetImageData: {ex.Message}");
            }

            return null;
        }

        private string SaveImageFile(byte[] imageData, string prompt)
        {
            try
            {
                string folder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "СгенерированныеОбои");
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                string safeName = Regex.Replace(prompt, @"[^\w\s]", "");
                if (safeName.Length > 20)
                    safeName = safeName.Substring(0, 20);

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"обои_{safeName}_{timestamp}.jpg";
                string filePath = System.IO.Path.Combine(folder, fileName);

                File.WriteAllBytes(filePath, imageData);
                return filePath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка SaveImageFile: {ex.Message}");
                return null;
            }
        }

        private void SetDesktopWallpaper(string imagePath)
        {
            try
            {
                if (!File.Exists(imagePath))
                    return;

                const int SPI_SETDESKWALLPAPER = 20;
                const int SPIF_UPDATEINIFILE = 0x01;
                const int SPIF_SENDWININICHANGE = 0x02;

                SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, imagePath, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
            }
            catch (Exception)
            {
                throw;
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);


    }
}