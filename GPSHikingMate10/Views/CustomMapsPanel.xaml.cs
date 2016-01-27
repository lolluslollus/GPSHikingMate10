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
        public PersistentData MyPersistentData { get { return App.PersistentData; } }
        public RuntimeData MyRuntimeData { get { return App.MyRuntimeData; } }

        private Main_VM _myVM = Main_VM.GetInstance();
        public Main_VM MyVM { get { return _myVM; } }

        public CustomMapsPanel()
        {
            InitializeComponent();
            BackPressedRaiser = MyVM;
        }

        private void OnClearCustomTileSource_Click(object sender, RoutedEventArgs e)
        {
            if (_myVM.IsClearCustomCacheEnabled) // this is redundant safety
            {
                ClearCustomCacheChooser.IsPopupOpen = true;
            }
            else MyPersistentData.LastMessage = "Cache busy";
        }

        private void OnClearCustomCacheChooser_ItemSelected(object sender, TextAndTag e)
        {
            if (e == null || e.Tag == null || e.Tag as TileSourceRecord == null) return;
            // clear cache if requested
            TileSourceRecord tag = (e.Tag as TileSourceRecord);
            if (!tag.IsNone && !tag.IsDefault)
            {
                _myVM?.ScheduleClearCacheAsync(tag, true);
            }
        }

        private void OnTestClicked(object sender, RoutedEventArgs e)
        {
            Task uuu = MyVM.StartUserTestingTileSourceAsync();
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
