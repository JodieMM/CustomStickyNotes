using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CustomStickyNotes.Models;

namespace CustomStickyNotes.Views;

public partial class OverviewWindow : Window
{
    private readonly App _app;
    private string _searchQuery = string.Empty;

    public OverviewWindow(App app)
    {
        InitializeComponent();
        _app = app;
        RefreshList();
    }

    public void RefreshList()
    {
        ActiveListPanel.Children.Clear();
        ArchivedListPanel.Children.Clear();

        var notes = _app.GetAllNotesSnapshot()
            .Where(n => string.IsNullOrWhiteSpace(_searchQuery)
                        || n.PlainText.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(n => n.ModifiedAt)
            .ToList();

        var active = notes.Where(n => !n.IsArchived).ToList();
        var archived = notes.Where(n => n.IsArchived).ToList();

        foreach (var n in active) ActiveListPanel.Children.Add(BuildRow(n, isArchived: false));
        foreach (var n in archived) ArchivedListPanel.Children.Add(BuildRow(n, isArchived: true));

        ActiveEmptyLabel.Visibility = active.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ArchivedEmptyLabel.Visibility = archived.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private UIElement BuildRow(NoteModel note, bool isArchived)
    {
        var color = (Color)ColorConverter.ConvertFromString(note.ColorHex);
        var swatch = new Border
        {
            Width = 10,
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(color),
            Margin = new Thickness(0, 0, 8, 0),
        };

        var snippet = string.IsNullOrWhiteSpace(note.PlainText) ? "(empty note)" : note.PlainText;
        var snippetBlock = new TextBlock
        {
            Text = snippet,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
            MaxWidth = 160,
        };
        var dateBlock = new TextBlock
        {
            Text = note.ModifiedAt.ToLocalTime().ToString("MMM d, h:mm tt"),
            Foreground = Brushes.Gray,
            FontSize = 10,
        };

        var textPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        textPanel.Children.Add(snippetBlock);
        textPanel.Children.Add(dateBlock);

        var buttonsPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

        if (!isArchived)
        {
            var showBtn = new Button { Content = "Show", Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(4, 0, 0, 0) };
            showBtn.Click += (_, _) => _app.ShowNoteWindow(note.Id);

            var archiveBtn = new Button { Content = "Archive", Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(4, 0, 0, 0) };
            archiveBtn.Click += (_, _) =>
            {
                _app.ArchiveNote(note.Id);
                RefreshList();
            };

            buttonsPanel.Children.Add(showBtn);
            buttonsPanel.Children.Add(archiveBtn);
        }
        else
        {
            var restoreBtn = new Button { Content = "Restore", Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(4, 0, 0, 0) };
            restoreBtn.Click += (_, _) =>
            {
                _app.RestoreNote(note.Id);
                RefreshList();
            };

            var deleteBtn = new Button { Content = "Delete", Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(4, 0, 0, 0) };
            deleteBtn.Click += (_, _) =>
            {
                var result = MessageBox.Show(this, "Delete this note permanently? This can't be undone.",
                    "Delete Note", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
                if (result == MessageBoxResult.Yes)
                {
                    _app.DeleteNote(note.Id);
                    RefreshList();
                }
            };

            buttonsPanel.Children.Add(restoreBtn);
            buttonsPanel.Children.Add(deleteBtn);
        }

        var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(swatch, 0);
        Grid.SetColumn(textPanel, 1);
        Grid.SetColumn(buttonsPanel, 2);
        grid.Children.Add(swatch);
        grid.Children.Add(textPanel);
        grid.Children.Add(buttonsPanel);

        return new Border
        {
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(2),
            Child = grid,
        };
    }

    private void NewNote_Click(object sender, RoutedEventArgs e)
    {
        _app.CreateNewNote();
        RefreshList();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchQuery = SearchBox.Text;
        RefreshList();
    }
}
