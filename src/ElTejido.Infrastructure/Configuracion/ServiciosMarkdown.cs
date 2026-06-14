using Azure.Identity;
using Azure.Storage.Blobs;
using ElTejido.Application.Markdown;
using ElTejido.Infrastructure.Blob;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ElTejido.Infrastructure.Configuracion;

/// <summary>
/// Registra la generacion de Markdown (09). El almacen de blobs usa Azure Blob Storage si hay
/// <c>Blob:AccountUrl</c> (Managed Identity); en caso contrario un fallback in-process (local/CI).
/// El compilador depende de los repositorios Cosmos (Fase 1/7) y se gatilla con la presencia de
/// <c>Cosmos:AccountEndpoint</c> (registro guardado).
/// </summary>
public static class ServiciosMarkdown
{
    public static IServiceCollection AgregarMarkdown(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);

        var accountUrl = configuration["Blob:AccountUrl"];
        if (string.IsNullOrWhiteSpace(accountUrl))
        {
            services.AddSingleton<IAlmacenBlob, AlmacenBlobMemoria>();
        }
        else
        {
            var contenedorNombre = configuration["Blob:ContainerName"] ?? "markdown";
            var servicio = new BlobServiceClient(new Uri(accountUrl), new DefaultAzureCredential());
            var contenedor = servicio.GetBlobContainerClient(contenedorNombre);
            services.AddSingleton<IAlmacenBlob>(new AlmacenBlobAzure(contenedor));
        }

        if (!string.IsNullOrWhiteSpace(configuration["Cosmos:AccountEndpoint"]))
        {
            services.AddScoped<ICompiladorMarkdown, CompiladorMarkdown>();
        }

        return services;
    }
}
