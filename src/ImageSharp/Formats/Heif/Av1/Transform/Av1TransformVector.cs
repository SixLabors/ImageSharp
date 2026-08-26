// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Stores the fixed set of SIMD values used by one bulk AV1 transform axis.
/// </summary>
/// <typeparam name="TVector">The SIMD vector type used for parallel transform lanes.</typeparam>
[StructLayout(LayoutKind.Sequential)]
internal struct Av1TransformVector<TVector>
    where TVector : struct
{
    // Explicit fields give the JIT constant offsets inside large transform operators. The previous inline-array
    // helpers were not inlined once those operators exceeded the JIT's expansion budget, causing a call per access.
    public TVector V0;
    public TVector V1;
    public TVector V2;
    public TVector V3;
    public TVector V4;
    public TVector V5;
    public TVector V6;
    public TVector V7;
    public TVector V8;
    public TVector V9;
    public TVector V10;
    public TVector V11;
    public TVector V12;
    public TVector V13;
    public TVector V14;
    public TVector V15;
    public TVector V16;
    public TVector V17;
    public TVector V18;
    public TVector V19;
    public TVector V20;
    public TVector V21;
    public TVector V22;
    public TVector V23;
    public TVector V24;
    public TVector V25;
    public TVector V26;
    public TVector V27;
    public TVector V28;
    public TVector V29;
    public TVector V30;
    public TVector V31;
    public TVector V32;
    public TVector V33;
    public TVector V34;
    public TVector V35;
    public TVector V36;
    public TVector V37;
    public TVector V38;
    public TVector V39;
    public TVector V40;
    public TVector V41;
    public TVector V42;
    public TVector V43;
    public TVector V44;
    public TVector V45;
    public TVector V46;
    public TVector V47;
    public TVector V48;
    public TVector V49;
    public TVector V50;
    public TVector V51;
    public TVector V52;
    public TVector V53;
    public TVector V54;
    public TVector V55;
    public TVector V56;
    public TVector V57;
    public TVector V58;
    public TVector V59;
    public TVector V60;
    public TVector V61;
    public TVector V62;
    public TVector V63;

    /// <summary>
    /// Gets a reference to the SIMD value at the requested transform position.
    /// </summary>
    /// <param name="index">The zero-based transform position.</param>
    /// <returns>The SIMD value at the requested position.</returns>
    [UnscopedRef]
    public ref TVector this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ref TVector first = ref Unsafe.As<Av1TransformVector<TVector>, TVector>(ref this);
            return ref Unsafe.Add(ref first, (uint)index);
        }
    }
}
