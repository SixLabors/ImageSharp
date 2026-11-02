// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

internal struct JxlWeightsSymmetric5
{
    public InlineArray4<float> C;

    public InlineArray4<float> R;

    public InlineArray4<float> R2;

    public InlineArray4<float> D;

    public InlineArray4<float> D2;

    public InlineArray4<float> L;

    public static InlineArray4<float> CreateVector4(float x)
    {
        InlineArray4<float> array = default;

        array[0] = array[1] = array[2] = array[3] = x;

        return array;
    }

    public readonly Vector128<float> GetCVector()
    {
        ref float first = ref Unsafe.AsRef(in this.C[0]);
        return Vector128.LoadUnsafe(ref first);
    }

    public readonly Vector128<float> GetRVector()
    {
        ref float first = ref Unsafe.AsRef(in this.R[0]);
        return Vector128.LoadUnsafe(ref first);
    }

    public readonly Vector128<float> GetR2Vector()
    {
        ref float first = ref Unsafe.AsRef(in this.R2[0]);
        return Vector128.LoadUnsafe(ref first);
    }

    public readonly Vector128<float> GetDVector()
    {
        ref float first = ref Unsafe.AsRef(in this.D[0]);
        return Vector128.LoadUnsafe(ref first);
    }

    public readonly Vector128<float> GetD2Vector()
    {
        ref float first = ref Unsafe.AsRef(in this.D2[0]);
        return Vector128.LoadUnsafe(ref first);
    }

    public readonly Vector128<float> GetLVector()
    {
        ref float first = ref Unsafe.AsRef(in this.L[0]);
        return Vector128.LoadUnsafe(ref first);
    }

    public readonly void SetC(Vector128<float> vec)
    {
        ref float first = ref Unsafe.AsRef(in this.C[0]);
        vec.StoreUnsafe(ref first);
    }

    public readonly void SetD(Vector128<float> vec)
    {
        ref float first = ref Unsafe.AsRef(in this.D[0]);
        vec.StoreUnsafe(ref first);
    }

    public readonly void SetD2(Vector128<float> vec)
    {
        ref float first = ref Unsafe.AsRef(in this.D2[0]);
        vec.StoreUnsafe(ref first);
    }

    public readonly void SetR(Vector128<float> vec)
    {
        ref float first = ref Unsafe.AsRef(in this.R[0]);
        vec.StoreUnsafe(ref first);
    }

    public readonly void SetR2(Vector128<float> vec)
    {
        ref float first = ref Unsafe.AsRef(in this.R2[0]);
        vec.StoreUnsafe(ref first);
    }

    public readonly void SetL(Vector128<float> vec)
    {
        ref float first = ref Unsafe.AsRef(in this.L[0]);
        vec.StoreUnsafe(ref first);
    }
}
