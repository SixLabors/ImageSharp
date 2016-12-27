namespace ImageSharp.Formats.Jpg
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    internal unsafe struct DecoderScanProcessor
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct ComponentData
        {
            public Block8x8F Block;

            public Block8x8F Temp1;

            public Block8x8F Temp2;

            public Block8x8F QuantiazationTable;

            public UnzigData Unzig;

            //public fixed byte ScanData [3 * JpegDecoderCore.MaxComponents];

            //public fixed int Dc [JpegDecoderCore.MaxComponents];

            public static ComponentData Create()
            {
                ComponentData data = default(ComponentData);
                data.Unzig = UnzigData.Create();
                return data;
            }
        }

        public struct ComponentPointers
        {
            public Block8x8F* Block;

            public Block8x8F* Temp1;

            public Block8x8F* Temp2;

            public Block8x8F* QuantiazationTable;

            public int* Unzig;

            //public Scan* Scan;

            //public int* Dc;

            public ComponentPointers(ComponentData* basePtr)
            {
                this.Block = &basePtr->Block;
                this.Temp1 = &basePtr->Temp1;
                this.Temp2 = &basePtr->Temp2;
                this.QuantiazationTable = &basePtr->QuantiazationTable;
                this.Unzig = basePtr->Unzig.Data;
                //this.Scan = (Scan*) basePtr->ScanData;
                //this.Dc = basePtr->Dc;
            }
        }

        //private void ResetDc()
        //{
        //    Unsafe.InitBlock(this.Pointers.Dc, default(byte), sizeof(int) * JpegDecoderCore.MaxComponents);
        //}

        public int bx;

        public int by;

        public int zigStart;

        public int zigEnd;

        public int ah;

        public int al;

        public int mxx;

        public int myy;

        public int scanComponentCount;

        public ComponentData Data;

        public ComponentPointers Pointers;
        
        public static void Init(DecoderScanProcessor* p, JpegDecoderCore decoder, int remaining, Scan[] scan)
        {
            p->Data = ComponentData.Create();
            p->Pointers = new ComponentPointers(&p->Data);
            p->InitImpl(decoder, remaining, scan);
        }

        private void InitImpl(JpegDecoderCore decoder, int remaining, Scan[] scan)
        {
            if (decoder.ComponentCount == 0)
            {
                throw new ImageFormatException("Missing SOF marker");
            }

            if (remaining < 6 || 4 + (2 * decoder.ComponentCount) < remaining || remaining % 2 != 0)
            {
                throw new ImageFormatException("SOS has wrong length");
            }

            decoder.ReadFull(decoder.Temp, 0, remaining);
            this.scanComponentCount = decoder.Temp[0];

            int scanComponentCountX2 = 2 * this.scanComponentCount;
            if (remaining != 4 + scanComponentCountX2)
            {
                throw new ImageFormatException("SOS length inconsistent with number of components");
            }

            int totalHv = 0;

            for (int i = 0; i < this.scanComponentCount; i++)
            {
                this.ProcessScanImpl(decoder, i, ref scan[i], ref totalHv, scan);
            }
            // Section B.2.3 states that if there is more than one component then the
            // total H*V values in a scan must be <= 10.
            if (decoder.ComponentCount > 1 && totalHv > 10)
            {
                throw new ImageFormatException("Total sampling factors too large.");
            }

            this.zigEnd = Block8x8F.ScalarCount - 1;

            if (decoder.IsProgressive)
            {
                this.zigStart = decoder.Temp[1 + scanComponentCountX2];
                this.zigEnd = decoder.Temp[2 + scanComponentCountX2];
                this.ah = decoder.Temp[3 + scanComponentCountX2] >> 4;
                this.al = decoder.Temp[3 + scanComponentCountX2] & 0x0f;

                if ((this.zigStart == 0 && this.zigEnd != 0) || this.zigStart > this.zigEnd
                    || this.zigEnd >= Block8x8F.ScalarCount)
                {
                    throw new ImageFormatException("Bad spectral selection bounds");
                }

                if (this.zigStart != 0 && this.scanComponentCount != 1)
                {
                    throw new ImageFormatException("Progressive AC coefficients for more than one component");
                }

                if (this.ah != 0 && this.ah != this.al + 1)
                {
                    throw new ImageFormatException("Bad successive approximation values");
                }
            }

            // mxx and myy are the number of MCUs (Minimum Coded Units) in the image.
            int h0 = decoder.ComponentArray[0].HorizontalFactor;
            int v0 = decoder.ComponentArray[0].VerticalFactor;
            this.mxx = (decoder.ImageWidth + (8 * h0) - 1) / (8 * h0);
            this.myy = (decoder.ImageHeight + (8 * v0) - 1) / (8 * v0);

            if (decoder.IsProgressive)
            {
                for (int i = 0; i < this.scanComponentCount; i++)
                {
                    int compIndex = scan[i].Index;
                    if (decoder.ProgCoeffs[compIndex] == null)
                    {
                        int size = this.mxx * this.myy * decoder.ComponentArray[compIndex].HorizontalFactor
                                   * decoder.ComponentArray[compIndex].VerticalFactor;

                        decoder.ProgCoeffs[compIndex] = new Block8x8F[size];
                    }
                }
            }
        }

        private void ProcessScanImpl(JpegDecoderCore decoder, int i, ref Scan currentScan, ref int totalHv, Scan[] scan)
        {
            // Component selector.
            int cs = decoder.Temp[1 + (2 * i)];
            int compIndex = -1;
            for (int j = 0; j < decoder.ComponentCount; j++)
            {
                // Component compv = ;
                if (cs == decoder.ComponentArray[j].Identifier)
                {
                    compIndex = j;
                }
            }

            if (compIndex < 0)
            {
                throw new ImageFormatException("Unknown component selector");
            }

            currentScan.Index = (byte)compIndex;

            this.ProcessComponentImpl(decoder, i, ref currentScan, ref totalHv, ref decoder.ComponentArray[compIndex], scan);
        }


        private void ProcessComponentImpl(
            JpegDecoderCore decoder,
            int i,
            ref Scan currentScan,
            ref int totalHv,
            ref Component currentComponent, Scan[] scan)
        {
            // Section B.2.3 states that "the value of Cs_j shall be different from
            // the values of Cs_1 through Cs_(j-1)". Since we have previously
            // verified that a frame's component identifiers (C_i values in section
            // B.2.2) are unique, it suffices to check that the implicit indexes
            // into comp are unique.
            for (int j = 0; j < i; j++)
            {
                if (currentScan.Index == scan[j].Index)
                {
                    throw new ImageFormatException("Repeated component selector");
                }
            }

            totalHv += currentComponent.HorizontalFactor * currentComponent.VerticalFactor;

            currentScan.DcTableSelector = (byte)(decoder.Temp[2 + (2 * i)] >> 4);
            if (currentScan.DcTableSelector > HuffmanTree.MaxTh)
            {
                throw new ImageFormatException("Bad DC table selector value");
            }

            currentScan.AcTableSelector = (byte)(decoder.Temp[2 + (2 * i)] & 0x0f);
            if (currentScan.AcTableSelector > HuffmanTree.MaxTh)
            {
                throw new ImageFormatException("Bad AC table selector  value");
            }
        }

        public void ProcessBlocks(JpegDecoderCore decoder, Scan[] scan, ref int[] dc)
        {
            int blockCount = 0;
            int mcu = 0;
            byte expectedRst = JpegConstants.Markers.RST0;

            for (int my = 0; my < this.myy; my++)
            {
                for (int mx = 0; mx < this.mxx; mx++)
                {
                    for (int i = 0; i < this.scanComponentCount; i++)
                    {
                        int compIndex = scan[i].Index;
                        int hi = decoder.ComponentArray[compIndex].HorizontalFactor;
                        int vi = decoder.ComponentArray[compIndex].VerticalFactor;

                        for (int j = 0; j < hi * vi; j++)
                        {
                            // The blocks are traversed one MCU at a time. For 4:2:0 chroma
                            // subsampling, there are four Y 8x8 blocks in every 16x16 MCU.
                            // For a baseline 32x16 pixel image, the Y blocks visiting order is:
                            // 0 1 4 5
                            // 2 3 6 7
                            // For progressive images, the interleaved scans (those with component count > 1)
                            // are traversed as above, but non-interleaved scans are traversed left
                            // to right, top to bottom:
                            // 0 1 2 3
                            // 4 5 6 7
                            // Only DC scans (zigStart == 0) can be interleave AC scans must have
                            // only one component.
                            // To further complicate matters, for non-interleaved scans, there is no
                            // data for any blocks that are inside the image at the MCU level but
                            // outside the image at the pixel level. For example, a 24x16 pixel 4:2:0
                            // progressive image consists of two 16x16 MCUs. The interleaved scans
                            // will process 8 Y blocks:
                            // 0 1 4 5
                            // 2 3 6 7
                            // The non-interleaved scans will process only 6 Y blocks:
                            // 0 1 2
                            // 3 4 5
                            if (this.scanComponentCount != 1)
                            {
                                this.bx = (hi * mx) + (j % hi);
                                this.by = (vi * my) + (j / hi);
                            }
                            else
                            {
                                int q = this.mxx * hi;
                                this.bx = blockCount % q;
                                this.by = blockCount / q;
                                blockCount++;
                                if (this.bx * 8 >= decoder.ImageWidth || this.by * 8 >= decoder.ImageHeight)
                                {
                                    continue;
                                }
                            }

                            int qtIndex = decoder.ComponentArray[compIndex].Selector;

                            // TODO: Reading & processing blocks should be done in 2 separate loops. The second one could be parallelized. The first one could be async.

                            this.Data.QuantiazationTable = decoder.QuantizationTables[qtIndex];

                            //Load the previous partially decoded coefficients, if applicable.
                            if (decoder.IsProgressive)
                            {
                                int blockIndex = ((this.by * this.mxx) * hi) + this.bx;
                                this.Data.Block = decoder.ProgCoeffs[compIndex][blockIndex];
                            }
                            else
                            {
                                this.Data.Block.Clear();
                            }

                            this.ProcessBlockImpl(decoder, i, compIndex, hi, scan, ref dc);


                            //fixed (Block8x8F* qtp = &decoder.QuantizationTables[qtIndex])
                            //{
                            //    // Load the previous partially decoded coefficients, if applicable.
                            //    if (decoder.IsProgressive)
                            //    {
                            //        int blockIndex = ((@by * mxx) * hi) + bx;

                            //        fixed (Block8x8F* bp = &decoder.ProgCoeffs[compIndex][blockIndex])
                            //        {
                            //            Unsafe.CopyBlock(this.Pointers.Block, bp, (uint)sizeof(Block8x8F));
                            //        }
                            //    }
                            //    else
                            //    {
                            //        this.Data.Block.Clear();
                            //    }
                            //    decoder.ProcessBlockImpl(this.ah, this.Pointers.Block, this.Pointers.Temp1, this.Pointers.Temp2, this.Pointers.Unzig, scan, i, this.zigStart, this.zigEnd, this.al, dc, compIndex, this.@by, this.mxx, hi, this.bx, qtp);
                            //}


                        }

                        // for j
                    }

                    // for i
                    mcu++;

                    if (decoder.RestartInterval > 0 && mcu % decoder.RestartInterval == 0 && mcu < this.mxx * this.myy)
                    {
                        // A more sophisticated decoder could use RST[0-7] markers to resynchronize from corrupt input,
                        // but this one assumes well-formed input, and hence the restart marker follows immediately.
                        decoder.ReadFull(decoder.Temp, 0, 2);
                        if (decoder.Temp[0] != 0xff || decoder.Temp[1] != expectedRst)
                        {
                            throw new ImageFormatException("Bad RST marker");
                        }

                        expectedRst++;
                        if (expectedRst == JpegConstants.Markers.RST7 + 1)
                        {
                            expectedRst = JpegConstants.Markers.RST0;
                        }

                        // Reset the Huffman decoder.
                        decoder.Bits = default(Bits);

                        // Reset the DC components, as per section F.2.1.3.1.
                        //this.ResetDc();
                        dc = new int[JpegDecoderCore.MaxComponents];

                        // Reset the progressive decoder state, as per section G.1.2.2.
                        decoder.EobRun = 0;
                    }
                }

                // for mx
            }
        }

        /// <summary>
        ///     The AC table index
        /// </summary>
        internal const int AcTable = 1;

        /// <summary>
        ///     The DC table index
        /// </summary>
        internal const int DcTable = 0;

        private void Refine(JpegDecoderCore decoder, ref HuffmanTree h, int delta)
        {
            Block8x8F* b = this.Pointers.Block;
            // Refining a DC component is trivial.
            if (this.zigStart == 0)
            {
                if (this.zigEnd != 0)
                {
                    throw new ImageFormatException("Invalid state for zig DC component");
                }

                bool bit = decoder.DecodeBit();
                if (bit)
                {
                    int stuff = (int)Block8x8F.GetScalarAt(b, 0);

                    // int stuff = (int)b[0];
                    stuff |= delta;

                    // b[0] = stuff;
                    Block8x8F.SetScalarAt(b, 0, stuff);
                }

                return;
            }

            // Refining AC components is more complicated; see sections G.1.2.2 and G.1.2.3.
            int zig = this.zigStart;
            if (decoder.EobRun == 0)
            {
                for (; zig <= this.zigEnd; zig++)
                {
                    bool done = false;
                    int z = 0;
                    byte val = decoder.DecodeHuffman(ref h);
                    int val0 = val >> 4;
                    int val1 = val & 0x0f;

                    switch (val1)
                    {
                        case 0:
                            if (val0 != 0x0f)
                            {
                                decoder.EobRun = (ushort)(1 << val0);
                                if (val0 != 0)
                                {
                                    decoder.EobRun |= (ushort)decoder.DecodeBits(val0);
                                }

                                done = true;
                            }

                            break;
                        case 1:
                            z = delta;
                            bool bit = decoder.DecodeBit();
                            if (!bit)
                            {
                                z = -z;
                            }

                            break;
                        default:
                            throw new ImageFormatException("Unexpected Huffman code");
                    }

                    if (done)
                    {
                        break;
                    }

                    int blah = zig;

                    zig = this.RefineNonZeroes(decoder, zig, val0, delta);
                    if (zig > this.zigEnd)
                    {
                        throw new ImageFormatException($"Too many coefficients {zig} > {this.zigEnd}");
                    }

                    if (z != 0)
                    {
                        // b[Unzig[zig]] = z;
                        Block8x8F.SetScalarAt(b, this.Pointers.Unzig[zig], z);
                    }
                }
            }

            if (decoder.EobRun > 0)
            {
                decoder.EobRun--;
                this.RefineNonZeroes(decoder, zig,-1, delta);
            }
        }
        
        private int RefineNonZeroes(JpegDecoderCore decoder, int zig, int nz, int delta)
        {
            var b = this.Pointers.Block;
            for (; zig <= zigEnd; zig++)
            {
                int u = this.Pointers.Unzig[zig];
                float bu = Block8x8F.GetScalarAt(b, u);

                // TODO: Are the equality comparsions OK with floating point values? Isn't an epsilon value necessary?
                if (bu == 0)
                {
                    if (nz == 0)
                    {
                        break;
                    }

                    nz--;
                    continue;
                }

                bool bit = decoder.DecodeBit();
                if (!bit)
                {
                    continue;
                }

                if (bu >= 0)
                {
                    // b[u] += delta;
                    Block8x8F.SetScalarAt(b, u, bu + delta);
                }
                else
                {
                    // b[u] -= delta;
                    Block8x8F.SetScalarAt(b, u, bu - delta);
                }
            }

            return zig;
        }

        private void ProcessBlockImpl(JpegDecoderCore decoder, int i, int compIndex, int hi, Scan[] scan, ref int[] dc)
        {
            var b = this.Pointers.Block;
            //var dc = this.Pointers.Dc;
            int huffmannIdx = (AcTable * HuffmanTree.ThRowSize) + scan[i].AcTableSelector;
            if (this.ah != 0)
            {
                this.Refine(decoder, ref decoder.HuffmanTrees[huffmannIdx], 1 << this.al);
            }
            else
            {
                int zig = zigStart;
                if (zig == 0)
                {
                    zig++;

                    // Decode the DC coefficient, as specified in section F.2.2.1.
                    byte value =
                        decoder.DecodeHuffman(
                            ref decoder.HuffmanTrees[(DcTable * HuffmanTree.ThRowSize) + scan[i].DcTableSelector]);
                    if (value > 16)
                    {
                        throw new ImageFormatException("Excessive DC component");
                    }

                    int deltaDC = decoder.Bits.ReceiveExtend(value, decoder);
                    dc[compIndex] += deltaDC;

                    // b[0] = dc[compIndex] << al;
                    Block8x8F.SetScalarAt(b, 0, dc[compIndex] << al);
                }

                if (zig <= this.zigEnd && decoder.EobRun > 0)
                {
                    decoder.EobRun--;
                }
                else
                {
                    // Decode the AC coefficients, as specified in section F.2.2.2.
                    // Huffman huffv = ;
                    for (; zig <= this.zigEnd; zig++)
                    {
                        byte value = decoder.DecodeHuffman(ref decoder.HuffmanTrees[huffmannIdx]);
                        byte val0 = (byte)(value >> 4);
                        byte val1 = (byte)(value & 0x0f);
                        if (val1 != 0)
                        {
                            zig += val0;
                            if (zig > this.zigEnd)
                            {
                                break;
                            }

                            int ac = decoder.Bits.ReceiveExtend(val1, decoder);

                            // b[Unzig[zig]] = ac << al;
                            Block8x8F.SetScalarAt(b, this.Pointers.Unzig[zig], ac << this.al);
                        }
                        else
                        {
                            if (val0 != 0x0f)
                            {
                                decoder.EobRun = (ushort)(1 << val0);
                                if (val0 != 0)
                                {
                                    decoder.EobRun |= (ushort)decoder.DecodeBits(val0);
                                }

                                decoder.EobRun--;
                                break;
                            }

                            zig += 0x0f;
                        }
                    }
                }
            }

            if (decoder.IsProgressive)
            {
                if (this.zigEnd != Block8x8F.ScalarCount - 1 || this.al != 0)
                {
                    // We haven't completely decoded this 8x8 block. Save the coefficients. 
                    // this.ProgCoeffs[compIndex][((@by * mxx) * hi) + bx] = b.Clone();
                    decoder.ProgCoeffs[compIndex][((this.by * this.mxx) * hi) + this.bx] = *b;

                    // At this point, we could execute the rest of the loop body to dequantize and
                    // perform the inverse DCT, to save early stages of a progressive image to the
                    // *image.YCbCr buffers (the whole point of progressive encoding), but in Go,
                    // the jpeg.Decode function does not return until the entire image is decoded,
                    // so we "continue" here to avoid wasted computation.
                    return;
                }
            }

            // Dequantize, perform the inverse DCT and store the block to the image.
            Block8x8F.UnZig(b, this.Pointers.QuantiazationTable, this.Pointers.Unzig);

            DCT.TransformIDCT(ref *b, ref *this.Pointers.Temp1, ref *this.Pointers.Temp2);

            var destChannel = decoder.GetDestinationChannel(compIndex);
            var destArea = destChannel.GetOffsetedSubAreaForBlock(this.bx, this.by);
            destArea.LoadColorsFrom(this.Pointers.Temp1, this.Pointers.Temp2);
        }
    }
    
}