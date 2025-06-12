namespace UltraKV
{
    /// <summary>
    /// 哈希算法（MD5、SHA1、SHA256、SHA3、SHA384、SHA512、BLAKE3、XXH3、XXH128）
    /// </summary>
    public enum HashType : byte
    {
        MD5 = 0,
        SHA1 = 1,
        SHA256 = 2,
        SHA3 = 3,
        SHA384 = 4,
        SHA512 = 5,
        BLAKE3 = 6,
        XXH3 = 7,
        XXH128 = 8
    }
}