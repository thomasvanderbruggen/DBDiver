using DBDiver.Models;
using DBDiver.Services;
using Xunit;

namespace DBDiver.Tests;

public class NaturalLanguageQueryServiceTests
{
    private static NaturalLanguageQueryService CreateService(DbSchema schema) =>
        new(schema, new QueryPathService());

    private static DbSchema CreateSchema()
    {
        var products = new DbTable
        {
            Name = "Products",
            Columns =
            [
                new DbColumn { Name = "ProductID", DataType = "int" },
                new DbColumn { Name = "Name", DataType = "nvarchar" }
            ]
        };

        var ambiguous = new DbTable
        {
            Name = "Ambiguous",
            Columns =
            [
                new DbColumn { Name = "Value1", DataType = "int" },
                new DbColumn { Name = "Value2", DataType = "int" }
            ]
        };

        return new DbSchema { Tables = [products, ambiguous] };
    }

    private static NaturalLanguageQueryDto CreateDto(
        string table,
        string column,
        string? groupColumn = null,
        string? orderColumn = null,
        string? whereColumn = null) => new()
    {
        Select = new NaturalLanguageSelectDto
        {
            Columns = [new NaturalLanguageProjectionItemDto { Table = table, Column = column }]
        },
        From = [table],
        GroupBy = groupColumn is null ? [] : [new NaturalLanguageColumnReferenceDto { Table = table, Column = groupColumn }],
        OrderBy = orderColumn is null ? [] : [new NaturalLanguageOrderByDto { Table = table, Column = orderColumn }],
        Where = whereColumn is null ? [] : [new NaturalLanguageWhereConditionDto
        {
            Table = table,
            Column = whereColumn,
            Comparator = "=",
            Values = ["1"]
        }]
    };

    [Fact]
    public void Validate_RejectsUnsupportedVersion()
    {
        var dto = new NaturalLanguageQueryDto
        {
            Version = 2,
            Select = new NaturalLanguageSelectDto { Columns = [] },
            From = ["Products"]
        };
        var result = CreateService(CreateSchema()).Validate(dto);

        Assert.Contains(result.Errors, error => error.Contains("Unsupported query contract version", StringComparison.Ordinal));
        Assert.Null(result.QueryModel);
    }

    [Fact]
    public void Validate_MapsAggregateOrderBy()
    {
        var dto = new NaturalLanguageQueryDto
        {
            Select = new NaturalLanguageSelectDto
            {
                Columns = [new NaturalLanguageProjectionItemDto { Table = "Products", Column = "Name" }]
            },
            From = ["Products"],
            OrderBy = [new NaturalLanguageOrderByDto
            {
                Table = "Products",
                Column = "Name",
                Aggregation = "SUM",
                Direction = "DESC"
            }]
        };

        var result = CreateService(CreateSchema()).Validate(dto);

        Assert.Empty(result.Errors);
        Assert.NotNull(result.QueryModel);
        var orderBy = result.QueryModel!.OrderByItems[0];
        Assert.Equal(QueryAggregateFunction.Sum, orderBy.Aggregation);
        Assert.Equal(QueryOrderDirection.Descending, orderBy.Direction);
        Assert.Contains(
            "ORDER BY SUM([Products].[Name]) DESC",
            new SqlServerQueryRenderer().Render(result.QueryModel),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_AutoCorrectsUniqueNearMatchTablesAndColumns()
    {
        var dto = CreateDto("Product", "ProductID", "Name1", "ProductID1", "Name1");
        var result = CreateService(CreateSchema()).Validate(dto);

        Assert.Empty(result.Errors);
        Assert.NotNull(result.QueryModel);
        Assert.Equal("Products", result.QueryModel!.Tables[0].Name);
        Assert.Equal("ProductID", result.QueryModel.Projection!.Items[0].Column);
        Assert.Equal("Name", result.QueryModel.GroupByItems[0].Column);
        Assert.Equal("ProductID", result.QueryModel.OrderByItems[0].Column);
        var where = Assert.IsType<QueryWhereClause>(result.QueryModel.WhereClauses[0]);
        Assert.Equal("Name", where.Column);
    }

    [Fact]
    public void Validate_RejectsAmbiguousNearMatchColumn()
    {
        var dto = CreateDto("Ambiguous", "Value3");
        var result = CreateService(CreateSchema()).Validate(dto);

        Assert.Contains(result.Errors, error => error.Contains("does not exist", StringComparison.Ordinal));
        Assert.Null(result.QueryModel);
    }

    [Fact]
    public void Validate_RejectsUnknownColumnWithoutNearMatch()
    {
        var dto = CreateDto("Products", "MissingColumn");
        var result = CreateService(CreateSchema()).Validate(dto);

        Assert.Contains(result.Errors, error => error.Contains("does not exist", StringComparison.Ordinal));
        Assert.Null(result.QueryModel);
    }

    [Fact]
    public void Validate_RejectsNonSelectRequests()
    {
        var dto = new NaturalLanguageQueryDto
        {
            Rejected = true,
            RejectionReason = "Only SELECT queries are supported."
        };
        var result = CreateService(CreateSchema()).Validate(dto);

        Assert.Contains(result.Errors, error => error.Contains("Only SELECT queries are supported.", StringComparison.Ordinal));
        Assert.Null(result.QueryModel);
    }
}
