using System.Net;
using System.Net.Http.Json;
using static PersonalCalendar.Api.Tests.ApiTestHelpers;

namespace PersonalCalendar.Api.Tests;

public sealed class UpdateDeleteEndpointTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public UpdateDeleteEndpointTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    private async Task<string> CreateDentistAsync() =>
        (await _client.CreateAsync(TimedInput())).GetProperty("id").GetString()!;

    [Fact]
    public async Task Put_MovesEventToNewDay_AndIncrementsVersion()
    {
        var id = await CreateDentistAsync();

        var response = await _client.PutAsJsonAsync($"/api/events/{id}",
            TimedInput(start: "2026-10-15T09:00", end: "2026-10-15T10:00", version: 1));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, (await response.ReadJsonAsync()).GetProperty("version").GetInt32());
        Assert.Equal(["2026-10-15"], (await _client.GetMonthAsync(2026, 10)).DaysWithTitle("Dentist"));
    }

    [Fact]
    public async Task Put_StaleVersion_Returns409()
    {
        var id = await CreateDentistAsync();
        await _client.PutAsJsonAsync($"/api/events/{id}", TimedInput(title: "First edit", version: 1));

        var response = await _client.PutAsJsonAsync($"/api/events/{id}", TimedInput(title: "Second edit", version: 1));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("concurrency-conflict", (await response.ReadJsonAsync()).GetProperty("type").GetString());
    }

    [Fact]
    public async Task Put_EndNotAfterStart_Returns400_AndKeepsOldValues()
    {
        var id = await CreateDentistAsync();

        var response = await _client.PutAsJsonAsync($"/api/events/{id}",
            TimedInput(start: "2026-10-15T09:00", end: "2026-10-15T08:00", version: 1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var stored = await (await _client.GetAsync($"/api/events/{id}?timeZone={Chicago}")).ReadJsonAsync();
        Assert.Equal("2026-10-14T09:00:00-05:00", stored.GetProperty("start").GetString());
        Assert.Equal(1, stored.GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task Put_MissingVersion_Returns400()
    {
        var id = await CreateDentistAsync();

        var response = await _client.PutAsJsonAsync($"/api/events/{id}", TimedInput());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_UnknownId_Returns404()
    {
        var response = await _client.PutAsJsonAsync($"/api/events/{Guid.NewGuid()}", TimedInput(version: 1));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_RemovesEventPermanently()
    {
        var id = await CreateDentistAsync();

        var response = await _client.DeleteAsync($"/api/events/{id}?version=1");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var restarted = _factory.CreateRestartedFactory();
        var afterRestart = await restarted.CreateClient().GetAsync($"/api/events/{id}?timeZone={Chicago}");
        Assert.Equal(HttpStatusCode.NotFound, afterRestart.StatusCode);
    }

    [Fact]
    public async Task Delete_StaleVersion_Returns409_AndKeepsEvent()
    {
        var id = await CreateDentistAsync();

        var response = await _client.DeleteAsync($"/api/events/{id}?version=5");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"/api/events/{id}?timeZone={Chicago}")).StatusCode);
    }

    [Fact]
    public async Task Delete_UnknownIdOrMissingVersion_Returns404Or400()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync($"/api/events/{Guid.NewGuid()}?version=1")).StatusCode);

        var id = await CreateDentistAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.DeleteAsync($"/api/events/{id}")).StatusCode);
    }
}
