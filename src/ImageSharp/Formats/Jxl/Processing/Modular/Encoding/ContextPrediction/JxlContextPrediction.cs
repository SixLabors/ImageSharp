// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.ContextPrediction;

/// <summary>
/// Context prediction helper methods.
/// </summary>
internal static class JxlContextPrediction
{
    public const int ExtraPropertiesPerChannel = 4;

    public const int NumberOfProperties = 1;

    public static void SetPredictorMode(int i, JxlModularHeader header)
    {
        Span<uint> w = header.GetW();

        switch (i)
        {
            case 0:
                // ~ lossless16 predictor
                w[0] = 0xd;
                w[1] = 0xc;
                w[2] = 0xc;
                w[3] = 0xc;
                header.P1C = 16;
                header.P2C = 10;
                header.P3Ca = 7;
                header.P3Cb = 7;
                header.P3Cc = 7;
                header.P3Cd = 0;
                header.P3Ce = 0;
                break;

            case 1:
                // ~ default lossless8 predictor
                w[0] = 0xd;
                w[1] = 0xc;
                w[2] = 0xc;
                w[3] = 0xb;
                header.P1C = 8;
                header.P2C = 8;
                header.P3Ca = 4;
                header.P3Cb = 0;
                header.P3Cc = 3;
                header.P3Cd = 23;
                header.P3Ce = 2;
                break;

            case 2:
                // ~ west lossless8 predictor
                w[0] = 0xd;
                w[1] = 0xc;
                w[2] = 0xd;
                w[3] = 0xc;
                header.P1C = 10;
                header.P2C = 9;
                header.P3Ca = 7;
                header.P3Cb = 0;
                header.P3Cc = 0;
                header.P3Cd = 16;
                header.P3Ce = 9;
                break;

            case 3:
                // ~ north lossless8 predictor
                w[0] = 0xd;
                w[1] = 0xd;
                w[2] = 0xc;
                w[3] = 0xc;
                header.P1C = 16;
                header.P2C = 8;
                header.P3Ca = 0;
                header.P3Cb = 16;
                header.P3Cc = 0;
                header.P3Cd = 23;
                header.P3Ce = 0;
                break;

            case 4:
            default:
                // something else, because why not
                w[0] = 0xd;
                w[1] = 0xc;
                w[2] = 0xc;
                w[3] = 0xc;
                header.P1C = 10;
                header.P2C = 10;
                header.P3Ca = 5;
                header.P3Cb = 5;
                header.P3Cc = 5;
                header.P3Cd = 12;
                header.P3Ce = 4;
                break;
        }
    }

    /// <summary>
    /// Returns true if the (meta)predictor makes use of the weighted predictor.
    /// </summary>
    /// <param name="predictor">The input predictor.</param>
    /// <returns>Value indicating whether the predictor uses weighted prediction.</returns>
    public static bool IsWeightedPredictor(JxlPredictor predictor) => predictor switch
    {
        JxlPredictor.Zero or
            JxlPredictor.Left or
            JxlPredictor.Top or
            JxlPredictor.Average0 or
            JxlPredictor.Select or
            JxlPredictor.Gradient => false,

        JxlPredictor.Weighted => true,

        JxlPredictor.TopRight or
            JxlPredictor.TopLeft or
            JxlPredictor.LeftLeft or
            JxlPredictor.Average1 or
            JxlPredictor.Average2 or
            JxlPredictor.Average3 or
            JxlPredictor.Average4 => false,

        JxlPredictor.Best or
            JxlPredictor.Variable => true,

        _ => false,
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ClampedGradient(int n, int w, int l)
    {
        int min = Math.Min(n, w);
        int max = Math.Max(n, w);

        int gradient = n + w - l;

        int clamp = l < min ? max : gradient;
        return l > max ? min : clamp;
    }

    // This is actually a simple Paeth predictor, we'd often see
    // this in PNG files
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Select(int a, int b, int c)
    {
        int p = a + b - c;
        int pa = Numerics.Abs(p - a);
        int pb = Numerics.Abs(p - b);
        return pa < pb ? a : b;
    }

    /// <summary>
    /// Prepares the current row's reference properties in storage borrowed before the channel traversal.
    /// </summary>
    /// <param name="channel">The channel whose samples will be predicted.</param>
    /// <param name="y">The current row.</param>
    /// <param name="image">The image containing already reconstructed reference channels.</param>
    /// <param name="i">The current channel index. Only earlier channels can supply references.</param>
    /// <param name="references">The reference-record dimensions and physical stride.</param>
    /// <param name="referenceStorage">The caller-borrowed reference plane, cleared and reused for this row.</param>
    public static void PrecomputeReferences(
        JxlModularChannel channel,
        int y,
        JxlModularImage image,
        int i,
        JxlModularChannel references,
        Span<int> referenceStorage)
    {
        referenceStorage.Clear();
        int offset = 0;
        int numExtraProps = references.Width;
        int oneRow = references.Plane.PixelsPerRow;
        JxlModularChannel channelI = image.Channels[i];

        for (int j = i - 1; j >= 0 && offset < numExtraProps; j--)
        {
            JxlModularChannel channelJ = image.Channels[j];

            if (channelJ.Width != channelI.Width || channelJ.Height != channelI.Height)
            {
                continue;
            }

            if (channelJ.HorizontalShift != channelI.HorizontalShift ||
                channelJ.VerticalShift != channelI.VerticalShift)
            {
                continue;
            }

            Span<int> source = channelJ.GetRow(0);
            int sourceStride = channelJ.Plane.PixelsPerRow;
            Span<int> rpp = source[(y * sourceStride)..];
            Span<int> rpprev = source[((y > 0 ? y - 1 : 0) * sourceStride)..];

            for (int x = 0; x < channel.Width; x++)
            {
                // A reference record is one row of this transposed plane. Address it directly; advancing a final prefixed span can exceed the allocation.
                Span<int> rp = referenceStorage.Slice((x * oneRow) + offset, ExtraPropertiesPerChannel);
                int v = rpp[x];
                rp[0] = Numerics.Abs(v);
                rp[1] = v;

                // Neighboring variables
                int vleft = x > 0 ? rpp[x - 1] : 0;
                int vtop = y > 0 ? rpprev[x] : vleft;
                int vtopleft = x > 0 && y > 0 ? rpprev[x - 1] : vleft;

                // Prediction
                int vpredicted = ClampedGradient(vleft, vtop, vtopleft);
                rp[2] = Numerics.Abs(v - vpredicted);
                rp[3] = v - vpredicted;
            }

            offset += ExtraPropertiesPerChannel;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void InitializePropertiesForRow(Span<int> properties, InlineArray2<int> staticProperties, int y)
    {
        properties[0] = staticProperties[0];
        properties[1] = staticProperties[1];
        properties[2] = y;
        properties[9] = 0; // Local gradient
    }

    // Prediction for one pixel using neighbors
    [MethodImpl(InliningOptions.HotPath)] // This method is called frequently
    public static int PredictOne(
        JxlPredictor predictor,
        int left,
        int top,
        int toptop,
        int topleft,
        int topright,
        int leftleft,
        int toprightright,
        int weightedPrediction) => predictor switch
        {
            JxlPredictor.Zero => 0,
            JxlPredictor.Left => left,
            JxlPredictor.Top => top,
            JxlPredictor.Select => Select(left, top, topleft),
            JxlPredictor.Weighted => weightedPrediction,
            JxlPredictor.Gradient => ClampedGradient(left, top, topleft),
            JxlPredictor.TopLeft => topleft,
            JxlPredictor.TopRight => topright,
            JxlPredictor.LeftLeft => leftleft,
            JxlPredictor.Average0 => (left + top) / 2,
            JxlPredictor.Average1 => (left + topleft) / 2,
            JxlPredictor.Average2 => (topleft + top) / 2,
            JxlPredictor.Average3 => (top + topright) / 2,
            JxlPredictor.Average4 => ((6 * top) - (2 * toptop) + (7 * left) + (1 * leftleft) +
                                    (1 * toprightright) + (3 * topright) + 8) /
                                   16,
            _ => 0,
        };

    /// <summary>
    /// Predicts a modular sample and evaluates the context properties selected by the mode.
    /// </summary>
    /// <param name="mode">The prediction and property-evaluation flags.</param>
    /// <param name="properties">The destination context properties.</param>
    /// <param name="width">The number of samples in the current row.</param>
    /// <param name="currentSample">The reference used as the origin for neighboring samples.</param>
    /// <param name="rowStride">The distance between row starts, in samples.</param>
    /// <param name="x">The current sample column.</param>
    /// <param name="y">The current sample row.</param>
    /// <param name="predictor">The predictor used when tree selection is disabled.</param>
    /// <param name="treeLookup">The optional prediction tree.</param>
    /// <param name="referenceProperties">The exact precomputed reference-property record for this pixel, empty when no reference properties are used.</param>
    /// <param name="weightedPredictorState">The optional weighted-prediction owner.</param>
    /// <param name="weightedStorage">The borrowed prediction storage, empty when weighted prediction is disabled.</param>
    /// <param name="weightedStateIndex">The independently live channel slot when weighted prediction is enabled.</param>
    /// <param name="predictions">The destination for all predictor results when requested by the mode.</param>
    /// <returns>The predicted value and entropy context.</returns>
    public static JxlPredictionResult Predict(
        JxlPredictorMode mode,
        Span<int> properties,
        int width,
        ref int currentSample,
        int rowStride,
        int x,
        int y,
        JxlPredictor predictor,
        JxlMaTreeLookup? treeLookup,
        ReadOnlySpan<int> referenceProperties,
        JxlModularState? weightedPredictorState,
        Span<uint> weightedStorage,
        int weightedStateIndex,
        Span<int> predictions)
    {
        int offset = 3; // Start at position 3 because of 2 static properties + y

        // Status flags
        bool computeProperties = (mode & JxlPredictorMode.UseTree) != 0 || (mode & JxlPredictorMode.ForceComputeProperties) != 0;
        bool skipEdgeChecks = (mode & JxlPredictorMode.NoEdgeCases) != 0;

        // Interior callers omit edge checks only when every addressed neighbor exists.
        // At the left edge use north, or zero on the first row. Missing north and diagonal samples use the nearest available neighbor.
        int left = skipEdgeChecks || x > 0 ? Unsafe.Subtract(ref currentSample, 1) : (y > 0 ? Unsafe.Subtract(ref currentSample, rowStride) : 0);
        int top = skipEdgeChecks || y > 0 ? Unsafe.Subtract(ref currentSample, rowStride) : left;
        int topleft = skipEdgeChecks || (x > 0 && y > 0) ? Unsafe.Add(ref currentSample, -1 - rowStride) : left;
        int topright = skipEdgeChecks || (x + 1 < width && y > 0) ? Unsafe.Add(ref currentSample, 1 - rowStride) : top;
        int leftleft = skipEdgeChecks || x > 1 ? Unsafe.Subtract(ref currentSample, 2) : left;
        int toptop = skipEdgeChecks || y > 1 ? Unsafe.Add(ref currentSample, -rowStride - rowStride) : top;
        int toprightright = skipEdgeChecks || (x + 2 < width && y > 0) ? Unsafe.Add(ref currentSample, 2 - rowStride) : topright;

        if (computeProperties)
        {
            properties[offset++] = x;
            properties[offset++] = top > 0 ? top : -top;
            properties[offset++] = left > 0 ? left : -left;
            properties[offset++] = top;
            properties[offset++] = left;

            // Local gradient
            properties[offset] = left - properties[offset + 1];
            offset++;

            // Local gradient
            properties[offset++] = left + top - topleft;

            // FFV1 context properties
            properties[offset++] = left - topleft;
            properties[offset++] = topleft - top;
            properties[offset++] = top - topright;
            properties[offset++] = top - toptop;
            properties[offset++] = left - leftleft;
        }

        // Predicted weighted prediction value
        int weightedPrediction = 0;

        if ((mode & JxlPredictorMode.UseWeightedPrediction) != 0)
        {
            if (weightedPredictorState is null)
            {
                throw new InvalidOperationException("Weighted prediction state is missing");
            }

            weightedPrediction = unchecked((int)weightedPredictorState.Predict(
                computeProperties,
                x,
                y,
                weightedStorage,
                weightedStateIndex,
                top,
                left,
                topright,
                topleft,
                toptop,
                properties,
                offset));
        }

        if (!skipEdgeChecks && computeProperties)
        {
            offset += NumberOfProperties;

            // The caller slices one pixel's reference record from storage borrowed before the row loop.
            foreach (int value in referenceProperties)
            {
                properties[offset++] = value;
            }
        }

        JxlPredictionResult predResult = default;

        if ((mode & JxlPredictorMode.UseTree) != 0)
        {
            if (treeLookup is null)
            {
                throw new InvalidOperationException("Lookup is missing");
            }

            JxlMaTreeLookupResult result = treeLookup.Lookup(properties);
            predictor = result.Predictor;
            predResult = new((int)result.Context, result.Offset, default, result.Multiplier);
        }

        if ((mode & JxlPredictorMode.AllPredictions) != 0)
        {
            for (int i = 0; i < JxlPredictorFacts.ModularPredictors; i++)
            {
                predictions[i] = PredictOne((JxlPredictor)i, left, top, toptop, topleft, topright, leftleft, toprightright, weightedPrediction);
            }
        }

        predResult = new(
            predResult.Context,
            predResult.Guess + PredictOne(predictor, left, top, toptop, topleft, topright, leftleft, toprightright, weightedPrediction),
            predictor,
            predResult.Multiplier);

        return predResult;
    }

    // The following methods are just wrappers over the Predict
    // method.
    // See https://github.com/libjxl/libjxl/blob/main/lib/jxl/modular/encoding/context_predict.h#L593-L709
    /// <summary>
    /// Predicts a sample using the fixed mode selected by this entry point.
    /// </summary>
    /// <param name="width">The number of samples in the current row.</param>
    /// <param name="currentSample">The reference used as the origin for neighboring samples.</param>
    /// <param name="rowStride">The distance between row starts, in samples.</param>
    /// <param name="x">The current sample column.</param>
    /// <param name="y">The current sample row.</param>
    /// <param name="predictor">The predictor to evaluate.</param>
    /// <returns>The predicted sample and entropy context.</returns>
    public static JxlPredictionResult PredictNoTreeNoWeightedPrediction(
        int width,
        ref int currentSample,
        int rowStride,
        int x,
        int y,
        JxlPredictor predictor)
        => Predict(0, [], width, ref currentSample, rowStride, x, y, predictor, null, [], null, [], 0, []);

    /// <summary>
    /// Predicts a sample using the fixed mode selected by this entry point.
    /// </summary>
    /// <param name="width">The number of samples in the current row.</param>
    /// <param name="currentSample">The reference used as the origin for neighboring samples.</param>
    /// <param name="rowStride">The distance between row starts, in samples.</param>
    /// <param name="x">The current sample column.</param>
    /// <param name="y">The current sample row.</param>
    /// <param name="predictor">The predictor to evaluate.</param>
    /// <param name="weightedPredictorState">The operation's weighted-prediction owner.</param>
    /// <param name="weightedStorage">The storage borrowed when the channel or palette histories were reset.</param>
    /// <param name="weightedStateIndex">The independently live channel slot, zero for a sequential channel.</param>
    /// <returns>The predicted sample and entropy context.</returns>
    public static JxlPredictionResult PredictNoTreeWeightedPrediction(
        int width,
        ref int currentSample,
        int rowStride,
        int x,
        int y,
        JxlPredictor predictor,
        JxlModularState weightedPredictorState,
        Span<uint> weightedStorage,
        int weightedStateIndex)
        => Predict(
            JxlPredictorMode.UseWeightedPrediction,
            [],
            width,
            ref currentSample,
            rowStride,
            x,
            y,
            predictor,
            null,
            [],
            weightedPredictorState,
            weightedStorage,
            weightedStateIndex,
            []);

    /// <summary>
    /// Predicts a sample using the fixed mode selected by this entry point.
    /// </summary>
    /// <param name="properties">The destination context properties.</param>
    /// <param name="width">The number of samples in the current row.</param>
    /// <param name="currentSample">The reference used as the origin for neighboring samples.</param>
    /// <param name="rowStride">The distance between row starts, in samples.</param>
    /// <param name="x">The current sample column.</param>
    /// <param name="y">The current sample row.</param>
    /// <param name="treeLookup">The prediction tree.</param>
    /// <param name="referenceProperties">The exact precomputed reference-property record for this pixel, empty when no reference properties are used.</param>
    /// <returns>The predicted sample and entropy context.</returns>
    public static JxlPredictionResult PredictTreeNoWeightedPrediction(
        Span<int> properties,
        int width,
        ref int currentSample,
        int rowStride,
        int x,
        int y,
        JxlMaTreeLookup treeLookup,
        ReadOnlySpan<int> referenceProperties)
        => Predict(
            JxlPredictorMode.UseTree,
            properties,
            width,
            ref currentSample,
            rowStride,
            x,
            y,
            JxlPredictor.Zero,
            treeLookup,
            referenceProperties,
            null,
            [],
            0,
            []);

    /// <summary>
    /// Predicts a sample using the fixed mode selected by this entry point.
    /// </summary>
    /// <param name="properties">The destination context properties.</param>
    /// <param name="width">The number of samples in the current row.</param>
    /// <param name="currentSample">The reference used as the origin for neighboring samples.</param>
    /// <param name="rowStride">The distance between row starts, in samples.</param>
    /// <param name="x">The current sample column.</param>
    /// <param name="y">The current sample row.</param>
    /// <param name="treeLookup">The prediction tree.</param>
    /// <param name="referenceProperties">The exact precomputed reference-property record for this pixel, empty when no reference properties are used.</param>
    /// <returns>The predicted sample and entropy context.</returns>
    public static JxlPredictionResult PredictTreeNoWeightedPredictionNoEdgeCases(
        Span<int> properties,
        int width,
        ref int currentSample,
        int rowStride,
        int x,
        int y,
        JxlMaTreeLookup treeLookup,
        ReadOnlySpan<int> referenceProperties)
        => Predict(
            JxlPredictorMode.UseTree | JxlPredictorMode.NoEdgeCases,
            properties,
            width,
            ref currentSample,
            rowStride,
            x,
            y,
            JxlPredictor.Zero,
            treeLookup,
            referenceProperties,
            null,
            [],
            0,
            []);

    /// <summary>
    /// Predicts a sample using the fixed mode selected by this entry point.
    /// </summary>
    /// <param name="properties">The destination context properties.</param>
    /// <param name="width">The number of samples in the current row.</param>
    /// <param name="currentSample">The reference used as the origin for neighboring samples.</param>
    /// <param name="rowStride">The distance between row starts, in samples.</param>
    /// <param name="x">The current sample column.</param>
    /// <param name="y">The current sample row.</param>
    /// <param name="treeLookup">The prediction tree.</param>
    /// <param name="referenceProperties">The exact precomputed reference-property record for this pixel, empty when no reference properties are used.</param>
    /// <param name="weightedPredictorState">The operation's weighted-prediction owner.</param>
    /// <param name="weightedStorage">The storage borrowed when the channel or palette histories were reset.</param>
    /// <param name="weightedStateIndex">The independently live channel slot, zero for a sequential channel.</param>
    /// <returns>The predicted sample and entropy context.</returns>
    public static JxlPredictionResult PredictTreeWeightedPrediction(
        Span<int> properties,
        int width,
        ref int currentSample,
        int rowStride,
        int x,
        int y,
        JxlMaTreeLookup treeLookup,
        ReadOnlySpan<int> referenceProperties,
        JxlModularState weightedPredictorState,
        Span<uint> weightedStorage,
        int weightedStateIndex)
        => Predict(
            JxlPredictorMode.UseTree | JxlPredictorMode.UseWeightedPrediction,
            properties,
            width,
            ref currentSample,
            rowStride,
            x,
            y,
            JxlPredictor.Zero,
            treeLookup,
            referenceProperties,
            weightedPredictorState,
            weightedStorage,
            weightedStateIndex,
            []);

    /// <summary>
    /// Predicts a sample using the fixed mode selected by this entry point.
    /// </summary>
    /// <param name="properties">The destination context properties.</param>
    /// <param name="width">The number of samples in the current row.</param>
    /// <param name="currentSample">The reference used as the origin for neighboring samples.</param>
    /// <param name="rowStride">The distance between row starts, in samples.</param>
    /// <param name="x">The current sample column.</param>
    /// <param name="y">The current sample row.</param>
    /// <param name="treeLookup">The prediction tree.</param>
    /// <param name="referenceProperties">The exact precomputed reference-property record for this pixel, empty when no reference properties are used.</param>
    /// <param name="weightedPredictorState">The operation's weighted-prediction owner.</param>
    /// <param name="weightedStorage">The storage borrowed when the channel or palette histories were reset.</param>
    /// <param name="weightedStateIndex">The independently live channel slot, zero for a sequential channel.</param>
    /// <returns>The predicted sample and entropy context.</returns>
    public static JxlPredictionResult PredictTreeWeightedPredictionNoEdgeCases(
        Span<int> properties,
        int width,
        ref int currentSample,
        int rowStride,
        int x,
        int y,
        JxlMaTreeLookup treeLookup,
        ReadOnlySpan<int> referenceProperties,
        JxlModularState weightedPredictorState,
        Span<uint> weightedStorage,
        int weightedStateIndex)
        => Predict(
            JxlPredictorMode.UseTree | JxlPredictorMode.UseWeightedPrediction | JxlPredictorMode.NoEdgeCases,
            properties,
            width,
            ref currentSample,
            rowStride,
            x,
            y,
            JxlPredictor.Zero,
            treeLookup,
            referenceProperties,
            weightedPredictorState,
            weightedStorage,
            weightedStateIndex,
            []);

    /// <summary>
    /// Predicts a sample using the fixed mode selected by this entry point.
    /// </summary>
    /// <param name="properties">The destination context properties.</param>
    /// <param name="width">The number of samples in the current row.</param>
    /// <param name="currentSample">The reference used as the origin for neighboring samples.</param>
    /// <param name="rowStride">The distance between row starts, in samples.</param>
    /// <param name="x">The current sample column.</param>
    /// <param name="y">The current sample row.</param>
    /// <param name="predictor">The predictor to evaluate.</param>
    /// <param name="referenceProperties">The exact precomputed reference-property record for this pixel, empty when no reference properties are used.</param>
    /// <param name="weightedPredictorState">The operation's weighted-prediction owner.</param>
    /// <param name="weightedStorage">The storage borrowed when the channel or palette histories were reset.</param>
    /// <param name="weightedStateIndex">The independently live channel slot, zero for a sequential channel.</param>
    /// <returns>The predicted sample and entropy context.</returns>
    public static JxlPredictionResult PredictLearn(
        Span<int> properties,
        int width,
        ref int currentSample,
        int rowStride,
        int x,
        int y,
        JxlPredictor predictor,
        ReadOnlySpan<int> referenceProperties,
        JxlModularState weightedPredictorState,
        Span<uint> weightedStorage,
        int weightedStateIndex)
        => Predict(
            JxlPredictorMode.ForceComputeProperties | JxlPredictorMode.UseWeightedPrediction,
            properties,
            width,
            ref currentSample,
            rowStride,
            x,
            y,
            predictor,
            null,
            referenceProperties,
            weightedPredictorState,
            weightedStorage,
            weightedStateIndex,
            []);

    /// <summary>
    /// Predicts a sample using the fixed mode selected by this entry point.
    /// </summary>
    /// <param name="properties">The destination context properties.</param>
    /// <param name="width">The number of samples in the current row.</param>
    /// <param name="currentSample">The reference used as the origin for neighboring samples.</param>
    /// <param name="rowStride">The distance between row starts, in samples.</param>
    /// <param name="x">The current sample column.</param>
    /// <param name="y">The current sample row.</param>
    /// <param name="referenceProperties">The exact precomputed reference-property record for this pixel, empty when no reference properties are used.</param>
    /// <param name="weightedPredictorState">The operation's weighted-prediction owner.</param>
    /// <param name="weightedStorage">The storage borrowed when the channel or palette histories were reset.</param>
    /// <param name="weightedStateIndex">The independently live channel slot, zero for a sequential channel.</param>
    /// <param name="predictions">The destination for all predictor results.</param>
    /// <returns>The predicted sample and entropy context.</returns>
    public static JxlPredictionResult PredictLearnAll(
        Span<int> properties,
        int width,
        ref int currentSample,
        int rowStride,
        int x,
        int y,
        ReadOnlySpan<int> referenceProperties,
        JxlModularState weightedPredictorState,
        Span<uint> weightedStorage,
        int weightedStateIndex,
        Span<int> predictions)
        => Predict(
            JxlPredictorMode.ForceComputeProperties | JxlPredictorMode.UseWeightedPrediction | JxlPredictorMode.AllPredictions,
            properties,
            width,
            ref currentSample,
            rowStride,
            x,
            y,
            JxlPredictor.Zero,
            null,
            referenceProperties,
            weightedPredictorState,
            weightedStorage,
            weightedStateIndex,
            predictions);

    /// <summary>
    /// Predicts a sample using the fixed mode selected by this entry point.
    /// </summary>
    /// <param name="properties">The destination context properties.</param>
    /// <param name="width">The number of samples in the current row.</param>
    /// <param name="currentSample">The reference used as the origin for neighboring samples.</param>
    /// <param name="rowStride">The distance between row starts, in samples.</param>
    /// <param name="x">The current sample column.</param>
    /// <param name="y">The current sample row.</param>
    /// <param name="predictor">The predictor to evaluate.</param>
    /// <param name="referenceProperties">The exact precomputed reference-property record for this pixel, empty when no reference properties are used.</param>
    /// <param name="weightedPredictorState">The operation's weighted-prediction owner.</param>
    /// <param name="weightedStorage">The storage borrowed when the channel or palette histories were reset.</param>
    /// <param name="weightedStateIndex">The independently live channel slot, zero for a sequential channel.</param>
    /// <returns>The predicted sample and entropy context.</returns>
    public static JxlPredictionResult PredictLearnNoEdgeCases(
        Span<int> properties,
        int width,
        ref int currentSample,
        int rowStride,
        int x,
        int y,
        JxlPredictor predictor,
        ReadOnlySpan<int> referenceProperties,
        JxlModularState weightedPredictorState,
        Span<uint> weightedStorage,
        int weightedStateIndex)
        => Predict(
            JxlPredictorMode.ForceComputeProperties | JxlPredictorMode.UseWeightedPrediction | JxlPredictorMode.NoEdgeCases,
            properties,
            width,
            ref currentSample,
            rowStride,
            x,
            y,
            predictor,
            null,
            referenceProperties,
            weightedPredictorState,
            weightedStorage,
            weightedStateIndex,
            []);

    /// <summary>
    /// Predicts a sample using the fixed mode selected by this entry point.
    /// </summary>
    /// <param name="properties">The destination context properties.</param>
    /// <param name="width">The number of samples in the current row.</param>
    /// <param name="currentSample">The reference used as the origin for neighboring samples.</param>
    /// <param name="rowStride">The distance between row starts, in samples.</param>
    /// <param name="x">The current sample column.</param>
    /// <param name="y">The current sample row.</param>
    /// <param name="referenceProperties">The exact precomputed reference-property record for this pixel, empty when no reference properties are used.</param>
    /// <param name="weightedPredictorState">The operation's weighted-prediction owner.</param>
    /// <param name="weightedStorage">The storage borrowed when the channel or palette histories were reset.</param>
    /// <param name="weightedStateIndex">The independently live channel slot, zero for a sequential channel.</param>
    /// <param name="predictions">The destination for all predictor results.</param>
    /// <returns>The predicted sample and entropy context.</returns>
    public static JxlPredictionResult PredictLearnAllNoEdgeCases(
        Span<int> properties,
        int width,
        ref int currentSample,
        int rowStride,
        int x,
        int y,
        ReadOnlySpan<int> referenceProperties,
        JxlModularState weightedPredictorState,
        Span<uint> weightedStorage,
        int weightedStateIndex,
        Span<int> predictions)
        => Predict(
            JxlPredictorMode.ForceComputeProperties | JxlPredictorMode.UseWeightedPrediction | JxlPredictorMode.AllPredictions | JxlPredictorMode.NoEdgeCases,
            properties,
            width,
            ref currentSample,
            rowStride,
            x,
            y,
            JxlPredictor.Zero,
            null,
            referenceProperties,
            weightedPredictorState,
            weightedStorage,
            weightedStateIndex,
            predictions);

    /// <summary>
    /// Predicts a sample using the fixed mode selected by this entry point.
    /// </summary>
    /// <param name="width">The number of samples in the current row.</param>
    /// <param name="currentSample">The reference used as the origin for neighboring samples.</param>
    /// <param name="rowStride">The distance between row starts, in samples.</param>
    /// <param name="x">The current sample column.</param>
    /// <param name="y">The current sample row.</param>
    /// <param name="predictions">The destination for all predictor results.</param>
    /// <returns>The predicted sample and entropy context.</returns>
    public static JxlPredictionResult PredictAllNoWeightedPrediction(
        int width,
        ref int currentSample,
        int rowStride,
        int x,
        int y,
        Span<int> predictions)
        => Predict(JxlPredictorMode.AllPredictions, [], width, ref currentSample, rowStride, x, y, JxlPredictor.Zero, null, [], null, [], 0, predictions);
}
