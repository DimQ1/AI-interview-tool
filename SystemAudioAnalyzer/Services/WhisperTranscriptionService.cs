using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Whisper.net;
using Whisper.net.LibraryLoader;

namespace SystemAudioAnalyzer.Services
{
    public class WhisperTranscriptionService
    {
        private WhisperFactory? _whisperFactory;
        private WhisperProcessor? _processor;
        private string _modelPath;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public WhisperTranscriptionService()
        {
            // Initial path, will be updated in InitializeAsync based on settings if needed, 
            // but ideally we pass settings to this service. 
            // However, since the service is instantiated in MainWindow and settings are loaded there,
            // we should probably allow updating the model path or reading it from settings.
            // For now, let's assume the file name comes from settings.
            
            // We'll rely on InitializeAsync to set up the correct path based on current settings.
        }

        public async Task InitializeAsync()
        {
            // Reload settings to get the latest model choice
            var settings = SettingsManager.Load();
            string modelFilename = settings.WhisperModelFilename;
            
            // Fallback if empty
            if (string.IsNullOrEmpty(modelFilename))
            {
                modelFilename = "ggml-large-v3-turbo-q5_0.bin";
            }

            _modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, modelFilename);

            if (!File.Exists(_modelPath))
            {
                // If the configured model doesn't exist, we can't proceed.
                // In a real app, we might trigger a download or notify the user.
                // For now, we'll just return and let TranscribeAsync fail gracefully or try to download default.
                
                // Let's try to download if it's the default one, otherwise fail.
                if (modelFilename == "ggml-large-v3-turbo-q5_0.bin")
                {
                     await DownloadModelAsync();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Selected Whisper model not found: {_modelPath}");
                    return;
                }
            }

            // Optional set the order of the runtimes:
            //RuntimeOptions.RuntimeLibraryOrder = [RuntimeLibrary.Cuda];

            try 
            {
                _whisperFactory = WhisperFactory.FromPath(_modelPath);
                _processor = _whisperFactory.CreateBuilder()
                    .WithLanguage("auto")
                    .Build();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing Whisper factory: {ex.Message}");
            }
        }

        private async Task DownloadModelAsync()
        {
            // This is a fallback for the default model if it's missing.
            // The UI now handles downloading other models.
            string url = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3-turbo-q5_0.bin";
            
            using var httpClient = new HttpClient();
            using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(_modelPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await stream.CopyToAsync(fileStream);
            await fileStream.FlushAsync();
        }

        public async Task<string> TranscribeAsync(string filePath)
        {
            await _semaphore.WaitAsync();
            try
            {
                if (_processor == null)
                {
                    await InitializeAsync();
                }

                if (_processor == null) return string.Empty;

                // Whisper.net expects 16kHz mono PCM. 
                // Our AudioRecorder saves as whatever the system loopback is (usually 48kHz stereo).
                // We need to resample.

                // For simplicity, let's assume we need to resample using NAudio before passing to Whisper.
                // But wait, Whisper.net might handle wav reading? 
                // WhisperProcessor.ProcessAsync takes a stream of float samples or byte array of 16-bit PCM.

                // Let's use NAudio to read and resample to 16kHz mono.
                using var reader = new NAudio.Wave.AudioFileReader(filePath);
                var resampler = new NAudio.Wave.MediaFoundationResampler(reader, new NAudio.Wave.WaveFormat(16000, 1));

                // Read resampled data
                var buffer = new byte[resampler.WaveFormat.AverageBytesPerSecond * (int)reader.TotalTime.TotalSeconds + 4096];
                // Better: read in chunks or memory stream.

                using var memStream = new MemoryStream();
                var readBuffer = new byte[4096];
                int read;
                while ((read = resampler.Read(readBuffer, 0, readBuffer.Length)) > 0)
                {
                    memStream.Write(readBuffer, 0, read);
                }

                memStream.Position = 0;

                // Whisper.net processes float[] or short[]. 
                // We have bytes (16-bit PCM).
                // Let's convert to short[] then float[]
                var bytes = memStream.ToArray();
                var samples = new short[bytes.Length / 2];
                Buffer.BlockCopy(bytes, 0, samples, 0, bytes.Length);

                var floatSamples = new float[samples.Length];
                for (int i = 0; i < samples.Length; i++)
                {
                    floatSamples[i] = samples[i] / 32768f;
                }

                var text = "";
                if (floatSamples.Length > 0)
                {
                    await foreach (var segment in _processor.ProcessAsync(floatSamples))
                    {
                        text += segment.Text + " ";
                    }
                }

                return text.Trim();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Whisper Transcription Error: {ex}");
                // Return empty string on error to keep app running
                return string.Empty;
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
