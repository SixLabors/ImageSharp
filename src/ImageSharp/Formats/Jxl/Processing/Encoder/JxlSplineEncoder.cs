// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Ans;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.AuxiliaryOutput;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Splines;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;

internal sealed class JxlSplineEncoder
{
    private static void Tokenize(JxlQuantizedSpline spline, List<JxlToken> tokens)
    {
        tokens.Add(new(JxlSplineEntropyContext.NumControlPoints, (uint)spline.ControlPoints.Length));

        foreach (JxlControlPoint point in spline.ControlPoints.Span)
        {
            tokens.Add(new(JxlSplineEntropyContext.ControlPoints, JxlPackSigned.PackUnsigned(point.First)));
            tokens.Add(new(JxlSplineEntropyContext.ControlPoints, JxlPackSigned.PackUnsigned(point.Second)));
        }

        void EncodeDCT(Span<int> dct)
        {
            for (int i = 0; i < 32; i++)
            {
                tokens.Add(new(JxlSplineEntropyContext.Dct, JxlPackSigned.PackUnsigned(dct[i])));
            }
        }

        foreach (Span<int> dct in spline.ColorDct)
        {
            EncodeDCT(dct);
        }

        EncodeDCT(spline.SigmaDct);
    }

    public static void EncodeAllStartingPoints(Span<PointF> points, List<JxlToken> tokens)
    {
        long lastX = 0;
        long lastY = 0;

        for (int i = 0; i < points.Length; i++)
        {
            long x = (long)MathF.Round(points[i].X, MidpointRounding.AwayFromZero);
            long y = (long)MathF.Round(points[i].Y, MidpointRounding.AwayFromZero);

            if (i == 0)
            {
                tokens.Add(new(JxlSplineEntropyContext.StartingPosition, (uint)x));
                tokens.Add(new(JxlSplineEntropyContext.StartingPosition, (uint)y));
            }
            else
            {
                tokens.Add(new(JxlSplineEntropyContext.StartingPosition, JxlPackSigned.PackUnsigned((int)(x - lastX))));
                tokens.Add(new(JxlSplineEntropyContext.StartingPosition, JxlPackSigned.PackUnsigned((int)(y - lastY))));
            }

            lastX = x;
            lastY = y;
        }
    }

    public static void EncodeSplines(JxlSplines splines, JxlBitWriter writer, JxlLayerType layer, JxlHistogramParameters histogramParameters, JxlAuxiliaryOutput auxOut)
    {
        Span<JxlQuantizedSpline> quantizedSplines = splines.QuantizedSplines;
        List<List<JxlToken>> tokens = [[]];
        tokens[0].Add(new(JxlSplineEntropyContext.NumSplineContexts, (uint)(quantizedSplines.Length - 1)));

        EncodeAllStartingPoints(splines.StartingPoints, tokens[0]);

        tokens[0].Add(new(JxlSplineEntropyContext.QuantizationAdjustment, JxlPackSigned.PackUnsigned(splines.QuantizationAdjustment)));

        foreach (JxlQuantizedSpline spline in quantizedSplines)
        {
            Tokenize(spline, tokens[0]);
        }

        _ = BuildAndEncodeHistograms(writer, histogramParameters, JxlSplineEntropyContext.NumSplineContexts, tokens, out JxlEntropyEncodingData codes, writer, layer, auxOut);
        WriteTokens(tokens[0], codes, 0, writer, layer, auxOut);
    }
}
