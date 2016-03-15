namespace ImageProcessorCore.Formats.Jpg
{
	using System;
	using System.IO;

	internal partial class Encoder
	{
		private const int sof0Marker = 0xc0; // Start Of Frame (Baseline).
		private const int sof1Marker = 0xc1; // Start Of Frame (Extended Sequential).
		private const int sof2Marker = 0xc2; // Start Of Frame (Progressive).
		private const int dhtMarker  = 0xc4; // Define Huffman Table.
		private const int rst0Marker = 0xd0; // ReSTart (0).
		private const int rst7Marker = 0xd7; // ReSTart (7).
		private const int soiMarker  = 0xd8; // Start Of Image.
		private const int eoiMarker  = 0xd9; // End Of Image.
		private const int sosMarker  = 0xda; // Start Of Scan.
		private const int dqtMarker  = 0xdb; // Define Quantization Table.
		private const int driMarker  = 0xdd; // Define Restart Interval.
		private const int comMarker  = 0xfe; // COMment.
		// "APPlication specific" markers aren't part of the JPEG spec per se,
		// but in practice, their use is described at
		// http://www.sno.phy.queensu.ca/~phil/exiftool/TagNames/JPEG.html
		private const int app0Marker  = 0xe0;
		private const int app14Marker = 0xee;
		private const int app15Marker = 0xef;

		// bitCount counts the number of bits needed to hold an integer.
		private readonly byte[] bitCount = {
			0, 1, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 4, 4, 4, 4,
			5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5,
			6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6,
			6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6,
			7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
			7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
			7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
			7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
			8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
			8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
			8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
			8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
			8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
			8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
			8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
			8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, };

		// unzig maps from the zig-zag ordering to the natural ordering. For example,
		// unzig[3] is the column and row of the fourth element in zig-zag order. The
		// value is 16, which means first column (16%8 == 0) and third row (16/8 == 2).
		private static readonly int[] unzig = new int[] {
			0, 1, 8, 16, 9, 2, 3, 10,
			17, 24, 32, 25, 18, 11, 4, 5,
			12, 19, 26, 33, 40, 48, 41, 34,
			27, 20, 13, 6, 7, 14, 21, 28,
			35, 42, 49, 56, 57, 50, 43, 36,
			29, 22, 15, 23, 30, 37, 44, 51,
			58, 59, 52, 45, 38, 31, 39, 46,
			53, 60, 61, 54, 47, 55, 62, 63,
		};

		private const int nQuantIndex = 2;
		private const int nHuffIndex = 4;

		private enum quantIndex
		{
			quantIndexLuminance = 0,
			quantIndexChrominance = 1,
		}

		private enum huffIndex
		{
			huffIndexLuminanceDC = 0,
			huffIndexLuminanceAC = 1,
			huffIndexChrominanceDC = 2,
			huffIndexChrominanceAC = 3,
		}

		// unscaledQuant are the unscaled quantization tables in zig-zag order. Each
		// encoder copies and scales the tables according to its quality parameter.
		// The values are derived from section K.1 after converting from natural to
		// zig-zag order.
		private byte[,] unscaledQuant = new byte[,] {
			// Luminance.
			{
				16, 11, 12, 14, 12, 10, 16, 14,
				13, 14, 18, 17, 16, 19, 24, 40,
				26, 24, 22, 22, 24, 49, 35, 37,
				29, 40, 58, 51, 61, 60, 57, 51,
				56, 55, 64, 72, 92, 78, 64, 68,
				87, 69, 55, 56, 80, 109, 81, 87,
				95, 98, 103, 104, 103, 62, 77, 113,
				121, 112, 100, 120, 92, 101, 103, 99,
			},
			// Chrominance.
			{
				17, 18, 18, 24, 21, 24, 47, 26,
				26, 47, 99, 66, 56, 66, 99, 99,
				99, 99, 99, 99, 99, 99, 99, 99,
				99, 99, 99, 99, 99, 99, 99, 99,
				99, 99, 99, 99, 99, 99, 99, 99,
				99, 99, 99, 99, 99, 99, 99, 99,
				99, 99, 99, 99, 99, 99, 99, 99,
				99, 99, 99, 99, 99, 99, 99, 99,
			},
		};

		private class huffmanSpec
		{
			public huffmanSpec(byte[] c, byte[] v) { count = c; values = v; }
			public byte[] count;
			public byte[] values;
		};

		// theHuffmanSpec is the Huffman encoding specifications.
		// This encoder uses the same Huffman encoding for all images.
		private huffmanSpec[] theHuffmanSpec = new huffmanSpec[] {
			// Luminance DC.
			new huffmanSpec(
				new byte[] {0, 1, 5, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0},
				new byte[] {0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11}
			),
			new huffmanSpec(
				new byte[] {0, 2, 1, 3, 3, 2, 4, 3, 5, 5, 4, 4, 0, 0, 1, 125},
				new byte[] {
					0x01, 0x02, 0x03, 0x00, 0x04, 0x11, 0x05, 0x12,
					0x21, 0x31, 0x41, 0x06, 0x13, 0x51, 0x61, 0x07,
					0x22, 0x71, 0x14, 0x32, 0x81, 0x91, 0xa1, 0x08,
					0x23, 0x42, 0xb1, 0xc1, 0x15, 0x52, 0xd1, 0xf0,
					0x24, 0x33, 0x62, 0x72, 0x82, 0x09, 0x0a, 0x16,
					0x17, 0x18, 0x19, 0x1a, 0x25, 0x26, 0x27, 0x28,
					0x29, 0x2a, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39,
					0x3a, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49,
					0x4a, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59,
					0x5a, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69,
					0x6a, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79,
					0x7a, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89,
					0x8a, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98,
					0x99, 0x9a, 0xa2, 0xa3, 0xa4, 0xa5, 0xa6, 0xa7,
					0xa8, 0xa9, 0xaa, 0xb2, 0xb3, 0xb4, 0xb5, 0xb6,
					0xb7, 0xb8, 0xb9, 0xba, 0xc2, 0xc3, 0xc4, 0xc5,
					0xc6, 0xc7, 0xc8, 0xc9, 0xca, 0xd2, 0xd3, 0xd4,
					0xd5, 0xd6, 0xd7, 0xd8, 0xd9, 0xda, 0xe1, 0xe2,
					0xe3, 0xe4, 0xe5, 0xe6, 0xe7, 0xe8, 0xe9, 0xea,
					0xf1, 0xf2, 0xf3, 0xf4, 0xf5, 0xf6, 0xf7, 0xf8,
					0xf9, 0xfa,
				}
			),
			new huffmanSpec(
				new byte[] {0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0},
				new byte[] {0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11}
			),
			// Chrominance AC.
			new huffmanSpec(
				new byte[] {0, 2, 1, 2, 4, 4, 3, 4, 7, 5, 4, 4, 0, 1, 2, 119},
				new byte[] {
					0x00, 0x01, 0x02, 0x03, 0x11, 0x04, 0x05, 0x21,
					0x31, 0x06, 0x12, 0x41, 0x51, 0x07, 0x61, 0x71,
					0x13, 0x22, 0x32, 0x81, 0x08, 0x14, 0x42, 0x91,
					0xa1, 0xb1, 0xc1, 0x09, 0x23, 0x33, 0x52, 0xf0,
					0x15, 0x62, 0x72, 0xd1, 0x0a, 0x16, 0x24, 0x34,
					0xe1, 0x25, 0xf1, 0x17, 0x18, 0x19, 0x1a, 0x26,
					0x27, 0x28, 0x29, 0x2a, 0x35, 0x36, 0x37, 0x38,
					0x39, 0x3a, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48,
					0x49, 0x4a, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58,
					0x59, 0x5a, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68,
					0x69, 0x6a, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78,
					0x79, 0x7a, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87,
					0x88, 0x89, 0x8a, 0x92, 0x93, 0x94, 0x95, 0x96,
					0x97, 0x98, 0x99, 0x9a, 0xa2, 0xa3, 0xa4, 0xa5,
					0xa6, 0xa7, 0xa8, 0xa9, 0xaa, 0xb2, 0xb3, 0xb4,
					0xb5, 0xb6, 0xb7, 0xb8, 0xb9, 0xba, 0xc2, 0xc3,
					0xc4, 0xc5, 0xc6, 0xc7, 0xc8, 0xc9, 0xca, 0xd2,
					0xd3, 0xd4, 0xd5, 0xd6, 0xd7, 0xd8, 0xd9, 0xda,
					0xe2, 0xe3, 0xe4, 0xe5, 0xe6, 0xe7, 0xe8, 0xe9,
					0xea, 0xf2, 0xf3, 0xf4, 0xf5, 0xf6, 0xf7, 0xf8,
					0xf9, 0xfa,
				}
			),
		};

		// huffmanLUT is a compiled look-up table representation of a huffmanSpec.
		// Each value maps to a uint32 of which the 8 most significant bits hold the
		// codeword size in bits and the 24 least significant bits hold the codeword.
		// The maximum codeword size is 16 bits.
		private class huffmanLUT
		{
			public uint[] values;

			public huffmanLUT(huffmanSpec s)
			{
				int maxValue = 0;

				foreach(var v in s.values)
				{
					if(v > maxValue)
						maxValue = v;
				}

				values = new uint[maxValue+1];

				int code = 0;
				int k = 0;

				for(int i = 0; i < s.count.Length; i++)
				{
					int nBits = (i+1) << 24;
					for(int j = 0; j < s.count[i]; j++)
					{
						values[s.values[k]] = (uint)(nBits | code);
						code++;
						k++;
					}
					code <<= 1;
				}
			}
		}

		// w is the writer to write to. err is the first error encountered during
		// writing. All attempted writes after the first error become no-ops.
		private Stream w;
		// buf is a scratch buffer.
		private byte[] buf = new byte[16];
		// bits and nBits are accumulated bits to write to w.
		private uint bits, nBits;
		// quant is the scaled quantization tables, in zig-zag order.
		private byte[][] quant = new byte[nQuantIndex][];//[Block.blockSize];
		// theHuffmanLUT are compiled representations of theHuffmanSpec.
		private huffmanLUT[] theHuffmanLUT = new huffmanLUT[4];

		private void writeByte(byte b)
		{
			var data = new byte[1];
			data[0] = b;
			w.Write(data, 0, 1);
		}

		// emit emits the least significant nBits bits of bits to the bit-stream.
		// The precondition is bits < 1<<nBits && nBits <= 16.
		private void emit(uint bits, uint nBits)
		{
			nBits += this.nBits;
			bits <<= (int)(32 - nBits);
			bits |= this.bits;
			while(nBits >= 8)
			{
				byte b = (byte)(bits >> 24);
				writeByte(b);
				if(b == 0xff)
					writeByte(0x00);
				bits <<= 8;
				nBits -= 8;
			}
			this.bits = bits;
			this.nBits = nBits;
		}

		// emitHuff emits the given value with the given Huffman encoder.
		private void emitHuff(huffIndex h, int v)
		{
			uint x = theHuffmanLUT[(int)h].values[v];
			emit(x&((1<<24)-1), x>>24);
		}

		// emitHuffRLE emits a run of runLength copies of value encoded with the given
		// Huffman encoder.
		private void emitHuffRLE(huffIndex h, int runLength, int v)
		{
			int a = v;
			int b = v;
			if(a < 0)
			{
				a = -v;
				b = v-1;
			}
			uint nBits = 0;
			if(a < 0x100)
				nBits = bitCount[a];
			else
				nBits = 8 + (uint)bitCount[a>>8];

			emitHuff(h, (int)((runLength<<4)|nBits));
			if(nBits > 0)
				emit((uint)b & (uint)((1 << ((int)nBits)) - 1), nBits);
		}

		// writeMarkerHeader writes the header for a marker with the given length.
		private void writeMarkerHeader(byte marker, int markerlen)
		{
			buf[0] = 0xff;
			buf[1] = marker;
			buf[2] = (byte)(markerlen >> 8);
			buf[3] = (byte)(markerlen & 0xff);
			w.Write(buf, 0, 4);
		}

		// writeDQT writes the Define Quantization Table marker.
		private void writeDQT()
		{
			int markerlen = 2 + nQuantIndex*(1+Block.blockSize);
			writeMarkerHeader(dqtMarker, markerlen);
			for(int i = 0; i < nQuantIndex; i++)
			{ 
				writeByte((byte)i);
				w.Write(quant[i], 0, quant[i].Length);
			}
		}

		// writeSOF0 writes the Start Of Frame (Baseline) marker.
		private void writeSOF0(int wid, int hei, int nComponent)
		{
			byte[] chroma1 = new byte[] { 0x22, 0x11, 0x11 };
			byte[] chroma2 = new byte[] { 0x00, 0x01, 0x01 };

			int markerlen = 8 + 3*nComponent;
			writeMarkerHeader(sof0Marker, markerlen);
			buf[0] = 8; // 8-bit color.
			buf[1] = (byte)(hei >> 8);
			buf[2] = (byte)(hei & 0xff);
			buf[3] = (byte)(wid >> 8);
			buf[4] = (byte)(wid & 0xff);
			buf[5] = (byte)(nComponent);
			if(nComponent == 1)
			{
				buf[6] = 1;
				// No subsampling for grayscale image.
				buf[7] = 0x11;
				buf[8] = 0x00;
			}
			else
			{
				for(int i = 0; i < nComponent; i++)
				{
					buf[3*i+6] = (byte)(i + 1);
					// We use 4:2:0 chroma subsampling.
					buf[3*i+7] = chroma1[i];
					buf[3*i+8] = chroma2[i];
				}
			}
			w.Write(buf, 0, 3*(nComponent-1)+9);
		}

		// writeDHT writes the Define Huffman Table marker.
		private void writeDHT(int nComponent)
		{
			byte[] headers = new byte[] { 0x00, 0x10, 0x01, 0x11 };
			int markerlen = 2;
			huffmanSpec[] specs = theHuffmanSpec;

			if(nComponent == 1)
			{
				// Drop the Chrominance tables.
				specs = new huffmanSpec[] { theHuffmanSpec[0], theHuffmanSpec[1] };
			}

			foreach(var s in specs)
				markerlen += 1 + 16 + s.values.Length;

			writeMarkerHeader(dhtMarker, markerlen);
			for(int i = 0; i < specs.Length; i++)
			{
				var s = specs[i];

				writeByte(headers[i]);
				w.Write(s.count, 0, s.count.Length);
				w.Write(s.values, 0, s.values.Length);
			}
		}

		// writeBlock writes a block of pixel data using the given quantization table,
		// returning the post-quantized DC value of the DCT-transformed block. b is in
		// natural (not zig-zag) order.
		private int writeBlock(Block b, quantIndex q, int prevDC)
		{
			FDCT(b);

			// Emit the DC delta.
			int dc = div(b[0], 8*quant[(int)q][0]);
			emitHuffRLE((huffIndex)(2*(int)q+0), 0, dc-prevDC);

			// Emit the AC components.
			var h = (huffIndex)(2*(int)q+1);
			int runLength = 0;
			
			for(int zig = 1; zig < Block.blockSize; zig++)
			{
				int ac = div(b[unzig[zig]], 8*quant[(int)q][zig]);

				if(ac == 0)
				{
					runLength++;
				}
				else
				{
					while(runLength > 15)
					{
						emitHuff(h, 0xf0);
						runLength -= 16;
					}

					emitHuffRLE(h, runLength, ac);
					runLength = 0;
				}
			}
			if(runLength > 0)
				emitHuff(h, 0x00);
			return dc;
		}

		// toYCbCr converts the 8x8 region of m whose top-left corner is p to its
		// YCbCr values.
		private void toYCbCr(ImageBase m, int x, int y, Block yBlock, Block cbBlock, Block crBlock)
		{
			int xmax = m.Width - 1;
			int ymax = m.Height - 1;
			for(int j = 0; j < 8; j++)
			{
				for(int i = 0; i < 8; i++)
				{
					byte yy, cb, cr;

					var c = m[Math.Min(x+i, xmax), Math.Min(y+j, ymax)];
					Colors.RGBToYCbCr((byte)(c.R*255), (byte)(c.G*255), (byte)(c.B*255), out yy, out cb, out cr);
					yBlock[8*j+i] = yy;
					cbBlock[8*j+i] = cb;
					crBlock[8*j+i] = cr;
				}
			}
		}

		// grayToY stores the 8x8 region of m whose top-left corner is p in yBlock.
		/*func grayToY(m *image.Gray, p image.Point, yBlock *block) {
			b := m.Bounds()
			xmax := b.Max.X - 1
			ymax := b.Max.Y - 1
			pix := m.Pix
			for j := 0; j < 8; j++ {
				for i := 0; i < 8; i++ {
					idx := m.PixOffset(min(p.X+i, xmax), min(p.Y+j, ymax))
					yBlock[8*j+i] = int32(pix[idx])
				}
			}
		}

		// rgbaToYCbCr is a specialized version of toYCbCr for image.RGBA images.
		func rgbaToYCbCr(m *image.RGBA, p image.Point, yBlock, cbBlock, crBlock *block) {
			b := m.Bounds()
			xmax := b.Max.X - 1
			ymax := b.Max.Y - 1
			for j := 0; j < 8; j++ {
				sj := p.Y + j
				if sj > ymax {
					sj = ymax
				}
				offset := (sj-b.Min.Y)*m.Stride - b.Min.X*4
				for i := 0; i < 8; i++ {
					sx := p.X + i
					if sx > xmax {
						sx = xmax
					}
					pix := m.Pix[offset+sx*4:]
					yy, cb, cr := color.RGBToYCbCr(pix[0], pix[1], pix[2])
					yBlock[8*j+i] = int32(yy)
					cbBlock[8*j+i] = int32(cb)
					crBlock[8*j+i] = int32(cr)
				}
			}
		}*/

		// scale scales the 16x16 region represented by the 4 src blocks to the 8x8
		// dst block.
		private void scale(Block dst, Block[] src)
		{
			for(int i = 0; i < 4; i++)
			{
				int dstOff = ((i&2)<<4) | ((i&1)<<2);
				for(int y = 0; y < 4; y++)
				{
					for(int x = 0; x < 4; x++)
					{
						int j = 16*y + 2*x;
						int sum = src[i][j] + src[i][j+1] + src[i][j+8] + src[i][j+9];
						dst[8*y+x+dstOff] = (sum + 2) >> 2;
					}
				}
			}
		}

		// sosHeaderY is the SOS marker "\xff\xda" followed by 8 bytes:
		//	- the marker length "\x00\x08",
		//	- the number of components "\x01",
		//	- component 1 uses DC table 0 and AC table 0 "\x01\x00",
		//	- the bytes "\x00\x3f\x00". Section B.2.3 of the spec says that for
		//	  sequential DCTs, those bytes (8-bit Ss, 8-bit Se, 4-bit Ah, 4-bit Al)
		//	  should be 0x00, 0x3f, 0x00<<4 | 0x00.
		private readonly byte[] sosHeaderY = new byte[] {
			0xff, 0xda, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3f, 0x00,
		};

		// sosHeaderYCbCr is the SOS marker "\xff\xda" followed by 12 bytes:
		//	- the marker length "\x00\x0c",
		//	- the number of components "\x03",
		//	- component 1 uses DC table 0 and AC table 0 "\x01\x00",
		//	- component 2 uses DC table 1 and AC table 1 "\x02\x11",
		//	- component 3 uses DC table 1 and AC table 1 "\x03\x11",
		//	- the bytes "\x00\x3f\x00". Section B.2.3 of the spec says that for
		//	  sequential DCTs, those bytes (8-bit Ss, 8-bit Se, 4-bit Ah, 4-bit Al)
		//	  should be 0x00, 0x3f, 0x00<<4 | 0x00.
		private readonly byte[] sosHeaderYCbCr = new byte[] {
			0xff, 0xda, 0x00, 0x0c, 0x03, 0x01, 0x00, 0x02,
			0x11, 0x03, 0x11, 0x00, 0x3f, 0x00,
		};

		// writeSOS writes the StartOfScan marker.
		private void writeSOS(ImageBase m)
		{
			w.Write(sosHeaderYCbCr, 0, sosHeaderYCbCr.Length);

			Block b = new Block();
			Block[] cb = new Block[4];
			Block[] cr = new Block[4];
			int prevDCY = 0, prevDCCb = 0, prevDCCr = 0;

			for(int i = 0; i < 4; i++) cb[i] = new Block();
			for(int i = 0; i < 4; i++) cr[i] = new Block();

			for(int y = 0; y < m.Height; y += 16)
			{
				for(int x = 0; x < m.Width; x += 16)
				{
					for(int i = 0; i < 4; i++)
					{
						int xOff = (i & 1) * 8;
						int yOff = (i & 2) * 4;

						toYCbCr(m, x+xOff, y+yOff, b, cb[i], cr[i]);
						prevDCY = writeBlock(b, 0, prevDCY);
					}
					scale(b, cb);
					prevDCCb = writeBlock(b, (quantIndex)1, prevDCCb);
					scale(b, cr);
					prevDCCr = writeBlock(b, (quantIndex)1, prevDCCr);
				}
			}

			// Pad the last byte with 1's.
			emit(0x7f, 7);
		}

		// Encode writes the Image m to w in JPEG 4:2:0 baseline format with the given
		// options. Default parameters are used if a nil *Options is passed.
		public void Encode(Stream w, ImageBase m, int quality)
		{
			this.w = w;

			for(int i = 0; i < theHuffmanSpec.Length; i++)
				theHuffmanLUT[i] = new huffmanLUT(theHuffmanSpec[i]);

			for(int i = 0; i < nQuantIndex; i++)
				quant[i] = new byte[Block.blockSize];

			if(m.Width >= (1<<16) || m.Height >= (1<<16))
				throw new Exception("jpeg: image is too large to encode");

			if(quality < 1) quality = 1;
			if(quality > 100) quality = 100;

			// Convert from a quality rating to a scaling factor.
			int scale;
			if(quality < 50)
				scale = 5000 / quality;
			else
				scale = 200 - quality*2;

			// Initialize the quantization tables.
	
			for(int i = 0; i < nQuantIndex; i++)
			{
				for(int j = 0; j < Block.blockSize; j++)
				{
					int x = unscaledQuant[i,j];
					x = (x*scale + 50) / 100;
					if(x < 1) x = 1;
					if(x > 255) x = 255;
					quant[i][j] = (byte)x;
				}
			}

			// Compute number of components based on input image type.
			int nComponent = 3;

			// Write the Start Of Image marker.
			buf[0] = 0xff;
			buf[1] = 0xd8;
			w.Write(buf, 0, 2);

			// Write the quantization tables.
			writeDQT();

			// Write the image dimensions.
			writeSOF0(m.Width, m.Height, nComponent);

			// Write the Huffman tables.
			writeDHT(nComponent);

			// Write the image data.
			writeSOS(m);

			// Write the End Of Image marker.
			buf[0] = 0xff;
			buf[1] = 0xd9;
			w.Write(buf, 0, 2);
			w.Flush();
		}

		// div returns a/b rounded to the nearest integer, instead of rounded to zero.
		private static int div(int a, int b)
		{
			if(a >= 0)
				return (a + (b >> 1)) / b;
			else
				return -((-a + (b >> 1)) / b);
		}
	}
}
