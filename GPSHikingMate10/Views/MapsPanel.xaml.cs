using LolloGPS.Controlz;
using LolloGPS.Data;
using LolloGPS.Data.Runtime;
using LolloGPS.ViewModels;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.UI.Xaml;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace LolloGPS.Core
{
    public sealed partial class MapsPanel : Utilz.Controlz.OpenableObservableControl
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
        public MapsPanelVM MapsPanelVM
        {
            get { return (MapsPanelVM)GetValue(MapsPanelVMProperty); }
            set { SetValue(MapsPanelVMProperty, value); }
        }
        public static readonly DependencyProperty MapsPanelVMProperty =
            DependencyProperty.Register("MapsPanelVM", typeof(MapsPanelVM), typeof(MapsPanel), new PropertyMetadata(null));

        public MapsPanel()
        {
            InitializeComponent();
        }

        protected override async Task OpenMayOverrideAsync(object args = null)
        {
            await BaseMapSourceChooser.OpenAsync(args).ConfigureAwait(false);
            await OverlayMapSourceChooser.OpenAsync(args).ConfigureAwait(false);
            await ZoomLevelChooser.OpenAsync(args).ConfigureAwait(false);
            await ClearCacheChooser.OpenAsync(args).ConfigureAwait(false);
            await base.OpenMayOverrideAsync(args).ConfigureAwait(false);
        }

        protected override async Task CloseMayOverrideAsync(object args = null)
        {
            await BaseMapSourceChooser.CloseAsync(args).ConfigureAwait(false);
            await OverlayMapSourceChooser.CloseAsync(args).ConfigureAwait(false);
            await ZoomLevelChooser.CloseAsync(args).ConfigureAwait(false);
            await ClearCacheChooser.CloseAsync(args).ConfigureAwait(false);
            await base.CloseMayOverrideAsync(args).ConfigureAwait(false);
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
            if (MapsPanelVM?.IsClearCacheEnabled == true) // this is redundant safety
            {
                ClearCacheChooser.IsPopupOpen = true;
            }
            else PersistentData.LastMessage = "Cache busy";
        }
        private void OnClearCacheChooser_ItemSelected(object sender, TextAndTag e)
        {
            Task sch = MapsPanelVM?.ScheduleClearCacheAsync(e?.Tag as TileSourceRecord, false);
        }

        private void OnDownloadMap_Click(object sender, RoutedEventArgs e)
        {
            MapsPanelVM?.ShowDownloadZooms();
        }
        private void OnZoomLevelChooser_ItemSelected(object sender, TextAndTag e)
        {
            if (!(e?.Tag is int)) return;
            int maxZoom = (int)(e.Tag);
            MapsPanelVM?.ChooseDownloadZoomLevelAsync(maxZoom);
        }

        private void OnBaseMapSourceChooser_ItemDeselected(object sender, TextAndTag args)
        {
            Debugger.Break(); // this should not happen
            if (args == null) return;
            Task set = MapsPanelVM?.RemoveMapSource(args?.Tag as TileSourceRecord);
        }
        private void OnBaseMapSourceChooser_ItemSelected(object sender, TextAndTag args)
        {
            if (args == null) return;
            Task set = MapsPanelVM?.AddMapSource(args?.Tag as TileSourceRecord);
        }

        private void OnOverlayMapSourceChooser_ItemDeselected(object sender, TextAndTag args)
        {
            if (args == null) return;
            Task set = MapsPanelVM?.RemoveMapSource(args?.Tag as TileSourceRecord);
        }
        private void OnOverlayMapSourceChooser_ItemSelected(object sender, TextAndTag args)
        {
            if (args == null) return;
            Task set = MapsPanelVM?.AddMapSource(args?.Tag as TileSourceRecord);
        }
    }
}