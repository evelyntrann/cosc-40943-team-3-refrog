using System.Text.Json.Serialization;

namespace ReFrog.Contracts.Api;

[JsonConverter(typeof(JsonStringEnumConverter<ApiErrorCategory>))]
public enum ApiErrorCategory
{
    [JsonStringEnumMemberName("VALIDATION")]
    Validation,

    [JsonStringEnumMemberName("AUTHORIZATION")]
    Authorization,

    [JsonStringEnumMemberName("UNAVAILABLE")]
    Unavailable,

    [JsonStringEnumMemberName("UNEXPECTED")]
    Unexpected,
}
