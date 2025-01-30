// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Webp.BitReader;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Webp.Lossless;

/// <summary>
/// Decoder for lossless webp images. This code is a port of libwebp, which can be found here: https://chromium.googlesource.com/webm/libwebp
/// </summary>
/// <remarks>
/// The lossless specification can be found here:
/// https://developers.google.com/speed/webp/docs/webp_lossless_bitstream_specification
/// </remarks>
internal sealed class WebpLosslessDecoder
{
    /// <summary>
    /// A bit reader for reading lossless webp streams.
    /// </summary>
    private readonly Vp8LBitReader bitReader;

    /// <summary>
    /// The global configuration.
    /// </summary>
    private readonly Configuration configuration;

    /// <summary>
    /// Used for allocating memory during processing operations.
    /// </summary>
    private readonly MemoryAllocator memoryAllocator;

    private const int BitsSpecialMarker = 0x100;

    private const uint PackedNonLiteralCode = 0;

    private static readonly int CodeToPlaneCodes = WebpLookupTables.CodeToPlane.Length;

    // Memory needed for lookup tables of one Huffman tree group. Red, blue, alpha and distance alphabets are constant (256 for red, blue and alpha, 40 for
    // distance) and lookup table sizes for them in worst case are 630 and 410 respectively. Size of green alphabet depends on color cache size and is equal
    // to 256 (green component values) + 24 (length prefix values) + color_cache_size (between 0 and 2048).
    // All values computed for 8-bit first level lookup with Mark Adler's tool:
    // http://www.hdfgroup.org/ftp/lib-external/zlib/zlib-1.2.5/examples/enough.c
    private const int FixedTableSize = (630 * 3) + 410;

    private static readonly int[] TableSize =
    {
        FixedTableSize + 654,
        FixedTableSize + 656,
        FixedTableSize + 658,
        FixedTableSize + 662,
        FixedTableSize + 670,
        FixedTableSize + 686,
        FixedTableSize + 718,
        FixedTableSize + 782,
        FixedTableSize + 912,
        FixedTableSize + 1168,
        FixedTableSize + 1680,
        FixedTableSize + 2704
    };

    private static readonly int NumCodeLengthCodes = CodeLengthCodeOrder.Length;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebpLosslessDecoder"/> class.
    /// </summary>
    /// <param name="bitReader">Bitreader to read from the stream.</param>
    /// <param name="memoryAllocator">Used for allocating memory during processing operations.</param>
    /// <param name="configuration">The configuration.</param>
    public WebpLosslessDecoder(Vp8LBitReader bitReader, MemoryAllocator memoryAllocator, Configuration configuration)
    {
        this.bitReader = bitReader;
        this.memoryAllocator = memoryAllocator;
        this.configuration = configuration;
    }

    // This uses C#'s compiler optimization to refer to assembly's static data directly.
    private static ReadOnlySpan<byte> CodeLengthCodeOrder => new byte[] { 17, 18, 0, 1, 2, 3, 4, 5, 16, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };

    // This uses C#'s compiler optimization to refer to assembly's static data directly.
    private static ReadOnlySpan<byte> LiteralMap => new byte[] { 0, 1, 1, 1, 0 };

    /// <summary>
    /// Decodes the lossless webp image from the stream.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="pixels">The pixel buffer to store the decoded data.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    public void Decode<TPixel>(Buffer2D<TPixel> pixels, int width, int height)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Vp8LDecoder decoder = new(width, height, this.memoryAllocator);
        this.DecodeImageStream(decoder, width, height, true);
        this.DecodeImageData(decoder, decoder.Pixels.Memory.Span);
        this.DecodePixelValues(decoder, pixels, width, height);
    }

    public IMemoryOwner<uint> DecodeImageStream(Vp8LDecoder decoder, int xSize, int ySize, bool isLevel0)
    {
        int transformXSize = xSize;
        int transformYSize = ySize;
        int numberOfTransformsPresent = 0;
        if (isLevel0)
        {
            decoder.Transforms = new(WebpConstants.MaxNumberOfTransforms);

            // Next bit indicates, if a transformation is present.
            while (this.bitReader.ReadBit())
            {
                if (numberOfTransformsPresent > WebpConstants.MaxNumberOfTransforms)
                {
                    WebpThrowHelper.ThrowImageFormatException($"The maximum number of transforms of {WebpConstants.MaxNumberOfTransforms} was exceeded");
                }

                this.ReadTransformation(transformXSize, transformYSize, decoder);
                if (decoder.Transforms[numberOfTransformsPresent].TransformType == Vp8LTransformType.ColorIndexingTransform)
                {
                    transformXSize = LosslessUtils.SubSampleSize(transformXSize, decoder.Transforms[numberOfTransformsPresent].Bits);
                }

                numberOfTransformsPresent++;
            }
        }
        else
        {
            decoder.Metadata = new();
        }

        // Color cache.
        bool isColorCachePresent = this.bitReader.ReadBit();
        int colorCacheBits = 0;
        int colorCacheSize = 0;
        if (isColorCachePresent)
        {
            colorCacheBits = (int)this.bitReader.ReadValue(4);

            // Note: According to webpinfo color cache bits of 11 are valid, even though 10 is defined in the source code as maximum.
            // That is why 11 bits is also considered valid here.
            bool colorCacheBitsIsValid = colorCacheBits is >= 1 and <= WebpConstants.MaxColorCacheBits + 1;
            if (!colorCacheBitsIsValid)
            {
                WebpThrowHelper.ThrowImageFormatException("Invalid color cache bits found");
            }
        }

        // Read the Huffman codes (may recurse).
        this.ReadHuffmanCodes(decoder, transformXSize, transformYSize, colorCacheBits, isLevel0);
        decoder.Metadata.ColorCacheSize = colorCacheSize;

        // Finish setting up the color-cache.
        if (isColorCachePresent)
        {
            decoder.Metadata.ColorCache = new(colorCacheBits);
            colorCacheSize = 1 << colorCacheBits;
            decoder.Metadata.ColorCacheSize = colorCacheSize;
        }
        else
        {
            decoder.Metadata.ColorCacheSize = 0;
        }

        UpdateDecoder(decoder, transformXSize, transformYSize);
        if (isLevel0)
        {
            // level 0 complete.
            return null;
        }

        // Use the Huffman trees to decode the LZ77 encoded data.
        IMemoryOwner<uint> pixelData = this.memoryAllocator.Allocate<uint>(decoder.Width * decoder.Height, AllocationOptions.Clean);
        this.DecodeImageData(decoder, pixelData.GetSpan());

        return pixelData;
    }

    private void DecodePixelValues<TPixel>(Vp8LDecoder decoder, Buffer2D<TPixel> pixels, int width, int height)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Span<uint> pixelData = decoder.Pixels.GetSpan();

        // Apply reverse transformations, if any are present.
        ApplyInverseTransforms(decoder, pixelData, this.memoryAllocator);

        Span<byte> pixelDataAsBytes = MemoryMarshal.Cast<uint, byte>(pixelData);
        int bytesPerRow = width * 4;
        for (int y = 0; y < height; y++)
        {
            Span<byte> rowAsBytes = pixelDataAsBytes.Slice(y * bytesPerRow, bytesPerRow);
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y);
            PixelOperations<TPixel>.Instance.FromBgra32Bytes(
                this.configuration,
                rowAsBytes[..bytesPerRow],
                pixelRow[..width],
                width);
        }
    }

    public void DecodeImageData(Vp8LDecoder decoder, Span<uint> pixelData)
    {
        const int lastPixel = 0;
        int width = decoder.Width;
        int height = decoder.Height;
        int row = lastPixel / width;
        int col = lastPixel % width;
        const int lenCodeLimit = WebpConstants.NumLiteralCodes + WebpConstants.NumLengthCodes;
        int colorCacheSize = decoder.Metadata.ColorCacheSize;
        ColorCache colorCache = decoder.Metadata.ColorCache;
        int colorCacheLimit = lenCodeLimit + colorCacheSize;
        int mask = decoder.Metadata.HuffmanMask;
        Span<HTreeGroup> hTreeGroup = GetHTreeGroupForPos(decoder.Metadata, col, row);

        int totalPixels = width * height;
        int decodedPixels = 0;
        int lastCached = decodedPixels;
        while (decodedPixels < totalPixels)
        {
            int code;
            if ((col & mask) == 0)
            {
                hTreeGroup = GetHTreeGroupForPos(decoder.Metadata, col, row);
            }

            if (hTreeGroup[0].IsTrivialCode)
            {
                pixelData[decodedPixels] = hTreeGroup[0].LiteralArb;
                AdvanceByOne(ref col, ref row, width, colorCache, ref decodedPixels, pixelData, ref lastCached);
                continue;
            }

            this.bitReader.FillBitWindow();
            if (hTreeGroup[0].UsePackedTable)
            {
                code = (int)this.ReadPackedSymbols(hTreeGroup, pixelData, decodedPixels);
                if (this.bitReader.IsEndOfStream())
                {
                    break;
                }

                if (code == PackedNonLiteralCode)
                {
                    AdvanceByOne(ref col, ref row, width, colorCache, ref decodedPixels, pixelData, ref lastCached);
                    continue;
                }
            }
            else
            {
                code = (int)this.ReadSymbol(hTreeGroup[0].HTrees[HuffIndex.Green]);
            }

            if (this.bitReader.IsEndOfStream())
            {
                break;
            }

            // Literal
            if (code < WebpConstants.NumLiteralCodes)
            {
                if (hTreeGroup[0].IsTrivialLiteral)
                {
                    pixelData[decodedPixels] = hTreeGroup[0].LiteralArb | ((uint)code << 8);
                }
                else
                {
                    uint red = this.ReadSymbol(hTreeGroup[0].HTrees[HuffIndex.Red]);
                    this.bitReader.FillBitWindow();
                    uint blue = this.ReadSymbol(hTreeGroup[0].HTrees[HuffIndex.Blue]);
                    uint alpha = this.ReadSymbol(hTreeGroup[0].HTrees[HuffIndex.Alpha]);
                    if (this.bitReader.IsEndOfStream())
                    {
                        break;
                    }

                    pixelData[decodedPixels] = (uint)(((byte)alpha << 24) | ((byte)red << 16) | ((byte)code << 8) | (byte)blue);
                }

                AdvanceByOne(ref col, ref row, width, colorCache, ref decodedPixels, pixelData, ref lastCached);
            }
            else if (code < lenCodeLimit)
            {
                // Backward reference is used.
                int lengthSym = code - WebpConstants.NumLiteralCodes;
                int length = this.GetCopyLength(lengthSym);
                uint distSymbol = this.ReadSymbol(hTreeGroup[0].HTrees[HuffIndex.Dist]);
                this.bitReader.FillBitWindow();
                int distCode = this.GetCopyDistance((int)distSymbol);
                int dist = PlaneCodeToDistance(width, distCode);
                if (this.bitReader.IsEndOfStream())
                {
                    break;
                }

                CopyBlock(pixelData, decodedPixels, dist, length);
                decodedPixels += length;
                col += length;
                while (col >= width)
                {
                    col -= width;
                    row++;
                }

                if ((col & mask) != 0)
                {
                    hTreeGroup = GetHTreeGroupForPos(decoder.Metadata, col, row);
                }

                if (colorCache != null)
                {
                    while (lastCached < decodedPixels)
                    {
                        colorCache.Insert(pixelData[lastCached]);
                        lastCached++;
                    }
                }
            }
            else if (code < colorCacheLimit)
            {
                // Color cache should be used.
                int key = code - lenCodeLimit;
                while (lastCached < decodedPixels)
                {
                    colorCache.Insert(pixelData[lastCached]);
                    lastCached++;
                }

                pixelData[decodedPixels] = colorCache.Lookup(key);
                AdvanceByOne(ref col, ref row, width, colorCache, ref decodedPixels, pixelData, ref lastCached);
            }
            else
            {
                WebpThrowHelper.ThrowImageFormatException("Webp parsing error");
            }
        }
    }

    private static void AdvanceByOne(ref int col, ref int row, int width, ColorCache colorCache, ref int decodedPixels, Span<uint> pixelData, ref int lastCached)
    {
        col++;
        decodedPixels++;
        if (col >= width)
        {
            col = 0;
            row++;

            if (colorCache != null)
            {
                while (lastCached < decodedPixels)
                {
                    colorCache.Insert(pixelData[lastCached]);
                    lastCached++;
                }
            }
        }
    }

    private void ReadHuffmanCodes(Vp8LDecoder decoder, int xSize, int ySize, int colorCacheBits, bool allowRecursion)
    {
        int maxAlphabetSize = 0;
        int numHTreeGroups = 1;
        int numHTreeGroupsMax = 1;

        // If the next bit is zero, there is only one meta Huffman code used everywhere in the image. No more data is stored.
        // If this bit is one, the image uses multiple meta Huffman codes. These meta Huffman codes are stored as an entropy image.
        if (allowRecursion && this.bitReader.ReadBit())
        {
            // Use meta Huffman codes.
            int huffmanPrecision = (int)(this.bitReader.ReadValue(3) + 2);
            int huffmanXSize = LosslessUtils.SubSampleSize(xSize, huffmanPrecision);
            int huffmanYSize = LosslessUtils.SubSampleSize(ySize, huffmanPrecision);
            int huffmanPixels = huffmanXSize * huffmanYSize;

            IMemoryOwner<uint> huffmanImage = this.DecodeImageStream(decoder, huffmanXSize, huffmanYSize, false);
            Span<uint> huffmanImageSpan = huffmanImage.GetSpan();
            decoder.Metadata.HuffmanSubSampleBits = huffmanPrecision;

            // TODO: Isn't huffmanPixels the length of the span?
            for (int i = 0; i < huffmanPixels; i++)
            {
                // The huffman data is stored in red and green bytes.
                uint group = (huffmanImageSpan[i] >> 8) & 0xffff;
                huffmanImageSpan[i] = group;
                if (group >= numHTreeGroupsMax)
                {
                    numHTreeGroupsMax = (int)group + 1;
                }
            }

            numHTreeGroups = numHTreeGroupsMax;
            decoder.Metadata.HuffmanImage = huffmanImage;
        }

        // Find maximum alphabet size for the hTree group.
        for (int j = 0; j < WebpConstants.HuffmanCodesPerMetaCode; j++)
        {
            int alphabetSize = WebpConstants.AlphabetSize[j];
            if (j == 0 && colorCacheBits > 0)
            {
                alphabetSize += 1 << colorCacheBits;
            }

            if (maxAlphabetSize < alphabetSize)
            {
                maxAlphabetSize = alphabetSize;
            }
        }

        int tableSize = TableSize[colorCacheBits];
        HuffmanCode[] huffmanTables = new HuffmanCode[numHTreeGroups * tableSize];
        HTreeGroup[] hTreeGroups = new HTreeGroup[numHTreeGroups];
        Span<HuffmanCode> huffmanTable = huffmanTables.AsSpan();
        int[] codeLengths = new int[maxAlphabetSize];
        for (int i = 0; i < numHTreeGroupsMax; i++)
        {
            hTreeGroups[i] = new(HuffmanUtils.HuffmanPackedTableSize);
            HTreeGroup hTreeGroup = hTreeGroups[i];
            int totalSize = 0;
            bool isTrivialLiteral = true;
            int maxBits = 0;
            codeLengths.AsSpan().Clear();
            for (int j = 0; j < WebpConstants.HuffmanCodesPerMetaCode; j++)
            {
                int alphabetSize = WebpConstants.AlphabetSize[j];
                if (j == 0 && colorCacheBits > 0)
                {
                    alphabetSize += 1 << colorCacheBits;
                }

                int size = this.ReadHuffmanCode(alphabetSize, codeLengths, huffmanTable);
                if (size == 0)
                {
                    WebpThrowHelper.ThrowImageFormatException("Huffman table size is zero");
                }

                // TODO: Avoid allocation.
                hTreeGroup.HTrees.Add(huffmanTable[..size].ToArray());

                HuffmanCode huffTableZero = huffmanTable[0];
                if (isTrivialLiteral && LiteralMap[j] == 1)
                {
                    isTrivialLiteral = huffTableZero.BitsUsed == 0;
                }

                totalSize += huffTableZero.BitsUsed;
                huffmanTable = huffmanTable[size..];

                if (j <= HuffIndex.Alpha)
                {
                    int localMaxBits = codeLengths[0];
                    int k;
                    for (k = 1; k < alphabetSize; ++k)
                    {
                        int codeLengthK = codeLengths[k];
                        if (codeLengthK > localMaxBits)
                        {
                            localMaxBits = codeLengthK;
                        }
                    }

                    maxBits += localMaxBits;
                }
            }

            hTreeGroup.IsTrivialLiteral = isTrivialLiteral;
            hTreeGroup.IsTrivialCode = false;
            if (isTrivialLiteral)
            {
                uint red = hTreeGroup.HTrees[HuffIndex.Red][0].Value;
                uint blue = hTreeGroup.HTrees[HuffIndex.Blue][0].Value;
                uint green = hTreeGroup.HTrees[HuffIndex.Green][0].Value;
                uint alpha = hTreeGroup.HTrees[HuffIndex.Alpha][0].Value;
                hTreeGroup.LiteralArb = (alpha << 24) | (red << 16) | blue;
                if (totalSize == 0 && green < WebpConstants.NumLiteralCodes)
                {
                    hTreeGroup.IsTrivialCode = true;
                    hTreeGroup.LiteralArb |= green << 8;
                }
            }

            hTreeGroup.UsePackedTable = !hTreeGroup.IsTrivialCode && maxBits < HuffmanUtils.HuffmanPackedBits;
            if (hTreeGroup.UsePackedTable)
            {
                BuildPackedTable(hTreeGroup);
            }
        }

        decoder.Metadata.NumHTreeGroups = numHTreeGroups;
        decoder.Metadata.HTreeGroups = hTreeGroups;
        decoder.Metadata.HuffmanTables = huffmanTables;
    }

    private int ReadHuffmanCode(int alphabetSize, int[] codeLengths, Span<HuffmanCode> table)
    {
        bool simpleCode = this.bitReader.ReadBit();
        codeLengths.AsSpan(0, alphabetSize).Clear();

        if (simpleCode)
        {
            // (i) Simple Code Length Code.
            // This variant is used in the special case when only 1 or 2 Huffman code lengths are non-zero,
            // and are in the range of[0, 255]. All other Huffman code lengths are implicitly zeros.

            // Read symbols, codes & code lengths directly.
            uint numSymbols = this.bitReader.ReadValue(1) + 1;
            uint firstSymbolLenCode = this.bitReader.ReadValue(1);

            // The first code is either 1 bit or 8 bit code.
            uint symbol = this.bitReader.ReadValue(firstSymbolLenCode == 0 ? 1 : 8);
            codeLengths[symbol] = 1;

            // The second code (if present), is always 8 bit long.
            if (numSymbols == 2)
            {
                symbol = this.bitReader.ReadValue(8);
                codeLengths[symbol] = 1;
            }
        }
        else
        {
            // (ii) Normal Code Length Code:
            // The code lengths of a Huffman code are read as follows: num_code_lengths specifies the number of code lengths;
            // the rest of the code lengths (according to the order in kCodeLengthCodeOrder) are zeros.
            int[] codeLengthCodeLengths = new int[NumCodeLengthCodes];
            uint numCodes = this.bitReader.ReadValue(4) + 4;
            if (numCodes > NumCodeLengthCodes)
            {
                WebpThrowHelper.ThrowImageFormatException("Bitstream error, numCodes has an invalid value");
            }

            for (int i = 0; i < numCodes; i++)
            {
                codeLengthCodeLengths[CodeLengthCodeOrder[i]] = (int)this.bitReader.ReadValue(3);
            }

            this.ReadHuffmanCodeLengths(table, codeLengthCodeLengths, alphabetSize, codeLengths);
        }

        return HuffmanUtils.BuildHuffmanTable(table, HuffmanUtils.HuffmanTableBits, codeLengths, alphabetSize);
    }

    private void ReadHuffmanCodeLengths(Span<HuffmanCode> table, int[] codeLengthCodeLengths, int numSymbols, int[] codeLengths)
    {
        int maxSymbol;
        int symbol = 0;
        int prevCodeLen = WebpConstants.DefaultCodeLength;
        int size = HuffmanUtils.BuildHuffmanTable(table, WebpConstants.LengthTableBits, codeLengthCodeLengths, NumCodeLengthCodes);
        if (size == 0)
        {
            WebpThrowHelper.ThrowImageFormatException("Error building huffman table");
        }

        if (this.bitReader.ReadBit())
        {
            int lengthNBits = 2 + (2 * (int)this.bitReader.ReadValue(3));
            maxSymbol = 2 + (int)this.bitReader.ReadValue(lengthNBits);
        }
        else
        {
            maxSymbol = numSymbols;
        }

        while (symbol < numSymbols)
        {
            if (maxSymbol-- == 0)
            {
                break;
            }

            this.bitReader.FillBitWindow();
            ulong prefetchBits = this.bitReader.PrefetchBits();
            int idx = (int)(prefetchBits & 127);
            HuffmanCode huffmanCode = table[idx];
            this.bitReader.AdvanceBitPosition(huffmanCode.BitsUsed);
            uint codeLen = huffmanCode.Value;
            if (codeLen < WebpConstants.CodeLengthLiterals)
            {
                codeLengths[symbol++] = (int)codeLen;
                if (codeLen != 0)
                {
                    prevCodeLen = (int)codeLen;
                }
            }
            else
            {
                bool usePrev = codeLen == WebpConstants.CodeLengthRepeatCode;
                uint slot = codeLen - WebpConstants.CodeLengthLiterals;
                int extraBits = WebpConstants.CodeLengthExtraBits[slot];
                int repeatOffset = WebpConstants.CodeLengthRepeatOffsets[slot];
                int repeat = (int)(this.bitReader.ReadValue(extraBits) + repeatOffset);
                if (symbol + repeat > numSymbols)
                {
                    return;
                }

                int length = usePrev ? prevCodeLen : 0;
                while (repeat-- > 0)
                {
                    codeLengths[symbol++] = length;
                }
            }
        }
    }

    /// <summary>
    /// Reads the transformations, if any are present.
    /// </summary>
    /// <param name="xSize">The width of the image.</param>
    /// <param name="ySize">The height of the image.</param>
    /// <param name="decoder">Vp8LDecoder where the transformations will be stored.</param>
    private void ReadTransformation(int xSize, int ySize, Vp8LDecoder decoder)
    {
        Vp8LTransformType transformType = (Vp8LTransformType)this.bitReader.ReadValue(2);
        Vp8LTransform transform = new(transformType, xSize, ySize);

        // Each transform is allowed to be used only once.
        if (decoder.Transforms.Any(decoderTransform => decoderTransform.TransformType == transform.TransformType))
        {
            WebpThrowHelper.ThrowImageFormatException("Each transform can only be present once");
        }

        switch (transformType)
        {
            case Vp8LTransformType.SubtractGreen:
                // There is no data associated with this transform.
                break;
            case Vp8LTransformType.ColorIndexingTransform:
                // The transform data contains color table size and the entries in the color table.
                // 8 bit value for color table size.
                uint numColors = this.bitReader.ReadValue(8) + 1;
                if (numColors > 16)
                {
                    transform.Bits = 0;
                }
                else if (numColors > 4)
                {
                    transform.Bits = 1;
                }
                else if (numColors > 2)
                {
                    transform.Bits = 2;
                }
                else
                {
                    transform.Bits = 3;
                }

                using (IMemoryOwner<uint> colorMap = this.DecodeImageStream(decoder, (int)numColors, 1, false))
                {
                    int finalNumColors = 1 << (8 >> transform.Bits);
                    IMemoryOwner<uint> newColorMap = this.memoryAllocator.Allocate<uint>(finalNumColors, AllocationOptions.Clean);
                    LosslessUtils.ExpandColorMap((int)numColors, colorMap.GetSpan(), newColorMap.GetSpan());
                    transform.Data = newColorMap;
                }

                break;

            case Vp8LTransformType.PredictorTransform:
            case Vp8LTransformType.CrossColorTransform:

                // The first 3 bits of prediction data define the block width and height in number of bits.
                transform.Bits = (int)this.bitReader.ReadValue(3) + 2;
                int blockWidth = LosslessUtils.SubSampleSize(transform.XSize, transform.Bits);
                int blockHeight = LosslessUtils.SubSampleSize(transform.YSize, transform.Bits);
                transform.Data = this.DecodeImageStream(decoder, blockWidth, blockHeight, false);
                break;
        }

        decoder.Transforms.Add(transform);
    }

    /// <summary>
    /// A Webp lossless image can go through four different types of transformation before being entropy encoded.
    /// This will reverse the transformations, if any are present.
    /// </summary>
    /// <param name="decoder">The decoder holding the transformation infos.</param>
    /// <param name="pixelData">The pixel data to apply the transformation.</param>
    /// <param name="memoryAllocator">The memory allocator is needed to allocate memory during the predictor transform.</param>
    public static void ApplyInverseTransforms(Vp8LDecoder decoder, Span<uint> pixelData, MemoryAllocator memoryAllocator)
    {
        List<Vp8LTransform> transforms = decoder.Transforms;
        for (int i = transforms.Count - 1; i >= 0; i--)
        {
            // TODO: Review these 1D allocations. They could conceivably exceed limits.
            Vp8LTransform transform = transforms[i];
            switch (transform.TransformType)
            {
                case Vp8LTransformType.PredictorTransform:
                    using (IMemoryOwner<uint> output = memoryAllocator.Allocate<uint>(pixelData.Length, AllocationOptions.Clean))
                    {
                        LosslessUtils.PredictorInverseTransform(transform, pixelData, output.GetSpan());
                    }

                    break;
                case Vp8LTransformType.SubtractGreen:
                    LosslessUtils.AddGreenToBlueAndRed(pixelData);
                    break;
                case Vp8LTransformType.CrossColorTransform:
                    LosslessUtils.ColorSpaceInverseTransform(transform, pixelData);
                    break;
                case Vp8LTransformType.ColorIndexingTransform:
                    using (IMemoryOwner<uint> output = memoryAllocator.Allocate<uint>(transform.XSize * transform.YSize, AllocationOptions.Clean))
                    {
                        LosslessUtils.ColorIndexInverseTransform(transform, pixelData, output.GetSpan());
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// The alpha channel of a lossy webp image can be compressed using the lossless webp compression.
    /// This method will undo the compression.
    /// </summary>
    /// <param name="dec">The alpha decoder.</param>
    public void DecodeAlphaData(AlphaDecoder dec)
    {
        Span<uint> pixelData = dec.Vp8LDec.Pixels.Memory.Span;
        Span<byte> data = MemoryMarshal.Cast<uint, byte>(pixelData);
        int row = 0;
        int col = 0;
        Vp8LDecoder vp8LDec = dec.Vp8LDec;
        int width = vp8LDec.Width;
        int height = vp8LDec.Height;
        Vp8LMetadata hdr = vp8LDec.Metadata;
        int pos = 0; // Current position.
        int end = width * height; // End of data.
        int last = end; // Last pixel to decode.
        int lastRow = height;
        const int lenCodeLimit = WebpConstants.NumLiteralCodes + WebpConstants.NumLengthCodes;
        int mask = hdr.HuffmanMask;
        Span<HTreeGroup> htreeGroup = pos < last ? GetHTreeGroupForPos(hdr, col, row) : null;
        while (!this.bitReader.Eos && pos < last)
        {
            // Only update when changing tile.
            if ((col & mask) == 0)
            {
                htreeGroup = GetHTreeGroupForPos(hdr, col, row);
            }

            this.bitReader.FillBitWindow();
            int code = (int)this.ReadSymbol(htreeGroup[0].HTrees[HuffIndex.Green]);
            switch (code)
            {
                case < WebpConstants.NumLiteralCodes:
                {
                    // Literal
                    data[pos] = (byte)code;
                    ++pos;
                    ++col;

                    if (col >= width)
                    {
                        col = 0;
                        ++row;
                        if (row <= lastRow && row % WebpConstants.NumArgbCacheRows == 0)
                        {
                            dec.ExtractPalettedAlphaRows(row);
                        }
                    }

                    break;
                }

                case < lenCodeLimit:
                {
                    // Backward reference
                    int lengthSym = code - WebpConstants.NumLiteralCodes;
                    int length = this.GetCopyLength(lengthSym);
                    int distSymbol = (int)this.ReadSymbol(htreeGroup[0].HTrees[HuffIndex.Dist]);
                    this.bitReader.FillBitWindow();
                    int distCode = this.GetCopyDistance(distSymbol);
                    int dist = PlaneCodeToDistance(width, distCode);
                    if (pos >= dist && end - pos >= length)
                    {
                        CopyBlock8B(data, pos, dist, length);
                    }
                    else
                    {
                        WebpThrowHelper.ThrowImageFormatException("error while decoding alpha data");
                    }

                    pos += length;
                    col += length;
                    while (col >= width)
                    {
                        col -= width;
                        ++row;
                        if (row <= lastRow && row % WebpConstants.NumArgbCacheRows == 0)
                        {
                            dec.ExtractPalettedAlphaRows(row);
                        }
                    }

                    if (pos < last && (col & mask) > 0)
                    {
                        htreeGroup = GetHTreeGroupForPos(hdr, col, row);
                    }

                    break;
                }

                default:
                    WebpThrowHelper.ThrowImageFormatException("bitstream error while parsing alpha data");
                    break;
            }

            this.bitReader.Eos = this.bitReader.IsEndOfStream();
        }

        // Process the remaining rows corresponding to last row-block.
        dec.ExtractPalettedAlphaRows(row > lastRow ? lastRow : row);
    }

    private static void UpdateDecoder(Vp8LDecoder decoder, int width, int height)
    {
        int numBits = decoder.Metadata.HuffmanSubSampleBits;
        decoder.Width = width;
        decoder.Height = height;
        decoder.Metadata.HuffmanXSize = LosslessUtils.SubSampleSize(width, numBits);
        decoder.Metadata.HuffmanMask = numBits == 0 ? ~0 : (1 << numBits) - 1;
    }

    private uint ReadPackedSymbols(Span<HTreeGroup> group, Span<uint> pixelData, int decodedPixels)
    {
        uint val = (uint)(this.bitReader.PrefetchBits() & (HuffmanUtils.HuffmanPackedTableSize - 1));
        HuffmanCode code = group[0].PackedTable[val];
        if (code.BitsUsed < BitsSpecialMarker)
        {
            this.bitReader.AdvanceBitPosition(code.BitsUsed);
            pixelData[decodedPixels] = code.Value;
            return PackedNonLiteralCode;
        }

        this.bitReader.AdvanceBitPosition(code.BitsUsed - BitsSpecialMarker);

        return code.Value;
    }

    private static void BuildPackedTable(HTreeGroup hTreeGroup)
    {
        for (uint code = 0; code < HuffmanUtils.HuffmanPackedTableSize; code++)
        {
            uint bits = code;
            ref HuffmanCode huff = ref hTreeGroup.PackedTable[bits];
            HuffmanCode hCode = hTreeGroup.HTrees[HuffIndex.Green][bits];
            if (hCode.Value >= WebpConstants.NumLiteralCodes)
            {
                huff.BitsUsed = hCode.BitsUsed + BitsSpecialMarker;
                huff.Value = hCode.Value;
            }
            else
            {
                huff.BitsUsed = 0;
                huff.Value = 0;
                bits >>= AccumulateHCode(hCode, 8, ref huff);
                bits >>= AccumulateHCode(hTreeGroup.HTrees[HuffIndex.Red][bits], 16, ref huff);
                bits >>= AccumulateHCode(hTreeGroup.HTrees[HuffIndex.Blue][bits], 0, ref huff);
                bits >>= AccumulateHCode(hTreeGroup.HTrees[HuffIndex.Alpha][bits], 24, ref huff);
            }
        }
    }

    /// <summary>
    /// Decodes the next Huffman code from the bit-stream.
    /// FillBitWindow() needs to be called at minimum every second call to ReadSymbol, in order to pre-fetch enough bits.
    /// </summary>
    /// <param name="table">The Huffman table.</param>
    private uint ReadSymbol(Span<HuffmanCode> table)
    {
        uint val = (uint)this.bitReader.PrefetchBits();
        Span<HuffmanCode> tableSpan = table[(int)(val & HuffmanUtils.HuffmanTableMask)..];
        int nBits = tableSpan[0].BitsUsed - HuffmanUtils.HuffmanTableBits;
        if (nBits > 0)
        {
            this.bitReader.AdvanceBitPosition(HuffmanUtils.HuffmanTableBits);
            val = (uint)this.bitReader.PrefetchBits();
            tableSpan = tableSpan[(int)tableSpan[0].Value..];
            tableSpan = tableSpan[((int)val & ((1 << nBits) - 1))..];
        }

        this.bitReader.AdvanceBitPosition(tableSpan[0].BitsUsed);

        return tableSpan[0].Value;
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private int GetCopyLength(int lengthSymbol) =>
        this.GetCopyDistance(lengthSymbol); // Length and distance prefixes are encoded the same way.

    private int GetCopyDistance(int distanceSymbol)
    {
        if (distanceSymbol < 4)
        {
            return distanceSymbol + 1;
        }

        int extraBits = (distanceSymbol - 2) >> 1;
        int offset = (2 + (distanceSymbol & 1)) << extraBits;

        return (int)(offset + this.bitReader.ReadValue(extraBits) + 1);
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private static Span<HTreeGroup> GetHTreeGroupForPos(Vp8LMetadata metadata, int x, int y)
    {
        uint metaIndex = GetMetaIndex(metadata.HuffmanImage, metadata.HuffmanXSize, metadata.HuffmanSubSampleBits, x, y);
        return metadata.HTreeGroups.AsSpan((int)metaIndex);
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private static uint GetMetaIndex(IMemoryOwner<uint> huffmanImage, int xSize, int bits, int x, int y)
    {
        if (bits is 0)
        {
            return 0;
        }

        Span<uint> huffmanImageSpan = huffmanImage.GetSpan();
        return huffmanImageSpan[(xSize * (y >> bits)) + (x >> bits)];
    }

    private static int PlaneCodeToDistance(int xSize, int planeCode)
    {
        if (planeCode > CodeToPlaneCodes)
        {
            return planeCode - CodeToPlaneCodes;
        }

        int distCode = WebpLookupTables.CodeToPlane[planeCode - 1];
        int yOffset = distCode >> 4;
        int xOffset = 8 - (distCode & 0xf);
        int dist = (yOffset * xSize) + xOffset;

        // dist < 1 can happen if xSize is very small.
        return dist >= 1 ? dist : 1;
    }

    /// <summary>
    /// Copies pixels when a backward reference is used.
    /// Copy 'length' number of pixels (in scan-line order) from the sequence of pixels prior to them by 'dist' pixels.
    /// </summary>
    /// <param name="pixelData">The pixel data.</param>
    /// <param name="decodedPixels">The number of so far decoded pixels.</param>
    /// <param name="dist">The backward reference distance prior to the current decoded pixel.</param>
    /// <param name="length">The number of pixels to copy.</param>
    private static void CopyBlock(Span<uint> pixelData, int decodedPixels, int dist, int length)
    {
        int start = decodedPixels - dist;
        if (start < 0)
        {
            WebpThrowHelper.ThrowImageFormatException("webp image data seems to be invalid");
        }

        if (dist >= length)
        {
            // no overlap.
            Span<uint> src = pixelData.Slice(start, length);
            Span<uint> dest = pixelData[decodedPixels..];
            src.CopyTo(dest);
        }
        else
        {
            // There is overlap between the backward reference distance and the pixels to copy.
            Span<uint> src = pixelData[start..];
            Span<uint> dest = pixelData[decodedPixels..];
            for (int i = 0; i < length; i++)
            {
                dest[i] = src[i];
            }
        }
    }

    /// <summary>
    /// Copies alpha values when a backward reference is used.
    /// Copy 'length' number of alpha values from the sequence of alpha values prior to them by 'dist'.
    /// </summary>
    /// <param name="data">The alpha values.</param>
    /// <param name="pos">The position of the so far decoded pixels.</param>
    /// <param name="dist">The backward reference distance prior to the current decoded pixel.</param>
    /// <param name="length">The number of pixels to copy.</param>
    private static void CopyBlock8B(Span<byte> data, int pos, int dist, int length)
    {
        if (dist >= length)
        {
            // no overlap.
            data.Slice(pos - dist, length).CopyTo(data[pos..]);
        }
        else
        {
            Span<byte> dst = data[pos..];
            Span<byte> src = data[(pos - dist)..];
            for (int i = 0; i < length; i++)
            {
                dst[i] = src[i];
            }
        }
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private static int AccumulateHCode(HuffmanCode hCode, int shift, ref HuffmanCode huff)
    {
        huff.BitsUsed += hCode.BitsUsed;
        huff.Value |= hCode.Value << shift;
        return hCode.BitsUsed;
    }
}
