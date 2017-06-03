using Utilz.Controlz;
using LolloGPS.Data;
using LolloGPS.Data.Runtime;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Core;


// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace LolloGPS.Core
{
	public sealed partial class CustomMapsPanel : OpenableObservableControl
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
		}

		protected override async Task OpenMayOverrideAsync(object args = null)
		{
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
			if (MainVM?.IsClearCustomCacheEnabled == true) // this is redundant safety
			{
				ClearCustomCacheChooser.IsPopupOpen = true;
			}
			else PersistentData.LastMessage = "Cache busy";
		}

		private void OnClearCustomCacheChooser_ItemSelected(object sender, TextAndTag e)
		{
			Task sch = MainVM?.ScheduleClearCacheAsync(e?.Tag as TileSourceRecord, true);
		}

		private void OnTestClicked(object sender, RoutedEventArgs e)
		{
			Task uuu = MainVM?.StartUserTestingTileSourceAsync();
		}
	}
}