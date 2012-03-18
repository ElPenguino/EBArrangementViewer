using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Reflection;
using System.IO;

namespace EarthboundArrViewer {

    public class WarpEffect : ShaderEffect {
        private static PixelShader theShader = new PixelShader();

        public WarpEffect(String location) {
            theShader.SetStreamSource(new FileStream(location, FileMode.Open));
            UpdateShaderValue(InputProperty);
        }
        /// <summary>
        /// The explict input for this pixel shader.
        /// </summary>
        public static readonly DependencyProperty InputProperty = ShaderEffect.RegisterPixelShaderSamplerProperty("Input", typeof(WarpEffect), 0);
        /// <summary>
        /// Gets or sets the Input shader sampler.
        /// </summary>
        [System.ComponentModel.BrowsableAttribute(false)]
        public Brush Input {
            get { return (Brush)GetValue(InputProperty); }
            set { SetValue(InputProperty, value); }
        }

    }
}