using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SystemAudioAnalyzer.Services
{
    public class WhisperModelInfo
    {
        public string Name { get; set; }
        public string Filename { get; set; }
        public string Url { get; set; }
        public long Size { get; set; } // Approximate size in bytes
    }

    public static class WhisperModelManager
    {
        public static readonly List<WhisperModelInfo> AvailableModels = new List<WhisperModelInfo>
        {
            new WhisperModelInfo 
            { 
                Name = "Tiny (English Only)", 
                Filename = "ggml-tiny.en.bin", 
                Url = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.en.bin",
                Size = 77_000_000 
            },
            new WhisperModelInfo 
            { 
                Name = "Tiny", 
                Filename = "ggml-tiny.bin", 
                Url = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.bin",
                Size = 77_000_000 
            },
            new WhisperModelInfo 
            { 
                Name = "Base (English Only)", 
                Filename = "ggml-base.en.bin", 
                Url = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.en.bin",
                Size = 148_000_000 
            },
            new WhisperModelInfo 
            { 
                Name = "Base", 
                Filename = "ggml-base.bin", 
                Url = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin",
                Size = 148_000_000 
            },
            new WhisperModelInfo 
            { 
                Name = "Small (English Only)", 
                Filename = "ggml-small.en.bin", 
                Url = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.en.bin",
                Size = 488_000_000 
            },
            new WhisperModelInfo 
            { 
                Name = "Small", 
                Filename = "ggml-small.bin", 
                Url = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.bin",
                Size = 488_000_000 
            },
            new WhisperModelInfo 
            { 
                Name = "Medium (English Only)", 
                Filename = "ggml-medium.en.bin", 
                Url = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-medium.en.bin",
                Size = 1_530_000_000 
            },
            new WhisperModelInfo 
            { 
                Name = "Medium", 
                Filename = "ggml-medium.bin", 
                Url = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-medium.bin",
                Size = 1_530_000_000 
            },
            new WhisperModelInfo 
            { 
                Name = "Large v3 Turbo (Quantized q5_0)", 
                Filename = "ggml-large-v3-turbo-q5_0.bin", 
                Url = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3-turbo-q5_0.bin",
                Size = 575_000_000 // Approximate
            },
            new WhisperModelInfo
            {
                Name = "Large v3 Turbo (Quantized q8_0)",
                Filename = "ggml-large-v3-turbo-q8_0.bin",
                Url = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3-turbo-q8_0.bin",
                Size = 834_000_000 // Approximate
            }
            //large-v3-turbo-q8_0
        };

        public static bool IsModelDownloaded(string filename)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filename);
            return File.Exists(path) && new FileInfo(path).Length > 0;
        }

        public static async Task DownloadModelAsync(WhisperModelInfo model, IProgress<double> progress, CancellationToken cancellationToken)
        {
            var destinationPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, model.Filename);
            
            using var httpClient = new HttpClient();
            using var response = await httpClient.GetAsync(model.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? model.Size;
            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            long totalRead = 0;
            int read;

            while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, read, cancellationToken);
                totalRead += read;
                
                if (totalBytes > 0)
                {
                    progress?.Report((double)totalRead / totalBytes * 100);
                }
            }
        }
    }
}
