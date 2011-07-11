using System;
using System.IO;
using System.Text;
using System.Collections.Generic;


namespace PKHack
{
    /*
     * This is a set of general-purpose ROM related utility functions;
     * I'm putting them in the Rom class because I can't really think of
     * anywhere else it would make sense to put them.
     */
    public partial class Rom
    {
        // This is an internal optimization for the comp/decomp methods.
        // Every element in this array is the binary reverse of its index.
        private static byte[] bitrevs = new byte[]
        {
            0,   128, 64,  192, 32,  160, 96,  224, 16,  144, 80,  208, 48,  176, 112, 240,
            8,   136, 72,  200, 40,  168, 104, 232, 24,  152, 88,  216, 56,  184, 120, 248,
            4,   132, 68,  196, 36,  164, 100, 228, 20,  148, 84,  212, 52,  180, 116, 244,
            12,  140, 76,  204, 44,  172, 108, 236, 28,  156, 92,  220, 60,  188, 124, 252,
            2,   130, 66,  194, 34,  162, 98,  226, 18,  146, 82,  210, 50,  178, 114, 242,
            10,  138, 74,  202, 42,  170, 106, 234, 26,  154, 90,  218, 58,  186, 122, 250,
            6,   134, 70,  198, 38,  166, 102, 230, 22,  150, 86,  214, 54,  182, 118, 246,
            14,  142, 78,  206, 46,  174, 110, 238, 30,  158, 94,  222, 62,  190, 126, 254,
            1,   129, 65,  193, 33,  161, 97,  225, 17,  145, 81,  209, 49,  177, 113, 241,
            9,   137, 73,  201, 41,  169, 105, 233, 25,  153, 89,  217, 57,  185, 121, 249,
            5,   133, 69,  197, 37,  165, 101, 229, 21,  149, 85,  213, 53,  181, 117, 245,
            13,  141, 77,  205, 45,  173, 109, 237, 29,  157, 93,  221, 61,  189, 125, 253,
            3,   131, 67,  195, 35,  163, 99,  227, 19,  147, 83,  211, 51,  179, 115, 243,
            11,  139, 75,  203, 43,  171, 107, 235, 27,  155, 91,  219, 59,  187, 123, 251,
            7,   135, 71,  199, 39,  167, 103, 231, 23,  151, 87,  215, 55,  183, 119, 247,
            15,  143, 79,  207, 47,  175, 111, 239, 31,  159, 95,  223, 63,  191, 127, 255,
        };

        // Do not try to understand what this is doing. It will hurt you.
        // The only documentation for this decompression routine is a 65816
        // disassembly.

        /// <summary>
        ///
        /// </summary>
        /// <param name="start"></param>
        /// <param name="data"></param>
        /// <param name="output">Must already be allocated with at least enough space</param>
        /// <returns>The size of the decompressed data</returns>
        public static int Decomp(int start, BinaryReader stream, byte[] output)
        {
            int maxlen = output.Length;
            int bpos = 0, bpos2 = 0;
            byte tmp, temp;
            stream.BaseStream.Seek(start, SeekOrigin.Begin);
            temp = stream.ReadByte();

            while (temp != 0xFF)
            {
                if (stream.BaseStream.Position >= stream.BaseStream.Length)
                    throw new Exception("Data overflow");

                int cmdtype = temp >> 5;
                int len = (temp & 0x1F) + 1;

                if (cmdtype == 7)
                {
                    cmdtype = (temp & 0x1C) >> 2;
                    len = ((temp & 3) << 8) + stream.ReadByte() + 1;
                }

                if (bpos + len > maxlen || bpos + len < 0)
                    throw new Exception("Block overflowed buffer");

                if (cmdtype >= 4)
                {
                    bpos2 = (stream.ReadByte() << 8) + stream.ReadByte();

                    if (bpos2 >= maxlen || bpos2 < 0)
                        throw new Exception("Repeat Block overflows buffer");
                }

                switch (cmdtype)
                {
                    case 0: // Uncompressed block
                        while (len-- != 0)
                            output[bpos++] = stream.ReadByte();
                        break;

                    case 1: // RLE
                        temp = stream.ReadByte();
                        while (len-- != 0)
                            output[bpos++] = temp;
                        break;

                    case 2: // 2-byte RLE
                        temp = stream.ReadByte();
                        tmp = stream.ReadByte();
                        if (bpos + 2 * len > maxlen || bpos < 0)
                            throw new Exception("RLE Block overflows buffer");
                        while (len-- != 0)
                        {
                            output[bpos++] = temp;
                            output[bpos++] = tmp;
                        }
                        break;

                    case 3: // Incremental sequence
                        tmp = stream.ReadByte();
                        while (len-- != 0)
                            output[bpos++] = tmp++;
                        break;

                    case 4: // Repeat previous data
                        if (bpos2 + len > maxlen || bpos2 < 0)
                            throw new Exception("Repeat Block overflows buffer");
                        for (int i = 0; i < len; i++)
                            output[bpos++] = output[bpos2 + i];
                        break;

                    case 5: // Output with bits reversed
                        if (bpos2 + len > maxlen || bpos2 < 0)
                            throw new Exception("Bit Reversal Block overflows buffer");
                        while (len-- != 0)
                            output[bpos++] = bitrevs[output[bpos2++] & 0xFF];
                        break;

                    case 6:
                        if (bpos2 - len + 1 < 0)
                            throw new Exception("??? Block overflows buffer");
                        while (len-- != 0)
                            output[bpos++] = output[bpos2--];
                        break;

                    case 7:
                        throw new Exception("Bad command type");
                }
                temp = stream.ReadByte();
            }
            return bpos;
        }

        public static int GetDecompressedSize(int start, BinaryReader stream)
        {
            int bpos = 0, bpos2 = 0;
            byte tmp;
            stream.BaseStream.Seek(start, SeekOrigin.Begin);
            tmp = stream.ReadByte();
            while (tmp != 0xFF)
            {
                if (stream.BaseStream.Position >= stream.BaseStream.Length)
                    throw new Exception("Data overflow");

                int cmdtype = tmp >> 5;
                int len = (tmp & 0x1F) + 1;

                if (cmdtype == 7)
                {
                    cmdtype = (tmp & 0x1C) >> 2;
                    len = ((tmp & 3) << 8) + stream.ReadByte() + 1;
                }

                if (bpos + len < 0)
                    throw new Exception("Block overflows buffer");

                if (cmdtype >= 4)
                {
                    bpos2 = (stream.ReadByte() << 8) + stream.ReadByte();
                    if (bpos2 < 0)
                        throw new Exception("Block overflows buffer");
                }

                switch (cmdtype)
                {
                    case 0: // Uncompressed block
                        bpos += len;
                        stream.BaseStream.Seek(len, SeekOrigin.Current);
                        break;

                    case 1: // RLE
                        bpos += len;
                        stream.BaseStream.Seek(1, SeekOrigin.Current);
                        break;

                    case 2: // 2-byte RLE
                        if (bpos < 0)
                            throw new Exception("RLE Block overflows buffer");
                        bpos += 2 * len;
                        stream.BaseStream.Seek(2, SeekOrigin.Current);
                        break;

                    case 3: // Incremental sequence
                        bpos += len;
                        stream.BaseStream.Seek(1, SeekOrigin.Current);
                        break;

                    case 4: // Repeat previous data
                        if (bpos2 < 0)
                            throw new Exception("Repeat Block overflows buffer");
                        bpos += len;
                        break;

                    case 5: // Output with bits reversed
                        if (bpos2 < 0)
                            throw new Exception("Bit Reversal Block overflows buffer");
                        bpos += len;
                        break;

                    case 6:
                        if (bpos2 - len + 1 < 0)
                            throw new Exception("??? Block overflows buffer");
                        bpos += len;
                        break;

                    case 7:
                        throw new Exception("Bad command type");
                }
                tmp = stream.ReadByte();
            }
            return bpos;
        }
    }
}
