using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace InventoryManagementSystem.UI.Views;

public partial class AuditTrailView : UserControl
{
    public AuditTrailView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
