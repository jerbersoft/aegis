using System.Text.Json;
using System.Text.Json.Serialization;
using NodaTime;
using NodaTime.Text;

namespace Aegis.Shared.Serialization;

public static class AegisJson
{
    public static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Configure(options);
        return options;
    }

    public static void Configure(JsonSerializerOptions options)
    {
        if (!options.Converters.OfType<InstantJsonConverter>().Any())
        {
            options.Converters.Add(new InstantJsonConverter());
        }

        if (!options.Converters.OfType<LocalDateJsonConverter>().Any())
        {
            options.Converters.Add(new LocalDateJsonConverter());
        }
    }

    private sealed class InstantJsonConverter : JsonConverter<Instant>
    {
        public override Instant Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new JsonException("An ISO-8601 instant value is required.");
            }

            var parseResult = InstantPattern.ExtendedIso.Parse(value);
            if (!parseResult.Success)
            {
                throw new JsonException($"Invalid instant value '{value}'.");
            }

            return parseResult.Value;
        }

        public override void Write(Utf8JsonWriter writer, Instant value, JsonSerializerOptions options) =>
            writer.WriteStringValue(InstantPattern.ExtendedIso.Format(value));
    }

    private sealed class LocalDateJsonConverter : JsonConverter<LocalDate>
    {
        public override LocalDate Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new JsonException("An ISO local date value is required.");
            }

            var parseResult = LocalDatePattern.Iso.Parse(value);
            if (!parseResult.Success)
            {
                throw new JsonException($"Invalid local date value '{value}'.");
            }

            return parseResult.Value;
        }

        public override void Write(Utf8JsonWriter writer, LocalDate value, JsonSerializerOptions options) =>
            writer.WriteStringValue(LocalDatePattern.Iso.Format(value));
    }
}
