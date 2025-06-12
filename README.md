# 🚀 UltraKV - Lightning-Fast Key-Value Storage Engine

<div align="center">

[![MIT License](https://img.shields.io/badge/License-MIT-green.svg)](https://choosealicense.com/licenses/mit/)
[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![C#](https://img.shields.io/badge/Language-C%23-blue.svg)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![Performance](https://img.shields.io/badge/Performance-⚡%20Lightning%20Fast-red.svg)](#performance-benchmarks)

**An ultra-high performance, enterprise-grade key-value storage solution designed for the .NET ecosystem**

[English](README.md) | [中文文档](README_CN.md)

</div>

## 🎯 Minimal Code, Maximum Performance

<div align="center">

### **🚀 Less than 1,000 Lines of Code for a Complete Database Engine**

*Proving that simplicity and performance can coexist*

</div>

One of the most remarkable aspects of **UltraKV** is achieving enterprise-grade database performance with an incredibly compact codebase. The entire **UltraKV** engine core is implemented in **less than 1,000 lines of C# code**, demonstrating the power of focused, efficient design.


## Installation

```bash
dotnet add package UltraKV

using var engine = new UltraKVEngine<string, string>("test.db");
engine.Set("key1", "value1");
var value = engine.Get("key1");
engine.Delete("key1")
```

## 📋 Table of Contents

- [🌟 Project Overview](#-project-overview)
- [🔥 Key Features](#-key-features)
- [⚡ Performance Benchmarks](#-performance-benchmarks)
- [🏗️ Architecture Design](#️-architecture-design)
- [🚀 Quick Start](#-quick-start)
- [📖 Detailed Usage Guide](#-detailed-usage-guide)
- [🔧 Advanced Configuration](#-advanced-configuration)
- [📊 Performance Testing](#-performance-testing)
- [🔐 Security Features](#-security-features)
- [🛠️ Best Practices](#️-best-practices)
- [🤝 Contributing](#-contributing)
- [📄 License](#-license)

## 🌟 Project Overview

**UltraKV** is a modern, single file, high-performance key-value storage system.

### 🎯 Design Goals

- **🚀 Extreme Performance**: Supporting millions of ops/sec for read/write operations per instance
- **🔒 Data Security**: Enterprise-grade encryption, compression, and data integrity protection
- **🛡️ High Reliability**: Atomic transactions, data persistence, and fault recovery
- **⚙️ Highly Configurable**: Flexible configuration options for different application scenarios
- **📈 Scalable**: Multi-engine management and horizontal scaling support

## 🔥 Key Features

### 🎯 UltraKV Engine Features

| Feature Category | Specific Functionality | Description |
|-----------------|------------------------|-------------|
| **🚀 Performance Optimization** | In-Memory Index + Disk Storage | Dual-guarantee high-performance architecture |
| | Batch Operations Support | Efficient batch read/write and delete operations |
| | Smart Buffering Mechanism | Configurable write buffer |
| | Concurrency Control | Thread-safe concurrent access |
| **💾 Storage Management** | Automatic Space Reclaim | Intelligent disk space compaction |
| | Multiple Update Modes | Append mode and replace mode |
| | Scheduled Persistence | Configurable auto-flush strategy |
| | Optimized File Format | Compact binary storage format |
| **🔐 Data Security** | Multiple Encryption Algorithms | AES256-GCM, ChaCha20-Poly1305 |
| | Compression Support | LZ4, Zstd, Snappy, Gzip, etc. |
| | Data Integrity Verification | Multiple hash algorithms for validation |
| | Atomicity Guarantee | Write locks ensure transaction atomicity |

### 🏗️ File Storage Structure

#### UltraKV Storage Format
```
┌──────────────────────────────────────────────────┐
│ 1. Database Header (Fixed 64 bytes)   │ ← File Start │
├──────────────────────────────────────────────────┤
│ 2. Value Data Area                               │
│   ├─ Data Record 1 (Variable length)            │
│   ├─ Data Record 2 (Variable length)            │
│   └─ ...                                        │
├──────────────────────────────────────────────────┤
│ 3. Index Data Area [Start, End]    │ ← File End   │
│   ├─ Index Entry 1 (Key + Position info)        │
│   ├─ Index Entry 2 (Key + Position info)        │
│   └─ ...                                        │
└──────────────────────────────────────────────────┘
```


## ⚡ Performance Benchmarks

### 🏆 UltraKV Performance Benchmark

> Test Environment: .NET 8.0, Windows 11, SSD Storage

| Operation Type | Performance (ops/sec) | Notes |
|---------------|----------------------|-------|
| **Sequential Write** | **462,963** | Optimized for high-frequency writes |
| **Batch Insert** | **564,972** | Enhanced batch operation performance |
| **Random Read** | **632,911** | Accelerated by in-memory index |
| **Contains Check** | **25,000,000** | Ultra-fast memory operations |
| **Data Delete** | **833,333** | Efficient delete operations |
| **Batch Delete** | **1,562,500** | Outstanding batch delete performance |
| **Data Update** | **333,333** | In-place update optimization |
| **Random Access** | **500,000** | Excellent random access performance |

## 🚀 Quick Start

### 📦 Installation

```xml
<PackageReference Include="UltraKV" Version="1.0.0" />
```

### 🔧 Basic Usage

#### UltraKV Basic Example

```csharp
using UltraKV;

// Create engine manager
using var manager = new UltraKVManager<string, string>("./data");

// Get engine instance
var engine = manager.GetEngine("my_database");

// Basic operations
engine.Set("user:1001", "John Doe");
engine.Set("user:1002", "Jane Smith");

// Read data
var user = engine.Get("user:1001"); // Returns: "John Doe"
var exists = engine.ContainsKey("user:1001"); // Returns: true

// Delete data
engine.Remove("user:1002");

// Batch operations
var batch = new Dictionary<string, string>
{
    ["product:1"] = "Laptop",
    ["product:2"] = "Mouse",
    ["product:3"] = "Keyboard"
};
engine.SetBatch(batch);

// Persist data
engine.Flush();
```

#### Advanced Configuration Example

```csharp
// Create high-performance configuration
var config = new UltraKVConfig
{
    // Compression configuration
    CompressionType = CompressionType.LZ4,
    
    // Encryption configuration
    EncryptionType = EncryptionType.AES256GCM,
    EncryptionKey = "MySecureKey32BytesLong!@#$%^&*()",
    
    // Performance configuration
    FileStreamBufferSizeKB = 1024, // 1MB buffer
    WriteBufferSizeKB = 2048,      // 2MB write buffer
    FlushInterval = 10,            // 10-second auto-flush
    
    // Maintenance configuration
    AutoCompactEnabled = true,     // Enable auto-compaction
    AutoCompactThreshold = 30,     // 30% fragmentation triggers compaction
    
    // File update mode
    FileUpdateMode = FileUpdateMode.Append // Append mode for higher performance
};

var engine = manager.GetEngine("high_performance_db", config);
```

## 📖 Detailed Usage Guide

### 🔧 Configuration Options Explained

#### UltraKVConfig Core Configuration

```csharp
public class UltraKVConfig
{
    // 🎯 Performance Related
    public bool EnableMemoryMode { get; set; } = false;           // Memory mode
    public int FileStreamBufferSizeKB { get; set; } = 64;         // File buffer
    public bool EnableWriteBuffer { get; set; } = true;           // Write buffer
    public int WriteBufferSizeKB { get; set; } = 1024;            // Buffer size
    
    // 🔐 Security Related
    public CompressionType CompressionType { get; set; }          // Compression algorithm
    public EncryptionType EncryptionType { get; set; }            // Encryption algorithm
    public HashType HashType { get; set; } = HashType.XXH3;       // Hash algorithm
    public string? EncryptionKey { get; set; }                    // Encryption key
    
    // 🔄 Maintenance Related
    public bool AutoCompactEnabled { get; set; } = false;         // Auto-compaction
    public byte AutoCompactThreshold { get; set; } = 50;          // Compaction threshold
    public ushort FlushInterval { get; set; } = 5;                // Flush interval
    public FileUpdateMode FileUpdateMode { get; set; }            // Update mode
    
    // 🛡️ Validation Related
    public bool EnableUpdateValidation { get; set; } = false;     // Update validation
    public int MaxKeyLength { get; set; } = 4096;                 // Maximum key length
}
```

### 🔄 Lifecycle Management

```csharp
// Engine manager supports multiple engines
using var manager = new UltraKVManager<string, object>("./databases");

// Create engines for different purposes
var userEngine = manager.GetEngine("users", UltraKVConfig.Default);
var sessionEngine = manager.GetEngine("sessions", UltraKVConfig.Minimal);
var cacheEngine = manager.GetEngine("cache", new UltraKVConfig 
{ 
    FlushInterval = 30,      // Cache data can flush less frequently
    EnableMemoryMode = true  // Enable memory mode for cache
});

// Batch flush
manager.FlushAll();

// Close specific engine
manager.CloseEngine("cache");

// Get engine list
var engineNames = manager.GetEngineNames();
```

### 📊 Performance Monitoring

```csharp
// Get engine statistics
var stats = engine.GetStats();
Console.WriteLine($"Record Count: {stats.RecordCount}");
Console.WriteLine($"Deleted Count: {stats.DeletedCount}");
Console.WriteLine($"File Size: {stats.FileSize / 1024.0 / 1024.0:F2} MB");
Console.WriteLine($"Deletion Ratio: {stats.DeletionRatio:P1}");
Console.WriteLine($"Compaction Recommended: {stats.ShrinkRecommended}");

// Manually trigger compaction
if (engine.ShouldShrink())
{
    engine.Compact(fullRebuild: false);
}
```

## 🔧 Advanced Configuration

### 🚀 Performance Optimization Configuration

#### High Throughput Write Scenarios
```csharp
var highThroughputConfig = new UltraKVConfig
{
    FileUpdateMode = FileUpdateMode.Append,  // Append mode for best performance
    WriteBufferSizeKB = 4096,                // 4MB large buffer
    FileStreamBufferSizeKB = 2048,           // 2MB file buffer
    FlushInterval = 30,                      // Longer flush interval
    AutoCompactThreshold = 70                // Higher compaction threshold
};
```

#### Low Latency Read Scenarios
```csharp
var lowLatencyConfig = new UltraKVConfig
{
    EnableMemoryMode = true,                 // Memory mode for lowest latency
    FlushInterval = 5,                       // Frequent flushing for data safety
    EnableUpdateValidation = true            // Enable validation for data correctness
};
```

#### Storage Space Sensitive Scenarios
```csharp
var compactConfig = new UltraKVConfig
{
    CompressionType = CompressionType.Zstd,  // Best compression ratio
    AutoCompactEnabled = true,               // Enable auto-compaction
    AutoCompactThreshold = 20,               // Low threshold triggers compaction
    FileUpdateMode = FileUpdateMode.Replace  // Replace mode reduces fragmentation
};
```

### 🔐 Security Configuration Examples

#### Enterprise-Grade Security Configuration
```csharp
var secureConfig = UltraKVConfig.Secure("MyEnterprise256BitSecretKey!@#");
// Equivalent to:
var secureConfig = new UltraKVConfig
{
    CompressionType = CompressionType.Gzip,
    EncryptionType = EncryptionType.AES256GCM,
    EncryptionKey = "MyEnterprise256BitSecretKey!@#",
    HashType = HashType.SHA256,
    EnableUpdateValidation = true
};
```

#### Debug and Development Configuration
```csharp
var debugConfig = UltraKVConfig.Debug; // Enable all validation options
```

### 🔄 Compression Algorithm Selection Guide

| Algorithm | Compression Ratio | Speed | Use Case |
|-----------|------------------|-------|----------|
| **LZ4** | Medium | Extremely Fast | High-performance requirements |
| **Zstd** | Excellent | Fast | Balance performance and compression |
| **Snappy** | Medium | Extremely Fast | Google ecosystem |
| **Gzip** | Good | Medium | General-purpose compression |
| **Brotli** | Excellent | Slower | Web application optimization |

### 🔐 Encryption Algorithm Selection Guide

| Algorithm | Security Level | Performance | Use Case |
|-----------|---------------|-------------|----------|
| **AES256-GCM** | Extremely High | Excellent | Enterprise applications |
| **ChaCha20-Poly1305** | Extremely High | Excellent | Mobile device optimization |

## 📊 Performance Testing

### 🧪 Built-in Performance Tests

The project includes a complete performance testing suite. You can run the following tests:

```bash
# Clone the project
git clone https://github.com/trueai-org/UltraKV.git
cd UltraKV

# Run UltraKV performance tests
dotnet run --project src/UltraKV --configuration Release

# Run comparison tests
dotnet test src/UltraKV.Tests --configuration Release
```

### 📈 Custom Performance Testing

```csharp
// Performance testing example
public async Task BenchmarkWritePerformance()
{
    using var manager = new UltraKVManager<string, string>("./benchmark");
    var engine = manager.GetEngine("test");
    
    const int iterations = 100_000;
    var stopwatch = Stopwatch.StartNew();
    
    for (int i = 0; i < iterations; i++)
    {
        engine.Set($"key_{i}", $"value_{i}");
        
        if (i % 1000 == 0)  // Flush every 1000 operations
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

### 🔄 Compaction Performance Testing

```csharp
// Test compaction operation performance
public void BenchmarkCompactPerformance()
{
    using var engine = new UltraKVEngine<string, string>("./compact_test.db");
    
    // Write large amount of data
    for (int i = 0; i < 50_000; i++)
    {
        engine.Set($"key_{i}", new string('x', 1024)); // 1KB data
    }
    
    // Delete 50% of data to create fragmentation
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
    
    Console.WriteLine($"Compaction Time: {stopwatch.ElapsedMilliseconds}ms");
    Console.WriteLine($"File Size: {beforeSize / 1024 / 1024}MB -> {afterSize / 1024 / 1024}MB");
    Console.WriteLine($"Space Saved: {(1 - (double)afterSize / beforeSize):P1}");
}
```

## 🔐 Security Features

### 🔒 Data Encryption

UltraKV supports industry-standard encryption algorithms:

- **AES256-GCM**: Widely recognized enterprise-grade encryption standard
- **ChaCha20-Poly1305**: Modern high-performance encryption algorithm

```csharp
// Enable encrypted storage
var encryptedEngine = manager.GetEngine("secure_data", new UltraKVConfig
{
    EncryptionType = EncryptionType.AES256GCM,
    EncryptionKey = "MySecure32ByteEncryptionKey12345"
});

// Data will be automatically encrypted when stored
encryptedEngine.Set("sensitive_data", "confidential_information");
```

### 📋 Data Integrity

Multiple data integrity protection mechanisms:

```csharp
var validatedConfig = new UltraKVConfig
{
    EnableUpdateValidation = true,  // Enable write validation
    HashType = HashType.SHA256,     // Use SHA256 for data verification
};
```

Supported hash algorithms:
- **XXH3**: Ultra-fast hash, default choice
- **SHA256**: Cryptographic-grade hash
- **BLAKE3**: Modern high-performance hash
- **XXH128**: 128-bit hash with low collision rate

## 🛠️ Best Practices

### 💡 Performance Optimization Recommendations

1. **Buffer Configuration**
```csharp
// Adjust buffer size based on memory availability
var config = new UltraKVConfig
{
    FileStreamBufferSizeKB = Environment.Is64BitProcess ? 1024 : 256,
    WriteBufferSizeKB = Environment.Is64BitProcess ? 4096 : 1024
};
```

2. **Batch Operation Optimization**
```csharp
// Use batch operations to improve performance
var batch = new Dictionary<string, string>();
for (int i = 0; i < 10000; i++)
{
    batch[$"key_{i}"] = $"value_{i}";
}
engine.SetBatch(batch);  // Much faster than individual Set operations
```

3. **Reasonable Flush Strategy**
```csharp
// High-frequency write scenarios
var highWriteConfig = new UltraKVConfig
{
    FlushInterval = 30,      // Flush every 30 seconds
    WriteBufferSizeKB = 8192 // 8MB buffer
};

// Low-latency scenarios
var lowLatencyConfig = new UltraKVConfig
{
    FlushInterval = 1,       // Flush every 1 second
    EnableUpdateValidation = true  // Enable validation
};
```

### 🔧 Maintenance and Monitoring

1. **Regular Statistics Monitoring**
```csharp
// Regularly check engine status
var timer = new Timer(async _ =>
{
    var stats = engine.GetStats();
    if (stats.DeletionRatio > 0.3)  // Deletion ratio exceeds 30%
    {
        Console.WriteLine("Compaction operation recommended");
        if (engine.ShouldShrink())
        {
            engine.Compact();
        }
    }
}, null, TimeSpan.Zero, TimeSpan.FromMinutes(5));
```

2. **Graceful Shutdown Handling**
```csharp
// Ensure data safety when application shuts down
AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
{
    manager.FlushAll();  // Flush all engines
    manager.Dispose();   // Release resources
};
```

### 🚨 Error Handling

```csharp
try
{
    engine.Set("key", "value");
}
catch (InvalidOperationException ex) when (ex.Message.Contains("disposed"))
{
    // Engine has been disposed
    Console.WriteLine("Engine is closed, please reinitialize");
}
catch (ArgumentException ex) when (ex.Message.Contains("EncryptionKey"))
{
    // Encryption configuration error
    Console.WriteLine("Encryption key configuration error");
}
catch (IOException ex)
{
    // Disk IO error
    Console.WriteLine($"Disk operation failed: {ex.Message}");
}
```

## 🔄 Data Migration and Backup

### 📤 Data Export

```csharp
// Export all data
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

### 📥 Data Import

```csharp
// Restore data from backup
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

## 🌐 Integration with Other Technologies

### 🔄 ASP.NET Core Integration

```csharp
// Startup.cs or Program.cs
services.AddSingleton<UltraKVManager<string, object>>(provider =>
    new UltraKVManager<string, object>("./app_data"));

services.AddSingleton<IMemoryCache>(provider =>
{
    var manager = provider.GetService<UltraKVManager<string, object>>();
    return new UltraKVCache(manager.GetEngine("cache"));
});
```

### 🔧 Custom Cache Implementation

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
    
    // ... other interface implementations
}
```

## 🤝 Contributing

We welcome all forms of contribution! Please read [CONTRIBUTING.md](CONTRIBUTING.md) for detailed information.

### 🐛 Bug Reports

Before submitting an issue, please ensure:

1. Search existing issues
2. Provide detailed error information and reproduction steps
3. Include environment information (.NET version, operating system, etc.)

### 💡 Feature Requests

We welcome feature suggestions! Please describe in detail in the issue:

1. Use case for the feature
2. Expected behavior
3. Possible implementation approaches

### 🔧 Development Environment Setup

```bash
# 1. Fork and clone the project
git clone https://github.com/your-username/UltraKV.git
cd UltraKV

# 2. Install dependencies
dotnet restore

# 3. Run tests
dotnet test

# 4. Build project
dotnet build --configuration Release
```

## 📚 References

This project references the following excellent open-source projects:

- [Lightning.NET](https://github.com/CoreyKaylor/Lightning.NET) - LMDB .NET bindings
- [Fast-Persistent-Dictionary](https://github.com/jgric2/Fast-Persistent-Dictionary) - Persistent dictionary implementation
- [Microsoft FASTER](https://github.com/microsoft/FASTER) - High-performance key-value store
- [DBreeze](https://github.com/hhblaze/DBreeze) - .NET embedded database

## 📊 Performance Comparison

| Database | Write (ops/s) | Read (ops/s) | Features |
|----------|--------------|-------------|----------|
| **UltraKV UltraKV** | **462,963** | **632,911** | Pure .NET, zero dependencies |
| FASTER | ~400,000 | ~1,000,000 | Microsoft product, memory optimized |
| LevelDB (C++) | ~100,000 | ~200,000 | Google product, battle-tested |
| SQLite | ~50,000 | ~100,000 | Relational, feature-complete |

> *Performance data based on benchmark tests on the same hardware environment. Actual performance varies by environment*


## 📈 Roadmap

### 🎯 Near-term Goals (v1.1)
- [ ] Distributed support and cluster mode
- [ ] More compression algorithm support
- [ ] Performance monitoring and metrics export
- [ ] Database repair tools

### 🚀 Mid-term Goals (v2.0)
- [ ] Support for complex queries and indexing
- [ ] Plugin-based storage backends
- [ ] Cloud-native support
- [ ] Graphical management interface

### 🌟 Long-term Goals (v3.0)
- [ ] Machine learning optimized performance tuning
- [ ] Automated operations and fault recovery
- [ ] Cross-platform mobile support

## 📱 Community and Support

- 💬 [Discussions](https://github.com/trueai-org/UltraKV/discussions) - Technical discussions and Q&A
- 📧 [Mailing List](mailto:ultrakv@trueai.org) - Official announcements and updates
- 🐛 [Issue Tracker](https://github.com/trueai-org/UltraKV/issues) - Bug reports and feature requests
- 📖 [Wiki](https://github.com/trueai-org/UltraKV/wiki) - Detailed documentation and tutorials

## 📄 License

This project is licensed under the [MIT License](LICENSE).

---

<div align="center">

**⭐ If this project helps you, please give us a Star! ⭐**

[🏠 Home](https://github.com/trueai-org/UltraKV) • 
[📚 Documentation](https://github.com/trueai-org/UltraKV/wiki) • 
[🐛 Report Issues](https://github.com/trueai-org/UltraKV/issues) • 
[💡 Feature Requests](https://github.com/trueai-org/UltraKV/issues/new?template=feature_request.md)

Copyright © 2024 TrueAI.org. All rights reserved.

</div>