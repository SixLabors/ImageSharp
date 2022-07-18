// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Webp.Lossless
{
    internal sealed class Vp8LHashChain : IDisposable
    {
        private const uint HashMultiplierHi = 0xc6a4a793u;

        private const uint HashMultiplierLo = 0x5bd1e996u;

        private const int HashBits = 18;

        private const int HashSize = 1 << HashBits;

        /// <summary>
        /// The number of bits for the window size.
        /// </summary>
        private const int WindowSizeBits = 20;

        /// <summary>
        /// 1M window (4M bytes) minus 120 special codes for short distances.
        /// </summary>
        private const int WindowSize = (1 << WindowSizeBits) - 120;

        private readonly MemoryAllocator memoryAllocator;

        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8LHashChain"/> class.
        /// </summary>
        /// <param name="memoryAllocator">The memory allocator.</param>
        /// <param name="size">The size off the chain.</param>
        public Vp8LHashChain(MemoryAllocator memoryAllocator, int size)
        {
            this.memoryAllocator = memoryAllocator;
            this.OffsetLength = this.memoryAllocator.Allocate<uint>(size, AllocationOptions.Clean);
            this.Size = size;
        }

        /// <summary>
        /// Gets the offset length.
        /// The 20 most significant bits contain the offset at which the best match is found.
        /// These 20 bits are the limit defined by GetWindowSizeForHashChain (through WindowSize = 1 &lt;&lt; 20).
        /// The lower 12 bits contain the length of the match.
        /// </summary>
        public IMemoryOwner<uint> OffsetLength { get; }

        /// <summary>
        /// Gets the size of the hash chain.
        /// This is the maximum size of the hashchain that can be constructed.
        /// Typically this is the pixel count (width x height) for a given image.
        /// </summary>
        public int Size { get; }

        public void Fill(ReadOnlySpan<uint> bgra, int quality, int xSize, int ySize, bool lowEffort)
        {
            int size = xSize * ySize;
            int iterMax = GetMaxItersForQuality(quality);
            int windowSize = GetWindowSizeForHashChain(quality, xSize);
            int pos;

            if (size <= 2)
            {
                this.OffsetLength.GetSpan()[0] = 0;
                return;
            }

            using IMemoryOwner<int> hashToFirstIndexBuffer = this.memoryAllocator.Allocate<int>(HashSize);
            using IMemoryOwner<int> chainBuffer = this.memoryAllocator.Allocate<int>(size, AllocationOptions.Clean);
            Span<int> hashToFirstIndex = hashToFirstIndexBuffer.GetSpan();
            Span<int> chain = chainBuffer.GetSpan();

            // Initialize hashToFirstIndex array to -1.
            hashToFirstIndex.Fill(-1);

            // Fill the chain linking pixels with the same hash.
            bool bgraComp = bgra.Length > 1 && bgra[0] == bgra[1];
            Span<uint> tmp = stackalloc uint[2];
            for (pos = 0; pos < size - 2;)
            {
                uint hashCode;
                bool bgraCompNext = bgra[pos + 1] == bgra[pos + 2];
                if (bgraComp && bgraCompNext)
                {
                    // Consecutive pixels with the same color will share the same hash.
                    // We therefore use a different hash: the color and its repetition length.
                    tmp.Clear();
                    uint len = 1;
                    tmp[0] = bgra[pos];

                    // Figure out how far the pixels are the same. The last pixel has a different 64 bit hash,
                    // as its next pixel does not have the same color, so we just need to get to
                    // the last pixel equal to its follower.
                    while (pos + (int)len + 2 < size && bgra[(int)(pos + len + 2)] == bgra[pos])
                    {
                        ++len;
                    }

                    if (len > BackwardReferenceEncoder.MaxLength)
                    {
                        // Skip the pixels that match for distance=1 and length>MaxLength
                        // because they are linked to their predecessor and we automatically
                        // check that in the main for loop below. Skipping means setting no
                        // predecessor in the chain, hence -1.
                        pos += (int)(len - BackwardReferenceEncoder.MaxLength);
                        len = BackwardReferenceEncoder.MaxLength;
                    }

                    // Process the rest of the hash chain.
                    while (len > 0)
                    {
                        tmp[1] = len--;
                        hashCode = GetPixPairHash64(tmp);
                        chain[pos] = hashToFirstIndex[(int)hashCode];
                        hashToFirstIndex[(int)hashCode] = pos++;
                    }

                    bgraComp = false;
                }
                else
                {
                    // Just move one pixel forward.
                    hashCode = GetPixPairHash64(bgra.Slice(pos));
                    chain[pos] = hashToFirstIndex[(int)hashCode];
                    hashToFirstIndex[(int)hashCode] = pos++;
                    bgraComp = bgraCompNext;
                }
            }

            // Process the penultimate pixel.
            chain[pos] = hashToFirstIndex[(int)GetPixPairHash64(bgra.Slice(pos))];

            // Find the best match interval at each pixel, defined by an offset to the
            // pixel and a length. The right-most pixel cannot match anything to the right
            // (hence a best length of 0) and the left-most pixel nothing to the left (hence an offset of 0).
            Span<uint> offsetLength = this.OffsetLength.GetSpan();
            offsetLength[0] = offsetLength[size - 1] = 0;
            for (int basePosition = size - 2; basePosition > 0;)
            {
                int maxLen = LosslessUtils.MaxFindCopyLength(size - 1 - basePosition);
                int bgraStart = basePosition;
                int iter = iterMax;
                int bestLength = 0;
                uint bestDistance = 0;
                int minPos = basePosition > windowSize ? basePosition - windowSize : 0;
                int lengthMax = maxLen < 256 ? maxLen : 256;
                pos = chain[basePosition];
                int currLength;

                if (!lowEffort)
                {
                    // Heuristic: use the comparison with the above line as an initialization.
                    if (basePosition >= (uint)xSize)
                    {
                        currLength = LosslessUtils.FindMatchLength(bgra.Slice(bgraStart - xSize), bgra.Slice(bgraStart), bestLength, maxLen);
                        if (currLength > bestLength)
                        {
                            bestLength = currLength;
                            bestDistance = (uint)xSize;
                        }

                        iter--;
                    }

                    // Heuristic: compare to the previous pixel.
                    currLength = LosslessUtils.FindMatchLength(bgra.Slice(bgraStart - 1), bgra.Slice(bgraStart), bestLength, maxLen);
                    if (currLength > bestLength)
                    {
                        bestLength = currLength;
                        bestDistance = 1;
                    }

                    iter--;

                    // Skip the for loop if we already have the maximum.
                    if (bestLength == BackwardReferenceEncoder.MaxLength)
                    {
                        pos = minPos - 1;
                    }
                }

                uint bestBgra = bgra.Slice(bgraStart)[bestLength];

                for (; pos >= minPos && (--iter > 0); pos = chain[pos])
                {
                    if (bgra[pos + bestLength] != bestBgra)
                    {
                        continue;
                    }

                    currLength = LosslessUtils.VectorMismatch(bgra.Slice(pos), bgra.Slice(bgraStart), maxLen);
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
                uint maxBasePosition = (uint)basePosition;
                while (true)
                {
                    offsetLength[basePosition] = (bestDistance << BackwardReferenceEncoder.MaxLengthBits) | (uint)bestLength;
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
                    if (bestLength == BackwardReferenceEncoder.MaxLength && bestDistance != 1 && basePosition + BackwardReferenceEncoder.MaxLength < maxBasePosition)
                    {
                        break;
                    }

                    if (bestLength < BackwardReferenceEncoder.MaxLength)
                    {
                        bestLength++;
                        maxBasePosition = (uint)basePosition;
                    }
                }
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public int FindLength(int basePosition) => (int)(this.OffsetLength.GetSpan()[basePosition] & ((1U << BackwardReferenceEncoder.MaxLengthBits) - 1));

        [MethodImpl(InliningOptions.ShortMethod)]
        public int FindOffset(int basePosition) => (int)(this.OffsetLength.GetSpan()[basePosition] >> BackwardReferenceEncoder.MaxLengthBits);

        /// <summary>
        /// Calculates the hash for a pixel pair.
        /// </summary>
        /// <param name="bgra">An Span with two pixels.</param>
        /// <returns>The hash.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        private static uint GetPixPairHash64(ReadOnlySpan<uint> bgra)
        {
            uint key = bgra[1] * HashMultiplierHi;
            key += bgra[0] * HashMultiplierLo;
            key >>= 32 - HashBits;
            return key;
        }

        /// <summary>
        /// Returns the maximum number of hash chain lookups to do for a
        /// given compression quality. Return value in range [8, 86].
        /// </summary>
        /// <param name="quality">The quality.</param>
        /// <returns>Number of hash chain lookups.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        private static int GetMaxItersForQuality(int quality) => 8 + (quality * quality / 128);

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int GetWindowSizeForHashChain(int quality, int xSize)
        {
            int maxWindowSize = quality > 75 ? WindowSize
                : quality > 50 ? xSize << 8
                : quality > 25 ? xSize << 6
                : xSize << 4;

            return maxWindowSize > WindowSize ? WindowSize : maxWindowSize;
        }

        /// <inheritdoc />
        public void Dispose() => this.OffsetLength.Dispose();
    }
}
