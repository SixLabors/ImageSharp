// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

/// <content>
/// <see cref="Vector128{Single}"/> version of <see cref="Block8x8F"/>.
/// </content>
internal partial struct Block8x8F
{
    /// <summary>
    /// <see cref="Vector128{Single}"/> version of <see cref="NormalizeColorsInPlace(float)"/> and <see cref="RoundInPlace()"/>.
    /// </summary>
    /// <param name="maximum">The maximum value to normalize to.</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    public void NormalizeColorsAndRoundInPlaceVector128(float maximum)
    {
        Vector128<float> max = Vector128.Create(maximum);
        Vector128<float> off = Vector128.Ceiling(max * .5F);

        this.V0L = NormalizeAndRoundVector128(this.V0L.AsVector128(), off, max).AsVector4();
        this.V0R = NormalizeAndRoundVector128(this.V0R.AsVector128(), off, max).AsVector4();
        this.V1L = NormalizeAndRoundVector128(this.V1L.AsVector128(), off, max).AsVector4();
        this.V1R = NormalizeAndRoundVector128(this.V1R.AsVector128(), off, max).AsVector4();
        this.V2L = NormalizeAndRoundVector128(this.V2L.AsVector128(), off, max).AsVector4();
        this.V2R = NormalizeAndRoundVector128(this.V2R.AsVector128(), off, max).AsVector4();
        this.V3L = NormalizeAndRoundVector128(this.V3L.AsVector128(), off, max).AsVector4();
        this.V3R = NormalizeAndRoundVector128(this.V3R.AsVector128(), off, max).AsVector4();
        this.V4L = NormalizeAndRoundVector128(this.V4L.AsVector128(), off, max).AsVector4();
        this.V4R = NormalizeAndRoundVector128(this.V4R.AsVector128(), off, max).AsVector4();
        this.V5L = NormalizeAndRoundVector128(this.V5L.AsVector128(), off, max).AsVector4();
        this.V5R = NormalizeAndRoundVector128(this.V5R.AsVector128(), off, max).AsVector4();
        this.V6L = NormalizeAndRoundVector128(this.V6L.AsVector128(), off, max).AsVector4();
        this.V6R = NormalizeAndRoundVector128(this.V6R.AsVector128(), off, max).AsVector4();
        this.V7L = NormalizeAndRoundVector128(this.V7L.AsVector128(), off, max).AsVector4();
        this.V7R = NormalizeAndRoundVector128(this.V7R.AsVector128(), off, max).AsVector4();
    }

    /// <summary>
    /// Loads values from <paramref name="source"/> using extended AVX2 intrinsics.
    /// </summary>
    /// <param name="source">The source <see cref="Block8x8"/></param>
    public void LoadFromInt16ExtendedVector128(ref Block8x8 source)
    {
        DebugGuard.IsTrue(Vector128.IsHardwareAccelerated, "Vector128 support is required to run this operation!");

        ref Vector128<short> srcBase = ref Unsafe.As<Block8x8, Vector128<short>>(ref source);
        ref Vector128<float> destBase = ref Unsafe.As<Block8x8F, Vector128<float>>(ref this);

        // Only 8 iterations, one per 128b short block
        for (nuint i = 0; i < 8; i++)
        {
            Vector128<short> src = Unsafe.Add(ref srcBase, i);

            // Step 1: Widen short -> int
            Vector128<int> lower = Vector128.WidenLower(src); // lower 4 shorts -> 4 ints
            Vector128<int> upper = Vector128.WidenUpper(src); // upper 4 shorts -> 4 ints

            // Step 2: Convert int -> float
            Vector128<float> lowerF = Vector128.ConvertToSingle(lower);
            Vector128<float> upperF = Vector128.ConvertToSingle(upper);

            // Step 3: Store to destination (this is 16 lanes -> two Vector128<float> blocks)
            Unsafe.Add(ref destBase, (i * 2) + 0) = lowerF;
            Unsafe.Add(ref destBase, (i * 2) + 1) = upperF;
        }
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private static Vector128<float> NormalizeAndRoundVector128(Vector128<float> value, Vector128<float> off, Vector128<float> max)
        => Vector128_.RoundToNearestInteger(Vector128_.Clamp(value + off, Vector128<float>.Zero, max));

    private static void MultiplyIntoInt16Vector128(ref Block8x8F a, ref Block8x8F b, ref Block8x8 dest)
    {
        DebugGuard.IsTrue(Vector128.IsHardwareAccelerated, "Vector128 support is required to run this operation!");

        ref Vector128<float> aBase = ref Unsafe.As<Block8x8F, Vector128<float>>(ref a);
        ref Vector128<float> bBase = ref Unsafe.As<Block8x8F, Vector128<float>>(ref b);
        ref Vector128<short> destBase = ref Unsafe.As<Block8x8, Vector128<short>>(ref dest);

        for (nuint i = 0; i < 16; i += 2)
        {
            Vector128<int> left = Vector128_.ConvertToInt32RoundToEven(Unsafe.Add(ref aBase, i + 0) * Unsafe.Add(ref bBase, i + 0));
            Vector128<int> right = Vector128_.ConvertToInt32RoundToEven(Unsafe.Add(ref aBase, i + 1) * Unsafe.Add(ref bBase, i + 1));

            Unsafe.Add(ref destBase, i / 2) = Vector128_.PackSignedSaturate(left, right);
        }
    }
}
