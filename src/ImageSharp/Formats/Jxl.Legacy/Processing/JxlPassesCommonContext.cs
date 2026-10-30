// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

#pragma warning disable SA1401 // The entropy decoder replaces BlockContextMap by reference.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;
using SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Quantization;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

/// <summary>
/// Owns quantization state, frame maps, and reference samples shared by the frames of one codestream.
/// </summary>
/// <remarks>
/// Metadata is borrowed from the codestream decoder. Matrices, their quantizer, and reference slots retain their identity across frames.
/// Frame preparation replaces only frame maps and working DC storage. Borrowed row views must not survive frame preparation or disposal.
/// </remarks>
internal sealed class JxlPassesCommonContext : IDisposable
{
    /// <summary>
    /// The configuration that owns sample and coefficient storage.
    /// </summary>
    private readonly Configuration configuration;

    /// <summary>
    /// Owns the retained capacity for integer coefficient-order indices.
    /// </summary>
    private IMemoryOwner<int>? coefficientOrderOwner;

    /// <summary>
    /// Borrows the complete coefficient-order allocation, including reusable capacity.
    /// </summary>
    private Memory<int> coefficientOrderStorage;

    /// <summary>
    /// The entropy context map for the current frame. The entropy reader replaces this field by reference.
    /// </summary>
    public JxlBlockContextMap BlockContextMap = new();

    /// <summary>
    /// Creates codestream state and allocates maps for its first parsed frame.
    /// </summary>
    /// <param name="configuration">The configuration that owns all numeric allocations.</param>
    /// <param name="metadata">The parsed metadata retained by the codestream decoder.</param>
    /// <param name="frameHeader">The first parsed frame header, bound to the same metadata.</param>
    /// <param name="encoder">Whether to reserve encoder coefficient orders instead of borrowing saved DC samples.</param>
    public JxlPassesCommonContext(Configuration configuration, JxlCodecMetadata metadata, JxlFrameHeader frameHeader, bool encoder)
    {
        this.configuration = configuration;
        this.CodecMetadata = metadata;
        this.FrameDimensions = frameHeader.FrameDimensions;
        this.Quantizer = new(this.Matrices);

        // A reference slot owns a stable bundle, not a copy of another frame's wrapper. Empty slots allocate no sample planes.
        // Patches borrow these slots throughout the codestream, so preparing another frame must not replace the slot array.
        this.ReferenceFrames = new (JxlImageBundle Frame, bool ImageBundleIsInXyb)[JxlShared.MaximumNumberOfReferenceFrames];
        this.DcFrames = new JxlImage3<float>[JxlShared.MaximumNumberOfReferenceFrames];

        for (int i = 0; i < this.ReferenceFrames.Length; i++)
        {
            this.ReferenceFrames[i] = (new(metadata.ImageMetadata), false);
            this.DcFrames[i] = new();
        }

        this.ImageFeatures.PatchDictionary.SetShared(this.ReferenceFrames);

        bool useDcFrame = !encoder && (frameHeader.Flags & (ulong)JxlFrameHeaderFlags.Dc) != 0;
        JxlImage3<float>? dcFrame = useDcFrame ? this.GetDcFrame(frameHeader.DcLevel) : null;
        (this.AcStrategy, this.RawQuantField, this.EpfSharpness, this.ColorMap, this.QuantDc, this.DcStorage) =
            this.AllocateFrameMaps(this.FrameDimensions, useDcFrame);

        this.Dc = dcFrame ?? this.DcStorage;
        if (useDcFrame)
        {
            this.QuantDc.Clear();
        }

        this.CoeffOrderSize = JxlCoefficientOrder.CoefficientOrderMaxSize;

        try
        {
            if (encoder && frameHeader.Encoding == JxlFrameEncoding.VarDct)
            {
                this.EnsureCoefficientOrderCapacity((int)frameHeader.Passes.NumberOfPasses * this.CoeffOrderSize);
            }
        }
        catch
        {
            // A constructor cannot transfer partially acquired storage to its caller.
            this.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Gets the metadata borrowed from the codestream decoder.
    /// </summary>
    public JxlCodecMetadata CodecMetadata { get; }

    /// <summary>
    /// Gets the current frame's parsed pixel, block, and group dimensions.
    /// </summary>
    public JxlFrameDimensions FrameDimensions { get; private set; }

    /// <summary>
    /// Gets the owned transform-selection map for the current frame.
    /// </summary>
    public JxlAcStrategyImage AcStrategy { get; private set; }

    /// <summary>
    /// Gets the matrix owner retained for this codestream. The quantizer borrows this same instance.
    /// </summary>
    public JxlDequantMatrices Matrices { get; } = new();

    /// <summary>
    /// Gets the quantizer that borrows this owner's retained matrices.
    /// </summary>
    public JxlQuantizer Quantizer { get; }

    /// <summary>
    /// Gets the owned per-block quantization field.
    /// </summary>
    public JxlPlane<int> RawQuantField { get; private set; }

    /// <summary>
    /// Gets the owned per-block edge-preserving filter sharpness field.
    /// </summary>
    public JxlPlane<byte> EpfSharpness { get; private set; }

    /// <summary>
    /// Gets the owned current-frame color-correlation map.
    /// </summary>
    public JxlColorCorrelationMap ColorMap { get; private set; }

    /// <summary>
    /// Gets the current frame's patch, spline, and noise data.
    /// </summary>
    public JxlImageFeatures ImageFeatures { get; } = new();

    /// <summary>
    /// Gets or sets the number of coefficient-order entries used by one pass.
    /// </summary>
    public int CoeffOrderSize { get; set; }

    /// <summary>
    /// Gets the active coefficient-order indices. Each pass occupies <see cref="CoeffOrderSize"/> entries.
    /// </summary>
    /// <remarks>
    /// Indices address complete transforms and can exceed the byte range. The view borrows allocator-owned integer storage.
    /// Preparing coefficient capacity can invalidate the view; borrow it after pass tables are prepared.
    /// </remarks>
    public Memory<int> CoeffOrders { get; private set; }

    /// <summary>
    /// Gets the owned quantized DC context field for the current frame.
    /// </summary>
    public JxlPlane<byte> QuantDc { get; private set; }

    /// <summary>
    /// Gets the owned working DC image. A frame that uses a saved DC level leaves this wrapper empty.
    /// </summary>
    public JxlImage3<float> DcStorage { get; private set; }

    /// <summary>
    /// Gets the borrowed DC image: current working storage or one retained DC slot.
    /// </summary>
    public JxlImage3<float> Dc { get; private set; }

    /// <summary>
    /// Gets the owned saved DC images. Moving samples into a slot retains them across subsequent frame preparation.
    /// </summary>
    public JxlImage3<float>[] DcFrames { get; }

    /// <summary>
    /// Gets the owned reference bundles and each bundle's XYB flag. Patch and blending code borrow these slots.
    /// </summary>
    public (JxlImageBundle Frame, bool ImageBundleIsInXyb)[] ReferenceFrames { get; }

    /// <summary>
    /// Gets or sets the number of encoded histogram selections for one pass.
    /// </summary>
    public int NumHistograms { get; set; }

    /// <summary>
    /// Prepares the next frame's maps while retaining codestream references, reusable capacity and matrix identity.
    /// </summary>
    /// <param name="frameHeader">The next parsed frame header, bound to this codestream's metadata.</param>
    /// <param name="encoder">Whether the frame needs encoder coefficient-order capacity.</param>
    public void PrepareFrame(JxlFrameHeader frameHeader, bool encoder)
    {
        JxlFrameDimensions dimensions = frameHeader.FrameDimensions;
        bool useDcFrame = !encoder && (frameHeader.Flags & (ulong)JxlFrameHeaderFlags.Dc) != 0;
        JxlImage3<float>? dcFrame = useDcFrame ? this.GetDcFrame(frameHeader.DcLevel) : null;
        if (encoder && frameHeader.Encoding == JxlFrameEncoding.VarDct)
        {
            this.EnsureCoefficientOrderCapacity((int)frameHeader.Passes.NumberOfPasses * JxlCoefficientOrder.CoefficientOrderMaxSize);
        }

        // Each map keeps its allocation when the new extent fits. Replacing one map invalidates its old row views.
        // Frame preparation is not resumable after allocation failure; the decoder disposes the context on that path.
        if (!this.AcStrategy.ShrinkTo(dimensions.XSizeBlocks, dimensions.YSizeBlocks))
        {
            JxlAcStrategyImage replacement = JxlAcStrategyImage.Create(this.configuration, dimensions.XSizeBlocks, dimensions.YSizeBlocks);
            this.AcStrategy.Dispose();
            this.AcStrategy = replacement;
        }

        this.RawQuantField.Resize(this.configuration, dimensions.XSizeBlocks, dimensions.YSizeBlocks);
        this.EpfSharpness.Resize(this.configuration, dimensions.XSizeBlocks, dimensions.YSizeBlocks);
        this.QuantDc.Resize(this.configuration, dimensions.XSizeBlocks, dimensions.YSizeBlocks);

        if (!this.ColorMap.ShrinkTo(dimensions.XSize, dimensions.YSize))
        {
            JxlColorCorrelationMap replacement = JxlColorCorrelationMap.Create(this.configuration, dimensions.XSize, dimensions.YSize);
            this.ColorMap.Dispose();
            this.ColorMap = replacement;
        }
        else
        {
            // An all-default correlation field carries no replacements. Restore scalar defaults as well as map samples.
            this.ColorMap.Reset(true);
        }

        if (useDcFrame)
        {
            this.DcStorage.Dispose();
        }
        else if (!this.DcStorage.ShrinkTo(dimensions.XSizeBlocks, dimensions.YSizeBlocks))
        {
            using JxlImage3<float> replacement = new(this.configuration, dimensions.XSizeBlocks, dimensions.YSizeBlocks);
            this.DcStorage.Swap(replacement);
        }

        this.Dc = dcFrame ?? this.DcStorage;
        if (useDcFrame)
        {
            this.QuantDc.Clear();
        }

        this.FrameDimensions = dimensions;
        this.CoeffOrderSize = JxlCoefficientOrder.CoefficientOrderMaxSize;
        this.NumHistograms = 0;
        this.BlockContextMap = new();

        // Feature payloads describe one frame. References survive this reset because later frames can address them.
        this.ImageFeatures.PatchDictionary.Clear();
        this.ImageFeatures.Splines.Clear();
        this.ImageFeatures.NoiseParameters.Clear();
    }

    /// <summary>
    /// Selects the active order length and grows allocator-owned capacity only when the prepared passes require it.
    /// </summary>
    /// <param name="count">The total number of integer indices required by the prepared passes.</param>
    public void EnsureCoefficientOrderCapacity(int count)
    {
        if (count > this.coefficientOrderStorage.Length)
        {
            IMemoryOwner<int> replacement = this.configuration.MemoryAllocator.Allocate<int>(count, AllocationOptions.Clean);
            Memory<int> storage = replacement.Memory[..count];

            // Later progression passes retain orders decoded earlier. New entries start at zero, as in native vector growth.
            this.CoeffOrders.CopyTo(storage);
            this.coefficientOrderOwner?.Dispose();
            this.coefficientOrderOwner = replacement;
            this.coefficientOrderStorage = storage;
        }

        else if (count > this.CoeffOrders.Length)
        {
            // Shrinking the logical view does not release capacity. Regrowth must initialize only its newly active entries,
            // matching native vector resize while preserving orders already decoded for earlier progression passes.
            this.coefficientOrderStorage.Span.Slice(this.CoeffOrders.Length, count - this.CoeffOrders.Length).Clear();
        }

        this.CoeffOrders = this.coefficientOrderStorage[..count];
    }

    /// <summary>
    /// Releases frame maps, coefficient storage, matrices, and retained reference samples without disposing metadata.
    /// </summary>
    public void Dispose()
    {
        this.AcStrategy.Dispose();
        this.RawQuantField.Dispose();
        this.EpfSharpness.Dispose();
        this.ColorMap.Dispose();
        this.QuantDc.Dispose();
        this.DcStorage.Dispose();
        this.Matrices.Dispose();
        this.coefficientOrderOwner?.Dispose();
        this.coefficientOrderOwner = null;
        this.coefficientOrderStorage = default;
        this.CoeffOrders = default;
        this.ImageFeatures.PatchDictionary.Clear();
        this.ImageFeatures.Splines.Clear();

        // Dc can alias DcStorage or a saved DC slot. Dispose each owner once, never the borrowed Dc view separately.
        for (int i = 0; i < this.ReferenceFrames.Length; i++)
        {
            this.ReferenceFrames[i].Frame.Dispose();
            this.DcFrames[i].Dispose();
        }
    }

    /// <summary>
    /// Borrows the saved DC image requested by an encoded frame.
    /// </summary>
    /// <param name="level">The encoded DC level used as a reference-slot index.</param>
    /// <returns>The populated saved DC image, still owned by this context.</returns>
    private JxlImage3<float> GetDcFrame(uint level)
    {
        // The header permits level four for a DC frame, but no frame can reference a fifth saved DC slot.
        // An absent referenced image is invalid input, not an empty working image to reconstruct silently.
        if (level >= this.DcFrames.Length || this.DcFrames[level].XSize == 0)
        {
            throw new InvalidImageContentException("The frame requests a DC level that has not been decoded.");
        }

        return this.DcFrames[level];
    }

    /// <summary>
    /// Allocates the first frame's maps and releases partial allocations if construction fails.
    /// </summary>
    /// <param name="dimensions">The parsed dimensions of the first frame.</param>
    /// <param name="useDcFrame">Whether DC samples come from a saved slot rather than working storage.</param>
    /// <returns>The complete map owners transferred to this context.</returns>
    private (
        JxlAcStrategyImage AcStrategy,
        JxlPlane<int> RawQuantField,
        JxlPlane<byte> EpfSharpness,
        JxlColorCorrelationMap ColorMap,
        JxlPlane<byte> QuantDc,
        JxlImage3<float> DcStorage) AllocateFrameMaps(JxlFrameDimensions dimensions, bool useDcFrame)
    {
        JxlAcStrategyImage? strategy = null;
        JxlPlane<int>? quantization = null;
        JxlPlane<byte>? sharpness = null;
        JxlColorCorrelationMap? correlation = null;
        JxlPlane<byte>? quantizedDc = null;
        JxlImage3<float>? dcStorage = null;

        try
        {
            strategy = JxlAcStrategyImage.Create(this.configuration, dimensions.XSizeBlocks, dimensions.YSizeBlocks);
            quantization = new(this.configuration, dimensions.XSizeBlocks, dimensions.YSizeBlocks);
            sharpness = new(this.configuration, dimensions.XSizeBlocks, dimensions.YSizeBlocks);
            correlation = JxlColorCorrelationMap.Create(this.configuration, dimensions.XSize, dimensions.YSize);
            quantizedDc = new(this.configuration, dimensions.XSizeBlocks, dimensions.YSizeBlocks);
            dcStorage = useDcFrame ? new() : new(this.configuration, dimensions.XSizeBlocks, dimensions.YSizeBlocks);
            return (strategy, quantization, sharpness, correlation, quantizedDc, dcStorage);
        }
        catch
        {
            // No partially constructed context reaches the caller. Release only allocations completed before the failure.
            strategy?.Dispose();
            quantization?.Dispose();
            sharpness?.Dispose();
            correlation?.Dispose();
            quantizedDc?.Dispose();
            dcStorage?.Dispose();
            throw;
        }
    }
}
