// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

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

        private static readonly int BitsSpecialMarker = 0x100;

        private static readonly int NumArgbCacheRows = 16;

        private static readonly uint PackedNonLiteralCode = 0;

        private static readonly int CodeToPlaneCodes = WebPLookupTables.CodeToPlane.Length;

        private static readonly int FixedTableSize = (630 * 3) + 410;

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

        private static readonly byte[] CodeLengthCodeOrder = { 17, 18, 0, 1, 2, 3, 4, 5, 16, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };

        private static readonly int NumCodeLengthCodes = CodeLengthCodeOrder.Length;

        private static readonly byte[] LiteralMap =
        {
            0, 1, 1, 1, 0
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="WebPLosslessDecoder"/> class.
        /// </summary>
        /// <param name="bitReader">Bitreader to read from the stream.</param>
        /// <param name="memoryAllocator">Used for allocating memory during processing operations.</param>
        /// <param name="configuration">The configuration.</param>
        public WebPLosslessDecoder(Vp8LBitReader bitReader, MemoryAllocator memoryAllocator, Configuration configuration)
        {
            this.bitReader = bitReader;
            this.memoryAllocator = memoryAllocator;
            this.configuration = configuration;
        }

        /// <summary>
        /// Decodes the image from the stream using the bitreader.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="pixels">The pixel buffer to store the decoded data.</param>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        public void Decode<TPixel>(Buffer2D<TPixel> pixels, int width, int height)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (var decoder = new Vp8LDecoder(width, height, this.memoryAllocator))
            {
                this.DecodeImageStream(decoder, width, height, true);
                this.DecodeImageData(decoder, decoder.Pixels.Memory.Span);
                this.DecodePixelValues(decoder, pixels);
            }
        }

        public IMemoryOwner<uint> DecodeImageStream(Vp8LDecoder decoder, int xSize, int ySize, bool isLevel0)
        {
            int transformXSize = xSize;
            int transformYSize = ySize;
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
                decoder.Metadata = new Vp8LMetadata();
            }

            // Color cache.
            bool colorCachePresent = this.bitReader.ReadBit();
            int colorCacheBits = 0;
            int colorCacheSize = 0;
            if (colorCachePresent)
            {
                colorCacheBits = (int)this.bitReader.ReadValue(4);
                bool coloCacheBitsIsValid = colorCacheBits >= 1 && colorCacheBits <= WebPConstants.MaxColorCacheBits;
                if (!coloCacheBitsIsValid)
                {
                    WebPThrowHelper.ThrowImageFormatException("Invalid color cache bits found");
                }
            }

            // Read the Huffman codes (may recurse).
            this.ReadHuffmanCodes(decoder, transformXSize, transformYSize, colorCacheBits, isLevel0);
            decoder.Metadata.ColorCacheSize = colorCacheSize;

            // Finish setting up the color-cache.
            if (colorCachePresent)
            {
                decoder.Metadata.ColorCache = new ColorCache();
                colorCacheSize = 1 << colorCacheBits;
                decoder.Metadata.ColorCacheSize = colorCacheSize;
                if (!(colorCacheBits >= 1 && colorCacheBits <= WebPConstants.MaxColorCacheBits))
                {
                    WebPThrowHelper.ThrowImageFormatException("Invalid color cache bits found");
                }

                decoder.Metadata.ColorCache.Init(colorCacheBits);
            }
            else
            {
                decoder.Metadata.ColorCacheSize = 0;
            }

            this.UpdateDecoder(decoder, transformXSize, transformYSize);
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

        private void DecodePixelValues<TPixel>(Vp8LDecoder decoder, Buffer2D<TPixel> pixels)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Span<uint> pixelData = decoder.Pixels.GetSpan();
            int width = decoder.Width;

            // Apply reverse transformations, if any are present.
            this.ApplyInverseTransforms(decoder, pixelData);

            Span<byte> pixelDataAsBytes = MemoryMarshal.Cast<uint, byte>(pixelData);
            for (int y = 0; y < decoder.Height; y++)
            {
                Span<byte> row = pixelDataAsBytes.Slice(y * width * 4, width * 4);
                Span<TPixel> pixelSpan = pixels.GetRowSpan(y);
                PixelOperations<TPixel>.Instance.FromBgra32Bytes(
                    this.configuration,
                    row,
                    pixelSpan,
                    width);
            }
        }

        private void DecodeImageData(Vp8LDecoder decoder, Span<uint> pixelData)
        {
            int lastPixel = 0;
            int width = decoder.Width;
            int height = decoder.Height;
            int row = lastPixel / width;
            int col = lastPixel % width;
            int lenCodeLimit = WebPConstants.NumLiteralCodes + WebPConstants.NumLengthCodes;
            int colorCacheSize = decoder.Metadata.ColorCacheSize;
            ColorCache colorCache = decoder.Metadata.ColorCache;
            int colorCacheLimit = lenCodeLimit + colorCacheSize;
            int mask = decoder.Metadata.HuffmanMask;
            HTreeGroup[] hTreeGroup = this.GetHTreeGroupForPos(decoder.Metadata, col, row);

            int totalPixels = width * height;
            int decodedPixels = 0;
            int lastCached = decodedPixels;
            while (decodedPixels < totalPixels)
            {
                int code;
                if ((col & mask) is 0)
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
                uint huffmanPrecision = this.bitReader.ReadValue(3) + 2;
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
                int alphabetSize = WebPConstants.AlphabetSize[j];
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
                    int alphabetSize = WebPConstants.AlphabetSize[j];
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

                    if (isTrivialLiteral && LiteralMap[j] == 1)
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
                uint numSymbols = this.bitReader.ReadValue(1) + 1;
                uint firstSymbolLenCode = this.bitReader.ReadValue(1);

                // The first code is either 1 bit or 8 bit code.
                uint symbol = this.bitReader.ReadValue((firstSymbolLenCode is 0) ? 1 : 8);
                codeLengths[symbol] = 1;

                // The second code (if present), is always 8 bit long.
                if (numSymbols is 2)
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
                var codeLengthCodeLengths = new int[NumCodeLengthCodes];
                uint numCodes = this.bitReader.ReadValue(4) + 4;
                if (numCodes > NumCodeLengthCodes)
                {
                    WebPThrowHelper.ThrowImageFormatException("Bitstream error, numCodes has an invalid value");
                }

                for (int i = 0; i < numCodes; i++)
                {
                    codeLengthCodeLengths[CodeLengthCodeOrder[i]] = (int)this.bitReader.ReadValue(3);
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
                int lengthNBits = 2 + (2 * (int)this.bitReader.ReadValue(3));
                maxSymbol = 2 + (int)this.bitReader.ReadValue(lengthNBits);
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
                if (codeLen < WebPConstants.CodeLengthLiterals)
                {
                    codeLengths[symbol++] = (int)codeLen;
                    if (codeLen != 0)
                    {
                        prevCodeLen = (int)codeLen;
                    }
                }
                else
                {
                    bool usePrev = codeLen == WebPConstants.CodeLengthRepeatCode;
                    uint slot = codeLen - WebPConstants.CodeLengthLiterals;
                    int extraBits = WebPConstants.CodeLengthExtraBits[slot];
                    int repeatOffset = WebPConstants.CodeLengthRepeatOffsets[slot];
                    int repeat = (int)(this.bitReader.ReadValue(extraBits) + repeatOffset);
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
            var transformType = (Vp8LTransformType)this.bitReader.ReadValue(2);
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
                    uint numColors = this.bitReader.ReadValue(8) + 1;
                    int bits = (numColors > 16) ? 0
                                     : (numColors > 4) ? 1
                                     : (numColors > 2) ? 2
                                     : 3;
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
                        transform.Bits = (int)this.bitReader.ReadValue(3) + 2;
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
            int lenCodeLimit = WebPConstants.NumLiteralCodes + WebPConstants.NumLengthCodes;
            int mask = hdr.HuffmanMask;
            HTreeGroup[] htreeGroup = (pos < last) ? this.GetHTreeGroupForPos(hdr, col, row) : null;
            while (!this.bitReader.Eos && pos < last)
            {
                // Only update when changing tile.
                if ((col & mask) is 0)
                {
                    htreeGroup = this.GetHTreeGroupForPos(hdr, col, row);
                }

                this.bitReader.FillBitWindow();
                int code = (int)this.ReadSymbol(htreeGroup[0].HTrees[HuffIndex.Green]);
                if (code < WebPConstants.NumLiteralCodes)
                {
                    // Literal
                    data[pos] = (byte)code;
                    ++pos;
                    ++col;

                    if (col >= width)
                    {
                        col = 0;
                        ++row;
                        if (row <= lastRow && (row % NumArgbCacheRows is 0))
                        {
                            this.ExtractPalettedAlphaRows(dec, row);
                        }
                    }
                }
                else if (code < lenCodeLimit)
                {
                    // Backward reference
                    int lengthSym = code - WebPConstants.NumLiteralCodes;
                    int length = this.GetCopyLength(lengthSym);
                    int distSymbol = (int)this.ReadSymbol(htreeGroup[0].HTrees[HuffIndex.Dist]);
                    this.bitReader.FillBitWindow();
                    int distCode = this.GetCopyDistance(distSymbol);
                    int dist = this.PlaneCodeToDistance(width, distCode);
                    if (pos >= dist && end - pos >= length)
                    {
                        data.Slice(pos - dist, length).CopyTo(data.Slice(pos));
                    }
                    else
                    {
                        WebPThrowHelper.ThrowImageFormatException("error while decoding alpha data");
                    }

                    pos += length;
                    col += length;
                    while (col >= width)
                    {
                        col -= width;
                        ++row;
                        if (row <= lastRow && (row % NumArgbCacheRows is 0))
                        {
                            this.ExtractPalettedAlphaRows(dec, row);
                        }
                    }

                    if (pos < last && (col & mask) > 0)
                    {
                        htreeGroup = this.GetHTreeGroupForPos(hdr, col, row);
                    }
                }
                else
                {
                    WebPThrowHelper.ThrowImageFormatException("bitstream error while parsing alpha data");
                }

                this.bitReader.Eos = this.bitReader.IsEndOfStream();
            }

            // Process the remaining rows corresponding to last row-block.
            this.ExtractPalettedAlphaRows(dec, row > lastRow ? lastRow : row);
        }

        private void ExtractPalettedAlphaRows(AlphaDecoder dec, int lastRow)
        {
            // For vertical and gradient filtering, we need to decode the part above the
            // cropTop row, in order to have the correct spatial predictors.
            int topRow = (dec.AlphaFilterType is WebPAlphaFilterType.None || dec.AlphaFilterType is WebPAlphaFilterType.Horizontal)
                             ? dec.CropTop
                             : dec.LastRow;
            int firstRow = (dec.LastRow < topRow) ? topRow : dec.LastRow;
            if (lastRow > firstRow)
            {
                // Special method for paletted alpha data. We only process the cropped area.
                Span<byte> output = dec.Alpha.Memory.Span;
                Span<uint> pixelData = dec.Vp8LDec.Pixels.Memory.Span;
                Span<byte> pixelDataAsBytes = MemoryMarshal.Cast<uint, byte>(pixelData);
                Span<byte> dst = output.Slice(dec.Width * firstRow);
                Span<byte> input = pixelDataAsBytes.Slice(dec.Vp8LDec.Width * firstRow);

                // TODO: check if any and the correct transform is present
                Vp8LTransform transform = dec.Vp8LDec.Transforms[0];
                this.ColorIndexInverseTransformAlpha(transform, firstRow, lastRow, input, dst);
                dec.AlphaApplyFilter(firstRow, lastRow, dst, dec.Width);
            }

            dec.LastRow = lastRow;
        }

        private void ColorIndexInverseTransformAlpha(
            Vp8LTransform transform,
            int yStart,
            int yEnd,
            Span<byte> src,
            Span<byte> dst)
        {
            int bitsPerPixel = 8 >> transform.Bits;
            int width = transform.XSize;
            Span<uint> colorMap = transform.Data.Memory.Span;
            int srcOffset = 0;
            int dstOffset = 0;
            if (bitsPerPixel < 8)
            {
                int pixelsPerByte = 1 << transform.Bits;
                int countMask = pixelsPerByte - 1;
                int bitMask = (1 << bitsPerPixel) - 1;
                for (int y = yStart; y < yEnd; ++y)
                {
                    int packedPixels = 0;
                    for (int x = 0; x < width; ++x)
                    {
                        if ((x & countMask) is 0)
                        {
                            packedPixels = src[srcOffset];
                            srcOffset++;
                        }

                        dst[dstOffset] = GetAlphaValue((int)colorMap[packedPixels & bitMask]);
                        dstOffset++;
                        packedPixels >>= bitsPerPixel;
                    }
                }
            }
            else
            {
                MapAlpha(src, colorMap, dst, yStart, yEnd, width);
            }
        }

        private static void MapAlpha(Span<byte> src, Span<uint> colorMap, Span<byte> dst, int yStart, int yEnd, int width)
        {
            int offset = 0;
            for (int y = yStart; y < yEnd; ++y)
            {
                for (int x = 0; x < width; ++x)
                {
                    dst[offset] = GetAlphaValue((int)colorMap[src[offset]]);
                    offset++;
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

        /// <summary>
        /// Decodes the next Huffman code from the bit-stream.
        /// FillBitWindow(br) needs to be called at minimum every second call to ReadSymbol, in order to pre-fetch enough bits.
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

        private HTreeGroup[] GetHTreeGroupForPos(Vp8LMetadata metadata, int x, int y)
        {
            uint metaIndex = this.GetMetaIndex(metadata.HuffmanImage, metadata.HuffmanXSize, metadata.HuffmanSubSampleBits, x, y);
            return metadata.HTreeGroups.AsSpan((int)metaIndex).ToArray();
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

        private int PlaneCodeToDistance(int xSize, int planeCode)
        {
            if (planeCode > CodeToPlaneCodes)
            {
                return planeCode - CodeToPlaneCodes;
            }

            int distCode = WebPLookupTables.CodeToPlane[planeCode - 1];
            int yOffset = distCode >> 4;
            int xOffset = 8 - (distCode & 0xf);
            int dist = (yOffset * xSize) + xOffset;

            // dist < 1 can happen if xSize is very small.
            return (dist >= 1) ? dist : 1;
        }

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

        private int GetCopyLength(int lengthSymbol)
        {
            // Length and distance prefixes are encoded the same way.
            return this.GetCopyDistance(lengthSymbol);
        }

        private int AccumulateHCode(HuffmanCode hCode, int shift, HuffmanCode huff)
        {
            huff.BitsUsed += hCode.BitsUsed;
            huff.Value |= hCode.Value << shift;
            return hCode.BitsUsed;
        }

        private static byte GetAlphaValue(int val)
        {
            return (byte)((val >> 8) & 0xff);
        }
    }
}
