using System.Windows;

namespace SetupCrate;

public partial class ReportWindow : Window
{
    private readonly string _name;

    public string ReasonTitle { get; private set; } = "";

    public string Comment => TxtComment.Text.Trim();

    public bool HidePack => ChkHide.IsChecked == true;

    public ReportWindow(string name, string author, string id)
    {
        InitializeComponent();

        _name = name;
        TxtReportTitle.Text = string.Format("Пожаловаться на сборку «{0}»", name);
        TxtReportMeta.Text = string.Format("Автор: {0} • ID: {1}", author, id);
    }

    private void Reason_Checked(object sender, RoutedEventArgs e)
    {
        if (BtnSend == null) return;

        ReasonTitle = sender switch
        {
            _ when ReferenceEquals(sender, R1) => "Подозрение на вирус / вредоносную ссылку",
            _ when ReferenceEquals(sender, R2) => "Пиратское ПО (кряк, репак, активатор)",
            _ when ReferenceEquals(sender, R3) => "Ссылка ведёт не на официальный сайт",
            _ when ReferenceEquals(sender, R4) => "Ссылки не работают (ошибка скачивания)",
            _ => "Неприемлемое название или спам"
        };

        BtnSend.IsEnabled = true;
    }

    private void BtnSend_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ReasonTitle)) return;
        DialogResult = true;
    }
}
