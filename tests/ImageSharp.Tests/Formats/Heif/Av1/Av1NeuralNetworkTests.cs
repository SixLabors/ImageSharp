// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies that the network and convolution arithmetic gives the floats of the x64 AVX2 reference kernels on every
/// intrinsic tier. The expected values come from an emulation of those kernels in this class: each 128-bit or 256-bit
/// register is an array of lanes, and each instruction is applied lane by lane with its documented semantics.
/// </summary>
[Trait("Format", "Avif")]
public class Av1NeuralNetworkTests
{
    /// <summary>
    /// The hardware configurations that run every register width and the scalar overloads.
    /// </summary>
    private const HwIntrinsics Configurations =
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableSSE42 | HwIntrinsics.DisableHWIntrinsic;

    /// <summary>
    /// Verifies the dense networks: each layer shape selects its own addition order, and the output precision
    /// reduction adds the half in double precision.
    /// </summary>
    [Fact]
    public void PredictMatchesReferenceKernel()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidatePredict, Configurations);

    /// <summary>
    /// Verifies the five-by-five layer, including grouped and single windows, and the two-by-two layers.
    /// </summary>
    [Fact]
    public void ConvolveMatchesReferenceKernel()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateConvolve, Configurations);

    private static void ValidatePredict()
    {
        Random random = new(0x4E4E);

        // 32 inputs to 8 outputs: four groups of eight inputs for each block of eight outputs. The products reach
        // 90000, so one unit in the last place of a sum is far above the 1/512 output step, and the plain sequential
        // order gives different rounded outputs.
        float[] inputs32 = Scale(CreateValues(random, 32), 300F);
        float[] weights32 = Scale(CreateValues(random, 32 * 8), 300F);
        float[] biases8 = CreateValues(random, 8);
        float[] actual = new float[8];
        Av1NeuralNetwork.Predict(inputs32, weights32, biases8, actual);
        float[] expected = ReferencePredict(inputs32, [weights32], [biases8]);
        Assert.Equal(expected, actual);
        Assert.NotEqual(SequentialPredict(inputs32, [weights32], [biases8]), expected);

        // 13 inputs to 12 hidden nodes to 3 outputs: one group of eight for blocks of four nodes, then five inputs
        // one at a time; the output layer then has one group of eight in eight lanes and four inputs in pairs. Large
        // inputs make the order visible after the rounding, as above.
        float[] inputs13 = Scale(CreateValues(random, 13), 100000F);
        float[] hiddenWeights12 = CreateValues(random, 13 * 12);
        float[] hiddenBiases12 = CreateValues(random, 12);
        float[] outputWeights3 = CreateValues(random, 12 * 3);
        float[] outputBiases3 = CreateValues(random, 3);
        actual = new float[3];
        Av1NeuralNetwork.Predict(inputs13, hiddenWeights12, hiddenBiases12, outputWeights3, outputBiases3, actual);
        expected = ReferencePredict(inputs13, [hiddenWeights12, outputWeights3], [hiddenBiases12, outputBiases3]);
        Assert.Equal(expected, actual);
        Assert.NotEqual(SequentialPredict(inputs13, [hiddenWeights12, outputWeights3], [hiddenBiases12, outputBiases3]), expected);

        // 20 inputs to 16 and 24 hidden nodes to 10 outputs: groups of eight then four inputs in pairs for blocks of
        // eight nodes, a second hidden layer whose node count splits across register widths, and eight lanes per output.
        float[] inputs20 = Scale(CreateValues(random, 20), 100000F);
        float[] firstWeights = CreateValues(random, 20 * 16);
        float[] firstBiases = CreateValues(random, 16);
        float[] secondWeights = CreateValues(random, 16 * 24);
        float[] secondBiases = CreateValues(random, 24);
        float[] outputWeights10 = CreateValues(random, 24 * 10);
        float[] outputBiases10 = CreateValues(random, 10);
        actual = new float[10];
        Av1NeuralNetwork.Predict(inputs20, firstWeights, firstBiases, secondWeights, secondBiases, outputWeights10, outputBiases10, actual);
        expected = ReferencePredict(inputs20, [firstWeights, secondWeights, outputWeights10], [firstBiases, secondBiases, outputBiases10]);
        Assert.Equal(expected, actual);
        Assert.NotEqual(SequentialPredict(inputs20, [firstWeights, secondWeights, outputWeights10], [firstBiases, secondBiases, outputBiases10]), expected);

        // Four inputs to 16 nodes, which only add four inputs in pairs; one node per register width tier.
        float[] inputs4 = CreateValues(random, 4);
        float[] weights4 = CreateValues(random, 4 * 16);
        float[] biases16 = CreateValues(random, 16);
        actual = new float[16];
        Av1NeuralNetwork.Predict(inputs4, weights4, biases16, actual);
        Assert.Equal(ReferencePredict(inputs4, [weights4], [biases16]), actual);

        // The rounding edge: 512 times the first output is the largest float below one half. Adding the half in
        // float precision would round the sum up to one; in double precision it stays below one and truncates to zero.
        float edge = MathF.BitDecrement(0.5F) / 512F;
        Assert.Equal(1F, (edge * 512F) + 0.5F);
        float[] unitInput = [1F, 0F, 0F, 0F, 0F, 0F, 0F, 0F];
        float[] edgeWeights = new float[8 * 4];
        edgeWeights[0] = edge;
        edgeWeights[8] = -edge;
        edgeWeights[16] = 1.5F / 512F;
        edgeWeights[24] = -2.5F / 512F;
        actual = new float[4];
        Av1NeuralNetwork.Predict(unitInput, edgeWeights, new float[4], actual);
        Assert.Equal([0F, 0F, 2F / 512F, -2F / 512F], actual);
        Assert.Equal(ReferencePredict(unitInput, [edgeWeights], [new float[4]]), actual);
    }

    private static void ValidateConvolve()
    {
        Random random = new(0xC0DE);

        // Layer 0: one 65x65 input plane to twenty 16x16 planes. Each row has five groups of three windows and one
        // single window. The positive biases keep most outputs above the activation threshold.
        float[] image = CreateValues(random, 65 * 65);
        float[] weights0 = CreateValues(random, 25 * 20);
        float[] biases0 = CreateValues(random, 20);
        for (int i = 0; i < biases0.Length; i++)
        {
            biases0[i] += 3F;
        }

        float[] actual0 = new float[20 * 16 * 16];
        Av1NeuralNetwork.Convolve(image, 65, 1, 5, 4, weights0, biases0, actual0);
        float[] expected0 = ReferenceConvolveFiveByFive(image, weights0, biases0);

        // Window 4 is the second window of its group, window 6 the first, window 8 the third, and window 15 the single
        // window at the end of the row. The full planes must match too.
        Assert.Equal(expected0[(7 * 16) + 4], actual0[(7 * 16) + 4]);
        Assert.Equal(expected0[(3 * 256) + (2 * 16) + 6], actual0[(3 * 256) + (2 * 16) + 6]);
        Assert.Equal(expected0[(19 * 256) + (15 * 16) + 8], actual0[(19 * 256) + (15 * 16) + 8]);
        Assert.Equal(expected0[(11 * 256) + (9 * 16) + 15], actual0[(11 * 256) + (9 * 16) + 15]);
        Assert.Equal(expected0, actual0);
        Assert.NotEqual(SequentialConvolve(image, 65, 1, 5, 4, weights0, biases0), expected0);

        // Layer 1: twenty 16x16 planes to twenty 8x8 planes.
        float[] actual1 = new float[20 * 8 * 8];
        float[] weights1 = CreateValues(random, 4 * 20 * 20);
        float[] biases1 = CreateValues(random, 20);
        Av1NeuralNetwork.Convolve(actual0, 16, 20, 2, 2, weights1, biases1, actual1);
        float[] expected1 = ReferenceConvolveTwoByTwo(actual0, 16, 20, weights1, biases1);
        Assert.Equal(expected1[(5 * 64) + (3 * 8) + 6], actual1[(5 * 64) + (3 * 8) + 6]);
        Assert.Equal(expected1, actual1);
        Assert.NotEqual(SequentialConvolve(actual0, 16, 20, 2, 2, weights1, biases1), expected1);

        // Layer 2: twenty 8x8 planes to twenty 4x4 planes.
        float[] actual2 = new float[20 * 4 * 4];
        float[] weights2 = CreateValues(random, 4 * 20 * 20);
        float[] biases2 = CreateValues(random, 20);
        Av1NeuralNetwork.Convolve(actual1, 8, 20, 2, 2, weights2, biases2, actual2);
        Assert.Equal(ReferenceConvolveTwoByTwo(actual1, 8, 20, weights2, biases2), actual2);

        // Layer 3: twenty 4x4 planes to four 2x2 planes, which the reference adds in the plain order.
        float[] actual3 = new float[4 * 2 * 2];
        float[] weights3 = CreateValues(random, 4 * 20 * 4);
        float[] biases3 = CreateValues(random, 4);
        Av1NeuralNetwork.Convolve(actual2, 4, 20, 2, 2, weights3, biases3, actual3);
        Assert.Equal(SequentialConvolve(actual2, 4, 20, 2, 2, weights3, biases3), actual3);
    }

    /// <summary>
    /// Creates values in [-1, 1).
    /// </summary>
    private static float[] CreateValues(Random random, int count)
    {
        float[] values = new float[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = (float)((random.NextDouble() * 2) - 1);
        }

        return values;
    }

    /// <summary>
    /// Multiplies every value by <paramref name="factor"/> in place.
    /// </summary>
    private static float[] Scale(float[] values, float factor)
    {
        for (int i = 0; i < values.Length; i++)
        {
            values[i] *= factor;
        }

        return values;
    }

    /// <summary>
    /// Emulates the AVX2 network kernel: the groups of eight inputs, the SSE3 helpers for four remaining inputs, the
    /// scalar path for other remainders, the hidden-layer activation and the output precision reduction.
    /// </summary>
    private static float[] ReferencePredict(float[] input, float[][] weights, float[][] biases)
    {
        float[] inputNodes = input;
        int inputCount = input.Length;
        for (int layer = 0; layer < weights.Length; layer++)
        {
            bool isOutputLayer = layer == weights.Length - 1;
            float[] w = weights[layer];
            float[] b = biases[layer];
            int outputCount = b.Length;
            float[] outputNodes = new float[outputCount];
            if (inputCount % 8 == 0)
            {
                PropagateMultipleOfEight(inputNodes, w, b, inputCount, inputCount, isOutputLayer, outputCount, outputNodes);
            }
            else
            {
                int groupCount = inputCount / 8;
                int groupedCount = groupCount * 8;
                if (groupCount > 0)
                {
                    PropagateMultipleOfEight(inputNodes, w, b, groupedCount, inputCount, isOutputLayer, outputCount, outputNodes);
                }

                float[] start = groupCount > 0 ? outputNodes : b;
                int remaining = inputCount % 8;
                if (remaining % 4 == 0 && outputCount % 8 == 0)
                {
                    for (int output = 0; output < outputCount; output += 8)
                    {
                        float[] high = Load(start, output + 4, 4);
                        float[] low = Load(start, output, 4);
                        float[] values = Load(inputNodes, groupedCount, 4);
                        float[][] pairs = new float[4][];
                        for (int i = 0; i < 4; i++)
                        {
                            int index = (output * inputCount) + groupedCount + (2 * i * inputCount);
                            pairs[i] = HorizontalAdd(Multiply(values, Load(w, index, 4)), Multiply(values, Load(w, index + inputCount, 4)));
                        }

                        low = Add(low, HorizontalAdd(pairs[0], pairs[1]));
                        high = Add(high, HorizontalAdd(pairs[2], pairs[3]));
                        if (!isOutputLayer)
                        {
                            high = Maximum(high, new float[4]);
                            low = Maximum(low, new float[4]);
                        }

                        high.CopyTo(outputNodes, output + 4);
                        low.CopyTo(outputNodes, output);
                    }
                }
                else if (remaining % 4 == 0 && outputCount % 4 == 0)
                {
                    for (int output = 0; output < outputCount; output += 4)
                    {
                        float[] totals = Load(start, output, 4);
                        float[] values = Load(inputNodes, groupedCount, 4);
                        float[][] pairs = new float[2][];
                        for (int i = 0; i < 2; i++)
                        {
                            int index = (output * inputCount) + groupedCount + (2 * i * inputCount);
                            pairs[i] = HorizontalAdd(Multiply(Load(w, index, 4), values), Multiply(Load(w, index + inputCount, 4), values));
                        }

                        totals = Add(totals, HorizontalAdd(pairs[0], pairs[1]));
                        if (!isOutputLayer)
                        {
                            totals = Maximum(totals, new float[4]);
                        }

                        totals.CopyTo(outputNodes, output);
                    }
                }
                else if (remaining % 4 == 0)
                {
                    for (int output = 0; output < outputCount; output++)
                    {
                        float[] total = Broadcast(start[output], 4);
                        float[] products = Multiply(Load(inputNodes, groupedCount, 4), Load(w, (output * inputCount) + groupedCount, 4));
                        float[] pairs = HorizontalAdd(products, products);
                        total = Add(total, HorizontalAdd(pairs, pairs));
                        if (!isOutputLayer)
                        {
                            total = Maximum(total, new float[4]);
                        }

                        outputNodes[output] = total[0];
                    }
                }
                else
                {
                    for (int output = 0; output < outputCount; output++)
                    {
                        float[] total = Broadcast(start[output], 4);
                        for (int i = groupedCount; i < inputCount; i++)
                        {
                            total = Add(total, Multiply(Broadcast(inputNodes[i], 4), Broadcast(w[(inputCount * output) + i], 4)));
                        }

                        if (!isOutputLayer)
                        {
                            total = Maximum(total, new float[4]);
                        }

                        outputNodes[output] = total[0];
                    }
                }
            }

            inputNodes = outputNodes;
            inputCount = outputCount;
        }

        // The product is a float; 0.5 is a double constant; the integer converts to float for the multiplication.
        for (int i = 0; i < inputNodes.Length; i++)
        {
            inputNodes[i] = (int)((double)(inputNodes[i] * 512) + 0.5) * (float)(1.0 / 512);
        }

        return inputNodes;
    }

    /// <summary>
    /// Emulates the AVX2 propagation of the inputs in whole groups of eight, which picks its kernel by output count.
    /// </summary>
    private static void PropagateMultipleOfEight(
        float[] inputs,
        float[] weights,
        float[] biases,
        int processedCount,
        int inputCount,
        bool isOutputLayer,
        int outputCount,
        float[] outputs)
    {
        bool clip = !isOutputLayer && processedCount == inputCount;
        if (outputCount % 8 == 0)
        {
            for (int output = 0; output < outputCount; output += 8)
            {
                float[] total = new float[8];
                for (int input = 0; input < processedCount; input += 8)
                {
                    float[] values = Load(inputs, input, 8);
                    float[][] pairs = new float[4][];
                    for (int i = 0; i < 4; i++)
                    {
                        int index = input + (output * inputCount) + (2 * i * inputCount);
                        pairs[i] = HorizontalAdd(Multiply(values, Load(weights, index, 8)), Multiply(values, Load(weights, index + inputCount, 8)));
                    }

                    float[] quads0 = HorizontalAdd(pairs[0], pairs[1]);
                    float[] quads1 = HorizontalAdd(pairs[2], pairs[3]);
                    total = Add(total, Add(Permute2x128(quads0, quads1, 0x20), Permute2x128(quads0, quads1, 0x31)));
                }

                total = Add(total, Load(biases, output, 8));
                if (clip)
                {
                    total = Maximum(total, new float[8]);
                }

                total.CopyTo(outputs, output);
            }
        }
        else if (outputCount % 4 == 0)
        {
            for (int output = 0; output < outputCount; output += 4)
            {
                float[] total = new float[4];
                for (int input = 0; input < processedCount; input += 8)
                {
                    float[] values = Load(inputs, input, 8);
                    float[][] pairs = new float[2][];
                    for (int i = 0; i < 2; i++)
                    {
                        int index = input + (output * inputCount) + (2 * i * inputCount);
                        pairs[i] = HorizontalAdd(Multiply(values, Load(weights, index, 8)), Multiply(values, Load(weights, index + inputCount, 8)));
                    }

                    float[] quads = HorizontalAdd(pairs[0], pairs[1]);
                    total = Add(total, Add(Load(quads, 0, 4), Load(quads, 4, 4)));
                }

                total = Add(total, Load(biases, output, 4));
                if (clip)
                {
                    total = Maximum(total, new float[4]);
                }

                total.CopyTo(outputs, output);
            }
        }
        else
        {
            for (int output = 0; output < outputCount; output++)
            {
                float[] total = new float[8];
                float bias = biases[output];
                for (int input = 0; input < processedCount; input += 8)
                {
                    total = Add(total, Multiply(Load(inputs, input, 8), Load(weights, input + (output * inputCount), 8)));
                }

                float[] halves = Add(Load(total, 0, 4), Load(total, 4, 4));
                float[] pairs = HorizontalAdd(halves, halves);
                float[] sums = Add(Shuffle(pairs, pairs, 0x99), pairs);
                bias += sums[0];
                if (clip)
                {
                    bias = bias > 0 ? bias : 0;
                }

                outputs[output] = bias;
            }
        }
    }

    /// <summary>
    /// Emulates the AVX2 five-by-five convolution of one input plane with a step of four, followed by the activation.
    /// </summary>
    private static float[] ReferenceConvolveFiveByFive(float[] input, float[] weights, float[] biases)
    {
        const int width = 65;
        const int outputWidth = 16;
        int channels = biases.Length;
        int[] sourceMask0 = [0, 1, 2, 3, 4, 4, 5, 6];
        int[] sourceMask1 = [0, 1, 1, 2, 3, 4, 5, 0];
        int[] weightMask0 = [0, 1, 2, 3, 4, 0, 1, 2];
        int[] weightMask1 = [3, 4, 0, 1, 2, 3, 4, 0];
        float[] output = new float[channels * outputWidth * outputWidth];
        for (int channel = 0; channel < channels; channel++)
        {
            float bias = biases[channel];
            float[][] weight = new float[5][];
            float[][] shuffled = new float[10][];
            int offset = channel;
            for (int row = 0; row < 5; row++)
            {
                weight[row] = new float[8];
                for (int column = 0; column < 5; column++)
                {
                    weight[row][column] = weights[offset];
                    offset += channels;
                }

                shuffled[row] = Permute(weight[row], weightMask0);
            }

            for (int row = 0; row < 5; row++)
            {
                shuffled[5 + row] = Permute(shuffled[row], weightMask1);
            }

            float[] plane = new float[outputWidth * outputWidth];
            for (int h = 0, u = 0; h < width - 5 + 1; h += 4, u++)
            {
                int v = 0;
                int w = 0;
                int remaining = width;
                while (remaining >= 13)
                {
                    float[] accumulator0 = new float[8];
                    float[] accumulator1 = new float[8];
                    int position = (h * width) + w;
                    for (int row = 0; row < 5; row++)
                    {
                        float[] source0 = Multiply(Permute(Load(input, position, 8), sourceMask0), shuffled[row]);
                        float[] source1 = Multiply(Permute(Load(input, position + 7, 8), sourceMask1), shuffled[5 + row]);
                        accumulator0 = Add(source0, accumulator0);
                        accumulator1 = Add(source1, accumulator1);
                        position += width;
                    }

                    float[] sums = HorizontalAdd(accumulator0, accumulator1);
                    float[] upper0 = Load(accumulator0, 4, 4);
                    float[] upper1 = Load(accumulator1, 4, 4);
                    float[] sumsLow = Load(sums, 0, 4);
                    float[] sumsHigh = Load(sums, 4, 4);
                    float[] partial2 = Add(sumsLow, upper0);
                    float[] partial3 = Add(upper0, sumsHigh);
                    float[] partial4 = Add(upper1, sumsHigh);
                    plane[(u * outputWidth) + v] = bias + partial2[0] + Shuffle(sumsLow, sumsLow, 1)[0];
                    plane[(u * outputWidth) + v + 1] = bias + Shuffle(partial3, partial3, 1)[0] + Shuffle(sumsLow, sumsLow, 2)[0];
                    plane[(u * outputWidth) + v + 2] = bias + Shuffle(partial4, partial4, 2)[0] + Shuffle(sumsLow, sumsLow, 3)[0];
                    v += 3;
                    w += 12;
                    remaining -= 12;
                }

                while (remaining >= 5)
                {
                    float lastColumn = 0;
                    float[] accumulator = new float[4];
                    float[][] source = new float[5][];
                    int position = (h * width) + w;
                    for (int row = 0; row < 5; row++)
                    {
                        source[row] = Load(input, position, 4);
                        lastColumn += input[position + 4] * weight[row][4];
                        position += width;
                    }

                    for (int row = 0; row < 5; row++)
                    {
                        source[row] = Multiply(source[row], Load(shuffled[row], 0, 4));
                    }

                    accumulator = Add(source[0], accumulator);
                    source[1] = Add(source[1], source[2]);
                    source[3] = Add(source[3], source[4]);
                    source[1] = Add(source[1], source[3]);
                    accumulator = Add(accumulator, source[1]);
                    accumulator = HorizontalAdd(accumulator, accumulator);
                    plane[(u * outputWidth) + v] = bias + lastColumn + accumulator[0] + Shuffle(accumulator, accumulator, 1)[0];
                    v++;
                    w += 4;
                    remaining -= 4;
                }
            }

            for (int i = 0; i < plane.Length; i++)
            {
                output[(channel * plane.Length) + i] = plane[i] > 0 ? plane[i] : 0;
            }
        }

        return output;
    }

    /// <summary>
    /// Emulates the AVX2 two-by-two convolution with a step of two of the 16-sample and the 8-sample layer, followed
    /// by the activation.
    /// </summary>
    private static float[] ReferenceConvolveTwoByTwo(float[] input, int width, int inputChannels, float[] weights, float[] biases)
    {
        int channels = biases.Length;
        int cstep = inputChannels * channels;
        int outputWidth = width / 2;
        int inputArea = width * width;
        int outputArea = outputWidth * outputWidth;
        int[] outputMask = [0, 1, 4, 5, 2, 3, 6, 7];
        float[] output = new float[channels * outputArea];
        for (int channel = 0; channel < channels; channel++)
        {
            int registerCount = outputArea / 8;
            float[][] totals = new float[registerCount][];
            for (int j = 0; j < registerCount; j++)
            {
                totals[j] = Broadcast(biases[channel], 8);
            }

            for (int k = 0; k < inputChannels; k++)
            {
                float[] weight = new float[8];
                int offset = (k * channels) + channel;
                for (int i = 0; i < 4; i++)
                {
                    weight[i] = weights[offset];
                    offset += cstep;
                }

                float[] top = Permute(weight, [0, 1, 0, 1, 0, 1, 0, 1]);
                float[] bottom = Permute(weight, [2, 3, 2, 3, 2, 3, 2, 3]);
                int planeOffset = k * inputArea;
                for (int u = 0; u < registerCount; u++)
                {
                    float[] sums;
                    if (width == 16)
                    {
                        // Eight horizontal windows of one output row.
                        int position = planeOffset + (2 * u * width);
                        float[] left = Add(Multiply(Load(input, position, 8), top), Multiply(Load(input, position + width, 8), bottom));
                        float[] right = Add(Multiply(Load(input, position + 8, 8), top), Multiply(Load(input, position + width + 8, 8), bottom));
                        sums = HorizontalAdd(left, right);
                    }
                    else
                    {
                        // Four horizontal windows of two output rows.
                        int position = planeOffset + (4 * u * width);
                        float[] first = Add(Multiply(Load(input, position, 8), top), Multiply(Load(input, position + width, 8), bottom));
                        float[] second = Add(Multiply(Load(input, position + (2 * width), 8), top), Multiply(Load(input, position + (3 * width), 8), bottom));
                        sums = HorizontalAdd(first, second);
                    }

                    totals[u] = Add(totals[u], Permute(sums, outputMask));
                }
            }

            for (int j = 0; j < registerCount; j++)
            {
                for (int lane = 0; lane < 8; lane++)
                {
                    float value = totals[j][lane];
                    output[(channel * outputArea) + (j * 8) + lane] = value > 0 ? value : 0;
                }
            }
        }

        return output;
    }

    /// <summary>
    /// Evaluates a dense network by adding each product in input order onto the bias, without the rounding contract
    /// of the reference kernel, to show that the kernel order matters for the test data.
    /// </summary>
    private static float[] SequentialPredict(float[] input, float[][] weights, float[][] biases)
    {
        float[] values = input;
        for (int layer = 0; layer < weights.Length; layer++)
        {
            float[] next = new float[biases[layer].Length];
            for (int node = 0; node < next.Length; node++)
            {
                float sum = biases[layer][node];
                for (int i = 0; i < values.Length; i++)
                {
                    sum += weights[layer][(node * values.Length) + i] * values[i];
                }

                next[node] = layer < weights.Length - 1 && sum <= 0 ? 0 : sum;
            }

            values = next;
        }

        for (int i = 0; i < values.Length; i++)
        {
            values[i] = (int)((double)(values[i] * 512) + 0.5) * (float)(1.0 / 512);
        }

        return values;
    }

    /// <summary>
    /// Applies a valid convolution with the activation in the plain order: the bias, then each input channel, filter
    /// row and filter column.
    /// </summary>
    private static float[] SequentialConvolve(float[] input, int width, int inputChannels, int filterWidth, int step, float[] weights, float[] biases)
    {
        int channels = biases.Length;
        int cstep = inputChannels * channels;
        int outputWidth = ((width - filterWidth) / step) + 1;
        float[] output = new float[channels * outputWidth * outputWidth];
        for (int channel = 0; channel < channels; channel++)
        {
            for (int y = 0; y < outputWidth; y++)
            {
                for (int x = 0; x < outputWidth; x++)
                {
                    float sum = biases[channel];
                    for (int k = 0; k < inputChannels; k++)
                    {
                        int offset = (k * channels) + channel;
                        for (int row = 0; row < filterWidth; row++)
                        {
                            for (int column = 0; column < filterWidth; column++)
                            {
                                sum += weights[offset] * input[(k * width * width) + (((y * step) + row) * width) + (x * step) + column];
                                offset += cstep;
                            }
                        }
                    }

                    output[(channel * outputWidth * outputWidth) + (y * outputWidth) + x] = sum > 0 ? sum : 0;
                }
            }
        }

        return output;
    }

    /// <summary>
    /// Copies <paramref name="count"/> lanes starting at <paramref name="offset"/>, as an unaligned load does.
    /// </summary>
    private static float[] Load(float[] source, int offset, int count) => source.AsSpan(offset, count).ToArray();

    /// <summary>
    /// Fills <paramref name="count"/> lanes with one value, as a broadcast load does.
    /// </summary>
    private static float[] Broadcast(float value, int count)
    {
        float[] lanes = new float[count];
        Array.Fill(lanes, value);
        return lanes;
    }

    /// <summary>
    /// Adds two registers lane by lane.
    /// </summary>
    private static float[] Add(float[] left, float[] right)
    {
        float[] result = new float[left.Length];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = left[i] + right[i];
        }

        return result;
    }

    /// <summary>
    /// Multiplies two registers lane by lane.
    /// </summary>
    private static float[] Multiply(float[] left, float[] right)
    {
        float[] result = new float[left.Length];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = left[i] * right[i];
        }

        return result;
    }

    /// <summary>
    /// Takes the larger lane of two registers; equal lanes and unordered lanes take the second operand.
    /// </summary>
    private static float[] Maximum(float[] left, float[] right)
    {
        float[] result = new float[left.Length];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = left[i] > right[i] ? left[i] : right[i];
        }

        return result;
    }

    /// <summary>
    /// Adds adjacent lane pairs within each 128-bit lane: the pairs of the first register fill the lower two lanes
    /// and the pairs of the second register the upper two lanes.
    /// </summary>
    private static float[] HorizontalAdd(float[] left, float[] right)
    {
        float[] result = new float[left.Length];
        for (int lane = 0; lane < result.Length; lane += 4)
        {
            result[lane] = left[lane] + left[lane + 1];
            result[lane + 1] = left[lane + 2] + left[lane + 3];
            result[lane + 2] = right[lane] + right[lane + 1];
            result[lane + 3] = right[lane + 2] + right[lane + 3];
        }

        return result;
    }

    /// <summary>
    /// Selects each lane of an eight-lane register by index.
    /// </summary>
    private static float[] Permute(float[] source, int[] indices)
    {
        float[] result = new float[indices.Length];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = source[indices[i]];
        }

        return result;
    }

    /// <summary>
    /// Selects each 128-bit half of the result from the four halves of two registers.
    /// </summary>
    private static float[] Permute2x128(float[] left, float[] right, int control)
    {
        float[][] halves = [Load(left, 0, 4), Load(left, 4, 4), Load(right, 0, 4), Load(right, 4, 4)];
        return [.. halves[control & 3], .. halves[(control >> 4) & 3]];
    }

    /// <summary>
    /// Selects the two lower lanes from the first register and the two upper lanes from the second by two-bit fields.
    /// </summary>
    private static float[] Shuffle(float[] left, float[] right, int control)
        => [left[control & 3], left[(control >> 2) & 3], right[(control >> 4) & 3], right[(control >> 6) & 3]];
}
