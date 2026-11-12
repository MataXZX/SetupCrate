using System.Windows;
using System.Windows.Input;

namespace SetupCrate;

public partial class LinkPickerWindow : Window
{
    public string? SelectedLink { get; private set; }

    public LinkPickerWindow(IReadOnlyList<string> links)
    {
        InitializeComponent();
        List.ItemsSource = links;
        if (links.Count > 0) List.SelectedIndex = 0;
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e) => Accept();

    private void List_MouseDoubleClick(object sender, MouseButtonEventArgs e) => Accept();

    private void Accept()
    {
        if (List.SelectedItem is not string link) return;
        SelectedLink = link;
        DialogResult = true;
    }
}
