using ReedSolomon;
using System;
using System.Linq;

namespace ReedSolomonCli
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            if (args.Contains("-e"))
            {
                Console.WriteLine($"Enter {Rs8.DataLength} bytes in hex format (NN or 0xNN), separated by whitespace.");
                byte[] input = ReadArray(Rs8.DataLength);
                Span<byte> block = stackalloc byte[Rs8.BlockLength];
                input.CopyTo(block);
                Rs8.Encode(block.Slice(0, Rs8.DataLength), block.Slice(Rs8.DataLength, Rs8.ParityLength));
                Console.WriteLine("Encoded:");
                Console.WriteLine();
                WriteSpan(block);
            }
            else if (args.Contains("-d"))
            {
                Console.WriteLine($"Enter {Rs8.BlockLength} bytes in hex format (NN or 0xNN), separated by whitespace.");
                Span<byte> block = ReadArray(Rs8.BlockLength);
                int bytesCorrected = Rs8.Decode(block, Span<int>.Empty);
                if (bytesCorrected > 0)
                {
                    Console.WriteLine($"Decoded ({bytesCorrected} bytes corrected):");
                }
                else
                {
                    Console.WriteLine("Decoded (unrecoverable):");
                }
                Console.WriteLine();
                WriteSpan(block);
            }
            else
            {
                Console.WriteLine("Arg is required. Use -e for encode or -d for decode.");
            }
            while (true)
            {
                Console.ReadLine();
            }
        }

        private static void WriteSpan(Span<byte> span)
        {
            for (int i = 0; i < span.Length; i++)
            {
                if (i % 16 == 0)
                {
                    Console.WriteLine();
                }

                Console.Write("0x{0:X2}", span[i]);

                if (i < span.Length - 1)
                {
                    Console.Write(", ");
                }
            }
            Console.WriteLine();
        }

        private static byte[] ReadArray(int byteCount)
        {
            int byteIndex = 0;
            byte[] bytesRead = new byte[byteCount];

            int charIndex = 0;
            Span<char> charBuffer = stackalloc char[4];

            while (byteIndex < byteCount) {
                int charCode = Console.Read();
                if (charCode < 0)
                {
                    break;
                }
                else
                {
                    char character = (char)charCode;

                    if (char.IsDigit(character) || character == 'x' || character == 'X')
                    {
                        if (charIndex < charBuffer.Length)
                        {
                            charBuffer[charIndex] = character;
                            charIndex++;
                        }
                        else
                        {
                            Console.WriteLine($"Sequence too long");
                            charIndex = 0;
                        }
                    }
                    else if (char.IsWhiteSpace(character))
                    {
                        if (charIndex > 0)
                        {
                            string input = new string(charBuffer.Slice(0, charIndex));
                            charIndex = 0;
                            try
                            {
                                int parsedByte = Convert.ToInt32(input, 16);
                                bytesRead[byteIndex] = (byte)parsedByte;
                                byteIndex++;
                            }
                            catch
                            {
                                Console.WriteLine($"Unrecognised sequence {input}");
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
