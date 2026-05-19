using Provider = Infrastructure.Web.Taap;

namespace ExpediaChaseRoomApp;

internal sealed class ProviderExpediaTaapClientAdapter : IExpediaTaapClient
{
    private readonly Provider.IExpediaTaapClient _client;

    public ProviderExpediaTaapClientAdapter(Provider.IExpediaTaapClient client)
    {
        _client = client;
    }

    public async Task<ExpediaTaapPriceQueryResult> QueryPricesAsync(
        int accountId,
        ExpediaTaapLoginState loginState,
        ExpediaTaapPriceQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _client.QueryPricesAsync(
                accountId,
                new Provider.ExpediaTaapLoginState
                {
                    CookieHeader = loginState.CookieHeader,
                    Sha256Hash = loginState.Sha256Hash,
                    ClientInfoToken = loginState.ClientInfoToken,
                    Duaid = loginState.Duaid,
                    Eapid = loginState.Eapid,
                    ExpiresAt = loginState.ExpiresAt
                },
                new Provider.ExpediaTaapPriceQueryRequest
                {
                    PropertyId = request.PropertyId,
                    CheckIn = request.CheckIn,
                    CheckOut = request.CheckOut,
                    Adults = request.Adults,
                    Rooms = request.Rooms,
                    RoomOccupancies = request.RoomOccupancies?.Select(
                        occupancy => new Provider.ExpediaTaapRoomOccupancy
                        {
                            Adults = occupancy.Adults,
                            ChildrenAges = occupancy.ChildrenAges
                        }).ToList(),
                    Sha256Hash = request.Sha256Hash,
                    Locale = request.Locale,
                    Currency = request.Currency
                },
                cancellationToken);

            return new ExpediaTaapPriceQueryResult
            {
                PropertyId = result.PropertyId,
                CheckIn = result.CheckIn,
                CheckOut = result.CheckOut,
                RawContent = result.RawContent,
                RequestContent = result.RequestContent
            };
        }
        catch (Provider.ExpediaTaapLoginStateExpiredException ex)
        {
            throw new ExpediaTaapLoginStateExpiredException(
                ex.Message,
                ex.StatusCode,
                ex.ResponseContent,
                ex.ShouldRefreshSharedSha256Hash);
        }
    }
}

internal sealed class ProviderExpediaTaapAuthenticationServiceAdapter : IExpediaTaapAuthenticationService
{
    private readonly Provider.IExpediaTaapAuthenticationService _authenticationService;

    public ProviderExpediaTaapAuthenticationServiceAdapter(Provider.IExpediaTaapAuthenticationService authenticationService)
    {
        _authenticationService = authenticationService;
    }

    public async Task<ExpediaTaapLoginState> LoginAsync(
        ExpediaTaapCredential credential,
        bool captureSharedSha256Hash = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _authenticationService.LoginAsync(
            new Provider.ExpediaTaapCredential
            {
                SigninEmailAddress = credential.SigninEmailAddress,
                SigninPassword = credential.SigninPassword
            },
            captureSharedSha256Hash,
            cancellationToken);

        return MapLoginState(result);
    }

    public async Task<ExpediaTaapLoginState> RefreshSharedSha256HashAsync(
        ExpediaTaapCredential credential,
        ExpediaTaapLoginState? existingLoginState = null,
        CancellationToken cancellationToken = default)
    {
        Provider.ExpediaTaapLoginState? providerLoginState = null;
        if (existingLoginState is not null)
        {
            providerLoginState = new Provider.ExpediaTaapLoginState
            {
                CookieHeader = existingLoginState.CookieHeader,
                Sha256Hash = existingLoginState.Sha256Hash,
                ClientInfoToken = existingLoginState.ClientInfoToken,
                Duaid = existingLoginState.Duaid,
                Eapid = existingLoginState.Eapid,
                ExpiresAt = existingLoginState.ExpiresAt
            };
        }

        var result = await _authenticationService.RefreshSharedSha256HashAsync(
            new Provider.ExpediaTaapCredential
            {
                SigninEmailAddress = credential.SigninEmailAddress,
                SigninPassword = credential.SigninPassword
            },
            providerLoginState,
            cancellationToken);

        return MapLoginState(result);
    }

    private static ExpediaTaapLoginState MapLoginState(Provider.ExpediaTaapLoginState result)
    {
        return new ExpediaTaapLoginState
        {
            CookieHeader = result.CookieHeader,
            Sha256Hash = result.Sha256Hash,
            ClientInfoToken = result.ClientInfoToken,
            Duaid = result.Duaid,
            Eapid = result.Eapid,
            ExpiresAt = result.ExpiresAt
        };
    }
}
