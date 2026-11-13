using System.Windows;

using SetupCrate.Services;

namespace SetupCrate;

public partial class InputDialog : Window
{
    public string Value => TxtValue.Text.Trim();

    public InputDialog(string prompt, string initial = "")
    {
        InitializeComponent();
        TxtPrompt.Text = prompt;
        TxtValue.Text = initial;
        Loaded += (_, _) =>
        {
            TxtValue.Focus();
            TxtValue.SelectAll();
        };
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtValue.Text))
        {
            MessageBox.Show(Loc.T("Название не может быть пустым."), "SetupCrate",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }
}
