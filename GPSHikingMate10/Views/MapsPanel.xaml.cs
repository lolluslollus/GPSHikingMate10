using Utilz.Controlz;
using LolloGPS.Data;
using LolloGPS.Data.Runtime;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Windows.UI.Xaml;
using System.Threading.Tasks;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace LolloGPS.Core
{
    public sealed partial class MapsPanel : OpObsOrControl
	{
        public PersistentData PersistentData { get { return App.PersistentData; } }
        public RuntimeData RuntimeData { get { return App.RuntimeData; } }

		public MainVM MainVM
		{
			get { return (MainVM)GetValue(MainVMProperty); }
			set { SetValue(MainVMProperty, value); }
		}
		public static readonly DependencyProperty MainVMProperty =
			DependencyProperty.Register("MainVM", typeof(MainVM), typeof(MapsPanel), new PropertyMetadata(null, OnMainVM_Changed));
		private static void OnMainVM_Changed(DependencyObject obj, DependencyPropertyChangedEventArgs args)
		{
			var instance = obj as OpObsOrControl;
			instance.BackPressedRaiser = args.NewValue as IBackPressedRaiser;
		}


		public MapsPanel()
        {
            InitializeComponent();
        }

		protected override async Task OpenMayOverrideAsync()
		{
			await MapSourceChooser.OpenAsync().ConfigureAwait(false);
			await ZoomLevelChooser.OpenAsync().ConfigureAwait(false);
			await ClearCacheChooser.OpenAsync().ConfigureAwait(false);
			await base.OpenMayOverrideAsync().ConfigureAwait(false);
		}

		protected override async Task CloseMayOverrideAsync()
		{
			await MapSourceChooser.CloseAsync().ConfigureAwait(false);
			await ZoomLevelChooser.CloseAsync().ConfigureAwait(false);
			await ClearCacheChooser.CloseAsync().ConfigureAwait(false);
			await base.CloseMayOverrideAsync().ConfigureAwait(false);
		}

		private void OnGoto2D_Click(object sender, RoutedEventArgs e)
        {
            RaiseGoto2DRequested();
        }
        private void OnMapStyleButton_Click(object sender, RoutedEventArgs e)
        {
            PersistentData.CycleMapStyle();
        }

        private void OnClearMapCache_Click(object sender, RoutedEventArgs e)
        {
            if (MainVM.IsClearCacheEnabled) // this is redundant safety
            {
                ClearCacheChooser.IsPopupOpen = true;
            }
            else PersistentData.LastMessage = "Cache busy";
        }
        private void OnClearCacheChooser_ItemSelected(object sender, TextAndTag e)
        {
            if (e == null || e.Tag == null || e.Tag as TileSourceRecord == null) return;
            // clear cache if requested
            TileSourceRecord tag = (e.Tag as TileSourceRecord);
            if (!tag.IsNone && !tag.IsDefault)
            {
                MainVM?.ScheduleClearCacheAsync(tag, false);
            }
        }

        private async void OnDownloadMap_Click(object sender, RoutedEventArgs e)
        {
            if (MainVM.IsLeechingEnabled) // this is redundant safety
            {
                // present a choice of zoom levels
                List<Tuple<int, int>> howManyTiles4DifferentZooms = await MainVM.GetHowManyTiles4DifferentZoomsAsync().ConfigureAwait(true);

                Collection<TextAndTag> tts = new Collection<TextAndTag>();
                foreach (var item in howManyTiles4DifferentZooms)
                {
                    if (item.Item2 <= ConstantData.MAX_TILES_TO_LEECH && item.Item1 > 0 && item.Item2 > 0)
                    {
                        string message = "Zoom  " + item.Item1 + " gets up to " + item.Item2 + " tiles";
                        tts.Add(new TextAndTag(message, item.Item1));
                    }

                }
                if (tts.Count > 0)
                {
                    ZoomLevelChooser.ItemsSource = tts;
                    ZoomLevelChooser.IsPopupOpen = true;
                }
                else
                {
                    MainVM.SetLastMessage_UI("No downloads possible for this area");
                }
            }
            else PersistentData.LastMessage = "Download busy";
        }
        private void OnZoomLevelChooser_ItemSelected(object sender, TextAndTag e)
        {
            if (e == null || e.Tag == null || !(e.Tag is int)) return;
            int maxZoom = (int)(e.Tag);
            PersistentData.SetIsTilesDownloadDesired(true, maxZoom);
            PersistentData.IsShowingPivot = false;
        }

        #region events
        public event EventHandler Goto2DRequested;
        private void RaiseGoto2DRequested()
        {
            var listener = Goto2DRequested;
            if (listener != null)
            {
                listener(this, EventArgs.Empty);
            }
        }
        #endregion events
    }
}
