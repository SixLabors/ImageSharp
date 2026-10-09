// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.TemporalFilter;

/// <summary>
/// Owns the scratch storage of the temporal filter, allocated once and reused by every filtered frame so that the
/// per-block paths do not allocate.
/// </summary>
/// <typeparam name="TSample">The unsigned sample storage type.</typeparam>
internal sealed class Av1TemporalFilterWorkspace<TSample> : IDisposable
    where TSample : unmanaged
{
    /// <summary>
    /// The number of samples of one plane of a filter block.
    /// </summary>
    private const int BlockPixels = Av1TemporalFilter.BlockSize * Av1TemporalFilter.BlockSize;

    /// <summary>
    /// The number of samples of all three planes of a filter block without subsampling.
    /// </summary>
    private const int AllPlanePixels = 3 * BlockPixels;

    /// <summary>
    /// The number of zero samples, one 64-sample row.
    /// </summary>
    private const int ZeroCount = Av1TemporalFilter.BlockSize;

    /// <summary>
    /// The number of column sums of one edge-padded row: two replicated values on each side of 64 columns.
    /// </summary>
    private const int ColumnCount = Av1TemporalFilter.BlockSize + 4;

    private IMemoryOwner<TSample>? samples;
    private IMemoryOwner<uint>? errors;
    private IMemoryOwner<ushort>? counts;
    private IMemoryOwner<short>? intermediate;
    private IMemoryOwner<int>? motion;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TemporalFilterWorkspace{TSample}"/> class.
    /// </summary>
    /// <param name="configuration">The configuration providing the memory allocator.</param>
    public Av1TemporalFilterWorkspace(Configuration configuration)
    {
        MemoryAllocator allocator = configuration.MemoryAllocator;
        this.samples = allocator.Allocate<TSample>(AllPlanePixels + Av1TranslationalInterPredictor.SearchPredictionBufferLength + ZeroCount, AllocationOptions.Clean);
        this.errors = allocator.Allocate<uint>(AllPlanePixels + (4 * BlockPixels) + ColumnCount, AllocationOptions.Clean);
        this.counts = allocator.Allocate<ushort>(AllPlanePixels);
        this.intermediate = allocator.Allocate<short>(Av1TemporalFilter.PredictionIntermediateLength);
        this.motion = allocator.Allocate<int>(Av1MotionVectorCosts.IntegerStorageLength + Av1MotionSearchSites.StorageLength);

        // The L1 rate table never changes. The search sites depend on the frame stride, so each frame configures them again.
        Av1TemporalFilter.FillL1MotionCosts(this.MotionCosts);
    }

    /// <summary>
    /// Gets the predictions of all planes of one filter block.
    /// </summary>
    public Span<TSample> Prediction => this.samples!.Memory.Span[..AllPlanePixels];

    /// <summary>
    /// Gets the fractional motion search prediction buffer.
    /// </summary>
    public Span<TSample> FractionalPrediction => this.samples!.Memory.Span.Slice(AllPlanePixels, Av1TranslationalInterPredictor.SearchPredictionBufferLength);

    /// <summary>
    /// Gets a row of zero samples, the reference of the source variance measurements.
    /// </summary>
    public ReadOnlySpan<TSample> Zeros => this.samples!.Memory.Span.Slice(AllPlanePixels + Av1TranslationalInterPredictor.SearchPredictionBufferLength, ZeroCount);

    /// <summary>
    /// Gets the weighted sums of all planes of one filter block.
    /// </summary>
    public Span<uint> Accumulator => this.errors!.Memory.Span[..AllPlanePixels];

    /// <summary>
    /// Gets the squared differences of one plane block.
    /// </summary>
    public Span<uint> SquaredErrors => this.errors!.Memory.Span.Slice(AllPlanePixels, BlockPixels);

    /// <summary>
    /// Gets, for each chroma sample, the sum of the luma squared differences that the sample covers.
    /// </summary>
    public Span<uint> LumaErrors => this.errors!.Memory.Span.Slice(AllPlanePixels + BlockPixels, BlockPixels);

    /// <summary>
    /// Gets a block of zero luma errors. The luma plane adds these in place of <see cref="LumaErrors"/>.
    /// </summary>
    public ReadOnlySpan<uint> ZeroLumaErrors => this.errors!.Memory.Span.Slice(AllPlanePixels + (2 * BlockPixels), BlockPixels);

    /// <summary>
    /// Gets the window errors of one plane block: the 5x5 window sums of the squared differences plus the luma errors.
    /// </summary>
    public Span<uint> WindowErrors => this.errors!.Memory.Span.Slice(AllPlanePixels + (3 * BlockPixels), BlockPixels);

    /// <summary>
    /// Gets one edge-padded row of window column sums.
    /// </summary>
    public Span<uint> Columns => this.errors!.Memory.Span.Slice(AllPlanePixels + (4 * BlockPixels), ColumnCount);

    /// <summary>
    /// Gets the weight totals of all planes of one filter block.
    /// </summary>
    public Span<ushort> Count => this.counts!.Memory.Span[..AllPlanePixels];

    /// <summary>
    /// Gets the intermediate of the two-dimensional prediction filter.
    /// </summary>
    public Span<short> Intermediate => this.intermediate!.Memory.Span;

    /// <summary>
    /// Gets the L1 motion-vector rate table.
    /// </summary>
    public Span<int> MotionCosts => this.motion!.Memory.Span[..Av1MotionVectorCosts.IntegerStorageLength];

    /// <summary>
    /// Gets the storage of the n-step search sites.
    /// </summary>
    public Span<int> SearchSites => this.motion!.Memory.Span.Slice(Av1MotionVectorCosts.IntegerStorageLength, Av1MotionSearchSites.StorageLength);

    /// <summary>
    /// Releases the scratch storage.
    /// </summary>
    public void Dispose()
    {
        this.samples?.Dispose();
        this.errors?.Dispose();
        this.counts?.Dispose();
        this.intermediate?.Dispose();
        this.motion?.Dispose();
        this.samples = null;
        this.errors = null;
        this.counts = null;
        this.intermediate = null;
        this.motion = null;
    }
}
