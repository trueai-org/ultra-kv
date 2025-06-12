namespace UltraKV;

/// <summary>
/// UltraKV 数据库配置
/// </summary>
public class UltraKVConfig
{
    /// <summary>
    /// 是否开启内存模式，开启内存模式时，读/写将会直接从内存中获取数据，而不是从磁盘读取，数据将全部加载到内存中
    /// </summary>
    public bool EnableMemoryMode { get; set; } = false;

    /// <summary>
    /// 是否开启更新验证，每次更新完成后校验值是否正确，默认 false，一般开发时使用
    /// </summary>
    public bool EnableUpdateValidation { get; set; } = false;

    /// <summary>
    /// 限制 Key 的最大长度配置
    /// 默认最大 Key 长度为 4096 字节
    /// </summary>
    public int MaxKeyLength { get; set; } = 4096;

    /// <summary>
    /// 压缩算法类型（数据库级别，创建后不可变更）
    /// </summary>
    public CompressionType CompressionType { get; set; } = CompressionType.None;

    /// <summary>
    /// 加密算法类型（数据库级别，创建后不可变更）
    /// </summary>
    public EncryptionType EncryptionType { get; set; } = EncryptionType.None;

    /// <summary>
    /// 数据对比校验 Hash 算法，默认保存值 Hash 的前 8 个字节，默认 XXH3（数据库级别，创建后不可变更）
    /// </summary>
    public HashType HashType { get; set; } = HashType.XXH3;

    /// <summary>
    /// 加密密钥（仅在创建数据库时使用）
    /// </summary>
    public string? EncryptionKey { get; set; }

    /// <summary>
    /// 缓冲区大小，单位：KB，默认 64KB
    /// </summary>
    public int FileStreamBufferSizeKB { get; set; } = 64;

    /// <summary>
    /// 是否开启自动压实（Auto Compact）
    /// </summary>
    public bool AutoCompactEnabled { get; set; } = false;

    /// <summary>
    /// 空闲空间超过多少百分比触发自动压实（0~255）
    /// 默认：50%
    /// </summary>
    public byte AutoCompactThreshold { get; set; } = 50;

    /// <summary>
    /// 更新频率，默认：1秒
    /// 定期刷磁盘时间间隔（秒）（0~65535），默认 5 秒，为 0 时表示不刷盘
    /// 控制数据刷新到磁盘的频率
    /// </summary>
    public ushort FlushInterval { get; set; } = 5;

    /// <summary>
    /// 索引重建阈值（0~100），默认：20%
    /// 1、索引扩容百分比：创建索引分区时，默认扩容的百分比。
    /// 2、索引重建百分比：当删除索引时，超出百分比则重建索引。
    /// </summary>
    public byte IndexRebuildThreshold { get; set; } = 20;

    /// <summary>
    /// 空闲空间压缩阈值（0~255），默认：50%
    /// </summary>
    public byte FreeSpaceCompactThreshold { get; set; } = 50;

    /// <summary>
    /// 文件更新模式
    /// 文件追加模式：更新时追加到文件末尾，更加高性能，但可能导致文件碎片化
    /// 文件替换模式：更新时替换原有数据（当前数据长度不变或变小时），性能较低，但可以避免文件碎片化
    /// </summary>
    public FileUpdateMode FileUpdateMode { get; set; } = FileUpdateMode.Append;

    /// <summary>
    /// 是否启用写入缓冲机制，默认启用
    /// </summary>
    public bool EnableWriteBuffer { get; set; } = true;

    /// <summary>
    /// 写入缓冲区大小，单位：KB
    /// </summary>
    public int WriteBufferSizeKB { get; set; } = 1024;

    /// <summary>
    /// 写入缓冲区时间阈值，单位：毫秒，默认 5000ms (5秒)
    /// </summary>
    public int WriteBufferTimeThresholdMs { get; set; } = 5000;

    /// <summary>
    /// 获取加密填充长度
    /// </summary>
    public int EncryptionPaddingLength => EncryptionType == EncryptionType.None ? 0 : 28;

    /// <summary>
    /// 验证配置有效性
    /// </summary>
    public void Validate()
    {
        if (FileStreamBufferSizeKB < 4)
            FileStreamBufferSizeKB = 4;

        // 验证加密配置
        if (EncryptionType != EncryptionType.None)
        {
            if (string.IsNullOrWhiteSpace(EncryptionKey))
                throw new ArgumentException("EncryptionKey is required when encryption is enabled");

            if (EncryptionKey.Length < 16)
                throw new ArgumentException("EncryptionKey must be at least 16 characters");
        }

        if (IndexRebuildThreshold > 100)
            IndexRebuildThreshold = 100;

        if (FlushInterval < 0)
            FlushInterval = 0;

        if (WriteBufferTimeThresholdMs < 100)
            WriteBufferTimeThresholdMs = 100;
    }

    /// <summary>
    /// 获取实际的GC空闲空间阈值百分比
    /// </summary>
    public double GetActualAutoCompactThreshold()
    {
        return 1 + AutoCompactThreshold / 100.0;
    }

    /// <summary>
    /// 创建默认配置
    /// </summary>
    public static UltraKVConfig Default => new();

    /// <summary>
    /// 创建最小配置
    /// </summary>
    public static UltraKVConfig Minimal => new()
    {
        MaxKeyLength = 64,
        FileStreamBufferSizeKB = 16,
    };

    /// <summary>
    /// 创建安全配置
    /// </summary>
    public static UltraKVConfig Secure(string encryptionKey) => new()
    {
        CompressionType = CompressionType.Gzip,
        EncryptionType = EncryptionType.AES256GCM,
        EncryptionKey = encryptionKey,
    };

    /// <summary>
    /// 创建调试配置（最严格的验证）
    /// </summary>
    public static UltraKVConfig Debug => new()
    {
        EnableUpdateValidation = true, // 启用更新验证
    };

    public override string ToString()
    {
        return $"UltraKVConfig [EnableMemoryMode={EnableMemoryMode}, " +
               $"EnableUpdateValidation={EnableUpdateValidation}, " +
               $"MaxKeyLength={MaxKeyLength}, " +
               $"CompressionType={CompressionType}, " +
               $"EncryptionType={EncryptionType}, " +
               $"HashType={HashType}, " +
               $"FileStreamBufferSizeKB={FileStreamBufferSizeKB}KB, " +
               $"AutoCompactEnabled={AutoCompactEnabled}, " +
               $"AutoCompactThreshold={AutoCompactThreshold}%, " +
               $"FlushInterval={FlushInterval}s, " +
               $"IndexRebuildThreshold={IndexRebuildThreshold}%, " +
               $"FreeSpaceCompactThreshold={FreeSpaceCompactThreshold}%, " +
               $"FileUpdateMode={FileUpdateMode}]";
    }

    /// <summary>
    /// 验证配置与数据库头部信息的兼容性
    /// </summary>
    /// <param name="databaseHeader"></param>
    /// <param name="config"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void ValidateConfigCompatibility(DatabaseHeader databaseHeader)
    {
        if (CompressionType != databaseHeader.CompressionType)
        {
            throw new InvalidOperationException(
                $"Compression type mismatch. Database: {databaseHeader.CompressionType}, Config: {CompressionType}");
        }

        if (EncryptionType != databaseHeader.EncryptionType)
        {
            throw new InvalidOperationException(
                $"Encryption type mismatch. Database: {databaseHeader.EncryptionType}, Config: {EncryptionType}");
        }

        if (HashType != databaseHeader.HashType)
        {
            throw new InvalidOperationException(
                $"Hash type mismatch. Database: {databaseHeader.HashType}, Config: {HashType}");
        }
    }
}