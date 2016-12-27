namespace ImageSharp.Formats.Jpg
{
    using System;

    internal unsafe struct DecoderScanProcessor
    {
        public struct ComponentData
        {
            public Block8x8F Block;

            public Block8x8F Temp1;

            public Block8x8F Temp2;

            public UnzigData Unzig;

            public fixed byte ScanData [3 * JpegDecoderCore.MaxComponents];

            public fixed int Dc [JpegDecoderCore.MaxComponents];

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

            public int* Unzig;

            public Scan* Scan;

            public int* Dc;

            public ComponentPointers(ComponentData* basePtr)
            {
                this.Block = &basePtr->Block;
                this.Temp1 = &basePtr->Temp1;
                this.Temp2 = &basePtr->Temp2;
                this.Unzig = basePtr->Unzig.Data;
                this.Scan = (Scan*) basePtr->ScanData;
                this.Dc = basePtr->Dc;
            }
        }
        
        public int bx;

        public int by;

        public int zigStart;

        public int zigEnd;

        public int ah;

        public int al;

        public int mxx;

        public int myy;

        public ComponentData Data;

        public ComponentPointers Pointers;
        
        public static void Init(DecoderScanProcessor* p, JpegDecoderCore decoder, int remaining)
        {
            p->Pointers = new ComponentPointers(&p->Data);
            
        }

        private void InitCommon(JpegDecoderCore decoder, int remaining)
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
            byte scanComponentCount = decoder.Temp[0];

            int scanComponentCountX2 = 2 * scanComponentCount;
            if (remaining != 4 + scanComponentCountX2)
            {
                throw new ImageFormatException("SOS length inconsistent with number of components");
            }

            int totalHv = 0;

            for (int i = 0; i < scanComponentCount; i++)
            {
                this.ProcessScanImpl(decoder, i, ref this.Pointers.Scan[i], ref totalHv);
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

                if (this.zigStart != 0 && scanComponentCount != 1)
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
                for (int i = 0; i < scanComponentCount; i++)
                {
                    int compIndex = this.Pointers.Scan[i].Index;
                    if (decoder.ProgCoeffs[compIndex] == null)
                    {
                        int size = mxx * myy * decoder.ComponentArray[compIndex].HorizontalFactor
                                   * decoder.ComponentArray[compIndex].VerticalFactor;

                        decoder.ProgCoeffs[compIndex] = new Block8x8F[size];
                    }
                }
            }
        }

        private void ProcessScanImpl(JpegDecoderCore decoder, int i, ref Scan currentScan, ref int totalHv)
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

            this.ProcessComponentImpl(decoder, i, ref currentScan, ref totalHv, ref decoder.ComponentArray[compIndex]);
        }


        private void ProcessComponentImpl(
            JpegDecoderCore decoder,
            int i,
            ref Scan currentScan,
            ref int totalHv,
            ref Component currentComponent)
        {
            // Section B.2.3 states that "the value of Cs_j shall be different from
            // the values of Cs_1 through Cs_(j-1)". Since we have previously
            // verified that a frame's component identifiers (C_i values in section
            // B.2.2) are unique, it suffices to check that the implicit indexes
            // into comp are unique.
            for (int j = 0; j < i; j++)
            {
                if (currentScan.Index == this.Pointers.Scan[j].Index)
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

        private void InitProgressive(JpegDecoderCore decoder)
        {
            
        }
    }
    
}