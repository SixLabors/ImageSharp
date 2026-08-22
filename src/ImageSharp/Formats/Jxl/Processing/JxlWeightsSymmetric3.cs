// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

internal sealed class JxlWeightsSymmetric3
{
    private InlineArray4<float> c;

    private InlineArray4<float> r;

    private InlineArray4<float> d;

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

    public Vector128<float> GetDVector()
    {
        ref float first = ref Unsafe.AsRef(in this.d[0]);
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

    public void SetR(Vector128<float> vec)
    {
        ref float first = ref Unsafe.AsRef(in this.r[0]);
        vec.StoreUnsafe(ref first);
    }
}
