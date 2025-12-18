using System;
using System.Windows;
using Microsoft.Extensions.Configuration;
using SystemAudioAnalyzer.Services;

namespace SystemAudioAnalyzer
{
    public partial class MainWindow : Window
    {
        private AudioRecorder _recorder;
        private OpenRouterService _apiService;
        private WhisperTranscriptionService _transcriptionService;

        public MainWindow()
        {
            InitializeComponent();

            var builder = new ConfigurationBuilder()
                .AddUserSecrets<MainWindow>();
            var configuration = builder.Build();

            string apiKey = configuration["OpenRouterApiKey"] ?? string.Empty;

            if (string.IsNullOrEmpty(apiKey))
            {
                MessageBox.Show("API Key not found. Please configure 'OpenRouterApiKey' in User Secrets.");
            }

            _recorder = new AudioRecorder();
            _recorder.AudioChunkReady += OnAudioChunkReady;
            _apiService = new OpenRouterService(apiKey);
            _transcriptionService = new WhisperTranscriptionService();

            // Initialize Whisper in background
            _ = _transcriptionService.InitializeAsync();
        }

        private async void OnAudioChunkReady(object? sender, string filePath)
        {
            try
            {
                // 1. Transcribe (Local Whisper)
                var transcription = await _transcriptionService.TranscribeAsync(filePath);
                if (string.IsNullOrWhiteSpace(transcription)) return;

                Dispatcher.Invoke(() =>
                {
                    txtTranscription.Text += transcription + "\n";
                });

                // 2. Translate (OpenRouter)
                var translation = await _apiService.TranslateAsync(transcription);
                Dispatcher.Invoke(() =>
                {
                    txtTranslation.Text += translation + "\n";
                });

                // 3. Analyze
                var analysis = await _apiService.AnalyzeAsync(transcription);
                Dispatcher.Invoke(() =>
                {
                    if (!string.IsNullOrWhiteSpace(analysis.Questions))
                    {
                        txtQuestions.Text += analysis.Questions + "\n";
                    }
                    if (!string.IsNullOrWhiteSpace(analysis.Answers))
                    {
                        txtAnswers.Text += analysis.Answers + "\n";
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error processing audio chunk: {ex}");
                Dispatcher.Invoke(() =>
                {
                    txtStatus.Text = $"Error: {ex.Message}";
                });
            }
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _recorder.StartRecording();
                btnStart.IsEnabled = false;
                btnStop.IsEnabled = true;
                txtStatus.Text = "Recording...";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting recording: {ex.Message}");
            }
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _recorder.StopRecording();
                btnStart.IsEnabled = true;
                btnStop.IsEnabled = false;
                txtStatus.Text = "Stopped";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error stopping recording: {ex.Message}");
            }
        }
    }
}