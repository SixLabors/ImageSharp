// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

using System;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.WebP.Lossless
{
    internal class BackwardReferenceEncoder
    {
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
        /// Maximum bit length.
        /// </summary>
        private const int MaxLengthBits = 12;

        /// <summary>
        /// We want the max value to be attainable and stored in MaxLengthBits bits.
        /// </summary>
        private const int MaxLength = (1 << MaxLengthBits) - 1;

        /// <summary>
        /// Minimum number of pixels for which it is cheaper to encode a
        /// distance + length instead of each pixel as a literal.
        /// </summary>
        private const int MinLength = 4;

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
            // (hence a best length of 0) and the left-most pixel nothing to the left
            // (hence an offset of 0).
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

            int foo = 0;
        }

        /// <summary>
        /// Evaluates best possible backward references for specified quality.
        /// The input cache_bits to 'VP8LGetBackwardReferences' sets the maximum cache
        /// bits to use (passing 0 implies disabling the local color cache).
        /// The optimal cache bits is evaluated and set for the *cache_bits parameter.
        /// The return value is the pointer to the best of the two backward refs viz,
        /// refs[0] or refs[1].
        /// </summary>
        private static Vp8LBackwardRefs[] GetBackwardReferences(int width, int height, uint[] bgra, int quality,
            int lz77TypesToTry, int[] cacheBits, Vp8LHashChain[] hashChain, Vp8LBackwardRefs[] best, Vp8LBackwardRefs[] worst)
        {
            var histo = new Vp8LHistogram[WebPConstants.MaxColorCacheBits];
            int lz77Type = 0;
            int lz77TypeBest = 0;
            double bitCostBest = -1;
            int[] cacheBitsInitial = cacheBits;
            //  TODO: var hashChainBox = new Vp8LHashChain();
            for (lz77Type = 1; lz77TypesToTry > 0; lz77TypesToTry &= ~lz77Type, lz77Type <<= 1)
            {
                int res = 0;
                double bitCost;
                int[] cacheBitsTmp = cacheBitsInitial;
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
                        // Compute LZ77 with no cache (0 bits), as the ideal LZ77 with a color
                        // cache is not that different in practice.
                        BackwardReferencesLz77(width, height, bgra, 0, hashChain, worst);
                        break;
                    case Vp8LLz77Type.Lz77Box:
                        // TODO: HashChainInit(hashChainBox, width * height);
                        //BackwardReferencesLz77Box(width, height, bgra, 0, hashChain, hashChainBox, worst);
                        break;
                }

                // Next, try with a color cache and update the references.
                CalculateBestCacheSize(bgra, quality, worst, cacheBitsTmp);
                if (cacheBitsTmp[0] > 0)
                {
                    BackwardRefsWithLocalCache(bgra, cacheBitsTmp[0], worst);
                }

                // Keep the best backward references.
                // TODO: VP8LHistogramCreate(histo, worst, cacheBitsTmp);
                bitCost = histo[0].EstimateBits();

                if (lz77TypeBest == 0 || bitCost < bitCostBest)
                {
                    Vp8LBackwardRefs[] tmp = worst;
                    worst = best;
                    best = tmp;
                    bitCostBest = bitCost;
                    //*cacheBits = cacheBitsTmp;
                    lz77TypeBest = lz77Type;
                }
            }

            // Improve on simple LZ77 but only for high quality (TraceBackwards is costly).
            if ((lz77TypeBest == (int)Vp8LLz77Type.Lz77Standard || lz77TypeBest == (int)Vp8LLz77Type.Lz77Box) && quality >= 25)
            {
                /*HashChain[] hashChainTmp = (lz77TypeBest == (int)Vp8LLz77Type.Lz77Standard) ? hashChain : hashChainBox;
                if (BackwardReferencesTraceBackwards(width, height, bgra, cacheBits, hashChainTmp, best, worst))
                {
                    double bitCostTrace;
                    //HistogramCreate(histo, worst, cacheBits);
                    bitCostTrace = histo[0].EstimateBits();
                    if (bitCostTrace < bitCostBest)
                    {
                        best = worst;
                    }
                }*/
            }

            BackwardReferences2DLocality(width, best);

            return best;
        }

        /// <summary>
        /// Evaluate optimal cache bits for the local color cache.
        /// The input *best_cache_bits sets the maximum cache bits to use (passing 0
        /// implies disabling the local color cache). The local color cache is also
        /// disabled for the lower (<= 25) quality.
        /// </summary>
        private static void CalculateBestCacheSize(uint[] bgra, int quality, Vp8LBackwardRefs[] refs, int[] bestCacheBits)
        {
            int cacheBitsMax = (quality <= 25) ? 0 : bestCacheBits[0];
            double entropyMin = MaxEntropy;
            var ccInit = new int[WebPConstants.MaxColorCacheBits + 1];
            var hashers = new ColorCache[WebPConstants.MaxColorCacheBits + 1];
            var c = new Vp8LRefsCursor(refs);
            var histos = new Vp8LHistogram[WebPConstants.MaxColorCacheBits + 1];
            if (cacheBitsMax == 0)
            {
                // Local color cache is disabled.
                bestCacheBits[0] = 0;
                return;
            }

            // Find the cache_bits giving the lowest entropy. The search is done in a
            // brute-force way as the function (entropy w.r.t cache_bits) can be anything in practice.
            //while (VP8LRefsCursorOk(&c))
            /*while (true)
            {
                //PixOrCopy[] v = c.cur_pos;
                if (v.IsLiteral())
                {
                    uint pix = *bgra++;
                    uint a = (pix >> 24) & 0xff;
                    uint r = (pix >> 16) & 0xff;
                    uint g = (pix >> 8) & 0xff;
                    uint b = (pix >> 0) & 0xff;

                    // The keys of the caches can be derived from the longest one.
                    int key = HashPix(pix, 32 - cacheBitsMax);

                    // Do not use the color cache for cache_bits = 0.
                    ++histos[0].blue[b];
                    ++histos[0].literal[g];
                    ++histos[0].red[r];
                    ++histos[0].alpha[a];

                    // Deal with cache_bits > 0.
                    for (int i = cacheBitsMax; i >= 1; --i, key >>= 1)
                    {
                        if (VP8LColorCacheLookup(hashers[i], key) == pix)
                        {
                            ++histos[i]->literal[WebPConstants.NumLiteralCodes + WebPConstants.CodeLengthCodes + key];
                        }
                        else
                        {
                            VP8LColorCacheSet(hashers[i], key, pix);
                            ++histos[i].blue[b];
                            ++histos[i].literal[g];
                            ++histos[i].red[r];
                            ++histos[i].alpha[a];
                        }
                    }
                }
                else
                {
                    // We should compute the contribution of the (distance,length)
                    // histograms but those are the same independently from the cache size.
                    // As those constant contributions are in the end added to the other
                    // histogram contributions, we can safely ignore them.

                }
            }*/
        }

        private static void BackwardReferencesTraceBackwards()
        {

        }

        private static void BackwardReferencesLz77(int xSize, int ySize, uint[] bgra, int cacheBits, Vp8LHashChain[] hashChain, Vp8LBackwardRefs[] refs)
        {
            int iLastCheck = -1;
            int ccInit = 0;
            bool useColorCache = cacheBits > 0;
            int pixCount = xSize * ySize;
            var hashers = new ColorCache();
            if (useColorCache)
            {
                hashers.Init(cacheBits);
            }

            // TODO: VP8LClearBackwardRefs(refs);
            for (int i = 0; i < pixCount;)
            {
                // Alternative #1: Code the pixels starting at 'i' using backward reference.
                int offset = 0;
                int len = 0;
                int j;
                // TODO: VP8LHashChainFindCopy(hashChain, i, offset, ref len);
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
                        int lenJ = 0; // TODO: HashChainFindLength(hashChain, j);
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
                /*if (len == 1)
                {
                    AddSingleLiteral(bgra[i], useColorCache, hashers, refs);
                }
                else
                {
                    VP8LBackwardRefsCursorAdd(refs, PixOrCopyCreateCopy(offset, len));
                    if (useColorCache)
                    {
                        for (j = i; j < i + len; ++j)
                        {
                            VP8LColorCacheInsert(hashers, bgra[j]);
                        }
                    }
                }
                */
                i += len;
            }
        }

        /// <summary>
        /// Compute an LZ77 by forcing matches to happen within a given distance cost.
        /// We therefore limit the algorithm to the lowest 32 values in the PlaneCode definition.
        /// </summary>
        private static void BackwardReferencesLz77Box(int xSize, int ySize, uint[] bgra, int cacheBits, Vp8LHashChain[] hashChainBest, Vp8LHashChain[] hashChain, Vp8LBackwardRefs[] refs)
        {
            int i;
            int pixCount = xSize * ySize;
            short[] counts;
            var windowOffsets = new int[WindowOffsetsSizeMax];
            var windowOffsetsNew = new int[WindowOffsetsSizeMax];
            int windowOffsetsSize = 0;
            int windowOffsetsNewSize = 0;
            short[] countsIni = new short[xSize * ySize];
            int bestOffsetPrev = -1;
            int bestLengthPrev = -1;

            // counts[i] counts how many times a pixel is repeated starting at position i.
            i = pixCount - 2;
            /*counts = countsIni + i;
            counts[1] = 1;
            for (; i >= 0; i--, counts--)
            {
                if (bgra[i] == bgra[i + 1])
                {
                    // Max out the counts to MAX_LENGTH.
                    counts[0] = counts[1] + (counts[1] != MaxLength);
                }
                else
                {
                    counts[0] = 1;
                }
            }*/
        }

        private static void BackwardReferencesRle(int xSize, int ySize, uint[] bgra, int cacheBits, Vp8LBackwardRefs[] refs)
        {
            int pixCount = xSize * ySize;
            int i, k;
            bool useColorCache = cacheBits > 0;
        }

        /// <summary>
        /// Update (in-place) backward references for the specified cacheBits.
        /// </summary>
        private static void BackwardRefsWithLocalCache(uint[] bgra, int cacheBits, Vp8LBackwardRefs[] refs)
        {
            int pixelIndex = 0;
            var c = new Vp8LRefsCursor(refs);
            var hashers = new ColorCache();
            hashers.Init(cacheBits);
            //while (VP8LRefsCursorOk(&c))
            /*while (true)
            {
                PixOrCopy[] v = c.curPos;
                if (v.IsLiteral())
                {
                    uint bgraLiteral = v.BgraOrDistance;
                    int ix = VP8LColorCacheContains(hashers, bgraLiteral);
                    if (ix >= 0)
                    {
                        // hashers contains bgraLiteral
                        v = PixOrCopyCreateCacheIdx(ix);
                    }
                    else
                    {
                        VP8LColorCacheInsert(hashers, bgraLiteral);
                    }

                    pixelIndex++;
                }
                else
                {
                    // refs was created without local cache, so it can not have cache indexes.
                    for (int k = 0; k < v.len; k++)
                    {
                        VP8LColorCacheInsert(hashers, bgra[pixelIndex++]);
                    }
                }

                VP8LRefsCursorNext(c);
            }

            VP8LColorCacheClear(hashers);*/
        }

        private static void BackwardReferences2DLocality(int xSize, Vp8LBackwardRefs[] refs)
        {
            var c = new Vp8LRefsCursor(refs);
            /*while (VP8LRefsCursorOk(&c))
            {
                if (c.cur_pos.IsCopy())
                {
                    int dist = c.curPos.ArgbOrDistance;
                    int transformedDist = DistanceToPlaneCode(xSize, dist);
                    c.curPos.ArgbOrDistance = transformedDist;
                }

                VP8LRefsCursorNext(&c);
            }*/
        }

        private static int DistanceToPlaneCode(int xSize, int dist)
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
        /// inferior or equal to best_len_match, the return value just has to be strictly
        /// inferior to best_len_match. The current behavior is to return 0 if this index
        /// is best_len_match, and the index itself otherwise.
        /// If no two elements are the same, it returns max_limit.
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
