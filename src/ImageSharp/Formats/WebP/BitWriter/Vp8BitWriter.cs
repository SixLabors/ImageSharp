// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.WebP.Lossy;

namespace SixLabors.ImageSharp.Formats.WebP.BitWriter
{
    /// <summary>
    /// A bit writer for writing lossy webp streams.
    /// </summary>
    internal class Vp8BitWriter : BitWriterBase
    {
        private int range;

        private int value;

        /// <summary>
        /// Number of outstanding bits.
        /// </summary>
        private int run;

        /// <summary>
        /// Number of pending bits.
        /// </summary>
        private int nbBits;

        private uint pos;

        private int maxPos;

        // private bool error;

        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8BitWriter"/> class.
        /// </summary>
        /// <param name="expectedSize">The expected size in bytes.</param>
        public Vp8BitWriter(int expectedSize)
            : base(expectedSize)
        {
            this.range = 255 - 1;
            this.value = 0;
            this.run = 0;
            this.nbBits = -8;
            this.pos = 0;
            this.maxPos = 0;

            // this.error = false;
        }

        public uint Pos
        {
            get { return this.pos; }
        }

        public int PutCoeffs(int ctx, Vp8Residual residual)
        {
            int tabIdx = 0;
            int n = residual.First;
            Vp8ProbaArray p = residual.Prob[n][ctx];
            if (!this.PutBit(residual.Last >= 0, p.Probabilities[0]))
            {
                return 0;
            }

            while (n < 16)
            {
                int c = residual.Coeffs[n++];
                bool sign = c < 0;
                int v = sign ? -c : c;
                if (!this.PutBit(v != 0, p.Probabilities[1]))
                {
                    p = residual.Prob[WebPConstants.Bands[n]][0];
                    continue;
                }

                if (!this.PutBit(v > 1, p.Probabilities[2]))
                {
                    p = residual.Prob[WebPConstants.Bands[n]][1];
                }
                else
                {
                    if (!this.PutBit(v > 4, p.Probabilities[3]))
                    {
                        if (this.PutBit(v != 2, p.Probabilities[4]))
                        {
                            this.PutBit(v == 4, p.Probabilities[5]);
                        }
                    }
                    else if (!this.PutBit(v > 10, p.Probabilities[6]))
                    {
                        if (!this.PutBit(v > 6, p.Probabilities[7]))
                        {
                            this.PutBit(v == 6, 159);
                        }
                        else
                        {
                            this.PutBit(v >= 9, 165);
                            this.PutBit(!((v & 1) != 0), 145);
                        }
                    }
                    else
                    {
                        int mask;
                        byte[] tab;
                        if (v < 3 + (8 << 1))
                        {
                            // VP8Cat3  (3b)
                            this.PutBit(0, p.Probabilities[8]);
                            this.PutBit(0, p.Probabilities[9]);
                            v -= 3 + (8 << 0);
                            mask = 1 << 2;
                            tab = WebPConstants.Cat3;
                            tabIdx = 0;
                        }
                        else if (v < 3 + (8 << 2))
                        {
                            // VP8Cat4  (4b)
                            this.PutBit(0, p.Probabilities[8]);
                            this.PutBit(1, p.Probabilities[9]);
                            v -= 3 + (8 << 1);
                            mask = 1 << 3;
                            tab = WebPConstants.Cat4;
                            tabIdx = 0;
                        }
                        else if (v < 3 + (8 << 3))
                        {
                            // VP8Cat5  (5b)
                            this.PutBit(1, p.Probabilities[8]);
                            this.PutBit(0, p.Probabilities[10]);
                            v -= 3 + (8 << 2);
                            mask = 1 << 4;
                            tab = WebPConstants.Cat5;
                            tabIdx = 0;
                        }
                        else
                        {
                            // VP8Cat6 (11b)
                            this.PutBit(1, p.Probabilities[8]);
                            this.PutBit(1, p.Probabilities[10]);
                            v -= 3 + (8 << 3);
                            mask = 1 << 10;
                            tab = WebPConstants.Cat6;
                            tabIdx = 0;
                        }

                        while (mask != 0)
                        {
                            this.PutBit(v & mask, tab[tabIdx++]);
                            mask >>= 1;
                        }
                    }

                    p = residual.Prob[WebPConstants.Bands[n]][2];
                }

                this.PutBitUniform(sign ? 1 : 0);
                if (n == 16 || !this.PutBit(n <= residual.Last, p.Probabilities[0]))
                {
                    return 1;   // EOB
                }
            }

            return 1;
        }

        private bool PutBit(bool bit, int prob)
        {
            return this.PutBit(bit ? 1 : 0, prob);
        }

        private bool PutBit(int bit, int prob)
        {
            int split = (this.range * prob) >> 8;
            if (bit != 0)
            {
                this.value += split + 1;
                this.range -= split + 1;
            }
            else
            {
                this.range = split;
            }

            if (this.range < 127)
            {
                // emit 'shift' bits out and renormalize.
                int shift = WebPLookupTables.Norm[this.range];
                this.range = WebPLookupTables.NewRange[this.range];
                this.value <<= shift;
                this.nbBits += shift;
                if (this.nbBits > 0)
                {
                    this.Flush();
                }
            }

            return bit != 0;
        }

        private int PutBitUniform(int bit)
        {
            int split = this.range >> 1;
            if (bit != 0)
            {
                this.value += split + 1;
                this.range -= split + 1;
            }
            else
            {
                this.range = split;
            }

            if (this.range < 127)
            {
                this.range = WebPLookupTables.NewRange[this.range];
                this.value <<= 1;
                this.nbBits += 1;
                if (this.nbBits > 0)
                {
                    this.Flush();
                }
            }

            return bit;
        }

        private void Flush()
        {
            int s = 8 + this.nbBits;
            int bits = this.value >> s;
            this.value -= bits << s;
            this.nbBits -= 8;
            if ((bits & 0xff) != 0xff)
            {
                var pos = this.pos;
                this.BitWriterResize(this.run + 1);

                if ((bits & 0x100) != 0)
                {
                    // overflow -> propagate carry over pending 0xff's
                    if (pos > 0)
                    {
                         this.Buffer[pos - 1]++;
                    }
                }

                if (this.run > 0)
                {
                    int value = (bits & 0x100) != 0 ? 0x00 : 0xff;
                    for (; this.run > 0; --this.run)
                    {
                        this.Buffer[pos++] = (byte)value;
                    }
                }

                this.Buffer[pos++] = (byte)(bits & 0xff);
                this.pos = pos;
            }
            else
            {
                this.run++;   // Delay writing of bytes 0xff, pending eventual carry.
            }
        }

        /// <summary>
        /// Resizes the buffer to write to.
        /// </summary>
        /// <param name="extraSize">The extra size in bytes needed.</param>
        public override void BitWriterResize(int extraSize)
        {
            // TODO: review again if this works as intended. Probably needs a unit test ...
            var neededSize = this.pos + extraSize;
            if (neededSize <= this.maxPos)
            {
                return;
            }

            this.ResizeBuffer(this.maxPos, (int)neededSize);
        }
    }
}
