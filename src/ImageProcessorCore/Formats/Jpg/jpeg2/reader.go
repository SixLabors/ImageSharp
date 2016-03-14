// Copyright 2009 The Go Authors. All rights reserved.
// Use of this source code is governed by a BSD-style
// license that can be found in the LICENSE file.

// Package golang implements a JPEG image decoder and encoder.
//
// JPEG is defined in ITU-T T.81: http://www.w3.org/Graphics/JPEG/itu-t81.pdf.
package jpeg2

import (
	"fmt"
	"image"
	"image/color"
	"io"
)

// TODO(nigeltao): fix up the doc comment style so that sentences start with
// the name of the type or function that they annotate.

// A FormatError reports that the input is not a valid JPEG.
type FormatError string

func (e FormatError) Error() string { return "invalid JPEG format: " + string(e) }

// An UnsupportedError reports that the input uses a valid but unimplemented JPEG feature.
type UnsupportedError string

func (e UnsupportedError) Error() string { return "unsupported JPEG feature: " + string(e) }

var errUnsupportedSubsamplingRatio = UnsupportedError("luma/chroma subsampling ratio")

// Component specification, specified in section B.2.2.
type component struct {
	h  int   // Horizontal sampling factor.
	v  int   // Vertical sampling factor.
	c  uint8 // Component identifier.
	tq uint8 // Quantization table destination selector.
}

const (
	dcTable = 0
	acTable = 1
	maxTc   = 1
	maxTh   = 3
	maxTq   = 3

	maxComponents = 4
)

const (
	sof0Marker = 0xc0 // Start Of Frame (Baseline).
	sof1Marker = 0xc1 // Start Of Frame (Extended Sequential).
	sof2Marker = 0xc2 // Start Of Frame (Progressive).
	dhtMarker  = 0xc4 // Define Huffman Table.
	rst0Marker = 0xd0 // ReSTart (0).
	rst7Marker = 0xd7 // ReSTart (7).
	soiMarker  = 0xd8 // Start Of Image.
	eoiMarker  = 0xd9 // End Of Image.
	sosMarker  = 0xda // Start Of Scan.
	dqtMarker  = 0xdb // Define Quantization Table.
	driMarker  = 0xdd // Define Restart Interval.
	comMarker  = 0xfe // COMment.
	// "APPlication specific" markers aren't part of the JPEG spec per se,
	// but in practice, their use is described at
	// http://www.sno.phy.queensu.ca/~phil/exiftool/TagNames/JPEG.html
	app0Marker  = 0xe0
	app14Marker = 0xee
	app15Marker = 0xef
)

// See http://www.sno.phy.queensu.ca/~phil/exiftool/TagNames/JPEG.html#Adobe
const (
	adobeTransformUnknown = 0
	adobeTransformYCbCr   = 1
	adobeTransformYCbCrK  = 2
)

// unzig maps from the zig-zag ordering to the natural ordering. For example,
// unzig[3] is the column and row of the fourth element in zig-zag order. The
// value is 16, which means first column (16%8 == 0) and third row (16/8 == 2).
var unzig = [blockSize]int{
	0, 1, 8, 16, 9, 2, 3, 10,
	17, 24, 32, 25, 18, 11, 4, 5,
	12, 19, 26, 33, 40, 48, 41, 34,
	27, 20, 13, 6, 7, 14, 21, 28,
	35, 42, 49, 56, 57, 50, 43, 36,
	29, 22, 15, 23, 30, 37, 44, 51,
	58, 59, 52, 45, 38, 31, 39, 46,
	53, 60, 61, 54, 47, 55, 62, 63,
}

// Deprecated: Reader is deprecated.
type Reader interface {
	io.ByteReader
	io.Reader
}

// bits holds the unprocessed bits that have been taken from the byte-stream.
// The n least significant bits of a form the unread bits, to be read in MSB to
// LSB order.
type bits struct {
	a uint32 // accumulator.
	m uint32 // mask. m==1<<(n-1) when n>0, with m==0 when n==0.
	n int32  // the number of unread bits in a.
}

type decoder struct {
	r    io.Reader
	bits bits
	// bytes is a byte buffer, similar to a bufio.Reader, except that it
	// has to be able to unread more than 1 byte, due to byte stuffing.
	// Byte stuffing is specified in section F.1.2.3.
	bytes struct {
		// buf[i:j] are the buffered bytes read from the underlying
		// io.Reader that haven't yet been passed further on.
		buf  [4096]byte
		i, j int
		// nUnreadable is the number of bytes to back up i after
		// overshooting. It can be 0, 1 or 2.
		nUnreadable int
	}
	width, height int

	img1        *image.Gray
	img3        *image.YCbCr
	blackPix    []byte
	blackStride int

	ri                  int // Restart Interval.
	nComp               int
	progressive         bool
	jfif                bool
	adobeTransformValid bool
	adobeTransform      uint8
	eobRun              uint16 // End-of-Band run, specified in section G.1.2.2.

	comp       [maxComponents]component
	progCoeffs [maxComponents][]block // Saved state between progressive-mode scans.
	huff       [maxTc + 1][maxTh + 1]huffman
	quant      [maxTq + 1]block // Quantization tables, in zig-zag order.
	tmp        [2 * blockSize]byte
}

// fill fills up the d.bytes.buf buffer from the underlying io.Reader. It
// should only be called when there are no unread bytes in d.bytes.
func (d *decoder) fill() error {
	if d.bytes.i != d.bytes.j {
		panic("jpeg: fill called when unread bytes exist")
	}
	// Move the last 2 bytes to the start of the buffer, in case we need
	// to call unreadByteStuffedByte.
	if d.bytes.j > 2 {
		d.bytes.buf[0] = d.bytes.buf[d.bytes.j-2]
		d.bytes.buf[1] = d.bytes.buf[d.bytes.j-1]
		d.bytes.i, d.bytes.j = 2, 2
	}
	// Fill in the rest of the buffer.
	n, err := d.r.Read(d.bytes.buf[d.bytes.j:])
	d.bytes.j += n
	if n > 0 {
		err = nil
	}
	return err
}

// unreadByteStuffedByte undoes the most recent readByteStuffedByte call,
// giving a byte of data back from d.bits to d.bytes. The Huffman look-up table
// requires at least 8 bits for look-up, which means that Huffman decoding can
// sometimes overshoot and read one or two too many bytes. Two-byte overshoot
// can happen when expecting to read a 0xff 0x00 byte-stuffed byte.
func (d *decoder) unreadByteStuffedByte() {
	d.bytes.i -= d.bytes.nUnreadable
	d.bytes.nUnreadable = 0
	if d.bits.n >= 8 {
		d.bits.a >>= 8
		d.bits.n -= 8
		d.bits.m >>= 8
	}
}

// readByte returns the next byte, whether buffered or not buffered. It does
// not care about byte stuffing.
func (d *decoder) readByte() (x byte, err error) {
	for d.bytes.i == d.bytes.j {
		if err = d.fill(); err != nil {
			return 0, err
		}
	}
	x = d.bytes.buf[d.bytes.i]
	d.bytes.i++
	d.bytes.nUnreadable = 0
	return x, nil
}

// errMissingFF00 means that readByteStuffedByte encountered an 0xff byte (a
// marker byte) that wasn't the expected byte-stuffed sequence 0xff, 0x00.
var errMissingFF00 = FormatError("missing 0xff00 sequence")

// readByteStuffedByte is like readByte but is for byte-stuffed Huffman data.
func (d *decoder) readByteStuffedByte() (x byte, err error) {
	// Take the fast path if d.bytes.buf contains at least two bytes.
	if d.bytes.i+2 <= d.bytes.j {
		x = d.bytes.buf[d.bytes.i]
		d.bytes.i++
		d.bytes.nUnreadable = 1
		if x != 0xff {
			return x, err
		}
		if d.bytes.buf[d.bytes.i] != 0x00 {
			return 0, errMissingFF00
		}
		d.bytes.i++
		d.bytes.nUnreadable = 2
		return 0xff, nil
	}

	d.bytes.nUnreadable = 0

	x, err = d.readByte()
	if err != nil {
		return 0, err
	}
	d.bytes.nUnreadable = 1
	if x != 0xff {
		return x, nil
	}

	x, err = d.readByte()
	if err != nil {
		return 0, err
	}
	d.bytes.nUnreadable = 2
	if x != 0x00 {
		return 0, errMissingFF00
	}
	return 0xff, nil
}

// readFull reads exactly len(p) bytes into p. It does not care about byte
// stuffing.
func (d *decoder) readFull(p []byte) error {
	// Unread the overshot bytes, if any.
	if d.bytes.nUnreadable != 0 {
		if d.bits.n >= 8 {
			d.unreadByteStuffedByte()
		}
		d.bytes.nUnreadable = 0
	}

	for {
		n := copy(p, d.bytes.buf[d.bytes.i:d.bytes.j])
		p = p[n:]
		d.bytes.i += n
		if len(p) == 0 {
			break
		}
		if err := d.fill(); err != nil {
			if err == io.EOF {
				err = io.ErrUnexpectedEOF
			}
			return err
		}
	}
	return nil
}

// ignore ignores the next n bytes.
func (d *decoder) ignore(n int) error {
	// Unread the overshot bytes, if any.
	if d.bytes.nUnreadable != 0 {
		if d.bits.n >= 8 {
			d.unreadByteStuffedByte()
		}
		d.bytes.nUnreadable = 0
	}

	for {
		m := d.bytes.j - d.bytes.i
		if m > n {
			m = n
		}
		d.bytes.i += m
		n -= m
		if n == 0 {
			break
		}
		if err := d.fill(); err != nil {
			if err == io.EOF {
				err = io.ErrUnexpectedEOF
			}
			return err
		}
	}
	return nil
}

// Specified in section B.2.2.
func (d *decoder) processSOF(n int) error {
	if d.nComp != 0 {
		return FormatError("multiple SOF markers")
	}
	switch n {
	case 6 + 3*1: // Grayscale image.
		d.nComp = 1
	case 6 + 3*3: // YCbCr or RGB image.
		d.nComp = 3
	case 6 + 3*4: // YCbCrK or CMYK image.
		d.nComp = 4
	default:
		return UnsupportedError("number of components")
	}
	if err := d.readFull(d.tmp[:n]); err != nil {
		return err
	}
	// We only support 8-bit precision.
	if d.tmp[0] != 8 {
		return UnsupportedError("precision")
	}
	d.height = int(d.tmp[1])<<8 + int(d.tmp[2])
	d.width = int(d.tmp[3])<<8 + int(d.tmp[4])
	if int(d.tmp[5]) != d.nComp {
		return FormatError("SOF has wrong length")
	}

	for i := 0; i < d.nComp; i++ {
		d.comp[i].c = d.tmp[6+3*i]
		// Section B.2.2 states that "the value of C_i shall be different from
		// the values of C_1 through C_(i-1)".
		for j := 0; j < i; j++ {
			if d.comp[i].c == d.comp[j].c {
				return FormatError("repeated component identifier")
			}
		}

		d.comp[i].tq = d.tmp[8+3*i]
		if d.comp[i].tq > maxTq {
			return FormatError("bad Tq value")
		}

		hv := d.tmp[7+3*i]
		h, v := int(hv>>4), int(hv&0x0f)
		if h < 1 || 4 < h || v < 1 || 4 < v {
			return FormatError("luma/chroma subsampling ratio")
		}
		if h == 3 || v == 3 {
			return errUnsupportedSubsamplingRatio
		}
		switch d.nComp {
		case 1:
			// If a JPEG image has only one component, section A.2 says "this data
			// is non-interleaved by definition" and section A.2.2 says "[in this
			// case...] the order of data units within a scan shall be left-to-right
			// and top-to-bottom... regardless of the values of H_1 and V_1". Section
			// 4.8.2 also says "[for non-interleaved data], the MCU is defined to be
			// one data unit". Similarly, section A.1.1 explains that it is the ratio
			// of H_i to max_j(H_j) that matters, and similarly for V. For grayscale
			// images, H_1 is the maximum H_j for all components j, so that ratio is
			// always 1. The component's (h, v) is effectively always (1, 1): even if
			// the nominal (h, v) is (2, 1), a 20x5 image is encoded in three 8x8
			// MCUs, not two 16x8 MCUs.
			h, v = 1, 1

		case 3:
			// For YCbCr images, we only support 4:4:4, 4:4:0, 4:2:2, 4:2:0,
			// 4:1:1 or 4:1:0 chroma subsampling ratios. This implies that the
			// (h, v) values for the Y component are either (1, 1), (1, 2),
			// (2, 1), (2, 2), (4, 1) or (4, 2), and the Y component's values
			// must be a multiple of the Cb and Cr component's values. We also
			// assume that the two chroma components have the same subsampling
			// ratio.
			switch i {
			case 0: // Y.
				// We have already verified, above, that h and v are both
				// either 1, 2 or 4, so invalid (h, v) combinations are those
				// with v == 4.
				if v == 4 {
					return errUnsupportedSubsamplingRatio
				}
			case 1: // Cb.
				if d.comp[0].h%h != 0 || d.comp[0].v%v != 0 {
					return errUnsupportedSubsamplingRatio
				}
			case 2: // Cr.
				if d.comp[1].h != h || d.comp[1].v != v {
					return errUnsupportedSubsamplingRatio
				}
			}

		case 4:
			// For 4-component images (either CMYK or YCbCrK), we only support two
			// hv vectors: [0x11 0x11 0x11 0x11] and [0x22 0x11 0x11 0x22].
			// Theoretically, 4-component JPEG images could mix and match hv values
			// but in practice, those two combinations are the only ones in use,
			// and it simplifies the applyBlack code below if we can assume that:
			//	- for CMYK, the C and K channels have full samples, and if the M
			//	  and Y channels subsample, they subsample both horizontally and
			//	  vertically.
			//	- for YCbCrK, the Y and K channels have full samples.
			switch i {
			case 0:
				if hv != 0x11 && hv != 0x22 {
					return errUnsupportedSubsamplingRatio
				}
			case 1, 2:
				if hv != 0x11 {
					return errUnsupportedSubsamplingRatio
				}
			case 3:
				if d.comp[0].h != h || d.comp[0].v != v {
					return errUnsupportedSubsamplingRatio
				}
			}
		}

		d.comp[i].h = h
		d.comp[i].v = v
	}
	return nil
}

// Specified in section B.2.4.1.
func (d *decoder) processDQT(n int) error {
loop:
	for n > 0 {
		n--
		x, err := d.readByte()
		if err != nil {
			return err
		}
		tq := x & 0x0f
		if tq > maxTq {
			return FormatError("bad Tq value")
		}
		switch x >> 4 {
		default:
			return FormatError("bad Pq value")
		case 0:
			if n < blockSize {
				break loop
			}
			n -= blockSize
			if err := d.readFull(d.tmp[:blockSize]); err != nil {
				return err
			}
			for i := range d.quant[tq] {
				d.quant[tq][i] = int32(d.tmp[i])
			}
		case 1:
			if n < 2*blockSize {
				break loop
			}
			n -= 2 * blockSize
			if err := d.readFull(d.tmp[:2*blockSize]); err != nil {
				return err
			}
			for i := range d.quant[tq] {
				d.quant[tq][i] = int32(d.tmp[2*i])<<8 | int32(d.tmp[2*i+1])
			}
		}
	}
	if n != 0 {
		return FormatError("DQT has wrong length")
	}
	return nil
}

// Specified in section B.2.4.4.
func (d *decoder) processDRI(n int) error {
	if n != 2 {
		return FormatError("DRI has wrong length")
	}
	if err := d.readFull(d.tmp[:2]); err != nil {
		return err
	}
	d.ri = int(d.tmp[0])<<8 + int(d.tmp[1])
	return nil
}

func (d *decoder) processApp0Marker(n int) error {
	if n < 5 {
		return d.ignore(n)
	}
	if err := d.readFull(d.tmp[:5]); err != nil {
		return err
	}
	n -= 5

	d.jfif = d.tmp[0] == 'J' && d.tmp[1] == 'F' && d.tmp[2] == 'I' && d.tmp[3] == 'F' && d.tmp[4] == '\x00'

	if n > 0 {
		return d.ignore(n)
	}
	return nil
}

func (d *decoder) processApp14Marker(n int) error {
	if n < 12 {
		return d.ignore(n)
	}
	if err := d.readFull(d.tmp[:12]); err != nil {
		return err
	}
	n -= 12

	if d.tmp[0] == 'A' && d.tmp[1] == 'd' && d.tmp[2] == 'o' && d.tmp[3] == 'b' && d.tmp[4] == 'e' {
		d.adobeTransformValid = true
		d.adobeTransform = d.tmp[11]
	}

	if n > 0 {
		return d.ignore(n)
	}
	return nil
}

// decode reads a JPEG image from r and returns it as an image.Image.
func (d *decoder) decode(r io.Reader, configOnly bool) (image.Image, error) {
	d.r = r

	// Check for the Start Of Image marker.
	if err := d.readFull(d.tmp[:2]); err != nil {
		return nil, err
	}
	if d.tmp[0] != 0xff || d.tmp[1] != soiMarker {
		return nil, FormatError("missing SOI marker")
	}

	// Process the remaining segments until the End Of Image marker.
	for {
		err := d.readFull(d.tmp[:2])
		if err != nil {
			return nil, err
		}
		for d.tmp[0] != 0xff {
			// Strictly speaking, this is a format error. However, libjpeg is
			// liberal in what it accepts. As of version 9, next_marker in
			// jdmarker.c treats this as a warning (JWRN_EXTRANEOUS_DATA) and
			// continues to decode the stream. Even before next_marker sees
			// extraneous data, jpeg_fill_bit_buffer in jdhuff.c reads as many
			// bytes as it can, possibly past the end of a scan's data. It
			// effectively puts back any markers that it overscanned (e.g. an
			// "\xff\xd9" EOI marker), but it does not put back non-marker data,
			// and thus it can silently ignore a small number of extraneous
			// non-marker bytes before next_marker has a chance to see them (and
			// print a warning).
			//
			// We are therefore also liberal in what we accept. Extraneous data
			// is silently ignored.
			//
			// This is similar to, but not exactly the same as, the restart
			// mechanism within a scan (the RST[0-7] markers).
			//
			// Note that extraneous 0xff bytes in e.g. SOS data are escaped as
			// "\xff\x00", and so are detected a little further down below.
			d.tmp[0] = d.tmp[1]
			d.tmp[1], err = d.readByte()
			if err != nil {
				return nil, err
			}
		}
		marker := d.tmp[1]
		if marker == 0 {
			// Treat "\xff\x00" as extraneous data.
			continue
		}
		for marker == 0xff {
			// Section B.1.1.2 says, "Any marker may optionally be preceded by any
			// number of fill bytes, which are bytes assigned code X'FF'".
			marker, err = d.readByte()
			if err != nil {
				return nil, err
			}
		}
		if marker == eoiMarker { // End Of Image.
			break
		}
		if rst0Marker <= marker && marker <= rst7Marker {
			// Figures B.2 and B.16 of the specification suggest that restart markers should
			// only occur between Entropy Coded Segments and not after the final ECS.
			// However, some encoders may generate incorrect JPEGs with a final restart
			// marker. That restart marker will be seen here instead of inside the processSOS
			// method, and is ignored as a harmless error. Restart markers have no extra data,
			// so we check for this before we read the 16-bit length of the segment.
			continue
		}

		// Read the 16-bit length of the segment. The value includes the 2 bytes for the
		// length itself, so we subtract 2 to get the number of remaining bytes.
		if err = d.readFull(d.tmp[:2]); err != nil {
			return nil, err
		}
		n := int(d.tmp[0])<<8 + int(d.tmp[1]) - 2
		if n < 0 {
			return nil, FormatError("short segment length")
		}

		switch marker {
		case sof0Marker, sof1Marker, sof2Marker:
			d.progressive = marker == sof2Marker
			err = d.processSOF(n)
			if configOnly && d.jfif {
				return nil, err
			}
		case dhtMarker:
			if configOnly {
				err = d.ignore(n)
			} else {
				err = d.processDHT(n)
			}
		case dqtMarker:
			if configOnly {
				err = d.ignore(n)
			} else {
				err = d.processDQT(n)
			}
		case sosMarker:
			if configOnly {
				return nil, nil
			}
			err = d.processSOS(n)
		case driMarker:
			if configOnly {
				err = d.ignore(n)
			} else {
				err = d.processDRI(n)
			}
		case app0Marker:
			err = d.processApp0Marker(n)
		case app14Marker:
			err = d.processApp14Marker(n)
		default:
			if app0Marker <= marker && marker <= app15Marker || marker == comMarker {
				err = d.ignore(n)
			} else if marker < 0xc0 { // See Table B.1 "Marker code assignments".
				err = FormatError("unknown marker")
			} else {
				err = UnsupportedError("unknown marker")
			}
		}
		if err != nil {
			return nil, err
		}
	}
	if d.img1 != nil {
		return d.img1, nil
	}
	if d.img3 != nil {
		if d.blackPix != nil {
			return d.applyBlack()
		} else if d.isRGB() {
			return d.convertToRGB()
		}
		return d.img3, nil
	}
	return nil, FormatError("missing SOS marker")
}

// applyBlack combines d.img3 and d.blackPix into a CMYK image. The formula
// used depends on whether the JPEG image is stored as CMYK or YCbCrK,
// indicated by the APP14 (Adobe) metadata.
//
// Adobe CMYK JPEG images are inverted, where 255 means no ink instead of full
// ink, so we apply "v = 255 - v" at various points. Note that a double
// inversion is a no-op, so inversions might be implicit in the code below.
func (d *decoder) applyBlack() (image.Image, error) {
	/*if !d.adobeTransformValid {
		return nil, UnsupportedError("unknown color model: 4-component JPEG doesn't have Adobe APP14 metadata")
	}

	// If the 4-component JPEG image isn't explicitly marked as "Unknown (RGB
	// or CMYK)" as per
	// http://www.sno.phy.queensu.ca/~phil/exiftool/TagNames/JPEG.html#Adobe
	// we assume that it is YCbCrK. This matches libjpeg's jdapimin.c.
	if d.adobeTransform != adobeTransformUnknown {
		// Convert the YCbCr part of the YCbCrK to RGB, invert the RGB to get
		// CMY, and patch in the original K. The RGB to CMY inversion cancels
		// out the 'Adobe inversion' described in the applyBlack doc comment
		// above, so in practice, only the fourth channel (black) is inverted.
		bounds := d.img3.Bounds()
		img := image.NewRGBA(bounds)
		imageutil.DrawYCbCr(img, bounds, d.img3, bounds.Min)
		for iBase, y := 0, bounds.Min.Y; y < bounds.Max.Y; iBase, y = iBase+img.Stride, y+1 {
			for i, x := iBase+3, bounds.Min.X; x < bounds.Max.X; i, x = i+4, x+1 {
				img.Pix[i] = 255 - d.blackPix[(y-bounds.Min.Y)*d.blackStride+(x-bounds.Min.X)]
			}
		}
		return &image.CMYK{
			Pix:    img.Pix,
			Stride: img.Stride,
			Rect:   img.Rect,
		}, nil
	}

	// The first three channels (cyan, magenta, yellow) of the CMYK
	// were decoded into d.img3, but each channel was decoded into a separate
	// []byte slice, and some channels may be subsampled. We interleave the
	// separate channels into an image.CMYK's single []byte slice containing 4
	// contiguous bytes per pixel.
	bounds := d.img3.Bounds()
	img := image.NewCMYK(bounds)

	translations := [4]struct {
		src    []byte
		stride int
	}{
		{d.img3.Y, d.img3.YStride},
		{d.img3.Cb, d.img3.CStride},
		{d.img3.Cr, d.img3.CStride},
		{d.blackPix, d.blackStride},
	}
	for t, translation := range translations {
		subsample := d.comp[t].h != d.comp[0].h || d.comp[t].v != d.comp[0].v
		for iBase, y := 0, bounds.Min.Y; y < bounds.Max.Y; iBase, y = iBase+img.Stride, y+1 {
			sy := y - bounds.Min.Y
			if subsample {
				sy /= 2
			}
			for i, x := iBase+t, bounds.Min.X; x < bounds.Max.X; i, x = i+4, x+1 {
				sx := x - bounds.Min.X
				if subsample {
					sx /= 2
				}
				img.Pix[i] = 255 - translation.src[sy*translation.stride+sx]
			}
		}
	}*/
	return nil, nil
}

func (d *decoder) isRGB() bool {
	if d.jfif {
		return false
	}
	if d.adobeTransformValid && d.adobeTransform == adobeTransformUnknown {
		// http://www.sno.phy.queensu.ca/~phil/exiftool/TagNames/JPEG.html#Adobe
		// says that 0 means Unknown (and in practice RGB) and 1 means YCbCr.
		return true
	}
	return d.comp[0].c == 'R' && d.comp[1].c == 'G' && d.comp[2].c == 'B'
}

func (d *decoder) convertToRGB() (image.Image, error) {
	cScale := d.comp[0].h / d.comp[1].h
	bounds := d.img3.Bounds()
	img := image.NewRGBA(bounds)
	for y := bounds.Min.Y; y < bounds.Max.Y; y++ {
		po := img.PixOffset(bounds.Min.X, y)
		yo := d.img3.YOffset(bounds.Min.X, y)
		co := d.img3.COffset(bounds.Min.X, y)
		for i, iMax := 0, bounds.Max.X-bounds.Min.X; i < iMax; i++ {
			img.Pix[po+4*i+0] = d.img3.Y[yo+i]
			img.Pix[po+4*i+1] = d.img3.Cb[co+i/cScale]
			img.Pix[po+4*i+2] = d.img3.Cr[co+i/cScale]
			img.Pix[po+4*i+3] = 255
		}
	}
	return img, nil
}

// Decode reads a JPEG image from r and returns it as an image.Image.
func Decode(r io.Reader) (image.Image, error) {
	var d decoder
	return d.decode(r, false)
}

// DecodeConfig returns the color model and dimensions of a JPEG image without
// decoding the entire image.
func DecodeConfig(r io.Reader) (image.Config, error) {
	var d decoder
	if _, err := d.decode(r, true); err != nil {
		return image.Config{}, err
	}
	switch d.nComp {
	case 1:
		return image.Config{
			ColorModel: color.GrayModel,
			Width:      d.width,
			Height:     d.height,
		}, nil
	case 3:
		cm := color.YCbCrModel
		if d.isRGB() {
			cm = color.RGBAModel
		}
		return image.Config{
			ColorModel: cm,
			Width:      d.width,
			Height:     d.height,
		}, nil
	case 4:
		return image.Config{
			ColorModel: color.CMYKModel,
			Width:      d.width,
			Height:     d.height,
		}, nil
	}
	return image.Config{}, FormatError("missing SOF marker")
}

func init() {
	image.RegisterFormat("jpeg", "\xff\xd8", Decode, DecodeConfig)
}

const blockSize = 64 // A DCT block is 8x8.

type block [blockSize]int32

const (
	w1 = 2841 // 2048*sqrt(2)*cos(1*pi/16)
	w2 = 2676 // 2048*sqrt(2)*cos(2*pi/16)
	w3 = 2408 // 2048*sqrt(2)*cos(3*pi/16)
	w5 = 1609 // 2048*sqrt(2)*cos(5*pi/16)
	w6 = 1108 // 2048*sqrt(2)*cos(6*pi/16)
	w7 = 565  // 2048*sqrt(2)*cos(7*pi/16)

	w1pw7 = w1 + w7
	w1mw7 = w1 - w7
	w2pw6 = w2 + w6
	w2mw6 = w2 - w6
	w3pw5 = w3 + w5
	w3mw5 = w3 - w5

	r2 = 181 // 256/sqrt(2)
)

// idct performs a 2-D Inverse Discrete Cosine Transformation.
//
// The input coefficients should already have been multiplied by the
// appropriate quantization table. We use fixed-point computation, with the
// number of bits for the fractional component varying over the intermediate
// stages.
//
// For more on the actual algorithm, see Z. Wang, "Fast algorithms for the
// discrete W transform and for the discrete Fourier transform", IEEE Trans. on
// ASSP, Vol. ASSP- 32, pp. 803-816, Aug. 1984.
func idct(src *block) {
	// Horizontal 1-D IDCT.
	for y := 0; y < 8; y++ {
		y8 := y * 8
		// If all the AC components are zero, then the IDCT is trivial.
		if src[y8+1] == 0 && src[y8+2] == 0 && src[y8+3] == 0 &&
			src[y8+4] == 0 && src[y8+5] == 0 && src[y8+6] == 0 && src[y8+7] == 0 {
			dc := src[y8+0] << 3
			src[y8+0] = dc
			src[y8+1] = dc
			src[y8+2] = dc
			src[y8+3] = dc
			src[y8+4] = dc
			src[y8+5] = dc
			src[y8+6] = dc
			src[y8+7] = dc
			continue
		}

		// Prescale.
		x0 := (src[y8+0] << 11) + 128
		x1 := src[y8+4] << 11
		x2 := src[y8+6]
		x3 := src[y8+2]
		x4 := src[y8+1]
		x5 := src[y8+7]
		x6 := src[y8+5]
		x7 := src[y8+3]

		// Stage 1.
		x8 := w7 * (x4 + x5)
		x4 = x8 + w1mw7*x4
		x5 = x8 - w1pw7*x5
		x8 = w3 * (x6 + x7)
		x6 = x8 - w3mw5*x6
		x7 = x8 - w3pw5*x7

		// Stage 2.
		x8 = x0 + x1
		x0 -= x1
		x1 = w6 * (x3 + x2)
		x2 = x1 - w2pw6*x2
		x3 = x1 + w2mw6*x3
		x1 = x4 + x6
		x4 -= x6
		x6 = x5 + x7
		x5 -= x7

		// Stage 3.
		x7 = x8 + x3
		x8 -= x3
		x3 = x0 + x2
		x0 -= x2
		x2 = (r2*(x4+x5) + 128) >> 8
		x4 = (r2*(x4-x5) + 128) >> 8

		// Stage 4.
		src[y8+0] = (x7 + x1) >> 8
		src[y8+1] = (x3 + x2) >> 8
		src[y8+2] = (x0 + x4) >> 8
		src[y8+3] = (x8 + x6) >> 8
		src[y8+4] = (x8 - x6) >> 8
		src[y8+5] = (x0 - x4) >> 8
		src[y8+6] = (x3 - x2) >> 8
		src[y8+7] = (x7 - x1) >> 8
	}

	// Vertical 1-D IDCT.
	for x := 0; x < 8; x++ {
		// Similar to the horizontal 1-D IDCT case, if all the AC components are zero, then the IDCT is trivial.
		// However, after performing the horizontal 1-D IDCT, there are typically non-zero AC components, so
		// we do not bother to check for the all-zero case.

		// Prescale.
		y0 := (src[8*0+x] << 8) + 8192
		y1 := src[8*4+x] << 8
		y2 := src[8*6+x]
		y3 := src[8*2+x]
		y4 := src[8*1+x]
		y5 := src[8*7+x]
		y6 := src[8*5+x]
		y7 := src[8*3+x]

		// Stage 1.
		y8 := w7*(y4+y5) + 4
		y4 = (y8 + w1mw7*y4) >> 3
		y5 = (y8 - w1pw7*y5) >> 3
		y8 = w3*(y6+y7) + 4
		y6 = (y8 - w3mw5*y6) >> 3
		y7 = (y8 - w3pw5*y7) >> 3

		// Stage 2.
		y8 = y0 + y1
		y0 -= y1
		y1 = w6*(y3+y2) + 4
		y2 = (y1 - w2pw6*y2) >> 3
		y3 = (y1 + w2mw6*y3) >> 3
		y1 = y4 + y6
		y4 -= y6
		y6 = y5 + y7
		y5 -= y7

		// Stage 3.
		y7 = y8 + y3
		y8 -= y3
		y3 = y0 + y2
		y0 -= y2
		y2 = (r2*(y4+y5) + 128) >> 8
		y4 = (r2*(y4-y5) + 128) >> 8

		// Stage 4.
		src[8*0+x] = (y7 + y1) >> 14
		src[8*1+x] = (y3 + y2) >> 14
		src[8*2+x] = (y0 + y4) >> 14
		src[8*3+x] = (y8 + y6) >> 14
		src[8*4+x] = (y8 - y6) >> 14
		src[8*5+x] = (y0 - y4) >> 14
		src[8*6+x] = (y3 - y2) >> 14
		src[8*7+x] = (y7 - y1) >> 14
	}
}

// makeImg allocates and initializes the destination image.
func (d *decoder) makeImg(mxx, myy int) {
	if d.nComp == 1 {
		m := image.NewGray(image.Rect(0, 0, 8*mxx, 8*myy))
		d.img1 = m.SubImage(image.Rect(0, 0, d.width, d.height)).(*image.Gray)
		return
	}

	h0 := d.comp[0].h
	v0 := d.comp[0].v
	hRatio := h0 / d.comp[1].h
	vRatio := v0 / d.comp[1].v
	var subsampleRatio image.YCbCrSubsampleRatio
	switch hRatio<<4 | vRatio {
	case 0x11:
		subsampleRatio = image.YCbCrSubsampleRatio444
	case 0x12:
		subsampleRatio = image.YCbCrSubsampleRatio440
	case 0x21:
		subsampleRatio = image.YCbCrSubsampleRatio422
	case 0x22:
		subsampleRatio = image.YCbCrSubsampleRatio420
	case 0x41:
		subsampleRatio = image.YCbCrSubsampleRatio411
	case 0x42:
		subsampleRatio = image.YCbCrSubsampleRatio410
	default:
		panic("unreachable")
	}
	m := image.NewYCbCr(image.Rect(0, 0, 8*h0*mxx, 8*v0*myy), subsampleRatio)
	d.img3 = m.SubImage(image.Rect(0, 0, d.width, d.height)).(*image.YCbCr)

	if d.nComp == 4 {
		h3, v3 := d.comp[3].h, d.comp[3].v
		d.blackPix = make([]byte, 8*h3*mxx*8*v3*myy)
		d.blackStride = 8 * h3 * mxx
	}
}

// Specified in section B.2.3.
func (d *decoder) processSOS(n int) error {
	if d.nComp == 0 {
		return FormatError("missing SOF marker")
	}
	if n < 6 || 4+2*d.nComp < n || n%2 != 0 {
		return FormatError("SOS has wrong length")
	}
	if err := d.readFull(d.tmp[:n]); err != nil {
		return err
	}
	nComp := int(d.tmp[0])
	if n != 4+2*nComp {
		return FormatError("SOS length inconsistent with number of components")
	}
	var scan [maxComponents]struct {
		compIndex uint8
		td        uint8 // DC table selector.
		ta        uint8 // AC table selector.
	}
	totalHV := 0
	for i := 0; i < nComp; i++ {
		cs := d.tmp[1+2*i] // Component selector.
		compIndex := -1
		for j, comp := range d.comp[:d.nComp] {
			if cs == comp.c {
				compIndex = j
			}
		}
		if compIndex < 0 {
			return FormatError("unknown component selector")
		}
		scan[i].compIndex = uint8(compIndex)
		// Section B.2.3 states that "the value of Cs_j shall be different from
		// the values of Cs_1 through Cs_(j-1)". Since we have previously
		// verified that a frame's component identifiers (C_i values in section
		// B.2.2) are unique, it suffices to check that the implicit indexes
		// into d.comp are unique.
		for j := 0; j < i; j++ {
			if scan[i].compIndex == scan[j].compIndex {
				return FormatError("repeated component selector")
			}
		}
		totalHV += d.comp[compIndex].h * d.comp[compIndex].v

		scan[i].td = d.tmp[2+2*i] >> 4
		if scan[i].td > maxTh {
			return FormatError("bad Td value")
		}
		scan[i].ta = d.tmp[2+2*i] & 0x0f
		if scan[i].ta > maxTh {
			return FormatError("bad Ta value")
		}
	}
	// Section B.2.3 states that if there is more than one component then the
	// total H*V values in a scan must be <= 10.
	if d.nComp > 1 && totalHV > 10 {
		return FormatError("total sampling factors too large")
	}

	// zigStart and zigEnd are the spectral selection bounds.
	// ah and al are the successive approximation high and low values.
	// The spec calls these values Ss, Se, Ah and Al.
	//
	// For progressive JPEGs, these are the two more-or-less independent
	// aspects of progression. Spectral selection progression is when not
	// all of a block's 64 DCT coefficients are transmitted in one pass.
	// For example, three passes could transmit coefficient 0 (the DC
	// component), coefficients 1-5, and coefficients 6-63, in zig-zag
	// order. Successive approximation is when not all of the bits of a
	// band of coefficients are transmitted in one pass. For example,
	// three passes could transmit the 6 most significant bits, followed
	// by the second-least significant bit, followed by the least
	// significant bit.
	//
	// For baseline JPEGs, these parameters are hard-coded to 0/63/0/0.
	zigStart, zigEnd, ah, al := int32(0), int32(blockSize-1), uint32(0), uint32(0)
	if d.progressive {
		zigStart = int32(d.tmp[1+2*nComp])
		zigEnd = int32(d.tmp[2+2*nComp])
		ah = uint32(d.tmp[3+2*nComp] >> 4)
		al = uint32(d.tmp[3+2*nComp] & 0x0f)
		if (zigStart == 0 && zigEnd != 0) || zigStart > zigEnd || blockSize <= zigEnd {
			return FormatError("bad spectral selection bounds")
		}
		if zigStart != 0 && nComp != 1 {
			return FormatError("progressive AC coefficients for more than one component")
		}
		if ah != 0 && ah != al+1 {
			return FormatError("bad successive approximation values")
		}
	}

	// mxx and myy are the number of MCUs (Minimum Coded Units) in the image.
	h0, v0 := d.comp[0].h, d.comp[0].v // The h and v values from the Y components.
fmt.Printf("w=%d h=%d h0=%d v0=%d\n", d.width, d.height, h0, v0);
	mxx := (d.width + 8*h0 - 1) / (8 * h0)
	myy := (d.height + 8*v0 - 1) / (8 * v0)
	if d.img1 == nil && d.img3 == nil {
		d.makeImg(mxx, myy)
	}
	if d.progressive {
		for i := 0; i < nComp; i++ {
			compIndex := scan[i].compIndex
			if d.progCoeffs[compIndex] == nil {
				d.progCoeffs[compIndex] = make([]block, mxx*myy*d.comp[compIndex].h*d.comp[compIndex].v)
			}
		}
	}

	d.bits = bits{}
	mcu, expectedRST := 0, uint8(rst0Marker)
	var (
		// b is the decoded coefficients, in natural (not zig-zag) order.
		b  block
		dc [maxComponents]int32
		// bx and by are the location of the current block, in units of 8x8
		// blocks: the third block in the first row has (bx, by) = (2, 0).
		bx, by     int
		blockCount int
	)
fmt.Printf("mxx=%d myy=%d\n", mxx, myy);
	for my := 0; my < myy; my++ {
		for mx := 0; mx < mxx; mx++ {
			for i := 0; i < nComp; i++ {
				compIndex := scan[i].compIndex
				hi := d.comp[compIndex].h
				vi := d.comp[compIndex].v
				qt := &d.quant[d.comp[compIndex].tq]
				for j := 0; j < hi*vi; j++ {

fmt.Printf("my=%d mx=%d i=%d j=%d\n", my, mx, i, j);
					// The blocks are traversed one MCU at a time. For 4:2:0 chroma
					// subsampling, there are four Y 8x8 blocks in every 16x16 MCU.
					//
					// For a baseline 32x16 pixel image, the Y blocks visiting order is:
					//	0 1 4 5
					//	2 3 6 7
					//
					// For progressive images, the interleaved scans (those with nComp > 1)
					// are traversed as above, but non-interleaved scans are traversed left
					// to right, top to bottom:
					//	0 1 2 3
					//	4 5 6 7
					// Only DC scans (zigStart == 0) can be interleaved. AC scans must have
					// only one component.
					//
					// To further complicate matters, for non-interleaved scans, there is no
					// data for any blocks that are inside the image at the MCU level but
					// outside the image at the pixel level. For example, a 24x16 pixel 4:2:0
					// progressive image consists of two 16x16 MCUs. The interleaved scans
					// will process 8 Y blocks:
					//	0 1 4 5
					//	2 3 6 7
					// The non-interleaved scans will process only 6 Y blocks:
					//	0 1 2
					//	3 4 5
					if nComp != 1 {
						bx = hi*mx + j%hi
						by = vi*my + j/hi
					} else {
						q := mxx * hi
						bx = blockCount % q
						by = blockCount / q
						blockCount++
						if bx*8 >= d.width || by*8 >= d.height {
							continue
						}
					}

					// Load the previous partially decoded coefficients, if applicable.
					if d.progressive {
						b = d.progCoeffs[compIndex][by*mxx*hi+bx]
					} else {
						b = block{}
					}

					if ah != 0 {
						if err := d.refine(&b, &d.huff[acTable][scan[i].ta], zigStart, zigEnd, 1<<al); err != nil {
							return err
						}
					} else {
						zig := zigStart
						if zig == 0 {
							zig++
							// Decode the DC coefficient, as specified in section F.2.2.1.
							value, err := d.decodeHuffman(&d.huff[dcTable][scan[i].td])
fmt.Printf("A %02X\n", value);
							if err != nil {
								return err
							}
							if value > 16 {
								return UnsupportedError("excessive DC component")
							}
							dcDelta, err := d.receiveExtend(value)
							if err != nil {
								return err
							}
							dc[compIndex] += dcDelta
							b[0] = dc[compIndex] << al
						}

						if zig <= zigEnd && d.eobRun > 0 {
							d.eobRun--
						} else {
							// Decode the AC coefficients, as specified in section F.2.2.2.
							huff := &d.huff[acTable][scan[i].ta]
							for ; zig <= zigEnd; zig++ {
								value, err := d.decodeHuffman(huff)
fmt.Printf("B %02X\n", value);
								if err != nil {
									return err
								}
								val0 := value >> 4
								val1 := value & 0x0f
								if val1 != 0 {
									zig += int32(val0)
									if zig > zigEnd {
										break
									}
									ac, err := d.receiveExtend(val1)
									if err != nil {
										return err
									}
									b[unzig[zig]] = ac << al
								} else {
									if val0 != 0x0f {
										d.eobRun = uint16(1 << val0)
										if val0 != 0 {
											bits, err := d.decodeBits(int32(val0))
											if err != nil {
												return err
											}
											d.eobRun |= uint16(bits)
										}
										d.eobRun--
										break
									}
									zig += 0x0f
								}
							}
						}
					}

					if d.progressive {
						if zigEnd != blockSize-1 || al != 0 {
							// We haven't completely decoded this 8x8 block. Save the coefficients.
							d.progCoeffs[compIndex][by*mxx*hi+bx] = b
							// At this point, we could execute the rest of the loop body to dequantize and
							// perform the inverse DCT, to save early stages of a progressive image to the
							// *image.YCbCr buffers (the whole point of progressive encoding), but in Go,
							// the jpeg.Decode function does not return until the entire image is decoded,
							// so we "continue" here to avoid wasted computation.
							continue
						}
					}

					// Dequantize, perform the inverse DCT and store the block to the image.
					for zig := 0; zig < blockSize; zig++ {
						b[unzig[zig]] *= qt[zig]
					}
					idct(&b)
					dst, stride := []byte(nil), 0
					if d.nComp == 1 {
						dst, stride = d.img1.Pix[8*(by*d.img1.Stride+bx):], d.img1.Stride
					} else {
						switch compIndex {
						case 0:
							dst, stride = d.img3.Y[8*(by*d.img3.YStride+bx):], d.img3.YStride
						case 1:
							dst, stride = d.img3.Cb[8*(by*d.img3.CStride+bx):], d.img3.CStride
						case 2:
							dst, stride = d.img3.Cr[8*(by*d.img3.CStride+bx):], d.img3.CStride
						case 3:
							dst, stride = d.blackPix[8*(by*d.blackStride+bx):], d.blackStride
						default:
							return UnsupportedError("too many components")
						}
					}
					// Level shift by +128, clip to [0, 255], and write to dst.
					for y := 0; y < 8; y++ {
						y8 := y * 8
						yStride := y * stride
						for x := 0; x < 8; x++ {
							c := b[y8+x]
							if c < -128 {
								c = 0
							} else if c > 127 {
								c = 255
							} else {
								c += 128
							}
							dst[yStride+x] = uint8(c)
						}
					}
				} // for j
			} // for i
			mcu++
			if d.ri > 0 && mcu%d.ri == 0 && mcu < mxx*myy {
				// A more sophisticated decoder could use RST[0-7] markers to resynchronize from corrupt input,
				// but this one assumes well-formed input, and hence the restart marker follows immediately.
				if err := d.readFull(d.tmp[:2]); err != nil {
					return err
				}
				if d.tmp[0] != 0xff || d.tmp[1] != expectedRST {
					return FormatError("bad RST marker")
				}
				expectedRST++
				if expectedRST == rst7Marker+1 {
					expectedRST = rst0Marker
				}
				// Reset the Huffman decoder.
				d.bits = bits{}
				// Reset the DC components, as per section F.2.1.3.1.
				dc = [maxComponents]int32{}
				// Reset the progressive decoder state, as per section G.1.2.2.
				d.eobRun = 0
			}
		} // for mx
	} // for my

	return nil
}

// refine decodes a successive approximation refinement block, as specified in
// section G.1.2.
func (d *decoder) refine(b *block, h *huffman, zigStart, zigEnd, delta int32) error {
	// Refining a DC component is trivial.
	if zigStart == 0 {
		if zigEnd != 0 {
			panic("unreachable")
		}
		bit, err := d.decodeBit()
		if err != nil {
			return err
		}
		if bit {
			b[0] |= delta
		}
		return nil
	}

	// Refining AC components is more complicated; see sections G.1.2.2 and G.1.2.3.
	zig := zigStart
	if d.eobRun == 0 {
	loop:
		for ; zig <= zigEnd; zig++ {
			z := int32(0)
			value, err := d.decodeHuffman(h)
			if err != nil {
				return err
			}
			val0 := value >> 4
			val1 := value & 0x0f

			switch val1 {
			case 0:
				if val0 != 0x0f {
					d.eobRun = uint16(1 << val0)
					if val0 != 0 {
						bits, err := d.decodeBits(int32(val0))
						if err != nil {
							return err
						}
						d.eobRun |= uint16(bits)
					}
					break loop
				}
			case 1:
				z = delta
				bit, err := d.decodeBit()
				if err != nil {
					return err
				}
				if !bit {
					z = -z
				}
			default:
				return FormatError("unexpected Huffman code")
			}

			zig, err = d.refineNonZeroes(b, zig, zigEnd, int32(val0), delta)
			if err != nil {
				return err
			}
			if zig > zigEnd {
				return FormatError("too many coefficients")
			}
			if z != 0 {
				b[unzig[zig]] = z
			}
		}
	}
	if d.eobRun > 0 {
		d.eobRun--
		if _, err := d.refineNonZeroes(b, zig, zigEnd, -1, delta); err != nil {
			return err
		}
	}
	return nil
}

// refineNonZeroes refines non-zero entries of b in zig-zag order. If nz >= 0,
// the first nz zero entries are skipped over.
func (d *decoder) refineNonZeroes(b *block, zig, zigEnd, nz, delta int32) (int32, error) {
	for ; zig <= zigEnd; zig++ {
		u := unzig[zig]
		if b[u] == 0 {
			if nz == 0 {
				break
			}
			nz--
			continue
		}
		bit, err := d.decodeBit()
		if err != nil {
			return 0, err
		}
		if !bit {
			continue
		}
		if b[u] >= 0 {
			b[u] += delta
		} else {
			b[u] -= delta
		}
	}
	return zig, nil
}


// maxCodeLength is the maximum (inclusive) number of bits in a Huffman code.
const maxCodeLength = 16

// maxNCodes is the maximum (inclusive) number of codes in a Huffman tree.
const maxNCodes = 256

// lutSize is the log-2 size of the Huffman decoder's look-up table.
const lutSize = 8

// huffman is a Huffman decoder, specified in section C.
type huffman struct {
	// length is the number of codes in the tree.
	nCodes int32
	// lut is the look-up table for the next lutSize bits in the bit-stream.
	// The high 8 bits of the uint16 are the encoded value. The low 8 bits
	// are 1 plus the code length, or 0 if the value is too large to fit in
	// lutSize bits.
	lut [1 << lutSize]uint16
	// vals are the decoded values, sorted by their encoding.
	vals [maxNCodes]uint8
	// minCodes[i] is the minimum code of length i, or -1 if there are no
	// codes of that length.
	minCodes [maxCodeLength]int32
	// maxCodes[i] is the maximum code of length i, or -1 if there are no
	// codes of that length.
	maxCodes [maxCodeLength]int32
	// valsIndices[i] is the index into vals of minCodes[i].
	valsIndices [maxCodeLength]int32
}

// errShortHuffmanData means that an unexpected EOF occurred while decoding
// Huffman data.
var errShortHuffmanData = FormatError("short Huffman data")

// ensureNBits reads bytes from the byte buffer to ensure that d.bits.n is at
// least n. For best performance (avoiding function calls inside hot loops),
// the caller is the one responsible for first checking that d.bits.n < n.
func (d *decoder) ensureNBits(n int32) error {
	for {
		c, err := d.readByteStuffedByte()
		if err != nil {
			if err == io.EOF {
				return errShortHuffmanData
			}
			return err
		}
		d.bits.a = d.bits.a<<8 | uint32(c)
		d.bits.n += 8
		if d.bits.m == 0 {
			d.bits.m = 1 << 7
		} else {
			d.bits.m <<= 8
		}
		if d.bits.n >= n {
			break
		}
	}
	return nil
}

// receiveExtend is the composition of RECEIVE and EXTEND, specified in section
// F.2.2.1.
func (d *decoder) receiveExtend(t uint8) (int32, error) {
	if d.bits.n < int32(t) {
		if err := d.ensureNBits(int32(t)); err != nil {
			return 0, err
		}
	}
	d.bits.n -= int32(t)
	d.bits.m >>= t
	s := int32(1) << t
	x := int32(d.bits.a>>uint8(d.bits.n)) & (s - 1)
	if x < s>>1 {
		x += ((-1) << t) + 1
	}
	return x, nil
}

// processDHT processes a Define Huffman Table marker, and initializes a huffman
// struct from its contents. Specified in section B.2.4.2.
func (d *decoder) processDHT(n int) error {
	for n > 0 {
		if n < 17 {
			return FormatError("DHT has wrong length")
		}
		if err := d.readFull(d.tmp[:17]); err != nil {
			return err
		}
		tc := d.tmp[0] >> 4
		if tc > maxTc {
			return FormatError("bad Tc value")
		}
		th := d.tmp[0] & 0x0f
		if th > maxTh || !d.progressive && th > 1 {
			return FormatError("bad Th value")
		}
		h := &d.huff[tc][th]

		// Read nCodes and h.vals (and derive h.nCodes).
		// nCodes[i] is the number of codes with code length i.
		// h.nCodes is the total number of codes.
		h.nCodes = 0
		var nCodes [maxCodeLength]int32
		for i := range nCodes {
			nCodes[i] = int32(d.tmp[i+1])
			h.nCodes += nCodes[i]
		}
		if h.nCodes == 0 {
			return FormatError("Huffman table has zero length")
		}
		if h.nCodes > maxNCodes {
			return FormatError("Huffman table has excessive length")
		}
		n -= int(h.nCodes) + 17
		if n < 0 {
			return FormatError("DHT has wrong length")
		}
		if err := d.readFull(h.vals[:h.nCodes]); err != nil {
			return err
		}

		// Derive the look-up table.
		for i := range h.lut {
			h.lut[i] = 0
		}
		var x, code uint32
		for i := uint32(0); i < lutSize; i++ {
			code <<= 1
			for j := int32(0); j < nCodes[i]; j++ {
				// The codeLength is 1+i, so shift code by 8-(1+i) to
				// calculate the high bits for every 8-bit sequence
				// whose codeLength's high bits matches code.
				// The high 8 bits of lutValue are the encoded value.
				// The low 8 bits are 1 plus the codeLength.
				base := uint8(code << (7 - i))
				lutValue := uint16(h.vals[x])<<8 | uint16(2+i)
				for k := uint8(0); k < 1<<(7-i); k++ {
					h.lut[base|k] = lutValue
				}
				code++
				x++
			}
		}

		// Derive minCodes, maxCodes, and valsIndices.
		var c, index int32
		for i, n := range nCodes {
			if n == 0 {
				h.minCodes[i] = -1
				h.maxCodes[i] = -1
				h.valsIndices[i] = -1
			} else {
				h.minCodes[i] = c
				h.maxCodes[i] = c + n - 1
				h.valsIndices[i] = index
				c += n
				index += n
			}
			c <<= 1
		}
	}
	return nil
}

// decodeHuffman returns the next Huffman-coded value from the bit-stream,
// decoded according to h.
func (d *decoder) decodeHuffman(h *huffman) (uint8, error) {
	if h.nCodes == 0 {
		return 0, FormatError("uninitialized Huffman table")
	}

	if d.bits.n < 8 {
		if err := d.ensureNBits(8); err != nil {
			if err != errMissingFF00 && err != errShortHuffmanData {
				return 0, err
			}
			// There are no more bytes of data in this segment, but we may still
			// be able to read the next symbol out of the previously read bits.
			// First, undo the readByte that the ensureNBits call made.
			if d.bytes.nUnreadable != 0 {
				d.unreadByteStuffedByte()
			}
			goto slowPath
		}
	}
	if v := h.lut[(d.bits.a>>uint32(d.bits.n-lutSize))&0xff]; v != 0 {
		fmt.Printf("HD a=%08X idx=%d v=%04X\n", d.bits.a, (d.bits.a>>uint32(d.bits.n-lutSize))&0xff, v);
		n := (v & 0xff) - 1
		d.bits.n -= int32(n)
		d.bits.m >>= n
		return uint8(v >> 8), nil
	}

slowPath:
fmt.Printf("slow\n");
	for i, code := 0, int32(0); i < maxCodeLength; i++ {
		if d.bits.n == 0 {
			if err := d.ensureNBits(1); err != nil {
				return 0, err
			}
		}
		if d.bits.a&d.bits.m != 0 {
			code |= 1
		}
		d.bits.n--
		d.bits.m >>= 1
		if code <= h.maxCodes[i] {
			return h.vals[h.valsIndices[i]+code-h.minCodes[i]], nil
		}
		code <<= 1
	}
	return 0, FormatError("bad Huffman code")
}

func (d *decoder) decodeBit() (bool, error) {
	if d.bits.n == 0 {
		if err := d.ensureNBits(1); err != nil {
			return false, err
		}
	}
	ret := d.bits.a&d.bits.m != 0
	d.bits.n--
	d.bits.m >>= 1
	return ret, nil
}

func (d *decoder) decodeBits(n int32) (uint32, error) {
	if d.bits.n < n {
		if err := d.ensureNBits(n); err != nil {
			return 0, err
		}
	}
	ret := d.bits.a >> uint32(d.bits.n-n)
	ret &= (1 << uint32(n)) - 1
	d.bits.n -= n
	d.bits.m >>= uint32(n)
	return ret, nil
}
