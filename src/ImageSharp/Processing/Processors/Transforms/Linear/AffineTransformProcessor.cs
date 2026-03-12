// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms;

/// <summary>
/// Defines an affine transformation applicable on an <see cref="Image"/>.
/// </summary>
public class AffineTransformProcessor : CloningImageProcessor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AffineTransformProcessor"/> class.
    /// </summary>
    /// <param name="matrix">The transform matrix.</param>
    /// <param name="sampler">The sampler to perform the transform operation.</param>
    /// <param name="targetDimensions">The target dimensions.</param>
    public AffineTransformProcessor(Matrix3x2 matrix, IResampler sampler, Size targetDimensions)
    {
        Guard.NotNull(sampler, nameof(sampler));
        Guard.MustBeValueType(sampler);

        if (TransformUtilities.IsDegenerate(matrix))
        {
            throw new DegenerateTransformException("Matrix is degenerate. Check input values.");
        }

        this.Sampler = sampler;
        this.TransformMatrix = matrix;
        this.DestinationSize = targetDimensions;
    }

    /// <summary>
    /// Gets the sampler to perform interpolation of the transform operation.
    /// </summary>
    public IResampler Sampler { get; }

    /// <summary>
    /// Gets the matrix used to supply the affine transform.
    /// </summary>
    public Matrix3x2 TransformMatrix { get; }

    /// <summary>
    /// Gets the destination size to constrain the transformed image to.
    /// </summary>
    public Size DestinationSize { get; }

    /// <inheritdoc/>
    public override ICloningImageProcessor<TPixel> CreatePixelSpecificCloningProcessor<TPixel>(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
        => new AffineTransformProcessor<TPixel>(configuration, this, source, sourceRectangle);
}
