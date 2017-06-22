using LolloGPS.Converters;
using LolloGPS.Core;
using LolloGPS.Data;
using LolloGPS.Data.Runtime;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace LolloGPS.Controlz
{
    public sealed partial class CheckpointStyleSelector : Utilz.Controlz.ObservableControl
    {
        #region properties
        public PersistentData PersistentData { get { return App.PersistentData; } }

        public MainVM MainVM
        {
            get { return (MainVM)GetValue(MainVMProperty); }
            set { SetValue(MainVMProperty, value); }
        }
        public static readonly DependencyProperty MainVMProperty =
            DependencyProperty.Register("MainVM", typeof(MainVM), typeof(PointsPanel), new PropertyMetadata(null));

        public Brush AlternativeForeground
        {
            get { return (Brush)GetValue(AlternativeForegroundProperty); }
            set { SetValue(AlternativeForegroundProperty, value); }
        }
        public static readonly DependencyProperty AlternativeForegroundProperty =
            DependencyProperty.Register("AlternativeForeground", typeof(Brush), typeof(CheckpointStyleSelector), new PropertyMetadata(new SolidColorBrush(Colors.Gray)));

        public PointRecord Checkpoint
        {
            get { return (PointRecord)GetValue(CheckpointProperty); }
            set { SetValue(CheckpointProperty, value); }
        }
        public static readonly DependencyProperty CheckpointProperty =
            DependencyProperty.Register("Checkpoint", typeof(PointRecord), typeof(CheckpointStyleSelector), new PropertyMetadata(new PointRecord()));
        #endregion properties

        #region events
        public event EventHandler<string> SymbolChanged;
        #endregion events

        #region lifecycle
        public CheckpointStyleSelector()
        {
            InitializeComponent();
        }
        #endregion lifecycle


        #region event handlers
        private void OnSymbolCircle_Click(object sender, RoutedEventArgs e)
        {
            Checkpoint.Symbol = PersistentData.CheckpointSymbols.Circle;
            SymbolChanged?.Invoke(this, PersistentData.CheckpointSymbols.Circle);
        }

        private void OnSymbolCross_Click(object sender, RoutedEventArgs e)
        {
            Checkpoint.Symbol = PersistentData.CheckpointSymbols.Cross;
            SymbolChanged?.Invoke(this, PersistentData.CheckpointSymbols.Cross);
        }

        private void OnSymbolEcs_Click(object sender, RoutedEventArgs e)
        {
            Checkpoint.Symbol = PersistentData.CheckpointSymbols.Ecs;
            SymbolChanged?.Invoke(this, PersistentData.CheckpointSymbols.Ecs);
        }

        private void OnSymbolSquare_Click(object sender, RoutedEventArgs e)
        {
            Checkpoint.Symbol = PersistentData.CheckpointSymbols.Square;
            SymbolChanged?.Invoke(this, PersistentData.CheckpointSymbols.Square);
        }

        private void OnSymbolTriangle_Click(object sender, RoutedEventArgs e)
        {
            Checkpoint.Symbol = PersistentData.CheckpointSymbols.Triangle;
            SymbolChanged?.Invoke(this, PersistentData.CheckpointSymbols.Triangle);
        }
        #endregion event handlers
    }

    public class CheckpointSymbolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return false;
            var sym = value.ToString();
            if (string.IsNullOrWhiteSpace(sym)) sym = PersistentData.CheckpointSymbols.Circle;
            var shouldBeForTrue = parameter.ToString();
            return sym.Equals(shouldBeForTrue);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new Exception("this is a one-way binding, it should never come here");
        }
    }
}