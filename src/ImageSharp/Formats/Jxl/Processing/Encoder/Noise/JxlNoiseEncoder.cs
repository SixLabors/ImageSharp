// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics.Tensors;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.AuxiliaryOutput;
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

    public static float GetScoreSumsOfAbsoluteDifferences(JxlImage3<float> opsin, int x, int y, int blockSize)
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
        JxlImage3<float> opsin,
        int blockSize,
        int binCount,
        JxlNoiseHistogram sadHistogram)
    {
        IMemoryOwner<float> sadScores = configuration.MemoryAllocator.Allocate<float>((opsin.YSize / blockSize) * (opsin.XSize / blockSize));
        Span<float> sadScoresData = sadScores.GetSpan();
        int blockIndex = 0;

        for (int y = 0; y <= opsin.YSize - blockSize; y += blockSize)
        {
            for (int x = 0; x <= opsin.XSize - blockSize; x += blockSize)
            {
                float score = GetScoreSumsOfAbsoluteDifferences(opsin, x, y, blockSize);
                sadScoresData[blockIndex++] = score;
                sadHistogram.Increment(score * binCount);
            }
        }

        return sadScores;
    }

    /// <summary>
    /// Fits the eight noise lookup values to a nonempty set of patch measurements.
    /// </summary>
    /// <param name="noiseLevel">The measurements selected by the patch threshold.</param>
    /// <param name="noiseParameters">The lookup values to replace, or clear when the fit error is too large.</param>
    /// <param name="multiplier">The strength applied to the fitted lookup values.</param>
    public static void OptimizeNoiseParameters(List<JxlNoiseLevel> noiseLevel, JxlNoiseParameters noiseParameters, float multiplier)
    {
        const double maxError = 1e-3;
        ReadOnlySpan<JxlNoiseLevel> noiseLevelSpan = CollectionsMarshal.AsSpan(noiseLevel);

        // Start with the measured mean at every lookup point. Patch selection guarantees at least one measurement.
        float avg = 0;

        for (int i = 0; i < noiseLevelSpan.Length; i++)
        {
            avg += noiseLevelSpan[i].NoiseLevel;
        }

        avg /= noiseLevelSpan.Length;

        // The curve always has eight points. Its numeric workspace stays on the stack and owns no pooled memory.
        InlineArray8<double> parameterStorage = default;
        Span<double> parameters = parameterStorage;
        parameters.Fill(avg);
        OptimizeNoiseCurve(noiseLevelSpan, parameters);

        // The lookup is stored as floats. Apply that rounding before measuring the final fit against the codestream limits.
        for (int i = 0; i < parameters.Length; i++)
        {
            parameters[i] = Math.Clamp((float)(parameters[i] * multiplier), 0F, JxlNoiseConstants.NoiseLutMax);
        }

        InlineArray8<double> unusedGradient = default;
        float loss = (float)(ComputeNoiseLoss(noiseLevelSpan, parameters, unusedGradient, false) / noiseLevelSpan.Length);

        if (loss > maxError)
        {
            noiseParameters.Clear();
            return;
        }

        for (int i = 0; i < parameters.Length; i++)
        {
            noiseParameters.Lookup[i] = (float)parameters[i];
        }
    }

    /// <summary>
    /// Fits a noise curve with scaled conjugate gradients and independent accepted and trial state.
    /// </summary>
    /// <param name="levels">The nonempty patch measurements.</param>
    /// <param name="parameters">The eight initial values, replaced with the last accepted curve.</param>
    private static void OptimizeNoiseCurve(ReadOnlySpan<JxlNoiseLevel> levels, Span<double> parameters)
    {
        const double gradientNormThreshold = 1e-8;
        const double squaredGradientThreshold = gradientNormThreshold * gradientNormThreshold;
        const double probeScale = 0.0001;
        const double minimumDamping = 1e-15;
        const double maximumDamping = 1e15;
        const int maximumIterations = 40;

        // Each span has its own eight doubles. Accepting a trial must copy values, not alias the next trial's storage.
        // TensorPrimitives selects the available SIMD width and handles the scalar remainder of these small spans.
        InlineArray8<double> trialStorage = default;
        InlineArray8<double> gradientStorage = default;
        InlineArray8<double> trialGradientStorage = default;
        InlineArray8<double> previousGradientStorage = default;
        InlineArray8<double> directionStorage = default;
        InlineArray8<double> differenceStorage = default;
        Span<double> trial = trialStorage;
        Span<double> gradient = gradientStorage;
        Span<double> trialGradient = trialGradientStorage;
        Span<double> previousGradient = previousGradientStorage;
        Span<double> direction = directionStorage;
        Span<double> difference = differenceStorage;

        double loss = ComputeNoiseLoss(levels, parameters, gradient, true);
        gradient.CopyTo(direction);
        double squaredGradient = TensorPrimitives.Dot<double>(gradient, gradient);
        double damping = 1;
        double slope = 0;
        double squaredDirection = 0;
        double curvature = 0;
        bool accepted = true;
        int acceptedCount = 0;

        // Constant measurements can already match the initial flat curve. Stop before dividing by its zero direction norm.
        for (int iteration = 0; iteration < maximumIterations && squaredGradient > squaredGradientThreshold; iteration++)
        {
            if (accepted)
            {
                // ComputeNoiseLoss returns the negative gradient. A negative slope therefore denotes a descent direction.
                slope = -TensorPrimitives.Dot<double>(direction, gradient);
                if (slope >= 0)
                {
                    gradient.CopyTo(direction);
                    slope = -TensorPrimitives.Dot<double>(direction, gradient);
                }

                // Probe the gradient a short distance along the direction to estimate its curvature.
                squaredDirection = TensorPrimitives.Dot<double>(direction, direction);
                double probeStep = probeScale / Math.Sqrt(squaredDirection);
                TensorPrimitives.Multiply(direction, probeStep, trial);
                TensorPrimitives.Add(parameters, trial, trial);
                _ = ComputeNoiseLoss(levels, trial, trialGradient, true);
                TensorPrimitives.Subtract(gradient, trialGradient, difference);
                curvature = TensorPrimitives.Dot<double>(direction, difference) / probeStep;
            }

            // Positive damping keeps the trial step usable when the measured curvature is not positive.
            double dampedCurvature = curvature + (damping * squaredDirection);
            if (dampedCurvature <= 0)
            {
                dampedCurvature = damping * squaredDirection;
                damping -= curvature / squaredDirection;
            }

            double step = -slope / dampedCurvature;
            TensorPrimitives.Multiply(direction, step, trial);
            TensorPrimitives.Add(parameters, trial, trial);
            double trialLoss = ComputeNoiseLoss(levels, trial, trialGradient, true);
            double agreement = 2 * (trialLoss - loss) / (step * slope);
            accepted = agreement >= 0;

            if (accepted)
            {
                acceptedCount++;
                trial.CopyTo(parameters);
                gradient.CopyTo(previousGradient);
                trialGradient.CopyTo(gradient);
                squaredGradient = TensorPrimitives.Dot<double>(gradient, gradient);
                loss = trialLoss;

                if (squaredGradient <= squaredGradientThreshold)
                {
                    break;
                }
            }

            // Rejecting a step retains the accepted curve and gradient. Retry with the same curvature and stronger damping.
            if (agreement < 0.25)
            {
                damping = Math.Min(4 * damping, maximumDamping);
            }
            else if (agreement > 0.75)
            {
                damping = Math.Max(0.25 * damping, minimumDamping);
            }

            // Restart after eight accepted steps. Otherwise combine the new gradient with the previous search direction.
            if (acceptedCount % JxlNoiseParameters.NoisePoints == 0)
            {
                gradient.CopyTo(direction);
                damping = 1;
            }
            else if (accepted)
            {
                TensorPrimitives.Subtract(previousGradient, gradient, difference);
                double conjugateWeight = TensorPrimitives.Dot<double>(difference, gradient) / slope;
                TensorPrimitives.Multiply(direction, conjugateWeight, direction);
                TensorPrimitives.Add(direction, gradient, direction);
            }
        }
    }

    /// <summary>
    /// Measures the noise-curve error and writes its negative gradient without changing the curve.
    /// </summary>
    /// <param name="levels">The patch measurements.</param>
    /// <param name="parameters">The eight lookup values.</param>
    /// <param name="negativeGradient">Eight independent output values for the negative gradient.</param>
    /// <param name="includeRegularization">Whether to penalize differences between adjacent lookup values.</param>
    /// <returns>The summed fitting error, including the selected regularization.</returns>
    private static double ComputeNoiseLoss(
        ReadOnlySpan<JxlNoiseLevel> levels,
        ReadOnlySpan<double> parameters,
        Span<double> negativeGradient,
        bool includeRegularization)
    {
        const double regularization = 0.005;
        const double overestimateWeight = 1.1;
        negativeGradient.Clear();
        double loss = 0;

        // Each measurement interpolates two adjacent lookup points. Overestimates receive a slightly larger penalty.
        // The gradient sign and scale match the scaled conjugate-gradient calculation above.
        for (int i = 0; i < levels.Length; i++)
        {
            JxlNoiseLevel level = levels[i];
            JxlNoiseIndexAndFraction position = JxlNoiseHelper.IndexAndFraction(level.Intensity);
            double low = parameters[position.Index];
            double high = parameters[position.Index + 1];
            double value = (low * (1F - position.Fraction)) + (high * position.Fraction);
            double distance = value - level.NoiseLevel;

            if (distance > 0)
            {
                loss += overestimateWeight * distance * distance;
                negativeGradient[position.Index] -= overestimateWeight * (1F - position.Fraction) * distance;
                negativeGradient[position.Index + 1] -= overestimateWeight * position.Fraction * distance;
            }
            else
            {
                loss += distance * distance;
                negativeGradient[position.Index] -= (1F - position.Fraction) * distance;
                negativeGradient[position.Index + 1] -= position.Fraction * distance;
            }
        }

        if (includeRegularization)
        {
            // Scale smoothing by the measurement count so its strength does not depend on the number of selected patches.
            for (int i = 0; i + 1 < parameters.Length; i++)
            {
                double difference = parameters[i] - parameters[i + 1];
                loss += regularization * levels.Length * difference * difference;
                negativeGradient[i] -= regularization * difference * levels.Length;
                negativeGradient[i + 1] += regularization * difference * levels.Length;
            }
        }

        return loss;
    }

    public static List<JxlNoiseLevel> GetNoiseLevel(
        JxlImage3<float> opsin,
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

    /// <summary>
    /// Estimates a noise lookup from selected patches and clears it when no usable fit is available.
    /// </summary>
    /// <param name="configuration">The allocator configuration for patch scores.</param>
    /// <param name="opsin">The borrowed image planes used to measure patch noise.</param>
    /// <param name="noiseParams">The lookup values to replace or clear.</param>
    /// <param name="qualityCoefficient">The strength used to scale the fitted noise.</param>
    /// <returns>Whether the fitted lookup contains a nonzero noise level.</returns>
    public static bool GetNoiseParameter(Configuration configuration, JxlImage3<float> opsin, JxlNoiseParameters noiseParams, float qualityCoefficient)
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

        // The histogram threshold can exclude every patch. There is then no measured curve to average or fit.
        if (nl.Count == 0)
        {
            noiseParams.Clear();
            return false;
        }

        OptimizeNoiseParameters(nl, noiseParams, qualityCoefficient * 1.4f);

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
}
