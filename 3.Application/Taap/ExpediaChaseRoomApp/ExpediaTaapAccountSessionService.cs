using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Infrastructure.Sql.SupCacheDB;
using Infrastructure.Sql.SupCacheDB.Entitys;
using Infrastructure.Web.Taap;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExpediaChaseRoomApp;

public sealed class ExpediaTaapAccountSessionService : IExpediaTaapAccountSessionService
{
    private static readonly JsonSerializerOptions LoginStateJsonSerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly IMemoryCache _memoryCache;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IExpediaTaapAuthenticationService _authenticationService;
    private readonly IOptionsMonitor<ExpediaTaapOptions> _optionsMonitor;
    private readonly ExpediaChaseRoomMetrics _metrics;
    private readonly ILogger<ExpediaTaapAccountSessionService> _logger;
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _locks = new();
    private readonly string _cacheDirectory;

    public ExpediaTaapAccountSessionService(
        IMemoryCache memoryCache,
        IServiceScopeFactory scopeFactory,
        IExpediaTaapAuthenticationService authenticationService,
        IOptionsMonitor<ExpediaTaapOptions> optionsMonitor,
        ExpediaChaseRoomMetrics metrics,
        ILogger<ExpediaTaapAccountSessionService> logger)
    {
        _memoryCache = memoryCache;
        _scopeFactory = scopeFactory;
        _authenticationService = authenticationService;
        _optionsMonitor = optionsMonitor;
        _metrics = metrics;
        _logger = logger;
        _cacheDirectory = Path.Combine(AppContext.BaseDirectory, "config");
    }

    public async Task<ExpediaTaapLoginState> GetLoginStateAsync(
        ExpediaTaapAccountWorkerContext account,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        var options = _optionsMonitor.CurrentValue;
        var cacheKey = BuildMemoryCacheKey(options, account.AccountId);
        if (_memoryCache.TryGetValue<ExpediaTaapLoginState>(cacheKey, out var memoryState) &&
            IsValidAccountSession(memoryState))
        {
            _metrics.RecordSessionEvent(account.AccountName, "cache_hit_memory");
            var sharedSha256Hash = await GetSharedSha256HashAsync(cancellationToken);
            return ApplySharedSha256Hash(memoryState!, sharedSha256Hash);
        }

        _memoryCache.Remove(cacheKey);

        var accountRow = await GetAccountAsync(account.AccountId, asTracking: false, cancellationToken);
        if (TryBuildLoginStateFromAccount(accountRow, out var persistedState))
        {
            _metrics.RecordSessionEvent(account.AccountName, "cache_hit_db");
            var sharedSha256Hash = await GetSharedSha256HashAsync(cancellationToken);
            var loginState = ApplySharedSha256Hash(persistedState, sharedSha256Hash);
            await PersistRecoveredLoginStateIfNeededAsync(accountRow, loginState, cancellationToken);
            CacheLoginState(options, account.AccountId, loginState);
            return loginState;
        }

        if (TryReadLoginStateFromFile(account.AccountId, out var fileState))
        {
            _metrics.RecordSessionEvent(account.AccountName, "cache_hit_file");
            var sharedSha256Hash = await GetSharedSha256HashAsync(cancellationToken);
            var loginState = ApplySharedSha256Hash(fileState, sharedSha256Hash);
            await PersistRecoveredLoginStateIfNeededAsync(accountRow, loginState, cancellationToken);
            CacheLoginState(options, account.AccountId, loginState);
            return loginState;
        }

        return await RefreshLoginStateAsync(account, cancellationToken);
    }

    public async Task<ExpediaTaapLoginState> RefreshLoginStateAsync(
        ExpediaTaapAccountWorkerContext account,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        return await RefreshLoginStateCoreAsync(account.AccountId, cancellationToken);
    }

    public async Task<string> RefreshSharedSha256HashAsync(CancellationToken cancellationToken = default)
    {
        var options = _optionsMonitor.CurrentValue;
        ExpediaTaapAccount? providerAccount = null;
        while (true)
        {
            try
            {
                providerAccount = await GetSharedShaProviderAccountAsync(asTracking: false, cancellationToken);
            }
            catch (Exception)
            {

            }

            if (providerAccount is not null)
            {
                break;
            }
            _logger.LogWarning("未找到可刷新共享 sha256Hash 的 Expedia TAAP 账号，5 秒后重试。");
            await Task.Delay(5000, cancellationToken);
        }
        TryGetKnownSharedSha256Hash(options, providerAccount, out var originalSharedSha256Hash);
        var accountLock = _locks.GetOrAdd(providerAccount.Id, _ => new SemaphoreSlim(1, 1));
        await accountLock.WaitAsync(cancellationToken);

        try
        {
            providerAccount = await GetSharedShaProviderAccountAsync(asTracking: true, cancellationToken);
            if (TryGetKnownSharedSha256Hash(options, providerAccount, out var sharedSha256Hash) &&
                HasSharedSha256HashChanged(originalSharedSha256Hash, sharedSha256Hash))
            {
                CacheSharedSha256Hash(options, sharedSha256Hash);
                return sharedSha256Hash;
            }

            _metrics.RecordSessionEvent(providerAccount.AccountName, "shared_sha_refresh");
            var startedAt = Stopwatch.GetTimestamp();
            var existingLoginState = TryGetReusableLoginStateSnapshot(options, providerAccount, out var snapshot)
                ? snapshot
                : null;

            ExpediaTaapLoginState refreshedLoginState;
            try
            {
                refreshedLoginState = await _authenticationService.RefreshSharedSha256HashAsync(
                    new ExpediaTaapCredential
                    {
                        SigninEmailAddress = providerAccount.SigninEmailAddress,
                        SigninPassword = providerAccount.SigninPassword
                    },
                    existingLoginState,
                    cancellationToken);
            }
            finally
            {
                _metrics.RecordOperationDuration(
                    providerAccount.AccountName,
                    "shared_sha_refresh",
                    Stopwatch.GetElapsedTime(startedAt));
            }

            sharedSha256Hash = EnsureSharedSha256Hash(refreshedLoginState.Sha256Hash, providerAccount.Id);
            CacheSharedSha256Hash(options, sharedSha256Hash);
            await PersistLoginStateAsync(providerAccount, refreshedLoginState, cancellationToken);
            CacheLoginState(options, providerAccount.Id, refreshedLoginState);
            return sharedSha256Hash;
        }
        finally
        {
            accountLock.Release();
        }
    }

    public async Task InvalidateAsync(int accountId, CancellationToken cancellationToken = default)
    {
        var options = _optionsMonitor.CurrentValue;

        var accountRow = await GetAccountAsync(accountId, asTracking: true, cancellationToken);
        accountRow.CookieHeader = string.Empty;
        accountRow.Sha256Hash = string.Empty;
        accountRow.ClientInfoToken = string.Empty;
        accountRow.Duaid = string.Empty;
        accountRow.Eapid = 0;
        accountRow.LoginStateExpiresAt = null;
        accountRow.UpdateTime = DateTime.Now;

        await SaveChangesAsync(accountRow, cancellationToken);
        _memoryCache.Remove(BuildMemoryCacheKey(options, accountId));
        if (accountRow.CanRefreshSharedSha256Hash)
        {
            _memoryCache.Remove(BuildSharedShaMemoryCacheKey(options));
        }
    }

    private async Task<ExpediaTaapLoginState> RefreshLoginStateCoreAsync(
        int accountId,
        CancellationToken cancellationToken)
    {
        var originalLoginState = await TryGetCurrentLoginStateSnapshotAsync(accountId, cancellationToken);
        var accountLock = _locks.GetOrAdd(accountId, _ => new SemaphoreSlim(1, 1));
        await accountLock.WaitAsync(cancellationToken);

        try
        {
            var options = _optionsMonitor.CurrentValue;
            var accountRow = await GetAccountAsync(accountId, asTracking: true, cancellationToken);
            var reusableLoginState = await TryGetReusableLoginStateUnderLockAsync(
                options,
                accountRow,
                originalLoginState,
                cancellationToken);
            if (reusableLoginState is not null)
            {
                _metrics.RecordSessionEvent(accountRow.AccountName, "login_reuse_after_wait");
                return reusableLoginState;
            }

            _metrics.RecordSessionEvent(accountRow.AccountName, "login_refresh");
            var startedAt = Stopwatch.GetTimestamp();

            ExpediaTaapLoginState rawLoginState;
            try
            {
                rawLoginState = await _authenticationService.LoginAsync(
                    new ExpediaTaapCredential
                    {
                        SigninEmailAddress = accountRow.SigninEmailAddress,
                        SigninPassword = accountRow.SigninPassword
                    },
                    captureSharedSha256Hash: accountRow.CanRefreshSharedSha256Hash,
                    cancellationToken);
            }
            finally
            {
                _metrics.RecordOperationDuration(
                    accountRow.AccountName,
                    "login",
                    Stopwatch.GetElapsedTime(startedAt));
            }

            await PersistLoginStateAsync(accountRow, rawLoginState, cancellationToken);

            string sharedSha256Hash;
            if (accountRow.CanRefreshSharedSha256Hash)
            {
                sharedSha256Hash = EnsureSharedSha256Hash(rawLoginState.Sha256Hash, accountId);
                CacheSharedSha256Hash(options, sharedSha256Hash);
            }
            else
            {
                sharedSha256Hash = await GetSharedSha256HashAsync(cancellationToken);
            }

            var loginState = ApplySharedSha256Hash(rawLoginState, sharedSha256Hash);
            await PersistLoginStateAsync(accountRow, loginState, cancellationToken);
            CacheLoginState(options, accountId, loginState);
            return loginState;
        }
        finally
        {
            accountLock.Release();
        }
    }

    private async Task<string> GetSharedSha256HashAsync(CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;
        var sharedShaCacheKey = BuildSharedShaMemoryCacheKey(options);
        if (_memoryCache.TryGetValue<string>(sharedShaCacheKey, out var memorySha256Hash) &&
            !string.IsNullOrWhiteSpace(memorySha256Hash))
        {
            return memorySha256Hash!;
        }

        var providerAccount = await GetSharedShaProviderAccountAsync(asTracking: false, cancellationToken);
        if (!string.IsNullOrWhiteSpace(providerAccount.Sha256Hash))
        {
            CacheSharedSha256Hash(options, providerAccount.Sha256Hash);
            return providerAccount.Sha256Hash;
        }

        return await RefreshSharedSha256HashAsync(cancellationToken);
    }

    private async Task<ExpediaTaapAccount> GetSharedShaProviderAccountAsync(
        bool asTracking,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SupCacheDbContext>();
        IQueryable<ExpediaTaapAccount> query = dbContext.ExpediaTaapAccounts
            .Where(x => x.CanRefreshSharedSha256Hash);
        query = asTracking ? query.AsTracking() : query.AsNoTracking();

        var providers = await query
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        if (providers.Count == 0)
        {
            throw new InvalidOperationException("未配置可刷新共享 sha256Hash 的 Expedia TAAP 账号。");
        }

        var provider = providers[0];
        if (providers.Count > 1)
        {
            _logger.LogWarning(
                "检测到多个可刷新共享 sha256Hash 的 Expedia TAAP 账号，当前使用账号 {AccountId}。",
                provider.Id);
        }

        if (string.IsNullOrWhiteSpace(provider.SigninEmailAddress) || string.IsNullOrWhiteSpace(provider.SigninPassword))
        {
            throw new InvalidOperationException(
                $"账号 {provider.Id} 被标记为共享 sha256Hash 提供者，但登录凭据不完整。");
        }

        return provider;
    }

    private async Task<ExpediaTaapAccount> GetAccountAsync(
        int accountId,
        bool asTracking,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SupCacheDbContext>();
        IQueryable<ExpediaTaapAccount> query = dbContext.ExpediaTaapAccounts;
        query = asTracking ? query.AsTracking() : query.AsNoTracking();

        var account = await query.FirstOrDefaultAsync(x => x.Id == accountId, cancellationToken);
        if (account is null)
        {
            throw new InvalidOperationException($"未找到 Expedia TAAP 账号 {accountId}。");
        }

        if (!account.IsEnabled)
        {
            throw new InvalidOperationException($"Expedia TAAP 账号 {accountId} 已被停用。");
        }

        if (string.IsNullOrWhiteSpace(account.SigninEmailAddress) || string.IsNullOrWhiteSpace(account.SigninPassword))
        {
            throw new InvalidOperationException($"Expedia TAAP 账号 {accountId} 缺少登录凭据。");
        }

        return account;
    }

    private async Task PersistLoginStateAsync(
        ExpediaTaapAccount account,
        ExpediaTaapLoginState loginState,
        CancellationToken cancellationToken)
    {
        account.CookieHeader = loginState.CookieHeader;
        account.Sha256Hash = loginState.Sha256Hash;
        account.ClientInfoToken = loginState.ClientInfoToken;
        account.Duaid = loginState.Duaid;
        account.Eapid = loginState.Eapid;
        account.LoginStateExpiresAt = loginState.ExpiresAt;
        account.LastLoginAt = DateTimeOffset.UtcNow;
        account.UpdateTime = DateTime.Now;

        await SaveChangesAsync(account, cancellationToken);
    }

    private async Task PersistRecoveredLoginStateIfNeededAsync(
        ExpediaTaapAccount account,
        ExpediaTaapLoginState loginState,
        CancellationToken cancellationToken)
    {
        if (!HasLoginStateChanges(account, loginState))
        {
            return;
        }

        account.CookieHeader = loginState.CookieHeader;
        account.Sha256Hash = loginState.Sha256Hash;
        account.ClientInfoToken = loginState.ClientInfoToken;
        account.Duaid = loginState.Duaid;
        account.Eapid = loginState.Eapid;
        account.LoginStateExpiresAt = loginState.ExpiresAt;
        account.UpdateTime = DateTime.Now;

        await SaveChangesAsync(account, cancellationToken);
    }

    private async Task SaveChangesAsync(ExpediaTaapAccount account, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SupCacheDbContext>();
        dbContext.ExpediaTaapAccounts.Update(account);
        await dbContext.SaveChangesAsync(cancellationToken);
    }



    private bool TryReadLoginStateFromFile(int accountId, out ExpediaTaapLoginState loginState)
    {
        loginState = null!;
        var cachePath = BuildLoginStateCachePath(accountId);
        if (!File.Exists(cachePath))
        {
            return false;
        }

        try
        {
            var content = File.ReadAllText(cachePath);
            var persistedState = JsonSerializer.Deserialize<ExpediaTaapLoginState>(content);
            if (!IsValidAccountSession(persistedState))
            {
                return false;
            }

            loginState = persistedState!;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogWarning(ex, "读取 Expedia TAAP 登录缓存失败，账号 {AccountId}。", accountId);
            return false;
        }
    }

    private bool TryGetReusableLoginStateSnapshot(
        ExpediaTaapOptions options,
        ExpediaTaapAccount account,
        out ExpediaTaapLoginState loginState)
    {
        loginState = null!;
        var cacheKey = BuildMemoryCacheKey(options, account.Id);
        if (_memoryCache.TryGetValue<ExpediaTaapLoginState>(cacheKey, out var memoryState) &&
            CanReuseLoginStateSnapshot(memoryState))
        {
            loginState = memoryState!;
            return true;
        }

        if (TryBuildLoginStateSnapshotFromAccount(account, out var persistedState))
        {
            loginState = persistedState;
            return true;
        }

        if (TryReadLoginStateSnapshotFromFile(account.Id, out var fileState))
        {
            loginState = fileState;
            return true;
        }

        return false;
    }

    private bool TryReadLoginStateSnapshotFromFile(int accountId, out ExpediaTaapLoginState loginState)
    {
        loginState = null!;
        var cachePath = BuildLoginStateCachePath(accountId);
        if (!File.Exists(cachePath))
        {
            return false;
        }

        try
        {
            var content = File.ReadAllText(cachePath);
            var persistedState = JsonSerializer.Deserialize<ExpediaTaapLoginState>(content);
            if (!CanReuseLoginStateSnapshot(persistedState))
            {
                return false;
            }

            loginState = persistedState!;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogWarning(ex, "读取共享 sha256Hash 提供者缓存失败，账号 {AccountId}。", accountId);
            return false;
        }
    }

    private static bool TryBuildLoginStateSnapshotFromAccount(ExpediaTaapAccount account, out ExpediaTaapLoginState loginState)
    {
        loginState = null!;
        if (!account.LoginStateExpiresAt.HasValue)
        {
            return false;
        }

        var state = new ExpediaTaapLoginState
        {
            CookieHeader = account.CookieHeader,
            Sha256Hash = account.Sha256Hash,
            ClientInfoToken = account.ClientInfoToken,
            Duaid = account.Duaid,
            Eapid = account.Eapid,
            ExpiresAt = account.LoginStateExpiresAt.Value
        };

        if (!CanReuseLoginStateSnapshot(state))
        {
            return false;
        }

        loginState = state;
        return true;
    }

    private static ExpediaTaapLoginState ApplySharedSha256Hash(
        ExpediaTaapLoginState loginState,
        string sharedSha256Hash)
    {
        return new ExpediaTaapLoginState
        {
            CookieHeader = loginState.CookieHeader,
            Sha256Hash = sharedSha256Hash,
            ClientInfoToken = loginState.ClientInfoToken,
            Duaid = loginState.Duaid,
            Eapid = loginState.Eapid,
            ExpiresAt = loginState.ExpiresAt
        };
    }

    private static string EnsureSharedSha256Hash(string? sha256Hash, int accountId)
    {
        if (!string.IsNullOrWhiteSpace(sha256Hash))
        {
            return sha256Hash;
        }

        throw new InvalidOperationException($"账号 {accountId} 被标记为共享 sha256Hash 提供者，但登录后未捕获到 sha256Hash。");
    }

    private void CacheSharedSha256Hash(ExpediaTaapOptions options, string sharedSha256Hash)
    {
        _memoryCache.Set(BuildSharedShaMemoryCacheKey(options), sharedSha256Hash);
    }

    private void CacheLoginState(ExpediaTaapOptions options, int accountId, ExpediaTaapLoginState loginState)
    {
        _memoryCache.Set(BuildMemoryCacheKey(options, accountId), loginState);
    }

    private string BuildLoginStateCachePath(int accountId)
    {
        return Path.Combine(_cacheDirectory, $"expedia-taap-login-state-{accountId}.json");
    }

    private static string BuildMemoryCacheKey(ExpediaTaapOptions options, int accountId)
    {
        return $"{options.LoginStateCacheKey}:{accountId}";
    }

    private static string BuildSharedShaMemoryCacheKey(ExpediaTaapOptions options)
    {
        return $"{options.LoginStateCacheKey}:shared-sha256-hash";
    }

    private static bool TryBuildLoginStateFromAccount(ExpediaTaapAccount account, out ExpediaTaapLoginState loginState)
    {
        loginState = null!;
        if (!account.LoginStateExpiresAt.HasValue)
        {
            return false;
        }

        var state = new ExpediaTaapLoginState
        {
            CookieHeader = account.CookieHeader,
            Sha256Hash = account.Sha256Hash,
            ClientInfoToken = account.ClientInfoToken,
            Duaid = account.Duaid,
            Eapid = account.Eapid,
            ExpiresAt = account.LoginStateExpiresAt.Value
        };

        if (!IsValidAccountSession(state))
        {
            return false;
        }

        loginState = state;
        return true;
    }

    private static bool HasLoginStateChanges(ExpediaTaapAccount account, ExpediaTaapLoginState loginState)
    {
        return !string.Equals(account.CookieHeader, loginState.CookieHeader, StringComparison.Ordinal) ||
               !string.Equals(account.Sha256Hash, loginState.Sha256Hash, StringComparison.Ordinal) ||
               !string.Equals(account.ClientInfoToken, loginState.ClientInfoToken, StringComparison.Ordinal) ||
               !string.Equals(account.Duaid, loginState.Duaid, StringComparison.Ordinal) ||
               account.Eapid != loginState.Eapid ||
               account.LoginStateExpiresAt != loginState.ExpiresAt;
    }

    private static bool CanReuseLoginStateSnapshot(ExpediaTaapLoginState? loginState)
    {
        return loginState is not null &&
               !string.IsNullOrWhiteSpace(loginState.CookieHeader) &&
               !string.IsNullOrWhiteSpace(loginState.ClientInfoToken) &&
               !string.IsNullOrWhiteSpace(loginState.Duaid) &&
               loginState.Eapid > 0;
    }

    private static bool IsValidAccountSession(ExpediaTaapLoginState? loginState)
    {
        return CanReuseLoginStateSnapshot(loginState) &&
               loginState!.ExpiresAt > DateTimeOffset.UtcNow;
    }

    private async Task<ExpediaTaapLoginState?> TryGetCurrentLoginStateSnapshotAsync(
        int accountId,
        CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;
        var cacheKey = BuildMemoryCacheKey(options, accountId);
        if (_memoryCache.TryGetValue<ExpediaTaapLoginState>(cacheKey, out var memoryState) &&
            IsValidAccountSession(memoryState))
        {
            return memoryState;
        }

        _memoryCache.Remove(cacheKey);

        var accountRow = await GetAccountAsync(accountId, asTracking: false, cancellationToken);
        if (TryBuildLoginStateFromAccount(accountRow, out var persistedState))
        {
            return persistedState;
        }

        if (TryReadLoginStateFromFile(accountId, out var fileState))
        {
            return fileState;
        }

        return null;
    }

    private async Task<ExpediaTaapLoginState?> TryGetReusableLoginStateUnderLockAsync(
        ExpediaTaapOptions options,
        ExpediaTaapAccount account,
        ExpediaTaapLoginState? originalLoginState,
        CancellationToken cancellationToken)
    {
        if (TryBuildLoginStateFromAccount(account, out var persistedState))
        {
            var loginState = await TryBuildUsableLoginStateAsync(options, account, persistedState, cancellationToken);
            if (loginState is not null && HasAccountSessionChanged(originalLoginState, loginState))
            {
                await PersistRecoveredLoginStateIfNeededAsync(account, loginState, cancellationToken);
                CacheLoginState(options, account.Id, loginState);
                return loginState;
            }
        }

        if (TryReadLoginStateFromFile(account.Id, out var fileState))
        {
            var loginState = await TryBuildUsableLoginStateAsync(options, account, fileState, cancellationToken);
            if (loginState is not null && HasAccountSessionChanged(originalLoginState, loginState))
            {
                await PersistRecoveredLoginStateIfNeededAsync(account, loginState, cancellationToken);
                CacheLoginState(options, account.Id, loginState);
                return loginState;
            }
        }

        return null;
    }

    private async Task<ExpediaTaapLoginState?> TryBuildUsableLoginStateAsync(
        ExpediaTaapOptions options,
        ExpediaTaapAccount account,
        ExpediaTaapLoginState loginState,
        CancellationToken cancellationToken)
    {
        if (account.CanRefreshSharedSha256Hash)
        {
            if (!TryGetKnownSharedSha256Hash(options, loginState, out var sharedSha256Hash))
            {
                return null;
            }

            CacheSharedSha256Hash(options, sharedSha256Hash);
            return ApplySharedSha256Hash(loginState, sharedSha256Hash);
        }

        var sharedSha = await GetSharedSha256HashAsync(cancellationToken);
        return ApplySharedSha256Hash(loginState, sharedSha);
    }

    private bool TryGetKnownSharedSha256Hash(
        ExpediaTaapOptions options,
        ExpediaTaapAccount providerAccount,
        out string sharedSha256Hash)
    {
        if (_memoryCache.TryGetValue<string>(BuildSharedShaMemoryCacheKey(options), out var memorySha256Hash) &&
            !string.IsNullOrWhiteSpace(memorySha256Hash))
        {
            sharedSha256Hash = memorySha256Hash!;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(providerAccount.Sha256Hash))
        {
            sharedSha256Hash = providerAccount.Sha256Hash;
            return true;
        }

        if (TryReadLoginStateSnapshotFromFile(providerAccount.Id, out var fileState) &&
            !string.IsNullOrWhiteSpace(fileState.Sha256Hash))
        {
            sharedSha256Hash = fileState.Sha256Hash;
            return true;
        }

        sharedSha256Hash = string.Empty;
        return false;
    }

    private bool TryGetKnownSharedSha256Hash(
        ExpediaTaapOptions options,
        ExpediaTaapLoginState loginState,
        out string sharedSha256Hash)
    {
        if (!string.IsNullOrWhiteSpace(loginState.Sha256Hash))
        {
            sharedSha256Hash = loginState.Sha256Hash;
            return true;
        }

        if (_memoryCache.TryGetValue<string>(BuildSharedShaMemoryCacheKey(options), out var memorySha256Hash) &&
            !string.IsNullOrWhiteSpace(memorySha256Hash))
        {
            sharedSha256Hash = memorySha256Hash!;
            return true;
        }

        sharedSha256Hash = string.Empty;
        return false;
    }

    private static bool HasSharedSha256HashChanged(string? originalSharedSha256Hash, string currentSharedSha256Hash)
    {
        return !string.IsNullOrWhiteSpace(currentSharedSha256Hash) &&
               !string.Equals(originalSharedSha256Hash, currentSharedSha256Hash, StringComparison.Ordinal);
    }

    private static bool HasAccountSessionChanged(
        ExpediaTaapLoginState? originalLoginState,
        ExpediaTaapLoginState currentLoginState)
    {
        if (originalLoginState is null)
        {
            return true;
        }

        return !string.Equals(originalLoginState.CookieHeader, currentLoginState.CookieHeader, StringComparison.Ordinal) ||
               !string.Equals(originalLoginState.ClientInfoToken, currentLoginState.ClientInfoToken, StringComparison.Ordinal) ||
               !string.Equals(originalLoginState.Duaid, currentLoginState.Duaid, StringComparison.Ordinal) ||
               originalLoginState.Eapid != currentLoginState.Eapid ||
               originalLoginState.ExpiresAt != currentLoginState.ExpiresAt;
    }
}
