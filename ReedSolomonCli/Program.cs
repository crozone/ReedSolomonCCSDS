// Author: Ryan Crosby, September 2018

using ReedSolomon;
using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace ReedSolomonCli
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            bool encode = false;
            bool decode = false;
            bool verbose = false;
            bool veryVerbose = false;
            bool printInput = false;
            bool dualBasis = false;
            bool textMode = false;
            bool continuousMode = false;
            int padding = 0;
            int ignorePreambleCount = 0;

            string inputFilePath = null;
            string outputFilePath = null;

            // Parse args
            //
            if (args.Length > 0)
            {
                if (args.Contains("-h") || args.Contains("--help"))
                {
                    PrintUsage();
                    return (int)ReturnCodes.ExOk;
                }

                verbose = args.Any(s => s.StartsWith("-v")) || args.Any(s => s.StartsWith("--verbose"));
                veryVerbose = args.Any(s => s.StartsWith("-vv")) || args.Any(s => s.StartsWith("--veryverbose"));

                if (args.Contains("-t"))
                {
                    // Treat STDIN and STDOUT as hex encoded text streams.
                    textMode = true;
                }
                else if (args.Contains("-b"))
                {
                    // Treat STDIN and STDOUT as binary streams.
                    //
                    textMode = false;
                }

                if (args.Contains("-x"))
                {
                    dualBasis = true;
                }

                if (args.Contains("-c"))
                {
                    continuousMode = true;
                }

                if (args.Contains("-p"))
                {
                    printInput = true;
                }

                // Get padding length
                //
                string paddingString = inputFilePath = args.SkipWhile(s => s != "-i").ElementAtOrDefault(1);
                if (paddingString != null)
                {
                    if (!int.TryParse(paddingString, out padding))
                    {
                        Console.Error.WriteLine("Invalid padding length");
                        return (int)ReturnCodes.ExUsage;
                    }
                }

                // Get preamble ignore length
                //
                string ignorePreambleCountString = args.SkipWhile(s => s != "-n").ElementAtOrDefault(1);

                if (ignorePreambleCountString != null)
                {
                    if (!int.TryParse(ignorePreambleCountString, out ignorePreambleCount))
                    {
                        Console.Error.WriteLine("Invalid ignore preamble count");
                        return (int)ReturnCodes.ExUsage;
                    }
                }

                // Get input file path
                //
                inputFilePath = args.SkipWhile(s => s != "-i").ElementAtOrDefault(1);

                if (inputFilePath == "-")
                {
                    inputFilePath = null;
                }

                // Get output file path
                //
                outputFilePath = args.SkipWhile(s => s != "-o").ElementAtOrDefault(1);

                if (outputFilePath == "-")
                {
                    outputFilePath = null;
                }

                encode = args.Contains("-e");
                decode = args.Contains("-d");

                if (encode && decode)
                {
                    Console.Error.WriteLine("Cannot specify both encode and decode");
                    return (int)ReturnCodes.ExUsage;
                }
                else if (!encode && !decode)
                {
                    Console.Error.WriteLine("Must specify encode or decode");
                    return (int)ReturnCodes.ExUsage;
                }
            }

            using (Stream inputStream = GetInputStream(inputFilePath))
            {
                using (Stream outputStream = GetOutputStream(outputFilePath))
                {
                    do
                    {
                        // Read preamble off the input stream
                        //
                        if (ignorePreambleCount > 0)
                        {
                            if (verbose) Console.Error.WriteLine($"Ignoring {ignorePreambleCount} bytes {(textMode ? "in hex text" : "in byte stream")}");

                            ReadDummyInput(ignorePreambleCount, inputStream, textMode, veryVerbose);
                        }

                        if (encode)
                        {
                            if (verbose) Console.Error.WriteLine($"Waiting for {Rs8.DataLength} bytes {(textMode ? "in hex text" : "in byte stream")}");

                            // Alloc the block on the stack
                            //
                            Span<byte> block = stackalloc byte[Rs8.BlockLength];

                            // Read in data
                            //
                            try
                            {
                                ReadToSpan(block.Slice(0, Rs8.DataLength), inputStream, textMode, veryVerbose);
                            }
                            catch (Exception ex)
                            {
                                Console.Error.WriteLine($"Error reading from {inputFilePath ?? "stdin"}: {ex.Message}");
                                return (int)ReturnCodes.ExIoErr;
                            }

                            if (printInput)
                            {
                                if (verbose) Console.Error.WriteLine("Input parsed:");

                                WriteFromSpan(block, outputStream, textMode);
                            }

                            if (verbose) Console.Error.WriteLine("Encoding ...");

                            // Encode the block
                            //
                            Rs8.Encode(block, dualBasis);

                            if (verbose) Console.Error.WriteLine("Encoded.");

                            // Write output
                            //
                            try
                            {
                                WriteFromSpan(block, outputStream, textMode);
                            }
                            catch (Exception ex)
                            {
                                Console.Error.WriteLine($"Error writing to {outputFilePath ?? "stdout"}: {ex.Message}");
                                return (int)ReturnCodes.ExCantCreate;
                            }
                        }
                        else if (decode)
                        {
                            if (verbose) Console.Error.WriteLine($"Waiting for {Rs8.BlockLength} bytes {(textMode ? "in hex text" : "in byte stream")}");

                            // Alloc the block on the stack
                            //
                            Span<byte> block = stackalloc byte[Rs8.BlockLength];

                            // Read in data
                            //
                            try
                            {
                                ReadToSpan(block, inputStream, textMode, veryVerbose);

                            }
                            catch (Exception ex)
                            {
                                Console.Error.WriteLine($"Error reading from {outputFilePath ?? "stdin"}: {ex.Message}");
                                return (int)ReturnCodes.ExIoErr;
                            }

                            if (printInput)
                            {
                                if (verbose) Console.Error.WriteLine("Input parsed:");

                                WriteFromSpan(block, outputStream, textMode);
                            }

                            if (verbose) Console.Error.WriteLine("Decoding ...");

                            // Decode the block, correcting errors.
                            //
                            int bytesCorrected = Rs8.Decode(block, null, dualBasis);

                            // Check if the block was successfully recovered
                            //
                            if (bytesCorrected >= 0)
                            {
                                Console.Error.WriteLine($"Decoded. {bytesCorrected} errors corrected.");
                            }
                            else
                            {
                                Console.Error.WriteLine($"Unrecoverable, errors >{Rs8.ParityLength / 2}.");
                            }

                            // Write output
                            //
                            try
                            {
                                WriteFromSpan(block, outputStream, textMode);
                            }
                            catch (Exception ex)
                            {
                                Console.Error.WriteLine($"Error writing to {outputFilePath ?? "stdout"}: {ex.Message}");
                                return (int)ReturnCodes.ExCantCreate;
                            }                            
                        }
                        else
                        {
                            throw new InvalidOperationException();
                        }
                    }
                    while (continuousMode);

                    return (int)ReturnCodes.ExOk;
                }
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: TODO");
        }

        private static Stream GetInputStream(string filePath)
        {
            // Select input stream
            //
            Stream inputStream;
            if (filePath == null)
            {
                inputStream = Console.OpenStandardInput();
            }
            else
            {
                inputStream = File.OpenRead(filePath);
            }

            return inputStream;
        }

        private static Stream GetOutputStream(string filePath)
        {
            // Select output stream
            //
            Stream outputStream;
            if (filePath == null)
            {
                outputStream = Console.OpenStandardOutput();
            }
            else
            {
                outputStream = File.OpenWrite(filePath);
            }

            return outputStream;
        }

        private static void WriteFromSpan(Span<byte> span, Stream stream, bool textMode)
        {
            if (textMode)
            {
                StreamWriter streamWriter = new StreamWriter(stream);

                for (int i = 0; i < span.Length; i++)
                {
                    if (i > 0 && i % 16 == 0)
                    {
                        streamWriter.WriteLine();
                    }

                    streamWriter.Write("{0:X2}", span[i]);
                }
                streamWriter.WriteLine();
                streamWriter.Flush();
            }
            else
            {
                stream.Write(span);
                stream.Flush();
            }
        }

        private static void ReadToSpan(Span<byte> span, Stream stream, bool textMode, bool verbose)
        {
            if (textMode)
            {
                StreamReader streamReader = new StreamReader(stream);

                int byteIndex = 0;

                int charIndex = 0;
                Span<char> charBuffer = stackalloc char[2];

                while (byteIndex < span.Length)
                {
                    int charCode = streamReader.Read();
                    if (charCode < 0)
                    {
                        break;
                    }
                    else
                    {
                        char character = (char)charCode;

                        if (char.IsLetterOrDigit(character))
                        {
                            if (charIndex < charBuffer.Length)
                            {
                                charBuffer[charIndex] = character;
                                charIndex++;
                            }

                            if (charIndex == charBuffer.Length)
                            {
                                if (byte.TryParse(charBuffer, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte parsedByte))
                                {
                                    if (verbose)
                                    {
                                        Console.Error.WriteLine($"Decoded {byteIndex + 1}/{span.Length}:{parsedByte}");
                                    }

                                    span[byteIndex] = parsedByte;
                                    byteIndex++;
                                }
                                else
                                {
                                    Console.Error.WriteLine($"Unrecognised sequence \"{new string(charBuffer)}\"");
                                }
                                charIndex = 0;
                            }
                        }
                        else if (character == '\x04')
                        {
                            // Ctrl-D
                            break;
                        }
                    }
                }
            }
            else
            {
                int bytesRead = 0;
                while (bytesRead < span.Length)
                {
                    bytesRead += stream.Read(span.Slice(bytesRead));
                }
            }
        }

        // Consumes a set number of bytes from the input stream.
        // Returns false if the stream ended before byte count was reached (via ctrl-d or eof)
        //
        private static bool ReadDummyInput(int byteCount, Stream stream, bool textMode, bool verbose)
        {
            if (textMode)
            {
                StreamReader streamReader = new StreamReader(stream);

                int byteIndex = 0;

                int charIndex = 0;
                Span<char> charBuffer = stackalloc char[2];

                while (byteIndex < byteCount)
                {
                    int charCode = streamReader.Read();
                    if (charCode < 0)
                    {
                        return false;
                    }
                    else
                    {
                        char character = (char)charCode;

                        if (char.IsLetterOrDigit(character))
                        {
                            if (charIndex < charBuffer.Length)
                            {
                                charBuffer[charIndex] = character;
                                charIndex++;
                            }

                            if (charIndex == charBuffer.Length)
                            {
                                if (byte.TryParse(charBuffer, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte parsedByte))
                                {
                                    if (verbose)
                                    {
                                        Console.Error.WriteLine($"Skipping byte {byteIndex + 1}/{byteCount}: {parsedByte}");
                                    }

                                    byteIndex++;
                                }
                                else
                                {
                                    Console.Error.WriteLine($"Unrecognised sequence \"{new string(charBuffer)}\"");
                                }
                                charIndex = 0;
                            }
                        }
                        else if (character == '\x04')
                        {
                            // Ctrl-D
                            return false;
                        }
                    }
                }
            }
            else
            {
                int bytesRead = 0;
                while (bytesRead < byteCount)
                {
                    Span<byte> dummyBuffer = stackalloc byte[1];
                    bytesRead += stream.Read(dummyBuffer);
                }
            }

            return true;
        }
    }
}
