using LolloBaseUserControls;
using LolloChartMobile;
using LolloGPS.Data;
using LolloGPS.Data.Runtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Utilz;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236
// Multithreading documentation:
//https://msdn.microsoft.com/en-us/library/vstudio/hh191443(v=vs.110).aspx
//http://blogs.msdn.com/b/pfxteam/archive/2011/10/24/10229468.aspx
//https://msdn.microsoft.com/en-us/magazine/jj991977.aspx

namespace LolloGPS.Core
{
	public sealed partial class AltitudeProfiles : OrientationResponsiveUserControl, IMapApController
	{
		private AltitudeProfiles_VM _myVM = null;
		public AltitudeProfiles_VM MyVM { get { return _myVM; } }
		
		public PersistentData MyPersistentData { get { return App.PersistentData; } }
		public RuntimeData MyRuntimeData { get { return App.MyRuntimeData; } }

		private CancellationTokenSource _cts = null;
		static SemaphoreSlimSafeRelease _semaphore = null;

		public AltitudeProfiles()
		{
			InitializeComponent();
			_myVM = new AltitudeProfiles_VM(this as IMapApController);
			DataContext = MyPersistentData;
		}
		public void Activate()
		{
			try
			{
				if (!SemaphoreSlimSafeRelease.IsAlive(_semaphore)) _semaphore = new SemaphoreSlimSafeRelease(1, 1);
				HistoryChart.Activate();
				Route0Chart.Activate();
				LandmarksChart.Activate();
				UpdateCharts();
				AddHandlers();
				Debug.WriteLine("AltitudeProfiles activated");
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
		}
		public void Deactivate()
		{
			try
			{
				RemoveHandlers();
				HistoryChart.Deactivate();
				Route0Chart.Deactivate();
				LandmarksChart.Deactivate();
				MyPointInfoPanel.Deactivate();
				CancelPendingTasks(); // after removing the handlers
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
		}
		private void CancelPendingTasks()
		{
			_cts?.Cancel();
			//_cts.Dispose(); This is done in the exception handler that catches the OperationCanceled exception. If you do it here, the exception handler will throw an ObjectDisposed exception
			//_cts = null;
			SemaphoreSlimSafeRelease.TryDispose(_semaphore);
		}

		private bool _isHandlersActive = false;
		private void AddHandlers()
		{
			if (!_isHandlersActive && MyPersistentData != null)
			{
				MyPersistentData.PropertyChanged += OnPersistentData_PropertyChanged;
				MyPersistentData.History.CollectionChanged += OnHistory_CollectionChanged;
				MyPersistentData.Route0.CollectionChanged += OnRoute0_CollectionChanged;
				MyPersistentData.Landmarks.CollectionChanged += OnLandmarks_CollectionChanged;
				_isHandlersActive = true;
			}
		}
		private void RemoveHandlers()
		{
			if (MyPersistentData != null)
			{
				MyPersistentData.PropertyChanged -= OnPersistentData_PropertyChanged;
				MyPersistentData.History.CollectionChanged -= OnHistory_CollectionChanged;
				MyPersistentData.Route0.CollectionChanged -= OnRoute0_CollectionChanged;
				MyPersistentData.Landmarks.CollectionChanged -= OnLandmarks_CollectionChanged;
				_isHandlersActive = false;
			}
		}

		private void OnHistory_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			if (Visibility == Visibility.Visible && MyPersistentData != null && MyPersistentData.History != null)
			{
				Task draw = Task.Run(() => DrawOneSeriesAsync(MyPersistentData.History, HistoryChart, false, true));
			}
		}
		private void OnRoute0_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			if (Visibility == Visibility.Visible && MyPersistentData != null && MyPersistentData.Route0 != null)
			{
				Task draw = Task.Run(() => DrawOneSeriesAsync(MyPersistentData.Route0, Route0Chart, false, true));
			}
		}
		private void OnLandmarks_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			if (Visibility == Visibility.Visible && MyPersistentData != null && MyPersistentData.Landmarks != null)
			{
				Task draw = Task.Run(() => DrawOneSeriesAsync(MyPersistentData.Landmarks, LandmarksChart, true, false));
			}
		}
		private void OnPersistentData_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(PersistentData.IsShowingAltitudeProfiles))
			{
				UpdateCharts();
			}
		}
		private void UpdateCharts()
		{
			if (MyPersistentData != null && MyPersistentData.IsShowingAltitudeProfiles) // even if still invisible!
			{
				Task drawH = Task.Run(() => DrawOneSeriesAsync(MyPersistentData.History, HistoryChart, false, true));
				Task drawR = Task.Run(() => DrawOneSeriesAsync(MyPersistentData.Route0, Route0Chart, false, true));
				Task drawL = Task.Run(() => DrawOneSeriesAsync(MyPersistentData.Landmarks, LandmarksChart, true, false));
			}
		}
		private void OnInfoPanelPointChanged(object sender, EventArgs e)
		{
			try
			{
				if (MyPersistentData.IsSelectedSeriesNonNullAndNonEmpty())
				{
					switch (MyPersistentData.SelectedSeries)
					{
						case PersistentData.Tables.History:
							HistoryChart.CrossPoint(HistoryChart.XY1DataSeries, MyPersistentData.SelectedIndex_Base1 - 1, MyPersistentData.Selected.Altitude);
							break;
						case PersistentData.Tables.Route0:
							Route0Chart.CrossPoint(Route0Chart.XY1DataSeries, MyPersistentData.SelectedIndex_Base1 - 1, MyPersistentData.Selected.Altitude);
							break;
						case PersistentData.Tables.Landmarks:
							LandmarksChart.CrossPoint(LandmarksChart.XY1DataSeries, MyPersistentData.SelectedIndex_Base1 - 1, MyPersistentData.Selected.Altitude);
							break;
						case PersistentData.Tables.nil:
							break;
						default:
							break;
					}
				}
				else
				{
					SelectedPointPopup.IsOpen = false;
				}
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
		}

		protected override void OnHardwareOrSoftwareButtons_BackPressed(object sender, BackOrHardSoftKeyPressedEventArgs e)
		{
			if (Visibility == Visibility.Visible) // && ActualHeight > 0.0 && ActualWidth > 0.0)
			{
				if (e != null) e.Handled = true;
				SelectedPointPopup.IsOpen = false;
				//BackKeyPressed?.Invoke(this, EventArgs.Empty);
				//RaiseBackKeyPressed();
			}            
		}
		//private void OnBackKeyPressed(object sender, EventArgs e)
		//{
		//    SelectedPointPopup.IsOpen = false;
		//}
		private void OnInfoPanelClosed(object sender, object e)
		{
			try
			{
				switch (MyPersistentData.SelectedSeries)
				{
					case PersistentData.Tables.History:
						HistoryChart.UncrossPoint(HistoryChart.XY1DataSeries);
						break;
					case PersistentData.Tables.Route0:
						Route0Chart.UncrossPoint(Route0Chart.XY1DataSeries);
						break;
					case PersistentData.Tables.Landmarks:
						LandmarksChart.UncrossPoint(LandmarksChart.XY1DataSeries);
						break;
					case PersistentData.Tables.nil:
						break;
					default:
						break;
				}
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
		}

		private void OnHistoryChartTapped(object sender, LolloChart.ChartTappedArguments e)
		{
			try
			{
				if (e.XMax * MyPersistentData.History.Count > 0)
				{
					MyPointInfoPanel.SetDetails(MyPersistentData.History[Convert.ToInt32(Math.Floor(e.X / e.XMax * MyPersistentData.History.Count))], PersistentData.Tables.History);
					SelectedPointPopup.IsOpen = true;
				}
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
		}
		private void OnRoute0ChartTapped(object sender, LolloChart.ChartTappedArguments e)
		{
			try
			{
				if (e.X / e.XMax * MyPersistentData.Route0.Count > 0)
				{
					MyPointInfoPanel.SetDetails(MyPersistentData.Route0[Convert.ToInt32(Math.Floor(e.X / e.XMax * MyPersistentData.Route0.Count))], PersistentData.Tables.Route0);
					SelectedPointPopup.IsOpen = true;
				}
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
		}
		private void OnLandmarksChartTapped(object sender, LolloChart.ChartTappedArguments e)
		{
			try
			{
				if (e.X / e.XMax * MyPersistentData.Landmarks.Count > 0)
				{
					MyPointInfoPanel.SetDetails(MyPersistentData.Landmarks[Convert.ToInt32(Math.Floor(e.X / e.XMax * MyPersistentData.Landmarks.Count))], PersistentData.Tables.Landmarks);
					SelectedPointPopup.IsOpen = true;
				}
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
		}

		private async Task DrawOneSeriesAsync(Collection<PointRecord> coll, LolloChart chart, bool isHistogram, bool respectDatesAndTimes)
		{
			// this method can run on any thread
			try
			{
				bool sortIfRespectingDatesAndTimes = true;
				double maxAltitude = default(double);
				double minAltitude = default(double);
				double maxTime = default(double);
				double minTime = default(double);
				double[,] points = null;
				_myVM.InitialiseChartData(coll, respectDatesAndTimes, sortIfRespectingDatesAndTimes,
					ref maxAltitude, ref minAltitude, ref maxTime, ref minTime, ref points);

				await _semaphore.WaitAsync().ConfigureAwait(false); // if another thread is working in this method already, wait.
				// Otherwise, it might dispose _cts when I still need it, unduly causing an ObjectDisposedException.
				// Essentially, this semaphore protects the cancellation token.
				if (_cts == null) _cts = new CancellationTokenSource();
				CancellationToken token = _cts.Token;

				await RunInUiThreadAsync(delegate
				{
					DrawOneSeries(maxAltitude, minAltitude, maxTime, minTime, points, chart, isHistogram, token);
				}).ConfigureAwait(false);
			}
			catch (ObjectDisposedException) { } // fires when I dispose sema and have not rector'd it while the current thread is inside it (unlikely)
			catch (SemaphoreFullException) { } // fires when I dispose sema and rector it while the current thread is inside it
			catch (OperationCanceledException) { } // fires when cts is cancelled
			catch (Exception ex)
			{
				if (SemaphoreSlimSafeRelease.IsAlive(_semaphore))
					await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename).ConfigureAwait(false);
			}
			finally
			{
				_cts?.Dispose();
				_cts = null;
				SemaphoreSlimSafeRelease.TryRelease(_semaphore);
			}
		}

		private void DrawOneSeries(double maxAltitude, double minAltitude, double maxTime, double minTime, double[,] points,
			LolloChart chart, bool isHistogram, CancellationToken token)
		{
			// this method must run in the UI thread
			chart.Visibility = Visibility.Collapsed; // we set the visibility again after drawing, it seems a little faster
			if (MyPersistentData != null && points != null && points.GetUpperBound(0) >= 0)
			{
				try
				{
#if DEBUG
					Stopwatch sw = new Stopwatch(); sw.Start();
#endif
					chart.XGridScale = new GridScale(ScaleType.Linear, minTime, maxTime);
					chart.Y1GridScale = new GridScale(ScaleType.Linear, minAltitude, maxAltitude);
					chart.XY1DataSeries = new XYDataSeries(points, isHistogram);
					double[] xLabels = { maxTime, minTime };
					//chart.XGridLabels = new GridLabels(xLabels);
					chart.XPrimaryGridLines = new GridLines(xLabels);

					double[] yLabels = {
						maxAltitude,
						minAltitude + (maxAltitude - minAltitude) * .75,
						minAltitude + (maxAltitude - minAltitude) * .5,
						minAltitude + (maxAltitude - minAltitude) * .25,
						minAltitude };
					chart.Y1GridLabels = new GridLabels(yLabels, "#0. m");
					chart.YPrimaryGridLines = new GridLines(yLabels);

					token.ThrowIfCancellationRequested();
					chart.Draw();

					chart.Visibility = Visibility.Visible;
#if DEBUG
					sw.Stop();
					Debug.WriteLine("DrawOneSeries took " + sw.ElapsedMilliseconds + " ms to draw the chart");
#endif
				}
				catch (Exception ex)
				{
					Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
				}
			}
			RefreshTBNoData();
		}

		private void RefreshTBNoData()
		{
			try
			{
				if (HistoryChart.Visibility == Visibility.Collapsed && Route0Chart.Visibility == Visibility.Collapsed && LandmarksChart.Visibility == Visibility.Collapsed)
					TBNoData.Visibility = Visibility.Visible;
				else
					TBNoData.Visibility = Visibility.Collapsed;
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
		}
#region IMapApController
		public Task CentreOnHistoryAsync()
		{
			try
			{
				return RunInUiThreadAsync(delegate
				{
					if (MyPersistentData != null && MyPersistentData.IsShowingAltitudeProfiles && HistoryChart.Visibility == Visibility.Visible)
					{
						MyScrollViewer.ChangeView(0.0, 0.0, 1, false);
					}
				});
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
			return Task.CompletedTask; // to respect the form of the output
		}
		public Task CentreOnRoute0Async()
		{
			try
			{
				return RunInUiThreadAsync(delegate
				{
					if (MyPersistentData != null && MyPersistentData.IsShowingAltitudeProfiles && Route0Chart.Visibility == Visibility.Visible)
					{
						double vOffset = HistoryChart.Visibility == Visibility.Visible ? HistoryChart.ActualHeight : 0.0;
						MyScrollViewer.ChangeView(0.0, vOffset, 1, false);
					}
				});
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
			return Task.CompletedTask; // to respect the form of the output
		}
		public Task CentreOnLandmarksAsync()
		{
			try
			{
				return RunInUiThreadAsync(delegate 
				{
					if (MyPersistentData != null && MyPersistentData.IsShowingAltitudeProfiles && LandmarksChart.Visibility == Visibility.Visible)
					{
						double vOffset = HistoryChart.Visibility == Visibility.Visible ? HistoryChart.ActualHeight : 0.0;
						if (Route0Chart.Visibility == Visibility.Visible) vOffset += Route0Chart.ActualHeight;
						MyScrollViewer.ChangeView(0.0, vOffset, 1, false);
					}
				});
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
			return Task.CompletedTask; // to respect the form of the output
		}
		public Task CentreOnTargetAsync()
		{
			return Task.CompletedTask; // to respect the form of the output and interface
		}
		public Task Goto2DAsync()
		{
			return Task.CompletedTask; // to respect the form of the output and interface
		}
#endregion IMapApController
	}
}
