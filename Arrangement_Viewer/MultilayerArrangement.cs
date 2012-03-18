using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media.Imaging;

namespace EarthboundArrViewer {
    class MultilayerArrangement {
        private EBArrangement[] layers;
        private int numlayers;
        public String Name;
        public double[] opacity;
        public MultilayerArrangement(EBArrangement layer1, EBArrangement layer2) {
            numlayers = 2;
            layers = new EBArrangement[numlayers];
            opacity = new double[numlayers];
            layers[0] = layer1;
            layers[1] = layer2;
            opacity[0] = 1;
            opacity[1] = 1;
            this.Name = layer1.Name + " + " + layer2.Name;
        }
        public MultilayerArrangement(EBArrangement layer1) {
            numlayers = 1;
            layers = new EBArrangement[numlayers];
            layers[0] = layer1;
            this.Name = layer1.Name;
        }
        public BitmapSource GetLayer(int id) {
            if (id >= numlayers)
                return null;
            return layers[id].getGraphic();
            //return layers[id].getTileDump();
        }
    }
}
