using System.Globalization;
using System.Text.Json;

namespace ExpediaChaseRoomApp;

internal static class ExpediaChaseRoomPriceParser
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static ExpediaChaseRoomPriceParseResult Parse(
        ExpediaChaseRoomTask task,
        string rawContent,
        string signinEmailAddress,
        IReadOnlyDictionary<string, decimal> promoLabels)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            return ExpediaChaseRoomPriceParseResult.Empty;
        }

        var responseItems = JsonSerializer.Deserialize<List<ExpediaRoomAndRateResponseItem>>(rawContent, SerializerOptions);
        if (responseItems is null || responseItems.Count == 0)
        {
            return ExpediaChaseRoomPriceParseResult.Empty;
        }

        var prices = new List<ExpediaChaseRoomPriceRecord>();
        var chargeTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unknownPromoLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var roomAndRate in responseItems)
        {
            var listings = roomAndRate.Data?.PropertyOffers?.CategorizedListings;
            if (listings is null)
            {
                continue;
            }

            foreach (var item in listings)
            {
                var policyGroups = BuildPolicyGroups(item);
                var roomId = item.UnitId ?? string.Empty;
                var roomName = item.Header?.Text ?? string.Empty;
                var people = item.Features?
                    .FirstOrDefault(x => string.Equals(x.Graphic?.Id, "people", StringComparison.OrdinalIgnoreCase))?
                    .Text ?? string.Empty;

                foreach (var primarySelection in item.PrimarySelections ?? [])
                {
                    var charges = primarySelection.SecondarySelections?
                        .Where(x => x.TertiarySelections is { Count: > 0 })
                        .SelectMany(x => x.TertiarySelections!)
                        .Where(x => x.Enabled)
                        .ToList() ?? [];

                    foreach (var charge in charges)
                    {
                        if (!string.IsNullOrWhiteSpace(charge.Description))
                        {
                            chargeTitles.Add(charge.Description);
                        }
                    }

                    foreach (var ratePlan in primarySelection.PropertyUnit?.RatePlans ?? [])
                    {
                        if (ratePlan.PriceDetails is null || ratePlan.PriceDetails.Count == 0)
                        {
                            continue;
                        }

                        var currentCharges = charges
                            .Where(x => string.Equals(x.OptionId, ratePlan.Id, StringComparison.OrdinalIgnoreCase))
                            .Select(MapChargeOption)
                            .ToList();

                        var (discountAmount, marketingTotalPrice) = AnalyzeDiscountAmount(ratePlan, promoLabels, unknownPromoLabels);

                        foreach (var detail in ratePlan.PriceDetails.Where(x => !x.HotelCollect))
                        {
                            var totalPrice = detail.LodgingPrepareCheckout?.Action?.TotalPrice?.Amount ?? 0m;
                            var originalTotalPrice = totalPrice;
                            if (marketingTotalPrice.HasValue && totalPrice == marketingTotalPrice.Value)
                            {
                                totalPrice -= discountAmount;
                            }

                            var dto = new ExpediaChaseRoomPriceRecord
                            {
                                Id = ExpediaChaseRoomPriceRecordIdGenerator.Next(),
                                RoomId = roomId,
                                RoomName = roomName,
                                TotalPrice = totalPrice,
                                OriginalTotalPrice = originalTotalPrice,
                                CurrencyCode = detail.LodgingPrepareCheckout?.Action?.TotalPrice?.CurrencyInfo?.Code ?? string.Empty,
                                People = people,
                                Surplus = detail.Availability?.ScarcityMessage ?? string.Empty,
                                LodgingCategorizedUnit = currentCharges.FirstOrDefault()?.Description ?? string.Empty,
                                LodgingCategorizedUnits = currentCharges,
                                PropertyId = task.PropertyId,
                                Rooms = task.Rooms,
                                Adults = task.Adults,
                                CheckIn = task.CheckIn,
                                CheckOut = task.CheckOut,
                                OptionId = ratePlan.Id ?? string.Empty,
                                ExpediaChaseId = task.Id,
                                SigninEmailAddress = signinEmailAddress,
                                CreateTime = DateTime.Now
                            };

                            AttachPolicies(dto, ratePlan, policyGroups);
                            prices.Add(dto);
                        }
                    }
                }
            }
        }

        return new ExpediaChaseRoomPriceParseResult(prices, chargeTitles, unknownPromoLabels);
    }

    private static List<PolicyGroup> BuildPolicyGroups(CategorizedListing item)
    {
        var groups = new List<PolicyGroup>();

        if (item.SecondaryHeader?.ComplexDialog?.PolicyContent?.Policies is { Count: > 0 } secondaryPolicies)
        {
            foreach (var policy in secondaryPolicies)
            {
                var policyName = policy.Title?.Text ?? item.SecondaryHeader.ComplexDialog.PolicyContent.Title?.Text ?? string.Empty;
                var cancellationPolicies = new List<ExpediaChaseRoomPriceRefundPolicy>();
                foreach (var section in policy.MessageSections ?? [])
                {
                    cancellationPolicies.Add(CreatePolicy(policyName, section.PrimaryInfo?.Heading?.Text, section.PrimaryInfo?.SubHeading?.Text, section.SecondaryInfo?.Title?.Text, section.SecondaryInfo?.Message));
                }

                groups.Add(new PolicyGroup(policyName, cancellationPolicies));
            }
        }
        else
        {
            foreach (var highlightedMessage in item.HighlightedMessages ?? [])
            {
                var defaultPolicyName = highlightedMessage.PolicyContent?.Title?.Text ?? string.Empty;
                foreach (var policy in highlightedMessage.PolicyContent?.Policies ?? [])
                {
                    var policyName = policy.Title?.Text ?? defaultPolicyName;
                    var cancellationPolicies = new List<ExpediaChaseRoomPriceRefundPolicy>();
                    foreach (var section in policy.MessageSections ?? [])
                    {
                        cancellationPolicies.Add(CreatePolicy(policyName, section.PrimaryInfo?.Heading?.Text, section.PrimaryInfo?.SubHeading?.Text, section.SecondaryInfo?.Title?.Text, section.SecondaryInfo?.Message));
                    }

                    groups.Add(new PolicyGroup(policyName, cancellationPolicies));
                }
            }
        }

        return groups;
    }

    private static void AttachPolicies(
        ExpediaChaseRoomPriceRecord dto,
        RatePlan ratePlan,
        List<PolicyGroup> policyGroups)
    {
        if (policyGroups.Count == 0 || ratePlan.HighlightedMessages is null || ratePlan.HighlightedMessages.Count == 0)
        {
            return;
        }

        var trigger = ratePlan.HighlightedMessages[0].Trigger;
        var policyName = trigger?.Value;
        if (string.IsNullOrWhiteSpace(policyName))
        {
            return;
        }

        var candidates = policyGroups
            .Where(x => string.Equals(x.PolicyName, policyName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (candidates.Count == 0)
        {
            return;
        }

        var selectedPolicies = candidates[0].Policies;
        if (candidates.Count > 1)
        {
            selectedPolicies = ResolveDuplicatePolicies(dto, trigger, candidates);
        }

        foreach (var policy in selectedPolicies)
        {
            dto.Policies.Add(new ExpediaChaseRoomPriceRefundPolicy
            {
                Id = ExpediaChaseRoomPriceRecordIdGenerator.Next(),
                PropertyId = dto.PropertyId,
                ExpediaRoomPriceId = dto.Id,
                PolicyName = policy.PolicyName,
                PolicyEqualType = policy.PolicyEqualType,
                PolicyEqualTime = policy.PolicyEqualTime,
                PolicyDisplayName = policy.PolicyDisplayName,
                PolicyMessages = policy.PolicyMessages
            });
        }
    }

    private static List<ExpediaChaseRoomPriceRefundPolicy> ResolveDuplicatePolicies(
        ExpediaChaseRoomPriceRecord dto,
        RatePlanHighlightedMessageTrigger? trigger,
        List<PolicyGroup> candidates)
    {
        var equalTime = trigger?.SecondaryValue?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (equalTime is null || equalTime.Length <= 1)
        {
            return candidates[0].Policies;
        }

        var matchedGroups = candidates
            .Where(x => x.Policies.Any(p => string.Equals(p.PolicyEqualTime, equalTime[^1], StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (matchedGroups.Count > 1)
        {
            var priceFormat = string.Format(CultureInfo.CurrentCulture, "{0:C0}", dto.TotalPrice);
            var preciseMatch = matchedGroups
                .Where(x => x.Policies.Any(y => y.PolicyMessages.Contains(priceFormat, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            return (preciseMatch.Count > 0 ? preciseMatch : matchedGroups)[0].Policies;
        }

        return matchedGroups.Count > 0 ? matchedGroups[0].Policies : candidates[0].Policies;
    }

    private static (decimal DiscountAmount, decimal? MarketingTotalPrice) AnalyzeDiscountAmount(
        RatePlan ratePlan,
        IReadOnlyDictionary<string, decimal> promoLabels,
        ISet<string> unknownPromoLabels)
    {
        try
        {
            var dialog = ratePlan.MarketingSection?.FeeDialog;
            var content = dialog?.Content?.FirstOrDefault() ?? string.Empty;
            var amountMatch = System.Text.RegularExpressions.Regex.Match(content, @"estimated total of CNY\s([\d,]+\.\d+)");
            var amountText = amountMatch.Success ? amountMatch.Groups[1].Value : "0";

            var title = dialog?.Title ?? string.Empty;
            var discountMatch = System.Text.RegularExpressions.Regex.Match(title, @"\d{1,3}(?:,\d{3})*(?:\.\d{2})?");
            var discountText = discountMatch.Success ? discountMatch.Groups[0].Value : "0";

            var label = dialog?.Trigger?.Value;
            if (!string.IsNullOrWhiteSpace(label))
            {
                if (promoLabels.TryGetValue(label, out var ratio))
                {
                    discountText = (ParseDecimal(amountText) * (ratio / 100m)).ToString("F2", CultureInfo.InvariantCulture);
                }
                else
                {
                    unknownPromoLabels.Add(label);
                }
            }

            return (ParseDecimal(discountText), ParseDecimal(amountText));
        }
        catch
        {
            return (0m, 0m);
        }
    }

    private static decimal ParseDecimal(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0m;
        }

        return decimal.TryParse(
            value.Replace(",", string.Empty, StringComparison.Ordinal),
            NumberStyles.Number | NumberStyles.AllowCurrencySymbol,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : 0m;
    }

    private static ExpediaChaseRoomPriceRefundPolicy CreatePolicy(
        string policyName,
        string? policyEqualType,
        string? policyEqualTime,
        string? policyDisplayName,
        List<string>? messages)
    {
        return new ExpediaChaseRoomPriceRefundPolicy
        {
            PolicyName = policyName,
            PolicyEqualType = policyEqualType ?? string.Empty,
            PolicyEqualTime = policyEqualTime ?? string.Empty,
            PolicyDisplayName = policyDisplayName ?? string.Empty,
            PolicyMessages = messages is { Count: > 0 } ? string.Join("\n", messages) : string.Empty
        };
    }

    private static ExpediaChaseRoomChargeOption MapChargeOption(TertiarySelection option)
    {
        return new ExpediaChaseRoomChargeOption
        {
            Description = option.Description ?? string.Empty,
            OptionId = option.OptionId,
            SubText = option.SubText,
            Enabled = option.Enabled,
            Price = option.Price ?? string.Empty,
            Selected = option.Selected
        };
    }

    internal sealed record ExpediaChaseRoomPriceParseResult(
        IReadOnlyList<ExpediaChaseRoomPriceRecord> Prices,
        IReadOnlySet<string> ChargeTitles,
        IReadOnlySet<string> UnknownPromoLabels)
    {
        public static ExpediaChaseRoomPriceParseResult Empty { get; } = new([], new HashSet<string>(StringComparer.OrdinalIgnoreCase), new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }

    private sealed record PolicyGroup(string PolicyName, List<ExpediaChaseRoomPriceRefundPolicy> Policies);

    private sealed class ExpediaRoomAndRateResponseItem
    {
        public ExpediaData? Data { get; set; }

        public List<ExpediaError>? Errors { get; set; }
    }

    private sealed class ExpediaData
    {
        public PropertyOffers? PropertyOffers { get; set; }
    }

    private sealed class PropertyOffers
    {
        public List<CategorizedListing>? CategorizedListings { get; set; }
    }

    private sealed class ExpediaError
    {
        public string? Message { get; set; }
    }

    private sealed class CategorizedListing
    {
        public string? UnitId { get; set; }

        public ListingHeader? Header { get; set; }

        public List<ListingFeature>? Features { get; set; }

        public List<ListingHighlightedMessage>? HighlightedMessages { get; set; }

        public List<PrimarySelection>? PrimarySelections { get; set; }

        public SecondaryHeader? SecondaryHeader { get; set; }
    }

    private sealed class ListingHeader
    {
        public string? Text { get; set; }
    }

    private sealed class ListingFeature
    {
        public string? Text { get; set; }

        public ListingGraphic? Graphic { get; set; }
    }

    private sealed class ListingGraphic
    {
        public string? Id { get; set; }
    }

    private sealed class ListingHighlightedMessage
    {
        public HighlightedPolicyContent? PolicyContent { get; set; }
    }

    private sealed class HighlightedPolicyContent
    {
        public TextNode? Title { get; set; }

        public List<HighlightedPolicy>? Policies { get; set; }
    }

    private sealed class HighlightedPolicy
    {
        public TextNode? Title { get; set; }

        public List<MessageSection>? MessageSections { get; set; }
    }

    private sealed class SecondaryHeader
    {
        public ComplexDialog? ComplexDialog { get; set; }
    }

    private sealed class ComplexDialog
    {
        public ComplexDialogPolicyContent? PolicyContent { get; set; }
    }

    private sealed class ComplexDialogPolicyContent
    {
        public TextNode? Title { get; set; }

        public List<ComplexDialogPolicy>? Policies { get; set; }
    }

    private sealed class ComplexDialogPolicy
    {
        public TextNode? Title { get; set; }

        public List<MessageSection>? MessageSections { get; set; }
    }

    private sealed class MessageSection
    {
        public MessagePrimaryInfo? PrimaryInfo { get; set; }

        public MessageSecondaryInfo? SecondaryInfo { get; set; }
    }

    private sealed class MessagePrimaryInfo
    {
        public TextNode? Heading { get; set; }

        public TextNode? SubHeading { get; set; }
    }

    private sealed class MessageSecondaryInfo
    {
        public TextNode? Title { get; set; }

        public List<string>? Message { get; set; }
    }

    private sealed class TextNode
    {
        public string? Text { get; set; }
    }

    private sealed class PrimarySelection
    {
        public PropertyUnit? PropertyUnit { get; set; }

        public List<SecondarySelection>? SecondarySelections { get; set; }
    }

    private sealed class SecondarySelection
    {
        public List<TertiarySelection>? TertiarySelections { get; set; }
    }

    public sealed class TertiarySelection
    {
        public string? Description { get; set; }

        public string? OptionId { get; set; }

        public string? SubText { get; set; }

        public bool Enabled { get; set; }

        public string? Price { get; set; }

        public bool Selected { get; set; }
    }

    private sealed class PropertyUnit
    {
        public List<RatePlan>? RatePlans { get; set; }
    }

    private sealed class RatePlan
    {
        public string? Id { get; set; }

        public List<PriceDetail>? PriceDetails { get; set; }

        public List<RatePlanHighlightedMessage>? HighlightedMessages { get; set; }

        public MarketingSection? MarketingSection { get; set; }
    }

    private sealed class MarketingSection
    {
        public FeeDialog? FeeDialog { get; set; }
    }

    private sealed class FeeDialog
    {
        public string? Title { get; set; }

        public string[]? Content { get; set; }

        public FeeDialogTrigger? Trigger { get; set; }
    }

    private sealed class FeeDialogTrigger
    {
        public string? Value { get; set; }
    }

    private sealed class RatePlanHighlightedMessage
    {
        public RatePlanHighlightedMessageTrigger? Trigger { get; set; }
    }

    private sealed class RatePlanHighlightedMessageTrigger
    {
        public string? Value { get; set; }

        public string? SecondaryValue { get; set; }
    }

    private sealed class PriceDetail
    {
        public bool HotelCollect { get; set; }

        public Availability? Availability { get; set; }

        public LodgingPrepareCheckout? LodgingPrepareCheckout { get; set; }
    }

    private sealed class Availability
    {
        public string? ScarcityMessage { get; set; }
    }

    private sealed class LodgingPrepareCheckout
    {
        public LodgingAction? Action { get; set; }
    }

    private sealed class LodgingAction
    {
        public TotalPrice? TotalPrice { get; set; }
    }

    private sealed class TotalPrice
    {
        public decimal Amount { get; set; }

        public CurrencyInfo? CurrencyInfo { get; set; }
    }

    private sealed class CurrencyInfo
    {
        public string? Code { get; set; }
    }
}

internal static class ExpediaChaseRoomPriceRecordIdGenerator
{
    private static long _lastTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    private static int _sequence;
    private static readonly Lock SyncRoot = new();

    public static long Next()
    {
        lock (SyncRoot)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (timestamp == _lastTimestamp)
            {
                _sequence = (_sequence + 1) & 0xFFF;
                if (_sequence == 0)
                {
                    while ((_lastTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) <= timestamp)
                    {
                    }

                    timestamp = _lastTimestamp;
                }
            }
            else
            {
                _sequence = 0;
                _lastTimestamp = timestamp;
            }

            return (timestamp << 12) | (uint)_sequence;
        }
    }
}