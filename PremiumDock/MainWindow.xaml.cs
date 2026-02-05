using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace PremiumDock;

public partial class MainWindow : Window
{
    public ObservableCollection<DockItem> DockItems { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        AllowDrop = true;
        Drop += OnDrop;
        DragOver += OnDragOver;
        Loaded += (_, _) => PositionWindow();
        SystemEvents.DisplaySettingsChanged += (_, _) => PositionWindow();
    }

    private void PositionWindow()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + (workArea.Width - Width) / 2;
        Top = workArea.Bottom - Height - 24;
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        foreach (var path in files)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            AddDockItem(path);
        }
    }

    private void AddDockItem(string path)
    {
        if (DockItems.Any(item => string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var displayName = Path.GetFileNameWithoutExtension(path);
        var icon = IconUtilities.GetIcon(path) ?? new BitmapImage();
        DockItems.Add(new DockItem(path, displayName, icon));
    }

    private void OnDockItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not DockItem item)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(item.Path)
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не вдалося відкрити: {item.Path}\n{ex.Message}", "Premium Dock");
        }
    }
}

internal static class IconUtilities
{
    public static ImageSource? GetIcon(string path)
    {
        try
        {
            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (icon == null)
            {
                return null;
            }

            return Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(64, 64));
        }
        catch
        {
            return null;
        }
    }
}
