using System.Windows;
using GameLauncher.Services;

namespace GameLauncher.Views;

public partial class PixDonationDialog : Window
{
    private const string PixKey = "freitas.kleber@uol.com.br";

    public PixDonationDialog()
    {
        InitializeComponent();
        GenerateQrCode();
    }

    private void GenerateQrCode()
    {
        var payload = PixQrCodeService.BuildPixPayload(
            pixKey: PixKey,
            merchantName: "Kleber Freitas",
            merchantCity: "SAO PAULO",
            description: "GLauncher");

        QrCodeImage.Source = PixQrCodeService.GenerateQrCode(payload, moduleSize: 8, quietZone: 2);
    }

    private void CopyKey_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(PixKey);

        if (FindName("CopyText") is System.Windows.Controls.TextBlock tb)
        {
            tb.Text = "✓ Chave copiada!";
            tb.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#00E676"));

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            timer.Tick += (_, _) =>
            {
                tb.Text = "Copiar chave PIX";
                tb.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#AABBDD"));
                timer.Stop();
            };
            timer.Start();
        }
    }
}
