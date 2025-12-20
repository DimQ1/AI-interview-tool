using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using SystemAudioAnalyzer.Services;

namespace SystemAudioAnalyzer
{
    public partial class SettingsWindow : Window
    {
        public AppSettings Settings { get; private set; }
        private CancellationTokenSource _downloadCts;

        public SettingsWindow(AppSettings currentSettings)
        {
            InitializeComponent();
            Settings = new AppSettings
            {
                ApiKey = currentSettings.ApiKey,
                Model = currentSettings.Model,
                AnalysisPrompt = currentSettings.AnalysisPrompt,
                TranslationPrompt = currentSettings.TranslationPrompt,
                WhisperModelFilename = currentSettings.WhisperModelFilename
            };

            txtApiKey.Text = Settings.ApiKey;
            txtModel.Text = Settings.Model;
            txtAnalysisPrompt.Text = Settings.AnalysisPrompt;
            txtTranslationPrompt.Text = Settings.TranslationPrompt;
            txtActiveModel.Text = Settings.WhisperModelFilename;

            InitializeWhisperModels();
        }

        private void InitializeWhisperModels()
        {
            cmbWhisperModels.ItemsSource = WhisperModelManager.AvailableModels;
            
            // Select current model if available in list
            var current = WhisperModelManager.AvailableModels.FirstOrDefault(m => m.Filename == Settings.WhisperModelFilename);
            if (current != null)
            {
                cmbWhisperModels.SelectedItem = current;
            }
            else
            {
                cmbWhisperModels.SelectedIndex = 0;
            }
        }

        private void cmbWhisperModels_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateModelStatus();
        }

        private void UpdateModelStatus()
        {
            if (cmbWhisperModels.SelectedItem is WhisperModelInfo selectedModel)
            {
                bool isDownloaded = WhisperModelManager.IsModelDownloaded(selectedModel.Filename);
                txtModelStatus.Text = isDownloaded ? "Downloaded" : "Not Downloaded";
                btnDownloadModel.IsEnabled = !isDownloaded;
                btnUseModel.IsEnabled = isDownloaded;
            }
        }

        private async void btnDownloadModel_Click(object sender, RoutedEventArgs e)
        {
            if (cmbWhisperModels.SelectedItem is not WhisperModelInfo selectedModel) return;

            try
            {
                btnDownloadModel.IsEnabled = false;
                cmbWhisperModels.IsEnabled = false;
                pbDownload.Visibility = Visibility.Visible;
                pbDownload.Value = 0;
                txtModelStatus.Text = "Downloading...";

                _downloadCts = new CancellationTokenSource();
                var progress = new Progress<double>(p => pbDownload.Value = p);

                await WhisperModelManager.DownloadModelAsync(selectedModel, progress, _downloadCts.Token);

                txtModelStatus.Text = "Downloaded";
                btnUseModel.IsEnabled = true;
                MessageBox.Show($"Model {selectedModel.Name} downloaded successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                txtModelStatus.Text = "Cancelled";
            }
            catch (Exception ex)
            {
                txtModelStatus.Text = "Error";
                MessageBox.Show($"Download failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnDownloadModel.IsEnabled = !WhisperModelManager.IsModelDownloaded(selectedModel.Filename);
                cmbWhisperModels.IsEnabled = true;
                pbDownload.Visibility = Visibility.Collapsed;
                _downloadCts = null;
                UpdateModelStatus();
            }
        }

        private void btnUseModel_Click(object sender, RoutedEventArgs e)
        {
            if (cmbWhisperModels.SelectedItem is WhisperModelInfo selectedModel)
            {
                Settings.WhisperModelFilename = selectedModel.Filename;
                txtActiveModel.Text = selectedModel.Filename;
                MessageBox.Show($"Model set to {selectedModel.Name}. It will be used next time the transcription service initializes.", "Model Selected", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            Settings.ApiKey = txtApiKey.Text;
            Settings.Model = txtModel.Text;
            Settings.AnalysisPrompt = txtAnalysisPrompt.Text;
            Settings.TranslationPrompt = txtTranslationPrompt.Text;
            // WhisperModelFilename is already updated via btnUseModel_Click, but let's ensure consistency if we wanted to bind it differently.
            // For now, it's fine.

            DialogResult = true;
            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            _downloadCts?.Cancel();
            DialogResult = false;
            Close();
        }
    }
}
