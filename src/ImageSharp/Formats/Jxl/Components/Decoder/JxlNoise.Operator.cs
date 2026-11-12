// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;

internal readonly partial struct JxlNoise
{
    /// <summary>
    /// Convolves the three raw random neighborhoods and adds their correlated contribution to one native color row.
    /// </summary>
    /// <param name="workspace">The borrowed initialized raw tile and border storage.</param>
    /// <param name="rows">Five row offsets for each of the three random channels, in channel-major order.</param>
    /// <param name="x">The native X row segment.</param>
    /// <param name="y">The native Y row segment, disjoint from the other output components.</param>
    /// <param name="b">The native B row segment, disjoint from the other output components.</param>
    /// <param name="operation">The prepared eight-point strength table and base correlation.</param>
    private static void FilterNoiseRow(
        ReadOnlySpan<float> workspace,
        in InlineArray16<int> rows,
        Span<float> x,
        Span<float> y,
        Span<float> b,
        NoiseOperator operation)
    {
        ref float source = ref MemoryMarshal.GetReference(workspace);
        ref float outputX = ref MemoryMarshal.GetReference(x);
        ref float outputY = ref MemoryMarshal.GetReference(y);
        ref float outputB = ref MemoryMarshal.GetReference(b);
        int position = 0;

        // Each float lane computes one independent output pixel. All source rows have two initialized halo
        // samples on each side. Limiting a batch to the visible segment keeps shifted loads inside that row.
        // The scalar remainder uses the same neighbor sum, interpolation and multiply-add grouping.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = (x.Length - position).Vector512Count<float>();
            for (nuint block = 0; block < count; block++, position += Vector512<float>.Count)
            {
                Vector512<float> red = Convolve512(ref source, rows, 0, position + Radius);
                Vector512<float> green = Convolve512(ref source, rows, Diameter, position + Radius);
                Vector512<float> common = Convolve512(ref source, rows, 2 * Diameter, position + Radius);
                Vector512<float> vx = Vector512.LoadUnsafe(ref outputX, (nuint)position);
                Vector512<float> vy = Vector512.LoadUnsafe(ref outputY, (nuint)position);
                Vector512<float> vb = Vector512.LoadUnsafe(ref outputB, (nuint)position);
                operation.Invoke(red, green, common, ref vx, ref vy, ref vb);
                vx.StoreUnsafe(ref outputX, (nuint)position);
                vy.StoreUnsafe(ref outputY, (nuint)position);
                vb.StoreUnsafe(ref outputB, (nuint)position);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (x.Length - position).Vector256Count<float>();
            for (nuint block = 0; block < count; block++, position += Vector256<float>.Count)
            {
                Vector256<float> red = Convolve256(ref source, rows, 0, position + Radius);
                Vector256<float> green = Convolve256(ref source, rows, Diameter, position + Radius);
                Vector256<float> common = Convolve256(ref source, rows, 2 * Diameter, position + Radius);
                Vector256<float> vx = Vector256.LoadUnsafe(ref outputX, (nuint)position);
                Vector256<float> vy = Vector256.LoadUnsafe(ref outputY, (nuint)position);
                Vector256<float> vb = Vector256.LoadUnsafe(ref outputB, (nuint)position);
                operation.Invoke(red, green, common, ref vx, ref vy, ref vb);
                vx.StoreUnsafe(ref outputX, (nuint)position);
                vy.StoreUnsafe(ref outputY, (nuint)position);
                vb.StoreUnsafe(ref outputB, (nuint)position);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (x.Length - position).Vector128Count<float>();
            for (nuint block = 0; block < count; block++, position += Vector128<float>.Count)
            {
                Vector128<float> red = Convolve128(ref source, rows, 0, position + Radius);
                Vector128<float> green = Convolve128(ref source, rows, Diameter, position + Radius);
                Vector128<float> common = Convolve128(ref source, rows, 2 * Diameter, position + Radius);
                Vector128<float> vx = Vector128.LoadUnsafe(ref outputX, (nuint)position);
                Vector128<float> vy = Vector128.LoadUnsafe(ref outputY, (nuint)position);
                Vector128<float> vb = Vector128.LoadUnsafe(ref outputB, (nuint)position);
                operation.Invoke(red, green, common, ref vx, ref vy, ref vb);
                vx.StoreUnsafe(ref outputX, (nuint)position);
                vy.StoreUnsafe(ref outputY, (nuint)position);
                vb.StoreUnsafe(ref outputB, (nuint)position);
            }
        }

        for (; position < x.Length; position++)
        {
            float red = ConvolveScalar(ref source, rows, 0, position + Radius);
            float green = ConvolveScalar(ref source, rows, Diameter, position + Radius);
            float common = ConvolveScalar(ref source, rows, 2 * Diameter, position + Radius);
            operation.Invoke(
                red,
                green,
                common,
                ref Unsafe.Add(ref outputX, position),
                ref Unsafe.Add(ref outputY, position),
                ref Unsafe.Add(ref outputB, position));
        }
    }

    /// <summary>
    /// Computes the native five-by-five noise convolution for one pixel.
    /// </summary>
    /// <param name="source">The first float in the initialized group and border workspace.</param>
    /// <param name="rows">The prepared offsets for three five-row neighborhoods.</param>
    /// <param name="channelStart">The first of this channel's five offsets: zero, five or ten.</param>
    /// <param name="x">The local center column, including the two-column initialized halo.</param>
    /// <returns>The convolved random samples, before amplitude normalization.</returns>
    private static float ConvolveScalar(ref float source, in InlineArray16<int> rows, int channelStart, int x)
    {
        // ApplyGroup constructs every row offset from a live tile or a completed border strip. Horizontal
        // halos and the visible-lane limit bound all shifted loads, including the first and last output pixels.
        // Preserve native's column-major order for the four noncentral rows, then the four center-row neighbors.
        float others = 0F;
        for (int offset = -Radius; offset <= Radius; offset++)
        {
            others += Unsafe.Add(ref source, rows[channelStart + 0] + x + offset);
            others += Unsafe.Add(ref source, rows[channelStart + 1] + x + offset);
            others += Unsafe.Add(ref source, rows[channelStart + 3] + x + offset);
            others += Unsafe.Add(ref source, rows[channelStart + 4] + x + offset);
        }

        others += Unsafe.Add(ref source, rows[channelStart + Radius] + x - 2);
        others += Unsafe.Add(ref source, rows[channelStart + Radius] + x - 1);
        others += Unsafe.Add(ref source, rows[channelStart + Radius] + x + 1);
        others += Unsafe.Add(ref source, rows[channelStart + Radius] + x + 2);

        // The center coefficient is -3.84 and each of the other 24 coefficients is 0.16. Their exact
        // mathematical sum is zero. Keep the native float coefficients and final multiply-add operation.
        float center = Unsafe.Add(ref source, rows[channelStart + Radius] + x);
        return float.MultiplyAddEstimate(others, NeighborWeight, center * CenterWeight);
    }

    /// <summary>
    /// Computes the native five-by-five noise convolution for consecutive independent pixels.
    /// </summary>
    /// <param name="source">The first float in the initialized group and border workspace.</param>
    /// <param name="rows">The prepared offsets for three five-row neighborhoods.</param>
    /// <param name="channelStart">The first of this channel's five offsets: zero, five or ten.</param>
    /// <param name="x">The local center column, including the two-column initialized halo.</param>
    /// <returns>The convolved random samples, before amplitude normalization.</returns>
    private static Vector128<float> Convolve128(ref float source, in InlineArray16<int> rows, int channelStart, int x)
    {
        // ApplyGroup constructs every row offset from a live tile or a completed border strip. Horizontal
        // halos and the visible-lane limit bound all shifted loads, including the first and last output pixels.
        // Preserve native's column-major order for the four noncentral rows, then the four center-row neighbors.
        Vector128<float> others = Vector128<float>.Zero;
        for (int offset = -Radius; offset <= Radius; offset++)
        {
            others += Vector128.LoadUnsafe(ref Unsafe.Add(ref source, rows[channelStart + 0] + x + offset));
            others += Vector128.LoadUnsafe(ref Unsafe.Add(ref source, rows[channelStart + 1] + x + offset));
            others += Vector128.LoadUnsafe(ref Unsafe.Add(ref source, rows[channelStart + 3] + x + offset));
            others += Vector128.LoadUnsafe(ref Unsafe.Add(ref source, rows[channelStart + 4] + x + offset));
        }

        others += Vector128.LoadUnsafe(ref Unsafe.Add(ref source, rows[channelStart + Radius] + x - 2));
        others += Vector128.LoadUnsafe(ref Unsafe.Add(ref source, rows[channelStart + Radius] + x - 1));
        others += Vector128.LoadUnsafe(ref Unsafe.Add(ref source, rows[channelStart + Radius] + x + 1));
        others += Vector128.LoadUnsafe(ref Unsafe.Add(ref source, rows[channelStart + Radius] + x + 2));

        // The center coefficient is -3.84 and each of the other 24 coefficients is 0.16. Their exact
        // mathematical sum is zero. Keep the native float coefficients and final multiply-add operation.
        Vector128<float> center = Vector128.LoadUnsafe(ref Unsafe.Add(ref source, rows[channelStart + Radius] + x));
        return Vector128.MultiplyAddEstimate(others, Vector128.Create(NeighborWeight), center * Vector128.Create(CenterWeight));
    }

    /// <summary>
    /// Computes the native five-by-five noise convolution for consecutive independent pixels.
    /// </summary>
    /// <param name="source">The first float in the initialized group and border workspace.</param>
    /// <param name="rows">The prepared offsets for three five-row neighborhoods.</param>
    /// <param name="channelStart">The first of this channel's five offsets: zero, five or ten.</param>
    /// <param name="x">The local center column, including the two-column initialized halo.</param>
    /// <returns>The convolved random samples, before amplitude normalization.</returns>
    private static Vector256<float> Convolve256(ref float source, in InlineArray16<int> rows, int channelStart, int x)
    {
        // ApplyGroup constructs every row offset from a live tile or a completed border strip. Horizontal
        // halos and the visible-lane limit bound all shifted loads, including the first and last output pixels.
        // Preserve native's column-major order for the four noncentral rows, then the four center-row neighbors.
        Vector256<float> others = Vector256<float>.Zero;
        for (int offset = -Radius; offset <= Radius; offset++)
        {
            others += Vector256.LoadUnsafe(ref Unsafe.Add(ref source, rows[channelStart + 0] + x + offset));
            others += Vector256.LoadUnsafe(ref Unsafe.Add(ref source, rows[channelStart + 1] + x + offset));
            others += Vector256.LoadUnsafe(ref Unsafe.Add(ref source, rows[channelStart + 3] + x + offset));
            others += Vector256.LoadUnsafe(ref Unsafe.Add(ref source, rows[channelStart + 4] + x + offset));
        }

        others += Vector256.LoadUnsafe(ref Unsafe.Add(ref source, rows[channelStart + Radius] + x - 2));
        others += Vector256.LoadUnsafe(ref Unsafe.Add(ref source, rows[channelStart + Radius] + x - 1));
        others += Vector256.LoadUnsafe(ref Unsafe.Add(ref source, rows[channelStart + Radius] + x + 1));
        others += Vector256.LoadUnsafe(ref Unsafe.Add(ref source, rows[channelStart + Radius] + x + 2));

        // The center coefficient is -3.84 and each of the other 24 coefficients is 0.16. Their exact
        // mathematical sum is zero. Keep the native float coefficients and final multiply-add operation.
        Vector256<float> center = Vector256.LoadUnsafe(ref Unsafe.Add(ref source, rows[channelStart + Radius] + x));
        return Vector256.MultiplyAddEstimate(others, Vector256.Create(NeighborWeight), center * Vector256.Create(CenterWeight));
    }

    /// <summary>
    /// Computes the native five-by-five noise convolution for consecutive independent pixels.
    /// </summary>
    /// <param name="source">The first float in the initialized group and border workspace.</param>
    /// <param name="rows">The prepared offsets for three five-row neighborhoods.</param>
    /// <param name="channelStart">The first of this channel's five offsets: zero, five or ten.</param>
    /// <param name="x">The local center column, including the two-column initialized halo.</param>
    /// <returns>The convolved random samples, before amplitude normalization.</returns>
    private static Vector512<float> Convolve512(ref float source, in InlineArray16<int> rows, int channelStart, int x)
    {
        // ApplyGroup constructs every row offset from a live tile or a completed border strip. Horizontal
        // halos and the visible-lane limit bound all shifted loads, including the first and last output pixels.
        // Preserve native's column-major order for the four noncentral rows, then the four center-row neighbors.
        Vector512<float> others = Vector512<float>.Zero;
        for (int offset = -Radius; offset <= Radius; offset++)
        {
            others += Vector512.LoadUnsafe(ref Unsafe.Add(ref source, rows[channelStart + 0] + x + offset));
            others += Vector512.LoadUnsafe(ref Unsafe.Add(ref source, rows[channelStart + 1] + x + offset));
            others += Vector512.LoadUnsafe(ref Unsafe.Add(ref source, rows[channelStart + 3] + x + offset));
            others += Vector512.LoadUnsafe(ref Unsafe.Add(ref source, rows[channelStart + 4] + x + offset));
        }

        others += Vector512.LoadUnsafe(ref Unsafe.Add(ref source, rows[channelStart + Radius] + x - 2));
        others += Vector512.LoadUnsafe(ref Unsafe.Add(ref source, rows[channelStart + Radius] + x - 1));
        others += Vector512.LoadUnsafe(ref Unsafe.Add(ref source, rows[channelStart + Radius] + x + 1));
        others += Vector512.LoadUnsafe(ref Unsafe.Add(ref source, rows[channelStart + Radius] + x + 2));

        // The center coefficient is -3.84 and each of the other 24 coefficients is 0.16. Their exact
        // mathematical sum is zero. Keep the native float coefficients and final multiply-add operation.
        Vector512<float> center = Vector512.LoadUnsafe(ref Unsafe.Add(ref source, rows[channelStart + Radius] + x));
        return Vector512.MultiplyAddEstimate(others, Vector512.Create(NeighborWeight), center * Vector512.Create(CenterWeight));
    }

    /// <summary>
    /// Applies the native eight-point intensity model and correlated-noise equations without pixel bounds.
    /// </summary>
    private readonly struct NoiseOperator
    {
        // These are the format's intensity coordinates and correlated-noise coefficients from stage_noise.cc.
        private const float IntensityScale = StrengthCount - 2;
        private const float LastIntensity = StrengthCount - 1;
        private const float NoiseNormalization = 0.22F;
        private const float IndependentCorrelation = 1F / 128F;
        private const float CommonCorrelation = 127F / 128F;
        private readonly Vector256<float> strengths;
        private readonly Vector2 correlation;

        /// <summary>
        /// Initializes a new instance of the <see cref="NoiseOperator"/> struct.
        /// </summary>
        /// <param name="strengths">The eight parsed strengths in increasing intensity order.</param>
        /// <param name="correlation">The base Y-to-X and Y-to-B ratios.</param>
        public NoiseOperator(in InlineArray8<float> strengths, Vector2 correlation)
        {
            this.strengths = Vector256.LoadUnsafe(ref Unsafe.AsRef(in strengths[0]));
            this.correlation = correlation;
        }

        /// <summary>
        /// Adds the independent red, green and common random components to one native pixel.
        /// </summary>
        /// <param name="red">The convolved independent red samples.</param>
        /// <param name="green">The convolved independent green samples.</param>
        /// <param name="common">The convolved common samples.</param>
        /// <param name="x">The original X values, replaced with their noise-adjusted values.</param>
        /// <param name="y">The original Y values, replaced with their noise-adjusted values.</param>
        /// <param name="b">The original B values, replaced with their noise-adjusted values.</param>
        public void Invoke(float red, float green, float common, ref float x, ref float y, ref float b)
        {
            // Strengths depend on the original X and Y values. Read both before publishing any changed component.
            // Native uses half of Y-X and Y+X as the green and red intensity coordinates, respectively.
            float greenStrength = this.Strength((y - x) * 0.5F);
            float redStrength = this.Strength((y + x) * 0.5F);
            red *= NoiseNormalization;
            green *= NoiseNormalization;
            common *= NoiseNormalization;

            // Both colors receive 127/128 of the same random component and 1/128 independent noise.
            // Keep multiplication, fused operations and final addition in the native source order.
            float redNoise = redStrength * float.MultiplyAddEstimate(IndependentCorrelation, red, common * CommonCorrelation);
            float greenNoise = greenStrength * float.MultiplyAddEstimate(IndependentCorrelation, green, common * CommonCorrelation);
            float sum = redNoise + greenNoise;
            x = float.MultiplyAddEstimate(this.correlation.X, sum, redNoise - greenNoise) + x;
            y += sum;
            b = float.MultiplyAddEstimate(this.correlation.Y, sum, b);
        }

        /// <summary>
        /// Adds the independent red, green and common random components to corresponding native pixel lanes.
        /// </summary>
        /// <param name="red">The convolved independent red samples.</param>
        /// <param name="green">The convolved independent green samples.</param>
        /// <param name="common">The convolved common samples.</param>
        /// <param name="x">The original X values, replaced with their noise-adjusted values.</param>
        /// <param name="y">The original Y values, replaced with their noise-adjusted values.</param>
        /// <param name="b">The original B values, replaced with their noise-adjusted values.</param>
        public void Invoke(
            Vector128<float> red,
            Vector128<float> green,
            Vector128<float> common,
            ref Vector128<float> x,
            ref Vector128<float> y,
            ref Vector128<float> b)
        {
            // Strengths depend on the original X and Y values. Read both before publishing any changed component.
            // Native uses half of Y-X and Y+X as the green and red intensity coordinates, respectively.
            Vector128<float> greenStrength = this.Strength((y - x) * Vector128.Create(0.5F));
            Vector128<float> redStrength = this.Strength((y + x) * Vector128.Create(0.5F));
            red *= Vector128.Create(NoiseNormalization);
            green *= Vector128.Create(NoiseNormalization);
            common *= Vector128.Create(NoiseNormalization);

            // Both colors receive 127/128 of the same random component and 1/128 independent noise.
            // Keep multiplication, fused operations and final addition in the native source order.
            Vector128<float> redNoise = redStrength
                * Vector128.MultiplyAddEstimate(Vector128.Create(IndependentCorrelation), red, common * Vector128.Create(CommonCorrelation));
            Vector128<float> greenNoise = greenStrength
                * Vector128.MultiplyAddEstimate(Vector128.Create(IndependentCorrelation), green, common * Vector128.Create(CommonCorrelation));
            Vector128<float> sum = redNoise + greenNoise;
            x = Vector128.MultiplyAddEstimate(Vector128.Create(this.correlation.X), sum, redNoise - greenNoise) + x;
            y += sum;
            b = Vector128.MultiplyAddEstimate(Vector128.Create(this.correlation.Y), sum, b);
        }

        /// <summary>
        /// Adds the independent red, green and common random components to corresponding native pixel lanes.
        /// </summary>
        /// <param name="red">The convolved independent red samples.</param>
        /// <param name="green">The convolved independent green samples.</param>
        /// <param name="common">The convolved common samples.</param>
        /// <param name="x">The original X values, replaced with their noise-adjusted values.</param>
        /// <param name="y">The original Y values, replaced with their noise-adjusted values.</param>
        /// <param name="b">The original B values, replaced with their noise-adjusted values.</param>
        public void Invoke(
            Vector256<float> red,
            Vector256<float> green,
            Vector256<float> common,
            ref Vector256<float> x,
            ref Vector256<float> y,
            ref Vector256<float> b)
        {
            // Strengths depend on the original X and Y values. Read both before publishing any changed component.
            // Native uses half of Y-X and Y+X as the green and red intensity coordinates, respectively.
            Vector256<float> greenStrength = this.Strength((y - x) * Vector256.Create(0.5F));
            Vector256<float> redStrength = this.Strength((y + x) * Vector256.Create(0.5F));
            red *= Vector256.Create(NoiseNormalization);
            green *= Vector256.Create(NoiseNormalization);
            common *= Vector256.Create(NoiseNormalization);

            // Both colors receive 127/128 of the same random component and 1/128 independent noise.
            // Keep multiplication, fused operations and final addition in the native source order.
            Vector256<float> redNoise = redStrength
                * Vector256.MultiplyAddEstimate(Vector256.Create(IndependentCorrelation), red, common * Vector256.Create(CommonCorrelation));
            Vector256<float> greenNoise = greenStrength
                * Vector256.MultiplyAddEstimate(Vector256.Create(IndependentCorrelation), green, common * Vector256.Create(CommonCorrelation));
            Vector256<float> sum = redNoise + greenNoise;
            x = Vector256.MultiplyAddEstimate(Vector256.Create(this.correlation.X), sum, redNoise - greenNoise) + x;
            y += sum;
            b = Vector256.MultiplyAddEstimate(Vector256.Create(this.correlation.Y), sum, b);
        }

        /// <summary>
        /// Adds the independent red, green and common random components to corresponding native pixel lanes.
        /// </summary>
        /// <param name="red">The convolved independent red samples.</param>
        /// <param name="green">The convolved independent green samples.</param>
        /// <param name="common">The convolved common samples.</param>
        /// <param name="x">The original X values, replaced with their noise-adjusted values.</param>
        /// <param name="y">The original Y values, replaced with their noise-adjusted values.</param>
        /// <param name="b">The original B values, replaced with their noise-adjusted values.</param>
        public void Invoke(
            Vector512<float> red,
            Vector512<float> green,
            Vector512<float> common,
            ref Vector512<float> x,
            ref Vector512<float> y,
            ref Vector512<float> b)
        {
            // Strengths depend on the original X and Y values. Read both before publishing any changed component.
            // Native uses half of Y-X and Y+X as the green and red intensity coordinates, respectively.
            Vector512<float> greenStrength = this.Strength((y - x) * Vector512.Create(0.5F));
            Vector512<float> redStrength = this.Strength((y + x) * Vector512.Create(0.5F));
            red *= Vector512.Create(NoiseNormalization);
            green *= Vector512.Create(NoiseNormalization);
            common *= Vector512.Create(NoiseNormalization);

            // Both colors receive 127/128 of the same random component and 1/128 independent noise.
            // Keep multiplication, fused operations and final addition in the native source order.
            Vector512<float> redNoise = redStrength
                * Vector512.MultiplyAddEstimate(Vector512.Create(IndependentCorrelation), red, common * Vector512.Create(CommonCorrelation));
            Vector512<float> greenNoise = greenStrength
                * Vector512.MultiplyAddEstimate(Vector512.Create(IndependentCorrelation), green, common * Vector512.Create(CommonCorrelation));
            Vector512<float> sum = redNoise + greenNoise;
            x = Vector512.MultiplyAddEstimate(Vector512.Create(this.correlation.X), sum, redNoise - greenNoise) + x;
            y += sum;
            b = Vector512.MultiplyAddEstimate(Vector512.Create(this.correlation.Y), sum, b);
        }

        /// <summary>
        /// Interpolates and bounds the native noise strength for one intensity.
        /// </summary>
        /// <param name="value">Half of an original Y-plus-or-minus-X value.</param>
        /// <returns>The format's amplitude strength between zero and one.</returns>
        private float Strength(float value)
        {
            // Bound only the sampled LUT coordinate. Native IndexAndFrac maps NaN to zero through
            // std::max(0,x*6). Its upper plateau uses index six and fraction one. The shared finite clamp
            // supplies the same domain for scalar and SIMD without clipping the original X, Y or B values.
            float scaled = ColorNumerics.ClampToFinite(value * IntensityScale, 0F, LastIntensity);
            float floor = MathF.Min(MathF.Floor(scaled), IntensityScale);
            float fraction = scaled - floor;
            int index = (int)floor;
            float low = this.strengths.GetElement(index);
            float high = this.strengths.GetElement(index + 1);
            return MathF.Max(0F, MathF.Min(float.MultiplyAddEstimate(high - low, fraction, low), 1F));
        }

        /// <summary>
        /// Interpolates and bounds the native noise strength for independent intensity lanes.
        /// </summary>
        /// <param name="value">Half of an original Y-plus-or-minus-X value.</param>
        /// <returns>The format's amplitude strength between zero and one.</returns>
        private Vector128<float> Strength(Vector128<float> value)
        {
            // Normalize only the LUT coordinate, as in the scalar IndexAndFrac rule. NaN and negative
            // infinity select zero; positive infinity selects seven. Capping the floor at six makes the
            // upper endpoint use fraction one and keeps both shuffled indices inside the eight-point table.
            Vector128<float> scaled = ColorNumerics.ClampToFinite(
                value * Vector128.Create(IntensityScale), Vector128<float>.Zero, Vector128.Create(LastIntensity));

            Vector128<float> floor = Vector128.Min(Vector128.Floor(scaled), Vector128.Create(IntensityScale));
            Vector128<float> fraction = scaled - floor;
            Vector128<int> index = Vector128.ConvertToInt32(floor);

            // Two four-entry shuffles cover the eight-entry LUT. Shuffle returns zero for an out-of-range
            // index, so exactly one source contributes each result, including indices on either side of four.
            Vector128<float> table0 = this.strengths.GetLower();
            Vector128<float> table1 = this.strengths.GetUpper();
            Vector128<float> low = Vector128.BitwiseOr(
                Vector128.Shuffle(table0, index),
                Vector128.Shuffle(table1, index - Vector128.Create(4)));

            index += Vector128<int>.One;
            Vector128<float> high = Vector128.BitwiseOr(
                Vector128.Shuffle(table0, index),
                Vector128.Shuffle(table1, index - Vector128.Create(4)));

            Vector128<float> strength = Vector128.MultiplyAddEstimate(high - low, fraction, low);
            return Vector128.Max(Vector128<float>.Zero, Vector128.Min(strength, Vector128<float>.One));
        }

        /// <summary>
        /// Interpolates and bounds the native noise strength for independent intensity lanes.
        /// </summary>
        /// <param name="value">Half of an original Y-plus-or-minus-X value.</param>
        /// <returns>The format's amplitude strength between zero and one.</returns>
        private Vector256<float> Strength(Vector256<float> value)
        {
            // Normalize only the LUT coordinate, as in the scalar IndexAndFrac rule. NaN and negative
            // infinity select zero; positive infinity selects seven. Capping the floor at six makes the
            // upper endpoint use fraction one and keeps both shuffled indices inside the eight-point table.
            Vector256<float> scaled = ColorNumerics.ClampToFinite(
                value * Vector256.Create(IntensityScale), Vector256<float>.Zero, Vector256.Create(LastIntensity));

            Vector256<float> floor = Vector256.Min(Vector256.Floor(scaled), Vector256.Create(IntensityScale));
            Vector256<float> fraction = scaled - floor;
            Vector256<int> index = Vector256.ConvertToInt32(floor);
            Vector256<float> low = Vector256.Shuffle(this.strengths, index);
            Vector256<float> high = Vector256.Shuffle(this.strengths, index + Vector256<int>.One);

            Vector256<float> strength = Vector256.MultiplyAddEstimate(high - low, fraction, low);
            return Vector256.Max(Vector256<float>.Zero, Vector256.Min(strength, Vector256<float>.One));
        }

        /// <summary>
        /// Interpolates and bounds the native noise strength for independent intensity lanes.
        /// </summary>
        /// <param name="value">Half of an original Y-plus-or-minus-X value.</param>
        /// <returns>The format's amplitude strength between zero and one.</returns>
        private Vector512<float> Strength(Vector512<float> value)
        {
            // Normalize only the LUT coordinate, as in the scalar IndexAndFrac rule. NaN and negative
            // infinity select zero; positive infinity selects seven. Capping the floor at six makes the
            // upper endpoint use fraction one and keeps both shuffled indices inside the eight-point table.
            Vector512<float> scaled = ColorNumerics.ClampToFinite(
                value * Vector512.Create(IntensityScale), Vector512<float>.Zero, Vector512.Create(LastIntensity));

            Vector512<float> floor = Vector512.Min(Vector512.Floor(scaled), Vector512.Create(IntensityScale));
            Vector512<float> fraction = scaled - floor;
            Vector512<int> index = Vector512.ConvertToInt32(floor);

            // Only the first eight lanes are indexed. The initialized upper half does not contribute.
            Vector512<float> table = Vector512.Create(this.strengths, Vector256<float>.Zero);
            Vector512<float> low = Vector512.Shuffle(table, index);
            Vector512<float> high = Vector512.Shuffle(table, index + Vector512<int>.One);

            Vector512<float> strength = Vector512.MultiplyAddEstimate(high - low, fraction, low);
            return Vector512.Max(Vector512<float>.Zero, Vector512.Min(strength, Vector512<float>.One));
        }
    }

    /// <summary>
    /// Retains the eight parallel 64-bit generators defined by the JPEG XL noise synthesis algorithm.
    /// </summary>
    private struct Xorshift128Plus
    {
        /// <summary>
        /// The eight independent 64-bit generator lanes in libjxl xorshift128plus-inl.h Xorshift128Plus::N.
        /// </summary>
        private const int GeneratorCount = 8;

        /// <summary>
        /// The SplitMix64 seed increment from the native four-seed constructor, added with unsigned wrapping arithmetic.
        /// </summary>
        private const ulong SeedIncrement = 0x9E3779B97F4A7C15UL;

        /// <summary>
        /// The first fixed SplitMix64 multiplier from native Xorshift128Plus::SplitMix64. Products wrap to 64 bits.
        /// </summary>
        private const ulong FirstMixMultiplier = 0xBF58476D1CE4E5B9UL;

        /// <summary>
        /// The second fixed SplitMix64 multiplier from native Xorshift128Plus::SplitMix64. Products wrap to 64 bits.
        /// </summary>
        private const ulong SecondMixMultiplier = 0x94D049BB133111EBUL;

        /// <summary>
        /// The first SplitMix64 logical right shift, in bits, before its first xor and multiply.
        /// </summary>
        private const int FirstMixShift = 30;

        /// <summary>
        /// The second SplitMix64 logical right shift, in bits, before its second xor and multiply.
        /// </summary>
        private const int SecondMixShift = 27;

        /// <summary>
        /// The final SplitMix64 logical right shift, in bits, before the output xor.
        /// </summary>
        private const int FinalMixShift = 31;

        private InlineArray8<ulong> state0;
        private InlineArray8<ulong> state1;

        /// <summary>
        /// Initializes a new instance of the <see cref="Xorshift128Plus"/> struct with the native four-seed mapping.
        /// </summary>
        /// <param name="visibleFrame">The high word of the first seed.</param>
        /// <param name="nonvisibleFrame">The low word of the first seed.</param>
        /// <param name="left">The high word of the second seed.</param>
        /// <param name="top">The low word of the second seed.</param>
        public Xorshift128Plus(uint visibleFrame, uint nonvisibleFrame, uint left, uint top)
        {
            this.state0 = default;
            this.state1 = default;
            this.state0[0] = SplitMix64((((ulong)visibleFrame << 32) | nonvisibleFrame) + SeedIncrement);
            this.state1[0] = SplitMix64((((ulong)left << 32) | top) + SeedIncrement);
            for (int i = 1; i < GeneratorCount; i++)
            {
                this.state0[i] = SplitMix64(this.state0[i - 1]);
                this.state1[i] = SplitMix64(this.state1[i - 1]);
            }
        }

        /// <summary>
        /// Writes sixteen random floats in the native low-word, high-word order and advances all eight generators once.
        /// </summary>
        /// <param name="destination">Exactly sixteen writable floats.</param>
        public void Fill(Span<float> destination)
        {
            ref ulong first = ref this.state0[0];
            ref ulong second = ref this.state1[0];
            ref float output = ref MemoryMarshal.GetReference(destination);

            // Each 64-bit generator contributes its low and high 32-bit words in that order. Keep their top
            // 23 bits as a float mantissa with the exponent bits of 1.0: [1,2), exactly as native BitsToFloat.
            if (Vector512.IsHardwareAccelerated)
            {
                for (nuint i = 0; i < GeneratorCount; i += (nuint)Vector512<ulong>.Count)
                {
                    Vector512<ulong> s1 = Vector512.LoadUnsafe(ref first, i);
                    Vector512<ulong> s0 = Vector512.LoadUnsafe(ref second, i);
                    Vector512<ulong> bits = s1 + s0;
                    s0.StoreUnsafe(ref first, i);
                    s1 ^= Vector512.ShiftLeft(s1, 23);
                    s1 ^= s0 ^ Vector512.ShiftRightLogical(s1, 18) ^ Vector512.ShiftRightLogical(s0, 5);
                    s1.StoreUnsafe(ref second, i);
                    Vector512<uint> floatBits = Vector512.ShiftRightLogical(bits.AsUInt32(), 9) | Vector512.Create(0x3F800000U);
                    floatBits.AsSingle().StoreUnsafe(ref output, 2 * i);
                }
            }
            else if (Vector256.IsHardwareAccelerated)
            {
                for (nuint i = 0; i < GeneratorCount; i += (nuint)Vector256<ulong>.Count)
                {
                    Vector256<ulong> s1 = Vector256.LoadUnsafe(ref first, i);
                    Vector256<ulong> s0 = Vector256.LoadUnsafe(ref second, i);
                    Vector256<ulong> bits = s1 + s0;
                    s0.StoreUnsafe(ref first, i);
                    s1 ^= Vector256.ShiftLeft(s1, 23);
                    s1 ^= s0 ^ Vector256.ShiftRightLogical(s1, 18) ^ Vector256.ShiftRightLogical(s0, 5);
                    s1.StoreUnsafe(ref second, i);
                    Vector256<uint> floatBits = Vector256.ShiftRightLogical(bits.AsUInt32(), 9) | Vector256.Create(0x3F800000U);
                    floatBits.AsSingle().StoreUnsafe(ref output, 2 * i);
                }
            }
            else if (Vector128.IsHardwareAccelerated)
            {
                for (nuint i = 0; i < GeneratorCount; i += (nuint)Vector128<ulong>.Count)
                {
                    Vector128<ulong> s1 = Vector128.LoadUnsafe(ref first, i);
                    Vector128<ulong> s0 = Vector128.LoadUnsafe(ref second, i);
                    Vector128<ulong> bits = s1 + s0;
                    s0.StoreUnsafe(ref first, i);
                    s1 ^= Vector128.ShiftLeft(s1, 23);
                    s1 ^= s0 ^ Vector128.ShiftRightLogical(s1, 18) ^ Vector128.ShiftRightLogical(s0, 5);
                    s1.StoreUnsafe(ref second, i);
                    Vector128<uint> floatBits = Vector128.ShiftRightLogical(bits.AsUInt32(), 9) | Vector128.Create(0x3F800000U);
                    floatBits.AsSingle().StoreUnsafe(ref output, 2 * i);
                }
            }
            else
            {
                for (int i = 0; i < GeneratorCount; i++)
                {
                    ulong s1 = this.state0[i];
                    ulong s0 = this.state1[i];
                    ulong bits = s1 + s0;
                    this.state0[i] = s0;
                    s1 ^= s1 << 23;
                    this.state1[i] = s1 ^ s0 ^ (s1 >> 18) ^ (s0 >> 5);
                    Unsafe.Add(ref output, 2 * i) = BitConverter.UInt32BitsToSingle(((uint)bits >> 9) | 0x3F800000U);
                    Unsafe.Add(ref output, (2 * i) + 1) = BitConverter.UInt32BitsToSingle(((uint)(bits >> 32) >> 9) | 0x3F800000U);
                }
            }
        }

        /// <summary>
        /// Mixes one seed with the format's SplitMix64 initialization equations and wrapping unsigned arithmetic.
        /// </summary>
        /// <param name="value">The previous seed or lane state.</param>
        /// <returns>The mixed 64-bit state.</returns>
        private static ulong SplitMix64(ulong value)
        {
            value = (value ^ (value >> FirstMixShift)) * FirstMixMultiplier;
            value = (value ^ (value >> SecondMixShift)) * SecondMixMultiplier;
            return value ^ (value >> FinalMixShift);
        }
    }
}
