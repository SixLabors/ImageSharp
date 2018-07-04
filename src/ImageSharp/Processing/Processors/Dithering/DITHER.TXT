DITHER.TXT

What follows is everything you ever wanted to know (for the time being) about
dithering.  I'm sure it will be out of date as soon as it is released, but it
does serve to collect data from a wide variety of sources into a single
document, and should save you considerable searching time.

Numbers in brackets (like this [0]) are references.  A list of these works
appears at the end of this document.

Because this document describes ideas and algorithms which are constantly
changing, I expect that it may have many editions, additions, and corrections
before it gets to you.  I will list my name below as original author, but I
do not wish to deter others from adding their own thoughts and discoveries.
This is not copyrighted in any way, and was created solely for the purpose of
organizing my own knowledge on the subject, and sharing this with others.
Please distribute it to anyonw who might be interested.

If you add anything to this document, please feel free to include your name
below as a contributor or as a reference.  I would particularly like to see
additions to the "Other books of interest" section.  Please keep the text in
this simple format: no margins, no pagination, no lines longer that 79
characters, and no non-ASCII or non-printing characters other than a CR/LF
pair at the end of each line.  It is intended that this be read on as many
different machines as possible.

Original Author:

Lee Crocker                 I can be reached in the CompuServe Graphics
1380 Jewett Ave             Support Forum (GO PICS) with ID # 73407,2030.
Pittsburg, CA  94565

Contributors:

========================================================================
What is Dithering?

Dithering, also called Halftoning or Color Reduction, is the process of
rendering an image on a display device with fewer colors than are in the
image.  The number of different colors in an image or on a device I will call
its Color Resolution.  The term "resolution" means "fineness" and is used to
describe the level of detail in a digitally sampled signal.  It is used most
often in referring to the Spatial Resolution, which is the basic sampling
rate for a digitized image.

Spatial resolution describes the fineness of the "dots" used in an image.
Color resolution describes the fineness of detail available at each dot.  The
higher the resolution of a digital sample, the better it can reproduce high
frequency detail.  A compact disc, for example, has a temporal (time)
resolution of 44,000 samples per second, and a dynamic (volume) resolution of
16 bits (0..65535).  It can therefore reproduce sounds with a vast dynamic
range (from barely audible to ear-splitting) with great detail, but it has
problems with very high-frequency sounds, like violins and piccolos.

It is often possible to "trade" one kind of resolution for another.  If your
display device has a higher spatial resolution than the image you are trying
to reproduce, it can show a very good image even if its color resolution is
less.  This is what we will call "dithering" and is the subject of this
paper.  The other tradeoff, i.e., trading color resolution for spatial
resolution, is called "anti-aliasing" and is not discussed here.

It is important to emphasize here that dithering is a one-way operation.
Once an image has been dithered, although it may look like a good
reproduction of the original, information is permanently lost.  Many image
processing functions fail on dithered images.  For these reasons, dithering
must be considered only as a way to produce an image on hardware that would
otherwise be incapable of displaying it.  The data representing an image
should always be kept in full detail.


========================================================================
Classes of dithering algorithms

The classes of dithering algorithms we will discuss here are these:

1.  Random
2.  Pattern
3.  Ordered
4.  Error dispersion

Each of these methods is generally better than those listed before it, but
other considerations such as processing time, memory constraints, etc. may
weigh in favor of one of the simpler methods.

For the following discussions I will assume that we are given an image with
256 shades of gray (0=black..255=white) that we are trying to reproduce on a
black and white ouput device.  Most of these methods can be extended in
obvious ways to deal with displays that have more than two levels but fewer
than the image, or to color images.  Where such extension is not obvious, or
where better results can be obtained, I will go into more detail.

To convert any of the first three methods into color, simply apply the
algorithm separately for each primary and mix the resulting values.  This
assumes that you have at least eight output colors: black, red, green, blue,
cyan, magenta, yellow, and white.  Though this will work for error dispersion
as well, there are better methods in this case.


========================================================================
Random dither

This is the bubblesort of dithering algorithms.  It is not really acceptable
as a production method, but it is very simple to describe and implement.  For
each value in the image, simply generate a random number 1..256; if it is
geater than the image value at that point, plot the point white, otherwise
plot it black.  That's it.  This generates a picture with a lot of "white
noise", which looks like TV picture "snow".  Though the image produced is
very inaccurate and noisy, it is free from "artifacts" which are phenomena
produced by digital signal processing.

The most common type of artifact is the Moire pattern (Contributors: please
resist the urge to put an accent on the "e", as no portable character set
exists for this).  If you draw several lines close together radiating from a
single point on a computer display, you will see what appear to be flower-
like patterns.  These patterns are not part of the original idea of lines,
but are an illusion produced by the jaggedness of the display.

Many techniques exist for the reduction of digital artifacts like these, most
of which involve using a little randomness to "perturb" a regular algorithm a
little.  Random dither obviously takes this to extreme.

I should mention, of course, that unless your computer has a hardware-based
random number generator (and most don't) there may be some artifacts from the
random number generation algorithm itself.

While random dither adds a lot of high-frequency noise to a picture, it is
useful in reproducing very low-frequency images where the absence of
artifacts is more important than noise.  For example, a whole screen
containing a gradient of all levels from black to white would actually look
best with a random dither.  In this case, ordered dithering would produce
diagonal patterns, and error dispersion would produce clustering.

For efficiency, you can take the random number generator "out of the loop" by
generating a list of random numbers beforehand for use in the dither.  Make
sure that the list is larger than the number of pixels in the image or you
may get artifacts from the reuse of numbers.  The worst case would be if the
size of your list of random numbers is a multiple or near-multiple of the
horizontal size of the image, in which case unwanted vertical or diagonal
lines will appear.


========================================================================
Pattern dither

This is also a simple concept, but much more effective than random dither.
For each possible value in the image, create a pattern of dots that
approximates that value.  For instance, a 3-by-3 block of dots can have one
of 512 patterns, but for our purposes, there are only 10; the number of black
dots in the pattern determines the darkness of the pattern.

Which 10 patterns do we choose?  Obviously, we need the all-white and all-
black patterns.  We can eliminate those patterns which would create vertical
or horizontal lines if repeated over a large area because many images have
such regions of similar value [1].  It has been shown [1] that patterns for
adjacent colors should be similar to reduce an artifact called "contouring",
or visible edges between regions of adjacent values.  One easy way to assure
this is to make each pattern a superset of the previous.  Here are two good
sets of patterns for a 3-by-3 matrix:

    ---   ---   ---   -X-   -XX   -XX   -XX   -XX   XXX   XXX
    ---   -X-   -XX   -XX   -XX   -XX   XXX   XXX   XXX   XXX
    ---   ---   ---   ---   ---   -X-   -X-   XX-   XX-   XXX
or
    ---   X--   X--   X--   X-X   X-X   X-X   XXX   XXX   XXX
    ---   ---   ---   --X   --X   X-X   X-X   X-X   XXX   XXX
    ---   ---   -X-   -X-   -X-   -X-   XX-   XX-   XX-   XXX

The first set of patterns above are "clustered" in that as new dots are added
to each pattern, they are added next to dots already there.  The second set
is "dispersed" as the dots are spread out more.  This distinction is more
important on larger patterns.  Dispersed-dot patterns produce less grainy
images, but require that the output device render each dot distinctly.  When
this is not the case, as with a printing press which smears the dots a
little, clustered patterns are better.

For each pixel in the image we now print the pattern which is closest to its
value.  This will triple the size of the image in each direction, so this
method can only be used where the display spatial resolution is much greater
than that of the image.

We can exploit the fact that most images have large areas of similar value to
reduce our need for extra spatial resolution.  Instead of plotting a whole
pattern for each pixel, map each pixel in the image to a dot in the pattern
an only plot the corresponding dot for each pixel.

The simplest way to do this is to map the X and Y coordinates of each pixel
into the dot (X mod 3, Y mod 3) in the pattern.  Large areas of constant
value will come out as repetitions of the pattern as before.

To extend this method to color images, we must use patterns of colored dots
to represent shades not directly printable by the hardware.  For example, if
your hardware is capable of printing only red, green, blue, and black (the
minimal case for color dithering), other colors can be represented with
patterns of these four:

    Yellow = R G    Cyan = G B      Magenta = R B       Gray = R G
             G R           B G                B R              B K

(B here represents blue, K is black).  There are a total of 31 such distinct
patterns which can be used; I will leave their enumeration "as an exercise
for the reader" (don't you hate books that do that?).


========================================================================
Ordered dither

Because each of the patterns above is a superset of the previous, we can
express the patterns in compact form as the order of dots added:

        8  3  4          and          1  7  4
        6  1  2                       5  8  3
        7  5  9                       6  2  9

Then we can simply use the value in the array as a threshhold.  If the value
of the pixel (scaled into the 0-9 range) is less than the number in the
corresponding cell of the matrix, plot that pixel black, otherwise, plot it
white.  This process is called ordered dither.  As before, clustered patterns
should be used for devices which blur dots.  In fact, the clustered pattern
ordered dither is the process used by most newspapers, and the term
halftoning refers to this method if not otherwise qualified.

Bayer [2] has shown that for matrices of orders which are powers of two there
is an optimal pattern of dispersed dots which results in the pattern noise
being as high-frequency as possible.  The pattern for a 2x2 and 4x4 matrices
are as follows:

        1  3        1  9  3 11        These patterns (and their rotations
        4  2       13  5 15  7        and reflections) are optimal for a
                    4 12  2 10        dispersed-pattern ordered dither.
                   16  8 14  6

Ulichney [3] shows a recursive technique can be used to generate the larger
patterns.  To fully reproduce our 256-level image, we would need to use the
8x8 pattern.

Bayer's method is in very common use and is easily identified by the cross-
hatch pattern artifacts it produces in the resulting display.  This
artifacting is the major drawback of the technique wich is otherwise very
fast and powerful.  Ordered dithering also performs very badly on images
which have already been dithered to some extent.  As stated earlier,
dithering should be the last stage in producing a physical display from a
digitally stored image.  The dithered image should never be stored itself.


========================================================================
Error dispersion

This technique generates the best results of any method here, and is
naturally the slowest.  In fact, there are many variants of this technique as
well, and the better they get, the slower they are.

Error dispersion is very simple to describe: for each point in the image,
first find the closest color available.  Calculate the difference between the
value in the image and the color you have.  Now divide up these error values
and distribute them over the neighboring pixels which you have not visited
yet.  When you get to these later pixels, just add the errors distributed
from the earlier ones, clip the values to the allowed range if needed, then
continue as above.

If you are dithering a grayscale image for output to a black-and-white
device, the "find closest color" is just a simle threshholding operation.  In
color, it involves matching the input color to the closest available hardware
color, which can be difficult depending on the hardware palette.

There are many ways to distribute the errors and many ways to scan the
image, but I will deal here with only a few.  The two basic ways to scan the
image are with a normal left-to-right, top-to-bottom raster, or with an
alternating left-to-right then right-to-left raster.  The latter method
generally produces fewer artifacts and can be used with all the error
diffusion patterns discussed below.

The different ways of dividing up the error can be expressed as patterns
(called filters, for reasons too boring to go into here).

                 X   7            This is the Floyd and Steinberg [4]
             3   5   1            error diffusion filter.

In this filter, the X represents the pixel you are currently scanning, and
the numbers (called weights, for equally boring reasons) represent the
proportion of the error distributed to the pixel in that position.  Here, the
pixel immediately to the right gets 7/16 of the error (the divisor is 16
because the weights add to 16), the pixel directly below gets 5/16 of the
error, and the diagonally adjacent pixels get 3/16 and 1/16.  When scanning a
line right-to-left, this pattern is reversed.  This pattern was chosen
carefully so that it would produce a checkerboard pattern in areas with
intensity of 1/2 (or 128 in our image).  It is also fairly easy to calculate
when the division by 16 is replaced by shifts.

Another filter in common use, but not recommended:

                 X   3            A simpler filter.
                 3   2

This is often erroneously called the Floyd-Steinberg filter, but it does not
produce as good results.  An alternating raster scan of the image is
necessary with this filter to reduce artifacts.  Additional perturbations of
the formula are frequently necessary also.

Burke [5] suggests the following filter:

                 X   8   4        The Burke filter.
         2   4   8   4   2

Notice that this is just a simplification of the Stucki filter (below) with
the bottom row removed.  The main improvement is that the divisor is now 32,
which makes calculating the errors faster, and the removal of one row
reduces the memory requirements of the method.

This is also fairly easy to calculate and produces better results than Floyd
and Steinberg.  Jarvis, Judice, and Ninke [6] use the following:

                 X   7   5        The Jarvis, et al. pattern.
         3   5   7   5   3
         1   3   5   3   1

The divisor here is 48, which is a little more expensive to calculate, and
the errors are distributed over three lines, requiring extra memory and time
for processing.  Probably the best filter is from Stucki [7]:

                 X   8   4        The Stucki pattern.
         2   4   8   4   2
         1   2   4   2   1

This one takes a division by 42 for each pixel and is therefore slow if math
is done inside the loop.  After the initial 8/42 is calculated, some time can
be saved by producing the remaining fractions by shifts.

The speed advantages of the simpler filters can be eliminated somewhat by
performing the divisions beforehand and using lookup tables instead of per-
forming math inside the loop.  This makes it harder to use various filters
in the same program, but the speed benefits are enormous.

It is critical with all of these algorithms that when error values are added
to neighboring pixels, the values must be truncated to fit within the limits
of hardware, otherwise and area of very intense color may cause streaks into
an adjacent area of less intense color.  This truncation adds noise to the
image anagous to clipping in an audio amplifier, but it is not nearly so
offensive as the streaking.  It is mainly for this reason that the larger
filters work better--they split the errors up more finely and produce less of
this clipping noise.

With all of these filters, it is also important to ensure that the errors
you distribute properly add to the original error value.  This is easiest to
accomplish by subtracting each fraction from the whole error as it is
calculated, and using the final remainder as the last fraction.

Some of these methods (particularly the simpler ones) can be greatly improved
by skewing the weights with a little randomness [3].

Calculating the "nearest available color" is trivial with a monochrome image;
with color images it requires more work.  A table of RGB values of all
available colors must be scanned sequentially for each input pixel to find
the closest.  The "distance" formula most often used is a simple pythagorean
"least squares".  The difference for each color is squared, and the three
squares added to produce the distance value.  This value is equivalent to the
square of the distance between the points in RGB-space.  It is not necessary
to compute the square root of this value because we are not interested in the
actual distance, only in which is smallest.  The square root function is a
monotonic increasing function and does not affect the order of its operands.
If the total number of colors with which you are dealing is small, this part
of the algorithm can be replaced by a lookup table as well.

When your hardware allows you to select the available colors, very good
results can be achieved by selecting colors from the image itself.  You must
reserve at least 8 colors for the primaries, secondaries, black, and white
for best results.  If you do not know the colors in your image ahead of time,
or if you are going to use the same map to dither several different images,
you will have to fill your color map with a good range of colors.  This can
be done either by assigning a certain number of bits to each primary and
computing all combinations, or by a smoother distribution as suggested by
Heckbert [8].


========================================================================
Sample code

Despite my best efforts in expository writing, nothing explains an algorithm
better than real code.  With that in mind, presented here below is an
algorithm (in somewhat incomplete, very inefficient pseudo-C) which
implements error diffusion dithering with the Floyd and Steinberg filter.  It
is not efficiently coded, but its purpose is to show the method, which I
believe it does.

/* Floyd/Steinberg error diffusion dithering algorithm in color.  The array
** line[][] contains the RGB values for the current line being processed;
** line[0][x] = red, line[1][x] = green, line[2][x] = blue.  It uses the
** external functions getline() and putdot(), whose pupose should be easy
** to see from the code.
*/

unsigned char line[3][WIDTH];
unsigned char colormap[3][COLORS] = {
      0,   0,   0,     /* Black       This color map should be replaced   */
    255,   0,   0,     /* Red         by one available on your hardware.  */
      0, 255,   0,     /* Green       It may contain any number of colors */
      0,   0, 255,     /* Blue        as long as the constant COLORS is   */
    255, 255,   0,     /* Yellow      set correctly.                      */
    255,   0, 255,     /* Magenta                                         */
      0, 255, 255,     /* Cyan                                            */
    255, 255, 255 };   /* White                                           */

int getline();               /* Function to read line[] from image file;  */
                             /* must return EOF when done.                */
putdot(int x, int y, int c); /* Plot dot of color c at location x, y.     */

dither()
{
    static int ed[3][WIDTH] = {0};      /* Errors distributed down, i.e., */
                                        /* to the next line.              */
    int x, y, h, c, nc, v,              /* Working variables              */
        e[4],                           /* Error parts (7/8,1/8,5/8,3/8). */
        ef[3];                          /* Error distributed forward.     */
    long dist, sdist;                   /* Used for least-squares match.  */

    for (x=0; x<WIDTH; ++x) {
        ed[0][x] = ed[1][x] = ed[2][x] = 0;
    }
    y = 0;                              /* Get one line at a time from    */
    while (getline() != EOF) {          /* input image.                   */

        ef[0] = ef[1] = ef[2] = 0;      /* No forward error for first dot */

        for (x=0; x<WIDTH; ++x) {
            for (c=0; c<3; ++c) {
                v = line[c][x] + ef[c] + ed[c][x];  /* Add errors from    */
                if (v < 0) v = 0;                   /* previous pixels    */
                if (v > 255) v = 255;               /* and clip.          */
                line[c][x] = v;
            }

            sdist = 255L * 255L * 255L + 1L;        /* Compute the color  */
            for (c=0; c<COLORS; ++c) {              /* in colormap[] that */
                                                    /* is nearest to the  */
#define D(z) (line[z][x]-colormap[c][z])            /* corrected color.   */

                dist = D(0)*D(0) + D(1)*D(1) + D(2)*D(2);
                if (dist < sdist) {
                    nc = c;
                    sdist = dist;
                }
            }
            putdot(x, y, nc);           /* Nearest color found; plot it.  */

            for (c=0; c<3; ++c) {
                v = line[c][x] - colormap[c][nc];   /* V = new error; h = */
                h = v >> 1;                         /* half of v, e[1..4] */
                e[1] = (7 * h) >> 3;                /* will be filled     */
                e[2] = h - e[1];                    /* with the Floyd and */
                h = v - h;                          /* Steinberg weights. */
                e[3] = (5 * h) >> 3;
                e[4] = h = e[3];

                ef[c] = e[1];                       /* Distribute errors. */
                if (x < WIDTH-1) ed[c][x+1] = e[2];
                if (x == 0) ed[c][x] = e[3]; else ed[c][x] += e[3];
                if (x > 0) ed[c][x-1] += e[4];
            }
        } /* next x */

        ++y;
    } /* next y */
}


========================================================================
Bibliography

[1] Foley, J. D. and Andries Van Dam (1982)
    Fundamentals of Interactive Computer Graphics.  Reading, MA: Addisson
    Wesley.

    This is a standard reference for many graphic techniques which has not
    declined with age.  Highly recommended.

[2] Bayer, B. E. (1973)
    "An Optimum Method for Two-Level Rendition of Continuous Tone Pictures,"
    IEEE International Conference on Communications, Conference Records, pp.
    26-11 to 26-15.

    A short article proving the optimality of Bayer's pattern in the
    dispersed-dot ordered dither.

[3] Ulichney, R. (1987)
    Digital Halftoning.  Cambridge, MA: The MIT Press.

    This is the best book I know of for describing the various black and
    white dithering methods.  It has clear explanations (a little higher math
    may come in handy) and wonderful illustrations.  It does not contain any
    code, but don't let that keep you from getting this book.  Computer
    Literacy carries it but is often sold out.

[4] Floyd, R.W. and L. Steinberg (1975)
    "An Adaptive Algorithm for Spatial Gray Scale."  SID International
    Symposium Digest of Technical Papers, vol 1975m, pp. 36-37.

    Short article in which Floyd and Steinberg introduce their filter.

[5] Daniel Burkes is unpublished, but can be reached at this address:

    Daniel Burkes
    TerraVision Inc.
    2351 College Station Road Suite 563
    Athens, GA  30305

    or via CompuServe's Graphics Support Forum, ID # 72077,356.

[6] Jarvis, J. F., C. N. Judice, and W. H. Ninke (1976)
    "A Survey of Techniques for the Display of Continuous Tone Pictures on
    Bi-Level Displays."  Computer Graphics and Image Processing, vol. 5, pp.
    13-40.

[7] Stucki, P. (1981)
    "MECCA - a multiple-error correcting computation algorithm for bilevel
    image hardcopy reproduction."  Research Report RZ1060, IBM Research
    Laboratory, Zurich, Switzerland.

[8] Heckbert, Paul (9182)
    "Color Image Quantization for Frame Buffer Display."  Computer Graphics
    (SIGGRAPH 82), vol. 16, pp. 297-307.


========================================================================
Other works of interest:

Newman, William M., and Robert F. S. Sproull (1979)
Principles of Interactive Computer Graphics.  2nd edition.  New York:
McGraw-Hill.

Rogers, David F. (1985)
Procedural Elements for Computer Graphics.  New York: McGraw-Hill.

Rogers, David F., and J. A. Adams (1976)
Mathematical Elements for Computer Graphics.  New York: McGraw-Hill.


========================================================================
About CompuServe Graphics Support Forum:

CompuServe Information Service is a service of the H&R Block companies
providing computer users with electronic mail, teleconferencing, and many
other telecommunications services.  Call 800-848-8199 for more information.

The Graphics Support Forum is dedicated to helping its users get the most out
of their computers' graphics capabilities.  It has a small staff and a large
number of "Developers" who create images and software on all types of
machines from Apple IIs to Sun workstations.  While on CompuServe, type GO
PICS from any "!" prompt to gain access to the forum.