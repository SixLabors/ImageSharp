// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Heif;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Heif;

/// <summary>
/// Measures the encoder primitives of one transform block against the libaom reference timings.
/// </summary>
public class Av1PrimitiveBenchmarks
{
    private const int QIndex = 64;
    private readonly short[] residual = new short[16 * 16];
    private readonly int[] coefficients = new int[16 * 16];
    private readonly int[] quantized = new int[16 * 16];
    private readonly int[] dequantized = new int[16 * 16];
    private readonly int[] workspace = new int[Av1TransformWorkspace.MaximumLength];
    private readonly byte[] prediction = new byte[16 * 16];
    private readonly byte[] reconstruction = new byte[16 * 16];
    private readonly byte[] source = new byte[16 * 16];
    private readonly byte[] above = new byte[64];
    private readonly byte[] left = new byte[64];
    private Av1SymbolEncoder writer;
    private Av1EncoderBlockWorkspace blockWorkspace;
    private ushort endOfBlock8;
    private ushort endOfBlock16;

    /// <summary>
    /// Gets or sets the transform width and height.
    /// </summary>
    [Params(8, 16)]
    public int Size { get; set; }

    /// <summary>
    /// Prepares a residual with an energy similar to a quality-75 photo block.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        Random random = new(7);
        for (int index = 0; index < this.residual.Length; index++)
        {
            this.residual[index] = (short)random.Next(-24, 25);
            this.source[index] = (byte)random.Next(40, 200);
            this.prediction[index] = (byte)Math.Clamp(this.source[index] - this.residual[index], 0, 255);
        }

        for (int index = 0; index < this.above.Length; index++)
        {
            this.above[index] = (byte)random.Next(40, 200);
            this.left[index] = (byte)random.Next(40, 200);
        }

        this.writer = new Av1SymbolEncoder(Configuration.Default, 4096, QIndex, updateCdf: false)
        {
            EncodingSpeed = HeifEncodingSpeed.Level6
        };

        this.writer.RefreshCosts();
        this.blockWorkspace = new Av1EncoderBlockWorkspace(Configuration.Default)
        {
            SpeedSettings = new Av1EncoderSpeedSettings(HeifEncodingSpeed.Level6, true, true, Av1FrameUpdateType.Key, QIndex, new Size(512, 512))
        };

        this.Forward();
        this.endOfBlock8 = this.endOfBlock16 = this.Quantize();
    }

    /// <summary>
    /// Releases the writer and workspace.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        this.writer?.Dispose();
        this.blockWorkspace?.Dispose();
    }

    private Av1TransformSize TransformSize => this.Size == 8 ? Av1TransformSize.Size8x8 : Av1TransformSize.Size16x16;

    /// <summary>
    /// Measures the squared error between a source block and its prediction.
    /// </summary>
    /// <returns>The sum of squared differences.</returns>
    [Benchmark]
    public long SumSquaredError()
        => Av1ResidualBuilder.SumSquaredError(this.source, this.Size, this.prediction, this.Size, this.Size, this.Size);

    /// <summary>
    /// Measures the forward DCT.
    /// </summary>
    /// <returns>The DC coefficient.</returns>
    [Benchmark]
    public int Forward()
    {
        Av1ForwardTransformer.Transform2d(
            this.residual,
            this.coefficients,
            (uint)this.Size,
            Av1TransformType.DctDct,
            this.TransformSize,
            8,
            this.workspace);

        return this.coefficients[0];
    }

    /// <summary>
    /// Measures the forward ADST-ADST transform.
    /// </summary>
    /// <returns>The DC coefficient.</returns>
    [Benchmark]
    public int ForwardAdst()
    {
        Av1ForwardTransformer.Transform2d(
            this.residual,
            this.coefficients,
            (uint)this.Size,
            Av1TransformType.AdstAdst,
            this.TransformSize,
            8,
            this.workspace);

        return this.coefficients[0];
    }

    /// <summary>
    /// Measures the fast quantizer.
    /// </summary>
    /// <returns>The end of block.</returns>
    [Benchmark]
    public ushort Quantize()
        => Av1ForwardQuantizer.QuantizeLossy(
            this.coefficients,
            this.quantized,
            this.dequantized,
            this.TransformSize,
            Av1TransformType.DctDct,
            QIndex,
            0,
            0,
            Av1BitDepth.EightBit,
            0);

    /// <summary>
    /// Measures the regular quantizer.
    /// </summary>
    /// <returns>The end of block.</returns>
    [Benchmark]
    public ushort QuantizeRegular()
        => Av1ForwardQuantizer.QuantizeRegular(
            this.coefficients,
            this.quantized,
            this.dequantized,
            this.TransformSize,
            Av1TransformType.DctDct,
            QIndex,
            0,
            0,
            Av1BitDepth.EightBit,
            0);

    /// <summary>
    /// Measures the inverse transform plus the pixel-domain distortion.
    /// </summary>
    /// <returns>The distortion.</returns>
    [Benchmark]
    public long Reconstruct()
    {
        Av1EncoderTransformBlockState state = default;
        state.EndOfBlock = this.endOfBlock8;
        state.TransformType = Av1TransformType.DctDct;
        this.prediction.AsSpan().CopyTo(this.reconstruction);
        Av1InverseTransformer.Reconstruct8Bit(
            this.dequantized,
            this.reconstruction,
            this.Size,
            this.TransformSize,
            Av1TransformType.DctDct,
            0,
            state.EndOfBlock,
            false,
            this.workspace);

        return Av1ResidualBuilder.SumSquaredError(this.source, this.Size, this.reconstruction, this.Size, this.Size, this.Size);
    }

    /// <summary>
    /// Measures the transform-domain distortion.
    /// </summary>
    /// <returns>The distortion.</returns>
    [Benchmark]
    public long TransformError()
        => Av1TransformBlockEncoder.GetTransformError(this.coefficients.AsSpan(0, this.Size * this.Size), this.dequantized.AsSpan(0, this.Size * this.Size), this.TransformSize, Av1BitDepth.EightBit, out _);

    /// <summary>
    /// Measures the coefficient rate.
    /// </summary>
    /// <returns>The rate.</returns>
    [Benchmark]
    public int CoefficientCost()
        => this.writer.GetCoefficientCost(
            this.TransformSize,
            Av1TransformType.DctDct,
            Av1PredictionMode.DC,
            this.quantized,
            Av1ComponentType.Luminance,
            default,
            this.endOfBlock8,
            false,
            Av1FilterIntraMode.AllFilterIntraModes,
            false);

    /// <summary>
    /// Measures coefficient refinement.
    /// </summary>
    /// <returns>The end of block.</returns>
    [Benchmark]
    public ushort Optimize()
    {
        // Refinement changes the coefficients; restore them so each call does the same work.
        this.Quantize();
        return this.writer.OptimizeCoefficients(
            this.coefficients,
            this.quantized,
            this.dequantized,
            this.TransformSize,
            Av1TransformType.DctDct,
            Av1ComponentType.Luminance,
            default,
            Av1QuantizationLookup.GetDcQuant(QIndex, 0, Av1BitDepth.EightBit),
            Av1QuantizationLookup.GetAcQuant(QIndex, 0, Av1BitDepth.EightBit),
            128,
            Av1BitDepth.EightBit,
            false,
            false,
            this.endOfBlock8,
            Av1CoefficientOptimizationWeights.Default,
            out _);
    }

    /// <summary>
    /// Measures DC intra prediction with the residual.
    /// </summary>
    /// <returns>The first residual.</returns>
    [Benchmark]
    public int PredictDc()
    {
        Av1TransformBlockEncoder.PrepareIntraPrediction(
            this.blockWorkspace,
            this.source,
            this.Size,
            this.prediction,
            this.Size,
            this.above,
            this.left,
            true,
            true,
            Av1PredictionMode.DC,
            0,
            true,
            false,
            this.residual,
            this.TransformSize);

        return this.residual[0];
    }

    /// <summary>
    /// Measures directional intra prediction with the residual.
    /// </summary>
    /// <returns>The first residual.</returns>
    [Benchmark]
    public int PredictDirectional() => this.Predict(Av1PredictionMode.Directional45Degrees, 1);

    [Benchmark]
    public int PredictZ2() => this.Predict(Av1PredictionMode.Directional135Degrees, 1);

    [Benchmark]
    public int PredictZ3() => this.Predict(Av1PredictionMode.Directional203Degrees, 1);

    [Benchmark]
    public int PredictVertical() => this.Predict(Av1PredictionMode.Vertical, 0);

    [Benchmark]
    public int PredictSmooth() => this.Predict(Av1PredictionMode.Smooth, 0);

    [Benchmark]
    public int PredictPaeth() => this.Predict(Av1PredictionMode.Paeth, 0);

    /// <summary>
    /// Measures one transform-type search candidate: transform, gate, quantizer and refinement or cost.
    /// </summary>
    /// <returns>The candidate rate.</returns>
    [Benchmark]
    public int SearchCandidate()
    {
        Av1EncoderTransformBlockState state = default;
        return Av1TransformBlockEncoder.EncodeTypeSearchCandidate(
            this.blockWorkspace,
            this.writer,
            default,
            this.residual,
            this.Size,
            this.quantized,
            this.dequantized,
            this.TransformSize,
            Av1TransformType.DctDct,
            Av1PredictionMode.DC,
            Av1FilterIntraMode.AllFilterIntraModes,
            false,
            false,
            QIndex,
            0,
            0,
            Av1BitDepth.EightBit,
            Av1ComponentType.Luminance,
            128,
            false,
            true,
            false,
            uint.MaxValue,
            false,
            0,
            ref state,
            out _);
    }

    /// <summary>
    /// Measures one transform-type search candidate without refinement.
    /// </summary>
    /// <returns>The candidate rate.</returns>
    [Benchmark]
    public int SearchCandidateNoTrellis()
    {
        Av1EncoderTransformBlockState state = default;
        return Av1TransformBlockEncoder.EncodeTypeSearchCandidate(
            this.blockWorkspace,
            this.writer,
            default,
            this.residual,
            this.Size,
            this.quantized,
            this.dequantized,
            this.TransformSize,
            Av1TransformType.DctDct,
            Av1PredictionMode.DC,
            Av1FilterIntraMode.AllFilterIntraModes,
            false,
            false,
            QIndex,
            0,
            0,
            Av1BitDepth.EightBit,
            Av1ComponentType.Luminance,
            128,
            false,
            true,
            true,
            uint.MaxValue,
            false,
            0,
            ref state,
            out _);
    }

    [Benchmark]
    public int Subtract()
    {
        Av1ResidualBuilder.Subtract(this.source, this.Size, this.prediction, this.Size, this.residual, this.Size, this.Size, this.Size);
        return this.residual[0];
    }

    private int Predict(Av1PredictionMode mode, int angleDelta)
    {
        Av1TransformBlockEncoder.PrepareIntraPrediction(
            this.blockWorkspace,
            this.source,
            this.Size,
            this.prediction,
            this.Size,
            this.above,
            this.left,
            true,
            true,
            mode,
            angleDelta,
            true,
            false,
            this.residual,
            this.TransformSize);

        return this.residual[0];
    }
}
