// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Defines the lane arithmetic of the <see cref="Av1NeuralNetwork"/> traversals.
/// </content>
internal static partial class Av1NeuralNetwork
{
    /// <summary>
    /// The block position that marks a five-by-five window convolved on its own, after the groups of three.
    /// </summary>
    internal const int SingleBlockPosition = 3;

    /// <summary>
    /// Defines the network arithmetic across hardware widths.
    /// </summary>
    /// <remarks>
    /// Float addition is not associative, so each member fixes the exact order in which it adds its products. The
    /// order is the one of the x64 reference kernels, and every width and the scalar overload use the same order for
    /// each output. The width only sets how many outputs one call computes. No member fuses a multiplication with an
    /// addition, because the reference rounds every product before it adds it.
    /// <para>
    /// In the dense members one lane is one output node, and the weights of node r start r weight strides after the
    /// first node. In the convolution members one lane is one output channel, and the weights of consecutive channels
    /// are adjacent.
    /// </para>
    /// </remarks>
    internal interface INeuralNetworkOperator
    {
        /// <summary>
        /// Adds the products of eight inputs to the totals of four consecutive nodes.
        /// </summary>
        /// <param name="inputs">The first of the eight inputs.</param>
        /// <param name="weights">The weight of the first input for the first node.</param>
        /// <param name="weightStride">The distance between the weights of two consecutive nodes.</param>
        /// <param name="totals">The running totals of the four nodes.</param>
        /// <returns>
        /// The totals plus, for each node, ((p0 + p1) + (p2 + p3)) + ((p4 + p5) + (p6 + p7)), where pi is the product of
        /// input i and its weight.
        /// </returns>
        public static abstract Vector128<float> AccumulateEightInputs(ref float inputs, ref float weights, nuint weightStride, Vector128<float> totals);

        /// <summary>
        /// Adds the products of eight inputs to the totals of eight consecutive nodes.
        /// </summary>
        /// <param name="inputs">The first of the eight inputs.</param>
        /// <param name="weights">The weight of the first input for the first node.</param>
        /// <param name="weightStride">The distance between the weights of two consecutive nodes.</param>
        /// <param name="totals">The running totals of the eight nodes.</param>
        /// <returns>The totals plus the pairwise sum of each node, as for four nodes.</returns>
        public static abstract Vector256<float> AccumulateEightInputs(ref float inputs, ref float weights, nuint weightStride, Vector256<float> totals);

        /// <summary>
        /// Adds the products of eight inputs to the totals of sixteen consecutive nodes.
        /// </summary>
        /// <param name="inputs">The first of the eight inputs.</param>
        /// <param name="weights">The weight of the first input for the first node.</param>
        /// <param name="weightStride">The distance between the weights of two consecutive nodes.</param>
        /// <param name="totals">The running totals of the sixteen nodes.</param>
        /// <returns>The totals plus the pairwise sum of each node, as for four nodes.</returns>
        public static abstract Vector512<float> AccumulateEightInputs(ref float inputs, ref float weights, nuint weightStride, Vector512<float> totals);

        /// <summary>
        /// Adds the products of eight inputs to the total of one node.
        /// </summary>
        /// <param name="inputs">The first of the eight inputs.</param>
        /// <param name="weights">The weight of the first input.</param>
        /// <param name="total">The running total of the node.</param>
        /// <returns>The total plus the pairwise sum of the node, as for four nodes.</returns>
        public static abstract float AccumulateEightInputs(ref float inputs, ref float weights, float total);

        /// <summary>
        /// Adds the products of four inputs to the totals of four consecutive nodes.
        /// </summary>
        /// <param name="inputs">The first of the four inputs.</param>
        /// <param name="weights">The weight of the first input for the first node.</param>
        /// <param name="weightStride">The distance between the weights of two consecutive nodes.</param>
        /// <param name="totals">The running totals of the four nodes.</param>
        /// <returns>The totals plus, for each node, (p0 + p1) + (p2 + p3).</returns>
        public static abstract Vector128<float> AccumulateFourInputs(ref float inputs, ref float weights, nuint weightStride, Vector128<float> totals);

        /// <summary>
        /// Adds the products of four inputs to the totals of eight consecutive nodes.
        /// </summary>
        /// <param name="inputs">The first of the four inputs.</param>
        /// <param name="weights">The weight of the first input for the first node.</param>
        /// <param name="weightStride">The distance between the weights of two consecutive nodes.</param>
        /// <param name="totals">The running totals of the eight nodes.</param>
        /// <returns>The totals plus, for each node, (p0 + p1) + (p2 + p3).</returns>
        public static abstract Vector256<float> AccumulateFourInputs(ref float inputs, ref float weights, nuint weightStride, Vector256<float> totals);

        /// <summary>
        /// Adds the products of four inputs to the totals of sixteen consecutive nodes.
        /// </summary>
        /// <param name="inputs">The first of the four inputs.</param>
        /// <param name="weights">The weight of the first input for the first node.</param>
        /// <param name="weightStride">The distance between the weights of two consecutive nodes.</param>
        /// <param name="totals">The running totals of the sixteen nodes.</param>
        /// <returns>The totals plus, for each node, (p0 + p1) + (p2 + p3).</returns>
        public static abstract Vector512<float> AccumulateFourInputs(ref float inputs, ref float weights, nuint weightStride, Vector512<float> totals);

        /// <summary>
        /// Adds the products of four inputs to the total of one node.
        /// </summary>
        /// <param name="inputs">The first of the four inputs.</param>
        /// <param name="weights">The weight of the first input.</param>
        /// <param name="total">The running total of the node.</param>
        /// <returns>The total plus (p0 + p1) + (p2 + p3).</returns>
        public static abstract float AccumulateFourInputs(ref float inputs, ref float weights, float total);

        /// <summary>
        /// Adds the rounded products of four value pairs to four totals, lane by lane.
        /// </summary>
        /// <param name="values">The values.</param>
        /// <param name="weights">The weights.</param>
        /// <param name="totals">The running totals.</param>
        /// <returns>The totals plus the products, with the product rounded before the addition.</returns>
        public static abstract Vector128<float> MultiplyAdd(Vector128<float> values, Vector128<float> weights, Vector128<float> totals);

        /// <summary>
        /// Adds the rounded products of eight value pairs to eight totals, lane by lane.
        /// </summary>
        /// <param name="values">The values.</param>
        /// <param name="weights">The weights.</param>
        /// <param name="totals">The running totals.</param>
        /// <returns>The totals plus the products, with the product rounded before the addition.</returns>
        public static abstract Vector256<float> MultiplyAdd(Vector256<float> values, Vector256<float> weights, Vector256<float> totals);

        /// <summary>
        /// Adds the rounded products of sixteen value pairs to sixteen totals, lane by lane.
        /// </summary>
        /// <param name="values">The values.</param>
        /// <param name="weights">The weights.</param>
        /// <param name="totals">The running totals.</param>
        /// <returns>The totals plus the products, with the product rounded before the addition.</returns>
        public static abstract Vector512<float> MultiplyAdd(Vector512<float> values, Vector512<float> weights, Vector512<float> totals);

        /// <summary>
        /// Adds the rounded product of one value pair to a total.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="weight">The weight.</param>
        /// <param name="total">The running total.</param>
        /// <returns>The total plus the product, with the product rounded before the addition.</returns>
        public static abstract float MultiplyAdd(float value, float weight, float total);

        /// <summary>
        /// Reduces the eight lane totals of one node to one sum.
        /// </summary>
        /// <param name="lanes">The lane totals; lane j holds inputs j, j + 8, j + 16 and so on.</param>
        /// <returns>(s0 + s1) + (s2 + s3), where sj is lane j plus lane j + 4.</returns>
        public static abstract float SumLanes(Vector256<float> lanes);

        /// <summary>
        /// Reduces the eight lane totals of one node, held as two halves, to one sum.
        /// </summary>
        /// <param name="lower">The totals of lanes zero to three.</param>
        /// <param name="upper">The totals of lanes four to seven.</param>
        /// <returns>(s0 + s1) + (s2 + s3), where sj is lane j plus lane j + 4.</returns>
        public static abstract float SumLanes(Vector128<float> lower, Vector128<float> upper);

        /// <summary>
        /// Reduces the eight lane totals of one node, held as eight values, to one sum.
        /// </summary>
        /// <param name="lanes">The eight lane totals.</param>
        /// <returns>(s0 + s1) + (s2 + s3), where sj is lane j plus lane j + 4.</returns>
        public static abstract float SumLanes(ReadOnlySpan<float> lanes);

        /// <summary>
        /// Clips four values to zero from below.
        /// </summary>
        /// <param name="values">The values.</param>
        /// <returns>Each value when it is greater than zero, otherwise positive zero.</returns>
        public static abstract Vector128<float> Rectify(Vector128<float> values);

        /// <summary>
        /// Clips eight values to zero from below.
        /// </summary>
        /// <param name="values">The values.</param>
        /// <returns>Each value when it is greater than zero, otherwise positive zero.</returns>
        public static abstract Vector256<float> Rectify(Vector256<float> values);

        /// <summary>
        /// Clips sixteen values to zero from below.
        /// </summary>
        /// <param name="values">The values.</param>
        /// <returns>Each value when it is greater than zero, otherwise positive zero.</returns>
        public static abstract Vector512<float> Rectify(Vector512<float> values);

        /// <summary>
        /// Clips one value to zero from below.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The value when it is greater than zero, otherwise positive zero.</returns>
        public static abstract float Rectify(float value);

        /// <summary>
        /// Convolves one five-by-five window of one input channel for four output channels.
        /// </summary>
        /// <param name="input">The top-left input of the window.</param>
        /// <param name="inputStride">The distance between two input rows.</param>
        /// <param name="weights">The weight of the top-left filter tap for the first output channel.</param>
        /// <param name="weightStep">The distance between the weights of two consecutive filter taps.</param>
        /// <param name="biases">The biases of the four output channels.</param>
        /// <param name="position">The position of the window in its group of three, or <see cref="SingleBlockPosition"/>.</param>
        /// <returns>The four outputs before the activation.</returns>
        public static abstract Vector128<float> ConvolveFiveByFive(
            ref float input,
            nuint inputStride,
            ref float weights,
            nuint weightStep,
            Vector128<float> biases,
            int position);

        /// <summary>
        /// Convolves one five-by-five window of one input channel for eight output channels.
        /// </summary>
        /// <param name="input">The top-left input of the window.</param>
        /// <param name="inputStride">The distance between two input rows.</param>
        /// <param name="weights">The weight of the top-left filter tap for the first output channel.</param>
        /// <param name="weightStep">The distance between the weights of two consecutive filter taps.</param>
        /// <param name="biases">The biases of the eight output channels.</param>
        /// <param name="position">The position of the window in its group of three, or <see cref="SingleBlockPosition"/>.</param>
        /// <returns>The eight outputs before the activation.</returns>
        public static abstract Vector256<float> ConvolveFiveByFive(
            ref float input,
            nuint inputStride,
            ref float weights,
            nuint weightStep,
            Vector256<float> biases,
            int position);

        /// <summary>
        /// Convolves one five-by-five window of one input channel for sixteen output channels.
        /// </summary>
        /// <param name="input">The top-left input of the window.</param>
        /// <param name="inputStride">The distance between two input rows.</param>
        /// <param name="weights">The weight of the top-left filter tap for the first output channel.</param>
        /// <param name="weightStep">The distance between the weights of two consecutive filter taps.</param>
        /// <param name="biases">The biases of the sixteen output channels.</param>
        /// <param name="position">The position of the window in its group of three, or <see cref="SingleBlockPosition"/>.</param>
        /// <returns>The sixteen outputs before the activation.</returns>
        public static abstract Vector512<float> ConvolveFiveByFive(
            ref float input,
            nuint inputStride,
            ref float weights,
            nuint weightStep,
            Vector512<float> biases,
            int position);

        /// <summary>
        /// Convolves one five-by-five window of one input channel for one output channel.
        /// </summary>
        /// <param name="input">The top-left input of the window.</param>
        /// <param name="inputStride">The distance between two input rows.</param>
        /// <param name="weights">The weight of the top-left filter tap.</param>
        /// <param name="weightStep">The distance between the weights of two consecutive filter taps.</param>
        /// <param name="bias">The bias of the output channel.</param>
        /// <param name="position">The position of the window in its group of three, or <see cref="SingleBlockPosition"/>.</param>
        /// <returns>The output before the activation.</returns>
        public static abstract float ConvolveFiveByFive(ref float input, nuint inputStride, ref float weights, nuint weightStep, float bias, int position);

        /// <summary>
        /// Adds the two-by-two window of one input channel to the totals of four output channels.
        /// </summary>
        /// <param name="input">The top-left input of the window.</param>
        /// <param name="inputStride">The distance between two input rows.</param>
        /// <param name="weights">The weight of the top-left filter tap for the first output channel.</param>
        /// <param name="weightStep">The distance between the weights of two consecutive filter taps.</param>
        /// <param name="totals">The running totals of the four output channels.</param>
        /// <returns>The totals plus ((t0 + b0) + (t1 + b1)), where t are the top-row and b the bottom-row products.</returns>
        public static abstract Vector128<float> AccumulateTwoByTwo(
            ref float input,
            nuint inputStride,
            ref float weights,
            nuint weightStep,
            Vector128<float> totals);

        /// <summary>
        /// Adds the two-by-two window of one input channel to the totals of eight output channels.
        /// </summary>
        /// <param name="input">The top-left input of the window.</param>
        /// <param name="inputStride">The distance between two input rows.</param>
        /// <param name="weights">The weight of the top-left filter tap for the first output channel.</param>
        /// <param name="weightStep">The distance between the weights of two consecutive filter taps.</param>
        /// <param name="totals">The running totals of the eight output channels.</param>
        /// <returns>The totals plus ((t0 + b0) + (t1 + b1)), where t are the top-row and b the bottom-row products.</returns>
        public static abstract Vector256<float> AccumulateTwoByTwo(
            ref float input,
            nuint inputStride,
            ref float weights,
            nuint weightStep,
            Vector256<float> totals);

        /// <summary>
        /// Adds the two-by-two window of one input channel to the totals of sixteen output channels.
        /// </summary>
        /// <param name="input">The top-left input of the window.</param>
        /// <param name="inputStride">The distance between two input rows.</param>
        /// <param name="weights">The weight of the top-left filter tap for the first output channel.</param>
        /// <param name="weightStep">The distance between the weights of two consecutive filter taps.</param>
        /// <param name="totals">The running totals of the sixteen output channels.</param>
        /// <returns>The totals plus ((t0 + b0) + (t1 + b1)), where t are the top-row and b the bottom-row products.</returns>
        public static abstract Vector512<float> AccumulateTwoByTwo(
            ref float input,
            nuint inputStride,
            ref float weights,
            nuint weightStep,
            Vector512<float> totals);

        /// <summary>
        /// Adds the two-by-two window of one input channel to the total of one output channel.
        /// </summary>
        /// <param name="input">The top-left input of the window.</param>
        /// <param name="inputStride">The distance between two input rows.</param>
        /// <param name="weights">The weight of the top-left filter tap.</param>
        /// <param name="weightStep">The distance between the weights of two consecutive filter taps.</param>
        /// <param name="total">The running total of the output channel.</param>
        /// <returns>The total plus ((t0 + b0) + (t1 + b1)), where t are the top-row and b the bottom-row products.</returns>
        public static abstract float AccumulateTwoByTwo(ref float input, nuint inputStride, ref float weights, nuint weightStep, float total);
    }

    /// <summary>
    /// Evaluates the network arithmetic in the addition order of the x64 reference kernels.
    /// </summary>
    internal readonly struct NeuralNetworkOperator : INeuralNetworkOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<float> AccumulateEightInputs(ref float inputs, ref float weights, nuint weightStride, Vector128<float> totals)
        {
            // The reference multiplies one node row by all eight inputs and adds adjacent products in 128-bit lanes.
            // Here the lower and the upper four inputs are kept apart. One horizontal addition of two nodes gives
            // [r0(p0 + p1), r0(p2 + p3), r1(p0 + p1), r1(p2 + p3)], and a second one of two such vectors gives the sum
            // of the four products of each of the four nodes, which is the sum the reference makes in one 128-bit lane.
            Vector128<float> lowerInputs = Vector128.LoadUnsafe(ref inputs);
            Vector128<float> upperInputs = Vector128.LoadUnsafe(ref inputs, 4);
            ref float row1 = ref Unsafe.Add(ref weights, weightStride);
            ref float row2 = ref Unsafe.Add(ref weights, 2 * weightStride);
            ref float row3 = ref Unsafe.Add(ref weights, 3 * weightStride);

            ref float upper0 = ref Unsafe.Add(ref weights, 4);
            ref float upper1 = ref Unsafe.Add(ref row1, 4);
            ref float upper2 = ref Unsafe.Add(ref row2, 4);
            ref float upper3 = ref Unsafe.Add(ref row3, 4);

            Vector128<float> lower01 = Vector128_.HorizontalAdd(lowerInputs * Vector128.LoadUnsafe(ref weights), lowerInputs * Vector128.LoadUnsafe(ref row1));
            Vector128<float> lower23 = Vector128_.HorizontalAdd(lowerInputs * Vector128.LoadUnsafe(ref row2), lowerInputs * Vector128.LoadUnsafe(ref row3));
            Vector128<float> upper01 = Vector128_.HorizontalAdd(upperInputs * Vector128.LoadUnsafe(ref upper0), upperInputs * Vector128.LoadUnsafe(ref upper1));
            Vector128<float> upper23 = Vector128_.HorizontalAdd(upperInputs * Vector128.LoadUnsafe(ref upper2), upperInputs * Vector128.LoadUnsafe(ref upper3));

            // The two half sums of each node add once, and the chunk sum then adds onto the running total.
            return totals + (Vector128_.HorizontalAdd(lower01, lower23) + Vector128_.HorizontalAdd(upper01, upper23));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<float> AccumulateEightInputs(ref float inputs, ref float weights, nuint weightStride, Vector256<float> totals)
        {
            // This is the reference layout. Each product vector holds one node. The 256-bit horizontal addition works
            // in two independent 128-bit lanes, so after two levels the lower lane holds four node sums of inputs zero
            // to three and the upper lane the same four nodes for inputs four to seven.
            Vector256<float> values = Vector256.LoadUnsafe(ref inputs);
            Vector256<float> pairs01 = Vector256_.HorizontalAdd(
                values * Vector256.LoadUnsafe(ref weights),
                values * Vector256.LoadUnsafe(ref Unsafe.Add(ref weights, weightStride)));

            Vector256<float> pairs23 = Vector256_.HorizontalAdd(
                values * Vector256.LoadUnsafe(ref Unsafe.Add(ref weights, 2 * weightStride)),
                values * Vector256.LoadUnsafe(ref Unsafe.Add(ref weights, 3 * weightStride)));

            Vector256<float> pairs45 = Vector256_.HorizontalAdd(
                values * Vector256.LoadUnsafe(ref Unsafe.Add(ref weights, 4 * weightStride)),
                values * Vector256.LoadUnsafe(ref Unsafe.Add(ref weights, 5 * weightStride)));

            Vector256<float> pairs67 = Vector256_.HorizontalAdd(
                values * Vector256.LoadUnsafe(ref Unsafe.Add(ref weights, 6 * weightStride)),
                values * Vector256.LoadUnsafe(ref Unsafe.Add(ref weights, 7 * weightStride)));

            Vector256<float> quads0123 = Vector256_.HorizontalAdd(pairs01, pairs23);
            Vector256<float> quads4567 = Vector256_.HorizontalAdd(pairs45, pairs67);

            // Gather the lower lanes of nodes 0-3 and 4-7, and the upper lanes likewise, then add the halves.
            Vector256<float> lower = Vector256.Create(quads0123.GetLower(), quads4567.GetLower());
            Vector256<float> upper = Vector256.Create(quads0123.GetUpper(), quads4567.GetUpper());
            return totals + (lower + upper);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<float> AccumulateEightInputs(ref float inputs, ref float weights, nuint weightStride, Vector512<float> totals)
        {
            // Each product vector holds node i in its lower half and node i + 8 in its upper half. The horizontal
            // addition works in 128-bit lanes, so each half follows the 256-bit layout above on its own.
            Vector256<float> values256 = Vector256.LoadUnsafe(ref inputs);
            Vector512<float> values = Vector512.Create(values256, values256);
            ref float upperNodes = ref Unsafe.Add(ref weights, 8 * weightStride);

            Vector512<float> pairs01 = Vector512_.HorizontalAdd(
                values * LoadEightInputNodePair(ref weights, ref upperNodes, 0),
                values * LoadEightInputNodePair(ref weights, ref upperNodes, weightStride));

            Vector512<float> pairs23 = Vector512_.HorizontalAdd(
                values * LoadEightInputNodePair(ref weights, ref upperNodes, 2 * weightStride),
                values * LoadEightInputNodePair(ref weights, ref upperNodes, 3 * weightStride));

            Vector512<float> pairs45 = Vector512_.HorizontalAdd(
                values * LoadEightInputNodePair(ref weights, ref upperNodes, 4 * weightStride),
                values * LoadEightInputNodePair(ref weights, ref upperNodes, 5 * weightStride));

            Vector512<float> pairs67 = Vector512_.HorizontalAdd(
                values * LoadEightInputNodePair(ref weights, ref upperNodes, 6 * weightStride),
                values * LoadEightInputNodePair(ref weights, ref upperNodes, 7 * weightStride));

            // The 128-bit lanes now hold: quads0123 = [0-3 low, 0-3 high, 8-11 low, 8-11 high] and
            // quads4567 = [4-7 low, 4-7 high, 12-15 low, 12-15 high], where low and high name the input halves.
            Vector512<float> quads0123 = Vector512_.HorizontalAdd(pairs01, pairs23);
            Vector512<float> quads4567 = Vector512_.HorizontalAdd(pairs45, pairs67);

            // Indices 0 to 15 select from quads0123 and 16 to 31 from quads4567, so the two permutes put the input
            // halves of nodes 0-3, 4-7, 8-11 and 12-15 in node order before they add.
            Vector512<int> lowerIndices = Vector512.Create(0, 1, 2, 3, 16, 17, 18, 19, 8, 9, 10, 11, 24, 25, 26, 27);
            Vector512<int> upperIndices = Vector512.Create(4, 5, 6, 7, 20, 21, 22, 23, 12, 13, 14, 15, 28, 29, 30, 31);
            Vector512<float> lower = Vector512_.PermuteVar16x32x2(quads0123.AsInt32(), lowerIndices, quads4567.AsInt32()).AsSingle();
            Vector512<float> upper = Vector512_.PermuteVar16x32x2(quads0123.AsInt32(), upperIndices, quads4567.AsInt32()).AsSingle();
            return totals + (lower + upper);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float AccumulateEightInputs(ref float inputs, ref float weights, float total)
        {
            float p0 = inputs * weights;
            float p1 = Unsafe.Add(ref inputs, 1) * Unsafe.Add(ref weights, 1);
            float p2 = Unsafe.Add(ref inputs, 2) * Unsafe.Add(ref weights, 2);
            float p3 = Unsafe.Add(ref inputs, 3) * Unsafe.Add(ref weights, 3);
            float p4 = Unsafe.Add(ref inputs, 4) * Unsafe.Add(ref weights, 4);
            float p5 = Unsafe.Add(ref inputs, 5) * Unsafe.Add(ref weights, 5);
            float p6 = Unsafe.Add(ref inputs, 6) * Unsafe.Add(ref weights, 6);
            float p7 = Unsafe.Add(ref inputs, 7) * Unsafe.Add(ref weights, 7);
            return total + (((p0 + p1) + (p2 + p3)) + ((p4 + p5) + (p6 + p7)));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<float> AccumulateFourInputs(ref float inputs, ref float weights, nuint weightStride, Vector128<float> totals)
        {
            // Two levels of horizontal additions turn four node rows of four products into the four node sums.
            Vector128<float> values = Vector128.LoadUnsafe(ref inputs);
            Vector128<float> pairs01 = Vector128_.HorizontalAdd(
                values * Vector128.LoadUnsafe(ref weights),
                values * Vector128.LoadUnsafe(ref Unsafe.Add(ref weights, weightStride)));

            Vector128<float> pairs23 = Vector128_.HorizontalAdd(
                values * Vector128.LoadUnsafe(ref Unsafe.Add(ref weights, 2 * weightStride)),
                values * Vector128.LoadUnsafe(ref Unsafe.Add(ref weights, 3 * weightStride)));

            return totals + Vector128_.HorizontalAdd(pairs01, pairs23);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<float> AccumulateFourInputs(ref float inputs, ref float weights, nuint weightStride, Vector256<float> totals)
        {
            // The lower 128-bit lane holds nodes 0-3 and the upper lane nodes 4-7, so each lane repeats the 128-bit
            // layout above and the result is in node order.
            Vector128<float> values128 = Vector128.LoadUnsafe(ref inputs);
            Vector256<float> values = Vector256.Create(values128, values128);
            ref float upperNodes = ref Unsafe.Add(ref weights, 4 * weightStride);
            Vector256<float> pairs01 = Vector256_.HorizontalAdd(
                values * LoadFourInputNodePair(ref weights, ref upperNodes, 0),
                values * LoadFourInputNodePair(ref weights, ref upperNodes, weightStride));

            Vector256<float> pairs23 = Vector256_.HorizontalAdd(
                values * LoadFourInputNodePair(ref weights, ref upperNodes, 2 * weightStride),
                values * LoadFourInputNodePair(ref weights, ref upperNodes, 3 * weightStride));

            return totals + Vector256_.HorizontalAdd(pairs01, pairs23);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<float> AccumulateFourInputs(ref float inputs, ref float weights, nuint weightStride, Vector512<float> totals)
        {
            // The 128-bit lane k holds nodes 4k to 4k + 3, so each lane repeats the 128-bit layout above.
            Vector128<float> values128 = Vector128.LoadUnsafe(ref inputs);
            Vector256<float> values256 = Vector256.Create(values128, values128);
            Vector512<float> values = Vector512.Create(values256, values256);
            Vector512<float> pairs01 = Vector512_.HorizontalAdd(
                values * LoadNodeQuad(ref weights, weightStride, 0),
                values * LoadNodeQuad(ref weights, weightStride, weightStride));

            Vector512<float> pairs23 = Vector512_.HorizontalAdd(
                values * LoadNodeQuad(ref weights, weightStride, 2 * weightStride),
                values * LoadNodeQuad(ref weights, weightStride, 3 * weightStride));

            return totals + Vector512_.HorizontalAdd(pairs01, pairs23);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float AccumulateFourInputs(ref float inputs, ref float weights, float total)
        {
            float p0 = inputs * weights;
            float p1 = Unsafe.Add(ref inputs, 1) * Unsafe.Add(ref weights, 1);
            float p2 = Unsafe.Add(ref inputs, 2) * Unsafe.Add(ref weights, 2);
            float p3 = Unsafe.Add(ref inputs, 3) * Unsafe.Add(ref weights, 3);
            return total + ((p0 + p1) + (p2 + p3));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<float> MultiplyAdd(Vector128<float> values, Vector128<float> weights, Vector128<float> totals)
            => totals + (values * weights);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<float> MultiplyAdd(Vector256<float> values, Vector256<float> weights, Vector256<float> totals)
            => totals + (values * weights);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<float> MultiplyAdd(Vector512<float> values, Vector512<float> weights, Vector512<float> totals)
            => totals + (values * weights);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float MultiplyAdd(float value, float weight, float total)
            => total + (value * weight);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SumLanes(Vector256<float> lanes)
            => SumLanes(lanes.GetLower(), lanes.GetUpper());

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SumLanes(Vector128<float> lower, Vector128<float> upper)
        {
            // The halves add first, then adjacent sums: lane 0 is s0 + s1 and lane 1 is s2 + s3. The reference adds
            // the two pair sums in the other order, which gives the same bits because one addition commutes.
            Vector128<float> halves = lower + upper;
            Vector128<float> pairs = Vector128_.HorizontalAdd(halves, halves);
            return pairs.ToScalar() + pairs.GetElement(1);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SumLanes(ReadOnlySpan<float> lanes)
            => ((lanes[0] + lanes[4]) + (lanes[1] + lanes[5])) + ((lanes[2] + lanes[6]) + (lanes[3] + lanes[7]));

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<float> Rectify(Vector128<float> values)
            => values & Vector128.GreaterThan(values, Vector128<float>.Zero);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<float> Rectify(Vector256<float> values)
            => values & Vector256.GreaterThan(values, Vector256<float>.Zero);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<float> Rectify(Vector512<float> values)
            => values & Vector512.GreaterThan(values, Vector512<float>.Zero);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Rectify(float value) => value > 0F ? value : 0F;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<float> ConvolveFiveByFive(
            ref float input,
            nuint inputStride,
            ref float weights,
            nuint weightStep,
            Vector128<float> biases,
            int position)
        {
            // Each column sum adds the five row products of one window column. The reference keeps one running
            // vector per column for a group of three windows, which adds the rows one at a time onto zero. A single
            // window keeps the first row apart and adds the other four rows in pairs; its last column is a scalar
            // running sum, which again adds the rows one at a time onto zero.
            bool single = position == SingleBlockPosition;
            Vector128<float> column0 = SumColumn128(ref input, inputStride, ref weights, weightStep, 0, single);
            Vector128<float> column1 = SumColumn128(ref input, inputStride, ref weights, weightStep, 1, single);
            Vector128<float> column2 = SumColumn128(ref input, inputStride, ref weights, weightStep, 2, single);
            Vector128<float> column3 = SumColumn128(ref input, inputStride, ref weights, weightStep, 3, single);
            Vector128<float> column4 = SumColumn128(ref input, inputStride, ref weights, weightStep, 4, false);

            // The reference reduces the column sums of a group of three windows with one horizontal addition of its two
            // column vectors and three additions of 128-bit halves, so each window position has its own grouping. The
            // bias is added first, then the two partial sums.
            return position switch
            {
                0 => (biases + ((column0 + column1) + column4)) + (column2 + column3),
                1 => (biases + (column0 + (column1 + column2))) + (column3 + column4),
                2 => (biases + (column4 + (column2 + column3))) + (column0 + column1),
                _ => ((biases + column4) + (column0 + column1)) + (column2 + column3)
            };
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<float> ConvolveFiveByFive(
            ref float input,
            nuint inputStride,
            ref float weights,
            nuint weightStep,
            Vector256<float> biases,
            int position)
        {
            bool single = position == SingleBlockPosition;
            Vector256<float> column0 = SumColumn256(ref input, inputStride, ref weights, weightStep, 0, single);
            Vector256<float> column1 = SumColumn256(ref input, inputStride, ref weights, weightStep, 1, single);
            Vector256<float> column2 = SumColumn256(ref input, inputStride, ref weights, weightStep, 2, single);
            Vector256<float> column3 = SumColumn256(ref input, inputStride, ref weights, weightStep, 3, single);
            Vector256<float> column4 = SumColumn256(ref input, inputStride, ref weights, weightStep, 4, false);
            return position switch
            {
                0 => (biases + ((column0 + column1) + column4)) + (column2 + column3),
                1 => (biases + (column0 + (column1 + column2))) + (column3 + column4),
                2 => (biases + (column4 + (column2 + column3))) + (column0 + column1),
                _ => ((biases + column4) + (column0 + column1)) + (column2 + column3)
            };
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<float> ConvolveFiveByFive(
            ref float input,
            nuint inputStride,
            ref float weights,
            nuint weightStep,
            Vector512<float> biases,
            int position)
        {
            bool single = position == SingleBlockPosition;
            Vector512<float> column0 = SumColumn512(ref input, inputStride, ref weights, weightStep, 0, single);
            Vector512<float> column1 = SumColumn512(ref input, inputStride, ref weights, weightStep, 1, single);
            Vector512<float> column2 = SumColumn512(ref input, inputStride, ref weights, weightStep, 2, single);
            Vector512<float> column3 = SumColumn512(ref input, inputStride, ref weights, weightStep, 3, single);
            Vector512<float> column4 = SumColumn512(ref input, inputStride, ref weights, weightStep, 4, false);
            return position switch
            {
                0 => (biases + ((column0 + column1) + column4)) + (column2 + column3),
                1 => (biases + (column0 + (column1 + column2))) + (column3 + column4),
                2 => (biases + (column4 + (column2 + column3))) + (column0 + column1),
                _ => ((biases + column4) + (column0 + column1)) + (column2 + column3)
            };
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ConvolveFiveByFive(ref float input, nuint inputStride, ref float weights, nuint weightStep, float bias, int position)
        {
            bool single = position == SingleBlockPosition;
            float column0 = SumColumn(ref input, inputStride, ref weights, weightStep, 0, single);
            float column1 = SumColumn(ref input, inputStride, ref weights, weightStep, 1, single);
            float column2 = SumColumn(ref input, inputStride, ref weights, weightStep, 2, single);
            float column3 = SumColumn(ref input, inputStride, ref weights, weightStep, 3, single);
            float column4 = SumColumn(ref input, inputStride, ref weights, weightStep, 4, false);
            return position switch
            {
                0 => (bias + ((column0 + column1) + column4)) + (column2 + column3),
                1 => (bias + (column0 + (column1 + column2))) + (column3 + column4),
                2 => (bias + (column4 + (column2 + column3))) + (column0 + column1),
                _ => ((bias + column4) + (column0 + column1)) + (column2 + column3)
            };
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<float> AccumulateTwoByTwo(ref float input, nuint inputStride, ref float weights, nuint weightStep, Vector128<float> totals)
        {
            // The reference adds the top-row and the bottom-row products of each column first, then the two columns
            // with one horizontal addition, then the window onto the running total of the output.
            Vector128<float> top0 = Vector128.Create(input) * Vector128.LoadUnsafe(ref weights);
            Vector128<float> top1 = Vector128.Create(Unsafe.Add(ref input, 1)) * Vector128.LoadUnsafe(ref weights, weightStep);
            Vector128<float> bottom0 = Vector128.Create(Unsafe.Add(ref input, inputStride)) * Vector128.LoadUnsafe(ref weights, 2 * weightStep);
            Vector128<float> bottom1 = Vector128.Create(Unsafe.Add(ref input, inputStride + 1)) * Vector128.LoadUnsafe(ref weights, 3 * weightStep);
            return totals + ((top0 + bottom0) + (top1 + bottom1));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<float> AccumulateTwoByTwo(ref float input, nuint inputStride, ref float weights, nuint weightStep, Vector256<float> totals)
        {
            Vector256<float> top0 = Vector256.Create(input) * Vector256.LoadUnsafe(ref weights);
            Vector256<float> top1 = Vector256.Create(Unsafe.Add(ref input, 1)) * Vector256.LoadUnsafe(ref weights, weightStep);
            Vector256<float> bottom0 = Vector256.Create(Unsafe.Add(ref input, inputStride)) * Vector256.LoadUnsafe(ref weights, 2 * weightStep);
            Vector256<float> bottom1 = Vector256.Create(Unsafe.Add(ref input, inputStride + 1)) * Vector256.LoadUnsafe(ref weights, 3 * weightStep);
            return totals + ((top0 + bottom0) + (top1 + bottom1));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<float> AccumulateTwoByTwo(ref float input, nuint inputStride, ref float weights, nuint weightStep, Vector512<float> totals)
        {
            Vector512<float> top0 = Vector512.Create(input) * Vector512.LoadUnsafe(ref weights);
            Vector512<float> top1 = Vector512.Create(Unsafe.Add(ref input, 1)) * Vector512.LoadUnsafe(ref weights, weightStep);
            Vector512<float> bottom0 = Vector512.Create(Unsafe.Add(ref input, inputStride)) * Vector512.LoadUnsafe(ref weights, 2 * weightStep);
            Vector512<float> bottom1 = Vector512.Create(Unsafe.Add(ref input, inputStride + 1)) * Vector512.LoadUnsafe(ref weights, 3 * weightStep);
            return totals + ((top0 + bottom0) + (top1 + bottom1));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float AccumulateTwoByTwo(ref float input, nuint inputStride, ref float weights, nuint weightStep, float total)
        {
            float top0 = input * weights;
            float top1 = Unsafe.Add(ref input, 1) * Unsafe.Add(ref weights, weightStep);
            float bottom0 = Unsafe.Add(ref input, inputStride) * Unsafe.Add(ref weights, 2 * weightStep);
            float bottom1 = Unsafe.Add(ref input, inputStride + 1) * Unsafe.Add(ref weights, 3 * weightStep);
            return total + ((top0 + bottom0) + (top1 + bottom1));
        }

        /// <summary>
        /// Loads the weights of two nodes into the two 128-bit halves of one vector.
        /// </summary>
        /// <param name="lowerNodes">The first weight of the node that fills the lower half, before the offset.</param>
        /// <param name="upperNodes">The first weight of the node that fills the upper half, before the offset.</param>
        /// <param name="offset">The offset of both nodes from their first weight references.</param>
        /// <returns>The four weights of each node.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<float> LoadFourInputNodePair(ref float lowerNodes, ref float upperNodes, nuint offset)
            => Vector256.Create(Vector128.LoadUnsafe(ref lowerNodes, offset), Vector128.LoadUnsafe(ref upperNodes, offset));

        /// <summary>
        /// Loads the eight-input weights of two nodes into the two 256-bit halves of one vector.
        /// </summary>
        /// <param name="lowerNodes">The first weight of the node that fills the lower half, before the offset.</param>
        /// <param name="upperNodes">The first weight of the node that fills the upper half, before the offset.</param>
        /// <param name="offset">The offset of both nodes from their first weight references.</param>
        /// <returns>The eight weights of each node.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<float> LoadEightInputNodePair(ref float lowerNodes, ref float upperNodes, nuint offset)
            => Vector512.Create(Vector256.LoadUnsafe(ref lowerNodes, offset), Vector256.LoadUnsafe(ref upperNodes, offset));

        /// <summary>
        /// Loads the four-input weights of the nodes offset, offset + 4, offset + 8 and offset + 12 node strides into
        /// the four 128-bit lanes of one vector.
        /// </summary>
        /// <param name="weights">The first weight of the first node.</param>
        /// <param name="weightStride">The distance between the weights of two consecutive nodes.</param>
        /// <param name="offset">The offset of the node of the lowest lane.</param>
        /// <returns>The four weights of each of the four nodes.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<float> LoadNodeQuad(ref float weights, nuint weightStride, nuint offset)
        {
            nuint quarter = 4 * weightStride;
            ref float first = ref Unsafe.Add(ref weights, offset);
            Vector256<float> lower = Vector256.Create(Vector128.LoadUnsafe(ref first), Vector128.LoadUnsafe(ref first, quarter));
            Vector256<float> upper = Vector256.Create(Vector128.LoadUnsafe(ref first, 2 * quarter), Vector128.LoadUnsafe(ref first, 3 * quarter));
            return Vector512.Create(lower, upper);
        }

        /// <summary>
        /// Adds the five row products of one window column for four output channels.
        /// </summary>
        /// <param name="input">The top-left input of the window.</param>
        /// <param name="inputStride">The distance between two input rows.</param>
        /// <param name="weights">The weight of the top-left filter tap for the first output channel.</param>
        /// <param name="weightStep">The distance between the weights of two consecutive filter taps.</param>
        /// <param name="column">The window column.</param>
        /// <param name="inPairs">Whether the rows after the first add in pairs instead of one at a time.</param>
        /// <returns>
        /// (0 + p0) + ((p1 + p2) + (p3 + p4)) in pairs, otherwise ((((0 + p0) + p1) + p2) + p3) + p4, where pr is the
        /// product of row r.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<float> SumColumn128(ref float input, nuint inputStride, ref float weights, nuint weightStep, nuint column, bool inPairs)
        {
            // Consecutive rows of the filter are five taps apart.
            ref float top = ref Unsafe.Add(ref input, column);
            ref float tap = ref Unsafe.Add(ref weights, column * weightStep);
            nuint rowStep = 5 * weightStep;
            Vector128<float> p0 = Vector128.Create(top) * Vector128.LoadUnsafe(ref tap);
            Vector128<float> p1 = Vector128.Create(Unsafe.Add(ref top, inputStride)) * Vector128.LoadUnsafe(ref tap, rowStep);
            Vector128<float> p2 = Vector128.Create(Unsafe.Add(ref top, 2 * inputStride)) * Vector128.LoadUnsafe(ref tap, 2 * rowStep);
            Vector128<float> p3 = Vector128.Create(Unsafe.Add(ref top, 3 * inputStride)) * Vector128.LoadUnsafe(ref tap, 3 * rowStep);
            Vector128<float> p4 = Vector128.Create(Unsafe.Add(ref top, 4 * inputStride)) * Vector128.LoadUnsafe(ref tap, 4 * rowStep);
            Vector128<float> first = Vector128<float>.Zero + p0;
            return inPairs ? first + ((p1 + p2) + (p3 + p4)) : (((first + p1) + p2) + p3) + p4;
        }

        /// <summary>
        /// Adds the five row products of one window column for eight output channels.
        /// </summary>
        /// <param name="input">The top-left input of the window.</param>
        /// <param name="inputStride">The distance between two input rows.</param>
        /// <param name="weights">The weight of the top-left filter tap for the first output channel.</param>
        /// <param name="weightStep">The distance between the weights of two consecutive filter taps.</param>
        /// <param name="column">The window column.</param>
        /// <param name="inPairs">Whether the rows after the first add in pairs instead of one at a time.</param>
        /// <returns>The column sum in the order that <paramref name="inPairs"/> selects, as for four channels.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<float> SumColumn256(ref float input, nuint inputStride, ref float weights, nuint weightStep, nuint column, bool inPairs)
        {
            ref float top = ref Unsafe.Add(ref input, column);
            ref float tap = ref Unsafe.Add(ref weights, column * weightStep);
            nuint rowStep = 5 * weightStep;
            Vector256<float> p0 = Vector256.Create(top) * Vector256.LoadUnsafe(ref tap);
            Vector256<float> p1 = Vector256.Create(Unsafe.Add(ref top, inputStride)) * Vector256.LoadUnsafe(ref tap, rowStep);
            Vector256<float> p2 = Vector256.Create(Unsafe.Add(ref top, 2 * inputStride)) * Vector256.LoadUnsafe(ref tap, 2 * rowStep);
            Vector256<float> p3 = Vector256.Create(Unsafe.Add(ref top, 3 * inputStride)) * Vector256.LoadUnsafe(ref tap, 3 * rowStep);
            Vector256<float> p4 = Vector256.Create(Unsafe.Add(ref top, 4 * inputStride)) * Vector256.LoadUnsafe(ref tap, 4 * rowStep);
            Vector256<float> first = Vector256<float>.Zero + p0;
            return inPairs ? first + ((p1 + p2) + (p3 + p4)) : (((first + p1) + p2) + p3) + p4;
        }

        /// <summary>
        /// Adds the five row products of one window column for sixteen output channels.
        /// </summary>
        /// <param name="input">The top-left input of the window.</param>
        /// <param name="inputStride">The distance between two input rows.</param>
        /// <param name="weights">The weight of the top-left filter tap for the first output channel.</param>
        /// <param name="weightStep">The distance between the weights of two consecutive filter taps.</param>
        /// <param name="column">The window column.</param>
        /// <param name="inPairs">Whether the rows after the first add in pairs instead of one at a time.</param>
        /// <returns>The column sum in the order that <paramref name="inPairs"/> selects, as for four channels.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<float> SumColumn512(ref float input, nuint inputStride, ref float weights, nuint weightStep, nuint column, bool inPairs)
        {
            ref float top = ref Unsafe.Add(ref input, column);
            ref float tap = ref Unsafe.Add(ref weights, column * weightStep);
            nuint rowStep = 5 * weightStep;
            Vector512<float> p0 = Vector512.Create(top) * Vector512.LoadUnsafe(ref tap);
            Vector512<float> p1 = Vector512.Create(Unsafe.Add(ref top, inputStride)) * Vector512.LoadUnsafe(ref tap, rowStep);
            Vector512<float> p2 = Vector512.Create(Unsafe.Add(ref top, 2 * inputStride)) * Vector512.LoadUnsafe(ref tap, 2 * rowStep);
            Vector512<float> p3 = Vector512.Create(Unsafe.Add(ref top, 3 * inputStride)) * Vector512.LoadUnsafe(ref tap, 3 * rowStep);
            Vector512<float> p4 = Vector512.Create(Unsafe.Add(ref top, 4 * inputStride)) * Vector512.LoadUnsafe(ref tap, 4 * rowStep);
            Vector512<float> first = Vector512<float>.Zero + p0;
            return inPairs ? first + ((p1 + p2) + (p3 + p4)) : (((first + p1) + p2) + p3) + p4;
        }

        /// <summary>
        /// Adds the five row products of one window column for one output channel.
        /// </summary>
        /// <param name="input">The top-left input of the window.</param>
        /// <param name="inputStride">The distance between two input rows.</param>
        /// <param name="weights">The weight of the top-left filter tap.</param>
        /// <param name="weightStep">The distance between the weights of two consecutive filter taps.</param>
        /// <param name="column">The window column.</param>
        /// <param name="inPairs">Whether the rows after the first add in pairs instead of one at a time.</param>
        /// <returns>The column sum in the order that <paramref name="inPairs"/> selects, as for four channels.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SumColumn(ref float input, nuint inputStride, ref float weights, nuint weightStep, nuint column, bool inPairs)
        {
            ref float top = ref Unsafe.Add(ref input, column);
            ref float tap = ref Unsafe.Add(ref weights, column * weightStep);
            nuint rowStep = 5 * weightStep;
            float p0 = top * tap;
            float p1 = Unsafe.Add(ref top, inputStride) * Unsafe.Add(ref tap, rowStep);
            float p2 = Unsafe.Add(ref top, 2 * inputStride) * Unsafe.Add(ref tap, 2 * rowStep);
            float p3 = Unsafe.Add(ref top, 3 * inputStride) * Unsafe.Add(ref tap, 3 * rowStep);
            float p4 = Unsafe.Add(ref top, 4 * inputStride) * Unsafe.Add(ref tap, 4 * rowStep);
            float first = 0F + p0;
            return inPairs ? first + ((p1 + p2) + (p3 + p4)) : (((first + p1) + p2) + p3) + p4;
        }
    }
}
