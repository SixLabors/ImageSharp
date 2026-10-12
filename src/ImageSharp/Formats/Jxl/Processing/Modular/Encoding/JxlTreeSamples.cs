// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Jxl.IO.Entropy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.ContextPrediction;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using StaticPropertyRange = System.Runtime.CompilerServices.InlineArray2<System.Runtime.CompilerServices.InlineArray2<int>>;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding;

/// <summary>
/// Contains all the necessary data needed to build a tree.
/// </summary>
internal sealed class JxlTreeSamples
{
    /// <summary>
    /// This deduplication table entry marks an unused entry.
    /// </summary>
    private const int DeduplicationEntryUnused = -1;

    private const int PropertyRange = 511;

    /// <summary>
    /// Residual information: token and number of extra bits per predictor.
    /// </summary>
    private List<List<JxlResidualToken>> residuals = [];

    /// <summary>
    /// Number of occurrences of each sample.
    /// </summary>
    private readonly List<int> sampleCounts = [];

    /// <summary>
    /// Quantized static property values.
    /// </summary>
    private InlineArray2<List<int>> staticProperties;

    /// <summary>
    /// Property values, quantized to at most 256 distinct values.
    /// </summary>
    private readonly List<List<byte>> properties = [];

    /// <summary>
    /// Decompactification info for <see cref="properties"/>.
    /// </summary>
    private readonly List<List<int>> compactProperties = [];

    /// <summary>
    /// List of properties to use.
    /// </summary>
    private List<int> propertiesToUse = [];

    /// <summary>
    /// List of predictors to use.
    /// </summary>
    private List<JxlPredictor> predictors = [];

    /// <summary>
    /// Mapping property value -> quantized property value.
    /// </summary>
    private InlineArray2<List<int>> staticPropertyMapping;

    private readonly List<List<int>> propertyMapping = [];

    /// <summary>
    /// Table for deduplication.
    /// </summary>
    private List<int> deduplicationTable = [];

    /// <summary>
    /// Gets a value indicating whether there are any residual samples.
    /// </summary>
    public bool HasSamples => this.residuals.Count > 0 && this.residuals[0].Count > 0;

    /// <summary>
    /// Gets the total number of distinct samples.
    /// </summary>
    public int NumberOfDistinctSamples => this.sampleCounts.Count;

    /// <summary>
    /// Gets the total number of samples.
    /// </summary>
    public int NumberOfSamples { get; private set; }

    /// <summary>
    /// Gets the number of quantized static property values.
    /// </summary>
    public int NumberOfStaticProperties { get; private set; }

    /// <summary>
    /// Gets the total number of predictors.
    /// </summary>
    public int NumberOfPredictors => this.predictors.Count;

    /// <summary>
    /// Gets the total number of properties.
    /// </summary>
    public int NumberOfProperties => this.propertiesToUse.Count;

    /// <summary>
    /// Returns a List of residual tokens for the specified kind of prediction.
    /// </summary>
    /// <param name="prediction">The type of prediction.</param>
    /// <returns>Residual tokens corresponding to the prediction specified by the parameter <paramref name="prediction"/>.</returns>
    public List<JxlResidualToken> GetResidualTokensForPrediction(int prediction) => this.residuals[prediction];

    /// <summary>
    /// Returns a reference to the residual token.
    /// </summary>
    /// <param name="pred">Kind of prediction.</param>
    /// <param name="i">Index of the residual token within that prediction.</param>
    /// <returns>The residual token for prediction <paramref name="pred"/> indexed <paramref name="i"/>.</returns>
    public ref JxlResidualToken GetResidualToken(int pred, int i) => ref CollectionsMarshal.AsSpan(this.residuals[pred])[i];

    /// <summary>
    /// Returns a token of the residual for prediction <paramref name="prediction"/> index <paramref name="index"/>.
    /// </summary>
    /// <param name="prediction">The kind of prediction.</param>
    /// <param name="index">Index of the residual token within that prediction.</param>
    /// <returns>For residual token whose prediction is <paramref name="prediction"/> and index is <paramref name="index"/>, returns its token coefficient.</returns>
    public int GetToken(int prediction, int index) => this.residuals[prediction][index].Token;

    /// <summary>
    /// Returns the number of occurrences for sample <paramref name="i"/>.
    /// </summary>
    /// <param name="i">The index of the sample.</param>
    /// <returns>Number of times <paramref name="i"/> appears.</returns>
    public int GetCount(int i) => this.sampleCounts[i];

    /// <summary>
    /// Finds the index of the predictor <paramref name="predictor"/>.
    /// </summary>
    /// <param name="predictor">The predictor to find the index for.</param>
    /// <returns>Index of the predictor in the predictors storage.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the predictor can't be found.</exception>
    public int FindPredictorIndex(JxlPredictor predictor)
    {
        ReadOnlySpan<JxlPredictor> span = CollectionsMarshal.AsSpan(this.predictors);

        int index = span.IndexOf(predictor);

        if (index < 0)
        {
            // Should not happen.
            throw new InvalidOperationException("Cannot find the index of the predictor");
        }

        return index;
    }

    /// <summary>
    /// Finds the index of the property <paramref name="property"/>.
    /// </summary>
    /// <param name="property">The property to find the index for.</param>
    /// <returns>Index of the property in the properties storage.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the property isn't valid.</exception>
    public int FindPropertyIndex(int property)
    {
        ReadOnlySpan<int> span = CollectionsMarshal.AsSpan(this.propertiesToUse);

        int index = span.IndexOf(property);

        if (index != this.propertiesToUse[^1])
        {
            // Should not happen.
            throw new InvalidOperationException("Invalid property");
        }

        return index;
    }

    /// <summary>
    /// Returns the number of properties for a <paramref name="propertyIndex"/>.
    /// </summary>
    /// <param name="propertyIndex">Index of the property.</param>
    /// <returns>Number of property values for property with index <paramref name="propertyIndex"/>.</returns>
    public int CountPropertyValues(int propertyIndex) => this.compactProperties[propertyIndex].Count + 1;

    /// <summary>
    /// Returns the value of a property.
    /// </summary>
    /// <param name="useStaticProperty">Prefer a static property?</param>
    /// <param name="propertyIndex">The index of the properties table.</param>
    /// <param name="i">The index of the property.</param>
    /// <returns>
    /// Property for index <paramref name="i" /> within table <paramref name="propertyIndex"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetProperty(bool useStaticProperty, int propertyIndex, int i)
    {
        if (useStaticProperty)
        {
            return this.staticProperties[propertyIndex][i];
        }
        else
        {
            return this.properties[propertyIndex][i];
        }
    }

    /// <summary>
    /// Returns the dequantized property.
    /// </summary>
    /// <param name="propertyIndex">Index of the target property.</param>
    /// <param name="quant">Property quantizer.</param>
    /// <returns>The dequantized property.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the quant is out of range.</exception>
    public int UnquantizeProperty(int propertyIndex, int quant)
    {
        List<int> compactProperties = this.compactProperties[propertyIndex];

        if (quant >= compactProperties.Count)
        {
            throw new InvalidOperationException("Quant is out of range");
        }

        return compactProperties[quant];
    }

    /// <summary>
    /// Returns a predictor for index <paramref name="index"/>.
    /// </summary>
    /// <param name="index">The index of the predictor.</param>
    /// <returns>Predictor at index <paramref name="index"/>.</returns>
    public JxlPredictor PredictorFromIndex(int index)
    {
        DebugGuard.MustBeLessThan(index, this.predictors.Count, nameof(index));

        return this.predictors[index];
    }

    /// <summary>
    /// Returns a property for index <paramref name="index"/>.
    /// </summary>
    /// <param name="index">The index of the property.</param>
    /// <returns>Property at index <paramref name="index"/>.</returns>
    public int PropertyFromIndex(int index)
    {
        DebugGuard.MustBeLessThan(index, this.propertiesToUse.Count, nameof(index));

        return this.propertiesToUse[index];
    }

    /// <summary>
    /// Invoked after processing samples completed.
    /// </summary>
    public void AllSamplesDone() => this.deduplicationTable = [];

    /// <summary>
    /// Returns the quantized property value of <paramref name="v"/>.
    /// </summary>
    /// <param name="property">Index of the property.</param>
    /// <param name="v">Value to quantize.</param>
    /// <returns>Quantized value.</returns>
    public int QuantizeProperty(int property, int v)
    {
        DebugGuard.MustBeGreaterThanOrEqualTo(property, this.NumberOfStaticProperties, nameof(property));

        v = Math.Clamp(v, -PropertyRange, PropertyRange) + PropertyRange;

        return this.propertyMapping[property][v];
    }

    /// <summary>
    /// Returns the quantized static property value of <paramref name="v"/>.
    /// </summary>
    /// <param name="property">Index of the static property.</param>
    /// <param name="v">Value to quantize.</param>
    /// <returns>Quantized value.</returns>
    public int QuantizeStaticProperty(int property, int v)
    {
        DebugGuard.MustBeGreaterThanOrEqualTo(property, this.NumberOfStaticProperties, nameof(property));

        v = Math.Clamp(v, -PropertyRange, PropertyRange) + PropertyRange;

        return this.staticPropertyMapping[property][v];
    }

    public void SetPredictor(JxlPredictor predictor, JxlTreeMode wpTreeMode)
    {
        if (wpTreeMode == JxlTreeMode.WpOnly)
        {
            this.predictors = [JxlPredictor.Weighted];

            // equivalent of residuals.resize(1)
            if (this.residuals.Count > 1)
            {
                this.residuals = [this.residuals[0]];
            }
            else if (this.residuals.Count < 1)
            {
                this.residuals = [[]];
            }

            return;
        }

        if (wpTreeMode == JxlTreeMode.NoWp && predictor == JxlPredictor.Weighted)
        {
            throw new InvalidOperationException("Invalid predictor settings");
        }

        if (predictor == JxlPredictor.Variable)
        {
            for (int i = 0; i < JxlPredictorFacts.ModularPredictors; i++)
            {
                this.predictors.Add((JxlPredictor)i);
            }

            Span<JxlPredictor> predictorsSpan = CollectionsMarshal.AsSpan(this.predictors);
            RuntimeUtility.Swap(ref predictorsSpan[0], ref predictorsSpan[(int)JxlPredictor.Weighted]);
            RuntimeUtility.Swap(ref predictorsSpan[1], ref predictorsSpan[(int)JxlPredictor.Gradient]);
        }
        else if (predictor == JxlPredictor.Best)
        {
            this.predictors = [JxlPredictor.Weighted, JxlPredictor.Gradient];
        }
        else
        {
            this.predictors = [predictor];
        }

        if (wpTreeMode == JxlTreeMode.NoWp)
        {
            // delete all weighted predictors
            _ = this.predictors.RemoveAll(p => p == JxlPredictor.Weighted);
        }

        this.residuals.Grow([], this.predictors.Count);
    }

    public void SetProperties(List<int> properties, JxlTreeMode wpTreeMode)
    {
        this.propertiesToUse = properties;

        if (wpTreeMode == JxlTreeMode.WpOnly)
        {
            this.propertiesToUse = [JxlMaConstants.WpProp];
        }
        else if (wpTreeMode == JxlTreeMode.GradientOnly)
        {
            this.propertiesToUse = [JxlMaConstants.GradientProp];
        }
        else if (wpTreeMode == JxlTreeMode.NoWp)
        {
            // delete all weighted properties, tree mode
            // says "no weighted predictors"
            _ = this.propertiesToUse.RemoveAll(x => x == JxlMaConstants.WpProp);
        }

        if (this.propertiesToUse.Count == 0)
        {
            // could happen in default tree mode or when properties parameter
            // is empty
            throw new InvalidOperationException("Invalid property set configuration");
        }

        this.NumberOfStaticProperties = 0;

        for (int i = 0; i < this.propertiesToUse.Count; ++i)
        {
            int prop = this.propertiesToUse[i];

            if (prop < JxlPredictorFacts.StaticProperties)
            {
                if (i != prop)
                {
                    throw new InvalidOperationException("Index is not equal to the property");
                }

                this.NumberOfStaticProperties++;
            }
        }

        this.properties.Resize(this.propertiesToUse.Count - this.NumberOfStaticProperties);
    }

    public void InitializeTable(int logSize)
    {
        int size = 1 << logSize;

        if (this.deduplicationTable.Count == size)
        {
            return;
        }

        this.deduplicationTable.Resize(size, DeduplicationEntryUnused);

        for (int i = 0; i < this.NumberOfDistinctSamples; i++)
        {
            if (this.sampleCounts[i] != ushort.MaxValue)
            {
                this.AddToTable(i);
            }
        }
    }

    public void AddToTableAndMerge(int a)
    {
        int pos1 = Hash1(a);
        int pos2 = Hash2(a);

        if (this.deduplicationTable[pos1] != DeduplicationEntryUnused && this.IsSameSample(a, this.deduplicationTable[pos1]))
        {
            if (this.sampleCounts[a] != 1)
            {
                throw new InvalidOperationException("Sample count must be 1");
            }

            this.sampleCounts[this.deduplicationTable[pos1]]++;

            if (this.sampleCounts[this.deduplicationTable[pos1]] == ushort.MaxValue)
            {
                this.deduplicationTable[pos1] = DeduplicationEntryUnused;
            }

            return;
        }

        if (this.deduplicationTable[pos2] != DeduplicationEntryUnused && this.IsSameSample(a, this.deduplicationTable[pos2]))
        {
            if (this.sampleCounts[a] != 1)
            {
                throw new InvalidOperationException("Sample count must be 1");
            }

            this.sampleCounts[this.deduplicationTable[pos2]]++;

            if (this.sampleCounts[this.deduplicationTable[pos2]] == ushort.MaxValue)
            {
                this.deduplicationTable[pos2] = DeduplicationEntryUnused;
            }

            return;
        }

        this.AddToTable(a);
    }

    public void AddToTable(int a)
    {
        int pos1 = Hash1(a);
        int pos2 = Hash2(a);

        if (this.deduplicationTable[pos1] == DeduplicationEntryUnused)
        {
            this.deduplicationTable[pos1] = a;
        }
        else if (this.deduplicationTable[pos2] == DeduplicationEntryUnused)
        {
            this.deduplicationTable[pos2] = a;
        }
    }

    public void PrepareForSamples(int extraNumSamples)
    {
        foreach (List<JxlResidualToken> residual in this.residuals)
        {
            residual.Grow(default, residual.Count + extraNumSamples);
        }

        for (int i = 0; i < this.NumberOfStaticProperties; i++)
        {
            this.staticProperties[i].Grow(0, this.staticProperties[i].Count + extraNumSamples);
        }

        foreach (List<byte> prop in this.properties)
        {
            prop.Grow((byte)0, prop.Count + extraNumSamples);
        }

        int totalNumSamples = extraNumSamples + this.sampleCounts.Count;
        int nextSize = JxlMath.CeilLog2Nonzero(totalNumSamples * 3 / 2);

        this.InitializeTable(nextSize);
    }

    public int Hash1(int a)
    {
        const ulong constant = 0x1e35a7bd;

        ulong h = constant;

        foreach (List<JxlResidualToken> r in this.residuals)
        {
            h = (h * constant) + (ulong)r[a].Token;
            h = (h * constant) + (ulong)r[a].NumberOfBits;
        }

        for (int i = 0; i < this.NumberOfStaticProperties; i++)
        {
            h = (h * constant) + (ulong)this.staticProperties[i][a];
        }

        foreach (List<byte> property in this.properties)
        {
            h = (h * constant) + property[a];
        }

        return (int)((h >> 16) & (ulong)(this.deduplicationTable.Count - 1));
    }

    public int Hash2(int a)
    {
        const ulong constant = 0x1e35a7bd1e35a7bd;

        ulong h = constant;

        for (int i = 0; i < this.NumberOfStaticProperties; i++)
        {
            h = (h * constant) ^ (ulong)this.staticProperties[i][a];
        }

        foreach (List<byte> property in this.properties)
        {
            h = (h * constant) ^ property[a];
        }

        foreach (List<JxlResidualToken> r in this.residuals)
        {
            h = (h * constant) ^ (ulong)r[a].Token;
            h = (h * constant) ^ (ulong)r[a].NumberOfBits;
        }

        return (int)((h >> 16) & (ulong)(this.deduplicationTable.Count - 1));
    }

    public bool IsSameSample(int a, int b)
    {
        foreach (List<JxlResidualToken> r in this.residuals)
        {
            if (r[a].Token != r[b].Token)
            {
                return false;
            }

            if (r[a].NumberOfBits != r[b].NumberOfBits)
            {
                return false;
            }
        }

        for (int i = 0; i < this.NumberOfStaticProperties; ++i)
        {
            if (this.staticProperties[i][a] != this.staticProperties[i][b])
            {
                return false;
            }
        }

        foreach (List<byte> p in this.properties)
        {
            if (p[a] != p[b])
            {
                return false;
            }
        }

        return false;
    }

    public void AddSample(int pixel, Span<int> properties, Span<int> predictions)
    {
        for (int i = 0; i < this.predictors.Count; i++)
        {
            int v = pixel - predictions[(int)this.predictors[i]];
            new JxlAnsHybridUIntConfiguration(4, 1, 2).Encode(JxlPackSigned.PackUnsigned(v), out uint tok, out uint nbits, out uint bits);

            if (tok >= 256)
            {
                throw new InvalidOperationException("Token is too large");
            }

            if (nbits >= 256)
            {
                throw new InvalidOperationException("Number of bits is too large");
            }

            JxlResidualToken token = new((int)tok, (int)nbits);
            this.residuals[i].Add(token);
        }

        for (int i = 0; i < this.NumberOfStaticProperties; ++i)
        {
            this.staticProperties[i].Add(this.QuantizeStaticProperty(i, properties[i]));
        }

        for (int i = this.NumberOfStaticProperties; i < this.propertiesToUse.Count; i++)
        {
            this.properties[i - this.NumberOfStaticProperties].Add(unchecked((byte)this.QuantizeProperty(i, properties[this.propertiesToUse[i]])));
        }

        this.sampleCounts.Add(1);
        this.NumberOfSamples++;

        this.AddToTableAndMerge(this.sampleCounts.Count - 1);

        foreach (List<JxlResidualToken> residual in this.residuals)
        {
            // remove last item from List<T>
            residual.RemoveAt(residual.Count - 1);
        }

        for (int i = 0; i < this.NumberOfStaticProperties; i++)
        {
            // ditto
            this.staticProperties[i].RemoveAt(this.staticProperties[i].Count - 1);
        }

        foreach (List<byte> property in this.properties)
        {
            // ditto
            property.RemoveAt(property.Count - 1);
        }

        // ditto
        this.sampleCounts.RemoveAt(this.sampleCounts.Count - 1);
    }

    public void Swap(int a, int b)
    {
        if (a == b)
        {
            return;
        }

        foreach (List<JxlResidualToken> r in this.residuals)
        {
            // Get a Span for this List so we can get a ref to its items
            // for use in RuntimeUtility.Swap (tuple-swap is slightly slower)
            Span<JxlResidualToken> sp = CollectionsMarshal.AsSpan(r);

            RuntimeUtility.Swap(ref sp[a], ref sp[b]);
        }

        for (int i = 0; i < this.NumberOfStaticProperties; i++)
        {
            // Ditto
            Span<int> sp = CollectionsMarshal.AsSpan(this.staticProperties[i]);

            RuntimeUtility.Swap(ref sp[a], ref sp[b]);
        }

        foreach (List<byte> p in this.properties)
        {
            // Ditto
            Span<byte> sp = CollectionsMarshal.AsSpan(p);

            RuntimeUtility.Swap(ref sp[a], ref sp[b]);
        }

        // Ditto
        Span<int> sampleCounts = CollectionsMarshal.AsSpan(this.sampleCounts);

        RuntimeUtility.Swap(ref sampleCounts[a], ref sampleCounts[b]);
    }

    public void PreQuantizeProperties(
        Configuration configuration,
        StaticPropertyRange range,
        List<JxlModularMultiplierInfo> multiplierInfo,
        List<int> groupPixelCount,
        List<int> channelPixelCount,
        List<int> pixelSamples,
        List<int> diffSamples,
        int maxPropertyValues)
    {
        List<int> groupMultiplierThresholds = [];
        List<int> channelMultiplierThresholds = [];

        foreach (JxlModularMultiplierInfo v in multiplierInfo)
        {
            if (v.Range[0][0] != range[0][0])
            {
                channelMultiplierThresholds.Add(v.Range[0][0] - 1);
            }

            if (v.Range[0][1] != range[0][1])
            {
                channelMultiplierThresholds.Add(v.Range[0][1] - 1);
            }

            if (v.Range[1][0] != range[1][0])
            {
                groupMultiplierThresholds.Add(v.Range[1][0] - 1);
            }

            if (v.Range[1][1] != range[1][1])
            {
                groupMultiplierThresholds.Add(v.Range[1][1] - 1);
            }
        }

        channelMultiplierThresholds.Sort();
        channelMultiplierThresholds.Resize(0, channelMultiplierThresholds.Distinct().Count());
        groupMultiplierThresholds.Sort();
        groupMultiplierThresholds.Resize(0, groupMultiplierThresholds.Distinct().Count());

        this.compactProperties.Resize([], this.propertiesToUse.Count);

        List<int> QuantizeChannel()
        {
            if (channelMultiplierThresholds.Count > 0)
            {
                return channelMultiplierThresholds;
            }

            return JxlMaEncoder.QuantizeHistogram(
                CollectionsMarshal.AsSpan(groupPixelCount),
                maxPropertyValues);
        }

        List<int> QuantizeGroupId()
        {
            if (groupMultiplierThresholds.Count > 0)
            {
                return groupMultiplierThresholds;
            }

            return JxlMaEncoder.QuantizeHistogram(
                CollectionsMarshal.AsSpan(groupPixelCount),
                maxPropertyValues);
        }

        List<int> QuantizeCoordinate()
        {
            List<int> quantized = new(maxPropertyValues - 1);

            for (int i = 0; i + 1 < maxPropertyValues; i++)
            {
                quantized[i] = ((i + 1) * 256 / maxPropertyValues) - 1;
            }

            return quantized;
        }

        List<int> absPixelThresholds = [];
        List<int> pixelThresholds = [];

        List<int> QuantizePixelProperty()
        {
            if (pixelThresholds.Count == 0)
            {
                pixelThresholds = JxlMaEncoder.QuantizeSamples(
                    CollectionsMarshal.AsSpan(pixelSamples),
                    maxPropertyValues);
            }

            return pixelThresholds;
        }

        List<int> QuantizeAbsolutePixelProperty()
        {
            if (absPixelThresholds.Count == 0)
            {
                _ = QuantizePixelProperty(); // compute the non-abs thresholds

                Span<int> pixelSamplesSpan = CollectionsMarshal.AsSpan(pixelSamples);
                TensorPrimitives.Abs(pixelSamplesSpan, pixelSamplesSpan);

                absPixelThresholds = JxlMaEncoder.QuantizeSamples(pixelSamplesSpan, maxPropertyValues);
            }

            return absPixelThresholds;
        }

        List<int> absoluteDiffThresholds = [];
        List<int> diffThresholds = [];

        List<int> QuantizeDiffProperty()
        {
            if (diffThresholds.Count == 0)
            {
                diffThresholds = JxlMaEncoder.QuantizeSamples(
                    CollectionsMarshal.AsSpan(diffSamples),
                    maxPropertyValues);
            }

            return diffThresholds;
        }

        List<int> QuantizeAbsoluteDiffProperty()
        {
            if (absoluteDiffThresholds.Count == 0)
            {
                _ = QuantizeDiffProperty();

                Span<int> diffSamplesSpan = CollectionsMarshal.AsSpan(diffSamples);
                TensorPrimitives.Abs(diffSamplesSpan, diffSamplesSpan);

                absoluteDiffThresholds = JxlMaEncoder.QuantizeSamples(diffSamplesSpan, maxPropertyValues);
            }

            return absoluteDiffThresholds;
        }

        List<int> QuantizeWeightedPrediction()
        {
            // TODO: static ReadOnlySpan<int> ... => [...]?
            if (maxPropertyValues < 32)
            {
                return [-127, -63, -31, -15, -7, -3, -1, 0, 1,
                        3, 7, 15, 31, 63, 127];
            }
            else if (maxPropertyValues < 64)
            {
                return [-255, -191, -127, -95, -63, -47, -31, -23,
                        -15,  -11,  -7,   -5,  -3,  -1,  0,   1,
                        3,    5,    7,    11,  15,  23,  31,  47,
                        63,   95,   127,  191, 255];
            }
            else
            {
                return [-255, -223, -191, -159, -127, -111, -95, -79, -63, -55, -47,
                        -39,  -31,  -27,  -23,  -19,  -15,  -13, -11, -9,  -7,  -6,
                        -5,   -4,   -3,   -2,   -1,   0,    1,   2,   3,   4,   5,
                        6,    7,    9,    11,   13,   15,   19,  23,  27,  31,  39,
                        47,   55,   63,   79,   95,   111,  127, 159, 191, 223, 255];
            }
        }

        this.propertyMapping.Resize(0, this.propertiesToUse.Count - this.NumberOfStaticProperties);

        for (int i = 0; i < this.propertiesToUse.Count; i++)
        {
            if (this.propertiesToUse[i] == 0)
            {
                this.compactProperties[i] = QuantizeChannel();
            }
            else if (this.propertiesToUse[i] == 1)
            {
                this.compactProperties[i] = QuantizeGroupId();
            }
            else if (this.propertiesToUse[i] is 2 or 3)
            {
                this.compactProperties[i] = QuantizeCoordinate();
            }
            else if (this.propertiesToUse[i] == 6 || this.propertiesToUse[i] == 7 ||
                     this.propertiesToUse[i] == 8 ||
                     (this.propertiesToUse[i] >= JxlMaConstants.NumNonrefProperties && (this.propertiesToUse[i] - JxlMaConstants.NumNonrefProperties) % 4 == 1))
            {
                this.compactProperties[i] = QuantizePixelProperty();
            }
            else if (this.propertiesToUse[i] == 4 || this.propertiesToUse[i] == 5 ||
                     (this.propertiesToUse[i] >= JxlMaConstants.NumNonrefProperties && (this.propertiesToUse[i] - JxlMaConstants.NumNonrefProperties) % 4 == 0))
            {
                this.compactProperties[i] = QuantizeAbsolutePixelProperty();
            }
            else if (this.propertiesToUse[i] >= JxlMaConstants.NumNonrefProperties && (this.propertiesToUse[i] - JxlMaConstants.NumNonrefProperties) % 4 == 2)
            {
                this.compactProperties[i] = QuantizeAbsoluteDiffProperty();
            }
            else if (this.propertiesToUse[i] == JxlMaConstants.WpProp)
            {
                this.compactProperties[i] = QuantizeWeightedPrediction();
            }
            else
            {
                this.compactProperties[i] = QuantizeDiffProperty();
            }

            if (i < this.NumberOfStaticProperties)
            {
                JxlMaEncoder.QuantMap(
                    CollectionsMarshal.AsSpan(this.compactProperties[i]),
                    CollectionsMarshal.AsSpan(this.staticPropertyMapping[i]),
                    (PropertyRange * 2) + 1,
                    PropertyRange);
            }
            else
            {
                JxlMaEncoder.QuantMap(
                    CollectionsMarshal.AsSpan(this.compactProperties[i]),
                    CollectionsMarshal.AsSpan(this.propertyMapping[i - this.NumberOfStaticProperties]),
                    (PropertyRange * 2) + 1,
                    PropertyRange);
            }
        }
    }
}
