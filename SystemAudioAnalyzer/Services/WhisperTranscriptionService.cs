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
        private readonly string _modelPath;
        private const string ModelUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3-turbo-q5_0.bin";
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public WhisperTranscriptionService()
        {
            _modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ggml-large-v3-turbo-q5_0.bin");
        }

        public async Task InitializeAsync()
        {
            if (!File.Exists(_modelPath))
            {
                await DownloadModelAsync();
            }

            // Optional set the order of the runtimes:
            //RuntimeOptions.RuntimeLibraryOrder = [RuntimeLibrary.Cuda];

            _whisperFactory = WhisperFactory.FromPath(_modelPath);
            _processor = _whisperFactory.CreateBuilder()
                .WithLanguage("auto")
                .Build();
        }

        private async Task DownloadModelAsync()
        {
            using var httpClient = new HttpClient();
            using var response = await httpClient.GetAsync(ModelUrl, HttpCompletionOption.ResponseHeadersRead);
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
