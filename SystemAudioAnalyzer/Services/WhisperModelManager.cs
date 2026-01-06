using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
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
        private static readonly List<WhisperModelInfo> _defaultModels = new List<WhisperModelInfo>
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
        };

        private const string HfApiUrl = "https://huggingface.co/api/models/ggerganov/whisper.cpp/tree/main";

        public static List<WhisperModelInfo> GetAvailableModels(AppSettings settings)
        {
            var combined = new Dictionary<string, WhisperModelInfo>();

            // Add defaults first
            foreach(var model in _defaultModels)
            {
                combined[model.Filename] = model;
            }

            // Merge with known models from settings, overriding with persisted info but keeping defaults if missing
            if (settings.KnownModels != null)
            {
                foreach(var model in settings.KnownModels)
                {
                    combined[model.Filename] = model;
                }
            }
            
            // Ensure defaults are always in the final list if for some reason settings lost them, 
            // but we already did that with the initial population.
            
            return combined.Values.OrderBy(m => m.Size).ToList();
        }

        public static async Task<List<WhisperModelInfo>> RefreshModelsAsync(AppSettings settings)
        {
             try
            {
                using var httpClient = new HttpClient();
                var json = await httpClient.GetStringAsync(HfApiUrl);
                
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                
                var remoteModels = new List<WhisperModelInfo>();

                if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in root.EnumerateArray())
                    {
                        if (item.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "file" &&
                            item.TryGetProperty("path", out var pathProp))
                        {
                            string filename = pathProp.GetString();
                            if (!string.IsNullOrEmpty(filename) && filename.EndsWith(".bin") && filename.StartsWith("ggml-"))
                            {
                                long size = 0;
                                if (item.TryGetProperty("size", out var sizeProp) && sizeProp.TryGetInt64(out var s))
                                {
                                    size = s;
                                }

                                var name = GenerateReadableName(filename);
                                var url = $"https://huggingface.co/ggerganov/whisper.cpp/resolve/main/{filename}";

                                remoteModels.Add(new WhisperModelInfo
                                {
                                    Name = name,
                                    Filename = filename,
                                    Url = url,
                                    Size = size
                                });
                            }
                        }
                    }
                }

                // Merge Logic
                var currentList = GetAvailableModels(settings);
                var mergedDict = new Dictionary<string, WhisperModelInfo>();

                // Keep existing ones (preserve custom names if any, though we regenerate names above)
                foreach(var m in currentList)
                {
                     mergedDict[m.Filename] = m;
                }

                // Update/Add remotes
                foreach(var r in remoteModels)
                {
                    mergedDict[r.Filename] = r;
                }
                
                // Note: We do NOT remove local models that are missing from remote,
                // matching the requirement: "if model downloaded but missing on page -> keep"
                // Actually, we should keep them even if NOT downloaded? The prompt says:
                // "if model is downloaded but missing on page then keep it locally in the list."
                // My logic above keeps ALL local models.
                // But we actally want to remove models that are NOT downloaded AND NOT in remote AND NOT in defaults.
                // But currently everything in 'currentList' is either default or previously known.
                // If it's previously known and not downloaded and not in remote, maybe we should remove it?
                // The requirement is specific about "if model is downloaded... keep it".
                // Implication: If NOT downloaded and NOT in remote... remove it?
                // Let's implement that cleanup.
                
                var finalKeys = new HashSet<string>(mergedDict.Keys);
                var remoteFilenames = new HashSet<string>(remoteModels.Select(m => m.Filename));
                
                var cleanedList = new List<WhisperModelInfo>();
                
                foreach(var kvp in mergedDict)
                {
                    var filename = kvp.Key;
                    var model = kvp.Value;
                    
                    bool isRemote = remoteFilenames.Contains(filename);
                    bool isDefault = _defaultModels.Any(d => d.Filename == filename);
                    bool isDownloaded = IsModelDownloaded(filename);

                    if (isRemote || isDefault || isDownloaded)
                    {
                        cleanedList.Add(model);
                    }
                    // Else: drop it (it was a cached metadata entry that is no longer on remote and we don't not have the file)
                }

                settings.KnownModels = cleanedList;
                return cleanedList;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to refresh models: {ex.Message}");
                // In case of failure, return current known
                return GetAvailableModels(settings);
            }
        }

        private static string GenerateReadableName(string filename)
        {
            // ggml-base.en.bin -> Base (English Only)
            // ggml-large-v3-turbo-q5_0.bin -> Large v3 Turbo (q5_0)
            
            string name = filename.Replace("ggml-", "").Replace(".bin", "");
            
            if (name.Contains("."))
            {
                // handle .en cases
                 var parts = name.Split('.');
                 if (parts.Length > 1 && parts[1] == "en")
                 {
                     name = parts[0] + " (English Only)";
                 }
                 else
                 {
                     name = name.Replace(".", " ");
                 }
            }
            
            // Capitalize first letter
            if (name.Length > 0)
            {
                name = char.ToUpper(name[0]) + name.Substring(1);
            }

            return name;
        }

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
        
        public static void DeleteModel(string filename)
        {
             var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filename);
             if (File.Exists(path))
             {
                 try 
                 {
                    File.Delete(path);
                 }
                 catch(Exception ex)
                 {
                     System.Diagnostics.Debug.WriteLine($"Failed to delete model {filename}: {ex.Message}");
                     throw;
                 }
             }
        }
    }
}
