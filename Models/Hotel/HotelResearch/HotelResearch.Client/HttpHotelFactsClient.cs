using HotelResearch.Contracts;
using System.Net;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HotelResearch.Client;

public sealed class HttpHotelFactsClient(HttpClient httpClient) : IHotelFactsClient
{
    static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public async Task<HotelFactsResult?> ResolveAsync(
        string mtHotelCode,
        HotelResearchResolveMode mode = HotelResearchResolveMode.ReuseOrRefresh,
        CancellationToken cancellationToken = default)
    {
        var response = await SendUntilCompletedAsync(
            () => new HttpRequestMessage(HttpMethod.Post, "api/hotel-research/resolve")
            {
                Content = JsonContent.Create(
                    new HotelResearchResolveRequest { MTHotelCode = mtHotelCode, Mode = mode },
                    options: JsonOptions)
            },
            cancellationToken);
        if (response is null)
        {
            return null;
        }

        using (response)
        {
            var research = await response.Content.ReadFromJsonAsync<HotelResearchResult>(
                JsonOptions,
                cancellationToken)
                ?? throw new InvalidOperationException("Hotel research API returned an empty response.");
            return MapFacts(research);
        }
    }

    public async Task<HotelFactsResult?> GetAsync(
        string mtHotelCode,
        int? resultVersion = null,
        CancellationToken cancellationToken = default)
    {
        var code = Uri.EscapeDataString(mtHotelCode.Trim());
        var path = resultVersion is null
            ? $"api/hotel-facts/{code}"
            : $"api/hotel-facts/{code}/versions/{resultVersion.Value}";
        var response = await SendUntilCompletedAsync(
            () => new HttpRequestMessage(HttpMethod.Get, path),
            cancellationToken);
        if (response is null)
        {
            return null;
        }

        using (response)
        {
            return await response.Content.ReadFromJsonAsync<HotelFactsResult>(
                JsonOptions,
                cancellationToken)
                ?? throw new InvalidOperationException("Hotel facts API returned an empty response.");
        }
    }

    async Task<HttpResponseMessage?> SendUntilCompletedAsync(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken)
    {
        var attempt = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var request = requestFactory();
                var response = await httpClient.SendAsync(request, cancellationToken);
                if (response.StatusCode is HttpStatusCode.NotFound)
                {
                    response.Dispose();
                    return null;
                }

                if (response.IsSuccessStatusCode)
                {
                    return response;
                }

                response.Dispose();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (HttpRequestException)
            {
            }

            attempt++;
            await Task.Delay(RetryDelay(attempt), cancellationToken);
        }
    }

    static HotelFactsResult MapFacts(HotelResearchResult research)
        => new()
        {
            MTHotelCode = research.MTHotelCode,
            SourceResultVersion = research.ResultVersion,
            SourceRunId = research.RunId,
            ResearchedAt = research.ResearchedAt,
            Processor = research.Processor,
            DatabaseHotel = research.DatabaseHotel,
            ConfirmedSites = research.Sites,
            Sources = research.Sources
        };

    static TimeSpan RetryDelay(int attempt)
        => TimeSpan.FromSeconds(attempt switch
        {
            <= 1 => 1,
            2 => 2,
            3 => 5,
            4 => 10,
            _ => 30
        });

    static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
