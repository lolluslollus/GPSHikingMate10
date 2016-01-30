using LolloGPS.Data;
using LolloGPS.Data.Runtime;
using Utilz.Controlz;
using Windows.UI.Xaml;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace LolloGPS.Core
{
	public sealed partial class HelpPanel : OpObsOrControl
	{
        public PersistentData PersistentData { get { return App.PersistentData; } }
        public RuntimeData RuntimeData { get { return App.RuntimeData; } }

		public MainVM MainVM
		{
			get { return (MainVM)GetValue(MainVMProperty); }
			set { SetValue(MainVMProperty, value); }
		}
		public static readonly DependencyProperty MainVMProperty =
			DependencyProperty.Register("MainVM", typeof(MainVM), typeof(HelpPanel), new PropertyMetadata(null, OnMainVM_Changed));
		private static void OnMainVM_Changed(DependencyObject obj, DependencyPropertyChangedEventArgs args)
		{
			var instance = obj as OpObsOrControl;
			instance.BackPressedRaiser = args.NewValue as IBackPressedRaiser;
		}

		public HelpPanel()
        {
            InitializeComponent();
            // BackPressedRaiser = MainVM;
        }

        public string CheckpointsText { get { return string.Format("Checkpoints are marked this way. Checkpoints are like routes, except the points are not arranged in a sequence, and you can save {0} of them at most.", PersistentData.MaxRecordsInCheckpoints); } }
    }
}
