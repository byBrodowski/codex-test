using System.Windows.Media;

namespace PremiumDock;

public sealed class DockItem
{
    public DockItem(string path, string displayName, ImageSource icon)
    {
        Path = path;
        DisplayName = displayName;
        Icon = icon;
    }

    public string Path { get; }
    public string DisplayName { get; }
    public ImageSource Icon { get; }
}
