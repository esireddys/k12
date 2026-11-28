using Microsoft.Net.Http.Headers;

namespace K12.Api.Hosting;

/// <summary>
/// Serves the built React app (web/dist, copied to wwwroot in the container image) from the API's own address, so the
/// app and the API share one origin: the session cookie stays first-party (Safari on iPad blocks third-party cookies)
/// and no CORS is needed. Local runs have no wwwroot and keep using the Vite dev server.
/// </summary>
public static class WebAppHosting
{
    /// <summary>True when a built web app is present to serve.</summary>
    public static bool HasWebApp(this IWebHostEnvironment environment) =>
        environment.WebRootFileProvider.GetFileInfo("index.html").Exists;

    public static WebApplication UseK12WebApp(this WebApplication app)
    {
        if (!app.Environment.HasWebApp()) return app;

        app.UseDefaultFiles();
        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = context =>
            {
                // Vite puts a content hash in every file name under /assets, so those never change; everything else
                // (index.html, the manifest) is checked on each visit so a deployment shows up at once.
                var immutable = context.Context.Request.Path.StartsWithSegments("/assets");
                context.Context.Response.Headers[HeaderNames.CacheControl] =
                    immutable ? "public, max-age=31536000, immutable" : "no-cache";
            },
        });
        return app;
    }

    /// <summary>
    /// Sends every other browser route (for example /kids or /parent) to index.html so the React router can show it.
    /// Unknown /api paths still answer 404 rather than the app's page. Call after all endpoints are mapped.
    /// </summary>
    public static WebApplication MapK12WebAppFallback(this WebApplication app)
    {
        if (!app.Environment.HasWebApp()) return app;

        app.Map("/api/{**rest}", () => Results.NotFound()).ExcludeFromDescription();
        app.MapFallbackToFile("index.html", new StaticFileOptions
        {
            OnPrepareResponse = context => context.Context.Response.Headers[HeaderNames.CacheControl] = "no-cache",
        });
        return app;
    }
}
