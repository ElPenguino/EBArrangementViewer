using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace EarthboundArrViewer
{
    interface ROMFile {
        MultilayerArrangement[] getArrangements();
        bool isSupported();
        void Close();
        void Dispose();
    }
    class EBRom:BinaryReader, ROMFile
    {
        public static int Japan = 0;
        public static int America = 1;
        public static int Europe = 2;
        public Boolean HasHeader;
        public Boolean isValid = true;
        private Boolean isHiROM = true;
        private const int ARRANGEMENT = 1;
        private const int GRAPHICS = 2;
        private const int PALETTE = 4;
        private const int ARRANGEMENTSIZE = 32 * 32 * 2;
        public bool isEarthbound = false;

        public bool isSupported() {
            return this.isEarthbound;
        }
        public static int HexToSnes(int address)
        {

            if (address >= 0 && address < 0x400000)
                return address + 0xC00000;
            else if (address >= 0x400000 && address < 0x600000)
                return address;
            else
                throw new Exception("File offset out of range: " + address);
        }

        public static int SnesToHex(int address)
        {
            if (address >= 0x400000 && address < 0x600000)
                address -= 0x0;
            else if (address >= 0xC00000 && address < 0x1000000)
                address -= 0xC00000;
            else
                throw new Exception("SNES address out of range: " + address);

            return address;
        }

        public EBRom(string filename):base (File.Open(filename, FileMode.Open))
        {
            HasHeader = false;
            try { DetectHeader(); }
            catch {
                this.Close();
            }
        }
        
        public int ReadSNESPointer(int offset) {
            if (offset > this.BaseStream.Length)
            {
                Console.WriteLine("Warning: Attempted read beyond file end!");
                return 0;
            }
            this.BaseStream.Seek(offset+(HasHeader ? 0x200 : 0), SeekOrigin.Begin);
            return SnesToHex(this.ReadInt32());
        }
        public byte[] ReadCompressedData(int offset)
        {
           byte[] output = new byte[GetDecompressedSize(offset + (HasHeader ? 0x200 : 0), this)];

           //Console.WriteLine("Compressed data at {0:x}", offset); 
           Decomp(offset + (HasHeader ? 0x200 : 0), this, output);
           //return PKHack.Rom.Decomp(offset + (HasHeader ? 0x200 : 0), this); 
           return output;
        }
        public void SeekToOffset(int offset)
        {
            BaseStream.Seek(offset + (HasHeader ? 0x200 : 0), SeekOrigin.Begin);
        }
        public void SeekToSNESOffset(int offset)
        {
            BaseStream.Seek(SnesToHex(offset + (HasHeader ? 0x200 : 0)), SeekOrigin.Begin);
        }
        public void DetectHeaderHiROM()
        {
            Boolean HasHeaderOld = HasHeader;
            if (this.BaseStream.Length < 0x10000)
                throw new Exception("File too small");
            if (CheckHeader(0xFFDC))
                return;
            HasHeader = true;
            if (CheckHeader(0xFFDC))
                return;
            HasHeader = HasHeaderOld;
            throw new Exception("Not a HiROM");
        }
        public void DetectHeaderLoROM()
        {
            Boolean HasHeaderOld = HasHeader;
            if (this.BaseStream.Length < 0x8000)
                throw new Exception("File too small");
            if (CheckHeader(0x7FDC))
                return;
            HasHeader = true;
            if (CheckHeader(0x7FDC))
                return;
            HasHeader = HasHeaderOld;
            throw new Exception("Not a LoROM");
        }
        public Boolean CheckHeader(int offset)
        {
            SeekToOffset(offset);
            UInt16 complement = ReadUInt16();
            UInt16 checksum   = ReadUInt16();
            return ((UInt16)~complement == checksum);
        }
        public void DetectHeader()
        {
            Boolean HiROM = true;
            Boolean LoROM = true;
            try { DetectHeaderHiROM(); }
            catch (Exception)
            {
                HiROM = false;
            }
            try { DetectHeaderLoROM(); }
            catch (Exception)
            {
                LoROM = false;
            }
            if ((LoROM || HiROM) == false)
                throw new Exception("Bad ROM");
            else if ((LoROM && HiROM) == true)
                throw new Exception("Cannot handle this ROM");
            else if (LoROM == true)
                this.isHiROM = false;
            if (this.GetGameID() == "MB  ")
                this.isEarthbound = true;
        }

        public string GetGameID()
        {
            this.SeekToOffset(0xFFB2-(isHiROM ? 0 : 0x8000));
            return new string(this.ReadChars(4));
        }
        public byte GetGameDest()
        {
            this.SeekToOffset(0xFFD9 - (isHiROM ? 0 : 0x8000));
            return this.ReadByte();
        }
        public static byte[] Decomp(int start, BinaryReader Data) {
            Data.BaseStream.Seek(start, SeekOrigin.Begin);
            List<byte> outputbuffer = new List<byte>();
            int command;
            int commandlength;
            ushort tempaddress = 0xFFFF;
            byte tempbyte;
            byte tempbyte2;
            Boolean completed = false;

            while (!completed) {
                //Console.WriteLine(outputbuffer.Count);
                tempbyte = Data.ReadByte();
                command = (tempbyte >> 5);
                commandlength = ((tempbyte & 0x1F) + 1);
                //Console.WriteLine("{0}:{1}", command, commandlength);

                if (command == 7) { //Long Command
                    command = ((tempbyte & 0x1C) >> 2);
                    commandlength = (((tempbyte & 3) << 8) + Data.ReadByte() + 1);
                    //Console.WriteLine("\t{0}:{1}", command, commandlength);
                }
                Console.WriteLine(command);
                if (command >= 4) {
                    tempaddress = EndianSwap(Data.ReadUInt16());
                    //Console.WriteLine("{0}: {1}", tempaddress, outputbuffer.Count);
                    if (tempaddress > outputbuffer.Count)
                        throw new Exception("Bad Address!");
                }
                switch (command) {
                    case 0: //Direct Copy (uncompressed)
                        for (int i = 0; i < commandlength; i++) {
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
                        for (int i = 0; i < commandlength; i++) {
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
        private static ushort EndianSwap(ushort input) {
            return (ushort)((input >> 8) + (input << 8));
        }
        private static byte ReverseBits(byte input) {
            return (byte)((input * 0x0202020202 & 0x010884422010) % 1023);
        }
        public static int Decomp(int start, BinaryReader stream, byte[] output) {
            int maxlen = output.Length;
            int bpos = 0, bpos2 = 0;
            byte tmp, temp;
            stream.BaseStream.Seek(start, SeekOrigin.Begin);
            temp = stream.ReadByte();
            //Console.WriteLine(temp);

            while (temp != 0xFF) {
                //Console.WriteLine(bpos);
                if (stream.BaseStream.Position >= stream.BaseStream.Length)
                    throw new Exception("Data overflow");

                int cmdtype = temp >> 5;
                int len = (temp & 0x1F) + 1;
                //Console.WriteLine("{0}:{1}", cmdtype, len);
                if (cmdtype == 7) {
                    cmdtype = (temp & 0x1C) >> 2;
                    len = ((temp & 3) << 8) + stream.ReadByte() + 1;
                    //Console.WriteLine("\t{0}:{1}", cmdtype, len);
                }

                if (bpos + len > maxlen || bpos + len < 0)
                    throw new Exception("Block overflowed buffer");

                if (cmdtype >= 4) {
                    bpos2 = (stream.ReadByte() << 8) + stream.ReadByte();
                    //Console.WriteLine("{0}: {1}", bpos2, bpos);
                    if (bpos2 >= maxlen || bpos2 < 0)
                        throw new Exception("Repeat Block overflows buffer");
                }

                switch (cmdtype) {
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
                        while (len-- != 0) {
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

        public static int GetDecompressedSize(int start, BinaryReader stream) {
            int bpos = 0, bpos2 = 0;
            byte tmp;
            stream.BaseStream.Seek(start, SeekOrigin.Begin);
            tmp = stream.ReadByte();
            while (tmp != 0xFF) {
                if (stream.BaseStream.Position >= stream.BaseStream.Length)
                    throw new Exception("Data overflow");

                int cmdtype = tmp >> 5;
                int len = (tmp & 0x1F) + 1;

                if (cmdtype == 7) {
                    cmdtype = (tmp & 0x1C) >> 2;
                    len = ((tmp & 3) << 8) + stream.ReadByte() + 1;
                }

                if (bpos + len < 0)
                    throw new Exception("Block overflows buffer");

                if (cmdtype >= 4) {
                    bpos2 = (stream.ReadByte() << 8) + stream.ReadByte();
                    if (bpos2 < 0)
                        throw new Exception("Block overflows buffer");
                }

                switch (cmdtype) {
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
        public MultilayerArrangement[] getArrangements() {
            MultilayerArrangement[] arrangements = new MultilayerArrangement[0x1E3 + 15 + ((this.GetGameDest() == America) ? 1 : 0)];
            int i;
            this.SeekToOffset(0x0ADCA1);
            byte[] tableData = this.ReadBytes(17 * 327);
            List<Arrangement> bglayers = new List<Arrangement>();
            for (i = 0; i < tableData.Length / 17; i++) {
                bglayers.Add(ReadCompressedArrangement(
                       this.ReadSNESPointer(0xAD93D + tableData[i * 17] * 4),
                       this.ReadSNESPointer(0xAD7A1 + tableData[i * 17] * 4),
                       this.ReadSNESPointer(0xADAD9 + tableData[i * 17 + 1] * 4),
                       tableData[i * 17 + 2],
                       "BattleBG " + i,
                       ARRANGEMENT + GRAPHICS));
            }
            this.SeekToOffset(0x0BD89A);
            for (i = 0; i < 0x1E3; i++)
                arrangements[i] = new MultilayerArrangement(bglayers[this.ReadInt16()], bglayers[this.ReadInt16()]);
            bglayers.Clear();
            i = 0x1E3;
            if (this.GetGameDest() == Japan) {
                arrangements[i++] = new MultilayerArrangement(ReadCompressedArrangement(0x21C692, 0x21C6DF, 0x21C800, 2, "Nintendo", ARRANGEMENT + GRAPHICS + PALETTE));
                arrangements[i++] = new MultilayerArrangement(ReadCompressedArrangement(0x21C470, 0x21C4DC, 0x21C800, 2, "Itoi", ARRANGEMENT + GRAPHICS + PALETTE));
                arrangements[i++] = new MultilayerArrangement(ReadCompressedArrangement(0x18F8D6, 0x18FAD4, 0x18F8CE, 2, "Faulty cartridge", ARRANGEMENT + GRAPHICS));
                arrangements[i++] = new MultilayerArrangement(ReadCompressedArrangement(0x18F05E, 0x18F336, 0x18F8CE, 2, "Piracy is bad", ARRANGEMENT + GRAPHICS));
                arrangements[i++] = new MultilayerArrangement(ReadCompressedArrangement(0x2148AB, 0x2148EF, 0x2149B8, 2, "Logo 1", ARRANGEMENT + GRAPHICS + PALETTE));
                arrangements[i++] = new MultilayerArrangement(ReadCompressedArrangement(0x214317, 0x214380, 0x214586, 2, "Logo 2", ARRANGEMENT + GRAPHICS + PALETTE));
                arrangements[i++] = new MultilayerArrangement(ReadCompressedArrangement(0x2145CA, 0x21463E, 0x21480E, 2, "Logo 3", ARRANGEMENT + GRAPHICS + PALETTE));
                arrangements[i++] = new MultilayerArrangement(ReadCompressedArrangement(0x2149FC, 0x214F4E, 0x219CB9, 8, "Gas Station", ARRANGEMENT + GRAPHICS + PALETTE));
                arrangements[i++] = new MultilayerArrangement(ReadCompressedArrangement(0x2149FC, 0x214F4E, 0x219D5F, 8, "Gas Station Alt", ARRANGEMENT + GRAPHICS + PALETTE));
                //arrangements[i++] = new MultilayerArrangement(ReadCompressedArrangement(0x21B18C, 0x21A0A0, 0x219CB9, 8, "Title Screen", ARRANGEMENT + GRAPHICS + PALETTE));
                byte[] arrangementData;
                byte[] graphicsData, paletteData;
                byte[] decompBuffer;
                for (int j = 0; j < 6; j++) {
                    decompBuffer = this.ReadCompressedData(this.ReadSNESPointer(0x2030E5 + j * 4));
                    arrangementData = new byte[ARRANGEMENTSIZE];
                    paletteData = new byte[64];
                    graphicsData = new byte[decompBuffer.Length - ARRANGEMENTSIZE - 64];
                    Array.Copy(decompBuffer, paletteData, 64);
                    Array.Copy(decompBuffer, 64, arrangementData, 0, ARRANGEMENTSIZE);
                    Array.Copy(decompBuffer, ARRANGEMENTSIZE + 64, graphicsData, 0, graphicsData.Length);

                    arrangements[i + j] = new MultilayerArrangement(ReadMapArrangement(arrangementData, graphicsData, paletteData, 4, "Map " + j));
                }
            }
            else if (this.GetGameDest() == America) {

                arrangements[i++] = new MultilayerArrangement(ReadCompressedArrangement(0x21AD01, 0x21AD4E, 0x21AE70, 2, "Nintendo", ARRANGEMENT + GRAPHICS));
                arrangements[i++] = new MultilayerArrangement(ReadCompressedArrangement(0x21AADF, 0x21AB4B, 0x21AE70, 2, "Itoi", ARRANGEMENT + GRAPHICS));
                arrangements[i++] = new MultilayerArrangement(ReadCompressedArrangement(0x18F3C6, 0x18F5C4, 0x18F3BE, 2, "Faulty cartridge", ARRANGEMENT + GRAPHICS));
                arrangements[i++] = new MultilayerArrangement(ReadCompressedArrangement(0x18F05E, 0x18F20D, 0x18F3BE, 2, "Piracy is bad", ARRANGEMENT + GRAPHICS));
                arrangements[i++] = new MultilayerArrangement(ReadCompressedArrangement(0x215455, 0x21549E, 0x21558F, 2, "Logo 1", ARRANGEMENT + GRAPHICS + PALETTE));
                arrangements[i++] = new MultilayerArrangement(ReadCompressedArrangement(0x214EC1, 0x214F2A, 0x215130, 2, "Logo 2", ARRANGEMENT + GRAPHICS + PALETTE));
                arrangements[i++] = new MultilayerArrangement(ReadCompressedArrangement(0x215174, 0x2151E8, 0x2153B8, 2, "Logo 3", ARRANGEMENT + GRAPHICS + PALETTE));
                arrangements[i++] = new MultilayerArrangement(ReadCompressedArrangement(0x2155D3, 0x215B33, 0x21A9B7, 8, "Gas Station", ARRANGEMENT + GRAPHICS + PALETTE));
                arrangements[i++] = new MultilayerArrangement(ReadCompressedArrangement(0x2155D3, 0x215B33, 0x21AA5D, 8, "Gas Station Alt", ARRANGEMENT + GRAPHICS + PALETTE));
                arrangements[i++] = new MultilayerArrangement(ReadCompressedArrangement(0x21AF7D, 0x21B211, 0x21CDE1, 8, "Title Screen", ARRANGEMENT + GRAPHICS + PALETTE));
                byte[] arrangementData;
                byte[] graphicsData, paletteData;
                byte[] decompBuffer;
                for (int j = 0; j < 6; j++) {
                    decompBuffer = this.ReadCompressedData(this.ReadSNESPointer(0x202190 + j * 4));
                    arrangementData = new byte[ARRANGEMENTSIZE];
                    paletteData = new byte[64];
                    graphicsData = new byte[decompBuffer.Length - ARRANGEMENTSIZE - 64];
                    Array.Copy(decompBuffer, paletteData, 64);
                    Array.Copy(decompBuffer, 64, arrangementData, 0, ARRANGEMENTSIZE);
                    Array.Copy(decompBuffer, ARRANGEMENTSIZE + 64, graphicsData, 0, graphicsData.Length);

                    arrangements[i + j] = new MultilayerArrangement(ReadMapArrangement(arrangementData, graphicsData, paletteData, 4, "Map " + j));
                }
                //int gfxOffset;
                //for (int i = 0; i < 31; i++)
                //{
                //    romfile.seekToOffset(0xCF04D+i*12);
                //    gfxOffset = romfile.ReadUInt16()+0xC0000;
                //    arrangements.Add(buildArrangement(romfile.ReadSNESPointer(0xCF593+i*4), gfxOffset, 0x0CF47F+i*8, 2, "PSI " + i));
                //}
            }
            this.Close();
            this.Dispose();
            return arrangements;
        }
        public Arrangement ReadCompressedArrangement(int arrangementOffset, int graphicsOffset, int paletteOffset, byte bpp, String name, byte flags) {
            byte[] arrangementData, graphicsData, paletteData;
            if ((flags & ARRANGEMENT) == ARRANGEMENT) {
                arrangementData = this.ReadCompressedData(arrangementOffset);
            }
            else {
                this.SeekToOffset(arrangementOffset);
                arrangementData = this.ReadBytes(ARRANGEMENTSIZE);
            }
            if ((flags & GRAPHICS) == GRAPHICS) {
                graphicsData = this.ReadCompressedData(graphicsOffset);
            }
            else {
                this.SeekToOffset(paletteOffset);
                graphicsData = this.ReadBytes(8 * bpp * 256);
            }
            if ((flags & PALETTE) == PALETTE)
                paletteData = this.ReadCompressedData(paletteOffset);
            else {
                this.SeekToOffset(paletteOffset);
                paletteData = this.ReadBytes((int)Math.Pow(2, bpp + 1));
            }
            return new Arrangement(arrangementData, graphicsData, paletteData, bpp, name, false);
        }
        private Arrangement ReadMapArrangement(byte[] arrangementData, byte[] graphicsData, byte[] paletteData, byte bpp, String name) {
            return new Arrangement(arrangementData, graphicsData,paletteData, bpp, name, false);
        }
    }
}
