using LolloGPS.Converters;
using LolloGPS.Core;
using LolloGPS.Data;
using LolloGPS.Data.Runtime;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

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
}