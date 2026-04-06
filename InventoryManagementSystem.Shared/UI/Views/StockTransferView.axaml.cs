using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace InventoryManagementSystem.UI.Views;

public partial class StockTransferView : UserControl
{
    public StockTransferView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
