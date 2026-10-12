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

    public static void PrecomputeReferences(JxlModularChannel channel, int y, JxlModularImage image, int i, JxlModularChannel references)
    {
        references.Plane.Clear();
        int offset = 0;
        int numExtraProps = references.Width;
        int oneRow = references.Plane.PixelsPerRow;
        JxlModularChannel channelI = image.Channels[i];

        for (int j = i - 1; i >= 0 && offset < numExtraProps; j--)
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

            Span<int> rp = references.GetRow(0)[offset..];
            Span<int> rpp = channelJ.GetRow(y);
            Span<int> rpprev = channelJ.GetRow(y > 0 ? y - 1 : 0);

            for (int x = 0; x < channel.Width; x++, rp = rp[oneRow..])
            {
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
    public static void InitializePropertiesForRow(Span<int> p, InlineArray2<int> staticProperties, int y)
    {
        p[0] = staticProperties[0];
        p[1] = staticProperties[1];
        p[2] = y;
        p[9] = 0; // Local gradient
    }

    // Prediction for one pixel using neighbors
    [MethodImpl(InliningOptions.HotPath)] // This method is called frequently
    public static int PredictOne(
        JxlPredictor p,
        int left,
        int top,
        int toptop,
        int topleft,
        int topright,
        int leftleft,
        int toprightright,
        int wpPred) => p switch
        {
            JxlPredictor.Zero => 0,
            JxlPredictor.Left => left,
            JxlPredictor.Top => top,
            JxlPredictor.Select => Select(left, top, topleft),
            JxlPredictor.Weighted => wpPred,
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

    public static JxlPredictionResult Predict(
        JxlPredictorMode mode,
        Span<int> p, // contains properties
        int w, // block width
        ref int pp, // This is a reference to the output pixel stored in row-major order. Negative offsets are accessed to reference other pixels in the image, specifically neighboring pixles.
        int oneRow, // Number of pixels on one row
        int x,
        int y,
        JxlPredictor predictor,
        JxlMaTreeLookup? lookup,
        JxlModularChannel? references,
        JxlModularState? wpState,
        Span<int> predictions)
    {
        int offset = 3; // Start at position 3 because of 2 static properties + y

        // Status flags
        // computeProperties = should the p (properties) variable be updated?
        // nec = are there no edge cases?
        bool computeProperties = (mode & JxlPredictorMode.UseTree) != 0 || (mode & JxlPredictorMode.ForceComputeProperties) != 0;
        bool nec = (mode & JxlPredictorMode.NoEdgeCases) != 0;

        // The following variables are neighboring pixels relative to the pixel to predict.
        // Pixels may be unavailable and therefore replaced with default values. For example,
        // at Y=0, the top pixel may not be available because we're already at the very top
        // of the image, there's no "above" of that.
        int left = nec || x > 0 ? Unsafe.Subtract(ref pp, 1) : (y > 0 ? Unsafe.Subtract(ref pp, oneRow) : 0); // ⬅️ (or 0 if unavailable)
        int top = nec || y > 0 ? Unsafe.Subtract(ref pp, oneRow) : left; // ⬆️ (or ⬅️ if unavailable)
        int topleft = nec || (x > 0 && y > 0) ? Unsafe.Add(ref pp, -1 - oneRow) : left; // ↗️ (or ⬅️ if unavailable)
        int topright = nec || (x + 1 < w && y > 0) ? Unsafe.Add(ref pp, 1 - oneRow) : top; // ↖️ (or ⬆️ if unavailable)
        int leftleft = nec || x > 1 ? Unsafe.Subtract(ref pp, 2) : left; // ⬅️⬅️ (or ⬅️ if unavailable)
        int toptop = nec || y > 1 ? Unsafe.Add(ref pp, -oneRow - oneRow) : top; // ⬆️⬆️ (or ⬆️ if unavailable)
        int toprightright = nec || (x + 2 < w && y > 0) ? Unsafe.Add(ref pp, 2 - oneRow) : topright; // ↗️➡️ (or ↗️ if unavailable)

        if (computeProperties)
        {
            p[offset++] = x;
            p[offset++] = top > 0 ? top : -top;
            p[offset++] = left > 0 ? left : -left;
            p[offset++] = top;
            p[offset++] = left;

            // Local gradient
            p[offset] = left - p[offset + 1];
            offset++;

            // Local gradient
            p[offset++] = left + top - topleft;

            // FFV1 context properties
            p[offset++] = left - topleft;
            p[offset++] = topleft - top;
            p[offset++] = top - topright;
            p[offset++] = top - toptop;
            p[offset++] = left - leftleft;
        }

        // Predicted weighted prediction value
        int wpPred = 0;

        if ((mode & JxlPredictorMode.UseWeightedPrediction) != 0)
        {
            if (wpState is null)
            {
                throw new InvalidOperationException("Weighted prediction state is missing");
            }

            wpPred = unchecked((int)wpState.Predict(computeProperties, x, y, w, top, left, topright, topleft, toptop, p, offset));
        }

        if (!nec && computeProperties)
        {
            if (references is null)
            {
                throw new InvalidOperationException("References are missing");
            }

            offset += NumberOfProperties;

            // Extra properties
            Span<int> rp = references.GetRow(x);
            for (int i = 0; i < references.Width; i++)
            {
                p[offset++] = rp[i];
            }
        }

        JxlPredictionResult predResult = default;

        if ((mode & JxlPredictorMode.UseTree) != 0)
        {
            if (lookup is null)
            {
                throw new InvalidOperationException("Lookup is missing");
            }

            JxlMaTreeLookupResult result = lookup.Lookup(p);
            predictor = result.Predictor;
            predResult = new((int)result.Context, result.Offset, default, result.Multiplier);
        }

        if ((mode & JxlPredictorMode.AllPredictions) != 0)
        {
            for (int i = 0; i < JxlPredictorFacts.ModularPredictors; i++)
            {
                predictions[i] = PredictOne((JxlPredictor)i, left, top, toptop, topleft, topright, leftleft, toprightright, wpPred);
            }
        }

        predResult = new(
            predResult.Context,
            predResult.Guess + PredictOne(predictor, left, top, toptop, topleft, topright, leftleft, toprightright, wpPred),
            predictor,
            predResult.Multiplier);

        return predResult;
    }

    // The following methods are just wrappers over the Predict
    // method.
    // See https://github.com/libjxl/libjxl/blob/main/lib/jxl/modular/encoding/context_predict.h#L593-L709
    public static JxlPredictionResult PredictNoTreeNoWeightedPrediction(
        int w,
        ref int pp,
        int oneRow,
        int x,
        int y,
        JxlPredictor predictor)
        => Predict(0, [], w, ref pp, oneRow, x, y, predictor, null, null, null, []);

    public static JxlPredictionResult PredictNoTreeWeightedPrediction(
        int w,
        ref int pp,
        int oneRow,
        int x,
        int y,
        JxlPredictor predictor,
        JxlModularState wpState)
        => Predict(JxlPredictorMode.UseTree, [], w, ref pp, oneRow, x, y, predictor, null, null, wpState, []);

    public static JxlPredictionResult PredictTreeNoWeightedPrediction(
        Span<int> p,
        int w,
        ref int pp,
        int oneRow,
        int x,
        int y,
        JxlMaTreeLookup treeLookup,
        JxlModularChannel references)
        => Predict(JxlPredictorMode.UseTree, p, w, ref pp, oneRow, x, y, JxlPredictor.Zero, treeLookup, references, null, []);

    public static JxlPredictionResult PredictTreeNoWeightedPredictionNoEdgeCases(
        Span<int> p,
        int w,
        ref int pp,
        int oneRow,
        int x,
        int y,
        JxlMaTreeLookup treeLookup,
        JxlModularChannel references)
        => Predict(JxlPredictorMode.UseTree | JxlPredictorMode.NoEdgeCases, p, w, ref pp, oneRow, x, y, JxlPredictor.Zero, treeLookup, references, null, []);

    public static JxlPredictionResult PredictTreeWeightedPrediction(
        Span<int> p,
        int w,
        ref int pp,
        int oneRow,
        int x,
        int y,
        JxlMaTreeLookup treeLookup,
        JxlModularChannel references,
        JxlModularState wpState)
        => Predict(JxlPredictorMode.UseTree | JxlPredictorMode.UseWeightedPrediction, p, w, ref pp, oneRow, x, y, JxlPredictor.Zero, treeLookup, references, wpState, []);

    public static JxlPredictionResult PredictTreeWeightedPredictionNoEdgeCases(
        Span<int> p,
        int w,
        ref int pp,
        int oneRow,
        int x,
        int y,
        JxlMaTreeLookup treeLookup,
        JxlModularChannel references,
        JxlModularState wpState)
        => Predict(JxlPredictorMode.UseTree | JxlPredictorMode.UseWeightedPrediction | JxlPredictorMode.NoEdgeCases, p, w, ref pp, oneRow, x, y, JxlPredictor.Zero, treeLookup, references, wpState, []);

    public static JxlPredictionResult PredictLearn(
        Span<int> p,
        int w,
        ref int pp,
        int oneRow,
        int x,
        int y,
        JxlPredictor predictor,
        JxlModularChannel references,
        JxlModularState wpState)
        => Predict(JxlPredictorMode.ForceComputeProperties | JxlPredictorMode.UseWeightedPrediction, p, w, ref pp, oneRow, x, y, predictor, null, references, wpState, []);

    public static JxlPredictionResult PredictLearnAll(
        Span<int> p,
        int w,
        ref int pp,
        int oneRow,
        int x,
        int y,
        JxlModularChannel references,
        JxlModularState wpState,
        Span<int> predictions)
        => Predict(JxlPredictorMode.ForceComputeProperties | JxlPredictorMode.UseWeightedPrediction | JxlPredictorMode.AllPredictions, p, w, ref pp, oneRow, x, y, JxlPredictor.Zero, null, references, wpState, predictions);

    public static JxlPredictionResult PredictLearnNoEdgeCases(
        Span<int> p,
        int w,
        ref int pp,
        int oneRow,
        int x,
        int y,
        JxlPredictor predictor,
        JxlModularChannel references,
        JxlModularState wpState)
        => Predict(JxlPredictorMode.ForceComputeProperties | JxlPredictorMode.UseWeightedPrediction | JxlPredictorMode.NoEdgeCases, p, w, ref pp, oneRow, x, y, predictor, null, references, wpState, []);

    public static JxlPredictionResult PredictLearnAllNoEdgeCases(
        Span<int> p,
        int w,
        ref int pp,
        int oneRow,
        int x,
        int y,
        JxlModularChannel references,
        JxlModularState wpState,
        Span<int> predictions)
        => Predict(JxlPredictorMode.ForceComputeProperties | JxlPredictorMode.UseWeightedPrediction | JxlPredictorMode.AllPredictions | JxlPredictorMode.NoEdgeCases, p, w, ref pp, oneRow, x, y, JxlPredictor.Zero, null, references, wpState, predictions);

    public static JxlPredictionResult PredictAllNoWeightedPrediction(
        int w,
        ref int pp,
        int oneRow,
        int x,
        int y,
        Span<int> predictions)
        => Predict(JxlPredictorMode.AllPredictions, [], w, ref pp, oneRow, x, y, JxlPredictor.Zero, null, null, null, predictions);
}
