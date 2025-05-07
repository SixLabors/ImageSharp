// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
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
        Vector128<float> off = Vector128.Create(MathF.Ceiling(maximum * 0.5F));
        Vector128<float> max = Vector128.Create(maximum);

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

    [MethodImpl(InliningOptions.ShortMethod)]
    private static Vector128<float> NormalizeAndRoundVector128(Vector128<float> value, Vector128<float> off, Vector128<float> max)
        => Vector128_.RoundToNearestInteger(Vector128_.Clamp(value + off, Vector128<float>.Zero, max));

    private static void MultiplyIntoInt16_Sse2(ref Block8x8F a, ref Block8x8F b, ref Block8x8 dest)
    {
        DebugGuard.IsTrue(Sse2.IsSupported, "Sse2 support is required to run this operation!");

        ref Vector128<float> aBase = ref Unsafe.As<Block8x8F, Vector128<float>>(ref a);
        ref Vector128<float> bBase = ref Unsafe.As<Block8x8F, Vector128<float>>(ref b);

        ref Vector128<short> destBase = ref Unsafe.As<Block8x8, Vector128<short>>(ref dest);

        // TODO: We can use the v128 utilities for this.
        for (nuint i = 0; i < 16; i += 2)
        {
            Vector128<int> left = Sse2.ConvertToVector128Int32(Sse.Multiply(Unsafe.Add(ref aBase, i + 0), Unsafe.Add(ref bBase, i + 0)));
            Vector128<int> right = Sse2.ConvertToVector128Int32(Sse.Multiply(Unsafe.Add(ref aBase, i + 1), Unsafe.Add(ref bBase, i + 1)));

            Unsafe.Add(ref destBase, i / 2) = Sse2.PackSignedSaturate(left, right);
        }
    }
}
