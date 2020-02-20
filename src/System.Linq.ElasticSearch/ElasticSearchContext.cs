using Microsoft.Extensions.Logging;
using Nest;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ElasticSearch.SimpleQuery
{
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
        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <param name="logger"></param>
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
        /// 
        /// </summary>
        /// <param name="Ids"></param>
        /// <param name="indexName"></param>
        /// <returns></returns>
        public async Task<ISearchResponse<TEntity>> GetByIdsAsync<TEntity, TKey>(IEnumerable<TKey> Ids, string indexName = null) where TEntity : class
            where TKey : Id
        {
            SearchDescriptor<TEntity> selector = new SearchDescriptor<TEntity>();
            if (!string.IsNullOrWhiteSpace(indexName))
            {
                selector.Index(indexName);
            }
            selector.Query(q => q.Ids(i => i.Values(Ids)));
            var response = await Context.SearchAsync<TEntity>(selector);
            if (!response.IsValid)
            {
                Logger.LogError($"[Success:{response.ApiCall.Success}]\t{response.ApiCall.Uri}");
                Logger.LogError(response.ApiCall.DebugInformation);
                Logger.LogError(response.ApiCall.OriginalException?.Message);
            }
            return response;
        }
        /// <summary>
        /// 根据Id获取详情
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <typeparam name="TKey"></typeparam>
        /// <param name="id"></param>
        /// <returns></returns>
        public virtual async Task<TEntity> GetAsync<TEntity, TKey>(TKey id) where TEntity : class
            where TKey : Id
        {
            var response = await Context.GetAsync<TEntity>(id);
            return response.Source;
        }
        /// <summary>
        /// 根据Id获取详情
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <typeparam name="TKey"></typeparam>
        /// <param name="id"></param>
        /// <param name="indexName"></param>
        /// <returns></returns>
        public virtual async Task<TEntity> GetAsync<TEntity, TKey>(TKey id, string indexName) where TEntity : class
            where TKey : Id
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
            IIndexResponse response;
            IndexRequest<TEntity> indexRequest = new IndexRequest<TEntity>(entity);
            if (!string.IsNullOrWhiteSpace(indexName))
            {
                response = await Context.IndexAsync(indexRequest, f => f.Index(indexName));
            }
            else
            {
                response = await Context.IndexAsync(indexRequest);
            }
            if (!response.IsValid)
            {
                Logger.LogError($"[Success:{response.ApiCall.Success}]\t{response.ApiCall.Uri}");
                Logger.LogError(response.ApiCall.DebugInformation);
                Logger.LogError(response.ApiCall.OriginalException?.Message);
            }
            return response.ApiCall.Success;
        }
        /// <summary>
        /// 添加一个文档集合到索引中(只是简单的进行遍历并调用AddAsync)
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="entities"></param>
        /// <param name="indexName"></param>
        /// <returns></returns>
        public virtual async Task<bool> AddRangeAsync<TEntity>(IEnumerable<TEntity> entities, string indexName = null) where TEntity : class
        {
            var result = true;
            foreach (var entity in entities)
            {
                var _ = await AddAsync(entity, indexName);
                if (_ == false)
                {
                    result = false;
                }
            }
            return result;
        }
        /// <summary>
        /// 添加一个文档集合到索引中(此方法查询数据会有延时)
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="entities"></param>
        /// <param name="indexName"></param>
        /// <returns></returns>
        public virtual async Task<bool> BulkAsync<TEntity>(IEnumerable<TEntity> entities, string indexName = null) where TEntity : class
        {
            BulkDescriptor bulkDescriptor = new BulkDescriptor();
            if (!string.IsNullOrWhiteSpace(indexName))
            {
                bulkDescriptor.Index(indexName);
            }
            bulkDescriptor.Refresh(Elasticsearch.Net.Refresh.True);
            var response = await Context.BulkAsync(bulkDescriptor.IndexMany(entities));
            if (!response.IsValid)
            {
                Logger.LogError($"[Success:{response.ApiCall.Success}]\t{response.ApiCall.Uri}");
                Logger.LogError(response.ApiCall.DebugInformation);
                Logger.LogError(response.ApiCall.OriginalException?.Message);
            }
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
            IUpdateResponse<TEntity> response;
            if (!string.IsNullOrWhiteSpace(indexName))
            {
                response = await Context.UpdateAsync<TEntity>(entity, u => u.Doc(entity).Index(indexName));
            }
            else
            {
                response = await Context.UpdateAsync<TEntity>(entity, u => u.Doc(entity));
            }
            if (!response.IsValid)
            {
                Logger.LogError($"[Success:{response.ApiCall.Success}]\t{response.ApiCall.Uri}");
                Logger.LogError(response.ApiCall.DebugInformation);
                Logger.LogError(response.ApiCall.OriginalException?.Message);
            }
            return response.ApiCall.Success;
        }
    }
}
