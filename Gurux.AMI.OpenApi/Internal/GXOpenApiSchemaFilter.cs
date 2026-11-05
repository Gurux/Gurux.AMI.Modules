// --------------------------------------------------------------------------
// Gurux Ltd
//
// Copyright (c) Gurux Ltd.
//
// This file is part of Gurux Device Framework.
//
// Commit: $Format:%H$
// Date:   $Format:%cI$
//
// This software is available under either of the following licenses:
//
// 1. GNU General Public License, version 2 only (GPL-2.0-only).
//    See the LICENSE file included with this distribution.
//
// 2. A commercial license granted by Gurux Ltd under a separate written
//    agreement. Use under this option requires a valid commercial license
//    and is governed by the terms of that agreement.
//
// If you have not obtained a commercial license from Gurux Ltd,
// use of this software is governed by GPL-2.0-only.
//
// Commercial licensing: https://www.gurux.fi/
//
// SPDX-License-Identifier: GPL-2.0-only OR LicenseRef-Gurux-Commercial
// --------------------------------------------------------------------------

using Microsoft.OpenApi;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Gurux.DLMS.AMI.Module;
using System.Reflection;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;

namespace Gurux.AMI.OpenApi.Internal
{
    internal static class GXOpenApiSchemaFilter
    {
        private static readonly ConditionalWeakTable<Assembly, IReadOnlyDictionary<string, XElement>> XmlDocuments = new();

        internal static IResult MakeOpenApiDocument(IEnumerable<Endpoint> endpoints)
            => Results.Text(GenerateDocument(endpoints), "application/json");

        internal static string GenerateDocument(IEnumerable<Endpoint> endpoints)
        {
            var doc = new OpenApiDocument
            {
                Info = new OpenApiInfo { Title = "Gurux.DLMS.AMI API", Version = "v1" },
                Tags = new HashSet<OpenApiTag> { new() { Name = "Core" } },
                Servers = [new OpenApiServer { Url = ".." }],
                Paths = [],
                Components = new OpenApiComponents()
            };
            var builder = new GXOpenApiSchemaBuilder(doc);
            foreach (var endpoint in endpoints.OfType<RouteEndpoint>().OrderBy(e => e.RoutePattern.RawText, StringComparer.Ordinal))
            {
                if (endpoint.Metadata.GetMetadata<IExcludeFromDescriptionMetadata>()?.ExcludeFromDescription == true)
                {
                    continue;
                }
                var methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods;
                if (methods == null || string.IsNullOrWhiteSpace(endpoint.RoutePattern.RawText))
                {
                    continue;
                }
                var m = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>()?.MethodInfo ?? endpoint.Metadata.GetMetadata<MethodInfo>();
                var fullPath = "/" + endpoint.RoutePattern.RawText.TrimStart('/');
                var owner = endpoint.Metadata.GetMetadata<AmiModuleRouteOwner>()?.ModuleId;
                if (m == null && owner == null && !fullPath.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) &&
                    endpoint.Metadata.GetMetadata<IAcceptsMetadata>() == null &&
                    endpoint.Metadata.GetMetadata<IProducesResponseTypeMetadata>() == null &&
                    endpoint.Metadata.GetMetadata<IEndpointSummaryMetadata>() == null &&
                    endpoint.Metadata.GetMetadata<ITagsMetadata>() == null)
                {
                    // Razor pages and fallback UI routes do not declare an API contract.
                    continue;
                }
                fullPath = Regex.Replace(fullPath, @"\{\*{0,2}([^}:?]+)[^}]*\}", "{$1}");
                foreach (var verb in methods.Select(method => method.ToUpperInvariant()))
                {
                    var opType = new HttpMethod(verb);
                    if (!doc.Paths.TryGetValue(fullPath, out var pathItem))
                    {
                        pathItem = doc.Paths[fullPath] = new OpenApiPathItem { Operations = [] };
                    }
                    var tagNames = endpoint.Metadata.GetMetadata<ITagsMetadata>()?.Tags;
                    if (owner == null && fullPath.StartsWith("/api/extensions/", StringComparison.OrdinalIgnoreCase))
                    {
                        owner = fullPath.Split('/')[3];
                    }
                    var tags = tagNames?.Count > 0 ? tagNames : new[] { owner ?? "Core" };
                    foreach (var tag in tags)
                    {
                        if (!doc.Tags!.Any(existing => existing.Name == tag))
                        {
                            doc.Tags.Add(new OpenApiTag { Name = tag });
                        }
                    }
                    var op = new OpenApiOperation
                    {
                        Summary = endpoint.Metadata.GetMetadata<IEndpointSummaryMetadata>()?.Summary ??
                            (m == null ? endpoint.DisplayName ?? fullPath : OperationSummary(m, null)),
                        Description = endpoint.Metadata.GetMetadata<IEndpointDescriptionMetadata>()?.Description,
                        Tags = tags.Select(tag => new OpenApiTagReference(tag, doc)).ToHashSet(),
                        Responses = [],
                        Parameters = []
                    };
                    var routeParameters = ParseRouteParams(fullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var parameters = m?.GetParameters() ?? [];
                    var documentedParameters = new HashSet<ParameterInfo>();
                    foreach (var seg in routeParameters)
                    {
                        var parameter = parameters.FirstOrDefault(p => string.Equals(
                            p.GetCustomAttribute<FromRouteAttribute>()?.Name ?? p.Name, seg, StringComparison.OrdinalIgnoreCase));
                        op.Parameters.Add(parameter == null ? new OpenApiParameter
                            { Name = seg, In = ParameterLocation.Path, Required = true,
                                Schema = new OpenApiSchema { Type = JsonSchemaType.String } }
                            : BuildParameter(builder, parameter, seg, ParameterLocation.Path));
                        if (parameter != null)
                        {
                            documentedParameters.Add(parameter);
                        }
                    }

                    foreach (var parameter in parameters.Where(p => !documentedParameters.Contains(p) && !IsService(p)))
                    {
                        var query = parameter.GetCustomAttribute<FromQueryAttribute>();
                        if (query == null && ((verb != "GET" && !IsPrimitiveLike(parameter.ParameterType)) ||
                            parameter.GetCustomAttribute<FromBodyAttribute>() != null ||
                            parameter.GetCustomAttribute<FromHeaderAttribute>() != null ||
                            parameter.GetCustomAttribute<FromFormAttribute>() != null || !IsQueryType(parameter.ParameterType)))
                        {
                            continue;
                        }

                        op.Parameters.Add(BuildParameter(builder, parameter, query?.Name ?? parameter.Name!, ParameterLocation.Query));
                        documentedParameters.Add(parameter);
                    }

                    var bodyParam = parameters.FirstOrDefault(p => !documentedParameters.Contains(p) && !IsService(p) &&
                        p.GetCustomAttribute<FromQueryAttribute>() == null && p.GetCustomAttribute<FromHeaderAttribute>() == null &&
                        p.GetCustomAttribute<FromFormAttribute>() == null &&
                        (p.GetCustomAttribute<FromBodyAttribute>() != null || (verb != "GET" && !IsPrimitiveLike(p.ParameterType))));

                    var accepts = endpoint.Metadata.GetMetadata<IAcceptsMetadata>();
                    var bodyType = accepts?.RequestType ?? bodyParam?.ParameterType;
                    if (bodyType != null)
                    {
                        var schema = builder.Build(bodyType);
                        var mediaTypes = accepts?.ContentTypes.Count > 0 ? accepts.ContentTypes : new[] { "application/json" };
                        op.RequestBody = new OpenApiRequestBody
                        {
                            Required = accepts == null || !accepts.IsOptional,
                            Content = mediaTypes.ToDictionary(mediaType => mediaType,
                                _ => (IOpenApiMediaType)new OpenApiMediaType { Schema = schema })
                        };
                    }
                    var produces = endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>();
                    foreach (var response in produces)
                    {
                        AddResponse(response.StatusCode.ToString(System.Globalization.CultureInfo.InvariantCulture), response.Type, response.ContentTypes);
                    }
                    foreach (var response in endpoint.Metadata.GetOrderedMetadata<ProducesResponseTypeAttribute>())
                    {
                        AddResponse(response.StatusCode.ToString(System.Globalization.CultureInfo.InvariantCulture), response.Type, []);
                    }
                    if (op.Responses.Count == 0)
                    {
                        AddResponse("200", m == null ? null : UnwrapReturnType(m.ReturnType), []);
                    }
                    void AddResponse(string code, Type? type, IEnumerable<string> contentTypes)
                    {
                        var response = new OpenApiResponse { Description = code == "200" ? "OK" : "HTTP " + code };
                        if (type != null && type != typeof(void) && !typeof(IResult).IsAssignableFrom(type))
                        {
                            var schema = builder.Build(type);
                            var mediaTypes = contentTypes.Any() ? contentTypes : new[] { "application/json" };
                            response.Content = mediaTypes.ToDictionary(mediaType => mediaType,
                                _ => (IOpenApiMediaType)new OpenApiMediaType { Schema = schema });
                        }
                        op.Responses[code] = response;
                    }
                    var operations = pathItem.Operations
                        ?? throw new InvalidOperationException(global::Gurux.AMI.OpenApi.Properties.Resources.TheOpenAPIPathHasNoOperationCollection);
                    operations[opType] = op;
                }
            }

            using var sw = new StringWriter();
            var jw = new OpenApiJsonWriter(sw);
            doc.SerializeAsV3(jw);
            return sw.ToString();
        }

        static Type? UnwrapReturnType(Type t)
        {
            if (typeof(Task).IsAssignableFrom(t) || (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ValueTask<>)))
            {
                t = t.IsGenericType ? t.GetGenericArguments()[0] : typeof(void);
            }
            if (t.IsGenericType && t.Name.StartsWith("ActionResult"))
            {
                t = t.GetGenericArguments().FirstOrDefault() ?? typeof(void);
            }
            return t;
        }

        static IEnumerable<string> ParseRouteParams(string path)
        {
            int i = 0;
            while (i < path.Length)
            {
                var open = path.IndexOf('{', i);
                if (open < 0)
                {
                    yield break;
                }

                var close = path.IndexOf('}', open + 1);
                if (close < 0)
                {
                    yield break;
                }

                yield return path.Substring(open + 1, close - open - 1).Split(':')[0];
                i = close + 1;
            }
        }

        static bool IsPrimitiveLike(Type t) => GXOpenApiSchemaBuilder.TryMapSimple(t, out _);

        private static bool IsQueryType(Type type) => IsPrimitiveLike(type) ||
            (type.IsArray && IsPrimitiveLike(type.GetElementType()!)) ||
            type.GetInterfaces().Append(type).Any(t => t.IsGenericType &&
                t.GetGenericTypeDefinition() == typeof(IEnumerable<>) && IsPrimitiveLike(t.GetGenericArguments()[0]));

        private static bool IsService(ParameterInfo parameter) => parameter.ParameterType == typeof(CancellationToken) ||
            parameter.ParameterType == typeof(HttpContext) || parameter.ParameterType == typeof(HttpRequest) ||
            parameter.ParameterType == typeof(HttpResponse) || parameter.ParameterType == typeof(IServiceProvider) ||
            parameter.ParameterType == typeof(System.Security.Claims.ClaimsPrincipal) || parameter.ParameterType == typeof(AmiModuleRequest) ||
            parameter.GetCustomAttribute<FromServicesAttribute>() != null;

        private static OpenApiParameter BuildParameter(GXOpenApiSchemaBuilder builder, ParameterInfo parameter,
            string name, ParameterLocation location)
        {
            var schema = builder.Build(parameter.ParameterType);
            if (schema is OpenApiSchema inline)
            {
                GXOpenApiSchemaBuilder.ApplyValidation(inline, parameter.GetCustomAttributes());
                if (parameter.HasDefaultValue && parameter.DefaultValue is { } value && value != DBNull.Value && value != Missing.Value)
                {
                    inline.Default = parameter.ParameterType.IsEnum ? JsonValue.Create(value.ToString()) :
                        JsonSerializer.SerializeToNode(value);
                }
            }
            var required = parameter.GetCustomAttribute<RequiredAttribute>() != null ||
                parameter.GetCustomAttribute<Microsoft.AspNetCore.Mvc.ModelBinding.BindRequiredAttribute>() != null ||
                (!parameter.HasDefaultValue && new NullabilityInfoContext().Create(parameter).ReadState == NullabilityState.NotNull);
            return new OpenApiParameter
            {
                Name = name, In = location, Required = location == ParameterLocation.Path || required,
                Description = XmlText(XmlMember(parameter.Member as MethodInfo)?.Elements("param")
                    .FirstOrDefault(p => p.Attribute("name")?.Value == parameter.Name)),
                Schema = schema
            };
        }

        private static string OperationSummary(MethodInfo method, string? template)
        {
            var summary = method.GetCustomAttribute<Microsoft.AspNetCore.Http.EndpointSummaryAttribute>()?.Summary ??
                method.GetCustomAttribute<DescriptionAttribute>()?.Description ?? XmlText(XmlMember(method)?.Element("summary"));
            if (!string.IsNullOrWhiteSpace(summary))
            {
                return summary;
            }

            var action = string.IsNullOrWhiteSpace(template) ? method.Name : template.Split('/')[0];
            var resource = method.DeclaringType!.Name.Replace("Controller", "");
            return Regex.Replace(action + " " + resource, @"(?<=[a-z0-9])(?=[A-Z])", " ");
        }

        private static XElement? XmlMember(MethodInfo? method)
        {
            if (method == null)
            {
                return null;
            }

            var members = XmlDocuments.GetValue(method.Module.Assembly, assembly =>
            {
                var path = assembly.IsDynamic || string.IsNullOrEmpty(assembly.Location) ? null : Path.ChangeExtension(assembly.Location, ".xml");
                if (path == null)
                {
                    return new Dictionary<string, XElement>();
                }
                try
                {
                    return XDocument.Load(path).Descendants("member").Where(member => member.Attribute("name") != null).GroupBy(member => member.Attribute("name")!.Value).ToDictionary(group => group.Key, group => group.First());
                }
                catch (IOException)
                {
                    return new Dictionary<string, XElement>();
                }
                catch (System.Xml.XmlException)
                {
                    return new Dictionary<string, XElement>();
                }
            });
            var parameters = method.GetParameters();
            var key = "M:" + method.DeclaringType!.FullName!.Replace('+', '.') + "." + method.Name;
            if (parameters.Length != 0)
            {
                key += "(" + string.Join(",", parameters.Select(p => XmlTypeName(p.ParameterType))) + ")";
            }

            return members.GetValueOrDefault(key);
        }

        private static string XmlTypeName(Type type) => type.IsArray ? XmlTypeName(type.GetElementType()!) + "[]" :
            type.IsGenericType ? type.GetGenericTypeDefinition().FullName!.Split('`')[0].Replace('+', '.') +
                "{" + string.Join(",", type.GetGenericArguments().Select(XmlTypeName)) + "}" : type.FullName!.Replace('+', '.');

        private static string? XmlText(XElement? element) => element == null ? null : Regex.Replace(element.Value, @"\s+", " ").Trim();
    }
}
