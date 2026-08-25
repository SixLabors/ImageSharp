// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Stores the fixed set of SIMD values used by one bulk AV1 transform axis.
/// </summary>
/// <typeparam name="TVector">The SIMD vector type used for parallel transform lanes.</typeparam>
[InlineArray(Av1Constants.MaxTransformSize)]
internal struct Av1TransformVector<TVector>
    where TVector : struct
{
    /// <summary>
    /// The first value in the fixed transform vector.
    /// </summary>
    private TVector element0;
}
