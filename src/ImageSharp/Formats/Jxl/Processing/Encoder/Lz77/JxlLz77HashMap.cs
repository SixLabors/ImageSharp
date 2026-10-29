// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Compression;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Lz77;

internal sealed class JxlLz77HashMap
{
    private readonly List<uint> data;
    private readonly uint hashMask;
    private readonly HashBucket[] hashTable;
    private readonly List<sbyte> specialDistTable = [];
    private readonly int bucketSize;
    private readonly int hashSize;
    private readonly int numSpecialDistances;

    public JxlLz77HashMap(List<uint> data, int distanceMultiplier, int bucketSize, int hashSize, int hashBits = 14)
    {
        this.data = data;
        this.bucketSize = bucketSize;
        this.hashSize = hashSize;
        this.hashMask = (1u << hashBits) - 1;
        this.hashTable = new HashBucket[(int)this.hashMask + 1];

        for (int i = 0; i < this.hashTable.Length; i++)
        {
            this.hashTable[i] = new HashBucket(bucketSize);
        }

        this.numSpecialDistances = 0;

        if (distanceMultiplier != 0)
        {
            this.numSpecialDistances = Lz77DistanceCodes.Count;
            this.specialDistTable = new List<sbyte>((distanceMultiplier * 8) + 8);

            for (int i = 0; i < (distanceMultiplier * 8) + 8; i++)
            {
                this.specialDistTable.Add(-1);
            }

            for (int i = 0; i < Lz77DistanceCodes.Count; i++)
            {
                this.specialDistTable[Lz77DistanceCodes.GetDistance(i, distanceMultiplier)] = (sbyte)i;
            }
        }
    }

    public void FindMatch(int pos, out int len, out int distSymbol, int minLen)
    {
        uint h = JxlLz77.GetHash(this.hashSize, pos, this.data) & this.hashMask;
        HashBucket bucket = this.hashTable[(int)h];

        len = 0;
        distSymbol = 0;

        for (int i = 0; i < bucket.Size; i++)
        {
            uint candidate = bucket.Data[i];
            int dist = pos - (int)candidate;
            int curLength = this.MatchLength((int)candidate, pos);

            if (curLength < len || curLength < minLen)
            {
                continue;
            }

            int curDistSymbol = this.numSpecialDistances + dist - 1;

            if (dist < this.specialDistTable.Count)
            {
                int lookup = this.specialDistTable[dist];

                if (lookup >= 0)
                {
                    curDistSymbol = lookup;
                }
            }

            if (curLength == len && curDistSymbol >= distSymbol)
            {
                continue;
            }

            len = curLength;
            distSymbol = curDistSymbol;
        }

        this.Insert(pos, (int)h);
    }

    public void Update(int pos)
    {
        uint h = JxlLz77.GetHash(this.hashSize, pos, this.data) & this.hashMask;
        this.Insert(pos, (int)h);
    }

    private void Insert(int pos, int hash)
    {
        ref HashBucket bucket = ref this.hashTable[hash];

        bucket.Size = Math.Min(bucket.Size + 1, this.bucketSize);
        bucket.Data[bucket.Index] = (uint)pos;
        bucket.Index = (bucket.Index + 1) % this.bucketSize;
    }

    /// <summary>
    /// Counts matching values from an earlier candidate to the end of the current sequence.
    /// </summary>
    /// <param name="a">The earlier candidate position.</param>
    /// <param name="b">The current position, strictly after a.</param>
    /// <returns>The number of equal leading values.</returns>
    private int MatchLength(int a, int b)
    {
        if (a >= b)
        {
            throw new InvalidOperationException("a must be less than b");
        }

        ReadOnlySpan<uint> values = CollectionsMarshal.AsSpan(this.data);

        // The later position bounds the match. Since a is earlier, both views cover that same number of values.
        return MemoryExtensions.CommonPrefixLength<uint>(values.Slice(a, values.Length - b), values[b..]);
    }

    private struct HashBucket
    {
        public HashBucket(int bucketSize)
        {
            this.Data = new uint[bucketSize];
            this.Index = 0;
            this.Size = 0;
        }

        public uint[] Data { get; set; }

        public int Index { get; set; }

        public int Size { get; set; }
    }
}
