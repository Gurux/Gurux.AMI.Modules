using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Primitives;

namespace Gurux.AMI.Swagger;

/// <summary>Owns Swagger UI and its embedded assets without registering host middleware.</summary>
public sealed class GXSwaggerEndpointSource : EndpointDataSource
{
    private IReadOnlyList<Endpoint> endpoints = [];

    /// <summary>Initializes the swagger endpoint source data.</summary>
    /// <param name="hostServices">The host services used by this operation.</param>
    public void Initialize(IServiceProvider hostServices)
    {
        var application = new ApplicationBuilder(hostServices);
        application.UseSwaggerUI(options =>
        {
            options.RoutePrefix = "swagger";
            options.SwaggerEndpoint("../openapi/v1.json", "Gurux.DLMS.AMI API v1");
            options.ConfigObject.AdditionalItems["tagsSorter"] = "alpha";
            options.ConfigObject.AdditionalItems["operationsSorter"] = "alpha";
        });
        var endpoint = new RouteEndpointBuilder(application.Build(), RoutePatternFactory.Parse("/swagger/{**asset}"), 0)
        {
            DisplayName = global::Gurux.AMI.Swagger.Properties.Resources.Swagger
        };
        endpoint.Metadata.Add(new HttpMethodMetadata(["GET", "HEAD"]));
        endpoint.Metadata.Add(new ExcludeFromDescriptionAttribute());
        endpoints = [endpoint.Build()];
    }

    /// <inheritdoc />
    public override IReadOnlyList<Endpoint> Endpoints => endpoints;
    /// <inheritdoc />
    public override IChangeToken GetChangeToken() => new CancellationChangeToken(CancellationToken.None);
}
