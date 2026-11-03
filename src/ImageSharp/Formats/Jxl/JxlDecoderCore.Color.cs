// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Components.ColorConverters;
using SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Components.VarDct;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jxl;

internal sealed partial class JxlDecoderCore
{
    /// <summary>
    /// Converts completed native frame components and packs them into destination pixels.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format.</typeparam>
    /// <param name="header">The image's native color, sample and extra-channel declarations.</param>
    /// <param name="frame">The reconstructed native frame. All borrowed planes remain owned by this frame.</param>
    /// <param name="destination">The initialized destination frame.</param>
    /// <param name="cancellationToken">The token checked between converted rows.</param>
    private void PackFrame<TPixel>(
        JxlHeader header,
        JxlFrame frame,
        ImageFrame<TPixel> destination,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Configuration configuration = this.Options.Configuration;
        JxlColorConverterBase converter = JxlColorConverterBase.Create(header, frame.Header, configuration.MemoryAllocator);
        using JxlPixelConverter<TPixel> pixelConverter = new(
            configuration,
            header,
            frame.Modular,
            destination,
            frame.AlphaExtra,
            frame.BlackExtra,
            frame.UpsampledAlpha,
            frame.UpsampledBlack,
            converter);

        if (frame.VarDct is JxlVarDctDecoder varDct)
        {
            pixelConverter.ConvertFloat(
                varDct.GetReconstructedChannel(0),
                varDct.GetReconstructedChannel(1),
                varDct.GetReconstructedChannel(2),
                cancellationToken);
        }
        else if (!frame.ModularColors.IsEmpty)
        {
            ReadOnlySpan<Buffer2D<float>> colors = frame.ModularColors;
            Buffer2DRegion<float> first = colors[0].GetRegion();
            Buffer2DRegion<float> second = colors.Length == 1 ? first : colors[1].GetRegion();
            Buffer2DRegion<float> third = colors.Length == 1 ? first : colors[2].GetRegion();

            pixelConverter.ConvertFloat(first, second, third, cancellationToken);
        }
        else
        {
            // No spatial operation required float planes. Normalize and convert the existing integer rows directly.
            pixelConverter.ConvertModular(cancellationToken);
        }
    }
}
