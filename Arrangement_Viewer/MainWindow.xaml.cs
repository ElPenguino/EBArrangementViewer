using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using System.ComponentModel;
using System.Windows.Media.Effects;

namespace EarthboundArrViewer {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        private SNESRom romfile = null;
        private GBARom romfile2 = null;
        private List<MultilayerArrangement> arrangements = new List<MultilayerArrangement>();
        private int curArr;
        private List<String> effectnames = new List<String>();
        private List<System.Windows.Media.Effects.Effect> effects = new List<System.Windows.Media.Effects.Effect>();
        private MenuItem checkedeffect;
        const int ARRANGEMENT = 1;
        const int GRAPHICS = 2;
        const int PALETTE = 4;
        const int ARRANGEMENTSIZE = 32 * 32 * 2;
        public MainWindow() {
            InitializeComponent();
            effectnames.Add("None");
            effects.Add(null);
            effectnames.Add("Blur");
            effects.Add(new BlurEffect());
            effectnames.Add("Drop Shadow");
            effects.Add(new DropShadowEffect());
            MenuItem tempMenuItem;
            foreach (String effectname in effectnames) {
                tempMenuItem = new MenuItem();
                tempMenuItem.Header = effectname;
                tempMenuItem.IsCheckable = true;
                if (effectname == "None") {
                    checkedeffect = tempMenuItem;
                    tempMenuItem.IsChecked = true;
                }
                EffectsMenu.Items.Add(tempMenuItem);
            }
            curArr = -1;
        }

        private void FileOpen_Click(object sender, RoutedEventArgs e) {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = "All Supported|*.gba;*.smc;*.fig;*.sfc|Earthbound ROM|*.smc;*.fig;*.sfc|GBA ROM|*.gba|All Files|*.*";
            dlg.InitialDirectory = Properties.Settings.Default.lastOpenPath;
            Nullable<bool> result = dlg.ShowDialog();
            if (result == true) {
                OpenROM(dlg.FileName);
                Properties.Settings.Default.lastOpenPath = dlg.FileName.Substring(0, dlg.FileName.LastIndexOf('\\'));
                Properties.Settings.Default.Save();
            }

        }
        private EBArrangement buildArrangement(int arrangementOffset, int graphicsOffset, int paletteOffset, byte bpp, String name, byte flags) {
            byte[] arrangementData, graphicsData, paletteData;
            if ((flags & ARRANGEMENT) == ARRANGEMENT) {
                arrangementData = romfile.ReadCompressedData(arrangementOffset);
            }
            else {
                romfile.SeekToOffset(arrangementOffset);
                arrangementData = romfile.ReadBytes(ARRANGEMENTSIZE);
            }
            if ((flags & GRAPHICS) == GRAPHICS) {
                graphicsData = romfile.ReadCompressedData(graphicsOffset);
            }
            else {
                romfile.SeekToOffset(paletteOffset);
                graphicsData = romfile.ReadBytes(8 * bpp * 256);
            }
            if ((flags & PALETTE) == PALETTE)
                paletteData = romfile.ReadCompressedData(paletteOffset);
            else {
                romfile.SeekToOffset(paletteOffset);
                paletteData = romfile.ReadBytes((int)Math.Pow(2, bpp + 1));
            }
            return buildArrangement(arrangementData, graphicsData, paletteData, bpp, name);
        }
        private EBArrangement buildArrangement(int arrangementOffset, int graphicsOffset, int paletteOffset, byte bpp, String name) {
            return buildArrangement(arrangementOffset, graphicsOffset, paletteOffset, bpp, name, ARRANGEMENT + GRAPHICS);
        }

        private EBArrangement buildArrangement(byte[] arrangementData, byte[] graphicsData, byte[] paletteData, byte bpp, String name) {
            EBArrangement temp = new EBArrangement(arrangementData, graphicsData, bpp, name, (romfile2 != null));
            temp.SetPalette(paletteData);
            return temp;
        }

        private void ExitItem_Click(object sender, RoutedEventArgs e) {
            this.Close();
        }
        private void FileSave_Click(object sender, RoutedEventArgs e) {
            if (curArr == -1)
                return;
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.InitialDirectory = Properties.Settings.Default.lastSavePath;
            Console.WriteLine(dlg.InitialDirectory);
            dlg.FileName = arrangements[curArr].Name;
            dlg.DefaultExt = ".png";
            dlg.AddExtension = true;
            dlg.Filter = "Portable Network Graphics|*.png|GIF Image|*.gif";
            Nullable<bool> result = dlg.ShowDialog();

            if (result == true) {
                string filename = dlg.FileName;
                Properties.Settings.Default.lastSavePath = dlg.FileName.Substring(0, dlg.FileName.LastIndexOf('\\'));
                Properties.Settings.Default.Save();
                BitmapFrame frame = BitmapFrame.Create((BitmapSource)ArrangementCanvas.Source);

                BitmapEncoder encoder = null;
                if (filename.Substring(filename.Length - 3, 3) == "png") {
                    encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(frame);
                }
                if (filename.Substring(filename.Length - 3, 3) == "gif") {
                    encoder = new GifBitmapEncoder();
                    encoder.Frames.Add(frame);
                }
                FileStream saveFile = new FileStream(filename, FileMode.Create);
                if (encoder != null)
                    encoder.Save(saveFile);
                saveFile.Close();
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e) {
            ArrangementCanvas.Height = e.NewSize.Height - 100;
            ArrangementCanvas.Width = e.NewSize.Width - 50;
            ArrangementCanvas2.Height = e.NewSize.Height - 100;
            ArrangementCanvas2.Width = e.NewSize.Width - 50;
            menu1.Width = e.NewSize.Width;
            ArrangementList.Width = e.NewSize.Width - 50;
        }

        private void ArrangementList_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            curArr = ArrangementList.SelectedIndex;
            if (curArr < 0)
                return;
            UpdateArrangement();
        }

        private void Window_Drop(object sender, DragEventArgs e) {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                string[] droppedFilePaths = e.Data.GetData(DataFormats.FileDrop, true) as string[];
                OpenROM(droppedFilePaths[0]);
            }
        }
        private void OpenROM(string filename) {
            romfile = null;
            romfile2 = null;
            try {
                romfile = new SNESRom(filename);
                if (!romfile.isValid)
                    romfile2 = new GBARom(filename);
            }
            catch (Exception e) {
                //MessageBox.Show(e.Message);
                //return;
            }
            if ((romfile.isValid) && (romfile.GetGameID() == "MB  ") && (romfile.GetGameDest() == SNESRom.America)) {
                arrangements.Clear();
                OpenROM_Earthbound();
            }
            else if ((romfile.isValid) && (romfile.GetGameID() == "MB  ") && (romfile.GetGameDest() == SNESRom.Japan)) {
                arrangements.Clear();
                OpenROM_Mother2();
            }
            else if ((romfile2 != null) && (romfile2.GetGameID() == "A3UJ")) {
                arrangements.Clear();
                OpenROM_Mother3();
            }
            else {
                MessageBox.Show("Not a supported ROM.");
                return;
            }
            ArrangementList.Items.Clear();
            curArr = 0;
            ComboBoxItem tmp;
            foreach (MultilayerArrangement arr in arrangements) {
                tmp = new ComboBoxItem();
                tmp.Content = arr.Name;
                ArrangementList.Items.Add(tmp);
            }
            ArrangementList.SelectedIndex = 0;
            UpdateArrangement();
            ArrangementList.IsEnabled = true;

            romfile.Close();
            romfile.Dispose();
        }
        private void OpenROM_Mother2() {
            OpenROM_SNESCommon();
            arrangements.Add(new MultilayerArrangement(buildArrangement(0x21C692, 0x21C6DF, 0x21C800, 2, "Nintendo", ARRANGEMENT + GRAPHICS + PALETTE)));
            arrangements.Add(new MultilayerArrangement(buildArrangement(0x21C470, 0x21C4DC, 0x21C800, 2, "Itoi", ARRANGEMENT + GRAPHICS + PALETTE)));
            arrangements.Add(new MultilayerArrangement(buildArrangement(0x18F8D6, 0x18FAD4, 0x18F8CE, 2, "Faulty cartridge")));
            arrangements.Add(new MultilayerArrangement(buildArrangement(0x18F05E, 0x18F336, 0x18F8CE, 2, "Piracy is bad")));
            arrangements.Add(new MultilayerArrangement(buildArrangement(0x2148AB, 0x2148EF, 0x2149B8, 2, "Logo 1", ARRANGEMENT + GRAPHICS + PALETTE)));
            arrangements.Add(new MultilayerArrangement(buildArrangement(0x214317, 0x214380, 0x214586, 2, "Logo 2", ARRANGEMENT + GRAPHICS + PALETTE)));
            arrangements.Add(new MultilayerArrangement(buildArrangement(0x2145CA, 0x21463E, 0x21480E, 2, "Logo 3", ARRANGEMENT + GRAPHICS + PALETTE)));
            arrangements.Add(new MultilayerArrangement(buildArrangement(0x2149FC, 0x214F4E, 0x219CB9, 8, "Gas Station", ARRANGEMENT + GRAPHICS + PALETTE)));
            arrangements.Add(new MultilayerArrangement(buildArrangement(0x2149FC, 0x214F4E, 0x219D5F, 8, "Gas Station Alt", ARRANGEMENT + GRAPHICS + PALETTE)));
            //arrangements.Add(buildArrangement(0x21B18C, 0x21A0A0, 0x219CB9, 8, "Title Screen", 0x7));
            byte[] arrangementData;
            byte[] graphicsData, paletteData;
            byte[] decompBuffer;
            for (int i = 0; i < 6; i++) {
                decompBuffer = romfile.ReadCompressedData(romfile.ReadSNESPointer(0x2030E5 + i * 4));
                arrangementData = new byte[ARRANGEMENTSIZE];
                paletteData = new byte[64];
                graphicsData = new byte[decompBuffer.Length - ARRANGEMENTSIZE - 64];
                Array.Copy(decompBuffer, paletteData, 64);
                Array.Copy(decompBuffer, 64, arrangementData, 0, ARRANGEMENTSIZE);
                Array.Copy(decompBuffer, ARRANGEMENTSIZE + 64, graphicsData, 0, graphicsData.Length);

                arrangements.Add(new MultilayerArrangement(buildArrangement(arrangementData, graphicsData, paletteData, 4, "Map " + i)));
            }
        }
        private void OpenROM_Mother3() {
            romfile2.SeekToOffset(0x01D0BC9C);
            ushort numlayers = romfile2.ReadUInt16();
            ushort numarrs = romfile2.ReadUInt16();
            EBArrangement[] layers = new EBArrangement[numlayers];
            for (int i = 0; i < numlayers; i++)
                layers[i] = GetMO3Layer(i);
            MultilayerArrangement temp;
            romfile2.SeekToOffset(0x1D1EFC0);
            ushort layer1, layer2, alpha1, alpha2;
            for (int i = 0; i < numarrs; i++) {
                romfile2.SeekToOffset(0x1D1EFC0 + i * 12);
                layer1 = romfile2.ReadUInt16();
                layer2 = romfile2.ReadUInt16();
                alpha1 = romfile2.ReadUInt16();
                alpha2 = romfile2.ReadUInt16();
                temp = new MultilayerArrangement(layers[layer1], layers[layer2]);
                temp.opacity[0] = alpha1 / 16.0;
                temp.opacity[1] = alpha2 / 16.0;
                arrangements.Add(temp);
            }
        }
        private EBArrangement GetMO3Layer(int id) {
            if ((id == 0) || (id > 546))
                return buildArrangement(new byte[2048], new byte[32], new byte[16], 4, "BG 0");
            ushort gfxid;
            ushort arrid;
            byte[] palette, palette2;
            byte[] gfx, arr;
            int tmploc, datasize;

            romfile2.SeekToOffset(0x1D0BCA0 + id * 0x90);
            gfxid = romfile2.ReadUInt16();
            arrid = romfile2.ReadUInt16();
            palette = romfile2.ReadBytes(32);
            palette2 = romfile2.ReadBytes(32);

            romfile2.SeekToOffset(0x1D1FB30 + arrid * 8);
            tmploc = romfile2.ReadInt32();
            datasize = romfile2.ReadInt16();
            romfile2.SeekToOffset(0x1D1FB28 + tmploc);
            if (datasize != 2048)
                throw new Exception("Wait, what?");
            arr = romfile2.ReadBytes(datasize);

            romfile2.SeekToOffset(0x1D1FB30 + gfxid * 8);
            tmploc = romfile2.ReadInt32();
            datasize = romfile2.ReadInt16();
            romfile2.SeekToOffset(0x1D1FB28 + tmploc);
            gfx = romfile2.ReadBytes(datasize);

            return buildArrangement(arr, gfx, palette, 4, "BG " + id);
        }
        private void printarrangement(byte[] arr) {
           int i = 0;
           foreach (byte b in arr) {
               if ((i++ % 32) == 0)
                   Console.WriteLine();
               Console.Write("{0:X} ", b);
           }
        }
        private void OpenROM_Earthbound() {
            OpenROM_SNESCommon();
            arrangements.Add(new MultilayerArrangement(buildArrangement(0x21AD01, 0x21AD4E, 0x21AE70, 2, "Nintendo")));
            arrangements.Add(new MultilayerArrangement(buildArrangement(0x21AADF, 0x21AB4B, 0x21AE70, 2, "Itoi")));
            arrangements.Add(new MultilayerArrangement(buildArrangement(0x18F3C6, 0x18F5C4, 0x18F3BE, 2, "Faulty cartridge")));
            arrangements.Add(new MultilayerArrangement(buildArrangement(0x18F05E, 0x18F20D, 0x18F3BE, 2, "Piracy is bad")));
            arrangements.Add(new MultilayerArrangement(buildArrangement(0x215455, 0x21549E, 0x21558F, 2, "Logo 1", ARRANGEMENT + GRAPHICS + PALETTE)));
            arrangements.Add(new MultilayerArrangement(buildArrangement(0x214EC1, 0x214F2A, 0x215130, 2, "Logo 2", ARRANGEMENT + GRAPHICS + PALETTE)));
            arrangements.Add(new MultilayerArrangement(buildArrangement(0x215174, 0x2151E8, 0x2153B8, 2, "Logo 3", ARRANGEMENT + GRAPHICS + PALETTE)));
            arrangements.Add(new MultilayerArrangement(buildArrangement(0x2155D3, 0x215B33, 0x21A9B7, 8, "Gas Station", ARRANGEMENT + GRAPHICS + PALETTE)));
            arrangements.Add(new MultilayerArrangement(buildArrangement(0x2155D3, 0x215B33, 0x21AA5D, 8, "Gas Station Alt", ARRANGEMENT + GRAPHICS + PALETTE)));
            arrangements.Add(new MultilayerArrangement(buildArrangement(0x21AF7D, 0x21B211, 0x21CDE1, 8, "Title Screen", ARRANGEMENT + GRAPHICS + PALETTE)));
            byte[] arrangementData;
            byte[] graphicsData, paletteData;
            byte[] decompBuffer;
            for (int i = 0; i < 6; i++) {
                decompBuffer = romfile.ReadCompressedData(romfile.ReadSNESPointer(0x202190 + i * 4));
                arrangementData = new byte[ARRANGEMENTSIZE];
                paletteData = new byte[64];
                graphicsData = new byte[decompBuffer.Length - ARRANGEMENTSIZE - 64];
                Array.Copy(decompBuffer, paletteData, 64);
                Array.Copy(decompBuffer, 64, arrangementData, 0, ARRANGEMENTSIZE);
                Array.Copy(decompBuffer, ARRANGEMENTSIZE + 64, graphicsData, 0, graphicsData.Length);

                arrangements.Add(new MultilayerArrangement(buildArrangement(arrangementData, graphicsData, paletteData, 4, "Map " + i)));
            }
            //int gfxOffset;
            //for (int i = 0; i < 31; i++)
            //{
            //    romfile.seekToOffset(0xCF04D+i*12);
            //    gfxOffset = romfile.ReadUInt16()+0xC0000;
            //    arrangements.Add(buildArrangement(romfile.ReadSNESPointer(0xCF593+i*4), gfxOffset, 0x0CF47F+i*8, 2, "PSI " + i));
            //}
        }

        private void OpenROM_SNESCommon() {
            romfile.SeekToOffset(0x0ADCA1);
            byte[] tableData = romfile.ReadBytes(17 * 327);
            List<EBArrangement> bglayers = new List<EBArrangement>();
            for (int i = 0; i < tableData.Length / 17; i++) {
                bglayers.Add(buildArrangement(
                       romfile.ReadSNESPointer(0xAD93D + tableData[i * 17] * 4),
                       romfile.ReadSNESPointer(0xAD7A1 + tableData[i * 17] * 4),
                       romfile.ReadSNESPointer(0xADAD9 + tableData[i * 17 + 1] * 4),
                       tableData[i * 17 + 2],
                       "BattleBG " + i));
            }
            romfile.SeekToOffset(0x0BD89A);
            for (int i = 0; i < 0x1E3; i++)
                arrangements.Add(new MultilayerArrangement(bglayers[romfile.ReadInt16()], bglayers[romfile.ReadInt16()]));
        }
        private void UpdateArrangement() {
            if (arrangements.Count > 0) {
                try {
                    ArrangementCanvas.Source = arrangements[curArr].GetLayer(0);
                    ArrangementCanvas.Opacity = arrangements[curArr].opacity[0];
                    ArrangementCanvas2.Source = arrangements[curArr].GetLayer(1);
                    ArrangementCanvas2.Opacity = arrangements[curArr].opacity[1];
                }
                catch (Exception e) {
                    Console.WriteLine(e.Message);
                    MessageBox.Show("Bad tile/arrangement data!");
                }
            }
        }
        private void EffectsMenu_Click(object sender, RoutedEventArgs e) {
            int i = 0;
            foreach (String effect in effectnames) {
                if (effect == (string)((MenuItem)e.OriginalSource).Header)
                    break;
                i++;
            }
            if (i >= effectnames.Count)
                return;
            checkedeffect.IsChecked = false;
            checkedeffect = (MenuItem)e.OriginalSource;
            ((MenuItem)e.OriginalSource).IsChecked = true;
            ArrangementCanvas.Effect = effects[i];
            ArrangementCanvas2.Effect = effects[i];
        }
        private void BlackBG_Click(object sender, RoutedEventArgs e) {
            if (((MenuItem)e.OriginalSource).IsChecked)
                BGGrid.Background = new SolidColorBrush(Colors.Black);
            else
                BGGrid.Background = SystemColors.WindowBrush;
        }
    }
}
