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

        public object CheckedContent
        {
            get { return GetValue(CheckedContentProperty); }
            set { SetValue(CheckedContentProperty, value); }
        }
        public static readonly DependencyProperty CheckedContentProperty =
            DependencyProperty.Register("CheckedContent", typeof(object), typeof(ToggleButtonLollo), new PropertyMetadata(null));

        public object UncheckedContent
        {
            get { return GetValue(UncheckedContentProperty); }
            set { SetValue(UncheckedContentProperty, value); }
        }
        public static readonly DependencyProperty UncheckedContentProperty =
            DependencyProperty.Register("UncheckedContent", typeof(object), typeof(ToggleButtonLollo), new PropertyMetadata(null));


        public ToggleButtonLollo() : base()
        {
            Loaded += OnLoaded;
            RegisterPropertyChangedCallback(IsCheckedProperty, OnIsCheckedChanged);
        }
        private void OnIsCheckedChanged(DependencyObject obj, DependencyProperty prop)
        {
            UpdateAfterIsCheckedChanged();
        }
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateAfterIsCheckedChanged();
        }

        private void UpdateAfterIsCheckedChanged()
        {
            bool isChecked = IsChecked == true;
            if (isChecked && CheckedContent != null) Content = CheckedContent;
            else if (!isChecked && UncheckedContent != null) Content = UncheckedContent;
        }
    }
}
