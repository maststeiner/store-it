using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static StoreIt.Api.Service.Tests.ApiTestHelpers;

namespace StoreIt.Api.Service.Tests;

/// <summary>
/// SPEC-011 AC-01…AC-06 — tags over HTTP against the real API + PostgreSQL: the tags travel
/// with the item, identity is case-insensitive per storage, unused tags disappear, members
/// see and use the storage's tags, non-members get 404.
/// </summary>
public class ItemTagsTests(ApiTestFixture factory) : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _owner = factory.CreateClientAs("tags-owner");
    private static readonly DateOnly Expiry = ApiTestFixture.Today.AddDays(30);

    private static object Body(string name, params string[] tags) =>
        new
        {
            name,
            amount = 1,
            unit = "Piece",
            expiryDate = Expiry,
            productionDate = (DateOnly?)null,
            tags,
        };

    private static async Task<List<string>> TagsOf(
        HttpClient client,
        Guid storageId,
        string itemName
    )
    {
        var items = await client.GetFromJsonAsync<List<JsonElement>>(
            $"/api/v1/storages/{storageId}/items"
        );
        var item = items!.Single(i => i.GetProperty("name").GetString() == itemName);
        return item.GetProperty("tags").EnumerateArray().Select(t => t.GetString()!).ToList();
    }

    private static async Task<List<(string Name, int Count)>> StorageTags(
        HttpClient client,
        Guid storageId
    )
    {
        var tags = await client.GetFromJsonAsync<List<JsonElement>>(
            $"/api/v1/storages/{storageId}/tags"
        );
        return tags!
            .Select(t =>
                (t.GetProperty("name").GetString()!, t.GetProperty("itemCount").GetInt32())
            )
            .ToList();
    }

    [Fact]
    public async Task AddItem_WithTags_ReturnsThemCanonicalAndSorted()
    {
        // AC-01 / AC-06
        var storage = await _owner.CreateStorageAsync("Pantry");

        var response = await _owner.PostAsJsonAsync(
            $"/api/v1/storages/{storage.Id}/items",
            Body("Beans", "homemade", "Dosen", "  bio ")
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(["bio", "Dosen", "homemade"], await TagsOf(_owner, storage.Id, "Beans"));
    }

    [Fact]
    public async Task AddItem_WithoutTagsField_HasNoTags()
    {
        // AC-06 / EC-07: the field is optional on the wire; the response always carries the array.
        var storage = await _owner.CreateStorageAsync("Pantry");
        await _owner.AddItemAsync(storage.Id, ItemBody(name: "Milk", expiryDate: Expiry));

        Assert.Empty(await TagsOf(_owner, storage.Id, "Milk"));
    }

    [Fact]
    public async Task AddItem_WithExistingTagInOtherCase_ReusesTheFirstSpelling()
    {
        // AC-02 / EC-01
        var storage = await _owner.CreateStorageAsync("Pantry");
        await _owner.PostAsJsonAsync(
            $"/api/v1/storages/{storage.Id}/items",
            Body("Beans", "Dosen")
        );

        await _owner.PostAsJsonAsync($"/api/v1/storages/{storage.Id}/items", Body("Corn", "DOSEN"));

        Assert.Equal(["Dosen"], await TagsOf(_owner, storage.Id, "Corn"));
        Assert.Equal([("Dosen", 2)], await StorageTags(_owner, storage.Id));
    }

    [Fact]
    public async Task AddItem_WithNullTagElement_IgnoresIt()
    {
        // EC-08 over the wire: System.Text.Json does not enforce non-null elements.
        var storage = await _owner.CreateStorageAsync("Pantry");

        var response = await _owner.PostAsJsonAsync(
            $"/api/v1/storages/{storage.Id}/items",
            new
            {
                name = "Beans",
                amount = 1,
                unit = "Piece",
                expiryDate = Expiry,
                productionDate = (DateOnly?)null,
                tags = new string?[] { null, "Dosen" },
            }
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(["Dosen"], await TagsOf(_owner, storage.Id, "Beans"));
    }

    [Fact]
    public async Task AddItem_WithTooManyTags_Returns400AndKeepsTheStorageUnchanged()
    {
        // AC-03
        var storage = await _owner.CreateStorageAsync("Pantry");
        var eleven = Enumerable.Range(1, 11).Select(i => $"tag{i}").ToArray();

        var response = await _owner.PostAsJsonAsync(
            $"/api/v1/storages/{storage.Id}/items",
            Body("Many", eleven)
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("item.tags.tooMany", await response.ReadErrorCodeAsync());
        Assert.Empty(await StorageTags(_owner, storage.Id));
    }

    [Fact]
    public async Task UpdateItem_WithTooLongTag_Returns400AndKeepsTheItem()
    {
        // AC-03
        var storage = await _owner.CreateStorageAsync("Pantry");
        await _owner.PostAsJsonAsync(
            $"/api/v1/storages/{storage.Id}/items",
            Body("Beans", "Dosen")
        );
        var items = await _owner.GetItemsAsync(storage.Id);

        var response = await _owner.PutAsJsonAsync(
            $"/api/v1/storages/{storage.Id}/items/{items[0].Id}",
            Body("Beans", new string('x', 31))
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("item.tags.tooLong", await response.ReadErrorCodeAsync());
        Assert.Equal(["Dosen"], await TagsOf(_owner, storage.Id, "Beans"));
    }

    [Fact]
    public async Task UpdateAndDelete_PruneTagsNoItemCarries()
    {
        // AC-04 / EC-03: edit drops "homemade", delete drops "Dosen" once no item has it.
        var storage = await _owner.CreateStorageAsync("Pantry");
        await _owner.PostAsJsonAsync(
            $"/api/v1/storages/{storage.Id}/items",
            Body("Beans", "Dosen", "homemade")
        );
        await _owner.PostAsJsonAsync($"/api/v1/storages/{storage.Id}/items", Body("Corn", "Dosen"));
        var items = await _owner.GetItemsAsync(storage.Id);
        var beans = items.Single(i => i.Name == "Beans");
        var corn = items.Single(i => i.Name == "Corn");

        var update = await _owner.PutAsJsonAsync(
            $"/api/v1/storages/{storage.Id}/items/{beans.Id}",
            Body("Beans", "dosen")
        );
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);
        Assert.Equal([("Dosen", 2)], await StorageTags(_owner, storage.Id));

        await _owner.DeleteAsync($"/api/v1/storages/{storage.Id}/items/{beans.Id}");
        Assert.Equal([("Dosen", 1)], await StorageTags(_owner, storage.Id));

        await _owner.DeleteAsync($"/api/v1/storages/{storage.Id}/items/{corn.Id}");
        Assert.Empty(await StorageTags(_owner, storage.Id));
    }

    [Fact]
    public async Task GetTags_ListsNameAndItemCountSorted()
    {
        // AC-05
        var storage = await _owner.CreateStorageAsync("Pantry");
        await _owner.PostAsJsonAsync(
            $"/api/v1/storages/{storage.Id}/items",
            Body("Beans", "Dosen", "homemade")
        );
        await _owner.PostAsJsonAsync(
            $"/api/v1/storages/{storage.Id}/items",
            Body("Jam", "Bio", "dosen")
        );

        Assert.Equal(
            [("Bio", 1), ("Dosen", 2), ("homemade", 1)],
            await StorageTags(_owner, storage.Id)
        );
    }

    [Fact]
    public async Task GetTags_UnknownOrForeignStorage_Returns404()
    {
        // AC-05: same access rule as the item routes.
        var storage = await _owner.CreateStorageAsync("Pantry");
        var stranger = factory.CreateClientAs("tags-stranger");

        var foreign = await stranger.GetAsync($"/api/v1/storages/{storage.Id}/tags");
        var unknown = await _owner.GetAsync($"/api/v1/storages/{Guid.NewGuid()}/tags");

        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task GetTags_MalformedStorageId_Returns400()
    {
        var response = await _owner.GetAsync("/api/v1/storages/not-a-guid/tags");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("request.invalidId", await response.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task Member_SeesAndReusesTheStorageTags()
    {
        // EC-10: tags belong to the storage; a member works with the owner's tags.
        var storage = await _owner.CreateStorageAsync("Shared");
        await _owner.PostAsJsonAsync(
            $"/api/v1/storages/{storage.Id}/items",
            Body("Beans", "Dosen")
        );
        var member = factory.CreateClientAs("tags-member", name: "Member");
        var invitation = await _owner.PostAsync($"/api/v1/storages/{storage.Id}/invitation", null);
        var token = (await invitation.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("token")
            .GetString();
        var accept = await member.PostAsJsonAsync("/api/v1/invitations/accept", new { token });
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        Assert.Equal([("Dosen", 1)], await StorageTags(member, storage.Id));
        var response = await member.PostAsJsonAsync(
            $"/api/v1/storages/{storage.Id}/items",
            Body("Corn", "dosen", "Vorrat")
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(["Dosen", "Vorrat"], await TagsOf(_owner, storage.Id, "Corn"));
    }

    [Fact]
    public async Task DeleteStorage_TakesItsTagsWithIt()
    {
        // AC-04 / EC-05: no orphan rows — a new storage of the same name starts without tags,
        // and the deleted storage's tag route is gone.
        var storage = await _owner.CreateStorageAsync("Temporary");
        await _owner.PostAsJsonAsync(
            $"/api/v1/storages/{storage.Id}/items",
            Body("Beans", "Dosen")
        );

        var delete = await _owner.DeleteAsync($"/api/v1/storages/{storage.Id}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        var tags = await _owner.GetAsync($"/api/v1/storages/{storage.Id}/tags");
        Assert.Equal(HttpStatusCode.NotFound, tags.StatusCode);
    }
}
