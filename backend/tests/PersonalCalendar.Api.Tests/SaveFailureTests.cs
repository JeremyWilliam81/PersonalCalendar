using System.Net;
using System.Net.Http.Json;
using static PersonalCalendar.Api.Tests.ApiTestHelpers;

namespace PersonalCalendar.Api.Tests;

public sealed class SaveFailureTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose()
    {
        if (File.Exists(_factory.DatabasePath)) File.SetAttributes(_factory.DatabasePath, FileAttributes.Normal);
        _factory.Dispose();
    }

    [Fact]
    public async Task Post_WhenDatabaseCannotBeWritten_Returns500SaveFailed_AndStoresNothing()
    {
        var client = _factory.CreateClient(); // creates the database
        File.SetAttributes(_factory.DatabasePath, FileAttributes.ReadOnly);

        var response = await client.PostAsJsonAsync("/api/events", TimedInput());

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("save-failed", (await response.ReadJsonAsync()).GetProperty("type").GetString());

        File.SetAttributes(_factory.DatabasePath, FileAttributes.Normal);
        Assert.Empty((await client.GetMonthAsync(2026, 10)).DaysWithTitle("Dentist"));
    }
}
