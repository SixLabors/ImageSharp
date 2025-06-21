// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Webp.Lossless;

internal static class BackwardReferenceEncoder
{
    /// <summary>
    /// Maximum bit length.
    /// </summary>
    public const int MaxLengthBits = 12;

    private const float MaxEntropy = 1e30f;

    private const int WindowOffsetsSizeMax = 32;

    /// <summary>
    /// We want the max value to be attainable and stored in MaxLengthBits bits.
    /// </summary>
    public const int MaxLength = (1 << MaxLengthBits) - 1;

    /// <summary>
    /// Minimum number of pixels for which it is cheaper to encode a
    /// distance + length instead of each pixel as a literal.
    /// </summary>
    private const int MinLength = 4;

    /// <summary>
    /// Evaluates best possible backward references for specified quality. The input cacheBits to 'GetBackwardReferences'
    /// sets the maximum cache bits to use (passing 0 implies disabling the local color cache).
    /// The optimal cache bits is evaluated and set for the cacheBits parameter.
    /// The return value is the pointer to the best of the two backward refs viz, refs[0] or refs[1].
    /// </summary>
    public static Vp8LBackwardRefs GetBackwardReferences(
        int width,
        int height,
        ReadOnlySpan<uint> bgra,
        uint quality,
        int lz77TypesToTry,
        ref int cacheBits,
        MemoryAllocator memoryAllocator,
        Vp8LHashChain hashChain,
        Vp8LBackwardRefs best,
        Vp8LBackwardRefs worst)
    {
        int lz77TypeBest = 0;
        double bitCostBest = -1;
        int cacheBitsInitial = cacheBits;
        Vp8LHashChain? hashChainBox = null;
        Vp8LStreaks stats = new();
        Vp8LBitEntropy bitsEntropy = new();

        ColorCache[] colorCache = new ColorCache[WebpConstants.MaxColorCacheBits + 1];
        for (int lz77Type = 1; lz77TypesToTry > 0; lz77TypesToTry &= ~lz77Type, lz77Type <<= 1)
        {
            int cacheBitsTmp = cacheBitsInitial;
            if ((lz77TypesToTry & lz77Type) == 0)
            {
                continue;
            }

            switch ((Vp8LLz77Type)lz77Type)
            {
                case Vp8LLz77Type.Lz77Rle:
                    BackwardReferencesRle(width, height, bgra, 0, worst);
                    break;
                case Vp8LLz77Type.Lz77Standard:
                    // Compute LZ77 with no cache (0 bits), as the ideal LZ77 with a color cache is not that different in practice.
                    BackwardReferencesLz77(width, height, bgra, 0, hashChain, worst);
                    break;
                case Vp8LLz77Type.Lz77Box:
                    hashChainBox = new(memoryAllocator, width * height);
                    BackwardReferencesLz77Box(width, height, bgra, 0, hashChain, hashChainBox, worst);
                    break;
            }

            // Next, try with a color cache and update the references.
            cacheBitsTmp = CalculateBestCacheSize(memoryAllocator, colorCache, bgra, quality, worst, cacheBitsTmp);
            if (cacheBitsTmp > 0)
            {
                BackwardRefsWithLocalCache(bgra, cacheBitsTmp, worst);
            }

            // Keep the best backward references.
            using OwnedVp8LHistogram histo = OwnedVp8LHistogram.Create(memoryAllocator, worst, cacheBitsTmp);
            double bitCost = histo.EstimateBits(stats, bitsEntropy);

            if (lz77TypeBest == 0 || bitCost < bitCostBest)
            {
                (best, worst) = (worst, best);
                bitCostBest = bitCost;
                cacheBits = cacheBitsTmp;
                lz77TypeBest = lz77Type;
            }
        }

        // Improve on simple LZ77 but only for high quality (TraceBackwards is costly).
        if ((lz77TypeBest == (int)Vp8LLz77Type.Lz77Standard || lz77TypeBest == (int)Vp8LLz77Type.Lz77Box) && quality >= 25)
        {
            Vp8LHashChain hashChainTmp = lz77TypeBest == (int)Vp8LLz77Type.Lz77Standard ? hashChain : hashChainBox!;
            BackwardReferencesTraceBackwards(width, height, memoryAllocator, bgra, cacheBits, hashChainTmp, best, worst);
            using OwnedVp8LHistogram histo = OwnedVp8LHistogram.Create(memoryAllocator, worst, cacheBits);
            double bitCostTrace = histo.EstimateBits(stats, bitsEntropy);
            if (bitCostTrace < bitCostBest)
            {
                best = worst;
            }
        }

        BackwardReferences2DLocality(width, best);

        hashChainBox?.Dispose();

        return best;
    }

    /// <summary>
    /// Evaluate optimal cache bits for the local color cache.
    /// The input bestCacheBits sets the maximum cache bits to use (passing 0 implies disabling the local color cache).
    /// The local color cache is also disabled for the lower (smaller then 25) quality.
    /// </summary>
    /// <returns>Best cache size.</returns>
    private static int CalculateBestCacheSize(
        MemoryAllocator memoryAllocator,
        Span<ColorCache> colorCache,
        ReadOnlySpan<uint> bgra,
        uint quality,
        Vp8LBackwardRefs refs,
        int bestCacheBits)
    {
        int cacheBitsMax = quality <= 25 ? 0 : bestCacheBits;
        if (cacheBitsMax == 0)
        {
            // Local color cache is disabled.
            return 0;
        }

        double entropyMin = MaxEntropy;
        int pos = 0;

        using Vp8LHistogramSet histos = new(memoryAllocator, colorCache.Length, 0);
        for (int i = 0; i < colorCache.Length; i++)
        {
            histos[i].PaletteCodeBits = i;
            colorCache[i] = new(i);
        }

        // Find the cacheBits giving the lowest entropy.
        foreach (PixOrCopy v in refs)
        {
            if (v.IsLiteral())
            {
                uint pix = bgra[pos++];
                int a = (int)(pix >> 24) & 0xff;
                int r = (int)(pix >> 16) & 0xff;
                int g = (int)(pix >> 8) & 0xff;
                int b = (int)(pix >> 0) & 0xff;

                // The keys of the caches can be derived from the longest one.
                int key = ColorCache.HashPix(pix, 32 - cacheBitsMax);

                // Do not use the color cache for cacheBits = 0.
                ++histos[0].Blue[b];
                ++histos[0].Literal[g];
                ++histos[0].Red[r];
                ++histos[0].Alpha[a];

                // Deal with cacheBits > 0.
                for (int i = cacheBitsMax; i >= 1; --i, key >>= 1)
                {
                    if (colorCache[i].Lookup(key) == pix)
                    {
                        ++histos[i].Literal[WebpConstants.NumLiteralCodes + WebpConstants.NumLengthCodes + key];
                    }
                    else
                    {
                        colorCache[i].Set((uint)key, pix);
                        ++histos[i].Blue[b];
                        ++histos[i].Literal[g];
                        ++histos[i].Red[r];
                        ++histos[i].Alpha[a];
                    }
                }
            }
            else
            {
                // We should compute the contribution of the (distance, length)
                // histograms but those are the same independently from the cache size.
                // As those constant contributions are in the end added to the other
                // histogram contributions, we can ignore them, except for the length
                // prefix that is part of the literal_ histogram.
                int len = v.Len;
                uint bgraPrev = bgra[pos] ^ 0xffffffffu;

                int extraBits = 0, extraBitsValue = 0;
                int code = LosslessUtils.PrefixEncode(len, ref extraBits, ref extraBitsValue);
                for (int i = 0; i <= cacheBitsMax; i++)
                {
                    ++histos[i].Literal[WebpConstants.NumLiteralCodes + code];
                }

                // Update the color caches.
                do
                {
                    if (bgra[pos] != bgraPrev)
                    {
                        // Efficiency: insert only if the color changes.
                        int key = ColorCache.HashPix(bgra[pos], 32 - cacheBitsMax);
                        for (int i = cacheBitsMax; i >= 1; --i, key >>= 1)
                        {
                            colorCache[i].Colors[key] = bgra[pos];
                        }

                        bgraPrev = bgra[pos];
                    }

                    pos++;
                }
                while (--len != 0);
            }
        }

        Vp8LStreaks stats = new();
        Vp8LBitEntropy bitsEntropy = new();
        for (int i = 0; i <= cacheBitsMax; i++)
        {
            double entropy = histos[i].EstimateBits(stats, bitsEntropy);
            if (i == 0 || entropy < entropyMin)
            {
                entropyMin = entropy;
                bestCacheBits = i;
            }
        }

        return bestCacheBits;
    }

    private static void BackwardReferencesTraceBackwards(
        int xSize,
        int ySize,
        MemoryAllocator memoryAllocator,
        ReadOnlySpan<uint> bgra,
        int cacheBits,
        Vp8LHashChain hashChain,
        Vp8LBackwardRefs refsSrc,
        Vp8LBackwardRefs refsDst)
    {
        int distArraySize = xSize * ySize;
        using IMemoryOwner<ushort> distArrayBuffer = memoryAllocator.Allocate<ushort>(distArraySize);
        Span<ushort> distArray = distArrayBuffer.GetSpan();

        BackwardReferencesHashChainDistanceOnly(xSize, ySize, memoryAllocator, bgra, cacheBits, hashChain, refsSrc, distArrayBuffer);
        int chosenPathSize = TraceBackwards(distArray, distArraySize);
        Span<ushort> chosenPath = distArray[(distArraySize - chosenPathSize)..];
        BackwardReferencesHashChainFollowChosenPath(bgra, cacheBits, chosenPath, chosenPathSize, hashChain, refsDst);
    }

    private static void BackwardReferencesHashChainDistanceOnly(
        int xSize,
        int ySize,
        MemoryAllocator memoryAllocator,
        ReadOnlySpan<uint> bgra,
        int cacheBits,
        Vp8LHashChain hashChain,
        Vp8LBackwardRefs refs,
        IMemoryOwner<ushort> distArrayBuffer)
    {
        int pixCount = xSize * ySize;
        bool useColorCache = cacheBits > 0;
        int literalArraySize = WebpConstants.NumLiteralCodes + WebpConstants.NumLengthCodes + (cacheBits > 0 ? 1 << cacheBits : 0);
        CostModel costModel = new(memoryAllocator, literalArraySize);
        int offsetPrev = -1;
        int lenPrev = -1;
        double offsetCost = -1;
        int firstOffsetIsConstant = -1;  // initialized with 'impossible' value.
        int reach = 0;
        ColorCache? colorCache = null;

        if (useColorCache)
        {
            colorCache = new(cacheBits);
        }

        costModel.Build(xSize, cacheBits, refs);
        using CostManager costManager = new(memoryAllocator, distArrayBuffer, pixCount, costModel);
        Span<float> costManagerCosts = costManager.Costs.GetSpan();
        Span<ushort> distArray = distArrayBuffer.GetSpan();

        // We loop one pixel at a time, but store all currently best points to non-processed locations from this point.
        distArray[0] = 0;

        // Add first pixel as literal.
        AddSingleLiteralWithCostModel(bgra, colorCache, costModel, 0, useColorCache, 0.0f, costManagerCosts, distArray);

        for (int i = 1; i < pixCount; i++)
        {
            float prevCost = costManagerCosts[i - 1];
            int offset = hashChain.FindOffset(i);
            int len = hashChain.FindLength(i);

            // Try adding the pixel as a literal.
            AddSingleLiteralWithCostModel(bgra, colorCache, costModel, i, useColorCache, prevCost, costManagerCosts, distArray);

            // If we are dealing with a non-literal.
            if (len >= 2)
            {
                if (offset != offsetPrev)
                {
                    int code = DistanceToPlaneCode(xSize, offset);
                    offsetCost = costModel.GetDistanceCost(code);
                    firstOffsetIsConstant = 1;
                    costManager.PushInterval(prevCost + offsetCost, i, len);
                }
                else
                {
                    // Instead of considering all contributions from a pixel i by calling:
                    // costManager.PushInterval(prevCost + offsetCost, i, len);
                    // we optimize these contributions in case offsetCost stays the same
                    // for consecutive pixels. This describes a set of pixels similar to a
                    // previous set (e.g. constant color regions).
                    if (firstOffsetIsConstant != 0)
                    {
                        reach = i - 1 + lenPrev - 1;
                        firstOffsetIsConstant = 0;
                    }

                    if (i + len - 1 > reach)
                    {
                        int lenJ = 0;
                        int j;
                        for (j = i; j <= reach; j++)
                        {
                            int offsetJ = hashChain.FindOffset(j + 1);
                            lenJ = hashChain.FindLength(j + 1);
                            if (offsetJ != offset)
                            {
                                lenJ = hashChain.FindLength(j);
                                break;
                            }
                        }

                        // Update the cost at j - 1 and j.
                        costManager.UpdateCostAtIndex(j - 1, false);
                        costManager.UpdateCostAtIndex(j, false);

                        costManager.PushInterval(costManagerCosts[j - 1] + offsetCost, j, lenJ);
                        reach = j + lenJ - 1;
                    }
                }
            }

            costManager.UpdateCostAtIndex(i, true);
            offsetPrev = offset;
            lenPrev = len;
        }
    }

    private static int TraceBackwards(Span<ushort> distArray, int distArraySize)
    {
        int chosenPathSize = 0;
        int pathPos = distArraySize;
        int curPos = distArraySize - 1;
        while (curPos >= 0)
        {
            ushort cur = distArray[curPos];
            pathPos--;
            chosenPathSize++;
            distArray[pathPos] = cur;
            curPos -= cur;
        }

        return chosenPathSize;
    }

    private static void BackwardReferencesHashChainFollowChosenPath(ReadOnlySpan<uint> bgra, int cacheBits, Span<ushort> chosenPath, int chosenPathSize, Vp8LHashChain hashChain, Vp8LBackwardRefs backwardRefs)
    {
        bool useColorCache = cacheBits > 0;
        ColorCache? colorCache = null;
        int i = 0;

        if (useColorCache)
        {
            colorCache = new(cacheBits);
        }

        backwardRefs.Clear();
        for (int ix = 0; ix < chosenPathSize; ix++)
        {
            int len = chosenPath[ix];
            if (len != 1)
            {
                int offset = hashChain.FindOffset(i);
                backwardRefs.Add(PixOrCopy.CreateCopy((uint)offset, (ushort)len));

                if (useColorCache)
                {
                    for (int k = 0; k < len; k++)
                    {
                        colorCache!.Insert(bgra[i + k]);
                    }
                }

                i += len;
            }
            else
            {
                PixOrCopy v;
                int idx = useColorCache ? colorCache!.Contains(bgra[i]) : -1;
                if (idx >= 0)
                {
                    // useColorCache is true and color cache contains bgra[i]
                    // Push pixel as a color cache index.
                    v = PixOrCopy.CreateCacheIdx(idx);
                }
                else
                {
                    if (useColorCache)
                    {
                        colorCache!.Insert(bgra[i]);
                    }

                    v = PixOrCopy.CreateLiteral(bgra[i]);
                }

                backwardRefs.Add(v);
                i++;
            }
        }
    }

    private static void AddSingleLiteralWithCostModel(
        ReadOnlySpan<uint> bgra,
        ColorCache? colorCache,
        CostModel costModel,
        int idx,
        bool useColorCache,
        float prevCost,
        Span<float> cost,
        Span<ushort> distArray)
    {
        double costVal = prevCost;
        uint color = bgra[idx];
        int ix = useColorCache ? colorCache!.Contains(color) : -1;
        if (ix >= 0)
        {
            const double mul0 = 0.68;
            costVal += costModel.GetCacheCost((uint)ix) * mul0;
        }
        else
        {
            const double mul1 = 0.82;
            if (useColorCache)
            {
                colorCache!.Insert(color);
            }

            costVal += costModel.GetLiteralCost(color) * mul1;
        }

        if (cost[idx] > costVal)
        {
            cost[idx] = (float)costVal;
            distArray[idx] = 1;  // only one is inserted.
        }
    }

    private static void BackwardReferencesLz77(int xSize, int ySize, ReadOnlySpan<uint> bgra, int cacheBits, Vp8LHashChain hashChain, Vp8LBackwardRefs refs)
    {
        int iLastCheck = -1;
        bool useColorCache = cacheBits > 0;
        int pixCount = xSize * ySize;
        ColorCache? colorCache = null;
        if (useColorCache)
        {
            colorCache = new(cacheBits);
        }

        refs.Clear();
        for (int i = 0; i < pixCount;)
        {
            // Alternative #1: Code the pixels starting at 'i' using backward reference.
            int j;
            int offset = hashChain.FindOffset(i);
            int len = hashChain.FindLength(i);
            if (len >= MinLength)
            {
                int lenIni = len;
                int maxReach = 0;
                int jMax = i + lenIni >= pixCount ? pixCount - 1 : i + lenIni;

                // Only start from what we have not checked already.
                iLastCheck = i > iLastCheck ? i : iLastCheck;

                // We know the best match for the current pixel but we try to find the
                // best matches for the current pixel AND the next one combined.
                // The naive method would use the intervals:
                // [i,i+len) + [i+len, length of best match at i+len)
                // while we check if we can use:
                // [i,j) (where j<=i+len) + [j, length of best match at j)
                for (j = iLastCheck + 1; j <= jMax; j++)
                {
                    int lenJ = hashChain.FindLength(j);
                    int reach = j + (lenJ >= MinLength ? lenJ : 1); // 1 for single literal.
                    if (reach > maxReach)
                    {
                        len = j - i;
                        maxReach = reach;
                        if (maxReach >= pixCount)
                        {
                            break;
                        }
                    }
                }
            }
            else
            {
                len = 1;
            }

            // Go with literal or backward reference.
            if (len == 1)
            {
                AddSingleLiteral(bgra[i], useColorCache, colorCache, refs);
            }
            else
            {
                refs.Add(PixOrCopy.CreateCopy((uint)offset, (ushort)len));
                if (useColorCache)
                {
                    for (j = i; j < i + len; j++)
                    {
                        colorCache!.Insert(bgra[j]);
                    }
                }
            }

            i += len;
        }
    }

    /// <summary>
    /// Compute an LZ77 by forcing matches to happen within a given distance cost.
    /// We therefore limit the algorithm to the lowest 32 values in the PlaneCode definition.
    /// </summary>
    private static void BackwardReferencesLz77Box(int xSize, int ySize, ReadOnlySpan<uint> bgra, int cacheBits, Vp8LHashChain hashChainBest, Vp8LHashChain hashChain, Vp8LBackwardRefs refs)
    {
        int pixelCount = xSize * ySize;
        int[] windowOffsets = new int[WindowOffsetsSizeMax];
        int[] windowOffsetsNew = new int[WindowOffsetsSizeMax];
        int windowOffsetsSize = 0;
        int windowOffsetsNewSize = 0;
        short[] counts = new short[xSize * ySize];
        int bestOffsetPrev = -1;
        int bestLengthPrev = -1;

        // counts[i] counts how many times a pixel is repeated starting at position i.
        int i = pixelCount - 2;
        int countsPos = i;
        counts[countsPos + 1] = 1;
        for (; i >= 0; --i, --countsPos)
        {
            if (bgra[i] == bgra[i + 1])
            {
                // Max out the counts to MaxLength.
                counts[countsPos] = counts[countsPos + 1];
                if (counts[countsPos + 1] != MaxLength)
                {
                    counts[countsPos]++;
                }
            }
            else
            {
                counts[countsPos] = 1;
            }
        }

        // Figure out the window offsets around a pixel. They are stored in a
        // spiraling order around the pixel as defined by DistanceToPlaneCode.
        for (int y = 0; y <= 6; y++)
        {
            for (int x = -6; x <= 6; x++)
            {
                int offset = (y * xSize) + x;

                // Ignore offsets that bring us after the pixel.
                if (offset <= 0)
                {
                    continue;
                }

                int planeCode = DistanceToPlaneCode(xSize, offset) - 1;
                if (planeCode >= WindowOffsetsSizeMax)
                {
                    continue;
                }

                windowOffsets[planeCode] = offset;
            }
        }

        // For narrow images, not all plane codes are reached, so remove those.
        for (i = 0; i < WindowOffsetsSizeMax; i++)
        {
            if (windowOffsets[i] == 0)
            {
                continue;
            }

            windowOffsets[windowOffsetsSize++] = windowOffsets[i];
        }

        // Given a pixel P, find the offsets that reach pixels unreachable from P-1
        // with any of the offsets in windowOffsets[].
        for (i = 0; i < windowOffsetsSize; i++)
        {
            bool isReachable = false;
            for (int j = 0; j < windowOffsetsSize && !isReachable; j++)
            {
                isReachable |= windowOffsets[i] == windowOffsets[j] + 1;
            }

            if (!isReachable)
            {
                windowOffsetsNew[windowOffsetsNewSize] = windowOffsets[i];
                ++windowOffsetsNewSize;
            }
        }

        Span<uint> hashChainOffsetLength = hashChain.OffsetLength.GetSpan();
        hashChainOffsetLength[0] = 0;
        for (i = 1; i < pixelCount; i++)
        {
            int ind;
            int bestLength = hashChainBest.FindLength(i);
            int bestOffset = 0;
            bool doCompute = true;

            if (bestLength >= MaxLength)
            {
                // Do not recompute the best match if we already have a maximal one in the window.
                bestOffset = hashChainBest.FindOffset(i);
                for (ind = 0; ind < windowOffsetsSize; ind++)
                {
                    if (bestOffset == windowOffsets[ind])
                    {
                        doCompute = false;
                        break;
                    }
                }
            }

            if (doCompute)
            {
                // Figure out if we should use the offset/length from the previous pixel
                // as an initial guess and therefore only inspect the offsets in windowOffsetsNew[].
                bool usePrev = bestLengthPrev is > 1 and < MaxLength;
                int numInd = usePrev ? windowOffsetsNewSize : windowOffsetsSize;
                bestLength = usePrev ? bestLengthPrev - 1 : 0;
                bestOffset = usePrev ? bestOffsetPrev : 0;

                // Find the longest match in a window around the pixel.
                for (ind = 0; ind < numInd; ind++)
                {
                    int currLength = 0;
                    int j = i;
                    int jOffset = usePrev ? i - windowOffsetsNew[ind] : i - windowOffsets[ind];
                    if (jOffset < 0 || bgra[jOffset] != bgra[i])
                    {
                        continue;
                    }

                    // The longest match is the sum of how many times each pixel is repeated.
                    do
                    {
                        int countsJOffset = counts[jOffset];
                        int countsJ = counts[j];
                        if (countsJOffset != countsJ)
                        {
                            currLength += countsJOffset < countsJ ? countsJOffset : countsJ;
                            break;
                        }

                        // The same color is repeated counts_pos times at jOffset and j.
                        currLength += countsJOffset;
                        jOffset += countsJOffset;
                        j += countsJOffset;
                    }
                    while (currLength <= MaxLength && j < pixelCount && bgra[jOffset] == bgra[j]);

                    if (bestLength < currLength)
                    {
                        bestOffset = usePrev ? windowOffsetsNew[ind] : windowOffsets[ind];
                        if (currLength >= MaxLength)
                        {
                            bestLength = MaxLength;
                            break;
                        }

                        bestLength = currLength;
                    }
                }
            }

            if (bestLength <= MinLength)
            {
                hashChainOffsetLength[i] = 0;
                bestOffsetPrev = 0;
                bestLengthPrev = 0;
            }
            else
            {
                hashChainOffsetLength[i] = (uint)((bestOffset << MaxLengthBits) | bestLength);
                bestOffsetPrev = bestOffset;
                bestLengthPrev = bestLength;
            }
        }

        hashChainOffsetLength[0] = 0;
        BackwardReferencesLz77(xSize, ySize, bgra, cacheBits, hashChain, refs);
    }

    private static void BackwardReferencesRle(int xSize, int ySize, ReadOnlySpan<uint> bgra, int cacheBits, Vp8LBackwardRefs refs)
    {
        int pixelCount = xSize * ySize;
        bool useColorCache = cacheBits > 0;
        ColorCache? colorCache = null;

        if (useColorCache)
        {
            colorCache = new(cacheBits);
        }

        refs.Clear();

        // Add first pixel as literal.
        AddSingleLiteral(bgra[0], useColorCache, colorCache, refs);
        int i = 1;
        while (i < pixelCount)
        {
            int maxLen = LosslessUtils.MaxFindCopyLength(pixelCount - i);
            int rleLen = LosslessUtils.FindMatchLength(bgra[i..], bgra[(i - 1)..], 0, maxLen);
            int prevRowLen = i < xSize ? 0 : LosslessUtils.FindMatchLength(bgra[i..], bgra[(i - xSize)..], 0, maxLen);
            if (rleLen >= prevRowLen && rleLen >= MinLength)
            {
                refs.Add(PixOrCopy.CreateCopy(1, (ushort)rleLen));

                // We don't need to update the color cache here since it is always the
                // same pixel being copied, and that does not change the color cache state.
                i += rleLen;
            }
            else if (prevRowLen >= MinLength)
            {
                refs.Add(PixOrCopy.CreateCopy((uint)xSize, (ushort)prevRowLen));
                if (useColorCache)
                {
                    for (int k = 0; k < prevRowLen; ++k)
                    {
                        colorCache!.Insert(bgra[i + k]);
                    }
                }

                i += prevRowLen;
            }
            else
            {
                AddSingleLiteral(bgra[i], useColorCache, colorCache, refs);
                i++;
            }
        }
    }

    /// <summary>
    /// Update (in-place) backward references for the specified cacheBits.
    /// </summary>
    private static void BackwardRefsWithLocalCache(ReadOnlySpan<uint> bgra, int cacheBits, Vp8LBackwardRefs refs)
    {
        int pixelIndex = 0;
        ColorCache colorCache = new(cacheBits);
        foreach (ref PixOrCopy v in refs)
        {
            if (v.IsLiteral())
            {
                uint bgraLiteral = v.BgraOrDistance;
                int ix = colorCache.Contains(bgraLiteral);
                if (ix >= 0)
                {
                    // Color cache contains bgraLiteral
                    v = PixOrCopy.CreateCacheIdx(ix);
                }
                else
                {
                    colorCache.Insert(bgraLiteral);
                }

                pixelIndex++;
            }
            else
            {
                // refs was created without local cache, so it can not have cache indexes.
                for (int k = 0; k < v.Len; ++k)
                {
                    colorCache.Insert(bgra[pixelIndex++]);
                }
            }
        }
    }

    private static void BackwardReferences2DLocality(int xSize, Vp8LBackwardRefs refs)
    {
        foreach (ref PixOrCopy v in refs)
        {
            if (v.IsCopy())
            {
                int dist = (int)v.BgraOrDistance;
                int transformedDist = DistanceToPlaneCode(xSize, dist);
                v = PixOrCopy.CreateCopy((uint)transformedDist, v.Len);
            }
        }
    }

    private static void AddSingleLiteral(uint pixel, bool useColorCache, ColorCache? colorCache, Vp8LBackwardRefs refs)
    {
        PixOrCopy v;
        if (useColorCache)
        {
            int key = colorCache!.GetIndex(pixel);
            if (colorCache.Lookup(key) == pixel)
            {
                v = PixOrCopy.CreateCacheIdx(key);
            }
            else
            {
                v = PixOrCopy.CreateLiteral(pixel);
                colorCache.Set((uint)key, pixel);
            }
        }
        else
        {
            v = PixOrCopy.CreateLiteral(pixel);
        }

        refs.Add(v);
    }

    public static int DistanceToPlaneCode(int xSize, int dist)
    {
        int yOffset = dist / xSize;
        int xOffset = dist - (yOffset * xSize);
        if (xOffset <= 8 && yOffset < 8)
        {
            return (int)WebpLookupTables.PlaneToCodeLut[(yOffset * 16) + 8 - xOffset] + 1;
        }
        else if (xOffset > xSize - 8 && yOffset < 7)
        {
            return (int)WebpLookupTables.PlaneToCodeLut[((yOffset + 1) * 16) + 8 + (xSize - xOffset)] + 1;
        }

        return dist + 120;
    }
}
