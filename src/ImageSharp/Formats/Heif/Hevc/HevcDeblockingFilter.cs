// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Applies the HEVC luma and chroma deblocking kernels to four-sample edge segments.
/// </summary>
internal static class HevcDeblockingFilter
{
    /// <summary>
    /// Defines orientation-specific access to the four samples running along one deblocking edge segment.
    /// </summary>
    private interface IEdgeOperator
    {
        /// <summary>
        /// Loads four samples at one signed distance across the edge.
        /// </summary>
        /// <param name="picture">The reconstructed picture.</param>
        /// <param name="plane">The component plane.</param>
        /// <param name="x">The first Q-side sample X coordinate.</param>
        /// <param name="y">The first Q-side sample Y coordinate.</param>
        /// <param name="distance">The signed sample distance across the edge.</param>
        /// <returns>Four widened samples ordered along the edge.</returns>
        public static abstract Vector128<int> LoadVector(HevcPictureBuffer picture, HevcPlane plane, int x, int y, int distance);

        /// <summary>
        /// Stores four samples at one signed distance across the edge.
        /// </summary>
        /// <param name="picture">The reconstructed picture.</param>
        /// <param name="plane">The component plane.</param>
        /// <param name="x">The first Q-side sample X coordinate.</param>
        /// <param name="y">The first Q-side sample Y coordinate.</param>
        /// <param name="distance">The signed sample distance across the edge.</param>
        /// <param name="value">The four widened samples ordered along the edge.</param>
        /// <param name="count">The number of low lanes to store.</param>
        public static abstract void StoreVector(
            HevcPictureBuffer picture,
            HevcPlane plane,
            int x,
            int y,
            int distance,
            Vector128<int> value,
            int count);

        /// <summary>
        /// Loads one scalar sample at a signed distance across and an offset along the edge.
        /// </summary>
        /// <param name="picture">The reconstructed picture.</param>
        /// <param name="plane">The component plane.</param>
        /// <param name="x">The first Q-side sample X coordinate.</param>
        /// <param name="y">The first Q-side sample Y coordinate.</param>
        /// <param name="distance">The signed sample distance across the edge.</param>
        /// <param name="index">The sample offset along the edge.</param>
        /// <returns>The selected sample.</returns>
        public static abstract int LoadScalar(HevcPictureBuffer picture, HevcPlane plane, int x, int y, int distance, int index);

        /// <summary>
        /// Stores one scalar sample at a signed distance across and an offset along the edge.
        /// </summary>
        /// <param name="picture">The reconstructed picture.</param>
        /// <param name="plane">The component plane.</param>
        /// <param name="x">The first Q-side sample X coordinate.</param>
        /// <param name="y">The first Q-side sample Y coordinate.</param>
        /// <param name="distance">The signed sample distance across the edge.</param>
        /// <param name="index">The sample offset along the edge.</param>
        /// <param name="value">The filtered sample.</param>
        public static abstract void StoreScalar(HevcPictureBuffer picture, HevcPlane plane, int x, int y, int distance, int index, int value);
    }

    /// <summary>
    /// Filters four rows crossing one vertical luma boundary.
    /// </summary>
    /// <param name="picture">The reconstructed picture.</param>
    /// <param name="plane">The component plane.</param>
    /// <param name="x">The first Q-side sample X coordinate.</param>
    /// <param name="y">The top sample Y coordinate.</param>
    /// <param name="beta">The scaled discontinuity threshold.</param>
    /// <param name="tc">The scaled clipping threshold.</param>
    /// <param name="partPNoFilter">Whether the P-side block retains its original samples.</param>
    /// <param name="partQNoFilter">Whether the Q-side block retains its original samples.</param>
    /// <param name="bitDepth">The component sample precision.</param>
    public static void FilterVerticalLuma(
        HevcPictureBuffer picture,
        HevcPlane plane,
        int x,
        int y,
        int beta,
        int tc,
        bool partPNoFilter,
        bool partQNoFilter,
        int bitDepth)
        => FilterLuma<VerticalEdgeOperator>(picture, plane, x, y, beta, tc, partPNoFilter, partQNoFilter, bitDepth);

    /// <summary>
    /// Filters four columns crossing one horizontal luma boundary.
    /// </summary>
    /// <param name="picture">The reconstructed picture.</param>
    /// <param name="plane">The component plane.</param>
    /// <param name="x">The left sample X coordinate.</param>
    /// <param name="y">The first Q-side sample Y coordinate.</param>
    /// <param name="beta">The scaled discontinuity threshold.</param>
    /// <param name="tc">The scaled clipping threshold.</param>
    /// <param name="partPNoFilter">Whether the P-side block retains its original samples.</param>
    /// <param name="partQNoFilter">Whether the Q-side block retains its original samples.</param>
    /// <param name="bitDepth">The component sample precision.</param>
    public static void FilterHorizontalLuma(
        HevcPictureBuffer picture,
        HevcPlane plane,
        int x,
        int y,
        int beta,
        int tc,
        bool partPNoFilter,
        bool partQNoFilter,
        int bitDepth)
        => FilterLuma<HorizontalEdgeOperator>(picture, plane, x, y, beta, tc, partPNoFilter, partQNoFilter, bitDepth);

    /// <summary>
    /// Filters four rows crossing one vertical chroma boundary.
    /// </summary>
    /// <param name="picture">The reconstructed picture.</param>
    /// <param name="plane">The Cb or Cr component plane.</param>
    /// <param name="x">The first Q-side sample X coordinate.</param>
    /// <param name="y">The top sample Y coordinate.</param>
    /// <param name="tc">The scaled clipping threshold.</param>
    /// <param name="partPNoFilter">Whether the P-side block retains its original samples.</param>
    /// <param name="partQNoFilter">Whether the Q-side block retains its original samples.</param>
    /// <param name="bitDepth">The component sample precision.</param>
    /// <param name="count">The number of samples in the edge segment.</param>
    public static void FilterVerticalChroma(
        HevcPictureBuffer picture,
        HevcPlane plane,
        int x,
        int y,
        int tc,
        bool partPNoFilter,
        bool partQNoFilter,
        int bitDepth,
        int count)
        => FilterChroma<VerticalEdgeOperator>(picture, plane, x, y, tc, partPNoFilter, partQNoFilter, bitDepth, count);

    /// <summary>
    /// Filters four columns crossing one horizontal chroma boundary.
    /// </summary>
    /// <param name="picture">The reconstructed picture.</param>
    /// <param name="plane">The Cb or Cr component plane.</param>
    /// <param name="x">The left sample X coordinate.</param>
    /// <param name="y">The first Q-side sample Y coordinate.</param>
    /// <param name="tc">The scaled clipping threshold.</param>
    /// <param name="partPNoFilter">Whether the P-side block retains its original samples.</param>
    /// <param name="partQNoFilter">Whether the Q-side block retains its original samples.</param>
    /// <param name="bitDepth">The component sample precision.</param>
    /// <param name="count">The number of samples in the edge segment.</param>
    public static void FilterHorizontalChroma(
        HevcPictureBuffer picture,
        HevcPlane plane,
        int x,
        int y,
        int tc,
        bool partPNoFilter,
        bool partQNoFilter,
        int bitDepth,
        int count)
        => FilterChroma<HorizontalEdgeOperator>(picture, plane, x, y, tc, partPNoFilter, partQNoFilter, bitDepth, count);

    /// <summary>
    /// Applies the strong or weak luma kernel through one closed edge-orientation operator.
    /// </summary>
    /// <typeparam name="TOperator">The vertical or horizontal sample-access operator.</typeparam>
    /// <param name="picture">The reconstructed picture.</param>
    /// <param name="plane">The component plane.</param>
    /// <param name="x">The first Q-side sample X coordinate.</param>
    /// <param name="y">The first Q-side sample Y coordinate.</param>
    /// <param name="beta">The scaled discontinuity threshold.</param>
    /// <param name="tc">The scaled clipping threshold.</param>
    /// <param name="partPNoFilter">Whether the P-side block retains its original samples.</param>
    /// <param name="partQNoFilter">Whether the Q-side block retains its original samples.</param>
    /// <param name="bitDepth">The component sample precision.</param>
    private static void FilterLuma<TOperator>(
        HevcPictureBuffer picture,
        HevcPlane plane,
        int x,
        int y,
        int beta,
        int tc,
        bool partPNoFilter,
        bool partQNoFilter,
        int bitDepth)
        where TOperator : struct, IEdgeOperator
    {
        if (beta == 0)
        {
            return;
        }

        int p2Start = TOperator.LoadScalar(picture, plane, x, y, -3, 0);
        int p1Start = TOperator.LoadScalar(picture, plane, x, y, -2, 0);
        int p0Start = TOperator.LoadScalar(picture, plane, x, y, -1, 0);
        int q0Start = TOperator.LoadScalar(picture, plane, x, y, 0, 0);
        int q1Start = TOperator.LoadScalar(picture, plane, x, y, 1, 0);
        int q2Start = TOperator.LoadScalar(picture, plane, x, y, 2, 0);
        int p2End = TOperator.LoadScalar(picture, plane, x, y, -3, 3);
        int p1End = TOperator.LoadScalar(picture, plane, x, y, -2, 3);
        int p0End = TOperator.LoadScalar(picture, plane, x, y, -1, 3);
        int q0End = TOperator.LoadScalar(picture, plane, x, y, 0, 3);
        int q1End = TOperator.LoadScalar(picture, plane, x, y, 1, 3);
        int q2End = TOperator.LoadScalar(picture, plane, x, y, 2, 3);
        int dpStart = Math.Abs(p2Start - (2 * p1Start) + p0Start);
        int dqStart = Math.Abs(q0Start - (2 * q1Start) + q2Start);
        int dpEnd = Math.Abs(p2End - (2 * p1End) + p0End);
        int dqEnd = Math.Abs(q0End - (2 * q1End) + q2End);
        int dp = dpStart + dpEnd;
        int dq = dqStart + dqEnd;
        int discontinuity = dp + dq;
        if (discontinuity >= beta)
        {
            return;
        }

        int sideThreshold = (beta + (beta >> 1)) >> 3;
        bool filterSecondP = dp < sideThreshold;
        bool filterSecondQ = dq < sideThreshold;
        bool strong = UsesStrongFiltering<TOperator>(picture, plane, x, y, 0, 2 * (dpStart + dqStart), beta, tc)
            && UsesStrongFiltering<TOperator>(picture, plane, x, y, 3, 2 * (dpEnd + dqEnd), beta, tc);

        if (!Vector128.IsHardwareAccelerated)
        {
            for (int index = 0; index < 4; index++)
            {
                FilterLumaScalar<TOperator>(
                    picture,
                    plane,
                    x,
                    y,
                    index,
                    tc,
                    strong,
                    partPNoFilter,
                    partQNoFilter,
                    tc * 10,
                    filterSecondP,
                    filterSecondQ,
                    bitDepth);
            }

            return;
        }

        Vector128<int> p3 = TOperator.LoadVector(picture, plane, x, y, -4);
        Vector128<int> p2 = TOperator.LoadVector(picture, plane, x, y, -3);
        Vector128<int> p1 = TOperator.LoadVector(picture, plane, x, y, -2);
        Vector128<int> p0 = TOperator.LoadVector(picture, plane, x, y, -1);
        Vector128<int> q0 = TOperator.LoadVector(picture, plane, x, y, 0);
        Vector128<int> q1 = TOperator.LoadVector(picture, plane, x, y, 1);
        Vector128<int> q2 = TOperator.LoadVector(picture, plane, x, y, 2);
        Vector128<int> q3 = TOperator.LoadVector(picture, plane, x, y, 3);

        // Each Int32 lane is one row or column along the edge. The threshold decision is shared by all four lanes,
        // while the filter arithmetic stays lane-local and exactly matches the scalar equations below.
        if (strong)
        {
            Vector128<int> twiceTc = Vector128.Create(2 * tc);
            Vector128<int> four = Vector128.Create(4);
            Vector128<int> two = Vector128.Create(2);
            Vector128<int> filteredP0 = Vector128.Clamp((p2 + (p1 * 2) + (p0 * 2) + (q0 * 2) + q1 + four) >> 3, p0 - twiceTc, p0 + twiceTc);
            Vector128<int> filteredQ0 = Vector128.Clamp((p1 + (p0 * 2) + (q0 * 2) + (q1 * 2) + q2 + four) >> 3, q0 - twiceTc, q0 + twiceTc);
            Vector128<int> filteredP1 = Vector128.Clamp((p2 + p1 + p0 + q0 + two) >> 2, p1 - twiceTc, p1 + twiceTc);
            Vector128<int> filteredQ1 = Vector128.Clamp((p0 + q0 + q1 + q2 + two) >> 2, q1 - twiceTc, q1 + twiceTc);
            Vector128<int> filteredP2 = Vector128.Clamp(((p3 * 2) + (p2 * 3) + p1 + p0 + q0 + four) >> 3, p2 - twiceTc, p2 + twiceTc);
            Vector128<int> filteredQ2 = Vector128.Clamp((p0 + q0 + q1 + (q2 * 3) + (q3 * 2) + four) >> 3, q2 - twiceTc, q2 + twiceTc);

            TOperator.StoreVector(picture, plane, x, y, -3, partPNoFilter ? p2 : filteredP2, 4);
            TOperator.StoreVector(picture, plane, x, y, -2, partPNoFilter ? p1 : filteredP1, 4);
            TOperator.StoreVector(picture, plane, x, y, -1, partPNoFilter ? p0 : filteredP0, 4);
            TOperator.StoreVector(picture, plane, x, y, 0, partQNoFilter ? q0 : filteredQ0, 4);
            TOperator.StoreVector(picture, plane, x, y, 1, partQNoFilter ? q1 : filteredQ1, 4);
            TOperator.StoreVector(picture, plane, x, y, 2, partQNoFilter ? q2 : filteredQ2, 4);
            return;
        }

        Vector128<int> primaryDifference = (q0 - p0) * 9;
        Vector128<int> secondaryDifference = (q1 - p1) * 3;
        Vector128<int> delta = (primaryDifference - secondaryDifference + Vector128.Create(8)) >> 4;
        Vector128<int> filterMask = Vector128.LessThan(Vector128.Abs(delta), Vector128.Create(tc * 10));
        delta = Vector128.Clamp(delta, Vector128.Create(-tc), Vector128.Create(tc));
        Vector128<int> minimum = Vector128<int>.Zero;
        Vector128<int> maximum = Vector128.Create((1 << bitDepth) - 1);
        Vector128<int> filteredP0Weak = Vector128.ConditionalSelect(filterMask, Vector128.Clamp(p0 + delta, minimum, maximum), p0);
        Vector128<int> filteredQ0Weak = Vector128.ConditionalSelect(filterMask, Vector128.Clamp(q0 - delta, minimum, maximum), q0);
        TOperator.StoreVector(picture, plane, x, y, -1, partPNoFilter ? p0 : filteredP0Weak, 4);
        TOperator.StoreVector(picture, plane, x, y, 0, partQNoFilter ? q0 : filteredQ0Weak, 4);

        int halfTc = tc >> 1;
        if (filterSecondP && !partPNoFilter)
        {
            Vector128<int> secondary = (((p2 + p0 + Vector128<int>.One) >> 1) - p1 + delta) >> 1;
            secondary = Vector128.Clamp(secondary, Vector128.Create(-halfTc), Vector128.Create(halfTc));
            Vector128<int> filtered = Vector128.ConditionalSelect(filterMask, Vector128.Clamp(p1 + secondary, minimum, maximum), p1);
            TOperator.StoreVector(picture, plane, x, y, -2, filtered, 4);
        }

        if (filterSecondQ && !partQNoFilter)
        {
            Vector128<int> secondary = (((q2 + q0 + Vector128<int>.One) >> 1) - q1 - delta) >> 1;
            secondary = Vector128.Clamp(secondary, Vector128.Create(-halfTc), Vector128.Create(halfTc));
            Vector128<int> filtered = Vector128.ConditionalSelect(filterMask, Vector128.Clamp(q1 + secondary, minimum, maximum), q1);
            TOperator.StoreVector(picture, plane, x, y, 1, filtered, 4);
        }
    }

    /// <summary>
    /// Applies the chroma kernel through one closed edge-orientation operator.
    /// </summary>
    /// <typeparam name="TOperator">The vertical or horizontal sample-access operator.</typeparam>
    /// <param name="picture">The reconstructed picture.</param>
    /// <param name="plane">The Cb or Cr component plane.</param>
    /// <param name="x">The first Q-side sample X coordinate.</param>
    /// <param name="y">The first Q-side sample Y coordinate.</param>
    /// <param name="tc">The scaled clipping threshold.</param>
    /// <param name="partPNoFilter">Whether the P-side block retains its original samples.</param>
    /// <param name="partQNoFilter">Whether the Q-side block retains its original samples.</param>
    /// <param name="bitDepth">The component sample precision.</param>
    /// <param name="count">The number of samples in the edge segment.</param>
    private static void FilterChroma<TOperator>(
        HevcPictureBuffer picture,
        HevcPlane plane,
        int x,
        int y,
        int tc,
        bool partPNoFilter,
        bool partQNoFilter,
        int bitDepth,
        int count)
        where TOperator : struct, IEdgeOperator
    {
        if (tc == 0)
        {
            return;
        }

        if (!Vector128.IsHardwareAccelerated)
        {
            int maximum = (1 << bitDepth) - 1;
            for (int index = 0; index < count; index++)
            {
                int p1 = TOperator.LoadScalar(picture, plane, x, y, -2, index);
                int p0 = TOperator.LoadScalar(picture, plane, x, y, -1, index);
                int q0 = TOperator.LoadScalar(picture, plane, x, y, 0, index);
                int q1 = TOperator.LoadScalar(picture, plane, x, y, 1, index);
                int delta = Math.Clamp((((q0 - p0) << 2) + p1 - q1 + 4) >> 3, -tc, tc);
                if (!partPNoFilter)
                {
                    TOperator.StoreScalar(picture, plane, x, y, -1, index, Math.Clamp(p0 + delta, 0, maximum));
                }

                if (!partQNoFilter)
                {
                    TOperator.StoreScalar(picture, plane, x, y, 0, index, Math.Clamp(q0 - delta, 0, maximum));
                }
            }

            return;
        }

        Vector128<int> p1Vector = TOperator.LoadVector(picture, plane, x, y, -2);
        Vector128<int> p0Vector = TOperator.LoadVector(picture, plane, x, y, -1);
        Vector128<int> q0Vector = TOperator.LoadVector(picture, plane, x, y, 0);
        Vector128<int> q1Vector = TOperator.LoadVector(picture, plane, x, y, 1);
        Vector128<int> deltaVector = (((q0Vector - p0Vector) * 4) + p1Vector - q1Vector + Vector128.Create(4)) >> 3;
        deltaVector = Vector128.Clamp(deltaVector, Vector128.Create(-tc), Vector128.Create(tc));
        Vector128<int> minimum = Vector128<int>.Zero;
        Vector128<int> maximumVector = Vector128.Create((1 << bitDepth) - 1);

        if (!partPNoFilter)
        {
            TOperator.StoreVector(picture, plane, x, y, -1, Vector128.Clamp(p0Vector + deltaVector, minimum, maximumVector), count);
        }

        if (!partQNoFilter)
        {
            TOperator.StoreVector(picture, plane, x, y, 0, Vector128.Clamp(q0Vector - deltaVector, minimum, maximumVector), count);
        }
    }

    /// <summary>
    /// Applies the scalar luma equations to one sample along an edge.
    /// </summary>
    /// <typeparam name="TOperator">The vertical or horizontal sample-access operator.</typeparam>
    /// <param name="picture">The reconstructed picture.</param>
    /// <param name="plane">The component plane.</param>
    /// <param name="x">The first Q-side sample X coordinate.</param>
    /// <param name="y">The first Q-side sample Y coordinate.</param>
    /// <param name="index">The sample offset along the edge.</param>
    /// <param name="tc">The scaled clipping threshold.</param>
    /// <param name="strong">Whether the strong six-sample filter is selected.</param>
    /// <param name="partPNoFilter">Whether the P-side block retains its original samples.</param>
    /// <param name="partQNoFilter">Whether the Q-side block retains its original samples.</param>
    /// <param name="thresholdCut">The weak-filter delta threshold.</param>
    /// <param name="filterSecondP">Whether the second P-side sample is filtered.</param>
    /// <param name="filterSecondQ">Whether the second Q-side sample is filtered.</param>
    /// <param name="bitDepth">The component sample precision.</param>
    private static void FilterLumaScalar<TOperator>(
        HevcPictureBuffer picture,
        HevcPlane plane,
        int x,
        int y,
        int index,
        int tc,
        bool strong,
        bool partPNoFilter,
        bool partQNoFilter,
        int thresholdCut,
        bool filterSecondP,
        bool filterSecondQ,
        int bitDepth)
        where TOperator : struct, IEdgeOperator
    {
        int p3 = TOperator.LoadScalar(picture, plane, x, y, -4, index);
        int p2 = TOperator.LoadScalar(picture, plane, x, y, -3, index);
        int p1 = TOperator.LoadScalar(picture, plane, x, y, -2, index);
        int p0 = TOperator.LoadScalar(picture, plane, x, y, -1, index);
        int q0 = TOperator.LoadScalar(picture, plane, x, y, 0, index);
        int q1 = TOperator.LoadScalar(picture, plane, x, y, 1, index);
        int q2 = TOperator.LoadScalar(picture, plane, x, y, 2, index);
        int q3 = TOperator.LoadScalar(picture, plane, x, y, 3, index);
        if (strong)
        {
            if (!partPNoFilter)
            {
                int filteredP0 = Math.Clamp(
                    (p2 + (2 * p1) + (2 * p0) + (2 * q0) + q1 + 4) >> 3,
                    p0 - (2 * tc),
                    p0 + (2 * tc));

                TOperator.StoreScalar(picture, plane, x, y, -1, index, filteredP0);
                TOperator.StoreScalar(picture, plane, x, y, -2, index, Math.Clamp((p2 + p1 + p0 + q0 + 2) >> 2, p1 - (2 * tc), p1 + (2 * tc)));
                TOperator.StoreScalar(picture, plane, x, y, -3, index, Math.Clamp(((2 * p3) + (3 * p2) + p1 + p0 + q0 + 4) >> 3, p2 - (2 * tc), p2 + (2 * tc)));
            }

            if (!partQNoFilter)
            {
                int filteredQ0 = Math.Clamp(
                    (p1 + (2 * p0) + (2 * q0) + (2 * q1) + q2 + 4) >> 3,
                    q0 - (2 * tc),
                    q0 + (2 * tc));

                TOperator.StoreScalar(picture, plane, x, y, 0, index, filteredQ0);
                TOperator.StoreScalar(picture, plane, x, y, 1, index, Math.Clamp((p0 + q0 + q1 + q2 + 2) >> 2, q1 - (2 * tc), q1 + (2 * tc)));
                TOperator.StoreScalar(picture, plane, x, y, 2, index, Math.Clamp((p0 + q0 + q1 + (3 * q2) + (2 * q3) + 4) >> 3, q2 - (2 * tc), q2 + (2 * tc)));
            }

            return;
        }

        int delta = ((9 * (q0 - p0)) - (3 * (q1 - p1)) + 8) >> 4;
        if (Math.Abs(delta) >= thresholdCut)
        {
            return;
        }

        delta = Math.Clamp(delta, -tc, tc);
        int maximum = (1 << bitDepth) - 1;
        if (!partPNoFilter)
        {
            TOperator.StoreScalar(picture, plane, x, y, -1, index, Math.Clamp(p0 + delta, 0, maximum));
            if (filterSecondP)
            {
                int secondary = (((p2 + p0 + 1) >> 1) - p1 + delta) >> 1;
                secondary = Math.Clamp(secondary, -(tc >> 1), tc >> 1);
                TOperator.StoreScalar(picture, plane, x, y, -2, index, Math.Clamp(p1 + secondary, 0, maximum));
            }
        }

        if (!partQNoFilter)
        {
            TOperator.StoreScalar(picture, plane, x, y, 0, index, Math.Clamp(q0 - delta, 0, maximum));
            if (filterSecondQ)
            {
                int secondary = (((q2 + q0 + 1) >> 1) - q1 - delta) >> 1;
                secondary = Math.Clamp(secondary, -(tc >> 1), tc >> 1);
                TOperator.StoreScalar(picture, plane, x, y, 1, index, Math.Clamp(q1 + secondary, 0, maximum));
            }
        }
    }

    /// <summary>
    /// Determines whether one endpoint satisfies the strong-filter conditions.
    /// </summary>
    /// <typeparam name="TOperator">The vertical or horizontal sample-access operator.</typeparam>
    /// <param name="picture">The reconstructed picture.</param>
    /// <param name="plane">The component plane.</param>
    /// <param name="x">The first Q-side sample X coordinate.</param>
    /// <param name="y">The first Q-side sample Y coordinate.</param>
    /// <param name="index">The endpoint offset along the edge.</param>
    /// <param name="discontinuity">Twice the endpoint's second-derivative sum.</param>
    /// <param name="beta">The scaled discontinuity threshold.</param>
    /// <param name="tc">The scaled clipping threshold.</param>
    /// <returns><see langword="true"/> when strong filtering is permitted; otherwise, <see langword="false"/>.</returns>
    private static bool UsesStrongFiltering<TOperator>(
        HevcPictureBuffer picture,
        HevcPlane plane,
        int x,
        int y,
        int index,
        int discontinuity,
        int beta,
        int tc)
        where TOperator : struct, IEdgeOperator
    {
        int p3 = TOperator.LoadScalar(picture, plane, x, y, -4, index);
        int p0 = TOperator.LoadScalar(picture, plane, x, y, -1, index);
        int q0 = TOperator.LoadScalar(picture, plane, x, y, 0, index);
        int q3 = TOperator.LoadScalar(picture, plane, x, y, 3, index);
        int strongDiscontinuity = Math.Abs(p3 - p0) + Math.Abs(q3 - q0);
        int strongThreshold = ((5 * tc) + 1) >> 1;
        return strongDiscontinuity < (beta >> 3)
            && discontinuity < (beta >> 2)
            && Math.Abs(p0 - q0) < strongThreshold;
    }

    /// <summary>
    /// Accesses four rows across a vertical edge.
    /// </summary>
    private readonly struct VerticalEdgeOperator : IEdgeOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> LoadVector(HevcPictureBuffer picture, HevcPlane plane, int x, int y, int distance)
            => Vector128.Create(
                (int)picture.GetRowSpan(plane, y)[x + distance],
                picture.GetRowSpan(plane, y + 1)[x + distance],
                picture.GetRowSpan(plane, y + 2)[x + distance],
                picture.GetRowSpan(plane, y + 3)[x + distance]);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreVector(
            HevcPictureBuffer picture,
            HevcPlane plane,
            int x,
            int y,
            int distance,
            Vector128<int> value,
            int count)
        {
            for (int index = 0; index < count; index++)
            {
                picture.GetRowSpan(plane, y + index)[x + distance] = (ushort)value.GetElement(index);
            }
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LoadScalar(HevcPictureBuffer picture, HevcPlane plane, int x, int y, int distance, int index)
            => picture.GetRowSpan(plane, y + index)[x + distance];

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreScalar(HevcPictureBuffer picture, HevcPlane plane, int x, int y, int distance, int index, int value)
            => picture.GetRowSpan(plane, y + index)[x + distance] = (ushort)value;
    }

    /// <summary>
    /// Accesses four columns across a horizontal edge.
    /// </summary>
    private readonly struct HorizontalEdgeOperator : IEdgeOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> LoadVector(HevcPictureBuffer picture, HevcPlane plane, int x, int y, int distance)
        {
            ref ushort source = ref picture.GetRowSpan(plane, y + distance)[x];
            Vector64<ushort> packed = Unsafe.As<ushort, Vector64<ushort>>(ref source);
            return Vector128.WidenLower(Vector128.Create(packed, Vector64<ushort>.Zero)).AsInt32();
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreVector(
            HevcPictureBuffer picture,
            HevcPlane plane,
            int x,
            int y,
            int distance,
            Vector128<int> value,
            int count)
        {
            ref ushort destination = ref picture.GetRowSpan(plane, y + distance)[x];
            if (count == 4)
            {
                Vector64<ushort> packed = Vector128.Narrow(value, Vector128<int>.Zero).AsUInt16().GetLower();
                Unsafe.As<ushort, Vector64<ushort>>(ref destination) = packed;
                return;
            }

            destination = (ushort)value.GetElement(0);
            Unsafe.Add(ref destination, 1) = (ushort)value.GetElement(1);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LoadScalar(HevcPictureBuffer picture, HevcPlane plane, int x, int y, int distance, int index)
            => picture.GetRowSpan(plane, y + distance)[x + index];

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreScalar(HevcPictureBuffer picture, HevcPlane plane, int x, int y, int distance, int index, int value)
            => picture.GetRowSpan(plane, y + distance)[x + index] = (ushort)value;
    }
}
