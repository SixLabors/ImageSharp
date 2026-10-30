// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Formats.Jxl.Cms;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

/// <summary>
/// Reconstructs linear RGB from JPEG XL XYB samples.
/// </summary>
internal static class JxlXybDecoder
{
    /// <summary>
    /// Converts an XYB rectangle to an equally sized linear RGB image.
    /// </summary>
    /// <param name="opsin">The source planes that contain the rectangle.</param>
    /// <param name="rect">The logical source rectangle.</param>
    /// <param name="linear">The destination, whose logical dimensions must equal the rectangle dimensions.</param>
    /// <param name="transform">The inverse transform and original reference intensity.</param>
    /// <returns>Whether the rectangle and destination have equal dimensions.</returns>
    public static bool OpsinToLinear(JxlImage3<float> opsin, Rectangle rect, JxlImage3<float> linear, XybTransform transform)
    {
        if (!JxlImageOperations.SameSize(rect, linear))
        {
            return false;
        }

        // Borrow all allocations once. Rectangle origins belong only to the input; the output starts at its own origin.
        // Source and destination strides can differ. Exact row slices keep every vector and scalar store out of padding.
        ReadOnlySpan<float> source0 = opsin.PlaneRow(0, 0);
        ReadOnlySpan<float> source1 = opsin.PlaneRow(1, 0);
        ReadOnlySpan<float> source2 = opsin.PlaneRow(2, 0);
        Span<float> destination0 = linear.PlaneRow(0, 0);
        Span<float> destination1 = linear.PlaneRow(1, 0);
        Span<float> destination2 = linear.PlaneRow(2, 0);
        JxlColorConverter converter = JxlColorConverter.CreateXyb(transform);
        int sourceStride = opsin.PixelsPerRow;
        int destinationStride = linear.PixelsPerRow;

        for (int y = 0; y < rect.Height; y++)
        {
            int sourceOffset = ((rect.Top + y) * sourceStride) + rect.Left;
            int destinationOffset = y * destinationStride;
            converter.Convert(
                source0.Slice(sourceOffset, rect.Width),
                source1.Slice(sourceOffset, rect.Width),
                source2.Slice(sourceOffset, rect.Width),
                destination0.Slice(destinationOffset, rect.Width),
                destination1.Slice(destinationOffset, rect.Width),
                destination2.Slice(destinationOffset, rect.Width));
        }

        return true;
    }

    /// <summary>
    /// Checks whether native reconstruction supports the encoding's direct fields and transfer function.
    /// </summary>
    /// <param name="colorEncoding">The requested encoding.</param>
    /// <returns>Whether its direct color fields can describe supported reconstruction output.</returns>
    public static bool CanOutputToColorEncoding(JxlColorEncoding colorEncoding)
    {
        if (!colorEncoding.HaveFields)
        {
            return false;
        }

        JxlCustomTransferFunction tf = colorEncoding.TransferFunction;

        if (!tf.IsPq && !tf.IsSrgb && !tf.HaveGamma && !tf.IsLinear && !tf.IsHlg && !tf.IsDci && !tf.Is709)
        {
            return false;
        }

        if (colorEncoding.IsGray && colorEncoding.WhitePoint != JxlWhitePoint.D65)
        {
            return false;
        }

        return true;
    }
}
