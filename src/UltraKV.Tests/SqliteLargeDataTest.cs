using ServiceStack.Data;
using ServiceStack.DataAnnotations;
using ServiceStack.OrmLite;
using System.Diagnostics;

namespace UltraKV.Tests
{
    /// <summary>
    /// SQLite 大数据量测试
    /// </summary>
    public class SqliteLargeDataTest : BaseTests
    {
        private readonly string _dbPath;
        private readonly IDbConnectionFactory _dbFactory;

        public SqliteLargeDataTest()
        {
            SetOutputUTF8();

            _dbPath = Path.Combine(Path.GetTempPath(), $"ultrakv_large_test_{Guid.NewGuid()}.db");
            _dbFactory = new OrmLiteConnectionFactory($"Data Source={_dbPath};Cache Size=10000;Page Size=4096;Temp Store=Memory;Journal Mode=WAL;Synchronous=NORMAL;", SqliteDialect.Provider);

            using var db = _dbFactory.OpenDbConnection();
            db.CreateTableIfNotExists<SimpleKeyValue>();

            // 创建索引以提高查询性能
            db.ExecuteSql("CREATE INDEX IF NOT EXISTS idx_key ON SimpleKeyValue(Key)");
        }

        public override void Dispose()
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
            base.Dispose();
        }

        [Alias("SimpleKeyValue")]
        public class SimpleKeyValue
        {
            [PrimaryKey]
            [StringLength(100)]
            public string Key { get; set; } = string.Empty;

            [StringLength(500)]
            public string Value { get; set; } = string.Empty;
        }

        [Fact(Timeout = 300000)] // 5分钟超时
        public void Test_Insert_10K_Optimized()
        {
            const int recordCount = 10_0000;
            Console.WriteLine($"开始优化插入测试 - {recordCount:N0} 条记录");

            var data = GenerateSimpleData(recordCount);

            var stopwatch = Stopwatch.StartNew();

            using var db = _dbFactory.OpenDbConnection();

            // 使用事务和批量插入优化
            db.ExecuteSql("PRAGMA synchronous = OFF");
            db.ExecuteSql("PRAGMA journal_mode = MEMORY");

            using var trans = db.BeginTransaction();
            db.InsertAll(data);
            trans.Commit();

            stopwatch.Stop();

            Console.WriteLine($"✓ 插入 {recordCount:N0} 条记录耗时: {stopwatch.ElapsedMilliseconds:N0} ms");
            Console.WriteLine($"✓ 插入速度: {recordCount / stopwatch.Elapsed.TotalSeconds:N0} 条/秒");

            var count = db.Count<SimpleKeyValue>();
            Assert.Equal(recordCount, count);
        }

        [Fact(Timeout = 600000)] // 10分钟超时
        public void Test_Insert_100K_Optimized()
        {
            const int recordCount = 100_000;
            Console.WriteLine($"开始优化插入测试 - {recordCount:N0} 条记录");

            var data = GenerateSimpleData(recordCount);

            var stopwatch = Stopwatch.StartNew();

            using var db = _dbFactory.OpenDbConnection();

            // SQLite 性能优化设置
            db.ExecuteSql("PRAGMA synchronous = OFF");
            db.ExecuteSql("PRAGMA journal_mode = MEMORY");
            db.ExecuteSql("PRAGMA cache_size = 10000");
            db.ExecuteSql("PRAGMA temp_store = MEMORY");

            using var trans = db.BeginTransaction();
            db.InsertAll(data);
            trans.Commit();

            stopwatch.Stop();

            Console.WriteLine($"✓ 插入 {recordCount:N0} 条记录耗时: {stopwatch.ElapsedMilliseconds:N0} ms");
            Console.WriteLine($"✓ 插入速度: {recordCount / stopwatch.Elapsed.TotalSeconds:N0} 条/秒");

            var count = db.Count<SimpleKeyValue>();
            Assert.Equal(recordCount, count);

            // 数据库文件大小
            var fileInfo = new FileInfo(_dbPath);
            Console.WriteLine($"✓ 数据库文件大小: {fileInfo.Length / (1024.0 * 1024.0):F2} MB");
        }

        [Fact]
        public void Test_Chunked_Insert_100K()
        {
            const int recordCount = 100_000;
            const int chunkSize = 5_000;
            Console.WriteLine($"开始分块插入测试 - {recordCount:N0} 条记录 (块大小: {chunkSize:N0})");

            var data = GenerateSimpleData(recordCount);

            var stopwatch = Stopwatch.StartNew();

            using var db = _dbFactory.OpenDbConnection();

            // 性能优化
            db.ExecuteSql("PRAGMA synchronous = OFF");
            db.ExecuteSql("PRAGMA journal_mode = WAL");

            for (int i = 0; i < data.Count; i += chunkSize)
            {
                var chunk = data.Skip(i).Take(chunkSize).ToList();

                using var trans = db.BeginTransaction();
                db.InsertAll(chunk);
                trans.Commit();

                if ((i / chunkSize + 1) % 5 == 0)
                {
                    Console.WriteLine($"已插入 {Math.Min(i + chunkSize, recordCount):N0} / {recordCount:N0} 条记录");
                }
            }

            stopwatch.Stop();

            Console.WriteLine($"✓ 分块插入 {recordCount:N0} 条记录耗时: {stopwatch.ElapsedMilliseconds:N0} ms");
            Console.WriteLine($"✓ 插入速度: {recordCount / stopwatch.Elapsed.TotalSeconds:N0} 条/秒");

            var count = db.Count<SimpleKeyValue>();
            Assert.Equal(recordCount, count);
        }

        private List<SimpleKeyValue> GenerateSimpleData(int count)
        {
            var data = new List<SimpleKeyValue>(count);

            for (int i = 0; i < count; i++)
            {
                data.Add(new SimpleKeyValue
                {
                    Key = $"key_{i:D8}",
                    Value = $"value_{i}_{Guid.NewGuid():N}"
                });
            }

            return data;
        }
    }
}