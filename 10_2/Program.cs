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

        static void Main(string[] args)
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:5000/");
            listener.Start();

            while (true)
            {
                try
                {
                    HttpListenerContext context = listener.GetContext();
                    HttpListenerRequest request = context.Request;
                    HttpListenerResponse response = context.Response;

                    string path = request.Url.AbsolutePath.ToLower();
                    string method = request.HttpMethod;

                    Console.WriteLine($"[{DateTime.Now}] {method} {path} {request.Url.Query}");

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
            var task = _tasks.FirstOrDefault(t => t.Id == id);
            if (task == null)
            {
                WriteError(response, 404, $"Задача с id {id} не найдена", "TASK_NOT_FOUND");
                return;
            }

            WriteSuccess(response, task);
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

            var results = _tasks.Where(t =>
                t.Title.ToLower().Contains(searchQuery) ||
                t.Description.ToLower().Contains(searchQuery)
            ).ToList();

            WriteSuccess(response, results);
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

            WriteSuccess(response, stats);
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
                        WriteError(response, 400, "Тело запроса не может быть пустым", "EMPTY_BODY");
                        return;
                    }

                    var newTask = JsonSerializer.Deserialize<TaskItem>(body);

                    if (newTask == null)
                    {
                        WriteError(response, 400, "Некорректный формат JSON", "INVALID_JSON");
                        return;
                    }

                    if (string.IsNullOrEmpty(newTask.Title))
                    {
                        WriteError(response, 400, "Поле Title обязательно", "TITLE_REQUIRED");
                        return;
                    }

                    if (string.IsNullOrEmpty(newTask.Priority))
                        newTask.Priority = "medium";

                    if (newTask.CreatedAt == default)
                        newTask.CreatedAt = DateTime.Now;

                    int newId = _tasks.Count > 0 ? _tasks.Max(t => t.Id) + 1 : 1;
                    newTask.Id = newId;

                    _tasks.Add(newTask);

                    response.AddHeader("Location", $"/api/tasks/{newId}");
                    WriteSuccess(response, newTask, 201);
                }
            }
            catch (JsonException)
            {
                WriteError(response, 400, "Некорректный формат JSON", "INVALID_JSON");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в HandleCreateTask: {ex.Message}");
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
                    WriteError(response, 404, $"Задача с id {id} не найдена", "TASK_NOT_FOUND");
                    return;
                }

                using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                {
                    string body = reader.ReadToEnd();

                    if (string.IsNullOrWhiteSpace(body))
                    {
                        WriteError(response, 400, "Тело запроса не может быть пустым", "EMPTY_BODY");
                        return;
                    }

                    var updatedTask = JsonSerializer.Deserialize<TaskItem>(body);

                    if (updatedTask == null)
                    {
                        WriteError(response, 400, "Некорректный формат JSON", "INVALID_JSON");
                        return;
                    }

                    if (!string.IsNullOrEmpty(updatedTask.Title))
                        existingTask.Title = updatedTask.Title;

                    if (updatedTask.Description != null)
                        existingTask.Description = updatedTask.Description;

                    if (!string.IsNullOrEmpty(updatedTask.Priority))
                    {
                        if (new[] { "low", "medium", "high" }.Contains(updatedTask.Priority.ToLower()))
                            existingTask.Priority = updatedTask.Priority.ToLower();
                        else
                        {
                            WriteError(response, 400, "Приоритет должен быть low, medium или high", "INVALID_PRIORITY");
                            return;
                        }
                    }

                    existingTask.IsCompleted = updatedTask.IsCompleted;

                    if (updatedTask.DueDate != null)
                        existingTask.DueDate = updatedTask.DueDate;

                    WriteSuccess(response, existingTask);
                }
            }
            catch (JsonException)
            {
                WriteError(response, 400, "Некорректный формат JSON", "INVALID_JSON");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в HandleUpdateTask: {ex.Message}");
                WriteError(response, 500, "Внутренняя ошибка сервера", "INTERNAL_ERROR");
            }
        }

        static void HandleDeleteTask(HttpListenerRequest request, HttpListenerResponse response, int id)
        {
            var task = _tasks.FirstOrDefault(t => t.Id == id);
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