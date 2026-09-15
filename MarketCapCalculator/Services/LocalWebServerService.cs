using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MarketCapCalculator.Services
{
    // Mini server locale per displayare il sito per recupero password via telegram
    public class LocalWebServerService
    {
        private HttpListener? _listener;
        private readonly int _port = 8080;
        private string? _recoveryCode;
        private TaskCompletionSource<string?>? _passwordResetTcs;
        private string _wwwRoot;
        private TelegramBotService _telegramBot;

        public LocalWebServerService()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _wwwRoot = Path.Combine(baseDir, "www");

            if (!Directory.Exists(_wwwRoot))
            {
                var projectDir = Directory.GetParent(baseDir)?.Parent?.Parent?.Parent?.FullName;
                if (projectDir != null) _wwwRoot = Path.Combine(projectDir, "www");
            }

            _telegramBot = new TelegramBotService();
        }

        public async Task<string?> StartRecoveryServerAsync(string recoveryCode)
        {
            _recoveryCode = recoveryCode;
            _passwordResetTcs = new TaskCompletionSource<string?>();

            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{_port}/");
                _listener.Start();

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = $"http://localhost:{_port}",
                    UseShellExecute = true
                });

                _ = Task.Run(ListenForRequestsAsync);
                return await _passwordResetTcs.Task;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Server start error: {ex.Message}");
                return null;
            }
        }

        private async Task ListenForRequestsAsync()
        {
            while (_listener?.IsListening == true)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    await HandleRequestAsync(context);
                }
                catch { break; }
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;
            var path = request.Url?.AbsolutePath ?? "/";

            try
            {
                if (request.HttpMethod == "GET")
                    await ServeStaticFileAsync(path, response);
                else if (request.HttpMethod == "POST")
                    await HandlePostRequestAsync(request, response);
            }
            catch
            {
                response.StatusCode = 500;
                response.Close();
            }
        }

        private async Task ServeStaticFileAsync(string path, HttpListenerResponse response)
        {
            try
            {
                var filePath = Path.Combine(_wwwRoot, path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (path == "/" || string.IsNullOrEmpty(path))
                    filePath = Path.Combine(_wwwRoot, "index.html");

                if (!File.Exists(filePath))
                {
                    response.StatusCode = 404;
                    response.Close();
                    return;
                }

                var bytes = await File.ReadAllBytesAsync(filePath);
                response.ContentType = GetContentType(filePath);
                response.ContentLength64 = bytes.Length;
                await response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            }
            finally { response.Close(); }
        }

        private async Task HandlePostRequestAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            using var reader = new StreamReader(request.InputStream);
            var body = await reader.ReadToEndAsync();
            var path = request.Url?.AbsolutePath ?? "";

            var jsonResponse = "";

            if (path == "/send_code")
            {
                try { await _telegramBot.SendRecoveryCodeAsync(_recoveryCode ?? "123456"); } catch { }
                jsonResponse = "{\"success\":true}";
            }
            else if (path == "/verify_code")
            {
                try
                {
                    var json = System.Text.Json.JsonDocument.Parse(body);
                    var root = json.RootElement;
                    var code = root.TryGetProperty("code", out var c) ? c.GetString() : null;

                    jsonResponse = code == _recoveryCode
                        ? "{\"success\":true}"
                        : "{\"success\":false,\"error\":\"Codice non valido\"}";
                }
                catch { jsonResponse = "{\"success\":false,\"error\":\"Errore\"}"; }
            }
            else if (path == "/reset_password")
            {
                try
                {
                    var json = System.Text.Json.JsonDocument.Parse(body);
                    var root = json.RootElement;
                    var newPassword = root.TryGetProperty("new_password", out var p) ? p.GetString() : null;

                    if (!string.IsNullOrEmpty(newPassword) && newPassword.Length >= 8)
                    {
                        var secureStorage = new SecureStorageService();
                        secureStorage.SavePasswordHash(newPassword);

                        _passwordResetTcs?.TrySetResult(newPassword);
                        jsonResponse = "{\"success\":true}";
                    }
                    else jsonResponse = "{\"success\":false,\"error\":\"Password troppo corta\"}";
                }
                catch { jsonResponse = "{\"success\":false,\"error\":\"Errore\"}"; }
            }
            else jsonResponse = "{\"success\":false,\"error\":\"Azione non valida\"}";

            var buffer = Encoding.UTF8.GetBytes(jsonResponse);
            response.ContentType = "application/json; charset=utf-8";
            response.StatusCode = 200;
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.Close();
        }

        private string GetContentType(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLower();
            return ext switch
            {
                ".html" => "text/html; charset=utf-8",
                ".css" => "text/css; charset=utf-8",
                ".js" => "application/javascript; charset=utf-8",
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".svg" => "image/svg+xml",
                _ => "application/octet-stream"
            };
        }

        public void Stop()
        {
            try
            {
                if (_listener != null && _listener.IsListening)
                {
                    _listener.Stop();
                    _listener.Close();
                    _listener = null;
                }
            }
            catch { }
        }
    }
}