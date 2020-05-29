// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;
using SixLabors.ImageSharp.Formats.WebP.BitWriter;
using SixLabors.ImageSharp.Formats.WebP.Lossless;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// Image encoder for writing an image to a stream in the WebP format.
    /// </summary>
    internal sealed class WebPEncoderCore
    {
        /// <summary>
        /// Used for allocating memory during processing operations.
        /// </summary>
        private readonly MemoryAllocator memoryAllocator;

        /// <summary>
        /// The global configuration.
        /// </summary>
        private Configuration configuration;

        /// <summary>
        /// A bit writer for writing lossless webp streams.
        /// </summary>
        private Vp8LBitWriter bitWriter;

        /// <summary>
        /// Initializes a new instance of the <see cref="WebPEncoderCore"/> class.
        /// </summary>
        /// <param name="options">The encoder options.</param>
        /// <param name="memoryAllocator">The memory manager.</param>
        public WebPEncoderCore(IWebPEncoderOptions options, MemoryAllocator memoryAllocator)
        {
            this.memoryAllocator = memoryAllocator;
        }

        /// <summary>
        /// Encodes the image to the specified stream from the <see cref="ImageFrame{TPixel}"/>.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The <see cref="ImageFrame{TPixel}"/> to encode from.</param>
        /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
        public void Encode<TPixel>(Image<TPixel> image, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Guard.NotNull(image, nameof(image));
            Guard.NotNull(stream, nameof(stream));

            this.configuration = image.GetConfiguration();
            ImageMetadata metadata = image.Metadata;

            int width = image.Width;
            int height = image.Height;
            int initialSize = width * height;
            this.bitWriter = new Vp8LBitWriter(initialSize);

            // Write image size.
            this.WriteImageSize(width, height);

            // Write the non-trivial Alpha flag and lossless version.
            bool hasAlpha = false; // TODO: for the start, this will be always false.
            this.WriteAlphaAndVersion(hasAlpha);

            // Encode the main image stream.
            this.EncodeStream(image);
        }

        /// <summary>
        /// Writes the image size to the stream.
        /// </summary>
        /// <param name="inputImgWidth">The input image width.</param>
        /// <param name="inputImgHeight">The input image height.</param>
        private void WriteImageSize(int inputImgWidth, int inputImgHeight)
        {
            Guard.MustBeLessThan(inputImgWidth, WebPConstants.MaxDimension, nameof(inputImgWidth));
            Guard.MustBeLessThan(inputImgHeight, WebPConstants.MaxDimension, nameof(inputImgHeight));

            uint width = (uint)inputImgWidth - 1;
            uint height = (uint)inputImgHeight - 1;

            this.bitWriter.PutBits(width, WebPConstants.Vp8LImageSizeBits);
            this.bitWriter.PutBits(height, WebPConstants.Vp8LImageSizeBits);
        }

        private void WriteAlphaAndVersion(bool hasAlpha)
        {
            this.bitWriter.PutBits(hasAlpha ? 1U : 0, 1);
            this.bitWriter.PutBits(WebPConstants.Vp8LVersion, WebPConstants.Vp8LVersionBits);
        }

        /// <summary>
        /// Encodes the image stream using lossless webp format.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type.</typeparam>
        /// <param name="image">The image to encode.</param>
        private void EncodeStream<TPixel>(Image<TPixel> image)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var encoder = new Vp8LEncoder(this.memoryAllocator, image.Width, image.Height);

            // Analyze image (entropy, num_palettes etc).
            this.EncoderAnalyze(image, encoder);
        }

        /// <summary>
        /// Analyzes the image and decides what transforms should be used.
        /// </summary>
        private void EncoderAnalyze<TPixel>(Image<TPixel> image, Vp8LEncoder enc)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int width = image.Width;
            int height = image.Height;

            // Check if we only deal with a small number of colors and should use a palette.
            var usePalette = this.AnalyzeAndCreatePalette(image, enc);

            // Empirical bit sizes.
            int method = 4; // TODO: method hardcoded to 4 for now.
            enc.HistoBits = GetHistoBits(method, usePalette, width, height);
            enc.TransformBits = GetTransformBits(method, enc.HistoBits);

            // Convert image pixels to bgra array.
            using System.Buffers.IMemoryOwner<uint> bgraBuffer = this.memoryAllocator.Allocate<uint>(width * height);
            Span<uint> bgra = bgraBuffer.Memory.Span;
            int idx = 0;
            for (int y = 0; y < height; y++)
            {
                Span<TPixel> rowSpan = image.GetPixelRowSpan(y);
                for (int x = 0; x < rowSpan.Length; x++)
                {
                    bgra[idx++] = ToBgra32(rowSpan[x]).PackedValue;
                }
            }

            // Try out multiple LZ77 on images with few colors.
            var nlz77s = (enc.PaletteSize > 0 && enc.PaletteSize <= 16) ? 2 : 1;
            EntropyIx entropyIdx = this.AnalyzeEntropy(image, usePalette, enc.PaletteSize, enc.TransformBits, out bool redAndBlueAlwaysZero);

            enc.UsePalette = entropyIdx == EntropyIx.Palette;
            enc.UseSubtractGreenTransform = (entropyIdx == EntropyIx.SubGreen) || (entropyIdx == EntropyIx.SpatialSubGreen);
            enc.UsePredictorTransform = (entropyIdx == EntropyIx.Spatial) || (entropyIdx == EntropyIx.SpatialSubGreen);
            enc.UseCrossColorTransform = redAndBlueAlwaysZero ? false : enc.UsePredictorTransform;
            enc.UseColorCache = false;

            // Encode palette.
            if (enc.UsePalette)
            {
                this.EncodePalette(image, bgra, enc);
            }
        }

        /// <summary>
        /// Save the palette to the bitstream.
        /// </summary>
        /// <param name="image">The image.</param>
        /// <param name="enc">The Vp8L Encoder.</param>
        private void EncodePalette<TPixel>(Image<TPixel> image, Span<uint> bgra, Vp8LEncoder enc)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Span<uint> tmpPalette = new uint[WebPConstants.MaxPaletteSize];
            int paletteSize = enc.PaletteSize;
            Span<uint> palette = enc.Palette.Memory.Span;
            this.bitWriter.PutBits(WebPConstants.TransformPresent, 1);
            this.bitWriter.PutBits((uint)Vp8LTransformType.ColorIndexingTransform, 2);
            this.bitWriter.PutBits((uint)paletteSize - 1, 8);
            for (int i = paletteSize - 1; i >= 1; i--)
            {
                tmpPalette[i] = LosslessUtils.SubPixels(palette[i], palette[i - 1]);
            }

            tmpPalette[0] = palette[0];
            this.EncodeImageNoHuffman(tmpPalette, enc.HashChain, enc.Refs[0], enc.Refs[1], width: paletteSize, height: 1, quality: 20);
        }

        private void EncodeImageNoHuffman(Span<uint> bgra, Vp8LHashChain hashChain, Vp8LBackwardRefs refsTmp1, Vp8LBackwardRefs refsTmp2, int width, int height, int quality)
        {
            var huffmanCodes = new HuffmanTreeCode[5];
            int cacheBits = 0;
            HuffmanTreeToken[] tokens;
            var huffTree = new HuffmanTree[3UL * WebPConstants.CodeLengthCodes];

            // Calculate backward references from ARGB image.
            BackwardReferenceEncoder.HashChainFill(hashChain, bgra, quality, width, height);

            Vp8LBackwardRefs refs = BackwardReferenceEncoder.GetBackwardReferences(
                width,
                height,
                bgra,
                quality,
                (int)Vp8LLz77Type.Lz77Standard | (int)Vp8LLz77Type.Lz77Rle,
                cacheBits,
                hashChain,
                refsTmp1,
                refsTmp2);

            var histogramImage = new List<Vp8LHistogram>()
            {
                new Vp8LHistogram(cacheBits)
            };

            // Build histogram image and symbols from backward references.
            histogramImage[0].StoreRefs(refs);

            // Create Huffman bit lengths and codes for each histogram image.
            GetHuffBitLengthsAndCodes(histogramImage, huffmanCodes);

            // No color cache, no Huffman image.
            this.bitWriter.PutBits(0, 1);

            // Find maximum number of symbols for the huffman tree-set.
            /*for (i = 0; i < 5; ++i)
            {
                HuffmanTreeCode * const codes = &huffman_codes[i];
                if (max_tokens < codes->num_symbols)
                {
                    max_tokens = codes->num_symbols;
                }
            }*/

            // Store Huffman codes.
            /*
            for (i = 0; i < 5; ++i)
            {
                HuffmanTreeCode * const codes = &huffman_codes[i];
                StoreHuffmanCode(bw, huff_tree, tokens, codes);
                ClearHuffmanTreeIfOnlyOneSymbol(codes);
            }

            // Store actual literals.
            StoreImageToBitMask(bw, width, 0, refs, histogram_symbols, huffman_codes);
            */
        }

        private void StoreImageToBitMask(int width, int histoBits, short[] histogramSymbols, HuffmanTreeCode[] huffmanCodes)
        {
            int histoXSize = histoBits > 0 ? LosslessUtils.SubSampleSize(width, histoBits) : 1;
            int tileMask = (histoBits == 0) ? 0 : -(1 << histoBits);

            // x and y trace the position in the image.
            int x = 0;
            int y = 0;
            int tileX = x & tileMask;
            int tileY = y & tileMask;
            int histogramIx = histogramSymbols[0];
            Span<HuffmanTreeCode> codes = huffmanCodes.AsSpan(5 * histogramIx);

        }

        /// <summary>
        /// Analyzes the entropy of the input image to determine which transforms to use during encoding the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type of the image.</typeparam>
        /// <param name="image">The image to analyze.</param>
        /// <param name="usePalette">Indicates whether a palette should be used.</param>
        /// <param name="paletteSize">The palette size.</param>
        /// <param name="transformBits">The transformation bits.</param>
        /// <param name="redAndBlueAlwaysZero">Indicates if red and blue are always zero.</param>
        /// <returns>The entropy mode to use.</returns>
        private EntropyIx AnalyzeEntropy<TPixel>(Image<TPixel> image, bool usePalette, int paletteSize, int transformBits, out bool redAndBlueAlwaysZero)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int width = image.Width;
            int height = image.Height;

            if (usePalette && paletteSize <= 16)
            {
                // In the case of small palettes, we pack 2, 4 or 8 pixels together. In
                // practice, small palettes are better than any other transform.
                redAndBlueAlwaysZero = true;
                return EntropyIx.Palette;
            }

            using System.Buffers.IMemoryOwner<uint> histoBuffer = this.memoryAllocator.Allocate<uint>((int)HistoIx.HistoTotal * 256);
            Span<uint> histo = histoBuffer.Memory.Span;
            Bgra32 pixPrev = ToBgra32(image.GetPixelRowSpan(0)[0]); // Skip the first pixel.
            Span<TPixel> prevRow = null;
            for (int y = 0; y < height; y++)
            {
                Span<TPixel> currentRow = image.GetPixelRowSpan(y);
                for (int x = 0; x < width; x++)
                {
                    Bgra32 pix = ToBgra32(currentRow[x]);
                    uint pixDiff = LosslessUtils.SubPixels(pix.PackedValue, pixPrev.PackedValue);
                    pixPrev = pix;
                    if ((pixDiff == 0) || (prevRow != null && pix == ToBgra32(prevRow[x])))
                    {
                        continue;
                    }

                    AddSingle(
                        pix.PackedValue,
                        histo.Slice((int)HistoIx.HistoAlpha * 256),
                        histo.Slice((int)HistoIx.HistoRed * 256),
                        histo.Slice((int)HistoIx.HistoGreen * 256),
                        histo.Slice((int)HistoIx.HistoBlue * 256));
                    AddSingle(
                        pixDiff,
                        histo.Slice((int)HistoIx.HistoAlphaPred * 256),
                        histo.Slice((int)HistoIx.HistoRedPred * 256),
                        histo.Slice((int)HistoIx.HistoGreenPred * 256),
                        histo.Slice((int)HistoIx.HistoBluePred * 256));
                    AddSingleSubGreen(
                        pix.PackedValue,
                        histo.Slice((int)HistoIx.HistoRedSubGreen * 256),
                        histo.Slice((int)HistoIx.HistoBlueSubGreen * 256));
                    AddSingleSubGreen(
                        pixDiff,
                        histo.Slice((int)HistoIx.HistoRedPredSubGreen * 256),
                        histo.Slice((int)HistoIx.HistoBluePredSubGreen * 256));

                    // Approximate the palette by the entropy of the multiplicative hash.
                    uint hash = HashPix(pix.PackedValue);
                    histo[((int)HistoIx.HistoPalette * 256) + (int)hash]++;
                }

                var histo0 = histo[0];
                prevRow = currentRow;
            }

            var entropyComp = new double[(int)HistoIx.HistoTotal];
            var entropy = new double[(int)EntropyIx.NumEntropyIx];
            int lastModeToAnalyze = usePalette ? (int)EntropyIx.Palette : (int)EntropyIx.SpatialSubGreen;

            // Let's add one zero to the predicted histograms. The zeros are removed
            // too efficiently by the pixDiff == 0 comparison, at least one of the
            // zeros is likely to exist.
            histo[(int)HistoIx.HistoRedPredSubGreen * 256]++;
            histo[(int)HistoIx.HistoBluePredSubGreen * 256]++;
            histo[(int)HistoIx.HistoRedPred * 256]++;
            histo[(int)HistoIx.HistoGreenPred * 256]++;
            histo[(int)HistoIx.HistoBluePred * 256]++;
            histo[(int)HistoIx.HistoAlphaPred * 256]++;

            for (int j = 0; j < (int)HistoIx.HistoTotal; ++j)
            {
                var bitEntropy = new Vp8LBitEntropy();
                Span<uint> curHisto = histo.Slice(j * 256, 256);
                bitEntropy.BitsEntropyUnrefined(curHisto, 256);
                entropyComp[j] = bitEntropy.BitsEntropyRefine();
            }

            entropy[(int)EntropyIx.Direct] = entropyComp[(int)HistoIx.HistoAlpha] +
                                             entropyComp[(int)HistoIx.HistoRed] +
                                             entropyComp[(int)HistoIx.HistoGreen] +
                                             entropyComp[(int)HistoIx.HistoBlue];
            entropy[(int)EntropyIx.Spatial] = entropyComp[(int)HistoIx.HistoAlphaPred] +
                                              entropyComp[(int)HistoIx.HistoRedPred] +
                                              entropyComp[(int)HistoIx.HistoGreenPred] +
                                              entropyComp[(int)HistoIx.HistoBluePred];
            entropy[(int)EntropyIx.SubGreen] = entropyComp[(int)HistoIx.HistoAlpha] +
                                               entropyComp[(int)HistoIx.HistoRedSubGreen] +
                                               entropyComp[(int)HistoIx.HistoGreen] +
                                               entropyComp[(int)HistoIx.HistoBlueSubGreen];
            entropy[(int)EntropyIx.SpatialSubGreen] = entropyComp[(int)HistoIx.HistoAlphaPred] +
                                                      entropyComp[(int)HistoIx.HistoRedPredSubGreen] +
                                                      entropyComp[(int)HistoIx.HistoGreenPred] +
                                                      entropyComp[(int)HistoIx.HistoBluePredSubGreen];
            entropy[(int)EntropyIx.Palette] = entropyComp[(int)HistoIx.HistoPalette];

            // When including transforms, there is an overhead in bits from
            // storing them. This overhead is small but matters for small images.
            // For spatial, there are 14 transformations.
            entropy[(int)EntropyIx.Spatial] += LosslessUtils.SubSampleSize(width, transformBits) *
                                               LosslessUtils.SubSampleSize(height, transformBits) *
                                               LosslessUtils.FastLog2(14);

            // For color transforms: 24 as only 3 channels are considered in a ColorTransformElement.
            entropy[(int)EntropyIx.SpatialSubGreen] += LosslessUtils.SubSampleSize(width, transformBits) *
                                                       LosslessUtils.SubSampleSize(height, transformBits) *
                                                       LosslessUtils.FastLog2(24);

            // For palettes, add the cost of storing the palette.
            // We empirically estimate the cost of a compressed entry as 8 bits.
            // The palette is differential-coded when compressed hence a much
            // lower cost than sizeof(uint32_t)*8.
            entropy[(int)EntropyIx.Palette] += paletteSize * 8;

            EntropyIx minEntropyIx = EntropyIx.Direct;
            for (int k = (int)EntropyIx.Direct + 1; k <= lastModeToAnalyze; k++)
            {
                if (entropy[(int)minEntropyIx] > entropy[k])
                {
                    minEntropyIx = (EntropyIx)k;
                }
            }

            redAndBlueAlwaysZero = true;

            // Let's check if the histogram of the chosen entropy mode has
            // non-zero red and blue values. If all are zero, we can later skip
            // the cross color optimization.
            var histoPairs = new byte[][]
            {
                new byte[] { (byte)HistoIx.HistoRed, (byte)HistoIx.HistoBlue },
                new byte[] { (byte)HistoIx.HistoRedPred, (byte)HistoIx.HistoBluePred },
                new byte[] { (byte)HistoIx.HistoRedSubGreen, (byte)HistoIx.HistoBlueSubGreen },
                new byte[] { (byte)HistoIx.HistoRedPredSubGreen, (byte)HistoIx.HistoBluePredSubGreen },
                new byte[] { (byte)HistoIx.HistoRed, (byte)HistoIx.HistoBlue }
            };
            Span<uint> redHisto = histo.Slice(256 * histoPairs[(int)minEntropyIx][0]);
            Span<uint> blueHisto = histo.Slice(256 * histoPairs[(int)minEntropyIx][1]);
            for (int i = 1; i < 256; i++)
            {
                if ((redHisto[i] | blueHisto[i]) != 0)
                {
                    redAndBlueAlwaysZero = false;
                    break;
                }
            }

            return minEntropyIx;
        }

        /// <summary>
        /// If number of colors in the image is less than or equal to MAX_PALETTE_SIZE,
        /// creates a palette and returns true, else returns false.
        /// </summary>
        /// <returns>true, if a palette should be used.</returns>
        private bool AnalyzeAndCreatePalette<TPixel>(Image<TPixel> image, Vp8LEncoder enc)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Span<uint> palette = enc.Palette.Memory.Span;
            enc.PaletteSize = this.GetColorPalette(image, palette);
            if (enc.PaletteSize > WebPConstants.MaxPaletteSize)
            {
                enc.PaletteSize = 0;
                return false;
            }

            // TODO: figure out how the palette needs to be sorted.
            uint[] paletteArray = palette.Slice(0, enc.PaletteSize).ToArray();
            Array.Sort(paletteArray);
            paletteArray.CopyTo(palette);

            if (PaletteHasNonMonotonousDeltas(palette, enc.PaletteSize))
            {
                GreedyMinimizeDeltas(palette, enc.PaletteSize);
            }

            return true;
        }

        /// <summary>
        /// Gets the color palette.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type of the image.</typeparam>
        /// <param name="image">The image to get the palette from.</param>
        /// <param name="palette">The span to store the palette into.</param>
        /// <returns>The number of palette entries.</returns>
        private int GetColorPalette<TPixel>(Image<TPixel> image, Span<uint> palette)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var colors = new HashSet<TPixel>();
            for (int y = 0; y < image.Height; y++)
            {
                System.Span<TPixel> rowSpan = image.GetPixelRowSpan(y);
                for (int x = 0; x < rowSpan.Length; x++)
                {
                    colors.Add(rowSpan[x]);
                    if (colors.Count > WebPConstants.MaxPaletteSize)
                    {
                        // Exact count is not needed, because a palette will not be used then anyway.
                        return WebPConstants.MaxPaletteSize + 1;
                    }
                }
            }

            // Fill the colors into the palette.
            using HashSet<TPixel>.Enumerator colorEnumerator = colors.GetEnumerator();
            int idx = 0;
            while (colorEnumerator.MoveNext())
            {
                Bgra32 bgra = ToBgra32(colorEnumerator.Current);
                palette[idx++] = bgra.PackedValue;
            }

            return colors.Count;
        }

        /// <summary>
        /// The palette has been sorted by alpha. This function checks if the other components of the palette
        /// have a monotonic development with regards to position in the palette.
        /// If all have monotonic development, there is no benefit to re-organize them greedily. A monotonic development
        /// would be spotted in green-only situations (like lossy alpha) or gray-scale images.
        /// </summary>
        /// <param name="palette">The palette.</param>
        /// <param name="numColors">Number of colors in the palette.</param>
        /// <returns>True, if the palette has no monotonous deltas.</returns>
        private static bool PaletteHasNonMonotonousDeltas(Span<uint> palette, int numColors)
        {
            uint predict = 0x000000;
            byte signFound = 0x00;
            for (int i = 0; i < numColors; ++i)
            {
                uint diff = LosslessUtils.SubPixels(palette[i], predict);
                byte rd = (byte)((diff >> 16) & 0xff);
                byte gd = (byte)((diff >> 8) & 0xff);
                byte bd = (byte)((diff >> 0) & 0xff);
                if (rd != 0x00)
                {
                    signFound |= (byte)((rd < 0x80) ? 1 : 2);
                }

                if (gd != 0x00)
                {
                    signFound |= (byte)((gd < 0x80) ? 8 : 16);
                }

                if (bd != 0x00)
                {
                    signFound |= (byte)((bd < 0x80) ? 64 : 128);
                }
            }

            return (signFound & (signFound << 1)) != 0;  // two consequent signs.
        }

        /// <summary>
        /// Find greedily always the closest color of the predicted color to minimize
        /// deltas in the palette. This reduces storage needs since the palette is stored with delta encoding.
        /// </summary>
        /// <param name="palette">The palette.</param>
        /// <param name="numColors">The number of colors in the palette.</param>
        private static void GreedyMinimizeDeltas(Span<uint> palette, int numColors)
        {
            uint predict = 0x00000000;
            for (int i = 0; i < numColors; ++i)
            {
                int bestIdx = i;
                uint bestScore = ~0U;
                for (int k = i; k < numColors; ++k)
                {
                    uint curScore = PaletteColorDistance(palette[k], predict);
                    if (bestScore > curScore)
                    {
                        bestScore = curScore;
                        bestIdx = k;
                    }
                }

                // swap color(palette[bestIdx], palette[i]);
                uint best = palette[bestIdx];
                palette[bestIdx] = palette[i];
                palette[i] = best;
                predict = palette[i];
            }
        }

        private static void GetHuffBitLengthsAndCodes(List<Vp8LHistogram> histogramImage, HuffmanTreeCode[] huffmanCodes)
        {
            long totalLengthSize = 0;
            int maxNumSymbols = 0;

            // Iterate over all histograms and get the aggregate number of codes used.
            for (int i = 0; i < histogramImage.Count; i++)
            {
                Vp8LHistogram histo = histogramImage[i];
                int startIdx = 5 * i;
                for (int k = 0; k < 5; k++)
                {
                    int numSymbols =
                        (k == 0) ? histo.NumCodes() :
                        (k == 4) ? WebPConstants.NumDistanceCodes : 256;
                    huffmanCodes[startIdx + k].NumSymbols = numSymbols;
                    totalLengthSize += numSymbols;
                }
            }

            var end = 5 * histogramImage.Count;
            for (int i = 0; i < end; i++)
            {
                int bitLength = huffmanCodes[i].NumSymbols;
                huffmanCodes[i].Codes = new short[bitLength];
                huffmanCodes[i].CodeLengths = new byte[bitLength];
                if (maxNumSymbols < bitLength)
                {
                    maxNumSymbols = bitLength;
                }
            }

            // Create Huffman trees.
            bool[] bufRle = new bool[maxNumSymbols];
            var huffTree = new HuffmanTree[3 * maxNumSymbols];
            for (int i = 0; i < histogramImage.Count; i++)
            {
                int codesStartIdx = 5 * i;
                Vp8LHistogram histo = histogramImage[i];
                HuffmanUtils.CreateHuffmanTree(histo.Literal, 15, bufRle, huffTree, huffmanCodes[codesStartIdx]);
                HuffmanUtils.CreateHuffmanTree(histo.Red, 15, bufRle, huffTree, huffmanCodes[codesStartIdx + 1]);
                HuffmanUtils.CreateHuffmanTree(histo.Blue, 15, bufRle, huffTree, huffmanCodes[codesStartIdx + 2]);
                HuffmanUtils.CreateHuffmanTree(histo.Alpha, 15, bufRle, huffTree, huffmanCodes[codesStartIdx + 3]);
                HuffmanUtils.CreateHuffmanTree(histo.Distance, 15, bufRle, huffTree, huffmanCodes[codesStartIdx + 4]);
            }
        }

        /// <summary>
        /// Computes a value that is related to the entropy created by the palette entry diff.
        /// </summary>
        /// <param name="col1">First color.</param>
        /// <param name="col2">Second color.</param>
        /// <returns>The color distance.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        private static uint PaletteColorDistance(uint col1, uint col2)
        {
            uint diff = LosslessUtils.SubPixels(col1, col2);
            uint moreWeightForRGBThanForAlpha = 9;
            uint score = PaletteComponentDistance((diff >> 0) & 0xff);
            score += PaletteComponentDistance((diff >> 8) & 0xff);
            score += PaletteComponentDistance((diff >> 16) & 0xff);
            score *= moreWeightForRGBThanForAlpha;
            score += PaletteComponentDistance((diff >> 24) & 0xff);

            return score;
        }

        /// <summary>
        /// Calculates the huffman image bits.
        /// </summary>
        private static int GetHistoBits(int method, bool usePalette, int width, int height)
        {
            // Make tile size a function of encoding method (Range: 0 to 6).
            int histoBits = (usePalette ? 9 : 7) - method;
            while (true)
            {
                int huffImageSize = LosslessUtils.SubSampleSize(width, histoBits) * LosslessUtils.SubSampleSize(height, histoBits);
                if (huffImageSize <= WebPConstants.MaxHuffImageSize)
                {
                    break;
                }

                histoBits++;
            }

            return (histoBits < WebPConstants.MinHuffmanBits) ? WebPConstants.MinHuffmanBits :
                (histoBits > WebPConstants.MaxHuffmanBits) ? WebPConstants.MaxHuffmanBits : histoBits;
        }

        /// <summary>
        /// Calculates the bits used for the transformation.
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        private static int GetTransformBits(int method, int histoBits)
        {
            int maxTransformBits = (method < 4) ? 6 : (method > 4) ? 4 : 5;
            int res = (histoBits > maxTransformBits) ? maxTransformBits : histoBits;
            return res;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static Bgra32 ToBgra32<TPixel>(TPixel color)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Rgba32 rgba = default;
            color.ToRgba32(ref rgba);
            var bgra = new Bgra32(rgba.R, rgba.G, rgba.B, rgba.A);
            return bgra;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void AddSingle(uint p, Span<uint> a, Span<uint> r, Span<uint> g, Span<uint> b)
        {
            a[(int)(p >> 24) & 0xff]++;
            r[(int)(p >> 16) & 0xff]++;
            g[(int)(p >> 8) & 0xff]++;
            b[(int)(p >> 0) & 0xff]++;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void AddSingleSubGreen(uint p, Span<uint> r, Span<uint> b)
        {
            int green = (int)p >> 8;  // The upper bits are masked away later.
            r[(int)((p >> 16) - green) & 0xff]++;
            b[(int)((p >> 0) - green) & 0xff]++;
        }

        private static uint HashPix(uint pix)
        {
            // Note that masking with 0xffffffffu is for preventing an
            // 'unsigned int overflow' warning. Doesn't impact the compiled code.
            return (uint)((((long)pix + (pix >> 19)) * 0x39c5fba7L) & 0xffffffffu) >> 24;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static uint PaletteComponentDistance(uint v)
        {
            return (v <= 128) ? v : (256 - v);
        }
    }
}
