//using Microsoft.Extensions.Caching.Distributed;
//using Microsoft.Extensions.Caching.Memory;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace VBaseFramework.Core.Cache
//{
//    public interface IAppCache
//    {
//        Task SetAsync<T>(string key, T value, TimeSpan? expiration = null);
//        Task<T?> GetAsync<T>(string key);
//        Task RemoveAsync(string key);
//    }

//    public class MemoryAppCache : IAppCache
//    {
//        private readonly IMemoryCache _memoryCache;

//        public MemoryAppCache(IMemoryCache memoryCache)
//        {
//            _memoryCache = memoryCache;
//        }
//        public class DistributedAppCache : IAppCache
//        {
//            private readonly IDistributedCache _distributedCache;

//            public DistributedAppCache(IDistributedCache distributedCache)
//            {
//                _distributedCache = distributedCache;
//            }

//            public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
//            {
//                var json = JsonSerializer.Serialize(value);
//                var options = new DistributedCacheEntryOptions();
//                if (expiration.HasValue)
//                {
//                    options.SetAbsoluteExpiration(expiration.Value);
//                }
//                await _distributedCache.SetStringAsync(key, json, options);
//            }

//            public async Task<T?> GetAsync<T>(string key)
//            {
//                var json = await _distributedCache.GetStringAsync(key);
//                if (json == null)
//                {
//                    return default;
//                }
//                return JsonSerializer.Deserialize<T>(json);
//            }

//            public Task RemoveAsync(string key)
//            {
//                return _distributedCache.RemoveAsync(key);
//            }
//        }

//        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
//        {
//            var options = new MemoryCacheEntryOptions();
//            if (expiration.HasValue)
//            {
//                options.SetAbsoluteExpiration(expiration.Value);
//            }
//            _memoryCache.Set(key, value, options);
//            return Task.CompletedTask;
//        }

//        public Task<T?> GetAsync<T>(string key)
//        {
//            if (_memoryCache.TryGetValue(key, out T value))
//            {
//                return Task.FromResult<T?>(value);
//            }
//            return Task.FromResult<T?>(default);
//        }

//        public Task RemoveAsync(string key)
//        {
//            _memoryCache.Remove(key);
//            return Task.CompletedTask;
//        }
//    }
//}
