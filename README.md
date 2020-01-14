# 测试类
```csharp
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
    [Nest.Nested]//!!!!重点!!!!
    public List<CategoryEntity> Items { get; set; }
}
```
# 查询方法

```csharp
private readonly ElasticSearchContext esContext;
//注入ElasticSearchContext
//创建查询
var query = esContext.Query<DemoEntity>();
//or
//var query = esContext.Query<DemoEntity>("index-name");
//or
//query.Index("index-name");
query.Equal(f => f.Name, "姓名");//Name==姓名
query.ThanOrEquals(f => f.Age, 20);//Age>=20
query.LessThan(f => f.Age, 30);//Age<30
query.Like(p => p.Items, f => f.Items.First().Name, "类型");//Items集合中的Name=类型
var response=await query.Skip(0).Take(50).OrderByDescending(f => f.Age).ToListAsync();//跳过0条，取回50条，按照Age倒序
var totalCount=response.Total;
var result = response.Documents.Select(t => new
            {
                t.Id,
                t.Name,
                t.Age,
                t.Items
            });
```
