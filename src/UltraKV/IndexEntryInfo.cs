namespace UltraKV
{
    /// <summary>
    /// 索引条目扫描结果
    /// </summary>
    public class IndexEntryInfo<TKey> where TKey : notnull
    {
        /// <summary>
        /// 索引条目的 Key，可能为 null
        /// </summary>
        public TKey? Key { get; set; }

        public long KeyPosition;         // 8 bytes - 在页面中的位置，Key 数据在文件中的位置，默认：-1，表示未分配
        public int KeyLength;            // 4 bytes - Key数据长度（不包含当前条目的长度）
        public long ValuePosition;       // 8 bytes - 值数据在文件中的位置，默认：-1，表示未分配
        public int ValueLength;          // 4 bytes - 值数据真实长度（压缩/加密后）
        public long ValueHash;           // 8 bytes - 值 hash 值，用于快速验证数据完整性，避免重复计算
        public byte IsUpdated;           // 1 byte - 值变更/更新标记，0 表示未变更/未更新，1 表示需要更新
        public byte IsDeleted;           // 1 byte - 删除标记，0 表示未删除，1 表示已删除
        public long Timestamp;           // 8 bytes - 时间戳

        /// <summary>
        /// 创建未删除的条目对象
        /// </summary>
        /// <param name="keyPosition"></param>
        /// <param name="entry"></param>
        public IndexEntryInfo(long keyPosition, IndexEntry entry)
        {
            KeyPosition = keyPosition;
            KeyLength = entry.KeyLength;
            ValuePosition = entry.ValuePosition;
            ValueLength = entry.ValueLength;
            ValueHash = entry.ValueHash;
            Timestamp = entry.Timestamp;
            IsUpdated = 0;
            IsDeleted = 0;
        }

        /// <summary>
        /// 创建一个需要待保存的对象
        /// </summary>
        /// <param name="keyLength"></param>
        /// <param name="valueLength"></param>
        /// <param name="valueHash"></param>
        /// <param name="keyPosition"></param>
        /// <param name="valuePosition"></param>
        /// <param name="isUpdated"></param>
        public IndexEntryInfo(int keyLength, int valueLength, long valueHash, long keyPosition, long valuePosition, byte isUpdated = 1)
        {
            KeyPosition = keyPosition;
            KeyLength = keyLength;
            ValuePosition = valuePosition;
            ValueLength = valueLength;
            ValueHash = valueHash;
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            IsUpdated = isUpdated;
            IsDeleted = 0;
        }

        /// <summary>
        /// 不验证 position
        /// </summary>
        public bool IsValidEntry => KeyLength > 0 && IsDeleted == 0;

        /// <summary>
        /// 验证索引条目是否有效，包括 KeyLength 和 ValuePosition
        /// </summary>
        public bool IsValidEntryValue => IsValidEntry && ValuePosition > 0;

        /// <summary>
        /// 转换为 IndexEntry 对象
        /// </summary>
        /// <returns></returns>
        public IndexEntry ToEntry()
        {
            return new IndexEntry(KeyLength, ValueLength, ValueHash, ValuePosition, Timestamp, IsDeleted);
        }

        /// <summary>
        /// 转为删除的加密 IndexEntry 对象
        /// </summary>
        /// <returns></returns>
        public IndexEntryEncrypted ToDeletedEntry()
        {
            return new IndexEntryEncrypted(-1, -1, IsDeleted);
        }
    }
}