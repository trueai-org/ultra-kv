namespace UltraKV;

/// <summary>
/// 快速文件写入器 - 专门用于文件末尾追加写入的缓冲机制
/// </summary>
internal class FastFileWriter : IDisposable
{
    private readonly FileStream _fileStream;
    private readonly UltraKVConfig _config;
    private readonly object _lock = new object();
    private readonly Timer? _flushTimer;

    private byte[] _buffer;
    private int _bufferPosition;
    private bool _disposed;
    private DateTime _lastWriteTime;
    private readonly int _bufferSize;

    public FastFileWriter(FileStream fileStream, UltraKVConfig config)
    {
        _config = config;
        _bufferSize = _config!.WriteBufferSizeKB * 1024;
        if (_bufferSize < 4096)
        {
            _bufferSize = 4096;
        }

        _fileStream = fileStream;
        _buffer = new byte[_bufferSize];
        _bufferPosition = 0;
        _lastWriteTime = DateTime.UtcNow;

        // 启动定时刷新（如果启用缓冲）
        if (_config.WriteBufferEnabled && _config.WriteBufferTimeThresholdMs > 0)
        {
            _flushTimer = new Timer(OnTimerFlush, null,
                _config.WriteBufferTimeThresholdMs,
                _config.WriteBufferTimeThresholdMs);
        }
    }

    /// <summary>
    /// 写入数据到文件末尾（使用缓冲）
    /// </summary>
    /// <param name="data">要写入的数据</param>
    /// <returns>写入位置</returns>
    public long WriteToEnd(byte[] data)
    {
        if (!_config.WriteBufferEnabled)
        {
            // 禁用缓冲时直接写入
            return WriteDirectly(data);
        }

        lock (_lock)
        {
            // 检查缓冲区是否有足够空间
            if (_bufferPosition + data.Length > _buffer.Length)
            {
                // 缓冲区不够，先刷新
                FlushBuffer();
            }

            // 如果单条数据超过缓冲区大小，直接写入
            if (data.Length > _buffer.Length)
            {
                return WriteDirectly(data);
            }

            // 记录写入位置
            long writePosition = _fileStream.Length + _bufferPosition;

            // 缓冲区不够大时，先刷新
            if (_bufferPosition + data.Length > _buffer.Length)
            {
                FlushBuffer();
            }

            // 写入缓冲区
            Array.Copy(data, 0, _buffer, _bufferPosition, data.Length);
            _bufferPosition += data.Length;
            _lastWriteTime = DateTime.UtcNow;

            // 检查是否需要立即刷新
            if (ShouldFlush())
            {
                FlushBuffer();
            }

            return writePosition;
        }
    }

    /// <summary>
    /// 直接写入指定位置（不使用缓冲，用于替换模式）
    /// </summary>
    /// <param name="position">写入位置</param>
    /// <param name="data">数据</param>
    public void WriteAt(long position, byte[] data)
    {
        lock (_lock)
        {
            // 如果有缓冲数据，先刷新
            if (_bufferPosition > 0)
            {
                FlushBuffer();
            }

            _fileStream.Seek(position, SeekOrigin.Begin);
            _fileStream.Write(data);
        }
    }

    /// <summary>
    /// 检查是否应该刷新缓冲区
    /// </summary>
    private bool ShouldFlush()
    {
        // 检查时间阈值
        if ((DateTime.UtcNow - _lastWriteTime).TotalMilliseconds >= _config.WriteBufferTimeThresholdMs)
        {
            return true;
        }

        // 检查缓冲区使用率
        if (_bufferPosition >= _buffer.Length * 0.8) // 80% 阈值
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 直接写入（不使用缓冲）
    /// </summary>
    private long WriteDirectly(byte[] data)
    {
        // 先刷新缓冲区
        if (_bufferPosition > 0)
        {
            FlushBuffer();
        }

        long position = _fileStream.Length;

        // 检查位置后决定
        if (_fileStream.Position != _fileStream.Length)
        {
            _fileStream.Seek(0, SeekOrigin.End);
        }

        _fileStream.Write(data);
        return position;
    }

    /// <summary>
    /// 刷新缓冲区到磁盘
    /// </summary>
    public void Flush()
    {
        lock (_lock)
        {
            FlushBuffer();
            _fileStream.Flush();
        }
    }

    /// <summary>
    /// 内部刷新缓冲区
    /// </summary>
    private void FlushBuffer()
    {
        if (_bufferPosition == 0) return;

        try
        {
            // 检查位置后决定
            if (_fileStream.Position != _fileStream.Length)
            {
                _fileStream.Seek(0, SeekOrigin.End);
            }

            _fileStream.Write(_buffer, 0, _bufferPosition);
            _bufferPosition = 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FastFileWriter flush error: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 定时器刷新回调
    /// </summary>
    private void OnTimerFlush(object? state)
    {
        try
        {
            if (_bufferPosition > 0)
            {
                lock (_lock)
                {
                    FlushBuffer();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FastFileWriter timer flush error: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取当前文件长度（包括缓冲区）
    /// </summary>
    public long GetCurrentLength()
    {
        lock (_lock)
        {
            return _fileStream.Length + _bufferPosition;
        }
    }

    /// <summary>
    /// 获取缓冲区统计信息
    /// </summary>
    public (int BufferedBytes, bool IsEnabled) GetStats()
    {
        return (_bufferPosition, _config.WriteBufferEnabled);
    }

    /// <summary>
    /// 清空缓冲
    /// </summary>
    public void ClearBuffer()
    {
        lock (_lock)
        {
            _bufferPosition = 0;
            _lastWriteTime = DateTime.UtcNow;

            Array.Clear(_buffer, 0, _buffer.Length); // 清空缓冲区

            _flushTimer?.Change(_config.WriteBufferTimeThresholdMs, _config.WriteBufferTimeThresholdMs); // 重置定时器

            Console.WriteLine("FastFileWriter buffer cleared and flushed to disk.");
        }
    }

    /// <summary>
    /// 获取当前文件末尾位置
    /// </summary>
    /// <returns>文件末尾位置</returns>
    public long GetCurrentEndPosition()
    {
        return _fileStream.Length + _bufferPosition;
    }

    /// <summary>
    /// 预分配文件空间
    /// </summary>
    /// <param name="size">需要预分配的大小</param>
    public void PreallocateSpace(long size)
    {
        if (size <= 0) return;

        var currentLength = _fileStream.Length;
        var newLength = currentLength + _bufferPosition + size;

        // 预分配文件空间以减少文件系统调用
        _fileStream.SetLength(newLength);
    }

    /// <summary>
    /// 批量写入数据到文件末尾
    /// </summary>
    /// <param name="data">要写入的数据</param>
    /// <returns>写入位置</returns>
    public long WriteToEndBatch(byte[] data)
    {
        if (data == null || data.Length == 0)
            return _fileStream.Length + _bufferPosition;

        var startPosition = _fileStream.Length + _bufferPosition;

        // 如果数据很大，直接写入文件
        if (data.Length > _bufferSize / 2)
        {
            Flush(); // 先刷新缓冲区
            _fileStream.Seek(0, SeekOrigin.End);
            _fileStream.Write(data);
            return startPosition;
        }

        // 检查缓冲区空间
        if (_bufferPosition + data.Length > _bufferSize)
        {
            Flush();
        }

        // 写入缓冲区
        Buffer.BlockCopy(data, 0, _buffer, _bufferPosition, data.Length);
        _bufferPosition += data.Length;

        return startPosition;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;

            try
            {
                Flush(); // 最后刷新
            }
            catch { }

            _flushTimer?.Dispose();
        }
    }
}