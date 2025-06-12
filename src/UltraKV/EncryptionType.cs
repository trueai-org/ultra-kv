namespace UltraKV
{
    /// <summary>
    /// 加密类型枚举
    /// </summary>
    public enum EncryptionType : byte
    {
        /// <summary>
        /// 无加密
        /// </summary>
        None = 0,

        /// <summary>
        /// AES256-GCM
        /// </summary>
        AES256GCM = 1,

        /// <summary>
        /// ChaCha20-Poly1305
        /// </summary>
        ChaCha20Poly1305 = 2,
    }
}