using System.Net.Http.Json;
using System.Text.Json;
using static StoreIt.Api.Service.Tests.ApiTestHelpers;

namespace StoreIt.Api.Service.Tests;

/// <summary>
/// SPEC-008 AC-07 — every field against a seeded world. Own fixture (= own database), so
/// the absolute counts are deterministic: three users, three storages (one shared, one
/// empty), items in every expiry state, one open invitation link.
/// </summary>
public class AdminStatisticsScenarioTests(ApiTestFixture factory) : IClassFixture<ApiTestFixture>
{
    private static readonly DateOnly Today = ApiTestFixture.Today;

    [Fact]
    public async Task Statistics_SeededWorld_EveryFieldMatches()
    {
        // --- users: the operator, a member, and one who never created anything ---
        var olga = factory.CreateClientAs("stats-olga", email: "operator@test.local", name: "Olga");
        var max = factory.CreateClientAs("stats-max", email: "max@test.local", name: "Max");
        var noah = factory.CreateClientAs("stats-noah", email: "noah@test.local", name: "Noah");
        await noah.GetAsync("/auth/me"); // provisions Noah without any storage

        // --- storages: Pantry (owned by Olga, shared with Max), Fridge (Max), Empty (Olga) ---
        var pantry = await olga.CreateStorageAsync("Pantry");
        var pantryToken = await olga.CreateInvitationAsync(pantry.Id);
        (
            await max.PostAsJsonAsync("/api/v1/invitations/accept", new { token = pantryToken })
        ).EnsureSuccessStatusCode();
        (
            await olga.DeleteAsync($"/api/v1/storages/{pantry.Id}/invitation")
        ).EnsureSuccessStatusCode();

        var fridge = await max.CreateStorageAsync("Fridge");
        await max.CreateInvitationAsync(fridge.Id); // stays open → invitations.pending = 1
        await olga.CreateStorageAsync("Empty");

        // --- items (the domain requires an expiry or a production date): expired, expiring
        //     soon, fresh, two with a production date only; one litre of juice in the Fridge ---
        await olga.AddItemAsync(pantry.Id, ItemBody("Yoghurt", expiryDate: Today.AddDays(-1)));
        await olga.AddItemAsync(pantry.Id, ItemBody("Milk", expiryDate: Today.AddDays(2)));
        await olga.AddItemAsync(pantry.Id, ItemBody("Rice", expiryDate: Today.AddDays(30)));
        await olga.AddItemAsync(pantry.Id, ItemBody("Honey", productionDate: Today.AddDays(-100)));
        await olga.AddItemAsync(pantry.Id, ItemBody("Salt", productionDate: Today.AddDays(-400)));
        await max.AddItemAsync(
            fridge.Id,
            ItemBody("Juice", amount: 1.5m, unit: "Liter", expiryDate: Today.AddDays(60))
        );

        var stats = await olga.GetFromJsonAsync<JsonElement>("/api/v1/admin/statistics");

        var users = stats.GetProperty("users");
        Assert.Equal(3, users.GetProperty("total").GetInt32());
        Assert.Equal(3, users.GetProperty("newLast7Days").GetInt32());
        Assert.Equal(3, users.GetProperty("newLast30Days").GetInt32());
        Assert.Equal(1, users.GetProperty("withoutAnyStorage").GetInt32());
        var byIssuer = users.GetProperty("byIssuer");
        Assert.Single(byIssuer.EnumerateObject());
        Assert.Equal(3, byIssuer.GetProperty("https://test.local").GetInt32());

        var storages = stats.GetProperty("storages");
        Assert.Equal(3, storages.GetProperty("total").GetInt32());
        Assert.Equal(1, storages.GetProperty("shared").GetInt32());
        Assert.Equal(1, storages.GetProperty("empty").GetInt32());
        Assert.Equal(1, storages.GetProperty("itemsPerStorageMedian").GetInt32()); // 0, 1, 5
        Assert.Equal(5, storages.GetProperty("itemsPerStorageMax").GetInt32());

        Assert.Equal(1, stats.GetProperty("invitations").GetProperty("pending").GetInt32());

        var items = stats.GetProperty("items");
        Assert.Equal(6, items.GetProperty("total").GetInt32());
        Assert.Equal(4, items.GetProperty("withExpiryDate").GetInt32());
        Assert.Equal(2, items.GetProperty("withProductionDate").GetInt32());
        Assert.Equal(1, items.GetProperty("expired").GetInt32());
        Assert.Equal(1, items.GetProperty("expiringSoon").GetInt32());
        var byUnit = items.GetProperty("byUnit");
        Assert.Equal(5, byUnit.GetProperty("Piece").GetInt32());
        Assert.Equal(1, byUnit.GetProperty("Liter").GetInt32());

        // AC-06: aggregates only — nothing that identifies a person leaks through.
        var raw = stats.GetRawText();
        Assert.DoesNotContain("olga", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "test.local\"",
            raw.Replace("https://test.local\"", ""),
            StringComparison.Ordinal
        );
        Assert.DoesNotContain("stats-", raw, StringComparison.Ordinal);
    }
}

file static class AdminScenarioExtensions
{
    public static async Task<string> CreateInvitationAsync(this HttpClient owner, Guid storageId)
    {
        var response = await owner.PostAsync($"/api/v1/storages/{storageId}/invitation", null);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        return created.GetProperty("token").GetString()!;
    }
}
