// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

internal sealed class JxlWeightsSymmetric5
{
    private InlineArray4<float> c;

    private InlineArray4<float> r;

    private InlineArray4<float> r2;

    private InlineArray4<float> d;

    private InlineArray4<float> d2;

    private InlineArray4<float> l;

    public Vector128<float> GetCVector()
    {
        ref float first = ref Unsafe.AsRef(in this.c[0]);
        return Vector128.LoadUnsafe(ref first);
    }

    public Vector128<float> GetRVector()
    {
        ref float first = ref Unsafe.AsRef(in this.r[0]);
        return Vector128.LoadUnsafe(ref first);
    }

    public Vector128<float> GetR2Vector()
    {
        ref float first = ref Unsafe.AsRef(in this.r2[0]);
        return Vector128.LoadUnsafe(ref first);
    }

    public Vector128<float> GetDVector()
    {
        ref float first = ref Unsafe.AsRef(in this.d[0]);
        return Vector128.LoadUnsafe(ref first);
    }

    public Vector128<float> GetD2Vector()
    {
        ref float first = ref Unsafe.AsRef(in this.d2[0]);
        return Vector128.LoadUnsafe(ref first);
    }

    public Vector128<float> GetLVector()
    {
        ref float first = ref Unsafe.AsRef(in this.l[0]);
        return Vector128.LoadUnsafe(ref first);
    }

    public void SetC(Vector128<float> vec)
    {
        ref float first = ref Unsafe.AsRef(in this.c[0]);
        vec.StoreUnsafe(ref first);
    }

    public void SetD(Vector128<float> vec)
    {
        ref float first = ref Unsafe.AsRef(in this.d[0]);
        vec.StoreUnsafe(ref first);
    }

    public void SetD2(Vector128<float> vec)
    {
        ref float first = ref Unsafe.AsRef(in this.d2[0]);
        vec.StoreUnsafe(ref first);
    }

    public void SetR(Vector128<float> vec)
    {
        ref float first = ref Unsafe.AsRef(in this.r[0]);
        vec.StoreUnsafe(ref first);
    }

    public void SetR2(Vector128<float> vec)
    {
        ref float first = ref Unsafe.AsRef(in this.r2[0]);
        vec.StoreUnsafe(ref first);
    }

    public void SetL(Vector128<float> vec)
    {
        ref float first = ref Unsafe.AsRef(in this.l[0]);
        vec.StoreUnsafe(ref first);
    }
}
