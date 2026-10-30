// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.AuxiliaryOutput;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Noise;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Processing.Processors.Convolution;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Noise;

/// <summary>
/// Noise functions for encoder.
/// </summary>
internal static class JxlNoiseEncoder
{
    /// <summary>
    /// The width and height of each patch used to estimate noise.
    /// </summary>
    private const int PatchSize = 8;

    /// <summary>
    /// The width of each window compared with the central window inside a patch.
    /// </summary>
    private const int ComparisonWidth = 3;

    /// <summary>
    /// The height of each window compared with the central window inside a patch.
    /// </summary>
    private const int ComparisonHeight = 4;

    /// <summary>
    /// Gets the native row-major Laplacian coefficients for an eight-by-eight noise patch.
    /// </summary>
    private static ReadOnlySpan<float> LaplacianFilter =>
    [
        -0.25F, -1F, -0.25F,
        -1F, 5F, -1F,
        -0.25F, -1F, -0.25F
    ];

    /// <summary>
    /// Prepares one contiguous intensity patch from source spans borrowed before image traversal.
    /// </summary>
    /// <param name="planeX">The complete X-plane allocation from its logical origin.</param>
    /// <param name="planeY">The complete Y-plane allocation from its logical origin.</param>
    /// <param name="strideX">The physical X-plane row stride.</param>
    /// <param name="strideY">The physical Y-plane row stride.</param>
    /// <param name="x">The complete patch's left column.</param>
    /// <param name="y">The complete patch's top row.</param>
    /// <param name="patch">The reused eight-by-eight row-major intensity storage.</param>
    private static void PrepareIntensityPatch(
        ReadOnlySpan<float> planeX,
        ReadOnlySpan<float> planeY,
        int strideX,
        int strideY,
        int x,
        int y,
        Span<float> patch)
    {
        for (int row = 0; row < PatchSize; row++)
        {
            // Native intensity is (Y+X)*0.5. Preserve that operation order and use the shared binary SIMD traversal.
            TensorPrimitives_.Invoke(
                planeY.Slice(((y + row) * strideY) + x, PatchSize),
                planeX.Slice(((y + row) * strideX) + x, PatchSize),
                patch.Slice(row * PatchSize, PatchSize),
                default(MeanIntensityOperator));
        }
    }

    /// <summary>
    /// Measures the mean of the ten smallest window differences inside one prepared intensity patch.
    /// </summary>
    /// <param name="patch">The complete eight-by-eight row-major intensity patch.</param>
    /// <param name="workspace">Four eight-slot score rows. Only the first five positions in each row contribute.</param>
    /// <returns>The least-textured half's mean absolute difference.</returns>
    private static float GetScoreSumsOfAbsoluteDifferences(ReadOnlySpan<float> patch, Span<float> workspace)
    {
        const int comparisonRows = PatchSize - ComparisonHeight;
        const int comparisonColumns = PatchSize - ComparisonWidth;
        const int centerOffset = 2;
        ref float first = ref MemoryMarshal.GetReference(patch);
        ref float output = ref MemoryMarshal.GetReference(workspace);

        // Native uses strict window-end bounds: four rows of five windows, with the central window starting at (2,2).
        // A lane is an independent comparison window. Every lane accumulates the same twelve terms in native cy/cx order.
        // The eight-slot row stride matches the patch. Vector512 covers two comparison rows; Vector256 covers one.
        if (Vector512.IsHardwareAccelerated)
        {
            for (int row = 0; row < comparisonRows; row += 2)
            {
                Vector512<float> sum = Vector512<float>.Zero;
                for (int cy = 0; cy < ComparisonHeight; cy++)
                {
                    for (int cx = 0; cx < ComparisonWidth; cx++)
                    {
                        float center = patch[((centerOffset + cy) * PatchSize) + centerOffset + cx];
                        Vector512<float> windows = Vector512.LoadUnsafe(ref first, (uint)(((row + cy) * PatchSize) + cx));
                        sum += Vector512.Abs(Vector512.Create(center) - windows);
                    }
                }

                sum.StoreUnsafe(ref output, (uint)(row * PatchSize));
            }
        }
        else if (Vector256.IsHardwareAccelerated)
        {
            for (int row = 0; row < comparisonRows; row++)
            {
                Vector256<float> sum = Vector256<float>.Zero;
                for (int cy = 0; cy < ComparisonHeight; cy++)
                {
                    for (int cx = 0; cx < ComparisonWidth; cx++)
                    {
                        float center = patch[((centerOffset + cy) * PatchSize) + centerOffset + cx];
                        Vector256<float> windows = Vector256.LoadUnsafe(ref first, (uint)(((row + cy) * PatchSize) + cx));
                        sum += Vector256.Abs(Vector256.Create(center) - windows);
                    }
                }

                sum.StoreUnsafe(ref output, (uint)(row * PatchSize));
            }
        }
        else if (Vector128.IsHardwareAccelerated)
        {
            // Four lanes cover the first four windows. The fifth uses the same scalar terms without reading a padding lane.
            for (int row = 0; row < comparisonRows; row++)
            {
                Vector128<float> sum = Vector128<float>.Zero;
                float last = 0;
                for (int cy = 0; cy < ComparisonHeight; cy++)
                {
                    for (int cx = 0; cx < ComparisonWidth; cx++)
                    {
                        float center = patch[((centerOffset + cy) * PatchSize) + centerOffset + cx];
                        int source = ((row + cy) * PatchSize) + cx;
                        sum += Vector128.Abs(Vector128.Create(center) - Vector128.LoadUnsafe(ref first, (uint)source));
                        last += MathF.Abs(center - patch[source + comparisonColumns - 1]);
                    }
                }

                sum.StoreUnsafe(ref output, (uint)(row * PatchSize));
                workspace[(row * PatchSize) + comparisonColumns - 1] = last;
            }
        }
        else
        {
            for (int row = 0; row < comparisonRows; row++)
            {
                for (int column = 0; column < comparisonColumns; column++)
                {
                    float sum = 0;
                    for (int cy = 0; cy < ComparisonHeight; cy++)
                    {
                        for (int cx = 0; cx < ComparisonWidth; cx++)
                        {
                            float center = patch[((centerOffset + cy) * PatchSize) + centerOffset + cx];
                            float window = patch[((row + cy) * PatchSize) + column + cx];
                            sum += MathF.Abs(center - window);
                        }
                    }

                    workspace[(row * PatchSize) + column] = sum;
                }
            }
        }

        // Wider registers compute unused lanes, but all source loads stay within the initialized 64-value patch (last index 57).
        // Compact only the five real windows from each row. Never include the three spare slots in selection or reduction.
        for (int row = 1; row < comparisonRows; row++)
        {
            workspace.Slice(row * PatchSize, comparisonColumns).CopyTo(workspace.Slice(row * comparisonColumns, comparisonColumns));
        }

        Span<float> sad = workspace[..(comparisonRows * comparisonColumns)];
        int samples = sad.Length / 2;

        // Native sorts these twenty scores and sums the first ten in order. Keep that reduction order after SIMD window evaluation.
        sad.Sort();
        float total = 0;

        for (int i = 0; i < samples; i++)
        {
            total += sad[i];
        }

        return total / samples;
    }

    /// <summary>
    /// Allocates patch scores and fills them with source spans and bounded workspace borrowed by the operation owner.
    /// </summary>
    /// <param name="configuration">The allocator configuration for the returned scores.</param>
    /// <param name="opsin">The source image dimensions and physical row strides.</param>
    /// <param name="planeX">The borrowed complete X-plane storage.</param>
    /// <param name="planeY">The borrowed complete Y-plane storage.</param>
    /// <param name="workspace">The reused intensity and score workspace.</param>
    /// <param name="binCount">The scale used before histogram indexing.</param>
    /// <param name="sadHistogram">The histogram updated by completed patches.</param>
    /// <returns>The completed scores. The caller owns and disposes their allocation.</returns>
    private static IMemoryOwner<float> GetSadScoresForPatches(
        Configuration configuration,
        JxlImage3<float> opsin,
        ReadOnlySpan<float> planeX,
        ReadOnlySpan<float> planeY,
        Span<float> workspace,
        int binCount,
        JxlNoiseHistogram sadHistogram)
    {
        int count = (opsin.YSize / PatchSize) * (opsin.XSize / PatchSize);
        IMemoryOwner<float> sadScores = configuration.MemoryAllocator.Allocate<float>(count);

        try
        {
            Span<float> scores = sadScores.GetSpan()[..count];
            Span<float> patch = workspace[..(PatchSize * PatchSize)];
            Span<float> sad = workspace.Slice(PatchSize * PatchSize, (PatchSize - ComparisonHeight) * PatchSize);
            int strideX = opsin.Plane(0).PixelsPerRow;
            int strideY = opsin.Plane(1).PixelsPerRow;
            int blockIndex = 0;

            for (int y = 0; y <= opsin.YSize - PatchSize; y += PatchSize)
            {
                for (int x = 0; x <= opsin.XSize - PatchSize; x += PatchSize)
                {
                    PrepareIntensityPatch(planeX, planeY, strideX, strideY, x, y, patch);
                    float score = GetScoreSumsOfAbsoluteDifferences(patch, sad);
                    scores[blockIndex++] = score;
                    sadHistogram.Increment(score * binCount);
                }
            }

            return sadScores;
        }
        catch
        {
            sadScores.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Fits the eight noise lookup values to a nonempty set of patch measurements.
    /// </summary>
    /// <param name="noiseLevel">The measurements selected by the patch threshold.</param>
    /// <param name="noiseParameters">The lookup values to replace, or clear when the fit error is too large.</param>
    /// <param name="multiplier">The strength applied to the fitted lookup values.</param>
    public static void OptimizeNoiseParameters(ReadOnlySpan<JxlNoiseLevel> noiseLevel, JxlNoiseParameters noiseParameters, float multiplier)
    {
        const double maxError = 1e-3;

        // Start with the measured mean at every lookup point. Patch selection guarantees at least one measurement.
        float avg = 0;

        for (int i = 0; i < noiseLevel.Length; i++)
        {
            avg += noiseLevel[i].NoiseLevel;
        }

        avg /= noiseLevel.Length;

        // The curve always has eight points. Its numeric workspace stays on the stack and owns no pooled memory.
        InlineArray8<double> parameterStorage = default;
        Span<double> parameters = parameterStorage;
        parameters.Fill(avg);
        OptimizeNoiseCurve(noiseLevel, parameters);

        // The lookup is stored as floats. Apply that rounding before measuring the final fit against the codestream limits.
        for (int i = 0; i < parameters.Length; i++)
        {
            parameters[i] = Math.Clamp((float)(parameters[i] * multiplier), 0F, JxlNoiseConstants.NoiseLutMax);
        }

        InlineArray8<double> unusedGradient = default;
        float loss = (float)(ComputeNoiseLoss(noiseLevel, parameters, unusedGradient, false) / noiseLevel.Length);

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

    /// <summary>
    /// Applies the native Laplacian to one intensity patch with shared Bounce sampling offsets.
    /// </summary>
    /// <param name="patch">The complete eight-by-eight intensity patch.</param>
    /// <param name="filtered">The reused eight-by-eight filtered output.</param>
    /// <param name="rows">Three shared sampling offsets for each output row.</param>
    /// <param name="columns">Three shared sampling offsets for each output column.</param>
    /// <param name="leftIndices">Valid left-neighbor indices for two eight-value rows.</param>
    /// <param name="rightIndices">Valid right-neighbor indices for two eight-value rows.</param>
    private static void FilterIntensityPatch(
        ReadOnlySpan<float> patch,
        Span<float> filtered,
        ReadOnlySpan<int> rows,
        ReadOnlySpan<int> columns,
        in Vector512<int> leftIndices,
        in Vector512<int> rightIndices)
    {
        ReadOnlySpan<float> coefficients = LaplacianFilter;
        ref float first = ref MemoryMarshal.GetReference(patch);
        ref float output = ref MemoryMarshal.GetReference(filtered);

        // Every lane is one output position. Add all nine products in native row-major tap order; do not group rows or use FMA.
        // Shared Bounce offsets select row 1 above row 0 and row 6 below row 7. The patch boundary is independent of the image boundary.
        if (Vector512.IsHardwareAccelerated)
        {
            for (int y = 0; y < PatchSize; y += 2)
            {
                Vector512<float> sum = Vector512<float>.Zero;
                for (int tapRow = 0; tapRow < 3; tapRow++)
                {
                    Vector256<float> lower = Vector256.LoadUnsafe(ref first, (uint)(rows[(y * 3) + tapRow] * PatchSize));
                    Vector256<float> upper = Vector256.LoadUnsafe(ref first, (uint)(rows[((y + 1) * 3) + tapRow] * PatchSize));
                    Vector512<float> center = Vector512.Create(lower, upper);

                    // Lower/upper halves hold complete eight-value rows. Indices remain within their own row and are all valid.
                    // The native index-vector shuffle therefore matches Shuffle without adding out-of-range zeroing.
                    Vector512<float> left = Vector512.ShuffleNative(center, leftIndices);
                    Vector512<float> right = Vector512.ShuffleNative(center, rightIndices);
                    sum += left * Vector512.Create(coefficients[tapRow * 3]);
                    sum += center * Vector512.Create(coefficients[(tapRow * 3) + 1]);
                    sum += right * Vector512.Create(coefficients[(tapRow * 3) + 2]);
                }

                sum.StoreUnsafe(ref output, (uint)(y * PatchSize));
            }
        }
        else if (Vector256.IsHardwareAccelerated)
        {
            Vector256<int> leftMask = leftIndices.GetLower();
            Vector256<int> rightMask = rightIndices.GetLower();

            for (int y = 0; y < PatchSize; y++)
            {
                Vector256<float> sum = Vector256<float>.Zero;
                for (int tapRow = 0; tapRow < 3; tapRow++)
                {
                    Vector256<float> center = Vector256.LoadUnsafe(ref first, (uint)(rows[(y * 3) + tapRow] * PatchSize));
                    Vector256<float> left = Vector256.ShuffleNative(center, leftMask);
                    Vector256<float> right = Vector256.ShuffleNative(center, rightMask);
                    sum += left * Vector256.Create(coefficients[tapRow * 3]);
                    sum += center * Vector256.Create(coefficients[(tapRow * 3) + 1]);
                    sum += right * Vector256.Create(coefficients[(tapRow * 3) + 2]);
                }

                sum.StoreUnsafe(ref output, (uint)(y * PatchSize));
            }
        }
        else if (Vector128.IsHardwareAccelerated)
        {
            Vector128<int> leftMask = leftIndices.GetLower().GetLower();
            Vector128<int> rightMask = rightIndices.GetLower().GetUpper() - Vector128.Create(4);

            for (int y = 0; y < PatchSize; y++)
            {
                Vector128<float> sum0 = Vector128<float>.Zero;
                Vector128<float> sum1 = Vector128<float>.Zero;
                for (int tapRow = 0; tapRow < 3; tapRow++)
                {
                    uint source = (uint)(rows[(y * 3) + tapRow] * PatchSize);
                    Vector128<float> center0 = Vector128.LoadUnsafe(ref first, source);
                    Vector128<float> center1 = Vector128.LoadUnsafe(ref first, source + 4);
                    Vector128<float> left0 = Vector128.ShuffleNative(center0, leftMask);
                    Vector128<float> right0 = Vector128.LoadUnsafe(ref first, source + 1);
                    Vector128<float> left1 = Vector128.LoadUnsafe(ref first, source + 3);
                    Vector128<float> right1 = Vector128.ShuffleNative(center1, rightMask);
                    Vector128<float> coefficient0 = Vector128.Create(coefficients[tapRow * 3]);
                    Vector128<float> coefficient1 = Vector128.Create(coefficients[(tapRow * 3) + 1]);
                    Vector128<float> coefficient2 = Vector128.Create(coefficients[(tapRow * 3) + 2]);

                    // Loads across the middle use real columns 1..4 and 3..6. Only the actual outer borders use Bounce shuffles.
                    sum0 += left0 * coefficient0;
                    sum0 += center0 * coefficient1;
                    sum0 += right0 * coefficient2;
                    sum1 += left1 * coefficient0;
                    sum1 += center1 * coefficient1;
                    sum1 += right1 * coefficient2;
                }

                sum0.StoreUnsafe(ref output, (uint)(y * PatchSize));
                sum1.StoreUnsafe(ref output, (uint)((y * PatchSize) + 4));
            }
        }
        else
        {
            for (int y = 0; y < PatchSize; y++)
            {
                for (int x = 0; x < PatchSize; x++)
                {
                    float sum = 0;
                    for (int tapRow = 0; tapRow < 3; tapRow++)
                    {
                        for (int tapColumn = 0; tapColumn < 3; tapColumn++)
                        {
                            sum += patch[(rows[(y * 3) + tapRow] * PatchSize) + columns[(x * 3) + tapColumn]] * coefficients[(tapRow * 3) + tapColumn];
                        }
                    }

                    filtered[(y * PatchSize) + x] = sum;
                }
            }
        }
    }

    /// <summary>
    /// Measures noise in selected complete patches using cached source spans and one reused intensity/filter workspace.
    /// </summary>
    /// <param name="configuration">The configuration used for the shared boundary sampling map.</param>
    /// <param name="opsin">The source image dimensions and physical strides.</param>
    /// <param name="planeX">The complete borrowed X-plane storage.</param>
    /// <param name="planeY">The complete borrowed Y-plane storage.</param>
    /// <param name="textureStrength">The logical row-major patch scores.</param>
    /// <param name="threshold">The largest score selected for noise measurement.</param>
    /// <param name="workspace">The reused intensity and filtered patch storage.</param>
    /// <param name="measurements">Caller-owned storage with capacity for every complete patch.</param>
    /// <returns>The number of measurements written in patch order.</returns>
    private static int GetNoiseLevel(
        Configuration configuration,
        JxlImage3<float> opsin,
        ReadOnlySpan<float> planeX,
        ReadOnlySpan<float> planeY,
        ReadOnlySpan<float> textureStrength,
        float threshold,
        Span<float> workspace,
        Span<JxlNoiseLevel> measurements)
    {
        int measurementCount = 0;
        Span<float> patch = workspace[..(PatchSize * PatchSize)];
        Span<float> filtered = workspace.Slice(PatchSize * PatchSize, PatchSize * PatchSize);
        int strideX = opsin.Plane(0).PixelsPerRow;
        int strideY = opsin.Plane(1).PixelsPerRow;
        int patchIndex = 0;

        // The same fixed patch bounds apply everywhere. Build and borrow the existing Bounce map once, outside all patch loops.
        using KernelSamplingMap sampling = new(configuration.MemoryAllocator);
        sampling.BuildSamplingOffsetMap(3, 3, new Rectangle(0, 0, PatchSize, PatchSize), BorderWrappingMode.Bounce, BorderWrappingMode.Bounce);
        ReadOnlySpan<int> rows = sampling.GetRowOffsetSpan();
        ReadOnlySpan<int> columns = sampling.GetColumnOffsetSpan();
        InlineArray16<int> leftStorage = default;
        InlineArray16<int> rightStorage = default;
        Span<int> left = leftStorage;
        Span<int> right = rightStorage;

        for (int i = 0; i < left.Length; i++)
        {
            int column = i % PatchSize;
            int rowBase = (i / PatchSize) * PatchSize;
            left[i] = rowBase + columns[column * 3];
            right[i] = rowBase + columns[(column * 3) + 2];
        }

        Vector512<int> leftIndices = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(left));
        Vector512<int> rightIndices = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(right));

        for (int y = 0; y + PatchSize <= opsin.YSize; y += PatchSize)
        {
            for (int x = 0; x + PatchSize <= opsin.XSize; x += PatchSize)
            {
                if (textureStrength[patchIndex] <= threshold)
                {
                    PrepareIntensityPatch(planeX, planeY, strideX, strideY, x, y, patch);
                    FilterIntensityPatch(patch, filtered, rows, columns, in leftIndices, in rightIndices);
                    float meanIntensity = 0;
                    float noiseLevel = 0;

                    // SIMD changes independent output positions, not these native row-major reductions.
                    for (int i = 0; i < patch.Length; i++)
                    {
                        meanIntensity += patch[i];
                        noiseLevel += MathF.Abs(filtered[i]);
                    }

                    measurements[measurementCount++] = new JxlNoiseLevel
                    {
                        Intensity = meanIntensity / (PatchSize * PatchSize),
                        NoiseLevel = noiseLevel / (PatchSize * PatchSize)
                    };
                }

                patchIndex++;
            }
        }

        return measurementCount;
    }

    /// <summary>
    /// Calculates the native intensity mean without changing addition order or fusing operations.
    /// </summary>
    private readonly struct MeanIntensityOperator : TensorPrimitives_.IBinaryOperator
    {
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(float first, float second) => (first + second) * 0.5F;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> first, Vector128<float> second) => (first + second) * Vector128.Create(0.5F);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> first, Vector256<float> second) => (first + second) * Vector256.Create(0.5F);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> first, Vector512<float> second) => (first + second) * Vector512.Create(0.5F);
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
    /// <param name="configuration">The allocator configuration for patch scores, measurements, and sampling offsets.</param>
    /// <param name="opsin">The borrowed image planes used to measure patch noise.</param>
    /// <param name="noiseParams">The lookup values to replace or clear.</param>
    /// <param name="qualityCoefficient">The strength used to scale the fitted noise.</param>
    /// <returns>Whether the fitted lookup contains a nonzero noise level.</returns>
    public static bool GetNoiseParameter(Configuration configuration, JxlImage3<float> opsin, JxlNoiseParameters noiseParams, float qualityCoefficient)
    {
        const int binCount = 256;

        // One bounded stack workspace serves both passes: 64 intensity values followed by 32 SAD slots or 64 filtered values.
        InlineArray128<float> workspaceStorage = default;
        Span<float> workspace = workspaceStorage;
        ReadOnlySpan<float> planeX = opsin.PlaneRow(0, 0);
        ReadOnlySpan<float> planeY = opsin.PlaneRow(1, 0);
        JxlNoiseHistogram sadHistogram = new();
        using IMemoryOwner<float> sadScores = GetSadScoresForPatches(configuration, opsin, planeX, planeY, workspace, binCount, sadHistogram);
        float sadThreshold = GetSadThreshold(sadHistogram);

        if (sadThreshold is > 0.15f or <= 0.0f)
        {
            noiseParams.Clear();
            return false;
        }

        int patchCount = (opsin.YSize / PatchSize) * (opsin.XSize / PatchSize);
        ReadOnlySpan<float> scores = sadScores.GetSpan()[..patchCount];

        // Each complete patch contributes at most one measurement. Allocate once after the threshold accepts this image.
        using IMemoryOwner<JxlNoiseLevel> measurementOwner = configuration.MemoryAllocator.Allocate<JxlNoiseLevel>(patchCount);
        Span<JxlNoiseLevel> measurements = measurementOwner.GetSpan()[..patchCount];
        int measurementCount = GetNoiseLevel(configuration, opsin, planeX, planeY, scores, sadThreshold, workspace, measurements);

        // The histogram threshold can exclude every patch. There is then no measured curve to average or fit.
        if (measurementCount == 0)
        {
            noiseParams.Clear();
            return false;
        }

        OptimizeNoiseParameters(measurements[..measurementCount], noiseParams, qualityCoefficient * 1.4f);

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
