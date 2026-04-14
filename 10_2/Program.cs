using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Text.Json;

namespace _10_2
{
    

    public class ApiResponse<T>
    {
        public T Data { get; set; }
        public ErrorInfo Error { get; set; }
    }

    public class ErrorInfo
    {
        public string Message { get; set; }
        public string Code { get; set; }
    }

    public class ValidationError
    {
        public string Field { get; set; }
        public string Message { get; set; }
    }

    public class ValidationErrorResponse
    {
        public string Error { get; set; }
        public List<ValidationError> Errors { get; set; }
    }

    public class CreateTaskRequest
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Priority { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime? DueDate { get; set; }
    }

    public class TaskItem
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public bool IsCompleted { get; set; }
        public string Priority { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? DueDate { get; set; }
    }

    public class AppConfig
    {
        public List<string> ListenUrls { get; set; }
        public string LogLevel { get; set; }
        public string LogFilePath { get; set; }
        public bool EnableFileLogging { get; set; }

        public AppConfig()
        {
            ListenUrls = new List<string> { "http://localhost:5000/" };
            LogLevel = "Information";
            LogFilePath = "logs/app.log";
            EnableFileLogging = true;
        }
    }

    // ==================== ЛОГГЕР ====================

    public static class AppLogger
    {
        private static AppConfig _config;
        private static readonly object _fileLock = new object();

        public static void Initialize(AppConfig config)
        {
            _config = config;
        }

        private static bool ShouldLog(string level)
        {
            if (_config == null) return true;

            var levels = new string[] { "Error", "Warning", "Information", "Debug" };
            var configIndex = Array.IndexOf(levels, _config.LogLevel ?? "Information");
            var messageIndex = Array.IndexOf(levels, level);

            return messageIndex <= configIndex;
        }

        public static void LogInfo(string message)
        {
            Write("INFO", message);
        }

        public static void LogWarning(string message)
        {
            Write("WARN", message);
        }

        public static void LogError(string message, Exception ex = null)
        {
            var fullMessage = message;
            if (ex != null)
            {
                fullMessage = message + ": " + ex.Message;
            }
            Write("ERROR", fullMessage);
        }

        private static void Write(string level, string message)
        {
            if (!ShouldLog(level)) return;

            var timestamp = DateTime.UtcNow.ToString("O");
            var line = string.Format("[{0}] [{1}] {2}", timestamp, level, message);

            Console.WriteLine(line);

            if (_config != null && _config.EnableFileLogging && !string.IsNullOrEmpty(_config.LogFilePath))
            {
                try
                {
                    lock (_fileLock)
                    {
                        var dir = Path.GetDirectoryName(_config.LogFilePath);
                        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }
                        File.AppendAllText(_config.LogFilePath, line + Environment.NewLine);
                    }
                }
                catch (Exception fileEx)
                {
                    Console.WriteLine("[ERROR] Failed to write to log file: " + fileEx.Message);
                }
            }
        }
    }


    class Program
    {
        static List<TaskItem> _tasks = new List<TaskItem>
        {
            new TaskItem {
                Id = 1,
                Title = "Сделать лабораторную",
                Description = "Нужно выполнить ЛР 11.2",
                IsCompleted = false,
                Priority = "high",
                CreatedAt = DateTime.Now.AddDays(-2),
                DueDate = DateTime.Now.AddDays(1)
            },
            new TaskItem {
                Id = 2,
                Title = "Проверить почту",
                Description = "Ответить на письма",
                IsCompleted = true,
                Priority = "medium",
                CreatedAt = DateTime.Now.AddDays(-1),
                DueDate = DateTime.Now
            },
            new TaskItem {
                Id = 3,
                Title = "Купить продукты",
                Description = "Молоко, хлеб, яйца",
                IsCompleted = false,
                Priority = "low",
                CreatedAt = DateTime.Now,
                DueDate = DateTime.Now.AddDays(2)
            }
        };

        static AppConfig _config;

        static void Main(string[] args)
        {
            LoadConfiguration();

            AppLogger.Initialize(_config);
            AppLogger.LogInfo("=== Приложение запускается ===");
            AppLogger.LogInfo("Уровень логов: " + _config.LogLevel);
            AppLogger.LogInfo("Логирование в файл: " + (_config.EnableFileLogging ? "включено" : "выключено"));

            HttpListener listener = new HttpListener();
            foreach (var url in _config.ListenUrls)
            {
                listener.Prefixes.Add(url);
                AppLogger.LogInfo("Прослушивается: " + url);
            }

            listener.Start();
            AppLogger.LogInfo("Сервер запущен и готов к обработке запросов");

            while (true)
            {
                try
                {
                    HttpListenerContext context = listener.GetContext();
                    HandleRequest(context);
                }
                catch (Exception ex)
                {
                    AppLogger.LogError("Критическая ошибка в главном цикле", ex);
                }
            }
        }

        static void LoadConfiguration()
        {
            _config = new AppConfig();

            var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

            if (File.Exists(configPath))
            {
                try
                {
                    var json = File.ReadAllText(configPath);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    var loadedConfig = JsonSerializer.Deserialize<AppConfig>(json, options);

                    if (loadedConfig != null)
                    {
                        if (loadedConfig.ListenUrls != null && loadedConfig.ListenUrls.Count > 0)
                            _config.ListenUrls = loadedConfig.ListenUrls;
                        if (!string.IsNullOrEmpty(loadedConfig.LogLevel))
                            _config.LogLevel = loadedConfig.LogLevel;
                        if (!string.IsNullOrEmpty(loadedConfig.LogFilePath))
                            _config.LogFilePath = loadedConfig.LogFilePath;
                        _config.EnableFileLogging = loadedConfig.EnableFileLogging;
                    }

                    AppLogger.LogInfo("Конфигурация загружена из: " + configPath);
                }
                catch (Exception ex)
                {
                    AppLogger.LogWarning("Не удалось загрузить конфигурацию, используются значения по умолчанию: " + ex.Message);
                }
            }
            else
            {
                AppLogger.LogWarning("Файл конфигурации не найден: " + configPath);
            }

           
            var envPort = Environment.GetEnvironmentVariable("API_PORT");
            if (!string.IsNullOrEmpty(envPort) && _config.ListenUrls.Count > 0)
            {
                try
                {
                    var uri = new Uri(_config.ListenUrls[0]);
                    var newUrl = string.Format("http://localhost:{0}/", envPort);
                    _config.ListenUrls[0] = newUrl;
                    AppLogger.LogInfo("Порт переопределён из переменной окружения: " + envPort);
                }
                catch (Exception ex)
                {
                    AppLogger.LogWarning("Не удалось применить API_PORT: " + ex.Message);
                }
            }

            var envLogLevel = Environment.GetEnvironmentVariable("LOG_LEVEL");
            if (!string.IsNullOrEmpty(envLogLevel))
            {
                _config.LogLevel = envLogLevel;
            }
        }

        static void HandleRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            var path = request.Url.AbsolutePath.ToLower();
            var method = request.HttpMethod;

            AppLogger.LogInfo(string.Format("Входящий запрос: {0} {1}", method, path));

            try
            {
             
                if (path == "/api/tasks" && method == "GET")
                {
                    HandleGetTasks(request, response);
                }
                else if (path == "/api/tasks/search" && method == "GET")
                {
                    HandleSearchTasks(request, response);
                }
                else if (path == "/api/tasks/stats" && method == "GET")
                {
                    HandleGetStats(request, response);
                }
                else if (path.StartsWith("/api/tasks/") && method == "GET")
                {
                    var idStr = path.Replace("/api/tasks/", "");
                    if (int.TryParse(idStr, out int id))
                    {
                        HandleGetTaskById(request, response, id);
                    }
                    else
                    {
                        AppLogger.LogWarning("Некорректный ID в запросе: " + idStr);
                        WriteError(response, 400, "Некорректный формат ID", "INVALID_ID");
                    }
                }
                else if (path == "/api/tasks" && method == "POST")
                {
                    HandleCreateTask(request, response);
                }
                else if (path.StartsWith("/api/tasks/") && method == "PUT")
                {
                    var idStr = path.Replace("/api/tasks/", "");
                    if (int.TryParse(idStr, out int id))
                    {
                        HandleUpdateTask(request, response, id);
                    }
                    else
                    {
                        AppLogger.LogWarning("Некорректный ID в запросе: " + idStr);
                        WriteError(response, 400, "Некорректный формат ID", "INVALID_ID");
                    }
                }
                else if (path.StartsWith("/api/tasks/") && method == "DELETE")
                {
                    var idStr = path.Replace("/api/tasks/", "");
                    if (int.TryParse(idStr, out int id))
                    {
                        HandleDeleteTask(request, response, id);
                    }
                    else
                    {
                        AppLogger.LogWarning("Некорректный ID в запросе: " + idStr);
                        WriteError(response, 400, "Некорректный формат ID", "INVALID_ID");
                    }
                }
                else
                {
                    AppLogger.LogWarning(string.Format("Endpoint не найден: {0} {1}", method, path));
                    WriteError(response, 404, string.Format("Endpoint {0} не найден", path), "NOT_FOUND");
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError(string.Format("Ошибка обработки запроса {0} {1}", method, path), ex);
                WriteError(response, 500, "Внутренняя ошибка сервера", "INTERNAL_ERROR");
            }
        }

        

        static void WriteJson(HttpListenerResponse response, string json, int statusCode)
        {
            response.StatusCode = statusCode;
            response.ContentType = "application/json; charset=utf-8";
            response.ContentEncoding = Encoding.UTF8;

            byte[] buffer = Encoding.UTF8.GetBytes(json);
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.Close();
        }

        static void WriteSuccess<T>(HttpListenerResponse response, T data, int statusCode)
        {
            var apiResponse = new ApiResponse<T>
            {
                Data = data,
                Error = null
            };
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(apiResponse, options);
            WriteJson(response, json, statusCode);
        }

        static void WriteError(HttpListenerResponse response, int statusCode, string message, string code)
        {
            var apiResponse = new ApiResponse<object>
            {
                Data = null,
                Error = new ErrorInfo
                {
                    Message = message,
                    Code = code ?? statusCode.ToString()
                }
            };
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(apiResponse, options);
            WriteJson(response, json, statusCode);
        }

        static void WriteValidationErrors(HttpListenerResponse response, List<ValidationError> errors)
        {
            var errorResponse = new ValidationErrorResponse
            {
                Error = "Ошибка валидации",
                Errors = errors
            };
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(errorResponse, options);
            WriteJson(response, json, 400);
        }

        static void WriteValidationError(HttpListenerResponse response, string message)
        {
            var errors = new List<ValidationError>();
            errors.Add(new ValidationError { Field = "", Message = message });

            var errorResponse = new ValidationErrorResponse
            {
                Error = "Ошибка валидации",
                Errors = errors
            };
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(errorResponse, options);
            WriteJson(response, json, 400);
        }

        static Dictionary<string, string> ParseQueryString(string query)
        {
            var result = new Dictionary<string, string>();

            if (string.IsNullOrEmpty(query) || query == "?")
                return result;

            string queryWithoutQuestion = query.StartsWith("?") ? query.Substring(1) : query;

            if (string.IsNullOrEmpty(queryWithoutQuestion))
                return result;

            string[] pairs = queryWithoutQuestion.Split(new char[] { '&' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var pair in pairs)
            {
                string[] keyValue = pair.Split(new char[] { '=' }, 2);

                string key = Uri.UnescapeDataString(keyValue[0]).ToLower();
                string value = keyValue.Length > 1 ? Uri.UnescapeDataString(keyValue[1]) : "";

                result[key] = value;
            }

            return result;
        }


        static void HandleGetTasks(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                var queryParams = ParseQueryString(request.Url.Query);
                AppLogger.LogInfo(string.Format("Получение списка задач, фильтры: {0}", string.Join(", ", queryParams.Keys)));

                var filteredTasks = _tasks.AsEnumerable();

                if (queryParams.ContainsKey("iscompleted"))
                {
                    if (bool.TryParse(queryParams["iscompleted"], out bool isCompleted))
                    {
                        filteredTasks = filteredTasks.Where(t => t.IsCompleted == isCompleted);
                    }
                    else
                    {
                        WriteError(response, 400, "Некорректное значение параметра isCompleted", "INVALID_PARAMETER");
                        return;
                    }
                }

                if (queryParams.ContainsKey("priority"))
                {
                    string priority = queryParams["priority"].ToLower();
                    if (new string[] { "low", "medium", "high" }.Contains(priority))
                    {
                        filteredTasks = filteredTasks.Where(t => t.Priority.ToLower() == priority);
                    }
                    else
                    {
                        WriteError(response, 400, "Приоритет должен быть low, medium или high", "INVALID_PRIORITY");
                        return;
                    }
                }

                if (queryParams.ContainsKey("orderby"))
                {
                    string orderBy = queryParams["orderby"].ToLower();
                    string direction = queryParams.ContainsKey("direction") ?
                        queryParams["direction"].ToLower() : "asc";

                    switch (orderBy)
                    {
                        case "createdat":
                            filteredTasks = direction == "desc"
                                ? filteredTasks.OrderByDescending(t => t.CreatedAt)
                                : filteredTasks.OrderBy(t => t.CreatedAt);
                            break;
                        case "title":
                            filteredTasks = direction == "desc"
                                ? filteredTasks.OrderByDescending(t => t.Title)
                                : filteredTasks.OrderBy(t => t.Title);
                            break;
                        case "priority":
                            var priorityOrder = new string[] { "high", "medium", "low" };
                            filteredTasks = direction == "desc"
                                ? filteredTasks.OrderByDescending(t => Array.IndexOf(priorityOrder, t.Priority.ToLower()))
                                : filteredTasks.OrderBy(t => Array.IndexOf(priorityOrder, t.Priority.ToLower()));
                            break;
                        default:
                            WriteError(response, 400, string.Format("Неподдерживаемая сортировка: {0}", orderBy), "INVALID_ORDERBY");
                            return;
                    }
                }

                int page = 1;
                int pageSize = 10;

                if (queryParams.ContainsKey("page"))
                {
                    if (!int.TryParse(queryParams["page"], out page) || page < 1)
                    {
                        WriteError(response, 400, "Параметр page должен быть положительным числом", "INVALID_PAGE");
                        return;
                    }
                }

                if (queryParams.ContainsKey("pagesize"))
                {
                    if (!int.TryParse(queryParams["pagesize"], out pageSize) || pageSize < 1 || pageSize > 100)
                    {
                        WriteError(response, 400, "pageSize должен быть от 1 до 100", "INVALID_PAGESIZE");
                        return;
                    }
                }

                var totalItems = filteredTasks.Count();
                var pagedTasks = filteredTasks
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var pagination = new
                {
                    currentPage = page,
                    pageSize = pageSize,
                    totalItems = totalItems,
                    totalPages = (int)Math.Ceiling(totalItems / (double)pageSize)
                };

                var result = new
                {
                    items = pagedTasks,
                    pagination = pagination
                };

                AppLogger.LogInfo(string.Format("Возвращено {0} из {1} задач", pagedTasks.Count, totalItems));
                WriteSuccess(response, result, 200);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Ошибка в HandleGetTasks", ex);
                WriteError(response, 500, "Внутренняя ошибка сервера", "INTERNAL_ERROR");
            }
        }

        static void HandleGetTaskById(HttpListenerRequest request, HttpListenerResponse response, int id)
        {
            var task = _tasks.FirstOrDefault(t => t.Id == id);
            if (task == null)
            {
                AppLogger.LogWarning(string.Format("Задача с id {0} не найдена", id));
                WriteError(response, 404, string.Format("Задача с id {0} не найдена", id), "TASK_NOT_FOUND");
                return;
            }

            WriteSuccess(response, task, 200);
        }

        static void HandleSearchTasks(HttpListenerRequest request, HttpListenerResponse response)
        {
            var queryParams = ParseQueryString(request.Url.Query);

            if (!queryParams.ContainsKey("query"))
            {
                WriteError(response, 400, "Параметр query обязателен", "QUERY_REQUIRED");
                return;
            }

            string searchQuery = queryParams["query"].ToLower();
            AppLogger.LogInfo(string.Format("Поиск задач по запросу: {0}", searchQuery));

            var results = _tasks.Where(t =>
                t.Title.ToLower().Contains(searchQuery) ||
                (t.Description != null && t.Description.ToLower().Contains(searchQuery))
            ).ToList();

            AppLogger.LogInfo(string.Format("Найдено {0} результатов", results.Count));
            WriteSuccess(response, results, 200);
        }

        static void HandleGetStats(HttpListenerRequest request, HttpListenerResponse response)
        {
            var stats = new
            {
                total = _tasks.Count,
                completed = _tasks.Count(t => t.IsCompleted),
                notCompleted = _tasks.Count(t => !t.IsCompleted),
                byPriority = new
                {
                    high = _tasks.Count(t => t.Priority.ToLower() == "high"),
                    medium = _tasks.Count(t => t.Priority.ToLower() == "medium"),
                    low = _tasks.Count(t => t.Priority.ToLower() == "low")
                }
            };

            WriteSuccess(response, stats, 200);
        }

        static void HandleCreateTask(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                using (var reader = new StreamReader(request.InputStream, Encoding.UTF8))
                {
                    string body = reader.ReadToEnd();

                    if (string.IsNullOrWhiteSpace(body))
                    {
                        AppLogger.LogWarning("Пустое тело запроса при создании задачи");
                        WriteValidationError(response, "Тело запроса не может быть пустым");
                        return;
                    }

                    CreateTaskRequest requestData;
                    try
                    {
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };
                        requestData = JsonSerializer.Deserialize<CreateTaskRequest>(body, options);
                    }
                    catch (JsonException ex)
                    {
                        AppLogger.LogWarning("Ошибка парсинга JSON при создании задачи: " + ex.Message);
                        WriteValidationError(response, "Некорректный формат JSON");
                        return;
                    }

                    if (requestData == null)
                    {
                        WriteValidationError(response, "Некорректный формат данных");
                        return;
                    }

                    var errors = new List<ValidationError>();

                    if (string.IsNullOrWhiteSpace(requestData.Title))
                    {
                        errors.Add(new ValidationError { Field = "Title", Message = "Название обязательно" });
                    }
                    else if (requestData.Title.Length > 200)
                    {
                        errors.Add(new ValidationError { Field = "Title", Message = "Название не более 200 символов" });
                    }

                    if (!string.IsNullOrEmpty(requestData.Description) && requestData.Description.Length > 1000)
                    {
                        errors.Add(new ValidationError { Field = "Description", Message = "Описание не более 1000 символов" });
                    }

                    if (!string.IsNullOrEmpty(requestData.Priority))
                    {
                        var validPriorities = new string[] { "low", "medium", "high" };
                        if (!validPriorities.Contains(requestData.Priority.ToLower()))
                        {
                            errors.Add(new ValidationError { Field = "Priority", Message = "Приоритет должен быть low, medium или high" });
                        }
                    }

                    if (requestData.DueDate.HasValue && requestData.DueDate.Value.Date < DateTime.Now.Date)
                    {
                        errors.Add(new ValidationError { Field = "DueDate", Message = "Срок не может быть в прошлом" });
                    }

                    if (errors.Count > 0)
                    {
                        AppLogger.LogWarning(string.Format("Ошибка валидации: {0} ошибок", errors.Count));
                        WriteValidationErrors(response, errors);
                        return;
                    }

                    var newTask = new TaskItem
                    {
                        Id = _tasks.Count > 0 ? _tasks.Max(t => t.Id) + 1 : 1,
                        Title = requestData.Title.Trim(),
                        Description = requestData.Description != null ? requestData.Description.Trim() : null,
                        Priority = !string.IsNullOrEmpty(requestData.Priority) ? requestData.Priority.ToLower() : "medium",
                        IsCompleted = requestData.IsCompleted,
                        CreatedAt = DateTime.Now,
                        DueDate = requestData.DueDate
                    };

                    _tasks.Add(newTask);
                    response.AddHeader("Location", string.Format("/api/tasks/{0}", newTask.Id));

                    AppLogger.LogInfo(string.Format("Создана задача #{0}: {1}", newTask.Id, newTask.Title));
                    WriteSuccess(response, newTask, 201);
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Ошибка в HandleCreateTask", ex);
                WriteError(response, 500, "Внутренняя ошибка сервера", "INTERNAL_ERROR");
            }
        }

        static void HandleUpdateTask(HttpListenerRequest request, HttpListenerResponse response, int id)
        {
            try
            {
                var existingTask = _tasks.FirstOrDefault(t => t.Id == id);
                if (existingTask == null)
                {
                    AppLogger.LogWarning(string.Format("Задача с id {0} не найдена для обновления", id));
                    WriteError(response, 404, string.Format("Задача с id {0} не найдена", id), "TASK_NOT_FOUND");
                    return;
                }

                using (var reader = new StreamReader(request.InputStream, Encoding.UTF8))
                {
                    string body = reader.ReadToEnd();

                    if (string.IsNullOrWhiteSpace(body))
                    {
                        WriteValidationError(response, "Тело запроса не может быть пустым");
                        return;
                    }

                    CreateTaskRequest requestData;
                    try
                    {
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };
                        requestData = JsonSerializer.Deserialize<CreateTaskRequest>(body, options);
                    }
                    catch (JsonException ex)
                    {
                        AppLogger.LogWarning("Ошибка парсинга JSON при обновлении задачи: " + ex.Message);
                        WriteValidationError(response, "Некорректный формат JSON");
                        return;
                    }

                    if (requestData == null)
                    {
                        WriteValidationError(response, "Некорректный формат данных");
                        return;
                    }

                    var errors = new List<ValidationError>();

                    if (!string.IsNullOrEmpty(requestData.Title) && requestData.Title.Length > 200)
                    {
                        errors.Add(new ValidationError { Field = "Title", Message = "Название не более 200 символов" });
                    }

                    if (!string.IsNullOrEmpty(requestData.Description) && requestData.Description.Length > 1000)
                    {
                        errors.Add(new ValidationError { Field = "Description", Message = "Описание не более 1000 символов" });
                    }

                    if (!string.IsNullOrEmpty(requestData.Priority))
                    {
                        var validPriorities = new string[] { "low", "medium", "high" };
                        if (!validPriorities.Contains(requestData.Priority.ToLower()))
                        {
                            errors.Add(new ValidationError { Field = "Priority", Message = "Приоритет должен быть low, medium или high" });
                        }
                    }

                    if (requestData.DueDate.HasValue && requestData.DueDate.Value.Date < DateTime.Now.Date)
                    {
                        errors.Add(new ValidationError { Field = "DueDate", Message = "Срок не может быть в прошлом" });
                    }

                    if (errors.Count > 0)
                    {
                        WriteValidationErrors(response, errors);
                        return;
                    }

                    if (!string.IsNullOrEmpty(requestData.Title))
                        existingTask.Title = requestData.Title.Trim();

                    if (requestData.Description != null)
                        existingTask.Description = requestData.Description.Trim();

                    if (!string.IsNullOrEmpty(requestData.Priority))
                        existingTask.Priority = requestData.Priority.ToLower();

                    existingTask.IsCompleted = requestData.IsCompleted;

                    if (requestData.DueDate != null || body.Contains("\"dueDate\":null"))
                        existingTask.DueDate = requestData.DueDate;

                    AppLogger.LogInfo(string.Format("Обновлена задача #{0}", id));
                    WriteSuccess(response, existingTask, 200);
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Ошибка в HandleUpdateTask", ex);
                WriteError(response, 500, "Внутренняя ошибка сервера", "INTERNAL_ERROR");
            }
        }

        static void HandleDeleteTask(HttpListenerRequest request, HttpListenerResponse response, int id)
        {
            var task = _tasks.FirstOrDefault(t => t.Id == id);
            if (task == null)
            {
                AppLogger.LogWarning(string.Format("Задача с id {0} не найдена для удаления", id));
                WriteError(response, 404, string.Format("Задача с id {0} не найдена", id), "TASK_NOT_FOUND");
                return;
            }

            _tasks.Remove(task);
            AppLogger.LogInfo(string.Format("Удалена задача #{0}", id));
            WriteSuccess(response, new { message = "Задача успешно удалена" }, 200);
        }
    }
}