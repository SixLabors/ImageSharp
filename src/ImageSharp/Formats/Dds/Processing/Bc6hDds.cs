using SixLabors.ImageSharp.Formats.Dds;
using SixLabors.ImageSharp.Formats.Dds.Emums;
using SixLabors.ImageSharp.Formats.Dds.Processing;
using SixLabors.ImageSharp.Formats.Dds.Processing.Bc6hBc7;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Pfim.dds
{
    internal class Bc6hDds : CompressedDds
    { // Code based on commit 138efff1b9c53fd9a5dd34b8c865e8f5ae798030 2019/10/24 in DirectXTex C++ library
        private enum EField : byte
        {
            NA, // N/A
            M,  // Mode
            D,  // Shape
            RW,
            RX,
            RY,
            RZ,
            GW,
            GX,
            GY,
            GZ,
            BW,
            BX,
            BY,
            BZ,
        };

        private struct ModeDescriptor
        {
            public EField m_eField;
            public byte m_uBit;

            public ModeDescriptor(EField eF, byte uB)
            {
                m_eField = eF;
                m_uBit = uB;
            }
        };

        private struct ModeInfo
        {
            public byte uMode;
            public byte uPartitions;
            public bool bTransformed;
            public byte uIndexPrec;
            public readonly LDRColorA[][] RGBAPrec;//[Constants.BC6H_MAX_REGIONS][2];

            public ModeInfo(byte uM, byte uP, bool bT, byte uI, LDRColorA[][] prec)
            {
                uMode = uM;
                uPartitions = uP;
                bTransformed = bT;
                uIndexPrec = uI;
                RGBAPrec = prec;
            }
        };

        private static readonly ModeDescriptor[][] ms_aDesc = new ModeDescriptor[14][]
        {
            new ModeDescriptor[82] {   // Mode 1 (0x00) - 10 5 5 5
                new ModeDescriptor(EField.M, 0), new ModeDescriptor( EField.M, 1), new ModeDescriptor(EField.GY, 4), new ModeDescriptor(EField.BY, 4), new ModeDescriptor(EField.BZ, 4), new ModeDescriptor(EField.RW, 0), new ModeDescriptor(EField.RW, 1), new ModeDescriptor(EField.RW, 2), new ModeDescriptor(EField.RW, 3), new ModeDescriptor(EField.RW, 4),
                new ModeDescriptor(EField.RW, 5), new ModeDescriptor(EField.RW, 6), new ModeDescriptor(EField.RW, 7), new ModeDescriptor(EField.RW, 8), new ModeDescriptor(EField.RW, 9), new ModeDescriptor(EField.GW, 0), new ModeDescriptor(EField.GW, 1), new ModeDescriptor(EField.GW, 2), new ModeDescriptor(EField.GW, 3), new ModeDescriptor(EField.GW, 4),
                new ModeDescriptor(EField.GW, 5), new ModeDescriptor(EField.GW, 6), new ModeDescriptor(EField.GW, 7), new ModeDescriptor(EField.GW, 8), new ModeDescriptor(EField.GW, 9), new ModeDescriptor(EField.BW, 0), new ModeDescriptor(EField.BW, 1), new ModeDescriptor(EField.BW, 2), new ModeDescriptor(EField.BW, 3), new ModeDescriptor(EField.BW, 4),
                new ModeDescriptor(EField.BW, 5), new ModeDescriptor(EField.BW, 6), new ModeDescriptor(EField.BW, 7), new ModeDescriptor(EField.BW, 8), new ModeDescriptor(EField.BW, 9), new ModeDescriptor(EField.RX, 0), new ModeDescriptor(EField.RX, 1), new ModeDescriptor(EField.RX, 2), new ModeDescriptor(EField.RX, 3), new ModeDescriptor(EField.RX, 4),
                new ModeDescriptor(EField.GZ, 4), new ModeDescriptor(EField.GY, 0), new ModeDescriptor(EField.GY, 1), new ModeDescriptor(EField.GY, 2), new ModeDescriptor(EField.GY, 3), new ModeDescriptor(EField.GX, 0), new ModeDescriptor(EField.GX, 1), new ModeDescriptor(EField.GX, 2), new ModeDescriptor(EField.GX, 3), new ModeDescriptor(EField.GX, 4),
                new ModeDescriptor(EField.BZ, 0), new ModeDescriptor(EField.GZ, 0), new ModeDescriptor(EField.GZ, 1), new ModeDescriptor(EField.GZ, 2), new ModeDescriptor(EField.GZ, 3), new ModeDescriptor(EField.BX, 0), new ModeDescriptor(EField.BX, 1), new ModeDescriptor(EField.BX, 2), new ModeDescriptor(EField.BX, 3), new ModeDescriptor(EField.BX, 4),
                new ModeDescriptor(EField.BZ, 1), new ModeDescriptor(EField.BY, 0), new ModeDescriptor(EField.BY, 1), new ModeDescriptor(EField.BY, 2), new ModeDescriptor(EField.BY, 3), new ModeDescriptor(EField.RY, 0), new ModeDescriptor(EField.RY, 1), new ModeDescriptor(EField.RY, 2), new ModeDescriptor(EField.RY, 3), new ModeDescriptor(EField.RY, 4),
                new ModeDescriptor(EField.BZ, 2), new ModeDescriptor(EField.RZ, 0), new ModeDescriptor(EField.RZ, 1), new ModeDescriptor(EField.RZ, 2), new ModeDescriptor(EField.RZ, 3), new ModeDescriptor(EField.RZ, 4), new ModeDescriptor(EField.BZ, 3), new ModeDescriptor(EField.D, 0), new ModeDescriptor(EField.D, 1), new ModeDescriptor(EField.D, 2),
                new ModeDescriptor(EField.D, 3), new ModeDescriptor( EField.D, 4)
            },

            new ModeDescriptor[82] {   // Mode 2 (0x01) - 7 6 6 6
                new ModeDescriptor(EField. M, 0), new ModeDescriptor(EField. M, 1), new ModeDescriptor(EField.GY, 5), new ModeDescriptor(EField.GZ, 4), new ModeDescriptor(EField.GZ, 5), new ModeDescriptor(EField.RW, 0), new ModeDescriptor(EField.RW, 1), new ModeDescriptor(EField.RW, 2), new ModeDescriptor(EField.RW, 3), new ModeDescriptor(EField.RW, 4),
                new ModeDescriptor(EField.RW, 5), new ModeDescriptor(EField.RW, 6), new ModeDescriptor(EField.BZ, 0), new ModeDescriptor(EField.BZ, 1), new ModeDescriptor(EField.BY, 4), new ModeDescriptor(EField.GW, 0), new ModeDescriptor(EField.GW, 1), new ModeDescriptor(EField.GW, 2), new ModeDescriptor(EField.GW, 3), new ModeDescriptor(EField.GW, 4),
                new ModeDescriptor(EField.GW, 5), new ModeDescriptor(EField.GW, 6), new ModeDescriptor(EField.BY, 5), new ModeDescriptor(EField.BZ, 2), new ModeDescriptor(EField.GY, 4), new ModeDescriptor(EField.BW, 0), new ModeDescriptor(EField.BW, 1), new ModeDescriptor(EField.BW, 2), new ModeDescriptor(EField.BW, 3), new ModeDescriptor(EField.BW, 4),
                new ModeDescriptor(EField.BW, 5), new ModeDescriptor(EField.BW, 6), new ModeDescriptor(EField.BZ, 3), new ModeDescriptor(EField.BZ, 5), new ModeDescriptor(EField.BZ, 4), new ModeDescriptor(EField.RX, 0), new ModeDescriptor(EField.RX, 1), new ModeDescriptor(EField.RX, 2), new ModeDescriptor(EField.RX, 3), new ModeDescriptor(EField.RX, 4),
                new ModeDescriptor(EField.RX, 5), new ModeDescriptor(EField.GY, 0), new ModeDescriptor(EField.GY, 1), new ModeDescriptor(EField.GY, 2), new ModeDescriptor(EField.GY, 3), new ModeDescriptor(EField.GX, 0), new ModeDescriptor(EField.GX, 1), new ModeDescriptor(EField.GX, 2), new ModeDescriptor(EField.GX, 3), new ModeDescriptor(EField.GX, 4),
                new ModeDescriptor(EField.GX, 5), new ModeDescriptor(EField.GZ, 0), new ModeDescriptor(EField.GZ, 1), new ModeDescriptor(EField.GZ, 2), new ModeDescriptor(EField.GZ, 3), new ModeDescriptor(EField.BX, 0), new ModeDescriptor(EField.BX, 1), new ModeDescriptor(EField.BX, 2), new ModeDescriptor(EField.BX, 3), new ModeDescriptor(EField.BX, 4),
                new ModeDescriptor(EField.BX, 5), new ModeDescriptor(EField.BY, 0), new ModeDescriptor(EField.BY, 1), new ModeDescriptor(EField.BY, 2), new ModeDescriptor(EField.BY, 3), new ModeDescriptor(EField.RY, 0), new ModeDescriptor(EField.RY, 1), new ModeDescriptor(EField.RY, 2), new ModeDescriptor(EField.RY, 3), new ModeDescriptor(EField.RY, 4),
                new ModeDescriptor(EField.RY, 5), new ModeDescriptor(EField.RZ, 0), new ModeDescriptor(EField.RZ, 1), new ModeDescriptor(EField.RZ, 2), new ModeDescriptor(EField.RZ, 3), new ModeDescriptor(EField.RZ, 4), new ModeDescriptor(EField.RZ, 5), new ModeDescriptor(EField.D, 0 ), new ModeDescriptor(EField.D, 1 ), new ModeDescriptor(EField.D, 2 ),
                new ModeDescriptor(EField.D, 3), new ModeDescriptor(EField.D, 4  )
            },

            new ModeDescriptor[82] {   // Mode 3 (0x02) - 11 5 4 4
                new ModeDescriptor(EField. M, 0), new ModeDescriptor(EField. M, 1), new ModeDescriptor(EField. M, 2), new ModeDescriptor(EField. M, 3), new ModeDescriptor(EField. M, 4), new ModeDescriptor(EField.RW, 0), new ModeDescriptor(EField.RW, 1), new ModeDescriptor(EField.RW, 2), new ModeDescriptor(EField.RW, 3), new ModeDescriptor(EField.RW, 4),
                new ModeDescriptor(EField.RW, 5), new ModeDescriptor(EField.RW, 6), new ModeDescriptor(EField.RW, 7), new ModeDescriptor(EField.RW, 8), new ModeDescriptor(EField.RW, 9), new ModeDescriptor(EField.GW, 0), new ModeDescriptor(EField.GW, 1), new ModeDescriptor(EField.GW, 2), new ModeDescriptor(EField.GW, 3), new ModeDescriptor(EField.GW, 4),
                new ModeDescriptor(EField.GW, 5), new ModeDescriptor(EField.GW, 6), new ModeDescriptor(EField.GW, 7), new ModeDescriptor(EField.GW, 8), new ModeDescriptor(EField.GW, 9), new ModeDescriptor(EField.BW, 0), new ModeDescriptor(EField.BW, 1), new ModeDescriptor(EField.BW, 2), new ModeDescriptor(EField.BW, 3), new ModeDescriptor(EField.BW, 4),
                new ModeDescriptor(EField.BW, 5), new ModeDescriptor(EField.BW, 6), new ModeDescriptor(EField.BW, 7), new ModeDescriptor(EField.BW, 8), new ModeDescriptor(EField.BW, 9), new ModeDescriptor(EField.RX, 0), new ModeDescriptor(EField.RX, 1), new ModeDescriptor(EField.RX, 2), new ModeDescriptor(EField.RX, 3), new ModeDescriptor(EField.RX, 4),
                new ModeDescriptor(EField.RW,10), new ModeDescriptor(EField.GY, 0), new ModeDescriptor(EField.GY, 1), new ModeDescriptor(EField.GY, 2), new ModeDescriptor(EField.GY, 3), new ModeDescriptor(EField.GX, 0), new ModeDescriptor(EField.GX, 1), new ModeDescriptor(EField.GX, 2), new ModeDescriptor(EField.GX, 3), new ModeDescriptor(EField.GW,10),
                new ModeDescriptor(EField.BZ, 0), new ModeDescriptor(EField.GZ, 0), new ModeDescriptor(EField.GZ, 1), new ModeDescriptor(EField.GZ, 2), new ModeDescriptor(EField.GZ, 3), new ModeDescriptor(EField.BX, 0), new ModeDescriptor(EField.BX, 1), new ModeDescriptor(EField.BX, 2), new ModeDescriptor(EField.BX, 3), new ModeDescriptor(EField.BW,10),
                new ModeDescriptor(EField.BZ, 1), new ModeDescriptor(EField.BY, 0), new ModeDescriptor(EField.BY, 1), new ModeDescriptor(EField.BY, 2), new ModeDescriptor(EField.BY, 3), new ModeDescriptor(EField.RY, 0), new ModeDescriptor(EField.RY, 1), new ModeDescriptor(EField.RY, 2), new ModeDescriptor(EField.RY, 3), new ModeDescriptor(EField.RY, 4),
                new ModeDescriptor(EField.BZ, 2), new ModeDescriptor(EField.RZ, 0), new ModeDescriptor(EField.RZ, 1), new ModeDescriptor(EField.RZ, 2), new ModeDescriptor(EField.RZ, 3), new ModeDescriptor(EField.RZ, 4), new ModeDescriptor(EField.BZ, 3), new ModeDescriptor(EField. D, 0), new ModeDescriptor(EField. D, 1), new ModeDescriptor(EField. D, 2),
                new ModeDescriptor(EField. D, 3), new ModeDescriptor(EField. D, 4)
            },

            new ModeDescriptor[82] {   // Mode 4 (0x06) - 11 4 5 4
                new ModeDescriptor(EField. M, 0), new ModeDescriptor(EField. M, 1), new ModeDescriptor(EField. M, 2), new ModeDescriptor(EField. M, 3), new ModeDescriptor(EField. M, 4), new ModeDescriptor(EField.RW, 0), new ModeDescriptor(EField.RW, 1), new ModeDescriptor(EField.RW, 2), new ModeDescriptor(EField.RW, 3), new ModeDescriptor(EField.RW, 4),
                new ModeDescriptor(EField.RW, 5), new ModeDescriptor(EField.RW, 6), new ModeDescriptor(EField.RW, 7), new ModeDescriptor(EField.RW, 8), new ModeDescriptor(EField.RW, 9), new ModeDescriptor(EField.GW, 0), new ModeDescriptor(EField.GW, 1), new ModeDescriptor(EField.GW, 2), new ModeDescriptor(EField.GW, 3), new ModeDescriptor(EField.GW, 4),
                new ModeDescriptor(EField.GW, 5), new ModeDescriptor(EField.GW, 6), new ModeDescriptor(EField.GW, 7), new ModeDescriptor(EField.GW, 8), new ModeDescriptor(EField.GW, 9), new ModeDescriptor(EField.BW, 0), new ModeDescriptor(EField.BW, 1), new ModeDescriptor(EField.BW, 2), new ModeDescriptor(EField.BW, 3), new ModeDescriptor(EField.BW, 4),
                new ModeDescriptor(EField.BW, 5), new ModeDescriptor(EField.BW, 6), new ModeDescriptor(EField.BW, 7), new ModeDescriptor(EField.BW, 8), new ModeDescriptor(EField.BW, 9), new ModeDescriptor(EField.RX, 0), new ModeDescriptor(EField.RX, 1), new ModeDescriptor(EField.RX, 2), new ModeDescriptor(EField.RX, 3), new ModeDescriptor(EField.RW,10),
                new ModeDescriptor(EField.GZ, 4), new ModeDescriptor(EField.GY, 0), new ModeDescriptor(EField.GY, 1), new ModeDescriptor(EField.GY, 2), new ModeDescriptor(EField.GY, 3), new ModeDescriptor(EField.GX, 0), new ModeDescriptor(EField.GX, 1), new ModeDescriptor(EField.GX, 2), new ModeDescriptor(EField.GX, 3), new ModeDescriptor(EField.GX, 4),
                new ModeDescriptor(EField.GW,10), new ModeDescriptor(EField.GZ, 0), new ModeDescriptor(EField.GZ, 1), new ModeDescriptor(EField.GZ, 2), new ModeDescriptor(EField.GZ, 3), new ModeDescriptor(EField.BX, 0), new ModeDescriptor(EField.BX, 1), new ModeDescriptor(EField.BX, 2), new ModeDescriptor(EField.BX, 3), new ModeDescriptor(EField.BW,10),
                new ModeDescriptor(EField.BZ, 1), new ModeDescriptor(EField.BY, 0), new ModeDescriptor(EField.BY, 1), new ModeDescriptor(EField.BY, 2), new ModeDescriptor(EField.BY, 3), new ModeDescriptor(EField.RY, 0), new ModeDescriptor(EField.RY, 1), new ModeDescriptor(EField.RY, 2), new ModeDescriptor(EField.RY, 3), new ModeDescriptor(EField.BZ, 0),
                new ModeDescriptor(EField.BZ, 2), new ModeDescriptor(EField.RZ, 0), new ModeDescriptor(EField.RZ, 1), new ModeDescriptor(EField.RZ, 2), new ModeDescriptor(EField.RZ, 3), new ModeDescriptor(EField.GY, 4), new ModeDescriptor(EField.BZ, 3), new ModeDescriptor(EField. D, 0), new ModeDescriptor(EField. D, 1), new ModeDescriptor(EField. D, 2),
                new ModeDescriptor(EField. D, 3), new ModeDescriptor(EField. D, 4)
            },

            new ModeDescriptor[82] {   // Mode 5 (0x0a) - 11 4 4 5
                new ModeDescriptor(EField. M, 0), new ModeDescriptor(EField. M, 1), new ModeDescriptor(EField. M, 2), new ModeDescriptor(EField. M, 3), new ModeDescriptor(EField. M, 4), new ModeDescriptor(EField.RW, 0), new ModeDescriptor(EField.RW, 1), new ModeDescriptor(EField.RW, 2), new ModeDescriptor(EField.RW, 3), new ModeDescriptor(EField.RW, 4),
                new ModeDescriptor(EField.RW, 5), new ModeDescriptor(EField.RW, 6), new ModeDescriptor(EField.RW, 7), new ModeDescriptor(EField.RW, 8), new ModeDescriptor(EField.RW, 9), new ModeDescriptor(EField.GW, 0), new ModeDescriptor(EField.GW, 1), new ModeDescriptor(EField.GW, 2), new ModeDescriptor(EField.GW, 3), new ModeDescriptor(EField.GW, 4),
                new ModeDescriptor(EField.GW, 5), new ModeDescriptor(EField.GW, 6), new ModeDescriptor(EField.GW, 7), new ModeDescriptor(EField.GW, 8), new ModeDescriptor(EField.GW, 9), new ModeDescriptor(EField.BW, 0), new ModeDescriptor(EField.BW, 1), new ModeDescriptor(EField.BW, 2), new ModeDescriptor(EField.BW, 3), new ModeDescriptor(EField.BW, 4),
                new ModeDescriptor(EField.BW, 5), new ModeDescriptor(EField.BW, 6), new ModeDescriptor(EField.BW, 7), new ModeDescriptor(EField.BW, 8), new ModeDescriptor(EField.BW, 9), new ModeDescriptor(EField.RX, 0), new ModeDescriptor(EField.RX, 1), new ModeDescriptor(EField.RX, 2), new ModeDescriptor(EField.RX, 3), new ModeDescriptor(EField.RW,10),
                new ModeDescriptor(EField.BY, 4), new ModeDescriptor(EField.GY, 0), new ModeDescriptor(EField.GY, 1), new ModeDescriptor(EField.GY, 2), new ModeDescriptor(EField.GY, 3), new ModeDescriptor(EField.GX, 0), new ModeDescriptor(EField.GX, 1), new ModeDescriptor(EField.GX, 2), new ModeDescriptor(EField.GX, 3), new ModeDescriptor(EField.GW,10),
                new ModeDescriptor(EField.BZ, 0), new ModeDescriptor(EField.GZ, 0), new ModeDescriptor(EField.GZ, 1), new ModeDescriptor(EField.GZ, 2), new ModeDescriptor(EField.GZ, 3), new ModeDescriptor(EField.BX, 0), new ModeDescriptor(EField.BX, 1), new ModeDescriptor(EField.BX, 2), new ModeDescriptor(EField.BX, 3), new ModeDescriptor(EField.BX, 4),
                new ModeDescriptor(EField.BW,10), new ModeDescriptor(EField.BY, 0), new ModeDescriptor(EField.BY, 1), new ModeDescriptor(EField.BY, 2), new ModeDescriptor(EField.BY, 3), new ModeDescriptor(EField.RY, 0), new ModeDescriptor(EField.RY, 1), new ModeDescriptor(EField.RY, 2), new ModeDescriptor(EField.RY, 3), new ModeDescriptor(EField.BZ, 1),
                new ModeDescriptor(EField.BZ, 2), new ModeDescriptor(EField.RZ, 0), new ModeDescriptor(EField.RZ, 1), new ModeDescriptor(EField.RZ, 2), new ModeDescriptor(EField.RZ, 3), new ModeDescriptor(EField.BZ, 4), new ModeDescriptor(EField.BZ, 3), new ModeDescriptor(EField. D, 0), new ModeDescriptor(EField. D, 1), new ModeDescriptor(EField. D, 2),
                new ModeDescriptor(EField. D, 3), new ModeDescriptor(EField. D, 4)
            },

            new ModeDescriptor[82] {   // Mode 6 (0x0e) - 9 5 5 5
                new ModeDescriptor(EField. M, 0), new ModeDescriptor(EField. M, 1), new ModeDescriptor(EField. M, 2), new ModeDescriptor(EField. M, 3), new ModeDescriptor(EField. M, 4), new ModeDescriptor(EField.RW, 0), new ModeDescriptor(EField.RW, 1), new ModeDescriptor(EField.RW, 2), new ModeDescriptor(EField.RW, 3), new ModeDescriptor(EField.RW, 4),
                new ModeDescriptor(EField.RW, 5), new ModeDescriptor(EField.RW, 6), new ModeDescriptor(EField.RW, 7), new ModeDescriptor(EField.RW, 8), new ModeDescriptor(EField.BY, 4), new ModeDescriptor(EField.GW, 0), new ModeDescriptor(EField.GW, 1), new ModeDescriptor(EField.GW, 2), new ModeDescriptor(EField.GW, 3), new ModeDescriptor(EField.GW, 4),
                new ModeDescriptor(EField.GW, 5), new ModeDescriptor(EField.GW, 6), new ModeDescriptor(EField.GW, 7), new ModeDescriptor(EField.GW, 8), new ModeDescriptor(EField.GY, 4), new ModeDescriptor(EField.BW, 0), new ModeDescriptor(EField.BW, 1), new ModeDescriptor(EField.BW, 2), new ModeDescriptor(EField.BW, 3), new ModeDescriptor(EField.BW, 4),
                new ModeDescriptor(EField.BW, 5), new ModeDescriptor(EField.BW, 6), new ModeDescriptor(EField.BW, 7), new ModeDescriptor(EField.BW, 8), new ModeDescriptor(EField.BZ, 4), new ModeDescriptor(EField.RX, 0), new ModeDescriptor(EField.RX, 1), new ModeDescriptor(EField.RX, 2), new ModeDescriptor(EField.RX, 3), new ModeDescriptor(EField.RX, 4),
                new ModeDescriptor(EField.GZ, 4), new ModeDescriptor(EField.GY, 0), new ModeDescriptor(EField.GY, 1), new ModeDescriptor(EField.GY, 2), new ModeDescriptor(EField.GY, 3), new ModeDescriptor(EField.GX, 0), new ModeDescriptor(EField.GX, 1), new ModeDescriptor(EField.GX, 2), new ModeDescriptor(EField.GX, 3), new ModeDescriptor(EField.GX, 4),
                new ModeDescriptor(EField.BZ, 0), new ModeDescriptor(EField.GZ, 0), new ModeDescriptor(EField.GZ, 1), new ModeDescriptor(EField.GZ, 2), new ModeDescriptor(EField.GZ, 3), new ModeDescriptor(EField.BX, 0), new ModeDescriptor(EField.BX, 1), new ModeDescriptor(EField.BX, 2), new ModeDescriptor(EField.BX, 3), new ModeDescriptor(EField.BX, 4),
                new ModeDescriptor(EField.BZ, 1), new ModeDescriptor(EField.BY, 0), new ModeDescriptor(EField.BY, 1), new ModeDescriptor(EField.BY, 2), new ModeDescriptor(EField.BY, 3), new ModeDescriptor(EField.RY, 0), new ModeDescriptor(EField.RY, 1), new ModeDescriptor(EField.RY, 2), new ModeDescriptor(EField.RY, 3), new ModeDescriptor(EField.RY, 4),
                new ModeDescriptor(EField.BZ, 2), new ModeDescriptor(EField.RZ, 0), new ModeDescriptor(EField.RZ, 1), new ModeDescriptor(EField.RZ, 2), new ModeDescriptor(EField.RZ, 3), new ModeDescriptor(EField.RZ, 4), new ModeDescriptor(EField.BZ, 3), new ModeDescriptor(EField. D, 0), new ModeDescriptor(EField. D, 1), new ModeDescriptor(EField. D, 2),
                new ModeDescriptor(EField. D, 3), new ModeDescriptor(EField. D, 4)
            },

            new ModeDescriptor[82] {   // Mode 7 (0x12) - 8 6 5 5
                new ModeDescriptor(EField. M, 0), new ModeDescriptor(EField. M, 1), new ModeDescriptor(EField. M, 2), new ModeDescriptor(EField. M, 3), new ModeDescriptor(EField. M, 4), new ModeDescriptor(EField.RW, 0), new ModeDescriptor(EField.RW, 1), new ModeDescriptor(EField.RW, 2), new ModeDescriptor(EField.RW, 3), new ModeDescriptor(EField.RW, 4),
                new ModeDescriptor(EField.RW, 5), new ModeDescriptor(EField.RW, 6), new ModeDescriptor(EField.RW, 7), new ModeDescriptor(EField.GZ, 4), new ModeDescriptor(EField.BY, 4), new ModeDescriptor(EField.GW, 0), new ModeDescriptor(EField.GW, 1), new ModeDescriptor(EField.GW, 2), new ModeDescriptor(EField.GW, 3), new ModeDescriptor(EField.GW, 4),
                new ModeDescriptor(EField.GW, 5), new ModeDescriptor(EField.GW, 6), new ModeDescriptor(EField.GW, 7), new ModeDescriptor(EField.BZ, 2), new ModeDescriptor(EField.GY, 4), new ModeDescriptor(EField.BW, 0), new ModeDescriptor(EField.BW, 1), new ModeDescriptor(EField.BW, 2), new ModeDescriptor(EField.BW, 3), new ModeDescriptor(EField.BW, 4),
                new ModeDescriptor(EField.BW, 5), new ModeDescriptor(EField.BW, 6), new ModeDescriptor(EField.BW, 7), new ModeDescriptor(EField.BZ, 3), new ModeDescriptor(EField.BZ, 4), new ModeDescriptor(EField.RX, 0), new ModeDescriptor(EField.RX, 1), new ModeDescriptor(EField.RX, 2), new ModeDescriptor(EField.RX, 3), new ModeDescriptor(EField.RX, 4),
                new ModeDescriptor(EField.RX, 5), new ModeDescriptor(EField.GY, 0), new ModeDescriptor(EField.GY, 1), new ModeDescriptor(EField.GY, 2), new ModeDescriptor(EField.GY, 3), new ModeDescriptor(EField.GX, 0), new ModeDescriptor(EField.GX, 1), new ModeDescriptor(EField.GX, 2), new ModeDescriptor(EField.GX, 3), new ModeDescriptor(EField.GX, 4),
                new ModeDescriptor(EField.BZ, 0), new ModeDescriptor(EField.GZ, 0), new ModeDescriptor(EField.GZ, 1), new ModeDescriptor(EField.GZ, 2), new ModeDescriptor(EField.GZ, 3), new ModeDescriptor(EField.BX, 0), new ModeDescriptor(EField.BX, 1), new ModeDescriptor(EField.BX, 2), new ModeDescriptor(EField.BX, 3), new ModeDescriptor(EField.BX, 4),
                new ModeDescriptor(EField.BZ, 1), new ModeDescriptor(EField.BY, 0), new ModeDescriptor(EField.BY, 1), new ModeDescriptor(EField.BY, 2), new ModeDescriptor(EField.BY, 3), new ModeDescriptor(EField.RY, 0), new ModeDescriptor(EField.RY, 1), new ModeDescriptor(EField.RY, 2), new ModeDescriptor(EField.RY, 3), new ModeDescriptor(EField.RY, 4),
                new ModeDescriptor(EField.RY, 5), new ModeDescriptor(EField.RZ, 0), new ModeDescriptor(EField.RZ, 1), new ModeDescriptor(EField.RZ, 2), new ModeDescriptor(EField.RZ, 3), new ModeDescriptor(EField.RZ, 4), new ModeDescriptor(EField.RZ, 5), new ModeDescriptor(EField. D, 0), new ModeDescriptor(EField. D, 1), new ModeDescriptor(EField. D, 2),
                new ModeDescriptor(EField. D, 3), new ModeDescriptor(EField. D, 4)
            },

            new ModeDescriptor[82] {   // Mode 8 (0x16) - 8 5 6 5
                new ModeDescriptor(EField. M, 0), new ModeDescriptor(EField. M, 1), new ModeDescriptor(EField. M, 2), new ModeDescriptor(EField. M, 3), new ModeDescriptor(EField. M, 4), new ModeDescriptor(EField.RW, 0), new ModeDescriptor(EField.RW, 1), new ModeDescriptor(EField.RW, 2), new ModeDescriptor(EField.RW, 3), new ModeDescriptor(EField.RW, 4),
                new ModeDescriptor(EField.RW, 5), new ModeDescriptor(EField.RW, 6), new ModeDescriptor(EField.RW, 7), new ModeDescriptor(EField.BZ, 0), new ModeDescriptor(EField.BY, 4), new ModeDescriptor(EField.GW, 0), new ModeDescriptor(EField.GW, 1), new ModeDescriptor(EField.GW, 2), new ModeDescriptor(EField.GW, 3), new ModeDescriptor(EField.GW, 4),
                new ModeDescriptor(EField.GW, 5), new ModeDescriptor(EField.GW, 6), new ModeDescriptor(EField.GW, 7), new ModeDescriptor(EField.GY, 5), new ModeDescriptor(EField.GY, 4), new ModeDescriptor(EField.BW, 0), new ModeDescriptor(EField.BW, 1), new ModeDescriptor(EField.BW, 2), new ModeDescriptor(EField.BW, 3), new ModeDescriptor(EField.BW, 4),
                new ModeDescriptor(EField.BW, 5), new ModeDescriptor(EField.BW, 6), new ModeDescriptor(EField.BW, 7), new ModeDescriptor(EField.GZ, 5), new ModeDescriptor(EField.BZ, 4), new ModeDescriptor(EField.RX, 0), new ModeDescriptor(EField.RX, 1), new ModeDescriptor(EField.RX, 2), new ModeDescriptor(EField.RX, 3), new ModeDescriptor(EField.RX, 4),
                new ModeDescriptor(EField.GZ, 4), new ModeDescriptor(EField.GY, 0), new ModeDescriptor(EField.GY, 1), new ModeDescriptor(EField.GY, 2), new ModeDescriptor(EField.GY, 3), new ModeDescriptor(EField.GX, 0), new ModeDescriptor(EField.GX, 1), new ModeDescriptor(EField.GX, 2), new ModeDescriptor(EField.GX, 3), new ModeDescriptor(EField.GX, 4),
                new ModeDescriptor(EField.GX, 5), new ModeDescriptor(EField.GZ, 0), new ModeDescriptor(EField.GZ, 1), new ModeDescriptor(EField.GZ, 2), new ModeDescriptor(EField.GZ, 3), new ModeDescriptor(EField.BX, 0), new ModeDescriptor(EField.BX, 1), new ModeDescriptor(EField.BX, 2), new ModeDescriptor(EField.BX, 3), new ModeDescriptor(EField.BX, 4),
                new ModeDescriptor(EField.BZ, 1), new ModeDescriptor(EField.BY, 0), new ModeDescriptor(EField.BY, 1), new ModeDescriptor(EField.BY, 2), new ModeDescriptor(EField.BY, 3), new ModeDescriptor(EField.RY, 0), new ModeDescriptor(EField.RY, 1), new ModeDescriptor(EField.RY, 2), new ModeDescriptor(EField.RY, 3), new ModeDescriptor(EField.RY, 4),
                new ModeDescriptor(EField.BZ, 2), new ModeDescriptor(EField.RZ, 0), new ModeDescriptor(EField.RZ, 1), new ModeDescriptor(EField.RZ, 2), new ModeDescriptor(EField.RZ, 3), new ModeDescriptor(EField.RZ, 4), new ModeDescriptor(EField.BZ, 3), new ModeDescriptor(EField. D, 0), new ModeDescriptor(EField. D, 1), new ModeDescriptor(EField. D, 2),
                new ModeDescriptor(EField. D, 3), new ModeDescriptor(EField. D, 4)
            },

            new ModeDescriptor[82] {   // Mode 9 (0x1a) - 8 5 5 6
                new ModeDescriptor(EField. M, 0), new ModeDescriptor(EField. M, 1), new ModeDescriptor(EField. M, 2), new ModeDescriptor(EField. M, 3), new ModeDescriptor(EField. M, 4), new ModeDescriptor(EField.RW, 0), new ModeDescriptor(EField.RW, 1), new ModeDescriptor(EField.RW, 2), new ModeDescriptor(EField.RW, 3), new ModeDescriptor(EField.RW, 4),
                new ModeDescriptor(EField.RW, 5), new ModeDescriptor(EField.RW, 6), new ModeDescriptor(EField.RW, 7), new ModeDescriptor(EField.BZ, 1), new ModeDescriptor(EField.BY, 4), new ModeDescriptor(EField.GW, 0), new ModeDescriptor(EField.GW, 1), new ModeDescriptor(EField.GW, 2), new ModeDescriptor(EField.GW, 3), new ModeDescriptor(EField.GW, 4),
                new ModeDescriptor(EField.GW, 5), new ModeDescriptor(EField.GW, 6), new ModeDescriptor(EField.GW, 7), new ModeDescriptor(EField.BY, 5), new ModeDescriptor(EField.GY, 4), new ModeDescriptor(EField.BW, 0), new ModeDescriptor(EField.BW, 1), new ModeDescriptor(EField.BW, 2), new ModeDescriptor(EField.BW, 3), new ModeDescriptor(EField.BW, 4),
                new ModeDescriptor(EField.BW, 5), new ModeDescriptor(EField.BW, 6), new ModeDescriptor(EField.BW, 7), new ModeDescriptor(EField.BZ, 5), new ModeDescriptor(EField.BZ, 4), new ModeDescriptor(EField.RX, 0), new ModeDescriptor(EField.RX, 1), new ModeDescriptor(EField.RX, 2), new ModeDescriptor(EField.RX, 3), new ModeDescriptor(EField.RX, 4),
                new ModeDescriptor(EField.GZ, 4), new ModeDescriptor(EField.GY, 0), new ModeDescriptor(EField.GY, 1), new ModeDescriptor(EField.GY, 2), new ModeDescriptor(EField.GY, 3), new ModeDescriptor(EField.GX, 0), new ModeDescriptor(EField.GX, 1), new ModeDescriptor(EField.GX, 2), new ModeDescriptor(EField.GX, 3), new ModeDescriptor(EField.GX, 4),
                new ModeDescriptor(EField.BZ, 0), new ModeDescriptor(EField.GZ, 0), new ModeDescriptor(EField.GZ, 1), new ModeDescriptor(EField.GZ, 2), new ModeDescriptor(EField.GZ, 3), new ModeDescriptor(EField.BX, 0), new ModeDescriptor(EField.BX, 1), new ModeDescriptor(EField.BX, 2), new ModeDescriptor(EField.BX, 3), new ModeDescriptor(EField.BX, 4),
                new ModeDescriptor(EField.BX, 5), new ModeDescriptor(EField.BY, 0), new ModeDescriptor(EField.BY, 1), new ModeDescriptor(EField.BY, 2), new ModeDescriptor(EField.BY, 3), new ModeDescriptor(EField.RY, 0), new ModeDescriptor(EField.RY, 1), new ModeDescriptor(EField.RY, 2), new ModeDescriptor(EField.RY, 3), new ModeDescriptor(EField.RY, 4),
                new ModeDescriptor(EField.BZ, 2), new ModeDescriptor(EField.RZ, 0), new ModeDescriptor(EField.RZ, 1), new ModeDescriptor(EField.RZ, 2), new ModeDescriptor(EField.RZ, 3), new ModeDescriptor(EField.RZ, 4), new ModeDescriptor(EField.BZ, 3), new ModeDescriptor(EField. D, 0), new ModeDescriptor(EField. D, 1), new ModeDescriptor(EField. D, 2),
                new ModeDescriptor(EField. D, 3), new ModeDescriptor(EField. D, 4)
            },

            new ModeDescriptor[82] {   // Mode 10 (0x1e) - 6 6 6 6
                new ModeDescriptor(EField. M, 0), new ModeDescriptor(EField. M, 1), new ModeDescriptor(EField. M, 2), new ModeDescriptor(EField. M, 3), new ModeDescriptor(EField. M, 4), new ModeDescriptor(EField.RW, 0), new ModeDescriptor(EField.RW, 1), new ModeDescriptor(EField.RW, 2), new ModeDescriptor(EField.RW, 3), new ModeDescriptor(EField.RW, 4),
                new ModeDescriptor(EField.RW, 5), new ModeDescriptor(EField.GZ, 4), new ModeDescriptor(EField.BZ, 0), new ModeDescriptor(EField.BZ, 1), new ModeDescriptor(EField.BY, 4), new ModeDescriptor(EField.GW, 0), new ModeDescriptor(EField.GW, 1), new ModeDescriptor(EField.GW, 2), new ModeDescriptor(EField.GW, 3), new ModeDescriptor(EField.GW, 4),
                new ModeDescriptor(EField.GW, 5), new ModeDescriptor(EField.GY, 5), new ModeDescriptor(EField.BY, 5), new ModeDescriptor(EField.BZ, 2), new ModeDescriptor(EField.GY, 4), new ModeDescriptor(EField.BW, 0), new ModeDescriptor(EField.BW, 1), new ModeDescriptor(EField.BW, 2), new ModeDescriptor(EField.BW, 3), new ModeDescriptor(EField.BW, 4),
                new ModeDescriptor(EField.BW, 5), new ModeDescriptor(EField.GZ, 5), new ModeDescriptor(EField.BZ, 3), new ModeDescriptor(EField.BZ, 5), new ModeDescriptor(EField.BZ, 4), new ModeDescriptor(EField.RX, 0), new ModeDescriptor(EField.RX, 1), new ModeDescriptor(EField.RX, 2), new ModeDescriptor(EField.RX, 3), new ModeDescriptor(EField.RX, 4),
                new ModeDescriptor(EField.RX, 5), new ModeDescriptor(EField.GY, 0), new ModeDescriptor(EField.GY, 1), new ModeDescriptor(EField.GY, 2), new ModeDescriptor(EField.GY, 3), new ModeDescriptor(EField.GX, 0), new ModeDescriptor(EField.GX, 1), new ModeDescriptor(EField.GX, 2), new ModeDescriptor(EField.GX, 3), new ModeDescriptor(EField.GX, 4),
                new ModeDescriptor(EField.GX, 5), new ModeDescriptor(EField.GZ, 0), new ModeDescriptor(EField.GZ, 1), new ModeDescriptor(EField.GZ, 2), new ModeDescriptor(EField.GZ, 3), new ModeDescriptor(EField.BX, 0), new ModeDescriptor(EField.BX, 1), new ModeDescriptor(EField.BX, 2), new ModeDescriptor(EField.BX, 3), new ModeDescriptor(EField.BX, 4),
                new ModeDescriptor(EField.BX, 5), new ModeDescriptor(EField.BY, 0), new ModeDescriptor(EField.BY, 1), new ModeDescriptor(EField.BY, 2), new ModeDescriptor(EField.BY, 3), new ModeDescriptor(EField.RY, 0), new ModeDescriptor(EField.RY, 1), new ModeDescriptor(EField.RY, 2), new ModeDescriptor(EField.RY, 3), new ModeDescriptor(EField.RY, 4),
                new ModeDescriptor(EField.RY, 5), new ModeDescriptor(EField.RZ, 0), new ModeDescriptor(EField.RZ, 1), new ModeDescriptor(EField.RZ, 2), new ModeDescriptor(EField.RZ, 3), new ModeDescriptor(EField.RZ, 4), new ModeDescriptor(EField.RZ, 5), new ModeDescriptor(EField. D, 0), new ModeDescriptor(EField. D, 1), new ModeDescriptor(EField. D, 2),
                new ModeDescriptor(EField. D, 3), new ModeDescriptor(EField. D, 4)
            },

            new ModeDescriptor[82] {   // Mode 11 (0x03) - 10 10
                new ModeDescriptor(EField. M, 0), new ModeDescriptor(EField. M, 1), new ModeDescriptor(EField. M, 2), new ModeDescriptor(EField. M, 3), new ModeDescriptor(EField. M, 4), new ModeDescriptor(EField.RW, 0), new ModeDescriptor(EField.RW, 1), new ModeDescriptor(EField.RW, 2), new ModeDescriptor(EField.RW, 3), new ModeDescriptor(EField.RW, 4),
                new ModeDescriptor(EField.RW, 5), new ModeDescriptor(EField.RW, 6), new ModeDescriptor(EField.RW, 7), new ModeDescriptor(EField.RW, 8), new ModeDescriptor(EField.RW, 9), new ModeDescriptor(EField.GW, 0), new ModeDescriptor(EField.GW, 1), new ModeDescriptor(EField.GW, 2), new ModeDescriptor(EField.GW, 3), new ModeDescriptor(EField.GW, 4),
                new ModeDescriptor(EField.GW, 5), new ModeDescriptor(EField.GW, 6), new ModeDescriptor(EField.GW, 7), new ModeDescriptor(EField.GW, 8), new ModeDescriptor(EField.GW, 9), new ModeDescriptor(EField.BW, 0), new ModeDescriptor(EField.BW, 1), new ModeDescriptor(EField.BW, 2), new ModeDescriptor(EField.BW, 3), new ModeDescriptor(EField.BW, 4),
                new ModeDescriptor(EField.BW, 5), new ModeDescriptor(EField.BW, 6), new ModeDescriptor(EField.BW, 7), new ModeDescriptor(EField.BW, 8), new ModeDescriptor(EField.BW, 9), new ModeDescriptor(EField.RX, 0), new ModeDescriptor(EField.RX, 1), new ModeDescriptor(EField.RX, 2), new ModeDescriptor(EField.RX, 3), new ModeDescriptor(EField.RX, 4),
                new ModeDescriptor(EField.RX, 5), new ModeDescriptor(EField.RX, 6), new ModeDescriptor(EField.RX, 7), new ModeDescriptor(EField.RX, 8), new ModeDescriptor(EField.RX, 9), new ModeDescriptor(EField.GX, 0), new ModeDescriptor(EField.GX, 1), new ModeDescriptor(EField.GX, 2), new ModeDescriptor(EField.GX, 3), new ModeDescriptor(EField.GX, 4),
                new ModeDescriptor(EField.GX, 5), new ModeDescriptor(EField.GX, 6), new ModeDescriptor(EField.GX, 7), new ModeDescriptor(EField.GX, 8), new ModeDescriptor(EField.GX, 9), new ModeDescriptor(EField.BX, 0), new ModeDescriptor(EField.BX, 1), new ModeDescriptor(EField.BX, 2), new ModeDescriptor(EField.BX, 3), new ModeDescriptor(EField.BX, 4),
                new ModeDescriptor(EField.BX, 5), new ModeDescriptor(EField.BX, 6), new ModeDescriptor(EField.BX, 7), new ModeDescriptor(EField.BX, 8), new ModeDescriptor(EField.BX, 9), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0),
                new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0),
                new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0)
            },

            new ModeDescriptor[82] {   // Mode 12 (0x07) - 11 9
                new ModeDescriptor(EField. M, 0), new ModeDescriptor(EField. M, 1), new ModeDescriptor(EField. M, 2), new ModeDescriptor(EField. M, 3), new ModeDescriptor(EField. M, 4), new ModeDescriptor(EField.RW, 0), new ModeDescriptor(EField.RW, 1), new ModeDescriptor(EField.RW, 2), new ModeDescriptor(EField.RW, 3), new ModeDescriptor(EField.RW, 4),
                new ModeDescriptor(EField.RW, 5), new ModeDescriptor(EField.RW, 6), new ModeDescriptor(EField.RW, 7), new ModeDescriptor(EField.RW, 8), new ModeDescriptor(EField.RW, 9), new ModeDescriptor(EField.GW, 0), new ModeDescriptor(EField.GW, 1), new ModeDescriptor(EField.GW, 2), new ModeDescriptor(EField.GW, 3), new ModeDescriptor(EField.GW, 4),
                new ModeDescriptor(EField.GW, 5), new ModeDescriptor(EField.GW, 6), new ModeDescriptor(EField.GW, 7), new ModeDescriptor(EField.GW, 8), new ModeDescriptor(EField.GW, 9), new ModeDescriptor(EField.BW, 0), new ModeDescriptor(EField.BW, 1), new ModeDescriptor(EField.BW, 2), new ModeDescriptor(EField.BW, 3), new ModeDescriptor(EField.BW, 4),
                new ModeDescriptor(EField.BW, 5), new ModeDescriptor(EField.BW, 6), new ModeDescriptor(EField.BW, 7), new ModeDescriptor(EField.BW, 8), new ModeDescriptor(EField.BW, 9), new ModeDescriptor(EField.RX, 0), new ModeDescriptor(EField.RX, 1), new ModeDescriptor(EField.RX, 2), new ModeDescriptor(EField.RX, 3), new ModeDescriptor(EField.RX, 4),
                new ModeDescriptor(EField.RX, 5), new ModeDescriptor(EField.RX, 6), new ModeDescriptor(EField.RX, 7), new ModeDescriptor(EField.RX, 8), new ModeDescriptor(EField.RW,10), new ModeDescriptor(EField.GX, 0), new ModeDescriptor(EField.GX, 1), new ModeDescriptor(EField.GX, 2), new ModeDescriptor(EField.GX, 3), new ModeDescriptor(EField.GX, 4),
                new ModeDescriptor(EField.GX, 5), new ModeDescriptor(EField.GX, 6), new ModeDescriptor(EField.GX, 7), new ModeDescriptor(EField.GX, 8), new ModeDescriptor(EField.GW,10), new ModeDescriptor(EField.BX, 0), new ModeDescriptor(EField.BX, 1), new ModeDescriptor(EField.BX, 2), new ModeDescriptor(EField.BX, 3), new ModeDescriptor(EField.BX, 4),
                new ModeDescriptor(EField.BX, 5), new ModeDescriptor(EField.BX, 6), new ModeDescriptor(EField.BX, 7), new ModeDescriptor(EField.BX, 8), new ModeDescriptor(EField.BW,10), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0),
                new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0),
                new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0)
            },

            new ModeDescriptor[82] {   // Mode 13 (0x0b) - 12 8
                new ModeDescriptor(EField. M, 0), new ModeDescriptor(EField. M, 1), new ModeDescriptor(EField. M, 2), new ModeDescriptor(EField. M, 3), new ModeDescriptor(EField. M, 4), new ModeDescriptor(EField.RW, 0), new ModeDescriptor(EField.RW, 1), new ModeDescriptor(EField.RW, 2), new ModeDescriptor(EField.RW, 3), new ModeDescriptor(EField.RW, 4),
                new ModeDescriptor(EField.RW, 5), new ModeDescriptor(EField.RW, 6), new ModeDescriptor(EField.RW, 7), new ModeDescriptor(EField.RW, 8), new ModeDescriptor(EField.RW, 9), new ModeDescriptor(EField.GW, 0), new ModeDescriptor(EField.GW, 1), new ModeDescriptor(EField.GW, 2), new ModeDescriptor(EField.GW, 3), new ModeDescriptor(EField.GW, 4),
                new ModeDescriptor(EField.GW, 5), new ModeDescriptor(EField.GW, 6), new ModeDescriptor(EField.GW, 7), new ModeDescriptor(EField.GW, 8), new ModeDescriptor(EField.GW, 9), new ModeDescriptor(EField.BW, 0), new ModeDescriptor(EField.BW, 1), new ModeDescriptor(EField.BW, 2), new ModeDescriptor(EField.BW, 3), new ModeDescriptor(EField.BW, 4),
                new ModeDescriptor(EField.BW, 5), new ModeDescriptor(EField.BW, 6), new ModeDescriptor(EField.BW, 7), new ModeDescriptor(EField.BW, 8), new ModeDescriptor(EField.BW, 9), new ModeDescriptor(EField.RX, 0), new ModeDescriptor(EField.RX, 1), new ModeDescriptor(EField.RX, 2), new ModeDescriptor(EField.RX, 3), new ModeDescriptor(EField.RX, 4),
                new ModeDescriptor(EField.RX, 5), new ModeDescriptor(EField.RX, 6), new ModeDescriptor(EField.RX, 7), new ModeDescriptor(EField.RW,11), new ModeDescriptor(EField.RW,10), new ModeDescriptor(EField.GX, 0), new ModeDescriptor(EField.GX, 1), new ModeDescriptor(EField.GX, 2), new ModeDescriptor(EField.GX, 3), new ModeDescriptor(EField.GX, 4),
                new ModeDescriptor(EField.GX, 5), new ModeDescriptor(EField.GX, 6), new ModeDescriptor(EField.GX, 7), new ModeDescriptor(EField.GW,11), new ModeDescriptor(EField.GW,10), new ModeDescriptor(EField.BX, 0), new ModeDescriptor(EField.BX, 1), new ModeDescriptor(EField.BX, 2), new ModeDescriptor(EField.BX, 3), new ModeDescriptor(EField.BX, 4),
                new ModeDescriptor(EField.BX, 5), new ModeDescriptor(EField.BX, 6), new ModeDescriptor(EField.BX, 7), new ModeDescriptor(EField.BW,11), new ModeDescriptor(EField.BW,10), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0),
                new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0),
                new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0)
            },

            new ModeDescriptor[82] {   // Mode 14 (0x0f) - 16 4
                new ModeDescriptor(EField. M, 0), new ModeDescriptor(EField. M, 1), new ModeDescriptor(EField. M, 2), new ModeDescriptor(EField. M, 3), new ModeDescriptor(EField. M, 4), new ModeDescriptor(EField.RW, 0), new ModeDescriptor(EField.RW, 1), new ModeDescriptor(EField.RW, 2), new ModeDescriptor(EField.RW, 3), new ModeDescriptor(EField.RW, 4),
                new ModeDescriptor(EField.RW, 5), new ModeDescriptor(EField.RW, 6), new ModeDescriptor(EField.RW, 7), new ModeDescriptor(EField.RW, 8), new ModeDescriptor(EField.RW, 9), new ModeDescriptor(EField.GW, 0), new ModeDescriptor(EField.GW, 1), new ModeDescriptor(EField.GW, 2), new ModeDescriptor(EField.GW, 3), new ModeDescriptor(EField.GW, 4),
                new ModeDescriptor(EField.GW, 5), new ModeDescriptor(EField.GW, 6), new ModeDescriptor(EField.GW, 7), new ModeDescriptor(EField.GW, 8), new ModeDescriptor(EField.GW, 9), new ModeDescriptor(EField.BW, 0), new ModeDescriptor(EField.BW, 1), new ModeDescriptor(EField.BW, 2), new ModeDescriptor(EField.BW, 3), new ModeDescriptor(EField.BW, 4),
                new ModeDescriptor(EField.BW, 5), new ModeDescriptor(EField.BW, 6), new ModeDescriptor(EField.BW, 7), new ModeDescriptor(EField.BW, 8), new ModeDescriptor(EField.BW, 9), new ModeDescriptor(EField.RX, 0), new ModeDescriptor(EField.RX, 1), new ModeDescriptor(EField.RX, 2), new ModeDescriptor(EField.RX, 3), new ModeDescriptor(EField.RW,15),
                new ModeDescriptor(EField.RW,14), new ModeDescriptor(EField.RW,13), new ModeDescriptor(EField.RW,12), new ModeDescriptor(EField.RW,11), new ModeDescriptor(EField.RW,10), new ModeDescriptor(EField.GX, 0), new ModeDescriptor(EField.GX, 1), new ModeDescriptor(EField.GX, 2), new ModeDescriptor(EField.GX, 3), new ModeDescriptor(EField.GW,15),
                new ModeDescriptor(EField.GW,14), new ModeDescriptor(EField.GW,13), new ModeDescriptor(EField.GW,12), new ModeDescriptor(EField.GW,11), new ModeDescriptor(EField.GW,10), new ModeDescriptor(EField.BX, 0), new ModeDescriptor(EField.BX, 1), new ModeDescriptor(EField.BX, 2), new ModeDescriptor(EField.BX, 3), new ModeDescriptor(EField.BW,15),
                new ModeDescriptor(EField.BW,14), new ModeDescriptor(EField.BW,13), new ModeDescriptor(EField.BW,12), new ModeDescriptor(EField.BW,11), new ModeDescriptor(EField.BW,10), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0),
                new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0),
                new ModeDescriptor(EField.NA, 0), new ModeDescriptor(EField.NA, 0)
            },
        };

        private static readonly ModeInfo[] ms_aInfo =
        {
            new ModeInfo(0x00, 1, true,  3, new LDRColorA[][] { new LDRColorA[] { new LDRColorA(10,10,10,0), new LDRColorA( 5, 5, 5,0) }, new LDRColorA[] { new LDRColorA(5,5,5,0), new LDRColorA(5,5,5,0) } } ), // Mode 1
            new ModeInfo(0x01, 1, true,  3, new LDRColorA[][] { new LDRColorA[] { new LDRColorA( 7, 7, 7,0), new LDRColorA( 6, 6, 6,0) }, new LDRColorA[] { new LDRColorA(6,6,6,0), new LDRColorA(6,6,6,0) } } ), // Mode 2
            new ModeInfo(0x02, 1, true,  3, new LDRColorA[][] { new LDRColorA[] { new LDRColorA(11,11,11,0), new LDRColorA( 5, 4, 4,0) }, new LDRColorA[] { new LDRColorA(5,4,4,0), new LDRColorA(5,4,4,0) } } ), // Mode 3
            new ModeInfo(0x06, 1, true,  3, new LDRColorA[][] { new LDRColorA[] { new LDRColorA(11,11,11,0), new LDRColorA( 4, 5, 4,0) }, new LDRColorA[] { new LDRColorA(4,5,4,0), new LDRColorA(4,5,4,0) } } ), // Mode 4
            new ModeInfo(0x0a, 1, true,  3, new LDRColorA[][] { new LDRColorA[] { new LDRColorA(11,11,11,0), new LDRColorA( 4, 4, 5,0) }, new LDRColorA[] { new LDRColorA(4,4,5,0), new LDRColorA(4,4,5,0) } } ), // Mode 5
            new ModeInfo(0x0e, 1, true,  3, new LDRColorA[][] { new LDRColorA[] { new LDRColorA( 9, 9, 9,0), new LDRColorA( 5, 5, 5,0) }, new LDRColorA[] { new LDRColorA(5,5,5,0), new LDRColorA(5,5,5,0) } } ), // Mode 6
            new ModeInfo(0x12, 1, true,  3, new LDRColorA[][] { new LDRColorA[] { new LDRColorA( 8, 8, 8,0), new LDRColorA( 6, 5, 5,0) }, new LDRColorA[] { new LDRColorA(6,5,5,0), new LDRColorA(6,5,5,0) } } ), // Mode 7
            new ModeInfo(0x16, 1, true,  3, new LDRColorA[][] { new LDRColorA[] { new LDRColorA( 8, 8, 8,0), new LDRColorA( 5, 6, 5,0) }, new LDRColorA[] { new LDRColorA(5,6,5,0), new LDRColorA(5,6,5,0) } } ), // Mode 8
            new ModeInfo(0x1a, 1, true,  3, new LDRColorA[][] { new LDRColorA[] { new LDRColorA( 8, 8, 8,0), new LDRColorA( 5, 5, 6,0) }, new LDRColorA[] { new LDRColorA(5,5,6,0), new LDRColorA(5,5,6,0) } } ), // Mode 9
            new ModeInfo(0x1e, 1, false, 3, new LDRColorA[][] { new LDRColorA[] { new LDRColorA( 6, 6, 6,0), new LDRColorA( 6, 6, 6,0) }, new LDRColorA[] { new LDRColorA(6,6,6,0), new LDRColorA(6,6,6,0) } } ), // Mode 10
            new ModeInfo(0x03, 0, false, 4, new LDRColorA[][] { new LDRColorA[] { new LDRColorA(10,10,10,0), new LDRColorA(10,10,10,0) }, new LDRColorA[] { new LDRColorA(0,0,0,0), new LDRColorA(0,0,0,0) } } ), // Mode 11
            new ModeInfo(0x07, 0, true,  4, new LDRColorA[][] { new LDRColorA[] { new LDRColorA(11,11,11,0), new LDRColorA( 9, 9, 9,0) }, new LDRColorA[] { new LDRColorA(0,0,0,0), new LDRColorA(0,0,0,0) } } ), // Mode 12
            new ModeInfo(0x0b, 0, true,  4, new LDRColorA[][] { new LDRColorA[] { new LDRColorA(12,12,12,0), new LDRColorA( 8, 8, 8,0) }, new LDRColorA[] { new LDRColorA(0,0,0,0), new LDRColorA(0,0,0,0) } } ), // Mode 13
            new ModeInfo(0x0f, 0, true,  4, new LDRColorA[][] { new LDRColorA[] { new LDRColorA(16,16,16,0), new LDRColorA( 4, 4, 4,0) }, new LDRColorA[] { new LDRColorA(0,0,0,0), new LDRColorA(0,0,0,0) } } ), // Mode 14
        };

        private static readonly int[] ms_aModeToInfo =
        {
             0, // Mode 1   - 0x00
             1, // Mode 2   - 0x01
             2, // Mode 3   - 0x02
            10, // Mode 11  - 0x03
            -1, // Invalid  - 0x04
            -1, // Invalid  - 0x05
             3, // Mode 4   - 0x06
            11, // Mode 12  - 0x07
            -1, // Invalid  - 0x08
            -1, // Invalid  - 0x09
             4, // Mode 5   - 0x0a
            12, // Mode 13  - 0x0b
            -1, // Invalid  - 0x0c
            -1, // Invalid  - 0x0d
             5, // Mode 6   - 0x0e
            13, // Mode 14  - 0x0f
            -1, // Invalid  - 0x10
            -1, // Invalid  - 0x11
             6, // Mode 7   - 0x12
            -1, // Reserved - 0x13
            -1, // Invalid  - 0x14
            -1, // Invalid  - 0x15
             7, // Mode 8   - 0x16
            -1, // Reserved - 0x17
            -1, // Invalid  - 0x18
            -1, // Invalid  - 0x19
             8, // Mode 9   - 0x1a
            -1, // Reserved - 0x1b
            -1, // Invalid  - 0x1c
            -1, // Invalid  - 0x1d
             9, // Mode 10  - 0x1e
            -1, // Resreved - 0x1f
        };

        private readonly byte[] currentBlock;

        public Bc6hDds(DdsHeader ddsHeader, DdsHeaderDxt10 ddsHeaderDxt10)
            : base(ddsHeader, ddsHeaderDxt10)
        {
            currentBlock = new byte[CompressedBytesPerBlock];
        }

        public override int BitsPerPixel => PixelDepthBytes * 8;
        public override ImageFormat Format => ImageFormat.Rgba32;
        protected override byte PixelDepthBytes => 4;
        protected override byte DivSize => 4;
        protected override byte CompressedBytesPerBlock => 16;

        protected override int Decode(Span<byte> stream, Span<byte> data, int streamIndex, int dataIndex, int stride)
        {
            // I would prefer to use Span, but not sure if I should reference System.Memory in this project
            // copy data instead
            Buffer.BlockCopy(stream.ToArray(), streamIndex, currentBlock, 0, currentBlock.Length);
            streamIndex += currentBlock.Length;

            uint uStartBit = 0;
            byte uMode = GetBits(ref uStartBit, 2u);
            if (uMode != 0x00 && uMode != 0x01)
            {
                uMode = (byte)((GetBits(ref uStartBit, 3) << 2) | uMode);
            }

            Debug.Assert(uMode < 32);

            if (ms_aModeToInfo[uMode] >= 0)
            {
                bool bSigned = DdsHeaderDxt10.DxgiFormat == DxgiFormat.BC6H_SF16;

                Debug.Assert(ms_aModeToInfo[uMode] < ms_aInfo.Length);
                ModeDescriptor[] desc = ms_aDesc[ms_aModeToInfo[uMode]];

                Debug.Assert(ms_aModeToInfo[uMode] < ms_aDesc.Length);
                ref ModeInfo info = ref ms_aInfo[ms_aModeToInfo[uMode]];

                INTEndPntPair[] aEndPts = new INTEndPntPair[Constants.BC6H_MAX_REGIONS];
                for (int i = 0; i < aEndPts.Length; ++i)
                {
                    aEndPts[i] = new INTEndPntPair(new INTColor(), new INTColor());
                }
                uint uShape = 0;

                // Read header
                uint uHeaderBits = info.uPartitions > 0 ? 82u : 65u;
                while (uStartBit < uHeaderBits)
                {
                    uint uCurBit = uStartBit;
                    if (GetBit(ref uStartBit) != 0)
                    {
                        switch (desc[uCurBit].m_eField)
                        {
                            case EField.D: uShape |= 1u << (desc[uCurBit].m_uBit); break;
                            case EField.RW: aEndPts[0].A.r |= 1 << (desc[uCurBit].m_uBit); break;
                            case EField.RX: aEndPts[0].B.r |= 1 << (desc[uCurBit].m_uBit); break;
                            case EField.RY: aEndPts[1].A.r |= 1 << (desc[uCurBit].m_uBit); break;
                            case EField.RZ: aEndPts[1].B.r |= 1 << (desc[uCurBit].m_uBit); break;
                            case EField.GW: aEndPts[0].A.g |= 1 << (desc[uCurBit].m_uBit); break;
                            case EField.GX: aEndPts[0].B.g |= 1 << (desc[uCurBit].m_uBit); break;
                            case EField.GY: aEndPts[1].A.g |= 1 << (desc[uCurBit].m_uBit); break;
                            case EField.GZ: aEndPts[1].B.g |= 1 << (desc[uCurBit].m_uBit); break;
                            case EField.BW: aEndPts[0].A.b |= 1 << (desc[uCurBit].m_uBit); break;
                            case EField.BX: aEndPts[0].B.b |= 1 << (desc[uCurBit].m_uBit); break;
                            case EField.BY: aEndPts[1].A.b |= 1 << (desc[uCurBit].m_uBit); break;
                            case EField.BZ: aEndPts[1].B.b |= 1 << (desc[uCurBit].m_uBit); break;
                            default:
                                {
                                    Debug.WriteLine("BC6H: Invalid header bits encountered during decoding");
                                    Helpers.FillWithErrorColors(data, ref dataIndex, Constants.NUM_PIXELS_PER_BLOCK, DivSize, stride);
                                    return streamIndex;
                                }
                        }
                    }
                }

                Debug.Assert(uShape < 64);

                // Sign extend necessary end points
                if (bSigned)
                {
                    aEndPts[0].A.SignExtend(info.RGBAPrec[0][0]);
                }
                if (bSigned || info.bTransformed)
                {
                    Debug.Assert(info.uPartitions < Constants.BC6H_MAX_REGIONS);
                    for (int p = 0; p <= info.uPartitions; ++p)
                    {
                        if (p != 0)
                        {
                            aEndPts[p].A.SignExtend(info.RGBAPrec[p][0]);
                        }
                        aEndPts[p].B.SignExtend(info.RGBAPrec[p][1]);
                    }
                }

                // Inverse transform the end points
                if (info.bTransformed)
                {
                    Helpers.TransformInverse(aEndPts, info.RGBAPrec[0][0], bSigned);
                }

                // Read indices
                for (int i = 0; i < Constants.NUM_PIXELS_PER_BLOCK; ++i)
                {
                    uint uNumBits = Helpers.IsFixUpOffset(info.uPartitions, (byte)uShape, i) ? info.uIndexPrec - 1u : info.uIndexPrec;
                    if (uStartBit + uNumBits > 128)
                    {
                        Debug.WriteLine("BC6H: Invalid block encountered during decoding");
                        Helpers.FillWithErrorColors(data, ref dataIndex, Constants.NUM_PIXELS_PER_BLOCK, DivSize, stride);
                        return streamIndex;
                    }
                    uint uIndex = GetBits(ref uStartBit, uNumBits);

                    if (uIndex >= ((info.uPartitions > 0) ? 8 : 16))
                    {
                        Debug.WriteLine("BC6H: Invalid index encountered during decoding");
                        Helpers.FillWithErrorColors(data, ref dataIndex, Constants.NUM_PIXELS_PER_BLOCK, DivSize, stride);
                        return streamIndex;
                    }

                    uint uRegion = Constants.g_aPartitionTable[info.uPartitions][uShape][i];
                    Debug.Assert(uRegion < Constants.BC6H_MAX_REGIONS);

                    // Unquantize endpoints and interpolate
                    int r1 = Unquantize(aEndPts[uRegion].A.r, info.RGBAPrec[0][0].r, bSigned);
                    int g1 = Unquantize(aEndPts[uRegion].A.g, info.RGBAPrec[0][0].g, bSigned);
                    int b1 = Unquantize(aEndPts[uRegion].A.b, info.RGBAPrec[0][0].b, bSigned);
                    int r2 = Unquantize(aEndPts[uRegion].B.r, info.RGBAPrec[0][0].r, bSigned);
                    int g2 = Unquantize(aEndPts[uRegion].B.g, info.RGBAPrec[0][0].g, bSigned);
                    int b2 = Unquantize(aEndPts[uRegion].B.b, info.RGBAPrec[0][0].b, bSigned);
                    int[] aWeights = info.uPartitions > 0 ? Constants.g_aWeights3 : Constants.g_aWeights4;
                    INTColor fc = new INTColor();
                    fc.r = FinishUnquantize((r1 * (Constants.BC67_WEIGHT_MAX - aWeights[uIndex]) + r2 * aWeights[uIndex] + Constants.BC67_WEIGHT_ROUND) >> Constants.BC67_WEIGHT_SHIFT, bSigned);
                    fc.g = FinishUnquantize((g1 * (Constants.BC67_WEIGHT_MAX - aWeights[uIndex]) + g2 * aWeights[uIndex] + Constants.BC67_WEIGHT_ROUND) >> Constants.BC67_WEIGHT_SHIFT, bSigned);
                    fc.b = FinishUnquantize((b1 * (Constants.BC67_WEIGHT_MAX - aWeights[uIndex]) + b2 * aWeights[uIndex] + Constants.BC67_WEIGHT_ROUND) >> Constants.BC67_WEIGHT_SHIFT, bSigned);

                    ushort[] rgb = new ushort[3];
                    fc.ToF16(rgb, bSigned);

                    // Clamp 0..1, and convert to byte (we're losing high dynamic range)
                    data[dataIndex++] = (byte)((Math.Max(0.0f, Math.Min(1.0f, ConvertHalfToFloat(rgb[2]))) * 255.0f) + 0.5f); // blue
                    data[dataIndex++] = (byte)((Math.Max(0.0f, Math.Min(1.0f, ConvertHalfToFloat(rgb[1]))) * 255.0f) + 0.5f); // green
                    data[dataIndex++] = (byte)((Math.Max(0.0f, Math.Min(1.0f, ConvertHalfToFloat(rgb[0]))) * 255.0f) + 0.5f); // red
                    data[dataIndex++] = 255;

                    // Is mult 4?
                    if (((i + 1) & 0x3) == 0)
                        dataIndex += PixelDepthBytes * (stride - DivSize);
                }
            }
            else
            {
                string warnstr = "BC6H: Invalid mode encountered during decoding";
                switch (uMode)
                {
                    case 0x13: warnstr = "BC6H: Reserved mode 10011 encountered during decoding"; break;
                    case 0x17: warnstr = "BC6H: Reserved mode 10111 encountered during decoding"; break;
                    case 0x1B: warnstr = "BC6H: Reserved mode 11011 encountered during decoding"; break;
                    case 0x1F: warnstr = "BC6H: Reserved mode 11111 encountered during decoding"; break;
                }
                Debug.WriteLine(warnstr);

                // Per the BC6H format spec, we must return opaque black
                for (int i = 0; i < Constants.NUM_PIXELS_PER_BLOCK; ++i)
                {
                    data[dataIndex++] = 0;
                    data[dataIndex++] = 0;
                    data[dataIndex++] = 0;
                    data[dataIndex++] = 0;

                    // Is mult 4?
                    if (((i + 1) & 0x3) == 0)
                    {
                        dataIndex += this.PixelDepthBytes * (stride - this.DivSize);
                    }
                }
            }

            return streamIndex;
        }

        public byte GetBit(ref uint uStartBit)
        {
            Debug.Assert(uStartBit < 128);
            uint uIndex = uStartBit >> 3;
            var ret = (byte)((currentBlock[uIndex] >> (int)(uStartBit - (uIndex << 3))) & 0x01);
            uStartBit++;
            return ret;
        }
        public byte GetBits(ref uint uStartBit, uint uNumBits)
        {
            if (uNumBits == 0) return 0;
            Debug.Assert(uStartBit + uNumBits <= 128 && uNumBits <= 8);
            byte ret;
            uint uIndex = uStartBit >> 3;
            uint uBase = uStartBit - (uIndex << 3);
            if (uBase + uNumBits > 8)
            {
                uint uFirstIndexBits = 8 - uBase;
                uint uNextIndexBits = uNumBits - uFirstIndexBits;
                ret = (byte)((uint)(currentBlock[uIndex] >> (int)uBase) | ((currentBlock[uIndex + 1] & ((1u << (int)uNextIndexBits) - 1)) << (int)uFirstIndexBits));
            }
            else
            {
                ret = (byte)((currentBlock[uIndex] >> (int)uBase) & ((1 << (int)uNumBits) - 1));
            }
            Debug.Assert(ret < (1 << (int)uNumBits));
            uStartBit += uNumBits;
            return ret;
        }

        private static int Unquantize(int comp, byte uBitsPerComp, bool bSigned)
        {
            int unq = 0, s = 0;
            if (bSigned)
            {
                if (uBitsPerComp >= 16)
                {
                    unq = comp;
                }
                else
                {
                    if (comp < 0)
                    {
                        s = 1;
                        comp = -comp;
                    }

                    if (comp == 0) unq = 0;
                    else if (comp >= ((1 << (uBitsPerComp - 1)) - 1)) unq = 0x7FFF;
                    else unq = ((comp << 15) + 0x4000) >> (uBitsPerComp - 1);

                    if (s != 0) unq = -unq;
                }
            }
            else
            {
                if (uBitsPerComp >= 15) unq = comp;
                else if (comp == 0) unq = 0;
                else if (comp == ((1 << uBitsPerComp) - 1)) unq = 0xFFFF;
                else unq = ((comp << 16) + 0x8000) >> uBitsPerComp;
            }

            return unq;
        }
        private static int FinishUnquantize(int comp, bool bSigned)
        {
            if (bSigned)
            {
                return (comp < 0) ? -(((-comp) * 31) >> 5) : (comp * 31) >> 5;  // scale the magnitude by 31/32
            }
            else
            {
                return (comp * 31) >> 6;                                        // scale the magnitude by 31/64
            }
        }

        private static float ConvertHalfToFloat(ushort Value)
        {
            uint Mantissa = (uint)(Value & 0x03FF);

            uint Exponent = (uint)(Value & 0x7C00);
            if (Exponent == 0x7C00) // INF/NAN
            {
                Exponent = 0x8f;
            }
            else if (Exponent != 0)  // The value is normalized
            {
                Exponent = (uint)(((int)Value >> 10) & 0x1F);
            }
            else if (Mantissa != 0)     // The value is denormalized
            {
                // Normalize the value in the resulting float
                Exponent = 1;

                do
                {
                    Exponent--;
                    Mantissa <<= 1;
                } while ((Mantissa & 0x0400) == 0);

                Mantissa &= 0x03FF;
            }
            else                        // The value is zero
            {
                Exponent = unchecked((uint)(-112));
            }

            uint Result =
                ((uint)(Value & 0x8000) << 16) // Sign
                | ((Exponent + 112) << 23)                      // Exponent
                | (Mantissa << 13);                             // Mantissa

            float val;
            unsafe
            {
                val = *((float*)(&Result));
            }

            return val;
        }
    }
}
