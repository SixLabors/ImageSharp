// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Provides disjoint reusable buffers for single-reference and intra-block-copy mode decisions.
/// </summary>
/// <typeparam name="TSample">The native sample type selected by the encoder pipeline.</typeparam>
internal readonly ref struct Av1EncoderInterPredictionWorkspace<TSample>
    where TSample : unmanaged
{
    /// <summary>
    /// The largest coding-block dimension handled by inter prediction.
    /// </summary>
    private const int MaximumBlockDimension = 1 << Av1Constants.MaxSuperBlockSizeLog2;

    /// <summary>
    /// The maximum number of samples in one directly evaluated inter block.
    /// </summary>
    public const int MaximumSampleCount = MaximumBlockDimension * MaximumBlockDimension;

    /// <summary>
    /// The signed intermediate capacity needed when both translational interpolation axes are filtered: one row
    /// per filtered source row, each as wide as the largest block. It equals
    /// <see cref="Av1TranslationalInterPredictor.GetScratchLength(int, int)"/> for the largest block.
    /// </summary>
    public const int PredictionScratchCount =
        MaximumBlockDimension * (MaximumBlockDimension + Av1TranslationalInterPredictor.MaximumExtraRows);

    /// <summary>
    /// The number of sample buffers retained by one mode decision.
    /// </summary>
    public const int SampleBufferCount = 9;

    /// <summary>
    /// The sample capacity shared by sequential transform trials.
    /// </summary>
    public const int TransformSampleCount = Av1Constants.MaxTransformSize * Av1Constants.MaxTransformSize;

    /// <summary>
    /// The number of coefficient buffers retained by one mode decision.
    /// </summary>
    public const int CoefficientBufferCount = 6;

    private readonly Span<TSample> samples;
    private readonly Span<short> residual;
    private readonly Span<short> predictionScratch;
    private readonly Span<int> coefficients;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1EncoderInterPredictionWorkspace{TSample}"/> struct.
    /// </summary>
    /// <param name="samples">The sample storage.</param>
    /// <param name="residual">The residual storage shared by sequential plane evaluations.</param>
    /// <param name="predictionScratch">The intermediate storage used by two-dimensional interpolation.</param>
    /// <param name="coefficients">The coefficient storage.</param>
    public Av1EncoderInterPredictionWorkspace(
        Span<TSample> samples,
        Span<short> residual,
        Span<short> predictionScratch,
        Span<int> coefficients)
    {
        this.samples = samples;
        this.residual = residual;
        this.predictionScratch = predictionScratch;
        this.coefficients = coefficients;
    }

    /// <summary>
    /// Gets the selected luma reconstruction.
    /// </summary>
    public Span<TSample> SelectedLumaReconstruction => this.GetSamples(0);

    /// <summary>
    /// Gets the selected blue-difference chroma reconstruction.
    /// </summary>
    public Span<TSample> SelectedBlueReconstruction => this.GetSamples(1);

    /// <summary>
    /// Gets the selected red-difference chroma reconstruction.
    /// </summary>
    public Span<TSample> SelectedRedReconstruction => this.GetSamples(2);

    /// <summary>
    /// Gets the current luma prediction.
    /// </summary>
    public Span<TSample> LumaPrediction => this.GetSamples(3);

    /// <summary>
    /// Gets the current blue-difference chroma prediction.
    /// </summary>
    public Span<TSample> BluePrediction => this.GetSamples(4);

    /// <summary>
    /// Gets the current red-difference chroma prediction.
    /// </summary>
    public Span<TSample> RedPrediction => this.GetSamples(5);

    /// <summary>
    /// Gets the current luma candidate reconstruction.
    /// </summary>
    public Span<TSample> LumaCandidateReconstruction => this.GetSamples(6);

    /// <summary>
    /// Gets the current blue-difference chroma candidate reconstruction.
    /// </summary>
    public Span<TSample> BlueCandidateReconstruction => this.GetSamples(7);

    /// <summary>
    /// Gets the current red-difference chroma candidate reconstruction.
    /// </summary>
    public Span<TSample> RedCandidateReconstruction => this.GetSamples(8);

    /// <summary>
    /// Gets the reconstruction scratch overwritten by each transform trial.
    /// </summary>
    public Span<TSample> TransformReconstruction => this.samples.Slice(SampleBufferCount * MaximumSampleCount, TransformSampleCount);

    /// <summary>
    /// Gets the residual scratch shared by sequential plane evaluations.
    /// </summary>
    public Span<short> Residual => this.residual;

    /// <summary>
    /// Gets the intermediate scratch used when both translational interpolation axes are filtered.
    /// </summary>
    public Span<short> PredictionScratch => this.predictionScratch;

    /// <summary>
    /// Gets the local coefficient and transform edge contexts retained during inter search.
    /// </summary>
    public Span<byte> TransformContexts => MemoryMarshal.AsBytes(
        this.coefficients[((CoefficientBufferCount * MaximumSampleCount) + TransformSampleCount)..]);

    /// <summary>
    /// Gets the selected luma coefficients.
    /// </summary>
    public Span<int> SelectedLumaCoefficients => this.GetCoefficients(0);

    /// <summary>
    /// Gets the selected blue-difference chroma coefficients.
    /// </summary>
    public Span<int> SelectedBlueCoefficients => this.GetCoefficients(1);

    /// <summary>
    /// Gets the selected red-difference chroma coefficients.
    /// </summary>
    public Span<int> SelectedRedCoefficients => this.GetCoefficients(2);

    /// <summary>
    /// Gets the current luma candidate coefficients.
    /// </summary>
    public Span<int> LumaCandidateCoefficients => this.GetCoefficients(3);

    /// <summary>
    /// Gets the current blue-difference chroma candidate coefficients.
    /// </summary>
    public Span<int> BlueCandidateCoefficients => this.GetCoefficients(4);

    /// <summary>
    /// Gets the current red-difference chroma candidate coefficients.
    /// </summary>
    public Span<int> RedCandidateCoefficients => this.GetCoefficients(5);

    /// <summary>
    /// Gets the coefficient scratch overwritten by each transform trial.
    /// </summary>
    public Span<int> TransformCoefficients => this.coefficients.Slice(CoefficientBufferCount * MaximumSampleCount, TransformSampleCount);

    private Span<TSample> GetSamples(int index)
        => this.samples.Slice(index * MaximumSampleCount, MaximumSampleCount);

    private Span<int> GetCoefficients(int index)
        => this.coefficients.Slice(index * MaximumSampleCount, MaximumSampleCount);
}
