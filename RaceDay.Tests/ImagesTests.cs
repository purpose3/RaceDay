using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using RaceDay.Tests.Infrastructure;

namespace RaceDay.Tests;

public class ImagesTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;
    public ImagesTests(RaceDayFactory factory) => _factory = factory;

    // PNG signature followed by a few bytes: enough to pass the real-file-type check
    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4, 5, 6, 7, 8 };

    private static MultipartFormDataContent FileContent(byte[] bytes, string contentType = "image/png", string fileName = "image.png")
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(file, "file", fileName);
        return content;
    }

    private int BlobCount(string prefix) => _factory.BlobStorage.Blobs.Keys.Count(k => k.StartsWith(prefix));

    // ---------- Event banners ----------

    [Fact]
    public async Task UploadBanner_ByOwner_Returns200_AndBannerIsServedAnonymously()
    {
        var org = await TestApi.CreateUserAsync(_factory, "Organiser");
        var eventId = await TestApi.CreateEventAsync(org.Client);

        var upload = await org.Client.PostAsync($"/api/events/{eventId}/banner", FileContent(Png));

        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var json = await upload.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal($"/api/events/{eventId}/banner", json.GetProperty("imageUrl").GetString());

        var get = await _factory.CreateClient().GetAsync($"/api/events/{eventId}/banner");   // no token
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal("image/png", get.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Png, await get.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task EventDetail_IncludesBannerUrl_AfterUpload()
    {
        var org = await TestApi.CreateUserAsync(_factory, "Organiser");
        var eventId = await TestApi.CreateEventAsync(org.Client);
        await org.Client.PostAsync($"/api/events/{eventId}/banner", FileContent(Png));

        var json = await _factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/events/{eventId}");

        Assert.Equal($"/api/events/{eventId}/banner", json.GetProperty("bannerUrl").GetString());
    }

    [Fact]
    public async Task GetBanner_WhenNoneUploaded_Returns404()
    {
        var org = await TestApi.CreateUserAsync(_factory, "Organiser");
        var eventId = await TestApi.CreateEventAsync(org.Client);

        var res = await _factory.CreateClient().GetAsync($"/api/events/{eventId}/banner");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_ByDifferentOrganiser_Returns403()
    {
        var owner = await TestApi.CreateUserAsync(_factory, "Organiser");
        var other = await TestApi.CreateUserAsync(_factory, "Organiser");
        var eventId = await TestApi.CreateEventAsync(owner.Client);

        var res = await other.Client.PostAsync($"/api/events/{eventId}/banner", FileContent(Png));

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_ByParticipant_Returns403()
    {
        var org = await TestApi.CreateUserAsync(_factory, "Organiser");
        var participant = await TestApi.CreateUserAsync(_factory);
        var eventId = await TestApi.CreateEventAsync(org.Client);

        var res = await participant.Client.PostAsync($"/api/events/{eventId}/banner", FileContent(Png));

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_Unauthenticated_Returns401()
    {
        var org = await TestApi.CreateUserAsync(_factory, "Organiser");
        var eventId = await TestApi.CreateEventAsync(org.Client);

        var res = await _factory.CreateClient().PostAsync($"/api/events/{eventId}/banner", FileContent(Png));

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_UnknownEvent_Returns404()
    {
        var org = await TestApi.CreateUserAsync(_factory, "Organiser");
        var res = await org.Client.PostAsync("/api/events/999999/banner", FileContent(Png));
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_NotAnImage_Returns400_EvenIfLabelledAsPng()
    {
        var org = await TestApi.CreateUserAsync(_factory, "Organiser");
        var eventId = await TestApi.CreateEventAsync(org.Client);
        var notAnImage = Encoding.UTF8.GetBytes("this is definitely not an image file");

        var res = await org.Client.PostAsync($"/api/events/{eventId}/banner", FileContent(notAnImage, "image/png", "fake.png"));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_TooLarge_Returns400()
    {
        var org = await TestApi.CreateUserAsync(_factory, "Organiser");
        var eventId = await TestApi.CreateEventAsync(org.Client);
        var big = new byte[5 * 1024 * 1024 + 1];
        Array.Copy(Png, big, Png.Length);

        var res = await org.Client.PostAsync($"/api/events/{eventId}/banner", FileContent(big));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_NoFile_Returns400()
    {
        var org = await TestApi.CreateUserAsync(_factory, "Organiser");
        var eventId = await TestApi.CreateEventAsync(org.Client);

        var res = await org.Client.PostAsync($"/api/events/{eventId}/banner", new MultipartFormDataContent());

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_Twice_ReplacesTheOldImage()
    {
        var org = await TestApi.CreateUserAsync(_factory, "Organiser");
        var eventId = await TestApi.CreateEventAsync(org.Client);

        await org.Client.PostAsync($"/api/events/{eventId}/banner", FileContent(Png));
        await org.Client.PostAsync($"/api/events/{eventId}/banner", FileContent(Png));

        Assert.Equal(1, BlobCount($"events/{eventId}/"));   // old blob was deleted
    }

    [Fact]
    public async Task DeleteEvent_RemovesItsBannerFromStorage()
    {
        var org = await TestApi.CreateUserAsync(_factory, "Organiser");
        var eventId = await TestApi.CreateEventAsync(org.Client);
        await org.Client.PostAsync($"/api/events/{eventId}/banner", FileContent(Png));
        Assert.Equal(1, BlobCount($"events/{eventId}/"));

        await org.Client.DeleteAsync($"/api/events/{eventId}");

        Assert.Equal(0, BlobCount($"events/{eventId}/"));
    }

    [Fact]
    public async Task UploadBanner_WhenStorageIsDown_Returns503_NotACrash()
    {
        var org = await TestApi.CreateUserAsync(_factory, "Organiser");
        var eventId = await TestApi.CreateEventAsync(org.Client);

        _factory.BlobStorage.Fail = true;
        try
        {
            var res = await org.Client.PostAsync($"/api/events/{eventId}/banner", FileContent(Png));
            Assert.Equal(HttpStatusCode.ServiceUnavailable, res.StatusCode);
        }
        finally
        {
            _factory.BlobStorage.Fail = false;
        }
    }

    // ---------- Profile pictures ----------

    [Fact]
    public async Task UploadPicture_AsParticipant_ThenOwnerCanFetchIt()
    {
        var participant = await TestApi.CreateUserAsync(_factory);

        var upload = await participant.Client.PostAsync("/api/users/me/picture", FileContent(Png));
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);

        var get = await participant.Client.GetAsync("/api/users/me/picture");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal(Png, await get.Content.ReadAsByteArrayAsync());

        var profile = await participant.Client.GetFromJsonAsync<JsonElement>("/api/users/me");
        Assert.Equal("/api/users/me/picture", profile.GetProperty("profilePictureUrl").GetString());
    }

    [Fact]
    public async Task GetPicture_WhenNoneUploaded_Returns404()
    {
        var participant = await TestApi.CreateUserAsync(_factory);
        var res = await participant.Client.GetAsync("/api/users/me/picture");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task UploadPicture_Unauthenticated_Returns401()
    {
        var res = await _factory.CreateClient().PostAsync("/api/users/me/picture", FileContent(Png));
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task UploadPicture_NotAnImage_Returns400()
    {
        var participant = await TestApi.CreateUserAsync(_factory);
        var res = await participant.Client.PostAsync("/api/users/me/picture",
            FileContent(Encoding.UTF8.GetBytes("plain text pretending to be a picture"), "image/jpeg", "me.jpg"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task UsersCannotSeeEachOthersPictures()
    {
        var a = await TestApi.CreateUserAsync(_factory);
        var b = await TestApi.CreateUserAsync(_factory);
        await a.Client.PostAsync("/api/users/me/picture", FileContent(Png));

        var res = await b.Client.GetAsync("/api/users/me/picture");   // "me" is always the caller

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
