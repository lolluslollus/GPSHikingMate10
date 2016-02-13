using Utilz.Controlz;
using System.Threading.Tasks;
using Windows.UI.Xaml;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace LolloGPS.Core
{
	public sealed partial class SettingsPanel : ObservableControl
	{
		public MainVM MainVM
		{
			get { return (MainVM)GetValue(MainVMProperty); }
			set { SetValue(MainVMProperty, value); }
		}
		public static readonly DependencyProperty MainVMProperty =
			DependencyProperty.Register("MainVM", typeof(MainVM), typeof(SettingsPanel), new PropertyMetadata(null/*, OnVMChanged*/));
		//private static void OnVMChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
		//{
		//	if (args.NewValue != args.OldValue) (obj as SettingsPanel).UpdateDataContext();
		//}


		public SettingsPanel()
		{
			InitializeComponent();
			//UpdateDataContext();
		}
		//private void UpdateDataContext()
		//{
		//	Task upd = RunInUiThreadAsync(delegate
		//	{
		//		LayoutRoot.DataContext = MainVM; // LOLLO NOTE never set DataContent on self in a UserControl
		//	});
		//}
	}
}