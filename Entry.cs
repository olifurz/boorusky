using ImageMagick;
using Microsoft.Extensions.Logging;

namespace Boorusky;

public class Entry
{
    public required int Id;
    public required string? Artist;
    public List<Image> Images = [];
    public required List<string>? Tags;
    public required string? Source;
    public required string? Url;

    /// <summary>
    /// Find the image with type as an Image class, use FetchImage() for the actual data!
    /// </summary>
    /// <returns>Image (class)</returns>
    private Image GetImage(string type)
    {
        return Images.First(img => img.Type == type);
    }

    /// <summary>
    /// Downloads (sample) image and returns the data
    /// </summary>
    /// <param name="compress">Compress the image</param>
    /// <returns>Byte array of image data</returns>
    public async Task<byte[]?> FetchImage(bool compress = true)
    {
        if (!compress) return await GetData("original");

        var data = await GetData("original");
        if (data != null && data.Length < 1049000) return data;
        Boorusky.Logger!.LogWarning("Image too big after compression, moving to sample");
        data = await GetData("sample");
        if (data != null && data.Length < 1049000) return data;
        Boorusky.Logger!.LogWarning("Image too big after compression, moving to 720x720");
        data = await GetData("720x720");
        if (data != null && data.Length < 1049000) return data;
        Boorusky.Logger!.LogWarning("Image too big after compression, moving to 360x360");
        data = await GetData("360x360");
        if (data != null && data.Length < 1049000) return data;
        throw new Exception("Image failed to meet size checks even after compression.");
    }

    private async Task<byte[]?> GetData(string type)
    {
        var image = GetImage(type);
        var data = await image.Fetch();
        if (data == null) return null;

        using var stream = new MemoryStream(data);
        using var newImage = new MagickImage(stream);
        newImage.Format = MagickFormat.Jpg;
        newImage.InterpolativeResize(new Percentage(50), PixelInterpolateMethod.Bilinear);
        newImage.SetCompression(CompressionMethod.JPEG);
        newImage.Quality = 50;
        Boorusky.Logger!.LogInformation("Compression successful");
        return newImage.ToByteArray();
    }

    // Source - https://stackoverflow.com/a/1080445
    // Posted by pedrofernandes, modified by community. See post 'Timeline' for change history
    // Retrieved 2026-02-06, License - CC BY-SA 4.0
    private static byte[] ReadToEnd(Stream stream)
    {
        long originalPosition = 0;

        if (stream.CanSeek)
        {
            originalPosition = stream.Position;
            stream.Position = 0;
        }

        try
        {
            var readBuffer = new byte[4096];

            var totalBytesRead = 0;
            int bytesRead;

            while ((bytesRead = stream.Read(readBuffer, totalBytesRead, readBuffer.Length - totalBytesRead)) > 0)
            {
                totalBytesRead += bytesRead;

                if (totalBytesRead != readBuffer.Length) continue;
                var nextByte = stream.ReadByte();
                if (nextByte == -1) continue;

                var temp = new byte[readBuffer.Length * 2];
                Buffer.BlockCopy(readBuffer, 0, temp, 0, readBuffer.Length);
                Buffer.SetByte(temp, totalBytesRead, (byte)nextByte);
                readBuffer = temp;
                totalBytesRead++;
            }

            var buffer = readBuffer;
            if (readBuffer.Length == totalBytesRead) return buffer;

            buffer = new byte[totalBytesRead];
            Buffer.BlockCopy(readBuffer, 0, buffer, 0, totalBytesRead);
            return buffer;
        }
        finally
        {
            if (stream.CanSeek)
            {
                stream.Position = originalPosition;
            }
        }
    }
}