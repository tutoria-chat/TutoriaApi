using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TutoriaApi.Web.API.Json;

/// <summary>
/// Binds a JSON value to <see cref="Nullable{DateTime}"/>, treating an empty or
/// whitespace-only string as <c>null</c>.
///
/// Front-end date pickers commonly emit <c>""</c> for an unset date (e.g. the user
/// settings modal sends <c>"birthdate": ""</c> when no birthdate is set). Without
/// this converter System.Text.Json cannot bind <c>""</c> to <see cref="DateTime"/>?
/// and records a model-binding error, which surfaces to the client as a 400
/// "One or more validation errors occurred" — failing the whole request even when
/// every other field (matricula, name, email) is valid.
/// </summary>
public class EmptyStringToNullDateTimeConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;

            case JsonTokenType.String:
                var value = reader.GetString();
                if (string.IsNullOrWhiteSpace(value))
                {
                    return null;
                }

                if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                {
                    return parsed;
                }

                throw new JsonException($"Unable to parse '{value}' as a date.");

            default:
                // Any other token type (e.g. a JSON number) is not a valid date here.
                return reader.GetDateTime();
        }
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            writer.WriteStringValue(value.Value);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
