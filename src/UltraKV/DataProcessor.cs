namespace UltraKV;

/// <summary>
/// 数据处理管道，负责压缩和加密
/// </summary>
public class DataProcessor : IDisposable
{
    private readonly CompressionType _compressionType;
    private readonly EncryptionType _encryptionType;
    private readonly string? _encryptionKey;

    public DataProcessor(DatabaseHeader header, string? encryptionKey = null)
    {
        _compressionType = header.CompressionType;
        _encryptionType = header.EncryptionType;
        _encryptionKey = encryptionKey;
    }

    /// <summary>
    /// 处理数据（压缩 -> 加密）
    /// </summary>
    public byte[] ProcessData(ReadOnlySpan<byte> data)
    {
        byte[] result = data.ToArray();

        // 1. 压缩
        if (_compressionType != CompressionType.None)
        {
            result = CompressionHelper.Compress(result, _compressionType);
        }

        // 2. 加密
        if (_encryptionType != EncryptionType.None && !string.IsNullOrWhiteSpace(_encryptionKey))
        {
            result = EncryptionHelper.Encrypt(result, _encryptionType, _encryptionKey);
        }

        return result;
    }

    /// <summary>
    /// 逆向处理数据（解密 -> 解压缩）
    /// </summary>
    public byte[] UnprocessData(ReadOnlySpan<byte> data)
    {
        byte[] result = data.ToArray();

        // 1. 解密
        if (_encryptionType != EncryptionType.None && !string.IsNullOrWhiteSpace(_encryptionKey))
        {
            result = EncryptionHelper.Decrypt(result, _encryptionType, _encryptionKey);
        }

        // 2. 解压缩
        if (_compressionType != CompressionType.None)
        {
            result = CompressionHelper.Decompress(result, _compressionType);
        }

        return result;
    }

    public void Dispose()
    {
    }
}