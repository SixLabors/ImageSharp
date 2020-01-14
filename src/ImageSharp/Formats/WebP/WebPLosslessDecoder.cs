// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Memory;

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// Decoder for lossless webp images. This code is a port of libwebp, which can be found here: https://chromium.googlesource.com/webm/libwebp
    /// </summary>
    /// <remarks>
    /// The lossless specification can be found here:
    /// https://developers.google.com/speed/webp/docs/webp_lossless_bitstream_specification
    /// </remarks>
    internal sealed class WebPLosslessDecoder
    {
        private readonly Vp8LBitReader bitReader;

        private static readonly int BitsSpecialMarker = 0x100;

        private static readonly uint PackedNonLiteralCode = 0;

        private static readonly int NumArgbCacheRows = 16;

        private static readonly int FixedTableSize = (630 * 3) + 410;

        private static readonly int[] KTableSize =
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

        private static readonly int NumCodeLengthCodes = 19;
        private static readonly byte[] KCodeLengthCodeOrder = { 17, 18, 0, 1, 2, 3, 4, 5, 16, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };

        private static readonly int CodeToPlaneCodes = 120;
        private static readonly int[] KCodeToPlane =
        {
            0x18, 0x07, 0x17, 0x19, 0x28, 0x06, 0x27, 0x29, 0x16, 0x1a,
            0x26, 0x2a, 0x38, 0x05, 0x37, 0x39, 0x15, 0x1b, 0x36, 0x3a,
            0x25, 0x2b, 0x48, 0x04, 0x47, 0x49, 0x14, 0x1c, 0x35, 0x3b,
            0x46, 0x4a, 0x24, 0x2c, 0x58, 0x45, 0x4b, 0x34, 0x3c, 0x03,
            0x57, 0x59, 0x13, 0x1d, 0x56, 0x5a, 0x23, 0x2d, 0x44, 0x4c,
            0x55, 0x5b, 0x33, 0x3d, 0x68, 0x02, 0x67, 0x69, 0x12, 0x1e,
            0x66, 0x6a, 0x22, 0x2e, 0x54, 0x5c, 0x43, 0x4d, 0x65, 0x6b,
            0x32, 0x3e, 0x78, 0x01, 0x77, 0x79, 0x53, 0x5d, 0x11, 0x1f,
            0x64, 0x6c, 0x42, 0x4e, 0x76, 0x7a, 0x21, 0x2f, 0x75, 0x7b,
            0x31, 0x3f, 0x63, 0x6d, 0x52, 0x5e, 0x00, 0x74, 0x7c, 0x41,
            0x4f, 0x10, 0x20, 0x62, 0x6e, 0x30, 0x73, 0x7d, 0x51, 0x5f,
            0x40, 0x72, 0x7e, 0x61, 0x6f, 0x50, 0x71, 0x7f, 0x60, 0x70
        };

        private static readonly byte[] KLiteralMap =
        {
            0, 1, 1, 1, 0
        };

        /// <summary>
        /// Used for allocating memory during processing operations.
        /// </summary>
        private readonly MemoryAllocator memoryAllocator;

        /// <summary>
        /// Initializes a new instance of the <see cref="WebPLosslessDecoder"/> class.
        /// </summary>
        /// <param name="bitReader">Bitreader to read from the stream.</param>
        /// <param name="memoryAllocator">Used for allocating memory during processing operations.</param>
        public WebPLosslessDecoder(Vp8LBitReader bitReader, MemoryAllocator memoryAllocator)
        {
            this.bitReader = bitReader;
            this.memoryAllocator = memoryAllocator;
        }

        /// <summary>
        /// Decodes the image from the stream using the bitreader.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="pixels">The pixel buffer to store the decoded data.</param>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        public void Decode<TPixel>(Buffer2D<TPixel> pixels, int width, int height)
            where TPixel : struct, IPixel<TPixel>
        {
            var decoder = new Vp8LDecoder(width, height);
            IMemoryOwner<uint> pixelData = this.DecodeImageStream(decoder, width, height, true);
            this.DecodePixelValues(decoder, pixelData.GetSpan(), pixels);

            // Free up allocated memory.
            pixelData.Dispose();
            foreach (Vp8LTransform transform in decoder.Transforms)
            {
                transform.Data?.Dispose();
            }

            decoder.Metadata?.HuffmanImage?.Dispose();
        }

        private IMemoryOwner<uint> DecodeImageStream(Vp8LDecoder decoder, int xSize, int ySize, bool isLevel0)
        {
            int numberOfTransformsPresent = 0;
            if (isLevel0)
            {
                decoder.Transforms = new List<Vp8LTransform>(WebPConstants.MaxNumberOfTransforms);

                // Next bit indicates, if a transformation is present.
                while (this.bitReader.ReadBit())
                {
                    if (numberOfTransformsPresent > WebPConstants.MaxNumberOfTransforms)
                    {
                        WebPThrowHelper.ThrowImageFormatException($"The maximum number of transforms of {WebPConstants.MaxNumberOfTransforms} was exceeded");
                    }

                    this.ReadTransformation(xSize, ySize, decoder);
                    numberOfTransformsPresent++;
                }
            }
            else
            {
                decoder.Metadata = new Vp8LMetadata();
            }

            // Color cache.
            bool colorCachePresent = this.bitReader.ReadBit();
            int colorCacheBits = 0;
            int colorCacheSize = 0;
            if (colorCachePresent)
            {
                colorCacheBits = (int)this.bitReader.ReadBits(4);
                bool coloCacheBitsIsValid = colorCacheBits >= 1 && colorCacheBits <= WebPConstants.MaxColorCacheBits;
                if (!coloCacheBitsIsValid)
                {
                    WebPThrowHelper.ThrowImageFormatException("Invalid color cache bits found");
                }
            }

            // Read the Huffman codes (may recurse).
            this.ReadHuffmanCodes(decoder, xSize, ySize, colorCacheBits, isLevel0);
            decoder.Metadata.ColorCacheSize = colorCacheSize;

            // Finish setting up the color-cache
            ColorCache colorCache = null;
            if (colorCachePresent)
            {
                colorCache = new ColorCache();
                colorCacheSize = 1 << colorCacheBits;
                decoder.Metadata.ColorCacheSize = colorCacheSize;
                if (!(colorCacheBits >= 1 && colorCacheBits <= WebPConstants.MaxColorCacheBits))
                {
                    WebPThrowHelper.ThrowImageFormatException("Invalid color cache bits found");
                }

                colorCache.Init(colorCacheBits);
            }
            else
            {
                decoder.Metadata.ColorCacheSize = 0;
            }

            this.UpdateDecoder(decoder, xSize, ySize);

            IMemoryOwner<uint> pixelData = this.memoryAllocator.Allocate<uint>(decoder.Width * decoder.Height, AllocationOptions.Clean);
            this.DecodeImageData(decoder, pixelData.GetSpan(), colorCacheSize, colorCache);

            return pixelData;
        }

        private void DecodePixelValues<TPixel>(Vp8LDecoder decoder, Span<uint> pixelData, Buffer2D<TPixel> pixels)
            where TPixel : struct, IPixel<TPixel>
        {
            // Apply reverse transformations, if any are present.
            this.ApplyInverseTransforms(decoder, pixelData);

            TPixel color = default;
            for (int y = 0; y < decoder.Height; y++)
            {
                Span<TPixel> pixelRow = pixels.GetRowSpan(y);
                for (int x = 0; x < decoder.Width; x++)
                {
                    int idx = (y * decoder.Width) + x;
                    uint pixel = pixelData[idx];
                    byte a = (byte)((pixel & 0xFF000000) >> 24);
                    byte r = (byte)((pixel & 0xFF0000) >> 16);
                    byte g = (byte)((pixel & 0xFF00) >> 8);
                    byte b = (byte)(pixel & 0xFF);
                    color.FromRgba32(new Rgba32(r, g, b, a));
                    pixelRow[x] = color;
                }
            }
        }

        private void DecodeImageData(Vp8LDecoder decoder, Span<uint> pixelData, int colorCacheSize, ColorCache colorCache)
        {
            int lastPixel = 0;
            int width = decoder.Width;
            int height = decoder.Height;
            int row = lastPixel / width;
            int col = lastPixel % width;
            int lenCodeLimit = WebPConstants.NumLiteralCodes + WebPConstants.NumLengthCodes;
            int colorCacheLimit = lenCodeLimit + colorCacheSize;
            int mask = decoder.Metadata.HuffmanMask;
            HTreeGroup[] hTreeGroup = this.GetHTreeGroupForPos(decoder.Metadata, col, row);

            int totalPixels = width * height;
            int decodedPixels = 0;
            int lastCached = decodedPixels;
            while (decodedPixels < totalPixels)
            {
                int code;
                if ((col & mask) == 0)
                {
                    hTreeGroup = this.GetHTreeGroupForPos(decoder.Metadata, col, row);
                }

                if (hTreeGroup[0].IsTrivialCode)
                {
                    pixelData[decodedPixels] = hTreeGroup[0].LiteralArb;
                    this.AdvanceByOne(ref col, ref row, width, colorCache, ref decodedPixels, pixelData, ref lastCached);
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
                        this.AdvanceByOne(ref col, ref row, width, colorCache, ref decodedPixels, pixelData, ref lastCached);
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
                if (code < WebPConstants.NumLiteralCodes)
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

                        int pixelIdx = decodedPixels;
                        pixelData[pixelIdx] = (uint)(((byte)alpha << 24) | ((byte)red << 16) | ((byte)code << 8) | (byte)blue);
                    }

                    this.AdvanceByOne(ref col, ref row, width, colorCache, ref decodedPixels, pixelData, ref lastCached);
                }
                else if (code < lenCodeLimit)
                {
                    // Backward reference is used.
                    int lengthSym = code - WebPConstants.NumLiteralCodes;
                    int length = this.GetCopyLength(lengthSym);
                    uint distSymbol = this.ReadSymbol(hTreeGroup[0].HTrees[HuffIndex.Dist]);
                    this.bitReader.FillBitWindow();
                    int distCode = this.GetCopyDistance((int)distSymbol);
                    int dist = this.PlaneCodeToDistance(width, distCode);
                    if (this.bitReader.IsEndOfStream())
                    {
                        break;
                    }

                    this.CopyBlock(pixelData, decodedPixels, dist, length);
                    decodedPixels += length;
                    col += length;
                    while (col >= width)
                    {
                        col -= width;
                        ++row;
                    }

                    if ((col & mask) != 0)
                    {
                        hTreeGroup = this.GetHTreeGroupForPos(decoder.Metadata, col, row);
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
                    this.AdvanceByOne(ref col, ref row, width, colorCache, ref decodedPixels, pixelData, ref lastCached);
                }
                else
                {
                    WebPThrowHelper.ThrowImageFormatException("Webp parsing error");
                }
            }
        }

        private void AdvanceByOne(ref int col, ref int row, int width, ColorCache colorCache, ref int decodedPixels, Span<uint> pixelData, ref int lastCached)
        {
            ++col;
            decodedPixels++;
            if (col >= width)
            {
                col = 0;
                ++row;

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
                uint huffmanPrecision = this.bitReader.ReadBits(3) + 2;
                int huffmanXSize = LosslessUtils.SubSampleSize(xSize, (int)huffmanPrecision);
                int huffmanYSize = LosslessUtils.SubSampleSize(ySize, (int)huffmanPrecision);
                int huffmanPixels = huffmanXSize * huffmanYSize;
                IMemoryOwner<uint> huffmanImage = this.DecodeImageStream(decoder, huffmanXSize, huffmanYSize, false);
                Span<uint> huffmanImageSpan = huffmanImage.GetSpan();
                decoder.Metadata.HuffmanSubSampleBits = (int)huffmanPrecision;
                for (int i = 0; i < huffmanPixels; ++i)
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
            for (int j = 0; j < WebPConstants.HuffmanCodesPerMetaCode; j++)
            {
                int alphabetSize = WebPConstants.KAlphabetSize[j];
                if (j == 0 && colorCacheBits > 0)
                {
                    alphabetSize += 1 << colorCacheBits;
                }

                if (maxAlphabetSize < alphabetSize)
                {
                    maxAlphabetSize = alphabetSize;
                }
            }

            int tableSize = KTableSize[colorCacheBits];
            var huffmanTables = new HuffmanCode[numHTreeGroups * tableSize];
            var hTreeGroups = new HTreeGroup[numHTreeGroups];
            Span<HuffmanCode> huffmanTable = huffmanTables.AsSpan();
            for (int i = 0; i < numHTreeGroupsMax; i++)
            {
                hTreeGroups[i] = new HTreeGroup(HuffmanUtils.HuffmanPackedTableSize);
                HTreeGroup hTreeGroup = hTreeGroups[i];
                int totalSize = 0;
                bool isTrivialLiteral = true;
                int maxBits = 0;
                var codeLengths = new int[maxAlphabetSize];
                for (int j = 0; j < WebPConstants.HuffmanCodesPerMetaCode; j++)
                {
                    int alphabetSize = WebPConstants.KAlphabetSize[j];
                    if (j == 0 && colorCacheBits > 0)
                    {
                        alphabetSize += 1 << colorCacheBits;
                    }

                    int size = this.ReadHuffmanCode(alphabetSize, codeLengths, huffmanTable);
                    if (size is 0)
                    {
                        WebPThrowHelper.ThrowImageFormatException("Huffman table size is zero");
                    }

                    hTreeGroup.HTrees.Add(huffmanTable.ToArray());

                    if (isTrivialLiteral && KLiteralMap[j] == 1)
                    {
                        isTrivialLiteral = huffmanTable[0].BitsUsed == 0;
                    }

                    totalSize += huffmanTable[0].BitsUsed;
                    huffmanTable = huffmanTable.Slice(size);

                    if (j <= HuffIndex.Alpha)
                    {
                        int localMaxBits = codeLengths[0];
                        int k;
                        for (k = 1; k < alphabetSize; ++k)
                        {
                            if (codeLengths[k] > localMaxBits)
                            {
                                localMaxBits = codeLengths[k];
                            }
                        }

                        maxBits += localMaxBits;
                    }
                }

                hTreeGroup.IsTrivialLiteral = isTrivialLiteral;
                hTreeGroup.IsTrivialCode = false;
                if (isTrivialLiteral)
                {
                    uint red = hTreeGroup.HTrees[HuffIndex.Red].First().Value;
                    uint blue = hTreeGroup.HTrees[HuffIndex.Blue].First().Value;
                    uint green = hTreeGroup.HTrees[HuffIndex.Green].First().Value;
                    uint alpha = hTreeGroup.HTrees[HuffIndex.Alpha].First().Value;
                    hTreeGroup.LiteralArb = (alpha << 24) | (red << 16) | blue;
                    if (totalSize == 0 && green < WebPConstants.NumLiteralCodes)
                    {
                        hTreeGroup.IsTrivialCode = true;
                        hTreeGroup.LiteralArb |= green << 8;
                    }
                }

                hTreeGroup.UsePackedTable = !hTreeGroup.IsTrivialCode && maxBits < HuffmanUtils.HuffmanPackedBits;
                if (hTreeGroup.UsePackedTable)
                {
                    this.BuildPackedTable(hTreeGroup);
                }
            }

            decoder.Metadata.NumHTreeGroups = numHTreeGroups;
            decoder.Metadata.HTreeGroups = hTreeGroups;
            decoder.Metadata.HuffmanTables = huffmanTables;
        }

        private int ReadHuffmanCode(int alphabetSize, int[] codeLengths, Span<HuffmanCode> table)
        {
            bool simpleCode = this.bitReader.ReadBit();
            for (int i = 0; i < alphabetSize; i++)
            {
                codeLengths[i] = 0;
            }

            if (simpleCode)
            {
                // (i) Simple Code Length Code.
                // This variant is used in the special case when only 1 or 2 Huffman code lengths are non - zero,
                // and are in the range of[0, 255].All other Huffman code lengths are implicitly zeros.

                // Read symbols, codes & code lengths directly.
                uint numSymbols = this.bitReader.ReadBits(1) + 1;
                uint firstSymbolLenCode = this.bitReader.ReadBits(1);

                // The first code is either 1 bit or 8 bit code.
                uint symbol = this.bitReader.ReadBits((firstSymbolLenCode is 0) ? 1 : 8);
                codeLengths[symbol] = 1;

                // The second code (if present), is always 8 bit long.
                if (numSymbols is 2)
                {
                    symbol = this.bitReader.ReadBits(8);
                    codeLengths[symbol] = 1;
                }
            }
            else
            {
                // (ii) Normal Code Length Code:
                // The code lengths of a Huffman code are read as follows: num_code_lengths specifies the number of code lengths;
                // the rest of the code lengths (according to the order in kCodeLengthCodeOrder) are zeros.
                var codeLengthCodeLengths = new int[NumCodeLengthCodes];
                uint numCodes = this.bitReader.ReadBits(4) + 4;
                if (numCodes > NumCodeLengthCodes)
                {
                    WebPThrowHelper.ThrowImageFormatException("Bitstream error, numCodes has an invalid value");
                }

                for (int i = 0; i < numCodes; i++)
                {
                    codeLengthCodeLengths[KCodeLengthCodeOrder[i]] = (int)this.bitReader.ReadBits(3);
                }

                this.ReadHuffmanCodeLengths(table.ToArray(), codeLengthCodeLengths, alphabetSize, codeLengths);
            }

            int size = HuffmanUtils.BuildHuffmanTable(table, HuffmanUtils.HuffmanTableBits, codeLengths, alphabetSize);

            return size;
        }

        private void ReadHuffmanCodeLengths(HuffmanCode[] table, int[] codeLengthCodeLengths, int numSymbols, int[] codeLengths)
        {
            int maxSymbol;
            int symbol = 0;
            int prevCodeLen = WebPConstants.DefaultCodeLength;
            int size = HuffmanUtils.BuildHuffmanTable(table, WebPConstants.LengthTableBits, codeLengthCodeLengths, NumCodeLengthCodes);
            if (size is 0)
            {
                WebPThrowHelper.ThrowImageFormatException("Error building huffman table");
            }

            if (this.bitReader.ReadBit())
            {
                int lengthNBits = 2 + (2 * (int)this.bitReader.ReadBits(3));
                maxSymbol = 2 + (int)this.bitReader.ReadBits(lengthNBits);
            }
            else
            {
                maxSymbol = numSymbols;
            }

            while (symbol < numSymbols)
            {
                if (maxSymbol-- is 0)
                {
                    break;
                }

                this.bitReader.FillBitWindow();
                ulong prefetchBits = this.bitReader.PrefetchBits();
                ulong idx = prefetchBits & 127;
                HuffmanCode huffmanCode = table[idx];
                this.bitReader.AdvanceBitPosition(huffmanCode.BitsUsed);
                uint codeLen = huffmanCode.Value;
                if (codeLen < WebPConstants.KCodeLengthLiterals)
                {
                    codeLengths[symbol++] = (int)codeLen;
                    if (codeLen != 0)
                    {
                        prevCodeLen = (int)codeLen;
                    }
                }
                else
                {
                    bool usePrev = codeLen == WebPConstants.KCodeLengthRepeatCode;
                    uint slot = codeLen - WebPConstants.KCodeLengthLiterals;
                    int extraBits = WebPConstants.KCodeLengthExtraBits[slot];
                    int repeatOffset = WebPConstants.KCodeLengthRepeatOffsets[slot];
                    int repeat = (int)(this.bitReader.ReadBits(extraBits) + repeatOffset);
                    if (symbol + repeat > numSymbols)
                    {
                        // TODO: not sure, if this should be treated as an error here
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
            var transformType = (Vp8LTransformType)this.bitReader.ReadBits(2);
            var transform = new Vp8LTransform(transformType, xSize, ySize);

            // Each transform is allowed to be used only once.
            if (decoder.Transforms.Any(t => t.TransformType == transform.TransformType))
            {
                WebPThrowHelper.ThrowImageFormatException("Each transform can only be present once");
            }

            switch (transformType)
            {
                case Vp8LTransformType.SubtractGreen:
                    // There is no data associated with this transform.
                    break;
                case Vp8LTransformType.ColorIndexingTransform:
                    // The transform data contains color table size and the entries in the color table.
                    // 8 bit value for color table size.
                    uint numColors = this.bitReader.ReadBits(8) + 1;
                    int bits = (numColors > 16) ? 0
                                     : (numColors > 4) ? 1
                                     : (numColors > 2) ? 2
                                     : 3;
                    transform.XSize = LosslessUtils.SubSampleSize(transform.XSize, bits);
                    transform.Bits = bits;
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
                    {
                        // The first 3 bits of prediction data define the block width and height in number of bits.
                        transform.Bits = (int)this.bitReader.ReadBits(3) + 2;
                        int blockWidth = LosslessUtils.SubSampleSize(transform.XSize, transform.Bits);
                        int blockHeight = LosslessUtils.SubSampleSize(transform.YSize, transform.Bits);
                        IMemoryOwner<uint> transformData = this.DecodeImageStream(decoder, blockWidth, blockHeight, false);
                        transform.Data = transformData;
                        break;
                    }
            }

            decoder.Transforms.Add(transform);
        }

        /// <summary>
        /// A WebP lossless image can go through four different types of transformation before being entropy encoded.
        /// This will reverses the transformations, if any are present.
        /// </summary>
        /// <param name="decoder">The decoder holding the transformation infos.</param>
        /// <param name="pixelData">The pixel data to apply the transformation.</param>
        private void ApplyInverseTransforms(Vp8LDecoder decoder, Span<uint> pixelData)
        {
            List<Vp8LTransform> transforms = decoder.Transforms;
            for (int i = transforms.Count - 1; i >= 0; i--)
            {
                Vp8LTransformType transformType = transforms[i].TransformType;
                switch (transformType)
                {
                    case Vp8LTransformType.PredictorTransform:
                        using (IMemoryOwner<uint> output = this.memoryAllocator.Allocate<uint>(pixelData.Length, AllocationOptions.Clean))
                        {
                            LosslessUtils.PredictorInverseTransform(transforms[i], pixelData, output.GetSpan());
                        }

                        break;
                    case Vp8LTransformType.SubtractGreen:
                        LosslessUtils.AddGreenToBlueAndRed(pixelData);
                        break;
                    case Vp8LTransformType.CrossColorTransform:
                        LosslessUtils.ColorSpaceInverseTransform(transforms[i], pixelData);
                        break;
                    case Vp8LTransformType.ColorIndexingTransform:
                        LosslessUtils.ColorIndexInverseTransform(transforms[i], pixelData);
                        break;
                }
            }
        }

        private void UpdateDecoder(Vp8LDecoder decoder, int width, int height)
        {
            int numBits = decoder.Metadata.HuffmanSubSampleBits;
            decoder.Width = width;
            decoder.Height = height;
            decoder.Metadata.HuffmanXSize = LosslessUtils.SubSampleSize(width, numBits);
            decoder.Metadata.HuffmanMask = (numBits is 0) ? ~0 : (1 << numBits) - 1;
        }

        /// <summary>
        /// Decodes the next Huffman code from bit-stream.
        /// FillBitWindow(br) needs to be called at minimum every second call
        /// to ReadSymbol, in order to pre-fetch enough bits.
        /// </summary>
        private uint ReadSymbol(Span<HuffmanCode> table)
        {
            uint val = (uint)this.bitReader.PrefetchBits();
            Span<HuffmanCode> tableSpan = table.Slice((int)(val & HuffmanUtils.HuffmanTableMask));
            int nBits = tableSpan[0].BitsUsed - HuffmanUtils.HuffmanTableBits;
            if (nBits > 0)
            {
                this.bitReader.AdvanceBitPosition(HuffmanUtils.HuffmanTableBits);
                val = (uint)this.bitReader.PrefetchBits();
                tableSpan = tableSpan.Slice((int)tableSpan[0].Value);
                tableSpan = tableSpan.Slice((int)val & ((1 << nBits) - 1));
            }

            this.bitReader.AdvanceBitPosition(tableSpan[0].BitsUsed);

            return tableSpan[0].Value;
        }

        private uint ReadPackedSymbols(HTreeGroup[] group, Span<uint> pixelData, int decodedPixels)
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

        private void CopyBlock(Span<uint> pixelData, int decodedPixels, int dist, int length)
        {
            if (dist >= length)
            {
                Span<uint> src = pixelData.Slice(decodedPixels - dist, length);
                Span<uint> dest = pixelData.Slice(decodedPixels);
                src.CopyTo(dest);
            }
            else
            {
                Span<uint> src = pixelData.Slice(decodedPixels - dist);
                Span<uint> dest = pixelData.Slice(decodedPixels);
                for (int i = 0; i < length; ++i)
                {
                    dest[i] = src[i];
                }
            }
        }

        private int GetCopyDistance(int distanceSymbol)
        {
            if (distanceSymbol < 4)
            {
                return distanceSymbol + 1;
            }

            int extraBits = (distanceSymbol - 2) >> 1;
            int offset = (2 + (distanceSymbol & 1)) << extraBits;

            return (int)(offset + this.bitReader.ReadBits(extraBits) + 1);
        }

        private int GetCopyLength(int lengthSymbol)
        {
            // Length and distance prefixes are encoded the same way.
            return this.GetCopyDistance(lengthSymbol);
        }

        private int PlaneCodeToDistance(int xSize, int planeCode)
        {
            if (planeCode > CodeToPlaneCodes)
            {
                return planeCode - CodeToPlaneCodes;
            }

            int distCode = KCodeToPlane[planeCode - 1];
            int yOffset = distCode >> 4;
            int xOffset = 8 - (distCode & 0xf);
            int dist = (yOffset * xSize) + xOffset;

            // dist < 1 can happen if xSize is very small.
            return (dist >= 1) ? dist : 1;
        }

        private void BuildPackedTable(HTreeGroup hTreeGroup)
        {
            for (uint code = 0; code < HuffmanUtils.HuffmanPackedTableSize; ++code)
            {
                uint bits = code;
                HuffmanCode huff = hTreeGroup.PackedTable[bits];
                HuffmanCode hCode = hTreeGroup.HTrees[HuffIndex.Green][bits];
                if (hCode.Value >= WebPConstants.NumLiteralCodes)
                {
                    huff.BitsUsed = hCode.BitsUsed + BitsSpecialMarker;
                    huff.Value = hCode.Value;
                }
                else
                {
                    huff.BitsUsed = 0;
                    huff.Value = 0;
                    bits >>= this.AccumulateHCode(hCode, 8, huff);
                    bits >>= this.AccumulateHCode(hTreeGroup.HTrees[HuffIndex.Red][bits], 16, huff);
                    bits >>= this.AccumulateHCode(hTreeGroup.HTrees[HuffIndex.Blue][bits], 0, huff);
                    bits >>= this.AccumulateHCode(hTreeGroup.HTrees[HuffIndex.Alpha][bits], 24, huff);
                }
            }
        }

        private int AccumulateHCode(HuffmanCode hCode, int shift, HuffmanCode huff)
        {
            huff.BitsUsed += hCode.BitsUsed;
            huff.Value |= hCode.Value << shift;
            return hCode.BitsUsed;
        }

        private uint GetMetaIndex(IMemoryOwner<uint> huffmanImage, int xSize, int bits, int x, int y)
        {
            if (bits is 0)
            {
                return 0;
            }

            Span<uint> huffmanImageSpan = huffmanImage.GetSpan();
            return huffmanImageSpan[(xSize * (y >> bits)) + (x >> bits)];
        }

        private HTreeGroup[] GetHTreeGroupForPos(Vp8LMetadata metadata, int x, int y)
        {
            uint metaIndex = this.GetMetaIndex(metadata.HuffmanImage, metadata.HuffmanXSize, metadata.HuffmanSubSampleBits, x, y);
            return metadata.HTreeGroups.AsSpan((int)metaIndex).ToArray();
        }
    }
}
