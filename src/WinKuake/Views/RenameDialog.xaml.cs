using System.Windows;
using System.Windows.Input;

namespace WinKuake.Views;

public partial class RenameDialog : Window
{
    public string Result { get; private set; } = "";

    public RenameDialog(string current)
    {
        InitializeComponent();
        TxtName.Text = current;
        Loaded += (_, _) => { TxtName.Focus(); TxtName.SelectAll(); };
    }

    private void Accept_Click(object sender, RoutedEventArgs e)
    {
        Result = TxtName.Text;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void TxtName_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Cancel_Click(sender, new RoutedEventArgs());
    }
}
