using Microsoft.Extensions.Logging;
using Nest;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace ElasticSearch.SimpleQuery
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ElasticQuery<T> where T : class
    {
        private readonly List<Func<QueryContainerDescriptor<T>, QueryContainer>> _mustSelector;
        private readonly List<Func<QueryContainerDescriptor<T>, QueryContainer>> _mustNotSelector;
        private readonly ElasticSearchContext _searchContext;
        private string IndexName { get; set; }
        private int? SkipCount { get; set; }
        private int? TakeCount { get; set; }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="searchContext"></param>
        public ElasticQuery(ElasticSearchContext searchContext)
        {
            _searchContext = searchContext ?? throw new ArgumentNullException(nameof(searchContext));
            _mustSelector = new List<Func<QueryContainerDescriptor<T>, QueryContainer>>();
            _mustNotSelector = new List<Func<QueryContainerDescriptor<T>, QueryContainer>>();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="indexName"></param>
        /// <returns></returns>
        public ElasticQuery<T> Index(string indexName)
        {
            IndexName = indexName ?? throw new ArgumentNullException(nameof(indexName));
            return this;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="query"></param>
        public void Add(Func<QueryContainerDescriptor<T>, QueryContainer> query) => _mustSelector.Add(query);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="query"></param>
        public void AddNot(Func<QueryContainerDescriptor<T>, QueryContainer> query) => _mustNotSelector.Add(query);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="skip"></param>
        /// <returns></returns>
        public ElasticQuery<T> Skip(int skip)
        {
            if (skip < 0) throw new ArgumentException("skip不能小于0");
            SkipCount = skip;
            return this;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="take"></param>
        /// <returns></returns>
        public ElasticQuery<T> Take(int take)
        {
            if (take < 0) throw new ArgumentException("take不能小于0");
            TakeCount = take;
            return this;
        }
        private Func<SortDescriptor<T>, IPromise<IList<ISort>>> sortor;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="objectPath"></param>
        /// <returns></returns>
        public ElasticQuery<T> OrderByAscending(Expression<Func<T, object>> objectPath)
        {
            sortor = x => x.Ascending(objectPath);
            return this;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="objectPath"></param>
        /// <returns></returns>
        public ElasticQuery<T> OrderByDescending(Expression<Func<T, object>> objectPath)
        {
            sortor = x => x.Descending(objectPath);
            return this;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task<ISearchResponse<T>> ToListAsync()
        {
            SearchDescriptor<T> selector = new SearchDescriptor<T>();
            BoolQueryDescriptor<T> boolQuery = new BoolQueryDescriptor<T>();
            if (_mustSelector.Count > 0)
            {
                boolQuery.Must(_mustSelector);
            }
            if (_mustNotSelector.Count > 0)
            {
                boolQuery.MustNot(_mustNotSelector);
            }
            if (!string.IsNullOrWhiteSpace(IndexName))
            {
                selector.Index(IndexName);
            }
            selector = selector.Query(q => q.Bool(b => boolQuery)).From(SkipCount ?? 0);
            if (TakeCount > 0)
            {
                selector.Size(TakeCount.Value);
            }
            if (sortor != null)
            {
                selector.Sort(sortor);
            }
            var response = await _searchContext.Context.SearchAsync<T>(selector);
            if (!response.IsValid)
            {
                _searchContext.Logger.LogError($"[Success:{response.ApiCall.Success}]\t{response.ApiCall.Uri}");
                _searchContext.Logger.LogError(response.ApiCall.DebugInformation);
                _searchContext.Logger.LogError(response.ApiCall.OriginalException?.Message);
            }
            return response;
        }
    }
}
