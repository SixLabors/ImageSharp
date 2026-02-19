# Open Bitstream Unit

An OBU unit is a unit of parameters encoded in a bitstream format. In AVIF, it contains a single frame.
This frame is coded using no other frame as reference, it is a so called INTRA frame. AV1 movie encoding also defines INTER frames,
which are predictions of one or more other frames. INTER frames are not used in AVIF and therefore this coded ignores INTER frames.

An OBU section for AVIF consists of the following headers:

## Temporal delimiter

In AV1 movies this is a time point. Although irrelevant for AVIF, most implementtions write one such delimiter at the start of the section.

## Sequence header

Common herader for a list (or sequence) of frames. For AVIF, this is exaclty 1 frame. For AVIF, this header can be reduced in size when its `ReducedStillPictureHerader` parameter is true. 
This setting is recommended, as all the extra parameters are not applicable for AVIF.

## Frame header

Can be 3 different OBU types, which define a single INTRA frame in AVIF files.

## Tile group

Defines the tiling parameters and contains the parameters its tile using a different coding.

# Tiling

In AV1 a frame is made up of 1 or more tiles. The parameters for each tile are entropy encoded using the context aware symbol coding.
These parameters are contained in an OBU tile group header.

## Superblock

A tile consists of one or more superblocks. Superblocks can be either 64x64 or 128x128 pixels in size.
This choice is made per frame, and is specified in the `ObuFrameHeader`.
A superblock contains one or more partitions, to further devide the area.

## Partition

A superblock contains one or more Partitions. The partition Type determines the number of partitions it is further split in. 
Paritions can contain other partitions and blocks.

## Block


## Transform Block

A Transform Block is the smallest area of the image, which has the same transformation parameters. A block contains ore or more ModeInfos.


## ModeInfo

The smallest unit in the frame. It determines the parameters for an area of 4 by 4 pixels.

# References

[AV1 embedded in HEIF](https://aomediacodec.github.io/av1-isobmff)

[AV1 specification](https://aomediacodec.github.io/av1-spec/av1-spec.pdf)

[AVIF specification](https://aomediacodec.github.io/av1-avif)

[AV1/AVIF reference implementation](http://gitlab.com/AOMediaCodec/SVT-AV1)

[AOM's original development implementation](https://github.com/AOMediaCodec/libavif)

[Paper describing the techniques used in AV1](https://arxiv.org/pdf/2008.06091)

# Test images

[Netflix image repository](http://download.opencontent.netflix.com/?prefix=AV1/)

[AVIF sample images](https://github.com/link-u/avif-sample-images)

