using System.Runtime.InteropServices;

namespace UltraKV
{
    /// <summary>
    /// 高性能索引条目扫描器
    /// </summary>
    public unsafe class HighPerformanceIndexScanner<TKey, TValue> where TKey : notnull
    {
        private readonly byte[] _pageBuffer;
        private readonly int _pageSize;
        private long _indexStartPosition;
        private readonly UltraKVConfig _config;
        private readonly SerializeHelper _serializeHelper;

        public HighPerformanceIndexScanner(byte[] pageBuffer, int pageSize, long indexStartPosition, UltraKVConfig config, SerializeHelper serializeHelper)
        {
            _pageBuffer = pageBuffer;
            _pageSize = pageSize;
            _indexStartPosition = indexStartPosition;
            _config = config;
            _serializeHelper = serializeHelper;
        }

        /// <summary>
        /// 高效扫描所有IndexEntry - 基于魔术数的快速定位
        /// </summary>
        public List<IndexEntryInfo<TKey>> ScanAllIndexEntries()
        {
            var entries = new List<IndexEntryInfo<TKey>>();
            var magicBytes = BitConverter.GetBytes(IndexEntry.MAGIC_NUMBER);

            // 使用SIMD加速的魔术数搜索
            var positions = FindMagicNumberPositions(magicBytes);

            // 验证每个潜在的IndexEntry位置
            foreach (var position in positions)
            {
                if (TryReadIndexEntryAt(position, out var entryInfo)
                    && entryInfo != null
                    && entryInfo.IsValidEntryValue
                    && entryInfo.Key != null)
                {
                    entries.Add(entryInfo);
                }
            }

            return entries;
        }

        /// <summary>
        /// 使用优化算法查找魔术数位置
        /// </summary>
        private List<long> FindMagicNumberPositions(byte[] magicBytes)
        {
            var positions = new List<long>();
            var searchEnd = _pageSize - IndexEntry.SIZE; // 确保有足够空间读取完整的IndexEntry

            // 使用Boyer-Moore或类似算法进行快速搜索
            for (var i = 0; i <= searchEnd; i += 1)
            {
                //// 读取4字节并检查是否匹配魔术数
                //var value = *(uint*)(_pageBuffer + i);
                //if (value == IndexEntry.MAGIC_NUMBER)
                //{
                //    positions.Add(i);
                //}

                // 使用Span<byte>进行更高效的内存操作
                var span = new Span<byte>(_pageBuffer, i, IndexEntry.SIZE);
                if (span.Length >= magicBytes.Length && span.Slice(0, magicBytes.Length).SequenceEqual(magicBytes))
                {
                    positions.Add(i);
                }
            }

            return positions;
        }

        /// <summary>
        /// 在指定位置尝试读取IndexEntry
        /// </summary>
        private bool TryReadIndexEntryAt(long position, out IndexEntryInfo<TKey>? entryInfo)
        {
            entryInfo = default;

            try
            {
                // 检查边界
                if (position + IndexEntry.SIZE > _pageSize)
                {
                    return false;
                }

                //// 读取IndexEntry
                //var entryPtr = (IndexEntry*)(_pageBuffer + position);
                //var entry = *entryPtr;

                //// 尝试读取魔术数
                //if (length >= 4)
                //{
                //    var magic = MemoryMarshal.Read<uint>(span);
                //    Console.WriteLine($"Magic number at {position}: {magic:X8}");
                //}

                // 检查基本边界
                if (position < 0 || position >= _pageSize)
                {
                    Console.WriteLine($"Position {position} is out of page bounds [0, {_pageSize})");
                    return false;
                }

                if (_config.EncryptionType != EncryptionType.None)
                {
                    // 读取加密索引条目

                    // 验证实际的结构体大小
                    var actualSize = Marshal.SizeOf<IndexEntryEncrypted>();
                    if (IndexEntryEncrypted.SIZE != actualSize)
                    {
                        Console.WriteLine($"WARNING: Size mismatch! Using actual size: {actualSize}");
                    }

                    var sizeToUse = Math.Min(IndexEntryEncrypted.SIZE, actualSize);

                    // 再次检查边界
                    if (position + sizeToUse > _pageSize)
                    {
                        Console.WriteLine($"Not enough bytes for actual struct size: position={position}, size={sizeToUse}, pageSize={_pageSize}");
                        return false;
                    }

                    // 创建span时使用实际需要的大小
                    var entrySpan = new ReadOnlySpan<byte>(_pageBuffer, (int)position, sizeToUse);

                    // 读取IndexEntryEncrypted
                    var entryEncrypted = MemoryMarshal.Read<IndexEntryEncrypted>(entrySpan);
                    if (!entryEncrypted.IsValidEntry)
                    {
                        return false;
                    }

                    // 验证魔术数（双重检查）
                    if (!entryEncrypted.HasValidMagic)
                    {
                        Console.WriteLine($"Invalid magic number at position {position}: {entryEncrypted.Magic:X8}");
                        return false;
                    }

                    // 验证其他字段的合理性
                    if (entryEncrypted.Length < 0 || entryEncrypted.Length > int.MaxValue)
                    {
                        Console.WriteLine($"Invalid length {entryEncrypted.Length} at position {position}");
                        return false;
                    }

                    // 计算条目总大小
                    var entrySize = IndexEntryEncrypted.SIZE + entryEncrypted.Length;
                    // 检查Key数据是否在页面范围内
                    if (position + entrySize > _pageSize)
                    {
                        Console.WriteLine($"Entry at {position} extends beyond page boundary");
                        return false;
                    }

                    // 读取加密数据
                    var encryptedData = new byte[entryEncrypted.Length];
                    Buffer.BlockCopy(_pageBuffer, (int)(position + IndexEntryEncrypted.SIZE), encryptedData, 0, entryEncrypted.Length);

                    // 验证哈希
                    var calculatedHash = HashHelper.CalculateHashToInt64(encryptedData, _config.HashType);
                    if (calculatedHash != entryEncrypted.Hash)
                    {
                        Console.WriteLine($"Hash mismatch at position {position}: calculated={calculatedHash}, expected={entryEncrypted.Hash}");
                        return false;
                    }

                    // 解密数据
                    var plainData = EncryptionHelper.Decrypt(encryptedData, _config.EncryptionType, _config.EncryptionKey!);

                    // 解析明文索引条目
                    if (plainData.Length < IndexEntry.SIZE)
                    {
                        return false;
                    }

                    IndexEntry entry;
                    unsafe
                    {
                        fixed (byte* ptr = plainData)
                        {
                            entry = *(IndexEntry*)ptr;
                        }
                    }

                    if (!entry.HasValidMagic || !entry.IsValidEntry)
                    {
                        return false;
                    }

                    // 解密 key 数据
                    var key = _serializeHelper.DeserializeKey<TKey>(plainData.AsSpan(IndexEntry.SIZE).ToArray());
                    if (key != null)
                    {
                        var info = new IndexEntryInfo<TKey>(_indexStartPosition + position, entry);
                        info.Key = key;

                        entryInfo = info;
                        return true;
                    }
                }
                else
                {
                    // 检查是否有足够的字节来读取完整的IndexEntry
                    var availableBytes = _pageSize - position;
                    if (availableBytes < IndexEntry.SIZE)
                    {
                        Console.WriteLine($"Insufficient bytes at position {position}: available={availableBytes}, required={IndexEntry.SIZE}");
                        return false;
                    }

                    // 验证实际的结构体大小
                    var actualSize = Marshal.SizeOf<IndexEntry>();
                    if (IndexEntry.SIZE != actualSize)
                    {
                        Console.WriteLine($"WARNING: Size mismatch! Using actual size: {actualSize}");
                    }

                    var sizeToUse = Math.Min(IndexEntry.SIZE, actualSize);

                    // 再次检查边界
                    if (position + sizeToUse > _pageSize)
                    {
                        Console.WriteLine($"Not enough bytes for actual struct size: position={position}, size={sizeToUse}, pageSize={_pageSize}");
                        return false;
                    }

                    // 创建span时使用实际需要的大小
                    var entrySpan = new ReadOnlySpan<byte>(_pageBuffer, (int)position, sizeToUse);

                    // 读取IndexEntry，换一种方式
                    //var entrySpan = new Span<byte>(_pageBuffer, (int)position, IndexEntry.SIZE);
                    var entry = MemoryMarshal.Read<IndexEntry>(entrySpan);

                    // 验证魔术数（双重检查）
                    if (!entry.HasValidMagic)
                    {
                        return false;
                    }

                    // 验证其他字段的合理性
                    if (!IsValidIndexEntryStructure(entry, position))
                    {
                        return false;
                    }

                    // 计算条目总大小
                    var entrySize = IndexEntry.SIZE + entry.KeyLength;

                    // 检查Key数据是否在页面范围内
                    if (position + entrySize > _pageSize)
                    {
                        Console.WriteLine($"Entry at {position} extends beyond page boundary");
                        return false;
                    }

                    // 验证Key数据区域（可选的深度验证）
                    if (!ValidateKeyDataRegion(position + IndexEntry.SIZE, entry.KeyLength))
                    {
                        return false;
                    }


                    if (entry.IsValidEntry)
                    {
                        // 读取Key数据
                        var keyData = new byte[entry.KeyLength];
                        Buffer.BlockCopy(_pageBuffer, (int)(position + IndexEntry.SIZE), keyData, 0, entry.KeyLength);

                        // 反序列化Key
                        var key = _serializeHelper.DeserializeKey<TKey>(keyData);
                        if (key != null)
                        {
                            var info = new IndexEntryInfo<TKey>(_indexStartPosition + position, entry);
                            info.Key = key;

                            entryInfo = info;
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading IndexEntry at position {position}: {ex.Message}");
                return false;
            }

            return false;
        }

        /// <summary>
        /// 验证IndexEntry结构的合理性
        /// </summary>
        private bool IsValidIndexEntryStructure(IndexEntry entry, long position)
        {
            // 基本范围检查
            if (entry.KeyLength < 0 || entry.KeyLength > int.MaxValue)
            {
                return false;
            }

            // 检查时间戳合理性
            if (entry.Timestamp < 0)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 验证Key数据区域（防止误判）
        /// </summary>
        private bool ValidateKeyDataRegion(long keyDataStart, int keyLength)
        {
            if (keyLength == 0)
                return true;

            if (keyDataStart + keyLength > _pageSize)
                return false;

            // 可以添加更多的Key数据验证逻辑
            // 例如检查是否包含合理的字符、是否符合加密数据特征等

            return true;
        }
    }
}