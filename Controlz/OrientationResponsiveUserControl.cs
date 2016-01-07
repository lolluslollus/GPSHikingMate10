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
    public class OrientationResponsiveUserControl : UserControl, INotifyPropertyChanged
    {
        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        protected void RaisePropertyChanged([CallerMemberName] string propertyName = "")
        {
            var listener = PropertyChanged;
            if (listener != null)
            {
                listener(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        #endregion INotifyPropertyChanged

        #region construct and destroy
        public OrientationResponsiveUserControl()
            : base()
        {
            _appView = ApplicationView.GetForCurrentView();
            //_orientationSensor = SimpleOrientationSensor.GetDefault();
            //if (_orientationSensor != null) { _lastOrientation = _orientationSensor.GetCurrentOrientation(); }
            UseLayoutRounding = true;
            Loaded += OnLoadedInternal;
            Unloaded += OnUnloadedInternal;
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
        #endregion construct and destroy

        #region common
        private ApplicationView _appView = null;
        public ApplicationView AppView { get { return _appView; } }

        private void OnLoadedInternal(object sender, RoutedEventArgs e)
        {
            AddHandlers();
            OnVisibleBoundsChanged(_appView, null);
            OnLoaded();
        }

        private void OnUnloadedInternal(object sender, RoutedEventArgs e)
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
                if (_appView != null) _appView.VisibleBoundsChanged += OnVisibleBoundsChanged;
                //if (_orientationSensor != null) _orientationSensor.OrientationChanged += OnSensor_OrientationChanged;
                //if (_isHardwareButtonsAPIPresent) HardwareButtons.BackPressed += OnHardwareOrSoftwareButtons_BackPressed;
                if (BackPressedRaiser != null) BackPressedRaiser.BackOrHardSoftKeyPressed += OnHardwareOrSoftwareButtons_BackPressed;
                    _isHandlersActive = true;
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
        //private static bool _isHardwareButtonsAPIPresent = Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.Phone.UI.Input.HardwareButtons");

        /// <summary>
        /// Gets or sets a value, which overrides the default device behaviour when the user presses the back hardware key.
        /// </summary>
        //public bool IsOverrideBackKeyPressed
        //{
        //    get { return (bool)GetValue(IsOverrideBackKeyPressedProperty); }
        //    set { SetValue(IsOverrideBackKeyPressedProperty, value); }
        //}
        //public static readonly DependencyProperty IsOverrideBackKeyPressedProperty =
        //    DependencyProperty.Register("IsOverrideBackKeyPressed", typeof(bool), typeof(OrientationResponsiveUserControl), new PropertyMetadata(false));

        /// <summary>
        /// Back key pressed
        /// </summary>
        //public event EventHandler BackKeyPressed;
        //private void RaiseBackKeyPressed()
        //{
        //    var listener = BackKeyPressed;
        //    if (listener != null)
        //    {
        //        listener(this, EventArgs.Empty);
        //    }
        //}
        /// <summary>
        /// Gets or sets the behaviour when the back key is pressed.
        /// If true, do not respond to the event directly but raise BackKeyPressed instead.
        /// If false, the framework will run its course.
        /// </summary>
        protected virtual void OnHardwareOrSoftwareButtons_BackPressed(object sender, BackOrHardSoftKeyPressedEventArgs e)
        {
            //if (IsOverrideBackKeyPressed && Visibility == Visibility.Visible) // && ActualHeight > 0.0 && ActualWidth > 0.0)
            //{
            //    if (e != null) e.Handled = true;
            //    //BackKeyPressed?.Invoke(this, EventArgs.Empty);
            //    //RaiseBackKeyPressed();
            //}
        }

        public BackPressedRaiser BackPressedRaiser
        {
            get { return (BackPressedRaiser)GetValue(BackPressedRaiserProperty); }
            set { SetValue(BackPressedRaiserProperty, value); }
        }
        public static readonly DependencyProperty BackPressedRaiserProperty =
            DependencyProperty.Register("BackPressedRaiser", typeof(BackPressedRaiser), typeof(OrientationResponsiveUserControl), new PropertyMetadata(null));
        #endregion goBack

        #region rotation
        protected virtual void OnVisibleBoundsChanged(ApplicationView sender, object args)
        {
            RaiseVisibleBoundsChanged(args);
        }
        /// <summary>
        /// Raised when the orientation changes, only if rotation for the app is enabled
        /// </summary>
        public event TypedEventHandler<ApplicationView, object> VisibleBoundsChanged;
        private void RaiseVisibleBoundsChanged(object args)
        {
            var listener = VisibleBoundsChanged;
            if (listener != null)
            {
                listener(_appView, args);
            }
        }
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

    public interface BackPressedRaiser
    {
        event EventHandler<BackOrHardSoftKeyPressedEventArgs> BackOrHardSoftKeyPressed;
    }
    public class BackOrHardSoftKeyPressedEventArgs : EventArgs
    {
        public bool Handled { get; set; }
    }

}
