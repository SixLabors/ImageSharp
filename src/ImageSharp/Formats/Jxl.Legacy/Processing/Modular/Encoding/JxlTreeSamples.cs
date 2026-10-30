// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Jxl.IO.Entropy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.ContextPrediction;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Memory;
using StaticPropertyRange = SixLabors.ImageSharp.InlineArray2<SixLabors.ImageSharp.InlineArray2<int>>;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding;

/// <summary>
/// Owns the sampled residuals and quantized properties used to learn one modular prediction tree.
/// </summary>
/// <remarks>
/// Preparation allocates each numeric buffer once. Separate predictor and property planes retain contiguous tree-search reads.
/// The borrowed spans remain valid until disposal. Pass this owner by reference; copying it would copy ownership and logical counts.
/// </remarks>
internal ref struct JxlTreeSamples
{
    private const int DeduplicationEntryUnused = -1;
    private const int PropertyRange = 511;
    private const int PropertyMapSize = (2 * PropertyRange) + 1;

    private readonly MemoryAllocator allocator;
    private InlineArray16<JxlPredictor> predictors;
    private IMemoryOwner<int>? propertyIdsOwner;
    private IMemoryOwner<int>? quantizationOwner;
    private IMemoryOwner<JxlResidualToken>? residualsOwner;
    private IMemoryOwner<int>? countsOwner;
    private IMemoryOwner<byte>? propertiesOwner;
    private IMemoryOwner<int>? deduplicationOwner;

    // Each predictor/property occupies one capacity-sized plane. Only the first NumberOfDistinctSamples entries are samples.
    private Span<int> propertiesToUse;
    private Span<int> thresholdCounts;
    private Span<int> compactProperties;
    private Span<int> propertyMapping;
    private Span<JxlResidualToken> residuals;
    private Span<int> sampleCounts;
    private Span<int> staticProperties;
    private Span<byte> properties;
    private Span<int> deduplicationTable;
    private int sampleCapacity;
    private int thresholdCapacity;

    /// <summary>
    /// Creates an empty owner. Select predictors and properties before preparing quantization and sample storage.
    /// </summary>
    /// <param name="configuration">The configuration that owns all numeric storage.</param>
    public JxlTreeSamples(Configuration configuration)
    {
        this = default;
        this.allocator = configuration.MemoryAllocator;
    }

    /// <summary>Gets whether at least one distinct sample was inserted.</summary>
    public readonly bool HasSamples => this.NumberOfDistinctSamples != 0;

    /// <summary>Gets the number of retained sample records.</summary>
    public int NumberOfDistinctSamples { get; private set; }

    /// <summary>Gets the number of inserted samples, including merged duplicates.</summary>
    public int NumberOfSamples { get; private set; }

    /// <summary>Gets the number of selected static properties.</summary>
    public int NumberOfStaticProperties { get; private set; }

    /// <summary>Gets the number of selected predictors.</summary>
    public int NumberOfPredictors { get; private set; }

    /// <summary>Gets the number of selected properties.</summary>
    public readonly int NumberOfProperties => this.propertiesToUse.Length;

    /// <summary>Returns the logical residual plane for one selected predictor.</summary>
    /// <param name="prediction">The selected predictor index.</param>
    /// <returns>The retained residual tokens, without unused capacity.</returns>
    public readonly ReadOnlySpan<JxlResidualToken> GetResidualTokensForPrediction(int prediction)
        => this.residuals.Slice(prediction * this.sampleCapacity, this.NumberOfDistinctSamples);

    /// <summary>Returns one retained residual token.</summary>
    /// <param name="prediction">The selected predictor index.</param>
    /// <param name="sample">The retained sample index.</param>
    /// <returns>The token and extra-bit count.</returns>
    public readonly ref JxlResidualToken GetResidualToken(int prediction, int sample)
        => ref this.residuals[(prediction * this.sampleCapacity) + sample];

    /// <summary>Returns the entropy symbol for one predictor and sample.</summary>
    /// <param name="prediction">The selected predictor index.</param>
    /// <param name="sample">The retained sample index.</param>
    /// <returns>The residual symbol.</returns>
    public readonly int GetToken(int prediction, int sample) => this.residuals[(prediction * this.sampleCapacity) + sample].Token;

    /// <summary>Returns the occurrence count for one retained sample.</summary>
    /// <param name="sample">The retained sample index.</param>
    /// <returns>The count, saturated at 65535 for each retained record.</returns>
    public readonly int GetCount(int sample) => this.sampleCounts[sample];

    /// <summary>Finds a selected predictor.</summary>
    /// <param name="predictor">The predictor to find.</param>
    /// <returns>Its index in the selected predictor set.</returns>
    public readonly int FindPredictorIndex(JxlPredictor predictor)
    {
        for (int i = 0; i < this.NumberOfPredictors; i++)
        {
            if (this.predictors[i] == predictor)
            {
                return i;
            }
        }

        throw new InvalidOperationException("The predictor was not selected.");
    }

    /// <summary>Finds a selected property.</summary>
    /// <param name="property">The original property index.</param>
    /// <returns>Its index in the selected property set.</returns>
    public readonly int FindPropertyIndex(int property)
    {
        int index = this.propertiesToUse.IndexOf(property);
        DebugGuard.MustBeGreaterThanOrEqualTo(index, 0, nameof(property));
        return index;
    }

    /// <summary>Returns the number of quantization intervals for a selected property.</summary>
    /// <param name="propertyIndex">The selected property index.</param>
    /// <returns>The threshold count plus one.</returns>
    public readonly int CountPropertyValues(int propertyIndex) => this.thresholdCounts[propertyIndex] + 1;

    /// <summary>Reads one quantized static or dynamic property.</summary>
    /// <param name="useStaticProperty">Whether to read a static property plane.</param>
    /// <param name="propertyIndex">The index within the static or dynamic property planes.</param>
    /// <param name="sampleIndex">The retained sample index.</param>
    /// <returns>The quantization interval.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly int GetProperty(bool useStaticProperty, int propertyIndex, int sampleIndex)
        => useStaticProperty
            ? this.staticProperties[(propertyIndex * this.sampleCapacity) + sampleIndex]
            : this.properties[(propertyIndex * this.sampleCapacity) + sampleIndex];

    /// <summary>Returns the original threshold for a quantized split.</summary>
    /// <param name="propertyIndex">The selected property index.</param>
    /// <param name="quant">The threshold index.</param>
    /// <returns>The original property threshold.</returns>
    public readonly int UnquantizeProperty(int propertyIndex, int quant)
        => this.compactProperties[(propertyIndex * this.thresholdCapacity) + quant];

    /// <summary>Returns the predictor at a selected index.</summary>
    /// <param name="index">The selected predictor index.</param>
    /// <returns>The predictor.</returns>
    public readonly JxlPredictor PredictorFromIndex(int index) => this.predictors[index];

    /// <summary>Returns the original property at a selected index.</summary>
    /// <param name="index">The selected property index.</param>
    /// <returns>The original property index.</returns>
    public readonly int PropertyFromIndex(int index) => this.propertiesToUse[index];

    /// <summary>Releases the deduplication table after collection. Sample planes remain available for tree search.</summary>
    public void AllSamplesDone()
    {
        this.deduplicationTable = default;
        this.deduplicationOwner?.Dispose();
        this.deduplicationOwner = null;
    }

    /// <summary>Maps a dynamic property to its quantization interval.</summary>
    /// <param name="property">The selected property index, including static properties.</param>
    /// <param name="value">The original property value.</param>
    /// <returns>The quantization interval.</returns>
    public readonly int QuantizeProperty(int property, int value)
        => this.propertyMapping[(property * PropertyMapSize) + Math.Clamp(value, -PropertyRange, PropertyRange) + PropertyRange];

    /// <summary>Maps a static property to its quantization interval.</summary>
    /// <param name="property">The selected static property index.</param>
    /// <param name="value">The original property value.</param>
    /// <returns>The quantization interval.</returns>
    public readonly int QuantizeStaticProperty(int property, int value)
        => this.propertyMapping[(property * PropertyMapSize) + Math.Clamp(value, -PropertyRange, PropertyRange) + PropertyRange];

    /// <summary>Selects the predictors before sample storage is prepared.</summary>
    /// <param name="predictor">The requested predictor or predictor search set.</param>
    /// <param name="wpTreeMode">The weighted-prediction selection mode.</param>
    public void SetPredictor(JxlPredictor predictor, JxlTreeMode wpTreeMode)
    {
        this.NumberOfPredictors = 0;
        if (wpTreeMode == JxlTreeMode.WpOnly)
        {
            this.predictors[this.NumberOfPredictors++] = JxlPredictor.Weighted;
            return;
        }

        if (wpTreeMode == JxlTreeMode.NoWp && predictor == JxlPredictor.Weighted)
        {
            throw new InvalidOperationException("Weighted prediction is excluded by the tree mode.");
        }

        if (predictor == JxlPredictor.Variable)
        {
            for (int i = 0; i < JxlPredictorFacts.ModularPredictors; i++)
            {
                this.predictors[this.NumberOfPredictors++] = (JxlPredictor)i;
            }

            RuntimeUtility.Swap(ref this.predictors[0], ref this.predictors[(int)JxlPredictor.Weighted]);
            RuntimeUtility.Swap(ref this.predictors[1], ref this.predictors[(int)JxlPredictor.Gradient]);
        }
        else if (predictor == JxlPredictor.Best)
        {
            this.predictors[this.NumberOfPredictors++] = JxlPredictor.Weighted;
            this.predictors[this.NumberOfPredictors++] = JxlPredictor.Gradient;
        }
        else
        {
            this.predictors[this.NumberOfPredictors++] = predictor;
        }

        if (wpTreeMode == JxlTreeMode.NoWp)
        {
            int count = 0;
            for (int i = 0; i < this.NumberOfPredictors; i++)
            {
                if (this.predictors[i] != JxlPredictor.Weighted)
                {
                    this.predictors[count++] = this.predictors[i];
                }
            }

            this.NumberOfPredictors = count;
        }
    }

    /// <summary>Copies the selected property IDs. Tree modes do not modify the caller's options.</summary>
    /// <param name="selectedProperties">The original property IDs.</param>
    /// <param name="wpTreeMode">The weighted-prediction selection mode.</param>
    public void SetProperties(scoped ReadOnlySpan<int> selectedProperties, JxlTreeMode wpTreeMode)
    {
        int capacity = wpTreeMode is JxlTreeMode.WpOnly or JxlTreeMode.GradientOnly ? 1 : selectedProperties.Length;
        this.propertyIdsOwner = this.allocator.Allocate<int>(capacity);
        Span<int> destination = this.propertyIdsOwner.Memory.Span;
        int count = 0;
        if (wpTreeMode is JxlTreeMode.WpOnly or JxlTreeMode.GradientOnly)
        {
            destination[count++] = wpTreeMode == JxlTreeMode.WpOnly ? JxlMaConstants.WpProp : JxlMaConstants.GradientProp;
        }
        else
        {
            foreach (int property in selectedProperties)
            {
                if (wpTreeMode != JxlTreeMode.NoWp || property != JxlMaConstants.WpProp)
                {
                    destination[count++] = property;
                }
            }
        }

        if (count == 0)
        {
            throw new InvalidOperationException("The selected property set is empty.");
        }

        this.propertiesToUse = destination[..count];
        this.NumberOfStaticProperties = 0;
        for (int i = 0; i < count; i++)
        {
            if (destination[i] < JxlPredictorFacts.StaticProperties)
            {
                DebugGuard.IsTrue(i == destination[i], "Static properties must form the initial property prefix.");
                this.NumberOfStaticProperties++;
            }
        }
    }

    /// <summary>Allocates sample planes and the deduplication table for the exact accepted sample count.</summary>
    /// <param name="sampleCount">The number of samples selected by the prepared RNG sequence.</param>
    public void PrepareForSamples(int sampleCount)
    {
        this.sampleCapacity = sampleCount;
        this.residualsOwner = this.allocator.Allocate<JxlResidualToken>(checked(sampleCount * this.NumberOfPredictors));
        this.residuals = this.residualsOwner.Memory.Span;
        this.countsOwner = this.allocator.Allocate<int>(checked(sampleCount * (1 + this.NumberOfStaticProperties)));
        Span<int> counts = this.countsOwner.Memory.Span;
        this.sampleCounts = counts[..sampleCount];
        this.staticProperties = counts[sampleCount..];
        this.propertiesOwner = this.allocator.Allocate<byte>(checked(sampleCount * (this.NumberOfProperties - this.NumberOfStaticProperties)));
        this.properties = this.propertiesOwner.Memory.Span;

        // Native uses two candidate buckets and removes saturated records. Keep the same power-of-two load bound.
        // One accepted sample can add at most one distinct record; no insertion can outgrow these prepared planes.
        uint buckets = BitOperations.RoundUpToPowerOf2((uint)Math.Max(1L, ((long)sampleCount * 3) / 2));
        this.deduplicationOwner = this.allocator.Allocate<int>(checked((int)buckets));
        this.deduplicationTable = this.deduplicationOwner.Memory.Span;
        this.deduplicationTable.Fill(DeduplicationEntryUnused);
    }

    /// <summary>Inserts one selected sample, or merges its occurrence into a matching record.</summary>
    /// <param name="pixel">The source sample value.</param>
    /// <param name="sampleProperties">The original static and dynamic context properties.</param>
    /// <param name="predictions">Predictions indexed by the modular predictor enum.</param>
    public void AddSample(int pixel, scoped ReadOnlySpan<int> sampleProperties, scoped ReadOnlySpan<int> predictions)
    {
        int sample = this.NumberOfDistinctSamples;
        JxlAnsHybridUIntConfiguration coding = new(4, 1, 2);
        for (int i = 0; i < this.NumberOfPredictors; i++)
        {
            int residual = unchecked(pixel - predictions[(int)this.predictors[i]]);
            coding.Encode(JxlPackSigned.PackUnsigned(residual), out uint token, out uint bitCount, out _);
            this.residuals[(i * this.sampleCapacity) + sample] = new((int)token, (int)bitCount);
        }

        for (int i = 0; i < this.NumberOfStaticProperties; i++)
        {
            this.staticProperties[(i * this.sampleCapacity) + sample] = this.QuantizeStaticProperty(i, sampleProperties[i]);
        }

        for (int i = this.NumberOfStaticProperties; i < this.NumberOfProperties; i++)
        {
            int property = this.QuantizeProperty(i, sampleProperties[this.propertiesToUse[i]]);
            this.properties[((i - this.NumberOfStaticProperties) * this.sampleCapacity) + sample] = (byte)property;
        }

        this.sampleCounts[sample] = 1;
        this.NumberOfSamples++;
        if (!this.AddToTableAndMerge(sample))
        {
            this.NumberOfDistinctSamples++;
        }
    }

    /// <summary>Probes the native two-bucket table and commits a new record only if neither bucket matches.</summary>
    /// <param name="sample">The tentative record at the end of the logical sample planes.</param>
    /// <returns>Whether a previous record absorbed the sample.</returns>
    private bool AddToTableAndMerge(int sample)
    {
        int first = this.Hash1(sample);
        int second = this.Hash2(sample);
        int match = this.deduplicationTable[first];
        int bucket = first;
        if (match == DeduplicationEntryUnused || !this.IsSameSample(sample, match))
        {
            match = this.deduplicationTable[second];
            bucket = second;
            if (match == DeduplicationEntryUnused || !this.IsSameSample(sample, match))
            {
                if (this.deduplicationTable[first] == DeduplicationEntryUnused)
                {
                    this.deduplicationTable[first] = sample;
                }
                else if (this.deduplicationTable[second] == DeduplicationEntryUnused)
                {
                    this.deduplicationTable[second] = sample;
                }

                return false;
            }
        }

        if (++this.sampleCounts[match] == ushort.MaxValue)
        {
            this.deduplicationTable[bucket] = DeduplicationEntryUnused;
        }

        return true;
    }

    /// <summary>Computes the first native sample hash over residuals, static properties, then dynamic properties.</summary>
    /// <param name="sample">The retained or tentative record.</param>
    /// <returns>The first candidate bucket.</returns>
    private readonly int Hash1(int sample)
    {
        const ulong multiplier = 0x1e35a7bd;
        ulong hash = multiplier;
        unchecked
        {
            for (int i = 0; i < this.NumberOfPredictors; i++)
            {
                JxlResidualToken residual = this.residuals[(i * this.sampleCapacity) + sample];
                hash = (hash * multiplier) + (ulong)residual.Token;
                hash = (hash * multiplier) + (ulong)residual.NumberOfBits;
            }

            for (int i = 0; i < this.NumberOfStaticProperties; i++)
            {
                hash = (hash * multiplier) + (ulong)this.staticProperties[(i * this.sampleCapacity) + sample];
            }

            for (int i = 0; i < this.NumberOfProperties - this.NumberOfStaticProperties; i++)
            {
                hash = (hash * multiplier) + this.properties[(i * this.sampleCapacity) + sample];
            }
        }

        return (int)((hash >> 16) & (ulong)(this.deduplicationTable.Length - 1));
    }

    /// <summary>Computes the second native hash with properties first and XOR instead of addition.</summary>
    /// <param name="sample">The retained or tentative record.</param>
    /// <returns>The second candidate bucket.</returns>
    private readonly int Hash2(int sample)
    {
        const ulong multiplier = 0x1e35a7bd1e35a7bd;
        ulong hash = multiplier;
        unchecked
        {
            for (int i = 0; i < this.NumberOfStaticProperties; i++)
            {
                hash = (hash * multiplier) ^ (ulong)this.staticProperties[(i * this.sampleCapacity) + sample];
            }

            for (int i = 0; i < this.NumberOfProperties - this.NumberOfStaticProperties; i++)
            {
                hash = (hash * multiplier) ^ this.properties[(i * this.sampleCapacity) + sample];
            }

            for (int i = 0; i < this.NumberOfPredictors; i++)
            {
                JxlResidualToken residual = this.residuals[(i * this.sampleCapacity) + sample];
                hash = (hash * multiplier) ^ (ulong)residual.Token;
                hash = (hash * multiplier) ^ (ulong)residual.NumberOfBits;
            }
        }

        return (int)((hash >> 16) & (ulong)(this.deduplicationTable.Length - 1));
    }

    /// <summary>Compares every selected residual and quantized property without allocating a sample key.</summary>
    /// <param name="first">The first record.</param>
    /// <param name="second">The second record.</param>
    /// <returns>Whether all fields match.</returns>
    private readonly bool IsSameSample(int first, int second)
    {
        for (int i = 0; i < this.NumberOfPredictors; i++)
        {
            int offset = i * this.sampleCapacity;
            JxlResidualToken a = this.residuals[offset + first];
            JxlResidualToken b = this.residuals[offset + second];
            if (a.Token != b.Token || a.NumberOfBits != b.NumberOfBits)
            {
                return false;
            }
        }

        for (int i = 0; i < this.NumberOfStaticProperties; i++)
        {
            int offset = i * this.sampleCapacity;
            if (this.staticProperties[offset + first] != this.staticProperties[offset + second])
            {
                return false;
            }
        }

        for (int i = 0; i < this.NumberOfProperties - this.NumberOfStaticProperties; i++)
        {
            int offset = i * this.sampleCapacity;
            if (this.properties[offset + first] != this.properties[offset + second])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Swaps two retained records across all sample planes during tree partitioning.</summary>
    /// <param name="first">The first retained record.</param>
    /// <param name="second">The second retained record.</param>
    public void Swap(int first, int second)
    {
        for (int i = 0; i < this.NumberOfPredictors; i++)
        {
            int offset = i * this.sampleCapacity;
            RuntimeUtility.Swap(ref this.residuals[offset + first], ref this.residuals[offset + second]);
        }

        for (int i = 0; i < this.NumberOfStaticProperties; i++)
        {
            int offset = i * this.sampleCapacity;
            RuntimeUtility.Swap(ref this.staticProperties[offset + first], ref this.staticProperties[offset + second]);
        }

        for (int i = 0; i < this.NumberOfProperties - this.NumberOfStaticProperties; i++)
        {
            int offset = i * this.sampleCapacity;
            RuntimeUtility.Swap(ref this.properties[offset + first], ref this.properties[offset + second]);
        }

        RuntimeUtility.Swap(ref this.sampleCounts[first], ref this.sampleCounts[second]);
    }

    /// <summary>Builds thresholds and complete lookup maps before any sample is inserted.</summary>
    /// <param name="range">The channel and group ranges represented by this tree.</param>
    /// <param name="multiplierInfo">Forced multiplier splits.</param>
    /// <param name="groupPixelCount">The number of eligible pixels in each group.</param>
    /// <param name="channelPixelCount">The number of eligible pixels in each channel.</param>
    /// <param name="pixelSamples">Owned preparation samples. Absolute thresholds can replace these values with their magnitudes.</param>
    /// <param name="diffSamples">Owned residual preparation samples, with the same destructive absolute-value rule.</param>
    /// <param name="maxPropertyValues">The requested maximum number of quantization intervals.</param>
    public void PreQuantizeProperties(
        StaticPropertyRange range,
        scoped ReadOnlySpan<JxlModularMultiplierInfo> multiplierInfo,
        scoped ReadOnlySpan<int> groupPixelCount,
        scoped ReadOnlySpan<int> channelPixelCount,
        scoped Span<int> pixelSamples,
        scoped Span<int> diffSamples,
        int maxPropertyValues)
    {
        // Forced static splits can exceed the ordinary quantile count. Weighted thresholds use at most 55 fixed entries.
        this.thresholdCapacity = Math.Max(55, Math.Max(maxPropertyValues - 1, checked(2 * multiplierInfo.Length)));
        int thresholdLength = checked(this.NumberOfProperties * this.thresholdCapacity);
        int mapLength = checked(this.NumberOfProperties * PropertyMapSize);
        this.quantizationOwner = this.allocator.Allocate<int>(checked(this.NumberOfProperties + thresholdLength + mapLength));
        Span<int> storage = this.quantizationOwner.Memory.Span;
        this.thresholdCounts = storage[..this.NumberOfProperties];
        this.compactProperties = storage.Slice(this.NumberOfProperties, thresholdLength);
        this.propertyMapping = storage[(this.NumberOfProperties + thresholdLength)..];

        // Four cached distributions avoid rescanning pixel/residual samples for each property that uses the same thresholds.
        // Two additional rows retain sorted, unique forced channel/group thresholds. This scratch expires before collection.
        using IMemoryOwner<int> scratchOwner = this.allocator.Allocate<int>(checked(6 * this.thresholdCapacity));
        Span<int> scratch = scratchOwner.Memory.Span;
        Span<int> channelThresholds = scratch.Slice(0, this.thresholdCapacity);
        Span<int> groupThresholds = scratch.Slice(this.thresholdCapacity, this.thresholdCapacity);
        int channelCount = 0;
        int groupCount = 0;
        foreach (JxlModularMultiplierInfo multiplier in multiplierInfo)
        {
            if (multiplier.Range[0][0] != range[0][0])
            {
                channelThresholds[channelCount++] = checked((int)multiplier.Range[0][0]) - 1;
            }

            if (multiplier.Range[0][1] != range[0][1])
            {
                channelThresholds[channelCount++] = checked((int)multiplier.Range[0][1]) - 1;
            }

            if (multiplier.Range[1][0] != range[1][0])
            {
                groupThresholds[groupCount++] = checked((int)multiplier.Range[1][0]) - 1;
            }

            if (multiplier.Range[1][1] != range[1][1])
            {
                groupThresholds[groupCount++] = checked((int)multiplier.Range[1][1]) - 1;
            }
        }

        // Each forced boundary appears once, even when adjacent multiplier ranges share it.
        channelThresholds = channelThresholds[..channelCount];
        groupThresholds = groupThresholds[..groupCount];
        channelThresholds.Sort();
        groupThresholds.Sort();
        for (int kind = 0; kind < 2; kind++)
        {
            Span<int> thresholds = kind == 0 ? channelThresholds : groupThresholds;
            int unique = 0;
            foreach (int threshold in thresholds)
            {
                if (unique == 0 || thresholds[unique - 1] != threshold)
                {
                    thresholds[unique++] = threshold;
                }
            }

            if (kind == 0)
            {
                channelThresholds = thresholds[..unique];
            }
            else
            {
                groupThresholds = thresholds[..unique];
            }
        }

        InlineArray4<int> cachedCounts = default;
        InlineArray4<bool> required = default;
        foreach (int property in this.propertiesToUse)
        {
            if (property > 3 && property != JxlMaConstants.WpProp)
            {
                required[DistributionIndex(property)] = true;
            }
        }

        // Compute ordinary thresholds before converting the same owned input to magnitudes, as native enc_ma.cc requires.
        for (int kind = 0; kind < 2; kind++)
        {
            Span<int> samples = kind == 0 ? pixelSamples : diffSamples;
            int normal = 2 * kind;
            if (required[normal] || required[normal + 1])
            {
                Span<int> thresholds = scratch.Slice((normal + 2) * this.thresholdCapacity, this.thresholdCapacity);
                cachedCounts[normal] = JxlMaEncoder.QuantizeSamples(samples, maxPropertyValues, thresholds);
            }

            if (required[normal + 1])
            {
                TensorPrimitives.Abs(samples, samples);
                Span<int> thresholds = scratch.Slice((normal + 3) * this.thresholdCapacity, this.thresholdCapacity);
                cachedCounts[normal + 1] = JxlMaEncoder.QuantizeSamples(samples, maxPropertyValues, thresholds);
            }
        }

        for (int i = 0; i < this.NumberOfProperties; i++)
        {
            int property = this.propertiesToUse[i];
            Span<int> thresholds = this.compactProperties.Slice(i * this.thresholdCapacity, this.thresholdCapacity);
            int count;
            if (property is 0 or 1)
            {
                ReadOnlySpan<int> forced = property == 0 ? channelThresholds : groupThresholds;
                if (!forced.IsEmpty)
                {
                    forced.CopyTo(thresholds);
                    count = forced.Length;
                }
                else
                {
                    ReadOnlySpan<int> histogram = property == 0 ? channelPixelCount : groupPixelCount;
                    count = JxlMaEncoder.QuantizeHistogram(histogram, maxPropertyValues, thresholds);
                }
            }
            else if (property is 2 or 3)
            {
                count = maxPropertyValues - 1;
                for (int j = 0; j < count; j++)
                {
                    thresholds[j] = ((j + 1) * 256 / maxPropertyValues) - 1;
                }
            }
            else if (property == JxlMaConstants.WpProp)
            {
                ReadOnlySpan<int> weighted = WeightedThresholds(maxPropertyValues);
                weighted.CopyTo(thresholds);
                count = weighted.Length;
            }
            else
            {
                int distribution = DistributionIndex(property);
                count = cachedCounts[distribution];
                scratch.Slice((distribution + 2) * this.thresholdCapacity, count).CopyTo(thresholds);
            }

            this.thresholdCounts[i] = count;
            JxlMaEncoder.QuantMap(thresholds[..count], this.propertyMapping.Slice(i * PropertyMapSize, PropertyMapSize), PropertyMapSize, PropertyRange);
        }
    }

    /// <summary>Selects the native pixel, absolute-pixel, residual, or absolute-residual threshold distribution.</summary>
    /// <param name="property">The original non-static property ID.</param>
    /// <returns>Zero for pixel values, one for magnitudes, two for residuals, or three for residual magnitudes.</returns>
    private static int DistributionIndex(int property)
    {
        int referenceProperty = property - JxlMaConstants.NumNonrefProperties;
        if (property is 6 or 7 or 8 || (referenceProperty >= 0 && referenceProperty % 4 == 1))
        {
            return 0;
        }

        if (property is 4 or 5 || (referenceProperty >= 0 && referenceProperty % 4 == 0))
        {
            return 1;
        }

        return referenceProperty >= 0 && referenceProperty % 4 == 2 ? 3 : 2;
    }

    /// <summary>Returns the native fixed weighted-prediction thresholds for the requested quantization resolution.</summary>
    /// <param name="maxPropertyValues">The requested interval count.</param>
    /// <returns>The immutable signed error thresholds.</returns>
    private static ReadOnlySpan<int> WeightedThresholds(int maxPropertyValues)
    {
        if (maxPropertyValues < 32)
        {
            return [-127, -63, -31, -15, -7, -3, -1, 0, 1, 3, 7, 15, 31, 63, 127];
        }
        else if (maxPropertyValues < 64)
        {
            return [-255, -191, -127, -95, -63, -47, -31, -23, -15, -11, -7, -5, -3, -1, 0, 1, 3, 5, 7, 11, 15, 23, 31, 47, 63, 95, 127, 191, 255];
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

    /// <summary>Releases every allocation, including partially prepared state after an exception.</summary>
    public void Dispose()
    {
        this.AllSamplesDone();
        this.propertyIdsOwner?.Dispose();
        this.quantizationOwner?.Dispose();
        this.residualsOwner?.Dispose();
        this.countsOwner?.Dispose();
        this.propertiesOwner?.Dispose();
        this = default;
    }
}
