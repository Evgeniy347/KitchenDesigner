using Microsoft.Extensions.FileProviders;

namespace KitchenServer.Web.Services;

/// <summary>
/// Serves a Unity WebGL build (configured via WebGL:RootPath) under /unity.
/// Unity release builds ship pre-compressed files (*.wasm.gz etc.) that must be
/// served with the inner content type plus Content-Encoding, or the loader fails.
/// </summary>
public static class UnityWebGLStaticFiles
{
    /// <returns>(contentType, encoding). Encoding is null for uncompressed files.</returns>
    public static (string ContentType, string? Encoding) GetContentType(string fileName)
    {
        string? encoding = null;
        var name = fileName;
        if (name.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
        {
            encoding = "gzip";
            name = name[..^3];
        }
        else if (name.EndsWith(".br", StringComparison.OrdinalIgnoreCase))
        {
            encoding = "br";
            name = name[..^3];
        }

        var contentType = Path.GetExtension(name).ToLowerInvariant() switch
        {
            ".wasm" => "application/wasm",
            ".js" => "application/javascript",
            ".data" => "application/octet-stream",
            ".json" => "application/json",
            ".html" => "text/html",
            ".css" => "text/css",
            ".png" => "image/png",
            ".ico" => "image/x-icon",
            _ => "application/octet-stream"
        };
        return (contentType, encoding);
    }

    public static void Map(WebApplication app)
    {
        var root = app.Configuration["WebGL:RootPath"];
        if (string.IsNullOrEmpty(root))
            return;

        var fullPath = Path.GetFullPath(root);
        if (!Directory.Exists(fullPath))
        {
            app.Logger.LogWarning("WebGL:RootPath {Path} does not exist — /unity will not be served", fullPath);
            return;
        }

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(fullPath),
            RequestPath = "/unity",
            ServeUnknownFileTypes = true,
            OnPrepareResponse = ctx =>
            {
                var (contentType, encoding) = GetContentType(ctx.File.Name);
                ctx.Context.Response.ContentType = contentType;
                if (encoding != null)
                    ctx.Context.Response.Headers.ContentEncoding = encoding;
                // Debug iteration: a rebuilt wasm must not be shadowed by browser cache.
                ctx.Context.Response.Headers.CacheControl = "no-cache";
            }
        });

        app.Logger.LogInformation("Serving Unity WebGL build from {Path} at /unity", fullPath);
    }
}
