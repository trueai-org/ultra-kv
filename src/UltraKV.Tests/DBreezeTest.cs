using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DBreeze;
using Xunit;
using Xunit.Abstractions;

namespace UltraKV.Tests
{
    /// <summary>
    /// 非单文件
    /// </summary>
    public class DBreezeTest : BaseTests
    {
        private readonly ITestOutputHelper _output;
        private readonly string _testDbPath;

        public DBreezeTest(ITestOutputHelper output)
        {
            _output = output;
            _testDbPath = Path.Combine(Path.GetTempPath(), "DBreezePerformanceTest_" + Guid.NewGuid().ToString("N"));
        }

        [Theory]
        [InlineData(10_000, "1万")]
        [InlineData(100_000, "10万")]
        [InlineData(1_000_000, "100万")]
        public async Task TestPerformance_InsertAndRead(int recordCount, string description)
        {
            // 确保测试目录存在
            Directory.CreateDirectory(_testDbPath);

            try
            {
                using var engine = new DBreezeEngine(new DBreezeConfiguration()
                {
                    DBreezeDataFolderName = _testDbPath,
                    Storage = DBreezeConfiguration.eStorage.DISK
                });

                _output.WriteLine($"开始测试 {description} 条记录的性能");
                _output.WriteLine($"测试数据库路径: {_testDbPath}");

                // 生成测试数据
                var testData = GenerateTestData(recordCount);
                _output.WriteLine($"生成了 {testData.Count} 条测试数据");

                // 测试插入性能
                var insertTime = await MeasureInsertPerformance(engine, testData, description);

                // 测试读取性能
                var readTime = await MeasureReadPerformance(engine, testData.Keys.ToList(), description);

                // 测试批量读取性能
                var batchReadTime = await MeasureBatchReadPerformance(engine, testData.Keys.ToList(), description);

                // 输出性能报告
                PrintPerformanceReport(description, recordCount, insertTime, readTime, batchReadTime);

                // 验证数据完整性
                await VerifyDataIntegrity(engine, testData);
            }
            finally
            {
                // 清理测试数据
                CleanupTestData();
            }
        }

        private Dictionary<string, string> GenerateTestData(int count)
        {
            var data = new Dictionary<string, string>();
            var random = new Random(42); // 使用固定种子确保测试可重复

            for (int i = 0; i < count; i++)
            {
                var key = $"key_{i:D10}";
                var value = GenerateRandomString(random, 100); // 100字符的随机值
                data[key] = value;
            }

            return data;
        }

        private string GenerateRandomString(Random random, int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var result = new StringBuilder(length);

            for (int i = 0; i < length; i++)
            {
                result.Append(chars[random.Next(chars.Length)]);
            }

            return result.ToString();
        }

        private async Task<TimeSpan> MeasureInsertPerformance(DBreezeEngine engine, Dictionary<string, string> testData, string description)
        {
            _output.WriteLine($"开始插入测试 - {description}");
            var stopwatch = Stopwatch.StartNew();

            await Task.Run(() =>
            {
                using var tran = engine.GetTransaction();

                foreach (var kvp in testData)
                {
                    tran.Insert("TestTable", kvp.Key, kvp.Value);
                }

                tran.Commit();
            });

            stopwatch.Stop();
            _output.WriteLine($"插入完成 - {description}: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");

            return stopwatch.Elapsed;
        }

        private async Task<TimeSpan> MeasureReadPerformance(DBreezeEngine engine, List<string> keys, string description)
        {
            _output.WriteLine($"开始单个读取测试 - {description}");
            var stopwatch = Stopwatch.StartNew();
            var readCount = 0;

            await Task.Run(() =>
            {
                using var tran = engine.GetTransaction();

                foreach (var key in keys)
                {
                    var row = tran.Select<string, string>("TestTable", key);
                    if (row.Exists)
                    {
                        readCount++;
                    }
                }
            });

            stopwatch.Stop();
            _output.WriteLine($"单个读取完成 - {description}: {stopwatch.Elapsed.TotalMilliseconds:F2} ms, 读取条数: {readCount}");

            return stopwatch.Elapsed;
        }

        private async Task<TimeSpan> MeasureBatchReadPerformance(DBreezeEngine engine, List<string> keys, string description)
        {
            _output.WriteLine($"开始批量读取测试 - {description}");
            var stopwatch = Stopwatch.StartNew();
            var readCount = 0;

            await Task.Run(() =>
            {
                using var tran = engine.GetTransaction();

                // 使用 SelectForward 进行范围查询
                foreach (var row in tran.SelectForward<string, string>("TestTable"))
                {
                    readCount++;
                }
            });

            stopwatch.Stop();
            _output.WriteLine($"批量读取完成 - {description}: {stopwatch.Elapsed.TotalMilliseconds:F2} ms, 读取条数: {readCount}");

            return stopwatch.Elapsed;
        }

        private void PrintPerformanceReport(string description, int recordCount, TimeSpan insertTime, TimeSpan readTime, TimeSpan batchReadTime)
        {
            _output.WriteLine($"\n=== {description} 条记录性能报告 ===");
            _output.WriteLine($"记录数量: {recordCount:N0}");
            _output.WriteLine($"插入时间: {insertTime.TotalMilliseconds:F2} ms");
            _output.WriteLine($"插入速度: {recordCount / insertTime.TotalSeconds:F0} 条/秒");
            _output.WriteLine($"单个读取时间: {readTime.TotalMilliseconds:F2} ms");
            _output.WriteLine($"单个读取速度: {recordCount / readTime.TotalSeconds:F0} 条/秒");
            _output.WriteLine($"批量读取时间: {batchReadTime.TotalMilliseconds:F2} ms");
            _output.WriteLine($"批量读取速度: {recordCount / batchReadTime.TotalSeconds:F0} 条/秒");
            _output.WriteLine($"平均单条插入时间: {insertTime.TotalMilliseconds / recordCount:F4} ms");
            _output.WriteLine($"平均单条读取时间: {readTime.TotalMilliseconds / recordCount:F4} ms");
            _output.WriteLine($"===========================\n");

            // 保存报告
            var reportPath = Path.Combine(Directory.GetCurrentDirectory(), $"PerformanceReport_{description}.txt");
            File.WriteAllText(reportPath, $"=== {description} 条记录性能报告 ===\n" +
                $"记录数量: {recordCount:N0}\n" +
                $"插入时间: {insertTime.TotalMilliseconds:F2} ms\n" +
                $"插入速度: {recordCount / insertTime.TotalSeconds:F0} 条/秒\n" +
                $"单个读取时间: {readTime.TotalMilliseconds:F2} ms\n" +
                $"单个读取速度: {recordCount / readTime.TotalSeconds:F0} 条/秒\n" +
                $"批量读取时间: {batchReadTime.TotalMilliseconds:F2} ms\n" +
                $"批量读取速度: {recordCount / batchReadTime.TotalSeconds:F0} 条/秒\n" +
                $"平均单条插入时间: {insertTime.TotalMilliseconds / recordCount:F4} ms\n" +
                $"平均单条读取时间: {readTime.TotalMilliseconds / recordCount:F4} ms\n");
        }

        private async Task VerifyDataIntegrity(DBreezeEngine engine, Dictionary<string, string> originalData)
        {
            _output.WriteLine("开始验证数据完整性...");
            var verifyCount = 0;
            var errorCount = 0;

            await Task.Run(() =>
            {
                using var tran = engine.GetTransaction();

                foreach (var kvp in originalData.Take(1000)) // 验证前1000条数据
                {
                    var row = tran.Select<string, string>("TestTable", kvp.Key);
                    verifyCount++;

                    if (!row.Exists || row.Value != kvp.Value)
                    {
                        errorCount++;
                        if (errorCount <= 5) // 只输出前5个错误
                        {
                            _output.WriteLine($"数据不匹配: Key={kvp.Key}, Expected={kvp.Value}, Actual={row.Value ?? "NULL"}");
                        }
                    }
                }
            });

            _output.WriteLine($"数据完整性验证完成: 验证了 {verifyCount} 条记录, 错误 {errorCount} 条");
            Assert.True(errorCount == 0, $"数据完整性验证失败: {errorCount} 条记录不匹配");
        }

        private void CleanupTestData()
        {
            try
            {
                if (Directory.Exists(_testDbPath))
                {
                    Directory.Delete(_testDbPath, true);
                    _output.WriteLine($"清理测试数据完成: {_testDbPath}");
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"清理测试数据失败: {ex.Message}");
            }
        }

        [Fact]
        public void TestBasicOperations()
        {
            Directory.CreateDirectory(_testDbPath);

            try
            {
                using var engine = new DBreezeEngine(new DBreezeConfiguration()
                {
                    DBreezeDataFolderName = _testDbPath,
                    Storage = DBreezeConfiguration.eStorage.DISK
                });

                // 基本插入测试
                using (var tran = engine.GetTransaction())
                {
                    tran.Insert("TestTable", "test_key", "test_value");
                    tran.Commit();
                }

                // 基本读取测试
                using (var tran = engine.GetTransaction())
                {
                    var row = tran.Select<string, string>("TestTable", "test_key");
                    Assert.True(row.Exists);
                    Assert.Equal("test_value", row.Value);
                }

                _output.WriteLine("基本操作测试通过");
            }
            finally
            {
                CleanupTestData();
            }
        }

        [Fact]
        public async Task TestConcurrentOperations()
        {
            Directory.CreateDirectory(_testDbPath);

            try
            {
                using var engine = new DBreezeEngine(new DBreezeConfiguration()
                {
                    DBreezeDataFolderName = _testDbPath,
                    Storage = DBreezeConfiguration.eStorage.DISK
                });

                const int taskCount = 10;
                const int recordsPerTask = 1000;

                _output.WriteLine($"开始并发测试: {taskCount} 个任务，每个任务 {recordsPerTask} 条记录");

                var tasks = new List<Task>();
                var stopwatch = Stopwatch.StartNew();

                for (int taskId = 0; taskId < taskCount; taskId++)
                {
                    var currentTaskId = taskId;
                    var task = Task.Run(() =>
                    {
                        using var tran = engine.GetTransaction();

                        for (int i = 0; i < recordsPerTask; i++)
                        {
                            var key = $"concurrent_key_{currentTaskId}_{i}";
                            var value = $"concurrent_value_{currentTaskId}_{i}";
                            tran.Insert("ConcurrentTable", key, value);
                        }

                        tran.Commit();
                    });

                    tasks.Add(task);
                }

                await Task.WhenAll(tasks);
                stopwatch.Stop();

                _output.WriteLine($"并发插入完成: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
                _output.WriteLine($"总插入速度: {(taskCount * recordsPerTask) / stopwatch.Elapsed.TotalSeconds:F0} 条/秒");

                // 验证并发插入的数据
                using (var tran = engine.GetTransaction())
                {
                    var count = 0;
                    foreach (var row in tran.SelectForward<string, string>("ConcurrentTable"))
                    {
                        count++;
                    }

                    Assert.Equal(taskCount * recordsPerTask, count);
                    _output.WriteLine($"并发测试验证通过: 插入了 {count} 条记录");
                }
            }
            finally
            {
                CleanupTestData();
            }
        }
    }
}