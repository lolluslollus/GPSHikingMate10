using Utilz.Controlz;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

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
			DependencyProperty.Register("MainVM", typeof(MainVM), typeof(SettingsPanel), new PropertyMetadata(null));


		public SettingsPanel()
		{
			InitializeComponent();
		}
	}
}