using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SystemAudioAnalyzer.Services
{
    public class AnalysisResult
    {
        public string Questions { get; set; } = string.Empty;
        public string Answers { get; set; } = string.Empty;
    }

    public class OpenRouterService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private const string BaseUrl = "https://openrouter.ai/api/v1";

        public OpenRouterService(string apiKey)
        {
            _apiKey = apiKey;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://github.com/your-repo");
            _httpClient.DefaultRequestHeaders.Add("X-Title", "SystemAudioAnalyzer");
        }

        public async Task<string> TranscribeAudioAsync(string filePath)
        {
            // Note: OpenRouter might not support /audio/transcriptions directly.
            // If this fails, one would need to use a specific provider or model that supports audio input.
            // For this implementation, we assume a standard OpenAI-compatible endpoint or similar.
            // If OpenRouter doesn't support this, we might need to use 'openai/whisper' via a different path or provider.

            using var content = new MultipartFormDataContent();
            using var fileStream = File.OpenRead(filePath);
            using var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            content.Add(fileContent, "file", Path.GetFileName(filePath));
            content.Add(new StringContent("openai/whisper"), "model");

            // Try standard endpoint
            var response = await _httpClient.PostAsync($"{BaseUrl}/audio/transcriptions", content);

            if (!response.IsSuccessStatusCode)
            {
                // Fallback or error handling
                var error = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"Transcription API Error: {response.StatusCode} - {error}");
                throw new Exception($"Transcription failed: {response.StatusCode} - {error}");
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("text", out var textProp))
            {
                return textProp.GetString() ?? string.Empty;
            }
            return string.Empty;
        }

        public async Task<string> TranslateAsync(string text, string targetLanguage = "Russian")
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            var requestBody = new
            {
                model = "openai/gpt-3.5-turbo",
                messages = new[]
                {
                    new { role = "system", content = $"Translate the following text to {targetLanguage}." },
                    new { role = "user", content = text }
                }
            };

            return await SendChatCompletionAsync(requestBody);
        }

        public async Task<AnalysisResult> AnalyzeAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new AnalysisResult();

            var requestBody = new
            {
                model = "openai/gpt-3.5-turbo",
                messages = new[]
                {
                    new { role = "system", content = "Analyze the text. Extract questions asked by the speaker. For each question, provide a short answer. Return the result in JSON format: { \"questions\": [\"q1\", \"q2\"], \"answers\": [\"a1\", \"a2\"] } where a1 corresponds to q1." },
                    new { role = "user", content = text }
                },
                response_format = new { type = "json_object" }
            };

            var jsonResponse = await SendChatCompletionAsync(requestBody);

            try
            {
                using var doc = JsonDocument.Parse(jsonResponse);
                var root = doc.RootElement;
                var questions = new StringBuilder();
                var answers = new StringBuilder();

                if (root.TryGetProperty("questions", out var qArray))
                {
                    foreach (var q in qArray.EnumerateArray())
                    {
                        questions.AppendLine(q.GetString());
                    }
                }

                if (root.TryGetProperty("answers", out var aArray))
                {
                    foreach (var a in aArray.EnumerateArray())
                    {
                        answers.AppendLine(a.GetString());
                    }
                }

                return new AnalysisResult
                {
                    Questions = questions.ToString(),
                    Answers = answers.ToString()
                };
            }
            catch
            {
                // Fallback if JSON parsing fails
                return new AnalysisResult { Questions = "Error parsing analysis", Answers = "" };
            }
        }

        private async Task<string> SendChatCompletionAsync(object requestBody)
        {
            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{BaseUrl}/chat/completions", content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"API call failed: {response.StatusCode} - {error}");
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);
            return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
        }
    }
}
