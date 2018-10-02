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
            bool verbose = false;
            bool dualBasis = false;
            bool textMode = false;

            string inputFilePath = null;
            string outputFilePath = null;

            // Parse args
            //
            if (args.Length > 0)
            {
                verbose = args.Any(s => s.StartsWith("-v"));

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
                else if (args.Contains("-c"))
                {
                    dualBasis = false;
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
            }

            if (args.Contains("-e"))
            {
                if (verbose) Console.Error.WriteLine($"Waiting for {Rs8.DataLength} bytes {(textMode ? "in hex text" : "in byte stream")}");

                // Alloc the block on the stack
                //
                Span<byte> block = stackalloc byte[Rs8.BlockLength];

                // Read in data
                //
                try
                {
                    using (Stream inputStream = GetInputStream(inputFilePath))
                    {
                        ReadToSpan(block.Slice(0, Rs8.DataLength), inputStream, textMode);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error reading from {inputFilePath ?? "stdin"}: {ex.Message}");
                    return (int)ReturnCodes.ExIoErr;
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
                    using (Stream outputStream = GetOutputStream(outputFilePath))
                    {
                        WriteFromSpan(block, outputStream, textMode);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error writing to {outputFilePath ?? "stdout"}: {ex.Message}");
                    return (int)ReturnCodes.ExCantCreate;
                }

                return (int)ReturnCodes.ExOk;
            }
            else if (args.Contains("-d"))
            {
                if (verbose) Console.Error.WriteLine($"Waiting for {Rs8.BlockLength} bytes {(textMode ? "in hex text" : "in byte stream")}");

                // Alloc the block on the stack
                //
                Span<byte> block = stackalloc byte[Rs8.BlockLength];

                // Read in data
                //
                try
                {
                    using (Stream inputStream = GetInputStream(inputFilePath))
                    {
                        ReadToSpan(block, inputStream, textMode);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error reading from {outputFilePath ?? "stdin"}: {ex.Message}");
                    return (int)ReturnCodes.ExIoErr;
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
                    using (Stream outputStream = GetOutputStream(outputFilePath))
                    {
                        WriteFromSpan(block, outputStream, textMode);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error writing to {outputFilePath ?? "stdout"}: {ex.Message}");
                    return (int)ReturnCodes.ExCantCreate;
                }

                return (int)ReturnCodes.ExOk;
            }
            else
            {
                Console.Error.WriteLine("Must specify encode or decode");
                return (int)ReturnCodes.ExUsage;
            }
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

        private static void ReadToSpan(Span<byte> span, Stream stream, bool textMode)
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
                            else
                            {
                                if (byte.TryParse(charBuffer, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte parsedByte))
                                {
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
    }
}
