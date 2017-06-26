using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace LolloGPS.Controlz
{
    public sealed partial class LolloProgressBar : UserControl
    {
        public double Value
        {
            get { return (double)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(double), typeof(LolloProgressBar), new PropertyMetadata(0.0));

        public bool IsProgressHindered
        {
            get { return (bool)GetValue(IsProgressHinderedProperty); }
            set { SetValue(IsProgressHinderedProperty, value); }
        }
        public static readonly DependencyProperty IsProgressHinderedProperty =
            DependencyProperty.Register("IsProgressHindered", typeof(bool), typeof(LolloProgressBar), new PropertyMetadata(false));

        public string ProgressHinderedNotice
        {
            get { return (string)GetValue(ProgressHinderedNoticeProperty); }
            set { SetValue(ProgressHinderedNoticeProperty, value); }
        }
        public static readonly DependencyProperty ProgressHinderedNoticeProperty =
            DependencyProperty.Register("ProgressHinderedNotice", typeof(string), typeof(LolloProgressBar), new PropertyMetadata(string.Empty));

        public string ProgressUnhinderedNotice
        {
            get { return (string)GetValue(ProgressUnhinderedNoticeProperty); }
            set { SetValue(ProgressUnhinderedNoticeProperty, value); }
        }
        public static readonly DependencyProperty ProgressUnhinderedNoticeProperty =
            DependencyProperty.Register("ProgressUnhinderedNotice", typeof(string), typeof(LolloProgressBar), new PropertyMetadata(string.Empty));

        public event EventHandler Cancel;
        public LolloProgressBar()
        {
            this.InitializeComponent();
        }

        private void OnCancel_Click(object sender, RoutedEventArgs e)
        {
            Cancel?.Invoke(this, EventArgs.Empty);
        }
    }
}
