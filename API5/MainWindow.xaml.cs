
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
        // Константы для аутентификации в API GigaChat
        private const string CLIENT_ID = "019b46ca-19f5-7dc1-b8d0-c65a3a79add4";
        private const string AUTH_KEY = "MDE5YjQ2Y2EtMTlmNS03ZGMxLWI4ZDAtYzY1YTNhNzlhZGQ0Ojk2MTkyM2ViLTFmMjctNDA3My05MzhiLWI1MDYyODQ5YjcyYg==";

        // Токен доступа для API запросов
        private string token;

        /// <summary>
        /// Конструктор главного окна приложения
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            // Обработчики событий для чекбокса праздничной темы
            // При установке флажка активируется выпадающий список праздников
            chkHoliday.Checked += (s, e) => cmbHoliday.IsEnabled = true;
            // При снятии флажка деактивируется выпадающий список праздников
            chkHoliday.Unchecked += (s, e) => cmbHoliday.IsEnabled = false;
        }

        /// <summary>
        /// Обработчик нажатия кнопки генерации обоев
        /// Основной метод приложения, управляющий всем процессом:
        /// 1. Проверка ввода пользователя
        /// 2. Получение токена доступа
        /// 3. Генерация изображения через API
        /// 4. Сохранение и установка обоев
        /// </summary>
        private async void btnGenerate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Получаем и проверяем введенный пользователем промпт
                string prompt = txtPrompt.Text.Trim();
                if (string.IsNullOrEmpty(prompt))
                {
                    MessageBox.Show("Введите описание изображения", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Блокируем кнопку генерации на время выполнения операции
                // чтобы предотвратить повторные нажатия
                btnGenerate.IsEnabled = false;
                btnGenerate.Content = "Генерация...";

                // Формируем полный промпт с учетом выбранных пользователем параметров
                string fullPrompt = CreatePrompt(prompt);

                // Получаем токен доступа для работы с API GigaChat
                token = await GetAccessToken();
                if (string.IsNullOrEmpty(token))
                {
                    MessageBox.Show("Не удалось получить токен доступа", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    ResetButton();
                    return;
                }

                // Отправляем запрос на генерацию изображения через API GigaChat
                // Используем исправленные классы для десериализации ответа
                var result = await CreateImage(fullPrompt);
                if (result == null || result.choices == null || result.choices.Count == 0)
                {
                    MessageBox.Show("Ошибка при генерации изображения", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    ResetButton();
                    return;
                }

                // Извлекаем ID файла изображения из ответа API
                // Используем исправленный путь к данным изображения
                string fileId = GetImageId(result.choices[0].message.content);

                // ДЕБАГ ВЫВОД: для отладки и мониторинга работы приложения
                Console.WriteLine("Получен ответ от GigaChat:");
                Console.WriteLine(result.choices[0].message.content);
                Console.WriteLine($"Найден ID файла: {fileId}");

                // Проверяем, что ID файла был успешно извлечен
                if (string.IsNullOrEmpty(fileId))
                {
                    MessageBox.Show("В ответе не найдено изображение. Возможно, GigaChat не сгенерировал изображение.", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    ResetButton();
                    return;
                }

                // Загружаем данные изображения по полученному ID файла
                byte[] imageData = await GetImageData(fileId);
                if (imageData == null || imageData.Length == 0)
                {
                    MessageBox.Show("Не удалось скачать изображение", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    ResetButton();
                    return;
                }

                // Сохраняем изображение в файл на локальном диске
                string filePath = SaveImageFile(imageData, prompt);
                if (string.IsNullOrEmpty(filePath))
                {
                    MessageBox.Show("Не удалось сохранить изображение", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    ResetButton();
                    return;
                }

                // Устанавливаем сохраненное изображение в качестве обоев рабочего стола
                try
                {
                    SetDesktopWallpaper(filePath);
                    MessageBox.Show($"Обои успешно установлены!\n\nФайл: {System.IO.Path.GetFileName(filePath)}\nРазмер: {imageData.Length / 1024} КБ",
                        "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    // Если не удалось установить обои, но файл сохранен - показываем предупреждение
                    MessageBox.Show($"Ошибка при установке обоев: {ex.Message}\n\nФайл сохранен: {filePath}",
                        "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                // Восстанавливаем состояние кнопки генерации
                ResetButton();
            }
            catch (Exception ex)
            {
                // Обработка неожиданных исключений
                MessageBox.Show($"Произошла ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                ResetButton();
            }
        }

        /// <summary>
        /// Восстанавливает исходное состояние кнопки генерации
        /// Вызывается после завершения операции (успешного или с ошибкой)
        /// </summary>
        private void ResetButton()
        {
            btnGenerate.IsEnabled = true;
            btnGenerate.Content = "Создать обои";
        }

        /// <summary>
        /// Формирует полный промпт для генерации изображения
        /// Объединяет базовый запрос пользователя с выбранными параметрами:
        /// - Стиль изображения
        /// - Цветовая палитра
        /// - Соотношение сторон
        /// - Праздничная тема (если выбрана)
        /// </summary>
        private string CreatePrompt(string basePrompt)
        {
            var sb = new StringBuilder();

            // Базовые инструкции для AI
            sb.AppendLine("Создай изображение для обоев рабочего стола.");
            sb.AppendLine("Обязательно сгенерируй и верни изображение.");
            sb.AppendLine($"Тема: {basePrompt}");
            sb.AppendLine();

            // Заголовок раздела параметров
            sb.AppendLine("Параметры изображения:");

            // Извлекаем выбранный стиль из ComboBox или используем значение по умолчанию
            string style = (cmbStyle.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Реализм";
            sb.AppendLine($"- Стиль: {style}");

            // Извлекаем выбранную цветовую палитру из ComboBox или используем значение по умолчанию
            string palette = (cmbPalette.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Тёплые цвета";
            sb.AppendLine($"- Цветовая палитра: {palette}");

            // Извлекаем выбранное соотношение сторон из ComboBox или используем значение по умолчанию
            string aspect = (cmbAspect.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "16:9";
            sb.AppendLine($"- Соотношение сторон: {aspect}");

            // Добавляем праздничную тему, если пользователь ее выбрал
            if (chkHoliday.IsChecked == true)
            {
                string holiday = (cmbHoliday.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Новый год";
                sb.AppendLine($"- Праздничная тема: {holiday}");
                sb.AppendLine($"- Добавь праздничные элементы и атмосферу");
            }

            // Дополнительные требования к изображению
            sb.AppendLine();
            sb.AppendLine("Требования к изображению:");
            sb.AppendLine("- Высокое качество для обоев рабочего стола");
            sb.AppendLine("- Без текста и водяных знаков");
            sb.AppendLine("- Гармоничная композиция");
            sb.AppendLine("- Возвращай только изображение с ID файла");

            return sb.ToString();
        }

        /// <summary>
        /// Получает токен доступа для работы с API GigaChat
        /// Использует OAuth 2.0 аутентификацию с предоставленными CLIENT_ID и AUTH_KEY
        /// </summary>
        private async Task<string> GetAccessToken()
        {
            try
            {
                // Используем кастомный обработчик для обхода проверки SSL сертификатов
                using (var handler = new HttpClientHandler())
                {
                    // Отключаем проверку SSL сертификатов (только для тестирования!)
                    handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;

                    using (var client = new HttpClient(handler))
                    {
                        // Формируем запрос на получение токена
                        var request = new HttpRequestMessage(HttpMethod.Post, "https://ngw.devices.sberbank.ru:9443/api/v2/oauth");
                        request.Headers.Add("Accept", "application/json");
                        request.Headers.Add("RqUID", CLIENT_ID);
                        request.Headers.Add("Authorization", $"Bearer {AUTH_KEY}");

                        // Указываем scope для запроса токена
                        var content = new FormUrlEncodedContent(new[]
                        {
                        new KeyValuePair<string, string>("scope", "GIGACHAT_API_PERS")
                    });

                        request.Content = content;
                        var response = await client.SendAsync(request);

                        // Проверяем успешность запроса
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
                // Логируем ошибку получения токена
                Console.WriteLine($"Ошибка получения токена: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Отправляет запрос на генерацию изображения в API GigaChat
        /// Использует полученный ранее токен для авторизации
        /// </summary>
        private async Task<ResponseMessage> CreateImage(string prompt)
        {
            try
            {
                // Используем кастомный обработчик для обхода проверки SSL сертификатов
                using (var handler = new HttpClientHandler())
                {
                    // Отключаем проверку SSL сертификатов (только для тестирования!)
                    handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;

                    using (var client = new HttpClient(handler))
                    {
                        // Устанавливаем увеличенный таймаут для генерации изображения
                        client.Timeout = TimeSpan.FromSeconds(120);

                        // Формируем запрос к API чата GigaChat
                        var request = new HttpRequestMessage(HttpMethod.Post, "https://gigachat.devices.sberbank.ru/api/v1/chat/completions");
                        request.Headers.Add("Accept", "application/json");
                        request.Headers.Add("Authorization", $"Bearer {token}");

                        // Создаем сообщение с промптом пользователя
                        var messages = new List<Message>
                    {
                        new Message { role = "user", content = prompt }
                    };

                        // Формируем тело запроса с параметрами генерации
                        var dataRequest = new Request
                        {
                            model = "GigaChat",
                            messages = messages,
                            function_call = "auto",
                            temperature = 0.7,
                            max_tokens = 1500
                        };

                        // Сериализуем запрос в JSON и отправляем
                        string jsonContent = JsonConvert.SerializeObject(dataRequest);
                        request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                        var response = await client.SendAsync(request);

                        // Обрабатываем ответ от API
                        if (response.IsSuccessStatusCode)
                        {
                            var responseContent = await response.Content.ReadAsStringAsync();
                            Console.WriteLine($"Ответ API: {responseContent}");
                            return JsonConvert.DeserializeObject<ResponseMessage>(responseContent);
                        }
                        else
                        {
                            // Логируем ошибку от API
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

        /// <summary>
        /// Извлекает ID файла изображения из ответа API
        /// Использует регулярные выражения для поиска:
        /// 1. Сначала ищет src атрибуты в HTML-подобном контенте
        /// 2. Затем ищет UUID формат ID файла
        /// </summary>
        private string GetImageId(string content)
        {
            if (string.IsNullOrEmpty(content)) return null;

            try
            {
                // Паттерн для поиска src атрибутов (например, src="...")
                var srcPattern = @"src=[""']([^""']+)[""']";
                var srcMatch = Regex.Match(content, srcPattern, RegexOptions.IgnoreCase);

                // Если нашли src атрибут, извлекаем значение
                if (srcMatch.Success && srcMatch.Groups.Count > 1)
                {
                    string srcValue = srcMatch.Groups[1].Value;

                    // Пытаемся извлечь ID файла из URL
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

                // Альтернативный паттерн для поиска UUID формата
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

        /// <summary>
        /// Загружает данные изображения по ID файла
        /// Выполняет GET запрос к API для получения бинарных данных изображения
        /// </summary>
        private async Task<byte[]> GetImageData(string fileId)
        {
            try
            {
                using (var handler = new HttpClientHandler())
                {
                    // Отключаем проверку SSL сертификатов (только для тестирования!)
                    handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;

                    using (var client = new HttpClient(handler))
                    {
                        // Устанавливаем таймаут для загрузки изображения
                        client.Timeout = TimeSpan.FromSeconds(60);
                        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

                        // Формируем URL для загрузки файла
                        var response = await client.GetAsync($"https://gigachat.devices.sberbank.ru/api/v1/files/{fileId}/content");

                        if (response.IsSuccessStatusCode)
                        {
                            // Возвращаем бинарные данные изображения
                            return await response.Content.ReadAsByteArrayAsync();
                        }
                        else
                        {
                            // Логируем ошибку загрузки
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

        /// <summary>
        /// Сохраняет бинарные данные изображения в файл на диске
        /// Создает папку для сохранения, если она не существует
        /// Генерирует имя файла на основе промпта и временной метки
        /// </summary>
        private string SaveImageFile(byte[] imageData, string prompt)
        {
            try
            {
                // Создаем папку для сохранения сгенерированных обоев
                string folder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "СгенерированныеОбои");
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                // Создаем безопасное имя файла из промпта
                // Удаляем специальные символы, которые могут вызвать проблемы в имени файла
                string safeName = Regex.Replace(prompt, @"[^\w\s]", "");
                // Ограничиваем длину имени для удобства
                if (safeName.Length > 20)
                    safeName = safeName.Substring(0, 20);

                // Добавляем временную метку для уникальности имени файла
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"обои_{safeName}_{timestamp}.jpg";
                string filePath = System.IO.Path.Combine(folder, fileName);

                // Сохраняем данные в файл
                File.WriteAllBytes(filePath, imageData);
                return filePath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка SaveImageFile: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Устанавливает указанное изображение в качестве обоев рабочего стола
        /// Использует системный вызов Windows API
        /// </summary>
        private void SetDesktopWallpaper(string imagePath)
        {
            try
            {
                // Проверяем существование файла перед установкой
                if (!File.Exists(imagePath))
                    return;

                // Константы для системного вызова SystemParametersInfo
                const int SPI_SETDESKWALLPAPER = 20;
                const int SPIF_UPDATEINIFILE = 0x01;
                const int SPIF_SENDWININICHANGE = 0x02;

                // Вызываем Windows API для установки обоев
                SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, imagePath, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
            }
            catch (Exception)
            {
                // Пробрасываем исключение дальше для обработки в вызывающем методе
                throw;
            }
        }

        /// <summary>
        /// Импорт функции Windows API для установки обоев рабочего стола
        /// SystemParametersInfo - универсальная функция для работы с системными параметрами Windows
        /// </summary>
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);
    }
}