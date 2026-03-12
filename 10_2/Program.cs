using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;

namespace _10_2
{
    class Program
    { 
        static List<TaskItem> _tasks = new List<TaskItem>
            {
                new TaskItem { Id = 1, Title = "Сделать лабораторную", IsCompleted = false },
                new TaskItem { Id = 2, Title = "Проверить почту", IsCompleted = true }
            };
        static void Main(string[] args)
        {
           
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:5000/");
            listener.Start();

            while (true)
            {
                HttpListenerContext context = listener.GetContext();
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;

                string path = request.Url.AbsolutePath.ToLower();
                string method = request.HttpMethod;

                try
                {
                    if (path == "/api/tasks" && method == "GET")
                    {
                        HandleGetTasks(request, response);
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
                            response.StatusCode = 400;
                            response.Close();
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
                            response.StatusCode = 400;
                            response.Close();
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
                            response.StatusCode = 400;
                            response.Close();
                        }
                    }
                    else
                    {
                        response.StatusCode = 404;
                        response.Close();
                    }
                }
                catch (Exception ex)
                {
                    response.StatusCode = 500;
                    WriteJson(response, JsonSerializer.Serialize(new { error = ex.Message }));
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
        static void HandleGetTasks(HttpListenerRequest request, HttpListenerResponse response)
        {
            var copy = _tasks.ToList(); 
            string json = JsonSerializer.Serialize(copy);
            WriteJson(response, json, 200);
        }
        static void HandleGetTaskById(HttpListenerRequest request, HttpListenerResponse response, int id)
        {
            var task = _tasks.FirstOrDefault(t => t.Id == id);
            if (task == null)
            {
                WriteJson(response, JsonSerializer.Serialize(new { error = "Задача не найдена", id }), 404);
                return;
            }
            string json = JsonSerializer.Serialize(task);
            WriteJson(response, json, 200);
        }
        static void HandleCreateTask(HttpListenerRequest request, HttpListenerResponse response)
        {
            using (var reader = new StreamReader(request.InputStream, Encoding.UTF8))
            {
                string body = reader.ReadToEnd();
                var newTask = JsonSerializer.Deserialize<TaskItem>(body);

                if (string.IsNullOrEmpty(newTask?.Title))
                {
                    WriteJson(response, JsonSerializer.Serialize(new { error = "Title обязателен" }), 400);
                    return;
                }

                int newId = _tasks.Count > 0 ? _tasks.Max(t => t.Id) + 1 : 1;
                newTask.Id = newId;

                _tasks.Add(newTask);

                response.AddHeader("Location", $"/api/tasks/{newId}");

                string json = JsonSerializer.Serialize(newTask);
                WriteJson(response, json, 201);
            }
        }
        static void HandleUpdateTask(HttpListenerRequest request, HttpListenerResponse response, int id)
        {
            var existingTask = _tasks.FirstOrDefault(t => t.Id == id);
            if (existingTask == null)
            {
                WriteJson(response, JsonSerializer.Serialize(new { error = "Задача не найдена", id }), 404);
                return;
            }

            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                string body = reader.ReadToEnd();
                var updatedTask = JsonSerializer.Deserialize<TaskItem>(body);

                if (!string.IsNullOrEmpty(updatedTask?.Title))
                    existingTask.Title = updatedTask.Title;

                if (updatedTask?.Description != null)
                    existingTask.Description = updatedTask.Description;

                existingTask.IsCompleted = updatedTask?.IsCompleted ?? existingTask.IsCompleted;

                string json = JsonSerializer.Serialize(existingTask);
                WriteJson(response, json, 200);
            }
        }
        static void HandleDeleteTask(HttpListenerRequest request, HttpListenerResponse response, int id)
        {
            var task = _tasks.FirstOrDefault(t => t.Id == id);
            if (task == null)
            {
                WriteJson(response, JsonSerializer.Serialize(new { error = "Задача не найдена", id }), 404);
                return;
            }

            _tasks.Remove(task);
            WriteJson(response, "{}", 200); 
        }
    }

}
