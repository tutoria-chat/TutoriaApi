using System.Text.Json;
using TutoriaApi.Web.API.DTOs;
using TutoriaApi.Web.API.Json;
using Xunit;

namespace TutoriaApi.Tests.Unit.Json;

/// <summary>
/// Covers the converter that lets an unset date from a front-end date picker bind
/// cleanly, preventing the 400 "One or more validation errors occurred" that blocked
/// professors from saving their matricula via PUT /api/auth/me.
/// </summary>
public class EmptyStringToNullDateTimeConverterTests
{
    private sealed class Holder
    {
        [System.Text.Json.Serialization.JsonConverter(typeof(EmptyStringToNullDateTimeConverter))]
        public DateTime? Value { get; set; }
    }

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Read_EmptyString_ReturnsNull()
    {
        var result = JsonSerializer.Deserialize<Holder>("{\"value\":\"\"}", Options);

        Assert.NotNull(result);
        Assert.Null(result!.Value);
    }

    [Fact]
    public void Read_WhitespaceString_ReturnsNull()
    {
        var result = JsonSerializer.Deserialize<Holder>("{\"value\":\"   \"}", Options);

        Assert.NotNull(result);
        Assert.Null(result!.Value);
    }

    [Fact]
    public void Read_NullToken_ReturnsNull()
    {
        var result = JsonSerializer.Deserialize<Holder>("{\"value\":null}", Options);

        Assert.NotNull(result);
        Assert.Null(result!.Value);
    }

    [Fact]
    public void Read_DateOnlyString_ReturnsDate()
    {
        var result = JsonSerializer.Deserialize<Holder>("{\"value\":\"2000-01-15\"}", Options);

        Assert.NotNull(result);
        Assert.Equal(new DateTime(2000, 1, 15), result!.Value);
    }

    [Fact]
    public void Read_IsoDateTimeString_ReturnsDate()
    {
        var result = JsonSerializer.Deserialize<Holder>("{\"value\":\"2000-01-15T09:30:00\"}", Options);

        Assert.NotNull(result);
        Assert.Equal(new DateTime(2000, 1, 15, 9, 30, 0), result!.Value);
    }

    [Fact]
    public void Read_UnparseableString_ThrowsJsonException()
    {
        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<Holder>("{\"value\":\"not-a-date\"}", Options));
    }

    /// <summary>
    /// Reproduces the reported bug at the DTO layer: the user settings modal posts an
    /// empty birthdate alongside the matricula. This must deserialize without error so
    /// the request reaches the handler and the matricula is saved.
    /// </summary>
    [Fact]
    public void UpdateProfileRequest_EmptyBirthdateWithMatricula_DeserializesWithoutError()
    {
        const string json = """
            {
                "firstName": "Ana",
                "lastName": "Souza",
                "email": "ana@uni.edu",
                "birthdate": "",
                "externalId": "2023001234"
            }
            """;

        var request = JsonSerializer.Deserialize<UpdateProfileRequest>(json, Options);

        Assert.NotNull(request);
        Assert.Null(request!.Birthdate);
        Assert.Equal("2023001234", request.ExternalId);
        Assert.Equal("Ana", request.FirstName);
        Assert.Equal("ana@uni.edu", request.Email);
    }

    [Fact]
    public void UpdateProfileRequest_ValidBirthdate_Binds()
    {
        const string json = """
            { "birthdate": "1998-07-20", "externalId": "555" }
            """;

        var request = JsonSerializer.Deserialize<UpdateProfileRequest>(json, Options);

        Assert.NotNull(request);
        Assert.Equal(new DateTime(1998, 7, 20), request!.Birthdate);
        Assert.Equal("555", request.ExternalId);
    }
}
