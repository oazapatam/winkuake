using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using WinKuake.Services;

namespace WinKuake.Views;

/// <summary>
/// Paleta de comandos rápida: typing filtra; Enter inyecta el comando
/// seleccionado al pane activo; Shift+Enter inyecta+ejecuta.
/// </summary>
public partial class QuickCommandWindow : Window
{
    private readonly IReadOnlyList<CommandSnippet> _all;

    /// <summary>Comando seleccionado al cerrar con Enter. Null si el usuario cerró con Esc.</summary>
    public CommandSnippet? SelectedSnippet { get; private set; }

    /// <summary>True si el usuario quiere ejecutar (Enter normal añade \n); false = solo inyectar.</summary>
    public bool ExecuteAfterInject { get; private set; }

    public QuickCommandWindow(IReadOnlyList<CommandSnippet> snippets)
    {
        InitializeComponent();
        _all = snippets;
        ResultsList.ItemsSource = _all;
        if (_all.Count > 0) ResultsList.SelectedIndex = 0;

        Loaded += (_, _) => SearchBox.Focus();
        SearchBox.TextChanged += (_, _) => Refilter();
        SearchBox.PreviewKeyDown += OnSearchKeyDown;
        ResultsList.PreviewKeyDown += OnSearchKeyDown;
        ResultsList.MouseDoubleClick += (_, _) => Commit(executeAfter: false);

        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    private void Refilter()
    {
        var filtered = CommandSnippetService.Filter(_all, SearchBox.Text);
        ResultsList.ItemsSource = filtered;
        if (filtered.Count > 0) ResultsList.SelectedIndex = 0;
    }

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Commit(executeAfter: (Keyboard.Modifiers & ModifierKeys.Shift) != 0);
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            if (ResultsList.Items.Count == 0) return;
            ResultsList.SelectedIndex = System.Math.Min(ResultsList.SelectedIndex + 1, ResultsList.Items.Count - 1);
            ResultsList.ScrollIntoView(ResultsList.SelectedItem);
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            if (ResultsList.Items.Count == 0) return;
            ResultsList.SelectedIndex = System.Math.Max(ResultsList.SelectedIndex - 1, 0);
            ResultsList.ScrollIntoView(ResultsList.SelectedItem);
            e.Handled = true;
        }
    }

    private void Commit(bool executeAfter)
    {
        if (ResultsList.SelectedItem is CommandSnippet s)
        {
            SelectedSnippet = s;
            ExecuteAfterInject = executeAfter;
            DialogResult = true;
            Close();
        }
        else if (!string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            // Si no hay match, inyecta la query cruda como comando ad-hoc.
            SelectedSnippet = new CommandSnippet(SearchBox.Text, SearchBox.Text);
            ExecuteAfterInject = executeAfter;
            DialogResult = true;
            Close();
        }
    }
}
