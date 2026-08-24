// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

/// <summary>
/// Selects the scalar 8-bit or high-bit-depth AV1 intra predictor for a decoded prediction mode.
/// </summary>
internal class Av1PredictorFactory
{
    /// <summary>
    /// The Q8 directional derivatives indexed by acute angle in degrees; zero entries represent angles AV1 does not signal.
    /// </summary>
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

    /// <summary>
    /// Predicts an 8-bit block from the average of whichever top and left neighbor edges are available.
    /// </summary>
    /// <param name="hasLeft">Whether the left neighboring column is available.</param>
    /// <param name="hasAbove">Whether the top neighboring row is available.</param>
    /// <param name="transformSize">The predicted block dimensions.</param>
    /// <param name="destination">The destination block.</param>
    /// <param name="destinationStride">The distance, in samples, between destination rows.</param>
    /// <param name="aboveRow">The top neighboring samples.</param>
    /// <param name="leftColumn">The left neighboring samples.</param>
    public static void DcPredictor(bool hasLeft, bool hasAbove, Av1TransformSize transformSize, Span<byte> destination, nuint destinationStride, Span<byte> aboveRow, Span<byte> leftColumn)
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
    /// Predicts a high-bit-depth block from the average of whichever top and left neighbor edges are available.
    /// </summary>
    /// <param name="hasLeft">Whether the left neighboring column is available.</param>
    /// <param name="hasAbove">Whether the top neighboring row is available.</param>
    /// <param name="transformSize">The predicted block dimensions.</param>
    /// <param name="destination">The destination block.</param>
    /// <param name="destinationStride">The distance, in samples, between destination rows.</param>
    /// <param name="aboveRow">The top neighboring samples.</param>
    /// <param name="leftColumn">The left neighboring samples.</param>
    /// <param name="bitDepth">The coded sample bit depth used to select the midpoint when no edge is available.</param>
    public static void DcPredictor(bool hasLeft, bool hasAbove, Av1TransformSize transformSize, Span<short> destination, nuint destinationStride, Span<short> aboveRow, Span<short> leftColumn, int bitDepth)
        => Av1HighBitDepthPredictor.DcPredictor(hasLeft, hasAbove, transformSize, destination, destinationStride, aboveRow, leftColumn, bitDepth);

    /// <summary>
    /// Predicts an 8-bit block by projecting reference-edge samples along a directional angle.
    /// </summary>
    /// <param name="destination">The destination block.</param>
    /// <param name="stride">The distance, in samples, between destination rows.</param>
    /// <param name="transformSize">The predicted block dimensions.</param>
    /// <param name="aboveRow">The top and top-right reference samples.</param>
    /// <param name="leftColumn">The left and bottom-left reference samples.</param>
    /// <param name="upsampleAbove">Whether the top reference edge is stored at half-sample intervals.</param>
    /// <param name="upsampleLeft">Whether the left reference edge is stored at half-sample intervals.</param>
    /// <param name="angle">The prediction angle in degrees from 1 through 269.</param>
    /// <remarks>SVT-AV1: <c>svt_aom_highbd_dr_predictor</c>.</remarks>
    public static void DirectionalPredictor(Span<byte> destination, nuint stride, Av1TransformSize transformSize, Span<byte> aboveRow, Span<byte> leftColumn, bool upsampleAbove, bool upsampleLeft, int angle)
    {
        int dx = GetDeltaX(angle);
        int dy = GetDeltaY(angle);
        int bw = transformSize.GetWidth();
        int bh = transformSize.GetHeight();
        Guard.MustBeBetweenOrEqualTo(angle, 1, 269, nameof(angle));

        // The three open quadrants select which reference edge or edge pair the projected ray intersects.
        // Exact 90- and 180-degree modes reduce to copying the top row or left column without interpolation.
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

    /// <summary>
    /// Predicts a high-bit-depth block by projecting reference-edge samples along a directional angle.
    /// </summary>
    /// <param name="destination">The destination block.</param>
    /// <param name="stride">The distance, in samples, between destination rows.</param>
    /// <param name="transformSize">The predicted block dimensions.</param>
    /// <param name="aboveRow">The top and top-right reference samples.</param>
    /// <param name="leftColumn">The left and bottom-left reference samples.</param>
    /// <param name="upsampleAbove">Whether the top reference edge is stored at half-sample intervals.</param>
    /// <param name="upsampleLeft">Whether the left reference edge is stored at half-sample intervals.</param>
    /// <param name="angle">The prediction angle in degrees from 1 through 269.</param>
    /// <param name="bitDepth">The coded sample bit depth used to clamp interpolated values.</param>
    public static void DirectionalPredictor(Span<short> destination, nuint stride, Av1TransformSize transformSize, Span<short> aboveRow, Span<short> leftColumn, bool upsampleAbove, bool upsampleLeft, int angle, int bitDepth)
        => Av1HighBitDepthPredictor.DirectionalPredictor(destination, stride, transformSize, aboveRow, leftColumn, upsampleAbove, upsampleLeft, angle, bitDepth);

    /// <summary>
    /// Predicts an 8-bit block using the selected AV1 filter-intra kernel.
    /// </summary>
    /// <param name="destination">The destination block.</param>
    /// <param name="destinationStride">The distance, in samples, between destination rows.</param>
    /// <param name="transformSize">The predicted block dimensions.</param>
    /// <param name="aboveRow">The top neighboring samples.</param>
    /// <param name="leftColumn">The left neighboring samples.</param>
    /// <param name="filterIntraMode">The filter-intra coefficient set.</param>
    public static void FilterIntraPredictor(Span<byte> destination, nuint destinationStride, Av1TransformSize transformSize, Span<byte> aboveRow, Span<byte> leftColumn, Av1FilterIntraMode filterIntraMode)
        => Av1FilterIntraPredictor.Predict(destination, destinationStride, transformSize, aboveRow, leftColumn, filterIntraMode);

    /// <summary>
    /// Predicts a high-bit-depth block using the selected AV1 filter-intra kernel.
    /// </summary>
    /// <param name="destination">The destination block.</param>
    /// <param name="destinationStride">The distance, in samples, between destination rows.</param>
    /// <param name="transformSize">The predicted block dimensions.</param>
    /// <param name="aboveRow">The top neighboring samples.</param>
    /// <param name="leftColumn">The left neighboring samples.</param>
    /// <param name="filterIntraMode">The filter-intra coefficient set.</param>
    /// <param name="bitDepth">The coded sample bit depth used to clamp filtered values.</param>
    public static void FilterIntraPredictor(Span<short> destination, nuint destinationStride, Av1TransformSize transformSize, Span<short> aboveRow, Span<short> leftColumn, Av1FilterIntraMode filterIntraMode, int bitDepth)
        => Av1HighBitDepthPredictor.FilterIntraPredictor(destination, destinationStride, transformSize, aboveRow, leftColumn, filterIntraMode, bitDepth);

    /// <summary>
    /// Selects an 8-bit horizontal, vertical, Paeth, or smooth predictor for a non-directional mode.
    /// </summary>
    /// <param name="mode">The non-directional prediction mode.</param>
    /// <param name="transformSize">The predicted block dimensions.</param>
    /// <param name="destination">The destination block.</param>
    /// <param name="destinationStride">The distance, in samples, between destination rows.</param>
    /// <param name="aboveRow">The top neighboring samples.</param>
    /// <param name="leftColumn">The left neighboring samples.</param>
    public static void GeneralPredictor(Av1PredictionMode mode, Av1TransformSize transformSize, Span<byte> destination, nuint destinationStride, Span<byte> aboveRow, Span<byte> leftColumn)
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

    /// <summary>
    /// Selects a high-bit-depth horizontal, vertical, Paeth, or smooth predictor for a non-directional mode.
    /// </summary>
    /// <param name="mode">The non-directional prediction mode.</param>
    /// <param name="transformSize">The predicted block dimensions.</param>
    /// <param name="destination">The destination block.</param>
    /// <param name="destinationStride">The distance, in samples, between destination rows.</param>
    /// <param name="aboveRow">The top neighboring samples.</param>
    /// <param name="leftColumn">The left neighboring samples.</param>
    public static void GeneralPredictor(Av1PredictionMode mode, Av1TransformSize transformSize, Span<short> destination, nuint destinationStride, Span<short> aboveRow, Span<short> leftColumn)
        => Av1HighBitDepthPredictor.GeneralPredictor(mode, transformSize, destination, destinationStride, aboveRow, leftColumn);

    /// <summary>
    /// Gets the Q8 vertical displacement per unit horizontal displacement for a directional angle.
    /// </summary>
    /// <param name="angle">The prediction angle in degrees.</param>
    /// <returns>The Q8 vertical derivative, or one when the selected directional zone does not consume it.</returns>
    public static int GetDeltaY(int angle)
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
            // Zones one and the exact horizontal/vertical modes never consume dy; one avoids a zero placeholder.
            return 1;
        }
    }

    /// <summary>
    /// Gets the Q8 horizontal displacement per unit vertical displacement for a directional angle.
    /// </summary>
    /// <param name="angle">The prediction angle in degrees.</param>
    /// <returns>The Q8 horizontal derivative, or one when the selected directional zone does not consume it.</returns>
    public static int GetDeltaX(int angle)
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
            // Zone three and the exact horizontal/vertical modes never consume dx; one avoids a zero placeholder.
            return 1;
        }
    }
}
