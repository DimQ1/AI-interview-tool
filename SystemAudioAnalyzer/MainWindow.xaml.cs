using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.Configuration;
using SystemAudioAnalyzer.Services;

namespace SystemAudioAnalyzer
{
    public partial class MainWindow : Window
    {
        private AudioRecorder _recorder;
        private OpenRouterService _apiService;
        private WhisperTranscriptionService _transcriptionService;

        private readonly Queue<string> _transcriptionHistory = new Queue<string>();
        private readonly HashSet<string> _displayedQuestions = new HashSet<string>();
        private bool _isAutoScrollPaused = false;

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
            
            // Clear initial text in RichTextBox
            rtbAnalysis.Document.Blocks.Clear();
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

                // Prepare context for analysis (Current + 3 previous)
                string analysisContext;
                lock (_transcriptionHistory)
                {
                    if (_transcriptionHistory.Count > 0)
                    {
                        analysisContext = string.Join(" ", _transcriptionHistory) + " " + transcription;
                    }
                    else
                    {
                        analysisContext = transcription;
                    }

                    _transcriptionHistory.Enqueue(transcription);
                    while (_transcriptionHistory.Count > 3)
                    {
                        _transcriptionHistory.Dequeue();
                    }
                }

                // 3. Analyze
                var analysis = await _apiService.AnalyzeAsync(analysisContext);
                Dispatcher.Invoke(() =>
                {
                    var paragraph = new Paragraph();
                    bool hasContent = false;

                    for (int i = 0; i < analysis.Questions.Count; i++)
                    {
                        string q = analysis.Questions[i];
                        string a = (i < analysis.Answers.Count) ? analysis.Answers[i] : string.Empty;

                        if (!_displayedQuestions.Contains(q))
                        {
                            _displayedQuestions.Add(q);
                            
                            paragraph.Inlines.Add(new Run(q + "\n") { Foreground = Brushes.Blue });
                            paragraph.Inlines.Add(new Run(a + "\n") { Foreground = Brushes.Black });
                            hasContent = true;
                        }
                    }

                    if (hasContent)
                    {
                        rtbAnalysis.Document.Blocks.Add(paragraph);
                        if (!_isAutoScrollPaused)
                        {
                            rtbAnalysis.ScrollToEnd();
                        }
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

        private void rtbAnalysis_MouseEnter(object sender, MouseEventArgs e)
        {
            _isAutoScrollPaused = true;
        }

        private void rtbAnalysis_MouseLeave(object sender, MouseEventArgs e)
        {
            _isAutoScrollPaused = false;
            rtbAnalysis.ScrollToEnd();
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