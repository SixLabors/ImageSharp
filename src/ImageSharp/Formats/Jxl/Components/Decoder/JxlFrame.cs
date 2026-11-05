// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Numerics.Tensors;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Jxl.Components.ColorConverters;
using SixLabors.ImageSharp.Formats.Jxl.Components.Modular;
using SixLabors.ImageSharp.Formats.Jxl.Components.VarDct;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;
using BlendMode = SixLabors.ImageSharp.Formats.Jxl.Components.Decoder.JxlBlending.BlendMode;

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
    /// Indicates whether encoded extra channels include a spot color for displayed output.
    /// </summary>
    private readonly bool hasSpotColors;

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
    /// The displayed spot channels in encoded order. Planes borrow nativeExtras when spotPlanesBorrowed is true;
    /// otherwise only reduced channels own a plane, and full-resolution channels retain no float image storage.
    /// </summary>
    private (int Channel, JxlExtraChannelMetadata Metadata, Buffer2D<float>? Plane)[] spotColors = [];

    /// <summary>
    /// Indicates whether the spot descriptors borrow their planes from nativeExtras instead of owning them.
    /// </summary>
    private bool spotPlanesBorrowed;

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
        this.Size = header.OutputSize;
        this.Origin = header.Origin;
        this.Modular = modular;
        this.VarDct = varDct;
        this.dcFactors = dcFactors;
        this.patches = patches;
        this.splines = splines;
        this.noise = noise;

        this.AlphaExtra = -1;
        this.BlackExtra = -1;

        for (int i = 0; i < image.Metadata.ExtraChannels.Count; i++)
        {
            JxlExtraChannelType type = image.Metadata.ExtraChannels[i].Type;
            this.hasSpotColors |= type == JxlExtraChannelType.SpotColor;

            // LF storage contains native color only. It does not consume display alpha or a CMYK black channel.
            if (header.Type == JxlFrameHeader.FrameType.LowFrequency)
            {
                continue;
            }

            if (this.AlphaExtra < 0 && type == JxlExtraChannelType.Alpha)
            {
                this.AlphaExtra = i;
            }

            if (this.BlackExtra < 0 && type == JxlExtraChannelType.Black)
            {
                this.BlackExtra = i;
            }
        }

        if (header.Type != JxlFrameHeader.FrameType.LowFrequency)
        {
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
    /// Gets the current completed component dimensions, including canvas composition when applied.
    /// </summary>
    public Size Size { get; private set; }

    /// <summary>
    /// Gets the current component origin on the image canvas. A composed canvas has a zero origin.
    /// </summary>
    public Point Origin { get; private set; }

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
    /// Gets a value indicating whether the image declares spot colors for displayed output.
    /// </summary>
    public bool HasSpotColors => this.hasSpotColors;

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
        bool retainExtras = this.patches is not null || frame.CanBeReferenced || frame.NeedsBlending;
        bool lateExtraUpsampling = colorFactor > 1;
        int spotCount = 0;

        for (int extra = 0; extra < frame.ExtraChannelUpsampling.Length; extra++)
        {
            int factor = frame.ExtraChannelUpsampling[extra];
            bool spotColor = frame.IsDisplayed && this.image.Metadata.ExtraChannels[extra].Type == JxlExtraChannelType.SpotColor;
            if (retainExtras || spotColor)
            {
                maximumFactor = Math.Max(maximumFactor, factor);
            }

            if (spotColor)
            {
                spotCount++;
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

        if (spotCount != 0)
        {
            // Pointwise spot mixing uses pixel-converter row storage. Only a reduced spot channel
            // needs its own float plane; an existing retained extra supplies that plane when present.
            this.PrepareSpotColors(spotCount, allocator, cancellationToken);
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

                if (!this.spotPlanesBorrowed)
                {
                    foreach ((int channel, JxlExtraChannelMetadata _, Buffer2D<float>? plane) in this.spotColors)
                    {
                        if (plane is not null)
                        {
                            Size size = modular.GetChannel(modular.ColorChannelCount + channel).Size;
                            maximumSourceSize = new Size(Math.Max(maximumSourceSize.Width, size.Width), Math.Max(maximumSourceSize.Height, size.Height));
                        }
                    }
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
    /// Composes completed original-device components onto the image canvas using the selected reference backgrounds.
    /// </summary>
    /// <param name="references">The four ordinary reference slots, retained for the complete composition call.</param>
    /// <param name="canvasSize">The image canvas dimensions.</param>
    /// <param name="allocator">The tracked allocator for the canvas planes and bounded block scratch.</param>
    /// <param name="cancellationToken">The token checked between component rows.</param>
    /// <remarks>
    /// The caller restores coding color transforms before this call. Backgrounds must contain original-device samples.
    /// Each extra channel selects its own reference source, including extra channels used as blend weights.
    /// New canvas owners publish only after every row succeeds. Failure leaves the foreground owners unchanged.
    /// </remarks>
    public void Compose(ReadOnlySpan<JxlReferenceFrame?> references, Size canvasSize, MemoryAllocator allocator, CancellationToken cancellationToken)
    {
        JxlFrameHeader frame = this.Header;
        int extraCount = this.image.Metadata.ExtraChannels.Count;
        JxlFrameHeader.BlendingInfo colorInfo = frame.Blending;
        JxlBlending colorOperation = CreateFrameBlending(colorInfo);
        bool hasAlpha = this.AlphaExtra >= 0;
        if (!hasAlpha)
        {
            // Without an alpha channel, over replaces the background and weighted addition
            // becomes ordinary addition. Select this equation before reserving packed scratch.
            BlendMode opaqueMode = colorOperation.Mode switch
            {
                BlendMode.BlendAbove => BlendMode.Replace,
                BlendMode.AlphaWeightedAddAbove => BlendMode.Add,
                _ => colorOperation.Mode
            };

            colorOperation = new JxlBlending(opaqueMode, colorInfo.AlphaChannel, colorInfo.Clamp);
        }

        uint usedSources = 1U << frame.Blending.Source;
        foreach (JxlFrameHeader.BlendingInfo extra in frame.ExtraChannelBlending)
        {
            usedSources |= 1U << extra.Source;
        }

        // An absent slot is an all-zero background. Every populated source must cover the canvas
        // at its origin. Only the color source must follow the coding color transform: extra samples
        // are unchanged by that transform and can also come from a pre-transform reference.
        for (int slot = 0; slot < references.Length; slot++)
        {
            JxlReferenceFrame? reference = references[slot];
            if ((usedSources & (1U << slot)) != 0 && reference is not null
                && ((slot == frame.Blending.Source && reference.SavedBeforeColorTransform) || reference.Origin != Point.Empty
                    || reference.Size.Width < canvasSize.Width || reference.Size.Height < canvasSize.Height))
            {
                throw new InvalidImageContentException("The JPEG XL composition background is not a complete original-color canvas.");
            }
        }

        // Origins are signed. Widen before adding dimensions and intersecting the crop with the
        // canvas; only this intersection is narrowed to row and span indices.
        int left = (int)Math.Clamp((long)this.Origin.X, 0, canvasSize.Width);
        int top = (int)Math.Clamp((long)this.Origin.Y, 0, canvasSize.Height);
        int right = (int)Math.Clamp((long)this.Origin.X + this.Size.Width, 0, canvasSize.Width);
        int bottom = (int)Math.Clamp((long)this.Origin.Y + this.Size.Height, 0, canvasSize.Height);

        // Four maximum-width float registers bound the temporary block. Base scratch uses nine
        // ranges (2304 bytes); source-over alone adds two packed color blocks (2048 bytes).
        int blockLength = 4 * Vector512<float>.Count;
        int packedLength = colorOperation.Mode == BlendMode.BlendAbove ? 4 * blockLength : 0;

        Buffer2D<float>[] colors = new Buffer2D<float>[3];
        Buffer2D<float>[] extras = extraCount == 0 ? [] : new Buffer2D<float>[extraCount];
        int allocatedColors = 0;
        int allocatedExtras = 0;

        try
        {
            for (int channel = 0; channel < colors.Length; channel++)
            {
                colors[channel] = allocator.Allocate2D<float>(canvasSize);
                allocatedColors++;
            }

            for (int channel = 0; channel < extras.Length; channel++)
            {
                extras[channel] = allocator.Allocate2D<float>(canvasSize);
                allocatedExtras++;
            }

            // Base scratch holds [three normalized foreground planes | bounded | complement | alpha | contribution | reciprocal | zero].
            // Each range is 64 floats (256 bytes). Source-over appends packed background and foreground blocks of
            // 64 Vector4 values (1024 bytes each), giving 4352 bytes only when the selected equation needs them.
            // Foreground extras already have immutable float planes and need no scratch copies.
            using IMemoryOwner<float> owner = allocator.Allocate<float>((9 * blockLength) + (2 * packedLength));
            Span<float> workspace = owner.Memory.Span;
            Span<float> bounded = workspace.Slice(3 * blockLength, blockLength);
            Span<float> complement = workspace.Slice(4 * blockLength, blockLength);
            Span<float> alpha = workspace.Slice(5 * blockLength, blockLength);
            Span<float> contribution = workspace.Slice(6 * blockLength, blockLength);
            Span<float> reciprocal = workspace.Slice(7 * blockLength, blockLength);
            Span<float> zero = workspace.Slice(8 * blockLength, blockLength);
            Span<Vector4> backgroundColors = MemoryMarshal.Cast<float, Vector4>(workspace.Slice(9 * blockLength, packedLength));
            Span<Vector4> foregroundColors = MemoryMarshal.Cast<float, Vector4>(workspace.Slice((9 * blockLength) + packedLength, packedLength));
            zero.Clear();

            // Extras have independent background selections and equations. Complete them first;
            // the color equation can later replace its selected weight with the combined output alpha.
            for (int channel = 0; channel < extraCount; channel++)
            {
                JxlFrameHeader.BlendingInfo info = frame.ExtraChannelBlending[channel];
                JxlBlending operation = CreateFrameBlending(info);
                JxlReferenceFrame? background = references[info.Source];
                Buffer2DRegion<float> backgroundPlane = background is null ? default : background.GetExtraChannel(channel);
                bool weighted = operation.Mode is BlendMode.BlendAbove or BlendMode.AlphaWeightedAddAbove;
                int weight = info.AlphaChannel;
                JxlReferenceFrame? weightReference = weighted ? references[frame.ExtraChannelBlending[weight].Source] : null;
                Buffer2DRegion<float> backgroundWeightPlane = weightReference is null ? default : weightReference.GetExtraChannel(weight);
                bool associated = weighted && this.image.Metadata.ExtraChannels[weight].AlphaRepresentation == PixelAlphaRepresentation.Associated;
                bool selectedWeight = channel == weight;
                for (int y = 0; y < canvasSize.Height; y++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Span<float> destination = extras[channel].DangerousGetRowSpan(y);
                    ReadOnlySpan<float> backgroundRow = background is null ? [] : backgroundPlane.DangerousGetRowSpan(y)[..canvasSize.Width];
                    if (background is null)
                    {
                        destination.Clear();
                    }
                    else
                    {
                        backgroundRow.CopyTo(destination);
                    }

                    if (y < top || y >= bottom)
                    {
                        continue;
                    }

                    int sourceY = (int)((long)y - this.Origin.Y);
                    ReadOnlySpan<float> foregroundRow = this.nativeExtras[channel].DangerousGetRowSpan(sourceY);
                    ReadOnlySpan<float> backgroundWeightRow = weightReference is null ? [] : backgroundWeightPlane.DangerousGetRowSpan(y);
                    ReadOnlySpan<float> foregroundWeightRow = weighted ? this.nativeExtras[weight].DangerousGetRowSpan(sourceY) : [];
                    for (int x = left; x < right;)
                    {
                        int count = Math.Min(blockLength, right - x);
                        int sourceX = (int)((long)x - this.Origin.X);
                        ReadOnlySpan<float> backgroundSamples = background is null ? zero[..count] : backgroundRow.Slice(x, count);
                        ReadOnlySpan<float> backgroundWeight = weightReference is null ? zero[..count] : backgroundWeightRow.Slice(x, count);
                        ReadOnlySpan<float> foregroundWeight = weighted ? foregroundWeightRow.Slice(sourceX, count) : [];
                        if (weighted)
                        {
                            operation.PrepareWeights(
                                associated,
                                selectedWeight,
                                backgroundWeight,
                                foregroundWeight,
                                bounded[..count],
                                complement[..count],
                                alpha[..count],
                                reciprocal[..count]);
                        }

                        operation.Apply(
                            associated,
                            selectedWeight,
                            backgroundSamples,
                            foregroundRow.Slice(sourceX, count),
                            backgroundWeight,
                            foregroundWeight,
                            destination.Slice(x, count),
                            bounded[..count],
                            complement[..count],
                            alpha[..count],
                            contribution[..count],
                            reciprocal[..count]);

                        x += count;
                    }
                }
            }

            bool colorWeighted = colorOperation.Mode is BlendMode.BlendAbove or BlendMode.AlphaWeightedAddAbove;
            int colorWeight = colorInfo.AlphaChannel;
            JxlReferenceFrame? colorReference = references[colorInfo.Source];
            Buffer2DRegion<float> background0 = colorReference is null ? default : colorReference.GetColorChannel(0);
            Buffer2DRegion<float> background1 = colorReference is null ? default : colorReference.GetColorChannel(1);
            Buffer2DRegion<float> background2 = colorReference is null ? default : colorReference.GetColorChannel(2);
            JxlReferenceFrame? colorWeightReference = colorWeighted ? references[frame.ExtraChannelBlending[colorWeight].Source] : null;
            Buffer2DRegion<float> colorWeightPlane = colorWeightReference is null ? default : colorWeightReference.GetExtraChannel(colorWeight);
            bool colorAssociated = colorWeighted && this.image.Metadata.ExtraChannels[colorWeight].AlphaRepresentation == PixelAlphaRepresentation.Associated;
            bool retainedColors = this.reconstructedColors.Length != 0;
            bool narrowColors = this.Modular.Uses16BitStorage;
            Buffer2DRegion<short> raw16Red = default;
            Buffer2DRegion<short> raw16Green = default;
            Buffer2DRegion<short> raw16Blue = default;
            Buffer2DRegion<int> raw32Red = default;
            Buffer2DRegion<int> raw32Green = default;
            Buffer2DRegion<int> raw32Blue = default;
            if (!retainedColors)
            {
                JxlModularChannel first = this.Modular.GetChannel(0);
                JxlModularChannel second = this.Modular.GetChannel(this.Modular.ColorChannelCount == 1 ? 0 : 1);
                JxlModularChannel third = this.Modular.GetChannel(this.Modular.ColorChannelCount == 1 ? 0 : 2);
                if (narrowColors)
                {
                    raw16Red = first.Region16;
                    raw16Green = second.Region16;
                    raw16Blue = third.Region16;
                }
                else
                {
                    raw32Red = first.Region;
                    raw32Green = second.Region;
                    raw32Blue = third.Region;
                }
            }

            for (int y = 0; y < canvasSize.Height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Span<float> red = colors[0].DangerousGetRowSpan(y);
                Span<float> green = colors[1].DangerousGetRowSpan(y);
                Span<float> blue = colors[2].DangerousGetRowSpan(y);
                ReadOnlySpan<float> backgroundRed = colorReference is null ? [] : background0.DangerousGetRowSpan(y)[..canvasSize.Width];
                ReadOnlySpan<float> backgroundGreen = colorReference is null ? [] : background1.DangerousGetRowSpan(y)[..canvasSize.Width];
                ReadOnlySpan<float> backgroundBlue = colorReference is null ? [] : background2.DangerousGetRowSpan(y)[..canvasSize.Width];
                if (colorReference is null)
                {
                    red.Clear();
                    green.Clear();
                    blue.Clear();
                }
                else
                {
                    backgroundRed.CopyTo(red);
                    backgroundGreen.CopyTo(green);
                    backgroundBlue.CopyTo(blue);
                }

                if (y < top || y >= bottom)
                {
                    continue;
                }

                int sourceY = (int)((long)y - this.Origin.Y);
                ReadOnlySpan<float> backgroundWeightRow = colorWeightReference is null ? [] : colorWeightPlane.DangerousGetRowSpan(y);
                ReadOnlySpan<float> foregroundWeightRow = colorWeighted ? this.nativeExtras[colorWeight].DangerousGetRowSpan(sourceY) : [];
                Span<float> combinedAlphaRow = colorOperation.Mode == BlendMode.BlendAbove ? extras[colorWeight].DangerousGetRowSpan(y) : [];

                // Borrow source rows before block traversal. A block does not resolve Memory.Span
                // again, and raw modular colors normalize only the visible samples into bounded scratch.
                ReadOnlySpan<float> foregroundRed = retainedColors ? this.reconstructedColors[0].DangerousGetRowSpan(sourceY) : [];
                int secondPlane = this.reconstructedColors.Length == 1 ? 0 : 1;
                int thirdPlane = this.reconstructedColors.Length == 1 ? 0 : 2;
                ReadOnlySpan<float> foregroundGreen = retainedColors ? this.reconstructedColors[secondPlane].DangerousGetRowSpan(sourceY) : [];
                ReadOnlySpan<float> foregroundBlue = retainedColors ? this.reconstructedColors[thirdPlane].DangerousGetRowSpan(sourceY) : [];
                ReadOnlySpan<short> source16Red = !retainedColors && narrowColors ? raw16Red.DangerousGetRowSpan(sourceY) : [];
                ReadOnlySpan<short> source16Green = !retainedColors && narrowColors ? raw16Green.DangerousGetRowSpan(sourceY) : [];
                ReadOnlySpan<short> source16Blue = !retainedColors && narrowColors ? raw16Blue.DangerousGetRowSpan(sourceY) : [];
                ReadOnlySpan<int> source32Red = !retainedColors && !narrowColors ? raw32Red.DangerousGetRowSpan(sourceY) : [];
                ReadOnlySpan<int> source32Green = !retainedColors && !narrowColors ? raw32Green.DangerousGetRowSpan(sourceY) : [];
                ReadOnlySpan<int> source32Blue = !retainedColors && !narrowColors ? raw32Blue.DangerousGetRowSpan(sourceY) : [];
                for (int x = left; x < right;)
                {
                    int count = Math.Min(blockLength, right - x);
                    int sourceX = (int)((long)x - this.Origin.X);
                    ReadOnlySpan<float> backgroundWeight = colorWeightReference is null ? zero[..count] : backgroundWeightRow.Slice(x, count);
                    ReadOnlySpan<float> foregroundWeight = colorWeighted ? foregroundWeightRow.Slice(sourceX, count) : [];
                    if (colorWeighted)
                    {
                        colorOperation.PrepareWeights(
                            colorAssociated,
                            false,
                            backgroundWeight,
                            foregroundWeight,
                            bounded[..count],
                            complement[..count],
                            alpha[..count],
                            reciprocal[..count]);
                    }

                    if (!retainedColors)
                    {
                        // The raw modular branch has no full foreground color image. Normalize
                        // each source block once before either component or shared RGB composition.
                        for (int component = 0; component < 3; component++)
                        {
                            Span<float> normalized = workspace.Slice(component * blockLength, count);
                            if (narrowColors)
                            {
                                ReadOnlySpan<short> source = component == 0 ? source16Red : component == 1 ? source16Green : source16Blue;
                                JxlSampleConverter.Convert(source.Slice(sourceX, count), normalized, this.image.Metadata.BitDepth);
                            }
                            else
                            {
                                ReadOnlySpan<int> source = component == 0 ? source32Red : component == 1 ? source32Green : source32Blue;
                                JxlSampleConverter.Convert(source.Slice(sourceX, count), normalized, this.image.Metadata.BitDepth);
                            }
                        }
                    }

                    ReadOnlySpan<float> inputRed = retainedColors ? foregroundRed.Slice(sourceX, count) : workspace[..count];
                    ReadOnlySpan<float> inputGreen = retainedColors ? foregroundGreen.Slice(sourceX, count) : workspace.Slice(blockLength, count);
                    ReadOnlySpan<float> inputBlue = retainedColors ? foregroundBlue.Slice(sourceX, count) : workspace.Slice(2 * blockLength, count);
                    if (colorOperation.Mode == BlendMode.BlendAbove)
                    {
                        Span<Vector4> background = backgroundColors[..count];
                        Span<Vector4> foreground = foregroundColors[..count];

                        // The canvas and reference frames retain separate color planes. Join one
                        // complete block from each input as [C0, C1, C2, selected weight] for the
                        // shared blender. Empty background slots supply the initialized zero row.
                        // These scratch blocks do not alias either frame or the retained weight rows.
                        SimdUtils.InterleaveFloatPlanes(
                            colorReference is null ? zero[..count] : backgroundRed.Slice(x, count),
                            colorReference is null ? zero[..count] : backgroundGreen.Slice(x, count),
                            colorReference is null ? zero[..count] : backgroundBlue.Slice(x, count),
                            backgroundWeight,
                            background);
                        SimdUtils.InterleaveFloatPlanes(
                            inputRed,
                            inputGreen,
                            inputBlue,
                            colorOperation.Clamp ? bounded[..count] : foregroundWeight,
                            foreground);

                        colorOperation.ApplyColors(colorAssociated, background, foreground);
                        Span<float> outputRed = red.Slice(x, count);
                        Span<float> outputGreen = green.Slice(x, count);
                        Span<float> outputBlue = blue.Slice(x, count);

                        // Store the blended color back in canvas planes so reference saving and
                        // later frames keep the native layout. Ignore W: JXL's separately prepared
                        // combined weight is written to its selected extra only after all colors finish.
                        SimdUtils.DeinterleaveFloatPlanes(background, outputRed, outputGreen, outputBlue, []);
                        if (!colorAssociated)
                        {
                            // Convert the shared kernel's weighted numerator to straight components.
                            // JXL uses the reciprocal only for positive combined weights; otherwise
                            // the prepared multiplier is zero. This leaves the pixel alpha contract unchanged.
                            TensorPrimitives.Multiply(outputRed, reciprocal[..count], outputRed);
                            TensorPrimitives.Multiply(outputGreen, reciprocal[..count], outputGreen);
                            TensorPrimitives.Multiply(outputBlue, reciprocal[..count], outputBlue);
                        }
                    }
                    else
                    {
                        for (int component = 0; component < 3; component++)
                        {
                            ReadOnlySpan<float> foreground = component == 0 ? inputRed : component == 1 ? inputGreen : inputBlue;
                            ReadOnlySpan<float> backgroundRow = component == 0 ? backgroundRed : component == 1 ? backgroundGreen : backgroundBlue;
                            Span<float> destination = component == 0 ? red : component == 1 ? green : blue;

                            colorOperation.Apply(
                                colorAssociated,
                                false,
                                colorReference is null ? zero[..count] : backgroundRow.Slice(x, count),
                                foreground,
                                backgroundWeight,
                                foregroundWeight,
                                destination.Slice(x, count),
                                bounded[..count],
                                complement[..count],
                                alpha[..count],
                                contribution[..count],
                                reciprocal[..count]);
                        }
                    }

                    if (colorOperation.Mode == BlendMode.BlendAbove)
                    {
                        // Every color component has read the unchanged input weights. Only now
                        // replace the selected extra's output with the combined color alpha.
                        alpha[..count].CopyTo(combinedAlphaRow.Slice(x, count));
                    }

                    x += count;
                }
            }
        }
        catch
        {
            for (int channel = 0; channel < allocatedColors; channel++)
            {
                colors[channel].Dispose();
            }

            for (int channel = 0; channel < allocatedExtras; channel++)
            {
                extras[channel].Dispose();
            }

            throw;
        }

        // All new canvas rows are complete. Release distinct foreground owners only after success;
        // saved backgrounds remain owned by their reference slots and were never modified.
        foreach (Buffer2D<float> plane in this.reconstructedColors)
        {
            plane.Dispose();
        }

        if (this.nativeExtras.Length == 0)
        {
            this.upsampledAlpha?.Dispose();
            this.upsampledBlack?.Dispose();
        }
        else
        {
            foreach (Buffer2D<float> plane in this.nativeExtras)
            {
                plane.Dispose();
            }
        }

        if (!this.spotPlanesBorrowed)
        {
            foreach ((int _, JxlExtraChannelMetadata _, Buffer2D<float>? plane) in this.spotColors)
            {
                plane?.Dispose();
            }
        }

        this.reconstructedColors = colors;
        this.nativeExtras = extras;
        this.upsampledAlpha = this.AlphaExtra < 0 ? null : extras[this.AlphaExtra];
        this.upsampledBlack = this.BlackExtra < 0 ? null : extras[this.BlackExtra];
        for (int i = 0; i < this.spotColors.Length; i++)
        {
            // Rebind the existing descriptor array without allocating or copying any composed plane.
            (int channel, JxlExtraChannelMetadata metadata, _) = this.spotColors[i];
            this.spotColors[i] = (channel, metadata, extras[channel]);
        }

        this.spotPlanesBorrowed = true;
        this.Size = canvasSize;
        this.Origin = Point.Empty;
    }

    /// <summary>
    /// Adds encoded spot colors to one restored color row without changing alpha or retained frame samples.
    /// </summary>
    /// <param name="row">The output-resolution row index.</param>
    /// <param name="red">The restored red or first device component, replaced by its mixed values.</param>
    /// <param name="green">The restored green or second device component, with the same row length.</param>
    /// <param name="blue">The restored blue or third device component, with the same row length.</param>
    /// <param name="workspace">Three distinct row-length scratch ranges, disjoint from the component rows.</param>
    /// <remarks>
    /// The pixel converter restores coding XYB or YCbCr before this call and owns its row and scratch storage.
    /// ICC conversion and destination pixel bounds follow spot mixing. Spot weights are image samples,
    /// not pixel alpha. This operation does not clip their range or allocate any storage.
    /// </remarks>
    public void ApplySpotColors(int row, Span<float> red, Span<float> green, Span<float> blue, Span<float> workspace)
    {
        // The first range holds normalized samples until their weights have been calculated, then
        // holds the complementary weights. The other ranges retain weights and one color contribution.
        // The caller borrows these spans once before row traversal and reuses them for every spot.
        int width = red.Length;
        Span<float> normalized = workspace[..width];
        Span<float> mix = workspace.Slice(width, width);
        Span<float> contribution = workspace.Slice(2 * width, width);

        // Native stage_spot and Rust SpotColorStage apply encoded spots in order. For each channel,
        // mix = opacity * sample and result = mix * spotColor + (1 - mix) * currentColor.
        // Keep the same device-space boundary and leave alpha and all other extra channels unchanged.
        foreach ((int channel, JxlExtraChannelMetadata metadata, Buffer2D<float>? plane) in this.spotColors)
        {
            ReadOnlySpan<float> samples;
            if (plane is not null)
            {
                samples = plane.DangerousGetRowSpan(row);
            }
            else
            {
                // Full-resolution spots normalize directly into row scratch. They never need a
                // float image plane, and retained or expanded spots already supply normalized samples.
                JxlModularChannel source = this.Modular.GetChannel(this.Modular.ColorChannelCount + channel);
                if (source.Uses16BitStorage)
                {
                    JxlSampleConverter.Convert(source.Region16.DangerousGetRowSpan(row), normalized, metadata.BitDepth);
                }
                else
                {
                    JxlSampleConverter.Convert(source.Region.DangerousGetRowSpan(row), normalized, metadata.BitDepth);
                }

                samples = normalized;
            }

            Vector4 spot = metadata.SpotColor;
            TensorPrimitives.Multiply(samples, spot.W, mix);
            TensorPrimitives.Subtract(1F, mix, normalized);
            TensorPrimitives.Multiply(mix, spot.X, contribution);
            TensorPrimitives.MultiplyAddEstimate(red, normalized, contribution, red);
            TensorPrimitives.Multiply(mix, spot.Y, contribution);
            TensorPrimitives.MultiplyAddEstimate(green, normalized, contribution, green);
            TensorPrimitives.Multiply(mix, spot.Z, contribution);
            TensorPrimitives.MultiplyAddEstimate(blue, normalized, contribution, blue);
        }
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
            JxlReferenceFrame result = new(colors, this.nativeExtras, this.Size, this.Origin, savedBeforeColorTransform);

            // The reference owns every retained extra. The display selections only borrowed entries
            // from that array, so clear both aliases before this frame's remaining owners are disposed.
            this.nativeExtras = [];
            this.upsampledAlpha = null;
            this.upsampledBlack = null;
            if (this.spotPlanesBorrowed)
            {
                // Spot descriptors also borrowed the transferred extras. Drop those aliases so
                // this frame cannot read or dispose reference-owned samples after the transfer.
                this.spotColors = [];
                this.spotPlanesBorrowed = false;
            }

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
        if (!this.spotPlanesBorrowed)
        {
            foreach ((int _, JxlExtraChannelMetadata _, Buffer2D<float>? plane) in this.spotColors)
            {
                plane?.Dispose();
            }
        }

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
    /// Maps the five frame equations to the shared patch and frame blending equations.
    /// </summary>
    /// <param name="info">The validated serialized frame instruction.</param>
    /// <returns>The matching shared equation with unchanged weight selection and clamp behavior.</returns>
    private static JxlBlending CreateFrameBlending(JxlFrameHeader.BlendingInfo info)
    {
        BlendMode mode = info.Mode switch
        {
            0 => BlendMode.Replace,
            1 => BlendMode.Add,
            2 => BlendMode.BlendAbove,
            3 => BlendMode.AlphaWeightedAddAbove,
            _ => BlendMode.Multiply
        };

        return new JxlBlending(mode, info.AlphaChannel, info.Clamp);
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
    /// Prepares displayed spot descriptors and only the float planes needed by reduced spot channels.
    /// </summary>
    /// <param name="spotCount">The exact number of displayed spots selected from image metadata.</param>
    /// <param name="allocator">The tracked allocator for spot-only planes when extras are not already retained.</param>
    /// <param name="cancellationToken">The token checked between normalized source rows.</param>
    /// <remarks>
    /// Call after modular reconstruction and retained-extra preparation. Borrowed planes stay owned by nativeExtras;
    /// otherwise this frame owns each nonnull plane. Publication follows complete normalization so failure releases partial allocations.
    /// </remarks>
    private void PrepareSpotColors(int spotCount, MemoryAllocator allocator, CancellationToken cancellationToken)
    {
        (int Channel, JxlExtraChannelMetadata Metadata, Buffer2D<float>? Plane)[] spots =
            new (int Channel, JxlExtraChannelMetadata Metadata, Buffer2D<float>? Plane)[spotCount];

        bool borrowed = this.nativeExtras.Length != 0;
        int prepared = 0;
        try
        {
            for (int extra = 0; extra < this.image.Metadata.ExtraChannels.Count; extra++)
            {
                JxlExtraChannelMetadata metadata = this.image.Metadata.ExtraChannels[extra];
                if (metadata.Type != JxlExtraChannelType.SpotColor)
                {
                    continue;
                }

                Buffer2D<float>? plane = borrowed ? this.nativeExtras[extra] : null;
                if (!borrowed && this.Header.ExtraChannelUpsampling[extra] > 1)
                {
                    plane = allocator.Allocate2D<float>(this.Header.OutputSize);
                }

                spots[prepared++] = (extra, metadata, plane);
                if (!borrowed && plane is not null)
                {
                    JxlModularChannel source = this.Modular.GetChannel(this.Modular.ColorChannelCount + extra);
                    NormalizeChannel(source, plane, metadata.BitDepth, cancellationToken);
                }
            }
        }
        catch
        {
            if (!borrowed)
            {
                for (int i = 0; i < prepared; i++)
                {
                    spots[i].Plane?.Dispose();
                }
            }

            throw;
        }

        this.spotColors = spots;
        this.spotPlanesBorrowed = borrowed;
    }

    /// <summary>
    /// Expands retained native extras, or the selected display and spot channels, with one shared workspace.
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

        foreach ((int channel, JxlExtraChannelMetadata _, Buffer2D<float>? plane) in this.spotColors)
        {
            if (plane is not null)
            {
                // Retained extras returned above. A remaining plane belongs only to a reduced spot;
                // reuse its normalized samples and the common upsampling workspace in place.
                JxlModularChannel source = this.Modular.GetChannel(this.Modular.ColorChannelCount + channel);
                upsampler.Apply(plane, source.Size, this.Header.ExtraChannelUpsampling[channel], cancellationToken);
            }
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
