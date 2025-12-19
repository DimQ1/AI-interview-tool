using System.Windows;
using SystemAudioAnalyzer.Services;

namespace SystemAudioAnalyzer
{
    public partial class SettingsWindow : Window
    {
        public AppSettings Settings { get; private set; }

        public SettingsWindow(AppSettings currentSettings)
        {
            InitializeComponent();
            Settings = new AppSettings
            {
                ApiKey = currentSettings.ApiKey,
                Model = currentSettings.Model,
                AnalysisPrompt = currentSettings.AnalysisPrompt,
                TranslationPrompt = currentSettings.TranslationPrompt
            };

            txtApiKey.Text = Settings.ApiKey;
            txtModel.Text = Settings.Model;
            txtAnalysisPrompt.Text = Settings.AnalysisPrompt;
            txtTranslationPrompt.Text = Settings.TranslationPrompt;
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
            DialogResult = false;
            Close();
        }
    }
}
