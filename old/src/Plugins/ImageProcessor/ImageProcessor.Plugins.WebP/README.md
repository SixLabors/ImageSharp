#Build instructions for libwebp.dll 

Download libwebp source via git or through http download.

In Start Menu, run Visual Studio Tools > Command Prompt.

Change to the libwebp directory and run 

    nmake /f Makefile.vc CFG=release-dynamic RTLIBCFG=dynamic OBJDIR=output

Repeat with the
Visual Studio x64 Cross Tools Command Prompt


Copy to x86 and x64 directories from /output/

To verify p/invokes have not changed:

Review the following history logs for changes since the last release:

http://git.chromium.org/gitweb/?p=webm/libwebp.git;a=history;f=src/webp/types.h;hb=HEAD
http://git.chromium.org/gitweb/?p=webm/libwebp.git;a=history;f=src/webp/encode.h;hb=HEAD
http://git.chromium.org/gitweb/?p=webm/libwebp.git;a=history;f=src/webp/decode.h;hb=HEAD