using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Timers;
using static System.Environment;
using FishyFlip;
using FishyFlip.Lexicon;
using FishyFlip.Lexicon.App.Bsky.Embed;
using FishyFlip.Lexicon.App.Bsky.Richtext;
using FishyFlip.Models;
using Microsoft.Extensions.Logging;
using Timer = System.Timers.Timer;

namespace Boorusky;

public class Grabber()
{
    private ATProtocol? _atProtocol;

    private int _lastHour;
    private readonly ILogger? _logger = Boorusky.Logger;
    public async Task Initialise()
    {
        if (_logger == null) throw new NullReferenceException("Logger missing");
        try
        {
            _logger.LogInformation("Welcome to boorusky!");

            var atUrl = GetEnvironmentVariable("AT_INSTANCE_URL");
            if (atUrl == null) throw new NullReferenceException("AT_INSTANCE_URL not set");
            _logger.LogInformation("Target instance is {Url}", atUrl);

            var atProtocolBuilder = new ATProtocolBuilder()
                .EnableAutoRenewSession(true)
                .WithInstanceUrl(new Uri(atUrl));
            _atProtocol = atProtocolBuilder.Build();

            _logger.LogInformation("Attempting authentication..");

            var identifier = GetEnvironmentVariable("AT_IDENTIFIER");
            if (identifier == null) throw new NullReferenceException("AT_IDENTIFIER not set");

            var password = GetEnvironmentVariable("AT_PASSWORD");
            if (password == null) throw new NullReferenceException("AT_PASSWORD not set");

            var (session, error) = await _atProtocol.AuthenticateWithPasswordResultAsync(identifier, password);
            if (session is null)
            {
                throw new Exception("Failed to authenticate: " + error?.Detail?.Message);
            }

            _logger.LogInformation("Successfully authenticated as {SessionHandle}", session.Handle);
        }
        catch(Exception e)
        {
            _logger.LogError($"{e.Message}");
        }

        _logger.LogInformation("A test run will now occur, this will not post any messages");
        _logger.LogInformation("NOTE: If it gets stuck in a loop, then BOORU_TAGS, BOORU_MUST_INCLUDE, or BOORU_MUST_EXCLUDE may be invalid or overly specific.");
        var entry = await Grab()!;
        await entry.FetchImage()!;
    }

    private async Task<Entry> Grab()
    {
        if (_logger == null) throw new NullReferenceException("Logger missing");
        _logger.LogInformation("** GRAB **");
        Entry? result = null;
        while (result == null)
        {
            var url = GetEnvironmentVariable("BOORU_URL") ?? "https://safebooru.donmai.us/";
            var tags = GetEnvironmentVariable("BOORU_TAGS");
            if (tags == null) throw new NullReferenceException("BOORU_TAGS not set");

            try
            {

                var reqUrl = Path.Join(url + "posts.json?limit=1&tags=" + tags);
                var req = new HttpRequestMessage(HttpMethod.Get, reqUrl);
                Boorusky.AddHttpHeaders(req);

                _logger.LogInformation($"Searching for {tags} on {url} ({reqUrl})");
                var res = await Boorusky.Client.SendAsync(req);
                bool filterResult = true;
                if (res.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Grab successful!");

                    var data = await res.Content.ReadAsStringAsync();
                    var jsonDoc = JsonDocument.Parse(data);
                    var root = jsonDoc.RootElement[0];
                    var id = root.GetProperty("id").GetInt32();

                    var entry = new Entry()
                    {
                        Id = id,
                        Artist = root.GetProperty("tag_string_artist").GetString(),
                        Tags = root.GetProperty("tag_string_general").GetString()?.Split(' ').ToList(),
                        Source = root.GetProperty("source").GetString(),
                        Url = $"{url}/posts/{id}.json",
                    };

                    foreach (var variant in root.GetProperty("media_asset").GetProperty("variants").EnumerateArray())
                    {
                        entry.Images.Add(new Image()
                        {
                            Type = variant.GetProperty("type").GetString(),
                            Url = variant.GetProperty("url").GetString(),
                            Width = variant.GetProperty("width").GetInt32(),
                            Height = variant.GetProperty("height").GetInt32(),
                            FileExtension = variant.GetProperty("file_ext").GetString(),
                        });
                    }

                    _logger.LogInformation(
                        $"Parsed data:\nID: {entry.Id}\nArtist: {entry.Artist}\nTags: {entry.Tags?.Count}\nImage Variants: {entry.Images.Count}");

                    var mustIncludeTags = GetEnvironmentVariable("BOORU_MUST_INCLUDE")?.Split(" ") ??
                                          Array.Empty<string>();
                    var mustExcludeTags = GetEnvironmentVariable("BOORU_MUST_EXCLUDE")?.Split(" ") ??
                                          Array.Empty<string>();

                    if (mustIncludeTags.Length != 0)
                    {
                        foreach (var tag in mustIncludeTags)
                        {
                            if (!string.IsNullOrEmpty(tag) && entry.Tags != null && !entry.Tags.Contains(tag))
                            {
                                _logger.LogWarning($"Entry {entry.Id} does not have required tag '{tag}'.");
                                filterResult = false;
                            }
                        }
                    }

                    if (mustExcludeTags.Length != 0)
                    {
                        foreach (var tag in mustExcludeTags)
                        {
                            if (!string.IsNullOrEmpty(tag) && entry.Tags != null && entry.Tags.Contains(tag))
                            {
                                _logger.LogWarning($"Entry {entry.Id} has excluded tag '{tag}'.");
                                filterResult = false;
                            }
                        }
                    }

                    result = filterResult ? entry : null;
                }
            }
            catch (HttpRequestException e)
            {
                _logger.LogError($"{e.StatusCode}: {e.Message}");
            }

            // ReSharper disable once InvertIf
            if (result == null)
            {
                // Search delay
                _logger.LogWarning("Target entry does not meet BOORU_MUST_INCLUDE or BOORU_MUST_EXCLUDE or was null, retrying after 10 seconds!");
                await Task.Delay(TimeSpan.FromSeconds(10));
            }
        }
        return result;
    }

    public async Task Post(object? source, ElapsedEventArgs? e)
    {
        if (_logger == null) throw new NullReferenceException("Logger missing");

        if (_lastHour >= DateTime.Now.Hour && (_lastHour != 23 || DateTime.Now.Hour != 0)) return;
        _lastHour = DateTime.Now.Hour;

        var entry = await Grab();
        _logger.LogInformation("Preparing to post");
        var messageBefore = GetEnvironmentVariable("MESSAGE_BEFORE");
        var messageAfter = GetEnvironmentVariable("MESSAGE_AFTER");
        var post = $"{messageBefore}\n" +
                   $"art by: {{entry.Artist}}\\n\n" +
                   $"[source]({{entry.Source}}) ([meta]({{entry.Url}}))\\n\\n\n" +
                   $"{messageAfter}";
        var imageContent = await entry.FetchImage();
        if (imageContent != null)
        {
            using var stream = new MemoryStream(imageContent);
            var content = new StreamContent(stream);
            content.Headers.ContentLength = stream.Length;
            content.Headers.ContentType = new MediaTypeHeaderValue("image/jpg");

            var blobResult = await _atProtocol!.Repo.UploadBlobAsync(content);
            await blobResult.SwitchAsync(
                async success =>
                {
                    var alt = entry.Tags == null ? "boys kissing" : string.Join(", ", entry.Tags);

                    // Converts the blob to an image
                    var image = new FishyFlip.Lexicon.App.Bsky.Embed.Image(
                        image: success!.Blob,
                        alt: alt,
                        aspectRatio: new AspectRatio(width: entry.Images[0].Width, height: entry.Images[0].Height));

                    var markdownPost = MarkdownPost.Parse(post);

                    // Create a post with the image.
                    var postResult = await _atProtocol.Feed.CreatePostAsync(
                        markdownPost.Post,
                        markdownPost.Facets,
                        embed: new EmbedImages(images: [image]));

                    postResult.Switch(
                        output =>
                        {
                            _logger.LogInformation("Successfully posted! ({SuccessUri})", output?.Uri);
                        },
                        error =>
                        {
                            _logger.LogError("{ErrorStatusCode} {ErrorDetail}", error.StatusCode, error.Detail);
                        }
                    );
                }, error =>
                {
                    _logger.LogError("Error: {ErrorStatusCode} {ErrorDetail}", error.StatusCode, error.Detail);
                    return Task.CompletedTask;
                }
            );
        }
    }
}