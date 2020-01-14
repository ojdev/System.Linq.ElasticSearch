using Microsoft.Extensions.Logging;
using Nest;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace System.Linq.ElasticSearch
{
    public class ElasticQuery<T> where T : class
    {
        private readonly List<Func<QueryContainerDescriptor<T>, QueryContainer>> _mustSelector;
        private readonly List<Func<QueryContainerDescriptor<T>, QueryContainer>> _mustNotSelector;
        private readonly ElasticSearchContext _searchContext;
        private string IndexName { get; set; }
        private int? SkipCount { get; set; }
        private int? TakeCount { get; set; }

        public ElasticQuery(ElasticSearchContext searchContext)
        {
            _searchContext = searchContext ?? throw new ArgumentNullException(nameof(searchContext));
            _mustSelector = new List<Func<QueryContainerDescriptor<T>, QueryContainer>>();
            _mustNotSelector = new List<Func<QueryContainerDescriptor<T>, QueryContainer>>();
        }
        public ElasticQuery<T> Index(string indexName)
        {
            IndexName = indexName ?? throw new ArgumentNullException(nameof(indexName));
            return this;
        }
        public void Add(Func<QueryContainerDescriptor<T>, QueryContainer> query) => _mustSelector.Add(query);
        public void AddNot(Func<QueryContainerDescriptor<T>, QueryContainer> query) => _mustNotSelector.Add(query);
        public ElasticQuery<T> Skip(int skip)
        {
            if (skip < 0) throw new ArgumentException("skip不能小于0");
            SkipCount = skip;
            return this;
        }
        public ElasticQuery<T> Take(int take)
        {
            if (take < 0) throw new ArgumentException("take不能小于0");
            TakeCount = take;
            return this;
        }
        private Func<SortDescriptor<T>, IPromise<IList<ISort>>> sortor;
        public ElasticQuery<T> Ascending(Expression<Func<T, object>> objectPath)
        {
            sortor = x => x.Ascending(objectPath);
            return this;
        }
        public ElasticQuery<T> Descending(Expression<Func<T, object>> objectPath)
        {
            sortor = x => x.Descending(objectPath);
            return this;
        }
        public async Task<ISearchResponse<T>> ToListAsync()
        {
            SearchDescriptor<T> selector = new SearchDescriptor<T>();
            BoolQueryDescriptor<T> boolQuery = new BoolQueryDescriptor<T>();
            if (_mustNotSelector.Count > 0)
            {
                boolQuery = boolQuery.Must(_mustSelector);
            }
            if (_mustNotSelector.Count > 0)
            {
                boolQuery = boolQuery.MustNot(_mustNotSelector);
            }
            if (!string.IsNullOrWhiteSpace(IndexName))
            {
                selector.Index(IndexName);
            }
            selector.Query(q => q.Bool(b => boolQuery));
            selector.From(SkipCount ?? 0);
            if (TakeCount > 0)
            {
                selector.Size(TakeCount.Value);
            }
            if (sortor != null)
            {
                selector.Sort(sortor);
            }
            var response = await _searchContext.Context.SearchAsync<T>(selector);
            _searchContext.Logger.LogInformation($"[Success:{response.ApiCall.Success}]\t[IsValid:{response.IsValid}]\t{response.ApiCall.Uri}");
            if (!response.ApiCall.Success)
            {
                _searchContext.Logger.LogError(response.ApiCall.DebugInformation);
            }
            return response;
        }
    }
}
