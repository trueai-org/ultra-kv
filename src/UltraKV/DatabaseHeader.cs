using System.Runtime.InteropServices;

namespace UltraKV;

/// <summary>
/// 数据库头部信息
/// ┌─────────────────────────────────────┐
/// │ 1. 数据库头部信息 (固定 64 字节)                  │ ← 文件开头
/// ├─────────────────────────────────────┤
/// │ 2. 值数据                                          │
/// ├─────────────────────────────────────┤
/// │ 3. 索引数据 [IndexStartOffset, IndexEndOffset]     │ ← 文件末尾
/// └─────────────────────────────────────┘
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct DatabaseHeader
{
    public uint Magic;                      // 4 bytes - 魔法数字
    public byte Version;                    // 1 bytes - 版本号
    public CompressionType CompressionType; // 1 byte - 压缩类型
    public EncryptionType EncryptionType;   // 1 byte - 加密类型
    public HashType HashType;               // 1 byte - 数据对比校验 Hash 算法
    public long CreatedTime;                // 8 bytes - 创建时间
    public long LastUpdatedTime;            // 8 bytes - 最后更新时间
    public long IndexStartOffset;           // 8 bytes - 索引起始偏移量
    public int IndexUsedSize;               // 4 bytes - 索引已使用大小（字节）
    public int IndexSpaceSize;              // 4 bytes - 索引总分配空间大小（字节）
    public int IndexCount;                  // 4 bytes - 索引数/条目数
                                            // total 44 bytes

    public long Reserved1;                  // 8 bytes
    public long Reserved2;                  // 8 bytes
                                            // 16字节 预留字段 64 - total bytes - 4 bytes

    /// <summary>
    /// 校验和字段，使用校验和，必须保持固定字节和预留字段的大小为 64 bytes
    /// </summary>
    public uint Checksum;                   // 4 bytes - 校验和

    public const uint MAGIC_NUMBER = 0x46534B56; // "FSKV" - UltraKV Database
    public const byte CURRENT_VERSION = 1;
    public const int SIZE = 64;

    public static DatabaseHeader Create(UltraKVConfig config)
    {
        var header = new DatabaseHeader
        {
            Magic = MAGIC_NUMBER,
            Version = CURRENT_VERSION,
            CompressionType = config.CompressionType,
            EncryptionType = config.EncryptionType,
            HashType = config.HashType,
            IndexCount = 0,
            CreatedTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            LastUpdatedTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };

        header.Checksum = CalculateChecksum(header);
        return header;
    }

    public bool IsValid()
    {
        return Magic == MAGIC_NUMBER &&
               Version <= CURRENT_VERSION &&
               Checksum == CalculateChecksum(this);
    }

    /// <summary>
    /// 更新数据库头部信息的最后更新时间，并重新计算校验和。
    /// </summary>
    /// <returns></returns>
    public DatabaseHeader UpdateTime()
    {
        LastUpdatedTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Checksum = CalculateChecksum(this);
        return this;
    }

    /// <summary>
    /// 更新数据库头部信息，使用配置对象的值来更新当前实例的字段。
    /// </summary>
    /// <param name="config"></param>
    public DatabaseHeader UpdateFromConfig(UltraKVConfig config)
    {
        Magic = MAGIC_NUMBER;
        Version = CURRENT_VERSION;
        return UpdateTime();
    }

    public static unsafe uint CalculateChecksum(DatabaseHeader header)
    {
        var tempHeader = header;

        tempHeader.Checksum = 0; // 清零校验和字段

        var bytes = new ReadOnlySpan<byte>(&tempHeader, SIZE - 4); // 排除校验和字段和保留字段
        uint hash = 2166136261u; // FNV-1a初始值

        foreach (byte b in bytes)
        {
            hash ^= b;
            hash *= 16777619u; // FNV-1a素数
        }

        return hash;
    }

    /// <summary>
    /// 写入数据库头部信息
    /// </summary>
    public DatabaseHeader WriteDatabaseHeader(FileStream fileStream, UltraKVConfig config)
    {
        fileStream.Seek(0, SeekOrigin.Begin);

        var headerBytes = new byte[SIZE];

        UpdateTime();

        unsafe
        {
            fixed (byte* ptr = headerBytes)
            {
                *(DatabaseHeader*)ptr = this;
            }
        }

        // 如果是加密数据
        if (config.EncryptionType != EncryptionType.None)
        {
            headerBytes = EncryptionHelper.Encrypt(headerBytes, config.EncryptionType, config.EncryptionKey!);
        }

        fileStream.Write(headerBytes);

        return this;
    }

    /// <summary>
    /// 读数据库头
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidDataException"></exception>
    public static DatabaseHeader ReadDatabaseHeader(FileStream fileStream, UltraKVConfig config)
    {
        fileStream.Seek(0, SeekOrigin.Begin);

        var length = SIZE + config.EncryptionPaddingLength;

        var headerBytes = new byte[length];

        if (fileStream.Read(headerBytes) != length)
            throw new InvalidDataException("Cannot read database header");

        // 如果是加密数据
        if (config.EncryptionType != EncryptionType.None)
        {
            headerBytes = EncryptionHelper.Decrypt(headerBytes, config.EncryptionType, config.EncryptionKey!);
        }

        unsafe
        {
            fixed (byte* ptr = headerBytes)
            {
                return *(DatabaseHeader*)ptr;
            }
        }
    }

    public override readonly string ToString()
    {
        return $"DatabaseHeader: Magic={Magic:X8}, " +
               $"Version={Version}, " +
               $"CompressionType={CompressionType}, " +
               $"EncryptionType={EncryptionType}, " +
               $"HashType={HashType}, " +
               $"CreatedTime={CreatedTime}, " +
               $"LastUpdatedTime={LastUpdatedTime}, " +
               $"IndexStartOffset={IndexStartOffset}, " +
               $"IndexUsedSize={IndexUsedSize}, " +
               $"IndexSpaceSize={IndexSpaceSize}, " +
               $"IndexCount={IndexCount}, " +
               $"Checksum={Checksum:X8}";
    }
}