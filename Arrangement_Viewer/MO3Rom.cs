using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace EarthboundArrViewer
{
    class MO3Rom:BinaryReader,ROMFile
    {
        public static int Japan = 'J';
        public static int America = 'E';
        public static int Europe = 'P';
        public static int Germany = 'D';
        public static int French = 'F';
        public static int Italy = 'I';
        public static int Spain = 'S';
        public bool isMother3 = false;

        public bool isSupported() {
            return this.isMother3;
        }
        public static int HexToGBA(int address)
        {

            if (address >= 0 && address < 0x2000000)
                return address + 0x8000000;
            else
                throw new Exception("File offset out of range: " + address);
        }

        public static int GBAToHex(int address)
        {
            if (address >= 0x8000000 && address < 0xA000000)
                address -= 0x8000000;
            else
                throw new Exception("SNES address out of range: " + address);

            return address;
        }

        public MO3Rom(string filename):base (File.Open(filename, FileMode.Open))
        {
            if (this.GetGameID() == "A3UJ")
                this.isMother3 = true;
        }

        public int ReadGBAPointer(int offset) {
            if (offset > this.BaseStream.Length)
            {
                Console.WriteLine("Warning: Attempted read beyond file end!");
                return 0;
            }
            this.BaseStream.Seek(offset, SeekOrigin.Begin);
            return GBAToHex(this.ReadInt32());
        }
        public void SeekToOffset(int offset)
        {
            BaseStream.Seek(offset, SeekOrigin.Begin);
        }
        public void SeekToGBAOffset(int offset)
        {
            BaseStream.Seek(GBAToHex(offset), SeekOrigin.Begin);
        }

        public string GetGameID() {
            this.SeekToOffset(0xAC);
            return new string(this.ReadChars(4));
        }
        public string GetGameName() {
            this.SeekToOffset(0xA0);
            return new string(this.ReadChars(12));
        }
        public byte GetGameDest()
        {
            this.SeekToOffset(0xAF);
            return this.ReadByte();
        }
        public MultilayerArrangement[] getArrangements() {
            MultilayerArrangement[] output;
            this.SeekToOffset(0x01D0BC9C);
            ushort numlayers = this.ReadUInt16();
            ushort numarrs = this.ReadUInt16();
            output = new MultilayerArrangement[numarrs];
            Arrangement[] layers = new Arrangement[numlayers];
            for (int i = 0; i < numlayers; i++)
                layers[i] = GetMO3Layer(i);
            MultilayerArrangement temp;
            this.SeekToOffset(0x1D1EFC0);
            ushort layer1, layer2, alpha1, alpha2;
            for (int i = 0; i < numarrs; i++) {
                this.SeekToOffset(0x1D1EFC0 + i * 12);
                layer1 = this.ReadUInt16();
                layer2 = this.ReadUInt16();
                alpha1 = this.ReadUInt16();
                alpha2 = this.ReadUInt16();
                temp = new MultilayerArrangement(layers[layer1], layers[layer2]);
                temp.opacity[0] = alpha1 / 16.0;
                temp.opacity[1] = alpha2 / 16.0;
                output[i] = temp;
            }
            Console.WriteLine("Read {0} arrangements", output.Length);
            this.Close();
            this.Dispose();
            return output;
        }
        private Arrangement GetMO3Layer(int id) {
            if ((id == 0) || (id > 546))
                return buildArrangement(new byte[2048], new byte[32], new byte[16], 4, "BG 0");
            ushort gfxid;
            ushort arrid;
            byte[] palette, palette2;
            byte[] gfx, arr;
            int tmploc, datasize;

            this.SeekToOffset(0x1D0BCA0 + id * 0x90);
            gfxid = this.ReadUInt16();
            arrid = this.ReadUInt16();
            palette = this.ReadBytes(32);
            palette2 = this.ReadBytes(32);
            this.SeekToOffset(0x1D1FB30 + arrid * 8);
            tmploc = this.ReadInt32();
            datasize = this.ReadInt16();
            this.SeekToOffset(0x1D1FB28 + tmploc);
            if (datasize != 2048)
                throw new Exception("Arrangement size != 2048");
            arr = this.ReadBytes(datasize);

            this.SeekToOffset(0x1D1FB30 + gfxid * 8);
            tmploc = this.ReadInt32();
            datasize = this.ReadInt16();
            this.SeekToOffset(0x1D1FB28 + tmploc);
            gfx = this.ReadBytes(datasize);

            Arrangement output = buildArrangement(arr, gfx, palette, 4, "BG " + id);
            this.SeekToOffset(0x1D0BCA0 + id * 0x90 + 116);
            output.hdrift = this.ReadInt16();
            output.vdrift = this.ReadInt16();
            this.ReadInt32();
            output.hamplitude = (double)this.ReadInt16() / 256.0;
            output.vamplitude = (double)this.ReadInt16() / 256.0;
            output.hperiod = (double)this.ReadInt16() / 256.0;
            output.vperiod = (double)this.ReadInt16() / 256.0;
            output.hfrequency = (double)this.ReadInt16() / 256.0;
            output.vfrequency = (double)this.ReadInt16() / 256.0;

            return output;
        }
        private Arrangement buildArrangement(byte[] arrangementData, byte[] graphicsData, byte[] paletteData, byte bpp, String name) {
            return new Arrangement(arrangementData, graphicsData, paletteData, bpp, name, true);
        }
    }
}
