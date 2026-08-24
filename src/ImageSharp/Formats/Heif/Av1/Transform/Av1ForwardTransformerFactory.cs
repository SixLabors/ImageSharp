// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Selects the forward-transform coefficient shape used by AV1 encoder mode decision.
/// </summary>
internal static class Av1ForwardTransformerFactory
{
    /// <summary>
    /// Applies the encoder-selected coefficient-shape transform to a residual block.
    /// </summary>
    /// <param name="residualBuffer">The spatial residual samples.</param>
    /// <param name="residualStride">The number of residual samples between rows.</param>
    /// <param name="coefficientBuffer">The destination transform coefficients.</param>
    /// <param name="coefficientStride">The number of coefficient positions between rows.</param>
    /// <param name="transformSize">The transform-block dimensions.</param>
    /// <param name="threeQuadEnergy">The accumulated energy outside the retained coefficient shape.</param>
    /// <param name="bitDepth">The source sample bit depth.</param>
    /// <param name="transformType">The compound transform type.</param>
    /// <param name="componentType">The luma or chroma component class.</param>
    /// <param name="transformCoefficientShape">The subset of coefficients evaluated by mode decision.</param>
    internal static void EstimateTransform(
        Span<short> residualBuffer,
        uint residualStride,
        Span<int> coefficientBuffer,
        uint coefficientStride,
        Av1TransformSize transformSize,
        ref ulong threeQuadEnergy,
        int bitDepth,
        Av1TransformType transformType,
        Av1PlaneType componentType,
        Av1CoefficientShape transformCoefficientShape)
    {
        switch (transformCoefficientShape)
        {
            case Av1CoefficientShape.Default:
                EstimateTransformDefault(residualBuffer, residualStride, coefficientBuffer, coefficientStride, transformSize, ref threeQuadEnergy, bitDepth, transformType, componentType);
                break;
            case Av1CoefficientShape.N2:
                EstimateTransformN2(residualBuffer, residualStride, coefficientBuffer, coefficientStride, transformSize, ref threeQuadEnergy, bitDepth, transformType, componentType);
                break;
            case Av1CoefficientShape.N4:
                EstimateTransformN4(residualBuffer, residualStride, coefficientBuffer, coefficientStride, transformSize, ref threeQuadEnergy, bitDepth, transformType, componentType);
                break;
            case Av1CoefficientShape.OnlyDc:
                EstimateTransformOnlyDc(residualBuffer, residualStride, coefficientBuffer, coefficientStride, transformSize, ref threeQuadEnergy, bitDepth, transformType, componentType);
                break;
        }
    }

    /// <summary>
    /// Applies the complete two-dimensional transform without discarding coefficients.
    /// </summary>
    /// <param name="residualBuffer">The spatial residual samples.</param>
    /// <param name="residualStride">The number of residual samples between rows.</param>
    /// <param name="coefficientBuffer">The destination transform coefficients.</param>
    /// <param name="coefficientStride">The number of coefficient positions between rows.</param>
    /// <param name="transformSize">The transform-block dimensions.</param>
    /// <param name="threeQuadEnergy">The accumulated energy outside the retained coefficient shape.</param>
    /// <param name="bitDepth">The source sample bit depth.</param>
    /// <param name="transformType">The compound transform type.</param>
    /// <param name="componentType">The luma or chroma component class.</param>
    private static void EstimateTransformDefault(
        Span<short> residualBuffer,
        uint residualStride,
        Span<int> coefficientBuffer,
        uint coefficientStride,
        Av1TransformSize transformSize,
        ref ulong threeQuadEnergy,
        int bitDepth,
        Av1TransformType transformType,
        Av1PlaneType componentType)
        => Av1ForwardTransformer.Transform2d(residualBuffer, coefficientBuffer, residualStride, transformType, transformSize, bitDepth);

    /// <summary>
    /// Applies the half-coefficient transform shape and measures the discarded coefficient energy.
    /// </summary>
    /// <param name="residualBuffer">The spatial residual samples.</param>
    /// <param name="residualStride">The number of residual samples between rows.</param>
    /// <param name="coefficientBuffer">The destination transform coefficients.</param>
    /// <param name="coefficientStride">The number of coefficient positions between rows.</param>
    /// <param name="transformSize">The transform-block dimensions.</param>
    /// <param name="threeQuadEnergy">The accumulated energy outside the retained coefficient shape.</param>
    /// <param name="bitDepth">The source sample bit depth.</param>
    /// <param name="transformType">The compound transform type.</param>
    /// <param name="componentType">The luma or chroma component class.</param>
    private static void EstimateTransformN2(Span<short> residualBuffer, uint residualStride, Span<int> coefficientBuffer, uint coefficientStride, Av1TransformSize transformSize, ref ulong threeQuadEnergy, int bitDepth, Av1TransformType transformType, Av1PlaneType componentType) => throw new NotImplementedException();

    /// <summary>
    /// Applies the quarter-coefficient transform shape and measures the discarded coefficient energy.
    /// </summary>
    /// <param name="residualBuffer">The spatial residual samples.</param>
    /// <param name="residualStride">The number of residual samples between rows.</param>
    /// <param name="coefficientBuffer">The destination transform coefficients.</param>
    /// <param name="coefficientStride">The number of coefficient positions between rows.</param>
    /// <param name="transformSize">The transform-block dimensions.</param>
    /// <param name="threeQuadEnergy">The accumulated energy outside the retained coefficient shape.</param>
    /// <param name="bitDepth">The source sample bit depth.</param>
    /// <param name="transformType">The compound transform type.</param>
    /// <param name="componentType">The luma or chroma component class.</param>
    private static void EstimateTransformN4(Span<short> residualBuffer, uint residualStride, Span<int> coefficientBuffer, uint coefficientStride, Av1TransformSize transformSize, ref ulong threeQuadEnergy, int bitDepth, Av1TransformType transformType, Av1PlaneType componentType) => throw new NotImplementedException();

    /// <summary>
    /// Evaluates only the transform's DC coefficient and measures the discarded coefficient energy.
    /// </summary>
    /// <param name="residualBuffer">The spatial residual samples.</param>
    /// <param name="residualStride">The number of residual samples between rows.</param>
    /// <param name="coefficientBuffer">The destination transform coefficients.</param>
    /// <param name="coefficientStride">The number of coefficient positions between rows.</param>
    /// <param name="transformSize">The transform-block dimensions.</param>
    /// <param name="threeQuadEnergy">The accumulated energy outside the retained coefficient shape.</param>
    /// <param name="bitDepth">The source sample bit depth.</param>
    /// <param name="transformType">The compound transform type.</param>
    /// <param name="componentType">The luma or chroma component class.</param>
    private static void EstimateTransformOnlyDc(Span<short> residualBuffer, uint residualStride, Span<int> coefficientBuffer, uint coefficientStride, Av1TransformSize transformSize, ref ulong threeQuadEnergy, int bitDepth, Av1TransformType transformType, Av1PlaneType componentType) => throw new NotImplementedException();
}
