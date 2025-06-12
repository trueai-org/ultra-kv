using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;

namespace UltraKV;

/// <summary>
/// UltraKV 引擎 - 内存索引 + 磁盘存储的键值数据库
/// </summary>
public class UltraKVEngine<TKey, TValue> : IDisposable where TKey : notnull
{
    /// <summary>
    /// 内存索引：键 -> 值(位置, 长度)
    /// </summary>
    private readonly ConcurrentDictionary<TKey, IndexEntryInfo<TKey>> _index;

    /// <summary>
    /// 删除的对象
    /// </summary>
    private readonly ConcurrentDictionary<TKey, IndexEntryInfo<TKey>> _deletedIndex;

    private readonly FileStream _fileStream;
    private readonly DataProcessor _dataProcessor;
    private readonly SerializeHelper _serializeHelper;
    private readonly UltraKVConfig _config;
    private readonly Timer? _flushTimer;
    private readonly object _writeLock = new object();
    private readonly FastFileWriter _fastFileWriter;

    /// <summary>
    /// 数据读锁（当处于压实状态时，禁止读操作）
    /// </summary>
    private readonly object _readDataLock = new object();

    //// 配合读写锁使用
    //private readonly ReaderWriterLockSlim _indexLock = new ReaderWriterLockSlim();

    private readonly string _filePath;

    private DatabaseHeader _databaseHeader;
    private bool _disposed;
    private bool _isChanged;

    /// <summary>
    /// 是否压实中
    /// </summary>
    private bool _isCompacting;

    /// <summary>
    /// 内存缓存管理器
    /// </summary>
    private readonly UltraKVMemoryCache<TKey, TValue>? _memoryCache;

    public UltraKVEngine(string filePath, UltraKVConfig? config = null)
    {
        var sw = new Stopwatch();
        sw.Start();

        _config = config ?? UltraKVConfig.Default;
        _config.Validate();

        _filePath = filePath;
        _index = new ConcurrentDictionary<TKey, IndexEntryInfo<TKey>>();
        _deletedIndex = new ConcurrentDictionary<TKey, IndexEntryInfo<TKey>>();

        // 创建目录
        Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? ".");

        var isNewFile = !File.Exists(filePath);

        //_fileStream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);

        var fileOptions = FileOptions.RandomAccess;
        _fileStream = new FileStream(_filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite,
            FileShare.ReadWrite | FileShare.Delete, _config.FileStreamBufferSizeKB * 1024, fileOptions);

        // 初始化快速文件写入器
        _fastFileWriter = new FastFileWriter(_fileStream, _config);

        if (isNewFile)
        {
            // 新建数据库
            _databaseHeader = DatabaseHeader.Create(_config);
            _dataProcessor = new DataProcessor(_databaseHeader, _config.EncryptionKey);

            WriteDatabaseHeader();
        }
        else
        {
            try
            {
                // 打开现有数据库
                _databaseHeader = ReadDatabaseHeader();
            }
            // 解密异常
            catch (AuthenticationTagMismatchException ex)
            {
                Console.WriteLine($"Failed to read database header. The file may be corrupted or the encryption key is incorrect.");

                throw new InvalidOperationException("Failed to read database header. The file may be corrupted or the encryption key is incorrect.", ex);
            }
            catch (Exception)
            {
                throw;
            }

            if (!_databaseHeader.IsValid())
            {
                throw new InvalidDataException("Invalid database file format");
            }

            _config.ValidateConfigCompatibility(_databaseHeader);
            _databaseHeader = _databaseHeader.UpdateFromConfig(_config);

            _dataProcessor = new DataProcessor(_databaseHeader, _config.EncryptionKey);
        }

        _serializeHelper = new SerializeHelper(_dataProcessor);

        LoadIndex();

        // 如果启用了内存模式，初始化内存缓存
        if (_config.MemoryModeEnabled)
        {
            _memoryCache = new UltraKVMemoryCache<TKey, TValue>(_config, LoadFromDisk, _serializeHelper);

            Console.WriteLine("内存模式已启用");
        }

        // 启动定时刷新
        var flushInterval = _config.FlushInterval * 1000;
        if (flushInterval > 0)
        {
            _flushTimer = new Timer(OnFlushTimer, null, flushInterval, flushInterval);
        }

        sw.Stop();

        Console.WriteLine($"UltraKVEngine initialized. File: {_filePath}, Config: {_config}, Time: {sw.ElapsedMilliseconds} ms");
    }

    /// <summary>
    /// 内存模式是否启用
    /// </summary>
    public bool IsMemoryModeEnabled => _config.MemoryModeEnabled && _memoryCache != null;

    /// <summary>
    /// 加载索引数据到内存
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public void LoadIndex()
    {
        lock (_writeLock)
        {
            // 加载索引
            if (_databaseHeader.IndexStartOffset > 0 && _databaseHeader.IndexUsedSize > 0)
            {
                _fileStream.Seek(_databaseHeader.IndexStartOffset, SeekOrigin.Begin);
                var buffer = new byte[_databaseHeader.IndexUsedSize];
                if (_fileStream.Read(buffer, 0, buffer.Length) != buffer.Length)
                {
                    throw new InvalidOperationException($"Failed to read index page at position {_databaseHeader.IndexStartOffset}.");
                }

                var scanner = new HighPerformanceIndexScanner<TKey, TValue>(buffer,
                    _databaseHeader.IndexUsedSize,
                    _databaseHeader.IndexStartOffset,
                    _config,
                    _serializeHelper);

                var list = scanner.ScanAllIndexEntries();

                foreach (var entryInfo in list)
                {
                    if (entryInfo.IsValidEntryValue)
                    {
                        try
                        {
                            //var keyBytes = new byte[entryInfo.KeyLength];

                            //// 相对位置
                            //var position = entryInfo.KeyPosition - _databaseHeader.IndexStartOffset;

                            //Buffer.BlockCopy(buffer, (int)(position + IndexEntry.SIZE), keyBytes, 0, entryInfo.KeyLength);

                            //var key = DeserializeKey(keyBytes);

                            if (entryInfo.Key != null)
                            {
                                _index.AddOrUpdate(entryInfo.Key, entryInfo, (k, v) => entryInfo);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error deserializing key at position {entryInfo.KeyPosition}: {ex.Message}");
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// 设置键值对
    /// </summary>
    public void Set(TKey key, TValue value)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));

        var keyBytes = _serializeHelper.SerializeKey(key);
        var valueBytes = _serializeHelper.SerializeValue(value);
        var valueHash = HashHelper.CalculateHashToInt64(valueBytes, _config.HashType);
        lock (_writeLock)
        {
            // 检查是否是更新操作
            bool isAny = _index.TryGetValue(key, out var existingEntry);
            if (isAny && valueHash == existingEntry?.ValueHash)
            {
                return;
            }

            // 如果启用了内存模式，写入内存缓存
            if (IsMemoryModeEnabled)
            {
                _memoryCache!.Set(key, valueBytes);

                var keyPosition = -1;
                var valuePosition = -1;

                var indexEntry = new IndexEntryInfo<TKey>(keyBytes.Length, valueBytes.Length, valueHash, keyPosition, valuePosition);
                _index.AddOrUpdate(key, indexEntry, (k, v) => indexEntry);
            }
            else
            {
                long valuePosition;

                // 默认未分配
                long keyPosition = -1;

                // 文件变小，空间重用
                if (isAny && _config.FileUpdateMode == FileUpdateMode.Replace
                    && valueBytes.Length <= existingEntry?.ValueLength)
                {
                    valuePosition = existingEntry.ValuePosition;
                    keyPosition = existingEntry.KeyPosition;

                    //// 定位到写入位置
                    //_fileStream.Seek(valuePosition, SeekOrigin.Begin);
                    //_fileStream.Write(valueBytes);

                    // 直接写入指定位置
                    _fastFileWriter.WriteAt(valuePosition, valueBytes);
                }
                else
                {
                    //// 文件末尾
                    //valuePosition = _fileStream.Length;

                    //// 不需要分配新的位置，强制文件立即扩展到新的大小
                    //// SetLength 是一个同步的文件系统操作，立即修改文件的元数据，触发 IO
                    ////_fileStream.SetLength(valuePosition + valueBytes.Length);

                    //// 如果总是追加到文件末尾
                    ////_fileStream.Seek(0, SeekOrigin.End);
                    ////_fileStream.Seek(valuePosition, SeekOrigin.Begin);

                    //// 检查位置后决定
                    //if (_fileStream.Position != _fileStream.Length)
                    //{
                    //    _fileStream.Seek(0, SeekOrigin.End);
                    //}

                    //_fileStream.Write(valueBytes);

                    // 文件末尾追加（使用缓冲）
                    valuePosition = _fastFileWriter.WriteToEnd(valueBytes);
                }

                var indexEntry = new IndexEntryInfo<TKey>(keyBytes.Length, valueBytes.Length, valueHash, keyPosition, valuePosition);
                _index.AddOrUpdate(key, indexEntry, (k, v) => indexEntry);

                //_fileStream.Flush();

                // 更新后验证
                if (_config.UpdateValidationEnabled)
                {
                    _fastFileWriter.Flush(); // 确保数据已写入

                    var v = Get(key);
                    if (v == null || !EqualityComparer<TValue>.Default.Equals(v, value))
                    {
                        throw new InvalidOperationException($"Failed to set value for key '{key}'. Expected: {value}, Actual: {v}");
                    }
                }

                _isChanged = true;
            }
        }
    }

    /// <summary>
    /// 判断是否需要自动压实（文件持久化后调用）
    /// </summary>
    /// <returns></returns>
    public bool ShouldCompact()
    {
        if (!_config.AutoCompactEnabled)
        {
            return false;
        }

        // 验证真实文件大小
        var fileSize = _fileStream.Length;
        if (fileSize <= DatabaseHeader.SIZE)
        {
            return false; // 文件过小，不需要压实
        }

        // 计算索引实际占用空间
        var usedIndexSize = _databaseHeader.IndexUsedSize;

        // 计算数据已占用空间
        var usedDataSize = _index.Sum(x => x.Value.ValueLength);

        // 计算文件实际使用空间
        var usedTotalSize = fileSize - usedDataSize - _databaseHeader.IndexUsedSize - DatabaseHeader.SIZE - _config.EncryptionPaddingLength;

        // 排除索引扩容空间，索引扩容不参与压实计算
        usedTotalSize -= _databaseHeader.IndexSpaceSize - usedIndexSize;

        // 计算压实阈值
        var compactThreshold = _config.AutoCompactThreshold * fileSize / 100;

        // 如果空闲空间超过阈值，则需要压实
        if (usedTotalSize > compactThreshold)
        {
            // 需要自动压实
            Console.WriteLine($"Auto compact triggered. Free space: {usedTotalSize}, Threshold: {compactThreshold}");

            return true;
        }

        return false;
    }

    /// <summary>
    /// 内部方法 - 读取内容
    /// </summary>
    /// <param name="key"></param>
    /// <param name="entry"></param>
    /// <returns></returns>
    private TValue? ReadValue(TKey key, IndexEntryInfo<TKey> entry)
    {
        if (key == null || entry == null)
            return default;

        if (_isCompacting)
        {
            lock (_readDataLock)
            {
                if (_index.TryGetValue(key, out var newEntry) && newEntry != null && newEntry.IsValidEntryValue)
                {
                    try
                    {
                        entry = newEntry;

                        // 如果文件在缓冲区，还未写入
                        if (entry.ValuePosition + entry.ValueLength > _fileStream.Length)
                        {
                            _fastFileWriter.Flush();
                        }

                        _fileStream.Seek(entry.ValuePosition, SeekOrigin.Begin);
                        var valueBytes = new byte[entry.ValueLength];
                        if (_fileStream.Read(valueBytes, 0, entry.ValueLength) != entry.ValueLength)
                        {
                            Console.WriteLine($"Failed to read value for key '{key}'. Expected length: {entry.ValueLength}");
                            return default;
                        }

                        return _serializeHelper.DeserializeValue<TValue>(valueBytes);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error reading value for key: {ex.Message}");
                        return default;
                    }
                }
                else
                {
                    // 压实后，数据无效了
                    return default;
                }
            }
        }
        else
        {
            try
            {
                try
                {
                    // 如果文件在缓冲区，还未写入
                    if (entry.ValuePosition + entry.ValueLength > _fileStream.Length)
                    {
                        _fastFileWriter.Flush();
                    }

                    _fileStream.Seek(entry.ValuePosition, SeekOrigin.Begin);
                    var valueBytes = new byte[entry.ValueLength];
                    if (_fileStream.Read(valueBytes, 0, entry.ValueLength) != entry.ValueLength)
                    {
                        Console.WriteLine($"Failed to read value for key '{key}'. Expected length: {entry.ValueLength}");
                        return default;
                    }

                    return _serializeHelper.DeserializeValue<TValue>(valueBytes);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading value for key: {ex.Message}");
                    return default;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading value for key: {ex.Message}");
                return default;
            }
        }
    }

    /// <summary>
    /// 获取值
    /// </summary>
    public TValue? Get(TKey key)
    {
        if (key == null) return default;

        if (!_index.TryGetValue(key, out var entry) || !entry.IsValidEntryValue)
        {
            return default;
        }

        if (IsMemoryModeEnabled)
        {
            // 如果启用了内存模式，尝试从内存缓存中获取
            var cachedValue = _memoryCache!.Get(key);
            if (cachedValue != null)
            {
                return cachedValue;
            }
        }

        return ReadValue(key, entry);
    }

    /// <summary>
    /// 检查键是否存在
    /// </summary>
    public bool ContainsKey(TKey key)
    {
        return key != null && _index.ContainsKey(key);
    }

    /// <summary>
    /// 删除键值对
    /// </summary>
    public bool Remove(TKey key)
    {
        if (key == null) return false;

        lock (_writeLock)
        {
            if (_index.TryRemove(key, out var entry))
            {
                // 标记为删除
                entry.IsDeleted = 1;
                _deletedIndex.AddOrUpdate(key, entry, (k, v) => entry);

                _isChanged = true;

                // 从内存缓存删除
                if (IsMemoryModeEnabled)
                {
                    _memoryCache!.Remove(key);
                }

                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 清空所有数据
    /// </summary>
    public void Clear()
    {
        lock (_writeLock)
        {
            _isChanged = true;

            _index.Clear();
            _deletedIndex.Clear();

            _fastFileWriter.ClearBuffer();

            _isCompacting = false;

            _fileStream.SetLength(DatabaseHeader.SIZE);

            _databaseHeader.IndexCount = 0;
            _databaseHeader.IndexStartOffset = DatabaseHeader.SIZE;
            _databaseHeader.IndexUsedSize = 0;
            _databaseHeader.IndexSpaceSize = 0;

            _databaseHeader = _databaseHeader.UpdateTime();

            WriteDatabaseHeader();
        }
    }

    /// <summary>
    /// 获取所有键
    /// </summary>
    public IEnumerable<TKey> Keys => _index.Keys;

    /// <summary>
    /// 获取所有值
    /// </summary>
    public IEnumerable<TValue> Values
    {
        get
        {
            foreach (var key in _index.Keys)
            {
                var value = Get(key);
                if (value != null)
                    yield return value;
            }
        }
    }

    /// <summary>
    /// 获取记录数量
    /// </summary>
    public int Count => _index.Count;

    /// <summary>
    /// 索引器
    /// </summary>
    public TValue? this[TKey key]
    {
        get => Get(key);
        set => Set(key, value!);
    }

    /// <summary>
    /// 获取统计信息
    /// </summary>
    public string GetStats()
    {
        return $"{_databaseHeader.ToString()}, TotalFileSize: {_fileStream.Length}";
    }

    /// <summary>
    /// 执行压实操作
    /// </summary>
    /// <param name="allCompact">是否完全压实，默认：false，压实时默认保留 index 扩容百分比</param>
    public void Compact(bool allCompact = false)
    {
        lock (_writeLock)
        {
            PerformCompact(allCompact);
        }
    }

    /// <summary>
    /// 序列化加密的索引条目
    /// </summary>
    private byte[] SerializeEncryptedIndexEntry(TKey key, IndexEntryInfo<TKey> entryInfo)
    {
        var keyBytes = _serializeHelper.SerializeKey(key);
        var entry = entryInfo.ToEntry();

        // 创建明文索引数据（IndexEntry + Key）
        var plainData = new byte[IndexEntry.SIZE + keyBytes.Length];

        // 序列化 IndexEntry
        unsafe
        {
            fixed (byte* ptr = plainData)
            {
                *(IndexEntry*)ptr = entry;
            }
        }

        // 添加 Key 数据
        Buffer.BlockCopy(keyBytes, 0, plainData, IndexEntry.SIZE, keyBytes.Length);

        // 加密整个索引数据
        var encryptedData = EncryptionHelper.Encrypt(plainData, _config.EncryptionType, _config.EncryptionKey!);

        // 计算加密数据的哈希
        var encryptedHash = HashHelper.CalculateHashToInt64(encryptedData, _config.HashType);

        // 创建加密索引条目
        var encryptedEntry = new IndexEntryEncrypted(encryptedHash, encryptedData.Length);

        // 序列化加密条目头部
        var result = new byte[IndexEntryEncrypted.SIZE + encryptedData.Length];
        unsafe
        {
            fixed (byte* ptr = result)
            {
                *(IndexEntryEncrypted*)ptr = encryptedEntry;
            }
        }

        // 添加加密数据
        Buffer.BlockCopy(encryptedData, 0, result, IndexEntryEncrypted.SIZE, encryptedData.Length);

        return result;
    }

    /// <summary>
    /// 反序列化加密的索引条目
    /// </summary>
    private (TKey key, IndexEntryInfo<TKey> entryInfo) DeserializeEncryptedIndexEntry(byte[] encryptedIndexData, long position)
    {
        // 读取加密索引条目头部
        IndexEntryEncrypted encryptedEntry;
        unsafe
        {
            fixed (byte* ptr = encryptedIndexData)
            {
                encryptedEntry = *(IndexEntryEncrypted*)ptr;
            }
        }

        if (!encryptedEntry.HasValidMagic || !encryptedEntry.IsValidEntry)
        {
            throw new InvalidDataException("Invalid encrypted index entry");
        }

        // 提取加密数据
        var encryptedData = new byte[encryptedEntry.Length];
        Buffer.BlockCopy(encryptedIndexData, IndexEntryEncrypted.SIZE, encryptedData, 0, encryptedEntry.Length);

        // 验证加密数据哈希
        var calculatedHash = HashHelper.CalculateHashToInt64(encryptedData, _config.HashType);
        if (calculatedHash != encryptedEntry.Hash)
        {
            throw new InvalidDataException("Encrypted index entry hash mismatch");
        }

        // 解密数据
        var plainData = EncryptionHelper.Decrypt(encryptedData, _config.EncryptionType, _config.EncryptionKey!);

        // 反序列化 IndexEntry
        IndexEntry entry;
        unsafe
        {
            fixed (byte* ptr = plainData)
            {
                entry = *(IndexEntry*)ptr;
            }
        }

        if (!entry.HasValidMagic)
        {
            throw new InvalidDataException("Invalid decrypted index entry");
        }

        // 提取 Key 数据
        var keyBytes = new byte[entry.KeyLength];
        Buffer.BlockCopy(plainData, IndexEntry.SIZE, keyBytes, 0, entry.KeyLength);

        var key = _serializeHelper.DeserializeKey<TKey>(keyBytes);
        var entryInfo = new IndexEntryInfo<TKey>(position, entry);

        return (key, entryInfo);
    }

    /// <summary>
    /// 将内存索引追加到文件中
    /// </summary>
    public void AppendIndexToFile()
    {
        // 使用 buffer 一次写入所有索引数据
        var indexBuffer = new List<byte>();
        var useEncryption = _config.EncryptionType != EncryptionType.None;

        // 计算索引开始位置
        var indexStartPosition = _fileStream.Length;
        var indexStartOffset = _fileStream.Length;

        foreach (var kvp in _index)
        {
            var entryInfo = kvp.Value;

            // 重置更新标记
            entryInfo.IsUpdated = 0;
            entryInfo.KeyPosition = indexStartPosition;

            byte[] entryBytes;
            if (useEncryption)
            {
                // 使用加密索引
                entryBytes = SerializeEncryptedIndexEntry(kvp.Key, entryInfo);
            }
            else
            {
                // 使用明文索引（原有逻辑）
                var keyBytes = _serializeHelper.SerializeKey(kvp.Key);

                var entry = entryInfo.ToEntry();

                var plainEntryBytes = new byte[IndexEntry.SIZE];
                unsafe
                {
                    fixed (byte* ptr = plainEntryBytes)
                    {
                        *(IndexEntry*)ptr = entry;
                    }
                }

                entryBytes = new byte[IndexEntry.SIZE + keyBytes.Length];
                Buffer.BlockCopy(plainEntryBytes, 0, entryBytes, 0, IndexEntry.SIZE);
                Buffer.BlockCopy(keyBytes, 0, entryBytes, IndexEntry.SIZE, keyBytes.Length);
            }

            // 更新内存索引
            _index[kvp.Key] = entryInfo;
            indexBuffer.AddRange(entryBytes);

            indexStartPosition += entryBytes.Length;
        }

        var indexBytes = indexBuffer.ToArray();

        _fileStream.Seek(_fileStream.Length, SeekOrigin.Begin);
        _fileStream.Write(indexBytes);

        var indexAllocationSize = indexBytes.Length;

        // 如果索引阈值 > 0，则扩容文件
        if (_config.IndexRebuildThreshold > 0 && _index.Count >= 10)
        {
            // 扩容大小
            var appendSize = _config.IndexRebuildThreshold * indexBytes.Length / 100;

            // 扩容文件大小
            if (appendSize > 0)
            {
                // 写入空白字节以扩容
                var emptyBytes = new byte[appendSize];

                var end = _fileStream.Length;
                var newSize = end + appendSize;

                _fileStream.SetLength(newSize);
                _fileStream.Write(emptyBytes, 0, emptyBytes.Length);

                indexAllocationSize += appendSize;
            }
        }

        // 更新索引结束偏移量和实际使用量
        _databaseHeader.IndexStartOffset = indexStartOffset;
        _databaseHeader.IndexUsedSize = indexBytes.Length;
        _databaseHeader.IndexSpaceSize = indexAllocationSize;

        // 清空删除的索引
        _deletedIndex.Clear();
    }

    /// <summary>
    /// 强制刷新到磁盘
    /// </summary>
    public void Flush()
    {
        lock (_writeLock)
        {
            // 先刷新文件写入器
            _fastFileWriter.Flush();

            if (!_isChanged)
            {
                return;
            }

            var useEncryption = _config.EncryptionType != EncryptionType.None;

            var sw = new Stopwatch();
            sw.Start();

            // 数目较少，始终重建
            if (_index.Count < 10 || _config.IndexRebuildThreshold <= 0)
            {
                AppendIndexToFile();
            }
            else
            {
                // 总使用索引长度
                var useSize = _databaseHeader.IndexUsedSize;

                // 实际使用索引长度，排除新增
                var realSize =
                     useEncryption ?
                     _index.Where(c => c.Value.KeyPosition > 0).Sum(c => IndexEntryEncrypted.SIZE + IndexEntry.SIZE + c.Value.KeyLength + _config.EncryptionPaddingLength) :
                     _index.Where(c => c.Value.KeyPosition > 0).Sum(c => IndexEntry.SIZE + c.Value.KeyLength);

                // 删除的索引长度（真实的删除长度，不是当前索引内的）
                var deletedSize = useSize - realSize;

                // 如果删除的索引长度超过阈值，即：空闲索引空间超过阈值，则重建索引
                if (deletedSize > 0 && realSize > 0
                    && deletedSize > useSize * _config.IndexRebuildThreshold / 100)
                {
                    // 重建索引
                    AppendIndexToFile();
                }
                else
                {
                    // 判断 index 页面是否有剩余空间
                    var appendIndexCount = _index.Count(c => c.Value.IsUpdated == 1 && c.Value.KeyPosition == -1);
                    if (appendIndexCount > 0)
                    {
                        // 计算需要的空间
                        var requiredSpace = _index.Where(c => c.Value.IsUpdated == 1 && c.Value.KeyPosition == -1)
                            .Sum(kvp => IndexEntry.SIZE + kvp.Value.KeyLength);

                        // 判断当前分配的 index 页面是否需要扩容
                        // 计算当前剩余未分配的 index 页面的空间
                        var remainingSpace = _databaseHeader.IndexSpaceSize - _databaseHeader.IndexUsedSize;
                        if (remainingSpace < requiredSpace)
                        {
                            // 重建索引
                            AppendIndexToFile();
                        }
                        else
                        {
                            var isRebuildIndex = false;

                            // 将需要更新的索引条目写入索引区
                            // 在索引区的最后一个未占用的位置开始写入
                            var start = _databaseHeader.IndexUsedSize;
                            _fileStream.Seek(start, SeekOrigin.Begin);

                            foreach (var kvp in _index.Where(c => c.Value.IsUpdated == 1 && c.Value.KeyPosition == -1))
                            {
                                var keyBytes = _serializeHelper.SerializeKey(kvp.Key);
                                var entryInfo = kvp.Value;
                                entryInfo.IsUpdated = 0;

                                // 更新索引条目的位置
                                entryInfo.KeyPosition = start;

                                if (useEncryption)
                                {
                                    // 加密写入
                                    var entryBytes = SerializeEncryptedIndexEntry(kvp.Key, entryInfo);
                                    _fileStream.Write(entryBytes);

                                    // 更新索引使用大小
                                    start += entryBytes.Length;
                                    _databaseHeader.IndexUsedSize += entryBytes.Length;
                                }
                                else
                                {
                                    // 写入索引条目
                                    var entry = entryInfo.ToEntry();
                                    var entryBytes = new byte[IndexEntry.SIZE];
                                    unsafe
                                    {
                                        fixed (byte* ptr = entryBytes)
                                        {
                                            *(IndexEntry*)ptr = entry;
                                        }
                                    }
                                    _fileStream.Write(entryBytes);
                                    _fileStream.Write(keyBytes);

                                    start += IndexEntry.SIZE + keyBytes.Length;
                                    _databaseHeader.IndexUsedSize += IndexEntry.SIZE + keyBytes.Length;
                                }

                                // 更新内存索引
                                _index[kvp.Key] = entryInfo;

                                // 分配超出
                                if (_databaseHeader.IndexUsedSize > _databaseHeader.IndexSpaceSize)
                                {
                                    isRebuildIndex = true;
                                    break;
                                }
                            }

                            if (isRebuildIndex)
                            {
                                // 重建索引
                                AppendIndexToFile();
                            }
                        }
                    }

                    // 更新需要更新的索引条目
                    foreach (var kvp in _index.Where(c => c.Value.IsUpdated == 1 && c.Value.KeyPosition > 0))
                    {
                        if (kvp.Value.IsUpdated == 0)
                            continue;
                        var indexEntry = kvp.Value;
                        indexEntry.IsUpdated = 0;

                        if (useEncryption)
                        {
                            // 加密写入
                            var entryBytes = SerializeEncryptedIndexEntry(kvp.Key, indexEntry);
                            _fileStream.Seek(indexEntry.KeyPosition, SeekOrigin.Begin);
                            _fileStream.Write(entryBytes);
                        }
                        else
                        {
                            // 注意：重新序列化，长度是不变的
                            var keyBytes = _serializeHelper.SerializeKey(kvp.Key);

                            // 写入索引条目
                            var entry = indexEntry.ToEntry();
                            var entryBytes = new byte[IndexEntry.SIZE];
                            unsafe
                            {
                                fixed (byte* ptr = entryBytes)
                                {
                                    *(IndexEntry*)ptr = entry;
                                }
                            }
                            _fileStream.Seek(indexEntry.KeyPosition, SeekOrigin.Begin);
                            _fileStream.Write(entryBytes);
                            _fileStream.Write(keyBytes);
                        }
                    }
                }

                // 如果有删除的索引，只更新索引条目标识即可
                if (_deletedIndex.Count > 0)
                {
                    // 将删除的索引条目写入索引区
                    // 只更新有持久化话的索引
                    foreach (var kvp in _deletedIndex.Where(c => c.Value.KeyPosition > 0))
                    {
                        var entryInfo = kvp.Value;

                        // 写入删除标记
                        entryInfo.IsDeleted = 1;

                        // 写入索引条目
                        if (useEncryption)
                        {
                            // 加密写入
                            //var entryBytes = SerializeEncryptedIndexEntry(kvp.Key, entryInfo);
                            //_fileStream.Seek(entryInfo.KeyPosition, SeekOrigin.Begin);
                            //_fileStream.Write(entryBytes);

                            // 删除标记
                            // 只需要写头文件即可，不需要计算，因为数据不变，只是头文件标记删除
                            var entry = entryInfo.ToDeletedEntry();
                            var entryBytes = new byte[IndexEntryEncrypted.SIZE];
                            unsafe
                            {
                                fixed (byte* ptr = entryBytes)
                                {
                                    *(IndexEntryEncrypted*)ptr = entry;
                                }
                            }
                            _fileStream.Seek(entryInfo.KeyPosition, SeekOrigin.Begin);
                            _fileStream.Write(entryBytes);
                        }
                        else
                        {
                            // 明文写入
                            var entry = entryInfo.ToEntry();
                            var entryBytes = new byte[IndexEntry.SIZE];
                            unsafe
                            {
                                fixed (byte* ptr = entryBytes)
                                {
                                    *(IndexEntry*)ptr = entry;
                                }
                            }
                            _fileStream.Seek(entryInfo.KeyPosition, SeekOrigin.Begin);
                            _fileStream.Write(entryBytes);
                        }
                    }
                }
            }

            // 写入统计信息
            _databaseHeader.IndexCount = _index.Count;

            WriteDatabaseHeader();

            _fileStream.Flush();

            // 重置状态
            _isChanged = false;

            // 重置
            _deletedIndex.Clear();

            // 检查是否需要压实
            if (ShouldCompact())
            {
                PerformCompact();
            }

            sw.Stop();

            Console.WriteLine($"Flush completed in {sw.ElapsedMilliseconds} ms. Total records: {_index.Count}, Total file size: {_fileStream.Length} bytes");
        }
    }

    /// <summary>
    /// 执行压实操作 - 重新组织文件以消除碎片和无效数据
    /// </summary>
    /// <param name="allCompact">是否完全压实，默认：false，压实时默认保留 index 扩容百分比</param>
    /// <exception cref="InvalidDataException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    private void PerformCompact(bool allCompact = false)
    {
        if (_isCompacting)
        {
            return;
        }

        lock (_writeLock)
        {
            if (_isCompacting)
            {
                return;
            }

            _isCompacting = true;

            // 临时文件
            var tempPath = _filePath + ".compact.tmp";

            // 备份原文件
            var backupPath = _filePath + ".backup";

            try
            {
                var sw = new Stopwatch();
                sw.Start();

                Console.WriteLine($"Starting compact operation. Current file size: {_fileStream.Length} bytes, Records: {_index.Count}");

                // 1. 先刷新所有缓冲数据
                _fastFileWriter.Flush();

                // 2. 创建临时文件
                using var tempStream = new FileStream(tempPath, FileMode.Create, FileAccess.ReadWrite,
                    FileShare.None, _config.FileStreamBufferSizeKB * 1024, FileOptions.SequentialScan);

                // 3. 创建新的数据库头部
                var newHeader = new DatabaseHeader
                {
                    Magic = DatabaseHeader.MAGIC_NUMBER,
                    Version = DatabaseHeader.CURRENT_VERSION,
                    CompressionType = _databaseHeader.CompressionType,
                    EncryptionType = _databaseHeader.EncryptionType,
                    HashType = _databaseHeader.HashType,
                    CreatedTime = _databaseHeader.CreatedTime,
                    IndexCount = _index.Count
                };

                // 4. 先写入占位的头部
                newHeader = newHeader.WriteDatabaseHeader(tempStream, _config);

                // 5. 创建新的索引映射和统计信息
                var newIndex = new Dictionary<TKey, IndexEntryInfo<TKey>>();
                long currentDataPosition = DatabaseHeader.SIZE;
                long totalValueSize = 0;
                int validRecordCount = 0;

                // 6. 按文件位置排序以优化读取性能
                var sortedEntries = _index.ToList()
                    .OrderBy(kvp => kvp.Value.ValuePosition)
                    .ToList();

                // 7. 逐个读取并写入有效记录
                foreach (var kvp in sortedEntries)
                {
                    var key = kvp.Key;
                    var oldEntry = kvp.Value;

                    try
                    {
                        // 读取原始值数据
                        _fileStream.Seek(oldEntry.ValuePosition, SeekOrigin.Begin);
                        var valueBytes = new byte[oldEntry.ValueLength];
                        if (_fileStream.Read(valueBytes, 0, oldEntry.ValueLength) != oldEntry.ValueLength)
                        {
                            Console.WriteLine($"Warning: Failed to read value for key '{key}' during compact, skipping.");
                            continue;
                        }

                        // 验证数据完整性（可选）
                        if (_config.UpdateValidationEnabled)
                        {
                            var readHash = HashHelper.CalculateHashToInt64(valueBytes, _config.HashType);
                            if (readHash != oldEntry.ValueHash)
                            {
                                Console.WriteLine($"Warning: Hash mismatch for key '{key}' during compact, skipping.");
                                continue;
                            }
                        }

                        // 写入到新文件
                        tempStream.Seek(currentDataPosition, SeekOrigin.Begin);
                        tempStream.Write(valueBytes);

                        // 创建新的索引条目
                        var newEntry = new IndexEntryInfo<TKey>(
                            oldEntry.KeyLength,
                            oldEntry.ValueLength,
                            oldEntry.ValueHash,
                            -1, // KeyPosition 将在写入索引时设置
                            currentDataPosition
                        );

                        newIndex[key] = newEntry;
                        currentDataPosition += oldEntry.ValueLength;
                        totalValueSize += oldEntry.ValueLength;
                        validRecordCount++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing key '{key}' during compact: {ex.Message}");
                        // 继续处理其他记录
                    }
                }

                // 8. 写入索引数据
                var indexStartOffset = currentDataPosition;
                var indexStartPosition = currentDataPosition;
                var indexBuffer = new List<byte>();
                var useEncryption = _config.EncryptionType != EncryptionType.None;

                foreach (var kvp in newIndex)
                {
                    var entryInfo = kvp.Value;

                    // 设置索引条目的键位置
                    entryInfo.KeyPosition = indexStartPosition;
                    entryInfo.IsUpdated = 0;

                    byte[] entryBytes;
                    if (useEncryption)
                    {
                        // 使用加密索引
                        entryBytes = SerializeEncryptedIndexEntry(kvp.Key, entryInfo);
                    }
                    else
                    {
                        // 使用明文索引（原有逻辑）
                        var keyBytes = _serializeHelper.SerializeKey(kvp.Key);

                        var entry = entryInfo.ToEntry();

                        var plainEntryBytes = new byte[IndexEntry.SIZE];
                        unsafe
                        {
                            fixed (byte* ptr = plainEntryBytes)
                            {
                                *(IndexEntry*)ptr = entry;
                            }
                        }

                        entryBytes = new byte[IndexEntry.SIZE + keyBytes.Length];
                        Buffer.BlockCopy(plainEntryBytes, 0, entryBytes, 0, IndexEntry.SIZE);
                        Buffer.BlockCopy(keyBytes, 0, entryBytes, IndexEntry.SIZE, keyBytes.Length);
                    }

                    // 更新索引映射
                    newIndex[kvp.Key] = entryInfo;
                    indexBuffer.AddRange(entryBytes);
                    indexStartPosition += entryBytes.Length;
                }

                // 写入索引数据到文件
                var indexBytes = indexBuffer.ToArray();
                tempStream.Seek(indexStartOffset, SeekOrigin.Begin);
                tempStream.Write(indexBytes);

                var indexAllocationSize = indexBytes.Length;

                // 非完全压实时，保留索引空间
                // 如果索引阈值 > 0，则扩容文件
                if (!allCompact && _config.IndexRebuildThreshold > 0 && _index.Count >= 10)
                {
                    // 扩容大小
                    var appendSize = _config.IndexRebuildThreshold * indexBytes.Length / 100;

                    // 扩容文件大小
                    if (appendSize > 0)
                    {
                        // 写入空白字节以扩容
                        var emptyBytes = new byte[appendSize];

                        var end = tempStream.Length;
                        var newSize = end + appendSize;

                        tempStream.SetLength(newSize);
                        tempStream.Write(emptyBytes, 0, emptyBytes.Length);

                        indexAllocationSize += appendSize;
                    }
                }

                // 9. 更新头部信息
                newHeader.IndexStartOffset = indexStartOffset;
                newHeader.IndexUsedSize = indexBytes.Length;

                // 完全压实：不预留额外空间
                // 默认：不完全压实，预留 index 空间
                newHeader.IndexSpaceSize = indexAllocationSize;
                newHeader.IndexCount = validRecordCount;

                // 保存头部信息
                newHeader = newHeader.WriteDatabaseHeader(tempStream, _config);

                tempStream.Flush();

                // 读取头验证
                var readHeader = DatabaseHeader.ReadDatabaseHeader(tempStream, _config);
                if (!readHeader.Equals(newHeader))
                {
                    throw new InvalidDataException("Database header mismatch after writing.");
                }

                tempStream.Close();

                // 10. 原子性替换文件
                lock (_readDataLock)
                {
                    // 关闭原文件流
                    _fileStream.Close();
                    try
                    {
                        // 删除备份文件
                        if (File.Exists(backupPath))
                        {
                            File.Delete(backupPath);
                        }

                        // 备份原文件
                        File.Move(_filePath, backupPath);

                        // 移动新文件到原位置
                        File.Move(tempPath, _filePath);
                    }
                    catch (Exception ex)
                    {
                        // 如果替换失败，恢复备份
                        if (File.Exists(backupPath))
                        {
                            if (File.Exists(_filePath))
                            {
                                File.Delete(_filePath);
                            }
                            File.Move(backupPath, _filePath);
                        }

                        throw new InvalidOperationException($"Failed to replace database file during compact: {ex.Message}", ex);
                    }

                    // 11. 重新打开文件流
                    var fileOptions = FileOptions.RandomAccess;
                    var newFileStream = new FileStream(_filePath, FileMode.Open, FileAccess.ReadWrite,
                        FileShare.ReadWrite | FileShare.Delete, _config.FileStreamBufferSizeKB * 1024, fileOptions);

                    // 使用反射更新文件流引用（或者重构代码以支持文件流替换）
                    var fileStreamField = typeof(UltraKVEngine<TKey, TValue>)
                        .GetField("_fileStream", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    fileStreamField?.SetValue(this, newFileStream);

                    // 12. 更新内存数据结构
                    _index.Clear();
                    foreach (var kvp in newIndex)
                    {
                        _index[kvp.Key] = kvp.Value;
                    }

                    _databaseHeader = newHeader;
                    _deletedIndex.Clear();
                    _isChanged = false;

                    // 13. 重新初始化 FastFileWriter
                    _fastFileWriter.Dispose();

                    var newFastFileWriter = new FastFileWriter(newFileStream, _config);
                    var fastFileWriterField = typeof(UltraKVEngine<TKey, TValue>)
                        .GetField("_fastFileWriter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    fastFileWriterField?.SetValue(this, newFastFileWriter);

                    sw.Stop();
                }

                var originalSize = new FileInfo(backupPath).Length;
                var newFileSize = new FileInfo(_filePath).Length;
                var savedBytes = originalSize - newFileSize;
                var compressionRatio = (double)savedBytes / originalSize * 100;

                Console.WriteLine($"Compact completed successfully in {sw.ElapsedMilliseconds} ms");
                Console.WriteLine($"Original size: {originalSize:N0} bytes");
                Console.WriteLine($"New size: {newFileSize:N0} bytes");
                Console.WriteLine($"Saved: {savedBytes:N0} bytes ({compressionRatio:F1}%)");
                Console.WriteLine($"Valid records: {validRecordCount:N0}/{_index.Count + _deletedIndex.Count:N0}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Compact operation failed: {ex.Message}");
                throw;
            }
            finally
            {
                // 清理临时文件
                if (File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch { }
                }

                _isCompacting = false;
            }
        }
    }

    /// <summary>
    /// 读数据库头
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidDataException"></exception>
    private DatabaseHeader ReadDatabaseHeader()
    {
        return DatabaseHeader.ReadDatabaseHeader(_fileStream, _config);
    }

    /// <summary>
    /// 写入数据库头部信息
    /// </summary>
    private void WriteDatabaseHeader()
    {
        _databaseHeader = _databaseHeader.WriteDatabaseHeader(_fileStream, _config);
    }

    private void OnFlushTimer(object? state)
    {
        try
        {
            Flush();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Flush timer error: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _flushTimer?.Dispose();

            try
            {
                _fastFileWriter?.Flush();

                Flush();
            }
            catch { }

            _memoryCache?.Dispose();
            _dataProcessor?.Dispose();
            _fileStream?.Dispose();

            _disposed = true;
        }
    }

    public void Put(TKey v1, TValue v2)
    {
        Set(v1, v2);
    }

    public bool Delete(TKey key)
    {
        return Remove(key);
    }

    public int DeleteBatch(List<TKey> batchKeys)
    {
        var count = 0;
        foreach (var item in batchKeys)
        {
            if (Remove(item))
            {
                count++;
            }
        }
        return count;
    }

    public List<TKey> GetAllKeys()
    {
        return _index.Keys.ToList();
    }

    /// <summary>
    /// 高性能批量添加方法
    /// </summary>
    /// <param name="items">要添加的键值对字典</param>
    /// <param name="skipDuplicates">是否跳过重复的键，默认为 true</param>
    /// <returns>成功添加的记录数</returns>
    public int SetBatch(Dictionary<TKey, TValue> items, bool skipDuplicates = true)
    {
        if (items == null || items.Count == 0)
            return 0;

        var sw = new Stopwatch();
        sw.Start();

        int successCount = 0;
        var batchOperations = new List<BatchOperation<TKey>>();

        lock (_writeLock)
        {
            try
            {
                // 1. 预处理阶段：序列化并准备批量操作
                foreach (var kvp in items)
                {
                    if (kvp.Key == null) continue;

                    var keyBytes = _serializeHelper.SerializeKey(kvp.Key);
                    var valueBytes = _serializeHelper.SerializeValue(kvp.Value);
                    var valueHash = HashHelper.CalculateHashToInt64(valueBytes, _config.HashType);

                    // 检查是否是更新操作
                    bool isExisting = _index.TryGetValue(kvp.Key, out var existingEntry);

                    // 如果跳过重复且值哈希相同，则跳过
                    if (skipDuplicates && isExisting && valueHash == existingEntry?.ValueHash)
                    {
                        continue;
                    }

                    var operation = new BatchOperation<TKey>
                    {
                        Key = kvp.Key,
                        Value = kvp.Value,
                        KeyBytes = keyBytes,
                        ValueBytes = valueBytes,
                        ValueHash = valueHash,
                        IsUpdate = isExisting,
                        ExistingEntry = existingEntry
                    };

                    // 判断是否可以重用空间
                    if (isExisting && _config.FileUpdateMode == FileUpdateMode.Replace
                        && valueBytes.Length <= existingEntry?.ValueLength)
                    {
                        operation.CanReuseSpace = true;
                        operation.ValuePosition = existingEntry.ValuePosition;
                        operation.KeyPosition = existingEntry.KeyPosition;
                    }

                    batchOperations.Add(operation);
                }

                if (batchOperations.Count == 0)
                    return 0;

                // 2. 批量写入数据阶段
                var writeOperations = new List<BatchWriteOperation>();

                // 分组操作：重用空间的操作和新增操作
                var reuseOperations = batchOperations.Where(op => op.CanReuseSpace).ToList();
                var newOperations = batchOperations.Where(op => !op.CanReuseSpace).ToList();

                // 处理重用空间的操作（随机写入）
                foreach (var operation in reuseOperations)
                {
                    _fastFileWriter.WriteAt(operation.ValuePosition, operation.ValueBytes);

                    var indexEntry = new IndexEntryInfo<TKey>(
                        operation.KeyBytes.Length,
                        operation.ValueBytes.Length,
                        operation.ValueHash,
                        operation.KeyPosition,
                        operation.ValuePosition);

                    writeOperations.Add(new BatchWriteOperation
                    {
                        Operation = operation,
                        IndexEntry = indexEntry
                    });
                }

                // 处理新增操作（顺序追加，更高效）
                if (newOperations.Count > 0)
                {
                    // 批量计算所需空间
                    var totalSize = newOperations.Sum(op => op.ValueBytes.Length);
                    var startPosition = _fastFileWriter.GetCurrentEndPosition();

                    // 预分配文件空间以减少文件系统调用
                    _fastFileWriter.PreallocateSpace(totalSize);

                    var currentPosition = startPosition;

                    // 批量写入数据（连续写入，减少 IO 调用）
                    var batchBuffer = new List<byte>();
                    foreach (var operation in newOperations)
                    {
                        operation.ValuePosition = currentPosition;
                        batchBuffer.AddRange(operation.ValueBytes);
                        currentPosition += operation.ValueBytes.Length;
                    }

                    // 一次性写入所有数据
                    if (batchBuffer.Count > 0)
                    {
                        _fastFileWriter.WriteToEndBatch(batchBuffer.ToArray());
                    }

                    // 创建索引条目
                    foreach (var operation in newOperations)
                    {
                        var indexEntry = new IndexEntryInfo<TKey>(
                            operation.KeyBytes.Length,
                            operation.ValueBytes.Length,
                            operation.ValueHash,
                            -1, // KeyPosition 将在 Flush 时设置
                            operation.ValuePosition);

                        writeOperations.Add(new BatchWriteOperation
                        {
                            Operation = operation,
                            IndexEntry = indexEntry
                        });
                    }
                }

                // 3. 批量更新内存索引
                foreach (var writeOp in writeOperations)
                {
                    _index.AddOrUpdate(writeOp.Operation.Key, writeOp.IndexEntry, (k, v) => writeOp.IndexEntry);
                    successCount++;
                }

                // 4. 批量验证（如果启用）
                if (_config.UpdateValidationEnabled && successCount > 0)
                {
                    _fastFileWriter.Flush(); // 确保数据已写入

                    var validationTasks = new List<Task<bool>>();
                    var semaphore = new SemaphoreSlim(Environment.ProcessorCount, Environment.ProcessorCount);

                    foreach (var writeOp in writeOperations.Take(Math.Min(10, writeOperations.Count))) // 只验证前10个
                    {
                        var operation = writeOp.Operation;
                        validationTasks.Add(Task.Run(async () =>
                        {
                            await semaphore.WaitAsync();
                            try
                            {
                                var value = Get(operation.Key);
                                return value != null && EqualityComparer<TValue>.Default.Equals(value, operation.Value);
                            }
                            finally
                            {
                                semaphore.Release();
                            }
                        }));
                    }

                    var validationResults = Task.WhenAll(validationTasks).Result;
                    if (validationResults.Any(r => !r))
                    {
                        throw new InvalidOperationException("Batch validation failed for some records.");
                    }
                }

                _isChanged = true;

                sw.Stop();
                Console.WriteLine($"Batch set completed: {successCount} records in {sw.ElapsedMilliseconds} ms " +
                                $"({successCount / Math.Max(sw.ElapsedMilliseconds, 1):F1} records/ms)");

                return successCount;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Batch set operation failed: {ex.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// 批量添加的重载方法，接受键值对数组
    /// </summary>
    /// <param name="items">键值对数组</param>
    /// <param name="skipDuplicates">是否跳过重复的键</param>
    /// <returns>成功添加的记录数</returns>
    public int SetBatch(KeyValuePair<TKey, TValue>[] items, bool skipDuplicates = true)
    {
        if (items == null || items.Length == 0)
            return 0;

        var dictionary = new Dictionary<TKey, TValue>(items.Length);
        foreach (var item in items)
        {
            if (item.Key != null && !dictionary.ContainsKey(item.Key))
            {
                dictionary[item.Key] = item.Value;
            }
        }

        return SetBatch(dictionary, skipDuplicates);
    }

    /// <summary>
    /// 批量添加的重载方法，接受 IEnumerable
    /// </summary>
    /// <param name="items">键值对集合</param>
    /// <param name="skipDuplicates">是否跳过重复的键</param>
    /// <returns>成功添加的记录数</returns>
    public int SetBatch(IEnumerable<KeyValuePair<TKey, TValue>> items, bool skipDuplicates = true)
    {
        if (items == null)
            return 0;

        var dictionary = new Dictionary<TKey, TValue>();
        foreach (var item in items)
        {
            if (item.Key != null && !dictionary.ContainsKey(item.Key))
            {
                dictionary[item.Key] = item.Value;
            }
        }

        return SetBatch(dictionary, skipDuplicates);
    }

    /// <summary>
    /// 获取内存缓存统计信息
    /// </summary>
    public MemoryCacheStats? GetMemoryCacheStats()
    {
        return IsMemoryModeEnabled ? _memoryCache!.GetStats() : null;
    }

    // 辅助方法：从磁盘加载单个值
    private TValue? LoadFromDisk(TKey key)
    {
        // 使用现有的磁盘读取逻辑
        return GetFromDisk(key);
    }

    // 重构现有方法以支持内存模式
    private TValue? GetFromDisk(TKey key)
    {
        // 现有的磁盘读取逻辑
        if (!_index.TryGetValue(key, out var entryInfo))
            return default;

        return ReadValue(key, entryInfo);
    }

    private bool RemoveFromDisk(TKey key)
    {
        // 现有的磁盘删除逻辑
        if (!_index.TryRemove(key, out var entryInfo))
            return false;

        _deletedIndex[key] = entryInfo;
        return true;
    }

    /// <summary>
    /// 批量操作的内部类
    /// </summary>
    private class BatchOperation<T> where T : notnull
    {
        public T Key { get; set; } = default!;
        public TValue Value { get; set; } = default!;
        public byte[] KeyBytes { get; set; } = null!;
        public byte[] ValueBytes { get; set; } = null!;
        public long ValueHash { get; set; }
        public bool IsUpdate { get; set; }
        public IndexEntryInfo<T>? ExistingEntry { get; set; }
        public bool CanReuseSpace { get; set; }
        public long ValuePosition { get; set; }
        public long KeyPosition { get; set; } = -1;
    }

    /// <summary>
    /// 批量写入操作的内部类
    /// </summary>
    private class BatchWriteOperation
    {
        public BatchOperation<TKey> Operation { get; set; } = null!;
        public IndexEntryInfo<TKey> IndexEntry { get; set; } = null!;
    }
}