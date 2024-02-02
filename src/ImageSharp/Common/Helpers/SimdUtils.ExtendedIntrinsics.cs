// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;

// ReSharper disable MemberHidesStaticFromOuterClass
namespace SixLabors.ImageSharp;

internal static partial class SimdUtils
{
    /// <summary>
    /// Implementation methods based on newer <see cref="Vector{T}"/> API-s (Vector.Widen, Vector.Narrow, Vector.ConvertTo*).
    /// Only accelerated only on RyuJIT having dotnet/coreclr#10662 merged (.NET Core 2.1+ .NET 4.7.2+)
    /// See:
    /// https://github.com/dotnet/coreclr/pull/10662
    /// API Proposal:
    /// https://github.com/dotnet/corefx/issues/15957
    /// </summary>
    public static class ExtendedIntrinsics
    {
        public static bool IsAvailable { get; } = Vector.IsHardwareAccelerated;

        /// <summary>
        /// Widen and convert a vector of <see cref="short"/> values into 2 vectors of <see cref="float"/>-s.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void ConvertToSingle(
            Vector<short> source,
            out Vector<float> dest1,
            out Vector<float> dest2)
        {
            Vector.Widen(source, out Vector<int> i1, out Vector<int> i2);
            dest1 = Vector.ConvertToSingle(i1);
            dest2 = Vector.ConvertToSingle(i2);
        }
    }
}
