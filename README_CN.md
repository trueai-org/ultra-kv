# 🚀 UltraKV - 极速键值存储引擎

<div align="center">

[![MIT License](https://img.shields.io/badge/License-MIT-green.svg)](https://choosealicense.com/licenses/mit/)
[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![C#](https://img.shields.io/badge/Language-C%23-blue.svg)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![Performance](https://img.shields.io/badge/Performance-⚡%20Lightning%20Fast-red.svg)](#性能表现)

**一个专为 .NET 生态系统设计的超高性能、企业级键值存储解决方案**

[English](README.md) | [中文文档](README_CN.md)

</div>

## 🎯 极简代码，极致性能

<div align="center">

### **🚀 不到 1000 行代码实现完整数据库引擎**

*证明简洁与性能可以完美共存*

</div>

**UltraKV** 最令人惊叹的特点之一是用极其紧凑的代码库实现了企业级数据库性能。整个 **UltraKV** 引擎核心仅用 **不到 1000 行 C# 代码** 实现，充分展现了专注、高效设计的强大威力。

### 💡 哲学："完美是优秀的敌人"

UltraKV 证明了：
- **代码越少 = Bug 越少**
- **设计简单 = 性能更好**
- **范围专注 = 执行可靠**
- **逻辑清晰 = 维护容易**

> *"最好的代码是没有代码。次好的代码是简单高效到感觉像没有代码一样。"*
> 
## 安装使用

```bash
dotnet add package UltraKV

using var engine = new UltraKVEngine<string, string>("test.db");
engine.Set("key1", "value1");
var value = engine.Get("key1");
engine.Delete("key1")
```

## 📋 目录

- [🌟 项目概述](#-项目概述)
- [🔥 核心特性](#-核心特性)
- [⚡ 性能表现](#-性能表现)
- [🏗️ 架构设计](#️-架构设计)
- [🚀 快速开始](#-快速开始)
- [📖 详细使用指南](#-详细使用指南)
- [🔧 高级配置](#-高级配置)
- [📊 性能测试](#-性能测试)
- [🔐 安全特性](#-安全特性)
- [🛠️ 最佳实践](#️-最佳实践)
- [🤝 贡献指南](#-贡献指南)
- [📄 许可证](#-许可证)

## 🌟 项目概述

**UltraKV** 是一个单文件现代化的高性能键值存储系统。

### 🎯 设计目标

- **🚀 极致性能**: 单实例支持百万级 ops/s 的读写操作
- **🔒 数据安全**: 企业级加密、压缩和数据完整性保障
- **🛡️ 高可靠性**: 原子性事务、数据持久化和故障恢复
- **⚙️ 高度可配置**: 灵活的配置选项适应不同应用场景
- **📈 可扩展**: 支持多引擎管理和水平扩展

## 🔥 核心特性

### 🎯 UltraKV 引擎特性

| 特性分类    | 具体功能 | 说明 |
|-------------|---------|------|
| **🚀 性能优化** | 内存索引 + 磁盘存储 | 双重保障的高性能架构 |
| | 批量操作支持 | 高效的批量读写和删除 |
| | 智能缓冲机制 | 可配置的写入缓冲区 |
| | 并发控制 | 线程安全的并发访问 |
| **💾 存储管理** | 自动空间回收 | 智能的磁盘空间压实 |
| | 多种更新模式 | 追加模式和替换模式 |
| | 定时持久化 | 可配置的自动刷盘策略 |
| | 文件格式优化 | 紧凑的二进制存储格式 |
| **🔐 数据安全** | 多种加密算法 | AES256-GCM, ChaCha20-Poly1305 |
| | 压缩支持 | LZ4, Zstd, Snappy, Gzip, LZMA, Deflate, Brotli 等 |
| | 数据完整性校验 | 多种哈希算法验证 MD5, SHA1, SHA256, SHA3, <br /> SHA384, SHA512, BLAKE3, XXH3, XXH128  |
| | 原子性保证 | 写锁确保事务原子性 |

### 🏗️ 文件存储结构

#### UltraKV 存储格式
```
┌──────────────────────────────────────────────────┐
│ 1. 数据库头部信息 (固定 64 字节)   │   ← 文件开头      │
├──────────────────────────────────────────────────┤
│ 2. 值数据区域                                     │
│   ├─ 数据记录1 (可变长度)                        │
│   ├─ 数据记录2 (可变长度)                        │
│   └─ ...                                        │
├──────────────────────────────────────────────────┤
│ 3. 索引数据区域 [Start, End]    │ ← 文件末尾        │
│   ├─ 索引条目1 (Key + 位置信息)                   │
│   ├─ 索引条目2 (Key + 位置信息)                   │
│   └─ ...                                        │
└──────────────────────────────────────────────────┘
```

## ⚡ 性能表现

### 🏆 UltraKV 性能基准测试

> 测试环境: .NET 8.0, Windows 11, SSD 存储

| 操作类型 | 性能 (ops/sec) | 备注 |
|---------|----------------|------|
| **顺序写入** | **462,963** | 高频写入场景优化 |
| **批量插入** | **564,972** | 批量操作性能提升 |
| **随机读取** | **632,911** | 内存索引加速 |
| **包含检查** | **25,000,000** | 内存操作极速响应 |
| **数据删除** | **833,333** | 高效的删除操作 |
| **批量删除** | **1,562,500** | 批量删除性能卓越 |
| **数据更新** | **333,333** | 原地更新优化 |
| **随机访问** | **500,000** | 优异的随机访问性能 |

## 🚀 快速开始

### 📦 安装

```xml
<PackageReference Include="UltraKV" Version="1.0.0" />
```

### 🔧 基础使用

#### UltraKV 基础示例

```csharp
using UltraKV;

// 创建引擎管理器
using var manager = new UltraKVManager<string, string>("./data");

// 获取引擎实例
var engine = manager.GetEngine("my_database");

// 基础操作
engine.Set("user:1001", "John Doe");
engine.Set("user:1002", "Jane Smith");

// 读取数据
var user = engine.Get("user:1001"); // 返回: "John Doe"
var exists = engine.ContainsKey("user:1001"); // 返回: true

// 删除数据
engine.Remove("user:1002");

// 批量操作
var batch = new Dictionary<string, string>
{
    ["product:1"] = "Laptop",
    ["product:2"] = "Mouse",
    ["product:3"] = "Keyboard"
};
engine.SetBatch(batch);

// 持久化数据
engine.Flush();
```

#### 高级配置示例

```csharp
// 创建高性能配置
var config = new UltraKVConfig
{
    // 压缩配置
    CompressionType = CompressionType.LZ4,
    
    // 加密配置
    EncryptionType = EncryptionType.AES256GCM,
    EncryptionKey = "MySecureKey32BytesLong!@#$%^&*()",
    
    // 性能配置
    FileStreamBufferSizeKB = 1024, // 1MB 缓冲区
    WriteBufferSizeKB = 2048,      // 2MB 写入缓冲
    FlushInterval = 10,            // 10秒自动刷盘
    
    // 维护配置
    AutoCompactEnabled = true,     // 启用自动压实
    AutoCompactThreshold = 30,     // 30% 空间碎片触发压实
    
    // 文件更新模式
    FileUpdateMode = FileUpdateMode.Append // 追加模式获得更高性能
};

var engine = manager.GetEngine("high_performance_db", config);
```

## 📖 详细使用指南

### 🔧 配置选项详解

#### UltraKVConfig 核心配置

```csharp
public class UltraKVConfig
{
    // 🎯 性能相关
    public bool EnableMemoryMode { get; set; } = false;           // 内存模式
    public int FileStreamBufferSizeKB { get; set; } = 64;         // 文件缓冲区
    public bool EnableWriteBuffer { get; set; } = true;           // 写入缓冲
    public int WriteBufferSizeKB { get; set; } = 1024;            // 缓冲区大小
    
    // 🔐 安全相关
    public CompressionType CompressionType { get; set; }          // 压缩算法
    public EncryptionType EncryptionType { get; set; }            // 加密算法
    public HashType HashType { get; set; } = HashType.XXH3;       // 哈希算法
    public string? EncryptionKey { get; set; }                    // 加密密钥
    
    // 🔄 维护相关
    public bool AutoCompactEnabled { get; set; } = false;         // 自动压实
    public byte AutoCompactThreshold { get; set; } = 50;          // 压实阈值
    public ushort FlushInterval { get; set; } = 5;                // 刷盘间隔
    public FileUpdateMode FileUpdateMode { get; set; }            // 更新模式
    
    // 🛡️ 验证相关
    public bool EnableUpdateValidation { get; set; } = false;     // 更新验证
    public int MaxKeyLength { get; set; } = 4096;                 // 最大键长度
}
```

### 🔄 生命周期管理

```csharp
// 引擎管理器支持多引擎
using var manager = new UltraKVManager<string, object>("./databases");

// 创建不同用途的引擎
var userEngine = manager.GetEngine("users", UltraKVConfig.Default);
var sessionEngine = manager.GetEngine("sessions", UltraKVConfig.Minimal);
var cacheEngine = manager.GetEngine("cache", new UltraKVConfig 
{ 
    FlushInterval = 30,  // 缓存数据可以较少刷盘
    EnableMemoryMode = true  // 缓存启用内存模式
});

// 批量刷盘
manager.FlushAll();

// 关闭特定引擎
manager.CloseEngine("cache");

// 获取引擎列表
var engineNames = manager.GetEngineNames();
```

### 📊 性能监控

```csharp
// 获取引擎统计信息
var stats = engine.GetStats();
Console.WriteLine($"记录数: {stats.RecordCount}");
Console.WriteLine($"已删除: {stats.DeletedCount}");
Console.WriteLine($"文件大小: {stats.FileSize / 1024.0 / 1024.0:F2} MB");
Console.WriteLine($"删除率: {stats.DeletionRatio:P1}");
Console.WriteLine($"建议压实: {stats.ShrinkRecommended}");

// 手动触发压实
if (engine.ShouldShrink())
{
    engine.Compact(fullRebuild: false);
}
```

## 🔧 高级配置

### 🚀 性能优化配置

#### 高吞吐量写入场景
```csharp
var highThroughputConfig = new UltraKVConfig
{
    FileUpdateMode = FileUpdateMode.Append,  // 追加模式性能最佳
    WriteBufferSizeKB = 4096,                // 4MB 大缓冲区
    FileStreamBufferSizeKB = 2048,           // 2MB 文件缓冲
    FlushInterval = 30,                      // 较长刷盘间隔
    AutoCompactThreshold = 70                // 较高压实阈值
};
```

#### 低延迟读取场景
```csharp
var lowLatencyConfig = new UltraKVConfig
{
    EnableMemoryMode = true,                 // 内存模式获得最低延迟
    FlushInterval = 5,                       // 频繁刷盘保证数据安全
    EnableUpdateValidation = true            // 启用验证保证数据正确性
};
```

#### 存储空间敏感场景
```csharp
var compactConfig = new UltraKVConfig
{
    CompressionType = CompressionType.Zstd,  // 最佳压缩率
    AutoCompactEnabled = true,               // 启用自动压实
    AutoCompactThreshold = 20,               // 低阈值触发压实
    FileUpdateMode = FileUpdateMode.Replace  // 替换模式减少碎片
};
```

### 🔐 安全配置示例

#### 企业级安全配置
```csharp
var secureConfig = UltraKVConfig.Secure("MyEnterprise256BitSecretKey!@#");
// 等同于:
var secureConfig = new UltraKVConfig
{
    CompressionType = CompressionType.Gzip,
    EncryptionType = EncryptionType.AES256GCM,
    EncryptionKey = "MyEnterprise256BitSecretKey!@#",
    HashType = HashType.SHA256,
    EnableUpdateValidation = true
};
```

#### 调试和开发配置
```csharp
var debugConfig = UltraKVConfig.Debug; // 启用所有验证选项
```

### 🔄 压缩算法选择指南

| 算法 | 压缩率 | 速度 | 适用场景 |
|------|--------|------|----------|
| **LZ4** | 中等 | 极快 | 高性能需求 |
| **Zstd** | 优秀 | 快 | 平衡性能和压缩率 |
| **Snappy** | 中等 | 极快 | Google 生态系统 |
| **Gzip** | 良好 | 中等 | 通用压缩 |
| **Brotli** | 优秀 | 较慢 | Web 应用优化 |

### 🔐 加密算法选择指南

| 算法 | 安全级别 | 性能 | 适用场景 |
|------|----------|------|----------|
| **AES256-GCM** | 极高 | 优秀 | 企业级应用 |
| **ChaCha20-Poly1305** | 极高 | 优秀 | 移动设备优化 |

## 📊 性能测试

### 🧪 内置性能测试

项目包含了完整的性能测试套件，您可以运行以下测试：

```bash
# 克隆项目
git clone https://github.com/trueai-org/UltraKV.git
cd UltraKV

# 运行 UltraKV 性能测试
dotnet run --project src/UltraKV --configuration Release

# 运行对比测试
dotnet test src/UltraKV.Tests --configuration Release
```

### 📈 自定义性能测试

```csharp
// 性能测试示例
public async Task BenchmarkWritePerformance()
{
    using var manager = new UltraKVManager<string, string>("./benchmark");
    var engine = manager.GetEngine("test");
    
    const int iterations = 100_000;
    var stopwatch = Stopwatch.StartNew();
    
    for (int i = 0; i < iterations; i++)
    {
        engine.Set($"key_{i}", $"value_{i}");
        
        if (i % 1000 == 0)  // 每1000次操作刷盘一次
        {
            engine.Flush();
        }
    }
    
    engine.Flush();
    stopwatch.Stop();
    
    var opsPerSecond = iterations * 1000.0 / stopwatch.ElapsedMilliseconds;
    Console.WriteLine($"Write Performance: {opsPerSecond:N0} ops/sec");
}
```

### 🔄 压实性能测试

```csharp
// 测试压实操作性能
public void BenchmarkCompactPerformance()
{
    using var engine = new UltraKVEngine<string, string>("./compact_test.db");
    
    // 写入大量数据
    for (int i = 0; i < 50_000; i++)
    {
        engine.Set($"key_{i}", new string('x', 1024)); // 1KB 数据
    }
    
    // 删除50%的数据创建碎片
    for (int i = 0; i < 50_000; i += 2)
    {
        engine.Remove($"key_{i}");
    }
    
    var beforeSize = new FileInfo("./compact_test.db").Length;
    var beforeStats = engine.GetStats();
    
    var stopwatch = Stopwatch.StartNew();
    engine.Compact(fullRebuild: false);
    stopwatch.Stop();
    
    var afterSize = new FileInfo("./compact_test.db").Length;
    var afterStats = engine.GetStats();
    
    Console.WriteLine($"压实耗时: {stopwatch.ElapsedMilliseconds}ms");
    Console.WriteLine($"文件大小: {beforeSize / 1024 / 1024}MB -> {afterSize / 1024 / 1024}MB");
    Console.WriteLine($"空间节省: {(1 - (double)afterSize / beforeSize):P1}");
}
```

## 🔐 安全特性

### 🔒 数据加密

UltraKV 支持业界标准的加密算法：

- **AES256-GCM**: 广泛认可的企业级加密标准
- **ChaCha20-Poly1305**: 现代化的高性能加密算法

```csharp
// 启用加密存储
var encryptedEngine = manager.GetEngine("secure_data", new UltraKVConfig
{
    EncryptionType = EncryptionType.AES256GCM,
    EncryptionKey = "MySecure32ByteEncryptionKey12345"
});

// 数据将被自动加密存储
encryptedEngine.Set("sensitive_data", "confidential_information");
```

### 📋 数据完整性

多重数据完整性保障机制：

```csharp
var validatedConfig = new UltraKVConfig
{
    EnableUpdateValidation = true,  // 启用写入验证
    HashType = HashType.SHA256,     // 使用 SHA256 进行数据校验
};
```

支持的哈希算法：
- **XXH3**: 极速哈希，默认选择
- **SHA256**: 加密级别哈希
- **BLAKE3**: 现代化高性能哈希
- **XXH128**: 128位哈希，低碰撞率

## 🛠️ 最佳实践

### 💡 性能优化建议

1. **缓冲区配置**
```csharp
// 根据内存情况调整缓冲区大小
var config = new UltraKVConfig
{
    FileStreamBufferSizeKB = Environment.Is64BitProcess ? 1024 : 256,
    WriteBufferSizeKB = Environment.Is64BitProcess ? 4096 : 1024
};
```

2. **批量操作优化**
```csharp
// 使用批量操作提高性能
var batch = new Dictionary<string, string>();
for (int i = 0; i < 10000; i++)
{
    batch[$"key_{i}"] = $"value_{i}";
}
engine.SetBatch(batch);  // 比单个 Set 操作快数倍
```

3. **合理的刷盘策略**
```csharp
// 高频写入场景
var highWriteConfig = new UltraKVConfig
{
    FlushInterval = 30,  // 30秒刷盘一次
    WriteBufferSizeKB = 8192  // 8MB 缓冲区
};

// 低延迟场景
var lowLatencyConfig = new UltraKVConfig
{
    FlushInterval = 1,   // 1秒刷盘一次
    EnableUpdateValidation = true  // 启用验证
};
```

### 🔧 维护和监控

1. **定期监控统计信息**
```csharp
// 定期检查引擎状态
var timer = new Timer(async _ =>
{
    var stats = engine.GetStats();
    if (stats.DeletionRatio > 0.3)  // 删除率超过30%
    {
        Console.WriteLine("建议执行压实操作");
        if (engine.ShouldShrink())
        {
            engine.Compact();
        }
    }
}, null, TimeSpan.Zero, TimeSpan.FromMinutes(5));
```

2. **优雅的关闭处理**
```csharp
// 应用关闭时确保数据安全
AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
{
    manager.FlushAll();  // 刷新所有引擎
    manager.Dispose();   // 释放资源
};
```

### 🚨 错误处理

```csharp
try
{
    engine.Set("key", "value");
}
catch (InvalidOperationException ex) when (ex.Message.Contains("disposed"))
{
    // 引擎已被释放
    Console.WriteLine("引擎已关闭，请重新初始化");
}
catch (ArgumentException ex) when (ex.Message.Contains("EncryptionKey"))
{
    // 加密配置错误
    Console.WriteLine("加密密钥配置错误");
}
catch (IOException ex)
{
    // 磁盘IO错误
    Console.WriteLine($"磁盘操作失败: {ex.Message}");
}
```

## 🔄 数据迁移和备份

### 📤 数据导出

```csharp
// 导出所有数据
public void ExportData(UltraKVEngine<string, string> engine, string backupFile)
{
    var allKeys = engine.GetAllKeys();
    var backup = new Dictionary<string, string>();
    
    foreach (var key in allKeys)
    {
        var value = engine.Get(key);
        if (value != null)
        {
            backup[key] = value;
        }
    }
    
    var json = JsonSerializer.Serialize(backup, new JsonSerializerOptions 
    { 
        WriteIndented = true 
    });
    File.WriteAllText(backupFile, json);
}
```

### 📥 数据导入

```csharp
// 从备份恢复数据
public void ImportData(UltraKVEngine<string, string> engine, string backupFile)
{
    var json = File.ReadAllText(backupFile);
    var backup = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
    
    if (backup != null)
    {
        engine.SetBatch(backup);
        engine.Flush();
    }
}
```

## 🌐 与其他技术集成

### 🔄 与 ASP.NET Core 集成

```csharp
// Startup.cs 或 Program.cs
services.AddSingleton<UltraKVManager<string, object>>(provider =>
    new UltraKVManager<string, object>("./app_data"));

services.AddSingleton<IMemoryCache>(provider =>
{
    var manager = provider.GetService<UltraKVManager<string, object>>();
    return new UltraKVCache(manager.GetEngine("cache"));
});
```

### 🔧 自定义缓存实现

```csharp
public class UltraKVCache : IMemoryCache
{
    private readonly UltraKVEngine<string, object> _engine;
    
    public UltraKVCache(UltraKVEngine<string, object> engine)
    {
        _engine = engine;
    }
    
    public bool TryGetValue(object key, out object value)
    {
        value = _engine.Get(key.ToString()!);
        return value != null;
    }
    
    public ICacheEntry CreateEntry(object key)
    {
        return new UltraKVCacheEntry(key.ToString()!, _engine);
    }
    
    // ... 其他接口实现
}
```

## 🤝 贡献指南

我们欢迎所有形式的贡献！请阅读 [CONTRIBUTING.md](CONTRIBUTING.md) 了解详细信息。

### 🐛 问题报告

在提交问题之前，请确保：

1. 搜索现有的 issues
2. 提供详细的错误信息和重现步骤
3. 包含环境信息（.NET 版本、操作系统等）

### 💡 功能请求

我们欢迎功能建议！请在 issue 中详细描述：

1. 功能的使用场景
2. 预期的行为
3. 可能的实现方案

### 🔧 开发环境设置

```bash
# 1. Fork 并克隆项目
https://github.com/trueai-org/ultra-kv.git
cd ultra-kv

# 2. 安装依赖
dotnet restore

# 3. 运行测试
dotnet test

# 4. 构建项目
dotnet build --configuration Release
```

## 📚 参考资料

本项目参考了以下优秀的开源项目：

- [Lightning.NET](https://github.com/CoreyKaylor/Lightning.NET) - LMDB .NET 绑定
- [Fast-Persistent-Dictionary](https://github.com/jgric2/Fast-Persistent-Dictionary) - 持久化字典实现
- [Microsoft FASTER](https://github.com/microsoft/FASTER) - 高性能键值存储
- [DBreeze](https://github.com/hhblaze/DBreeze) - .NET 嵌入式数据库

## 📊 性能对比

| 数据库 | 写入 (ops/s) | 读取 (ops/s) | 特点 |
|--------|-------------|-------------|------|
| **UltraKV UltraKV** | **462,963** | **632,911** | 纯 .NET，零依赖 |
| FASTER | ~400,000 | ~1,000,000 | 微软出品，内存优化 |
| LevelDB (C++) | ~100,000 | ~200,000 | Google 出品，久经考验 |
| SQLite | ~50,000 | ~100,000 | 关系型，功能完整 |

> *性能数据基于相同硬件环境的基准测试，实际性能因环境而异*

## 📈 发展路线图

### 🎯 近期目标 (v1.1)
- [ ] 分布式支持和集群模式
- [ ] 更多压缩算法支持
- [ ] 性能监控和指标导出
- [ ] 数据库修复工具

### 🚀 中期目标 (v2.0)
- [ ] 支持复杂查询和索引
- [ ] 插件式存储后端
- [ ] 云原生支持
- [ ] 图形化管理界面

### 🌟 长期目标 (v3.0)
- [ ] 机器学习优化的性能调优
- [ ] 自动化运维和故障恢复
- [ ] 跨平台移动端支持

## 📱 社区和支持

- 💬 [讨论区](https://github.com/trueai-org/ultra-kv/discussions) - 技术讨论和问答
- 📧 [邮件列表](mailto:ultrakv@trueai.org) - 官方公告和更新
- 🐛 [问题跟踪](https://github.com/trueai-org/ultra-kv/issues) - Bug 报告和功能请求
- 📖 [Wiki](https://github.com/trueai-org/ultra-kv/wiki) - 详细文档和教程

## 📄 许可证

本项目采用 [MIT 许可证](LICENSE)。

---

<div align="center">

**⭐ 如果这个项目对您有帮助，请给我们一个 Star！⭐**

[🏠 首页](https://github.com/trueai-org/ultra-kv) • 
[📚 文档](https://github.com/trueai-org/ultra-kv/wiki) • 
[🐛 报告问题](https://github.com/trueai-org/ultra-kv/issues) • 
[💡 功能请求](https://github.com/trueai-org/ultra-kv/issues/new?template=feature_request.md)

Copyright © 2024 TrueAI.org. All rights reserved.

</div>