// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.ChromaFromLuma;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Provides typed views over the reusable storage shared by mutually exclusive AV1 mode searches.
/// </summary>
/// <typeparam name="TSample">The native sample type selected by the encoder pipeline.</typeparam>
internal readonly ref struct Av1EncoderModeDecisionWorkspace<TSample>
    where TSample : unmanaged
{
    /// <summary>
    /// The largest coding-block dimension evaluated directly by the current partition search.
    /// </summary>
    public const int MaximumBlockDimension = 16;

    /// <summary>
    /// The maximum number of samples in one directly evaluated coding block.
    /// </summary>
    public const int MaximumSampleCount = MaximumBlockDimension * MaximumBlockDimension;

    /// <summary>
    /// The number of 4x4 transform blocks covering one 8x8 coding block.
    /// </summary>
    public const int CandidateTransformBlockCount = 4;

    /// <summary>
    /// The required workspace length in signed-integer storage elements.
    /// </summary>
    public const int StorageLength = TransientStorageOffset + Av1EncoderPaletteWorkspace<ushort>.StorageLength;

    private const int ReferenceBufferLength = (2 * MaximumBlockDimension) + 1;
    private const int ReferenceBufferCount = 4;
    private const int ReferenceStorageLength = ReferenceBufferCount * ReferenceBufferLength * sizeof(ushort) / sizeof(int);
    private const int CandidateSampleStorageOffset = ReferenceStorageLength;
    private const int CandidateSampleStorageLength = 2 * MaximumSampleCount * sizeof(ushort) / sizeof(int);
    private const int CandidateCoefficientStorageOffset = CandidateSampleStorageOffset + CandidateSampleStorageLength;
    private const int CandidateCoefficientStorageLength = 2 * MaximumSampleCount;
    private const int CandidateTransformBlockStorageOffset = CandidateCoefficientStorageOffset + CandidateCoefficientStorageLength;
    private const int CandidateTransformBlockStorageLength = CandidateTransformBlockCount;
    private const int TransformContextStorageOffset = CandidateTransformBlockStorageOffset + CandidateTransformBlockStorageLength;
    private const int TransformContextStorageLength =
        2 * (MaximumBlockDimension >> Av1Constants.ModeInfoSizeLog2) * sizeof(byte) / sizeof(int);

    private const int TransientStorageOffset = TransformContextStorageOffset + TransformContextStorageLength;
    private const int ChromaFromLumaSampleCount =
        Av1ChromaFromLumaContext.BufferLine * MaximumBlockDimension;

    private const int ChromaFromLumaSampleStorageLength = ChromaFromLumaSampleCount * sizeof(short) / sizeof(int);
    private const int ChromaFromLumaBlueRateOffset = ChromaFromLumaSampleStorageLength;
    private const int ChromaFromLumaRedRateOffset = ChromaFromLumaBlueRateOffset + Av1ChromaFromLumaMath.AlphaCandidateCount;
    private const int ChromaFromLumaBlueDistortionOffset =
        ChromaFromLumaRedRateOffset + Av1ChromaFromLumaMath.AlphaCandidateCount;

    private const int ChromaFromLumaDistortionStorageLength =
        Av1ChromaFromLumaMath.AlphaCandidateCount * sizeof(long) / sizeof(int);

    private const int ChromaFromLumaRedDistortionOffset =
        ChromaFromLumaBlueDistortionOffset + ChromaFromLumaDistortionStorageLength;

    private readonly Span<int> storage;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1EncoderModeDecisionWorkspace{TSample}"/> struct.
    /// </summary>
    /// <param name="storage">The reusable aligned decision storage.</param>
    public Av1EncoderModeDecisionWorkspace(Span<int> storage) => this.storage = storage;

    /// <summary>
    /// Gets the temporary prediction span shared by mutually exclusive mode searches.
    /// </summary>
    public Span<TSample> Prediction
        => MemoryMarshal.Cast<int, TSample>(this.storage[TransientStorageOffset..])[..MaximumSampleCount];

    /// <summary>
    /// Gets the temporary residual span shared by mutually exclusive mode searches.
    /// </summary>
    public Span<short> Residual
        => MemoryMarshal.Cast<int, short>(
            this.storage.Slice(
                TransientStorageOffset + (MaximumSampleCount * sizeof(ushort) / sizeof(int)),
                MaximumSampleCount * sizeof(short) / sizeof(int)));

    /// <summary>
    /// Gets the fixed-stride subsampled luma values used by chroma-from-luma mode search.
    /// </summary>
    public Span<short> ChromaFromLumaSamples
        => MemoryMarshal.Cast<int, short>(
            this.storage.Slice(TransientStorageOffset, ChromaFromLumaSampleStorageLength));

    /// <summary>
    /// Gets the palette-search view over transient storage that is no longer needed after spatial and CfL search.
    /// </summary>
    public Av1EncoderPaletteWorkspace<TSample> Palette
        => new(this.storage[TransientStorageOffset..]);

    /// <summary>
    /// Gets the transform state retained while evaluating a uniform 4x4 luma layout.
    /// </summary>
    public Span<Av1EncoderTransformBlockState> CandidateTransformBlocks
        => MemoryMarshal.Cast<int, Av1EncoderTransformBlockState>(
            this.storage.Slice(CandidateTransformBlockStorageOffset, CandidateTransformBlockStorageLength));

    /// <summary>
    /// Gets the two above and two left coefficient contexts used by a uniform 4x4 luma layout.
    /// </summary>
    public Span<byte> TransformContexts
        => MemoryMarshal.AsBytes(this.storage.Slice(TransformContextStorageOffset, TransformContextStorageLength));

    /// <summary>
    /// Gets one reference edge including its common-corner prefix.
    /// </summary>
    /// <param name="index">The zero-based edge index.</param>
    /// <returns>The fixed reference-edge span.</returns>
    public Span<TSample> GetReferenceSamples(int index)
        => MemoryMarshal.Cast<int, TSample>(this.storage[..ReferenceStorageLength])
            .Slice(index * ReferenceBufferLength, ReferenceBufferLength);

    /// <summary>
    /// Gets one candidate reconstruction plane.
    /// </summary>
    /// <param name="index">The zero-based plane index.</param>
    /// <returns>The maximum-size candidate reconstruction span.</returns>
    public Span<TSample> GetCandidateReconstruction(int index)
        => MemoryMarshal.Cast<int, TSample>(
            this.storage.Slice(CandidateSampleStorageOffset, CandidateSampleStorageLength))
            .Slice(index * MaximumSampleCount, MaximumSampleCount);

    /// <summary>
    /// Gets one candidate coefficient plane.
    /// </summary>
    /// <param name="index">The zero-based plane index.</param>
    /// <returns>The maximum-size candidate coefficient span.</returns>
    public Span<int> GetCandidateCoefficients(int index)
        => this.storage
            .Slice(CandidateCoefficientStorageOffset, CandidateCoefficientStorageLength)
            .Slice(index * MaximumSampleCount, MaximumSampleCount);

    /// <summary>
    /// Gets one plane's chroma-from-luma coefficient-rate table.
    /// </summary>
    /// <param name="planeIndex">The zero-based chroma plane index.</param>
    /// <returns>The rate table for every signed alpha candidate.</returns>
    public Span<int> GetChromaFromLumaRates(int planeIndex)
        => this.storage.Slice(
            TransientStorageOffset + ChromaFromLumaBlueRateOffset +
                (planeIndex * Av1ChromaFromLumaMath.AlphaCandidateCount),
            Av1ChromaFromLumaMath.AlphaCandidateCount);

    /// <summary>
    /// Gets one plane's chroma-from-luma distortion table.
    /// </summary>
    /// <param name="planeIndex">The zero-based chroma plane index.</param>
    /// <returns>The distortion table for every signed alpha candidate.</returns>
    public Span<long> GetChromaFromLumaDistortions(int planeIndex)
        => MemoryMarshal.Cast<int, long>(
            this.storage.Slice(
                TransientStorageOffset + ChromaFromLumaBlueDistortionOffset +
                    (planeIndex * ChromaFromLumaDistortionStorageLength),
                ChromaFromLumaDistortionStorageLength));
}

/// <summary>
/// Provides typed luma and chroma palette-search buffers over reusable mode-decision storage.
/// </summary>
/// <typeparam name="TSample">The native sample type selected by the encoder pipeline.</typeparam>
internal readonly ref struct Av1EncoderPaletteWorkspace<TSample>
    where TSample : unmanaged
{
    /// <summary>
    /// The required workspace length in signed-integer storage elements.
    /// </summary>
    public const int StorageLength = ColorCacheOffset + ColorCacheStorageLength;

    private const int MaximumSampleCount = Av1EncoderModeDecisionWorkspace<ushort>.MaximumSampleCount;
    private const int PlaneShortStorageLength = MaximumSampleCount * sizeof(short) / sizeof(int);
    private const int PlaneSampleStorageLength = MaximumSampleCount * sizeof(ushort) / sizeof(int);
    private const int PlaneByteStorageLength = MaximumSampleCount / sizeof(int);
    private const int PaletteColorStorageLength = Av1Constants.PaletteMaxSize * sizeof(ushort) / sizeof(int);
    private const int FirstSampleOffset = 0;
    private const int SecondSampleOffset = FirstSampleOffset + PlaneShortStorageLength;
    private const int FirstUniqueColorOffset = SecondSampleOffset + PlaneShortStorageLength;
    private const int SecondUniqueColorOffset = FirstUniqueColorOffset + PlaneShortStorageLength;
    private const int FirstPredictionOffset = SecondUniqueColorOffset + PlaneShortStorageLength;
    private const int SecondPredictionOffset = FirstPredictionOffset + PlaneSampleStorageLength;
    private const int FirstResidualOffset = SecondPredictionOffset + PlaneSampleStorageLength;
    private const int SecondResidualOffset = FirstResidualOffset + PlaneShortStorageLength;
    private const int RetainedIndexOffset = SecondResidualOffset + PlaneShortStorageLength;
    private const int IndexOffset = RetainedIndexOffset + PlaneByteStorageLength;
    private const int FirstCentroidOffset = IndexOffset + PlaneByteStorageLength;
    private const int SecondCentroidOffset = FirstCentroidOffset + PaletteColorStorageLength;
    private const int FirstPaletteColorOffset = SecondCentroidOffset + PaletteColorStorageLength;
    private const int SecondPaletteColorOffset = FirstPaletteColorOffset + PaletteColorStorageLength;
    private const int FirstAlternateCentroidOffset = SecondPaletteColorOffset + PaletteColorStorageLength;
    private const int SecondAlternateCentroidOffset = FirstAlternateCentroidOffset + PaletteColorStorageLength;
    private const int AlternateIndexOffset = SecondAlternateCentroidOffset + PaletteColorStorageLength;
    private const int ColorCountOffset = AlternateIndexOffset + PlaneByteStorageLength;
    private const int DominantOrderOffset = ColorCountOffset + MaximumSampleCount;
    private const int ColorCacheOffset = DominantOrderOffset + PlaneByteStorageLength;
    private const int ColorCacheStorageLength = 2 * Av1Constants.PaletteMaxSize * sizeof(ushort) / sizeof(int);

    private readonly Span<int> storage;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1EncoderPaletteWorkspace{TSample}"/> struct.
    /// </summary>
    /// <param name="storage">The reusable aligned palette storage.</param>
    public Av1EncoderPaletteWorkspace(Span<int> storage) => this.storage = storage;

    /// <summary>
    /// Gets the retained winning color-index map.
    /// </summary>
    public Span<byte> RetainedIndices
        => MemoryMarshal.AsBytes(this.storage.Slice(RetainedIndexOffset, PlaneByteStorageLength));

    /// <summary>
    /// Gets the current color-index map.
    /// </summary>
    public Span<byte> Indices
        => MemoryMarshal.AsBytes(this.storage.Slice(IndexOffset, PlaneByteStorageLength));

    /// <summary>
    /// Gets the alternate K-means color-index map.
    /// </summary>
    public Span<byte> AlternateIndices
        => MemoryMarshal.AsBytes(this.storage.Slice(AlternateIndexOffset, PlaneByteStorageLength));

    /// <summary>
    /// Gets the luma occurrence count for every unique color.
    /// </summary>
    public Span<int> LumaColorCounts
        => this.storage.Slice(ColorCountOffset, MaximumSampleCount);

    /// <summary>
    /// Gets the luma unique-color ordering by descending occurrence count.
    /// </summary>
    public Span<byte> LumaDominantOrder
        => MemoryMarshal.AsBytes(this.storage.Slice(DominantOrderOffset, PlaneByteStorageLength));

    /// <summary>
    /// Gets the sorted neighboring palette colors available to the current block.
    /// </summary>
    public Span<ushort> ColorCache
        => MemoryMarshal.Cast<int, ushort>(this.storage.Slice(ColorCacheOffset, ColorCacheStorageLength));

    /// <summary>
    /// Gets one plane's active palette samples.
    /// </summary>
    /// <param name="planeIndex">The zero-based plane index.</param>
    /// <returns>The maximum-size sample span.</returns>
    public Span<short> GetSamples(int planeIndex)
        => MemoryMarshal.Cast<int, short>(
            this.storage.Slice(FirstSampleOffset + (planeIndex * PlaneShortStorageLength), PlaneShortStorageLength));

    /// <summary>
    /// Gets one plane's unique palette colors.
    /// </summary>
    /// <param name="planeIndex">The zero-based plane index.</param>
    /// <returns>The maximum-size unique-color span.</returns>
    public Span<short> GetUniqueColors(int planeIndex)
        => MemoryMarshal.Cast<int, short>(
            this.storage.Slice(
                FirstUniqueColorOffset + (planeIndex * PlaneShortStorageLength),
                PlaneShortStorageLength));

    /// <summary>
    /// Gets one plane's palette prediction.
    /// </summary>
    /// <param name="planeIndex">The zero-based plane index.</param>
    /// <returns>The maximum-size prediction span.</returns>
    public Span<TSample> GetPrediction(int planeIndex)
        => MemoryMarshal.Cast<int, TSample>(
            this.storage.Slice(
                FirstPredictionOffset + (planeIndex * PlaneSampleStorageLength),
                PlaneSampleStorageLength))[..MaximumSampleCount];

    /// <summary>
    /// Gets one plane's palette residual.
    /// </summary>
    /// <param name="planeIndex">The zero-based plane index.</param>
    /// <returns>The maximum-size residual span.</returns>
    public Span<short> GetResidual(int planeIndex)
        => MemoryMarshal.Cast<int, short>(
            this.storage.Slice(FirstResidualOffset + (planeIndex * PlaneShortStorageLength), PlaneShortStorageLength));

    /// <summary>
    /// Gets one plane's current palette centroids.
    /// </summary>
    /// <param name="planeIndex">The zero-based plane index.</param>
    /// <returns>The maximum-size centroid span.</returns>
    public Span<short> GetCentroids(int planeIndex)
        => MemoryMarshal.Cast<int, short>(
            this.storage.Slice(
                FirstCentroidOffset + (planeIndex * PaletteColorStorageLength),
                PaletteColorStorageLength));

    /// <summary>
    /// Gets one plane's coded palette colors.
    /// </summary>
    /// <param name="planeIndex">The zero-based plane index.</param>
    /// <returns>The maximum-size coded-color span.</returns>
    public Span<ushort> GetPaletteColors(int planeIndex)
        => MemoryMarshal.Cast<int, ushort>(
            this.storage.Slice(
                FirstPaletteColorOffset + (planeIndex * PaletteColorStorageLength),
                PaletteColorStorageLength));

    /// <summary>
    /// Gets one plane's alternate K-means centroids.
    /// </summary>
    /// <param name="planeIndex">The zero-based plane index.</param>
    /// <returns>The maximum-size alternate-centroid span.</returns>
    public Span<short> GetAlternateCentroids(int planeIndex)
        => MemoryMarshal.Cast<int, short>(
            this.storage.Slice(
                FirstAlternateCentroidOffset + (planeIndex * PaletteColorStorageLength),
                PaletteColorStorageLength));
}
