using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PaperTrading.Api;

/// <summary>
/// Writes decimals as JSON strings ("0.1") and reads either strings or numbers.
/// JavaScript parses JSON numbers into binary floating point, which would reintroduce
/// rounding errors in the browser; a string keeps the exact value the server computed.
/// </summary>
public sealed class DecimalStringJsonConverter : JsonConverter<decimal>
{
    private const NumberStyles Style = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var text = reader.GetString();
            return decimal.TryParse(text, Style, CultureInfo.InvariantCulture, out var value)
                ? value
                : throw new JsonException($"'{text}' is not a valid decimal number.");
        }

        return reader.GetDecimal();
    }

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(Normalize(value).ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>Drops trailing zeros without changing the value: 67010.01000000 → 67010.01.</summary>
    public static decimal Normalize(decimal value) => value / 1.000000000000000000000000000000000m;
}
