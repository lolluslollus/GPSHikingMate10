using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Windows.Devices.Sensors;
using Windows.Foundation;
using Windows.Phone.UI.Input;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// LOLLO guide to uap https://msdn.microsoft.com/en-us/library/windows/apps/xaml/dn894631.aspx
namespace LolloBaseUserControls
{
    public class OrientationResponsiveUserControl : ObservableControl, INotifyPropertyChanged
    {
        #region ctor
        public OrientationResponsiveUserControl()
            : base()
        {
            _appView = ApplicationView.GetForCurrentView();
            //_orientationSensor = SimpleOrientationSensor.GetDefault();
            //if (_orientationSensor != null) { _lastOrientation = _orientationSensor.GetCurrentOrientation(); }
            UseLayoutRounding = true;
            Loaded += Open;
            Unloaded += Close;
        }
		//~OrientationResponsiveUserControl() // this fucks up
		//{
		//    try
		//    {
		//        this.Loaded -= OnLoadedInternal;
		//        this.Unloaded -= OnUnloadedInternal;
		//    }
		//    catch (Exception exc) { }
		//}
		#endregion ctor

		#region common
		private ApplicationView _appView = null;
        public ApplicationView AppView { get { return _appView; } }

        private void Open(object sender, RoutedEventArgs e)
        {
            AddHandlers();
            OnVisibleBoundsChanged(_appView, null);
            OnLoaded();
        }

        private void Close(object sender, RoutedEventArgs e)
        {
            OnUnloaded();
            RemoveHandlers();
        }
        protected virtual void OnLoaded()
        { }
        protected virtual void OnUnloaded()
        { }
        private bool _isHandlersActive = false;
        private void AddHandlers()
        {
            if (_isHandlersActive == false)
            {
				_isHandlersActive = true;
				if (_appView != null) _appView.VisibleBoundsChanged += OnVisibleBoundsChanged;
                //if (_orientationSensor != null) _orientationSensor.OrientationChanged += OnSensor_OrientationChanged;
                //if (_isHardwareButtonsAPIPresent) HardwareButtons.BackPressed += OnHardwareOrSoftwareButtons_BackPressed;
                if (BackPressedRaiser != null) BackPressedRaiser.BackOrHardSoftKeyPressed += OnHardwareOrSoftwareButtons_BackPressed;
            }
        }

        private void RemoveHandlers()
        {
            if (_appView != null) _appView.VisibleBoundsChanged -= OnVisibleBoundsChanged;
            //if (_orientationSensor != null) _orientationSensor.OrientationChanged -= OnSensor_OrientationChanged;
            //if (_isHardwareButtonsAPIPresent) HardwareButtons.BackPressed -= OnHardwareOrSoftwareButtons_BackPressed;
            if (BackPressedRaiser != null) BackPressedRaiser.BackOrHardSoftKeyPressed -= OnHardwareOrSoftwareButtons_BackPressed;
            _isHandlersActive = false;
        }
		#endregion common


		#region goBack
		protected virtual void OnHardwareOrSoftwareButtons_BackPressed(object sender, BackOrHardSoftKeyPressedEventArgs e) { }

        public IBackPressedRaiser BackPressedRaiser
        {
            get { return (IBackPressedRaiser)GetValue(BackPressedRaiserProperty); }
            set { SetValue(BackPressedRaiserProperty, value); }
        }
        public static readonly DependencyProperty BackPressedRaiserProperty =
            DependencyProperty.Register("BackPressedRaiser", typeof(IBackPressedRaiser), typeof(OrientationResponsiveUserControl), new PropertyMetadata(null));
        #endregion goBack


        #region rotation
        protected virtual void OnVisibleBoundsChanged(ApplicationView sender, object args) { }
        #endregion rotation

        // the following works but we don't need it
        //#region sensor
        //private SimpleOrientationSensor _orientationSensor;
        //private SimpleOrientation _lastOrientation;
        //protected virtual void OnSensor_OrientationChanged(SimpleOrientationSensor sender, SimpleOrientationSensorOrientationChangedEventArgs args)
        //{
        //    if (_lastOrientation == null || args.Orientation != _lastOrientation)
        //    {
        //        bool mustRaise = false; // LOLLO check this if you really want to use this code
        //        switch (args.Orientation)
        //        {
        //            case SimpleOrientation.Facedown:
        //                break;
        //            case SimpleOrientation.Faceup:
        //                break;
        //            case SimpleOrientation.NotRotated:
        //                break;
        //            case SimpleOrientation.Rotated180DegreesCounterclockwise:
        //                mustRaise = true; break;
        //            case SimpleOrientation.Rotated270DegreesCounterclockwise:
        //                mustRaise = true; break;
        //            case SimpleOrientation.Rotated90DegreesCounterclockwise:
        //                mustRaise = true; break;
        //            default:
        //                break;
        //        }
        //        _lastOrientation = args.Orientation;
        //        if (mustRaise) RaiseOrientationChanged(args);
        //    }
        //}
        ///// <summary>
        ///// Raised when the orientation changes, even if rotation for the app is disabled
        ///// </summary>
        //public event TypedEventHandler<SimpleOrientationSensor, SimpleOrientationSensorOrientationChangedEventArgs> OrientationChanged;
        //private void RaiseOrientationChanged(SimpleOrientationSensorOrientationChangedEventArgs args)
        //{
        //    var listener = OrientationChanged;
        //    if (listener != null)
        //    {
        //        listener(_orientationSensor, args);
        //    }
        //}
        //#endregion sensor
    }

	/// <summary>
	/// Only define one IBackPressedRaiser each page.
	/// The controls within will take it as a dependency property and respond to its events.
	/// </summary>
	public interface IBackPressedRaiser
    {
        event EventHandler<BackOrHardSoftKeyPressedEventArgs> BackOrHardSoftKeyPressed;
    }
    public class BackOrHardSoftKeyPressedEventArgs : EventArgs
    {
        public bool Handled { get; set; }
    }

}
