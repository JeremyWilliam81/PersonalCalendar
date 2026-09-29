using System.Net;
using System.Text.Json;
using static PersonalCalendar.Api.Tests.ApiTestHelpers;

namespace PersonalCalendar.Api.Tests;

public sealed class GetEventEndpointTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public GetEventEndpointTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Get_ExistingEvent_Returns200WithContractBody()
    {
        var created = await _client.CreateAsync(TimedInput(location: "Main St Clinic"));
        var id = created.GetProperty("id").GetString();

        var response = await _client.GetAsync($"/api/events/{id}?timeZone={Chicago}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal(id, body.GetProperty("id").GetString());
        Assert.Equal("Main St Clinic", body.GetProperty("location").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("notes").ValueKind);
        Assert.Equal("2026-10-14T09:00:00-05:00", body.GetProperty("start").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("startDate").ValueKind);
        Assert.Equal(1, body.GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task Get_InAnotherZone_ReturnsSameMomentInThatZone()
    {
        var id = (await _client.CreateAsync(TimedInput())).GetProperty("id").GetString();

        var body = await (await _client.GetAsync($"/api/events/{id}?timeZone=America/New_York")).ReadJsonAsync();

        Assert.Equal("2026-10-14T10:00:00-04:00", body.GetProperty("start").GetString());
    }

    [Fact]
    public async Task Get_UnknownId_Returns404()
    {
        var response = await _client.GetAsync($"/api/events/{Guid.NewGuid()}?timeZone={Chicago}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_MissingTimeZone_Returns400()
    {
        var id = (await _client.CreateAsync(TimedInput())).GetProperty("id").GetString();

        var response = await _client.GetAsync($"/api/events/{id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
