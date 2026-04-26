using System.Net.Http.Json;
using FileShareClient.Models;

namespace FileShareClient.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private string? _token;
        public string ServerUrl { get; set; } = "https://localhost:7217";
        public string? Token => _token;
        public User? CurrentUser { get; private set; }
        public bool IsAuthenticated => !string.IsNullOrWhiteSpace(_token);

        public ApiService()
        {
            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
            _httpClient = new HttpClient(handler);
        }

        public void SetToken(string token)
        {
            _token = token;
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        public void SetSession(string token, User user)
        {
            SetToken(token);
            CurrentUser = user;
        }

        public void ClearSession()
        {
            _token = null;
            CurrentUser = null;
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }

        public async Task<(bool Success, string Token, User? User, string Error)> RegisterAsync(string username, string email, string password)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(
                    $"{ServerUrl}/api/auth/register",
                    new { username, email, password });

                if (response.IsSuccessStatusCode)
                {
                    var authResponse = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
                    if (authResponse == null || string.IsNullOrWhiteSpace(authResponse.Token))
                    {
                        return (false, string.Empty, null, "Некорректный ответ сервера.");
                    }

                    return (true, authResponse.Token, authResponse.User, string.Empty);
                }
                var error = await response.Content.ReadAsStringAsync();
                return (false, string.Empty, null, string.IsNullOrWhiteSpace(error) ? "Ошибка регистрации" : error);
            }
            catch (HttpRequestException)
            {
                return (false, string.Empty, null, "Сервер недоступен. Проверьте, что FileShareServer запущен.");
            }
            catch (Exception ex)
            {
                return (false, string.Empty, null, ex.Message);
            }
        }

        public async Task<(bool Success, string Token, User? User, string Error)> LoginAsync(string username, string password)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(
                    $"{ServerUrl}/api/auth/login",
                    new { username, password });

                if (response.IsSuccessStatusCode)
                {
                    var authResponse = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
                    if (authResponse == null || string.IsNullOrWhiteSpace(authResponse.Token))
                    {
                        return (false, string.Empty, null, "Некорректный ответ сервера.");
                    }

                    return (true, authResponse.Token, authResponse.User, string.Empty);
                }
                var error = await response.Content.ReadAsStringAsync();
                return (false, string.Empty, null, string.IsNullOrWhiteSpace(error) ? "Ошибка входа" : error);
            }
            catch (HttpRequestException)
            {
                return (false, string.Empty, null, "Сервер недоступен. Проверьте, что FileShareServer запущен.");
            }
            catch (Exception ex)
            {
                return (false, string.Empty, null, ex.Message);
            }
        }

        // Users
        public async Task<List<User>> GetAllUsersAsync()
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<List<User>>($"{ServerUrl}/api/users") ?? new();
            }
            catch
            {
                return new();
            }
        }

        public async Task<User?> GetUserAsync(int userId)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<User>($"{ServerUrl}/api/users/{userId}");
            }
            catch
            {
                return null;
            }
        }

        public async Task<List<User>> SearchUsersAsync(string query)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<List<User>>($"{ServerUrl}/api/users/search/{query}") ?? new();
            }
            catch
            {
                return new();
            }
        }

        // Friends
        public async Task<bool> SendFriendRequestAsync(int friendId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"{ServerUrl}/api/friends/request/{friendId}", null);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> AcceptFriendRequestAsync(int friendId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"{ServerUrl}/api/friends/accept/{friendId}", null);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> RejectFriendRequestAsync(int friendId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"{ServerUrl}/api/friends/reject/{friendId}", null);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> RemoveFriendAsync(int friendId)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"{ServerUrl}/api/friends/remove/{friendId}");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<User>> GetFriendsAsync()
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<List<User>>($"{ServerUrl}/api/friends/list") ?? new();
            }
            catch
            {
                return new();
            }
        }

        public async Task<List<Friendship>> GetPendingRequestsAsync()
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<List<Friendship>>($"{ServerUrl}/api/friends/pending") ?? new();
            }
            catch
            {
                return new();
            }
        }

        public async Task<List<Friendship>> GetSentRequestsAsync()
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<List<Friendship>>($"{ServerUrl}/api/friends/sent") ?? new();
            }
            catch
            {
                return new();
            }
        }

        // Chat
        public async Task<List<ChatMessage>> GetConversationAsync(int friendId)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<List<ChatMessage>>($"{ServerUrl}/api/chat/conversation/{friendId}") ?? new();
            }
            catch
            {
                return new();
            }
        }

        public async Task<List<ChatMessage>> GetUnreadMessagesAsync()
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<List<ChatMessage>>($"{ServerUrl}/api/chat/unread") ?? new();
            }
            catch
            {
                return new();
            }
        }

        public async Task<bool> MarkAsReadAsync(int messageId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"{ServerUrl}/api/chat/mark-read/{messageId}", null);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        // Files
        public async Task<(bool Success, string Error)> UploadFileAsync(int receiverId, Stream stream, string fileName)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                using var fileContent = new StreamContent(stream);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                content.Add(fileContent, "file", fileName);

                var response = await _httpClient.PostAsync($"{ServerUrl}/api/files/upload/{receiverId}", content);
                if (response.IsSuccessStatusCode)
                {
                    return (true, string.Empty);
                }

                var error = await response.Content.ReadAsStringAsync();
                return (false, string.IsNullOrWhiteSpace(error) ? "Ошибка отправки файла." : error);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<List<IncomingFile>> GetIncomingFilesAsync()
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<List<IncomingFile>>($"{ServerUrl}/api/files/inbox") ?? new();
            }
            catch
            {
                return new();
            }
        }

        public async Task<(bool Success, byte[]? Data, string FileName, string Error)> DownloadFileAsync(int fileId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{ServerUrl}/api/files/download/{fileId}");
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return (false, null, string.Empty, string.IsNullOrWhiteSpace(error) ? "Ошибка скачивания файла." : error);
                }

                var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                    ?? response.Content.Headers.ContentDisposition?.FileName
                    ?? $"file_{fileId}";
                fileName = fileName.Trim('"');
                var bytes = await response.Content.ReadAsByteArrayAsync();
                return (true, bytes, fileName, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, null, string.Empty, ex.Message);
            }
        }

        public async Task<bool> StoreMessageAsync(int friendId, string content)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(
                    $"{ServerUrl}/api/chat/store/{friendId}",
                    new { content });
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> StoreFileMessageAsync(int friendId, string fileName, long fileSize, string source, string token = "-")
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(
                    $"{ServerUrl}/api/chat/store-file/{friendId}",
                    new { fileName, fileSize, source, token });
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}
