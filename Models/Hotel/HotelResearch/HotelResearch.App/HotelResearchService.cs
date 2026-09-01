using AgentPlatform.Contracts;
using HotelBusiness.App;
using HotelBusiness.Contracts;
using HotelResearch.Contracts;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace HotelResearch.App;

public sealed class HotelResearchService(
    IHotelResearchSeedReader seedReader,
    IAgentService agentService,
    IHotelBusinessStore businessStore,
    IHotelOperationConcurrencyGuard concurrencyGuard) : IHotelResearchService
{
    const string AgentName = "hotel-research";
    const string ProcessorVersion = "1.3.1";
    const string PromptVersion = "3.1.0";
    const int ResultSchemaVersion = 2;

    const string JsonSchema = """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "sites": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "properties": {
                  "siteName": { "type": "string" },
                  "siteUrl": { "type": "string" },
                  "sourceUrls": { "type": "array", "items": { "type": "string" } },
                  "hotelNameChinese": { "type": ["string", "null"] },
                  "hotelNameEnglish": { "type": ["string", "null"] },
                  "hotelNameLocal": { "type": ["string", "null"] },
                  "aliases": { "type": "array", "items": { "type": "string" } },
                  "longitude": { "type": ["number", "null"] },
                  "latitude": { "type": ["number", "null"] },
                  "starRating": { "type": ["number", "null"] },
                  "hotelPhone": { "type": ["string", "null"] },
                  "addressChinese": { "type": ["string", "null"] },
                  "addressEnglish": { "type": ["string", "null"] },
                  "addressLocal": { "type": ["string", "null"] },
                  "identityVerification": {
                    "type": "object",
                    "additionalProperties": false,
                    "properties": {
                      "status": { "type": "string", "enum": ["confirmed", "uncertain", "rejected"] },
                      "matchedSeedName": { "type": ["string", "null"] },
                      "matchedCandidateName": { "type": ["string", "null"] },
                      "nameMatchKind": { "type": ["string", "null"] },
                      "matchedSignals": {
                        "type": "array",
                        "description": "Identity evidence written in Simplified Chinese.",
                        "items": { "type": "string" }
                      },
                      "conflicts": {
                        "type": "array",
                        "description": "Identity conflicts written in Simplified Chinese.",
                        "items": { "type": "string" }
                      },
                      "coordinateDistanceMeters": { "type": ["number", "null"] },
                      "summary": {
                        "type": "string",
                        "description": "Final hotel identity explanation written in Simplified Chinese."
                      }
                    },
                    "required": [
                      "status", "matchedSeedName", "matchedCandidateName", "nameMatchKind",
                      "matchedSignals", "conflicts", "coordinateDistanceMeters", "summary"
                    ]
                  },
                  "rooms": {
                    "type": "array",
                    "items": {
                      "type": "object",
                      "additionalProperties": false,
                      "properties": {
                        "roomNameChinese": {
                          "type": ["string", "null"],
                          "description": "Chinese text copied from the published room name; never translated."
                        },
                        "roomNameEnglish": {
                          "type": ["string", "null"],
                          "description": "English text copied from the published room name; never translated."
                        },
                        "roomNameLocal": {
                          "type": ["string", "null"],
                          "description": "Other-language or inseparable mixed-language published room name; never translated."
                        },
                        "area": { "type": ["string", "null"] },
                        "bedType": { "type": ["string", "null"] },
                        "isNonSmoking": { "type": ["boolean", "null"] },
                        "viewEnglish": { "type": ["string", "null"] },
                        "facilities": { "type": "array", "items": { "type": "string" } },
                        "offersAndServices": { "type": "array", "items": { "type": "string" } }
                      },
                      "required": [
                        "roomNameChinese", "roomNameEnglish", "roomNameLocal", "area", "bedType",
                        "isNonSmoking", "viewEnglish", "facilities", "offersAndServices"
                      ]
                    }
                  }
                },
                "required": [
                  "siteName", "siteUrl", "sourceUrls", "hotelNameChinese", "hotelNameEnglish",
                  "hotelNameLocal", "aliases", "longitude", "latitude", "starRating", "hotelPhone",
                  "addressChinese", "addressEnglish", "addressLocal", "identityVerification", "rooms"
                ]
              }
            }
          },
          "required": ["sites"]
        }
        """;

    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public async Task<HotelResearchResult?> ResearchAsync(
        HotelResearchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var mtHotelCode = HotelBusinessService.NormalizeHotelCode(request.MTHotelCode);

        var seed = await seedReader.FindAsync(mtHotelCode, cancellationToken);
        if (seed is null)
        {
            return null;
        }

        using var operationLease = concurrencyGuard.TryAcquire(
            mtHotelCode,
            HotelBusinessOperations.HotelResearch)
            ?? throw new HotelOperationAlreadyRunningException(
                mtHotelCode,
                HotelBusinessOperations.HotelResearch);

        var initialProcessor = CreateProcessor(provider: null, model: null);
        var run = await businessStore.StartRunAsync(
            new HotelOperationRunStart
            {
                MTHotelCode = mtHotelCode,
                HotelName = seed.HotelName,
                HotelNameEnglish = seed.HotelNameEnglish,
                OperationType = HotelBusinessOperations.HotelResearch,
                ConversationId = request.ConversationId,
                Processor = initialProcessor
            },
            cancellationToken);

        try
        {
            var response = await agentService.RunAsync(
                new AgentRequest
                {
                    AgentName = AgentName,
                    ConversationId = run.ConversationId,
                    EnableWebSearch = true,
                    Message = BuildPrompt(seed),
                    StorageScope = run.StorageScope,
                    Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["capability"] = HotelBusinessOperations.HotelResearch,
                        ["mtHotelCode"] = mtHotelCode,
                        ["runId"] = run.RunId,
                        ["processorVersion"] = ProcessorVersion,
                        ["promptVersion"] = PromptVersion
                    },
                    JsonResponseFormat = new AgentJsonResponseFormat
                    {
                        Name = "hotel_research_sites",
                        Description = "Public hotel and room information grouped by source website.",
                        Schema = JsonSchema
                    }
                },
                cancellationToken);

            HotelResearchPayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<HotelResearchPayload>(response.Text, JsonOptions);
            }
            catch (JsonException exception)
            {
                throw new HotelResearchExecutionException(
                    "MAF returned hotel research data that does not match the required JSON schema.",
                    exception);
            }

            if (payload is null)
            {
                throw new HotelResearchExecutionException("MAF returned an empty hotel research result.");
            }

            var processor = CreateProcessor(response.Provider, response.Model);
            var candidates = NormalizeSites(payload.Sites);
            var verified = VerifySites(seed, candidates);
            return await businessStore.CompleteRunAsync(
                run,
                processor,
                (resultVersion, completedAt) => new HotelResearchResult
                {
                    MTHotelCode = mtHotelCode,
                    RunId = run.RunId,
                    ResultVersion = resultVersion,
                    ConversationId = run.ConversationId,
                    StartedAt = run.StartedAt,
                    ResearchedAt = completedAt,
                    Processor = processor,
                    DatabaseHotel = seed,
                    Sites = verified.Confirmed,
                    RejectedCandidates = verified.Rejected,
                    SearchQueries = response.SearchQueries,
                    Sources = response.Sources,
                    Usage = response.Usage
                },
                cancellationToken);
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            await businessStore.FinishRunWithoutResultAsync(
                run,
                HotelOperationRunStatus.Cancelled,
                exception,
                CancellationToken.None);
            throw;
        }
        catch (Exception exception)
        {
            await businessStore.FinishRunWithoutResultAsync(
                run,
                HotelOperationRunStatus.Failed,
                exception,
                CancellationToken.None);
            throw;
        }
    }

    public async Task<HotelResearchResult?> ResolveAsync(
        HotelResearchResolveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var mtHotelCode = HotelBusinessService.NormalizeHotelCode(request.MTHotelCode);
        var attempt = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var seed = await seedReader.FindAsync(mtHotelCode, cancellationToken);
                if (seed is null)
                {
                    return null;
                }

                if (request.Mode is not HotelResearchResolveMode.RefreshAlways)
                {
                    var reused = await TryReadResearchResultAsync(mtHotelCode, null, cancellationToken);
                    if (reused is not null)
                    {
                        return reused;
                    }

                    if (request.Mode is HotelResearchResolveMode.ReuseOnly)
                    {
                        throw new HotelResearchResultUnavailableException(mtHotelCode);
                    }
                }

                return await ResearchAsync(
                    new HotelResearchRequest { MTHotelCode = mtHotelCode },
                    cancellationToken);
            }
            catch (HotelResearchResultUnavailableException)
            {
                throw;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                attempt++;
                var delay = RetryDelay(attempt);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    public async Task<HotelFactsResult?> GetFactsAsync(
        string mtHotelCode,
        int? resultVersion = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = HotelBusinessService.NormalizeHotelCode(mtHotelCode);
        if (resultVersion is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(resultVersion), "Result version must be positive.");
        }

        var result = await TryReadResearchResultAsync(
            normalizedCode,
            resultVersion,
            cancellationToken);
        return result is null ? null : CreateFacts(result);
    }

    static HotelFactsResult CreateFacts(HotelResearchResult result)
        => new()
        {
            MTHotelCode = result.MTHotelCode,
            SourceResultVersion = result.ResultVersion,
            SourceRunId = result.RunId,
            ResearchedAt = result.ResearchedAt,
            Processor = result.Processor,
            DatabaseHotel = result.DatabaseHotel,
            ConfirmedSites = result.Sites,
            Sources = result.Sources
        };

    async Task<HotelResearchResult?> TryReadResearchResultAsync(
        string mtHotelCode,
        int? resultVersion,
        CancellationToken cancellationToken)
    {
        try
        {
            var json = await businessStore.GetResultAsync(
                mtHotelCode,
                HotelBusinessOperations.HotelResearch,
                resultVersion,
                cancellationToken);
            if (json is null)
            {
                return null;
            }

            var result = json.Value.Deserialize<HotelResearchResult>(JsonOptions);
            return result is not null
                && result.ResultVersion > 0
                && string.Equals(result.MTHotelCode, mtHotelCode, StringComparison.OrdinalIgnoreCase)
                    ? result
                    : null;
        }
        catch (Exception exception) when (exception is JsonException or HotelBusinessStorageException)
        {
            return null;
        }
    }

    static TimeSpan RetryDelay(int attempt)
        => TimeSpan.FromSeconds(attempt switch
        {
            <= 1 => 1,
            2 => 2,
            3 => 5,
            4 => 10,
            _ => 30
        });

    static HotelProcessorDescriptor CreateProcessor(string? provider, string? model)
        => new()
        {
            Name = AgentName,
            ProcessorVersion = ProcessorVersion,
            PromptVersion = PromptVersion,
            ResultSchemaVersion = ResultSchemaVersion,
            AgentName = AgentName,
            Provider = provider,
            Model = model,
            WebSearchEnabled = true
        };

    static string BuildPrompt(HotelResearchDatabaseHotel seed)
        => $$"""
            酒店资料搜索 {{seed.MTHotelCode}}

            You are researching one specific hotel using public web pages. The JSON below is trusted internal
            RoomMapping OTAHotelInfo seed data used only to identify the hotel. MTHotelCode is an internal code and
            should not be searched as if it were a public hotel identifier.

            Identity verification is a mandatory first phase. Search the hotel's official website first, then useful
            OTA/property pages such as Agoda, Booking.com, Expedia, Trip.com, and other credible sites. Before using a
            page, prove that it describes the exact property in the seed. Return one item per candidate website,
            including uncertain or rejected candidates for audit, and keep every site's facts separate. Use the exact
            hotel detail page as siteUrl and include every consulted detail page in sourceUrls.

            Hotel identity rules:
            - Hotel names are important evidence, but an exact name match is not mandatory. The same physical hotel
              may be renamed, rebranded, translated, transliterated, or published with a brand suffix/prefix. A
              difference such as "by Nexta", Hotel, Resort, Apartment, Residence, Suites, 酒店, 公寓, or a branch word
              is a risk signal to investigate, not an automatic rejection.
            - An explicit official-site rename, former-name statement, or redirect from the seeded official hotel
              website to the current hotel detail page is decisive identity evidence when the destination still
              describes the seeded property.
            - Otherwise, confirm a differing name only with at least two independent strong property signals, such as
              the same hotel-owned phone, the same complete street address, coordinates within 200 metres, the same
              official website host, or an authoritative page explicitly linking the old and current names.
            - Exact or very similar names alone are insufficient. Shared addresses or nearby coordinates can belong
              to multiple hotels in one complex, so look for phone, official-site, branch, and property-type evidence.
            - Reject a candidate when evidence identifies another physical property. Use uncertain when the evidence
              is incomplete or contradictory. You may use official rename evidence to confirm that OTA pages using
              the same new name refer to the same property, but keep each site's published hotel and room facts
              separate.
            - Set identityVerification.status to your final confirmed, uncertain, or rejected decision and explain
              the evidence. Your identity decision is authoritative: the application does not perform a second name,
              address, coordinate, phone, or website matching decision.

            Identity explanation language:
            - Write identityVerification.summary, matchedSignals, and conflicts in Simplified Chinese. Keep published
              hotel names, addresses, quoted evidence, URLs, and identifiers in their original language; do not
              translate those source values merely to make the explanation Chinese.

            Collect as much explicitly published information as possible: names, aliases, coordinates, star rating,
            hotel-owned phone number, addresses, and room details. Never return an OTA/platform customer-service phone
            number. A phone is allowed only when the page clearly presents it as the hotel's own number. Do not infer,
            translate, merge across sites, or invent missing values.

            Put English names/addresses only in English fields, Chinese only in Chinese fields, and the destination's
            non-English/non-Chinese language only in local fields. If no distinct value is published, use null. For
            rooms, record published room names, area, bed type, smoking policy, view in English, facilities, and added
            offers/services. Classify room-name text into exactly three language fields without translating:
            - roomNameChinese: only Chinese text copied from the published room name.
            - roomNameEnglish: only English text copied from the published room name.
            - roomNameLocal: other-language text, or the complete original mixed-language name when it cannot be
              safely separated into equivalent Chinese and English labels.
            If one published room name contains both Chinese and English, split it only when the Chinese segment and
            English segment describe approximately the same room, for example “豪华大床房 / Deluxe King Room”. Remove
            only the separator around the two segments. Preserve each segment's original wording and never translate,
            paraphrase, complete, or infer a missing language. If the segments carry materially different qualifiers,
            do not pretend they are equivalents; keep the complete original value in roomNameLocal and set the other
            two name fields to null. Never duplicate the same published text into multiple language fields. Use null
            or an empty array whenever the site does not provide a value.

            Internal identity seed:
            {{JsonSerializer.Serialize(seed, JsonOptions)}}
            """;

    static IReadOnlyList<HotelResearchSite> NormalizeSites(IReadOnlyList<HotelResearchSite>? sites)
    {
        if (sites is null)
        {
            return [];
        }

        var results = new List<HotelResearchSite>();
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var site in sites)
        {
            if (string.IsNullOrWhiteSpace(site.SiteName)
                || !TryNormalizeHttpUrl(site.SiteUrl, out var siteUrl)
                || !seenUrls.Add(siteUrl))
            {
                continue;
            }

            var sourceUrls = (site.SourceUrls ?? [])
                .Select(url => TryNormalizeHttpUrl(url, out var normalized) ? normalized : null)
                .Where(url => url is not null)
                .Select(url => url!)
                .Append(siteUrl)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            results.Add(site with
            {
                SiteName = site.SiteName.Trim(),
                SiteUrl = siteUrl,
                SourceUrls = sourceUrls,
                Aliases = CleanValues(site.Aliases),
                IdentityVerification = NormalizeIdentityVerification(site.IdentityVerification),
                Rooms = (site.Rooms ?? []).Select(room => room with
                {
                    Facilities = CleanValues(room.Facilities),
                    OffersAndServices = CleanValues(room.OffersAndServices)
                }).ToArray()
            });
        }

        return results;
    }

    static VerifiedSites VerifySites(
        HotelResearchDatabaseHotel seed,
        IReadOnlyList<HotelResearchSite> candidates)
    {
        var confirmed = new List<HotelResearchSite>();
        var rejected = new List<HotelResearchRejectedCandidate>();
        foreach (var candidate in candidates)
        {
            var verification = candidate.IdentityVerification ?? new HotelResearchIdentityVerification
            {
                Status = "uncertain",
                MatchedSignals = [],
                Conflicts = ["搜索 Agent 未返回酒店身份判定"],
                Summary = "缺少 AI 身份判定，未将该候选站点归入目标酒店。"
            };
            if (string.Equals(verification.Status, "confirmed", StringComparison.Ordinal))
            {
                confirmed.Add(candidate with { IdentityVerification = verification });
                continue;
            }

            rejected.Add(new HotelResearchRejectedCandidate
            {
                SiteName = candidate.SiteName,
                SiteUrl = candidate.SiteUrl,
                CandidateNames = GetCandidateNames(candidate),
                IdentityVerification = verification
            });
        }

        return new VerifiedSites(confirmed, rejected);
    }

    static IReadOnlyList<string> GetCandidateNames(HotelResearchSite candidate)
        => CleanValues([
            candidate.HotelNameChinese,
            candidate.HotelNameEnglish,
            candidate.HotelNameLocal,
            .. candidate.Aliases
        ]);

    static HotelResearchIdentityVerification? NormalizeIdentityVerification(
        HotelResearchIdentityVerification? verification)
        => verification is null
            ? null
            : verification with
            {
                Status = verification.Status?.Trim().ToLowerInvariant() ?? "uncertain",
                MatchedSignals = CleanValues(verification.MatchedSignals),
                Conflicts = CleanValues(verification.Conflicts),
                Summary = verification.Summary?.Trim() ?? string.Empty
            };

    static IReadOnlyList<string> CleanValues(IEnumerable<string?>? values)
        => (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    static bool TryNormalizeHttpUrl(string? value, out string normalized)
    {
        if (Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https")
        {
            normalized = uri.AbsoluteUri;
            return true;
        }

        normalized = string.Empty;
        return false;
    }

    sealed record HotelResearchPayload
    {
        public IReadOnlyList<HotelResearchSite> Sites { get; init; } = [];
    }

    sealed record VerifiedSites(
        IReadOnlyList<HotelResearchSite> Confirmed,
        IReadOnlyList<HotelResearchRejectedCandidate> Rejected);
}
