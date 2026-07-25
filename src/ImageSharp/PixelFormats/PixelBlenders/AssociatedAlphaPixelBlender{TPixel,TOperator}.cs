// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.PixelFormats.PixelBlenders;

/// <summary>
/// Provides the vector representation used to blend pixels that store associated alpha.
/// </summary>
/// <typeparam name="TPixel">The associated-alpha pixel format.</typeparam>
/// <typeparam name="TOperator">The Porter-Duff equation.</typeparam>
internal abstract class AssociatedAlphaPixelBlender<TPixel, TOperator> : PixelBlender<TPixel, TOperator>
    where TPixel : unmanaged, IPixel<TPixel>
    where TOperator : struct, IPixelBlenderOperator
{
    private static readonly PixelOperations<TPixel> Operations = PixelOperations<TPixel>.Instance;

    /// <inheritdoc />
    public sealed override TPixel Blend(TPixel background, TPixel source, float amount)
    {
        // Associated RGB and alpha must remain in their stored representation throughout composition.
        Vector4 result = TOperator.Invoke(background.ToAssociatedScaledVector4(), source.ToAssociatedScaledVector4(), Numerics.Clamp(amount, 0, 1F));

        return TPixel.FromAssociatedScaledVector4(result);
    }

    /// <inheritdoc />
    protected override void ToBlendVector4<TPixelSource>(
        Configuration configuration,
        ReadOnlySpan<TPixelSource> source,
        Span<Vector4> destination)
    {
        // Selecting the source representation once per row avoids a format check for every blended pixel.
        PixelOperations<TPixelSource>.Instance.ToVector4(configuration, source, destination, PixelConversionModifiers.Scale | PixelConversionModifiers.Premultiply);
    }

    /// <inheritdoc />
    protected override Vector4 ToBlendVector4(TPixel source) => source.ToAssociatedScaledVector4();

    /// <inheritdoc />
    protected override void FromBlendVector4(
        Configuration configuration,
        Span<Vector4> source,
        Span<TPixel> destination)
    {
        Operations.FromVector4Destructive(configuration, source, destination, PixelConversionModifiers.Scale | PixelConversionModifiers.Premultiply);
    }
}
