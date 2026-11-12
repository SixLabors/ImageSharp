// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.VarDct;

internal sealed partial class JxlVarDctDecoder
{
    /// <summary>
    /// Smooths completed DC planes in place before any AC transform reads them. DC context indices remain unchanged.
    /// </summary>
    /// <param name="allocator">The tracked allocator for the reusable original-row rings.</param>
    /// <param name="cancellationToken">The token checked between DC rows.</param>
    public void SmoothDc(MemoryAllocator allocator, CancellationToken cancellationToken)
    {
        // The native AdaptiveDCSmoothing boundary uses the sampling-padded DC dimensions. A dimension of two or less has no interior.
        // The caller has already selected VarDCT frames without SkipAdaptiveDCSmoothing or UseDcFrame.
        if (this.dcSize.Width <= 2 || this.dcSize.Height <= 2)
        {
            return;
        }

        // One allocator-owned buffer holds three original rows per coupled component. Subsampled components are independent,
        // so they reuse a single three-row ring. Buffer2D retains the allocator's segmented-buffer support for wide images.
        using Buffer2D<float> storage = allocator.Allocate2D<float>(this.dcSize.Width, this.is444 ? 9 : 3);
        if (this.is444)
        {
            this.SmoothDc444(storage, cancellationToken);
        }
        else
        {
            this.SmoothDcSubsampled(storage, cancellationToken);
        }
    }

    /// <summary>
    /// Smooths equal-resolution X, Y and B planes with one strength shared by all three components at each position.
    /// </summary>
    /// <param name="storage">Nine reusable DC-width rows.</param>
    /// <param name="cancellationToken">The token checked between rows.</param>
    private void SmoothDc444(Buffer2D<float> storage, CancellationToken cancellationToken)
    {
        Buffer2DRegion<float> x = this.GetDcChannel(0);
        Buffer2DRegion<float> y = this.GetDcChannel(1);
        Buffer2DRegion<float> b = this.GetDcChannel(2);
        DcRowRing rowsX = new(storage, 0, x);
        DcRowRing rowsY = new(storage, 3, y);
        DcRowRing rowsB = new(storage, 6, b);
        DcSmoothingOperator operation = new(this.DcMultipliers);
        for (int row = 1; row < this.dcSize.Height - 1; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SmoothDcRow(in rowsX, in rowsY, in rowsB, x.DangerousGetRowSpan(row), y.DangerousGetRowSpan(row), b.DangerousGetRowSpan(row), operation);

            // The next entering row has not been written. Rotate span references, then copy only that original row into the released slot.
            // The row just written remains available in its ring, so later pixels never use already-smoothed neighbors.
            if (row + 2 < this.dcSize.Height)
            {
                rowsX.Advance(x.DangerousGetRowSpan(row + 2));
                rowsY.Advance(y.DangerousGetRowSpan(row + 2));
                rowsB.Advance(b.DangerousGetRowSpan(row + 2));
            }
        }
    }

    /// <summary>
    /// Smooths each reduced component independently, with the native floor-sized sampling-padded DC extent.
    /// </summary>
    /// <param name="storage">Three reusable full-grid-width rows.</param>
    /// <param name="cancellationToken">The token checked between rows.</param>
    private void SmoothDcSubsampled(Buffer2D<float> storage, CancellationToken cancellationToken)
    {
        DcSmoothingOperator operation = new(this.DcMultipliers);
        for (int component = 0; component < 3; component++)
        {
            Buffer2DRegion<float> plane = this.GetDcChannel(component);
            if (plane.Width <= 2 || plane.Height <= 2)
            {
                continue;
            }

            DcRowRing rows = new(storage, 0, plane);
            for (int row = 1; row < plane.Height - 1; row++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                SmoothDcRow(in rows, plane.DangerousGetRowSpan(row), operation, component);
                if (row + 2 < plane.Height)
                {
                    rows.Advance(plane.DangerousGetRowSpan(row + 2));
                }
            }
        }
    }

    /// <summary>
    /// Reconstructs interior columns of three coupled rows. Borders retain their original samples.
    /// </summary>
    /// <param name="rowsX">The three original X rows.</param>
    /// <param name="rowsY">The three original Y rows.</param>
    /// <param name="rowsB">The three original B rows.</param>
    /// <param name="outputX">The X row to update.</param>
    /// <param name="outputY">The Y row to update.</param>
    /// <param name="outputB">The B row to update.</param>
    /// <param name="operation">The prepared DC-factor arithmetic.</param>
    private static void SmoothDcRow(
        in DcRowRing rowsX,
        in DcRowRing rowsY,
        in DcRowRing rowsB,
        Span<float> outputX,
        Span<float> outputY,
        Span<float> outputB,
        DcSmoothingOperator operation)
    {
        int x = 1;
        int end = outputX.Length - 1;
        ref float targetX = ref MemoryMarshal.GetReference(outputX);
        ref float targetY = ref MemoryMarshal.GetReference(outputY);
        ref float targetB = ref MemoryMarshal.GetReference(outputB);

        // Each lane is one interior column. Neighbor loads can be unaligned; stop before the final border so every right neighbor exists.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = (end - x).Vector512Count<float>();
            for (nuint block = 0; block < count; block++, x += Vector512<float>.Count)
            {
                operation.Invoke(in rowsX, in rowsY, in rowsB, x, out Vector512<float> a, out Vector512<float> c, out Vector512<float> d);
                a.StoreUnsafe(ref targetX, (nuint)x);
                c.StoreUnsafe(ref targetY, (nuint)x);
                d.StoreUnsafe(ref targetB, (nuint)x);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (end - x).Vector256Count<float>();
            for (nuint block = 0; block < count; block++, x += Vector256<float>.Count)
            {
                operation.Invoke(in rowsX, in rowsY, in rowsB, x, out Vector256<float> a, out Vector256<float> c, out Vector256<float> d);
                a.StoreUnsafe(ref targetX, (nuint)x);
                c.StoreUnsafe(ref targetY, (nuint)x);
                d.StoreUnsafe(ref targetB, (nuint)x);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (end - x).Vector128Count<float>();
            for (nuint block = 0; block < count; block++, x += Vector128<float>.Count)
            {
                operation.Invoke(in rowsX, in rowsY, in rowsB, x, out Vector128<float> a, out Vector128<float> c, out Vector128<float> d);
                a.StoreUnsafe(ref targetX, (nuint)x);
                c.StoreUnsafe(ref targetY, (nuint)x);
                d.StoreUnsafe(ref targetB, (nuint)x);
            }
        }

        for (; x < end; x++)
        {
            operation.Invoke(in rowsX, in rowsY, in rowsB, x, out float a, out float c, out float d);
            outputX[x] = a;
            outputY[x] = c;
            outputB[x] = d;
        }
    }

    /// <summary>
    /// Reconstructs interior columns of one independently sampled component row.
    /// </summary>
    /// <param name="rows">The three original component rows.</param>
    /// <param name="output">The row to update.</param>
    /// <param name="operation">The prepared DC factors.</param>
    /// <param name="component">The component whose DC factor applies.</param>
    private static void SmoothDcRow(in DcRowRing rows, Span<float> output, DcSmoothingOperator operation, int component)
    {
        int x = 1;
        int end = output.Length - 1;
        ref float target = ref MemoryMarshal.GetReference(output);
        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = (end - x).Vector512Count<float>();
            for (nuint block = 0; block < count; block++, x += Vector512<float>.Count)
            {
                operation.Invoke(in rows, x, component, out Vector512<float> value);
                value.StoreUnsafe(ref target, (nuint)x);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (end - x).Vector256Count<float>();
            for (nuint block = 0; block < count; block++, x += Vector256<float>.Count)
            {
                operation.Invoke(in rows, x, component, out Vector256<float> value);
                value.StoreUnsafe(ref target, (nuint)x);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (end - x).Vector128Count<float>();
            for (nuint block = 0; block < count; block++, x += Vector128<float>.Count)
            {
                operation.Invoke(in rows, x, component, out Vector128<float> value);
                value.StoreUnsafe(ref target, (nuint)x);
            }
        }

        for (; x < end; x++)
        {
            operation.Invoke(in rows, x, component, out float value);
            output[x] = value;
        }
    }

    /// <summary>
    /// Retains three original rows while the destination plane is updated in place.
    /// </summary>
    private ref struct DcRowRing
    {
        private Span<float> top;
        private Span<float> current;
        private Span<float> bottom;

        /// <summary>
        /// Initializes a new instance of the <see cref="DcRowRing"/> struct and caches all three backing spans before traversal.
        /// </summary>
        /// <param name="storage">The allocator-owned row storage.</param>
        /// <param name="firstRow">The first of three reserved storage rows.</param>
        /// <param name="source">The original plane, with at least three rows and three columns.</param>
        public DcRowRing(Buffer2D<float> storage, int firstRow, Buffer2DRegion<float> source)
        {
            this.top = storage.DangerousGetRowSpan(firstRow)[..source.Width];
            this.current = storage.DangerousGetRowSpan(firstRow + 1)[..source.Width];
            this.bottom = storage.DangerousGetRowSpan(firstRow + 2)[..source.Width];
            source.DangerousGetRowSpan(0).CopyTo(this.top);
            source.DangerousGetRowSpan(1).CopyTo(this.current);
            source.DangerousGetRowSpan(2).CopyTo(this.bottom);
        }

        /// <summary>
        /// Gets the original row above the output row.
        /// </summary>
        public readonly ReadOnlySpan<float> Top => this.top;

        /// <summary>
        /// Gets the original output row.
        /// </summary>
        public readonly ReadOnlySpan<float> Current => this.current;

        /// <summary>
        /// Gets the original row below the output row.
        /// </summary>
        public readonly ReadOnlySpan<float> Bottom => this.bottom;

        /// <summary>
        /// Rotates the borrowed spans and copies one untouched source row into the released slot.
        /// </summary>
        /// <param name="entering">The next original row, with the ring's component width.</param>
        public void Advance(ReadOnlySpan<float> entering)
        {
            Span<float> released = this.top;
            this.top = this.current;
            this.current = this.bottom;
            this.bottom = released;
            entering.CopyTo(this.bottom);
        }
    }

    /// <summary>
    /// Defines the native three-by-three smoothing stencil and quantization-limited interpolation.
    /// </summary>
    private readonly struct DcSmoothingOperator
    {
        // libjxl compressed_dc.cc, AdaptiveDCSmoothing, defines these axial and diagonal weights.
        // Four neighbors use each weight. The remaining center weight makes a constant field unchanged.
        private const float SideWeight = 0.20345139757231578F;
        private const float CornerWeight = 0.0334829185968739F;
        private const float CenterWeight = 1F - (4F * (SideWeight + CornerWeight));
        private readonly Vector3 dcFactors;

        /// <summary>
        /// Initializes a new instance of the <see cref="DcSmoothingOperator"/> struct.
        /// </summary>
        /// <param name="dcFactors">The baseline dequantization steps in X, Y, B order, without per-group extra precision.</param>
        public DcSmoothingOperator(Vector3 dcFactors) => this.dcFactors = dcFactors;

        /// <summary>
        /// Smooths one coupled X/Y/B position.
        /// </summary>
        /// <param name="rowsX">The original X neighborhood rows.</param>
        /// <param name="rowsY">The original Y neighborhood rows.</param>
        /// <param name="rowsB">The original B neighborhood rows.</param>
        /// <param name="column">The first interior column.</param>
        /// <param name="x">The smoothed X values.</param>
        /// <param name="y">The smoothed Y values.</param>
        /// <param name="b">The smoothed B values.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(
            in DcRowRing rowsX,
            in DcRowRing rowsY,
            in DcRowRing rowsB,
            int column,
            out float x,
            out float y,
            out float b)
        {
            float gap = 0.5F;
            Compute(in rowsX, column, this.dcFactors.X, ref gap, out float centerX, out float smoothedX);
            Compute(in rowsY, column, this.dcFactors.Y, ref gap, out float centerY, out float smoothedY);
            Compute(in rowsB, column, this.dcFactors.Z, ref gap, out float centerB, out float smoothedB);

            // A discrepancy of at most half a DC step permits full smoothing. At three-quarters of a step, smoothing stops.
            // Taking the largest component discrepancy gives all three components the same strength in 4:4:4 images.
            float strength = MathF.Max(float.MultiplyAddEstimate(-4F, gap, 3F), 0F);
            x = float.MultiplyAddEstimate(smoothedX - centerX, strength, centerX);
            y = float.MultiplyAddEstimate(smoothedY - centerY, strength, centerY);
            b = float.MultiplyAddEstimate(smoothedB - centerB, strength, centerB);
        }

        /// <summary>
        /// Smooths one independently sampled component position.
        /// </summary>
        /// <param name="rows">The original neighborhood rows.</param>
        /// <param name="column">The first interior column.</param>
        /// <param name="component">The component whose baseline DC step applies.</param>
        /// <param name="value">The smoothed values.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(in DcRowRing rows, int column, int component, out float value)
        {
            float gap = 0.5F;
            Compute(in rows, column, this.dcFactors[component], ref gap, out float center, out float smoothed);
            float strength = MathF.Max(float.MultiplyAddEstimate(-4F, gap, 3F), 0F);
            value = float.MultiplyAddEstimate(smoothed - center, strength, center);
        }

        /// <summary>
        /// Computes one weighted neighborhood and updates its largest normalized discrepancy.
        /// </summary>
        /// <param name="rows">The three original rows, with a valid neighbor on either side of every lane.</param>
        /// <param name="column">The first interior column.</param>
        /// <param name="dcFactor">The component's positive baseline DC step.</param>
        /// <param name="gap">The largest discrepancy seen across the participating components, initially one-half.</param>
        /// <param name="center">The original center values.</param>
        /// <param name="smoothed">The weighted neighborhood values.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Compute(
            in DcRowRing rows,
            int column,
            float dcFactor,
            ref float gap,
            out float center,
            out float smoothed)
        {
            ReadOnlySpan<float> top = rows.Top;
            ReadOnlySpan<float> current = rows.Current;
            ReadOnlySpan<float> bottom = rows.Bottom;
            center = current[column];
            float corner = (top[column - 1] + top[column + 1]) + (bottom[column - 1] + bottom[column + 1]);
            float side = (current[column - 1] + current[column + 1]) + (top[column] + bottom[column]);
            smoothed = float.MultiplyAddEstimate(corner, CornerWeight, float.MultiplyAddEstimate(side, SideWeight, center * CenterWeight));
            gap = MathF.Max(gap, MathF.Abs((center - smoothed) / dcFactor));
        }

        /// <summary>
        /// Smooths 4 coupled X/Y/B positions in unchanged lane order.
        /// </summary>
        /// <param name="rowsX">The original X neighborhood rows.</param>
        /// <param name="rowsY">The original Y neighborhood rows.</param>
        /// <param name="rowsB">The original B neighborhood rows.</param>
        /// <param name="column">The first interior column.</param>
        /// <param name="x">The smoothed X values.</param>
        /// <param name="y">The smoothed Y values.</param>
        /// <param name="b">The smoothed B values.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(
            in DcRowRing rowsX,
            in DcRowRing rowsY,
            in DcRowRing rowsB,
            int column,
            out Vector128<float> x,
            out Vector128<float> y,
            out Vector128<float> b)
        {
            Vector128<float> gap = Vector128.Create(0.5F);
            Compute(in rowsX, column, this.dcFactors.X, ref gap, out Vector128<float> centerX, out Vector128<float> smoothedX);
            Compute(in rowsY, column, this.dcFactors.Y, ref gap, out Vector128<float> centerY, out Vector128<float> smoothedY);
            Compute(in rowsB, column, this.dcFactors.Z, ref gap, out Vector128<float> centerB, out Vector128<float> smoothedB);

            // A discrepancy of at most half a DC step permits full smoothing. At three-quarters of a step, smoothing stops.
            // Taking the largest component discrepancy gives all three components the same strength in 4:4:4 images.
            Vector128<float> strength = Vector128.Max(Vector128.MultiplyAddEstimate(Vector128.Create(-4F), gap, Vector128.Create(3F)), Vector128.Create(0F));
            x = Vector128.MultiplyAddEstimate(smoothedX - centerX, strength, centerX);
            y = Vector128.MultiplyAddEstimate(smoothedY - centerY, strength, centerY);
            b = Vector128.MultiplyAddEstimate(smoothedB - centerB, strength, centerB);
        }

        /// <summary>
        /// Smooths 4 independently sampled component positions.
        /// </summary>
        /// <param name="rows">The original neighborhood rows.</param>
        /// <param name="column">The first interior column.</param>
        /// <param name="component">The component whose baseline DC step applies.</param>
        /// <param name="value">The smoothed values.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(in DcRowRing rows, int column, int component, out Vector128<float> value)
        {
            Vector128<float> gap = Vector128.Create(0.5F);
            Compute(in rows, column, this.dcFactors[component], ref gap, out Vector128<float> center, out Vector128<float> smoothed);
            Vector128<float> strength = Vector128.Max(Vector128.MultiplyAddEstimate(Vector128.Create(-4F), gap, Vector128.Create(3F)), Vector128.Create(0F));
            value = Vector128.MultiplyAddEstimate(smoothed - center, strength, center);
        }

        /// <summary>
        /// Computes 4 weighted neighborhood values and updates their largest normalized discrepancy.
        /// </summary>
        /// <param name="rows">The three original rows, with a valid neighbor on either side of every lane.</param>
        /// <param name="column">The first interior column.</param>
        /// <param name="dcFactor">The component's positive baseline DC step.</param>
        /// <param name="gap">The largest discrepancy seen across the participating components, initially one-half.</param>
        /// <param name="center">The original center values.</param>
        /// <param name="smoothed">The weighted neighborhood values.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Compute(
            in DcRowRing rows,
            int column,
            float dcFactor,
            ref Vector128<float> gap,
            out Vector128<float> center,
            out Vector128<float> smoothed)
        {
            ref float top = ref MemoryMarshal.GetReference(rows.Top);
            ref float current = ref MemoryMarshal.GetReference(rows.Current);
            ref float bottom = ref MemoryMarshal.GetReference(rows.Bottom);
            nuint left = (nuint)(column - 1);
            nuint middle = (nuint)column;
            nuint right = (nuint)(column + 1);
            center = Vector128.LoadUnsafe(ref current, middle);
            Vector128<float> corner = (Vector128.LoadUnsafe(ref top, left) + Vector128.LoadUnsafe(ref top, right))
                + (Vector128.LoadUnsafe(ref bottom, left) + Vector128.LoadUnsafe(ref bottom, right));

            Vector128<float> side = (Vector128.LoadUnsafe(ref current, left) + Vector128.LoadUnsafe(ref current, right))
                + (Vector128.LoadUnsafe(ref top, middle) + Vector128.LoadUnsafe(ref bottom, middle));

            smoothed = Vector128.MultiplyAddEstimate(
                corner,
                Vector128.Create(CornerWeight),
                Vector128.MultiplyAddEstimate(side, Vector128.Create(SideWeight), center * Vector128.Create(CenterWeight)));

            gap = Vector128.Max(gap, Vector128.Abs((center - smoothed) / Vector128.Create(dcFactor)));
        }

        /// <summary>
        /// Smooths 8 coupled X/Y/B positions in unchanged lane order.
        /// </summary>
        /// <param name="rowsX">The original X neighborhood rows.</param>
        /// <param name="rowsY">The original Y neighborhood rows.</param>
        /// <param name="rowsB">The original B neighborhood rows.</param>
        /// <param name="column">The first interior column.</param>
        /// <param name="x">The smoothed X values.</param>
        /// <param name="y">The smoothed Y values.</param>
        /// <param name="b">The smoothed B values.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(
            in DcRowRing rowsX,
            in DcRowRing rowsY,
            in DcRowRing rowsB,
            int column,
            out Vector256<float> x,
            out Vector256<float> y,
            out Vector256<float> b)
        {
            Vector256<float> gap = Vector256.Create(0.5F);
            Compute(in rowsX, column, this.dcFactors.X, ref gap, out Vector256<float> centerX, out Vector256<float> smoothedX);
            Compute(in rowsY, column, this.dcFactors.Y, ref gap, out Vector256<float> centerY, out Vector256<float> smoothedY);
            Compute(in rowsB, column, this.dcFactors.Z, ref gap, out Vector256<float> centerB, out Vector256<float> smoothedB);

            // A discrepancy of at most half a DC step permits full smoothing. At three-quarters of a step, smoothing stops.
            // Taking the largest component discrepancy gives all three components the same strength in 4:4:4 images.
            Vector256<float> strength = Vector256.Max(Vector256.MultiplyAddEstimate(Vector256.Create(-4F), gap, Vector256.Create(3F)), Vector256.Create(0F));
            x = Vector256.MultiplyAddEstimate(smoothedX - centerX, strength, centerX);
            y = Vector256.MultiplyAddEstimate(smoothedY - centerY, strength, centerY);
            b = Vector256.MultiplyAddEstimate(smoothedB - centerB, strength, centerB);
        }

        /// <summary>
        /// Smooths 8 independently sampled component positions.
        /// </summary>
        /// <param name="rows">The original neighborhood rows.</param>
        /// <param name="column">The first interior column.</param>
        /// <param name="component">The component whose baseline DC step applies.</param>
        /// <param name="value">The smoothed values.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(in DcRowRing rows, int column, int component, out Vector256<float> value)
        {
            Vector256<float> gap = Vector256.Create(0.5F);
            Compute(in rows, column, this.dcFactors[component], ref gap, out Vector256<float> center, out Vector256<float> smoothed);
            Vector256<float> strength = Vector256.Max(Vector256.MultiplyAddEstimate(Vector256.Create(-4F), gap, Vector256.Create(3F)), Vector256.Create(0F));
            value = Vector256.MultiplyAddEstimate(smoothed - center, strength, center);
        }

        /// <summary>
        /// Computes 8 weighted neighborhood values and updates their largest normalized discrepancy.
        /// </summary>
        /// <param name="rows">The three original rows, with a valid neighbor on either side of every lane.</param>
        /// <param name="column">The first interior column.</param>
        /// <param name="dcFactor">The component's positive baseline DC step.</param>
        /// <param name="gap">The largest discrepancy seen across the participating components, initially one-half.</param>
        /// <param name="center">The original center values.</param>
        /// <param name="smoothed">The weighted neighborhood values.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Compute(
            in DcRowRing rows,
            int column,
            float dcFactor,
            ref Vector256<float> gap,
            out Vector256<float> center,
            out Vector256<float> smoothed)
        {
            ref float top = ref MemoryMarshal.GetReference(rows.Top);
            ref float current = ref MemoryMarshal.GetReference(rows.Current);
            ref float bottom = ref MemoryMarshal.GetReference(rows.Bottom);
            nuint left = (nuint)(column - 1);
            nuint middle = (nuint)column;
            nuint right = (nuint)(column + 1);
            center = Vector256.LoadUnsafe(ref current, middle);
            Vector256<float> corner = (Vector256.LoadUnsafe(ref top, left) + Vector256.LoadUnsafe(ref top, right))
                + (Vector256.LoadUnsafe(ref bottom, left) + Vector256.LoadUnsafe(ref bottom, right));

            Vector256<float> side = (Vector256.LoadUnsafe(ref current, left) + Vector256.LoadUnsafe(ref current, right))
                + (Vector256.LoadUnsafe(ref top, middle) + Vector256.LoadUnsafe(ref bottom, middle));

            smoothed = Vector256.MultiplyAddEstimate(
                corner,
                Vector256.Create(CornerWeight),
                Vector256.MultiplyAddEstimate(side, Vector256.Create(SideWeight), center * Vector256.Create(CenterWeight)));

            gap = Vector256.Max(gap, Vector256.Abs((center - smoothed) / Vector256.Create(dcFactor)));
        }

        /// <summary>
        /// Smooths 16 coupled X/Y/B positions in unchanged lane order.
        /// </summary>
        /// <param name="rowsX">The original X neighborhood rows.</param>
        /// <param name="rowsY">The original Y neighborhood rows.</param>
        /// <param name="rowsB">The original B neighborhood rows.</param>
        /// <param name="column">The first interior column.</param>
        /// <param name="x">The smoothed X values.</param>
        /// <param name="y">The smoothed Y values.</param>
        /// <param name="b">The smoothed B values.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(
            in DcRowRing rowsX,
            in DcRowRing rowsY,
            in DcRowRing rowsB,
            int column,
            out Vector512<float> x,
            out Vector512<float> y,
            out Vector512<float> b)
        {
            Vector512<float> gap = Vector512.Create(0.5F);
            Compute(in rowsX, column, this.dcFactors.X, ref gap, out Vector512<float> centerX, out Vector512<float> smoothedX);
            Compute(in rowsY, column, this.dcFactors.Y, ref gap, out Vector512<float> centerY, out Vector512<float> smoothedY);
            Compute(in rowsB, column, this.dcFactors.Z, ref gap, out Vector512<float> centerB, out Vector512<float> smoothedB);

            // A discrepancy of at most half a DC step permits full smoothing. At three-quarters of a step, smoothing stops.
            // Taking the largest component discrepancy gives all three components the same strength in 4:4:4 images.
            Vector512<float> strength = Vector512.Max(Vector512.MultiplyAddEstimate(Vector512.Create(-4F), gap, Vector512.Create(3F)), Vector512.Create(0F));
            x = Vector512.MultiplyAddEstimate(smoothedX - centerX, strength, centerX);
            y = Vector512.MultiplyAddEstimate(smoothedY - centerY, strength, centerY);
            b = Vector512.MultiplyAddEstimate(smoothedB - centerB, strength, centerB);
        }

        /// <summary>
        /// Smooths 16 independently sampled component positions.
        /// </summary>
        /// <param name="rows">The original neighborhood rows.</param>
        /// <param name="column">The first interior column.</param>
        /// <param name="component">The component whose baseline DC step applies.</param>
        /// <param name="value">The smoothed values.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(in DcRowRing rows, int column, int component, out Vector512<float> value)
        {
            Vector512<float> gap = Vector512.Create(0.5F);
            Compute(in rows, column, this.dcFactors[component], ref gap, out Vector512<float> center, out Vector512<float> smoothed);
            Vector512<float> strength = Vector512.Max(Vector512.MultiplyAddEstimate(Vector512.Create(-4F), gap, Vector512.Create(3F)), Vector512.Create(0F));
            value = Vector512.MultiplyAddEstimate(smoothed - center, strength, center);
        }

        /// <summary>
        /// Computes 16 weighted neighborhood values and updates their largest normalized discrepancy.
        /// </summary>
        /// <param name="rows">The three original rows, with a valid neighbor on either side of every lane.</param>
        /// <param name="column">The first interior column.</param>
        /// <param name="dcFactor">The component's positive baseline DC step.</param>
        /// <param name="gap">The largest discrepancy seen across the participating components, initially one-half.</param>
        /// <param name="center">The original center values.</param>
        /// <param name="smoothed">The weighted neighborhood values.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Compute(
            in DcRowRing rows,
            int column,
            float dcFactor,
            ref Vector512<float> gap,
            out Vector512<float> center,
            out Vector512<float> smoothed)
        {
            ref float top = ref MemoryMarshal.GetReference(rows.Top);
            ref float current = ref MemoryMarshal.GetReference(rows.Current);
            ref float bottom = ref MemoryMarshal.GetReference(rows.Bottom);
            nuint left = (nuint)(column - 1);
            nuint middle = (nuint)column;
            nuint right = (nuint)(column + 1);
            center = Vector512.LoadUnsafe(ref current, middle);
            Vector512<float> corner = (Vector512.LoadUnsafe(ref top, left) + Vector512.LoadUnsafe(ref top, right))
                + (Vector512.LoadUnsafe(ref bottom, left) + Vector512.LoadUnsafe(ref bottom, right));

            Vector512<float> side = (Vector512.LoadUnsafe(ref current, left) + Vector512.LoadUnsafe(ref current, right))
                + (Vector512.LoadUnsafe(ref top, middle) + Vector512.LoadUnsafe(ref bottom, middle));

            smoothed = Vector512.MultiplyAddEstimate(
                corner,
                Vector512.Create(CornerWeight),
                Vector512.MultiplyAddEstimate(side, Vector512.Create(SideWeight), center * Vector512.Create(CenterWeight)));

            gap = Vector512.Max(gap, Vector512.Abs((center - smoothed) / Vector512.Create(dcFactor)));
        }
    }
}
