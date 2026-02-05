using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
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

public partial class MainWindow : Window, INotifyPropertyChanged
{
    public ObservableCollection<DockItem> DockItems { get; } = new();
    private bool _isDropActive;

    public bool IsDropActive
    {
        get => _isDropActive;
        set
        {
            if (_isDropActive == value)
            {
                return;
            }

            _isDropActive = value;
            OnPropertyChanged(nameof(IsDropActive));
        }
    }

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        AllowDrop = true;
        Drop += OnDrop;
        DragOver += OnDragOver;
        DragLeave += OnDragLeave;
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
        var canDrop = e.Data.GetDataPresent(DataFormats.FileDrop);
        IsDropActive = canDrop;
        e.Effects = canDrop ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        IsDropActive = false;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        IsDropActive = false;
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        foreach (var path in files)
        {
            if (!File.Exists(path) && !Directory.Exists(path))
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

        var displayName = Directory.Exists(path)
            ? new DirectoryInfo(path).Name
            : Path.GetFileNameWithoutExtension(path);
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

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

internal static class IconUtilities
{
    private const uint FileAttributeDirectory = 0x10;
    private const uint FileAttributeNormal = 0x80;
    private const uint ShgfiIcon = 0x100;
    private const uint ShgfiLargeIcon = 0x0;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        out SHFILEINFO psfi,
        uint cbFileInfo,
        uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    public static ImageSource? GetIcon(string path)
    {
        try
        {
            var attributes = Directory.Exists(path) ? FileAttributeDirectory : FileAttributeNormal;
            var flags = ShgfiIcon | ShgfiLargeIcon;
            var result = SHGetFileInfo(path, attributes, out var info, (uint)Marshal.SizeOf<SHFILEINFO>(), flags);

            if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero)
            {
                return null;
            }

            var source = Imaging.CreateBitmapSourceFromHIcon(
                info.hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(64, 64));
            DestroyIcon(info.hIcon);
            source.Freeze();
            return source;
        }
        catch
        {
            return null;
        }
    }
}
