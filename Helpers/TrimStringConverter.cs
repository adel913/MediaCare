using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace MedicalApp.API.Helpers
{
    public class TrimStringConverter : JsonConverter<string>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            if (string.IsNullOrWhiteSpace(value))
                return value;

            // Trim leading and trailing whitespace
            var trimmedValue = value.Trim();

            // Simple sanitization to prevent basic XSS (HTML Encoding)
            // Note: If you need to accept HTML in some specific fields, you might need a more complex strategy,
            // but for a general medical app, HTML encoding strings from JSON is a safe default.
            // We will just trim for now as HTML encoding everything might double-encode.
            // For XSS we should encode on output or use a proper sanitization library like HtmlSanitizer if HTML is allowed.
            // But stripping basic tags can also be done. Here we just trim as it's the safest non-destructive action.
            
            return trimmedValue;
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }
    }
}
