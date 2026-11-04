//
// --------------------------------------------------------------------------
//  Gurux Ltd
//
//
//
// Filename:        $HeadURL$
//
// Version:         $Revision$,
//                  $Date$
//                  $Author$
//
// Copyright (c) Gurux Ltd
//
//---------------------------------------------------------------------------
//
//  DESCRIPTION
//
// This file is a part of Gurux Device Framework.
//
// Gurux Device Framework is Open Source software; you can redistribute it
// and/or modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; version 2 of the License.
// Gurux Device Framework is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU General Public License for more details.
//
// This code is licensed under the GNU General Public License v2.
// Full text may be retrieved at http://www.gnu.org/licenses/gpl-2.0.txt
//---------------------------------------------------------------------------

using Gurux.DLMS.AMI.Shared;
using Microsoft.OpenApi;
using System.Globalization;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gurux.AMI.OpenApi.Internal
{
    /// <summary>Builds reusable schemas for each type and its inherited OpenAPI restrictions.</summary>
    internal sealed class GXOpenApiSchemaBuilder(OpenApiDocument document)
    {
        private readonly Dictionary<string, string> _schemas = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _names = new(StringComparer.Ordinal);

        internal IOpenApiSchema Build(Type type) => Build(type, [], []);

        private IOpenApiSchema Build(Type type, IReadOnlyList<ExcludeOpenApiAttribute> excluded,
            IReadOnlyList<IncludeOpenApiAttribute> included)
        {
            type = UnderlyingNullable(type);
            if (TryMapSimple(type, out var simple))
            {
                return simple;
            }

            if (IsDictionary(type, out var valueType))
            {
                return new OpenApiSchema { Type = JsonSchemaType.Object, AdditionalProperties = Build(valueType, excluded, included) };
            }

            if (IsEnumerable(type, out var element))
            {
                return new OpenApiSchema { Type = JsonSchemaType.Array, Items = Build(element, excluded, included) };
            }

            // Normalize the complete inherited context: restrictions on descendants also affect this schema.
            // Sorting and merging makes attribute order and repeated attributes irrelevant, including on cycles.
            var key = GetKey(type, excluded, included);
            if (_schemas.TryGetValue(key, out var existing))
            {
                return new OpenApiSchemaReference(existing, document);
            }

            var prefix = new string(type.Name.Split('`')[0].Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
            var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
            var name = prefix + "_" + digest[..16];
            if (_names.TryGetValue(name, out var owner) && owner != key)
            {
                name = prefix + "_" + digest;
            }

            _names.Add(name, key);
            _schemas.Add(key, name);
            document.Components ??= new OpenApiComponents();
            document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>();
            var schema = new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                Description = type.GetCustomAttribute<DescriptionAttribute>()?.Description,
                Required = new HashSet<string>(),
                Properties = new Dictionary<string, IOpenApiSchema>()
            };
            // Register before following properties, so a parent reference can reuse this component.
            document.Components.Schemas.Add(name, schema);
            document.Workspace ??= new OpenApiWorkspace();
            document.Workspace.RegisterComponentForDocument<IOpenApiSchema>(document, schema, name);
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead))
            {
                if (property.GetCustomAttributes<JsonIgnoreAttribute>().Any())
                {
                    continue;
                }

                if (excluded.Any(e => e.Type == type && e.Exclude.Contains(property.Name)))
                {
                    continue;
                }

                var limits = included.Where(e => e.Type == type).ToArray();
                if (limits.Length != 0 && !limits.Any(e => e.Include.Contains(property.Name)))
                {
                    continue;
                }

                var childExcluded = excluded.Concat(property.GetCustomAttributes<ExcludeOpenApiAttribute>()).ToArray();
                var childIncluded = included.Concat(property.GetCustomAttributes<IncludeOpenApiAttribute>()).ToArray();
                var propertyName = ToCamel(property.Name);
                var childSchema = Build(property.PropertyType, childExcluded, childIncluded);
                var description = property.GetCustomAttribute<DescriptionAttribute>()?.Description;
                if (childSchema is OpenApiSchemaReference && !string.IsNullOrWhiteSpace(description))
                {
                    // OpenAPI 3.0 ignores siblings of $ref. Keep per-property metadata
                    // on a wrapper rather than changing the shared component.
                    childSchema = new OpenApiSchema { AllOf = [childSchema] };
                }
                if (childSchema is OpenApiSchema inline)
                {
                    ApplyValidation(inline, property.GetCustomAttributes());
                    inline.Description = string.IsNullOrWhiteSpace(description) ? inline.Description :
                        string.IsNullOrWhiteSpace(inline.Description) ? description : description + "\n\n" + inline.Description;
                    if (property.GetCustomAttribute<DefaultValueAttribute>()?.Value is { } defaultValue)
                    {
                        var propertyType = UnderlyingNullable(property.PropertyType);
                        var value = propertyType.IsEnum
                            ? JsonValue.Create(Enum.ToObject(propertyType, defaultValue).ToString())
                            : JsonSerializer.SerializeToNode(defaultValue);
                        inline.Default = value;
                        inline.Examples = [value!.DeepClone()];
                    }
                }
                schema.Properties.Add(propertyName, childSchema);
                var optionalPaging = property.Name is "Index" or "Count" &&
                    property.GetCustomAttribute<DefaultValueAttribute>() != null;
                if (property.PropertyType.IsValueType && Nullable.GetUnderlyingType(property.PropertyType) is null &&
                    !optionalPaging)
                {
                    schema.Required.Add(propertyName);
                }
            }
            return new OpenApiSchemaReference(name, document);
        }

        private static string GetKey(Type type, IEnumerable<ExcludeOpenApiAttribute> excluded,
            IEnumerable<IncludeOpenApiAttribute> included)
        {
            var key = new StringBuilder(type.AssemblyQualifiedName);
            AppendRestrictions(key, "exclude", excluded.Select(e => (e.Type, Names: e.Exclude)));
            AppendRestrictions(key, "include", included.Select(e => (e.Type, Names: e.Include)));
            return key.ToString();
        }

        private static void AppendRestrictions(StringBuilder key, string kind,
            IEnumerable<(Type Type, string[] Names)> restrictions)
        {
            foreach (var group in restrictions.GroupBy(e => e.Type).OrderBy(g => g.Key.AssemblyQualifiedName, StringComparer.Ordinal))
            {
                key.Append('\n').Append(kind).Append(':').Append(group.Key.AssemblyQualifiedName).Append(':');
                key.AppendJoin(',', group.SelectMany(e => e.Names).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
            }
        }

        private static string ToCamel(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s[1..];

        internal static void ApplyValidation(OpenApiSchema schema, IEnumerable<Attribute> attributes)
        {
            foreach (var attribute in attributes)
            {
                if (attribute is StringLengthAttribute length && schema.Type == JsonSchemaType.String)
                {
                    schema.MinLength = Math.Max(schema.MinLength ?? 0, length.MinimumLength);
                    schema.MaxLength = Math.Min(schema.MaxLength ?? int.MaxValue, length.MaximumLength);
                }
                if (attribute is MinLengthAttribute min)
                {
                    if (schema.Type == JsonSchemaType.String)
                    {
                        schema.MinLength = Math.Max(schema.MinLength ?? 0, min.Length);
                    }

                    if (schema.Type == JsonSchemaType.Array)
                    {
                        schema.MinItems = Math.Max(schema.MinItems ?? 0, min.Length);
                    }
                }
                if (attribute is MaxLengthAttribute max && max.Length >= 0)
                {
                    if (schema.Type == JsonSchemaType.String)
                    {
                        schema.MaxLength = Math.Min(schema.MaxLength ?? int.MaxValue, max.Length);
                    }

                    if (schema.Type == JsonSchemaType.Array)
                    {
                        schema.MaxItems = Math.Min(schema.MaxItems ?? int.MaxValue, max.Length);
                    }
                }
                if (attribute is RegularExpressionAttribute expression && schema.Type == JsonSchemaType.String)
                {
                    schema.Pattern = expression.Pattern;
                }

                if (attribute is RangeAttribute range && schema.Type is JsonSchemaType.Integer or JsonSchemaType.Number)
                {
                    var minimum = Convert.ToString(range.Minimum, CultureInfo.InvariantCulture)!;
                    var maximum = Convert.ToString(range.Maximum, CultureInfo.InvariantCulture)!;
                    if (range.MinimumIsExclusive)
                    {
                        schema.ExclusiveMinimum = minimum;
                    }
                    else
                    {
                        schema.Minimum = minimum;
                    }

                    if (range.MaximumIsExclusive)
                    {
                        schema.ExclusiveMaximum = maximum;
                    }
                    else
                    {
                        schema.Maximum = maximum;
                    }
                }
            }
        }

        private static Type UnderlyingNullable(Type t) => Nullable.GetUnderlyingType(t) ?? t;

        internal static bool TryMapSimple(Type t, out OpenApiSchema schema)
        {
            t = UnderlyingNullable(t);

            if (t == typeof(string))
            {
                schema = new OpenApiSchema { Type = JsonSchemaType.String };
                return true;
            }
            if (t == typeof(bool))
            {
                schema = new OpenApiSchema { Type = JsonSchemaType.Boolean };
                return true;
            }
            if (t == typeof(int))
            {
                schema = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" };
                return true;
            }
            if (t == typeof(long))
            {
                schema = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int64" };
                return true;
            }
            if (t == typeof(float))
            {
                schema = new OpenApiSchema { Type = JsonSchemaType.Number, Format = "float" };
                return true;
            }
            if (t == typeof(double) || t == typeof(decimal))
            {
                schema = new OpenApiSchema { Type = JsonSchemaType.Number, Format = "double" };
                return true;
            }
            if (t == typeof(DateTime) || t == typeof(DateTimeOffset))
            {
                schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "date-time" };
                return true;
            }
            if (t == typeof(Guid))
            {
                schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" };
                return true;
            }
            if (t == typeof(byte[]))
            {
                schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "byte" };
                return true;
            }
            if (t == typeof(byte))
            {
                schema = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32", Minimum = 0.ToString(CultureInfo.InvariantCulture), Maximum = byte.MaxValue.ToString(CultureInfo.InvariantCulture) };
                return true;
            }
            if (t == typeof(ushort))
            {
                schema = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32", Minimum = 0.ToString(CultureInfo.InvariantCulture), Maximum = ushort.MaxValue.ToString(CultureInfo.InvariantCulture) };
                return true;
            }
            if (t == typeof(uint))
            {
                schema = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32", Minimum = 0.ToString(CultureInfo.InvariantCulture), Maximum = uint.MaxValue.ToString(CultureInfo.InvariantCulture) };
                return true;
            }
            if (t == typeof(ulong))
            {
                schema = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int64", Minimum = 0.ToString(CultureInfo.InvariantCulture), Maximum = ulong.MaxValue.ToString(CultureInfo.InvariantCulture) };
                return true;
            }

            if (t.IsEnum)
            {
                var names = Enum.GetNames(t).Select(n => (JsonNode)JsonValue.Create(n)!).ToList();
                var descriptions = t.GetFields(BindingFlags.Public | BindingFlags.Static)
                    .Select(field => (field.Name, Description: field.GetCustomAttribute<DescriptionAttribute>()?.Description))
                    .Where(field => !string.IsNullOrWhiteSpace(field.Description))
                    .Select(field => $"- {field.Name}: {field.Description}");
                var description = string.Join("\n\n", new[]
                {
                    t.GetCustomAttribute<DescriptionAttribute>()?.Description,
                    string.Join("\n", descriptions)
                }.Where(value => !string.IsNullOrWhiteSpace(value)));
                schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Enum = names,
                    Description = description.Length == 0 ? null : description
                };
                return true;
            }

            schema = default!;
            return false;
        }

        static bool IsEnumerable(Type t, out Type elem)
        {
            if (t == typeof(string))
            {
                elem = null!;
                return false;
            }
            if (t.IsArray)
            {
                elem = t.GetElementType()!;
                return true;
            }

            var ienum = t.GetInterfaces().Concat([t])
                .FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            if (ienum != null)
            {
                elem = ienum.GetGenericArguments()[0];
                return true;
            }
            elem = null!;
            return false;
        }

        private static bool IsDictionary(Type t, out Type valueType)
        {
            valueType = null!;
            var idict = t.GetInterfaces().Concat([t])
                .FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IDictionary<,>));
            if (idict != null && idict.GetGenericArguments()[0] == typeof(string))
            {
                valueType = idict.GetGenericArguments()[1];
                return true;
            }
            return false;
        }
    }
}
