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
    /// The decoded noise strengths. A default value has no active noise and needs no workspace.
    /// </summary>
    private readonly JxlNoise noise;

    /// <summary>
    /// The parsed patch owner retained until its native sample additions complete, or null when absent.
    /// </summary>
    private JxlPatches? patches;

    /// <summary>
    /// The parsed curves retained until native spline reconstruction completes, or null when absent.
    /// </summary>
    private JxlSplines? splines;

    // Retained VarDCT output transfers its restored planes here before native frame features run.
    // Modular output needs these float planes only for spatial operations or saved references;
    // ordinary modular output reads integer channels directly, and group-row VarDCT keeps its row workspace.
    private Buffer2D<float>[] reconstructedColors = [];

    /// <summary>
    /// The distinct normalized extra-channel owners required by patches or a saved reference, in encoded order.
    /// </summary>
    private Buffer2D<float>[] nativeExtras = [];

    /// <summary>
    /// The ready display alpha plane. It belongs to nativeExtras when every extra is retained, or directly to this frame otherwise.
    /// </summary>
    private Buffer2D<float>? upsampledAlpha;

    /// <summary>
    /// The ready display black plane. It belongs to nativeExtras when every extra is retained, or directly to this frame otherwise.
    /// </summary>
    private Buffer2D<float>? upsampledBlack;

    /// <summary>
    /// Indicates whether the frame's retained owners have been released.
    /// </summary>
    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlFrame"/> class with prepared native component owners.
    /// </summary>
    /// <param name="image">The image's color and sample declarations.</param>
    /// <param name="header">The serialized frame geometry and reconstruction parameters.</param>
    /// <param name="modular">The modular color or extra-channel owner transferred to this frame.</param>
    /// <param name="varDct">The prepared VarDCT owner transferred to this frame, or null for modular color.</param>
    /// <param name="dcFactors">The factors that restore modular XYB integer samples to native float units.</param>
    /// <param name="patches">The complete parsed patch owner transferred to this frame, or null when absent.</param>
    /// <param name="splines">The complete parsed spline owner transferred to this frame, or null when absent.</param>
    /// <param name="noise">The complete decoded noise strengths, or the inactive default when absent.</param>
    /// <remarks>
    /// The caller retains ownership if construction fails. A successful constructor transfers the component, patch and spline owners.
    /// The decoder then completes AC groups and modular inverses. Retained spatial reconstruction follows group decoding;
    /// sequential output instead consumes completed group rows while this frame retains their owners.
    /// </remarks>
    public JxlFrame(
        JxlHeader image,
        JxlFrameHeader header,
        JxlModularDecoder modular,
        JxlVarDctDecoder? varDct,
        Vector3 dcFactors,
        JxlPatches? patches,
        JxlSplines? splines,
        JxlNoise noise)
    {
        this.image = image;
        this.Header = header;
        this.Modular = modular;
        this.VarDct = varDct;
        this.dcFactors = dcFactors;
        this.patches = patches;
        this.splines = splines;
        this.noise = noise;

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
    /// Gets the borrowed reconstructed float color owners, or an empty span when conversion reads integer or group rows.
    /// </summary>
    public ReadOnlySpan<Buffer2D<float>> ReconstructedColors => this.reconstructedColors;

    /// <summary>
    /// Gets the borrowed full-resolution float alpha plane, or null when the pixel converter reads the original modular channel.
    /// </summary>
    public Buffer2D<float>? UpsampledAlpha => this.upsampledAlpha;

    /// <summary>
    /// Gets the borrowed full-resolution float black plane, or null when the pixel converter reads the original modular channel.
    /// </summary>
    public Buffer2D<float>? UpsampledBlack => this.upsampledBlack;

    /// <summary>
    /// Completes chroma expansion, restoration, native image features and upsampling before LF storage or displayed output.
    /// </summary>
    /// <param name="allocator">The tracked allocator for the same component planes and row workspaces used by both outputs.</param>
    /// <param name="visibleFrame">The visible-frame noise seed counter, advanced before this frame.</param>
    /// <param name="nonvisibleFrame">The nonvisible-frame noise seed counter, advanced or reset before this frame.</param>
    /// <param name="cancellationToken">The token checked between converted rows and reconstruction passes.</param>
    /// <remarks>
    /// Call once after section decoding. Failure leaves every acquired owner attached to this frame for disposal.
    /// No pixel bounds, color conversion or alpha unassociation apply to these native component samples.
    /// </remarks>
    public void Reconstruct(
        MemoryAllocator allocator,
        uint visibleFrame,
        uint nonvisibleFrame,
        CancellationToken cancellationToken)
    {
        JxlFrameHeader frame = this.Header;
        JxlModularDecoder modular = this.Modular;
        JxlVarDctDecoder? varDct = this.VarDct;
        int colorFactor = frame.Upsampling;
        int alphaFactor = this.AlphaExtra < 0 ? 1 : frame.ExtraChannelUpsampling[this.AlphaExtra];
        int blackFactor = this.BlackExtra < 0 ? 1 : frame.ExtraChannelUpsampling[this.BlackExtra];
        int maximumFactor = Math.Max(colorFactor, Math.Max(alphaFactor, blackFactor));
        bool retainExtras = this.patches is not null || frame.CanBeReferenced;
        bool lateExtraUpsampling = colorFactor > 1;

        foreach (byte factor in frame.ExtraChannelUpsampling)
        {
            if (retainExtras)
            {
                maximumFactor = Math.Max(maximumFactor, factor);
            }

            lateExtraUpsampling &= factor == colorFactor;
        }

        bool restore = frame.RestorationFilter.Gaborish || frame.RestorationFilter.Iterations != 0;
        bool subsampled = false;
        for (int component = 0; component < frame.HorizontalSamplingShifts.Length; component++)
        {
            subsampled |= frame.HorizontalSamplingShifts[component] != 0 || frame.VerticalSamplingShifts[component] != 0;
        }

        if (varDct is not null)
        {
            varDct.ApplyRestoration(frame, allocator, cancellationToken);

            // Restoration expands every color to CodingSize. Transfer the existing plane owners before
            // further reconstruction so the frame owns one set of colors through output or reference storage.
            // The same owner array transfers with the planes; no new allocation or sample copy is needed.
            this.reconstructedColors = varDct.TakeReconstructedChannels();
        }
        else if (this.image.UsesXybTransform || colorFactor > 1 || subsampled || restore || this.patches is not null || frame.CanBeReferenced
            || this.splines is not null || this.noise.HasAny
            || frame.Type == JxlFrameHeader.FrameType.LowFrequency)
        {
            // Native modular RGB stays integer for ordinary output. Patches, references and spatial operations require float planes.
            // Assign completed allocations before filtering, so a failed filter remains covered by frame disposal.
            Buffer2D<float>[] colors = this.image.UsesXybTransform
                ? modular.ReconstructXyb(frame, this.dcFactors, cancellationToken)
                : this.ReconstructNativeModularColors(allocator, cancellationToken);

            this.reconstructedColors = colors;
            this.RestoreModularColors(colors, allocator, cancellationToken);
        }

        if (retainExtras)
        {
            // Patches can modify every encoded extra, not only the displayed alpha or CMYK black.
            // Normalize once into final-capacity owners, and let display conversion borrow those same planes.
            this.PrepareNativeExtraChannels(allocator, cancellationToken);
        }

        // One workspace spans early extra expansion, native frame features and common color/extra expansion.
        // Equal extra and color factors defer extra expansion until after patches and splines.
        // Other extra factors expand first. These native samples retain their values without pixel or alpha bounds.
        JxlUpsampler? upsampler = null;
        try
        {
            if (maximumFactor > 1)
            {
                Size maximumSourceSize = colorFactor > 1 ? frame.CodingSize : default;
                if (retainExtras)
                {
                    for (int extra = 0; extra < this.nativeExtras.Length; extra++)
                    {
                        if (frame.ExtraChannelUpsampling[extra] > 1)
                        {
                            Size size = modular.GetChannel(modular.ColorChannelCount + extra).Size;
                            maximumSourceSize = new Size(Math.Max(maximumSourceSize.Width, size.Width), Math.Max(maximumSourceSize.Height, size.Height));
                        }
                    }
                }
                else if (alphaFactor > 1)
                {
                    Size size = modular.GetChannel(modular.ColorChannelCount + this.AlphaExtra).Size;
                    maximumSourceSize = new Size(Math.Max(maximumSourceSize.Width, size.Width), Math.Max(maximumSourceSize.Height, size.Height));
                }

                if (!retainExtras && blackFactor > 1)
                {
                    Size size = modular.GetChannel(modular.ColorChannelCount + this.BlackExtra).Size;
                    maximumSourceSize = new Size(Math.Max(maximumSourceSize.Width, size.Width), Math.Max(maximumSourceSize.Height, size.Height));
                }

                upsampler = new JxlUpsampler(this.image, frame.OutputSize, maximumSourceSize, maximumFactor, allocator);
                if (!lateExtraUpsampling)
                {
                    this.UpsampleExtraChannels(upsampler, allocator, cancellationToken);
                }
            }

            if (this.patches is JxlPatches patches)
            {
                // Patches read saved pre-transform samples. Apply them after any required early
                // extra expansion, while color still has coding resolution and precedes splines.
                patches.Apply(this.reconstructedColors, this.nativeExtras, this.image, frame.CodingSize, allocator, cancellationToken);
                patches.Dispose();
                this.patches = null;
            }

            if (this.splines is JxlSplines splines)
            {
                // Splines modify restored native components at coding resolution. Common upsampling and
                // the color transform must see these contributions. VarDCT supplies its parsed base correlation;
                // modular frames retain the default X=0 and B=1 ratios because they have no VarDCT map,
                // even when their stored components use native RGB instead of XYB.
                Vector2 correlation = varDct is null
                    ? Vector2.UnitY
                    : new Vector2(varDct.BaseCorrelationX, varDct.BaseCorrelationB);

                splines.Apply(this.reconstructedColors, correlation, cancellationToken);

                // Every curve has reached its last consumer. Return its tracked storage before upsampling
                // or pixel conversion. On failure, the attached owner remains covered by frame disposal.
                splines.Dispose();
                this.splines = null;
            }

            if (upsampler is not null)
            {
                if (colorFactor > 1)
                {
                    // Restoration leaves all color planes at CodingSize. Each owner already reserves
                    // OutputSize, so expansion reuses the same storage for modular and VarDCT samples.
                    foreach (Buffer2D<float> plane in this.reconstructedColors)
                    {
                        upsampler.Apply(plane, frame.CodingSize, colorFactor, cancellationToken);
                    }
                }

                if (lateExtraUpsampling)
                {
                    this.UpsampleExtraChannels(upsampler, allocator, cancellationToken);
                }
            }
        }
        finally
        {
            upsampler?.Dispose();
        }

        // Noise uses final-resolution native components. Applying it before upsampling changes its spatial
        // spectrum; applying it after color conversion changes its channel correlation and strength lookup.
        // The generator uses frame counters and output-space group origins, as native Random3Planes does.
        Vector2 noiseCorrelation = varDct is null ? Vector2.UnitY : new Vector2(varDct.BaseCorrelationX, varDct.BaseCorrelationB);
        this.noise.Apply(
            this.reconstructedColors,
            frame.OutputSize,
            frame.GroupSize,
            noiseCorrelation,
            visibleFrame,
            nonvisibleFrame,
            allocator,
            cancellationToken);
    }

    /// <summary>
    /// Transfers the completed native color planes to decoder-owned reference storage without copying samples.
    /// </summary>
    /// <returns>The transferred plane owners. The decoder must dispose them after their final dependent frame.</returns>
    /// <remarks>
    /// Complete <see cref="Reconstruct"/> and every borrowed color read before transfer. Do not read or reconstruct the colors afterward.
    /// The existing owner array transfers with its planes. This operation does not allocate storage.
    /// </remarks>
    public Buffer2D<float>[] TakeReconstructedChannels()
    {
        Buffer2D<float>[] result = this.reconstructedColors;
        this.reconstructedColors = [];
        return result;
    }

    /// <summary>
    /// Transfers this frame's completed color and extra planes to one ordinary reference owner.
    /// </summary>
    /// <param name="savedBeforeColorTransform">Whether the transferred samples precede the frame color transform.</param>
    /// <returns>The complete reference owner. The decoder controls its slot lifetime.</returns>
    /// <remarks>
    /// Complete reconstruction and every borrowed pixel read before transfer. This operation does not convert samples.
    /// The caller supplies their actual transform domain. Failure leaves extra channels on this frame and releases any detached colors.
    /// </remarks>
    public JxlReferenceFrame TakeReferenceFrame(bool savedBeforeColorTransform)
    {
        Buffer2D<float>[] colors = this.TakeReconstructedChannels();

        try
        {
            JxlReferenceFrame result = new(colors, this.nativeExtras, this.Header.OutputSize, this.Header.Origin, savedBeforeColorTransform);

            // The reference owns every retained extra. The display selections only borrowed entries
            // from that array, so clear both aliases before this frame's remaining owners are disposed.
            this.nativeExtras = [];
            this.upsampledAlpha = null;
            this.upsampledBlack = null;
            return result;
        }
        catch
        {
            foreach (Buffer2D<float> plane in colors)
            {
                plane.Dispose();
            }

            throw;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.isDisposed)
        {
            return;
        }

        if (this.nativeExtras.Length == 0)
        {
            this.upsampledBlack?.Dispose();
            this.upsampledAlpha?.Dispose();
        }
        else
        {
            foreach (Buffer2D<float> plane in this.nativeExtras)
            {
                plane.Dispose();
            }
        }

        this.patches?.Dispose();
        this.splines?.Dispose();
        foreach (Buffer2D<float> plane in this.reconstructedColors)
        {
            plane.Dispose();
        }

        this.VarDct?.Dispose();
        this.Modular.Dispose();
        this.isDisposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Normalizes a retained color or extra plane using its declared modular storage width.
    /// </summary>
    /// <param name="source">The borrowed reconstructed samples.</param>
    /// <param name="destination">The float plane with room for the coded rectangle.</param>
    /// <param name="bitDepth">The component's integer or floating-point sample representation.</param>
    /// <param name="cancellationToken">The token checked between rows.</param>
    private static void NormalizeChannel(JxlModularChannel source, Buffer2D<float> destination, JxlBitDepth bitDepth, CancellationToken cancellationToken)
    {
        // The image selects its carrier width before decoding. Keep that choice outside the row loop;
        // both branches use the same sample operators and write only the original coded rectangle.
        if (source.Uses16BitStorage)
        {
            Buffer2DRegion<short> region = source.Region16;
            for (int y = 0; y < source.Height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                JxlSampleConverter.Convert(region.DangerousGetRowSpan(y), destination.DangerousGetRowSpan(y)[..source.Width], bitDepth);
            }
        }
        else
        {
            Buffer2DRegion<int> region = source.Region;
            for (int y = 0; y < source.Height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                JxlSampleConverter.Convert(region.DangerousGetRowSpan(y), destination.DangerousGetRowSpan(y)[..source.Width], bitDepth);
            }
        }
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

        // LF slots, restoration, splines and noise require three distinct component planes.
        // Native modular gray initializes all three before component-specific filtering or additions.
        // Unfiltered ordinary gray needs one owner; the color converter borrows that row three times.
        bool restore = frame.RestorationFilter.Gaborish || frame.RestorationFilter.Iterations != 0;
        int planeCount = restore || this.patches is not null || this.splines is not null || this.noise.HasAny
            || frame.Type == JxlFrameHeader.FrameType.LowFrequency ? 3 : colorCount;

        Buffer2D<float>[] planes = new Buffer2D<float>[planeCount];
        int allocated = 0;
        try
        {
            for (int component = 0; component < planeCount; component++)
            {
                Buffer2D<float> plane = allocator.Allocate2D<float>(frame.OutputSize);
                planes[component] = plane;
                allocated++;
                JxlModularChannel source = modular.GetChannel(colorCount == 1 ? 0 : component);
                NormalizeChannel(source, plane, header.Metadata.BitDepth, cancellationToken);
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
    /// Normalizes every encoded extra into a distinct final-capacity float owner for patches or reference retention.
    /// </summary>
    /// <param name="allocator">The tracked allocator for the normalized component planes.</param>
    /// <param name="cancellationToken">The token checked between normalized rows.</param>
    /// <remarks>
    /// Only each source rectangle is initialized here. The shared upsampler completes the output rectangle at the required reconstruction boundary.
    /// The display alpha and black selections borrow these owners; they do not acquire another plane.
    /// </remarks>
    private void PrepareNativeExtraChannels(MemoryAllocator allocator, CancellationToken cancellationToken)
    {
        int extraCount = this.image.Metadata.ExtraChannels.Count;
        Buffer2D<float>[] extras = extraCount == 0 ? [] : new Buffer2D<float>[extraCount];
        int allocated = 0;

        try
        {
            for (int extra = 0; extra < extras.Length; extra++)
            {
                Buffer2D<float> plane = allocator.Allocate2D<float>(this.Header.OutputSize);
                extras[extra] = plane;
                allocated++;
                JxlModularChannel source = this.Modular.GetChannel(this.Modular.ColorChannelCount + extra);
                NormalizeChannel(source, plane, this.image.Metadata.ExtraChannels[extra].BitDepth, cancellationToken);
            }

            // Publish only the completed array. Until this point, failure releases every acquired
            // plane in the catch, and the frame has no partial extra-channel array to dispose.
            this.nativeExtras = extras;
            this.upsampledAlpha = this.AlphaExtra < 0 ? null : extras[this.AlphaExtra];
            this.upsampledBlack = this.BlackExtra < 0 ? null : extras[this.BlackExtra];
        }
        catch
        {
            for (int extra = 0; extra < allocated; extra++)
            {
                extras[extra].Dispose();
            }

            throw;
        }
    }

    /// <summary>
    /// Expands retained native extras, or the selected display alpha and black channels, with one shared workspace.
    /// </summary>
    /// <param name="upsampler">The ready workspace covering every selected source and factor.</param>
    /// <param name="allocator">The tracked allocator for display-only planes that have not been normalized.</param>
    /// <param name="cancellationToken">The token checked between source and output rows.</param>
    private void UpsampleExtraChannels(JxlUpsampler upsampler, MemoryAllocator allocator, CancellationToken cancellationToken)
    {
        if (this.nativeExtras.Length != 0)
        {
            for (int extra = 0; extra < this.nativeExtras.Length; extra++)
            {
                int factor = this.Header.ExtraChannelUpsampling[extra];
                if (factor > 1)
                {
                    JxlModularChannel source = this.Modular.GetChannel(this.Modular.ColorChannelCount + extra);
                    upsampler.Apply(this.nativeExtras[extra], source.Size, factor, cancellationToken);
                }
            }

            return;
        }

        if (this.AlphaExtra >= 0 && this.Header.ExtraChannelUpsampling[this.AlphaExtra] > 1)
        {
            this.upsampledAlpha = this.UpsampleExtraChannel(
                this.AlphaExtra, upsampler, this.Header.ExtraChannelUpsampling[this.AlphaExtra], allocator, cancellationToken);
        }

        if (this.BlackExtra >= 0 && this.Header.ExtraChannelUpsampling[this.BlackExtra] > 1)
        {
            this.upsampledBlack = this.UpsampleExtraChannel(
                this.BlackExtra, upsampler, this.Header.ExtraChannelUpsampling[this.BlackExtra], allocator, cancellationToken);
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
        JxlModularChannel source = this.Modular.GetChannel(this.Modular.ColorChannelCount + extraIndex);
        Buffer2D<float> plane = allocator.Allocate2D<float>(upsampler.TargetSize);
        try
        {
            NormalizeChannel(source, plane, this.image.Metadata.ExtraChannels[extraIndex].BitDepth, cancellationToken);

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
