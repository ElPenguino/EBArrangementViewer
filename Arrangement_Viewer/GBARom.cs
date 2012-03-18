using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace EarthboundArrViewer
{
    class GBARom:BinaryReader
    {
        public static int Japan = 'J';
        public static int America = 'E';
        public static int Europe = 'P';
        public static int Germany = 'D';
        public static int French = 'F';
        public static int Italy = 'I';
        public static int Spain = 'S';

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

        public GBARom(string filename):base (File.Open(filename, FileMode.Open))
        {
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
    }
}
