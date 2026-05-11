using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WinKuake.Services;

namespace WinKuake.Views;

/// <summary>
/// Overlay modal para búsqueda global multi-buffer. Recibe la lista de
/// fuentes (ya recolectadas por el host) y dispara <see cref="GlobalFindService.Search"/>
/// con debounce mientras el usuario escribe. La selección final se expone vía
/// <see cref="SelectedResult"/> + <c>DialogResult = true</c>.
/// </summary>
public partial class FindGlobalWindow : Window
{
    private readonly IReadOnlyList<GlobalFindSource> _sources;
    private readonly DispatcherTimer _debounce;

    /// <summary>Resultado elegido por el usuario (null si canceló).</summary>
    public FindResult? SelectedResult { get; private set; }

    public FindGlobalWindow(IReadOnlyList<GlobalFindSource> sources)
    {
        InitializeComponent();
        _sources = sources;

        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _debounce.Tick += (_, _) => { _debounce.Stop(); RunSearch(); };

        Loaded += (_, _) => SearchBox.Focus();
        SearchBox.TextChanged += (_, _) => { _debounce.Stop(); _debounce.Start(); };
        SearchBox.PreviewKeyDown += OnSearchKeyDown;
        ResultsList.PreviewKeyDown += OnSearchKeyDown;
        ResultsList.MouseDoubleClick += (_, _) => Commit();
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { DialogResult = false; Close(); } };
    }

    private void RunSearch()
    {
        var q = SearchBox.Text;
        var results = GlobalFindService.Search(_sources, q);
        var items = new List<FindResultItem>(results.Count);
        foreach (var r in results)
            items.Add(new FindResultItem(r, q));

        ResultsList.ItemsSource = items;
        if (items.Count > 0) ResultsList.SelectedIndex = 0;
        StatusLine.Text = string.IsNullOrWhiteSpace(q)
            ? "Buscando en todos los terminales abiertos."
            : $"{items.Count} resultado{(items.Count == 1 ? "" : "s")} para «{q}»";
    }

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Commit();
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            if (ResultsList.Items.Count == 0) return;
            ResultsList.SelectedIndex = Math.Min(ResultsList.SelectedIndex + 1, ResultsList.Items.Count - 1);
            ResultsList.ScrollIntoView(ResultsList.SelectedItem);
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            if (ResultsList.Items.Count == 0) return;
            ResultsList.SelectedIndex = Math.Max(ResultsList.SelectedIndex - 1, 0);
            ResultsList.ScrollIntoView(ResultsList.SelectedItem);
            e.Handled = true;
        }
    }

    private void Commit()
    {
        if (ResultsList.SelectedItem is FindResultItem item)
        {
            SelectedResult = item.Result;
            DialogResult = true;
            Close();
        }
    }
}

/// <summary>
/// ViewModel mostrado en la lista. Header = ubicación; Preview = línea con
/// match resaltado vía <see cref="Inlines"/> (consumido por el template).
/// </summary>
public sealed class FindResultItem
{
    public FindResult Result { get; }
    public string Header { get; }
    public string Preview { get; }
    public string Query { get; }

    public FindResultItem(FindResult result, string query)
    {
        Result = result;
        Query = query ?? "";
        Header = $"[{result.SessionLabel} · pane #{result.PaneIndex + 1}]  línea {result.LineNumber + 1}";
        Preview = result.LinePreview;
    }

    /// <summary>Construye los inlines del preview con el query resaltado en bold/amarillo.</summary>
    public IEnumerable<Inline> BuildInlines()
    {
        if (string.IsNullOrEmpty(Query)) { yield return new Run(Preview); yield break; }
        var src = Preview;
        var q = Query;
        int idx = 0;
        while (idx < src.Length)
        {
            var hit = src.IndexOf(q, idx, StringComparison.OrdinalIgnoreCase);
            if (hit < 0)
            {
                yield return new Run(src.Substring(idx));
                yield break;
            }
            if (hit > idx)
                yield return new Run(src.Substring(idx, hit - idx));
            var matched = src.Substring(hit, q.Length);
            yield return new Run(matched)
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(140, 193, 156, 0)),
                FontWeight = FontWeights.Bold,
            };
            idx = hit + q.Length;
        }
    }
}

/// <summary>
/// Attached property que aplica los inlines del <see cref="FindResultItem"/> al
/// <see cref="TextBlock"/> del template, sustituyendo el binding de Text.
/// </summary>
public static class FindResultPreview
{
    public static readonly DependencyProperty ItemProperty =
        DependencyProperty.RegisterAttached(
            "Item", typeof(FindResultItem), typeof(FindResultPreview),
            new PropertyMetadata(null, OnItemChanged));

    public static void SetItem(DependencyObject d, FindResultItem? value) => d.SetValue(ItemProperty, value);
    public static FindResultItem? GetItem(DependencyObject d) => (FindResultItem?)d.GetValue(ItemProperty);

    private static void OnItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock tb) return;
        tb.Inlines.Clear();
        if (e.NewValue is FindResultItem item)
            foreach (var inl in item.BuildInlines())
                tb.Inlines.Add(inl);
    }
}
