using LolloBaseUserControls;
using LolloGPS.Data;
using LolloGPS.Data.Files;
using LolloGPS.Data.Runtime;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace LolloGPS.Core
{
    public sealed partial class HelpPanel : OrientationResponsiveUserControl
    {
        public PersistentData MyPersistentData { get { return App.PersistentData; } }
        public RuntimeData MyRuntimeData { get { return App.MyRuntimeData; } }

		public MainVM MyVM
		{
			get { return (MainVM)GetValue(MyVMProperty); }
			set { SetValue(MyVMProperty, value); }
		}
		public static readonly DependencyProperty MyVMProperty =
			DependencyProperty.Register("MyVM", typeof(MainVM), typeof(HelpPanel), new PropertyMetadata(null));

		public HelpPanel()
        {
            InitializeComponent();
            BackPressedRaiser = MyVM;
        }

        public string LandmarksText { get { return string.Format("Landmarks are marked this way. Landmarks are like routes, except the points are not arranged in a sequence, and you can save {0} of them at most.", PersistentData.MaxRecordsInLandmarks); } }
    }
}
