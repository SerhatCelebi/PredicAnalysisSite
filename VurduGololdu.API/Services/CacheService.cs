using Microsoft.Extensions.Caching.Memory;
using VurduGololdu.API.Models;

namespace VurduGololdu.API.Services
{
    public interface ICacheService
    {
        Task<T?> GetAsync<T>(string key);
        Task SetAsync<T>(string key, T value, TimeSpan? expiry = null);
        Task RemoveAsync(string key);
        Task RemoveByPatternAsync(string pattern);
        void ClearPredictionsCache();
    }

    public class CacheService : ICacheService
    {
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<CacheService> _logger;
        private readonly HashSet<string> _cacheKeys;

        public CacheService(IMemoryCache memoryCache, ILogger<CacheService> logger)
        {
            _memoryCache = memoryCache;
            _logger = logger;
            _cacheKeys = new HashSet<string>();
        }

        public Task<T?> GetAsync<T>(string key)
        {
            try
            {
                if (_memoryCache.TryGetValue(key, out T? value))
                {
                    _logger.LogDebug("Cache hit for key: {Key}", key);
                    return Task.FromResult<T?>(value);
                }

                _logger.LogDebug("Cache miss for key: {Key}", key);
                return Task.FromResult<T?>(default(T));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache value for key: {Key}", key);
                return Task.FromResult<T?>(default(T));
            }
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
        {
            try
            {
                var options = new MemoryCacheEntryOptions();

                if (expiry.HasValue)
                {
                    options.AbsoluteExpirationRelativeToNow = expiry;
                }
                else
                {
                    // Default expiry for predictions: 5 minutes
                    options.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                }

                // Set priority based on cache type
                if (key.Contains("predictions"))
                {
                    options.Priority = CacheItemPriority.High;
                }
                else
                {
                    options.Priority = CacheItemPriority.Normal;
                }

                // Register removal callback
                options.RegisterPostEvictionCallback((k, v, reason, state) =>
                {
                    _cacheKeys.Remove(k.ToString()!);
                    _logger.LogDebug("Cache entry removed: {Key}, Reason: {Reason}", k, reason);
                });

                _memoryCache.Set(key, value, options);
                _cacheKeys.Add(key);

                _logger.LogDebug("Cache set for key: {Key}", key);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cache value for key: {Key}", key);
                return Task.CompletedTask;
            }
        }

        public async Task RemoveAsync(string key)
        {
            try
            {
                _memoryCache.Remove(key);
                _cacheKeys.Remove(key);
                _logger.LogDebug("Cache removed for key: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache value for key: {Key}", key);
            }

            // Ensure the async method contains an awaited operation to avoid CS1998 warning
            await Task.CompletedTask;
        }

        public async Task RemoveByPatternAsync(string pattern)
        {
            try
            {
                var keysToRemove = _cacheKeys.Where(k => k.Contains(pattern)).ToList();

                foreach (var key in keysToRemove)
                {
                    _memoryCache.Remove(key);
                    _cacheKeys.Remove(key);
                }

                _logger.LogDebug("Removed {Count} cache entries matching pattern: {Pattern}", keysToRemove.Count, pattern);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache entries by pattern: {Pattern}", pattern);
            }

            // Ensure the async method contains an awaited operation to avoid CS1998 warning
            await Task.CompletedTask;
        }

        public void ClearPredictionsCache()
        {
            try
            {
                var predictionKeys = _cacheKeys.Where(k => k.Contains("predictions")).ToList();

                foreach (var key in predictionKeys)
                {
                    _memoryCache.Remove(key);
                    _cacheKeys.Remove(key);
                }

                _logger.LogInformation("Cleared {Count} prediction cache entries", predictionKeys.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing predictions cache");
            }
        }
    }

    // Cache key constants
    public static class CacheKeys
    {
        public const string AllPredictions = "predictions:all";
        public const string FreePredictions = "predictions:free";
        public const string VipPredictions = "predictions:vip";
        public const string UserPredictions = "predictions:user:{0}";
        public const string PredictionDetail = "predictions:detail:{0}";
        public const string PredictionComments = "predictions:comments:{0}";
        public const string PredictionLikes = "predictions:likes:{0}";
    }
}