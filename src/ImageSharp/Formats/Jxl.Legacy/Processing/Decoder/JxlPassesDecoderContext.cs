// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Dct;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Ans;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Group;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

/// <summary>
/// Owns the entropy tables and reusable reconstruction workspace for a codestream's progressive frames.
/// </summary>
/// <remarks>
/// The codestream decoder retains the borrowed common context across frames. This owner supplies one reusable group cache;
/// groups execute sequentially. It does not own output callbacks, output byte buffers, or a render-stage scheduler.
/// </remarks>
internal sealed class JxlPassesDecoderContext : IDisposable
{
    /// <summary>
    /// The number of extra block rows and columns on each side of the edge-preserving filter's sigma field.
    /// </summary>
    private const int SigmaPadding = 2;

    /// <summary>
    /// The configuration that owns numeric workspace.
    /// </summary>
    private readonly Configuration configuration;

    /// <summary>
    /// Owns kernels and scratch for the three encoded upsampling factors: two, four and eight.
    /// </summary>
    private readonly JxlUpsampler?[] upsamplers = new JxlUpsampler?[3];

    /// <summary>
    /// Allocates decoder-specific state after the common owner has prepared this frame's maps.
    /// </summary>
    /// <param name="configuration">The configuration that owns numeric storage.</param>
    /// <param name="frameHeader">The parsed frame header for the prepared common context.</param>
    /// <param name="shared">The borrowed codestream owner, which outlives this frame.</param>
    public JxlPassesDecoderContext(Configuration configuration, JxlFrameHeader frameHeader, JxlPassesCommonContext shared)
    {
        this.configuration = configuration;
        this.Shared = shared;
        this.GroupCache = new(configuration, frameHeader);

        try
        {
            this.PrepareFrameStorage(frameHeader);
        }
        catch
        {
            this.Sigma?.Dispose();
            this.GroupCache.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Gets the borrowed codestream owner of maps, quantization tables, and saved reference samples.
    /// </summary>
    public JxlPassesCommonContext Shared { get; }

    /// <summary>
    /// Gets the one coefficient and sample workspace reused by sequential groups.
    /// </summary>
    public JxlGroupDecoderCache GroupCache { get; }

    /// <summary>
    /// Gets the current frame's entropy code tables, one entry for each coefficient pass.
    /// </summary>
    public List<JxlAnsCode> Code { get; } = [];

    /// <summary>
    /// Gets the coefficient context maps corresponding to <see cref="Code"/>.
    /// </summary>
    public List<List<byte>> ContextMap { get; } = [];

    /// <summary>
    /// Gets the parsed X-channel dequantization-matrix multiplier.
    /// </summary>
    public float XDmMultiplier { get; private set; }

    /// <summary>
    /// Gets the parsed B-channel dequantization-matrix multiplier.
    /// </summary>
    public float BDmMultiplier { get; private set; }

    /// <summary>
    /// Gets the owned sigma field when the frame enables edge-preserving filtering.
    /// </summary>
    public JxlPlane<float>? Sigma { get; private set; }

    /// <summary>
    /// Gets or sets the visible-frame index used to seed deterministic frame noise.
    /// </summary>
    public int VisibleFrameIndex { get; set; }

    /// <summary>
    /// Gets or sets the nonvisible-frame index used to seed deterministic frame noise.
    /// </summary>
    public int NonvisibleFrameIndex { get; set; }

    /// <summary>
    /// Gets or sets the bit mask of transform strategies decoded from the frame's AC metadata.
    /// </summary>
    public int UsedAcs { get; set; }

    /// <summary>
    /// Gets owned coefficient storage when progressive reconstruction retains coefficients between passes.
    /// </summary>
    /// <remarks>
    /// The frame decoder selects accumulation before group traversal. A single-pass frame can decode into the reusable group cache instead.
    /// The image belongs to this context. Single-pass frames omit this image but still use the selected coefficient width.
    /// </remarks>
    public IJxlDctAcImage? Coefficients { get; private set; }

    /// <summary>
    /// Gets the integer width selected for AC decoding, including frames that need no retained coefficient image.
    /// </summary>
    public JxlDctAcType CoefficientType { get; private set; } = JxlDctAcType.Ac32;

    /// <summary>
    /// Gets the owned current-frame snapshot when later frames can reference its samples.
    /// </summary>
    /// <remarks>
    /// Moving its sample allocations to a common reference slot leaves this wrapper empty.
    /// Frames that cannot be referenced do not create this bundle.
    /// </remarks>
    public JxlImageBundle? FrameStorageForReferencing { get; private set; }

    /// <summary>
    /// Prepares decoder-specific storage after the common owner has prepared the next frame.
    /// </summary>
    /// <param name="frameHeader">The next parsed frame header from the same codestream.</param>
    public void PrepareFrame(JxlFrameHeader frameHeader)
    {
        this.GroupCache.PrepareFrame(frameHeader);
        this.PrepareFrameStorage(frameHeader);
    }

    /// <summary>
    /// Gets the reusable upsampler for a factor already validated by the frame header.
    /// </summary>
    /// <param name="factor">The encoded factor: two, four or eight.</param>
    /// <returns>The prepared kernels and allocator-owned scratch retained by this decoder.</returns>
    public JxlUpsampler GetUpsampler(int factor)
    {
        // The three powers of two map to consecutive slots. Custom kernels belong to codestream metadata,
        // so later frames can reuse this preparation. Frames without upsampling do not allocate this workspace.
        int index = BitOperations.Log2((uint)factor) - 1;
        return this.upsamplers[index] ??= new(this.configuration, this.Shared.CodecMetadata.CustomTransformData, factor);
    }

    /// <summary>
    /// Prepares integer coefficient-order storage after AC metadata identifies the frame's transform strategies.
    /// </summary>
    /// <param name="numPasses">The parsed number of coefficient passes.</param>
    public void InitializeForAc(int numPasses)
    {
        int orderSize = 0;

        for (int strategy = 0; strategy < JxlAcStrategy.NumberOfValidStrategies; strategy++)
        {
            if (((1 << strategy) & this.UsedAcs) == 0)
            {
                continue;
            }

            int order = JxlCoefficientOrder.StrategyOrder[strategy];
            orderSize = Math.Max(
                JxlCoefficientOrder.CoefficientOrderOffsets[3 * (order + 1)] * JxlFrameDimensions.DctBlockSize,
                orderSize);
        }

        // Each pass stores a complete set of the orders it uses. Multiplication preserves that stride;
        // bitwise AND would truncate capacity and byte storage would truncate transform indices.
        this.Shared.EnsureCoefficientOrderCapacity(numPasses * orderSize);
        this.Shared.CoeffOrderSize = orderSize;
        this.GroupCache.InitializeOnce(numPasses, this.UsedAcs);
    }

    /// <summary>
    /// Selects the AC integer width and allocates retained coefficients only for progressive frames.
    /// </summary>
    /// <param name="numPasses">The parsed number of coefficient passes whose entropy tables are already decoded.</param>
    /// <param name="isJpeg">Whether JPEG reconstruction requires full-width integer coefficients.</param>
    public void PrepareCoefficients(int numPasses, bool isJpeg)
    {
        int maximumBits = 0;

        for (int pass = 0; pass < numPasses; pass++)
        {
            maximumBits = Math.Max(maximumBits, this.Code[pass].MaxNumBits);
        }

        // Pass contributions accumulate in the same coefficients. The pass count adds its ceiling log2 to the symbol bound.
        // Native decoding selects 16 bits only below 16 total bits and excludes JPEG reconstruction from that path.
        maximumBits += JxlMath.CeilLog2Nonzero(numPasses);
        JxlDctAcType type = maximumBits < 16 && !isJpeg ? JxlDctAcType.Ac16 : JxlDctAcType.Ac32;
        IJxlDctAcImage? coefficients = null;
        if (numPasses > 1)
        {
            // Each image row stores one group's complete coefficient plane. Passes add into this retained image,
            // so its logical samples must start at zero. Single-pass decoding writes directly into group scratch.
            int width = this.Shared.FrameDimensions.GroupDimension * this.Shared.FrameDimensions.GroupDimension;
            int height = this.Shared.FrameDimensions.NumGroups;
            coefficients = type == JxlDctAcType.Ac16
                ? new JxlDctAcImage<short>(this.configuration, width, height)
                : new JxlDctAcImage<int>(this.configuration, width, height);

            coefficients.Clear();
        }

        this.Coefficients?.Dispose();
        this.Coefficients = coefficients;
        this.CoefficientType = type;
    }

    /// <summary>
    /// Releases decoder workspace and snapshots without disposing the borrowed common context.
    /// </summary>
    public void Dispose()
    {
        this.GroupCache.Dispose();
        this.Sigma?.Dispose();
        this.Coefficients?.Dispose();
        this.FrameStorageForReferencing?.Dispose();

        foreach (JxlUpsampler? upsampler in this.upsamplers)
        {
            upsampler?.Dispose();
        }
    }

    /// <summary>
    /// Applies one frame's matrix scales and resets payloads that cannot survive a frame change.
    /// </summary>
    /// <param name="frameHeader">The parsed frame settings that control optional decoder storage.</param>
    private void PrepareFrameStorage(JxlFrameHeader frameHeader)
    {
        this.XDmMultiplier = MathF.Pow(1F / 1.25F, frameHeader.XQmScale - 2F);
        this.BDmMultiplier = MathF.Pow(1F / 1.25F, frameHeader.BQmScale - 2F);

        if (frameHeader.LoopFilter.EpfIterations > 0)
        {
            // Sigma is sampled on the block grid. Padding is added on both sides, not multiplied by the image extent.
            int width = this.Shared.FrameDimensions.XSizeBlocks + (2 * SigmaPadding);
            int height = this.Shared.FrameDimensions.YSizeBlocks + (2 * SigmaPadding);
            if (this.Sigma is null)
            {
                this.Sigma = new(this.configuration, width, height);
            }
            else
            {
                this.Sigma.Resize(this.configuration, width, height);
            }
        }
        else
        {
            this.Sigma?.Dispose();
            this.Sigma = null;
        }

        // Progressive coefficients and entropy tables belong to one frame. The group cache retains only reusable capacity.
        this.Coefficients?.Dispose();
        this.Coefficients = null;
        this.CoefficientType = JxlDctAcType.Ac32;
        this.Code.Clear();
        this.ContextMap.Clear();
        this.UsedAcs = 0;

        // A saved reference must already have moved into a common reference slot before the frame changes.
        // Dispose only this frame's remaining payload; the common slots retain their own wrappers and sample allocations.
        this.FrameStorageForReferencing?.Dispose();
        this.FrameStorageForReferencing = frameHeader.CanBeReferenced ? new(this.Shared.CodecMetadata.ImageMetadata) : null;
    }
}
