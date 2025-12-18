using System;
using System.Windows;
using SystemAudioAnalyzer.Services;

namespace SystemAudioAnalyzer
{
    public partial class MainWindow : Window
    {
        private AudioRecorder _recorder;
        private OpenRouterService _apiService;
        // TODO: Replace with your actual API key or load from config
        private const string ApiKey = "YOUR_OPENROUTER_API_KEY"; 

        public MainWindow()
        {
            InitializeComponent();
            _recorder = new AudioRecorder();
            _recorder.AudioChunkReady += OnAudioChunkReady;
            _apiService = new OpenRouterService(ApiKey);
        }

        private async void OnAudioChunkReady(object? sender, string filePath)
        {
            try
            {
                // 1. Transcribe
                var transcription = await _apiService.TranscribeAudioAsync(filePath);
                if (string.IsNullOrWhiteSpace(transcription)) return;

                Dispatcher.Invoke(() =>
                {
                    txtTranscription.Text += transcription + "\n";
                });

                // 2. Translate
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