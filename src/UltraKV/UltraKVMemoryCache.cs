using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace UltraKV
{
    /// <summary>
    /// UltraKV 内存缓存管理器
    /// </summary>
    internal class UltraKVMemoryCache<TKey, TValue> : IDisposable where TKey : notnull
    {
        private readonly IMemoryCache _memoryCache;
        private readonly UltraKVConfig _config;
        private readonly ConcurrentDictionary<TKey, CacheMetadata> _metadata;
        private readonly Func<TKey, TValue?> _diskLoader;
        private readonly object _lock = new object();
        private long _totalMemoryUsage = 0;
        private volatile bool _disposed = false;
        private readonly long _maxSize;
        private readonly SerializeHelper _serializeHelper;

        public UltraKVMemoryCache(UltraKVConfig config, Func<TKey, TValue?> diskLoader, SerializeHelper serializeHelper)
        {
            _config = config;
            _diskLoader = diskLoader;
            _metadata = new ConcurrentDictionary<TKey, CacheMetadata>();
            _maxSize = config.MemoryModeMaxSizeMB * 1024 * 1024; // 转换为字节
            _serializeHelper = serializeHelper;

            // 配置 MemoryCache 选项
            var cacheOptions = new MemoryCacheOptions
            {
                SizeLimit = _config.MemoryModeMaxSizeMB * 1024 * 1024, // 转换为字节
            };

            _memoryCache = new MemoryCache(cacheOptions);

            Console.WriteLine($"UltraKV内存缓存已启动，最大内存限制: {_config.MemoryModeMaxSizeMB}MB");
        }

        /// <summary>
        /// 获取缓存值，支持懒加载
        /// </summary>
        public TValue? Get(TKey key)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(UltraKVMemoryCache<TKey, TValue>));

            // 尝试从内存缓存获取
            if (_memoryCache.TryGetValue(key, out TValue? cachedValue))
            {
                // 更新访问时间
                if (_metadata.TryGetValue(key, out var metadata))
                {
                    metadata.LastAccessTime = DateTime.UtcNow;
                    metadata.AccessCount++;
                }

                return cachedValue;
            }

            // 懒加载模式：从磁盘加载
            if (_config.MemoryModeLazyLoadEnabled)
            {
                var diskValue = _diskLoader(key);
                if (diskValue != null)
                {
                    var bytes = _serializeHelper.SerializeValue(diskValue);
                    TryAddToCache(key, bytes);

                    return diskValue;
                }
            }

            return default;
        }

        /// <summary>
        /// 设置缓存值
        /// </summary>
        public bool Set(TKey key, byte[] value)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(UltraKVMemoryCache<TKey, TValue>));

            return TryAddToCache(key, value);
        }

        /// <summary>
        /// 删除缓存值
        /// </summary>
        public bool Remove(TKey key)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(UltraKVMemoryCache<TKey, TValue>));

            _memoryCache.Remove(key);

            if (_metadata.TryRemove(key, out var metadata))
            {
                Interlocked.Add(ref _totalMemoryUsage, -metadata.EstimatedSize);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 检查是否包含指定键
        /// </summary>
        public bool ContainsKey(TKey key)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(UltraKVMemoryCache<TKey, TValue>));

            return _memoryCache.TryGetValue(key, out _);
        }

        /// <summary>
        /// 获取所有缓存的键
        /// </summary>
        public IEnumerable<TKey> GetAllKeys()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(UltraKVMemoryCache<TKey, TValue>));

            return _metadata.Keys.ToList();
        }

        /// <summary>
        /// 尝试添加到缓存
        /// </summary>
        private bool TryAddToCache(TKey key, byte[] value)
        {
            if (value == null) return false;

            var estimatedSize = value.Length;

            // 检查单个对象大小限制
            if (estimatedSize > _maxSize)
            {
                return false;
            }

            //// 检查总内存使用量
            //while (_totalMemoryUsage + estimatedSize > _maxSize)
            //{
            //    // 尝试清理一些缓存项
            //    PerformEviction();
            //}

            // 再次检查
            if (_totalMemoryUsage + estimatedSize > _maxSize)
            {
                return false;
            }

            // 配置缓存项选项
            var cacheEntryOptions = new MemoryCacheEntryOptions
            {
                Size = estimatedSize,
                SlidingExpiration = TimeSpan.FromSeconds(_config.MemoryModeDefaultCacheTimeSeconds),
                Priority = CacheItemPriority.Normal
            };

            // 添加到缓存
            _memoryCache.Set(key, value, cacheEntryOptions);

            // 更新元数据
            var metadata = new CacheMetadata
            {
                EstimatedSize = estimatedSize,
                CreationTime = DateTime.UtcNow,
                LastAccessTime = DateTime.UtcNow,
                AccessCount = 1
            };

            _metadata.AddOrUpdate(key, metadata, (k, old) => metadata);

            Interlocked.Add(ref _totalMemoryUsage, estimatedSize);

            return true;
        }

        /// <summary>
        /// 执行缓存淘汰
        /// </summary>
        private void PerformEviction()
        {
            lock (_lock)
            {
                var itemsToEvict = _metadata
                    .OrderBy(kvp => GetEvictionScore(kvp.Value))
                    .Take(Math.Max(1, _metadata.Count / 10)) // 淘汰10%的项目
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in itemsToEvict)
                {
                    Remove(key);
                }

                Console.WriteLine($"内存缓存淘汰了 {itemsToEvict.Count} 个项目");
            }
        }

        /// <summary>
        /// 计算淘汰分数
        /// </summary>
        private double GetEvictionScore(CacheMetadata metadata)
        {
            return (DateTime.UtcNow - metadata.LastAccessTime).TotalSeconds;
        }

        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        public MemoryCacheStats GetStats()
        {
            return new MemoryCacheStats
            {
                TotalItems = _metadata.Count,
                TotalMemoryUsageMB = _totalMemoryUsage / 1024.0 / 1024.0,
                MaxMemoryLimitMB = _config.MemoryModeMaxSizeMB,
                MemoryUsagePercentage = (double)_totalMemoryUsage / (_config.MemoryModeMaxSizeMB * 1024 * 1024) * 100,
                CacheHitRate = CalculateCacheHitRate()
            };
        }

        private double CalculateCacheHitRate()
        {
            var totalAccess = _metadata.Values.Sum(m => m.AccessCount);
            return totalAccess > 0 ? (double)_metadata.Count / totalAccess * 100 : 0;
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            _memoryCache?.Dispose();
            _metadata.Clear();

            Console.WriteLine("UltraKV内存缓存已释放");
        }

        /// <summary>
        /// 缓存元数据
        /// </summary>
        private class CacheMetadata
        {
            public long EstimatedSize { get; set; }
            public DateTime CreationTime { get; set; }
            public DateTime LastAccessTime { get; set; }
            public long AccessCount { get; set; }
        }
    }

    /// <summary>
    /// 内存缓存统计信息
    /// </summary>
    public class MemoryCacheStats
    {
        public int TotalItems { get; set; }
        public double TotalMemoryUsageMB { get; set; }
        public int MaxMemoryLimitMB { get; set; }
        public double MemoryUsagePercentage { get; set; }
        public double CacheHitRate { get; set; }

        public override string ToString()
        {
            return $"内存缓存统计: 项目数={TotalItems}, 内存使用={TotalMemoryUsageMB:F2}MB/{MaxMemoryLimitMB}MB ({MemoryUsagePercentage:F1}%), 命中率={CacheHitRate:F1}%";
        }
    }
}