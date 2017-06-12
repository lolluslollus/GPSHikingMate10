using Utilz.Controlz;
using LolloGPS.Data;
using LolloGPS.Data.Runtime;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.UI.Xaml;
using System.Threading.Tasks;
using Utilz;
using GPSHikingMate10.ViewModels;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace LolloGPS.Core
{
    public sealed partial class MapsPanel : OpenableObservableControl
    {
        public PersistentData PersistentData { get { return App.PersistentData; } }
        public RuntimeData RuntimeData { get { return App.RuntimeData; } }

        public MainVM MainVM
        {
            get { return (MainVM)GetValue(MainVMProperty); }
            set { SetValue(MainVMProperty, value); }
        }
        public static readonly DependencyProperty MainVMProperty =
            DependencyProperty.Register("MainVM", typeof(MainVM), typeof(MapsPanel), new PropertyMetadata(null));

        public LolloMapVM LolloMapVM
        {
            get { return (LolloMapVM)GetValue(LolloMapVMProperty); }
            set { SetValue(LolloMapVMProperty, value); }
        }
        public static readonly DependencyProperty LolloMapVMProperty =
            DependencyProperty.Register("LolloMapVM", typeof(LolloMapVM), typeof(MapsPanel), new PropertyMetadata(null));

        public MapsVM MapsVM
        {
            get { return (MapsVM)GetValue(MapsVMProperty); }
            set { SetValue(MapsVMProperty, value); }
        }
        public static readonly DependencyProperty MapsVMProperty =
            DependencyProperty.Register("MapsVM", typeof(MapsVM), typeof(MapsPanel), new PropertyMetadata(null));

        public MapsPanel()
        {
            InitializeComponent();
        }

        protected override async Task OpenMayOverrideAsync(object args = null)
        {
            await MapSourceChooser.OpenAsync().ConfigureAwait(false);
            await ZoomLevelChooser.OpenAsync().ConfigureAwait(false);
            await ClearCacheChooser.OpenAsync().ConfigureAwait(false);
            await base.OpenMayOverrideAsync().ConfigureAwait(false);
        }

        protected override async Task CloseMayOverrideAsync(object args = null)
        {
            await MapSourceChooser.CloseAsync().ConfigureAwait(false);
            await ZoomLevelChooser.CloseAsync().ConfigureAwait(false);
            await ClearCacheChooser.CloseAsync().ConfigureAwait(false);
            await base.CloseMayOverrideAsync().ConfigureAwait(false);
        }

        private void OnGoto2D_Click(object sender, RoutedEventArgs e)
        {
            Task gt = MainVM?.Goto2DAsync();
        }
        private void OnMapStyleButton_Click(object sender, RoutedEventArgs e)
        {
            PersistentData.CycleMapStyle();
        }

        private void OnClearMapCache_Click(object sender, RoutedEventArgs e)
        {
            if (MapsVM?.IsClearCacheEnabled == true) // this is redundant safety
            {
                ClearCacheChooser.IsPopupOpen = true;
            }
            else PersistentData.LastMessage = "Cache busy";
        }
        private void OnClearCacheChooser_ItemSelected(object sender, TextAndTag e)
        {
            Task sch = MapsVM?.ScheduleClearCacheAsync(e?.Tag as TileSourceRecord, false);
        }

        private void OnDownloadMap_Click(object sender, RoutedEventArgs e)
        {
            MapsVM?.DownloadMapAsync();
        }
        private void OnZoomLevelChooser_ItemSelected(object sender, TextAndTag e)
        {
            if (!(e?.Tag is int)) return;
            int maxZoom = (int)(e.Tag);
            MapsVM?.Download_ChooseZoomLevel(maxZoom);
        }
        private void OnMapSourceChooser_ItemSelected(object sender, TextAndTag e)
        {
            Task set = MapsVM?.SetMapSource(e?.Tag as TileSourceRecord);
        }
    }
}