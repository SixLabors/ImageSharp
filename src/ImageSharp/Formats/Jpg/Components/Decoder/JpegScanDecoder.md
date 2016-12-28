## JpegScanDecoder
Encapsulates the impementation of the Jpeg top-to bottom scan decoder triggered by the `SOS` marker. 
The implementation is optimized to hold most of the necessary data in a single value type, which is intended to be used as an on-stack object.

#### Benefits:
- Maximized locality of reference by keeping most of the operation data on the stack
- Reaching this without long parameter lists, most of the values describing the state of the decoder algorithm 
are members of the `JpegScanDecoder` struct
- Most of the logic related to Scan decoding is refactored & simplified now to live in the methods of `JpegScanDecoder`
- The first step is done towards separating the stream reading from block processing. They can be refactored later to be executed in two disctinct loops.
  - The input processing loop can be `async`
  - The block processing loop can be parallelized

#### Data layout

|JpegScanDecoder    |
|-------------------|
|Variables          |
|ComputationData    |
|DataPointers       |

- **ComputationData** holds the "large" data blocks needed for computations (Mostly `Block8x8F`-s)
- **DataPointers** contains pointers to the memory regions of `ComponentData` so they can be easily passed around to pointer based utility methods of `Block8x8F`


