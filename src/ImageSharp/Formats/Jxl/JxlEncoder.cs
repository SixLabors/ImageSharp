// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl;

/// <summary>
/// Image encoder for writing an image as JPEG XL, encoded via the
/// external libjxl encoder (cjxl) from a managed PNG intermediate.
/// </summary>
public class JxlEncoder : ImageEncoder
{
    /// <summary>
    /// Gets the encoder effort (1-10, default 7) passed to cjxl.
    /// </summary>
    public int Effort { get; init; } = 7;

    /// <summary>
    /// Gets the Butteraugli distance (default 1.0, ignored when lossless).
    /// </summary>
    public float Distance { get; init; } = 1.0f;

    /// <summary>
    /// Gets a value indicating whether to encode losslessly (Modular mode).
    /// </summary>
    public bool Lossless { get; init; }

    /// <inheritdoc />
    protected override void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
    {
        string dir = JxlTool.NewTempDir();
        try
        {
            string pngPath = Path.Combine(dir, "image.png");
            string jxlPath = Path.Combine(dir, "image.jxl");
            image.Save(pngPath);
            try
            {
                JxlTool.Run(
                    "cjxl",
                    pngPath,
                    jxlPath,
                    $"--effort={this.Effort}",
                    this.Lossless ? "--lossless=1" : $"--distance={this.Distance.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            }
            catch (System.ComponentModel.Win32Exception e)
            {
                throw new ImageFormatException("JPEG XL encoding requires the libjxl 'cjxl' tool on PATH.", e);
            }

            using var file = new FileStream(jxlPath, FileMode.Open, FileAccess.Read);
            file.CopyTo(stream);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
