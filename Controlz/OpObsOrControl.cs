﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Utilz;
using Windows.UI.Xaml;

namespace LolloBaseUserControls
{
	/// <summary>
	/// This is a smarter UserControl that can be opened and closed, asynchronously. 
	/// It will stay disabled as long as it is closed.
	/// Do not bind to IsEnabled, but to IsEnabledOverride instead.
	/// </summary>
	public abstract class OpObsOrControl : OrientationResponsiveUserControl
	{
		#region properties
		protected volatile SemaphoreSlimSafeRelease _isOpenSemaphore = null;

		protected volatile bool _isOpen = false;
		public bool IsOpen { get { return _isOpen; } protected set { if (_isOpen != value) { _isOpen = value; RaisePropertyChanged_UI(); } } }

		protected volatile bool _isEnabledAllowed = false;
		public bool IsEnabledAllowed
		{
			get { return _isEnabledAllowed; }
			protected set
			{
				if (_isEnabledAllowed != value)
				{
					_isEnabledAllowed = value; RaisePropertyChanged_UI();
					Task upd = UpdateIsEnabledAsync();
				}
			}
		}

		public bool IsEnabledOverride
		{
			get { return (bool)GetValue(IsEnabledOverrideProperty); }
			set { SetValue(IsEnabledOverrideProperty, value); }
		}
		public static readonly DependencyProperty IsEnabledOverrideProperty =
			DependencyProperty.Register("IsEnabledOverride", typeof(bool), typeof(OpObsOrControl), new PropertyMetadata(true, OnIsEnabledOverrideChanged));
		private static void OnIsEnabledOverrideChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
		{
			Task upd = (obj as OpObsOrControl)?.UpdateIsEnabledAsync();
		}
		private Task UpdateIsEnabledAsync()
		{
			return RunInUiThreadAsync(delegate
			{
				IsEnabled = IsEnabledAllowed && IsEnabledOverride;
			});
		}

		private volatile CancellationTokenSource _cts = null;
		protected CancellationTokenSource Cts { get { return _cts; } }
		protected CancellationToken CancToken
		{
			get
			{
				var cts = _cts;
				if (cts != null) return cts.Token;
				else return new CancellationToken(true); // throw new OperationCanceledException();
			}
		}
		#endregion properties


		#region ctor
		public OpObsOrControl() : base()
		{
			Task upd = UpdateIsEnabledAsync();
		}
		#endregion ctor


		#region open close
		public async Task<bool> OpenAsync()
		{
			if (!_isOpen)
			{
				if (!SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore)) _isOpenSemaphore = new SemaphoreSlimSafeRelease(1, 1);
				try
				{
					await _isOpenSemaphore.WaitAsync().ConfigureAwait(false);
					if (!_isOpen)
					{
						_cts = new CancellationTokenSource(); // LOLLO TODO test this new cts and token handling

						await OpenMayOverrideAsync().ConfigureAwait(false);

						IsOpen = true;
						IsEnabledAllowed = true;
						return true;
					}
				}
				catch (Exception ex)
				{
					if (SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore))
						await Logger.AddAsync(GetType().Name + ex.ToString(), Logger.ForegroundLogFilename);
				}
				finally
				{
					SemaphoreSlimSafeRelease.TryRelease(_isOpenSemaphore);
				}
			}
			if (_isOpen) await SetIsEnabledAsync(true).ConfigureAwait(false);
			return false;
		}

		protected virtual Task OpenMayOverrideAsync()
		{
			return Task.CompletedTask; // avoid warning
		}

		public virtual async Task<bool> CloseAsync()
		{
			if (_isOpen)
			{
				_cts?.Cancel(true);
				_cts?.Dispose();
				_cts = null;

				try
				{
					await _isOpenSemaphore.WaitAsync().ConfigureAwait(false);
					if (_isOpen)
					{
						IsEnabledAllowed = false;
						IsOpen = false;
						await CloseMayOverrideAsync().ConfigureAwait(false);
						return true;
					}
				}
				catch (Exception ex)
				{
					if (SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore))
						await Logger.AddAsync(GetType().Name + ex.ToString(), Logger.ForegroundLogFilename);
				}
				finally
				{
					SemaphoreSlimSafeRelease.TryDispose(_isOpenSemaphore);
					_isOpenSemaphore = null;
				}
			}
			return false;
		}
#pragma warning disable 1998
		protected virtual async Task CloseMayOverrideAsync() { } // LOLLO return null dumps
#pragma warning restore 1998
		#endregion open close


		#region while open
		private async Task<bool> SetIsEnabledAsync(bool enable)
		{
			if (_isOpen && IsEnabled != enable)
			{
				try
				{
					await _isOpenSemaphore.WaitAsync().ConfigureAwait(false);
					if (_isOpen && IsEnabled != enable)
					{
						IsEnabledAllowed = enable;
						return true;
					}
				}
				catch (Exception ex)
				{
					if (SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore))
						await Logger.AddAsync(GetType().Name + ex.ToString(), Logger.ForegroundLogFilename);
				}
				finally
				{
					SemaphoreSlimSafeRelease.TryRelease(_isOpenSemaphore);
				}
			}
			return false;
		}

		protected async Task<bool> RunFunctionIfOpenAsyncA(Action func)
		{
			if (_isOpen)
			{
				try
				{
					await _isOpenSemaphore.WaitAsync(); //.ConfigureAwait(false);
					if (_isOpen)
					{
						func();
						return true;
					}
				}
				catch (Exception ex)
				{
					if (SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore))
						await Logger.AddAsync(GetType().Name + ex.ToString(), Logger.ForegroundLogFilename);
				}
				finally
				{
					SemaphoreSlimSafeRelease.TryRelease(_isOpenSemaphore);
				}
			}
			return false;
		}
		protected async Task<bool> RunFunctionIfOpenAsyncB(Func<bool> func)
		{
			if (_isOpen)
			{
				try
				{
					await _isOpenSemaphore.WaitAsync(); //.ConfigureAwait(false);
					if (_isOpen) return func();
				}
				catch (Exception ex)
				{
					if (SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore))
						await Logger.AddAsync(GetType().Name + ex.ToString(), Logger.ForegroundLogFilename);
				}
				finally
				{
					SemaphoreSlimSafeRelease.TryRelease(_isOpenSemaphore);
				}
			}
			return false;
		}
		protected async Task<bool> RunFunctionIfOpenAsyncT(Func<Task> funcAsync)
		{
			if (_isOpen)
			{
				try
				{
					await _isOpenSemaphore.WaitAsync(); //.ConfigureAwait(false);
					if (_isOpen)
					{
						await funcAsync().ConfigureAwait(false);
						return true;
					}
				}
				catch (Exception ex)
				{
					if (SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore))
						await Logger.AddAsync(GetType().Name + ex.ToString(), Logger.ForegroundLogFilename);
				}
				finally
				{
					SemaphoreSlimSafeRelease.TryRelease(_isOpenSemaphore);
				}
			}
			return false;
		}
		protected async Task<bool> RunFunctionIfOpenAsyncTB(Func<Task<bool>> funcAsync)
		{
			if (_isOpen)
			{
				try
				{
					await _isOpenSemaphore.WaitAsync(); //.ConfigureAwait(false);
					if (_isOpen) return await funcAsync().ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					if (SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore))
						await Logger.AddAsync(GetType().Name + ex.ToString(), Logger.ForegroundLogFilename);
				}
				finally
				{
					SemaphoreSlimSafeRelease.TryRelease(_isOpenSemaphore);
				}
			}
			return false;
		}
		protected async Task<bool> RunFunctionIfOpenAsyncT_MT(Func<Task> funcAsync)
		{
			if (_isOpen)
			{
				try
				{
					await _isOpenSemaphore.WaitAsync(); //.ConfigureAwait(false);
					if (_isOpen)
					{
						await Task.Run(delegate { return funcAsync(); }).ConfigureAwait(false);
						return true;
					}
				}
				catch (Exception ex)
				{
					if (SemaphoreSlimSafeRelease.IsAlive(_isOpenSemaphore))
						await Logger.AddAsync(GetType().Name + ex.ToString(), Logger.ForegroundLogFilename);
				}
				finally
				{
					SemaphoreSlimSafeRelease.TryRelease(_isOpenSemaphore);
				}
			}
			return false;
		}
		#endregion while open
	}
}