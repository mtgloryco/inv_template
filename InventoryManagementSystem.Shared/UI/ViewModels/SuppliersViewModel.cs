using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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
        private readonly InventoryService _inventoryService;

        [ObservableProperty]
        private ObservableCollection<Supplier> _suppliers = new();

        [ObservableProperty]
        private Supplier _currentSupplier = new();

        [ObservableProperty]
        private Supplier? _selectedSupplier;

        [ObservableProperty]
        private SupplierPerformance _selectedSupplierPerformance = new();

        [ObservableProperty]
        private ObservableCollection<Product> _products = new();

        [ObservableProperty]
        private ObservableCollection<Product> _supplierProducts = new();

        [ObservableProperty]
        private Product? _selectedProductToLink;

        [ObservableProperty]
        private bool _isDeleteConfirmationOpen;

        [ObservableProperty]
        private Supplier? _supplierToDelete;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private Avalonia.Media.Imaging.Bitmap? _selectedSupplierLogoBitmap;

        [ObservableProperty]
        private Avalonia.Media.Imaging.Bitmap? _currentLogoBitmap;

        [ObservableProperty]
        private string? _tempLogoFilePath;

        [ObservableProperty]
        private bool _hasWebsite;

        [ObservableProperty]
        private bool _isFormVisible;

        public ObservableCollection<string> SupplierTypes { get; } = new() { "Company", "Individual" };
        public ObservableCollection<string> PaymentTermsOptions { get; } = new()
        {
            "Direct Payment",
            "Installment",
            "Payment on Delivery",
            "Other"
        };

        public SuppliersViewModel(SupplierService supplierService, InventoryService inventoryService)
        {
            _supplierService = supplierService;
            _inventoryService = inventoryService;
            LoadSuppliersCommand.Execute(null);
            _ = LoadAllProductsAsync();
        }

        [RelayCommand]
        private async Task LoadSuppliers()
        {
            var list = await _supplierService.GetAllSuppliersAsync();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var query = SearchText.ToLower();
                list = list.Where(s =>
                    s.Name.ToLower().Contains(query) ||
                    s.ContactPerson.ToLower().Contains(query) ||
                    s.Phone.ToLower().Contains(query) ||
                    s.Email.ToLower().Contains(query) ||
                    s.Address.ToLower().Contains(query) ||
                    (s.TinNumber != null && s.TinNumber.ToLower().Contains(query)) ||
                    (s.WebsiteUrl != null && s.WebsiteUrl.ToLower().Contains(query))
                ).ToList();
            }

            Suppliers = new ObservableCollection<Supplier>(list);
        }

        private async Task LoadAllProductsAsync()
        {
            var list = await _inventoryService.GetAllProductsAsync();
            var purchasable = list.Where(p => p.CanBePurchased).OrderBy(p => p.Name).ToList();
            Products = new ObservableCollection<Product>(purchasable);
        }

        [RelayCommand]
        private async Task SaveSupplier()
        {
            if (!string.IsNullOrEmpty(TempLogoFilePath))
            {
                var extension = Path.GetExtension(TempLogoFilePath);
                var newFileName = $"{Guid.NewGuid()}{extension}";
                var logosDir = GetLogosDirectory();
                var destPath = Path.Combine(logosDir, newFileName);

                try
                {
                    Directory.CreateDirectory(logosDir);
                    File.Copy(TempLogoFilePath, destPath, true);

                    // Delete old logo file if exists
                    if (!string.IsNullOrEmpty(CurrentSupplier.LogoFileName))
                    {
                        var oldPath = Path.Combine(logosDir, CurrentSupplier.LogoFileName);
                        if (File.Exists(oldPath))
                        {
                            File.Delete(oldPath);
                        }
                    }

                    CurrentSupplier.LogoFileName = newFileName;
                }
                catch { /* Ignore logo copy errors */ }
            }

            if (CurrentSupplier.Id == 0)
            {
                await _supplierService.AddSupplierAsync(CurrentSupplier);
            }
            else
            {
                await _supplierService.UpdateSupplierAsync(CurrentSupplier);
            }

            CurrentSupplier = new Supplier();
            TempLogoFilePath = null;
            CurrentLogoBitmap = null;
            IsFormVisible = false;
            await LoadSuppliers();
        }

        [RelayCommand]
        private void EditSupplier(Supplier? supplier)
        {
            if (supplier == null)
            {
                CurrentSupplier = new Supplier();
                TempLogoFilePath = null;
                CurrentLogoBitmap = null;
                return;
            }
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
                SupplierType = supplier.SupplierType,
                TinNumber = supplier.TinNumber,
                WebsiteUrl = supplier.WebsiteUrl,
                LogoFileName = supplier.LogoFileName,
                Rating = supplier.Rating,
                IsActive = supplier.IsActive,
                CreatedAt = supplier.CreatedAt
            };
            TempLogoFilePath = null;
            UpdateCurrentLogo(supplier.LogoFileName);
            IsFormVisible = true;
        }

        [RelayCommand]
        private void ShowAddSupplierForm()
        {
            CurrentSupplier = new Supplier();
            TempLogoFilePath = null;
            CurrentLogoBitmap = null;
            IsFormVisible = true;
        }

        [RelayCommand]
        private void CloseForm()
        {
            IsFormVisible = false;
        }

        [RelayCommand]
        private void ConfirmDeleteSupplier(Supplier? supplier)
        {
            if (supplier == null) return;
            SupplierToDelete = supplier;
            IsDeleteConfirmationOpen = true;
        }

        [RelayCommand]
        private void CancelDelete()
        {
            IsDeleteConfirmationOpen = false;
            SupplierToDelete = null;
        }

        [RelayCommand]
        private async Task ExecuteDelete()
        {
            if (SupplierToDelete == null) return;
            
            // Delete logo file if exists
            if (!string.IsNullOrEmpty(SupplierToDelete.LogoFileName))
            {
                var logosDir = GetLogosDirectory();
                var path = Path.Combine(logosDir, SupplierToDelete.LogoFileName);
                if (File.Exists(path))
                {
                    try { File.Delete(path); } catch {}
                }
            }

            await _supplierService.DeleteSupplierAsync(SupplierToDelete.Id);
            IsDeleteConfirmationOpen = false;
            SupplierToDelete = null;
            SelectedSupplier = null; // Clear selection
            await LoadSuppliers();
        }

        [RelayCommand]
        private async Task LinkProduct()
        {
            if (SelectedSupplier == null || SelectedProductToLink == null) return;
            await _supplierService.LinkProductToSupplierAsync(SelectedSupplier.Id, SelectedProductToLink.Id);
            SelectedProductToLink = null;
            await LoadSupplierProductsAsync(SelectedSupplier);
        }

        [RelayCommand]
        private async Task UnlinkProduct(Product? product)
        {
            if (SelectedSupplier == null || product == null) return;
            await _supplierService.UnlinkProductFromSupplierAsync(SelectedSupplier.Id, product.Id);
            await LoadSupplierProductsAsync(SelectedSupplier);
        }

        [RelayCommand]
        private async Task UploadLogo()
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is not Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop || desktop.MainWindow == null)
                return;

            var topLevel = desktop.MainWindow;
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Select Supplier Logo",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new Avalonia.Platform.Storage.FilePickerFileType("Images")
                    {
                        Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp" }
                    }
                }
            });

            if (files.Count > 0)
            {
                var file = files[0];
                var localPath = file.Path.LocalPath;
                if (File.Exists(localPath))
                {
                    TempLogoFilePath = localPath;
                    try
                    {
                        CurrentLogoBitmap = new Avalonia.Media.Imaging.Bitmap(localPath);
                    }
                    catch
                    {
                        CurrentLogoBitmap = null;
                    }
                }
            }
        }

        [RelayCommand]
        private void RemoveLogo()
        {
            TempLogoFilePath = null;
            CurrentLogoBitmap = null;
            CurrentSupplier.LogoFileName = string.Empty;
        }

        [RelayCommand]
        private void OpenWebsite()
        {
            if (SelectedSupplier == null || string.IsNullOrWhiteSpace(SelectedSupplier.WebsiteUrl)) return;
            try
            {
                var url = SelectedSupplier.WebsiteUrl;
                if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                {
                    url = "https://" + url;
                }

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch { /* Ignore process launch errors */ }
        }

        partial void OnSelectedSupplierChanged(Supplier? value)
        {
            _ = LoadSupplierPerformanceAsync(value);
            _ = LoadSupplierProductsAsync(value);
            UpdateSelectedSupplierLogo();
            HasWebsite = value != null && !string.IsNullOrWhiteSpace(value.WebsiteUrl);
        }

        private string GetLogosDirectory()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var path = Path.Combine(appData, "InventoryManagementSystem", "SupplierLogos");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            return path;
        }

        private void UpdateSelectedSupplierLogo()
        {
            if (SelectedSupplier == null || string.IsNullOrEmpty(SelectedSupplier.LogoFileName))
            {
                SelectedSupplierLogoBitmap = null;
                return;
            }

            var logosDir = GetLogosDirectory();
            var path = Path.Combine(logosDir, SelectedSupplier.LogoFileName);
            if (File.Exists(path))
            {
                try
                {
                    SelectedSupplierLogoBitmap = new Avalonia.Media.Imaging.Bitmap(path);
                }
                catch
                {
                    SelectedSupplierLogoBitmap = null;
                }
            }
            else
            {
                SelectedSupplierLogoBitmap = null;
            }
        }

        private void UpdateCurrentLogo(string? fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                CurrentLogoBitmap = null;
                return;
            }

            var logosDir = GetLogosDirectory();
            var path = Path.Combine(logosDir, fileName);
            if (File.Exists(path))
            {
                try
                {
                    CurrentLogoBitmap = new Avalonia.Media.Imaging.Bitmap(path);
                }
                catch
                {
                    CurrentLogoBitmap = null;
                }
            }
            else
            {
                CurrentLogoBitmap = null;
            }
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

        private async Task LoadSupplierProductsAsync(Supplier? supplier)
        {
            if (supplier == null)
            {
                SupplierProducts = new ObservableCollection<Product>();
                return;
            }

            var list = await _supplierService.GetSupplierProductsAsync(supplier.Id);
            SupplierProducts = new ObservableCollection<Product>(list);
        }

        partial void OnSearchTextChanged(string value)
        {
            LoadSuppliersCommand.Execute(null);
        }
    }
}


