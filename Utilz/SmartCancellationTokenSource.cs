using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Utilz
{
	public class SmartCancellationTokenSource : CancellationTokenSource
	{
		private volatile bool _isDisposed = false;
		public bool IsDisposed { get { return _isDisposed; } }
		protected override void Dispose(bool disposing)
		{
			_isDisposed = true;
			base.Dispose(disposing);
		}
		public void CancelSafe(bool throwOnFirstException = false)
		{
			try
			{
				Cancel(throwOnFirstException);
			}
			catch (Exception ex)
			{
				// LOLLO TODO check this
			}
		}
		public bool IsCancellationRequestedSafe
		{
			get
			{
				try
				{
					return IsCancellationRequestedSafe;
				}
				catch (Exception)
				{
					return true;
				}
			}
		}
	}
	public static class SmartCancellationTokenSourceExtensions
	{
		public static bool IsAlive(this SmartCancellationTokenSource cts)
		{
			var lcts = cts;
			return (lcts != null && !lcts.IsDisposed);
		}
	}
}