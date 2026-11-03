// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Jxl.Components.ColorConverters;
using SixLabors.ImageSharp.Formats.Jxl.Components.Modular;
using SixLabors.ImageSharp.Formats.Jxl.Components.VarDct;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;

/// <summary>
/// Owns one decoded JPEG XL frame's native components and their spatial reconstruction.
/// </summary>
/// <remarks>
/// The frame header contains serialized declarations. This owner retains the decoded samples until output or LF transfer.
/// Saved LF planes belong to the decoder. A dependent VarDCT owner only borrows those planes.
/// </remarks>
internal sealed class JxlFrame : IDisposable
{
    private readonly JxlHeader image;
    private readonly Vector3 dcFactors;

    /// <summary>
    /// The parsed curves retained until native spline reconstruction completes, or null when absent.
    /// </summary>
    private JxlSplines? splines;

    // Empty storage means the modular integer channels need no spatial reconstruction. VarDCT keeps its float
    // planes in its own component owner, so ordinary output does not allocate an additional plane-owner array.
    private Buffer2D<float>[] modularColors = [];

    /// <summary>
    /// Indicates whether the frame's retained owners have been released.
    /// </summary>
    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlFrame"/> class with completed native component owners.
    /// </summary>
    /// <param name="image">The image's color and sample declarations.</param>
    /// <param name="header">The serialized frame geometry and reconstruction parameters.</param>
    /// <param name="modular">The completed modular color or extra-channel owner transferred to this frame.</param>
    /// <param name="varDct">The completed VarDCT owner transferred to this frame, or null for modular color.</param>
    /// <param name="dcFactors">The factors that restore modular XYB integer samples to native float units.</param>
    /// <param name="splines">The complete parsed spline owner transferred to this frame, or null when absent.</param>
    /// <remarks>
    /// The caller retains ownership if construction fails. A successful constructor transfers the component and spline owners.
    /// </remarks>
    public JxlFrame(JxlHeader image, JxlFrameHeader header, JxlModularDecoder modular, JxlVarDctDecoder? varDct, Vector3 dcFactors, JxlSplines? splines)
    {
        this.image = image;
        this.Header = header;
        this.Modular = modular;
        this.VarDct = varDct;
        this.dcFactors = dcFactors;
        this.splines = splines;
        this.AlphaExtra = -1;
        this.BlackExtra = -1;

        // LF storage contains native color only. It does not consume display alpha or a CMYK black channel.
        if (header.Type != JxlFrameHeader.FrameType.LowFrequency)
        {
            for (int i = 0; i < image.Metadata.ExtraChannels.Count; i++)
            {
                JxlExtraChannelType type = image.Metadata.ExtraChannels[i].Type;
                if (this.AlphaExtra < 0 && type == JxlExtraChannelType.Alpha)
                {
                    this.AlphaExtra = i;
                }

                if (this.BlackExtra < 0 && type == JxlExtraChannelType.Black)
                {
                    this.BlackExtra = i;
                }
            }

            // Black is an ICC device component. An alpha extra channel must never substitute for missing black.
            bool cmyk = !image.UsesXybTransform && image.IccProfile?.Header.DataColorSpace == IccColorSpaceType.Cmyk;
            if (cmyk && this.BlackExtra < 0)
            {
                throw new InvalidImageContentException("The JPEG XL CMYK profile requires a black extra channel.");
            }

            if (!cmyk)
            {
                this.BlackExtra = -1;
            }
        }
    }

    /// <summary>
    /// Gets the serialized frame declarations.
    /// </summary>
    public JxlFrameHeader Header { get; }

    /// <summary>
    /// Gets the retained modular color and extra-channel owner.
    /// </summary>
    public JxlModularDecoder Modular { get; }

    /// <summary>
    /// Gets the retained VarDCT component owner, or null for modular color.
    /// </summary>
    public JxlVarDctDecoder? VarDct { get; }

    /// <summary>
    /// Gets the first alpha extra-channel index, or minus one when output does not use alpha.
    /// </summary>
    public int AlphaExtra { get; }

    /// <summary>
    /// Gets the CMYK black extra-channel index, or minus one when output does not use black.
    /// </summary>
    public int BlackExtra { get; }

    /// <summary>
    /// Gets the borrowed modular float color owners, or an empty span when integer rows are sufficient.
    /// </summary>
    public ReadOnlySpan<Buffer2D<float>> ModularColors => this.modularColors;

    /// <summary>
    /// Gets the borrowed expanded alpha plane, or null when the pixel converter reads the original modular channel.
    /// </summary>
    public Buffer2D<float>? UpsampledAlpha { get; private set; }

    /// <summary>
    /// Gets the borrowed expanded black plane, or null when the pixel converter reads the original modular channel.
    /// </summary>
    public Buffer2D<float>? UpsampledBlack { get; private set; }

    /// <summary>
    /// Completes chroma expansion, restoration, native image features and upsampling before LF storage or displayed output.
    /// </summary>
    /// <param name="allocator">The tracked allocator for the same component planes and row workspaces used by both outputs.</param>
    /// <param name="cancellationToken">The token checked between converted rows and reconstruction passes.</param>
    /// <remarks>
    /// Call once after section decoding. Failure leaves every acquired owner attached to this frame for disposal.
    /// No pixel bounds, color conversion or alpha unassociation apply to these native component samples.
    /// </remarks>
    public void Reconstruct(MemoryAllocator allocator, CancellationToken cancellationToken)
    {
        JxlFrameHeader frame = this.Header;
        JxlModularDecoder modular = this.Modular;
        JxlVarDctDecoder? varDct = this.VarDct;
        int colorFactor = frame.Upsampling;
        int alphaFactor = this.AlphaExtra < 0 ? 1 : frame.ExtraChannelUpsampling[this.AlphaExtra];
        int blackFactor = this.BlackExtra < 0 ? 1 : frame.ExtraChannelUpsampling[this.BlackExtra];
        int maximumFactor = Math.Max(colorFactor, Math.Max(alphaFactor, blackFactor));
        bool restore = frame.RestorationFilter.Gaborish || frame.RestorationFilter.Iterations != 0;
        bool subsampled = false;
        for (int component = 0; component < frame.HorizontalSamplingShifts.Length; component++)
        {
            subsampled |= frame.HorizontalSamplingShifts[component] != 0 || frame.VerticalSamplingShifts[component] != 0;
        }

        if (varDct is not null)
        {
            varDct.ApplyRestoration(frame, allocator, cancellationToken);
        }
        else if (this.image.UsesXybTransform || colorFactor > 1 || subsampled || restore || this.splines is not null
            || frame.Type == JxlFrameHeader.FrameType.LowFrequency)
        {
            // Native modular RGB stays integer unless a spatial operation or LF storage requires float planes.
            // Assign completed allocations before filtering, so a failed filter remains covered by frame disposal.
            Buffer2D<float>[] colors = this.image.UsesXybTransform
                ? modular.ReconstructXyb(frame, this.dcFactors, cancellationToken)
                : this.ReconstructNativeModularColors(allocator, cancellationToken);

            this.modularColors = colors;
            this.RestoreModularColors(colors, allocator, cancellationToken);
        }

        if (this.splines is JxlSplines splines)
        {
            // Splines modify restored native components at coding resolution. Common upsampling and
            // the color transform must see these contributions. VarDCT supplies its parsed base correlation;
            // modular frames retain the default X=0 and B=1 ratios because they have no VarDCT map,
            // even when their stored components use native RGB instead of XYB.
            ReadOnlySpan<Buffer2D<float>> colors = varDct is null ? this.modularColors : varDct.ReconstructedChannels;
            Vector2 correlation = varDct is null
                ? Vector2.UnitY
                : new Vector2(varDct.BaseCorrelationX, varDct.BaseCorrelationB);

            splines.Apply(colors, correlation, cancellationToken);

            // Every curve has reached its last consumer. Return its tracked storage before upsampling
            // or pixel conversion. On failure, the attached owner remains covered by frame disposal.
            splines.Dispose();
            this.splines = null;
        }

        if (maximumFactor > 1)
        {
            // One kernel and row window serves color and selected display extras. The extra-channel parser
            // requires their factors to be at least the color factor. Without color expansion, use the largest
            // actual extra-channel source instead. LF frames select no display extras and expand only color.
            Size maximumSourceSize = colorFactor > 1 ? frame.CodingSize : default;
            if (alphaFactor > 1)
            {
                Size size = modular.GetChannel(modular.ColorChannelCount + this.AlphaExtra).Size;
                maximumSourceSize = new Size(Math.Max(maximumSourceSize.Width, size.Width), Math.Max(maximumSourceSize.Height, size.Height));
            }

            if (blackFactor > 1)
            {
                Size size = modular.GetChannel(modular.ColorChannelCount + this.BlackExtra).Size;
                maximumSourceSize = new Size(Math.Max(maximumSourceSize.Width, size.Width), Math.Max(maximumSourceSize.Height, size.Height));
            }

            using JxlUpsampler upsampler = new(this.image, frame.OutputSize, maximumSourceSize, maximumFactor, allocator);
            if (colorFactor > 1)
            {
                if (varDct is not null)
                {
                    varDct.ApplyUpsampling(upsampler, colorFactor, cancellationToken);
                }
                else
                {
                    foreach (Buffer2D<float> plane in this.modularColors)
                    {
                        upsampler.Apply(plane, frame.CodingSize, colorFactor, cancellationToken);
                    }
                }
            }

            if (alphaFactor > 1)
            {
                this.UpsampledAlpha = this.UpsampleExtraChannel(this.AlphaExtra, upsampler, alphaFactor, allocator, cancellationToken);
            }

            if (blackFactor > 1)
            {
                this.UpsampledBlack = this.UpsampleExtraChannel(this.BlackExtra, upsampler, blackFactor, allocator, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Transfers the three completed native color planes to a decoder-owned LF slot without copying samples.
    /// </summary>
    /// <returns>The transferred plane owners. The decoder must dispose them after their final dependent frame.</returns>
    /// <remarks>
    /// Call only for an LF frame after <see cref="Reconstruct"/>. Do not read or reconstruct this frame's colors after transfer.
    /// Array construction for VarDCT completes before its owner releases the planes. Allocation failure preserves ownership.
    /// </remarks>
    public Buffer2D<float>[] TakeReconstructedChannels()
    {
        if (this.VarDct is JxlVarDctDecoder varDct)
        {
            return varDct.TakeReconstructedChannels();
        }

        Buffer2D<float>[] result = this.modularColors;
        this.modularColors = [];
        return result;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.isDisposed)
        {
            return;
        }

        this.UpsampledBlack?.Dispose();
        this.UpsampledAlpha?.Dispose();
        this.splines?.Dispose();
        foreach (Buffer2D<float> plane in this.modularColors)
        {
            plane.Dispose();
        }

        this.VarDct?.Dispose();
        this.Modular.Dispose();
        this.isDisposed = true;
    }

    /// <summary>
    /// Normalizes native modular color into final-capacity float planes for spatial reconstruction or an LF slot.
    /// </summary>
    /// <param name="allocator">The tracked allocator for the completed float plane owners.</param>
    /// <param name="cancellationToken">The token checked between converted rows.</param>
    /// <returns>Caller-owned planes. Only each original channel's coded rectangle is initialized.</returns>
    private Buffer2D<float>[] ReconstructNativeModularColors(MemoryAllocator allocator, CancellationToken cancellationToken)
    {
        JxlHeader header = this.image;
        JxlFrameHeader frame = this.Header;
        JxlModularDecoder modular = this.Modular;
        int colorCount = modular.ColorChannelCount;

        // LF slots, restoration and spline reconstruction require three distinct component planes.
        // Native modular gray initializes all three before component-specific filtering or spline additions.
        // Unfiltered ordinary gray needs one owner; the color converter borrows that row three times.
        bool restore = frame.RestorationFilter.Gaborish || frame.RestorationFilter.Iterations != 0;
        int planeCount = restore || this.splines is not null || frame.Type == JxlFrameHeader.FrameType.LowFrequency ? 3 : colorCount;
        Buffer2D<float>[] planes = new Buffer2D<float>[planeCount];
        int allocated = 0;
        try
        {
            for (int component = 0; component < planeCount; component++)
            {
                Buffer2D<float> plane = allocator.Allocate2D<float>(frame.OutputSize);
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
    /// <param name="colors">The borrowed, distinct final-capacity float color planes.</param>
    /// <param name="allocator">The tracked allocator for the shared restoration workspace.</param>
    /// <param name="cancellationToken">The token checked between reconstructed rows and passes.</param>
    private void RestoreModularColors(Buffer2D<float>[] colors, MemoryAllocator allocator, CancellationToken cancellationToken)
    {
        JxlFrameHeader frame = this.Header;
        JxlModularDecoder modular = this.Modular;
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
            JxlRestoration.Apply(colors, frame.CodingSize, filter, sigma, allocator, cancellationToken);
        }
    }

    /// <summary>
    /// Normalizes and expands one required extra channel with the shared color and extra-channel workspace.
    /// </summary>
    /// <param name="extraIndex">The image-wide extra-channel index.</param>
    /// <param name="upsampler">The ready workspace sized for every expanded plane.</param>
    /// <param name="factor">The validated effective factor for this channel.</param>
    /// <param name="allocator">The tracked allocator for the completed extra-channel plane.</param>
    /// <param name="cancellationToken">The token checked between source and expanded rows.</param>
    /// <returns>The caller-owned final-size float plane. Failure disposes the unfinished plane.</returns>
    private Buffer2D<float> UpsampleExtraChannel(
        int extraIndex,
        JxlUpsampler upsampler,
        int factor,
        MemoryAllocator allocator,
        CancellationToken cancellationToken)
    {
        Buffer2DRegion<int> source = this.Modular.GetChannel(this.Modular.ColorChannelCount + extraIndex);
        Buffer2D<float> plane = allocator.Allocate2D<float>(upsampler.TargetSize);
        try
        {
            JxlBitDepth bitDepth = this.image.Metadata.ExtraChannels[extraIndex].BitDepth;
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
