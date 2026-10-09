// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Tpl;

/// <content>
/// Owns the per-run state and the block scratch storage, which the model allocates once so that no block allocates.
/// </content>
internal sealed partial class Av1TplModel<TSample, TSearchOperator, TSampleOperator>
{
    /// <summary>
    /// The number of samples that an intra edge buffer holds.
    /// </summary>
    private const int EdgeLength = (2 * Av1Constants.MaxTransformSize) + 32;

    /// <summary>
    /// The number of samples before the first edge sample. The edge filter and the upsampler read and write this prefix.
    /// </summary>
    private const int EdgePrefix = 16;

    /// <summary>
    /// The sample count of the search prediction buffer: the vertical support of a 16-row block at a 128-sample stride.
    /// </summary>
    private const int SearchPredictionLength = 128 * (Av1TplModelConstants.BlockSize + 7);

    /// <summary>
    /// The packed 16x16 prediction of the intra and inter trials.
    /// </summary>
    private TSample[] predictor = null!;

    /// <summary>
    /// The fixed prediction of the other reference of the joint motion search.
    /// </summary>
    private TSample[] secondPrediction = null!;

    /// <summary>
    /// The prediction and intermediate storage of the fractional search.
    /// </summary>
    private TSample[] searchPrediction = null!;

    /// <summary>
    /// The above intra edge with its prefix.
    /// </summary>
    private TSample[] aboveEdge = null!;

    /// <summary>
    /// The left intra edge with its prefix.
    /// </summary>
    private TSample[] leftEdge = null!;

    /// <summary>
    /// The intra edge filter workspace.
    /// </summary>
    private TSample[] originalEdge = null!;

    /// <summary>
    /// The directional predictor workspace.
    /// </summary>
    private TSample[] transposedBlock = null!;

    /// <summary>
    /// The packed residual.
    /// </summary>
    private short[] residual = null!;

    /// <summary>
    /// The convolution intermediate of the inter predictions.
    /// </summary>
    private short[] intermediateRows = null!;

    /// <summary>
    /// The compound intermediate of the first reference.
    /// </summary>
    private ushort[] firstIntermediate = null!;

    /// <summary>
    /// The compound intermediate of the second reference.
    /// </summary>
    private ushort[] secondIntermediate = null!;

    /// <summary>
    /// The transform coefficients.
    /// </summary>
    private int[] coefficients = null!;

    /// <summary>
    /// The quantized coefficients.
    /// </summary>
    private int[] quantized = null!;

    /// <summary>
    /// The dequantized coefficients.
    /// </summary>
    private int[] dequantized = null!;

    /// <summary>
    /// The forward and inverse transform workspace.
    /// </summary>
    private int[] transformWorkspace = null!;

    /// <summary>
    /// The search site geometry of the full-pixel search.
    /// </summary>
    private int[] siteStorage = null!;

    /// <summary>
    /// The vector cost tables of the model search.
    /// </summary>
    private IMemoryOwner<int> motionVectorCostStorage = null!;

    /// <summary>
    /// All-zero vector cost tables. A search that uses them gives no cost to any vector.
    /// </summary>
    private IMemoryOwner<int> zeroCostStorage = null!;

    /// <summary>
    /// The default vector distributions of a key frame.
    /// </summary>
    private Av1MotionVectorContext defaultMotionVectorContext = null!;

    /// <summary>
    /// The method and stride that the search site storage is configured for.
    /// </summary>
    private int configuredSiteMethod = -1;
    private int configuredSiteStride = -1;

    /// <summary>
    /// The precision of the captured vector costs.
    /// </summary>
    private Av1MotionVectorPrecision costPrecision;

    /// <summary>
    /// Whether the last entry of the group is a key frame, which the rate multiplier reads as the frame type.
    /// </summary>
    private bool lastEntryIsKeyFrame;

    /// <summary>
    /// The frame being measured.
    /// </summary>
    private int frameIndex;

    /// <summary>
    /// The quantizer of the model.
    /// </summary>
    private int qIndex;

    /// <summary>
    /// The sharpness of the quantizer tables, which sets the quantizer rounding.
    /// </summary>
    private int quantizerSharpness;

    /// <summary>
    /// The rate multiplier of the motion search.
    /// </summary>
    private int rateMultiplier;

    /// <summary>
    /// The absolute difference rate scale of the motion search.
    /// </summary>
    private int sadPerBit;

    /// <summary>
    /// The storage entry of the frame of each named reference, or -1.
    /// </summary>
    private readonly int[] referenceEntries = new int[Av1TplModelConstants.InterReferenceCount];

    /// <summary>
    /// Whether the model uses the reconstruction of each named reference.
    /// </summary>
    private readonly bool[] hasReference = new bool[Av1TplModelConstants.InterReferenceCount];

    /// <summary>
    /// Whether the source of each named reference is present.
    /// </summary>
    private readonly bool[] hasSourceReference = new bool[Av1TplModelConstants.InterReferenceCount];

    /// <summary>
    /// Whether the block above is available.
    /// </summary>
    private bool upAvailable;

    /// <summary>
    /// Whether the block to the left is available.
    /// </summary>
    private bool leftAvailable;

    /// <summary>
    /// The mode-information row that ends the first tile. It bounds every block of the model.
    /// </summary>
    private int tileModeInfoRowEnd;

    /// <summary>
    /// The mode-information column that ends the first tile. It bounds every block of the model.
    /// </summary>
    private int tileModeInfoColumnEnd;

    /// <summary>
    /// Gets the reference types in the order that duplicate buffers are removed.
    /// </summary>
    private static ReadOnlySpan<byte> ReferencePriorityOrder => [1, 7, 5, 4, 6, 2, 3];

    /// <summary>
    /// Gets the reference types that a reference limit disables first.
    /// </summary>
    private static ReadOnlySpan<byte> DisableOrder => [3, 2, 6, 5];

    /// <summary>
    /// Allocates the block scratch storage.
    /// </summary>
    private void CreateScratch()
    {
        const int Area = Av1TplModelConstants.BlockArea;
        this.predictor = new TSample[2 * Area];
        this.secondPrediction = new TSample[Area];
        this.searchPrediction = new TSample[SearchPredictionLength];
        this.aboveEdge = new TSample[EdgeLength];
        this.leftEdge = new TSample[EdgeLength];
        this.originalEdge = new TSample[Av1IntraEdgeFilter.ScratchLength];
        this.transposedBlock = new TSample[Area];
        this.residual = new short[Area];
        this.intermediateRows = new short[Av1TranslationalInterPredictor.GetScratchLength(Av1TplModelConstants.BlockSize, Av1TplModelConstants.BlockSize)];
        this.firstIntermediate = new ushort[Area];
        this.secondIntermediate = new ushort[Area];
        this.coefficients = new int[Area];
        this.quantized = new int[Area];
        this.dequantized = new int[Area];
        this.transformWorkspace = new int[Math.Max(Av1TransformWorkspace.MaximumLength, Av1TransformWorkspace.InverseMaximumLength)];
        this.siteStorage = new int[Av1MotionSearchSites.StorageLength];
        this.motionVectorCostStorage = this.configuration.MemoryAllocator.Allocate<int>(Av1MotionVectorCosts.StorageLength);
        this.zeroCostStorage = this.configuration.MemoryAllocator.Allocate<int>(Av1MotionVectorCosts.StorageLength, AllocationOptions.Clean);
        this.defaultMotionVectorContext = new Av1MotionVectorContext();
    }

    /// <summary>
    /// Releases the pooled scratch storage.
    /// </summary>
    private void DisposeScratch()
    {
        this.motionVectorCostStorage.Dispose();
        this.zeroCostStorage.Dispose();
    }

    /// <summary>
    /// Captures the vector costs of the model search from the entropy context. A key frame uses the default distributions.
    /// </summary>
    /// <param name="input">The encoder state of the run.</param>
    private void FillMotionVectorCosts(Av1TplSetupInput<TSample> input)
    {
        this.costPrecision = input.ForceIntegerMotionVector
            ? Av1MotionVectorPrecision.Integer
            : input.AllowHighPrecisionMotionVector ? Av1MotionVectorPrecision.EighthSample : Av1MotionVectorPrecision.QuarterSample;

        Av1MotionVectorContext context = input.IsKeyFrame ? this.defaultMotionVectorContext : input.MotionVectorContext;
        Av1MotionVectorCosts costs = new(this.motionVectorCostStorage.Memory.Span, this.costPrecision);
        costs.Fill(context);
    }

    /// <summary>
    /// Returns the search sites of a method. It configures the storage again only when the method or the stride changes.
    /// </summary>
    /// <param name="method">The full-pixel search method.</param>
    /// <param name="stride">The row stride of the searched plane.</param>
    /// <returns>The search sites.</returns>
    private Av1MotionSearchSites GetSearchSites(Av1MotionSearchSettings.FullPixelSearchMethod method, int stride)
    {
        // Fast diamond variants share the big-diamond geometry.
        Av1MotionSearchSettings.FullPixelSearchMethod shape = method > Av1MotionSearchSettings.FullPixelSearchMethod.BigDiamond
            ? Av1MotionSearchSettings.FullPixelSearchMethod.BigDiamond
            : method;

        Av1MotionSearchSites sites = new(this.siteStorage);
        if ((int)shape != this.configuredSiteMethod || stride != this.configuredSiteStride)
        {
            sites.Configure(shape, stride);
            this.configuredSiteMethod = (int)shape;
            this.configuredSiteStride = stride;
        }

        return sites;
    }

    /// <summary>
    /// Borrows the whole backing buffer of a plane with the index of its coded origin.
    /// </summary>
    /// <param name="frame">The frame.</param>
    /// <param name="plane">The plane, zero for luma.</param>
    /// <returns>The plane access.</returns>
    private static PlaneAccess GetPlane(Av1EncoderFrame<TSample> frame, int plane)
    {
        Av1PlaneRegion<TSample> region = frame.CodedView.GetPlane((Av1Plane)plane);
        return new PlaneAccess(
            region.Samples,
            region.Stride,
            (region.Bounds.Y * region.Stride) + region.Bounds.X);
    }

    /// <summary>
    /// The backing storage of a plane with its row stride and the index of its coded origin.
    /// </summary>
    private readonly ref struct PlaneAccess
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PlaneAccess"/> struct.
        /// </summary>
        /// <param name="samples">The whole backing buffer, borders included.</param>
        /// <param name="stride">The row stride.</param>
        /// <param name="origin">The index of the coded origin.</param>
        public PlaneAccess(Span<TSample> samples, int stride, int origin)
        {
            this.Samples = samples;
            this.Stride = stride;
            this.Origin = origin;
        }

        /// <summary>
        /// Gets the whole backing buffer.
        /// </summary>
        public Span<TSample> Samples { get; }

        /// <summary>
        /// Gets the row stride.
        /// </summary>
        public int Stride { get; }

        /// <summary>
        /// Gets the index of the coded origin.
        /// </summary>
        public int Origin { get; }

        /// <summary>
        /// Returns the index of a position relative to the coded origin.
        /// </summary>
        /// <param name="x">The column.</param>
        /// <param name="y">The row.</param>
        /// <returns>The buffer index.</returns>
        public int IndexOf(int x, int y) => this.Origin + (y * this.Stride) + x;
    }
}
