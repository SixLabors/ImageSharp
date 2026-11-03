// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Jxl.Components.ColorConverters;
using SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Components.Modular;
using SixLabors.ImageSharp.Formats.Jxl.Components.VarDct;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jxl;

internal sealed partial class JxlDecoderCore
{
    /// <summary>
    /// Prepares native color planes and extra channels, then packs the complete frame through one color converter.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format.</typeparam>
    /// <param name="header">The image's native color, sample and extra-channel declarations.</param>
    /// <param name="frame">The completed frame's geometry and upsampling factors.</param>
    /// <param name="modular">The completed modular color or extra-channel planes.</param>
    /// <param name="varDct">The restored VarDCT color owner, or null for modular color.</param>
    /// <param name="dcFactors">The factors used to restore modular XYB samples.</param>
    /// <param name="destination">The initialized destination frame.</param>
    /// <param name="cancellationToken">The token checked during reconstruction, upsampling and row packing.</param>
    private void PackFrame<TPixel>(
        JxlHeader header,
        JxlFrameHeader frame,
        JxlModularDecoder modular,
        JxlVarDctDecoder? varDct,
        Vector3 dcFactors,
        ImageFrame<TPixel> destination,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int alphaExtra = -1;
        int blackExtra = -1;
        for (int i = 0; i < header.Metadata.ExtraChannels.Count; i++)
        {
            JxlExtraChannelType type = header.Metadata.ExtraChannels[i].Type;
            if (alphaExtra < 0 && type == JxlExtraChannelType.Alpha)
            {
                alphaExtra = i;
            }

            if (blackExtra < 0 && type == JxlExtraChannelType.Black)
            {
                blackExtra = i;
            }
        }

        // Black is a device component, not alpha. Only a CMYK source profile consumes it. Other extra
        // channels remain reconstructed in the modular owner until their image-feature consumers run.
        bool cmyk = !header.UsesXybTransform && header.IccProfile?.Header.DataColorSpace == IccColorSpaceType.Cmyk;
        if (cmyk && blackExtra < 0)
        {
            throw new InvalidImageContentException("The JPEG XL CMYK profile requires a black extra channel.");
        }

        if (!cmyk)
        {
            blackExtra = -1;
        }

        Configuration configuration = this.Options.Configuration;
        MemoryAllocator allocator = configuration.MemoryAllocator;
        JxlColorConverterBase converter = JxlColorConverterBase.Create(header, frame, allocator);
        int extraOffset = modular.ColorChannelCount;
        int colorFactor = frame.Upsampling;
        int alphaFactor = alphaExtra < 0 ? 1 : frame.ExtraChannelUpsampling[alphaExtra];
        int blackFactor = blackExtra < 0 ? 1 : frame.ExtraChannelUpsampling[blackExtra];
        int maximumFactor = Math.Max(colorFactor, Math.Max(alphaFactor, blackFactor));
        bool restore = frame.RestorationFilter.Gaborish || frame.RestorationFilter.Iterations != 0;
        bool subsampled = false;
        for (int component = 0; component < frame.HorizontalSamplingShifts.Length; component++)
        {
            subsampled |= frame.HorizontalSamplingShifts[component] != 0 || frame.VerticalSamplingShifts[component] != 0;
        }

        Buffer2D<float>[]? modularColors = null;
        Buffer2D<float>? upsampledAlpha = null;
        Buffer2D<float>? upsampledBlack = null;
        try
        {
            if (varDct is null && (header.UsesXybTransform || colorFactor > 1 || subsampled || restore))
            {
                // Native modular RGB stays integer unless spatial reconstruction needs float planes.
                // XYB always restores its signed integer units before the shared color operation.
                modularColors = header.UsesXybTransform
                    ? modular.ReconstructXyb(frame, dcFactors, cancellationToken)
                    : this.ReconstructNativeModularColors(header, frame, modular, cancellationToken);

                this.RestoreModularColors(frame, modular, modularColors, cancellationToken);
            }

            if (maximumFactor > 1)
            {
                // A single kernel and row window serves all expanded planes. The extra-channel parser
                // requires their effective factors to be at least the color factor, so none needs a wider
                // source than color. With no color expansion, reserve the largest actual extra-channel source.
                Size maximumSourceSize = colorFactor > 1 ? frame.CodingSize : default;
                if (alphaFactor > 1)
                {
                    Size size = modular.GetChannel(extraOffset + alphaExtra).Size;
                    maximumSourceSize = new Size(Math.Max(maximumSourceSize.Width, size.Width), Math.Max(maximumSourceSize.Height, size.Height));
                }

                if (blackFactor > 1)
                {
                    Size size = modular.GetChannel(extraOffset + blackExtra).Size;
                    maximumSourceSize = new Size(Math.Max(maximumSourceSize.Width, size.Width), Math.Max(maximumSourceSize.Height, size.Height));
                }

                using JxlUpsampler upsampler = new(header, frame.OutputSize, maximumSourceSize, maximumFactor, allocator);
                if (colorFactor > 1)
                {
                    if (varDct is not null)
                    {
                        varDct.ApplyUpsampling(upsampler, colorFactor, cancellationToken);
                    }
                    else if (modularColors is not null)
                    {
                        foreach (Buffer2D<float> plane in modularColors)
                        {
                            upsampler.Apply(plane, frame.CodingSize, colorFactor, cancellationToken);
                        }
                    }
                }

                if (alphaFactor > 1)
                {
                    upsampledAlpha = this.UpsampleExtraChannel(header, modular, extraOffset, alphaExtra, upsampler, alphaFactor, cancellationToken);
                }

                if (blackFactor > 1)
                {
                    upsampledBlack = this.UpsampleExtraChannel(header, modular, extraOffset, blackExtra, upsampler, blackFactor, cancellationToken);
                }
            }

            using JxlPixelConverter<TPixel> pixelConverter = new(
                configuration,
                header,
                modular,
                destination,
                alphaExtra,
                blackExtra,
                upsampledAlpha,
                upsampledBlack,
                converter);

            if (varDct is not null)
            {
                pixelConverter.ConvertFloat(
                    varDct.GetReconstructedChannel(0),
                    varDct.GetReconstructedChannel(1),
                    varDct.GetReconstructedChannel(2),
                    cancellationToken);
            }
            else if (modularColors is not null)
            {
                Buffer2DRegion<float> first = modularColors[0].GetRegion();
                Buffer2DRegion<float> second = modularColors.Length == 1 ? first : modularColors[1].GetRegion();
                Buffer2DRegion<float> third = modularColors.Length == 1 ? first : modularColors[2].GetRegion();

                pixelConverter.ConvertFloat(first, second, third, cancellationToken);
            }
            else
            {
                pixelConverter.ConvertModular(cancellationToken);
            }
        }
        finally
        {
            upsampledBlack?.Dispose();
            upsampledAlpha?.Dispose();
            if (modularColors is not null)
            {
                foreach (Buffer2D<float> plane in modularColors)
                {
                    plane.Dispose();
                }
            }
        }
    }

    /// <summary>
    /// Normalizes native modular color into final-capacity float planes for spatial reconstruction or an LF slot.
    /// </summary>
    /// <param name="header">The native sample precision and color model.</param>
    /// <param name="frame">The coded and final output dimensions.</param>
    /// <param name="modular">The completed original integer channels.</param>
    /// <param name="cancellationToken">The token checked between converted rows.</param>
    /// <returns>Caller-owned planes. Only each original channel's coded rectangle is initialized.</returns>
    private Buffer2D<float>[] ReconstructNativeModularColors(
        JxlHeader header, JxlFrameHeader frame, JxlModularDecoder modular, CancellationToken cancellationToken)
    {
        int colorCount = modular.ColorChannelCount;

        // LF slots and restoration require three distinct component planes. Native modular gray
        // initializes all three from its single channel before component-specific Gaborish filtering.
        // Unfiltered ordinary gray needs one owner; the color converter borrows that row three times.
        bool restore = frame.RestorationFilter.Gaborish || frame.RestorationFilter.Iterations != 0;
        int planeCount = restore || frame.Type == JxlFrameHeader.FrameType.LowFrequency ? 3 : colorCount;
        Buffer2D<float>[] planes = new Buffer2D<float>[planeCount];
        int allocated = 0;
        try
        {
            for (int component = 0; component < planeCount; component++)
            {
                Buffer2D<float> plane = this.Options.Configuration.MemoryAllocator.Allocate2D<float>(frame.OutputSize);
                planes[component] = plane;
                allocated++;
                Buffer2DRegion<int> source = modular.GetChannel(colorCount == 1 ? 0 : component);
                for (int y = 0; y < source.Height; y++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    JxlSampleConverter.Convert(source.DangerousGetRowSpan(y), plane.DangerousGetRowSpan(y)[..source.Width], header.Metadata.BitDepth);
                }
            }

            return planes;
        }
        catch
        {
            for (int component = 0; component < allocated; component++)
            {
                planes[component].Dispose();
            }

            throw;
        }
    }

    /// <summary>
    /// Expands modular chroma and applies the same restoration kernels used by VarDCT.
    /// </summary>
    /// <param name="frame">The coding geometry, sampling shifts and restoration parameters.</param>
    /// <param name="modular">The completed source channels that establish each coded rectangle.</param>
    /// <param name="colors">The borrowed, distinct final-capacity float color planes.</param>
    /// <param name="cancellationToken">The token checked between reconstructed rows and passes.</param>
    private void RestoreModularColors(
        JxlFrameHeader frame, JxlModularDecoder modular, Buffer2D<float>[] colors, CancellationToken cancellationToken)
    {
        int colorCount = modular.ColorChannelCount;
        for (int component = 0; component < colors.Length; component++)
        {
            int sourceComponent = colorCount == 1 ? 0 : component;
            Size codedSize = modular.GetChannel(sourceComponent).Size;
            JxlUpsampler.ExpandChroma(
                colors[component],
                codedSize,
                frame.CodingSize,
                frame.HorizontalSamplingShifts[sourceComponent],
                frame.VerticalSamplingShifts[sourceComponent],
                cancellationToken);
        }

        JxlRestorationFilter filter = frame.RestorationFilter;
        if (filter.Gaborish || filter.Iterations != 0)
        {
            // Modular coding signals one physical sigma instead of VarDCT's quantizer/sharpness map.
            // Only that setup differs. Both coding modes share the same float row rings and SIMD kernels.
            JxlRestoration.ModularSigmaSource sigma = new(frame.CodingSize.Width, filter.ModularSigma);
            JxlRestoration.Apply(colors, frame.CodingSize, filter, sigma, this.Options.Configuration.MemoryAllocator, cancellationToken);
        }
    }

    /// <summary>
    /// Normalizes and expands one required extra channel with the shared color and extra-channel workspace.
    /// </summary>
    /// <param name="header">The extra channel's own integer or floating-point layout.</param>
    /// <param name="modular">The completed native extra-channel owner.</param>
    /// <param name="extraOffset">The number of original modular color channels before the extras.</param>
    /// <param name="extraIndex">The image-wide extra-channel index.</param>
    /// <param name="upsampler">The ready workspace sized for every expanded plane.</param>
    /// <param name="factor">The validated effective factor for this channel.</param>
    /// <param name="cancellationToken">The token checked between source and expanded rows.</param>
    /// <returns>The caller-owned final-size float plane. Failure disposes the unfinished plane.</returns>
    private Buffer2D<float> UpsampleExtraChannel(
        JxlHeader header,
        JxlModularDecoder modular,
        int extraOffset,
        int extraIndex,
        JxlUpsampler upsampler,
        int factor,
        CancellationToken cancellationToken)
    {
        Buffer2DRegion<int> source = modular.GetChannel(extraOffset + extraIndex);
        Buffer2D<float> plane = this.Options.Configuration.MemoryAllocator.Allocate2D<float>(upsampler.TargetSize);
        try
        {
            JxlBitDepth bitDepth = header.Metadata.ExtraChannels[extraIndex].BitDepth;
            for (int y = 0; y < source.Height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                JxlSampleConverter.Convert(source.DangerousGetRowSpan(y), plane.DangerousGetRowSpan(y)[..source.Width], bitDepth);
            }

            // Reconstruction and interpolation can retain values outside zero through one. Extra channels
            // use their own precision; the destination pixel API applies alpha bounds after color conversion.
            upsampler.Apply(plane, source.Size, factor, cancellationToken);
            return plane;
        }
        catch
        {
            plane.Dispose();
            throw;
        }
    }
}
