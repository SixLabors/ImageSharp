// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;

namespace SixLabors.ImageSharp.Formats.WebP.Lossless
{
    internal class BackwardReferenceEncoder
    {
        /// <summary>
        /// Maximum bit length.
        /// </summary>
        public const int MaxLengthBits = 12;

        private const int HashBits = 18;

        private const int HashSize = 1 << HashBits;

        private const uint HashMultiplierHi = 0xc6a4a793u;

        private const uint HashMultiplierLo = 0x5bd1e996u;

        private const float MaxEntropy = 1e30f;

        private const int WindowOffsetsSizeMax = 32;

        /// <summary>
        /// Minimum block size for backward references.
        /// </summary>
        private const int MinBlockSize = 256;

        /// <summary>
        /// The number of bits for the window size.
        /// </summary>
        private const int WindowSizeBits = 20;

        /// <summary>
        /// 1M window (4M bytes) minus 120 special codes for short distances.
        /// </summary>
        private const int WindowSize = (1 << WindowSizeBits) - 120;

        /// <summary>
        /// We want the max value to be attainable and stored in MaxLengthBits bits.
        /// </summary>
        public const int MaxLength = (1 << MaxLengthBits) - 1;

        /// <summary>
        /// Minimum number of pixels for which it is cheaper to encode a
        /// distance + length instead of each pixel as a literal.
        /// </summary>
        private const int MinLength = 4;

        // TODO: move to Hashchain?
        public static void HashChainFill(Vp8LHashChain p, Span<uint> bgra, int quality, int xSize, int ySize)
        {
            int size = xSize * ySize;
            int iterMax = GetMaxItersForQuality(quality);
            int windowSize = GetWindowSizeForHashChain(quality, xSize);
            int pos;
            var hashToFirstIndex = new int[HashSize];  // TODO: use memory allocator

            // Initialize hashToFirstIndex array to -1.
            hashToFirstIndex.AsSpan().Fill(-1);

            var chain = new int[size]; // TODO: use memory allocator.

            // Fill the chain linking pixels with the same hash.
            var bgraComp = bgra[0] == bgra[1];
            for (pos = 0; pos < size - 2;)
            {
                uint hashCode;
                bool bgraCompNext = bgra[pos + 1] == bgra[pos + 2];
                if (bgraComp && bgraCompNext)
                {
                    // Consecutive pixels with the same color will share the same hash.
                    // We therefore use a different hash: the color and its repetition length.
                    var tmp = new uint[2];
                    uint len = 1;
                    tmp[0] = bgra[pos];

                    // Figure out how far the pixels are the same. The last pixel has a different 64 bit hash,
                    // as its next pixel does not have the same color, so we just need to get to
                    // the last pixel equal to its follower.
                    while (pos + (int)len + 2 < size && bgra[(int)(pos + len + 2)] == bgra[pos])
                    {
                        ++len;
                    }

                    if (len > MaxLength)
                    {
                        // Skip the pixels that match for distance=1 and length>MaxLength
                        // because they are linked to their predecessor and we automatically
                        // check that in the main for loop below. Skipping means setting no
                        // predecessor in the chain, hence -1.
                        pos += (int)(len - MaxLength);
                        len = MaxLength;
                    }

                    // Process the rest of the hash chain.
                    while (len > 0)
                    {
                        tmp[1] = len--;
                        hashCode = GetPixPairHash64(tmp);
                        chain[pos] = hashToFirstIndex[hashCode];
                        hashToFirstIndex[hashCode] = pos++;
                    }

                    bgraComp = false;
                }
                else
                {
                    // Just move one pixel forward.
                    hashCode = GetPixPairHash64(bgra.Slice(pos));
                    chain[pos] = hashToFirstIndex[hashCode];
                    hashToFirstIndex[hashCode] = pos++;
                    bgraComp = bgraCompNext;
                }
            }

            // Process the penultimate pixel.
            chain[pos] = hashToFirstIndex[GetPixPairHash64(bgra.Slice(pos))];

            // Find the best match interval at each pixel, defined by an offset to the
            // pixel and a length. The right-most pixel cannot match anything to the right
            // (hence a best length of 0) and the left-most pixel nothing to the left (hence an offset of 0).
            p.OffsetLength[0] = p.OffsetLength[size - 1] = 0;
            for (int basePosition = size - 2; basePosition > 0;)
            {
                int maxLen = MaxFindCopyLength(size - 1 - basePosition);
                int bgraStart = basePosition;
                int iter = iterMax;
                int bestLength = 0;
                uint bestDistance = 0;
                uint bestBgra;
                int minPos = (basePosition > windowSize) ? basePosition - windowSize : 0;
                int lengthMax = (maxLen < 256) ? maxLen : 256;
                uint maxBasePosition;
                pos = (int)chain[basePosition];
                int currLength;

                // Heuristic: use the comparison with the above line as an initialization.
                if (basePosition >= (uint)xSize)
                {
                    currLength = FindMatchLength(bgra.Slice(bgraStart - xSize), bgra.Slice(bgraStart), bestLength, maxLen);
                    if (currLength > bestLength)
                    {
                        bestLength = currLength;
                        bestDistance = (uint)xSize;
                    }

                    iter--;
                }

                // Heuristic: compare to the previous pixel.
                currLength = FindMatchLength(bgra.Slice(bgraStart - 1), bgra.Slice(bgraStart), bestLength, maxLen);
                if (currLength > bestLength)
                {
                    bestLength = currLength;
                    bestDistance = 1;
                }

                iter--;

                if (bestLength == MaxLength)
                {
                    pos = minPos - 1;
                }

                bestBgra = bgra.Slice(bgraStart)[bestLength];

                for (; pos >= minPos && (--iter > 0); pos = chain[pos])
                {
                    if (bgra[pos + bestLength] != bestBgra)
                    {
                        continue;
                    }

                    currLength = VectorMismatch(bgra.Slice(pos), bgra.Slice(bgraStart), maxLen);
                    if (bestLength < currLength)
                    {
                        bestLength = currLength;
                        bestDistance = (uint)(basePosition - pos);
                        bestBgra = bgra.Slice(bgraStart)[bestLength];

                        // Stop if we have reached a good enough length.
                        if (bestLength >= lengthMax)
                        {
                            break;
                        }
                    }
                }

                // We have the best match but in case the two intervals continue matching
                // to the left, we have the best matches for the left-extended pixels.
                maxBasePosition = (uint)basePosition;
                while (true)
                {
                    p.OffsetLength[basePosition] = (bestDistance << MaxLengthBits) | (uint)bestLength;
                    --basePosition;

                    // Stop if we don't have a match or if we are out of bounds.
                    if (bestDistance == 0 || basePosition == 0)
                    {
                        break;
                    }

                    // Stop if we cannot extend the matching intervals to the left.
                    if (basePosition < bestDistance || bgra[(int)(basePosition - bestDistance)] != bgra[basePosition])
                    {
                        break;
                    }

                    // Stop if we are matching at its limit because there could be a closer
                    // matching interval with the same maximum length. Then again, if the
                    // matching interval is as close as possible (best_distance == 1), we will
                    // never find anything better so let's continue.
                    if (bestLength == MaxLength && bestDistance != 1 && basePosition + MaxLength < maxBasePosition)
                    {
                        break;
                    }

                    if (bestLength < MaxLength)
                    {
                        bestLength++;
                        maxBasePosition = (uint)basePosition;
                    }
                }
            }
        }

        /// <summary>
        /// Evaluates best possible backward references for specified quality. The input cacheBits to 'GetBackwardReferences'
        /// sets the maximum cache bits to use (passing 0 implies disabling the local color cache).
        /// The optimal cache bits is evaluated and set for the cacheBits parameter.
        /// The return value is the pointer to the best of the two backward refs viz, refs[0] or refs[1].
        /// </summary>
        public static Vp8LBackwardRefs GetBackwardReferences(
            int width,
            int height,
            Span<uint> bgra,
            int quality,
            int lz77TypesToTry,
            ref int cacheBits,
            Vp8LHashChain hashChain,
            Vp8LBackwardRefs best,
            Vp8LBackwardRefs worst)
        {
            var histo = new Vp8LHistogram[WebPConstants.MaxColorCacheBits];
            int lz77TypeBest = 0;
            double bitCostBest = -1;
            int cacheBitsInitial = cacheBits;
            Vp8LHashChain hashChainBox = null;
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
                        hashChainBox = new Vp8LHashChain(width * height);
                        BackwardReferencesLz77Box(width, height, bgra, 0, hashChain, hashChainBox, worst);
                        break;
                }

                // Next, try with a color cache and update the references.
                cacheBitsTmp = CalculateBestCacheSize(bgra, quality, worst, cacheBitsTmp);
                if (cacheBitsTmp > 0)
                {
                    BackwardRefsWithLocalCache(bgra, cacheBitsTmp, worst);
                }

                // Keep the best backward references.
                histo[0] = new Vp8LHistogram(worst, cacheBitsTmp);
                var bitCost = histo[0].EstimateBits();

                if (lz77TypeBest == 0 || bitCost < bitCostBest)
                {
                    Vp8LBackwardRefs tmp = worst;
                    worst = best;
                    best = tmp;
                    bitCostBest = bitCost;
                    cacheBits = cacheBitsTmp;
                    lz77TypeBest = lz77Type;
                }
            }

            // Improve on simple LZ77 but only for high quality (TraceBackwards is costly).
            if ((lz77TypeBest == (int)Vp8LLz77Type.Lz77Standard || lz77TypeBest == (int)Vp8LLz77Type.Lz77Box) && quality >= 25)
            {
                Vp8LHashChain hashChainTmp = (lz77TypeBest == (int)Vp8LLz77Type.Lz77Standard) ? hashChain : hashChainBox;
                BackwardReferencesTraceBackwards(width, height, bgra, cacheBits, hashChainTmp, best, worst);
                histo[0] = new Vp8LHistogram(worst, cacheBits);
                double bitCostTrace = histo[0].EstimateBits();
                if (bitCostTrace < bitCostBest)
                {
                    best = worst;
                }
            }

            BackwardReferences2DLocality(width, best);

            return best;
        }

        /// <summary>
        /// Evaluate optimal cache bits for the local color cache.
        /// The input bestCacheBits sets the maximum cache bits to use (passing 0 implies disabling the local color cache).
        /// The local color cache is also disabled for the lower (smaller then 25) quality.
        /// </summary>
        /// <returns>Best cache size.</returns>
        private static int CalculateBestCacheSize(Span<uint> bgra, int quality, Vp8LBackwardRefs refs, int bestCacheBits)
        {
            int cacheBitsMax = (quality <= 25) ? 0 : bestCacheBits;
            if (cacheBitsMax == 0)
            {
                // Local color cache is disabled.
                return 0;
            }

            double entropyMin = MaxEntropy;
            int pos = 0;
            var colorCache = new ColorCache[WebPConstants.MaxColorCacheBits + 1];
            var histos = new Vp8LHistogram[WebPConstants.MaxColorCacheBits + 1];
            for (int i = 0; i <= WebPConstants.MaxColorCacheBits; i++)
            {
                histos[i] = new Vp8LHistogram(paletteCodeBits: i);
                colorCache[i] = new ColorCache();
                colorCache[i].Init(i);
            }

            // Find the cache_bits giving the lowest entropy.
            using List<PixOrCopy>.Enumerator c = refs.Refs.GetEnumerator();
            while (c.MoveNext())
            {
                PixOrCopy v = c.Current;
                if (v.IsLiteral())
                {
                    uint pix = bgra[pos++];
                    uint a = (pix >> 24) & 0xff;
                    uint r = (pix >> 16) & 0xff;
                    uint g = (pix >> 8) & 0xff;
                    uint b = (pix >> 0) & 0xff;

                    // The keys of the caches can be derived from the longest one.
                    int key = ColorCache.HashPix(pix, 32 - cacheBitsMax);

                    // Do not use the color cache for cacheBits = 0.
                    ++histos[0].Blue[b];
                    ++histos[0].Literal[g];
                    ++histos[0].Red[r];
                    ++histos[0].Alpha[a];

                    // Deal with cacheBits > 0.
                    for (int i = cacheBitsMax; i >= 1; i--, key >>= 1)
                    {
                        if (colorCache[i].Lookup(key) == pix)
                        {
                            ++histos[i].Literal[WebPConstants.NumLiteralCodes + WebPConstants.NumLengthCodes + key];
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
                    // histogram contributions, we can safely ignore them.
                    int len = v.Len;
                    uint bgraPrev = bgra[pos] ^ 0xffffffffu;

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

            for (int i = 0; i <= cacheBitsMax; i++)
            {
                double entropy = histos[i].EstimateBits();
                if (i == 0 || entropy < entropyMin)
                {
                    entropyMin = entropy;
                    bestCacheBits = i;
                }
            }

            return bestCacheBits;
        }

        private static void BackwardReferencesTraceBackwards(int xSize, int ySize, Span<uint> bgra, int cacheBits, Vp8LHashChain hashChain, Vp8LBackwardRefs refsSrc, Vp8LBackwardRefs refsDst)
        {
            int distArraySize = xSize * ySize;
            var distArray = new short[distArraySize];

            BackwardReferencesHashChainDistanceOnly(xSize, ySize, bgra, cacheBits, hashChain, refsSrc, distArray);
            int chosenPathSize = TraceBackwards(distArray, distArraySize);
            Span<short> chosenPath = distArray.AsSpan(distArraySize - chosenPathSize);
            BackwardReferencesHashChainFollowChosenPath(bgra, cacheBits, chosenPath, chosenPathSize, hashChain, refsDst);
        }

        private static void BackwardReferencesHashChainDistanceOnly(int xSize, int ySize, Span<uint> bgra, int cacheBits, Vp8LHashChain hashChain, Vp8LBackwardRefs refs, short[] distArray)
        {
            int pixCount = xSize * ySize;
            bool useColorCache = cacheBits > 0;
            var literalArraySize = WebPConstants.NumLiteralCodes + WebPConstants.NumLengthCodes + ((cacheBits > 0) ? (1 << cacheBits) : 0);
            var costModel = new CostModel(literalArraySize);
            int offsetPrev = -1;
            int lenPrev = -1;
            double offsetCost = -1;
            int firstOffsetIsConstant = -1;  // initialized with 'impossible' value
            int reach = 0;
            var colorCache = new ColorCache();

            if (useColorCache)
            {
                colorCache.Init(cacheBits);
            }

            costModel.Build(xSize, cacheBits, refs);
            var costManager = new CostManager(distArray, pixCount, costModel);

            // We loop one pixel at a time, but store all currently best points to
            // non-processed locations from this point.
            distArray[0] = 0;

            // Add first pixel as literal.
            AddSingleLiteralWithCostModel(bgra, colorCache, costModel, 0, useColorCache, 0.0f, costManager.Costs, distArray);

            for (int i = 1; i < pixCount; i++)
            {
                float prevCost = costManager.Costs[i - 1];
                int offset = hashChain.FindOffset(i);
                int len = hashChain.FindLength(i);

                // Try adding the pixel as a literal.
                AddSingleLiteralWithCostModel(bgra, colorCache, costModel, i, useColorCache, prevCost, costManager.Costs, distArray);

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
                            int offsetJ = 0;
                            int lenJ = 0;
                            int j;
                            for (j = i; j <= reach; ++j)
                            {
                                offset = hashChain.FindOffset(j + 1);
                                len = hashChain.FindLength(j + 1);
                                if (offsetJ != offset)
                                {
                                    offset = hashChain.FindOffset(j);
                                    len = hashChain.FindLength(j);
                                    break;
                                }
                            }

                            // Update the cost at j - 1 and j.
                            costManager.UpdateCostAtIndex(j - 1, false);
                            costManager.UpdateCostAtIndex(j, false);

                            costManager.PushInterval(costManager.Costs[j - 1] + offsetCost, j, lenJ);
                            reach = j + lenJ - 1;
                        }
                    }
                }

                costManager.UpdateCostAtIndex(i, true);
                offsetPrev = offset;
                lenPrev = len;
            }
        }

        private static int TraceBackwards(short[] distArray, int distArraySize)
        {
            int chosenPathSize = 0;
            int pathPos = distArraySize;
            int curPos = distArraySize - 1;
            while (curPos >= 0)
            {
                short cur = distArray[curPos];
                pathPos--;
                chosenPathSize++;
                distArray[pathPos] = cur;
                curPos -= cur;
            }

            return chosenPathSize;
        }

        private static void BackwardReferencesHashChainFollowChosenPath(Span<uint> bgra, int cacheBits, Span<short> chosenPath, int chosenPathSize, Vp8LHashChain hashChain, Vp8LBackwardRefs backwardRefs)
        {
            bool useColorCache = cacheBits > 0;
            var colorCache = new ColorCache();
            int i = 0;

            if (useColorCache)
            {
                colorCache.Init(cacheBits);
            }

            backwardRefs.Refs.Clear();
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
                            colorCache.Insert(bgra[i + k]);
                        }
                    }

                    i += len;
                }
                else
                {
                    PixOrCopy v;
                    int idx = useColorCache ? colorCache.Contains(bgra[i]) : -1;
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
                            colorCache.Insert(bgra[i]);
                        }

                        v = PixOrCopy.CreateLiteral(bgra[i]);
                    }

                    backwardRefs.Add(v);
                    i++;
                }
            }
        }

        private static void AddSingleLiteralWithCostModel(Span<uint> bgra, ColorCache colorCache, CostModel costModel, int idx, bool useColorCache, float prevCost, float[] cost, short[] distArray)
        {
            double costVal = prevCost;
            uint color = bgra[idx];
            int ix = useColorCache ? colorCache.Contains(color) : -1;
            if (ix >= 0)
            {
                double mul0 = 0.68;
                costVal += costModel.GetCacheCost((uint)ix) * mul0;
            }
            else
            {
                double mul1 = 0.82;
                if (useColorCache)
                {
                    colorCache.Insert(color);
                }

                costVal += costModel.GetLiteralCost(color) * mul1;
            }

            if (cost[idx] > costVal)
            {
                cost[idx] = (float)costVal;
                distArray[idx] = 1;  // only one is inserted.
            }
        }

        private static void BackwardReferencesLz77(int xSize, int ySize, Span<uint> bgra, int cacheBits, Vp8LHashChain hashChain, Vp8LBackwardRefs refs)
        {
            int iLastCheck = -1;
            bool useColorCache = cacheBits > 0;
            int pixCount = xSize * ySize;
            var colorCache = new ColorCache();
            if (useColorCache)
            {
                colorCache.Init(cacheBits);
            }

            refs.Refs.Clear();
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
                    int jMax = (i + lenIni >= pixCount) ? pixCount - 1 : i + lenIni;

                    // Only start from what we have not checked already.
                    iLastCheck = (i > iLastCheck) ? i : iLastCheck;

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
                        for (j = i; j < i + len; ++j)
                        {
                            colorCache.Insert(bgra[j]);
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
        private static void BackwardReferencesLz77Box(int xSize, int ySize, Span<uint> bgra, int cacheBits, Vp8LHashChain hashChainBest, Vp8LHashChain hashChain, Vp8LBackwardRefs refs)
        {
            int pixelCount = xSize * ySize;
            var windowOffsets = new int[WindowOffsetsSizeMax];
            var windowOffsetsNew = new int[WindowOffsetsSizeMax];
            int windowOffsetsSize = 0;
            int windowOffsetsNewSize = 0;
            var counts = new short[xSize * ySize];
            int bestOffsetPrev = -1;
            int bestLengthPrev = -1;

            // counts[i] counts how many times a pixel is repeated starting at position i.
            int i = pixelCount - 2;
            int countsPos = i;
            counts[countsPos + 1] = 1;
            for (; i >= 0; i--, countsPos--)
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
                for (int j = 0; j < windowOffsetsSize && !isReachable; ++j)
                {
                    isReachable |= windowOffsets[i] == windowOffsets[j] + 1;
                }

                if (!isReachable)
                {
                    windowOffsetsNew[windowOffsetsNewSize] = windowOffsets[i];
                    windowOffsetsNewSize++;
                }
            }

            hashChain.OffsetLength[0] = 0;
            for (i = 1; i < pixelCount; ++i)
            {
                int ind;
                int bestLength = hashChainBest.FindLength(i);
                int bestOffset = 0;
                bool doCompute = true;

                if (bestLength >= MaxLength)
                {
                    // Do not recompute the best match if we already have a maximal one in the window.
                    bestOffset = hashChainBest.FindOffset(i);
                    for (ind = 0; ind < windowOffsetsSize; ++ind)
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
                    bool usePrev = (bestLengthPrev > 1) && (bestLengthPrev < MaxLength);
                    int numInd = usePrev ? windowOffsetsNewSize : windowOffsetsSize;
                    bestLength = usePrev ? bestLengthPrev - 1 : 0;
                    bestOffset = usePrev ? bestOffsetPrev : 0;

                    // Find the longest match in a window around the pixel.
                    for (ind = 0; ind < numInd; ++ind)
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
                                currLength += (countsJOffset < countsJ) ? countsJOffset : countsJ;
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
                            else
                            {
                                bestLength = currLength;
                            }
                        }
                    }
                }

                if (bestLength <= MinLength)
                {
                    hashChain.OffsetLength[i] = 0;
                    bestOffsetPrev = 0;
                    bestLengthPrev = 0;
                }
                else
                {
                    hashChain.OffsetLength[i] = (uint)((bestOffset << MaxLengthBits) | bestLength);
                    bestOffsetPrev = bestOffset;
                    bestLengthPrev = bestLength;
                }
            }

            hashChain.OffsetLength[0] = 0;
            BackwardReferencesLz77(xSize, ySize, bgra, cacheBits, hashChain, refs);
        }

        private static void BackwardReferencesRle(int xSize, int ySize, Span<uint> bgra, int cacheBits, Vp8LBackwardRefs refs)
        {
            int pixelCount = xSize * ySize;
            bool useColorCache = cacheBits > 0;
            var colorCache = new ColorCache();

            if (useColorCache)
            {
                colorCache.Init(cacheBits);
            }

            refs.Refs.Clear();

            // Add first pixel as literal.
            AddSingleLiteral(bgra[0], useColorCache, colorCache, refs);
            int i = 1;
            while (i < pixelCount)
            {
                int maxLen = MaxFindCopyLength(pixelCount - i);
                int rleLen = FindMatchLength(bgra.Slice(i), bgra.Slice(i - 1), 0, maxLen);
                int prevRowLen = (i < xSize) ? 0 : FindMatchLength(bgra.Slice(i), bgra.Slice(i - xSize), 0, maxLen);
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
                        for (int k = 0; k < prevRowLen; k++)
                        {
                            colorCache.Insert(bgra[i + k]);
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

            if (useColorCache)
            {
                // TODO: VP8LColorCacheClear()?
            }
        }

        /// <summary>
        /// Update (in-place) backward references for the specified cacheBits.
        /// </summary>
        private static void BackwardRefsWithLocalCache(Span<uint> bgra, int cacheBits, Vp8LBackwardRefs refs)
        {
            int pixelIndex = 0;
            using List<PixOrCopy>.Enumerator c = refs.Refs.GetEnumerator();
            var colorCache = new ColorCache();
            colorCache.Init(cacheBits);
            while (c.MoveNext())
            {
                PixOrCopy v = c.Current;
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
                    for (int k = 0; k < v.Len; k++)
                    {
                        colorCache.Insert(bgra[pixelIndex++]);
                    }
                }
            }

            // TODO: VP8LColorCacheClear(colorCache)?
        }

        private static void BackwardReferences2DLocality(int xSize, Vp8LBackwardRefs refs)
        {
            using List<PixOrCopy>.Enumerator c = refs.Refs.GetEnumerator();
            while (c.MoveNext())
            {
                if (c.Current.IsCopy())
                {
                    int dist = (int)c.Current.BgraOrDistance;
                    int transformedDist = DistanceToPlaneCode(xSize, dist);
                    c.Current.BgraOrDistance = (uint)transformedDist;
                }
            }
        }

        private static void AddSingleLiteral(uint pixel, bool useColorCache, ColorCache colorCache, Vp8LBackwardRefs refs)
        {
            PixOrCopy v;
            if (useColorCache)
            {
                int key = colorCache.GetIndex(pixel);
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
                return (int)WebPLookupTables.PlaneToCodeLut[(yOffset * 16) + 8 - xOffset] + 1;
            }
            else if (xOffset > xSize - 8 && yOffset < 7)
            {
                return (int)WebPLookupTables.PlaneToCodeLut[((yOffset + 1) * 16) + 8 + (xSize - xOffset)] + 1;
            }

            return dist + 120;
        }

        /// <summary>
        /// Returns the exact index where array1 and array2 are different. For an index
        /// inferior or equal to bestLenMatch, the return value just has to be strictly
        /// inferior to best_lenMatch. The current behavior is to return 0 if this index
        /// is bestLenMatch, and the index itself otherwise.
        /// If no two elements are the same, it returns maxLimit.
        /// </summary>
        private static int FindMatchLength(Span<uint> array1, Span<uint> array2, int bestLenMatch, int maxLimit)
        {
            // Before 'expensive' linear match, check if the two arrays match at the
            // current best length index.
            if (array1[bestLenMatch] != array2[bestLenMatch])
            {
                return 0;
            }

            return VectorMismatch(array1, array2, maxLimit);
        }

        private static int VectorMismatch(Span<uint> array1, Span<uint> array2, int length)
        {
            int matchLen = 0;

            while (matchLen < length && array1[matchLen] == array2[matchLen])
            {
                matchLen++;
            }

            return matchLen;
        }

        /// <summary>
        /// Calculates the hash for a pixel pair.
        /// </summary>
        /// <param name="bgra">An Span with two pixels.</param>
        /// <returns>The hash.</returns>
        private static uint GetPixPairHash64(Span<uint> bgra)
        {
            uint key = bgra[1] * HashMultiplierHi;
            key += bgra[0] * HashMultiplierLo;
            key = key >> (32 - HashBits);
            return key;
        }

        /// <summary>
        /// Returns the maximum number of hash chain lookups to do for a
        /// given compression quality. Return value in range [8, 86].
        /// </summary>
        /// <param name="quality">The quality.</param>
        /// <returns>Number of hash chain lookups.</returns>
        private static int GetMaxItersForQuality(int quality)
        {
            return 8 + (quality * quality / 128);
        }

        private static int MaxFindCopyLength(int len)
        {
            return (len < MaxLength) ? len : MaxLength;
        }

        private static int GetWindowSizeForHashChain(int quality, int xSize)
        {
            int maxWindowSize = (quality > 75) ? WindowSize
                : (quality > 50) ? (xSize << 8)
                : (quality > 25) ? (xSize << 6)
                : (xSize << 4);

            return (maxWindowSize > WindowSize) ? WindowSize : maxWindowSize;
        }
    }
}
