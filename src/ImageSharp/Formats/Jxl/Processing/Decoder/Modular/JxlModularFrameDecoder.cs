// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Numerics.Tensors;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.Fields;
using SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;
using SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Ans;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Transforms;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Quantization;
using Tree = System.Collections.Generic.List<SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.JxlPropertyDecisionNode>;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Modular;

/// <summary>
/// Decodes global modular data and owns its integer sample channels for one frame.
/// </summary>
/// <param name="configuration">The configuration used for owned sample allocations.</param>
/// <param name="frameDimensions">The borrowed parsed frame dimensions.</param>
internal sealed class JxlModularFrameDecoder(Configuration configuration, JxlFrameDimensions frameDimensions) : IDisposable
{
    private readonly Configuration configuration = configuration;

    /// <summary>
    /// The owned global channels. A failed replacement leaves its partial channels local to DecodeGlobalInfo.
    /// </summary>
    private JxlModularImage fullImage = new();

    private readonly List<JxlTransform> globalTransform = [];
    private readonly JxlFrameDimensions frameDimensions = frameDimensions;
    private bool doColor;
    private bool haveSomething;
    private bool useFullImage = true;
    private bool allSameShift;
    private readonly Tree tree = [];
    private JxlAnsCode code = new();
    private readonly List<byte> contextMap = [];
    private JxlGroupHeader groupHeader = new();

    public Configuration Configuration => this.configuration;

    public bool UsesFullImage => this.useFullImage;

    public bool ContainsDc => this.haveSomething;

    private static void MultiplySum(int xSize, ReadOnlySpan<int> rowIn, ReadOnlySpan<int> rowInY, float factor, Span<float> rowOut)
    {
        Vector<float> factorV = new(factor);

        int lanes = Vector<float>.Count;

        for (int x = 0; x < xSize; x += lanes)
        {
            Vector<float> inV = Vector.ConvertToSingle(Vector.Create(rowIn[x..]));
            inV += Vector.ConvertToSingle(Vector.Create(rowInY[x..]));

            Vector<float> outV = inV * factorV;
            outV.CopyTo(rowOut[x..]);
        }
    }

    private static void RgbFromSingle(int xSize, ReadOnlySpan<int> rowIn, float factor, Span<float> outR, Span<float> outG, Span<float> outB)
    {
        Vector<float> factorV = new(factor);

        int lanes = Vector<float>.Count;

        for (int x = 0; x < xSize; x += lanes)
        {
            Vector<float> inV = Vector.ConvertToSingle(Vector.Create(rowIn[x..]));
            Vector<float> outV = inV * factorV;

            outV.CopyTo(outR[x..]);
            outV.CopyTo(outG[x..]);
            outV.CopyTo(outB[x..]);
        }
    }

    private static void SingleFromSingle(int xSize, ReadOnlySpan<int> rowIn, float factor, Span<float> rowOut)
    {
        Vector<float> factorV = new(factor);

        int lanes = Vector<float>.Count;

        for (int x = 0; x < xSize; x += lanes)
        {
            Vector<float> inV = Vector.ConvertToSingle(Vector.Create(rowIn[x..]));
            Vector<float> outV = inV * factorV;

            outV.CopyTo(rowOut[x..]);
        }
    }

    private static void SingleFromSingleAccurate(int xSize, Span<int> rowIn, float factor, Span<float> rowOut)
    {
        // TensorPrimitives doesn't have a .ConvertToSingle
        // with Span<int> as input.
        for (int x = 0; x < xSize; x++)
        {
            rowOut[x] = rowIn[x] * factor;
        }
    }

    private static void IntToFloat(ReadOnlySpan<float> rowIn, Span<float> rowOut, int xSize, int bits, int expBits)
    {
        if (bits == 32)
        {
            if (expBits != 8)
            {
                throw new InvalidOperationException("Exponent bits must be equal to 8");
            }

            rowIn[..xSize].CopyTo(rowOut);
            return;
        }

        int expBias = (1 << (expBits - 1)) - 1;
        int signShift = bits - 1;
        int mantBits = bits - expBits - 1;
        int mantShift = 23 - mantBits;

        for (int x = 0; x < xSize; x++)
        {
            uint f = unchecked((uint)BitConverter.SingleToInt32Bits(rowIn[x]));
            uint signBit = f >> signShift;

            f &= (1u << signShift) - 1;

            if (f == 0)
            {
                rowOut[x] = signBit != 0 ? -0f : 0f;
                continue;
            }

            int exp = (int)(f >> mantBits);
            int mantissa = (int)(f & ((1u << mantBits) - 1));

            if (exp == (1 << expBits) - 1)
            {
                // NaN or infinity.
                uint result = signBit != 0 ? 0x80000000u : 0u;
                result |= 0xFFu << 23;
                result |= (uint)(mantissa << mantShift);

                rowOut[x] = BitConverter.Int32BitsToSingle(unchecked((int)result));
                continue;
            }

            mantissa <<= mantShift;

            // Try to normalize only if there is space for maneuver.
            if (exp == 0 && expBits < 8)
            {
                // Subnormal number.
                while ((mantissa & 0x800000) == 0)
                {
                    mantissa <<= 1;
                    exp--;
                }

                exp++;

                // Remove leading 1 because it is implicit now.
                mantissa &= 0x7FFFFF;
            }

            exp -= expBias;

            // Break up the arbitrary float into its parts, then reassemble
            // into binary32.
            exp += 127;

            if (exp < 0)
            {
                throw new InvalidOperationException("Exponent cannot be negative");
            }

            uint resultBits = signBit != 0 ? 0x80000000u : 0u;
            resultBits |= (uint)(exp << 23);
            resultBits |= (uint)mantissa;

            rowOut[x] = BitConverter.Int32BitsToSingle(unchecked((int)resultBits));
        }
    }

    /// <summary>Reads one positive raw quantization table through the existing modular decoder.</summary>
    /// <param name="configuration">The configuration that owns decoded channels and retained raw values.</param>
    /// <param name="requiredSizeX">The table width in coefficients.</param>
    /// <param name="requiredSizeY">The table height in coefficients.</param>
    /// <param name="reader">The reader positioned at the F16 table denominator.</param>
    /// <param name="encoding">The descriptor that owns the completed raw table.</param>
    /// <param name="index">The format's quantization-table index.</param>
    /// <param name="decoder">The frame's global modular context, or null for a standalone parameter roundtrip.</param>
    /// <returns>Whether the denominator and complete modular table were decoded successfully.</returns>
    public static bool DecodeQuantizerTable(
        Configuration configuration,
        int requiredSizeX,
        int requiredSizeY,
        JxlBitReader reader,
        JxlQuantizerEncoding encoding,
        int index,
        JxlModularFrameDecoder? decoder)
    {
        bool complete = false;
        try
        {
            float denominator = 0;
            if (!JxlF16Coder.Read(reader, ref denominator))
            {
                return false;
            }

            // Native raw weights require both a positive denominator and positive integer entries.
            // F16 read rejects nonfinite fields; this additional bound excludes zero and negative denominators.
            if (denominator < JxlQuantizerConstants.MinimumWeight)
            {
                throw new InvalidImageContentException("The raw quantization denominator is too small.");
            }

            encoding.QuantizationTableDenominator = denominator;
            using JxlModularImage image = JxlModularImage.Create(configuration, requiredSizeX, requiredSizeY, 8, 3);
            JxlGroupHeader header = new();
            JxlModularOptions options = new();
            int groupId = decoder is null ? 0 : JxlModularStreamId.CreateQuantizerTable(index).GetId(decoder.frameDimensions);
            if (!JxlModularEncoding.ModularGenericDecompress(
                configuration,
                reader,
                image,
                header,
                groupId,
                options,
                true,
                decoder?.tree,
                decoder?.code,
                decoder?.contextMap,
                false))
            {
                return false;
            }

            // Retain logical coefficients after the temporary modular image is disposed. Its physical row padding is not table data.
            // Borrow each channel span once, then copy into the descriptor's single packed X, Y and B allocation.
            Span<int> destination = encoding.PrepareRawTable(configuration, requiredSizeX * requiredSizeY * 3);
            for (int c = 0; c < 3; c++)
            {
                JxlModularChannel channel = image.Channels[c];
                ReadOnlySpan<int> channelSamples = channel.GetRow(0);
                int stride = channel.Plane.PixelsPerRow;
                int channelOffset = c * requiredSizeX * requiredSizeY;
                for (int y = 0; y < requiredSizeY; y++)
                {
                    ReadOnlySpan<int> row = channelSamples.Slice(y * stride, requiredSizeX);
                    if (TensorPrimitives.Min(row) <= 0)
                    {
                        throw new InvalidImageContentException("The raw quantization table contains a nonpositive value.");
                    }

                    row.CopyTo(destination.Slice(channelOffset + (y * requiredSizeX), requiredSizeX));
                }
            }

            complete = true;
            return true;
        }
        finally
        {
            if (!complete)
            {
                // A failed replacement must not retain partially copied raw coefficients, including a reused allocation.
                encoding.Dispose();
            }
        }
    }

    /// <summary>
    /// Decodes the global modular channels and replaces this decoder's previous owned image only after success.
    /// </summary>
    /// <param name="reader">The global modular bitstream.</param>
    /// <param name="frameHeader">The parsed frame header that determines channel dimensions and sampling.</param>
    /// <param name="allowTruncatedGroup">Whether the existing modular decoder may accept a truncated group.</param>
    public void DecodeGlobalInfo(JxlBitReader reader, JxlFrameHeader frameHeader, bool allowTruncatedGroup)
    {
        bool decodeColor = frameHeader.Encoding == JxlFrameEncoding.Modular;
        JxlImageMetadata metadata = frameHeader.Metadata!.ImageMetadata;
        bool isGray = metadata.ColorEncoding.IsGray;

        // We initially assume we have three color channels.
        // It could be XYB, RGB, etc. See the if statement
        // that follows immediately after this variable.
        int numberOfChannels = 3;

        if (isGray && frameHeader.ColorTransform == JxlColorTransform.None)
        {
            // A grayscale channel can only adjust light, so it has
            // no other colors. Meanwhile, having no additional color transforms
            // means the entire color system is limited to just one
            // color. That color is how bright or dark each pixel is: grayscale.
            // So set number of channels to 1.
            numberOfChannels = 1;
        }

        this.doColor = decodeColor;

        int extraChannelCount = metadata.ExtraChannels.Count;

        // When we decode global info, the first bit indicates whether
        // the MA tree is present.
        bool hasTree = reader.ReadBoolean();

        if (!allowTruncatedGroup ||

            // This condition checks whether the total bits
            // consumed are not byte-aligned.
            // For example, 12 bits. An aligned variant would be
            // 16 bits or 8 bits.
            reader.TotalBitsConsumed < reader.TotalBytes * JxlMath.BitsPerByte)
        {
            if (hasTree)
            {
                const int initialTreeNodes = 1024;
                const long samplesPerTreeNode = 16;

                // The format allows 1024 base nodes and one additional node per 16 samples, up to MaxTreeSize.
                // At least one color channel exists here. Limit pixels first: larger counts already reach the node cap with one channel.
                // This keeps both multiplications within signed 64-bit bounds for int-sized dimensions and channel counts.
                long pixels = Math.Min((long)this.frameDimensions.XSize * this.frameDimensions.YSize, JxlMaConstants.MaxTreeSize * samplesPerTreeNode);
                long channels = numberOfChannels + (long)extraChannelCount;
                int treeSizeLimit = (int)Math.Min(JxlMaConstants.MaxTreeSize, initialTreeNodes + (pixels * channels / samplesPerTreeNode));

                // Decoding the actual MA tree now, followed by the ANS histograms.
                JxlMaDecoder.DecodeTree(this.configuration, reader, this.tree, treeSizeLimit);
                JxlAnsReader.DecodeHistograms(this.configuration, reader, (this.tree.Count + 1) / 2, this.code, this.contextMap);
            }
        }

        if (!this.doColor)
        {
            // When we don't have to process any colors,
            // we effectively have 0 channels.
            //
            // In this function, this can only happen if
            // the frame header says the coding mode for the
            // input JPEG-XL file is VarDCT, which is different
            // from Modular.
            numberOfChannels = 0;
        }

        // Does the file use floating-point pixels?
        bool fp = metadata.BitDepth!.FloatingPointSample;

        // This statement only does validation.
        if (metadata.BitDepth.BitsPerSample >= 32 &&
            this.doColor &&
            frameHeader.ColorTransform != JxlColorTransform.Xyb)
        {
            // We use more than 32 bits per sample, we process
            // at least one or more colors, and the color
            // transform is either Y'Cb'Cr or none.
            if (metadata.BitDepth.BitsPerSample == 32 && !fp)
            {
                // We use exactly 32 bits per sample, but we
                // do not use floating-point samples.
                //
                // This would have the effect of uint32 samples,
                // but the Modular coding mode does not officially
                // support this.
                throw new NotSupportedException("JPEG-XL does not support uint32 samples in Modular");
            }

            if (metadata.BitDepth.BitsPerSample > 32)
            {
                // Additionally, the Modular coding mode
                // does not support more than 32 bits per
                // sample.
                throw new NotSupportedException("Too many bits per sample");
            }
        }

        // This is the image that will be returned.
        // The amount of channels is the number of channels
        // we have in the frame header, plus extra channels
        // that might be present.
        JxlModularImage img = JxlModularImage.Create(
            this.configuration,
            this.frameDimensions.XSize,
            this.frameDimensions.YSize,
            (int)metadata.BitDepth.BitsPerSample,
            numberOfChannels + extraChannelCount);

        try
        {
            this.allSameShift = true;

            if (frameHeader.ColorTransform == JxlColorTransform.YCbCr)
            {
                JxlYCbCrChromaSubsampling subsampling = frameHeader.ChromaSubsampling!;

                // For Y'Cb'Cr color transforms, we adjust our image
                // for chroma subsampling.
                for (int c = 0; c < numberOfChannels; c++)
                {
                    // Reference to the channel of the current index.
                    JxlModularChannel channel = img.Channels[c];

                    // First, we compute the shifts, which are for the
                    // chroma subsampling and therefore specific to the
                    // Y'Cb'Cr chroma transform.
                    channel.HorizontalShift = subsampling.HShift(c);
                    channel.VerticalShift = subsampling.VShift(c);

                    // Then we compute the widths and heights that are
                    // divided by the shifts. That's because in Y'Cb'Cr
                    // chroma subsampling, the luma (Y) part is usually
                    // the same, while the Chroma (Cb, Cr) part is downsampled.
                    // So the size for the chroma channel is not the same
                    // as the original image.
                    // See https://en.wikipedia.org/wiki/Chroma_subsampling
                    int xSizeShifted = JxlMath.DivCeil(this.frameDimensions.XSize, 1 << channel.HorizontalShift);
                    int ySizeShifted = JxlMath.DivCeil(this.frameDimensions.YSize, 1 << channel.VerticalShift);

                    // The chroma plane should be shrinked down to its downsampled
                    // size, as part of chroma subsampling.
                    //
                    // Note that if this is channel 0, which is a Luma channel,
                    // xSizeShifted and ySizeShifted will be the same as the channel's
                    // original width or height. There's no shift as Luma channels
                    // do not have downsampling; it's the Chroma channels that are
                    // downsampled. Meaning, this .Shrink() call wouldn't really do
                    // anything.
                    channel.Shrink(xSizeShifted, ySizeShifted);

                    // Now we check if the shift is the same for all channels.
                    // If they are not, then we know that the shift isn't
                    // the same for all channels.
                    //
                    // This wouldn't really work for the 0 channel, but that's
                    // expected as the 0 channel is the Luma channel which isn't
                    // really downsampled. For channels 1 and 2 which are chroma
                    // channels, this should work.
                    if (channel.HorizontalShift != img.Channels[0].HorizontalShift ||
                        channel.VerticalShift != img.Channels[0].VerticalShift)
                    {
                        this.allSameShift = false;
                    }
                }
            }

            // For the extra channels, we do almost the same as the
            // chroma subsampling for Y'Cb'Cr. Except, extra channels have
            // their own upsampling matrix, which we use.
            //
            // The upsampling matrix is a List<uint>, which can be
            // slow. So we can use a Span<uint> wrapper which is more
            // lightweight and faster.
            Span<uint> extraChannelUpsampling = CollectionsMarshal.AsSpan(frameHeader.ExtraChannelUpsampling);

            // We have two loop variables.
            //
            // 'ec', short for Extra Channel, is the index of the extra channel.
            // We need this to access the upsampling matrix which is specific for extra channels.
            //
            // 'c', on the other hand, just means channel. It is initially set to
            // numberOfChannels, because that's where the first extra channel starts.
            //
            // Suppose our image is coded using RGB and we have two extra channels, one
            // alpha channel and one black channel. In such a case:
            //   - numberOfChannels would be 3. We have 3 channels - R, G, and B.
            //   - That also means the first index of the extra channel is the last index
            //     of the RGB channel (2), plus 1. So that's 3.
            //
            // From then on, 'c' would keep incrementing, to access a generic list of channels.
            for (int ec = 0, c = numberOfChannels; ec < extraChannelCount; ec++, c++)
            {
                // Upsampling for this specific extra channel
                int upsampling = (int)extraChannelUpsampling[ec];

                // Reference to the channel of the current index.
                JxlModularChannel channel = img.Channels[c];

                // We shrink the channel, so it becomes 'upsampling' times smaller
                // than its upsampled size.
                channel.Shrink(
                    JxlMath.DivCeil(this.frameDimensions.XSizeUpsampled, upsampling),
                    JxlMath.DivCeil(this.frameDimensions.YSizeUpsampled, upsampling));

                // Then we have to adjust the shifts.
                // Both the horizontal shift and the vertical shift are
                // equal to Ceil(Log2(upsampling for this channel)) - Ceil(Log2(upsampling for the frame sizes)).
                channel.HorizontalShift = channel.VerticalShift =
                    JxlMath.CeilLog2Nonzero(upsampling) - JxlMath.CeilLog2Nonzero((int)frameHeader.Upsampling);

                // Now we want to make sure whether or not are shifts different
                // for each channel.
                if (channel.HorizontalShift != img.Channels[0].HorizontalShift ||
                    channel.VerticalShift != img.Channels[0].VerticalShift)
                {
                    this.allSameShift = false;
                }
            }

            // Main Modular decoding logic here. The result is stored
            // into img.
            JxlModularOptions options = new()
            {
                // Both the maximum channel size and the group dimension
                // is set equal to the group dimension specified in the
                // frame dimensions.
                MaxChannelSize = this.frameDimensions.GroupDimension,
                GroupDimension = this.frameDimensions.GroupDimension
            };

            // Do the actual Modular decode using the JxlModularOptions.
            bool decompressedSuccessfully = JxlModularEncoding.ModularGenericDecompress(
                this.configuration,
                reader,
                img,
                this.groupHeader,
                JxlModularStreamId.Global.GetId(this.frameDimensions),
                options,
                undoTransforms: false,
                this.tree,
                this.code,
                this.contextMap,
                allowTruncatedGroup);

            // The ModularGenericDecompress method returns a boolean indicating
            // whether or not has the modular decoding process completed successfully.
            // When this is false, we know that the decoding process failed, so
            // throw an exception.
            if (!decompressedSuccessfully)
            {
                throw new InvalidOperationException("Could not decode global Modular info");
            }

            // Initially we mark this Modular decoder as empty.
            // Then, go through each channel. If each channel is
            // within the frame dimensions, then at least one channel is
            // therefore usable, so we will mark this Modular
            // decoder as not empty.
            this.haveSomething = false;
            for (int c = 0; c < img.Channels.Count; c++)
            {
                JxlModularChannel channel = img.Channels[c];

                // We want to make sure that the number of channels are >= the number
                // of meta channels, and that each dimension fits within group dimensions.
                if (c >= img.MetaChannels &&
                    channel.Width <= this.frameDimensions.GroupDimension &&
                    channel.Height <= this.frameDimensions.GroupDimension)
                {
                    this.haveSomething = true;
                    break;
                }
            }

            // We have to move global transforms to groups, if both of these things happen:
            //   - Number of channels are < number of meta channels,
            //     or some channels are larger than group dimensions
            //   - Chroma subsampling is 4:4:4 or 4:0:0 (if the image uses Y'Cb'Cr),
            //     there are no extra channels, OR there are extra channels but they
            //     aren't upsampled - therefore, every channel has the same exact shift.
            if (!this.haveSomething && this.allSameShift)
            {
                // If we only have one transform and that transform is the Reversible
                // Color Transform (RCT), then we set the global transform to the RCT,
                // and we remove it from 'img'.
                if (img.Transforms.Count == 1 && img.Transforms[0].TransformType == JxlTransformType.Rct)
                {
                    this.globalTransform.Clear();
                    this.globalTransform.Add(img.Transforms[0]);
                    img.Transforms.Clear();
                }
            }

            // Release the previous owned image only after the replacement is complete. No channel storage is borrowed from img.
            this.fullImage.Dispose();
            this.fullImage = img;
        }
        catch
        {
            // The decoder takes ownership only after global parsing and transform preparation both succeed.
            img.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public void Dispose() => this.fullImage.Dispose();
}
