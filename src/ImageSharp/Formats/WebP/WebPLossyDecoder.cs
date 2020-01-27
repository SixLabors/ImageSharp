// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Memory;

namespace SixLabors.ImageSharp.Formats.WebP
{
    internal sealed class WebPLossyDecoder : WebPDecoderBase
    {
        private readonly Vp8BitReader bitReader;

        private readonly MemoryAllocator memoryAllocator;

        public WebPLossyDecoder(Vp8BitReader bitReader, MemoryAllocator memoryAllocator)
        {
            this.memoryAllocator = memoryAllocator;
            this.bitReader = bitReader;
        }

        public void Decode<TPixel>(Buffer2D<TPixel> pixels, int width, int height, int vp8Version)
            where TPixel : struct, IPixel<TPixel>
        {
            // we need buffers for Y U and V in size of the image
            // TODO: increase size to enable using all prediction blocks? (see https://tools.ietf.org/html/rfc6386#page-9 )
            Buffer2D<YUVPixel> yuvBufferCurrentFrame = this.memoryAllocator.Allocate2D<YUVPixel>(width, height);

            // TODO: var predictionBuffer - macro-block-sized with approximation of the portion of the image being reconstructed.
            //  those prediction values are the base, the values from DCT processing are added to that

            // TODO residue signal from DCT: 4x4 blocks of DCT transforms, 16Y, 4U, 4V
            Vp8Profile vp8Profile = this.DecodeProfile(vp8Version);

            // Paragraph 9.3: Parse the segment header.
            var proba = new Vp8Proba();
            Vp8SegmentHeader vp8SegmentHeader = this.ParseSegmentHeader(proba);

            // Paragraph 9.4: Parse the filter specs.
            Vp8FilterHeader vp8FilterHeader = this.ParseFilterHeader();

            // TODO: Review Paragraph 9.5: ParsePartitions.
            int numPartsMinusOne = (1 << (int)this.bitReader.ReadValue(2)) - 1;
            int lastPart = numPartsMinusOne;
            // TODO: check if we have enough data available here, throw exception if not
            int partStart = this.bitReader.Pos + (lastPart * 3);

            // Paragraph 9.6: Dequantization Indices.
            this.ParseDequantizationIndices(vp8SegmentHeader);

            // Ignore the value of update_proba
            this.bitReader.ReadBool();

            // Paragraph 13.4: Parse probabilities.
            this.ParseProbabilities(proba);
        }

        private Vp8Profile DecodeProfile(int version)
        {
            switch (version)
            {
                case 0:
                    return new Vp8Profile { ReconstructionFilter = ReconstructionFilter.Bicubic, LoopFilter = LoopFilter.Complex };
                case 1:
                    return new Vp8Profile { ReconstructionFilter = ReconstructionFilter.Bilinear, LoopFilter = LoopFilter.Simple };
                case 2:
                    return new Vp8Profile { ReconstructionFilter = ReconstructionFilter.Bilinear, LoopFilter = LoopFilter.None };
                case 3:
                    return new Vp8Profile { ReconstructionFilter = ReconstructionFilter.None, LoopFilter = LoopFilter.None };
                default:
                    // Reserved for future use in Spec.
                    // https://tools.ietf.org/html/rfc6386#page-30
                    WebPThrowHelper.ThrowNotSupportedException($"unsupported VP8 version {version} found");
                    return new Vp8Profile();
            }
        }

        private Vp8SegmentHeader ParseSegmentHeader(Vp8Proba proba)
        {
            var vp8SegmentHeader = new Vp8SegmentHeader
            {
                UseSegment = this.bitReader.ReadBool()
            };
            if (vp8SegmentHeader.UseSegment)
            {
                vp8SegmentHeader.UpdateMap = this.bitReader.ReadBool();
                bool updateData = this.bitReader.ReadBool();
                if (updateData)
                {
                    vp8SegmentHeader.Delta = this.bitReader.ReadBool();
                    bool hasValue;
                    for (int i = 0; i < vp8SegmentHeader.Quantizer.Length; i++)
                    {
                        hasValue = this.bitReader.ReadBool();
                        uint quantizeValue = hasValue ? this.bitReader.ReadValue(7) : 0;
                        vp8SegmentHeader.Quantizer[i] = (byte)quantizeValue;
                    }

                    for (int i = 0; i < vp8SegmentHeader.FilterStrength.Length; i++)
                    {
                        hasValue = this.bitReader.ReadBool();
                        uint filterStrengthValue = hasValue ? this.bitReader.ReadValue(6) : 0;
                        vp8SegmentHeader.FilterStrength[i] = (byte)filterStrengthValue;
                    }

                    if (vp8SegmentHeader.UpdateMap)
                    {
                        for (int s = 0; s < proba.Segments.Length; ++s)
                        {
                            hasValue = bitReader.ReadBool();
                            proba.Segments[s] = hasValue ? bitReader.ReadValue(8) : 255;
                        }
                    }
                }
            }
            else
            {
                vp8SegmentHeader.UpdateMap = false;
            }

            return vp8SegmentHeader;
        }

        private Vp8FilterHeader ParseFilterHeader()
        {
            var vp8FilterHeader = new Vp8FilterHeader();
            vp8FilterHeader.LoopFilter = this.bitReader.ReadBool() ? LoopFilter.Simple : LoopFilter.Complex;
            vp8FilterHeader.Level = (int)this.bitReader.ReadValue(6);
            vp8FilterHeader.Sharpness = (int)this.bitReader.ReadValue(3);
            vp8FilterHeader.UseLfDelta = this.bitReader.ReadBool();

            // TODO: use enum here?
            // 0 = 0ff, 1 = simple, 2 = complex
            int filterType = (vp8FilterHeader.Level is 0) ? 0 : vp8FilterHeader.LoopFilter is LoopFilter.Simple ? 1 : 2;
            if (vp8FilterHeader.UseLfDelta)
            {
                // Update lf-delta?
                if (this.bitReader.ReadBool())
                {
                    bool hasValue;
                    for (int i = 0; i < vp8FilterHeader.RefLfDelta.Length; i++)
                    {
                        hasValue = this.bitReader.ReadBool();
                        if (hasValue)
                        {
                            vp8FilterHeader.RefLfDelta[i] = this.bitReader.ReadSignedValue(6);
                        }
                    }

                    for (int i = 0; i < vp8FilterHeader.ModeLfDelta.Length; i++)
                    {
                        hasValue = this.bitReader.ReadBool();
                        if (hasValue)
                        {
                            vp8FilterHeader.ModeLfDelta[i] = this.bitReader.ReadSignedValue(6);
                        }
                    }
                }
            }

            return vp8FilterHeader;
        }

        private void ParseDequantizationIndices(Vp8SegmentHeader vp8SegmentHeader)
        {
            int baseQ0 = (int)this.bitReader.ReadValue(7);
            bool hasValue = this.bitReader.ReadBool();
            int dqy1Dc = hasValue ? this.bitReader.ReadSignedValue(4) : 0;
            hasValue = this.bitReader.ReadBool();
            int dqy2Dc = hasValue ? this.bitReader.ReadSignedValue(4) : 0;
            hasValue = this.bitReader.ReadBool();
            int dqy2Ac = hasValue ? this.bitReader.ReadSignedValue(4) : 0;
            hasValue = this.bitReader.ReadBool();
            int dquvDc = hasValue ? this.bitReader.ReadSignedValue(4) : 0;
            hasValue = this.bitReader.ReadBool();
            int dquvAc = hasValue ? this.bitReader.ReadSignedValue(4) : 0;
            for (int i = 0; i < WebPConstants.NumMbSegments; ++i)
            {
                int q;
                if (vp8SegmentHeader.UseSegment)
                {
                    q = vp8SegmentHeader.Quantizer[i];
                    if (!vp8SegmentHeader.Delta)
                    {
                        q += baseQ0;
                    }
                }
                else
                {
                    if (i > 0)
                    {
                        // dec->dqm_[i] = dec->dqm_[0];
                        continue;
                    }
                    else
                    {
                        q = baseQ0;
                    }
                }

                var m = new Vp8QuantMatrix();
                m.Y1Mat[0] = WebPConstants.DcTable[this.Clip(q + dqy1Dc, 127)];
                m.Y1Mat[1] = WebPConstants.AcTable[this.Clip(q + 0, 127)];
                m.Y2Mat[0] = WebPConstants.DcTable[this.Clip(q + dqy2Dc, 127)] * 2;

                // For all x in [0..284], x*155/100 is bitwise equal to (x*101581) >> 16.
                // The smallest precision for that is '(x*6349) >> 12' but 16 is a good word size.
                m.Y2Mat[1] = (WebPConstants.AcTable[this.Clip(q + dqy2Ac, 127)] * 101581) >> 16;
                if (m.Y2Mat[1] < 8)
                {
                    m.Y2Mat[1] = 8;
                }

                m.UvMat[0] = WebPConstants.DcTable[this.Clip(q + dquvDc, 117)];
                m.UvMat[1] = WebPConstants.AcTable[this.Clip(q + dquvAc, 127)];

                // For dithering strength evaluation.
                m.UvQuant = q + dquvAc;
            }
        }

        private void ParseProbabilities(Vp8Proba proba)
        {
            for (int t = 0; t < WebPConstants.NumTypes; ++t)
            {
                for (int b = 0; b < WebPConstants.NumBands; ++b)
                {
                    for (int c = 0; c < WebPConstants.NumCtx; ++c)
                    {
                        for (int p = 0; p < WebPConstants.NumProbas; ++p)
                        {
                            var prob = WebPConstants.CoeffsUpdateProba[t, b, c, p];
                            int v = this.bitReader.GetBit(prob) == 0
                                        ? (int)this.bitReader.ReadValue(8)
                                        : WebPConstants.DefaultCoeffsProba[t, b, c, p];
                            proba.Bands[t, b].Probabilities[c].Probabilities[p] = (uint)v;
                        }
                    }
                }

                for (int b = 0; b < 16 + 1; ++b)
                {
                    // TODO: This needs to be reviewed and fixed.
                    // proba->bands_ptr_[t][b] = &proba->bands_[t][kBands[b]];
                }
            }

            // TODO: those values needs to be stored somewhere
            bool useSkipProba = this.bitReader.ReadBool();
            if (useSkipProba)
            {
                var skipP = this.bitReader.ReadValue(8);
            }
        }

        static bool Is8bOptimizable(Vp8LMetadata hdr)
        {
            int i;
            if (hdr.ColorCacheSize > 0)
            {
                return false;
            }

            // When the Huffman tree contains only one symbol, we can skip the
            // call to ReadSymbol() for red/blue/alpha channels.
            for (i = 0; i < hdr.NumHTreeGroups; ++i)
            {
                List<HuffmanCode[]> htrees = hdr.HTreeGroups[i].HTrees;
                if (htrees[HuffIndex.Red][0].Value > 0)
                {
                    return false;
                }

                if (htrees[HuffIndex.Blue][0].Value > 0)
                {
                    return false;
                }

                if (htrees[HuffIndex.Alpha][0].Value > 0)
                {
                    return false;
                }
            }

            return true;
        }

        private int Clip(int v, int M)
        {
            return v < 0 ? 0 : v > M ? M : v;
        }
    }

    struct YUVPixel
    {
        public byte Y { get; }

        public byte U { get; }

        public byte V { get; }
    }
}
