using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace PersonalCalendar.Api.Tests;

public class HostSmokeTests
{
    [Fact]
    public async Task Startup_CreatesDatabase_AndUnknownApiRoutesReturn404()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.True(File.Exists(factory.DatabasePath));
    }

    /// <summary>Client routes such as /week/2026-10-14 are served the SPA (002 contracts/http-api.md "Client routes").</summary>
    [Theory]
    [InlineData("/week/2026-10-14")]
    [InlineData("/day")]
    [InlineData("/fortnight/nope")]
    public async Task ClientRoutes_ServeTheSpa(string path)
    {
        // A private web root keeps this test independent of a frontend build (wwwroot is build output).
        var webRoot = Directory.CreateTempSubdirectory("personal-calendar-webroot-").FullName;
        await File.WriteAllTextAsync(Path.Combine(webRoot, "index.html"), "<!doctype html><title>spa</title>");
        try
        {
            using var factory = new ApiFactory();
            using var spa = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
                services.Configure<StaticFileOptions>(options => options.FileProvider = new PhysicalFileProvider(webRoot))));
            var client = spa.CreateClient();

            var page = await client.GetAsync(path);
            var api = await client.GetAsync("/api/nope");

            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            Assert.Equal("text/html", page.Content.Headers.ContentType?.MediaType);
            Assert.Contains("<title>spa</title>", await page.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.NotFound, api.StatusCode);
        }
        finally
        {
            Directory.Delete(webRoot, recursive: true);
        }
    }
}
