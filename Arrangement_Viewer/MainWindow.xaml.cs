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
using System.Windows.Threading;

namespace EarthboundArrViewer {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        private ROMFile romfile = null;
        private MultilayerArrangement[] arrangements;
        private int curArr = -1;
        private WarpEffect battlebgshader1;
        private WarpEffect battlebgshader2;
        public MainWindow() {
            InitializeComponent();
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
                RenderTargetBitmap bitmap = new RenderTargetBitmap(Convert.ToInt32(ArrangementCanvas.Width), Convert.ToInt32(ArrangementCanvas.Height), 72, 72, PixelFormats.Pbgra32);
                bitmap.Render(ArrangementCanvas2);
                bitmap.Render(ArrangementCanvas);
                BitmapFrame frame = BitmapFrame.Create((BitmapSource)bitmap);

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
        private void Window_Loaded(object sender, RoutedEventArgs e) {
            DispatcherTimer dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(50000);
            dispatcherTimer.Start();
            battlebgshader1 = new WarpEffect();
            battlebgshader2 = new WarpEffect();
            ArrangementCanvas.Effect = battlebgshader1;
            ArrangementCanvas2.Effect = battlebgshader2;
        }
        private void Anim_Click(object sender, RoutedEventArgs e) {
            if (((MenuItem)sender).IsChecked) {
                ArrangementCanvas.Effect = battlebgshader1;
                ArrangementCanvas2.Effect = battlebgshader2;
            }
            else {
                ArrangementCanvas.Effect = null;
                ArrangementCanvas2.Effect = null;
            }

        }
        private void dispatcherTimer_Tick(object sender, EventArgs e) {
            battlebgshader1.Timer += 1;
            battlebgshader2.Timer -= 1;
        }
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e) {
            double newsize = 0;
            if (e.NewSize.Height - 100 > e.NewSize.Width - 50)
                newsize = e.NewSize.Width - 50;
            else
                newsize = e.NewSize.Height - 100;
            ArrangementCanvas.Height = newsize;
            ArrangementCanvas.Width = newsize;
            ArrangementCanvas2.Height = newsize;
            ArrangementCanvas2.Width = newsize;
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
            try {
                romfile = new EBRom(filename);
                if (!romfile.isSupported())
                    romfile = new MO3Rom(filename);
            }
            catch {
                MessageBox.Show("Not a supported ROM.");
                return;
            }
            if (!romfile.isSupported()) {
                MessageBox.Show("Not a supported ROM.");
                return;
            }
            ArrangementList.Items.Clear();
            curArr = 0;
            ComboBoxItem tmp;
            arrangements = romfile.getArrangements();
            foreach (MultilayerArrangement arr in arrangements) {
                tmp = new ComboBoxItem();
                tmp.Content = arr.Name;
                ArrangementList.Items.Add(tmp);
            }
            ArrangementList.SelectedIndex = 0;
            UpdateArrangement();
            ArrangementList.IsEnabled = true;
        }
        private void UpdateArrangement() {
            if (arrangements.Length > 0) {
                try {
                    ArrangementCanvas.Source = arrangements[curArr].GetBitmap(0);
                    ArrangementCanvas.Opacity = arrangements[curArr].opacity[0];
                    ArrangementCanvas2.Source = arrangements[curArr].GetBitmap(1);

                    if (arrangements[curArr].numlayers == 2)
                        ArrangementCanvas2.Opacity = arrangements[curArr].opacity[1];
                    else
                        ArrangementCanvas2.Opacity = 1;
                }
                catch (Exception e) {
                    Console.WriteLine(e.Message);
                    MessageBox.Show("Bad tile/arrangement data!");
                }
                if (arrangements[curArr].numlayers > 0)
                {
                    battlebgshader1.Hdrift = arrangements[curArr].GetLayer(0).hdrift;
                    battlebgshader1.Hamplitude = arrangements[curArr].GetLayer(0).hamplitude;
                    battlebgshader1.Hfrequency = arrangements[curArr].GetLayer(0).hfrequency;
                    battlebgshader1.Hperiod = arrangements[curArr].GetLayer(0).hperiod;
                    battlebgshader1.Vdrift = arrangements[curArr].GetLayer(0).vdrift;
                    battlebgshader1.Vamplitude = arrangements[curArr].GetLayer(0).vamplitude;
                    battlebgshader1.Vfrequency = arrangements[curArr].GetLayer(0).vfrequency;
                    battlebgshader1.Vperiod = arrangements[curArr].GetLayer(0).vperiod;
                    if (arrangements[curArr].numlayers > 1)
                    {
                        battlebgshader2.Hdrift = arrangements[curArr].GetLayer(1).hdrift;
                        battlebgshader2.Hamplitude = arrangements[curArr].GetLayer(1).hamplitude;
                        battlebgshader2.Hfrequency = arrangements[curArr].GetLayer(1).hfrequency;
                        battlebgshader2.Hperiod = arrangements[curArr].GetLayer(1).hperiod;
                        battlebgshader2.Vdrift = arrangements[curArr].GetLayer(1).vdrift;
                        battlebgshader2.Vamplitude = arrangements[curArr].GetLayer(1).vamplitude;
                        battlebgshader2.Vfrequency = arrangements[curArr].GetLayer(1).vfrequency;
                        battlebgshader2.Vperiod = arrangements[curArr].GetLayer(1).vperiod;
                    }
                }
            }
        }
        private void BlackBG_Click(object sender, RoutedEventArgs e) {
            if (((MenuItem)e.OriginalSource).IsChecked)
                BGGrid.Background = new SolidColorBrush(Colors.Black);
            else
                BGGrid.Background = SystemColors.WindowBrush;
        }
    }
}
