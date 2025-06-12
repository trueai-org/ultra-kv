using FASTER.core;
using System.Diagnostics;
using System.Text;
using Xunit.Abstractions;

namespace UltraKV.Tests
{
    public class FASTERPerformanceTest : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _testDir;
        private FasterKV<long, string> _fasterKV;
        private ClientSession<long, string, string, string, Empty, SimpleFunctions<long, string>> _session;
        private IDevice _logDevice;

        public FASTERPerformanceTest(ITestOutputHelper output)
        {
            _output = output;
            _testDir = Path.Combine(Path.GetTempPath(), "FASTERPerformanceTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
        }

        private void InitializeFASTER()
        {
            // 清理之前的实例
            _session?.Dispose();
            _fasterKV?.Dispose();
            _logDevice?.Dispose();

            // 创建日志设备
            _logDevice = Devices.CreateLogDevice(Path.Combine(_testDir, "test.log"), deleteOnClose: false);

            // 创建 FASTER 实例，为单文件

            _fasterKV = new FasterKV<long, string>(
                size: 1L << 20, // 1M hash table size
                logSettings: new LogSettings
                {
                    LogDevice = _logDevice,
                    MemorySizeBits = 25, // 32MB memory
                    MutableFraction = 0.9,
                    PageSizeBits = 16 // 64KB pages
                }
            );

            // 创建会话 - 使用正确的 SimpleFunctions
            _session = _fasterKV.For(new SimpleFunctions<long, string>()).NewSession<SimpleFunctions<long, string>>();
        }

        [Theory]
        [InlineData(10_000, "1万")]
        [InlineData(100_000, "10万")]
        [InlineData(1_000_000, "100万")]
        public async Task TestPerformance_InsertAndRead(int recordCount, string description)
        {
            InitializeFASTER();

            try
            {
                _output.WriteLine($"开始测试 {description} 条记录的 FASTER 性能");
                _output.WriteLine($"测试数据库路径: {_testDir}");

                // 生成测试数据
                var testData = GenerateTestData(recordCount);
                _output.WriteLine($"生成了 {testData.Count} 条测试数据");

                // 测试插入性能
                var insertTime = await MeasureInsertPerformance(testData, description);

                // 测试读取性能
                var readTime = await MeasureReadPerformance(testData.Keys.ToList(), description);

                // 测试 RMW 性能
                var rmwTime = await MeasureRMWPerformance(testData, description);

                // 测试删除性能
                var deleteTime = await MeasureDeletePerformance(testData.Keys.ToList(), description);

                // 输出性能报告
                PrintPerformanceReport(description, recordCount, insertTime, readTime, rmwTime, deleteTime);

                // 验证数据完整性（在删除之前重新插入数据）
                await ReinsertDataForVerification(testData);
                await VerifyDataIntegrity(testData);
            }
            finally
            {
                // 清理会在 Dispose 中进行
            }
        }

        private Dictionary<long, string> GenerateTestData(int count)
        {
            var data = new Dictionary<long, string>();
            var random = new Random(42); // 使用固定种子确保测试可重复

            for (long i = 0; i < count; i++)
            {
                var key = i;
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

        private async Task<TimeSpan> MeasureInsertPerformance(Dictionary<long, string> testData, string description)
        {
            _output.WriteLine($"开始插入测试 - {description}");
            var stopwatch = Stopwatch.StartNew();

            await Task.Run(() =>
            {
                foreach (var kvp in testData)
                {
                    var status = _session.Upsert(kvp.Key, kvp.Value);

                    // 处理 Pending 状态
                    if (status.IsPending)
                    {
                        _session.CompletePendingWithOutputs(out var completedOutputs, wait: true);
                        completedOutputs?.Dispose();
                    }
                }

                // 确保所有操作完成
                _session.CompletePendingWithOutputs(out var finalCompletedOutputs, wait: true);
                finalCompletedOutputs?.Dispose();
            });

            stopwatch.Stop();
            _output.WriteLine($"插入完成 - {description}: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");

            return stopwatch.Elapsed;
        }

        private async Task<TimeSpan> MeasureReadPerformance(List<long> keys, string description)
        {
            _output.WriteLine($"开始读取测试 - {description}");
            var stopwatch = Stopwatch.StartNew();
            var readCount = 0;

            await Task.Run(() =>
            {
                foreach (var key in keys)
                {
                    var status = _session.Read(key, out var value);

                    if (status.IsPending)
                    {
                        _session.CompletePendingWithOutputs(out var completedOutputs, wait: true);
                        if (completedOutputs != null)
                        {
                            while (completedOutputs.Next())
                            {
                                if (completedOutputs.Current.Status.Found)
                                    readCount++;
                            }
                            completedOutputs.Dispose();
                        }
                    }
                    else if (status.Found)
                    {
                        readCount++;
                    }
                }
            });

            stopwatch.Stop();
            _output.WriteLine($"读取完成 - {description}: {stopwatch.Elapsed.TotalMilliseconds:F2} ms, 读取条数: {readCount}");

            return stopwatch.Elapsed;
        }

        private async Task<TimeSpan> MeasureRMWPerformance(Dictionary<long, string> testData, string description)
        {
            _output.WriteLine($"开始RMW测试 - {description}");
            var stopwatch = Stopwatch.StartNew();

            await Task.Run(() =>
            {
                foreach (var kvp in testData)
                {
                    // 使用 RMW 操作来更新值
                    var status = _session.RMW(kvp.Key, "_updated");

                    if (status.IsPending)
                    {
                        _session.CompletePendingWithOutputs(out var completedOutputs, wait: true);
                        completedOutputs?.Dispose();
                    }
                }

                // 确保所有操作完成
                _session.CompletePendingWithOutputs(out var finalCompletedOutputs, wait: true);
                finalCompletedOutputs?.Dispose();
            });

            stopwatch.Stop();
            _output.WriteLine($"RMW完成 - {description}: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");

            return stopwatch.Elapsed;
        }

        private async Task<TimeSpan> MeasureDeletePerformance(List<long> keys, string description)
        {
            _output.WriteLine($"开始删除测试 - {description}");
            var stopwatch = Stopwatch.StartNew();

            await Task.Run(() =>
            {
                foreach (var key in keys)
                {
                    var status = _session.Delete(key);

                    if (status.IsPending)
                    {
                        _session.CompletePendingWithOutputs(out var completedOutputs, wait: true);
                        completedOutputs?.Dispose();
                    }
                }

                // 确保所有操作完成
                _session.CompletePendingWithOutputs(out var finalCompletedOutputs, wait: true);
                finalCompletedOutputs?.Dispose();
            });

            stopwatch.Stop();
            _output.WriteLine($"删除完成 - {description}: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");

            return stopwatch.Elapsed;
        }

        private async Task ReinsertDataForVerification(Dictionary<long, string> testData)
        {
            await Task.Run(() =>
            {
                foreach (var kvp in testData.Take(1000)) // 重新插入前1000条数据用于验证
                {
                    var status = _session.Upsert(kvp.Key, kvp.Value);
                    if (status.IsPending)
                    {
                        _session.CompletePendingWithOutputs(out var completedOutputs, wait: true);
                        completedOutputs?.Dispose();
                    }
                }

                _session.CompletePendingWithOutputs(out var finalCompletedOutputs, wait: true);
                finalCompletedOutputs?.Dispose();
            });
        }

        private void PrintPerformanceReport(string description, int recordCount, TimeSpan insertTime, TimeSpan readTime, TimeSpan rmwTime, TimeSpan deleteTime)
        {
            _output.WriteLine($"\n=== {description} 条记录 FASTER 性能报告 ===");
            _output.WriteLine($"记录数量: {recordCount:N0}");
            _output.WriteLine($"插入时间: {insertTime.TotalMilliseconds:F2} ms");
            _output.WriteLine($"插入速度: {recordCount / insertTime.TotalSeconds:F0} 条/秒");
            _output.WriteLine($"读取时间: {readTime.TotalMilliseconds:F2} ms");
            _output.WriteLine($"读取速度: {recordCount / readTime.TotalSeconds:F0} 条/秒");
            _output.WriteLine($"RMW时间: {rmwTime.TotalMilliseconds:F2} ms");
            _output.WriteLine($"RMW速度: {recordCount / rmwTime.TotalSeconds:F0} 条/秒");
            _output.WriteLine($"删除时间: {deleteTime.TotalMilliseconds:F2} ms");
            _output.WriteLine($"删除速度: {recordCount / deleteTime.TotalSeconds:F0} 条/秒");
            _output.WriteLine($"平均单条插入时间: {insertTime.TotalMilliseconds / recordCount:F4} ms");
            _output.WriteLine($"平均单条读取时间: {readTime.TotalMilliseconds / recordCount:F4} ms");
            _output.WriteLine($"===========================\n");
        }

        private async Task VerifyDataIntegrity(Dictionary<long, string> originalData)
        {
            _output.WriteLine("开始验证数据完整性...");
            var verifyCount = 0;
            var errorCount = 0;

            await Task.Run(() =>
            {
                foreach (var kvp in originalData.Take(1000)) // 验证前1000条数据
                {
                    var status = _session.Read(kvp.Key, out var value);
                    verifyCount++;

                    if (status.IsPending)
                    {
                        _session.CompletePendingWithOutputs(out var completedOutputs, wait: true);
                        if (completedOutputs != null)
                        {
                            while (completedOutputs.Next())
                            {
                                if (!completedOutputs.Current.Status.Found || completedOutputs.Current.Output != kvp.Value)
                                {
                                    errorCount++;
                                    if (errorCount <= 5) // 只输出前5个错误
                                    {
                                        _output.WriteLine($"数据不匹配: Key={kvp.Key}, Expected={kvp.Value}, Actual={completedOutputs.Current.Output ?? "NULL"}");
                                    }
                                }
                            }
                            completedOutputs.Dispose();
                        }
                        else
                        {
                            errorCount++;
                        }
                    }
                    else if (!status.Found || value != kvp.Value)
                    {
                        errorCount++;
                        if (errorCount <= 5) // 只输出前5个错误
                        {
                            _output.WriteLine($"数据不匹配: Key={kvp.Key}, Expected={kvp.Value}, Actual={value ?? "NULL"}");
                        }
                    }
                }
            });

            _output.WriteLine($"数据完整性验证完成: 验证了 {verifyCount} 条记录, 错误 {errorCount} 条");
            Assert.True(errorCount == 0, $"数据完整性验证失败: {errorCount} 条记录不匹配");
        }

        [Fact]
        public async Task TestBasicOperations()
        {
            InitializeFASTER();

            try
            {
                // 基本插入测试
                var status = _session.Upsert(42L, "test_value");
                Assert.True(status.IsCompletedSuccessfully);

                // 基本读取测试
                var readStatus = _session.Read(42L, out var value);
                Assert.True(readStatus.Found);
                Assert.Equal("test_value", value);

                // 基本 RMW 测试
                var rmwStatus = _session.RMW(42L, "_updated");
                Assert.True(rmwStatus.IsCompletedSuccessfully);

                // 验证 RMW 结果
                readStatus = _session.Read(42L, out value);
                Assert.True(readStatus.Found);
                Assert.Equal("_updated", value); // SimpleFunctions 中的 merger 会用新值替换旧值

                // 基本删除测试
                var deleteStatus = _session.Delete(42L);
                Assert.True(deleteStatus.Found);

                // 验证删除结果
                readStatus = _session.Read(42L, out _);
                Assert.True(readStatus.NotFound);

                _output.WriteLine("基本操作测试通过");
            }
            finally
            {
                // 清理会在 Dispose 中进行
            }
        }

        [Fact]
        public async Task TestConcurrentOperations()
        {
            InitializeFASTER();

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
                        // 每个任务创建自己的会话
                        using var taskSession = _fasterKV.For(new SimpleFunctions<long, string>()).NewSession<SimpleFunctions<long, string>>();

                        for (int i = 0; i < recordsPerTask; i++)
                        {
                            var key = (long)(currentTaskId * recordsPerTask + i);
                            var value = $"concurrent_value_{currentTaskId}_{i}";
                            var status = taskSession.Upsert(key, value);

                            if (status.IsPending)
                            {
                                taskSession.CompletePendingWithOutputs(out var completedOutputs, wait: true);
                                completedOutputs?.Dispose();
                            }
                        }

                        // 完成所有待处理操作
                        taskSession.CompletePendingWithOutputs(out var finalCompletedOutputs, wait: true);
                        finalCompletedOutputs?.Dispose();
                    });

                    tasks.Add(task);
                }

                await Task.WhenAll(tasks);
                stopwatch.Stop();

                _output.WriteLine($"并发插入完成: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
                _output.WriteLine($"总插入速度: {(taskCount * recordsPerTask) / stopwatch.Elapsed.TotalSeconds:F0} 条/秒");

                // 验证并发插入的数据
                var verifyCount = 0;
                for (int taskId = 0; taskId < taskCount; taskId++)
                {
                    for (int i = 0; i < recordsPerTask; i++)
                    {
                        var key = (long)(taskId * recordsPerTask + i);
                        var status = _session.Read(key, out var value);

                        if (status.IsPending)
                        {
                            _session.CompletePendingWithOutputs(out var completedOutputs, wait: true);
                            if (completedOutputs != null)
                            {
                                while (completedOutputs.Next())
                                {
                                    if (completedOutputs.Current.Status.Found)
                                        verifyCount++;
                                }
                                completedOutputs.Dispose();
                            }
                        }
                        else if (status.Found)
                        {
                            verifyCount++;
                        }
                    }
                }

                Assert.Equal(taskCount * recordsPerTask, verifyCount);
                _output.WriteLine($"并发测试验证通过: 插入了 {verifyCount} 条记录");
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
                _session?.Dispose();
                _session = null;
                _fasterKV?.Dispose();
                _fasterKV = null;
                _logDevice?.Dispose();
                _logDevice = null;

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

    /// <summary>
    /// 字符串连接的 SimpleFunctions，用于 RMW 操作
    /// </summary>
    public class StringConcatFunctions : SimpleFunctions<long, string>
    {
        public StringConcatFunctions() : base((input, value) => value + input)
        {
        }
    }
}