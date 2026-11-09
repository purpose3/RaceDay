using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RaceDay.Tests.Infrastructure;

namespace RaceDay.Tests;

public class OrganiserTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;
    public OrganiserTests(RaceDayFactory factory) => _factory = factory;

    private static async Task<int> EnrolAsync(TestUser participant, int categoryId)
    {
        var res = await participant.Client.PostAsJsonAsync($"/api/categories/{categoryId}/enrollments", new { });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("enrolmentId").GetInt32();
    }

    // ---------- Dashboard ----------

    [Fact]
    public async Task Dashboard_ShowsTotalsAndPerEventEnrolments()
    {
        var org = await TestApi.CreateUserAsync(_factory, "Organiser");
        var event1 = await TestApi.CreateEventAsync(org.Client);
        var event2 = await TestApi.CreateEventAsync(org.Client);
        var category1 = await TestApi.CreateCategoryAsync(org.Client, event1);
        await TestApi.CreateCategoryAsync(org.Client, event2);
        await EnrolAsync(await TestApi.CreateUserAsync(_factory), category1);
        await EnrolAsync(await TestApi.CreateUserAsync(_factory), category1);

        var json = await org.Client.GetFromJsonAsync<JsonElement>("/api/organiser/dashboard");

        Assert.Equal(2, json.GetProperty("totalEvents").GetInt32());
        Assert.Equal(2, json.GetProperty("upcomingEvents").GetInt32());
        Assert.Equal(2, json.GetProperty("totalEnrolments").GetInt32());
        Assert.False(string.IsNullOrEmpty(json.GetProperty("nextEventName").GetString()));

        var row = json.GetProperty("events").EnumerateArray().Single(e => e.GetProperty("eventId").GetInt32() == event1);
        Assert.Equal(2, row.GetProperty("totalEnrolments").GetInt32());
        Assert.Equal(1, row.GetProperty("categoryCount").GetInt32());
    }

    [Fact]
    public async Task Dashboard_OnlyIncludesTheOrganisersOwnEvents()
    {
        var mine = await TestApi.CreateUserAsync(_factory, "Organiser");
        var theirs = await TestApi.CreateUserAsync(_factory, "Organiser");
        await TestApi.CreateEventAsync(mine.Client);
        await TestApi.CreateEventAsync(theirs.Client);
        await TestApi.CreateEventAsync(theirs.Client);

        var json = await mine.Client.GetFromJsonAsync<JsonElement>("/api/organiser/dashboard");

        Assert.Equal(1, json.GetProperty("totalEvents").GetInt32());
    }

    [Fact]
    public async Task Dashboard_ForNewOrganiser_IsEmptyNotAnError()
    {
        var org = await TestApi.CreateUserAsync(_factory, "Organiser");

        var json = await org.Client.GetFromJsonAsync<JsonElement>("/api/organiser/dashboard");

        Assert.Equal(0, json.GetProperty("totalEvents").GetInt32());
        Assert.Equal(0, json.GetProperty("events").GetArrayLength());
    }

    [Fact]
    public async Task Dashboard_AsParticipant_Returns403()
    {
        var participant = await TestApi.CreateUserAsync(_factory);
        var res = await participant.Client.GetAsync("/api/organiser/dashboard");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Dashboard_Unauthenticated_Returns401()
    {
        var res = await _factory.CreateClient().GetAsync("/api/organiser/dashboard");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // ---------- Enrolments across an event ----------

    [Fact]
    public async Task EventEnrolments_ListsEveryCategoryWithParticipantAndStatus()
    {
        var org = await TestApi.CreateUserAsync(_factory, "Organiser");
        var eventId = await TestApi.CreateEventAsync(org.Client);
        var cat1 = await TestApi.CreateCategoryAsync(org.Client, eventId);
        var cat2 = await TestApi.CreateCategoryAsync(org.Client, eventId);
        var p1 = await TestApi.CreateUserAsync(_factory);
        var p2 = await TestApi.CreateUserAsync(_factory);
        await EnrolAsync(p1, cat1);
        await EnrolAsync(p2, cat2);

        var res = await org.Client.GetAsync($"/api/events/{eventId}/enrollments");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, json.GetArrayLength());
        Assert.Equal("Pending", json[0].GetProperty("status").GetString());
        Assert.False(string.IsNullOrEmpty(json[0].GetProperty("categoryName").GetString()));
        var emails = json.EnumerateArray().Select(e => e.GetProperty("participant").GetProperty("email").GetString()).ToList();
        Assert.Contains(p1.Email, emails);
        Assert.Contains(p2.Email, emails);
    }

    [Fact]
    public async Task EventEnrolments_ByDifferentOrganiser_Returns403()
    {
        var owner = await TestApi.CreateUserAsync(_factory, "Organiser");
        var other = await TestApi.CreateUserAsync(_factory, "Organiser");
        var eventId = await TestApi.CreateEventAsync(owner.Client);

        var res = await other.Client.GetAsync($"/api/events/{eventId}/enrollments");

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task EventEnrolments_ByParticipant_Returns403()
    {
        var org = await TestApi.CreateUserAsync(_factory, "Organiser");
        var participant = await TestApi.CreateUserAsync(_factory);
        var eventId = await TestApi.CreateEventAsync(org.Client);

        var res = await participant.Client.GetAsync($"/api/events/{eventId}/enrollments");

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task EventEnrolments_UnknownEvent_Returns404()
    {
        var org = await TestApi.CreateUserAsync(_factory, "Organiser");
        var res = await org.Client.GetAsync("/api/events/999999/enrollments");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // ---------- Pending / Confirmed status ----------

    [Fact]
    public async Task SetStatus_ByOwner_ConfirmsEnrolment()
    {
        var org = await TestApi.CreateUserAsync(_factory, "Organiser");
        var eventId = await TestApi.CreateEventAsync(org.Client);
        var categoryId = await TestApi.CreateCategoryAsync(org.Client, eventId);
        var participant = await TestApi.CreateUserAsync(_factory);
        var enrolmentId = await EnrolAsync(participant, categoryId);

        var res = await org.Client.PutAsJsonAsync($"/api/enrollments/{enrolmentId}/status", new { status = "confirmed" });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Confirmed", json.GetProperty("status").GetString());

        var mine = await participant.Client.GetFromJsonAsync<JsonElement>("/api/users/me/enrollments");
        Assert.Equal("Confirmed", mine[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task SetStatus_ByParticipant_Returns403()
    {
        var org = await TestApi.CreateUserAsync(_factory, "Organiser");
        var eventId = await TestApi.CreateEventAsync(org.Client);
        var categoryId = await TestApi.CreateCategoryAsync(org.Client, eventId);
        var participant = await TestApi.CreateUserAsync(_factory);
        var enrolmentId = await EnrolAsync(participant, categoryId);

        var res = await participant.Client.PutAsJsonAsync($"/api/enrollments/{enrolmentId}/status", new { status = "Confirmed" });

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task SetStatus_ByDifferentOrganiser_Returns403()
    {
        var owner = await TestApi.CreateUserAsync(_factory, "Organiser");
        var other = await TestApi.CreateUserAsync(_factory, "Organiser");
        var eventId = await TestApi.CreateEventAsync(owner.Client);
        var categoryId = await TestApi.CreateCategoryAsync(owner.Client, eventId);
        var enrolmentId = await EnrolAsync(await TestApi.CreateUserAsync(_factory), categoryId);

        var res = await other.Client.PutAsJsonAsync($"/api/enrollments/{enrolmentId}/status", new { status = "Confirmed" });

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task SetStatus_InvalidValue_Returns400()
    {
        var org = await TestApi.CreateUserAsync(_factory, "Organiser");
        var eventId = await TestApi.CreateEventAsync(org.Client);
        var categoryId = await TestApi.CreateCategoryAsync(org.Client, eventId);
        var enrolmentId = await EnrolAsync(await TestApi.CreateUserAsync(_factory), categoryId);

        var res = await org.Client.PutAsJsonAsync($"/api/enrollments/{enrolmentId}/status", new { status = "Banana" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task SetStatus_UnknownEnrolment_Returns404()
    {
        var org = await TestApi.CreateUserAsync(_factory, "Organiser");
        var res = await org.Client.PutAsJsonAsync("/api/enrollments/999999/status", new { status = "Confirmed" });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
