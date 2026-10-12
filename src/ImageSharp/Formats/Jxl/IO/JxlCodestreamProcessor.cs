// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Fields;
using SixLabors.ImageSharp.Formats.Jxl.Processing;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

namespace SixLabors.ImageSharp.Formats.Jxl.IO;

internal sealed class JxlCodestreamProcessor
{
    private bool parsedCodestreamSignature;
    private bool parsedBasicInfo;
    private long codestreamSizeInBits;
    private readonly Stream stream;
    private readonly JxlBitReader bitReader;

    public JxlCodestreamProcessor(Stream stream)
    {
        this.stream = stream;
        this.bitReader = new(stream);
    }

    public void ReadBasicInfo()
    {
        this.MaybeParseCodestreamSignature();

        long total = this.bitReader.TotalBitsConsumed;

        this.ReadBundle(this.metadata!.Size!);
        this.ReadBundle(this.metadata!.ImageMetadata!);

        long delta = total - this.bitReader.TotalBitsConsumed;
        this.UpdateCodestreamSize(delta);
        this.parsedBasicInfo = true;
        this.basicInfoSizeHint = 0;
        this.imageMetadata = this.metadata.ImageMetadata;

        if (!CheckSizeLimit(this.metadata.Size!.XSize, this.metadata.Size.YSize))
        {
            throw new InvalidOperationException("The image is too large");
        }
    }

    /// <summary>
    /// Reads a single bundle into <paramref name="bundle"/>.
    /// </summary>
    /// <typeparam name="T">Type of the bundle to read.</typeparam>
    /// <param name="bundle">The bundle to parse.</param>
    private void ReadBundle<T>(T bundle)
        where T : IJxlFields
    {
        if (!JxlBundle.Read(this.bitReader, bundle))
        {
            JxlCodestreamProcessorThrowHelper.ThrowNotEnoughSourceDataForBundle();
        }
    }

    /// <summary>
    /// Checks if width * height can be represented safely as a
    /// positive integer after rounding the width up to the next
    /// multiple of 32.
    /// </summary>
    /// <param name="width">Input width.</param>
    /// <param name="height">Input height.</param>
    /// <returns>
    /// Boolean indicating whether the padded image dimensions fit
    /// within a signed 32-bit integer when calculating the total
    /// pixel count.
    /// </returns>
    /// <remarks>
    /// Negative values aren't rejected, but will produce incorrect
    /// results. This method is meant to be used with positive values only.
    /// </remarks>
    private static bool CheckSizeLimit(int width, int height)
    {
        if (width == 0 || height == 0)
        {
            return true;
        }

        int paddedWidth = JxlMath.DivCeil(width, 32) * 32;

        if (paddedWidth < width)
        {
            // Overflow
            return false;
        }

        int pixelCount = paddedWidth * height;

        if (pixelCount / paddedWidth != height)
        {
            // Overflow
            return false;
        }

        return true;
    }
}
