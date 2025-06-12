namespace UltraKV
{
    /// <summary>
    /// 压缩算法类型
    /// </summary>
    public enum CompressionType : byte
    {
        None = 0,
        Gzip = 1,
        Deflate = 2,
        Brotli = 3,
        LZ4 = 4,
        Zstd = 5,
        Snappy = 6,
        LZMA = 7
    }
}
