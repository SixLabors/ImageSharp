### ReferenceOutput
Contains images to validate against in ImageSharp tests. In most cases the file hierarchy follows the hierarchy of `ActualOutput`

### tools
Various utilities to help dealing with images.
- `optipng.exe`: [lossless PNG compressor](http://optipng.sourceforge.net/), to keep the `ReferenceImages` folder as small as possible
- `optimize-all.cmd`: Runs lossless optimizer for reference PNG-s. Currently it has to be manually edited to add new test class directories.
- [`dump-jpeg-coeffs.exe`](https://github.com/SixLabors/Imagesharp/blob/main/tests/Images/External/tools/jpeg/README.md)
