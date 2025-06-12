using System.Text;

namespace UltraKV
{
    /// <summary>
    /// 序列化帮助类
    /// </summary>
    public class SerializeHelper
    {
        private readonly DataProcessor _dataProcessor;

        public SerializeHelper(DataProcessor dataProcessor)
        {
            _dataProcessor = dataProcessor ?? throw new ArgumentNullException(nameof(dataProcessor));
        }

        /// <summary>
        /// 序列化 Key
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        public byte[] SerializeKey<TKey>(TKey key)
        {
            if (key is string str)
                return Encoding.UTF8.GetBytes(str);

            return System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(key);
        }

        /// <summary>
        /// 反序列化 Key
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <param name="keyBytes"></param>
        /// <returns></returns>
        public TKey DeserializeKey<TKey>(byte[] keyBytes)
        {
            if (typeof(TKey) == typeof(string))
                return (TKey)(object)Encoding.UTF8.GetString(keyBytes);

            return System.Text.Json.JsonSerializer.Deserialize<TKey>(keyBytes)!;
        }

        /// <summary>
        /// 序列化 Value
        /// </summary>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="value"></param>
        /// <returns></returns>
        public byte[] SerializeValue<TValue>(TValue value)
        {
            var jsonBytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(value);
            return _dataProcessor.ProcessData(jsonBytes);
        }

        /// <summary>
        /// 反序列化 Value
        /// </summary>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="valueBytes"></param>
        /// <returns></returns>
        public TValue DeserializeValue<TValue>(byte[] valueBytes)
        {
            var jsonBytes = _dataProcessor.UnprocessData(valueBytes);
            return System.Text.Json.JsonSerializer.Deserialize<TValue>(jsonBytes)!;
        }
    }
}