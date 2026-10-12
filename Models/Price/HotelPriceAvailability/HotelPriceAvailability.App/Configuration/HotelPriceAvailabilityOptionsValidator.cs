using HotelPriceAvailability.Domain;
using Microsoft.Extensions.Options;

namespace HotelPriceAvailability.App.Configuration;

public sealed class HotelPriceAvailabilityOptionsValidator : IValidateOptions<HotelPriceAvailabilityOptions>
{
    public ValidateOptionsResult Validate(string? name, HotelPriceAvailabilityOptions options)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.BatchNo))
        {
            errors.Add("BatchNo is required.");
        }

        if (options.BatchSize <= 0)
        {
            errors.Add("BatchSize must be greater than zero.");
        }

        if (options.MaxConcurrency <= 0)
        {
            errors.Add("MaxConcurrency must be greater than zero.");
        }

        try
        {
            _ = PriceSearchPlan.Create(
                DateOnly.FromDateTime(DateTime.Today),
                options.DateWindows.Select(static item =>
                    new PriceWindowRule(item.StayDays, item.FromDay, item.ToDay)).ToArray(),
                options.Persons);
        }
        catch (ArgumentException ex)
        {
            errors.Add(ex.Message);
        }

        var duplicateSuppliers = options.SupplierOverrides
            .GroupBy(static item => item.SupId)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicateSuppliers is not null)
        {
            errors.Add($"SupplierOverrides contains duplicate SupId {duplicateSuppliers.Key}.");
        }

        foreach (var supplier in options.SupplierOverrides)
        {
            if (supplier.SupId <= 0 || supplier.MaxConcurrency <= 0 ||
                supplier.BatchSize <= 0 || supplier.RequestsPerSecond <= 0)
            {
                errors.Add($"Supplier override {supplier.SupId} contains non-positive values.");
            }
        }

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }
}
