using LolloBaseUserControls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace LolloGPS.Core
{
	public sealed partial class SettingsPanel : ObservableControl
	{
		public MainVM VM
		{
			get { return (MainVM)GetValue(VMProperty); }
			set { SetValue(VMProperty, value); }
		}
		public static readonly DependencyProperty VMProperty =
			DependencyProperty.Register("VM", typeof(MainVM), typeof(SettingsPanel), new PropertyMetadata(null, OnVMChanged));
		private static void OnVMChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
		{
			if (args.NewValue != args.OldValue) (obj as SettingsPanel).UpdateDataContext();
		}


		public SettingsPanel()
		{
			InitializeComponent();
			UpdateDataContext();
		}
		private void UpdateDataContext()
		{
			Task upd = RunInUiThreadAsync(delegate
			{
				LayoutRoot.DataContext = VM; // LOLLO NOTE never set DataContent on self in a UserControl
			});
		}
	}
}
