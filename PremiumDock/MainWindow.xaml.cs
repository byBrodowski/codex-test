using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace PremiumDock;

public partial class MainWindow : Window
{
    private const string StorageFileName = "dock-items.json";
    private readonly DockStorage _storage = new();

    public ObservableCollection<DockItem> Items { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        LoadItems();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        PositionDock();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PositionDock();
    }

    private void PositionDock()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + (workArea.Width - ActualWidth) / 2;
        Top = workArea.Bottom - ActualHeight - 12;
    }

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            DropOverlay.Opacity = 0.9;
            return;
        }

        e.Effects = DragDropEffects.None;
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        DropOverlay.Opacity = 0;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        DropOverlay.Opacity = 0;
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        var paths = (string[]?)e.Data.GetData(DataFormats.FileDrop);
        if (paths is null)
        {
            return;
        }

        var newItems = paths
            .Select(CreateDockItem)
            .Where(item => item is not null)
            .Cast<DockItem>()
            .ToList();

        if (newItems.Count == 0)
        {
            return;
        }

        foreach (var item in newItems)
        {
            if (Items.Any(existing => string.Equals(existing.SourcePath, item.SourcePath, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            Items.Add(item);
        }

        SaveItems();
    }

    private void OnLaunchClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not DockItem item)
        {
            return;
        }

        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = item.LaunchPath,
                UseShellExecute = true,
                WorkingDirectory = item.WorkingDirectory
            };
            System.Diagnostics.Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Не вдалося запустити: {ex.Message}", "Premium Dock", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadItems()
    {
        foreach (var item in _storage.LoadItems())
        {
            Items.Add(item);
        }
    }

    private void SaveItems()
    {
        _storage.SaveItems(Items);
    }

    private static DockItem? CreateDockItem(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return null;
        }

        var info = DockItemInfo.FromPath(path);
        if (info is null)
        {
            return null;
        }

        return new DockItem(info.DisplayName, info.SourcePath, info.LaunchPath, info.WorkingDirectory, info.Icon);
    }

    private sealed class DockStorage
    {
        private readonly string _storagePath;

        public DockStorage()
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PremiumDock");
            Directory.CreateDirectory(folder);
            _storagePath = Path.Combine(folder, StorageFileName);
        }

        public IEnumerable<DockItem> LoadItems()
        {
            if (!File.Exists(_storagePath))
            {
                return Array.Empty<DockItem>();
            }

            try
            {
                var json = File.ReadAllText(_storagePath);
                var items = JsonSerializer.Deserialize<List<DockItemRecord>>(json);
                if (items is null)
                {
                    return Array.Empty<DockItem>();
                }

                return items
                    .Select(record => DockItemInfo.FromPath(record.SourcePath))
                    .Where(info => info is not null)
                    .Select(info => new DockItem(info!.DisplayName, info.SourcePath, info.LaunchPath, info.WorkingDirectory, info.Icon))
                    .ToList();
            }
            catch
            {
                return Array.Empty<DockItem>();
            }
        }

        public void SaveItems(IEnumerable<DockItem> items)
        {
            var payload = items.Select(item => new DockItemRecord(item.SourcePath)).ToList();
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_storagePath, json);
        }
    }

    private sealed record DockItemRecord(string SourcePath);
}

public sealed record DockItem(string DisplayName, string SourcePath, string LaunchPath, string WorkingDirectory, ImageSource Icon);

internal sealed record DockItemInfo(string DisplayName, string SourcePath, string LaunchPath, string WorkingDirectory, ImageSource Icon)
{
    public static DockItemInfo? FromPath(string path)
    {
        var sourcePath = Path.GetFullPath(path);
        var displayName = Path.GetFileNameWithoutExtension(sourcePath);
        var workingDirectory = Directory.Exists(sourcePath)
            ? sourcePath
            : Path.GetDirectoryName(sourcePath) ?? Environment.CurrentDirectory;

        if (string.Equals(Path.GetExtension(sourcePath), ".lnk", StringComparison.OrdinalIgnoreCase))
        {
            var shortcut = ShortcutResolver.Resolve(sourcePath);
            if (shortcut is null)
            {
                return null;
            }

            displayName = shortcut.DisplayName;
            return new DockItemInfo(displayName, sourcePath, shortcut.TargetPath, shortcut.WorkingDirectory, shortcut.Icon);
        }

        if (Directory.Exists(sourcePath) || File.Exists(sourcePath))
        {
            return new DockItemInfo(displayName, sourcePath, sourcePath, workingDirectory, IconExtractor.Extract(sourcePath));
        }

        return null;
    }
}

internal sealed record ShortcutInfo(string DisplayName, string TargetPath, string WorkingDirectory, ImageSource Icon);

internal static class ShortcutResolver
{
    public static ShortcutInfo? Resolve(string shortcutPath)
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
            {
                return null;
            }

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(shortcutPath);

            string targetPath = shortcut.TargetPath;
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                return null;
            }

            string workingDir = shortcut.WorkingDirectory;
            var displayName = Path.GetFileNameWithoutExtension(shortcutPath);
            var iconPath = shortcut.IconLocation as string;
            var iconSource = IconExtractor.Extract(string.IsNullOrWhiteSpace(iconPath) ? targetPath : iconPath, targetPath);

            Marshal.FinalReleaseComObject(shortcut);
            Marshal.FinalReleaseComObject(shell);

            return new ShortcutInfo(displayName, targetPath, string.IsNullOrWhiteSpace(workingDir) ? Path.GetDirectoryName(targetPath) ?? Environment.CurrentDirectory : workingDir, iconSource);
        }
        catch
        {
            return null;
        }
    }
}

internal static class IconExtractor
{
    public static ImageSource Extract(string primaryPath, string? fallbackPath = null)
    {
        var iconPath = ExtractIconPath(primaryPath, fallbackPath);
        var icon = Icon.ExtractAssociatedIcon(iconPath) ?? SystemIcons.Application;
        return Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(32, 32));
    }

    private static string ExtractIconPath(string primaryPath, string? fallbackPath)
    {
        var cleanedPrimary = primaryPath.Split(',')[0].Trim();
        if (File.Exists(cleanedPrimary) || Directory.Exists(cleanedPrimary))
        {
            return cleanedPrimary;
        }

        if (!string.IsNullOrWhiteSpace(fallbackPath))
        {
            return fallbackPath;
        }

        return primaryPath;
    }
}
