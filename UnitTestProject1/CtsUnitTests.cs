using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTestProject1
{
	[TestClass]
	public class CtsUnitTests
	{
		[TestMethod]
		public async void LongRunner1()
		{
			var aaa = new LongRunner1();

			await aaa.OpenAsync();

			Task longRunner = aaa.ManySeconds();
			await Task.Delay(3000);
			aaa.Cancel();

			Assert.AreEqual(aaa.HowManyDelays, 2);
		}
		[TestMethod]
		public async void LongRunner2()
		{
			var aaa = new LongRunner2();

			await aaa.OpenAsync();

			Task longRunner = aaa.ManySecondsWhenOpen();
			await Task.Delay(3000);
			aaa.Cancel();

			Assert.AreEqual(aaa.HowManyDelays, 2);
		}
	}
}
