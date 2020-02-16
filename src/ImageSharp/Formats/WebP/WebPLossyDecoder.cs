// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Memory;

namespace SixLabors.ImageSharp.Formats.WebP
{
    internal sealed class WebPLossyDecoder : WebPDecoderBase
    {
        private readonly Vp8BitReader bitReader;

        private readonly MemoryAllocator memoryAllocator;

        private readonly byte[,][] bModesProba = new byte[10, 10][];

        public WebPLossyDecoder(Vp8BitReader bitReader, MemoryAllocator memoryAllocator)
        {
            this.memoryAllocator = memoryAllocator;
            this.bitReader = bitReader;
            this.InitializeModesProbabilities();
        }

        public void Decode<TPixel>(Buffer2D<TPixel> pixels, int width, int height, WebPImageInfo info)
            where TPixel : struct, IPixel<TPixel>
        {
            // we need buffers for Y U and V in size of the image
            // TODO: increase size to enable using all prediction blocks? (see https://tools.ietf.org/html/rfc6386#page-9 )
            Buffer2D<YUVPixel> yuvBufferCurrentFrame = this.memoryAllocator.Allocate2D<YUVPixel>(width, height);

            // TODO: var predictionBuffer - macro-block-sized with approximation of the portion of the image being reconstructed.
            //  those prediction values are the base, the values from DCT processing are added to that

            // TODO residue signal from DCT: 4x4 blocks of DCT transforms, 16Y, 4U, 4V
            Vp8Profile vp8Profile = this.DecodeProfile(info.Vp8Profile);

            // Paragraph 9.2: color space and clamp type follow.
            sbyte colorSpace = (sbyte)this.bitReader.ReadValue(1);
            sbyte clampType = (sbyte)this.bitReader.ReadValue(1);
            var pictureHeader = new Vp8PictureHeader()
                                   {
                                       Width = (uint)width,
                                       Height = (uint)height,
                                       XScale = info.XScale,
                                       YScale = info.YScale,
                                       ColorSpace = colorSpace,
                                       ClampType = clampType
                                   };

            // Paragraph 9.3: Parse the segment header.
            var proba = new Vp8Proba();
            Vp8SegmentHeader vp8SegmentHeader = this.ParseSegmentHeader(proba);

            Vp8Io io = InitializeVp8Io(pictureHeader);
            var decoder = new Vp8Decoder(info.Vp8FrameHeader, pictureHeader, vp8SegmentHeader, proba, io);

            // Paragraph 9.4: Parse the filter specs.
            this.ParseFilterHeader(decoder);

            // Paragraph 9.5: Parse partitions.
            this.ParsePartitions(decoder);

            // Paragraph 9.6: Dequantization Indices.
            this.ParseDequantizationIndices(decoder);

            // Ignore the value of update_proba
            this.bitReader.ReadBool();

            // Paragraph 13.4: Parse probabilities.
            this.ParseProbabilities(decoder);

            // Decode image data.
            this.ParseFrame(decoder, io);

            this.DecodePixelValues(width, height, decoder.Bgr, pixels);
        }

        private void DecodePixelValues<TPixel>(int width, int height, Span<byte> pixelData, Buffer2D<TPixel> pixels)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel color = default;
            for (int y = 0; y < height; y++)
            {
                Span<TPixel> pixelRow = pixels.GetRowSpan(y);
                for (int x = 0; x < width; x++)
                {
                    int idx = ((y * width) + x) * 3;
                    byte b = pixelData[idx];
                    byte g = pixelData[idx + 1];
                    byte r = pixelData[idx + 2];
                    color.FromRgba32(new Rgba32(r, g, b, 255));
                    pixelRow[x] = color;
                }
            }
        }

        private void ParseFrame(Vp8Decoder dec, Vp8Io io)
        {
            for (dec.MbY = 0; dec.MbY < dec.BottomRightMbY; ++dec.MbY)
            {
                // Parse bitstream for this row.
                long bitreaderIdx = dec.MbY & dec.NumPartsMinusOne;
                Vp8BitReader bitreader = dec.Vp8BitReaders[bitreaderIdx];

                // Parse intra mode mode row.
                for (int mbX = 0; mbX < dec.MbWidth; ++mbX)
                {
                    this.ParseIntraMode(dec, mbX);
                }

                for (; dec.MbX < dec.MbWidth; ++dec.MbX)
                {
                    this.DecodeMacroBlock(dec, bitreader);
                }

                // Prepare for next scanline.
                this.InitScanline(dec);

                // Reconstruct, filter and emit the row.
                this.ProcessRow(dec, io);
            }
        }

        private void ParseIntraMode(Vp8Decoder dec, int mbX)
        {
            Vp8MacroBlockData block = dec.MacroBlockData[mbX];
            byte[] left = dec.IntraL;
            byte[] top = dec.IntraT;

            if (dec.SegmentHeader.UpdateMap)
            {
                // Hardcoded tree parsing.
                block.Segment = this.bitReader.GetBit((int)dec.Probabilities.Segments[0]) != 0
                                    ? (byte)this.bitReader.GetBit((int)dec.Probabilities.Segments[1])
                                    : (byte)this.bitReader.GetBit((int)dec.Probabilities.Segments[2]);
            }
            else
            {
                // default for intra
                block.Segment = 0;
            }

            if (dec.UseSkipProbability)
            {
                block.Skip = (byte)this.bitReader.GetBit(dec.SkipProbability);
            }

            block.IsI4x4 = this.bitReader.GetBit(145) is 0;
            if (!block.IsI4x4)
            {
                // Hardcoded 16x16 intra-mode decision tree.
                int yMode = this.bitReader.GetBit(156) > 0 ?
                                this.bitReader.GetBit(128) > 0 ? WebPConstants.TmPred : WebPConstants.HPred :
                                this.bitReader.GetBit(163) > 0 ? WebPConstants.VPred : WebPConstants.DcPred;
                block.Modes[0] = (byte)yMode;
                for (int i = 0; i < left.Length; i++)
                {
                    left[i] = (byte)yMode;
                    top[i] = (byte)yMode;
                }
            }
            else
            {
                Span<byte> modes = block.Modes.AsSpan();
                for (int y = 0; y < 4; ++y)
                {
                    int yMode = left[y];
                    for (int x = 0; x < 4; ++x)
                    {
                        byte[] prob = this.bModesProba[top[x], yMode];
                        int i = WebPConstants.YModesIntra4[this.bitReader.GetBit(prob[0])];
                        while (i > 0)
                        {
                            i = WebPConstants.YModesIntra4[(2 * i) + this.bitReader.GetBit(prob[i])];
                        }

                        yMode = -i;
                        top[x] = (byte)yMode;
                    }

                    top.CopyTo(modes);
                    modes = modes.Slice(4);
                    left[y] = (byte)yMode;
                }
            }

            // Hardcoded UVMode decision tree.
            block.UvMode = (byte)(this.bitReader.GetBit(142) is 0 ? 0 :
                           this.bitReader.GetBit(114) is 0 ? 2 :
                           this.bitReader.GetBit(183) > 0 ? 1 : 3);
        }

        private void InitScanline(Vp8Decoder dec)
        {
            Vp8MacroBlock left = dec.MacroBlockInfo[0];
            left.NoneZeroAcDcCoeffs = 0;
            left.NoneZeroDcCoeffs = 0;
            for (int i = 0; i < dec.IntraL.Length; i++)
            {
                dec.IntraL[i] = 0;
            }

            dec.MbX = 0;
        }

        private void ProcessRow(Vp8Decoder dec, Vp8Io io)
        {
            bool filterRow = (dec.Filter != LoopFilter.None) &&
                             (dec.MbY >= dec.TopLeftMbY) && (dec.MbY <= dec.BottomRightMbY);

            this.ReconstructRow(dec, filterRow);
            this.FinishRow(dec, io);
        }

        private void ReconstructRow(Vp8Decoder dec, bool filterRow)
        {
            int mby = dec.MbY;

            int yOff = (WebPConstants.Bps * 1) + 8;
            int uOff = yOff + (WebPConstants.Bps * 16) + WebPConstants.Bps;
            int vOff = uOff + 16;

            byte[] yuv = dec.YuvBuffer;
            Span<byte> yDst = dec.YuvBuffer.AsSpan(yOff);
            Span<byte> uDst = dec.YuvBuffer.AsSpan(uOff);
            Span<byte> vDst = dec.YuvBuffer.AsSpan(vOff);

            // Initialize left-most block.
            for (int i = 0; i < 16; ++i)
            {
                yuv[(i * WebPConstants.Bps) - 1 + yOff] = 129;
            }

            for (int i = 0; i < 8; ++i)
            {
                yuv[(i * WebPConstants.Bps) - 1 + uOff] = 129;
                yuv[(i * WebPConstants.Bps) - 1 + vOff] = 129;
            }

            // Init top-left sample on left column too.
            if (mby > 0)
            {
                yuv[yOff - 1 - WebPConstants.Bps] = yuv[uOff - 1 - WebPConstants.Bps] = yuv[vOff - 1 - WebPConstants.Bps] = 129;
            }
            else
            {
                // We only need to do this init once at block (0,0).
                // Afterward, it remains valid for the whole topmost row.
                Span<byte> tmp = dec.YuvBuffer.AsSpan(yOff - WebPConstants.Bps - 1, 16 + 4 + 1);
                for (int i = 0; i < tmp.Length; ++i)
                {
                    tmp[i] = 127;
                }

                tmp = dec.YuvBuffer.AsSpan(uOff - WebPConstants.Bps - 1, 8 + 1);
                for (int i = 0; i < tmp.Length; ++i)
                {
                    tmp[i] = 127;
                }

                tmp = dec.YuvBuffer.AsSpan(vOff - WebPConstants.Bps - 1, 8 + 1);
                for (int i = 0; i < tmp.Length; ++i)
                {
                    tmp[i] = 127;
                }
            }

            // Reconstruct one row.
            for (int mbx = 0; mbx < dec.MbWidth; ++mbx)
            {
                Vp8MacroBlockData block = dec.MacroBlockData[mbx];

                // Rotate in the left samples from previously decoded block. We move four
                // pixels at a time for alignment reason, and because of in-loop filter.
                if (mbx > 0)
                {
                    for (int i = -1; i < 16; ++i)
                    {
                        int srcIdx = (i * WebPConstants.Bps) + 12 + yOff;
                        int dstIdx = (i * WebPConstants.Bps) - 4 + yOff;
                        yuv.AsSpan(srcIdx, 4).CopyTo(yuv.AsSpan(dstIdx));
                    }

                    for (int i = -1; i < 8; ++i)
                    {
                        int srcIdx = (i * WebPConstants.Bps) + 4 + uOff;
                        int dstIdx = (i * WebPConstants.Bps) - 4 + uOff;
                        yuv.AsSpan(srcIdx, 4).CopyTo(yuv.AsSpan(dstIdx));
                        srcIdx = (i * WebPConstants.Bps) + 4 + vOff;
                        dstIdx = (i * WebPConstants.Bps) - 4 + vOff;
                        yuv.AsSpan(srcIdx, 4).CopyTo(yuv.AsSpan(dstIdx));
                    }
                }

                // Bring top samples into the cache.
                Vp8TopSamples topYuv = dec.YuvTopSamples[mbx];
                short[] coeffs = block.Coeffs;
                uint bits = block.NonZeroY;
                if (mby > 0)
                {
                    topYuv.Y.CopyTo(yuv.AsSpan(yOff - WebPConstants.Bps));
                    topYuv.U.CopyTo(yuv.AsSpan(uOff - WebPConstants.Bps));
                    topYuv.V.CopyTo(yuv.AsSpan(vOff - WebPConstants.Bps));
                }

                // Predict and add residuals.
                if (block.IsI4x4)
                {
                    // uint32_t* const top_right = (uint32_t*)(y_dst - BPS + 16);
                    if (mby > 0)
                    {
                        if (mbx >= dec.MbWidth - 1)
                        {
                            // On rightmost border.
                            // memset(top_right, top_yuv[0].y[15], sizeof(*top_right));
                        }
                        else
                        {
                            // memcpy(top_right, top_yuv[1].y, sizeof(*top_right));
                        }
                    }

                    // Replicate the top-right pixels below.
                    // top_right[BPS] = top_right[2 * BPS] = top_right[3 * BPS] = top_right[0];

                    // Predict and add residuals for all 4x4 blocks in turn.
                    for (int n = 0; n < 16; ++n, bits <<= 2)
                    {
                        // uint8_t * const dst = y_dst + kScan[n];
                        Span<byte> dst = yDst.Slice(WebPConstants.Scan[n]);
                        byte lumaMode = block.Modes[n];
                        switch (lumaMode)
                        {
                            case 0:
                                LossyUtils.DC4_C(dst);
                                break;
                            case 1:
                                LossyUtils.TM4_C(dst);
                                break;
                            case 2:
                                LossyUtils.VE4_C(dst);
                                break;
                            case 3:
                                LossyUtils.HE4_C(dst);
                                break;
                            case 4:
                                LossyUtils.RD4_C(dst);
                                break;
                            case 5:
                                LossyUtils.VR4_C(dst);
                                break;
                            case 6:
                                LossyUtils.LD4_C(dst);
                                break;
                            case 7:
                                LossyUtils.VL4_C(dst);
                                break;
                            case 8:
                                LossyUtils.HD4_C(dst);
                                break;
                            case 9:
                                LossyUtils.HU4_C(dst);
                                break;
                        }

                        this.DoTransform(bits,  coeffs.AsSpan(n * 16), dst);
                    }
                }
                else
                {
                    // 16x16
                    int mode = CheckMode(mbx, mby, block.Modes[0]);
                    switch (mode)
                    {
                        case 0:
                            LossyUtils.DC16_C(yDst, yuv, yOff);
                            break;
                        case 1:
                            LossyUtils.TM16_C(yDst);
                            break;
                        case 2:
                            LossyUtils.VE16_C(yDst, yuv, yOff);
                            break;
                        case 3:
                            LossyUtils.HE16_C(yDst, yuv, yOff);
                            break;
                        case 4:
                            LossyUtils.DC16NoTop_C(yDst, yuv, yOff);
                            break;
                        case 5:
                            LossyUtils.DC16NoLeft_C(yDst, yuv, yOff);
                            break;
                        case 6:
                            LossyUtils.DC16NoTopLeft_C(yDst);
                            break;
                    }

                    if (bits != 0)
                    {
                        for (int n = 0; n < 16; ++n, bits <<= 2)
                        {
                            this.DoTransform(bits, coeffs.AsSpan(n * 16), yDst.Slice(WebPConstants.Scan[n]));
                        }
                    }
                }

                // Chroma
                uint bitsUv = block.NonZeroUv;
                int chromaMode = CheckMode(mbx, mby, block.UvMode);
                switch (chromaMode)
                {
                    case 0:
                        LossyUtils.DC8uv_C(uDst, yuv, uOff);
                        LossyUtils.DC8uv_C(vDst, yuv, vOff);
                        break;
                    case 1:
                        LossyUtils.TM8uv_C(uDst);
                        LossyUtils.TM8uv_C(vDst);
                        break;
                    case 2:
                        LossyUtils.VE8uv_C(uDst, yuv.AsSpan(uOff - WebPConstants.Bps, 8));
                        LossyUtils.VE8uv_C(vDst, yuv.AsSpan(vOff - WebPConstants.Bps, 8));
                        break;
                    case 3:
                        LossyUtils.HE8uv_C(uDst, yuv, uOff);
                        LossyUtils.HE8uv_C(vDst, yuv, vOff);
                        break;
                    case 4:
                        LossyUtils.DC8uvNoTop_C(uDst, yuv, uOff);
                        LossyUtils.DC8uvNoTop_C(vDst, yuv, vOff);
                        break;
                    case 5:
                        LossyUtils.DC8uvNoLeft_C(uDst, yuv, uOff);
                        LossyUtils.DC8uvNoLeft_C(vDst, yuv, vOff);
                        break;
                    case 6:
                        LossyUtils.DC8uvNoTopLeft_C(uDst);
                        LossyUtils.DC8uvNoTopLeft_C(vDst);
                        break;
                }

                this.DoUVTransform(bitsUv >> 0, coeffs.AsSpan(16 * 16), uDst);
                this.DoUVTransform(bitsUv >> 8, coeffs.AsSpan(20 * 16), vDst);

                // Stash away top samples for next block.
                if (mby < dec.MbHeight - 1)
                {
                    yDst.Slice(15 * WebPConstants.Bps, 16).CopyTo(topYuv.Y);
                    uDst.Slice(7 * WebPConstants.Bps, 8).CopyTo(topYuv.U);
                    vDst.Slice(7 * WebPConstants.Bps, 8).CopyTo(topYuv.V);
                }

                // Transfer reconstructed samples from yuv_b_ cache to final destination.
                int cacheId = 0; // TODO: what should be cacheId, always 0?
                int yOffset = cacheId * 16 * dec.CacheYStride;
                int uvOffset = cacheId * 8 * dec.CacheUvStride;
                Span<byte> yOut = dec.CacheY.AsSpan((mbx * 16) + yOffset);
                Span<byte> uOut = dec.CacheU.AsSpan((mbx * 8) + uvOffset);
                Span<byte> vOut = dec.CacheV.AsSpan((mbx * 8) + uvOffset);
                for (int j = 0; j < 16; ++j)
                {
                    yDst.Slice(j * WebPConstants.Bps, 16).CopyTo(yOut.Slice(j * dec.CacheYStride));
                }

                for (int j = 0; j < 8; ++j)
                {
                    uDst.Slice(j * WebPConstants.Bps, 8).CopyTo(uOut.Slice(j * dec.CacheUvStride));
                    vDst.Slice(j * WebPConstants.Bps, 8).CopyTo(vOut.Slice(j * dec.CacheUvStride));
                }
            }
        }

        private void FinishRow(Vp8Decoder dec, Vp8Io io)
        {
            int cacheId = 0;
            int yBps = dec.CacheYStride;
            int extraYRows = WebPConstants.FilterExtraRows[(int)dec.Filter];
            int ySize = extraYRows * dec.CacheYStride;
            int uvSize = (extraYRows / 2) * dec.CacheUvStride;
            int yOffset = cacheId * 16 * dec.CacheYStride;
            int uvOffset = cacheId * 8 * dec.CacheUvStride;
            Span<byte> yDst = dec.CacheY.AsSpan();
            Span<byte> uDst = dec.CacheU.AsSpan();
            Span<byte> vDst = dec.CacheV.AsSpan();
            int mby = dec.MbY;
            bool isFirstRow = mby is 0;
            bool isLastRow = mby >= dec.BottomRightMbY - 1;

            // TODO: Filter row
            //FilterRow(dec);

            int yStart = mby * 16;
            int yEnd = (mby + 1) * 16;
            if (!isFirstRow)
            {
                yStart -= extraYRows;
                io.Y = yDst;
                io.U = uDst;
                io.V = vDst;
            }
            else
            {
                io.Y = dec.CacheY.AsSpan(yOffset);
                io.U = dec.CacheU.AsSpan(uvOffset);
                io.V = dec.CacheV.AsSpan(uvOffset);
            }

            if (!isLastRow)
            {
                yEnd -= extraYRows;
            }

            if (yStart < yEnd)
            {
                io.Y = io.Y.Slice(io.CropLeft);
                io.U = io.U.Slice(io.CropLeft);
                io.V = io.V.Slice(io.CropLeft);

                io.MbY = yStart - io.CropTop;
                io.MbW = io.CropRight - io.CropLeft;
                io.MbH = yEnd - yStart;
                this.EmitRgb(dec, io);
            }

            // Rotate top samples if needed.
            if (!isLastRow)
            {
                // TODO: double check this.
                yDst.Slice(16 * dec.CacheYStride, ySize).CopyTo(dec.CacheY);
                uDst.Slice(8 * dec.CacheUvStride, uvSize).CopyTo(dec.CacheU);
                vDst.Slice(8 * dec.CacheUvStride, uvSize).CopyTo(dec.CacheV);
            }
        }

        private int EmitRgb(Vp8Decoder dec, Vp8Io io)
        {
            byte[] buf = dec.Bgr;
            int numLinesOut = io.MbH; // a priori guess.
            Span<byte> curY = io.Y;
            Span<byte> curU = io.U;
            Span<byte> curV = io.V;
            byte[] tmpYBuffer = dec.TmpYBuffer;
            byte[] tmpUBuffer = dec.TmpUBuffer;
            byte[] tmpVBuffer = dec.TmpVBuffer;
            Span<byte> topU = tmpUBuffer.AsSpan();
            Span<byte> topV = tmpVBuffer.AsSpan();
            int bpp = 3;
            int bufferStride = bpp * io.Width;
            int dstStartIdx = io.MbY * bufferStride;
            Span<byte> dst = buf.AsSpan(dstStartIdx);
            int yEnd = io.MbY + io.MbH;
            int mbw = io.MbW;
            int uvw = (mbw + 1) / 2;
            int y = io.MbY;

            if (y is 0)
            {
                // First line is special cased. We mirror the u/v samples at boundary.
                this.UpSample(curY, null, curU, curV, curU, curV, dst, null, mbw);
            }
            else
            {
                // We can finish the left-over line from previous call.
                this.UpSample(tmpYBuffer.AsSpan(), curY, topU, topV, curU, curV, buf.AsSpan(dstStartIdx - bufferStride), dst, mbw);
                numLinesOut++;
            }

            // Loop over each output pairs of row.
            for (; y + 2 < yEnd; y += 2)
            {
                topU = curU;
                topV = curV;
                curU = curU.Slice(io.UvStride);
                curV = curV.Slice(io.UvStride);
                this.UpSample(curY.Slice(io.YStride), curY, topU, topV, curU, curV, dst.Slice(bufferStride), dst.Slice(2 * bufferStride), mbw);
                curY = curY.Slice(2 * io.YStride);
                dst = dst.Slice(2 * bufferStride);
            }

            // Move to last row.
            curY = curY.Slice(io.YStride);
            if (io.CropTop + yEnd < io.CropBottom)
            {
                // Save the unfinished samples for next call (as we're not done yet).
                curY.Slice(0, mbw).CopyTo(tmpYBuffer);
                curU.Slice(0, uvw).CopyTo(tmpUBuffer);
                curV.Slice(0, uvw).CopyTo(tmpVBuffer);

                // The upsampler leaves a row unfinished behind (except for the very last row).
                numLinesOut--;
            }
            else
            {
                // Process the very last row of even-sized picture.
                if ((yEnd & 1) is 0)
                {
                    this.UpSample(curY, null, curU, curV, curU, curV, dst.Slice(bufferStride), null, mbw);
                }
            }

            return numLinesOut;
        }

        private void UpSample(Span<byte> topY, Span<byte> bottomY, Span<byte> topU, Span<byte> topV, Span<byte> curU, Span<byte> curV, Span<byte> topDst, Span<byte> bottomDst, int len)
        {
            int xStep = 3;
            int lastPixelPair = (len - 1) >> 1;
            uint tluv = LossyUtils.LoadUv(topU[0], topV[0]); // top-left sample
            uint luv = LossyUtils.LoadUv(curU[0], curV[0]); // left-sample
            uint uv0 = ((3 * tluv) + luv + 0x00020002u) >> 2;
            LossyUtils.YuvToBgr(topY[0], (int)(uv0 & 0xff), (int)(uv0 >> 16), topDst);

            if (bottomY != null)
            {
                uv0 = ((3 * luv) + tluv + 0x00020002u) >> 2;
                LossyUtils.YuvToBgr(bottomY[0], (int)uv0 & 0xff, (int)(uv0 >> 16), bottomDst);
            }

            for (int x = 1; x <= lastPixelPair; ++x)
            {
                uint tuv = LossyUtils.LoadUv(topU[x], topV[x]); // top sample
                uint uv = LossyUtils.LoadUv(curU[x], curV[x]); // sample

                // Precompute invariant values associated with first and second diagonals.
                uint avg = tluv + tuv + luv + uv + 0x00080008u;
                uint diag12 = (avg + (2 * (tuv + luv))) >> 3;
                uint diag03 = (avg + (2 * (tluv + uv))) >> 3;
                uv0 = (diag12 + tluv) >> 1;
                uint uv1 = (diag03 + tuv) >> 1;
                LossyUtils.YuvToBgr(topY[(2 * x) - 1], (int)(uv0 & 0xff), (int)(uv0 >> 16), topDst.Slice(((2 * x) - 1) * xStep));
                LossyUtils.YuvToBgr(topY[(2 * x) - 0], (int)(uv1 & 0xff), (int)(uv1 >> 16), topDst.Slice(((2 * x) - 0) * xStep));

                if (bottomY != null)
                {
                    uv0 = (diag03 + luv) >> 1;
                    uv1 = (diag12 + uv) >> 1;
                    LossyUtils.YuvToBgr(bottomY[(2 * x) - 1], (int)(uv0 & 0xff), (int)(uv0 >> 16), bottomDst.Slice(((2 * x) - 1) * xStep));
                    LossyUtils.YuvToBgr(bottomY[(2 * x) + 0], (int)(uv1 & 0xff), (int)(uv1 >> 16), bottomDst.Slice(((2 * x) + 0) * xStep));
                }

                tluv = tuv;
                luv = uv;
            }

            /*if ((len & 1) is 0)
            {
                uv0 = ((3 * tluv) + luv + 0x00020002u) >> 2;
                LossyUtils.YuvToBgr(topY[len - 1], (int)(uv0 & 0xff), (int)(uv0 >> 16), topDst.Slice((len - 1) * xStep));
                if (bottomY != null)
                {
                    uv0 = ((3 * luv) + tluv + 0x00020002u) >> 2;
                    LossyUtils.YuvToBgr(bottomY[len - 1], (int)(uv0 & 0xff), (int)(uv0 >> 16), bottomDst.Slice((len - 1) * xStep));
                }
            }*/
        }

        private void DoTransform(uint bits, Span<short> src, Span<byte> dst)
        {
            switch (bits >> 30)
            {
                case 3:
                    LossyUtils.Transform(src, dst, false);
                    break;
                case 2:
                    LossyUtils.TransformAc3(src, dst);
                    break;
                case 1:
                    LossyUtils.TransformDc(src, dst);
                    break;
                default:
                    break;
            }
        }

        private void DoUVTransform(uint bits, Span<short> src, Span<byte> dst)
        {
            // any non-zero coeff at all?
            if ((bits & 0xff) > 0)
            {
                // any non-zero AC coefficient?
                if ((bits & 0xaa) > 0)
                {
                    LossyUtils.TransformUv(src, dst); // note we don't use the AC3 variant for U/V.
                }
                else
                {
                    LossyUtils.TransformDcuv(src, dst);
                }
            }
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

        private void DecodeMacroBlock(Vp8Decoder dec, Vp8BitReader bitreader)
        {
            Vp8MacroBlock left = dec.LeftMacroBlock;
            Vp8MacroBlock macroBlock = dec.CurrentMacroBlock;
            Vp8MacroBlockData blockData = dec.CurrentBlockData;
            int skip = dec.UseSkipProbability ? blockData.Skip : 0;

            if (skip is 0)
            {
                this.ParseResiduals(dec, bitreader, macroBlock);
            }
            else
            {
                left.NoneZeroAcDcCoeffs = macroBlock.NoneZeroAcDcCoeffs = 0;
                if (blockData.IsI4x4)
                {
                    left.NoneZeroDcCoeffs = macroBlock.NoneZeroDcCoeffs = 0;
                }

                blockData.NonZeroY = 0;
                blockData.NonZeroUv = 0;
                blockData.Dither = 0;
            }

            // Store filter info.
            if (dec.Filter != LoopFilter.None)
            {
                dec.FilterInfo[dec.MbX] = dec.FilterStrength[blockData.Segment, blockData.IsI4x4 ? 1 : 0];
                dec.FilterInfo[dec.MbX].InnerFiltering |= (byte)(skip is 0 ? 1 : 0);
            }
        }

        private bool ParseResiduals(Vp8Decoder dec, Vp8BitReader br, Vp8MacroBlock mb)
        {
            uint nonZeroY = 0;
            uint nonZeroUv = 0;
            int first;
            int dstOffset = 0;
            Vp8MacroBlockData block = dec.CurrentBlockData;
            Vp8QuantMatrix q = dec.DeQuantMatrices[block.Segment];
            Vp8BandProbas[,] bands = dec.Probabilities.BandsPtr;
            Vp8BandProbas[] acProba;
            Vp8MacroBlock leftMb = dec.LeftMacroBlock;
            short[] dst = block.Coeffs;

            if (!block.IsI4x4)
            {
                // Parse DC
                var dc = new short[16];
                int ctx = (int)(mb.NoneZeroDcCoeffs + leftMb.NoneZeroDcCoeffs);
                int nz = this.GetCoeffs(br, GetBandsRow(bands, 1), ctx, q.Y2Mat, 0, dc);
                mb.NoneZeroDcCoeffs = leftMb.NoneZeroDcCoeffs = (uint)(nz > 0 ? 1 : 0);
                if (nz > 1)
                {
                    // More than just the DC -> perform the full transform.
                    this.TransformWht(dc, dst);
                }
                else
                {
                    int dc0 = (dc[0] + 3) >> 3;
                    for (int i = 0; i < 16 * 16; i += 16)
                    {
                        dst[i] = (short)dc0;
                    }
                }

                first = 1;
                acProba = GetBandsRow(bands, 0);
            }
            else
            {
                first = 0;
                acProba = GetBandsRow(bands, 3);
            }

            byte tnz = (byte)(mb.NoneZeroAcDcCoeffs & 0x0f);
            byte lnz = (byte)(leftMb.NoneZeroAcDcCoeffs & 0x0f);

            for (int y = 0; y < 4; ++y)
            {
                int l = lnz & 1;
                uint nzCoeffs = 0;
                for (int x = 0; x < 4; ++x)
                {
                    int ctx = l + (tnz & 1);
                    int nz = this.GetCoeffs(br, acProba, ctx, q.Y1Mat, first, dst.AsSpan(dstOffset));
                    l = (nz > first) ? 1 : 0;
                    tnz = (byte)((tnz >> 1) | (l << 7));
                    nzCoeffs = NzCodeBits(nzCoeffs, nz, dst[dstOffset] != 0 ? 1 : 0);
                    dstOffset += 16;
                }

                tnz >>= 4;
                lnz = (byte)((lnz >> 1) | (l << 7));
                nonZeroY = (nonZeroY << 8) | nzCoeffs;
            }

            uint outTnz = tnz;
            uint outLnz = (uint)(lnz >> 4);

            for (int ch = 0; ch < 4; ch += 2)
            {
                uint nzCoeffs = 0;
                tnz = (byte)(mb.NoneZeroAcDcCoeffs >> (4 + ch));
                lnz = (byte)(leftMb.NoneZeroAcDcCoeffs >> (4 + ch));
                for (int y = 0; y < 2; ++y)
                {
                    int l = lnz & 1;
                    for (int x = 0; x < 2; ++x)
                    {
                        int ctx = l + (tnz & 1);
                        int nz = this.GetCoeffs(br, GetBandsRow(bands, 2), ctx, q.UvMat, 0, dst.AsSpan(dstOffset));
                        l = (nz > 0) ? 1 : 0;
                        tnz = (byte)((tnz >> 1) | (l << 3));
                        nzCoeffs = NzCodeBits(nzCoeffs, nz, dst[dstOffset] != 0 ? 1 : 0);
                        dstOffset += 16;
                    }

                    tnz >>= 2;
                    lnz = (byte)((lnz >> 1) | (l << 5));
                }

                // Note: we don't really need the per-4x4 details for U/V blocks.
                nonZeroUv |= nzCoeffs << (4 * ch);
                outTnz |= (uint)((tnz << 4) << ch);
                outLnz |= (uint)((lnz & 0xf0) << ch);
            }

            mb.NoneZeroAcDcCoeffs = outTnz;
            leftMb.NoneZeroAcDcCoeffs = outLnz;

            block.NonZeroY = nonZeroY;
            block.NonZeroUv = nonZeroUv;

            // We look at the mode-code of each block and check if some blocks have less
            // than three non-zero coeffs (code < 2). This is to avoid dithering flat and
            // empty blocks.
            block.Dither = (byte)((nonZeroUv & 0xaaaa) > 0 ? 0 : q.Dither);

            return (nonZeroY | nonZeroUv) is 0;
        }

        private int GetCoeffs(Vp8BitReader br, Vp8BandProbas[] prob, int ctx, int[] dq, int n, Span<short> coeffs)
        {
            // Returns the position of the last non - zero coeff plus one.
            Vp8ProbaArray p = prob[n].Probabilities[ctx];
            for (; n < 16; ++n)
            {
                if (br.GetBit(p.Probabilities[0]) is 0)
                {
                    // Previous coeff was last non - zero coeff.
                    return n;
                }

                // Sequence of zero coeffs.
                while (br.GetBit(p.Probabilities[1]) is 0)
                {
                    p = prob[++n].Probabilities[0];
                    if (n is 16)
                    {
                        return 16;
                    }
                }

                // Non zero coeffs.
                int v;
                if (br.GetBit(p.Probabilities[2]) is 0)
                {
                    v = 1;
                    p = prob[n + 1].Probabilities[1];
                }
                else
                {
                    v = this.GetLargeValue(br, p.Probabilities);
                    p = prob[n + 1].Probabilities[2];
                }

                int idx = n > 0 ? 1 : 0;
                coeffs[WebPConstants.Zigzag[n]] = (short)(br.GetSigned(v) * dq[idx]);
            }

            return 16;
        }

        private int GetLargeValue(Vp8BitReader br, byte[] p)
        {
            // See section 13 - 2: http://tools.ietf.org/html/rfc6386#section-13.2
            int v;
            if (br.GetBit(p[3]) is 0)
            {
                if (br.GetBit(p[4]) is 0)
                {
                    v = 2;
                }
                else
                {
                    v = 3 + br.GetBit(p[5]);
                }
            }
            else
            {
                if (br.GetBit(p[6]) is 0)
                {
                    if (br.GetBit(p[7]) is 0)
                    {
                        v = 5 + br.GetBit(159);
                    }
                    else
                    {
                        v = 7 + (2 * br.GetBit(165));
                        v += br.GetBit(145);
                    }
                }
                else
                {
                    int bit1 = br.GetBit(p[8]);
                    int bit0 = br.GetBit(p[9] + bit1);
                    int cat = (2 * bit1) + bit0;
                    v = 0;
                    byte[] tab = null;
                    switch (cat)
                    {
                        case 0:
                            tab = WebPConstants.Cat3;
                            break;
                        case 1:
                            tab = WebPConstants.Cat4;
                            break;
                        case 2:
                            tab = WebPConstants.Cat5;
                            break;
                        case 3:
                            tab = WebPConstants.Cat6;
                            break;
                        default:
                            WebPThrowHelper.ThrowImageFormatException("VP8 parsing error");
                            break;
                    }

                    for (int i = 0; i < tab.Length; i++)
                    {
                        v += v + br.GetBit(tab[i]);
                    }

                    v += 3 + (8 << cat);
                }
            }

            return v;
        }

        /// <summary>
        /// Paragraph 14.3: Implementation of the Walsh-Hadamard transform inversion.
        /// </summary>
        private void TransformWht(short[] input, short[] output)
        {
            var tmp = new int[16];
            for (int i = 0; i < 4; ++i)
            {
                int a0 = input[0 + i] + input[12 + i];
                int a1 = input[4 + i] + input[8 + i];
                int a2 = input[4 + i] - input[8 + i];
                int a3 = input[0 + i] - input[12 + i];
                tmp[0 + i] = a0 + a1;
                tmp[8 + i] = a0 - a1;
                tmp[4 + i] = a3 + a2;
                tmp[12 + i] = a3 - a2;
            }

            int outputOffset = 0;
            for (int i = 0; i < 4; ++i)
            {
                int dc = tmp[0 + (i * 4)] + 3;
                int a0 = dc + tmp[3 + (i * 4)];
                int a1 = tmp[1 + (i * 4)] + tmp[2 + (i * 4)];
                int a2 = tmp[1 + (i * 4)] - tmp[2 + (i * 4)];
                int a3 = dc - tmp[3 + (i * 4)];
                output[outputOffset + 0] = (short)((a0 + a1) >> 3);
                output[outputOffset + 16] = (short)((a3 + a2) >> 3);
                output[outputOffset + 32] = (short)((a0 - a1) >> 3);
                output[outputOffset + 48] = (short)((a3 - a2) >> 3);
                outputOffset += 64;
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
                            hasValue = this.bitReader.ReadBool();
                            proba.Segments[s] = hasValue ? this.bitReader.ReadValue(8) : 255;
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

        private Vp8FilterHeader ParseFilterHeader(Vp8Decoder dec)
        {
            Vp8FilterHeader vp8FilterHeader = dec.FilterHeader;
            vp8FilterHeader.LoopFilter = this.bitReader.ReadBool() ? LoopFilter.Simple : LoopFilter.Complex;
            vp8FilterHeader.Level = (int)this.bitReader.ReadValue(6);
            vp8FilterHeader.Sharpness = (int)this.bitReader.ReadValue(3);
            vp8FilterHeader.UseLfDelta = this.bitReader.ReadBool();

            dec.Filter = (vp8FilterHeader.Level is 0) ? LoopFilter.None : vp8FilterHeader.LoopFilter;
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

        private void ParsePartitions(Vp8Decoder dec)
        {
            uint size = this.bitReader.Remaining - this.bitReader.PartitionLength;
            int startIdx = (int)this.bitReader.PartitionLength;
            Span<byte> sz = this.bitReader.Data.AsSpan(startIdx);
            int sizeLeft = (int)size;
            int numPartsMinusOne = (1 << (int)this.bitReader.ReadValue(2)) - 1;
            int lastPart = numPartsMinusOne;

            int partStart = startIdx + (lastPart * 3);
            sizeLeft -= lastPart * 3;
            for (int p = 0; p < lastPart; ++p)
            {
                int pSize = sz[0] | (sz[1] << 8) | (sz[2] << 16);
                if (pSize > sizeLeft)
                {
                    pSize = sizeLeft;
                }

                dec.Vp8BitReaders[p] = new Vp8BitReader(this.bitReader.Data, (uint)pSize, partStart);
                partStart += pSize;
                sizeLeft -= pSize;
                sz = sz.Slice(3);
            }

            dec.Vp8BitReaders[lastPart] = new Vp8BitReader(this.bitReader.Data, (uint)sizeLeft, partStart);
        }

        private void ParseDequantizationIndices(Vp8Decoder decoder)
        {
            Vp8SegmentHeader vp8SegmentHeader = decoder.SegmentHeader;

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
                        decoder.DeQuantMatrices[i] = decoder.DeQuantMatrices[0];
                        continue;
                    }
                    else
                    {
                        q = baseQ0;
                    }
                }

                Vp8QuantMatrix m = decoder.DeQuantMatrices[i];
                m.Y1Mat[0] = WebPConstants.DcTable[Clip(q + dqy1Dc, 127)];
                m.Y1Mat[1] = WebPConstants.AcTable[Clip(q + 0, 127)];
                m.Y2Mat[0] = WebPConstants.DcTable[Clip(q + dqy2Dc, 127)] * 2;

                // For all x in [0..284], x*155/100 is bitwise equal to (x*101581) >> 16.
                // The smallest precision for that is '(x*6349) >> 12' but 16 is a good word size.
                m.Y2Mat[1] = (WebPConstants.AcTable[Clip(q + dqy2Ac, 127)] * 101581) >> 16;
                if (m.Y2Mat[1] < 8)
                {
                    m.Y2Mat[1] = 8;
                }

                m.UvMat[0] = WebPConstants.DcTable[Clip(q + dquvDc, 117)];
                m.UvMat[1] = WebPConstants.AcTable[Clip(q + dquvAc, 127)];

                // For dithering strength evaluation.
                m.UvQuant = q + dquvAc;
            }
        }

        private void ParseProbabilities(Vp8Decoder dec)
        {
            Vp8Proba proba = dec.Probabilities;

            for (int t = 0; t < WebPConstants.NumTypes; ++t)
            {
                for (int b = 0; b < WebPConstants.NumBands; ++b)
                {
                    for (int c = 0; c < WebPConstants.NumCtx; ++c)
                    {
                        for (int p = 0; p < WebPConstants.NumProbas; ++p)
                        {
                            byte prob = WebPConstants.CoeffsUpdateProba[t, b, c, p];
                            int v = this.bitReader.GetBit(prob) != 0
                                        ? (int)this.bitReader.ReadValue(8)
                                        : WebPConstants.DefaultCoeffsProba[t, b, c, p];
                            proba.Bands[t, b].Probabilities[c].Probabilities[p] = (byte)v;
                        }
                    }
                }

                for (int b = 0; b < 16 + 1; ++b)
                {
                    proba.BandsPtr[t, b] = proba.Bands[t, WebPConstants.Bands[b]];
                }
            }

            dec.UseSkipProbability = this.bitReader.ReadBool();
            if (dec.UseSkipProbability)
            {
                dec.SkipProbability = (byte)this.bitReader.ReadValue(8);
            }
        }

        private static Vp8Io InitializeVp8Io(Vp8PictureHeader pictureHeader)
        {
            var io = default(Vp8Io);
            io.Width = (int)pictureHeader.Width;
            io.Height = (int)pictureHeader.Height;
            io.UseCropping = false;
            io.CropTop = 0;
            io.CropLeft = 0;
            io.CropRight = io.Width;
            io.CropBottom = io.Height;
            io.UseScaling = false;
            io.ScaledWidth = io.Width;
            io.ScaledHeight = io.ScaledHeight;
            io.MbW = io.Width;
            io.MbH = io.Height;
            return io;
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

        private static uint NzCodeBits(uint nzCoeffs, int nz, int dcNz)
        {
            nzCoeffs <<= 2;
            nzCoeffs |= (uint)((nz > 3) ? 3 : (nz > 1) ? 2 : dcNz);
            return nzCoeffs;
        }

        private static Vp8BandProbas[] GetBandsRow(Vp8BandProbas[,] bands, int rowIdx)
        {
            Vp8BandProbas[] bandsRow = Enumerable.Range(0, bands.GetLength(1)).Select(x => bands[rowIdx, x]).ToArray();
            return bandsRow;
        }

        private static int CheckMode(int mbx, int mby, int mode)
        {
            // B_DC_PRED
            if (mode is 0)
            {
                if (mbx is 0)
                {
                    return (mby is 0)
                               ? 6 // B_DC_PRED_NOTOPLEFT
                               : 5; // B_DC_PRED_NOLEFT
                }

                return (mby is 0)
                           ? 4 // B_DC_PRED_NOTOP
                           : 0; // B_DC_PRED
            }

            return mode;
        }

        private static int Clip(int value, int max)
        {
            return value < 0 ? 0 : value > max ? max : value;
        }

        private void InitializeModesProbabilities()
        {
            // Paragraph 11.5
            this.bModesProba[0, 0] = new byte[] { 231, 120, 48, 89, 115, 113, 120, 152, 112 };
            this.bModesProba[0, 1] = new byte[] { 152, 179, 64, 126, 170, 118, 46, 70, 95 };
            this.bModesProba[0, 2] = new byte[] { 175, 69, 143, 80, 85, 82, 72, 155, 103 };
            this.bModesProba[0, 3] = new byte[] { 56, 58, 10, 171, 218, 189, 17, 13, 152 };
            this.bModesProba[0, 4] = new byte[] { 114, 26, 17, 163, 44, 195, 21, 10, 173 };
            this.bModesProba[0, 5] = new byte[] { 121, 24, 80, 195, 26, 62, 44, 64, 85 };
            this.bModesProba[0, 6] = new byte[] { 144, 71, 10, 38, 171, 213, 144, 34, 26 };
            this.bModesProba[0, 7] = new byte[] { 170, 46, 55, 19, 136, 160, 33, 206, 71 };
            this.bModesProba[0, 8] = new byte[] { 63, 20, 8, 114, 114, 208, 12, 9, 226 };
            this.bModesProba[0, 9] = new byte[] { 81, 40, 11, 96, 182, 84, 29, 16, 36 };
            this.bModesProba[1, 0] = new byte[] { 134, 183, 89, 137, 98, 101, 106, 165, 148 };
            this.bModesProba[1, 1] = new byte[] { 72, 187, 100, 130, 157, 111, 32, 75, 80 };
            this.bModesProba[1, 2] = new byte[] { 66, 102, 167, 99, 74, 62, 40, 234, 128 };
            this.bModesProba[1, 3] = new byte[] { 41, 53, 9, 178, 241, 141, 26, 8, 107 };
            this.bModesProba[1, 4] = new byte[] { 74, 43, 26, 146, 73, 166, 49, 23, 157 };
            this.bModesProba[1, 5] = new byte[] { 65, 38, 105, 160, 51, 52, 31, 115, 128 };
            this.bModesProba[1, 6] = new byte[] { 104, 79, 12, 27, 217, 255, 87, 17, 7 };
            this.bModesProba[1, 7] = new byte[] { 87, 68, 71, 44, 114, 51, 15, 186, 23 };
            this.bModesProba[1, 8] = new byte[] { 47, 41, 14, 110, 182, 183, 21, 17, 194 };
            this.bModesProba[1, 9] = new byte[] { 66, 45, 25, 102, 197, 189, 23, 18, 22 };
            this.bModesProba[2, 0] = new byte[] { 88, 88, 147, 150, 42, 46, 45, 196, 205 };
            this.bModesProba[2, 1] = new byte[] { 43, 97, 183, 117, 85, 38, 35, 179, 61 };
            this.bModesProba[2, 2] = new byte[] { 39, 53, 200, 87, 26, 21, 43, 232, 171 };
            this.bModesProba[2, 3] = new byte[] { 56, 34, 51, 104, 114, 102, 29, 93, 77 };
            this.bModesProba[2, 4] = new byte[] { 39, 28, 85, 171, 58, 165, 90, 98, 64 };
            this.bModesProba[2, 5] = new byte[] { 34, 22, 116, 206, 23, 34, 43, 166, 73 };
            this.bModesProba[2, 6] = new byte[] { 107, 54, 32, 26, 51, 1, 81, 43, 31 };
            this.bModesProba[2, 7] = new byte[] { 68, 25, 106, 22, 64, 171, 36, 225, 114 };
            this.bModesProba[2, 8] = new byte[] { 34, 19, 21, 102, 132, 188, 16, 76, 124 };
            this.bModesProba[2, 9] = new byte[] { 62, 18, 78, 95, 85, 57, 50, 48, 51 };
            this.bModesProba[3, 0] = new byte[] { 193, 101, 35, 159, 215, 111, 89, 46, 111 };
            this.bModesProba[3, 1] = new byte[] { 60, 148, 31, 172, 219, 228, 21, 18, 111 };
            this.bModesProba[3, 2] = new byte[] { 112, 113, 77, 85, 179, 255, 38, 120, 114 };
            this.bModesProba[3, 3] = new byte[] { 40, 42, 1, 196, 245, 209, 10, 25, 109 };
            this.bModesProba[3, 4] = new byte[] { 88, 43, 29, 140, 166, 213, 37, 43, 154 };
            this.bModesProba[3, 5] = new byte[] { 61, 63, 30, 155, 67, 45, 68, 1, 209 };
            this.bModesProba[3, 6] = new byte[] { 100, 80, 8, 43, 154, 1, 51, 26, 71 };
            this.bModesProba[3, 7] = new byte[] { 142, 78, 78, 16, 255, 128, 34, 197, 171 };
            this.bModesProba[3, 8] = new byte[] { 41, 40, 5, 102, 211, 183, 4, 1, 221 };
            this.bModesProba[3, 9] = new byte[] { 51, 50, 17, 168, 209, 192, 23, 25, 82 };
            this.bModesProba[4, 0] = new byte[] { 138, 31, 36, 171, 27, 166, 38, 44, 229 };
            this.bModesProba[4, 1] = new byte[] { 67, 87, 58, 169, 82, 115, 26, 59, 179 };
            this.bModesProba[4, 2] = new byte[] { 63, 59, 90, 180, 59, 166, 93, 73, 154 };
            this.bModesProba[4, 3] = new byte[] { 40, 40, 21, 116, 143, 209, 34, 39, 175 };
            this.bModesProba[4, 4] = new byte[] { 47, 15, 16, 183, 34, 223, 49, 45, 183 };
            this.bModesProba[4, 5] = new byte[] { 46, 17, 33, 183, 6, 98, 15, 32, 183 };
            this.bModesProba[4, 6] = new byte[] { 57, 46, 22, 24, 128, 1, 54, 17, 37 };
            this.bModesProba[4, 7] = new byte[] { 65, 32, 73, 115, 28, 128, 23, 128, 205 };
            this.bModesProba[4, 8] = new byte[] { 40, 3, 9, 115, 51, 192, 18, 6, 223 };
            this.bModesProba[4, 9] = new byte[] { 87, 37, 9, 115, 59, 77, 64, 21, 47 };
            this.bModesProba[5, 0] = new byte[] { 104, 55, 44, 218, 9, 54, 53, 130, 226 };
            this.bModesProba[5, 1] = new byte[] { 64, 90, 70, 205, 40, 41, 23, 26, 57 };
            this.bModesProba[5, 2] = new byte[] { 54, 57, 112, 184, 5, 41, 38, 166, 213 };
            this.bModesProba[5, 3] = new byte[] { 30, 34, 26, 133, 152, 116, 10, 32, 134 };
            this.bModesProba[5, 4] = new byte[] { 39, 19, 53, 221, 26, 114, 32, 73, 255 };
            this.bModesProba[5, 5] = new byte[] { 31, 9, 65, 234, 2, 15, 1, 118, 73 };
            this.bModesProba[5, 6] = new byte[] { 75, 32, 12, 51, 192, 255, 160, 43, 51 };
            this.bModesProba[5, 7] = new byte[] { 88, 31, 35, 67, 102, 85, 55, 186, 85 };
            this.bModesProba[5, 8] = new byte[] { 56, 21, 23, 111, 59, 205, 45, 37, 192 };
            this.bModesProba[5, 9] = new byte[] { 55, 38, 70, 124, 73, 102, 1, 34, 98 };
            this.bModesProba[6, 0] = new byte[] { 125, 98, 42, 88, 104, 85, 117, 175, 82 };
            this.bModesProba[6, 1] = new byte[] { 95, 84, 53, 89, 128, 100, 113, 101, 45 };
            this.bModesProba[6, 2] = new byte[] { 75, 79, 123, 47, 51, 128, 81, 171, 1 };
            this.bModesProba[6, 3] = new byte[] { 57, 17, 5, 71, 102, 57, 53, 41, 49 };
            this.bModesProba[6, 4] = new byte[] { 38, 33, 13, 121, 57, 73, 26, 1, 85 };
            this.bModesProba[6, 5] = new byte[] { 41, 10, 67, 138, 77, 110, 90, 47, 114 };
            this.bModesProba[6, 6] = new byte[] { 115, 21, 2, 10, 102, 255, 166, 23, 6 };
            this.bModesProba[6, 7] = new byte[] { 101, 29, 16, 10, 85, 128, 101, 196, 26 };
            this.bModesProba[6, 8] = new byte[] { 57, 18, 10, 102, 102, 213, 34, 20, 43 };
            this.bModesProba[6, 9] = new byte[] { 117, 20, 15, 36, 163, 128, 68, 1, 26 };
            this.bModesProba[7, 0] = new byte[] { 102, 61, 71, 37, 34, 53, 31, 243, 192 };
            this.bModesProba[7, 1] = new byte[] { 69, 60, 71, 38, 73, 119, 28, 222, 37 };
            this.bModesProba[7, 2] = new byte[] { 68, 45, 128, 34, 1, 47, 11, 245, 171 };
            this.bModesProba[7, 3] = new byte[] { 62, 17, 19, 70, 146, 85, 55, 62, 70 };
            this.bModesProba[7, 4] = new byte[] { 37, 43, 37, 154, 100, 163, 85, 160, 1 };
            this.bModesProba[7, 5] = new byte[] { 63, 9, 92, 136, 28, 64, 32, 201, 85 };
            this.bModesProba[7, 6] = new byte[] { 75, 15, 9, 9, 64, 255, 184, 119, 16 };
            this.bModesProba[7, 7] = new byte[] { 86, 6, 28, 5, 64, 255, 25, 248, 1 };
            this.bModesProba[7, 8] = new byte[] { 56, 8, 17, 132, 137, 255, 55, 116, 128 };
            this.bModesProba[7, 9] = new byte[] { 58, 15, 20, 82, 135, 57, 26, 121, 40 };
            this.bModesProba[8, 0] = new byte[] { 164, 50, 31, 137, 154, 133, 25, 35, 218 };
            this.bModesProba[8, 1] = new byte[] { 51, 103, 44, 131, 131, 123, 31, 6, 158 };
            this.bModesProba[8, 2] = new byte[] { 86, 40, 64, 135, 148, 224, 45, 183, 128 };
            this.bModesProba[8, 3] = new byte[] { 22, 26, 17, 131, 240, 154, 14, 1, 209 };
            this.bModesProba[8, 4] = new byte[] { 45, 16, 21, 91, 64, 222, 7, 1, 197 };
            this.bModesProba[8, 5] = new byte[] { 56, 21, 39, 155, 60, 138, 23, 102, 213 };
            this.bModesProba[8, 6] = new byte[] { 83, 12, 13, 54, 192, 255, 68, 47, 28 };
            this.bModesProba[8, 7] = new byte[] { 85, 26, 85, 85, 128, 128, 32, 146, 171 };
            this.bModesProba[8, 8] = new byte[] { 18, 11, 7, 63, 144, 171, 4, 4, 246 };
            this.bModesProba[8, 9] = new byte[] { 35, 27, 10, 146, 174, 171, 12, 26, 128 };
            this.bModesProba[9, 0] = new byte[] { 190, 80, 35, 99, 180, 80, 126, 54, 45 };
            this.bModesProba[9, 1] = new byte[] { 85, 126, 47, 87, 176, 51, 41, 20, 32 };
            this.bModesProba[9, 2] = new byte[] { 101, 75, 128, 139, 118, 146, 116, 128, 85 };
            this.bModesProba[9, 3] = new byte[] { 56, 41, 15, 176, 236, 85, 37, 9, 62 };
            this.bModesProba[9, 4] = new byte[] { 71, 30, 17, 119, 118, 255, 17, 18, 138 };
            this.bModesProba[9, 5] = new byte[] { 101, 38, 60, 138, 55, 70, 43, 26, 142 };
            this.bModesProba[9, 6] = new byte[] { 146, 36, 19, 30, 171, 255, 97, 27, 20 };
            this.bModesProba[9, 7] = new byte[] { 138, 45, 61, 62, 219, 1, 81, 188, 64 };
            this.bModesProba[9, 8] = new byte[] { 32, 41, 20, 117, 151, 142, 20, 21, 163 };
            this.bModesProba[9, 9] = new byte[] { 112, 19, 12, 61, 195, 128, 48, 4, 24 };
        }

    }

    struct YUVPixel
    {
        public byte Y { get; }

        public byte U { get; }

        public byte V { get; }
    }
}
