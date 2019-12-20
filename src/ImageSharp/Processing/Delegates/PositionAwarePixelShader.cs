using System;
using System.Numerics;

namespace SixLabors.ImageSharp.Processing.Delegates
{
    /// <summary>
    /// A <see langword="delegate"/> representing a user defined pixel shader.
    /// </summary>
    /// <param name="span">The target row of <see cref="Vector4"/> pixels to process.</param>
    /// <param name="offsetY">The initial vertical offset for the input pixels to process.</param>
    /// <param name="offsetX">The initial horizontal offset for the input pixels to process.</param>
    /// <remarks>The <see cref="Vector4.X"/>, <see cref="Vector4.Y"/>, <see cref="Vector4.Z"/>, and <see cref="Vector4.W"/> fields map the RGBA channels respectively.</remarks>
    public delegate void PositionAwarePixelShader(Span<Vector4> span, int offsetY, int offsetX);
}
