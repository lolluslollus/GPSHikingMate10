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
            get { return (object)GetValue(CheckedContentProperty); }
            set { SetValue(CheckedContentProperty, value); }
        }
        public static readonly DependencyProperty CheckedContentProperty =
            DependencyProperty.Register("CheckedContent", typeof(object), typeof(ToggleButtonLollo), new PropertyMetadata(null));

        public object UncheckedContent
        {
            get { return (object)GetValue(UncheckedContentProperty); }
            set { SetValue(UncheckedContentProperty, value); }
        }
        public static readonly DependencyProperty UncheckedContentProperty =
            DependencyProperty.Register("UncheckedContent", typeof(object), typeof(ToggleButtonLollo), new PropertyMetadata(null));
        // LOLLO TODO this does not work with two-way binding: investigate
        public new bool IsChecked
        {
            get { return (bool)GetValue(IsCheckedProperty); }
            set { SetValue(IsCheckedProperty, value); }
        }
        public new static readonly DependencyProperty IsCheckedProperty =
            DependencyProperty.Register("IsChecked", typeof(bool), typeof(ToggleButtonLollo), new PropertyMetadata(false, OnIsChecked_PropertyChanged));
        private static void OnIsChecked_PropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            (obj as ToggleButtonLollo)?.UpdateAfterIsCheckedChanged();
        }

        public ToggleButtonLollo() : base()
        {
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateAfterIsCheckedChanged();
        }

        private void UpdateAfterIsCheckedChanged()
        {
            bool isChecked = IsChecked;
            if (isChecked && CheckedContent != null) Content = CheckedContent;
            else if (!isChecked && UncheckedContent != null) Content = UncheckedContent;
            base.IsChecked = isChecked;
        }
    }
}
