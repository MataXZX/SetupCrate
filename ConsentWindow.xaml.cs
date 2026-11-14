using System.Windows;

namespace SetupCrate;

public partial class ConsentWindow : Window
{
    public bool Remember => ChkRemember.IsChecked == true;

    public ConsentWindow(string title, string body, string acceptText, string confirmText, string? rememberText)
    {
        InitializeComponent();

        TxtTitle.Text = title;
        TxtBody.Text = body;
        TxtAccept.Text = acceptText;
        if (string.IsNullOrWhiteSpace(rememberText))
        {
            ChkRemember.Visibility = Visibility.Collapsed;
        }
        else
        {
            ChkRemember.Content = rememberText;
        }
        BtnConfirm.Content = confirmText;
        BtnConfirm.IsEnabled = false;
    }

    private void ChkAccept_Changed(object sender, RoutedEventArgs e) =>
        BtnConfirm.IsEnabled = ChkAccept.IsChecked == true;

    private void BtnConfirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
