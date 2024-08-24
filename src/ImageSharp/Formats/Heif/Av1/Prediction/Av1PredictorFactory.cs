// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

internal class Av1PredictorFactory
{
    internal static void DcPredictor(bool hasLeft, bool hasAbove, Av1TransformSize transformSize, ref byte destination, nuint destinationStride, Span<byte> aboveRow, Span<byte> leftColumn)
    {
        if (hasLeft)
        {
            if (hasAbove)
            {
                Av1DcPredictor.PredictScalar(transformSize, ref destination, destinationStride, ref aboveRow[0], ref leftColumn[0]);
            }
            else
            {
                Av1DcLeftPredictor.PredictScalar(transformSize, ref destination, destinationStride, ref aboveRow[0], ref leftColumn[0]);
            }
        }
        else
        {
            if (hasAbove)
            {
                Av1DcTopPredictor.PredictScalar(transformSize, ref destination, destinationStride, ref aboveRow[0], ref leftColumn[0]);
            }
            else
            {
                Av1DcFillPredictor.PredictScalar(transformSize, ref destination, destinationStride, ref aboveRow[0], ref leftColumn[0]);
            }
        }
    }

    internal static void DirectionalPredictor(ref byte destination, nuint destinationStride, Av1TransformSize transformSize, Span<byte> aboveRow, Span<byte> leftColumn, bool upsampleAbove, bool upsampleLeft, int angle) => throw new NotImplementedException();

    internal static void FilterIntraPredictor(ref byte destination, nuint destinationStride, Av1TransformSize transformSize, Span<byte> aboveRow, Span<byte> leftColumn, Av1FilterIntraMode filterIntraMode) => throw new NotImplementedException();

    internal static void GeneralPredictor(Av1PredictionMode mode, Av1TransformSize transformSize, ref byte destination, nuint destinationStride, Span<byte> aboveRow, Span<byte> leftColumn) => throw new NotImplementedException();
}
