@echo off

set SourceDir=%1
set DestDir=%2

echo Converting all jpeg-s in %InputDir% to PNG into %DestDir%

for /r "%SourceDir%" %%f in (*.jpeg *.jpg) do magick convert %%f "%DestDir%\%%~nf.png"

