using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NickeltownPOSV4.Services.Migration;

/// <summary>
/// Shared JSON options for migration. Initialization is guarded so host/runtime quirks cannot fail type load
/// (which would mark every file as ParseError).
/// </summary>
internal static class MigrationJsonDefaults
{
    public static JsonSerializerOptions SerializerOptions { get; }

    public static JsonDocumentOptions DocumentOptions { get; }

    static MigrationJsonDefaults()
    {
        try
        {
            SerializerOptions = BuildSerializerOptions();
        }
        catch
        {
            SerializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        }

        try
        {
            DocumentOptions = BuildDocumentOptions();
        }
        catch
        {
            DocumentOptions = default;
        }
    }

    private static JsonSerializerOptions BuildSerializerOptions()
    {
        try
        {
            var o = new JsonSerializerOptions();
            TrySet(o, x => x.PropertyNameCaseInsensitive = true);
            TrySet(o, x => x.ReadCommentHandling = JsonCommentHandling.Allow);
            TrySet(o, x => x.AllowTrailingCommas = true);
            TrySet(o, x => x.NumberHandling = JsonNumberHandling.AllowReadingFromString);
            return o;
        }
        catch
        {
            return new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        }
    }

    private static void TrySet(JsonSerializerOptions o, Action<JsonSerializerOptions> set)
    {
        try
        {
            set(o);
        }
        catch
        {
        }
    }

    private static JsonDocumentOptions BuildDocumentOptions()
    {
        try
        {
            return new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Allow,
                AllowTrailingCommas = true,
            };
        }
        catch
        {
            try
            {
                return new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Allow,
                };
            }
            catch
            {
                return default;
            }
        }
    }
}
