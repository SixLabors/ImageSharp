// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

internal static class Av1ForwardTransformerFactory
{
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

    private static void EstimateTransformN2(Span<short> residualBuffer, uint residualStride, Span<int> coefficientBuffer, uint coefficientStride, Av1TransformSize transformSize, ref ulong threeQuadEnergy, int bitDepth, Av1TransformType transformType, Av1PlaneType componentType) => throw new NotImplementedException();

    private static void EstimateTransformN4(Span<short> residualBuffer, uint residualStride, Span<int> coefficientBuffer, uint coefficientStride, Av1TransformSize transformSize, ref ulong threeQuadEnergy, int bitDepth, Av1TransformType transformType, Av1PlaneType componentType) => throw new NotImplementedException();

    private static void EstimateTransformOnlyDc(Span<short> residualBuffer, uint residualStride, Span<int> coefficientBuffer, uint coefficientStride, Av1TransformSize transformSize, ref ulong threeQuadEnergy, int bitDepth, Av1TransformType transformType, Av1PlaneType componentType) => throw new NotImplementedException();
}
