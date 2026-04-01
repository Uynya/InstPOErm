using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace _10_2
{
    // Модели для API ответов
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

    // Модели для задач
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
        public int UserId { get; set; }
    }

    // Модели для аутентификации
    public class User
    {
        public int Id { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public string Name { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class RegisterRequest
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public string Name { get; set; }
    }

    public class LoginRequest
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public class LoginResponse
    {
        public string Token { get; set; }
        public string Email { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string Name { get; set; }
    }

    class Program
    {
        // JWT настройки
        private const string JwtSecretKey = "your-super-secret-key-with-at-least-32-characters-long!";
        private const string JwtIssuer = "SimpleApiServer";
        private const string JwtAudience = "SimpleApiClient";
        private const int JwtExpirationMinutes = 60;

        // Хранилища данных
        static List<TaskItem> _tasks = new List<TaskItem>
        {
            new TaskItem {
                Id = 1,
                Title = "Сделать лабораторную",
                Description = "Нужно выполнить ЛР 11.2",
                IsCompleted = false,
                Priority = "high",
                CreatedAt = DateTime.Now.AddDays(-2),
                DueDate = DateTime.Now.AddDays(1),
                UserId = 1
            },
            new TaskItem {
                Id = 2,
                Title = "Проверить почту",
                Description = "Ответить на письма",
                IsCompleted = true,
                Priority = "medium",
                CreatedAt = DateTime.Now.AddDays(-1),
                DueDate = DateTime.Now,
                UserId = 1
            },
            new TaskItem {
                Id = 3,
                Title = "Купить продукты",
                Description = "Молоко, хлеб, яйца",
                IsCompleted = false,
                Priority = "low",
                CreatedAt = DateTime.Now,
                DueDate = DateTime.Now.AddDays(2),
                UserId = 1
            }
        };

        static List<User> _users = new List<User>
        {
            new User
            {
                Id = 1,
                Email = "test@example.com",
                PasswordHash = HashPassword("test123"),
                Name = "Test User",
                CreatedAt = DateTime.Now.AddDays(-5)
            }
        };

        static void Main(string[] args)
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:5000/");
            listener.Start();

            Console.WriteLine("Сервер запущен: http://localhost:5000/");
            Console.WriteLine("JWT Issuer: " + JwtIssuer);
            Console.WriteLine("JWT Audience: " + JwtAudience);
            Console.WriteLine();

            while (true)
            {
                try
                {
                    HttpListenerContext context = listener.GetContext();
                    HttpListenerRequest request = context.Request;
                    HttpListenerResponse response = context.Response;

                    string path = request.Url.AbsolutePath.ToLower();
                    string method = request.HttpMethod;

                    Console.WriteLine($"[{DateTime.Now}] {method} {path}");

                    try
                    {
                        // Маршруты аутентификации (публичные)
                        if (path == "/api/auth/register" && method == "POST")
                        {
                            HandleRegister(request, response);
                        }
                        else if (path == "/api/auth/login" && method == "POST")
                        {
                            HandleLogin(request, response);
                        }
                        // Маршруты для задач (защищенные)
                        else if (path == "/api/tasks" && method == "GET")
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
                            string idStr = path.Replace("/api/tasks/", "");
                            if (int.TryParse(idStr, out int id))
                            {
                                HandleGetTaskById(request, response, id);
                            }
                            else
                            {
                                WriteError(response, 400, "Некорректный формат ID", "INVALID_ID");
                            }
                        }
                        else if (path == "/api/tasks" && method == "POST")
                        {
                            HandleCreateTask(request, response);
                        }
                        else if (path.StartsWith("/api/tasks/") && method == "PUT")
                        {
                            string idStr = path.Replace("/api/tasks/", "");
                            if (int.TryParse(idStr, out int id))
                            {
                                HandleUpdateTask(request, response, id);
                            }
                            else
                            {
                                WriteError(response, 400, "Некорректный формат ID", "INVALID_ID");
                            }
                        }
                        else if (path.StartsWith("/api/tasks/") && method == "DELETE")
                        {
                            string idStr = path.Replace("/api/tasks/", "");
                            if (int.TryParse(idStr, out int id))
                            {
                                HandleDeleteTask(request, response, id);
                            }
                            else
                            {
                                WriteError(response, 400, "Некорректный формат ID", "INVALID_ID");
                            }
                        }
                        else
                        {
                            WriteError(response, 404, $"Endpoint {path} не найден", "NOT_FOUND");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка: {ex.Message}");
                        WriteError(response, 500, "Внутренняя ошибка сервера", "INTERNAL_ERROR");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Критическая ошибка: {ex.Message}");
                }
            }
        }

        // Хэширование пароля
        static string HashPassword(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        // Проверка пароля
        static bool VerifyPassword(string password, string hash)
        {
            return HashPassword(password) == hash;
        }

        // Генерация JWT токена
        static string GenerateJwtToken(User user)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecretKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.Name ?? user.Email),
                new Claim("UserId", user.Id.ToString())
            };

            var token = new JwtSecurityToken(
                issuer: JwtIssuer,
                audience: JwtAudience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(JwtExpirationMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // Валидация JWT токена
        static bool ValidateJwtToken(string token, out ClaimsPrincipal principal)
        {
            principal = null;
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecretKey)),
                    ValidateIssuer = true,
                    ValidIssuer = JwtIssuer,
                    ValidateAudience = true,
                    ValidAudience = JwtAudience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                principal = tokenHandler.ValidateToken(token, validationParameters, out _);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Извлечение пользователя из токена
        static bool TryGetUserFromToken(HttpListenerRequest request, out User user)
        {
            user = null;
            string authHeader = request.Headers["Authorization"];

            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return false;
            }

            string token = authHeader.Substring("Bearer ".Length);

            if (!ValidateJwtToken(token, out ClaimsPrincipal principal))
            {
                return false;
            }

            string userIdClaim = principal.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return false;
            }

            user = _users.FirstOrDefault(u => u.Id == userId);
            return user != null;
        }

        // Проверка авторизации для защищенных маршрутов
        static bool CheckAuthorization(HttpListenerRequest request, HttpListenerResponse response)
        {
            if (!TryGetUserFromToken(request, out User user))
            {
                WriteUnauthorized(response);
                return false;
            }
            return true;
        }

        // Возврат 401 Unauthorized
        static void WriteUnauthorized(HttpListenerResponse response)
        {
            var errorResponse = new
            {
                error = "Unauthorized",
                message = "Требуется авторизация"
            };
            string json = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions { WriteIndented = true });
            WriteJson(response, json, 401);
        }

        static void WriteJson(HttpListenerResponse response, string json, int statusCode = 200)
        {
            response.StatusCode = statusCode;
            response.ContentType = "application/json; charset=utf-8";
            response.ContentEncoding = Encoding.UTF8;

            byte[] buffer = Encoding.UTF8.GetBytes(json);
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.Close();
        }

        static void WriteSuccess<T>(HttpListenerResponse response, T data, int statusCode = 200)
        {
            var apiResponse = new ApiResponse<T>
            {
                Data = data,
                Error = null
            };
            string json = JsonSerializer.Serialize(apiResponse, new JsonSerializerOptions { WriteIndented = true });
            WriteJson(response, json, statusCode);
        }

        static void WriteError(HttpListenerResponse response, int statusCode, string message, string code = null)
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
            string json = JsonSerializer.Serialize(apiResponse, new JsonSerializerOptions { WriteIndented = true });
            WriteJson(response, json, statusCode);
        }

        static void WriteValidationErrors(HttpListenerResponse response, List<ValidationError> errors)
        {
            var errorResponse = new ValidationErrorResponse
            {
                Error = "Ошибка валидации",
                Errors = errors
            };
            string json = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions { WriteIndented = true });
            WriteJson(response, json, 400);
        }

        static void WriteValidationError(HttpListenerResponse response, string message)
        {
            var errorResponse = new ValidationErrorResponse
            {
                Error = "Ошибка валидации",
                Errors = new List<ValidationError> { new ValidationError { Field = "", Message = message } }
            };
            string json = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions { WriteIndented = true });
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

        // Обработчики для аутентификации
        static void HandleRegister(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                using (var reader = new StreamReader(request.InputStream, Encoding.UTF8))
                {
                    string body = reader.ReadToEnd();

                    if (string.IsNullOrWhiteSpace(body))
                    {
                        WriteValidationError(response, "Тело запроса не может быть пустым");
                        return;
                    }

                    RegisterRequest registerData;
                    try
                    {
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };
                        registerData = JsonSerializer.Deserialize<RegisterRequest>(body, options);
                    }
                    catch (JsonException)
                    {
                        WriteValidationError(response, "Некорректный формат JSON");
                        return;
                    }

                    if (registerData == null || string.IsNullOrEmpty(registerData.Email) || string.IsNullOrEmpty(registerData.Password))
                    {
                        WriteValidationError(response, "Email и пароль обязательны");
                        return;
                    }

                    // Проверка уникальности email
                    if (_users.Any(u => u.Email.Equals(registerData.Email, StringComparison.OrdinalIgnoreCase)))
                    {
                        WriteError(response, 400, "Пользователь с таким email уже существует", "EMAIL_EXISTS");
                        return;
                    }

                    // Создание нового пользователя
                    var newUser = new User
                    {
                        Id = _users.Count > 0 ? _users.Max(u => u.Id) + 1 : 1,
                        Email = registerData.Email.ToLower(),
                        PasswordHash = HashPassword(registerData.Password),
                        Name = registerData.Name,
                        CreatedAt = DateTime.Now
                    };

                    _users.Add(newUser);

                    // Генерация токена
                    string token = GenerateJwtToken(newUser);

                    var responseData = new LoginResponse
                    {
                        Token = token,
                        Email = newUser.Email,
                        ExpiresAt = DateTime.UtcNow.AddMinutes(JwtExpirationMinutes),
                        Name = newUser.Name
                    };

                    WriteSuccess(response, responseData, 201);
                    Console.WriteLine($"Зарегистрирован новый пользователь: {newUser.Email}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в HandleRegister: {ex.Message}");
                WriteError(response, 500, "Внутренняя ошибка сервера", "INTERNAL_ERROR");
            }
        }

        static void HandleLogin(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                using (var reader = new StreamReader(request.InputStream, Encoding.UTF8))
                {
                    string body = reader.ReadToEnd();

                    if (string.IsNullOrWhiteSpace(body))
                    {
                        WriteValidationError(response, "Тело запроса не может быть пустым");
                        return;
                    }

                    LoginRequest loginData;
                    try
                    {
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };
                        loginData = JsonSerializer.Deserialize<LoginRequest>(body, options);
                    }
                    catch (JsonException)
                    {
                        WriteValidationError(response, "Некорректный формат JSON");
                        return;
                    }

                    if (loginData == null || string.IsNullOrEmpty(loginData.Email) || string.IsNullOrEmpty(loginData.Password))
                    {
                        WriteValidationError(response, "Email и пароль обязательны");
                        return;
                    }

                    // Поиск пользователя
                    var user = _users.FirstOrDefault(u => u.Email.Equals(loginData.Email, StringComparison.OrdinalIgnoreCase));

                    if (user == null || !VerifyPassword(loginData.Password, user.PasswordHash))
                    {
                        var errorResponse = new
                        {
                            error = "Unauthorized",
                            message = "Неверный email или пароль"
                        };
                        string json = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions { WriteIndented = true });
                        WriteJson(response, json, 401);
                        return;
                    }

                    // Генерация токена
                    string token = GenerateJwtToken(user);

                    var responseData = new LoginResponse
                    {
                        Token = token,
                        Email = user.Email,
                        ExpiresAt = DateTime.UtcNow.AddMinutes(JwtExpirationMinutes),
                        Name = user.Name
                    };

                    WriteSuccess(response, responseData);
                    Console.WriteLine($"Успешный вход: {user.Email}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в HandleLogin: {ex.Message}");
                WriteError(response, 500, "Внутренняя ошибка сервера", "INTERNAL_ERROR");
            }
        }

        // Обработчики для задач (защищенные)
        static void HandleGetTasks(HttpListenerRequest request, HttpListenerResponse response)
        {
            if (!CheckAuthorization(request, response))
                return;

            if (!TryGetUserFromToken(request, out User user))
                return;

            try
            {
                var queryParams = ParseQueryString(request.Url.Query);
                var filteredTasks = _tasks.Where(t => t.UserId == user.Id).AsEnumerable();

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
                    if (new[] { "low", "medium", "high" }.Contains(priority))
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
                            var priorityOrder = new[] { "high", "medium", "low" };
                            filteredTasks = direction == "desc"
                                ? filteredTasks.OrderByDescending(t => Array.IndexOf(priorityOrder, t.Priority.ToLower()))
                                : filteredTasks.OrderBy(t => Array.IndexOf(priorityOrder, t.Priority.ToLower()));
                            break;
                        default:
                            WriteError(response, 400, $"Неподдерживаемая сортировка: {orderBy}", "INVALID_ORDERBY");
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

                var result = new
                {
                    items = pagedTasks,
                    pagination = new
                    {
                        currentPage = page,
                        pageSize = pageSize,
                        totalItems = totalItems,
                        totalPages = (int)Math.Ceiling(totalItems / (double)pageSize)
                    }
                };

                WriteSuccess(response, result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в HandleGetTasks: {ex.Message}");
                WriteError(response, 500, "Внутренняя ошибка сервера", "INTERNAL_ERROR");
            }
        }

        static void HandleGetTaskById(HttpListenerRequest request, HttpListenerResponse response, int id)
        {
            if (!CheckAuthorization(request, response))
                return;

            if (!TryGetUserFromToken(request, out User user))
                return;

            var task = _tasks.FirstOrDefault(t => t.Id == id && t.UserId == user.Id);
            if (task == null)
            {
                WriteError(response, 404, $"Задача с id {id} не найдена", "TASK_NOT_FOUND");
                return;
            }

            WriteSuccess(response, task);
        }

        static void HandleSearchTasks(HttpListenerRequest request, HttpListenerResponse response)
        {
            if (!CheckAuthorization(request, response))
                return;

            if (!TryGetUserFromToken(request, out User user))
                return;

            var queryParams = ParseQueryString(request.Url.Query);

            if (!queryParams.ContainsKey("query"))
            {
                WriteError(response, 400, "Параметр query обязателен", "QUERY_REQUIRED");
                return;
            }

            string searchQuery = queryParams["query"].ToLower();

            var results = _tasks.Where(t => t.UserId == user.Id &&
                (t.Title.ToLower().Contains(searchQuery) ||
                (t.Description != null && t.Description.ToLower().Contains(searchQuery)))
            ).ToList();

            WriteSuccess(response, results);
        }

        static void HandleGetStats(HttpListenerRequest request, HttpListenerResponse response)
        {
            if (!CheckAuthorization(request, response))
                return;

            if (!TryGetUserFromToken(request, out User user))
                return;

            var userTasks = _tasks.Where(t => t.UserId == user.Id).ToList();

            var stats = new
            {
                total = userTasks.Count,
                completed = userTasks.Count(t => t.IsCompleted),
                notCompleted = userTasks.Count(t => !t.IsCompleted),
                byPriority = new
                {
                    high = userTasks.Count(t => t.Priority.ToLower() == "high"),
                    medium = userTasks.Count(t => t.Priority.ToLower() == "medium"),
                    low = userTasks.Count(t => t.Priority.ToLower() == "low")
                }
            };

            WriteSuccess(response, stats);
        }

        static void HandleCreateTask(HttpListenerRequest request, HttpListenerResponse response)
        {
            if (!CheckAuthorization(request, response))
                return;

            if (!TryGetUserFromToken(request, out User user))
                return;

            try
            {
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
                    catch (JsonException)
                    {
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
                        var validPriorities = new[] { "low", "medium", "high" };
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

                    var newTask = new TaskItem
                    {
                        Id = _tasks.Count > 0 ? _tasks.Max(t => t.Id) + 1 : 1,
                        Title = requestData.Title.Trim(),
                        Description = requestData.Description != null ? requestData.Description.Trim() : null,
                        Priority = !string.IsNullOrEmpty(requestData.Priority) ? requestData.Priority.ToLower() : "medium",
                        IsCompleted = requestData.IsCompleted,
                        CreatedAt = DateTime.Now,
                        DueDate = requestData.DueDate,
                        UserId = user.Id
                    };

                    _tasks.Add(newTask);
                    response.AddHeader("Location", $"/api/tasks/{newTask.Id}");
                    WriteSuccess(response, newTask, 201);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в HandleCreateTask: {ex.Message}");
                WriteError(response, 500, "Внутренняя ошибка сервера", "INTERNAL_ERROR");
            }
        }

        static void HandleUpdateTask(HttpListenerRequest request, HttpListenerResponse response, int id)
        {
            if (!CheckAuthorization(request, response))
                return;

            if (!TryGetUserFromToken(request, out User user))
                return;

            try
            {
                var existingTask = _tasks.FirstOrDefault(t => t.Id == id && t.UserId == user.Id);
                if (existingTask == null)
                {
                    WriteError(response, 404, $"Задача с id {id} не найдена", "TASK_NOT_FOUND");
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
                    catch (JsonException)
                    {
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
                        var validPriorities = new[] { "low", "medium", "high" };
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

                    WriteSuccess(response, existingTask);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в HandleUpdateTask: {ex.Message}");
                WriteError(response, 500, "Внутренняя ошибка сервера", "INTERNAL_ERROR");
            }
        }

        static void HandleDeleteTask(HttpListenerRequest request, HttpListenerResponse response, int id)
        {
            if (!CheckAuthorization(request, response))
                return;

            if (!TryGetUserFromToken(request, out User user))
                return;

            var task = _tasks.FirstOrDefault(t => t.Id == id && t.UserId == user.Id);
            if (task == null)
            {
                WriteError(response, 404, $"Задача с id {id} не найдена", "TASK_NOT_FOUND");
                return;
            }

            _tasks.Remove(task);
            WriteSuccess(response, new { message = "Задача успешно удалена" });
        }
    }
}