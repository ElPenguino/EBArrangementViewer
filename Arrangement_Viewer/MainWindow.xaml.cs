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

namespace EarthboundArrViewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private SNESRom romfile;
        private List<EBArrangement> arrangements = new List<EBArrangement>();
        private int curArr;
        //private System.ComponentModel.BackgroundWorker backgroundWorker1;

        public MainWindow()
        {
            InitializeComponent();
            curArr = -1;
            InitializeBackgroundWorker();
        }
        private void InitializeBackgroundWorker()
        {
         /*   backgroundWorker1.DoWork +=
                new DoWorkEventHandler(backgroundWorker1_DoWork);
            backgroundWorker1.RunWorkerCompleted +=
                new RunWorkerCompletedEventHandler(
            backgroundWorker1_RunWorkerCompleted);
            backgroundWorker1.ProgressChanged +=
                new ProgressChangedEventHandler(
            backgroundWorker1_ProgressChanged);*/
        }
        /*private void backgroundWorker1_DoWork(object sender,
            DoWorkEventArgs e)
        {
            // Get the BackgroundWorker that raised this event.
            BackgroundWorker worker = sender as BackgroundWorker;

            // Assign the result of the computation
            // to the Result property of the DoWorkEventArgs
            // object. This is will be available to the 
            // RunWorkerCompleted eventhandler.
            e.Result = openRom((string)e.Argument);
        }*/

        private void FileOpen_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = "Earthbound ROM|*.smc;*.fig;*.sfc|All Files|*.*";
            dlg.InitialDirectory = Properties.Settings.Default.lastOpenPath;
            Nullable<bool> result = dlg.ShowDialog();
            Console.WriteLine(dlg.InitialDirectory);
            if (result == true)
            {
                OpenROM(dlg.FileName);
                Properties.Settings.Default.lastOpenPath = dlg.FileName.Substring(0, dlg.FileName.LastIndexOf('\\'));
                Properties.Settings.Default.Save();
            }

        }
        private EBArrangement buildArrangement(int arrangementOffset, int graphicsOffset, int paletteOffset, byte bpp, String name, byte flags)
        {
            byte[] arrangementData, graphicsData, paletteData;
            if ((flags & 0x1) == 1)
            {
                arrangementData = romfile.readCompressedData(arrangementOffset);
            }
            else
            {
                romfile.seekToOffset(arrangementOffset);
                arrangementData = romfile.ReadBytes(2048);
            }
            if ((flags & 0x2) == 2)
            {
                graphicsData = romfile.readCompressedData(graphicsOffset);
            }
            else
            {
                romfile.seekToOffset(paletteOffset);
                graphicsData = romfile.ReadBytes(8*bpp*256);
            }
            if ((flags & 0x4) == 4)
                paletteData = romfile.readCompressedData(paletteOffset);
            else
            {
                romfile.seekToOffset(paletteOffset);
                paletteData = romfile.ReadBytes((int)Math.Pow(2, bpp + 1));
            }
            return buildArrangement(arrangementData, graphicsData, paletteData, bpp, name);
        }
        private EBArrangement buildArrangement(int arrangementOffset, int graphicsOffset, int paletteOffset, byte bpp, String name)
        {
            return buildArrangement(arrangementOffset, graphicsOffset, paletteOffset, bpp, name, 0x3);
        }

        private EBArrangement buildArrangement(byte[] arrangementData, byte[] graphicsData, byte[] paletteData, byte bpp, String name)
        {
           EBArrangement temp = new EBArrangement(arrangementData, graphicsData, bpp, name);
           temp.setPalette(paletteData);
           return temp;
        }

        private Boolean CheckROMID()
        {
            if (romfile.getGameID() == "MB  ")
                return true;
            return false;
        }

        private void ExitItem_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        private void FileSave_Click(object sender, RoutedEventArgs e)
        {
            if (curArr == -1)
                return;
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.InitialDirectory = Properties.Settings.Default.lastSavePath;
            Console.WriteLine(dlg.InitialDirectory);
            dlg.FileName = arrangements[curArr].name;
            dlg.DefaultExt = ".png";
            dlg.AddExtension = true;
            dlg.Filter = "Portable Network Graphics|*.png|GIF Image|*.gif";
            Nullable<bool> result = dlg.ShowDialog();

            if (result == true)
            {
                string filename = dlg.FileName;
                Properties.Settings.Default.lastSavePath = dlg.FileName.Substring(0, dlg.FileName.LastIndexOf('\\'));
                Properties.Settings.Default.Save();
                BitmapFrame frame = BitmapFrame.Create((BitmapSource)ArrangementCanvas.Source);
                BitmapEncoder encoder = null;
                if (filename.Substring(filename.Length - 3,3) == "png") {
                    encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(frame);
                }
                if (filename.Substring(filename.Length - 3, 3) == "gif")
                {
                    encoder = new GifBitmapEncoder();
                    encoder.Frames.Add(frame);
                }
                FileStream saveFile = new FileStream(filename, FileMode.Create);
                if (encoder != null)
                    encoder.Save(saveFile);
                saveFile.Close();
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ArrangementCanvas.Height = e.NewSize.Height - 100;
            ArrangementCanvas.Width = e.NewSize.Width - 50;
            menu1.Width = e.NewSize.Width;
            ArrangementList.Width = e.NewSize.Width - 50;
        }

        private void ArrangementList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            curArr = ArrangementList.SelectedIndex;
            ArrangementCanvas.Source = arrangements[curArr].getGraphic();
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] droppedFilePaths = e.Data.GetData(DataFormats.FileDrop, true) as string[];
                OpenROM(droppedFilePaths[0]);
            }
        }
        private void OpenROM(string filename)
        {
            try
            {
                romfile = new SNESRom(filename);
            }
            catch (Exception e) 
            {
                MessageBox.Show(e.Message);
            }
            if (!CheckROMID())
            {
                MessageBox.Show("Not an Earthbound ROM.");
                return;
            }

            romfile.seekToOffset(0x0ADCA1);
            byte[] tableData = romfile.ReadBytes(17 * 326);
            for (int i = 0; i < tableData.Length / 17; i++)
            {
                //Console.WriteLine("Arrangement {0}: {1} {2}",i, tableData[i * 17], tableData[i*17+1]);
                arrangements.Add(buildArrangement(
                       romfile.ReadSNESPointer(0xAD93D + tableData[i * 17] * 4),
                       romfile.ReadSNESPointer(0xAD7A1 + tableData[i * 17] * 4),
                       romfile.ReadSNESPointer(0xADAD9 + tableData[i * 17 + 1] * 4),
                       tableData[i * 17 + 2],
                       "BattleBG " + i));
            }
            if (romfile.getGameDest() == SNESRom.America)
            {
                arrangements.Add(buildArrangement(0x21AD01, 0x21AD4E, 0x21AE70, 2, "Nintendo"));
                arrangements.Add(buildArrangement(0x21AADF, 0x21AB4B, 0x21AE70, 2, "Itoi"));
                arrangements.Add(buildArrangement(0x18F3C6, 0x18F5C4, 0x18F3BE, 2, "Faulty cartridge"));
                arrangements.Add(buildArrangement(0x18F05E, 0x18F20D, 0x18F3BE, 2, "Piracy is bad"));
                arrangements.Add(buildArrangement(0x215455, 0x21549E, 0x21558F, 2, "Logo 1", 0x7));
                arrangements.Add(buildArrangement(0x214EC1, 0x214F2A, 0x215130, 2, "Logo 2", 0x7));
                arrangements.Add(buildArrangement(0x215174, 0x2151E8, 0x2153B8, 2, "Logo 3", 0x7));
                arrangements.Add(buildArrangement(0x2155D3, 0x215B33, 0x21A9B7, 8, "Gas Station", 0x7));
                arrangements.Add(buildArrangement(0x2155D3, 0x215B33, 0x21AA5D, 8, "Gas Station Alt", 0x7));
                arrangements.Add(buildArrangement(0x21AF7D, 0x21B211, 0x21CDE1, 8, "Title Screen", 0x7));
                byte[] arrangementData;
                byte[] graphicsData,paletteData;
                byte[] decompBuffer;
                for (int i = 0; i < 6; i++)
                {
                    decompBuffer = romfile.readCompressedData(romfile.ReadSNESPointer(0x202190+i*4));
                    arrangementData = new byte[2048];
                    paletteData = new byte[64];
                    graphicsData = new byte[decompBuffer.Length-2048-64];
                    Array.Copy(decompBuffer, paletteData, 64);
                    Array.Copy(decompBuffer,64, arrangementData,0,2048);
                    Array.Copy(decompBuffer,2048+64, graphicsData,0,graphicsData.Length);
                    
                    arrangements.Add(buildArrangement(arrangementData, graphicsData, paletteData, 4, "Map " + i));
                }
                /*int gfxOffset;
                for (int i = 0; i < 31; i++)
                {
                    romfile.seekToOffset(0xCF04D+i*12);
                    gfxOffset = romfile.ReadUInt16()+0xC0000;
                    arrangements.Add(buildArrangement(romfile.ReadSNESPointer(0xCF593+i*4), gfxOffset, 0x0CF47F+i*8, 2, "PSI " + i));
                }*/
            }
            if (romfile.getGameDest() == SNESRom.Japan)
            {
                //Todo: find offsets in Mother 2 ROM.
            }
            curArr = 0;
            ComboBoxItem tmp;
            foreach (EBArrangement arr in arrangements)
            {
                tmp = new ComboBoxItem();
                tmp.Content = arr.name;
                ArrangementList.Items.Add(tmp);
            }
            ArrangementList.SelectedIndex = 0;
            ArrangementCanvas.Source = arrangements[curArr].getGraphic();
            ArrangementList.IsEnabled = true;

            romfile.Close();
            romfile.Dispose();
        }
    }
}
