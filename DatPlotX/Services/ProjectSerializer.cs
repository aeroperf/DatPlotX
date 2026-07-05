using DatPlotX.Models;
using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DatPlotX.Services;

/// <summary>
/// Handles JSON serialization and deserialization of project settings
/// Uses JSON with custom converters for complex types like DataTable
/// </summary>
public class ProjectSerializer : IProjectSerializer
{
    private readonly JsonSerializerOptions _jsonOptions;

    public ProjectSerializer()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            // Explicit depth cap (matches the System.Text.Json default of 64) documents intent and
            // bounds nesting on a hand-crafted .DPX. Project JSON is shallow; nothing legitimate
            // approaches this.
            MaxDepth = 64,
            // An empty / never-scaled axis can report ±Infinity; without this the whole save
            // throws. Allow named float literals so a stray Infinity/NaN round-trips instead
            // of failing the save (capture is also guarded — see ProjectStateManager).
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
            Converters =
            {
                new DataTableJsonConverter()
            }
        };
    }

    /// <summary>
    /// Serialize project settings to JSON string
    /// </summary>
    public string SerializeToJson(ProjectSettingsModel project)
    {
        ArgumentNullException.ThrowIfNull(project);

        // Update last modified timestamp
        project.LastModified = DateTime.Now;

        return JsonSerializer.Serialize(project, _jsonOptions);
    }

    /// <summary>
    /// Deserialize project settings from JSON string
    /// </summary>
    public ProjectSettingsModel DeserializeFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("JSON string cannot be null or empty", nameof(json));

        var project = JsonSerializer.Deserialize<ProjectSettingsModel>(json, _jsonOptions);

        if (project == null)
            throw new InvalidDataException("Failed to deserialize project file");

        // A pre-versioning file has no schemaVersion field, so it deserializes to 0. Those files
        // are the original on-disk shape, which is v1 — normalize so downstream code can rely on a
        // real version number. Future breaking changes add their migration steps here.
        if (project.SchemaVersion <= 0)
            project.SchemaVersion = 1;

        return project;
    }
}

/// <summary>
/// Interface for project serialization
/// </summary>
public interface IProjectSerializer
{
    /// <summary>
    /// Serialize project settings to JSON string
    /// </summary>
    string SerializeToJson(ProjectSettingsModel project);

    /// <summary>
    /// Deserialize project settings from JSON string
    /// </summary>
    ProjectSettingsModel DeserializeFromJson(string json);
}

/// <summary>
/// Custom JSON converter for DataTable
/// </summary>
public class DataTableJsonConverter : JsonConverter<DataTable>
{
    /// <summary>
    /// Whitelist of allowed types for deserialization to prevent type injection attacks
    /// </summary>
    private static readonly HashSet<string> AllowedTypes = new()
    {
        "System.String",
        "System.Int32",
        "System.Int64",
        "System.Double",
        "System.Single",
        "System.Boolean",
        "System.DateTime",
        "System.Decimal",
        "System.Byte",
        "System.Int16",
        "System.UInt32",
        "System.UInt64",
        "System.Char"
    };

    /// <summary>
    /// Validates that a type string is safe for deserialization
    /// </summary>
    private static Type ValidateAndGetType(string typeString)
    {
        if (string.IsNullOrWhiteSpace(typeString))
        {
            return typeof(string);
        }

        // SECURITY: Check against whitelist to prevent type injection attacks (CWE-502)
        if (!AllowedTypes.Contains(typeString))
        {
            throw new System.Security.SecurityException(
                $"Unsafe type '{typeString}' detected in project file. Only basic data types are allowed for security reasons.");
        }

        var type = Type.GetType(typeString);
        if (type == null)
        {
            throw new InvalidDataException($"Type '{typeString}' could not be loaded.");
        }

        return type;
    }

    public override DataTable? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        var dataTable = new DataTable();
        var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        // Read columns
        if (root.TryGetProperty("columns", out var columnsElement))
        {
            foreach (var columnElement in columnsElement.EnumerateArray())
            {
                // SECURITY: A hand-crafted .DPX bypasses the CSV parser, so enforce the same
                // resource-exhaustion caps here (the 200 MB decompress cap alone lets a small file
                // declare millions of columns/rows and OOM the load). See security-baseline.md.
                if (dataTable.Columns.Count >= ApplicationSettings.DefaultMaxColumnCount)
                {
                    throw new InvalidDataException(
                        $"Project file exceeds the maximum column count ({ApplicationSettings.DefaultMaxColumnCount:N0}).");
                }

                var columnName = columnElement.GetProperty("name").GetString() ?? "Column";
                var columnType = columnElement.GetProperty("type").GetString() ?? "System.String";

                // SECURITY: Validate type against whitelist before instantiation
                var safeType = ValidateAndGetType(columnType);
                dataTable.Columns.Add(columnName, safeType);
            }
        }

        // Read rows
        if (root.TryGetProperty("rows", out var rowsElement))
        {
            foreach (var rowElement in rowsElement.EnumerateArray())
            {
                if (dataTable.Rows.Count >= ApplicationSettings.DefaultMaxRowCount)
                {
                    throw new InvalidDataException(
                        $"Project file exceeds the maximum row count ({ApplicationSettings.DefaultMaxRowCount:N0}).");
                }

                var row = dataTable.NewRow();
                int columnIndex = 0;

                foreach (var valueElement in rowElement.EnumerateArray())
                {
                    if (columnIndex < dataTable.Columns.Count)
                    {
                        var column = dataTable.Columns[columnIndex];
                        object? value = null;

                        if (valueElement.ValueKind != JsonValueKind.Null)
                        {
                            value = column.DataType.Name switch
                            {
                                "Int16" => valueElement.GetInt16(),
                                "Int32" => valueElement.GetInt32(),
                                "Int64" => valueElement.GetInt64(),
                                "UInt32" => valueElement.GetUInt32(),
                                "UInt64" => valueElement.GetUInt64(),
                                "Byte" => valueElement.GetByte(),
                                // ±Infinity / NaN are written as string literals (see Write),
                                // so a Single/Double cell may arrive as a String token.
                                "Single" => valueElement.ValueKind == JsonValueKind.String
                                    ? float.Parse(valueElement.GetString()!, System.Globalization.CultureInfo.InvariantCulture)
                                    : valueElement.GetSingle(),
                                "Double" => valueElement.ValueKind == JsonValueKind.String
                                    ? double.Parse(valueElement.GetString()!, System.Globalization.CultureInfo.InvariantCulture)
                                    : valueElement.GetDouble(),
                                "Decimal" => valueElement.GetDecimal(),
                                "Boolean" => valueElement.GetBoolean(),
                                "DateTime" => valueElement.GetDateTime(),
                                "Char" => (valueElement.GetString() ?? string.Empty) is { Length: > 0 } s ? s[0] : '\0',
                                _ => valueElement.GetString()
                            };
                        }

                        row[columnIndex] = value ?? DBNull.Value;
                    }
                    columnIndex++;
                }

                dataTable.Rows.Add(row);
            }
        }

        return dataTable;
    }

    public override void Write(Utf8JsonWriter writer, DataTable value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        // Write columns
        writer.WriteStartArray("columns");
        foreach (DataColumn column in value.Columns)
        {
            writer.WriteStartObject();
            writer.WriteString("name", column.ColumnName);
            writer.WriteString("type", column.DataType.FullName);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        // Write rows
        writer.WriteStartArray("rows");
        foreach (DataRow row in value.Rows)
        {
            writer.WriteStartArray();
            foreach (var item in row.ItemArray)
            {
                if (item == null || item == DBNull.Value)
                {
                    writer.WriteNullValue();
                }
                else
                {
                    switch (item)
                    {
                        case short shortValue:
                            writer.WriteNumberValue(shortValue);
                            break;
                        case int intValue:
                            writer.WriteNumberValue(intValue);
                            break;
                        case long longValue:
                            writer.WriteNumberValue(longValue);
                            break;
                        case uint uintValue:
                            writer.WriteNumberValue(uintValue);
                            break;
                        case ulong ulongValue:
                            writer.WriteNumberValue(ulongValue);
                            break;
                        case byte byteValue:
                            writer.WriteNumberValue(byteValue);
                            break;
                        case float floatValue:
                            // Utf8JsonWriter.WriteNumberValue throws on ±Infinity/NaN
                            // regardless of NumberHandling, so emit the named literal as a
                            // string (matched by the float/double parsing in Read).
                            if (float.IsFinite(floatValue))
                                writer.WriteNumberValue(floatValue);
                            else
                                writer.WriteStringValue(floatValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
                            break;
                        case double doubleValue:
                            if (double.IsFinite(doubleValue))
                                writer.WriteNumberValue(doubleValue);
                            else
                                writer.WriteStringValue(doubleValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
                            break;
                        case decimal decimalValue:
                            writer.WriteNumberValue(decimalValue);
                            break;
                        case bool boolValue:
                            writer.WriteBooleanValue(boolValue);
                            break;
                        case DateTime dateTimeValue:
                            writer.WriteStringValue(dateTimeValue);
                            break;
                        case char charValue:
                            writer.WriteStringValue(charValue.ToString());
                            break;
                        default:
                            writer.WriteStringValue(item.ToString());
                            break;
                    }
                }
            }
            writer.WriteEndArray();
        }
        writer.WriteEndArray();

        writer.WriteEndObject();
    }
}
