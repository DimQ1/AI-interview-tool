using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using SystemAudioAnalyzer.Services;
using System.Collections.Generic;

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
                WhisperModelFilename = currentSettings.WhisperModelFilename,
                KnownModels = currentSettings.KnownModels // Copy existing known models
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
            var models = WhisperModelManager.GetAvailableModels(Settings);
            cmbWhisperModels.ItemsSource = models;
            
            // Select current model if available in list
            var current = models.FirstOrDefault(m => m.Filename == Settings.WhisperModelFilename);
            if (current != null)
            {
                cmbWhisperModels.SelectedItem = current;
            }
            else
            {
                // Try to find one that matches filename even if object ref is diff
                 current = models.FirstOrDefault(m => m.Filename == Settings.WhisperModelFilename);
                 if (current != null)
                     cmbWhisperModels.SelectedItem = current;
                 else if (models.Count > 0)
                    cmbWhisperModels.SelectedIndex = 0;
            }
        }
        
        private async void btnRefreshModels_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                btnRefreshModels.IsEnabled = false;
                var models = await WhisperModelManager.RefreshModelsAsync(Settings);
                cmbWhisperModels.ItemsSource = models;
                
                // Reselect
                var currentFilename = (cmbWhisperModels.SelectedItem as WhisperModelInfo)?.Filename ?? Settings.WhisperModelFilename;
                var toSelect = models.FirstOrDefault(m => m.Filename == currentFilename);
                if (toSelect != null)
                {
                    cmbWhisperModels.SelectedItem = toSelect;
                }
                
                MessageBox.Show("Model list refreshed successfully.", "Refreshed", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch(Exception ex)
            {
                MessageBox.Show($"Failed to refresh models: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnRefreshModels.IsEnabled = true;
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
                btnDownloadModel.IsEnabled = !isDownloaded;
                btnUseModel.IsEnabled = isDownloaded;
                
                // Check for broken file if downloaded
                if (isDownloaded)
                {
                     var path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, selectedModel.Filename);
                     var info = new System.IO.FileInfo(path);
                     if (info.Length == 0 || (selectedModel.Size > 0 && info.Length < selectedModel.Size / 100)) // Heuristic: if size is drastically smaller (<1%)
                     {
                         txtModelStatus.Text = "Error (Corrupt/Incomplete)";
                         btnUseModel.IsEnabled = false;
                         btnDownloadModel.IsEnabled = true; // Allow re-download
                         btnDeleteModel.Visibility = Visibility.Visible;
                     }
                     else
                     {
                         txtModelStatus.Text = "Downloaded";
                         btnDeleteModel.Visibility = Visibility.Collapsed;
                     }
                }
                else
                {
                    txtModelStatus.Text = "Not Downloaded";
                    btnDeleteModel.Visibility = Visibility.Collapsed;
                }
            }
        }
        
        private void btnDeleteModel_Click(object sender, RoutedEventArgs e)
        {
            if (cmbWhisperModels.SelectedItem is WhisperModelInfo selectedModel)
            {
                if (MessageBox.Show($"Are you sure you want to delete the model file '{selectedModel.Filename}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    try
                    {
                        WhisperModelManager.DeleteModel(selectedModel.Filename);
                        UpdateModelStatus();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to delete model: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
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
                btnDeleteModel.Visibility = Visibility.Collapsed;

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
                // Allow delete if file was created
                if (WhisperModelManager.IsModelDownloaded(selectedModel.Filename))
                {
                    btnDeleteModel.Visibility = Visibility.Visible;
                }
            }
            finally
            {
                var isDownloaded = WhisperModelManager.IsModelDownloaded(selectedModel.Filename);
                btnDownloadModel.IsEnabled = !isDownloaded;
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
