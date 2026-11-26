using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RDCore.SDK.Platform;

/// <summary>
/// Reads and writes a <see cref="PlatformVersion"/> as its text, such as <c>"0.2.1-alpha"</c>.
/// </summary>
/// <remarks>
/// A four-part numeric version, such as <c>"0.2.1.0"</c>, as <see cref="Version"/> writes one, is read by its first three numbers. Any other
/// value, a string that is not a version or a value that is not a string, is read as null.
/// </remarks>
public sealed class PlatformVersionJsonConverter : JsonConverter<PlatformVersion?>
{
    /// <inheritdoc/>
    public override bool HandleNull => true;

    /// <summary>
    /// The platform version of a numeric version: its major, minor and patch numbers, which is all that
    /// <see cref="PlatformVersion.IsCompatibleWith"/> reads.
    /// </summary>
    /// <param name="version">The numeric version, such as an assembly's or an assembly reference's.</param>
    /// <returns>The version without a prerelease: <c>0.2.1.0</c> is <c>0.2.1</c>, and <c>0.2</c> is <c>0.2.0</c>.</returns>
    public static PlatformVersion Of(Version version)
    {
        ArgumentNullException.ThrowIfNull(version);
        return PlatformVersion.Parse(string.Create(CultureInfo.InvariantCulture, $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}"));
    }

    /// <inheritdoc/>
    public override PlatformVersion? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            reader.Skip();
            return null;
        }

        var text = reader.GetString();
        return PlatformVersion.TryParse(text, out var version) ? version
            : Version.TryParse(text, out var numeric) ? Of(numeric)
            : null;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, PlatformVersion? value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(value.Text);
        }
    }
}
