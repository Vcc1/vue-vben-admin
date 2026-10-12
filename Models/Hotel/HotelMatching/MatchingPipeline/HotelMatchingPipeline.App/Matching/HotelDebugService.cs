using HotelMatchingPipeline.App.Configuration;
using HotelMatchingPipeline.App.Contracts;
using HotelMatchingPipeline.App.Models;
using HotelMatchingPipeline.Domain.Hotels;
using Microsoft.Extensions.Options;

namespace HotelMatchingPipeline.App;

public sealed class HotelDebugService
{
    readonly IHotelMatchingRepository _repository;
    readonly HotelMatchEvaluator _evaluator;
    readonly IOptions<HotelMatchingOptions> _options;

    public HotelDebugService(
        IHotelMatchingRepository repository,
        HotelMatchEvaluator evaluator,
        IOptions<HotelMatchingOptions> options)
    {
        _repository = repository;
        _evaluator = evaluator;
        _options = options;
    }

    public async Task<HotelDebugAnalysis> AnalyzeAsync(HotelDebugRequest request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.HotelCode);
        if (request.SourceType == HotelDebugSourceType.Supplier)
        {
            if (!request.SupId.HasValue)
            {
                return new HotelDebugAnalysis(request, null, null, ["supId_required"]);
            }

            var supplier = await _repository.GetSupplierHotelAsync(request.SupId.Value, request.HotelCode, cancellationToken);
            if (supplier is null)
            {
                return new HotelDebugAnalysis(request, null, null, ["supplier_hotel_not_found"]);
            }

            if (!supplier.Hotel.IsQualityValid)
            {
                return new HotelDebugAnalysis(request, supplier.Hotel, HotelMatchDecision.DataProblem("supplier_quality_problem"), []);
            }

            var candidates = await _repository.RecallOtaCandidatesAsync(
                supplier,
                _options.Value.RecallRadiusMeters,
                _options.Value.MaxCandidates,
                cancellationToken);
            var aliases = await _repository.GetTrustedAliasesAsync(candidates.Select(x => x.HotelCode).ToArray(), cancellationToken);
            return new HotelDebugAnalysis(request, supplier.Hotel, _evaluator.Evaluate(supplier.Hotel, candidates, aliases), [])
            {
                RecalledCandidates = candidates
            };
        }

        var ota = await _repository.GetOtaHotelAsync(request.HotelCode, cancellationToken);
        if (ota is null)
        {
            return new HotelDebugAnalysis(request, null, null, ["ota_hotel_not_found"]);
        }

        var supplierCandidates = await _repository.RecallSupplierCandidatesAsync(
            ota,
            _options.Value.RecallRadiusMeters,
            _options.Value.MaxCandidates,
            cancellationToken);
        return new HotelDebugAnalysis(request, ota, null, [])
        {
            RecalledCandidates = supplierCandidates
        };
    }
}
