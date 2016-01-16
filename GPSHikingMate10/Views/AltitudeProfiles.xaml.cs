using LolloBaseUserControls;
using LolloChartMobile;
using LolloGPS.Data;
using LolloGPS.Data.Runtime;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Utilz;
using Windows.UI.Xaml;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236
// Multithreading documentation:
//https://msdn.microsoft.com/en-us/library/vstudio/hh191443(v=vs.110).aspx
//http://blogs.msdn.com/b/pfxteam/archive/2011/10/24/10229468.aspx
//https://msdn.microsoft.com/en-us/magazine/jj991977.aspx

namespace LolloGPS.Core
{
	public sealed partial class AltitudeProfiles : OpObsOrControl, IMapApController, IInfoPanelEventReceiver
	{
		#region properties
		private AltitudeProfiles_VM _myVM = null;
		public AltitudeProfiles_VM MyVM { get { return _myVM; } }

		public PersistentData MyPersistentData { get { return App.PersistentData; } }
		public RuntimeData MyRuntimeData { get { return App.MyRuntimeData; } }

		private CancellationTokenSource _cts = null;
		#endregion properties


		#region lifecycle
		public AltitudeProfiles()
		{
			InitializeComponent();
			_myVM = new AltitudeProfiles_VM(this as IMapApController);
			DataContext = MyPersistentData;
		}
		protected override Task OpenMayOverrideAsync()
		{
			try
			{
				HistoryChart.Open();
				Route0Chart.Open();
				LandmarksChart.Open();

				AddHandlers();
				UpdateCharts();

				if (!((App)Application.Current).IsResuming)
				{
					Task centre = RunInUiThreadAsync(delegate
					{
						try
						{
							MyScrollViewer.ChangeView(0.0, MyPersistentData.AltLastVScroll, 1, true);
						}
						catch { }
					});
				}
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}

			return Task.CompletedTask;
		}
		protected override Task CloseMayOverrideAsync()
		{
			try
			{
				RemoveHandlers();

				MyPersistentData.AltLastVScroll = MyScrollViewer.VerticalOffset;

				HistoryChart.Close();
				Route0Chart.Close();
				LandmarksChart.Close();

				CancelPendingTasks(); // after removing the handlers
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}

			return Task.CompletedTask;
		}

		private void CancelPendingTasks()
		{
			_cts?.Cancel();
			//_cts.Dispose(); This is done in the exception handler that catches the OperationCanceled exception. If you do it here, the exception handler will throw an ObjectDisposed exception
			//_cts = null;
		}
		#endregion lifecycle


		#region data event handlers
		private bool _isHandlersActive = false;
		private void AddHandlers()
		{
			if (!_isHandlersActive && MyPersistentData != null)
			{
				_isHandlersActive = true;
				MyPersistentData.PropertyChanged += OnPersistentData_PropertyChanged;
				MyPersistentData.History.CollectionChanged += OnHistory_CollectionChanged;
				MyPersistentData.Route0.CollectionChanged += OnRoute0_CollectionChanged;
				MyPersistentData.Landmarks.CollectionChanged += OnLandmarks_CollectionChanged;
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
			if (e.Action != System.Collections.Specialized.NotifyCollectionChangedAction.Reset
				&& Visibility == Visibility.Visible
				&& MyPersistentData?.History != null)
			{
				Task draw = RunFunctionIfOpenAsyncT_MT(delegate
				{
					return DrawOneSeriesAsync(MyPersistentData.History, HistoryChart, false, true);
				});
			}
		}
		private void OnRoute0_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			if (e.Action != System.Collections.Specialized.NotifyCollectionChangedAction.Reset
				&& Visibility == Visibility.Visible
				&& MyPersistentData?.Route0 != null)
			{
				Task draw = RunFunctionIfOpenAsyncT_MT(delegate
				{
					return DrawOneSeriesAsync(MyPersistentData.Route0, Route0Chart, false, true);
				});
			}
		}
		private void OnLandmarks_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			if (e.Action != System.Collections.Specialized.NotifyCollectionChangedAction.Reset
				&& Visibility == Visibility.Visible
				&& MyPersistentData?.Landmarks != null)
			{
				Task draw = RunFunctionIfOpenAsyncT_MT(delegate
				{
					return DrawOneSeriesAsync(MyPersistentData.Landmarks, LandmarksChart, true, false);
				});
			}
		}
		private void OnPersistentData_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(PersistentData.IsShowingAltitudeProfiles))
			{
				Task upd = RunFunctionIfOpenAsyncA(delegate
				{
					UpdateCharts();
				});
			}
		}
		#endregion data event handlers


		#region user event handlers
		public void OnInfoPanelPointChanged(object sender, EventArgs e)
		{
			try
			{
				if (MyPersistentData.IsSelectedSeriesNonNullAndNonEmpty())
				{
					Task centre = CentreOnSeriesAsync(MyPersistentData.SelectedSeries);
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
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
		}

		public void OnInfoPanelClosed(object sender, object e)
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

		public event EventHandler<ShowOnePointDetailsRequestedArgs> ShowOnePointDetailsRequested;
		public class ShowOnePointDetailsRequestedArgs : EventArgs
		{
			private PointRecord _selectedRecord;
			public PointRecord SelectedRecord { get { return _selectedRecord; } }
			private PersistentData.Tables _selectedSeries;
			public PersistentData.Tables SelectedSeries { get { return _selectedSeries; } }
			public ShowOnePointDetailsRequestedArgs(PointRecord selectedRecord, PersistentData.Tables selectedSeries)
			{
				_selectedRecord = selectedRecord;
				_selectedSeries = selectedSeries;
			}
		}

		private void OnHistoryChartTapped(object sender, LolloChart.ChartTappedArguments e)
		{
			try
			{
				if (e.XMax * MyPersistentData.History.Count > 0)
				{
					ShowOnePointDetailsRequested?.Invoke(this, new ShowOnePointDetailsRequestedArgs(MyPersistentData.History[Convert.ToInt32(Math.Floor(e.X / e.XMax * MyPersistentData.History.Count))], PersistentData.Tables.History));
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
					ShowOnePointDetailsRequested?.Invoke(this, new ShowOnePointDetailsRequestedArgs(MyPersistentData.Route0[Convert.ToInt32(Math.Floor(e.X / e.XMax * MyPersistentData.Route0.Count))], PersistentData.Tables.Route0));
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
					ShowOnePointDetailsRequested?.Invoke(this, new ShowOnePointDetailsRequestedArgs(MyPersistentData.Landmarks[Convert.ToInt32(Math.Floor(e.X / e.XMax * MyPersistentData.Landmarks.Count))], PersistentData.Tables.Landmarks));
				}
			}
			catch (Exception ex)
			{
				Logger.Add_TPL(ex.ToString(), Logger.ForegroundLogFilename);
			}
		}
		#endregion user event handlers


		#region services
		private void UpdateCharts()
		{
			if (MyPersistentData?.IsShowingAltitudeProfiles == true) // even if still invisible!
			{
				Task drawH = Task.Run(() => DrawOneSeriesAsync(MyPersistentData.History, HistoryChart, false, true));
				if (!((App)Application.Current).IsResuming) // when resuming, skip drawing the series, which do not update in the background
				{
					Task drawR = Task.Run(() => DrawOneSeriesAsync(MyPersistentData.Route0, Route0Chart, false, true));
					Task drawL = Task.Run(() => DrawOneSeriesAsync(MyPersistentData.Landmarks, LandmarksChart, true, false));
				}
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

				//// if another thread is working in this method already, wait.
				//// Otherwise, it might dispose _cts when I still need it, unduly causing an ObjectDisposedException.
				//// Essentially, this semaphore protects the cancellation token.
				// However, by now, I always run this under _isOpenSemaphore, so I don't need this other semaphore anymore
				//await _semaphore.WaitAsync().ConfigureAwait(false);

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
												   //catch (Exception ex)
												   //{
												   //	if (SemaphoreSlimSafeRelease.IsAlive(_semaphore))
												   //		await Logger.AddAsync(ex.ToString(), Logger.ForegroundLogFilename).ConfigureAwait(false);
												   //}
			finally
			{
				_cts?.Dispose();
				_cts = null;
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
		#endregion services


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

		public Task CentreOnSeriesAsync(PersistentData.Tables series)
		{
			if (series == PersistentData.Tables.History) return CentreOnHistoryAsync();
			else if (series == PersistentData.Tables.Route0) return CentreOnRoute0Async();
			else if (series == PersistentData.Tables.Landmarks) return CentreOnLandmarksAsync();
			else return Task.CompletedTask;
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