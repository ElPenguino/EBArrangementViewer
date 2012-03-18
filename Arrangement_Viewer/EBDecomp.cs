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

        /// <summary>
        ///
        /// </summary>
        /// <param name="start"></param>
        /// <param name="data"></param>
        /// <returns>The size of the decompressed data</returns>
        public static byte[] Decomp(int start, BinaryReader Data)
        {
            Data.BaseStream.Seek(start, SeekOrigin.Begin);
            List<byte> outputbuffer = new List<byte>();
            int command;
            int commandlength;
            ushort tempaddress = 0xFFFF;
            byte tempbyte;
            byte tempbyte2;
            Boolean completed = false;

            while (!completed)
            {
                //Console.WriteLine(outputbuffer.Count);
                tempbyte = Data.ReadByte();
                command = (tempbyte >> 5);
                commandlength = ((tempbyte & 0x1F) + 1);
                //Console.WriteLine("{0}:{1}", command, commandlength);

                if (command == 7)
                { //Long Command
                    command = ((tempbyte & 0x1C) >> 2);
                    commandlength = (((tempbyte & 3) << 8) + Data.ReadByte() + 1);
                    //Console.WriteLine("\t{0}:{1}", command, commandlength);
                }
                Console.WriteLine(command);
                if (command >= 4)
                {
                    tempaddress = EndianSwap(Data.ReadUInt16());
                    //Console.WriteLine("{0}: {1}", tempaddress, outputbuffer.Count);
                    if (tempaddress > outputbuffer.Count)
                        throw new Exception("Bad Address!");
                }
                switch (command)
                {
                    case 0: //Direct Copy (uncompressed)
                        for (int i = 0; i < commandlength; i++)
                        {
                            tempbyte = Data.ReadByte();
                            Console.Write("{0:X2}", tempbyte);
                            outputbuffer.Add(tempbyte);
                        }
                        break;
                    case 1: //Byte Fill (1 byte RLE)
                        tempbyte = Data.ReadByte();
                        for (int i = 0; i < commandlength; i++)
                            outputbuffer.Add(tempbyte);
                        break;
                    case 2: //Word Fill (2 byte RLE)
                        tempbyte = Data.ReadByte();
                        tempbyte2 = Data.ReadByte();
                        for (int i = 0; i < commandlength; i++)
                        {
                            if (i % 2 == 1)
                                outputbuffer.Add(tempbyte2);
                            else
                                outputbuffer.Add(tempbyte);
                        }
                        break;
                    case 3: //Increasing Fill (1 byte RLE, increasing)
                        tempbyte = Data.ReadByte();
                        for (int i = 0; i < commandlength; i++)
                            outputbuffer.Add(tempbyte++);
                        break;
                    case 4: //Repeat (buffer copy)
                        for (int i = 0; i < commandlength; i++)
                            outputbuffer.Add(outputbuffer[tempaddress++]);
                        break;
                    case 5: // Same as 4, but with reversed bits
                        if (outputbuffer.Count + commandlength > 0xFFFF)
                            throw new Exception("Bit Reversal Block overflows buffer");
                        for (int i = 0; i < commandlength; i++)
                            outputbuffer.Add(ReverseBits(outputbuffer[tempaddress++])); //magically reverse bits! ooh!
                        break;
                    case 6: //Same as 4, but read backwards
                        if (outputbuffer.Count - commandlength + 1 < 0)
                            throw new Exception("Reverse Block overflows buffer");
                        for (int i = 0; i < commandlength; i++)
                           outputbuffer.Add(outputbuffer[tempaddress--]);
                        break;
                    case 7:
                        completed = true;
                        break;
                }
            }

            return outputbuffer.ToArray();
        }
        private static ushort EndianSwap(ushort input)
        {
            return (ushort)((input>>8) + (input<<8));
        }
        private static byte ReverseBits(byte input)
        {
            return (byte)((input * 0x0202020202 & 0x010884422010) % 1023);
        }
        public static int Decomp(int start, BinaryReader stream, byte[] output)
        {
            int maxlen = output.Length;
            int bpos = 0, bpos2 = 0;
            byte tmp, temp;
            stream.BaseStream.Seek(start, SeekOrigin.Begin);
            temp = stream.ReadByte();
            //Console.WriteLine(temp);

            while (temp != 0xFF)
            {
                //Console.WriteLine(bpos);
                if (stream.BaseStream.Position >= stream.BaseStream.Length)
                    throw new Exception("Data overflow");

                int cmdtype = temp >> 5;
                int len = (temp & 0x1F) + 1;
                //Console.WriteLine("{0}:{1}", cmdtype, len);
                if (cmdtype == 7)
                {
                    cmdtype = (temp & 0x1C) >> 2;
                    len = ((temp & 3) << 8) + stream.ReadByte() + 1;
                    //Console.WriteLine("\t{0}:{1}", cmdtype, len);
                }

                if (bpos + len > maxlen || bpos + len < 0)
                    throw new Exception("Block overflowed buffer");

                if (cmdtype >= 4)
                {
                    bpos2 = (stream.ReadByte() << 8) + stream.ReadByte();
                    //Console.WriteLine("{0}: {1}", bpos2, bpos);
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
                            output[bpos++] = ReverseBits(output[bpos2++]);
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
                //Console.WriteLine(temp);
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
