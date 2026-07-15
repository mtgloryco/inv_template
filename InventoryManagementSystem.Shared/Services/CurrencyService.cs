using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;

namespace InventoryManagementSystem.Services
{
    public class CurrencyService
    {
        private readonly DatabaseService _databaseService;

        public CurrencyService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task<List<ExchangeRate>> GetAllRatesAsync()
        {
            return await _databaseService.Connection.Table<ExchangeRate>()
                .OrderByDescending(r => r.EffectiveDate)
                .ThenBy(r => r.FromCurrency)
                .ToListAsync();
        }

        public async Task SaveRateAsync(ExchangeRate rate)
        {
            rate.FromCurrency = rate.FromCurrency.Trim().ToUpperInvariant();
            rate.ToCurrency = rate.ToCurrency.Trim().ToUpperInvariant();
            if (rate.Id == 0)
            {
                await _databaseService.Connection.InsertAsync(rate);
            }
            else
            {
                await _databaseService.Connection.UpdateAsync(rate);
            }
        }

        public async Task DeleteRateAsync(int id)
        {
            var rate = await _databaseService.Connection.FindAsync<ExchangeRate>(id);
            if (rate != null)
            {
                await _databaseService.Connection.DeleteAsync(rate);
            }
        }

        public async Task<decimal> GetRateAsync(string fromCurrency, string toCurrency, DateTime? asOf = null)
        {
            fromCurrency = fromCurrency.Trim().ToUpperInvariant();
            toCurrency = toCurrency.Trim().ToUpperInvariant();

            if (fromCurrency == toCurrency)
            {
                return 1m;
            }

            var date = (asOf ?? DateTime.Today).Date;
            var direct = await _databaseService.Connection.Table<ExchangeRate>()
                .Where(r => r.FromCurrency == fromCurrency && r.ToCurrency == toCurrency && r.EffectiveDate <= date)
                .OrderByDescending(r => r.EffectiveDate)
                .FirstOrDefaultAsync();

            if (direct != null && direct.Rate > 0)
            {
                return direct.Rate;
            }

            var inverse = await _databaseService.Connection.Table<ExchangeRate>()
                .Where(r => r.FromCurrency == toCurrency && r.ToCurrency == fromCurrency && r.EffectiveDate <= date)
                .OrderByDescending(r => r.EffectiveDate)
                .FirstOrDefaultAsync();

            if (inverse != null && inverse.Rate > 0)
            {
                return 1m / inverse.Rate;
            }

            throw new InvalidOperationException($"No exchange rate found for {fromCurrency} → {toCurrency} as of {date:yyyy-MM-dd}.");
        }

        public async Task<decimal> ConvertAsync(decimal amount, string fromCurrency, string toCurrency, DateTime? asOf = null)
        {
            var rate = await GetRateAsync(fromCurrency, toCurrency, asOf);
            return Math.Round(amount * rate, 2);
        }

        public async Task<(decimal? ConvertedAmount, string Label)> TryFormatBaseEquivalentAsync(
            decimal amount,
            string documentCurrency,
            string baseCurrency,
            DateTime? asOf = null)
        {
            documentCurrency = documentCurrency.Trim().ToUpperInvariant();
            baseCurrency = baseCurrency.Trim().ToUpperInvariant();

            if (documentCurrency == baseCurrency)
            {
                return (amount, string.Empty);
            }

            try
            {
                var converted = await ConvertAsync(amount, documentCurrency, baseCurrency, asOf);
                return (converted, $"≈ {converted:N2} {baseCurrency}");
            }
            catch (InvalidOperationException)
            {
                return (null, $"No rate: {documentCurrency} → {baseCurrency}");
            }
        }

        public async Task<(decimal? ConvertedAmount, string Label)> TryFormatFromBaseAsync(
            decimal baseAmount,
            string targetCurrency,
            string baseCurrency,
            DateTime? asOf = null)
        {
            targetCurrency = targetCurrency.Trim().ToUpperInvariant();
            baseCurrency = baseCurrency.Trim().ToUpperInvariant();

            if (targetCurrency == baseCurrency)
            {
                return (baseAmount, string.Empty);
            }

            try
            {
                var converted = await ConvertAsync(baseAmount, baseCurrency, targetCurrency, asOf);
                return (converted, $"≈ {converted:N2} {targetCurrency}");
            }
            catch (InvalidOperationException)
            {
                return (null, $"No rate: {baseCurrency} → {targetCurrency}");
            }
        }
    }
}
