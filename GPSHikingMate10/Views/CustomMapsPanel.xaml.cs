using LolloGPS.Data.TileCache;
using LolloGPS.ViewModels;
using System.Threading.Tasks;
using Utilz;
using Utilz.Controlz;
using Utilz.Data;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace LolloGPS.Core
{
    public sealed partial class CustomMapsPanel : OpenableObservableControl
    {
        public MainVM MainVM
        {
            get { return (MainVM)GetValue(MainVMProperty); }
            set { SetValue(MainVMProperty, value); }
        }
        public static readonly DependencyProperty MainVMProperty =
            DependencyProperty.Register("MainVM", typeof(MainVM), typeof(CustomMapsPanel), new PropertyMetadata(null));

        public MapsPanelVM MapsPanelVM
        {
            get { return (MapsPanelVM)GetValue(MapsPanelVMProperty); }
            set { SetValue(MapsPanelVMProperty, value); }
        }
        public static readonly DependencyProperty MapsPanelVMProperty =
            DependencyProperty.Register("MapsPanelVM", typeof(MapsPanelVM), typeof(CustomMapsPanel), new PropertyMetadata(null));

        public CustomMapsPanel()
        {
            InitializeComponent();
        }

        protected override async Task OpenMayOverrideAsync(object args = null)
        {
            await PickCustomTileSourceChooser.OpenAsync().ConfigureAwait(false);
            await ClearCustomCacheChooser.OpenAsync().ConfigureAwait(false);
            await base.OpenMayOverrideAsync().ConfigureAwait(false);
        }

        protected override async Task CloseMayOverrideAsync(object args = null)
        {
            await ClearCustomCacheChooser.CloseAsync().ConfigureAwait(false);
            await base.CloseMayOverrideAsync().ConfigureAwait(false);
        }

        private void OnClearCustomTileSource_Click(object sender, RoutedEventArgs e)
        {
            if (MapsPanelVM?.IsClearCustomCacheEnabled == true) // this is redundant safety
            {
                ClearCustomCacheChooser.IsPopupOpen = true;
            }
            else MainVM?.SetLastMessage_UI("Cache busy");
        }

        private void OnClearCustomCacheChooser_ItemSelected(object sender, TextAndTag e)
        {
            Task sch = MapsPanelVM?.ScheduleClearCacheAsync(e?.Tag as TileSourceRecord, true);
        }

        private void OnTestClicked(object sender, RoutedEventArgs e)
        {
            Task uuu = MapsPanelVM?.StartUserTestingTileSourceAsync();
        }

        private void OnPickFolderClicked(object sender, RoutedEventArgs e)
        {
            Task pick = MainVM?.PickCustomTileFolderAsync();
        }

        private void OnToggleLocalRemote_Click(object sender, RoutedEventArgs e)
        {
            Task toggle = MapsPanelVM?.ToggleIsFileSourceAsync();
        }

        private void OnPickCustomTileSourceChooser_ItemSelected(object sender, TextAndTag e)
        {
            Task pick = MapsPanelVM?.SetModelTileSourceAsync(e?.Tag as TileSourceRecord);
        }

        private void OnAddUriString_Click(object sender, RoutedEventArgs e)
        {
            Task add = MapsPanelVM?.AddEmptyUriStringToTestTileSourceAsync();
        }

        private void OnRemoveUriString_Click(object sender, RoutedEventArgs e)
        {
            Task add = MapsPanelVM?.RemoveUriStringFromTestTileSourceAsync((sender as FrameworkElement)?.DataContext as ObservableString);
        }

        private void OnAddRequestHeader_Click(object sender, RoutedEventArgs e)
        {
            Task add = MapsPanelVM?.AddEmptyRequestHeaderToTestTileSourceAsync();
        }
        private void OnRemoveRequestHeader_Click(object sender, RoutedEventArgs e)
        {
            Task remove = MapsPanelVM?.RemoveRequestHeaderFromTestTileSourceAsync((sender as FrameworkElement)?.DataContext as ObservableKeyAndValue);
        }

        private void OnToggleIsOverlay_Click(object sender, RoutedEventArgs e)
        {
            Task toggle = MapsPanelVM?.ToggleIsOverlayAsync();
        }
    }
}