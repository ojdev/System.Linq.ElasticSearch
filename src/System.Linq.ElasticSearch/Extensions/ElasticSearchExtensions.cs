using Elasticsearch.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace ElasticSearch.SimpleQuery
{
    /// <summary>
    /// 
    /// </summary>
    public static class ElasticSearchExtensions
    {
        /// <summary>
        /// 添加ElasticSearch
        /// </summary>
        /// <param name="services"></param>
        /// <param name="uri">连接地址</param>
        /// <param name="defaultIndexName">默认索引</param>
        /// <param name="userName">用户名</param>
        /// <param name="password">密码</param>
        /// <returns></returns>
        public static IElasticClient AddElastic(this IServiceCollection services, Uri uri, string defaultIndexName = null, string userName = null, string password = null)
        {
            var settings = new ConnectionSettings(new StaticConnectionPool(new Uri[] { uri }));
            if (!string.IsNullOrWhiteSpace(defaultIndexName))
            {
                settings.DefaultIndex(defaultIndexName);
            }
            if (!string.IsNullOrWhiteSpace(userName) && !string.IsNullOrWhiteSpace(password))
            {
                settings.BasicAuthentication(userName, password);
            }
            var client = new ElasticClient(settings);
            //因为IElasticClient没有实现IDisable接口，所以还是注入一个单例的方便
            services.TryAddSingleton<IElasticClient>(client);
            services.TryAddScoped<ElasticSearchContext>();
            //services.TryAddSingleton<ConnectionSettingsBase<ConnectionSettings>>(settings);
            return client;
        }
        /// <summary>
        /// 重建索引
        /// </summary>
        /// <param name="client"></param>
        /// <param name="oldIndexName">就索引名字</param>
        /// <param name="newIndexName">新索引名字</param>
        /// <param name="shardsNumber"></param>
        /// <returns></returns>
        public static bool ReIndex<T>(IElasticClient client, string oldIndexName, string newIndexName, int shardsNumber = 1) where T : class
        {
            //检查新索引别名
            var alias = client.AliasExists($"{newIndexName}-reindex");
            //如果改别名不存在
            if (!alias.Exists)
            {
                //检查是否存在新索引
                IExistsResponse newIndex = client.IndexExists(newIndexName);
                client.UpdateIndexSettings(Indices.Index(newIndexName), setting => setting.IndexSettings(isetting => isetting.Setting("max_result_window", 100000)));
                //如果新索引不存在则创建一个新索引，需要修改各项创建索引时才需要的参数则在这处理
                if (!newIndex.Exists)
                {
                    CreateIndexDescriptor createIndex = new CreateIndexDescriptor(newIndexName)
                    .Settings(s => s.NumberOfShards(shardsNumber).Analysis(a => a.Analyzers(aa => aa.Language("standard_listing", sa => sa.Language(Language.Chinese)))))
                    .Mappings(ms => ms.Map<T>(m => m.AutoMap()));
                    ICreateIndexResponse create = client.CreateIndex(createIndex);
                    Console.WriteLine($"{newIndexName}索引创建：{create.ApiCall.Success}");
                    //给新创建的索引添加一个索引别名表示时重新创建的
                    var r2 = client.Alias(t => t.Add(a => a.Index(newIndexName).Alias($"{newIndexName}-reindex")));
                    Console.WriteLine($"索引别名{newIndexName}=>{newIndexName}：{r2.ApiCall.Success}");
                }
                //将老索引中的文档索引导新建的索引
                var r1 = client.ReindexOnServer(r => r.Source(s => s.Index(Indices.Index(oldIndexName))).Destination(d => d.Index(newIndexName)));
                Console.WriteLine($"重新索引：{r1.ApiCall.Success}");
                if (r1.ApiCall.Success)
                {
                    //老索引中的所有文档全部到新索引之后删除老索引
                    client.DeleteIndex(Indices.Index(oldIndexName));
                    //新索引设置别名为老索引的名字
                    var r3 = client.Alias(t => t.Add(a => a.Index(newIndexName).Alias(oldIndexName)));
                    Console.WriteLine($"索引别名{newIndexName}=>{oldIndexName}：{r3.ApiCall.Success}");
                }
            }
            return alias.Exists;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="client"></param>
        /// <param name="indexName">索引名</param>
        /// <param name="newIndexName">新索引名，重建索引用</param>
        /// <param name="max_result_window"></param>
        /// <param name="shardsNumber">分片数</param>
        /// <returns></returns>
        public static IElasticClient CreateOrUpdateIndex<T>(this IElasticClient client, string indexName, string newIndexName = null, long max_result_window = 100000, int shardsNumber = 1) where T : class
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(newIndexName))
                {
                    var x = ReIndex<T>(client, indexName, newIndexName, shardsNumber);
                    if (x == false)
                    {
                        indexName = newIndexName;
                    }
                }
                IExistsResponse index = client.IndexExists(indexName);
                client.UpdateIndexSettings(Indices.Index(indexName), setting => setting.IndexSettings(isetting => isetting.Setting("max_result_window", max_result_window)));
                if (!index.Exists)
                {
                    CreateIndexDescriptor newIndex = new CreateIndexDescriptor(indexName)
                        .Settings(s => s.NumberOfShards(shardsNumber).NumberOfReplicas(1).Analysis(a => a.Analyzers(aa => aa.Language("standard_listing", sa => sa.Language(Language.Chinese)))))
                        .Mappings(ms => ms.Map<T>(m => m.AutoMap()));
                    ICreateIndexResponse create = client.CreateIndex(newIndex);
                    Console.WriteLine($"[{indexName}]\t索引已创建。");
                }
                else
                {
                    IPutMappingResponse putMap = client.Map<T>(m => m.AutoMap());
                    if (putMap.ApiCall.Success)
                    {
                        Console.WriteLine($"[{indexName}]\t索引已更新。");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return client;
        }
        /// <summary>
        ///  创建查询
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="client"></param>
        /// <param name="indexName"></param>
        /// <returns></returns>
        public static ElasticQuery<TEntity> Query<TEntity>(this ElasticSearchContext client, string indexName = null) where TEntity : class
        {
            var queries = new ElasticQuery<TEntity>(client);
            if (!string.IsNullOrWhiteSpace(indexName))
            {
                queries.Index(indexName);
            }
            return queries;
        }
        #region Must
        /// <summary>
        /// 判断相等
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="queries"></param>
        /// <param name="paths">Nested类型的集合</param>
        /// <param name="field">要进行判断的字段</param>
        /// <param name="value">要进行判断的值</param>
        /// <returns></returns>
        public static ElasticQuery<TEntity> Equal<TEntity>(this ElasticQuery<TEntity> queries, Expression<Func<TEntity, object>> paths, Expression<Func<TEntity, object>> field, string value) where TEntity : class
        {
            queries.Add(t => t.Nested(n => n.Path(paths).Query(q => q.Bool(b => b.Must(m => m.QueryString(qs => qs.DefaultField(field).Query($"\"{value}\"")))))));
            return queries;
        }
        /// <summary>
        /// 判断相等
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="queries"></param>
        /// <param name="field">要进行判断的字段</param>
        /// <param name="value">要进行判断的值</param>
        /// <returns></returns>
        public static ElasticQuery<TEntity> Equal<TEntity>(this ElasticQuery<TEntity> queries, Expression<Func<TEntity, object>> field, string value)
            where TEntity : class
        {
            queries.Add(t => t.QueryString(q => q.DefaultField(field).Query(value)));
            return queries;
        }
        /// <summary>
        /// 判断相等
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="queries"></param>
        /// <param name="field">要进行判断的字段</param>
        /// <param name="value">要进行判断的值</param>
        /// <returns></returns>
        public static ElasticQuery<TEntity> Equal<TEntity, TValue>(this ElasticQuery<TEntity> queries, Expression<Func<TEntity, object>> field, TValue value)
            where TEntity : class
            where TValue : struct
        {
            queries.Add(t => t.Term(q => q.Field(field).Value(value)));
            return queries;
        }
        #region 大于等于
        /// <summary>
        /// 大于或等于
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="queries"></param>
        /// <param name="field">要进行判断的字段</param>
        /// <param name="time"></param>
        /// <param name="timeZone"></param>
        /// <param name="dateMathTimeUnit"></param>
        /// <returns></returns>
        public static ElasticQuery<TEntity> ThanOrEquals<TEntity>(this ElasticQuery<TEntity> queries, Expression<Func<TEntity, object>> field, DateTime time, string timeZone = "+08:00", DateMathTimeUnit dateMathTimeUnit = DateMathTimeUnit.Day)
            where TEntity : class
        {
            queries.Add(mu => mu.DateRange(r => r.Field(field).TimeZone(timeZone).GreaterThanOrEquals(DateMath.Anchored(time).RoundTo(dateMathTimeUnit))));
            return queries;
        }
        /// <summary>
        /// 大于
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="queries"></param>
        /// <param name="field"></param>
        /// <param name="time"></param>
        /// <param name="timeZone"></param>
        /// <param name="dateMathTimeUnit"></param>
        /// <returns></returns>
        public static ElasticQuery<TEntity> Than<TEntity>(this ElasticQuery<TEntity> queries, Expression<Func<TEntity, object>> field, DateTime time, string timeZone = "+08:00", DateMathTimeUnit dateMathTimeUnit = DateMathTimeUnit.Day)
            where TEntity : class
        {
            queries.Add(mu => mu.DateRange(r => r.Field(field).TimeZone(timeZone).GreaterThan(DateMath.Anchored(time).RoundTo(dateMathTimeUnit))));
            return queries;
        }
        /// <summary>
        /// 大于或等于
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="queries"></param>
        /// <param name="field"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static ElasticQuery<TEntity> ThanOrEquals<TEntity>(this ElasticQuery<TEntity> queries, Expression<Func<TEntity, object>> field, double value)
            where TEntity : class
        {
            queries.Add(mu => mu.Range(r => r.Field(field).GreaterThanOrEquals(value)));
            return queries;
        }
        /// <summary>
        /// 大于
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="queries"></param>
        /// <param name="field"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static ElasticQuery<TEntity> Than<TEntity>(this ElasticQuery<TEntity> queries, Expression<Func<TEntity, object>> field, double value)
            where TEntity : class
        {
            queries.Add(mu => mu.Range(r => r.Field(field).GreaterThan(value)));
            return queries;
        }
        /// <summary>
        /// 大于或等于
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="queries"></param>
        /// <param name="field"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static ElasticQuery<TEntity> ThanOrEquals<TEntity>(this ElasticQuery<TEntity> queries, Expression<Func<TEntity, object>> field, long value)
            where TEntity : class
        {
            queries.Add(mu => mu.LongRange(r => r.Field(field).GreaterThanOrEquals(value)));
            return queries;
        }
        /// <summary>
        /// 大于
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="queries"></param>
        /// <param name="field">要进行判断的字段</param>
        /// <param name="value">要进行判断的值</param>
        /// <returns></returns>
        public static ElasticQuery<TEntity> Than<TEntity>(this ElasticQuery<TEntity> queries, Expression<Func<TEntity, object>> field, long value)
            where TEntity : class
        {
            queries.Add(mu => mu.LongRange(r => r.Field(field).GreaterThan(value)));
            return queries;
        }
        #region Nested
        /// <summary>
        /// 大于或等于
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="queries"></param>
        /// <param name="field">要进行判断的字段</param>
        /// <param name="value">要进行判断的值</param>
        /// <returns></returns>
        public static ElasticQuery<TEntity> ThanOrEquals<TEntity>(this ElasticQuery<TEntity> queries, Expression<Func<TEntity, object>> paths, Expression<Func<TEntity, object>> field, long value)
            where TEntity : class
        {
            queries.Add(t => t.Nested(n => n.Path(paths).Query(q => q.Bool(b => b.Must(m => m.Range(r => r.Field(field).GreaterThanOrEquals(value)))))));
            return queries;
        }
        /// <summary>
        /// 大于或等于
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="queries"></param>
        /// <param name="field">要进行判断的字段</param>
        /// <param name="value">要进行判断的值</param>
        /// <returns></returns>
        public static ElasticQuery<TEntity> ThanOrEquals<TEntity>(this ElasticQuery<TEntity> queries, Expression<Func<TEntity, object>> paths, Expression<Func<TEntity, object>> field, DateTime time, string timeZone = "+08:00", DateMathTimeUnit dateMathTimeUnit = DateMathTimeUnit.Day)
            where TEntity : class
        {
            queries.Add(t => t.Nested(n => n.Path(paths).Query(q => q.Bool(b => b.Must(m => m.DateRange(r => r.Field(field).TimeZone(timeZone).GreaterThanOrEquals(DateMath.Anchored(time).RoundTo(dateMathTimeUnit))))))));
            return queries;
        }
        /// <summary>
        /// 大于
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="queries"></param>
        /// <param name="field">要进行判断的字段</param>
        /// <param name="value">要进行判断的值</param>
        /// <returns></returns>
        public static ElasticQuery<TEntity> Than<TEntity>(this ElasticQuery<TEntity> queries, Expression<Func<TEntity, object>> paths, Expression<Func<TEntity, object>> field, DateTime time, string timeZone = "+08:00", DateMathTimeUnit dateMathTimeUnit = DateMathTimeUnit.Day)
            where TEntity : class
        {
            queries.Add(t => t.Nested(n => n.Path(paths).Query(q => q.Bool(b => b.Must(m => m.DateRange(r => r.Field(field).TimeZone(timeZone).GreaterThan(DateMath.Anchored(time).RoundTo(dateMathTimeUnit))))))));
            return queries;
        }
        /// <summary>
        /// 大于或等于
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="queries"></param>
        /// <param name="field">要进行判断的字段</param>
        /// <param name="value">要进行判断的值</param>
        /// <returns></returns>
        public static ElasticQuery<TEntity> ThanOrEquals<TEntity>(this ElasticQuery<TEntity> queries, Expression<Func<TEntity, object>> paths, Expression<Func<TEntity, object>> field, double value)
            where TEntity : class
        {
            queries.Add(t => t.Nested(n => n.Path(paths).Query(q => q.Bool(b => b.Must(m => m.Range(r => r.Field(field).GreaterThanOrEquals(value)))))));
            return queries;
        }
        /// <summary>
        /// 大于
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="queries"></param>
        /// <param name="field">要进行判断的字段</param>
        /// <param name="value">要进行判断的值</param>
        /// <returns></returns>
        public static ElasticQuery<TEntity> Than<TEntity>(this ElasticQuery<TEntity> queries, Expression<Func<TEntity, object>> paths, Expression<Func<TEntity, object>> field, double value)
            where TEntity : class
        {
            queries.Add(t => t.Nested(n => n.Path(paths).Query(q => q.Bool(b => b.Must(m => m.Range(r => r.Field(field).GreaterThan(value)))))));
            return queries;
        }
        /// <summary>
        /// 大于
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="queries"></param>
        /// <param name="field">要进行判断的字段</param>
        /// <param name="value">要进行判断的值</param>
        /// <returns></returns>
        public static ElasticQuery<TEntity> Than<TEntity>(this ElasticQuery<TEntity> queries, Expression<Func<TEntity, object>> paths, Expression<Func<TEntity, object>> field, long value)
            where TEntity : class
        {
            queries.Add(t => t.Nested(n => n.Path(paths).Query(q => q.Bool(b => b.Must(m => m.Range(r => r.Field(field).GreaterThan(value)))))));
            return queries;
        }
        #endregion
        #endregion
        #region 小于
        /// <summary>
        /// 小于或等于
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="queries"></param>
        /// <param name="field">要进行判断的字段</param>
        /// <param name="value">要进行判断的值</param>
        /// <returns></returns>
        public static ElasticQuery<TEntity> LessThanOrEquals<TEntity>(this ElasticQuery<TEntity> queries, Expression<Func<TEntity, object>> field, DateTime time, string timeZone = "+08:00", DateMathTimeUnit dateMathTimeUnit = DateMathTimeUnit.Day)
            where TEntity : class
        {
            queries.Add(mu => mu.DateRange(r => r.Field(field).TimeZone(timeZone).LessThanOrEquals(DateMath.Anchored(time).RoundTo(dateMathTimeUnit))));
            return queries;
        }
        /// <summary>
        /// 小于
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="queries"></param>
        /// <param name="field">要进行判断的字段</param>
        /// <param name="value">要进行判断的值</param>
        /// <returns></returns>
        public static ElasticQuery<TEntity> LessThan<TEntity>(this ElasticQuery<TEntity> queries, Expression<Func<TEntity, object>> field, DateTime time, string timeZone = "+08:00", DateMathTimeUnit dateMathTimeUnit = DateMathTimeUnit.Day)
            where TEntity : class
        {
            queries.Add(mu => mu.DateRange(r => r.Field(field).TimeZone(timeZone).LessThan(DateMath.Anchored(time).RoundTo(dateMathTimeUnit))));
            return queries;
        }
        /// <summary>
        /// 小于或等于
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="queries"></param>
        /// <param name="field">要进行判断的字段</param>
        /// <param name="value">要进行判断的值</param>
        /// <returns></returns>
        public static ElasticQuery<TEntity> LessThanOrEquals<TEntity>(this ElasticQuery<TEntity> queries, Expression<Func<TEntity, object>> field, double value)
            where TEntity : class
        {
            queries.Add(mu => mu.Range(r => r.Field(field).LessThanOrEquals(value)));
            return queries;
        }
        /// <summary>
        /// 小于
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="queries"></param>
        /// <param name="field">要进行判断的字段</param>
        /// <param name="value">要进行判断的值</param>
        /// <returns></returns>
        public static ElasticQuery<TEntity> LessThan<TEntity>(this ElasticQuery<TEntity> queries, Expression<Func<TEntity, object>> field, double value)
            where TEntity : class
        {
            queries.Add(mu => mu.Range(r => r.Field(field).LessThan(value)));
            return queries;
        }
        /// <summary>
        /// 小于或等于
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="queries"></param>
        /// <param name="field">要进行判断的字段</param>
        /// <param name="value">要进行判断的值</param>
        /// <returns></returns>
        public static ElasticQuery<TEntity> LessThanOrEquals<TEntity>(this ElasticQuery<TEntity> queries, Expression<Func<TEntity, object>> field, long value)
            where TEntity : class
        {
            queries.Add(mu => mu.LongRange(r => r.Field(field).LessThanOrEquals(value)));
            return queries;
        }
        /// <summary>
        /// 小于或等于
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="queries"></param>
        /// <param name="field">要进行判断的字段</param>
        /// <param name="value">要进行判断的值</param>
        /// <returns></returns>
        public static ElasticQuery<TEntity> LessThan<TEntity>(this ElasticQuery<TEntity> queries, Expression<Func<TEntity, object>> field, long value)
            where TEntity : class
        {
            queries.Add(mu => mu.LongRange(r => r.Field(field).LessThan(value)));
            return queries;
        }
        #region Nested
        /// <summary>
        /// 小于或等于
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="queries"></param>
        /// <param name="field">要进行判断的字段</param>
        /// <param name="value">要进行判断的值</param>
        /// <returns></returns>
        public static ElasticQuery<TEntity> LessThanOrEquals<TEntity>(this ElasticQuery<TEntity> queries, Expression<Func<TEntity, object>> paths, Expression<Func<TEntity, object>> field, long value)
            where TEntity : class
        {
            queries.Add(t => t.Nested(n => n.Path(paths).Query(q => q.Bool(b => b.Must(m => m.Range(r => r.Field(field).LessThanOrEquals(value)))))));
            return queries;
        }
        /// <summary>
        /// 小于或等于
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="queries"></param>
        /// <param name="field">要进行判断的字段</param>
        /// <param name="value">要进行判断的值</param>
        /// <returns></returns>
        public static ElasticQuery<TEntity> LessThanOrEquals<TEntity>(this ElasticQuery<TEntity> queries, Expression<Func<TEntity, object>> paths, Expression<Func<TEntity, object>> field, DateTime time, string timeZone = "+08:00", DateMathTimeUnit dateMathTimeUnit = DateMathTimeUnit.Day)
            where TEntity : class
        {
            queries.Add(t => t.Nested(n => n.Path(paths).Query(q => q.Bool(b => b.Must(m => m.DateRange(r => r.Field(field).TimeZone(timeZone).LessThanOrEquals(DateMath.Anchored(time).RoundTo(dateMathTimeUnit))))))));
            return queries;
        }
        /// <summary>
        /// 小于
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="queries"></param>
        /// <param name="field">要进行判断的字段</param>
        /// <param name="value">要进行判断的值</param>
        /// <returns></returns>
        public static ElasticQuery<TEntity> LessThan<TEntity>(this ElasticQuery<TEntity> queries, Expression<Func<TEntity, object>> paths, Expression<Func<TEntity, object>> field, DateTime time, string timeZone = "+08:00", DateMathTimeUnit dateMathTimeUnit = DateMathTimeUnit.Day)
            where TEntity : class
        {
            queries.Add(t => t.Nested(n => n.Path(paths).Query(q => q.Bool(b => b.Must(m => m.DateRange(r => r.Field(field).TimeZone(timeZone).LessThan(DateMath.Anchored(time).RoundTo(dateMathTimeUnit))))))));
            return queries;
        }
        /// <summary>
        /// 小于或等于
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="queries"></param>
        /// <param name="field">要进行判断的字段</param>
        /// <param name="value">要进行判断的值</param>
        /// <returns></returns>
        public static ElasticQuery<TEntity> LessThanOrEquals<TEntity>(this ElasticQuery<TEntity> queries, Expression<Func<TEntity, object>> paths, Expression<Func<TEntity, object>> field, double value)
            where TEntity : class
        {
            queries.Add(t => t.Nested(n => n.Path(paths).Query(q => q.Bool(b => b.Must(m => m.Range(r => r.Field(field).LessThanOrEquals(value)))))));
            return queries;
        }
        /// <summary>
        /// 小于
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="queries"></param>
        /// <param name="field">要进行判断的字段</param>
        /// <param name="value">要进行判断的值</param>
        /// <returns></returns>
        public static ElasticQuery<TEntity> LessThan<TEntity>(this ElasticQuery<TEntity> queries, Expression<Func<TEntity, object>> paths, Expression<Func<TEntity, object>> field, double value)
            where TEntity : class
        {
            queries.Add(t => t.Nested(n => n.Path(paths).Query(q => q.Bool(b => b.Must(m => m.Range(r => r.Field(field).LessThan(value)))))));
            return queries;
        }
        /// <summary>
        /// 小于
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="queries"></param>
        /// <param name="field">要进行判断的字段</param>
        /// <param name="value">要进行判断的值</param>
        /// <returns></returns>
        public static ElasticQuery<TEntity> LessThan<TEntity>(this ElasticQuery<TEntity> queries, Expression<Func<TEntity, object>> paths, Expression<Func<TEntity, object>> field, long value)
            where TEntity : class
        {
            queries.Add(t => t.Nested(n => n.Path(paths).Query(q => q.Bool(b => b.Must(m => m.Range(r => r.Field(field).LessThan(value)))))));
            return queries;
        }
        #endregion
        #endregion
        /// <summary>
        /// 判断包含
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="queries"></param>
        /// <param name="paths">Nested类型的集合</param>
        /// <param name="field">要进行判断的字段</param>
        /// <param name="value">要进行判断的值</param>
        /// <returns></returns>
        public static ElasticQuery<TEntity> Like<TEntity>(this ElasticQuery<TEntity> queries, Expression<Func<TEntity, object>> paths, Expression<Func<TEntity, object>> field, string value) where TEntity : class
        {
            queries.Add(t => t.Nested(n => n.Path(paths).Query(q => q.Bool(b => b.Must(m => m.QueryString(qs => qs.DefaultField(field).Query(value)))))));
            return queries;
        }
        /// <summary>
        /// 判断包含
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="queries"></param>
        /// <param name="field">要进行判断的字段</param>
        /// <param name="value">要进行判断的值</param>
        /// <returns></returns>
        public static ElasticQuery<TEntity> Like<TEntity>(this ElasticQuery<TEntity> queries, Expression<Func<TEntity, object>> field, string value) where TEntity : class
        {
            queries.Add(t => t.QueryString(q => q.DefaultField(field).Query(value).MinimumShouldMatch(1)));
            return queries;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="query"></param>
        /// <param name="value"></param>
        /// <param name="fields"></param>
        /// <returns></returns>
        public static QueryContainer Or<TEntity>(this QueryContainerDescriptor<TEntity> query, string value, params Expression<Func<TEntity, object>>[] fields) where TEntity : class
        {
            return query.QueryString(q => q.Fields(fields).Query(value));
        }
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="query"></param>
        /// <param name="value"></param>
        /// <param name="paths"></param>
        /// <param name="fields"></param>
        /// <returns></returns>
        public static QueryContainer OrNested<TEntity>(this QueryContainerDescriptor<TEntity> query, string value, Expression<Func<TEntity, object>> paths, params Expression<Func<TEntity, object>>[] fields) where TEntity : class
        {
            return query.Nested(n => n.Path(paths).Query(q => q.QueryString(qs => qs.Fields(fields).Query(string.Join(" ", value.ToCharArray().Select(t => $"\"{t}\""))).MinimumShouldMatch(1))));
        }
        /// <summary>
        /// 判断包含
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="queries"></param>
        /// <param name="paths"></param>
        /// <param name="field"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        public static ElasticQuery<TEntity> Like<TEntity, TValue>(this ElasticQuery<TEntity> queries, Expression<Func<TEntity, object>> paths, Expression<Func<TEntity, object>> field, IEnumerable<TValue> values) where TEntity : class
        {
            queries.Add(t => t.Nested(n => n.InnerHits(i => i.Explain()).Path(paths).Query(q => q.Terms(f => f.Field(field).Terms(values)))));
            return queries;
        }
        #endregion
        #region MustNot

        /// <summary>
        /// 判断不相等
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="queries"></param>
        /// <param name="paths">Nested类型的集合</param>
        /// <param name="field">要进行判断的字段</param>
        /// <param name="value">要进行判断的值</param>
        /// <returns></returns>
        public static ElasticQuery<TEntity> NotEqual<TEntity>(this ElasticQuery<TEntity> queries, Expression<Func<TEntity, object>> paths, Expression<Func<TEntity, object>> field, string value) where TEntity : class
        {
            queries.AddNot(t => t.Nested(n => n.Path(paths).Query(q => q.Bool(b => b.Must(m => m.Term(qs => qs.Field(field).Value(value)))))));
            return queries;
        }
        /// <summary>
        /// 判断不相等
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="queries"></param>
        /// <param name="field">要进行判断的字段</param>
        /// <param name="value">要进行判断的值</param>
        /// <returns></returns>
        public static ElasticQuery<TEntity> NotEqual<TEntity, TValue>(this ElasticQuery<TEntity> queries, Expression<Func<TEntity, object>> field, TValue value)
            where TEntity : class
            where TValue : struct
        {
            queries.AddNot(t => t.Term(q => q.Field(field).Value(value)));
            return queries;
        }
        /// <summary>
        /// 判断不包含
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="queries"></param>
        /// <param name="paths">Nested类型的集合</param>
        /// <param name="field">要进行判断的字段</param>
        /// <param name="value">要进行判断的值</param>
        /// <returns></returns>
        public static ElasticQuery<TEntity> NotLike<TEntity>(this ElasticQuery<TEntity> queries, Expression<Func<TEntity, object>> paths, Expression<Func<TEntity, object>> field, string value) where TEntity : class
        {
            queries.AddNot(t => t.Nested(n => n.Path(paths).Query(q => q.Bool(b => b.Must(m => m.QueryString(qs => qs.DefaultField(field).Query(value)))))));
            return queries;
        }
        /// <summary>
        /// 判断不包含
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="queries"></param>
        /// <param name="field">要进行判断的字段</param>
        /// <param name="value">要进行判断的值</param>
        /// <returns></returns>
        public static ElasticQuery<TEntity> NotLike<TEntity>(this ElasticQuery<TEntity> queries, Expression<Func<TEntity, object>> field, string value) where TEntity : class
        {
            queries.AddNot(t => t.QueryString(q => q.DefaultField(field).Query(value).MinimumShouldMatch(1)));
            return queries;
        }
        /// <summary>
        /// 判断不包含
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="queries"></param>
        /// <param name="paths"></param>
        /// <param name="field"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        public static ElasticQuery<TEntity> NotLike<TEntity, TValue>(this ElasticQuery<TEntity> queries, Expression<Func<TEntity, object>> paths, Expression<Func<TEntity, object>> field, IEnumerable<TValue> values) where TEntity : class
        {
            queries.AddNot(t => t.Nested(n => n.InnerHits(i => i.Explain()).Path(paths).Query(q => q.Terms(f => f.Field(field).Terms(values)))));
            return queries;
        }
        #endregion
    }
}
