using System.Windows;

namespace GameLauncher.Views;

public partial class LoadingProgressDialog : Window
{
    public LoadingProgressDialog(string gameName)
    {
        InitializeComponent();
        TitleText.Text = $"Buscando informações de '{gameName}'...";
    }

    public void UpdateProgress(double percent, string stepDescription)
    {
        ProgressGauge.Value = percent;
        StepText.Text = stepDescription;
    }

    public void Finish()
    {
        DialogResult = true;
        Close();
    }
}
