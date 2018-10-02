# Reed-Solomon
Reed Solomon (255, 223) CCSDS in C#.

# Overview
This is an implementation of an RS (255, 223) encoder and decoder.

The encoder is capable of encoding 223 bytes of data to produce 32 bytes of parity.
When the parity is concatenated with the data, it forms a 255 bytes long encoded block.

The decoder is capable of decoding a block to correct up to 16 errors of unknown position, at any position within the 255 byte block.
If the error locations are known, the indexes of the bad bytes can be specified as erasures,
allowing up to 32 bytes to be corrected within the 255 byte block.

The encoder and decoder use the RS (255,223) code with 8-bit symbols as specified by the CCSDS.
Specifically, they use a field generator polynomial of 1 + X + X^2 + X^7 + X^8,
and a code generator with first consecutive root = 112 and a primitive element of 11.

The conventional polynomial form is used by default. If required, the the strict CCSDS dual-basis polynomial form can be
enabled with the `dualBasis` parameter.

A summary of the CCSDS standard [can be found at their website.](https://public.ccsds.org/pubs/130x1g2.pdf) (Page 5-1)
In depth reference [is also available](https://public.ccsds.org/Pubs/131x0b3e1.pdf) (Page 4-1)

# Attribution
Based on code by Phil Karn, KA9Q, 2002, used under the terms of the GNU General Public License (GPL).

# Methods

### Rs8.Encode(Span<byte> block, bool dualBasis = false)
`public static void Encode(Span<byte> block, bool dualBasis = false)`
  
Takes a `Span<byte>` `block` of length `Rs8.BlockLength` (255), of which the first 223 bytes are data, and the last 32 bytes are space for parity. Parity is calculated for data, and then written to the last 32 bytes of the block.

This overload is the equivalent of calling:

`Encode(block.Slice(0, Rs8.DataLength), block.Slice(Rs8.DataLength, Rs8.ParityLength), dualBasis);`

### Rs8.Encode(Span<byte> data, Span<byte> parity, bool dualBasis = false)
`public static void Encode(Span<byte> data, Span<byte> parity, bool dualBasis = false)`

Takes a `Span<byte>` `data` of length `Rs8.DataLength` (223), and writes the parity of it into a `Span<byte>` `parity` of length `Rs8.ParityLength` (32).

Conventional form is used by default, but can be switched to dual-basis form by setting `dualBasis` to `true`.

A common scenario is to create a single contiguous 255 byte block of memory, fill the first 223 bytes with data, and pass the whole thing to the first overload of the encode method:

```
// Create a 255 byte block
Span<byte> block = (new byte[Rs8.BlockLength]).AsSpan();

// or with C# 7.2
// Span<byte> block = stackalloc byte[Rs8.BlockLength];

// Fill the first 223 bytes of block with data
// ...

// Encode the block, filling the last 32 bytes with parity data
Rs8.Encode(block);

// The block is now encoded.
```

### Decode(Span<byte> block, Span<int> erasurePositions, bool dualBasis = false)
`public static int Decode(Span<byte> block, Span<int> erasurePositions, bool dualBasis = false)`

Takes a `Span<byte>` `block` of length `Rs8.BlockLength` (255), and corrects errors within the block, in place.
Another `Span<byte>` `erasurePositions` may be given, to specify erasure indexes. Erasures are the indexes of known bad bytes within the block.
Specifying erasures can increase the number of recoverable errors from 16 to 32. It may be `Span<byte>.Empty` if no erasures are known.

Conventional form is used by default, but can be switched to dual-basis by setting `dualBasis` to `true`.

If correction was possible, the function returns the number of bytes that were corrected.

If correction was not possible (because there were too many errors), the function returns -1.

# Performance
This implementation differs from some others in that it **only** supports the common RS (255, 223) CCSDS encoding.
This allows the use of precalculated lookup tables, as well as more tightly optimised code than the general case.

This library also takes advantage of the new `Span<T>` class introduced in dotnet Core 2.1,
including the now safe `stackalloc` method (C# 7.2/7.3) for allocating temporary arrays.

This allows the `Encode` and `Decode` methods to be zero-allocating, and work entirely on the stack. GC pressure and the associated performance impacts of collection are therefore eliminated.

Benchmarks on an Intel(R) Core(TM) i7-8650U CPU (@ ~3GHz):

* Encode: ~29,000 blocks per second = ~52 Mbps data throughput
* Decode: ~13,000 blocks per second = ~23 Mbps data throughput

# CLI Tool:

A CLI tool is provided for basic command line encoding and decoding.

### Usage:

-v : Enable verbose output. All log output is written to standard error.

-t : Enable text mode for input and output. In this mode, input should be formatted as hexedecimal ASCII (each byte is two characters 0-9,a-f,A-F). All non-alphanumeric characters will be ignored on input. Output is formatted the same, with newlines after every 16 bytes.

-b : Enable binary mode for input and output (default). In this mode, input and output will be read as raw bytes with no conversion performed.

-x : Enable dual-basis representation for parity.

-c : Enable conventional representation for parity (default)

-i <path> : Read from a file specifid by path instead of standard input. '-' may be given as the path to read from standard input anyway.
  
-o <path> : Write to a file specified by path instead of standard output. '-' may be given as the path to write to standard output anyway.
