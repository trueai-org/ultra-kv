using LightningDB;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using Xunit.Abstractions;

namespace UltraKV.Tests
{
    public class LightningPerformanceTest : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _testDir;
        private LightningEnvironment _environment;
        private LightningDatabase _database;

        public LightningPerformanceTest(ITestOutputHelper output)
        {
            _output = output;
            _testDir = Path.Combine(Path.GetTempPath(), "LightningPerformanceTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
        }

        private void InitializeLightning()
        {
            // 清理之前的实例
            _database?.Dispose();
            _environment?.Dispose();

            // 创建 Lightning 环境
            _environment = new LightningEnvironment(_testDir)
            {
                MaxDatabases = 1,
                MapSize = 1024L * 1024L * 100L // 10MB, 1024L * 1024L * 1024L * 10L // 10GB
            };

            _environment.Open();

            // 创建数据库
            using var tx = _environment.BeginTransaction();
            _database = tx.OpenDatabase("test", new DatabaseConfiguration { Flags = DatabaseOpenFlags.Create });
            tx.Commit();
        }

        [Theory]
        [InlineData(10_000, "1万")]
        [InlineData(100_000, "10万")]
        [InlineData(1_000_000, "100万")]
        public async Task TestPerformance_InsertAndRead(int recordCount, string description)
        {

            // 打开压缩后的数据库（和普通数据库完全一样）
            using var env = new LightningEnvironment(Directory.GetCurrentDirectory());
            env.Open();

            // 正常使用数据库
            using var tx = env.BeginTransaction(TransactionBeginFlags.ReadOnly);
            using var db = tx.OpenDatabase();

            var result = tx.Get(db, Encoding.UTF8.GetBytes("test"));
            if (result.resultCode == MDBResultCode.Success)
            {
                // 处理数据
            }

            InitializeLightning();

            try
            {
                _output.WriteLine($"开始测试 {description} 条记录的 Lightning.NET 性能");
                _output.WriteLine($"测试数据库路径: {_testDir}");

                // 生成测试数据
                var testData = GenerateTestData(recordCount);
                _output.WriteLine($"生成了 {testData.Count} 条测试数据");

                // 测试插入性能
                var insertTime = await MeasureInsertPerformance(testData, description);

                // 测试读取性能
                var readTime = await MeasureReadPerformance(testData.Keys.ToList(), description);

                // 测试批量读取性能
                var batchReadTime = await MeasureBatchReadPerformance(testData.Keys.ToList(), description);

                // 测试更新性能
                var updateTime = await MeasureUpdatePerformance(testData, description);

                //// 测试删除性能
                //var deleteTime = await MeasureDeletePerformance(testData.Keys.ToList(), description);

                //// 输出性能报告
                //PrintPerformanceReport(description, recordCount, insertTime, readTime, batchReadTime, updateTime, deleteTime);

                //// 收缩数据库
                //_output.WriteLine("开始收缩数据库...");
                //var result = _environment.CopyTo(Directory.GetCurrentDirectory(), compact: true);
                //_output.WriteLine($"数据库收缩完成: {result}");


                // 验证数据完整性（在删除之前重新插入数据）
                await ReinsertDataForVerification(testData);

                //await VerifyDataIntegrity(testData);
            }
            finally
            {
                // 清理会在 Dispose 中进行
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

        private async Task<TimeSpan> MeasureInsertPerformance(Dictionary<string, string> testData, string description)
        {
            _output.WriteLine($"开始插入测试 - {description}");
            var stopwatch = Stopwatch.StartNew();

            await Task.Run(() =>
            {
                using var tx = _environment.BeginTransaction();

                foreach (var kvp in testData)
                {
                    tx.Put(_database, Encoding.UTF8.GetBytes(kvp.Key), Encoding.UTF8.GetBytes(kvp.Value));
                }

                tx.Commit();
            });

            stopwatch.Stop();
            _output.WriteLine($"插入完成 - {description}: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");

            await Task.Run(() =>
            {
                using var tx = _environment.BeginTransaction();

                foreach (var kvp in testData)
                {
                    var xx = tx.Get(_database, Encoding.UTF8.GetBytes(kvp.Key));
                    if (xx.resultCode == MDBResultCode.KeyExist)
                    {
                        var value = Encoding.UTF8.GetString(xx.value.AsSpan());

                    }
                }

                tx.Commit();
            });



            return stopwatch.Elapsed;
        }

        private async Task<TimeSpan> MeasureReadPerformance(List<string> keys, string description)
        {
            _output.WriteLine($"开始读取测试 - {description}");
            var stopwatch = Stopwatch.StartNew();
            var readCount = 0;

            await Task.Run(() =>
            {
                using var tx = _environment.BeginTransaction(TransactionBeginFlags.ReadOnly);

                foreach (var key in keys)
                {
                    try
                    {
                        var result = tx.Get(_database, Encoding.UTF8.GetBytes(key));
                        if (result.resultCode == MDBResultCode.KeyExist)
                        {
                            readCount++;
                        }
                    }
                    catch (LightningException ex)
                    {
                        // 忽略未找到的记录
                    }
                }

                tx.Commit();
            });

            stopwatch.Stop();
            _output.WriteLine($"读取完成 - {description}: {stopwatch.Elapsed.TotalMilliseconds:F2} ms, 读取条数: {readCount}");

            return stopwatch.Elapsed;
        }

        private async Task<TimeSpan> MeasureBatchReadPerformance(List<string> keys, string description)
        {
            _output.WriteLine($"开始批量读取测试 - {description}");
            var stopwatch = Stopwatch.StartNew();
            var readCount = 0;

            await Task.Run(() =>
            {
                using var tx = _environment.BeginTransaction(TransactionBeginFlags.ReadOnly);
                using var cursor = tx.CreateCursor(_database);

                // 使用游标进行批量读取
                var batchSize = 1000;
                for (int i = 0; i < keys.Count; i += batchSize)
                {
                    var batch = keys.Skip(i).Take(batchSize);
                    foreach (var key in batch)
                    {
                        try
                        {
                            //if (cursor.MoveToKey(Encoding.UTF8.GetBytes(key)))
                            //{
                            //    readCount++;
                            //}
                        }
                        catch (LightningException ex)
                        {
                            // 忽略未找到的记录
                        }
                    }
                }

                tx.Commit();
            });

            stopwatch.Stop();
            _output.WriteLine($"批量读取完成 - {description}: {stopwatch.Elapsed.TotalMilliseconds:F2} ms, 读取条数: {readCount}");

            return stopwatch.Elapsed;
        }

        private async Task<TimeSpan> MeasureUpdatePerformance(Dictionary<string, string> testData, string description)
        {
            _output.WriteLine($"开始更新测试 - {description}");
            var stopwatch = Stopwatch.StartNew();

            await Task.Run(() =>
            {
                using var tx = _environment.BeginTransaction();

                foreach (var kvp in testData)
                {
                    var updatedValue = kvp.Value + "_updated";
                    tx.Put(_database, Encoding.UTF8.GetBytes(kvp.Key), Encoding.UTF8.GetBytes(updatedValue));
                }

                tx.Commit();
            });

            stopwatch.Stop();
            _output.WriteLine($"更新完成 - {description}: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");

            return stopwatch.Elapsed;
        }

        private async Task<TimeSpan> MeasureDeletePerformance(List<string> keys, string description)
        {
            _output.WriteLine($"开始删除测试 - {description}");
            var stopwatch = Stopwatch.StartNew();
            var deleteCount = 0;

            await Task.Run(() =>
            {
                using var tx = _environment.BeginTransaction();

                foreach (var key in keys)
                {
                    try
                    {
                        var res = tx.Delete(_database, Encoding.UTF8.GetBytes(key));
                        if (res.HasFlag(MDBResultCode.KeyExist))
                        {
                            deleteCount++;
                        }
                    }
                    catch (LightningException ex)
                    {
                        // 忽略未找到的记录
                    }
                }

                tx.Commit();
            });

            stopwatch.Stop();
            _output.WriteLine($"删除完成 - {description}: {stopwatch.Elapsed.TotalMilliseconds:F2} ms, 删除条数: {deleteCount}");

            return stopwatch.Elapsed;
        }

        private async Task ReinsertDataForVerification(Dictionary<string, string> testData)
        {
            await Task.Run(() =>
            {
                using var tx = _environment.BeginTransaction();

                foreach (var kvp in testData)
                {
                    tx.Put(_database, Encoding.UTF8.GetBytes(kvp.Key), Encoding.UTF8.GetBytes(kvp.Value));
                }

                tx.Commit();
            });
        }

        private void PrintPerformanceReport(string description, int recordCount, TimeSpan insertTime, TimeSpan readTime, TimeSpan batchReadTime, TimeSpan updateTime, TimeSpan deleteTime)
        {
            _output.WriteLine($"\n=== {description} 条记录 Lightning.NET 性能报告 ===");
            _output.WriteLine($"记录数量: {recordCount:N0}");
            _output.WriteLine($"插入时间: {insertTime.TotalMilliseconds:F2} ms");
            _output.WriteLine($"插入速度: {recordCount / insertTime.TotalSeconds:F0} 条/秒");
            _output.WriteLine($"读取时间: {readTime.TotalMilliseconds:F2} ms");
            _output.WriteLine($"读取速度: {recordCount / readTime.TotalSeconds:F0} 条/秒");
            _output.WriteLine($"批量读取时间: {batchReadTime.TotalMilliseconds:F2} ms");
            _output.WriteLine($"批量读取速度: {recordCount / batchReadTime.TotalSeconds:F0} 条/秒");
            _output.WriteLine($"更新时间: {updateTime.TotalMilliseconds:F2} ms");
            _output.WriteLine($"更新速度: {recordCount / updateTime.TotalSeconds:F0} 条/秒");
            _output.WriteLine($"删除时间: {deleteTime.TotalMilliseconds:F2} ms");
            _output.WriteLine($"删除速度: {recordCount / deleteTime.TotalSeconds:F0} 条/秒");
            _output.WriteLine($"平均单条插入时间: {insertTime.TotalMilliseconds / recordCount:F4} ms");
            _output.WriteLine($"平均单条读取时间: {readTime.TotalMilliseconds / recordCount:F4} ms");
            _output.WriteLine($"平均单条更新时间: {updateTime.TotalMilliseconds / recordCount:F4} ms");
            _output.WriteLine($"平均单条删除时间: {deleteTime.TotalMilliseconds / recordCount:F4} ms");
            _output.WriteLine($"===========================\n");

            // 保存到文件
            var reportFile = Path.Combine(Directory.GetCurrentDirectory(), $"LightningPerformanceReport_{description}.txt");
            File.WriteAllText(reportFile, $"=== {description} 条记录 Lightning.NET 性能报告 ===\n" +
                $"记录数量: {recordCount:N0}\n" +
                $"插入时间: {insertTime.TotalMilliseconds:F2} ms\n" +
                $"插入速度: {recordCount / insertTime.TotalSeconds:F0} 条/秒\n" +
                $"读取时间: {readTime.TotalMilliseconds:F2} ms\n" +
                $"读取速度: {recordCount / readTime.TotalSeconds:F0} 条/秒\n" +
                $"批量读取时间: {batchReadTime.TotalMilliseconds:F2} ms\n" +
                $"批量读取速度: {recordCount / batchReadTime.TotalSeconds:F0} 条/秒\n" +
                $"更新时间: {updateTime.TotalMilliseconds:F2} ms\n" +
                $"更新速度: {recordCount / updateTime.TotalSeconds:F0} 条/秒\n" +
                $"删除时间: {deleteTime.TotalMilliseconds:F2} ms\n" +
                $"删除速度: {recordCount / deleteTime.TotalSeconds:F0} 条/秒\n" +
                $"平均单条插入时间: {insertTime.TotalMilliseconds / recordCount:F4} ms\n" +
                $"平均单条读取时间: {readTime.TotalMilliseconds / recordCount:F4} ms\n" +
                $"平均单条更新时间: {updateTime.TotalMilliseconds / recordCount:F4} ms\n" +
                $"平均单条删除时间: {deleteTime.TotalMilliseconds / recordCount:F4} ms\n" +
                "===========================\n");
        }

        private async Task VerifyDataIntegrity(Dictionary<string, string> originalData)
        {
            _output.WriteLine("开始验证数据完整性...");
            var errorCount = 0;

            await Task.Run(() =>
            {
                using var tx = _environment.BeginTransaction(TransactionBeginFlags.ReadOnly);

                foreach (var kvp in originalData)
                {
                    try
                    {
                        var result = tx.Get(_database, Encoding.UTF8.GetBytes(kvp.Key));
                        if (result.resultCode == MDBResultCode.KeyExist)
                        {
                            var storedValue = Encoding.UTF8.GetString(result.value.AsSpan());
                            if (storedValue != kvp.Value)
                            {
                                errorCount++;
                                _output.WriteLine($"数据不匹配 - Key: {kvp.Key}, Expected: {kvp.Value}, Actual: {storedValue}");
                            }
                        }
                        else
                        {
                            errorCount++;
                            _output.WriteLine($"数据缺失 - Key: {kvp.Key}");
                        }
                    }
                    catch (LightningException ex)
                    {
                        errorCount++;
                        _output.WriteLine($"数据缺失 - Key: {kvp.Key}");
                    }
                }

                tx.Commit();
            });

            _output.WriteLine($"数据完整性验证完成 - 错误数量: {errorCount}");
            Assert.True(errorCount == 0, $"数据完整性验证失败: {errorCount} 条记录不匹配");
        }

        [Fact]
        public async Task TestBasicOperations()
        {
            InitializeLightning();

            try
            {
                _output.WriteLine("开始基础操作测试");

                using var tx = _environment.BeginTransaction();

                // 测试插入
                var testKey = "test_key";
                var testValue = "test_value";
                tx.Put(_database, Encoding.UTF8.GetBytes(testKey), Encoding.UTF8.GetBytes(testValue));
                tx.Commit();

                // 测试读取
                using var readTx = _environment.BeginTransaction(TransactionBeginFlags.ReadOnly);
                var result = readTx.Get(_database, Encoding.UTF8.GetBytes(testKey));
                Assert.True(result.resultCode == MDBResultCode.KeyExist);
                var retrievedValue = Encoding.UTF8.GetString(result.value.AsSpan());
                Assert.Equal(testValue, retrievedValue);
                readTx.Commit();

                _output.WriteLine("基础操作测试通过");
            }
            finally
            {
                // 清理会在 Dispose 中进行
            }
        }

        [Fact]
        public async Task TestConcurrentOperations()
        {
            InitializeLightning();

            try
            {
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
                        using var tx = _environment.BeginTransaction();

                        for (int i = 0; i < recordsPerTask; i++)
                        {
                            var key = $"concurrent_key_{currentTaskId}_{i}";
                            var value = $"concurrent_value_{currentTaskId}_{i}";
                            tx.Put(_database, Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(value));
                        }

                        tx.Commit();
                    });

                    tasks.Add(task);
                }

                await Task.WhenAll(tasks);
                stopwatch.Stop();

                _output.WriteLine($"并发插入完成: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
                _output.WriteLine($"总插入速度: {(taskCount * recordsPerTask) / stopwatch.Elapsed.TotalSeconds:F0} 条/秒");

                // 验证并发插入的数据
                var verifyCount = 0;
                using var verifyTx = _environment.BeginTransaction(TransactionBeginFlags.ReadOnly);

                for (int taskId = 0; taskId < taskCount; taskId++)
                {
                    for (int i = 0; i < recordsPerTask; i++)
                    {
                        var key = $"concurrent_key_{taskId}_{i}";
                        try
                        {
                            var result = verifyTx.Get(_database, Encoding.UTF8.GetBytes(key));
                            if (result.resultCode == MDBResultCode.KeyExist)
                            {
                                verifyCount++;
                            }
                        }
                        catch (LightningException ex)
                        {
                            // 忽略未找到的记录
                        }
                    }
                }

                verifyTx.Commit();

                Assert.Equal(taskCount * recordsPerTask, verifyCount);
                _output.WriteLine($"并发测试验证通过: 插入了 {verifyCount} 条记录");
            }
            finally
            {
                // 清理会在 Dispose 中进行
            }
        }

        [Fact]
        public async Task TestIteratorPerformance()
        {
            InitializeLightning();

            try
            {
                const int recordCount = 10_000;
                _output.WriteLine($"开始游标迭代测试: {recordCount} 条记录");

                // 插入测试数据
                var testData = GenerateTestData(recordCount);
                using (var tx = _environment.BeginTransaction())
                {
                    foreach (var kvp in testData)
                    {
                        tx.Put(_database, Encoding.UTF8.GetBytes(kvp.Key), Encoding.UTF8.GetBytes(kvp.Value));
                    }
                    tx.Commit();
                }

                // 测试游标迭代性能
                var stopwatch = Stopwatch.StartNew();
                var iterateCount = 0;

                await Task.Run(() =>
                {
                    using var tx = _environment.BeginTransaction(TransactionBeginFlags.ReadOnly);
                    using var cursor = tx.CreateCursor(_database);

                    //if (cursor.MoveToFirst())
                    //{
                    //    do
                    //    {
                    //        iterateCount++;
                    //    } while (cursor.MoveNext());
                    //}

                    tx.Commit();
                });

                stopwatch.Stop();
                _output.WriteLine($"游标迭代完成: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
                _output.WriteLine($"迭代速度: {iterateCount / stopwatch.Elapsed.TotalSeconds:F0} 条/秒");
                _output.WriteLine($"迭代条数: {iterateCount}");

                Assert.Equal(recordCount, iterateCount);
            }
            finally
            {
                // 清理会在 Dispose 中进行
            }
        }

        public void Dispose()
        {
            try
            {
                _database?.Dispose();
                _database = null;
                _environment?.Dispose();
                _environment = null;

                if (Directory.Exists(_testDir))
                {
                    Directory.Delete(_testDir, true);
                    _output.WriteLine($"清理测试数据完成: {_testDir}");
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"清理测试数据失败: {ex.Message}");
            }
        }
    }
}