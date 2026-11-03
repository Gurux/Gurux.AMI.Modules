using Gurux.DLMS.AMI.Module;
using Gurux.DLMS.AMI.Module.Enums;
using Gurux.DLMS.AMI.Shared.DTOs.Module;
using Microsoft.Extensions.DependencyInjection;

namespace Gurux.AMI.Swagger;

/// <summary>Provides Swagger UI using the document owned by the OpenApi module.</summary>
public sealed class GXSwaggerModule : GXAmiModuleBase
{
    /// <inheritdoc />
    public override string Id => "Gurux.AMI.Swagger";

    /// <inheritdoc />
    public override string Name => Properties.Resources.Swagger;

    /// <inheritdoc />
    public override string Description => Properties.Resources.InteractiveSwaggerAPIDocumentation;
    /// <inheritdoc />
    public override IReadOnlyList<AmiModuleDependency> Dependencies => [new(AmiModuleDependencyKind.Module, "Gurux.AMI.OpenApi")];

    /// <inheritdoc />
    public override string Icon => Properties.Resources.SwaggerIcon;

    /// <inheritdoc />
    public override void Configure(AmiModuleBuilder builder)
    {
        builder.Services.AddSingleton<GXSwaggerEndpointSource>();
        builder.AddEndpointSource<GXSwaggerEndpointSource>();
    }

    /// <inheritdoc />
    public override Task StartAsync(AmiModuleContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        context.Services.GetRequiredService<GXSwaggerEndpointSource>().Initialize(context.HostServices);
        return Task.CompletedTask;
    }
}

