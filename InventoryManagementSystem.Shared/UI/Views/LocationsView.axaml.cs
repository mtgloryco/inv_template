using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace InventoryManagementSystem.UI.Views;

public partial class LocationsView : UserControl
{
    public LocationsView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
