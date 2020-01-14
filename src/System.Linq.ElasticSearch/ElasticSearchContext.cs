﻿using Microsoft.Extensions.Logging;
using Nest;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace System.Linq.ElasticSearch
{
    public class CategoryEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
    public class DemoEntity
    {
        public Guid Id { get; set; }
        public int Age { get; set; }
        public string Name { get; set; }
        [Nest.Nested]
        public List<CategoryEntity> Items { get; set; }
    }
    public class xxx
    {
        ElasticSearchContext esContext = new ElasticSearchContext(null, null);
        public void sss()
        {
            var query = esContext.Query<DemoEntity>("index-name");
            query.Index("index-name");
            query.Equal(f => f.Name, "姓名");
            query.ThanOrEquals(f => f.Age, 20);
            query.LessThan(f => f.Age, 30);
            query.Like(p => p.Items, f => f.Items.First().Name, "类型");
            var response = query.Skip(0).Take(50).OrderByDescending(f => f.Age).ToListAsync().Result;
            var result = response.Documents.Select(t => new
            {
                t.Id,
                t.Name,
                t.Age,
                t.Items
            });
        }
    }
    /// <summary>
    /// 
    /// </summary>
    public class ElasticSearchContext
    {
        /// <summary>
        /// 
        /// </summary>
        public IElasticClient Context { get; }
        /// <summary>
        /// 
        /// </summary>
        public ILogger<ElasticSearchContext> Logger { get; }
        public ElasticSearchContext(IElasticClient context, ILogger<ElasticSearchContext> logger)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        #region 因为ElasticClient没有实现IDisable接口，所以不需要这样额构造方法
        //public ElasticSearchContext(ConnectionSettingsBase<ConnectionSettings> settings, ILogger<ElasticSearchContext> logger)
        //{
        //    var _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        //    Context = new ElasticClient(_settings);
        //    Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        //} 
        #endregion
        /// <summary>
        /// 根据Id获取详情
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="id"></param>
        /// <returns></returns>
        public virtual async Task<TEntity> GetAsync<TEntity>(Guid id) where TEntity : class
        {
            var response = await Context.GetAsync<TEntity>(id);
            return response.Source;
        }
        /// <summary>
        /// 根据Id获取详情
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="id"></param>
        /// <param name="indexName"></param>
        /// <returns></returns>
        public virtual async Task<TEntity> GetAsync<TEntity>(Guid id, string indexName) where TEntity : class
        {
            var response = await Context.GetAsync<TEntity>(id, q => q.Index(indexName));
            return response.Source;
        }
        /// <summary>
        /// 添加一个文档到索引中
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="entity"></param>
        /// <param name="indexName"></param>
        /// <returns></returns>
        public virtual async Task<bool> AddAsync<TEntity>(TEntity entity, string indexName = null) where TEntity : class
        {
            BulkDescriptor bulkDescriptor = new BulkDescriptor();
            if (!string.IsNullOrWhiteSpace(indexName))
            {
                bulkDescriptor.Index(indexName);
            }
            var response = await Context.BulkAsync(bulkDescriptor.IndexMany(new TEntity[] { entity }));
            return response.ApiCall.Success;
        }
        /// <summary>
        /// 添加一个文档集合到索引中
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="entities"></param>
        /// <param name="indexName"></param>
        /// <returns></returns>
        public virtual async Task<bool> AddRangeAsync<TEntity>(TEntity[] entities, string indexName = null) where TEntity : class
        {
            BulkDescriptor bulkDescriptor = new BulkDescriptor();
            if (!string.IsNullOrWhiteSpace(indexName))
            {
                bulkDescriptor.Index(indexName);
            }
            var response = await Context.BulkAsync(bulkDescriptor.IndexMany(entities));
            return response.ApiCall.Success;
        }
        /// <summary>
        /// 更新一个文档
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="entity"></param>
        /// <param name="indexName"></param>
        /// <returns></returns>
        public virtual async Task<bool> UpdateAsync<TEntity>(TEntity entity, string indexName = null) where TEntity : class
        {
            UpdateDescriptor<TEntity, TEntity> doc = new UpdateDescriptor<TEntity, TEntity>(entity);
            if (!string.IsNullOrWhiteSpace(indexName))
            {
                doc.Index(indexName);
            }
            var response = await Context.UpdateAsync<TEntity>(entity, u => doc);
            return response.ApiCall.Success;
        }
    }
}
