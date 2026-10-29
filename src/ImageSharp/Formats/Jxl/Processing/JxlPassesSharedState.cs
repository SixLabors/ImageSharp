// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;
using SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Quantization;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

/// <summary>
/// Owns shared frame controls and the DC and reference-frame caches for one codestream.
/// </summary>
/// <remarks>
/// Frame initialization replaces frame controls but retains cached reference images.
/// Decoder workspaces borrow this object. They do not dispose it, including during encoder roundtrips.
/// </remarks>
internal sealed class JxlPassesSharedState : IDisposable
{
    /// <summary>
    /// The decoded block-context map, kept as a field because its decoder replaces it through a reference.
    /// </summary>
#pragma warning disable SA1401 // Field decoding replaces this shared object through a reference.
    public JxlBlockContextMap BlockContextMap = new();
#pragma warning restore SA1401

    /// <summary>
    /// Initializes empty sample storage and reference slots after the first frame header supplies its dimensions.
    /// </summary>
    /// <param name="configuration">The configuration that owns sample allocations.</param>
    /// <param name="metadata">The metadata shared by all frames in this codestream.</param>
    /// <param name="frameDimensions">The first frame's parsed dimensions.</param>
    public JxlPassesSharedState(Configuration configuration, JxlCodecMetadata metadata, JxlFrameDimensions frameDimensions)
    {
        this.Configuration = configuration;
        this.CodecMetadata = metadata;
        this.FrameDimensions = frameDimensions;
        this.AcStrategy = JxlAcStrategyImage.Create(configuration, 0, 0);
        this.ColorMap = JxlColorCorrelationMap.Create(configuration, 0, 0);
        this.Quantizer = new(this.Matrices);
        this.Dc = this.DcStorage;

        // Slots belong to the codestream, not to a single frame. Empty planes allocate no sample buffers.
        // A decoded DC image can later move into a slot; Dc only borrows whichever owned image the frame selects.
        for (int i = 0; i < this.DcFrames.Length; i++)
        {
            this.DcFrames[i] = new();
            this.ReferenceFrames[i] = (new JxlImageBundle(metadata.ImageMetadata), false);
        }

        this.ImageFeatures.PatchDictionary.SetShared(this.ReferenceFrames);
    }

    /// <summary>
    /// Gets the configuration that owns this codestream's sample storage.
    /// </summary>
    public Configuration Configuration { get; }

    /// <summary>
    /// Gets the original metadata shared by the codestream's frames.
    /// </summary>
    public JxlCodecMetadata CodecMetadata { get; }

    /// <summary>
    /// Gets the dimensions supplied by the current parsed frame header.
    /// </summary>
    public JxlFrameDimensions FrameDimensions { get; private set; }

    /// <summary>
    /// Gets the owned transform-selection plane for the current frame.
    /// </summary>
    public JxlAcStrategyImage AcStrategy { get; private set; }

    /// <summary>
    /// Gets the current dequantization matrices.
    /// </summary>
    public JxlDequantMatrices Matrices { get; private set; } = new();

    /// <summary>
    /// Gets the quantizer bound to the current matrices.
    /// </summary>
    public JxlQuantizer Quantizer { get; private set; }

    /// <summary>
    /// Gets the current frame's quantization field in block coordinates.
    /// </summary>
    public JxlPlane<int> RawQuantField { get; private set; } = new();

    /// <summary>
    /// Gets the current frame's edge-preserving filter sharpness values.
    /// </summary>
    public JxlPlane<byte> EpfSharpness { get; private set; } = new();

    /// <summary>
    /// Gets the owned color-correlation planes for the current frame.
    /// </summary>
    public JxlColorCorrelationMap ColorMap { get; private set; }

    /// <summary>
    /// Gets decoded patches, splines and noise parameters.
    /// </summary>
    public JxlImageFeatures ImageFeatures { get; } = new();

    /// <summary>
    /// Gets or sets the coefficient count occupied by one pass's orders.
    /// </summary>
    public int CoeffOrderSize { get; set; }

    /// <summary>
    /// Gets coefficient indices arranged as consecutive pass-sized orders.
    /// </summary>
    /// <remarks>Large transforms need indices greater than 255. Keep the indices as integers until coefficient lookup.</remarks>
    public List<int> CoeffOrders { get; } = [];

    /// <summary>
    /// Gets the current frame's quantized DC values.
    /// </summary>
    public JxlPlane<byte> QuantDc { get; private set; } = new();

    /// <summary>
    /// Gets the owned DC image used when the frame does not reference a cached DC image.
    /// </summary>
    public JxlImage3<float> DcStorage { get; } = new();

    /// <summary>
    /// Gets the borrowed DC image, which is either <see cref="DcStorage"/> or one entry in <see cref="DcFrames"/>.
    /// </summary>
    public JxlImage3<float> Dc { get; private set; }

    /// <summary>
    /// Gets the four owned DC reference images retained across frame initialization.
    /// </summary>
    public JxlImage3<float>[] DcFrames { get; } = new JxlImage3<float>[JxlShared.MaximumNumberOfReferenceFrames];

    /// <summary>
    /// Gets the four owned reference-frame slots borrowed by patches and frame blending.
    /// </summary>
    /// <remarks>
    /// Each slot owns its bundle. The flag distinguishes samples saved before the color transform from rendered samples.
    /// Update slots through this array so patch consumers see the same bundle and flag; modifying a copied tuple does not update a slot.
    /// Replacement must transfer or copy planes instead of creating another owner of the same planes.
    /// </remarks>
    public (JxlImageBundle Frame, bool ImageBundleIsInXyb)[] ReferenceFrames { get; } = new (JxlImageBundle, bool)[JxlShared.MaximumNumberOfReferenceFrames];

    /// <summary>
    /// Gets or sets the number of clustered histograms used by each pass.
    /// </summary>
    public int NumHistograms { get; set; }

    /// <summary>
    /// Initializes frame-sized controls after the frame header and dimensions have been validated.
    /// </summary>
    /// <param name="frameHeader">The parsed frame header, which borrows this codestream's metadata.</param>
    /// <param name="dimensions">The validated dimensions already derived from that header.</param>
    /// <param name="encoder">Whether the encoder must reserve the complete coefficient orders before strategy selection.</param>
    public void InitializeFrame(JxlFrameHeader frameHeader, JxlFrameDimensions dimensions, bool encoder)
    {
        this.FrameDimensions = dimensions;

        // Quantizer references must follow their matrices. Neither object owns sample buffers.
        // Reset both together so a later frame cannot use the previous frame's decoded quantization parameters.
        this.Matrices = new();
        this.Quantizer = new(this.Matrices);

        JxlAcStrategyImage strategy = JxlAcStrategyImage.Create(this.Configuration, dimensions.XSizeBlocks, dimensions.YSizeBlocks);
        this.AcStrategy.Dispose();
        this.AcStrategy = strategy;

        // Existing plane storage can serve smaller or equal frames. Reallocate only when its original capacity cannot hold the new dimensions.
        if (!this.RawQuantField.ShrinkTo(dimensions.XSizeBlocks, dimensions.YSizeBlocks))
        {
            JxlPlane<int> quantField = new(this.Configuration, dimensions.XSizeBlocks, dimensions.YSizeBlocks);
            this.RawQuantField.Dispose();
            this.RawQuantField = quantField;
        }

        if (!this.EpfSharpness.ShrinkTo(dimensions.XSizeBlocks, dimensions.YSizeBlocks))
        {
            JxlPlane<byte> sharpness = new(this.Configuration, dimensions.XSizeBlocks, dimensions.YSizeBlocks);
            this.EpfSharpness.Dispose();
            this.EpfSharpness = sharpness;
        }

        JxlColorCorrelationMap colorMap = JxlColorCorrelationMap.Create(this.Configuration, dimensions.XSize, dimensions.YSize);
        this.ColorMap.Dispose();
        this.ColorMap = colorMap;

        this.CoeffOrderSize = JxlCoefficientOrder.CoefficientOrderMaxSize;
        if (encoder && frameHeader.Encoding == JxlFrameEncoding.VarDct)
        {
            int length = (int)frameHeader.Passes.NumberOfPasses * this.CoeffOrderSize;
            if (this.CoeffOrders.Count < length)
            {
                int previousLength = this.CoeffOrders.Count;
                CollectionsMarshal.SetCount(this.CoeffOrders, length);
                CollectionsMarshal.AsSpan(this.CoeffOrders)[previousLength..].Clear();
            }
        }

        if (!this.QuantDc.ShrinkTo(dimensions.XSizeBlocks, dimensions.YSizeBlocks))
        {
            JxlPlane<byte> quantDc = new(this.Configuration, dimensions.XSizeBlocks, dimensions.YSizeBlocks);
            this.QuantDc.Dispose();
            this.QuantDc = quantDc;
        }

        bool useDcFrame = (frameHeader.Flags & (ulong)JxlFrameHeaderFlags.Dc) != 0;
        if (!encoder && useDcFrame)
        {
            if (frameHeader.DcLevel == 4)
            {
                throw new InvalidImageContentException("JPEG XL cannot reference a DC image at level four.");
            }

            // This index is the format's reference level. Saving a DC frame uses dcLevel - 1 at the controller, not here.
            JxlImage3<float> dc = this.DcFrames[(int)frameHeader.DcLevel];
            if (dc.XSize == 0)
            {
                throw new InvalidImageContentException("The referenced JPEG XL DC image has not been decoded.");
            }

            this.DcStorage.Dispose();
            this.Dc = dc;
            this.QuantDc.Clear();
        }
        else
        {
            if (!this.DcStorage.ShrinkTo(dimensions.XSizeBlocks, dimensions.YSizeBlocks))
            {
                // Swap new storage into the stable owned image. The temporary then owns and releases only the previous storage.
                using JxlImage3<float> dc = new(this.Configuration, dimensions.XSizeBlocks, dimensions.YSizeBlocks);
                this.DcStorage.Swap(dc);
            }

            this.Dc = this.DcStorage;
        }
    }

    /// <summary>
    /// Releases frame controls, decoded features and every owned reference image for this codestream.
    /// </summary>
    public void Dispose()
    {
        this.AcStrategy.Dispose();
        this.RawQuantField.Dispose();
        this.EpfSharpness.Dispose();
        this.ColorMap.Dispose();
        this.QuantDc.Dispose();
        this.DcStorage.Dispose();
        this.ImageFeatures.PatchDictionary.Clear();
        this.ImageFeatures.Splines.Clear();

        // Dc is a borrowed alias. Dispose only the storage owners, so a selected reference is never released twice through that alias.
        foreach (JxlImage3<float> dc in this.DcFrames)
        {
            dc.Dispose();
        }

        foreach ((JxlImageBundle frame, bool _) in this.ReferenceFrames)
        {
            frame.Dispose();
        }
    }
}
