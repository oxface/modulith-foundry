using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ModulithFoundry.TopologyTests;

public sealed partial class LocalRuntimeTests
{
    private static async Task AssertCancellationAsync(
        HttpClient alice,
        HttpClient bob,
        string aliceCsrf,
        string bobCsrf,
        string root,
        string orderUrl,
        CancellationToken cancellationToken
    )
    {
        var command = new { expectedVersion = 3, reason = "Customer withdrew" };
        using var noCsrf = await alice.PostAsJsonAsync(
            orderUrl + "/cancel",
            command,
            cancellationToken
        );
        Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);
        using var denied = await SendCommandAsync(
            bob,
            HttpMethod.Post,
            orderUrl + "/cancel",
            bobCsrf,
            command,
            cancellationToken
        );
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var stale = await SendCommandAsync(
            alice,
            HttpMethod.Post,
            orderUrl + "/cancel",
            aliceCsrf,
            new { expectedVersion = 2, reason = "Customer withdrew" },
            cancellationToken
        );
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var invalid = await SendCommandAsync(
            alice,
            HttpMethod.Post,
            orderUrl + "/cancel",
            aliceCsrf,
            new { expectedVersion = 3, reason = " " },
            cancellationToken
        );
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("application/problem+json", invalid.Content.Headers.ContentType?.MediaType);
        using var response = await SendCommandAsync(
            alice,
            HttpMethod.Post,
            orderUrl + "/cancel",
            aliceCsrf,
            command,
            cancellationToken
        );
        response.EnsureSuccessStatusCode();
        using var cancelled = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken)
        );
        Assert.Equal("cancelled", cancelled.RootElement.GetProperty("status").GetString());
        Assert.Equal(
            "Customer withdrew",
            cancelled.RootElement.GetProperty("cancellationReason").GetString()
        );
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bound.CancelAfter(TimeSpan.FromSeconds(30));
        while (true)
        {
            using var processResponse = await alice.GetAsync(orderUrl + "/fulfilment", bound.Token);
            processResponse.EnsureSuccessStatusCode();
            using var process = JsonDocument.Parse(
                await processResponse.Content.ReadAsStringAsync(bound.Token)
            );
            if (process.RootElement.GetProperty("status").GetString() == "compensated")
            {
                Assert.Equal(
                    "released",
                    Assert
                        .Single(process.RootElement.GetProperty("lines").EnumerateArray())
                        .GetProperty("releaseStatus")
                        .GetString()
                );
                break;
            }
            await Task.Delay(100, bound.Token);
        }
        using var stock = await alice.GetAsync(
            root + "/inventory/stock-positions/main/bolt",
            cancellationToken
        );
        stock.EnsureSuccessStatusCode();
        using var stockJson = JsonDocument.Parse(
            await stock.Content.ReadAsStringAsync(cancellationToken)
        );
        Assert.Equal(0m, stockJson.RootElement.GetProperty("reservedQuantity").GetDecimal());
        using var history = await alice.GetAsync(
            root + "/inventory/stock-positions/main/bolt/history",
            cancellationToken
        );
        history.EnsureSuccessStatusCode();
        using var historyJson = JsonDocument.Parse(
            await history.Content.ReadAsStringAsync(cancellationToken)
        );
        Assert.Contains(
            historyJson.RootElement.GetProperty("entries").EnumerateArray(),
            item => item.GetProperty("action").GetString() == "reservation-released"
        );
        using var retry = await SendCommandAsync(
            alice,
            HttpMethod.Post,
            orderUrl + "/cancel",
            aliceCsrf,
            command,
            cancellationToken
        );
        retry.EnsureSuccessStatusCode();
        using var retried = JsonDocument.Parse(
            await retry.Content.ReadAsStringAsync(cancellationToken)
        );
        Assert.Equal(
            cancelled.RootElement.GetProperty("cancelledAt").GetDateTimeOffset(),
            retried.RootElement.GetProperty("cancelledAt").GetDateTimeOffset()
        );
    }
}
