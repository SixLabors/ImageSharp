// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jxl;

/// <summary>
/// Image decoder for JPEG XL images, decoded via the external libjxl
/// decoder (djxl) to PNG and then with the managed PNG decoder.
/// </summary>
internal class JxlDecoder : ImageDecoder
{
    private JxlDecoder()
    {
    }

    public static JxlDecoder Instance { get; } = new();

    /// <inheritdoc />
    protected override Image<TPixel> Decode<TPixel>(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(stream, nameof(stream));

        string dir = JxlTool.NewTempDir();
        try
        {
            string jxlPath = Path.Combine(dir, "image.jxl");
            string pngPath = Path.Combine(dir, "image.png");
            using (var file = new FileStream(jxlPath, FileMode.Create, FileAccess.Write))
            {
                stream.CopyTo(file);
            }

            JxlTool.Run("djxl", jxlPath, pngPath);
            Image<TPixel> image = Image.Load<TPixel>(pngPath);
            ScaleToTargetSize(options, image);
            return image;
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            throw new ImageFormatException("JPEG XL decoding requires the libjxl 'djxl' tool on PATH.", e);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <inheritdoc />
    protected override Image Decode(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
        => this.Decode<Rgba32>(options, stream, cancellationToken);

    protected override ImageInfo Identify(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        using Image image = this.Decode(options, stream, cancellationToken);
        return new ImageInfo(new Size(image.Width, image.Height), image.Metadata);
    }
}
