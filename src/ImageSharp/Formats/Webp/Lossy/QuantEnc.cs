// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Webp.Lossy
{
    /// <summary>
    /// Quantization methods.
    /// </summary>
    internal static class QuantEnc
    {
        private static readonly byte[] Zigzag = { 0, 1, 4, 8, 5, 2, 3, 6, 9, 12, 13, 10, 7, 11, 14, 15 };

        private static readonly ushort[] WeightY = { 38, 32, 20, 9, 32, 28, 17, 7, 20, 17, 10, 4, 9, 7, 4, 2 };

        private const int MaxLevel = 2047;

        // Diffusion weights. We under-correct a bit (15/16th of the error is actually
        // diffused) to avoid 'rainbow' chessboard pattern of blocks at q~=0.
        private const int C1 = 7;    // fraction of error sent to the 4x4 block below
        private const int C2 = 8;    // fraction of error sent to the 4x4 block on the right
        private const int DSHIFT = 4;
        private const int DSCALE = 1;   // storage descaling, needed to make the error fit byte

        public static void PickBestIntra16(Vp8EncIterator it, ref Vp8ModeScore rd, Vp8SegmentInfo[] segmentInfos, Vp8EncProba proba)
        {
            const int numBlocks = 16;
            Vp8SegmentInfo dqm = segmentInfos[it.CurrentMacroBlockInfo.Segment];
            int lambda = dqm.LambdaI16;
            int tlambda = dqm.TLambda;
            Span<byte> src = it.YuvIn.AsSpan(Vp8EncIterator.YOffEnc);
            var rdTmp = new Vp8ModeScore();
            Vp8ModeScore rdCur = rdTmp;
            Vp8ModeScore rdBest = rd;
            int mode;
            bool isFlat = IsFlatSource16(src);
            rd.ModeI16 = -1;
            for (mode = 0; mode < WebpConstants.NumPredModes; ++mode)
            {
                // scratch buffer.
                Span<byte> tmpDst = it.YuvOut2.AsSpan(Vp8EncIterator.YOffEnc);
                rdCur.ModeI16 = mode;

                // Reconstruct.
                rdCur.Nz = (uint)ReconstructIntra16(it, dqm, rdCur, tmpDst, mode);

                // Measure RD-score.
                rdCur.D = LossyUtils.Vp8Sse16X16(src, tmpDst);
                rdCur.SD = tlambda != 0 ? Mult8B(tlambda, LossyUtils.Vp8Disto16X16(src, tmpDst, WeightY)) : 0;
                rdCur.H = WebpConstants.Vp8FixedCostsI16[mode];
                rdCur.R = it.GetCostLuma16(rdCur, proba);

                if (isFlat)
                {
                    // Refine the first impression (which was in pixel space).
                    isFlat = IsFlat(rdCur.YAcLevels, numBlocks, WebpConstants.FlatnessLimitI16);
                    if (isFlat)
                    {
                        // Block is very flat. We put emphasis on the distortion being very low!
                        rdCur.D *= 2;
                        rdCur.SD *= 2;
                    }
                }

                // Since we always examine Intra16 first, we can overwrite *rd directly.
                rdCur.SetRdScore(lambda);

                if (mode == 0 || rdCur.Score < rdBest.Score)
                {
                    Vp8ModeScore tmp = rdCur;
                    rdCur = rdBest;
                    rdBest = tmp;
                    it.SwapOut();
                }
            }

            if (rdBest != rd)
            {
                rd = rdBest;
            }

            // Finalize score for mode decision.
            rd.SetRdScore(dqm.LambdaMode);
            it.SetIntra16Mode(rd.ModeI16);

            // We have a blocky macroblock (only DCs are non-zero) with fairly high
            // distortion, record max delta so we can later adjust the minimal filtering
            // strength needed to smooth these blocks out.
            if ((rd.Nz & 0x100ffff) == 0x1000000 && rd.D > dqm.MinDisto)
            {
                dqm.StoreMaxDelta(rd.YDcLevels);
            }
        }

        public static bool PickBestIntra4(Vp8EncIterator it, ref Vp8ModeScore rd, Vp8SegmentInfo[] segmentInfos, Vp8EncProba proba, int maxI4HeaderBits)
        {
            Vp8SegmentInfo dqm = segmentInfos[it.CurrentMacroBlockInfo.Segment];
            int lambda = dqm.LambdaI4;
            int tlambda = dqm.TLambda;
            Span<byte> src0 = it.YuvIn.AsSpan(Vp8EncIterator.YOffEnc);
            Span<byte> bestBlocks = it.YuvOut2.AsSpan(Vp8EncIterator.YOffEnc);
            int totalHeaderBits = 0;
            var rdBest = new Vp8ModeScore();

            if (maxI4HeaderBits == 0)
            {
                return false;
            }

            rdBest.InitScore();
            rdBest.H = 211;  // '211' is the value of VP8BitCost(0, 145)
            rdBest.SetRdScore(dqm.LambdaMode);
            it.StartI4();
            do
            {
                int numBlocks = 1;
                var rdi4 = new Vp8ModeScore();
                int mode;
                int bestMode = -1;
                Span<byte> src = src0.Slice(WebpLookupTables.Vp8Scan[it.I4]);
                short[] modeCosts = it.GetCostModeI4(rd.ModesI4);
                Span<byte> bestBlock = bestBlocks.Slice(WebpLookupTables.Vp8Scan[it.I4]);
                Span<byte> tmpDst = it.Scratch.AsSpan();
                tmpDst.Fill(0);

                rdi4.InitScore();
                it.MakeIntra4Preds();
                for (mode = 0; mode < WebpConstants.NumBModes; ++mode)
                {
                    var rdTmp = new Vp8ModeScore();
                    short[] tmpLevels = new short[16];

                    // Reconstruct.
                    rdTmp.Nz = (uint)ReconstructIntra4(it, dqm, tmpLevels, src, tmpDst, mode);

                    // Compute RD-score.
                    rdTmp.D = LossyUtils.Vp8Sse4X4(src, tmpDst);
                    rdTmp.SD = tlambda != 0 ? Mult8B(tlambda, LossyUtils.Vp8Disto4X4(src, tmpDst, WeightY)) : 0;
                    rdTmp.H = modeCosts[mode];

                    // Add flatness penalty, to avoid flat area to be mispredicted by a complex mode.
                    if (mode > 0 && IsFlat(tmpLevels, numBlocks, WebpConstants.FlatnessLimitI4))
                    {
                        rdTmp.R = WebpConstants.FlatnessPenality * numBlocks;
                    }
                    else
                    {
                        rdTmp.R = 0;
                    }

                    // early-out check.
                    rdTmp.SetRdScore(lambda);
                    if (bestMode >= 0 && rdTmp.Score >= rdi4.Score)
                    {
                        continue;
                    }

                    // finish computing score.
                    rdTmp.R += it.GetCostLuma4(tmpLevels, proba);
                    rdTmp.SetRdScore(lambda);

                    if (bestMode < 0 || rdTmp.Score < rdi4.Score)
                    {
                        rdi4.CopyScore(rdTmp);
                        bestMode = mode;
                        Span<byte> tmp = tmpDst;
                        tmpDst = bestBlock;
                        bestBlock = tmp;
                        tmpLevels.CopyTo(rdBest.YAcLevels.AsSpan(it.I4 * 16, 16));
                    }
                }

                rdi4.SetRdScore(dqm.LambdaMode);
                rdBest.AddScore(rdi4);
                if (rdBest.Score >= rd.Score)
                {
                    return false;
                }

                totalHeaderBits += (int)rdi4.H;   // <- equal to modeCosts[bestMode];
                if (totalHeaderBits > maxI4HeaderBits)
                {
                    return false;
                }

                // Copy selected samples to the right place.
                LossyUtils.Vp8Copy4X4(bestBlock, bestBlocks.Slice(WebpLookupTables.Vp8Scan[it.I4]));

                rd.ModesI4[it.I4] = (byte)bestMode;
                it.TopNz[it.I4 & 3] = it.LeftNz[it.I4 >> 2] = rdi4.Nz != 0 ? 1 : 0;
            }
            while (it.RotateI4(bestBlocks));

            // Finalize state.
            rd.CopyScore(rdBest);
            it.SetIntra4Mode(rd.ModesI4);
            it.SwapOut();
            rdBest.YAcLevels.AsSpan().CopyTo(rd.YAcLevels);

            // Select intra4x4 over intra16x16.
            return true;
        }

        public static void PickBestUv(Vp8EncIterator it, ref Vp8ModeScore rd, Vp8SegmentInfo[] segmentInfos, Vp8EncProba proba)
        {
            const int numBlocks = 8;
            Vp8SegmentInfo dqm = segmentInfos[it.CurrentMacroBlockInfo.Segment];
            int lambda = dqm.LambdaUv;
            Span<byte> src = it.YuvIn.AsSpan(Vp8EncIterator.UOffEnc);
            Span<byte> tmpDst = it.YuvOut2.AsSpan(Vp8EncIterator.UOffEnc);
            Span<byte> dst0 = it.YuvOut.AsSpan(Vp8EncIterator.UOffEnc);
            Span<byte> dst = dst0;
            var rdBest = new Vp8ModeScore();
            int mode;

            rd.ModeUv = -1;
            rdBest.InitScore();
            for (mode = 0; mode < WebpConstants.NumPredModes; ++mode)
            {
                var rdUv = new Vp8ModeScore();

                // Reconstruct
                rdUv.Nz = (uint)ReconstructUv(it, dqm, rdUv, tmpDst, mode);

                // Compute RD-score
                rdUv.D = LossyUtils.Vp8Sse16X8(src, tmpDst);
                rdUv.SD = 0;    // not calling TDisto here: it tends to flatten areas.
                rdUv.H = WebpConstants.Vp8FixedCostsUv[mode];
                rdUv.R = it.GetCostUv(rdUv, proba);
                if (mode > 0 && IsFlat(rdUv.UvLevels, numBlocks, WebpConstants.FlatnessLimitIUv))
                {
                    rdUv.R += WebpConstants.FlatnessPenality * numBlocks;
                }

                rdUv.SetRdScore(lambda);
                if (mode == 0 || rdUv.Score < rdBest.Score)
                {
                    rdBest.CopyScore(rdUv);
                    rd.ModeUv = mode;
                    rdUv.UvLevels.CopyTo(rd.UvLevels.AsSpan());
                    for (int i = 0; i < 2; i++)
                    {
                        rd.Derr[i, 0] = rdUv.Derr[i, 0];
                        rd.Derr[i, 1] = rdUv.Derr[i, 1];
                        rd.Derr[i, 2] = rdUv.Derr[i, 2];
                    }

                    Span<byte> tmp = dst;
                    dst = tmpDst;
                    tmpDst = tmp;
                }
            }

            it.SetIntraUvMode(rd.ModeUv);
            rd.AddScore(rdBest);
            if (dst != dst0)
            {
                // copy 16x8 block if needed.
                LossyUtils.Vp8Copy16X8(dst, dst0);
            }

            // Store diffusion errors for next block.
            it.StoreDiffusionErrors(rd);
        }

        public static int ReconstructIntra16(Vp8EncIterator it, Vp8SegmentInfo dqm, Vp8ModeScore rd, Span<byte> yuvOut, int mode)
        {
            Span<byte> reference = it.YuvP.AsSpan(Vp8Encoding.Vp8I16ModeOffsets[mode]);
            Span<byte> src = it.YuvIn.AsSpan(Vp8EncIterator.YOffEnc);
            int nz = 0;
            int n;
            short[] dcTmp = new short[16];
            short[] tmp = new short[16 * 16];
            Span<short> tmpSpan = tmp.AsSpan();

            for (n = 0; n < 16; n += 2)
            {
                Vp8Encoding.FTransform2(src.Slice(WebpLookupTables.Vp8Scan[n]), reference.Slice(WebpLookupTables.Vp8Scan[n]), tmpSpan.Slice(n * 16, 16), tmpSpan.Slice((n + 1) * 16, 16));
            }

            Vp8Encoding.FTransformWht(tmp, dcTmp);
            nz |= QuantizeBlock(dcTmp, rd.YDcLevels, dqm.Y2) << 24;

            for (n = 0; n < 16; n += 2)
            {
                // Zero-out the first coeff, so that: a) nz is correct below, and
                // b) finding 'last' non-zero coeffs in SetResidualCoeffs() is simplified.
                tmp[n * 16] = tmp[(n + 1) * 16] = 0;
                nz |= Quantize2Blocks(tmpSpan.Slice(n * 16, 32), rd.YAcLevels.AsSpan(n * 16, 32), dqm.Y1) << n;
            }

            // Transform back.
            LossyUtils.TransformWht(dcTmp, tmpSpan);
            for (n = 0; n < 16; n += 2)
            {
                Vp8Encoding.ITransform(reference.Slice(WebpLookupTables.Vp8Scan[n]), tmpSpan.Slice(n * 16, 32), yuvOut.Slice(WebpLookupTables.Vp8Scan[n]), true);
            }

            return nz;
        }

        public static int ReconstructIntra4(Vp8EncIterator it, Vp8SegmentInfo dqm, Span<short> levels, Span<byte> src, Span<byte> yuvOut, int mode)
        {
            Span<byte> reference = it.YuvP.AsSpan(Vp8Encoding.Vp8I4ModeOffsets[mode]);
            short[] tmp = new short[16];
            Vp8Encoding.FTransform(src, reference, tmp);
            int nz = QuantizeBlock(tmp, levels, dqm.Y1);
            Vp8Encoding.ITransform(reference, tmp, yuvOut, false);

            return nz;
        }

        public static int ReconstructUv(Vp8EncIterator it, Vp8SegmentInfo dqm, Vp8ModeScore rd, Span<byte> yuvOut, int mode)
        {
            Span<byte> reference = it.YuvP.AsSpan(Vp8Encoding.Vp8UvModeOffsets[mode]);
            Span<byte> src = it.YuvIn.AsSpan(Vp8EncIterator.UOffEnc);
            int nz = 0;
            int n;
            short[] tmp = new short[8 * 16];

            for (n = 0; n < 8; n += 2)
            {
                Vp8Encoding.FTransform2(
                    src.Slice(WebpLookupTables.Vp8ScanUv[n]),
                    reference.Slice(WebpLookupTables.Vp8ScanUv[n]),
                    tmp.AsSpan(n * 16, 16),
                    tmp.AsSpan((n + 1) * 16, 16));
            }

            CorrectDcValues(it, dqm.Uv, tmp, rd);

            for (n = 0; n < 8; n += 2)
            {
                nz |= Quantize2Blocks(tmp.AsSpan(n * 16, 32), rd.UvLevels.AsSpan(n * 16, 32), dqm.Uv) << n;
            }

            for (n = 0; n < 8; n += 2)
            {
                Vp8Encoding.ITransform(reference.Slice(WebpLookupTables.Vp8ScanUv[n]), tmp.AsSpan(n * 16, 32), yuvOut.Slice(WebpLookupTables.Vp8ScanUv[n]), true);
            }

            return nz << 16;
        }

        // Refine intra16/intra4 sub-modes based on distortion only (not rate).
        public static void RefineUsingDistortion(Vp8EncIterator it, Vp8SegmentInfo[] segmentInfos, Vp8ModeScore rd, bool tryBothModes, bool refineUvMode, int mbHeaderLimit)
        {
            long bestScore = Vp8ModeScore.MaxCost;
            int nz = 0;
            int mode;
            bool isI16 = tryBothModes || it.CurrentMacroBlockInfo.MacroBlockType == Vp8MacroBlockType.I16X16;
            Vp8SegmentInfo dqm = segmentInfos[it.CurrentMacroBlockInfo.Segment];

            // Some empiric constants, of approximate order of magnitude.
            const int lambdaDi16 = 106;
            const int lambdaDi4 = 11;
            const int lambdaDuv = 120;
            long scoreI4 = dqm.I4Penalty;
            long i4BitSum = 0;
            long bitLimit = tryBothModes
                ? mbHeaderLimit
                : Vp8ModeScore.MaxCost; // no early-out allowed.

            if (isI16)
            {
                int bestMode = -1;
                Span<byte> src = it.YuvIn.AsSpan(Vp8EncIterator.YOffEnc);
                for (mode = 0; mode < WebpConstants.NumPredModes; ++mode)
                {
                    Span<byte> reference = it.YuvP.AsSpan(Vp8Encoding.Vp8I16ModeOffsets[mode]);
                    long score = (LossyUtils.Vp8Sse16X16(src, reference) * WebpConstants.RdDistoMult) + (WebpConstants.Vp8FixedCostsI16[mode] * lambdaDi16);

                    if (mode > 0 && WebpConstants.Vp8FixedCostsI16[mode] > bitLimit)
                    {
                        continue;
                    }

                    if (score < bestScore)
                    {
                        bestMode = mode;
                        bestScore = score;
                    }
                }

                if (it.X == 0 || it.Y == 0)
                {
                    // Avoid starting a checkerboard resonance from the border. See bug #432 of libwebp.
                    if (IsFlatSource16(src))
                    {
                        bestMode = it.X == 0 ? 0 : 2;
                        tryBothModes = false; // Stick to i16.
                    }
                }

                it.SetIntra16Mode(bestMode);

                // We'll reconstruct later, if i16 mode actually gets selected.
            }

            // Next, evaluate Intra4.
            if (tryBothModes || !isI16)
            {
                // We don't evaluate the rate here, but just account for it through a
                // constant penalty (i4 mode usually needs more bits compared to i16).
                isI16 = false;
                it.StartI4();
                do
                {
                    int bestI4Mode = -1;
                    long bestI4Score = Vp8ModeScore.MaxCost;
                    Span<byte> src = it.YuvIn.AsSpan(Vp8EncIterator.YOffEnc + WebpLookupTables.Vp8Scan[it.I4]);
                    short[] modeCosts = it.GetCostModeI4(rd.ModesI4);

                    it.MakeIntra4Preds();
                    for (mode = 0; mode < WebpConstants.NumBModes; ++mode)
                    {
                        Span<byte> reference = it.YuvP.AsSpan(Vp8Encoding.Vp8I4ModeOffsets[mode]);
                        long score = (LossyUtils.Vp8Sse4X4(src, reference) * WebpConstants.RdDistoMult) + (modeCosts[mode] * lambdaDi4);
                        if (score < bestI4Score)
                        {
                            bestI4Mode = mode;
                            bestI4Score = score;
                        }
                    }

                    i4BitSum += modeCosts[bestI4Mode];
                    rd.ModesI4[it.I4] = (byte)bestI4Mode;
                    scoreI4 += bestI4Score;
                    if (scoreI4 >= bestScore || i4BitSum > bitLimit)
                    {
                        // Intra4 won't be better than Intra16. Bail out and pick Intra16.
                        isI16 = true;
                        break;
                    }
                    else
                    {
                        // Reconstruct partial block inside YuvOut2 buffer
                        Span<byte> tmpDst = it.YuvOut2.AsSpan(Vp8EncIterator.YOffEnc + WebpLookupTables.Vp8Scan[it.I4]);
                        nz |= ReconstructIntra4(it, dqm, rd.YAcLevels.AsSpan(it.I4 * 16, 16), src, tmpDst, bestI4Mode) << it.I4;
                    }
                }
                while (it.RotateI4(it.YuvOut2.AsSpan(Vp8EncIterator.YOffEnc)));
            }

            // Final reconstruction, depending on which mode is selected.
            if (!isI16)
            {
                it.SetIntra4Mode(rd.ModesI4);
                it.SwapOut();
                bestScore = scoreI4;
            }
            else
            {
                int intra16Mode = it.Preds[it.PredIdx];
                nz = ReconstructIntra16(it, dqm, rd, it.YuvOut.AsSpan(Vp8EncIterator.YOffEnc), intra16Mode);
            }

            // ... and UV!
            if (refineUvMode)
            {
                int bestMode = -1;
                long bestUvScore = Vp8ModeScore.MaxCost;
                Span<byte> src = it.YuvIn.AsSpan(Vp8EncIterator.UOffEnc);
                for (mode = 0; mode < WebpConstants.NumPredModes; ++mode)
                {
                    Span<byte> reference = it.YuvP.AsSpan(Vp8Encoding.Vp8UvModeOffsets[mode]);
                    long score = (LossyUtils.Vp8Sse16X8(src, reference) * WebpConstants.RdDistoMult) + (WebpConstants.Vp8FixedCostsUv[mode] * lambdaDuv);
                    if (score < bestUvScore)
                    {
                        bestMode = mode;
                        bestUvScore = score;
                    }
                }

                it.SetIntraUvMode(bestMode);
            }

            nz |= ReconstructUv(it, dqm, rd, it.YuvOut.AsSpan(Vp8EncIterator.UOffEnc), it.CurrentMacroBlockInfo.UvMode);

            rd.Nz = (uint)nz;
            rd.Score = bestScore;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static int Quantize2Blocks(Span<short> input, Span<short> output, Vp8Matrix mtx)
        {
            int nz = QuantizeBlock(input, output, mtx) << 0;
            nz |= QuantizeBlock(input.Slice(1 * 16), output.Slice(1 * 16), mtx) << 1;
            return nz;
        }

        public static int QuantizeBlock(Span<short> input, Span<short> output, Vp8Matrix mtx)
        {
            int last = -1;
            int n;
            for (n = 0; n < 16; ++n)
            {
                int j = Zigzag[n];
                bool sign = input[j] < 0;
                uint coeff = (uint)((sign ? -input[j] : input[j]) + mtx.Sharpen[j]);
                if (coeff > mtx.ZThresh[j])
                {
                    uint q = mtx.Q[j];
                    uint iQ = mtx.IQ[j];
                    uint b = mtx.Bias[j];
                    int level = QuantDiv(coeff, iQ, b);
                    if (level > MaxLevel)
                    {
                        level = MaxLevel;
                    }

                    if (sign)
                    {
                        level = -level;
                    }

                    input[j] = (short)(level * (int)q);
                    output[n] = (short)level;
                    if (level != 0)
                    {
                        last = n;
                    }
                }
                else
                {
                    output[n] = 0;
                    input[j] = 0;
                }
            }

            return last >= 0 ? 1 : 0;
        }

        // Quantize as usual, but also compute and return the quantization error.
        // Error is already divided by DSHIFT.
        public static int QuantizeSingle(Span<short> v, Vp8Matrix mtx)
        {
            int v0 = v[0];
            bool sign = v0 < 0;
            if (sign)
            {
                v0 = -v0;
            }

            if (v0 > (int)mtx.ZThresh[0])
            {
                int qV = QuantDiv((uint)v0, mtx.IQ[0], mtx.Bias[0]) * mtx.Q[0];
                int err = v0 - qV;
                v[0] = (short)(sign ? -qV : qV);
                return (sign ? -err : err) >> DSCALE;
            }

            v[0] = 0;
            return (sign ? -v0 : v0) >> DSCALE;
        }

        public static void CorrectDcValues(Vp8EncIterator it, Vp8Matrix mtx, short[] tmp, Vp8ModeScore rd)
        {
#pragma warning disable SA1005 // Single line comments should begin with single space
            //         | top[0] | top[1]
            // --------+--------+---------
            // left[0] | tmp[0]   tmp[1]  <->   err0 err1
            // left[1] | tmp[2]   tmp[3]        err2 err3
            //
            // Final errors {err1,err2,err3} are preserved and later restored
            // as top[]/left[] on the next block.
#pragma warning restore SA1005 // Single line comments should begin with single space
            for (int ch = 0; ch <= 1; ++ch)
            {
                Span<sbyte> top = it.TopDerr.AsSpan((it.X * 4) + ch, 2);
                Span<sbyte> left = it.LeftDerr.AsSpan(ch, 2);
                Span<short> c = tmp.AsSpan(ch * 4 * 16, 4 * 16);
                c[0] += (short)(((C1 * top[0]) + (C2 * left[0])) >> (DSHIFT - DSCALE));
                int err0 = QuantizeSingle(c, mtx);
                c[1 * 16] += (short)(((C1 * top[1]) + (C2 * err0)) >> (DSHIFT - DSCALE));
                int err1 = QuantizeSingle(c.Slice(1 * 16), mtx);
                c[2 * 16] += (short)(((C1 * err0) + (C2 * left[1])) >> (DSHIFT - DSCALE));
                int err2 = QuantizeSingle(c.Slice(2 * 16), mtx);
                c[3 * 16] += (short)(((C1 * err1) + (C2 * err2)) >> (DSHIFT - DSCALE));
                int err3 = QuantizeSingle(c.Slice(3 * 16), mtx);

                rd.Derr[ch, 0] = err1;
                rd.Derr[ch, 1] = err2;
                rd.Derr[ch, 2] = err3;
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static bool IsFlatSource16(Span<byte> src)
        {
            uint v = src[0] * 0x01010101u;
            Span<byte> vSpan = BitConverter.GetBytes(v).AsSpan();
            for (int i = 0; i < 16; i++)
            {
                if (!src.Slice(0, 4).SequenceEqual(vSpan) || !src.Slice(4, 4).SequenceEqual(vSpan) ||
                    !src.Slice(8, 4).SequenceEqual(vSpan) || !src.Slice(12, 4).SequenceEqual(vSpan))
                {
                    return false;
                }

                src = src.Slice(WebpConstants.Bps);
            }

            return true;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static bool IsFlat(Span<short> levels, int numBlocks, int thresh)
        {
            int score = 0;
            while (numBlocks-- > 0)
            {
                for (int i = 1; i < 16; i++)
                {
                    // omit DC, we're only interested in AC
                    score += levels[i] != 0 ? 1 : 0;
                    if (score > thresh)
                    {
                        return false;
                    }
                }

                levels = levels.Slice(16);
            }

            return true;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int Mult8B(int a, int b) => ((a * b) + 128) >> 8;

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int QuantDiv(uint n, uint iQ, uint b) => (int)(((n * iQ) + b) >> WebpConstants.QFix);
    }
}
