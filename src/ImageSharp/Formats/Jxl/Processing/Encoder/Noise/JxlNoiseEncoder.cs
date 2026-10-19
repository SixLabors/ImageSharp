// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics.Tensors;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.AuxiliaryOutput;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Optimize;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Noise;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Noise;

/// <summary>
/// Noise functions for encoder.
/// </summary>
internal static class JxlNoiseEncoder
{
    private static readonly float[][] LaplacianFilter =
    [
        [-0.25f, -1.0f, -0.25f],
        [-1.0f, 5.0f, -1.0f],
        [-0.25f, -1.0f, -0.25f],
    ];

    public static float GetScoreSumsOfAbsoluteDifferences(JxlImage3F opsin, int x, int y, int blockSize)
    {
        const int smallBlockSizeX = 3;
        const int smallBlockSizeY = 4;

        int numSAD = (blockSize - smallBlockSizeX) * (blockSize - smallBlockSizeY);
        int counter = 0;
        const int offset = 2;

        float[]? pooled = null;

        Span<float> sad = numSAD <= 128
            ? stackalloc float[128].Slice(0, numSAD)
            : pooled = ArrayPool<float>.Shared.Rent(numSAD);

        for (int yBl = 0; yBl + smallBlockSizeY < blockSize; ++yBl)
        {
            for (int xBl = 0; xBl + smallBlockSizeX < blockSize; ++xBl)
            {
                float sadSum = 0;

                for (int cy = 0; cy < smallBlockSizeY; ++cy)
                {
                    for (int cx = 0; cx < smallBlockSizeX; ++cx)
                    {
                        float wnd = 0.5f * (opsin.PlaneRow(1, y + yBl + cy)[x + xBl + cx] + opsin.PlaneRow(0, y + yBl + cy)[x + xBl + cx]);
                        float center = 0.5f * (opsin.PlaneRow(1, y + offset + cy)[x + offset + cx] + opsin.PlaneRow(0, y + offset + cy)[x + offset + cx]);
                        sadSum += MathF.Abs(center - wnd);
                    }
                }

                sad[counter++] = sadSum;
            }
        }

        int samples = numSAD / 2;

        // As with ROAD (rank order absolute distance), we keep the smallest half of
        // the values in SAD (we use here the more robust patch SAD instead of
        // absolute single-pixel differences).
        sad.Sort();

        float totalSadSum = TensorPrimitives.Sum(sad);

        if (pooled is not null)
        {
            ArrayPool<float>.Shared.Return(pooled);
        }

        return totalSadSum / samples;
    }

    public static IMemoryOwner<float> GetSadScoresForPatches(
        Configuration configuration,
        JxlImage3F opsin,
        int blockSize,
        int binCount,
        JxlNoiseHistogram sadHistogram)
    {
        IMemoryOwner<float> sadScores = configuration.MemoryAllocator.Allocate<float>((opsin.YSize / blockSize) * (opsin.XSize / blockSize));
        Span<float> sadScoresData = sadScores.GetSpan();
        int blockIndex = 0;

        for (int y = 0; y + blockSize <= opsin.YSize; y += blockSize)
        {
            for (int x = 0; x + blockSize <= opsin.XSize; x += blockSize)
            {
                float score = GetScoreSumsOfAbsoluteDifferences(opsin, x, y, blockSize);
                sadScoresData[blockIndex++] = score;
                sadHistogram.Increment(score * binCount);
            }
        }

        return sadScores;
    }

    public static void OptimizeNoiseParameters(Configuration configuration, List<JxlNoiseLevel> noiseLevel, JxlNoiseParameters noiseParameters, float multiplier)
    {
        const double maxError = 1e-3;
        const double precision = 1e-8;
        const int maxIterations = 40;

        Span<JxlNoiseLevel> noiseLevelSpan = CollectionsMarshal.AsSpan(noiseLevel);

        // TODO: this is equivalent to .Average(x => x.NoiseLevel).
        // Unfortunately we can't use LINQ on a Span<T> or Memory<T> though.
        float avg = 0;

        for (int i = 0; i < noiseLevelSpan.Length; i++)
        {
            avg += noiseLevelSpan[i].NoiseLevel;
        }

        avg /= noiseLevelSpan.Length;

        JxlLossFunction lossFunction = new(noiseLevel);
        JxlOptimizeArray<double> parameterVector = new(configuration, OptimizeArrayWidth, avg);

        parameterVector = JxlOptimizeArray<double>.OptimizeWithScaledConjugateGradientMethod(
            new Factory(configuration),
            lossFunction.OptimizeFunction,
            parameterVector,
            precision,
            maxIterations);

        // Clamp to account codestream limits
        // TODO: TensorPrimitives
        for (int i = 0; i < parameterVector.Span.Length; i++)
        {
            parameterVector[i] = Math.Clamp(parameterVector[i] * multiplier, 0.0f, JxlNoiseConstants.NoiseLutMax);
        }

        Span<double> unused = stackalloc double[parameterVector.Span.Length];
        double loss = lossFunction.Compute(parameterVector.Span, unused, skipRegularization: true) / noiseLevel.Count;

        if (loss > maxError)
        {
            noiseParameters.Clear();
            return;
        }

        for (int i = 0; i < parameterVector.Span.Length; i++)
        {
            noiseParameters.Lookup[i] = (float)parameterVector.Span[i];
        }
    }

    public static List<JxlNoiseLevel> GetNoiseLevel(
        JxlImage3F opsin,
        Span<float> textureStrength,
        float threshold,
        int blockSize)
    {
        List<JxlNoiseLevel> noiseLevelPerIntensity = [];
        const int filterSize = 1;
        int patchIndex = 0;

        for (int y = 0; y + blockSize <= opsin.YSize; y += blockSize)
        {
            for (int x = 0; x + blockSize <= opsin.XSize; x += blockSize)
            {
                if (textureStrength[patchIndex] <= threshold)
                {
                    float meanInt = 0;

                    for (int yBlock = 0; yBlock < blockSize; ++yBlock)
                    {
                        for (int xBlock = 0; xBlock < blockSize; ++xBlock)
                        {
                            meanInt += 0.5f * (opsin.PlaneRow(1, y + yBlock)[x + xBlock] +
                                                opsin.PlaneRow(0, y + yBlock)[x + xBlock]);
                        }
                    }

                    meanInt /= blockSize * blockSize;

                    float noiseLevel = 0;
                    int count = 0;

                    for (int yBlock = 0; yBlock < blockSize; ++yBlock)
                    {
                        for (int xBlock = 0; xBlock < blockSize; ++xBlock)
                        {
                            float filteredValue = 0;

                            for (int yFilter = -1 * filterSize; yFilter <= filterSize; ++yFilter)
                            {
                                if (yBlock + yFilter >= 0 && (yBlock + yFilter) < blockSize)
                                {
                                    for (int xFilter = -1 * filterSize; xFilter <= filterSize; ++xFilter)
                                    {
                                        if ((xBlock + xFilter) >= 0 && (xBlock + xFilter) < blockSize)
                                        {
                                            filteredValue +=
                                                0.5f *
                                                (opsin.PlaneRow(1, y + yBlock + yFilter)[x + xBlock + xFilter] +
                                                 opsin.PlaneRow(0, y + yBlock + yFilter)[x + xBlock + xFilter]) *
                                                LaplacianFilter[yFilter + filterSize][xFilter + filterSize];
                                        }
                                        else
                                        {
                                            filteredValue +=
                                                0.5f *
                                                (opsin.PlaneRow(1, y + yBlock + yFilter)[x + xBlock - xFilter] +
                                                 opsin.PlaneRow(0, y + yBlock + yFilter)[x + xBlock - xFilter]) *
                                                LaplacianFilter[yFilter + filterSize][xFilter + filterSize];
                                        }
                                    }
                                }
                                else
                                {
                                    for (int xFilter = -1 * filterSize; xFilter <= filterSize; ++xFilter)
                                    {
                                        if ((xBlock + xFilter) >= 0 &&
                                            (xBlock + xFilter) < blockSize)
                                        {
                                            filteredValue +=
                                                0.5f *
                                                (opsin.PlaneRow(1, y + yBlock - yFilter)[x + xBlock + xFilter] +
                                                 opsin.PlaneRow(0, y + yBlock - yFilter)[x + xBlock + xFilter]) *
                                                LaplacianFilter[yFilter + filterSize][xFilter + filterSize];
                                        }
                                        else
                                        {
                                            filteredValue +=
                                                0.5f *
                                                (opsin.PlaneRow(1, y + yBlock - yFilter)[x + xBlock - xFilter] +
                                                 opsin.PlaneRow(0, y + yBlock - yFilter)[x + xBlock - xFilter]) *
                                                LaplacianFilter[yFilter + filterSize][xFilter + filterSize];
                                        }
                                    }
                                }
                            }

                            noiseLevel += MathF.Abs(filteredValue);
                            count++;
                        }
                    }

                    noiseLevel /= count;

                    JxlNoiseLevel nl = default;
                    nl.Intensity = meanInt;
                    nl.NoiseLevel = noiseLevel;

                    noiseLevelPerIntensity.Add(nl);
                }

                patchIndex++;
            }
        }

        return noiseLevelPerIntensity;
    }

    public static float GetSadThreshold(JxlNoiseHistogram histogram) => (float)histogram.Mode / JxlNoiseHistogram.Bins;

    public static void EncodeFloatParameter(float value, float precision, JxlBitWriter writer)
    {
        if (value < 0)
        {
            throw new InvalidOperationException("Float parameter is negative");
        }

        int absValQuant = (int)MathF.Round(value * precision, MidpointRounding.AwayFromZero);

        if (absValQuant >= 1 << 10)
        {
            throw new InvalidOperationException("Absolute value quant overflows");
        }

        writer.Write(10, absValQuant);
    }

    public static bool GetNoiseParameter(Configuration configuration, JxlImage3F opsin, JxlNoiseParameters noiseParams, float qualityCoefficient)
    {
        const int blockSize = 8;
        const int binCount = 256;

        JxlNoiseHistogram sadHistogram = new();
        using IMemoryOwner<float> sadScores = GetSadScoresForPatches(configuration, opsin, blockSize, binCount, sadHistogram);
        float sadThreshold = GetSadThreshold(sadHistogram);

        if (sadThreshold is > 0.15f or <= 0.0f)
        {
            noiseParams.Clear();
            return false;
        }

        List<JxlNoiseLevel> nl = GetNoiseLevel(opsin, sadScores.GetSpan(), sadThreshold, blockSize);
        OptimizeNoiseParameters(configuration, nl, noiseParams, qualityCoefficient * 1.4f);

        return noiseParams.ContainsAny;
    }

    public static bool EncodeNoise(JxlNoiseParameters noiseParams, JxlBitWriter writer, JxlLayerType layer, JxlAuxiliaryOutput auxOut)
    {
        if (!noiseParams.ContainsAny)
        {
            throw new InvalidOperationException("Noise parameters are empty");
        }

        return writer.WithMaxBits(
            JxlNoiseParameters.NoisePoints * 16, layer, auxOut, () =>
            {
                // Fast iteration without having to enumerate
                // through the generic IEnumerable<T>
                Span<float> fastLookup = noiseParams.Lookup;
                for (int i = 0; i < fastLookup.Length; i++)
                {
                    float item = fastLookup[i];
                    EncodeFloatParameter(item, JxlNoiseConstants.Precision, writer);
                }

                return true;
            });
    }

    private sealed class Factory : JxlOptimizeArrayFactory<double>
    {
        public Factory(Configuration configuration)
            : base(configuration, OptimizeArrayWidth)
        {
        }
    }
}
