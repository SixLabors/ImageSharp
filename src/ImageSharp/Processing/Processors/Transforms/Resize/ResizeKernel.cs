// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms;

/// <summary>
/// Describes one kernel of a <see cref="ResizeKernelMap"/>: the first source sample, the number of samples, and the
/// position of the weights in the weight buffer of the map.
/// </summary>
internal readonly partial struct ResizeKernel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ResizeKernel"/> struct.
    /// </summary>
    /// <param name="startIndex">The first source sample of the kernel.</param>
    /// <param name="weightsOffset">The index of the first weight of the kernel in the weight buffer of the map.</param>
    /// <param name="length">The number of source samples of the kernel.</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    public ResizeKernel(int startIndex, int weightsOffset, int length)
    {
        this.StartIndex = startIndex;
        this.WeightsOffset = weightsOffset;
        this.Length = length;
    }

    /// <summary>
    /// Gets the first source sample of the kernel.
    /// </summary>
    public int StartIndex
    {
        [MethodImpl(InliningOptions.ShortMethod)]
        get;
    }

    /// <summary>
    /// Gets the number of source samples of the kernel.
    /// </summary>
    public int Length
    {
        [MethodImpl(InliningOptions.ShortMethod)]
        get;
    }

    /// <summary>
    /// Gets the index of the first weight of the kernel in the weight buffer of the map.
    /// The buffer stores each weight 4 times, once for each channel of a pixel.
    /// </summary>
    public int WeightsOffset
    {
        [MethodImpl(InliningOptions.ShortMethod)]
        get;
    }

    /// <summary>
    /// Returns a copy of this kernel with the first source sample <paramref name="left"/> and the same weights.
    /// </summary>
    /// <param name="left">The first source sample of the copy.</param>
    /// <returns>The copy.</returns>
    [MethodImpl(InliningOptions.ShortMethod)]
    public ResizeKernel AlterLeftValue(int left)
        => new(left, this.WeightsOffset, this.Length);
}
