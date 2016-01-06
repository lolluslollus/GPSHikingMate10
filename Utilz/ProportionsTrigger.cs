using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Sensors;
using Windows.Foundation;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;

namespace Utilz
{
    public class ProportionsTrigger : StateTriggerBase
    {
        private ApplicationView _appView = null;
        //private SimpleOrientationSensor _orientationSensor;

        private FrameworkElement _targetElement;
        public FrameworkElement TargetElement
        {
            get
            {
                return _targetElement;
            }
            set
            {
                _targetElement = value;
                AddHandlers();
            }
        }

        private bool _isHandlersActive = false;
        private void AddHandlers()
        {
            //if (_orientationSensor == null) _orientationSensor = SimpleOrientationSensor.GetDefault();
            if (_appView == null) _appView = ApplicationView.GetForCurrentView();
            if (!_isHandlersActive /*&& _orientationSensor != null */ && _appView != null)
            {
                //_orientationSensor.OrientationChanged += OnSensor_OrientationChanged;
                _appView.VisibleBoundsChanged += OnVisibleBoundsChanged;
				UpdateTrigger(_appView.VisibleBounds.Width < _appView.VisibleBounds.Height);
				_isHandlersActive = true;
            }
        }

        private void RemoveHandlers()
        {
            //if (_orientationSensor != null) _orientationSensor.OrientationChanged -= OnSensor_OrientationChanged;
            if (_appView != null) _appView.VisibleBoundsChanged -= OnVisibleBoundsChanged;
            _isHandlersActive = false;
        }

        private void UpdateTrigger(bool newValue)
        {
            if (_targetElement != null)
            {
                bool newValue_mt = newValue;
                _targetElement.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, delegate
                {
                    SetActive(newValue_mt);
                }).AsTask().ConfigureAwait(false);
            }
            else
            {
                SetActive(false);
            }
        }

        private Rect? _lastVisibleBounds = null;
        private void OnVisibleBoundsChanged(ApplicationView sender, object args)
        {
            if (_lastVisibleBounds == null || _appView.VisibleBounds.Height != _lastVisibleBounds?.Height || _appView.VisibleBounds.Width != _lastVisibleBounds?.Width)
            {
                UpdateTrigger(_appView.VisibleBounds.Width < _appView.VisibleBounds.Height);
            }
            _lastVisibleBounds = _appView.VisibleBounds;
        }
    }
}
