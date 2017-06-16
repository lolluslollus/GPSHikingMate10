using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;

namespace LolloGPS.Controlz
{
    public class ToggleButtonLollo : ToggleButton
    {
        public Brush AlternativeForeground
        {
            get { return (Brush)GetValue(AlternativeForegroundProperty); }
            set { SetValue(AlternativeForegroundProperty, value); }
        }
        public static readonly DependencyProperty AlternativeForegroundProperty =
            DependencyProperty.Register("AlternativeForeground", typeof(Brush), typeof(ToggleButtonLollo), new PropertyMetadata(new SolidColorBrush(Colors.Gray)));
    }
}
