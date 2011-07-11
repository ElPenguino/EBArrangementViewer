using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media.Imaging;
using System.Windows.Media;


namespace EarthboundArrViewer
{
    class EBArrangement
    {
        const int SNESWidth = 256;
        const int SNESHeight = 256;
        const int SNESTileWidth = 8;
        const int SNESTileHeight = 8;

        private PixelFormat pf = PixelFormats.Indexed8;
        private BitmapPalette palette;
        private byte[] graphicsData;
        private short[] arrangementData, paletteData;
        private byte bitsPerPixel;
        private BitmapSource[] tiles;
        public String name;

        public EBArrangement(byte[] arrangementData, byte[] graphicsData, byte bitsPerPixel, String name)
        {
            this.arrangementData = new short[arrangementData.Length/2+1];
            Buffer.BlockCopy(arrangementData, 0, this.arrangementData, 0, arrangementData.Length);
            this.graphicsData = graphicsData;
            this.paletteData = new short[1];
            this.bitsPerPixel = bitsPerPixel;
            this.name = name;
            createPalette();
            createTilePalette();
        }
        private void createPalette() {
            List<System.Windows.Media.Color> colors = new List<System.Windows.Media.Color>();
            for (int i = 0; i < paletteData.Length; i++)
                colors.Add(System.Windows.Media.Color.FromRgb((byte)((paletteData[i] & 31) * 8), (byte)(((paletteData[i] >> 5) & 31) * 8), (byte)(((paletteData[i] >> 10) & 31) * 8)));
            palette = new BitmapPalette(colors);
        }
        public void setPalette(byte[] paletteData)
        {
            this.paletteData = new short[paletteData.Length / 2];
            Buffer.BlockCopy(paletteData, 0, this.paletteData, 0, paletteData.Length);
            createPalette();
        }
        private void createTilePalette()
        {
            int rawStride = ((SNESTileWidth * pf.BitsPerPixel + 31) & ~31) >> 3;
            byte[] rawImage;
            tiles = new BitmapSource[graphicsData.Length/(8*bitsPerPixel)];
            for (int i = 0; i < tiles.Length; i++)
            {
                rawImage = new byte[rawStride * 8];
        		for (byte x = 0; x < 8; x++)
        			for (byte y = 0; y < 8; y++)
        				for (byte bitplane = 0; bitplane < bitsPerPixel; bitplane++)
        					rawImage[y*8+x] += (byte)((((int)graphicsData[(i*8*bitsPerPixel)+y*2+((bitplane/2)*16+(bitplane&1))] & (1 << 7-x)) >> 7-x) << bitplane);
                tiles[i] = BitmapSource.Create(8, 8, 96, 96, pf, palette, rawImage, rawStride);
            }
        }
        public BitmapSource getGraphic()
        {
            int rawStride = ((SNESWidth * pf.BitsPerPixel + 31) & ~31) >> 3;
            byte[] rawImage = new byte[rawStride * SNESHeight];
            for (int tile = 0; tile < 32 * 32; tile++)
            {
                if (((arrangementData[tile] >> 8) & 192) == 0)
                    tiles[arrangementData[tile] & 1023].CopyPixels(rawImage, rawStride, (tile / 32) * 2048 + (tile % 32) * 8);
                else
                    flipBitmap(tiles[arrangementData[tile] & 1023], ((arrangementData[tile] & 0x4000) == 0x4000), ((arrangementData[tile] & 0x8000) == 0x8000)).CopyPixels(rawImage, rawStride, (tile / 32) * 2048 + (tile % 32) * 8);
            }
            BitmapSource output = BitmapSource.Create(SNESWidth, SNESHeight, 96, 96, pf, palette, rawImage, rawStride);
            return output;
        }
        private static BitmapSource flipBitmap(BitmapSource flipped, bool flipX, bool flipY)
        {
            System.Windows.Media.Transform tr = new System.Windows.Media.ScaleTransform((flipX ? -1 : 1), (flipY ? -1 : 1));

            TransformedBitmap transformedBmp = new TransformedBitmap();
            transformedBmp.BeginInit();
            transformedBmp.Source = flipped;
            transformedBmp.Transform = tr;
            transformedBmp.EndInit();
            return transformedBmp;
        }
    }
}
