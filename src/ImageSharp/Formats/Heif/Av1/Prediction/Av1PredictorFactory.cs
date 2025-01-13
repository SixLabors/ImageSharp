// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

internal class Av1PredictorFactory
{
    private static readonly int[] DirectionalIntraDerivative = [

        // More evenly spread out angles and limited to 10-bit
        // Values that are 0 will never be used
        //                    Approx angle
        0,    0, 0,        // 0
        1023, 0, 0,        // 3, ...
        547,  0, 0,        // 6, ...
        372,  0, 0, 0, 0,  // 9, ...
        273,  0, 0,        // 14, ...
        215,  0, 0,        // 17, ...
        178,  0, 0,        // 20, ...
        151,  0, 0,        // 23, ... (113 & 203 are base angles)
        132,  0, 0,        // 26, ...
        116,  0, 0,        // 29, ...
        102,  0, 0, 0,     // 32, ...
        90,   0, 0,        // 36, ...
        80,   0, 0,        // 39, ...
        71,   0, 0,        // 42, ...
        64,   0, 0,        // 45, ... (45 & 135 are base angles)
        57,   0, 0,        // 48, ...
        51,   0, 0,        // 51, ...
        45,   0, 0, 0,     // 54, ...
        40,   0, 0,        // 58, ...
        35,   0, 0,        // 61, ...
        31,   0, 0,        // 64, ...
        27,   0, 0,        // 67, ... (67 & 157 are base angles)
        23,   0, 0,        // 70, ...
        19,   0, 0,        // 73, ...
        15,   0, 0, 0, 0,  // 76, ...
        11,   0, 0,        // 81, ...
        7,    0, 0,        // 84, ...
        3,    0, 0,        // 87, ...
    ];

    internal static void DcPredictor(bool hasLeft, bool hasAbove, Av1TransformSize transformSize, Span<byte> destination, nuint destinationStride, Span<byte> aboveRow, Span<byte> leftColumn)
    {
        if (hasLeft)
        {
            if (hasAbove)
            {
                Av1DcPredictor.PredictScalar(transformSize, destination, destinationStride, aboveRow, leftColumn);
            }
            else
            {
                Av1DcLeftPredictor.PredictScalar(transformSize, destination, destinationStride, aboveRow, leftColumn);
            }
        }
        else
        {
            if (hasAbove)
            {
                Av1DcTopPredictor.PredictScalar(transformSize, destination, destinationStride, aboveRow, leftColumn);
            }
            else
            {
                Av1DcFillPredictor.PredictScalar(transformSize, destination, destinationStride, aboveRow, leftColumn);
            }
        }
    }

    /// <summary>
    /// SVT: svt_aom_highbd_dr_predictor
    /// </summary>
    internal static void DirectionalPredictor(Span<byte> destination, nuint stride, Av1TransformSize transformSize, Span<byte> aboveRow, Span<byte> leftColumn, bool upsampleAbove, bool upsampleLeft, int angle)
    {
        int dx = GetDeltaX(angle);
        int dy = GetDeltaY(angle);
        int bw = transformSize.GetWidth();
        int bh = transformSize.GetHeight();
        Guard.MustBeBetweenOrEqualTo(angle, 1, 269, nameof(angle));

        if (angle is > 0 and < 90)
        {
            Av1DirectionalZone1Predictor.PredictScalar(transformSize, destination, stride, aboveRow, upsampleAbove, dx);
        }
        else if (angle is > 90 and < 180)
        {
            Av1DirectionalZone2Predictor.PredictScalar(transformSize, destination, stride, aboveRow, leftColumn, upsampleAbove, upsampleLeft, dx, dy);
        }
        else if (angle is > 180 and < 270)
        {
            Av1DirectionalZone3Predictor.PredictScalar(transformSize, destination, stride, leftColumn, upsampleLeft, dx, dy);
        }
        else if (angle == 90)
        {
            Av1VerticalPredictor.PredictScalar(transformSize, destination, stride, aboveRow, leftColumn);
        }
        else if (angle == 180)
        {
            Av1HorizontalPredictor.PredictScalar(transformSize, destination, stride, aboveRow, leftColumn);
        }
    }

    internal static void FilterIntraPredictor(Span<byte> destination, nuint destinationStride, Av1TransformSize transformSize, Span<byte> aboveRow, Span<byte> leftColumn, Av1FilterIntraMode filterIntraMode) => throw new NotImplementedException();

    internal static void GeneralPredictor(Av1PredictionMode mode, Av1TransformSize transformSize, Span<byte> destination, nuint destinationStride, Span<byte> aboveRow, Span<byte> leftColumn)
    {
        switch (mode)
        {
            case Av1PredictionMode.Horizontal:
                Av1HorizontalPredictor.PredictScalar(transformSize, destination, destinationStride, aboveRow, leftColumn);
                break;
            case Av1PredictionMode.Vertical:
                Av1VerticalPredictor.PredictScalar(transformSize, destination, destinationStride, aboveRow, leftColumn);
                break;
            case Av1PredictionMode.Paeth:
                Av1PaethPredictor.PredictScalar(transformSize, destination, destinationStride, aboveRow, leftColumn);
                break;
            case Av1PredictionMode.Smooth:
                Av1SmoothPredictor.PredictScalar(transformSize, destination, destinationStride, aboveRow, leftColumn);
                break;
            case Av1PredictionMode.SmoothHorizontal:
                Av1SmoothHorizontalPredictor.PredictScalar(transformSize, destination, destinationStride, aboveRow, leftColumn);
                break;
            case Av1PredictionMode.SmoothVertical:
                Av1SmoothVerticalPredictor.PredictScalar(transformSize, destination, destinationStride, aboveRow, leftColumn);
                break;
        }
    }

    // Get the shift (up-scaled by 256) in Y w.r.t a unit change in X.
    // If angle > 0 && angle < 90, dy = 1;
    // If angle > 90 && angle < 180, dy = (int32_t)(256 * t);
    // If angle > 180 && angle < 270, dy = -((int32_t)(256 * t));
    private static int GetDeltaY(int angle)
    {
        if (angle is > 90 and < 180)
        {
            return DirectionalIntraDerivative[angle - 90];
        }
        else if (angle is > 180 and < 270)
        {
            return DirectionalIntraDerivative[270 - angle];
        }
        else
        {
            // In this case, we are not really going to use dy. We may return any value.
            return 1;
        }
    }

    // Get the shift (up-scaled by 256) in X w.r.t a unit change in Y.
    // If angle > 0 && angle < 90, dx = -((int32_t)(256 / t));
    // If angle > 90 && angle < 180, dx = (int32_t)(256 / t);
    // If angle > 180 && angle < 270, dx = 1;
    private static int GetDeltaX(int angle)
    {
        if (angle is > 0 and < 90)
        {
            return DirectionalIntraDerivative[angle];
        }
        else if (angle is > 90 and < 180)
        {
            return DirectionalIntraDerivative[180 - angle];
        }
        else
        {
            // In this case, we are not really going to use dx. We may return any value.
            return 1;
        }
    }
}
