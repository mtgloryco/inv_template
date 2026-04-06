using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Services;

namespace InventoryManagementSystem.UI.ViewModels
{
    public partial class SuppliersViewModel : ViewModelBase
    {
        private readonly SupplierService _supplierService;

        [ObservableProperty]
        private ObservableCollection<Supplier> _suppliers = new();

        [ObservableProperty]
        private Supplier _currentSupplier = new();

        [ObservableProperty]
        private Supplier? _selectedSupplier;

        [ObservableProperty]
        private SupplierPerformance _selectedSupplierPerformance = new();

        public SuppliersViewModel(SupplierService supplierService)
        {
            _supplierService = supplierService;
            LoadSuppliersCommand.Execute(null);
        }

        [RelayCommand]
        private async Task LoadSuppliers()
        {
            var list = await _supplierService.GetAllSuppliersAsync();
            Suppliers = new ObservableCollection<Supplier>(list);
        }

        [RelayCommand]
        private async Task SaveSupplier()
        {
            if (CurrentSupplier.Id == 0)
            {
                await _supplierService.AddSupplierAsync(CurrentSupplier);
            }
            else
            {
                await _supplierService.UpdateSupplierAsync(CurrentSupplier);
            }

            CurrentSupplier = new Supplier();
            await LoadSuppliers();
        }

        [RelayCommand]
        private void EditSupplier(Supplier supplier)
        {
            CurrentSupplier = new Supplier
            {
                Id = supplier.Id,
                Name = supplier.Name,
                ContactPerson = supplier.ContactPerson,
                Phone = supplier.Phone,
                Email = supplier.Email,
                Address = supplier.Address,
                DefaultLeadTimeDays = supplier.DefaultLeadTimeDays,
                PaymentTerms = supplier.PaymentTerms,
                Rating = supplier.Rating,
                IsActive = supplier.IsActive,
                CreatedAt = supplier.CreatedAt
            };
        }

        [RelayCommand]
        private async Task DeleteSupplier(Supplier supplier)
        {
            await _supplierService.DeleteSupplierAsync(supplier.Id);
            await LoadSuppliers();
        }

        partial void OnSelectedSupplierChanged(Supplier? value)
        {
            _ = LoadSupplierPerformanceAsync(value);
        }

        private async Task LoadSupplierPerformanceAsync(Supplier? supplier)
        {
            if (supplier == null)
            {
                SelectedSupplierPerformance = new SupplierPerformance();
                return;
            }

            SelectedSupplierPerformance = await _supplierService.GetSupplierPerformanceAsync(supplier.Id);
        }
    }
}
