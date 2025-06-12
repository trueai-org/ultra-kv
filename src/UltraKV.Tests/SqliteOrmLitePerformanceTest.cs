using ServiceStack.Data;
using ServiceStack.DataAnnotations;
using ServiceStack.OrmLite;
using System.Diagnostics;
using System.Text;

namespace UltraKV.Tests
{
    /// <summary>
    /// SQLite OrmLite 性能测试
    /// </summary>
    public class SqliteOrmLitePerformanceTest : BaseTests
    {
        private readonly string _dbPath;
        private readonly IDbConnectionFactory _dbFactory;

        public SqliteOrmLitePerformanceTest()
        {
            SetOutputUTF8();

            // 创建临时数据库文件路径
            _dbPath = Path.Combine(Path.GetTempPath(), $"ultrakv_test_{Guid.NewGuid()}.db");

            // 初始化 SQLite 连接工厂
            _dbFactory = new OrmLiteConnectionFactory($"Data Source={_dbPath};", SqliteDialect.Provider);

            // 创建表
            using var db = _dbFactory.OpenDbConnection();
            db.CreateTableIfNotExists<KeyValueEntity>();
        }

        public override void Dispose()
        {
            // 清理测试数据库文件
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
            base.Dispose();
        }

        /// <summary>
        /// Key-Value 实体类
        /// </summary>
        [Alias("KeyValue")]
        public class KeyValueEntity
        {
            [PrimaryKey]
            [StringLength(255)]
            public string Key { get; set; } = string.Empty;

            [StringLength(int.MaxValue)]
            public string Value { get; set; } = string.Empty;

            public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

            public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        }

        [Fact]
        public void Test_Insert_10K_Records()
        {
            const int recordCount = 10_000;
            Console.WriteLine($"开始测试插入 {recordCount:N0} 条记录到 SQLite...");

            var records = GenerateTestData(recordCount);

            var stopwatch = Stopwatch.StartNew();

            using (var db = _dbFactory.OpenDbConnection())
            {
                using var trans = db.BeginTransaction();
                try
                {
                    db.InsertAll(records);
                    trans.Commit();
                }
                catch
                {
                    trans.Rollback();
                    throw;
                }
            }

            stopwatch.Stop();

            Console.WriteLine($"插入 {recordCount:N0} 条记录耗时: {stopwatch.ElapsedMilliseconds:N0} ms");
            Console.WriteLine($"平均插入速度: {recordCount / stopwatch.Elapsed.TotalSeconds:N0} 条/秒");

            // 验证数据
            using var verifyDb = _dbFactory.OpenDbConnection();
            var count = verifyDb.Count<KeyValueEntity>();
            Assert.Equal(recordCount, count);

            Console.WriteLine($"验证完成，数据库中共有 {count:N0} 条记录");
        }

        [Fact]
        public void Test_Insert_100K_Records()
        {
            const int recordCount = 100_000;
            Console.WriteLine($"开始测试插入 {recordCount:N0} 条记录到 SQLite...");

            var records = GenerateTestData(recordCount);

            var stopwatch = Stopwatch.StartNew();

            using (var db = _dbFactory.OpenDbConnection())
            {
                using var trans = db.BeginTransaction();
                try
                {
                    db.InsertAll(records);
                    trans.Commit();
                }
                catch
                {
                    trans.Rollback();
                    throw;
                }
            }

            stopwatch.Stop();

            Console.WriteLine($"插入 {recordCount:N0} 条记录耗时: {stopwatch.ElapsedMilliseconds:N0} ms");
            Console.WriteLine($"平均插入速度: {recordCount / stopwatch.Elapsed.TotalSeconds:N0} 条/秒");

            // 验证数据
            using var verifyDb = _dbFactory.OpenDbConnection();
            var count = verifyDb.Count<KeyValueEntity>();
            Assert.Equal(recordCount, count);

            Console.WriteLine($"验证完成，数据库中共有 {count:N0} 条记录");
        }

        [Fact]
        public void Test_Batch_Insert_10K_Records()
        {
            const int recordCount = 10_000;
            const int batchSize = 1_000;
            Console.WriteLine($"开始测试批量插入 {recordCount:N0} 条记录到 SQLite (批次大小: {batchSize})...");

            var records = GenerateTestData(recordCount);

            var stopwatch = Stopwatch.StartNew();

            using var db = _dbFactory.OpenDbConnection();

            for (int i = 0; i < records.Count; i += batchSize)
            {
                var batch = records.Skip(i).Take(batchSize).ToList();

                using var trans = db.BeginTransaction();
                try
                {
                    db.InsertAll(batch);
                    trans.Commit();
                }
                catch
                {
                    trans.Rollback();
                    throw;
                }
            }

            stopwatch.Stop();

            Console.WriteLine($"批量插入 {recordCount:N0} 条记录耗时: {stopwatch.ElapsedMilliseconds:N0} ms");
            Console.WriteLine($"平均插入速度: {recordCount / stopwatch.Elapsed.TotalSeconds:N0} 条/秒");

            // 验证数据
            var count = db.Count<KeyValueEntity>();
            Assert.Equal(recordCount, count);

            Console.WriteLine($"验证完成，数据库中共有 {count:N0} 条记录");
        }

        [Fact]
        public void Test_Query_Performance_10K()
        {
            const int recordCount = 10_000;
            const int queryCount = 1_000;

            // 先插入测试数据
            Console.WriteLine($"准备测试数据：插入 {recordCount:N0} 条记录...");
            var records = GenerateTestData(recordCount);

            using (var db = _dbFactory.OpenDbConnection())
            {
                using var trans = db.BeginTransaction();
                db.InsertAll(records);
                trans.Commit();
            }

            Console.WriteLine($"开始测试查询性能：随机查询 {queryCount} 次...");

            var random = new Random(42); // 固定种子确保可重复性
            var queryKeys = records.OrderBy(x => random.Next()).Take(queryCount).Select(x => x.Key).ToList();

            var stopwatch = Stopwatch.StartNew();

            using var queryDb = _dbFactory.OpenDbConnection();
            int foundCount = 0;

            foreach (var key in queryKeys)
            {
                var result = queryDb.Single<KeyValueEntity>(x => x.Key == key);
                if (result != null)
                {
                    foundCount++;
                }
            }

            stopwatch.Stop();

            Console.WriteLine($"查询 {queryCount} 次耗时: {stopwatch.ElapsedMilliseconds:N0} ms");
            Console.WriteLine($"平均查询速度: {queryCount / stopwatch.Elapsed.TotalSeconds:N0} 次/秒");
            Console.WriteLine($"找到记录数: {foundCount}");

            Assert.Equal(queryCount, foundCount);
        }

        [Fact]
        public void Test_Update_Performance_10K()
        {
            const int recordCount = 10_000;
            const int updateCount = 1_000;

            // 先插入测试数据
            Console.WriteLine($"准备测试数据：插入 {recordCount:N0} 条记录...");
            var records = GenerateTestData(recordCount);

            using (var db = _dbFactory.OpenDbConnection())
            {
                using var trans1= db.BeginTransaction();
                db.InsertAll(records);
                trans1.Commit();
            }

            Console.WriteLine($"开始测试更新性能：随机更新 {updateCount} 条记录...");

            var random = new Random(42);
            var updateRecords = records.OrderBy(x => random.Next()).Take(updateCount).ToList();

            // 修改值
            foreach (var record in updateRecords)
            {
                record.Value = $"Updated_{Guid.NewGuid()}";
                record.UpdatedAt = DateTime.UtcNow;
            }

            var stopwatch = Stopwatch.StartNew();

            using var updateDb = _dbFactory.OpenDbConnection();
            using var trans = updateDb.BeginTransaction();

            foreach (var record in updateRecords)
            {
                updateDb.Update(record);
            }

            trans.Commit();
            stopwatch.Stop();

            Console.WriteLine($"更新 {updateCount} 条记录耗时: {stopwatch.ElapsedMilliseconds:N0} ms");
            Console.WriteLine($"平均更新速度: {updateCount / stopwatch.Elapsed.TotalSeconds:N0} 条/秒");

            // 验证更新
            var verifiedCount = 0;
            foreach (var record in updateRecords)
            {
                var result = updateDb.Single<KeyValueEntity>(x => x.Key == record.Key);
                if (result?.Value == record.Value)
                {
                    verifiedCount++;
                }
            }

            Assert.Equal(updateCount, verifiedCount);
            Console.WriteLine($"验证完成，成功更新 {verifiedCount} 条记录");
        }

        [Fact]
        public void Test_Delete_Performance_10K()
        {
            const int recordCount = 10_000;
            const int deleteCount = 1_000;

            // 先插入测试数据
            Console.WriteLine($"准备测试数据：插入 {recordCount:N0} 条记录...");
            var records = GenerateTestData(recordCount);

            using (var db = _dbFactory.OpenDbConnection())
            {
                using var trans1 = db.BeginTransaction();
                db.InsertAll(records);
                trans1.Commit();
            }

            Console.WriteLine($"开始测试删除性能：随机删除 {deleteCount} 条记录...");

            var random = new Random(42);
            var deleteKeys = records.OrderBy(x => random.Next()).Take(deleteCount).Select(x => x.Key).ToList();

            var stopwatch = Stopwatch.StartNew();

            using var deleteDb = _dbFactory.OpenDbConnection();
            using var trans = deleteDb.BeginTransaction();

            foreach (var key in deleteKeys)
            {
                deleteDb.Delete<KeyValueEntity>(x => x.Key == key);
            }

            trans.Commit();
            stopwatch.Stop();

            Console.WriteLine($"删除 {deleteCount} 条记录耗时: {stopwatch.ElapsedMilliseconds:N0} ms");
            Console.WriteLine($"平均删除速度: {deleteCount / stopwatch.Elapsed.TotalSeconds:N0} 条/秒");

            // 验证删除
            var remainingCount = deleteDb.Count<KeyValueEntity>();
            var expectedRemaining = recordCount - deleteCount;

            Assert.Equal(expectedRemaining, remainingCount);
            Console.WriteLine($"验证完成，剩余记录数: {remainingCount}");
        }

        [Fact]
        public void Test_Comprehensive_Performance_Comparison()
        {
            Console.WriteLine("=== SQLite OrmLite 综合性能测试 ===");

            var testSizes = new[] { 1_000, 5_000, 10_000 };
            var results = new List<(int Size, long InsertMs, long QueryMs, long UpdateMs, long DeleteMs)>();

            foreach (var size in testSizes)
            {
                Console.WriteLine($"\n--- 测试 {size:N0} 条记录 ---");

                // 重新创建数据库
                using var db = _dbFactory.OpenDbConnection();
                db.DropAndCreateTable<KeyValueEntity>();

                var records = GenerateTestData(size);

                // 测试插入
                var insertSw = Stopwatch.StartNew();
                using (var trans = db.BeginTransaction())
                {
                    db.InsertAll(records);
                    trans.Commit();
                }
                insertSw.Stop();

                // 测试查询
                var random = new Random(42);
                var queryKeys = records.OrderBy(x => random.Next()).Take(Math.Min(1000, size)).Select(x => x.Key).ToList();

                var querySw = Stopwatch.StartNew();
                foreach (var key in queryKeys)
                {
                    db.Single<KeyValueEntity>(x => x.Key == key);
                }
                querySw.Stop();

                // 测试更新
                var updateRecords = records.Take(Math.Min(500, size / 2)).ToList();
                foreach (var record in updateRecords)
                {
                    record.Value = $"Updated_{Guid.NewGuid()}";
                }

                var updateSw = Stopwatch.StartNew();
                using (var trans = db.BeginTransaction())
                {
                    foreach (var record in updateRecords)
                    {
                        db.Update(record);
                    }
                    trans.Commit();
                }
                updateSw.Stop();

                // 测试删除
                var deleteKeys = records.Skip(size / 2).Take(Math.Min(500, size / 4)).Select(x => x.Key).ToList();

                var deleteSw = Stopwatch.StartNew();
                using (var trans = db.BeginTransaction())
                {
                    foreach (var key in deleteKeys)
                    {
                        db.Delete<KeyValueEntity>(x => x.Key == key);
                    }
                    trans.Commit();
                }
                deleteSw.Stop();

                results.Add((size, insertSw.ElapsedMilliseconds, querySw.ElapsedMilliseconds,
                           updateSw.ElapsedMilliseconds, deleteSw.ElapsedMilliseconds));

                Console.WriteLine($"插入: {insertSw.ElapsedMilliseconds:N0} ms ({size / insertSw.Elapsed.TotalSeconds:N0} 条/秒)");
                Console.WriteLine($"查询: {querySw.ElapsedMilliseconds:N0} ms ({queryKeys.Count / querySw.Elapsed.TotalSeconds:N0} 次/秒)");
                Console.WriteLine($"更新: {updateSw.ElapsedMilliseconds:N0} ms ({updateRecords.Count / updateSw.Elapsed.TotalSeconds:N0} 条/秒)");
                Console.WriteLine($"删除: {deleteSw.ElapsedMilliseconds:N0} ms ({deleteKeys.Count / deleteSw.Elapsed.TotalSeconds:N0} 条/秒)");
            }

            Console.WriteLine("\n=== 性能测试总结 ===");
            Console.WriteLine("记录数\t\t插入(ms)\t查询(ms)\t更新(ms)\t删除(ms)");
            foreach (var (size, insertMs, queryMs, updateMs, deleteMs) in results)
            {
                Console.WriteLine($"{size:N0}\t\t{insertMs:N0}\t\t{queryMs:N0}\t\t{updateMs:N0}\t\t{deleteMs:N0}");
            }
        }

        /// <summary>
        /// 生成测试数据
        /// </summary>
        private List<KeyValueEntity> GenerateTestData(int count)
        {
            var records = new List<KeyValueEntity>(count);
            var random = new Random(42); // 固定种子确保可重复性

            for (int i = 0; i < count; i++)
            {
                var key = $"key_{i:D10}_{Guid.NewGuid():N}";
                var value = GenerateRandomValue(random, 100, 1000); // 100-1000 字符的随机值

                records.Add(new KeyValueEntity
                {
                    Key = key,
                    Value = value,
                    CreatedAt = DateTime.UtcNow.AddSeconds(-random.Next(0, 86400)), // 随机时间
                    UpdatedAt = DateTime.UtcNow
                });
            }

            return records;
        }

        /// <summary>
        /// 生成随机值
        /// </summary>
        private string GenerateRandomValue(Random random, int minLength, int maxLength)
        {
            var length = random.Next(minLength, maxLength + 1);
            var sb = new StringBuilder(length);

            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

            for (int i = 0; i < length; i++)
            {
                sb.Append(chars[random.Next(chars.Length)]);
            }

            return sb.ToString();
        }
    }
}