// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Evaluates the small fully connected networks and the intra partition convolution layers of the encoder with the
/// arithmetic of an <see cref="INeuralNetworkOperator"/>.
/// </summary>
/// <remarks>
/// The encoder compares network outputs with thresholds, so a different float addition order can change a decision.
/// This class fixes the order of every addition. The order matches the x64 output of other AV1 encoders.
/// Every hardware tier uses the same order, so the encoder output does not depend on the hardware.
/// <para>
/// A dense layer has one weight row per output node: the weight of input i for node r is weights[(r * inputs) + i].
/// The layer adds the bias after the input products. A hidden layer clips its outputs to zero from below.
/// </para>
/// </remarks>
internal static partial class Av1NeuralNetwork
{
    /// <summary>
    /// The number of fractional bits that the network outputs keep.
    /// </summary>
    private const int OutputPrecisionBits = 9;

    /// <summary>
    /// Names the addition order that a convolution layer follows.
    /// </summary>
    private enum ConvolutionKind
    {
        /// <summary>
        /// The five-by-five filter with a step of four over one input channel, whose windows go in groups of three.
        /// </summary>
        FiveByFive,

        /// <summary>
        /// The two-by-two filter with a step of two over 16 or 8 samples, which sums each window by columns.
        /// </summary>
        TwoByTwo,

        /// <summary>
        /// The plain order: the bias, then every input channel, filter row and filter column in turn.
        /// </summary>
        InOrder
    }

    /// <summary>
    /// Evaluates a network without hidden layers and reduces the output precision.
    /// </summary>
    /// <param name="inputs">The input features.</param>
    /// <param name="weights">The output weights, one row of input weights per output.</param>
    /// <param name="biases">The output biases, one per output.</param>
    /// <param name="outputs">Receives one value per output bias.</param>
    public static void Predict(ReadOnlySpan<float> inputs, ReadOnlySpan<float> weights, ReadOnlySpan<float> biases, Span<float> outputs)
    {
        Propagate<NeuralNetworkOperator>(inputs, weights, biases, outputs, false);
        ReduceOutputPrecision(outputs[..biases.Length]);
    }

    /// <summary>
    /// Evaluates a network with one hidden layer and reduces the output precision.
    /// </summary>
    /// <param name="inputs">The input features.</param>
    /// <param name="hiddenWeights">The hidden layer weights, one row of input weights per hidden node.</param>
    /// <param name="hiddenBiases">The hidden layer biases, one per hidden node.</param>
    /// <param name="outputWeights">The output weights, one row of hidden node weights per output.</param>
    /// <param name="outputBiases">The output biases, one per output.</param>
    /// <param name="outputs">Receives one value per output bias.</param>
    public static void Predict(
        ReadOnlySpan<float> inputs,
        ReadOnlySpan<float> hiddenWeights,
        ReadOnlySpan<float> hiddenBiases,
        ReadOnlySpan<float> outputWeights,
        ReadOnlySpan<float> outputBiases,
        Span<float> outputs)
    {
        Span<float> hidden = stackalloc float[hiddenBiases.Length];
        Propagate<NeuralNetworkOperator>(inputs, hiddenWeights, hiddenBiases, hidden, true);
        Propagate<NeuralNetworkOperator>(hidden, outputWeights, outputBiases, outputs, false);
        ReduceOutputPrecision(outputs[..outputBiases.Length]);
    }

    /// <summary>
    /// Evaluates a network with two hidden layers and reduces the output precision.
    /// </summary>
    /// <param name="inputs">The input features.</param>
    /// <param name="firstWeights">The first hidden layer weights, one row of input weights per node.</param>
    /// <param name="firstBiases">The first hidden layer biases, one per node.</param>
    /// <param name="secondWeights">The second hidden layer weights, one row of first-layer weights per node.</param>
    /// <param name="secondBiases">The second hidden layer biases, one per node.</param>
    /// <param name="outputWeights">The output weights, one row of second-layer weights per output.</param>
    /// <param name="outputBiases">The output biases, one per output.</param>
    /// <param name="outputs">Receives one value per output bias.</param>
    public static void Predict(
        ReadOnlySpan<float> inputs,
        ReadOnlySpan<float> firstWeights,
        ReadOnlySpan<float> firstBiases,
        ReadOnlySpan<float> secondWeights,
        ReadOnlySpan<float> secondBiases,
        ReadOnlySpan<float> outputWeights,
        ReadOnlySpan<float> outputBiases,
        Span<float> outputs)
    {
        Span<float> first = stackalloc float[firstBiases.Length];
        Span<float> second = stackalloc float[secondBiases.Length];
        Propagate<NeuralNetworkOperator>(inputs, firstWeights, firstBiases, first, true);
        Propagate<NeuralNetworkOperator>(first, secondWeights, secondBiases, second, true);
        Propagate<NeuralNetworkOperator>(second, outputWeights, outputBiases, outputs, false);
        ReduceOutputPrecision(outputs[..outputBiases.Length]);
    }

    /// <summary>
    /// Rounds network outputs to nine fractional bits. The scaled float output widens to double before the code adds one half.
    /// The sum truncates toward zero. Then the integer multiplies the float reciprocal of the scale.
    /// </summary>
    /// <param name="outputs">The outputs to round in place.</param>
    public static void ReduceOutputPrecision(Span<float> outputs)
    {
        const float Precision = 1 << OutputPrecisionBits;
        const float InversePrecision = 1F / Precision;
        for (int i = 0; i < outputs.Length; i++)
        {
            // The product rounds in float. The half then adds in double, so a product just below one half stays below it.
            // A float addition rounds that sum up to one.
            outputs[i] = (int)((double)(outputs[i] * Precision) + 0.5) * InversePrecision;
        }
    }

    /// <summary>
    /// Applies a valid convolution with rectification to channel-major planes, in the fixed addition order of each layer shape.
    /// </summary>
    /// <param name="input">The input planes, one square plane per input channel.</param>
    /// <param name="inputWidth">The width and height of each input plane.</param>
    /// <param name="inputChannels">The number of input channels.</param>
    /// <param name="filterWidth">The width and height of the filter.</param>
    /// <param name="step">The distance between two filter windows in both directions.</param>
    /// <param name="weights">
    /// The filter weights. The weight of tap t, input channel k and output channel c is
    /// weights[(t * inputChannels * outputChannels) + (k * outputChannels) + c], with taps in raster order.
    /// </param>
    /// <param name="biases">The biases, one per output channel.</param>
    /// <param name="output">Receives one square plane per output channel.</param>
    public static void Convolve(
        ReadOnlySpan<float> input,
        int inputWidth,
        int inputChannels,
        int filterWidth,
        int step,
        ReadOnlySpan<float> weights,
        ReadOnlySpan<float> biases,
        Span<float> output)
        => Convolve<NeuralNetworkOperator>(input, inputWidth, inputChannels, filterWidth, step, weights, biases, output);

    /// <summary>
    /// Evaluates one dense layer in the fixed addition order for its shape.
    /// </summary>
    /// <typeparam name="TOperator">The network arithmetic.</typeparam>
    /// <param name="inputs">The layer inputs.</param>
    /// <param name="weights">The layer weights, one row of input weights per output node.</param>
    /// <param name="biases">The layer biases, one per output node.</param>
    /// <param name="outputs">Receives one value per output node.</param>
    /// <param name="hidden">Whether the layer is a hidden layer, whose outputs are clipped to zero from below.</param>
    private static void Propagate<TOperator>(
        ReadOnlySpan<float> inputs,
        ReadOnlySpan<float> weights,
        ReadOnlySpan<float> biases,
        Span<float> outputs,
        bool hidden)
        where TOperator : struct, INeuralNetworkOperator
    {
        ref float inputBase = ref MemoryMarshal.GetReference(inputs);
        ref float weightBase = ref MemoryMarshal.GetReference(weights);
        ref float biasBase = ref MemoryMarshal.GetReference(biases);
        ref float outputBase = ref MemoryMarshal.GetReference(outputs);
        int inputCount = inputs.Length;
        int outputCount = biases.Length;

        // The layer first takes the inputs in groups of eight. When inputs remain, those groups end without the activation.
        // Then the remaining inputs add onto the partial outputs before the activation.
        int groupedCount = inputCount & ~7;
        int remainingCount = inputCount & 7;
        if (groupedCount > 0)
        {
            bool rectify = hidden && remainingCount == 0;

            // Four or eight outputs at a time sum each group of eight in pairs. Any other output count keeps eight
            // lane totals per output instead, so the two shapes need different orders.
            if ((outputCount & 3) == 0)
            {
                PropagateGroupsByNode<TOperator>(ref inputBase, ref weightBase, ref biasBase, ref outputBase, inputCount, groupedCount, outputCount, rectify);
            }
            else
            {
                PropagateGroupsByLane<TOperator>(ref inputBase, ref weightBase, ref biasBase, ref outputBase, inputCount, groupedCount, outputCount, rectify);
            }
        }

        if (remainingCount > 0)
        {
            // The remaining inputs start from the partial outputs, or from the biases when there was no group.
            ref float startBase = ref groupedCount > 0 ? ref outputBase : ref biasBase;
            PropagateRemainder<TOperator>(
                ref Unsafe.Add(ref inputBase, (nuint)groupedCount),
                ref Unsafe.Add(ref weightBase, (nuint)groupedCount),
                ref startBase,
                ref outputBase,
                inputCount,
                remainingCount,
                outputCount,
                hidden);
        }
    }

    /// <summary>
    /// Adds the groups of eight inputs of a layer whose output count is a multiple of four. Each group adds onto a
    /// zero total as ((p0 + p1) + (p2 + p3)) + ((p4 + p5) + (p6 + p7)), in group order, and the bias adds last.
    /// </summary>
    /// <typeparam name="TOperator">The network arithmetic.</typeparam>
    /// <param name="inputBase">The first input.</param>
    /// <param name="weightBase">The first weight of the first node.</param>
    /// <param name="biasBase">The first bias.</param>
    /// <param name="outputBase">The first output.</param>
    /// <param name="inputCount">The number of inputs, which is the weight stride of a node.</param>
    /// <param name="groupedCount">The number of inputs in whole groups of eight.</param>
    /// <param name="outputCount">The number of output nodes, a multiple of four.</param>
    /// <param name="rectify">Whether the outputs are clipped to zero from below.</param>
    private static void PropagateGroupsByNode<TOperator>(
        ref float inputBase,
        ref float weightBase,
        ref float biasBase,
        ref float outputBase,
        int inputCount,
        int groupedCount,
        int outputCount,
        bool rectify)
        where TOperator : struct, INeuralNetworkOperator
    {
        // Every width computes the same expression for each node, so the widths can share the nodes in any split.
        nuint stride = (nuint)inputCount;
        nuint grouped = (nuint)groupedCount;
        int node = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            for (; node <= outputCount - Vector512<float>.Count; node += Vector512<float>.Count)
            {
                ref float nodeWeights = ref Unsafe.Add(ref weightBase, (nuint)node * stride);
                Vector512<float> totals = Vector512<float>.Zero;
                for (nuint input = 0; input < grouped; input += 8)
                {
                    totals = TOperator.AccumulateEightInputs(ref Unsafe.Add(ref inputBase, input), ref Unsafe.Add(ref nodeWeights, input), stride, totals);
                }

                totals += Vector512.LoadUnsafe(ref biasBase, (nuint)node);
                (rectify ? TOperator.Rectify(totals) : totals).StoreUnsafe(ref outputBase, (nuint)node);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; node <= outputCount - Vector256<float>.Count; node += Vector256<float>.Count)
            {
                ref float nodeWeights = ref Unsafe.Add(ref weightBase, (nuint)node * stride);
                Vector256<float> totals = Vector256<float>.Zero;
                for (nuint input = 0; input < grouped; input += 8)
                {
                    totals = TOperator.AccumulateEightInputs(ref Unsafe.Add(ref inputBase, input), ref Unsafe.Add(ref nodeWeights, input), stride, totals);
                }

                totals += Vector256.LoadUnsafe(ref biasBase, (nuint)node);
                (rectify ? TOperator.Rectify(totals) : totals).StoreUnsafe(ref outputBase, (nuint)node);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; node <= outputCount - Vector128<float>.Count; node += Vector128<float>.Count)
            {
                ref float nodeWeights = ref Unsafe.Add(ref weightBase, (nuint)node * stride);
                Vector128<float> totals = Vector128<float>.Zero;
                for (nuint input = 0; input < grouped; input += 8)
                {
                    totals = TOperator.AccumulateEightInputs(ref Unsafe.Add(ref inputBase, input), ref Unsafe.Add(ref nodeWeights, input), stride, totals);
                }

                totals += Vector128.LoadUnsafe(ref biasBase, (nuint)node);
                (rectify ? TOperator.Rectify(totals) : totals).StoreUnsafe(ref outputBase, (nuint)node);
            }
        }

        for (; node < outputCount; node++)
        {
            ref float nodeWeights = ref Unsafe.Add(ref weightBase, (nuint)node * stride);
            float total = 0F;
            for (nuint input = 0; input < grouped; input += 8)
            {
                total = TOperator.AccumulateEightInputs(ref Unsafe.Add(ref inputBase, input), ref Unsafe.Add(ref nodeWeights, input), total);
            }

            total += Unsafe.Add(ref biasBase, (nuint)node);
            Unsafe.Add(ref outputBase, (nuint)node) = rectify ? TOperator.Rectify(total) : total;
        }
    }

    /// <summary>
    /// Adds the groups of eight inputs of a layer whose output count is not a multiple of four. Each output keeps
    /// eight lane totals, lane j adding the products of inputs j, j + 8 and so on onto zero in group order. The upper
    /// four lanes then add onto the lower four, the adjacent sums add, and the bias adds to the total.
    /// </summary>
    /// <typeparam name="TOperator">The network arithmetic.</typeparam>
    /// <param name="inputBase">The first input.</param>
    /// <param name="weightBase">The first weight of the first node.</param>
    /// <param name="biasBase">The first bias.</param>
    /// <param name="outputBase">The first output.</param>
    /// <param name="inputCount">The number of inputs, which is the weight stride of a node.</param>
    /// <param name="groupedCount">The number of inputs in whole groups of eight.</param>
    /// <param name="outputCount">The number of output nodes.</param>
    /// <param name="rectify">Whether the outputs are clipped to zero from below.</param>
    private static void PropagateGroupsByLane<TOperator>(
        ref float inputBase,
        ref float weightBase,
        ref float biasBase,
        ref float outputBase,
        int inputCount,
        int groupedCount,
        int outputCount,
        bool rectify)
        where TOperator : struct, INeuralNetworkOperator
    {
        // One output fills exactly eight lanes, so the widest register that keeps this layout is 256 bits wide. A
        // 128-bit register holds the lower and the upper four lanes as two vectors.
        nuint stride = (nuint)inputCount;
        nuint grouped = (nuint)groupedCount;
        InlineArray8<float> laneStorage = default;
        Span<float> lanes = laneStorage;
        ref float laneBase = ref MemoryMarshal.GetReference(lanes);
        for (int node = 0; node < outputCount; node++)
        {
            ref float nodeWeights = ref Unsafe.Add(ref weightBase, (nuint)node * stride);
            float sum;
            if (Vector256.IsHardwareAccelerated)
            {
                Vector256<float> totals = Vector256<float>.Zero;
                for (nuint input = 0; input < grouped; input += 8)
                {
                    totals = TOperator.MultiplyAdd(Vector256.LoadUnsafe(ref inputBase, input), Vector256.LoadUnsafe(ref nodeWeights, input), totals);
                }

                sum = TOperator.SumLanes(totals);
            }
            else if (Vector128.IsHardwareAccelerated)
            {
                Vector128<float> lower = Vector128<float>.Zero;
                Vector128<float> upper = Vector128<float>.Zero;
                for (nuint input = 0; input < grouped; input += 8)
                {
                    lower = TOperator.MultiplyAdd(Vector128.LoadUnsafe(ref inputBase, input), Vector128.LoadUnsafe(ref nodeWeights, input), lower);
                    upper = TOperator.MultiplyAdd(Vector128.LoadUnsafe(ref inputBase, input + 4), Vector128.LoadUnsafe(ref nodeWeights, input + 4), upper);
                }

                sum = TOperator.SumLanes(lower, upper);
            }
            else
            {
                lanes.Clear();
                for (nuint input = 0; input < grouped; input += 8)
                {
                    for (nuint lane = 0; lane < 8; lane++)
                    {
                        ref float laneTotal = ref Unsafe.Add(ref laneBase, lane);
                        laneTotal = TOperator.MultiplyAdd(Unsafe.Add(ref inputBase, input + lane), Unsafe.Add(ref nodeWeights, input + lane), laneTotal);
                    }
                }

                sum = TOperator.SumLanes(lanes);
            }

            float value = Unsafe.Add(ref biasBase, (nuint)node) + sum;
            Unsafe.Add(ref outputBase, (nuint)node) = rectify ? TOperator.Rectify(value) : value;
        }
    }

    /// <summary>
    /// Adds the inputs after the last whole group of eight to the outputs. Four inputs add as (p0 + p1) + (p2 + p3).
    /// Any other count adds its products one at a time.
    /// </summary>
    /// <typeparam name="TOperator">The network arithmetic.</typeparam>
    /// <param name="inputBase">The first remaining input.</param>
    /// <param name="weightBase">The weight of the first remaining input for the first node.</param>
    /// <param name="startBase">The first value the outputs start from: the partial outputs or the biases.</param>
    /// <param name="outputBase">The first output.</param>
    /// <param name="inputCount">The number of inputs, which is the weight stride of a node.</param>
    /// <param name="remainingCount">The number of remaining inputs, one to seven.</param>
    /// <param name="outputCount">The number of output nodes.</param>
    /// <param name="rectify">Whether the outputs are clipped to zero from below.</param>
    private static void PropagateRemainder<TOperator>(
        ref float inputBase,
        ref float weightBase,
        ref float startBase,
        ref float outputBase,
        int inputCount,
        int remainingCount,
        int outputCount,
        bool rectify)
        where TOperator : struct, INeuralNetworkOperator
    {
        nuint stride = (nuint)inputCount;
        int node = 0;
        if (remainingCount == 4)
        {
            if (Vector512.IsHardwareAccelerated)
            {
                for (; node <= outputCount - Vector512<float>.Count; node += Vector512<float>.Count)
                {
                    Vector512<float> totals = TOperator.AccumulateFourInputs(
                        ref inputBase, ref Unsafe.Add(ref weightBase, (nuint)node * stride), stride, Vector512.LoadUnsafe(ref startBase, (nuint)node));

                    (rectify ? TOperator.Rectify(totals) : totals).StoreUnsafe(ref outputBase, (nuint)node);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; node <= outputCount - Vector256<float>.Count; node += Vector256<float>.Count)
                {
                    Vector256<float> totals = TOperator.AccumulateFourInputs(
                        ref inputBase, ref Unsafe.Add(ref weightBase, (nuint)node * stride), stride, Vector256.LoadUnsafe(ref startBase, (nuint)node));

                    (rectify ? TOperator.Rectify(totals) : totals).StoreUnsafe(ref outputBase, (nuint)node);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; node <= outputCount - Vector128<float>.Count; node += Vector128<float>.Count)
                {
                    Vector128<float> totals = TOperator.AccumulateFourInputs(
                        ref inputBase, ref Unsafe.Add(ref weightBase, (nuint)node * stride), stride, Vector128.LoadUnsafe(ref startBase, (nuint)node));

                    (rectify ? TOperator.Rectify(totals) : totals).StoreUnsafe(ref outputBase, (nuint)node);
                }
            }

            for (; node < outputCount; node++)
            {
                ref float nodeWeights = ref Unsafe.Add(ref weightBase, (nuint)node * stride);
                float total = TOperator.AccumulateFourInputs(ref inputBase, ref nodeWeights, Unsafe.Add(ref startBase, (nuint)node));
                Unsafe.Add(ref outputBase, (nuint)node) = rectify ? TOperator.Rectify(total) : total;
            }

            return;
        }

        // These products add one at a time in input order. This path uses scalar arithmetic only.
        nuint remaining = (nuint)remainingCount;
        for (; node < outputCount; node++)
        {
            ref float nodeWeights = ref Unsafe.Add(ref weightBase, (nuint)node * stride);
            float total = Unsafe.Add(ref startBase, (nuint)node);
            for (nuint input = 0; input < remaining; input++)
            {
                total = TOperator.MultiplyAdd(Unsafe.Add(ref inputBase, input), Unsafe.Add(ref nodeWeights, input), total);
            }

            Unsafe.Add(ref outputBase, (nuint)node) = rectify ? TOperator.Rectify(total) : total;
        }
    }

    /// <summary>
    /// Traverses <see cref="Convolve(ReadOnlySpan{float}, int, int, int, int, ReadOnlySpan{float}, ReadOnlySpan{float}, Span{float})"/>
    /// at descending register widths, one output channel per lane.
    /// </summary>
    /// <typeparam name="TOperator">The network arithmetic.</typeparam>
    /// <param name="input">The input planes, one square plane per input channel.</param>
    /// <param name="inputWidth">The width and height of each input plane.</param>
    /// <param name="inputChannels">The number of input channels.</param>
    /// <param name="filterWidth">The width and height of the filter.</param>
    /// <param name="step">The distance between two filter windows in both directions.</param>
    /// <param name="weights">The filter weights, with consecutive output channels adjacent.</param>
    /// <param name="biases">The biases, one per output channel.</param>
    /// <param name="output">Receives one square plane per output channel.</param>
    private static void Convolve<TOperator>(
        ReadOnlySpan<float> input,
        int inputWidth,
        int inputChannels,
        int filterWidth,
        int step,
        ReadOnlySpan<float> weights,
        ReadOnlySpan<float> biases,
        Span<float> output)
        where TOperator : struct, INeuralNetworkOperator
    {
        ref float inputBase = ref MemoryMarshal.GetReference(input);
        ref float weightBase = ref MemoryMarshal.GetReference(weights);
        ref float biasBase = ref MemoryMarshal.GetReference(biases);
        ref float outputBase = ref MemoryMarshal.GetReference(output);
        int outputChannels = biases.Length;
        int outputWidth = ((inputWidth - filterWidth) / step) + 1;
        nuint outputArea = (nuint)(outputWidth * outputWidth);
        nuint inputStride = (nuint)inputWidth;
        nuint inputArea = inputStride * inputStride;
        nuint channelStep = (nuint)outputChannels;
        nuint weightStep = (nuint)(inputChannels * outputChannels);

        // The five-by-five layer and the two-by-two layers of 16 and 8 samples each have their own addition order.
        // The other layers use the plain order: the bias, then each input channel, filter row and filter column.
        // The five-by-five order takes the windows in groups of three while at least 13 samples remain in the row.
        // It takes the last windows one at a time. The position in the group sets the order of the window sums.
        // The five-by-five order writes each input channel over the result of the previous one. The only five-by-five
        // layer has one input channel, so this code reads only the first plane.
        ConvolutionKind kind = filterWidth == 5 && step == 4 ? ConvolutionKind.FiveByFive
            : filterWidth == 2 && step == 2 && (inputWidth == 16 || inputWidth == 8) ? ConvolutionKind.TwoByTwo
            : ConvolutionKind.InOrder;

        int groupedWindows = inputWidth >= 13 ? 3 * (((inputWidth - 13) / 12) + 1) : 0;

        // Each lane holds one output channel, and the planes are channel-major, so the lanes scatter to their planes.
        InlineArray16<float> laneStorage = default;
        Span<float> lanes = laneStorage;
        ref float laneBase = ref MemoryMarshal.GetReference(lanes);
        int channel = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            for (; channel <= outputChannels - Vector512<float>.Count; channel += Vector512<float>.Count)
            {
                Vector512<float> channelBiases = Vector512.LoadUnsafe(ref biasBase, (nuint)channel);
                ref float channelWeights = ref Unsafe.Add(ref weightBase, (nuint)channel);
                nuint outputIndex = 0;
                for (int y = 0; y < outputWidth; y++)
                {
                    for (int x = 0; x < outputWidth; x++, outputIndex++)
                    {
                        ref float window = ref Unsafe.Add(ref inputBase, ((nuint)(y * step) * inputStride) + (nuint)(x * step));
                        Vector512<float> sums;
                        if (kind == ConvolutionKind.FiveByFive)
                        {
                            int position = x < groupedWindows ? x % 3 : SingleBlockPosition;
                            sums = TOperator.ConvolveFiveByFive(ref window, inputStride, ref channelWeights, weightStep, channelBiases, position);
                        }
                        else
                        {
                            sums = channelBiases;
                            for (nuint inputChannel = 0; inputChannel < (nuint)inputChannels; inputChannel++)
                            {
                                ref float channelWindow = ref Unsafe.Add(ref window, inputChannel * inputArea);
                                ref float tapWeights = ref Unsafe.Add(ref channelWeights, inputChannel * channelStep);
                                if (kind == ConvolutionKind.TwoByTwo)
                                {
                                    sums = TOperator.AccumulateTwoByTwo(ref channelWindow, inputStride, ref tapWeights, weightStep, sums);
                                    continue;
                                }

                                for (nuint filterY = 0; filterY < (nuint)filterWidth; filterY++)
                                {
                                    for (nuint filterX = 0; filterX < (nuint)filterWidth; filterX++)
                                    {
                                        nuint tap = (filterY * (nuint)filterWidth) + filterX;
                                        sums = TOperator.MultiplyAdd(
                                            Vector512.Create(Unsafe.Add(ref channelWindow, (filterY * inputStride) + filterX)),
                                            Vector512.LoadUnsafe(ref tapWeights, tap * weightStep),
                                            sums);
                                    }
                                }
                            }
                        }

                        TOperator.Rectify(sums).StoreUnsafe(ref laneBase);
                        for (nuint lane = 0; lane < (nuint)Vector512<float>.Count; lane++)
                        {
                            Unsafe.Add(ref outputBase, (((nuint)channel + lane) * outputArea) + outputIndex) = Unsafe.Add(ref laneBase, lane);
                        }
                    }
                }
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; channel <= outputChannels - Vector256<float>.Count; channel += Vector256<float>.Count)
            {
                Vector256<float> channelBiases = Vector256.LoadUnsafe(ref biasBase, (nuint)channel);
                ref float channelWeights = ref Unsafe.Add(ref weightBase, (nuint)channel);
                nuint outputIndex = 0;
                for (int y = 0; y < outputWidth; y++)
                {
                    for (int x = 0; x < outputWidth; x++, outputIndex++)
                    {
                        ref float window = ref Unsafe.Add(ref inputBase, ((nuint)(y * step) * inputStride) + (nuint)(x * step));
                        Vector256<float> sums;
                        if (kind == ConvolutionKind.FiveByFive)
                        {
                            int position = x < groupedWindows ? x % 3 : SingleBlockPosition;
                            sums = TOperator.ConvolveFiveByFive(ref window, inputStride, ref channelWeights, weightStep, channelBiases, position);
                        }
                        else
                        {
                            sums = channelBiases;
                            for (nuint inputChannel = 0; inputChannel < (nuint)inputChannels; inputChannel++)
                            {
                                ref float channelWindow = ref Unsafe.Add(ref window, inputChannel * inputArea);
                                ref float tapWeights = ref Unsafe.Add(ref channelWeights, inputChannel * channelStep);
                                if (kind == ConvolutionKind.TwoByTwo)
                                {
                                    sums = TOperator.AccumulateTwoByTwo(ref channelWindow, inputStride, ref tapWeights, weightStep, sums);
                                    continue;
                                }

                                for (nuint filterY = 0; filterY < (nuint)filterWidth; filterY++)
                                {
                                    for (nuint filterX = 0; filterX < (nuint)filterWidth; filterX++)
                                    {
                                        nuint tap = (filterY * (nuint)filterWidth) + filterX;
                                        sums = TOperator.MultiplyAdd(
                                            Vector256.Create(Unsafe.Add(ref channelWindow, (filterY * inputStride) + filterX)),
                                            Vector256.LoadUnsafe(ref tapWeights, tap * weightStep),
                                            sums);
                                    }
                                }
                            }
                        }

                        TOperator.Rectify(sums).StoreUnsafe(ref laneBase);
                        for (nuint lane = 0; lane < (nuint)Vector256<float>.Count; lane++)
                        {
                            Unsafe.Add(ref outputBase, (((nuint)channel + lane) * outputArea) + outputIndex) = Unsafe.Add(ref laneBase, lane);
                        }
                    }
                }
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; channel <= outputChannels - Vector128<float>.Count; channel += Vector128<float>.Count)
            {
                Vector128<float> channelBiases = Vector128.LoadUnsafe(ref biasBase, (nuint)channel);
                ref float channelWeights = ref Unsafe.Add(ref weightBase, (nuint)channel);
                nuint outputIndex = 0;
                for (int y = 0; y < outputWidth; y++)
                {
                    for (int x = 0; x < outputWidth; x++, outputIndex++)
                    {
                        ref float window = ref Unsafe.Add(ref inputBase, ((nuint)(y * step) * inputStride) + (nuint)(x * step));
                        Vector128<float> sums;
                        if (kind == ConvolutionKind.FiveByFive)
                        {
                            int position = x < groupedWindows ? x % 3 : SingleBlockPosition;
                            sums = TOperator.ConvolveFiveByFive(ref window, inputStride, ref channelWeights, weightStep, channelBiases, position);
                        }
                        else
                        {
                            sums = channelBiases;
                            for (nuint inputChannel = 0; inputChannel < (nuint)inputChannels; inputChannel++)
                            {
                                ref float channelWindow = ref Unsafe.Add(ref window, inputChannel * inputArea);
                                ref float tapWeights = ref Unsafe.Add(ref channelWeights, inputChannel * channelStep);
                                if (kind == ConvolutionKind.TwoByTwo)
                                {
                                    sums = TOperator.AccumulateTwoByTwo(ref channelWindow, inputStride, ref tapWeights, weightStep, sums);
                                    continue;
                                }

                                for (nuint filterY = 0; filterY < (nuint)filterWidth; filterY++)
                                {
                                    for (nuint filterX = 0; filterX < (nuint)filterWidth; filterX++)
                                    {
                                        nuint tap = (filterY * (nuint)filterWidth) + filterX;
                                        sums = TOperator.MultiplyAdd(
                                            Vector128.Create(Unsafe.Add(ref channelWindow, (filterY * inputStride) + filterX)),
                                            Vector128.LoadUnsafe(ref tapWeights, tap * weightStep),
                                            sums);
                                    }
                                }
                            }
                        }

                        TOperator.Rectify(sums).StoreUnsafe(ref laneBase);
                        for (nuint lane = 0; lane < (nuint)Vector128<float>.Count; lane++)
                        {
                            Unsafe.Add(ref outputBase, (((nuint)channel + lane) * outputArea) + outputIndex) = Unsafe.Add(ref laneBase, lane);
                        }
                    }
                }
            }
        }

        for (; channel < outputChannels; channel++)
        {
            float channelBias = Unsafe.Add(ref biasBase, (nuint)channel);
            ref float channelWeights = ref Unsafe.Add(ref weightBase, (nuint)channel);
            nuint outputIndex = 0;
            for (int y = 0; y < outputWidth; y++)
            {
                for (int x = 0; x < outputWidth; x++, outputIndex++)
                {
                    ref float window = ref Unsafe.Add(ref inputBase, ((nuint)(y * step) * inputStride) + (nuint)(x * step));
                    float sum;
                    if (kind == ConvolutionKind.FiveByFive)
                    {
                        int position = x < groupedWindows ? x % 3 : SingleBlockPosition;
                        sum = TOperator.ConvolveFiveByFive(ref window, inputStride, ref channelWeights, weightStep, channelBias, position);
                    }
                    else
                    {
                        sum = channelBias;
                        for (nuint inputChannel = 0; inputChannel < (nuint)inputChannels; inputChannel++)
                        {
                            ref float channelWindow = ref Unsafe.Add(ref window, inputChannel * inputArea);
                            ref float tapWeights = ref Unsafe.Add(ref channelWeights, inputChannel * channelStep);
                            if (kind == ConvolutionKind.TwoByTwo)
                            {
                                sum = TOperator.AccumulateTwoByTwo(ref channelWindow, inputStride, ref tapWeights, weightStep, sum);
                                continue;
                            }

                            for (nuint filterY = 0; filterY < (nuint)filterWidth; filterY++)
                            {
                                for (nuint filterX = 0; filterX < (nuint)filterWidth; filterX++)
                                {
                                    nuint tap = (filterY * (nuint)filterWidth) + filterX;
                                    sum = TOperator.MultiplyAdd(
                                        Unsafe.Add(ref channelWindow, (filterY * inputStride) + filterX),
                                        Unsafe.Add(ref tapWeights, tap * weightStep),
                                        sum);
                                }
                            }
                        }
                    }

                    Unsafe.Add(ref outputBase, ((nuint)channel * outputArea) + outputIndex) = TOperator.Rectify(sum);
                }
            }
        }
    }
}
