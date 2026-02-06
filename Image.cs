using Microsoft.Extensions.Logging;

namespace Boorusky;

public class Image
{
    public required string? Type;
    public required string? Url;
    public int Width;
    public int Height;
    public required string? FileExtension;

    public async Task<byte[]?> Fetch()
    {
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get, Url);
            Boorusky.AddHttpHeaders(req);

            using var response = await Boorusky.Client.SendAsync(req);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadAsByteArrayAsync();
            Boorusky.Logger!.LogInformation("Fetched {Url} with size {result} bytes", Url, result.Length);
            return result;
        }
        catch(Exception ex)
        {
            Boorusky.Logger!.LogError("Failed to fetch {Url} ({Exception})", Url, ex.Message);
            return null;
        }
    }
}