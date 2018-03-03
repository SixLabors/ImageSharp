#!/bin/bash

# Build in release mode
dotnet build -c Release -f netcoreapp2.0

# Run benchmarks
dotnet bin/Release/netcoreapp2.0/ImageSharp.Benchmarks.dll