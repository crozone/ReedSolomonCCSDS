using System;
using static ReedSolomon.Precomputed;

namespace ReedSolomon
{
    public static class Rs8
    {
        public const int BlockLength = Nn;
        public const int DataLength = Nn - NRoots;
        public const int ParityLength = NRoots;

        /// <summary>
        /// Calculates parity for data using Reed Solomon RS(255, 223)
        /// </summary>
        public static void Encode(Span<byte> data, Span<byte> parity, bool dualBasis = false)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (parity == null) throw new ArgumentNullException(nameof(parity));
            if (data.Length < DataLength) throw new ArgumentException($"{nameof(data)} must have at least length {DataLength}");
            if (parity.Length < ParityLength) throw new ArgumentException($"{nameof(parity)} must have at least length {ParityLength}");

            if (dualBasis)
            {
                // Convert data from dual basis to conventional
                //
                for (int i = 0; i < (Nn - NRoots); i++)
                {
                    data[i] = TalToConventional[data[i]];
                }
            }

            // Zero parity
            //
            parity.Fill(0);

            for (int i = 0; i < (Nn - NRoots); i++)
            {
                byte feedback = IndexOf[data[i] ^ parity[0]];

                if (feedback != A0)
                {
                    for (int j = 1; j < NRoots; j++)
                    {
                        parity[j] ^= AlphaTo[(feedback + GenPoly[NRoots - j]) % Nn];
                    }
                }

                //memmove(&parity[0], &parity[1], sizeof(unsigned char) * (NROOTS - 1));
                for (int j = 0; j < NRoots - 1; j++)
                {
                    parity[j] = parity[j + 1];
                }

                if (feedback != A0)
                {
                    parity[NRoots - 1] = AlphaTo[(feedback + GenPoly[0]) % Nn];
                }
                else
                {
                    parity[NRoots - 1] = 0;
                }
            }

            if (dualBasis)
            {
                // Convert data back from conventional to dual basis
                //
                for (int i = 0; i < (Nn - NRoots); i++)
                {
                    data[i] = TalToDualBasis[data[i]];
                }

                // Convert parity from conventional to dual basis
                //
                for (int i = 0; i < NRoots; i++)
                {
                    parity[i] = TalToDualBasis[parity[i]];
                }
            }
        }

        /// <summary>
        /// Decodes an RS(255, 223) encoded block
        /// </summary>
        /// <param name="block">RS(255, 223) encoded block. The first 223 bytes must be data, the last 32 bytes must be parity.</param>
        /// <param name="erasurePositions">The positions of any known erasures. May be empty.</param>
        /// <returns></returns>
        public static int Decode(Span<byte> block, Span<int> erasurePositions, bool dualBasis = false)
        {
            if (block == null) throw new ArgumentNullException(nameof(block));
            if (block.Length < BlockLength) throw new ArgumentException($"{nameof(block)} must have at least length {DataLength}");

            int erasureCount = erasurePositions.Length;

            if (dualBasis)
            {
                // Convert block from dual basis to conventional
                //
                for (int i = 0; i < Nn; i++)
                {
                    block[i] = TalToConventional[block[i]];
                }
            }

            Span<byte> s = stackalloc byte[NRoots];

            // Form the syndromes; i.e., evaluate data(x) at roots of g(x)
            for (int i = 0; i < NRoots; i++)
            {
                s[i] = block[0];
            }

            for (int j = 1; j < Nn; j++)
            {
                for (int i = 0; i < NRoots; i++)
                {
                    if (s[i] == 0)
                    {
                        s[i] = block[j];
                    }
                    else
                    {
                        s[i] = (byte)(block[j] ^ AlphaTo[(IndexOf[s[i]] + (Fcr + i) * Prim) % Nn]);
                    }
                }
            }

            // Convert syndromes to index form, checking for nonzero condition
            int synError = 0;
            for (int i = 0; i < NRoots; i++)
            {
                synError |= s[i];
                s[i] = IndexOf[s[i]];
            }

            int count = 0;

            if (synError == 0)
            {
                // If syndrome is zero, block[] is a codeword and there are no
                // errors to correct. So return block[] unmodified.
                //
                count = 0;
            }
            else
            {
                // Use C# 7.2 Span stackalloc feature to avoid allocating.
                // Memory is not guaranteed to be zeroed.
                // We do not need to zero these spans, since each index is
                // never read before it is overwritten.
                Span<byte> b = stackalloc byte[NRoots + 1];
                Span<byte> t = stackalloc byte[NRoots + 1];
                Span<byte> omega = stackalloc byte[NRoots + 1];
                Span<byte> root = stackalloc byte[NRoots];
                Span<byte> reg = stackalloc byte[NRoots + 1];
                Span<byte> loc = stackalloc byte[NRoots];

                // lambda: Err + Eras Locator poly and syndrome poly
                Span<byte> lambda = stackalloc byte[NRoots + 1];
                // Init lambda to 0. This is required because lambda gets XOR'd with
                // itself (and othe stuff), and the initial state must be 0 for this to work.
                lambda.Fill(0);
                lambda[0] = 1;

                if (erasureCount > 0)
                {
                    // Init lambda to be the erasure locator polynomial
                    lambda[1] = AlphaTo[(Prim * (Nn - 1 - erasurePositions[0])) % Nn];
                    for (int i = 1; i < erasureCount; i++)
                    {
                        byte u = (byte)((Prim * (Nn - 1 - erasurePositions[i])) % Nn);
                        for (int j = i + 1; j > 0; j--)
                        {
                            byte tmp = IndexOf[lambda[j - 1]];
                            if (tmp != A0)
                            {
                                lambda[j] ^= AlphaTo[(u + tmp) % Nn];
                            }
                        }
                    }
                }

                for (int i = 0; i < NRoots + 1; i++)
                {
                    b[i] = IndexOf[lambda[i]];
                }

                //
                // Begin Berlekamp-Massey algorithm to determine error+erasure
                // locator polynomial
                //

                // r is the step number
                int r = erasureCount;
                int el = erasureCount;

                while (++r <= NRoots)
                {
                    // Compute discrepancy at the r-th step in poly-form
                    byte discrRth = 0;
                    for (int i = 0; i < r; i++)
                    {
                        if ((lambda[i] != 0) && (s[r - i - 1] != A0))
                        {
                            discrRth ^= AlphaTo[(IndexOf[lambda[i]] + s[r - i - 1]) % Nn];
                        }
                    }

                    // Convert to index form
                    discrRth = IndexOf[discrRth];

                    if (discrRth == A0)
                    {
                        // B(x) <-- x*B(x)
                        // b[0] -> b[1]
                        //memmove(&b[1], &b[0], NRoots * sizeof(b[0]));
                        for (int i = NRoots - 1; i >= 0; i--)
                        {
                            b[i + 1] = b[i];
                        }

                        b[0] = A0;
                    }
                    else
                    {
                        // T(x) <-- lambda(x) - discr_r*x*b(x)
                        t[0] = lambda[0];
                        for (int i = 0; i < NRoots; i++)
                        {
                            if (b[i] != A0)
                            {
                                t[i + 1] = (byte)(lambda[i + 1] ^ AlphaTo[(discrRth + b[i]) % Nn]);
                            }
                            else
                            {
                                t[i + 1] = lambda[i + 1];
                            }
                        }

                        if ((2 * el) <= (r + erasureCount - 1))
                        {
                            el = r + erasureCount - el;

                            // B(x) <-- inv(discr_r) * lambda(x)
                            for (int i = 0; i <= NRoots; i++)
                            {
                                b[i] = (byte)((lambda[i] == 0) ? A0 : (IndexOf[lambda[i]] - discrRth + Nn) % Nn);
                            }
                        }
                        else
                        {
                            // B(x) <-- x*B(x)
                            // b[0] -> b[1]
                            //memmove(&b[1], b, NRoots * sizeof(b[0]));
                            for (int i = NRoots - 1; i >= 0 ; i--)
                            {
                                b[i + 1] = b[i];
                            }

                            b[0] = A0;
                        }

                        //memcpy(lambda, t, (kNRoots + 1) * sizeof(t[0]));
                        t.CopyTo(lambda);
                    }
                }

                // Convert lambda to index form and compute deg(lambda(x))
                int degLambda = 0;
                for (int i = 0; i < NRoots + 1; i++)
                {
                    lambda[i] = IndexOf[lambda[i]];
                    if (lambda[i] != A0)
                    {
                        degLambda = i;
                    }
                }

                // Find roots of the error+erasure locator polynomial by Chien search
                //memcpy(&(reg[1]), &(lambda[1]), NRoots * sizeof(reg[0]));
                lambda.Slice(1).CopyTo(reg.Slice(1));

                count = 0;  // Number of roots of lambda(x)

                for (int i = 1, k = IPrim - 1; i <= Nn; i++, k = (k + IPrim) % Nn)
                {

                    byte q = 1;  // lambda[0] is always 0
                    for (int j = degLambda; j > 0; j--)
                    {
                        if (reg[j] != A0)
                        {
                            reg[j] = (byte)((reg[j] + j) % Nn);
                            q ^= AlphaTo[reg[j]];
                        }
                    }

                    if (q != 0)
                    {
                        // Not a root
                        continue;
                    }

                    // Store root (index-form) and error location number
                    root[count] = (byte)i;
                    loc[count] = (byte)k;

                    // Increment count
                    count++;

                    // If we've already found max possible roots,
                    // abort the search to save time
                    //
                    if (count == degLambda)
                    {
                        break;
                    }
                }

                if (degLambda != count)
                {
                    //
                    // deg(lambda) unequal to number of roots => uncorrectable
                    // error detected
                    //
                    count = -1;
                }
                else
                {
                    //
                    // Compute err+eras evaluator poly omega(x) = s(x)*lambda(x) (modulo x*NRoots). in index form. Also find deg(omega).
                    //
                    int deg_omega = 0;
                    for (int i = 0; i < NRoots; i++)
                    {
                        byte tmp = 0;
                        for (int j = (degLambda < i) ? degLambda : i; j >= 0; j--)
                        {
                            if ((s[i - j] != A0) && (lambda[j] != A0))
                            {
                                tmp ^= AlphaTo[(s[i - j] + lambda[j]) % Nn];
                            }
                        }

                        if (tmp != 0)
                        {
                            deg_omega = i;
                        }

                        omega[i] = IndexOf[tmp];
                    }
                    omega[NRoots] = A0;

                    //
                    // Compute error values in poly-form. num1 = omega(inv(X(l))), num2
                    // = inv(X(l))**(kFcr-1) and den = lambda_pr(inv(X(l))) all in
                    // poly-form
                    //
                    for (int j = count - 1; j >= 0; j--)
                    {
                        byte num1 = 0;
                        for (int i = deg_omega; i >= 0; i--)
                        {
                            if (omega[i] != A0)
                            {
                                num1 ^= AlphaTo[(omega[i] + (i * root[j])) % Nn];
                            }
                        }

                        byte num2 = AlphaTo[((root[j] * (Fcr - 1)) + Nn) % Nn];
                        byte den = 0;

                        //
                        // lambda[i+1] for i even is the formal derivative lambda_pr of lambda[i]
                        //

                        // Find the minimum of deg_lambda and NRoots - 1
                        int start = (degLambda < NRoots - 1) ? degLambda : NRoots - 1;

                        // If this minimum is odd, step it down to the next lowest integer.
                        // We can do this with a bithack: AND the number with 0xFFFFFFFE
                        start &= ~1;

                        // Step down through evens
                        for (int i = start; i >= 0; i -= 2)
                        {
                            if (lambda[i + 1] != A0)
                            {
                                den ^= AlphaTo[(lambda[i + 1] + (i * root[j])) % Nn];
                            }
                        }

                        if (den == 0)
                        {
                            count = -1;
                        }
                        else
                        {
                            // Apply error to data
                            if (num1 != 0)
                            {
                                block[loc[j]] ^= AlphaTo[(IndexOf[num1] + IndexOf[num2] + Nn - IndexOf[den]) % Nn];
                            }
                        }
                    }

                    if (erasureCount > 0)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            erasurePositions[i] = loc[i];
                        }
                    }
                }
            }

            if (dualBasis)
            {
                // Convert block from conventional to dual basis
                //
                for (int i = 0; i < Nn; i++)
                {
                    block[i] = TalToDualBasis[block[i]];
                }
            }

            return count;
        }
    }
}
