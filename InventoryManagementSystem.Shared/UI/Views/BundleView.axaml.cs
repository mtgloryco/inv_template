using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace InventoryManagementSystem.UI.Views;

public partial class BundleView : UserControl
{
    public BundleView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
