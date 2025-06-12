namespace UltraKV
{
    /// <summary>
    /// 文件更新模式
    /// 文件追加模式：更新时追加到文件末尾，更加高性能，但可能导致文件碎片化
    /// 文件替换模式：更新时替换原有数据（当前数据长度不变或变小时），性能较低，但可以避免文件碎片化
    /// </summary>
    public enum FileUpdateMode
    {
        /// <summary>
        /// 追加模式，更新时追加到文件末尾
        /// </summary>
        Append = 0,

        /// <summary>
        /// 替换模式，更新时替换原有数据（当前数据长度不变或变小时）
        /// </summary>
        Replace = 1
    }
}