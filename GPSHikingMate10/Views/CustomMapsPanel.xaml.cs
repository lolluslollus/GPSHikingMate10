using LolloBaseUserControls;
using LolloGPS.Data;
using LolloGPS.Data.Runtime;
using LolloListChooser;
using System;
using System.Threading.Tasks;
using Windows.UI.Xaml;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace LolloGPS.Core
{
    public sealed partial class CustomMapsPanel : OrientationResponsiveUserControl
    {
        public PersistentData PersistentData { get { return App.PersistentData; } }
        public RuntimeData RuntimeData { get { return App.RuntimeData; } }

		public MainVM MainVM
		{
			get { return (MainVM)GetValue(MainVMProperty); }
			set { SetValue(MainVMProperty, value); }
		}
		public static readonly DependencyProperty MainVMProperty =
			DependencyProperty.Register("MainVM", typeof(MainVM), typeof(CustomMapsPanel), new PropertyMetadata(null));

        public CustomMapsPanel()
        {
            InitializeComponent();
            BackPressedRaiser = MainVM;
        }

        private void OnClearCustomTileSource_Click(object sender, RoutedEventArgs e)
        {
            if (MainVM?.IsClearCustomCacheEnabled == true) // this is redundant safety
            {
                ClearCustomCacheChooser.IsPopupOpen = true;
            }
            else PersistentData.LastMessage = "Cache busy";
        }

        private void OnClearCustomCacheChooser_ItemSelected(object sender, TextAndTag e)
        {
            if (e == null || e.Tag == null || e.Tag as TileSourceRecord == null) return;
            // clear cache if requested
            TileSourceRecord tag = (e.Tag as TileSourceRecord);
            if (!tag.IsNone && !tag.IsDefault)
            {
                MainVM?.ScheduleClearCacheAsync(tag, true);
            }
        }

        private void OnTestClicked(object sender, RoutedEventArgs e)
        {
            Task uuu = MainVM.StartUserTestingTileSourceAsync();
        }

        //#region events
        //public event EventHandler Goto2DRequested;
        //private void RaiseGoto2DRequested()
        //{
        //    var listener = Goto2DRequested;
        //    if (listener != null)
        //    {
        //        listener(this, EventArgs.Empty);
        //    }
        //}
        //#endregion events
    }
}
