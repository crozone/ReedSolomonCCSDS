using ReedSolomon;
using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace ReedSolomonCli
{
    public static class Program
    {
        public static StreamWriter StandardOutWriter = new StreamWriter(Console.OpenStandardOutput());
        public static StreamWriter StandardErrorWriter = new StreamWriter(Console.OpenStandardError());
        public static StreamReader StandardInputReader = new StreamReader(Console.OpenStandardInput());

        public static void Main(string[] args)
        {
            StandardErrorWriter.AutoFlush = true;
            StandardOutWriter.AutoFlush = true;

            bool dualBasis = false;

            if (args.Contains("-b"))
            {
                dualBasis = true;
            }

            if (args.Contains("-e"))
            {
                StandardErrorWriter.WriteLine($"Enter {Rs8.DataLength} bytes in integer (000) or hex (0x00) format, separated by whitespace or ','");
                byte[] input = ReadArray(Rs8.DataLength);
                Span<byte> block = stackalloc byte[Rs8.BlockLength];
                input.CopyTo(block);
                Rs8.Encode(block.Slice(0, Rs8.DataLength), block.Slice(Rs8.DataLength, Rs8.ParityLength), dualBasis);
                StandardErrorWriter.WriteLine("Encoded:");
                StandardErrorWriter.WriteLine();
                WriteSpan(block);
            }
            else if (args.Contains("-d"))
            {
                StandardErrorWriter.WriteLine($"Enter {Rs8.BlockLength} bytes in integer (000) or hex (0x00) format, separated by whitespace or ','");
                Span<byte> block = ReadArray(Rs8.BlockLength);
                int bytesCorrected = Rs8.Decode(block, null, dualBasis);
                if (bytesCorrected > 0)
                {
                    StandardErrorWriter.WriteLine($"Decoded ({bytesCorrected} bytes corrected):");
                }
                else
                {
                    StandardErrorWriter.WriteLine("Decoded (unrecoverable):");
                }
                StandardErrorWriter.WriteLine();
                WriteSpan(block);
            }
            else
            {
                StandardErrorWriter.WriteLine("Arg is required. Use -e for encode or -d for decode.");
            }
        }

        private static void WriteSpan(Span<byte> span)
        {
            for (int i = 0; i < span.Length; i++)
            {
                if (i % 16 == 0)
                {
                    StandardOutWriter.WriteLine();
                }

                StandardOutWriter.Write("0x{0:X2}", span[i]);

                if (i < span.Length - 1)
                {
                    StandardOutWriter.Write(", ");
                }
            }
            StandardOutWriter.WriteLine();
        }

        private static byte[] ReadArray(int byteCount)
        {
            int byteIndex = 0;
            byte[] bytesRead = new byte[byteCount];

            int charIndex = 0;
            Span<char> charBuffer = stackalloc char[4];

            while (byteIndex < byteCount) {
                int charCode = StandardInputReader.Read();
                if (charCode < 0)
                {
                    break;
                }
                else
                {
                    char character = (char)charCode;

                    if (char.IsLetterOrDigit(character) || character == '-')
                    {
                        if (charIndex < charBuffer.Length)
                        {
                            charBuffer[charIndex] = character;
                        }

                        charIndex++;
                    }
                    else if (char.IsWhiteSpace(character) || character == ',')
                    {
                        if (charIndex > charBuffer.Length)
                        {
                            StandardErrorWriter.WriteLine($"Sequence too long ({charIndex} > {charBuffer.Length})");
                            charIndex = 0;
                        }
                        else if (charIndex > 0)
                        {
                            Span<char> symbol = charBuffer.Slice(0, charIndex);

                            bool parsed;
                            if (symbol.Length > 2 && symbol[0] == '0'
                                && (symbol[1] == 'x' || symbol[1] == 'X')
                                && int.TryParse(symbol.Slice(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int parsedByte))
                            {
                                parsed = true;
                            }
                            else if (int.TryParse(symbol, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedByte))
                            {
                                parsed = true;
                            }
                            else
                            {
                                parsedByte = -1;
                                parsed = false;
                                StandardErrorWriter.WriteLine($"Unrecognised sequence \"{new string(symbol)}\"");
                            }

                            charIndex = 0;

                            if (parsed)
                            {
                                if (parsedByte >= byte.MinValue && parsedByte <= byte.MaxValue)
                                {
                                    bytesRead[byteIndex] = (byte)parsedByte;
                                    byteIndex++;
                                }
                                else
                                {
                                    StandardErrorWriter.WriteLine($"Input {parsedByte} not {byte.MinValue} -> { byte.MaxValue}");
                                }
                            }
                        }
                    }
                    else if (character == '\x04')
                    {
                        // Ctrl-D
                        break;
                    }
                }
            }

            return bytesRead;
        }
    }
}
