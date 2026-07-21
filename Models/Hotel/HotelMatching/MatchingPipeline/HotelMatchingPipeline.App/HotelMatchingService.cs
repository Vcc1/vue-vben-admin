using System.Security.Cryptography;
using System.Text;
using HotelMatchingPipeline.App.Configuration;
using HotelMatchingPipeline.App.Contracts;
using HotelMatchingPipeline.App.Models;
using HotelMatchingPipeline.Domain.Hotels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HotelMatchingPipeline.App;

public sealed class HotelMatchingService
{
    readonly IHotelMatchingRepository _repository;
    readonly IHotelMatchDeliveryRepository _deliveryRepository;
    readonly IThirdPartyHotelMatchClient _deliveryClient;
    readonly HotelMatchEvaluator _evaluator;
    readonly IOptions<HotelMatchingOptions> _options;
    readonly ILogger<HotelMatchingService> _logger;

    public HotelMatchingService(
        IHotelMatchingRepository repository,
        IHotelMatchDeliveryRepository deliveryRepository,
        IThirdPartyHotelMatchClient deliveryClient,
        HotelMatchEvaluator evaluator,
        IOptions<HotelMatchingOptions> options,
        ILogger<HotelMatchingService> logger)
    {
        _repository = repository;
        _deliveryRepository = deliveryRepository;
        _deliveryClient = deliveryClient;
        _evaluator = evaluator;
        _options = options;
        _logger = logger;
    }

    public async Task<int> ProcessNextBatchAsync(CancellationToken cancellationToken)
    {
        var options = _options.Value;
        var workItems = await _repository.ClaimUnmatchedSupplierHotelsAsync(options.Version, options.BatchSize, cancellationToken);
        foreach (var workItem in workItems)
        {
            await ProcessOneAsync(workItem, options, cancellationToken);
        }

        return workItems.Count;
    }

    public async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var count = await ProcessNextBatchAsync(cancellationToken);
            if (count == 0)
            {
                await Task.Delay(_options.Value.IdleDelayMilliseconds, cancellationToken);
            }
        }
    }

    public async Task<HotelManualReviewResult> ConfirmAsync(
        HotelManualReviewRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SHotelCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MTHotelCode);
        var workItem = await _repository.GetSupplierHotelAsync(request.SupId, request.SHotelCode, cancellationToken);
        if (workItem is null)
        {
            return new HotelManualReviewResult(false, "notFound", "supplier_hotel_not_found");
        }

        if (!workItem.Hotel.IsQualityValid)
        {
            return new HotelManualReviewResult(false, "dataProblem", "supplier_quality_problem");
        }

        var ota = await _repository.GetOtaHotelAsync(request.MTHotelCode!, cancellationToken);
        if (ota is null || !ota.IsQualityValid)
        {
            return new HotelManualReviewResult(false, "invalidCandidate", "ota_hotel_not_found_or_invalid");
        }

        var receipt = await DeliverAsync(workItem, request.MTHotelCode!, _options.Value.Version, cancellationToken);
        var decision = new HotelMatchDecision(
            receipt.Success ? HotelMatchDecisionKind.AutoMatched : HotelMatchDecisionKind.NoMatch,
            receipt.Success ? request.MTHotelCode : null,
            [],
            ["manual_confirm"]);
        await _repository.CompleteMatchAsync(
            workItem,
            decision,
            _options.Value.Version,
            receipt.Error,
            receipt.Success ? null : DateTimeOffset.Now.AddMinutes(15),
            cancellationToken);
        return new HotelManualReviewResult(receipt.Success, receipt.Success ? "delivered" : "deliveryFailed", receipt.Error);
    }

    public async Task<HotelManualReviewResult> RejectAsync(
        HotelManualReviewRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SHotelCode);
        var workItem = await _repository.GetSupplierHotelAsync(request.SupId, request.SHotelCode, cancellationToken);
        if (workItem is null)
        {
            return new HotelManualReviewResult(false, "notFound", "supplier_hotel_not_found");
        }

        await _repository.CompleteMatchAsync(
            workItem,
            new HotelMatchDecision(HotelMatchDecisionKind.NoMatch, null, [], ["manual_reject", request.Reason ?? string.Empty]),
            _options.Value.Version,
            null,
            null,
            cancellationToken);
        return new HotelManualReviewResult(true, "rejected", null);
    }

    async Task ProcessOneAsync(
        HotelMatchWorkItem workItem,
        HotelMatchingOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!workItem.Hotel.IsQualityValid || !string.IsNullOrWhiteSpace(workItem.Hotel.MtHotelCode))
            {
                var blocked = workItem.Hotel.IsQualityValid
                    ? new HotelMatchDecision(HotelMatchDecisionKind.NoMatch, null, [], ["already_has_mt_hotel_code"])
                    : HotelMatchDecision.DataProblem("supplier_quality_problem");
                await _repository.CompleteMatchAsync(workItem, blocked, options.Version, null, null, cancellationToken);
                return;
            }

            var otaCandidates = await _repository.RecallOtaCandidatesAsync(
                workItem,
                options.RecallRadiusMeters,
                options.MaxCandidates,
                cancellationToken);
            var aliases = await _repository.GetTrustedAliasesAsync(
                otaCandidates.Select(x => x.HotelCode).ToArray(),
                cancellationToken);
            var decision = _evaluator.Evaluate(workItem.Hotel, otaCandidates, aliases);

            if (decision.Kind == HotelMatchDecisionKind.AutoMatched && decision.MTHotelCode is not null)
            {
                var receipt = await DeliverAsync(workItem, decision.MTHotelCode, options.Version, cancellationToken);
                if (!receipt.Success)
                {
                    await _repository.CompleteMatchAsync(
                        workItem,
                        new HotelMatchDecision(HotelMatchDecisionKind.NoMatch, null, decision.Candidates, ["delivery_failed"]),
                        options.Version,
                        receipt.Error,
                        DateTimeOffset.Now.AddMinutes(15),
                        cancellationToken);
                    return;
                }
            }

            await _repository.CompleteMatchAsync(workItem, decision, options.Version, null, null, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Hotel matching failed. SupId={SupId}, SHotelCode={SHotelCode}", workItem.Hotel.SupplierId, workItem.Hotel.HotelCode);
            await _repository.CompleteMatchAsync(
                workItem,
                new HotelMatchDecision(HotelMatchDecisionKind.NoMatch, null, [], ["processing_failed"]),
                options.Version,
                ex.Message,
                DateTimeOffset.Now.AddMinutes(15),
                cancellationToken);
        }
    }

    async Task<HotelMatchDeliveryReceipt> DeliverAsync(
        HotelMatchWorkItem workItem,
        string mtHotelCode,
        int matchingVersion,
        CancellationToken cancellationToken)
    {
        var supId = workItem.Hotel.SupplierId ?? throw new InvalidOperationException("Supplier hotel is missing SupId.");
        var idempotencyKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{supId}\u001F{workItem.Hotel.HotelCode}\u001F{workItem.NormalizedDataHash}\u001F{matchingVersion}")));
        var command = new HotelMatchDeliveryCommand(
            supId,
            workItem.Hotel.HotelCode,
            mtHotelCode,
            workItem.SourceDataVersion,
            workItem.NormalizedDataHash,
            workItem.CleanVersion,
            matchingVersion,
            idempotencyKey);

        if (!await _deliveryRepository.TryCreateAsync(command, cancellationToken))
        {
            return new HotelMatchDeliveryReceipt(true, "already_recorded", null);
        }

        await _deliveryRepository.MarkSendingAsync(idempotencyKey, cancellationToken);
        var receipt = await _deliveryClient.DeliverAsync(command, cancellationToken);
        await _deliveryRepository.MarkCompletedAsync(
            idempotencyKey,
            receipt,
            receipt.Success ? null : DateTimeOffset.Now.AddMinutes(15),
            cancellationToken);
        return receipt;
    }
}
