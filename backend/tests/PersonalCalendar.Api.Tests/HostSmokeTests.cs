using System.Net;

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
}
