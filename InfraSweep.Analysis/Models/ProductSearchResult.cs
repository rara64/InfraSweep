using System.Text.Json.Serialization;

namespace InfraSweep.Analysis.Models;

public class ProductSearchResult
{
    [JsonPropertyName("metadata")]
    public required MetadataInfo Metadata {get; set;}

    [JsonPropertyName("data")]
    public required List<ProductSearchData> Data {get; set;}
}

public class MetadataInfo
{
    [JsonPropertyName("count")]
    public int Count {get; set;}

    [JsonPropertyName("page")]
    public int Page {get; set;}

    [JsonPropertyName("per_page")]
    public int PerPage {get; set;}
}

public class ProductSearchData
{
    [JsonPropertyName("id")]
    public required string Id {get; set;}

    [JsonPropertyName("uuid")]
    public required string Uuid {get; set;}

    [JsonPropertyName("name")]
    public required string Name {get; set;}

    [JsonPropertyName("description")]
    public required string Description {get; set;}

    [JsonPropertyName("creation_timestamp")]
    public DateTimeOffset CreationTimestamp {get; set;}

    [JsonPropertyName("updated_timestamp")]
    public DateTimeOffset UpdatedTimestamp {get; set;}
}