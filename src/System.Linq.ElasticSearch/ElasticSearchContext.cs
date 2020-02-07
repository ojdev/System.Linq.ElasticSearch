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
        public async Task<ISearchResponse<TEntity>> GetByIdsAsync<TEntity>(IEnumerable<Guid> Ids, string indexName = null) where TEntity : class
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
        /// 
        /// </summary>
        /// <param name="Ids"></param>
        /// <param name="indexName"></param>
        /// <returns></returns>
        public async Task<ISearchResponse<TEntity>> GetByIdsAsync<TEntity>(IEnumerable<string> Ids, string indexName = null) where TEntity : class
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
        /// 
        /// </summary>
        /// <param name="Ids"></param>
        /// <param name="indexName"></param>
        /// <returns></returns>
        public async Task<ISearchResponse<TEntity>> GetByIdsAsync<TEntity>(IEnumerable<long> Ids, string indexName = null) where TEntity : class
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
            if (!response.IsValid)
            {
                Logger.LogError($"[Success:{response.ApiCall.Success}]\t{response.ApiCall.Uri}");
                Logger.LogError(response.ApiCall.DebugInformation);
                Logger.LogError(response.ApiCall.OriginalException?.Message);
            }
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
            UpdateDescriptor<TEntity, TEntity> doc = new UpdateDescriptor<TEntity, TEntity>(entity);
            if (!string.IsNullOrWhiteSpace(indexName))
            {
                doc.Index(indexName);
            }
            var response = await Context.UpdateAsync<TEntity>(entity, u => doc);
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
