using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SystemAudioAnalyzer;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void btnStart_Click(object sender, RoutedEventArgs e)
    {
        btnStart.IsEnabled = false;
        btnStop.IsEnabled = true;
        txtStatus.Text = "Recording...";
    }

    private void btnStop_Click(object sender, RoutedEventArgs e)
    {
        btnStart.IsEnabled = true;
        btnStop.IsEnabled = false;
        txtStatus.Text = "Stopped";
    }
}