using System.Runtime.InteropServices;

namespace UltraKV
{
    /// <summary>
    /// 索引页内的单个索引条目
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct IndexEntry
    {
        public uint Magic;               // 4 bytes - 魔术数 "IDXE" - 用于快速定位
        public byte IsDeleted;           // 1 byte - 删除标记，0 表示未删除，1 表示已删除
        public int KeyLength;            // 4 bytes - Key数据长度（不包含当前条目的长度）
        public long ValuePosition;       // 8 bytes - 值数据在文件中的位置，默认：-1，表示未分配
        public int ValueLength;          // 4 bytes - 值数据真实长度（压缩/加密后）
        public long ValueHash;           // 8 bytes - 值 hash 值，用于快速验证数据完整性，避免重复计算
        public long Timestamp;           // 8 bytes - 时间戳
                                         // total 37 bytes

        public ushort Reserved3;         // 2 bytes
        public byte Reserved4;           // 1 bytes
                                         // 3 bytes - 预留字段对齐

        public const int SIZE = 40;

        public const uint MAGIC_NUMBER = 0x49445845; // "IDXE" - Index Entry

        public IndexEntry(int keyLength, int valueLength, long valueHash,
            long valuePosition = -1,
            long? timestamp = null,
            byte isDeleted = 0)
        {
            IsDeleted = isDeleted;
            ValuePosition = valuePosition; // -1: 默认未分配
            Magic = MAGIC_NUMBER;
            KeyLength = keyLength;
            ValueLength = valueLength;
            ValueHash = valueHash;
            Timestamp = timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        /// <summary>
        /// 验证魔术数是否正确
        /// </summary>
        public readonly bool HasValidMagic => Magic == MAGIC_NUMBER;

        /// <summary>
        /// 不验证 position
        /// </summary>
        public bool IsValidEntry => HasValidMagic && KeyLength > 0 && IsDeleted == 0;

        /// <summary>
        /// 验证索引条目是否有效，包括 KeyLength 和 ValuePosition
        /// </summary>
        public bool IsValidEntryValue => HasValidMagic && IsValidEntry && ValuePosition > 0;

        public override string ToString()
        {
            return $"IndexEntry:, Magic={Magic:X8}, " +
                   $"KeyLen={KeyLength}, " +
                   $"ValuePosition: Pos={ValuePosition}, " +
                   $"ValueLen={ValueLength}, " +
                   $"Timestamp={Timestamp}";
        }
    }

    /// <summary>
    /// 加密的 IndexEntry
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct IndexEntryEncrypted
    {
        public uint Magic;               // 4 bytes - 魔术数 "IDXE" - 用于快速定位
        public byte IsDeleted;           // 1 byte - 删除标记，0 表示未删除，1 表示已删除
        public long Hash;                // 8 bytes - 加密后的 hash，用于验证数据完整性
        public int Length;               // 4 bytes - 加密后的数据长度
                                         // total 17 bytes

        public ushort Reserved1;         // 2 bytes - 预留字段对齐
        public byte Reserved2;           // 1 bytes - 预留字段对齐
                                         // 3 bytes - 预留字段对齐

        public const int SIZE = 20;

        public const uint MAGIC_NUMBER = IndexEntry.MAGIC_NUMBER; // "IDXE" - Index Entry

        public IndexEntryEncrypted(long hash, int length, byte isDeleted = 0)
        {
            Magic = MAGIC_NUMBER;
            Hash = hash;
            Length = length;
            IsDeleted = isDeleted;
        }

        public readonly bool HasValidMagic => Magic == MAGIC_NUMBER ;

        public bool IsValidEntry => HasValidMagic && Length > 0 && IsDeleted == 0;

        public override string ToString()
        {
            return $"IndexEntryEncrypted:, Magic={Magic:X8}, " +
                   $"Hash={Hash:X16}, " +
                   $"Length={Length}";
        }
    }
}