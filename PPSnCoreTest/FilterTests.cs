using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TecWare.PPSn.Data;

namespace TecWare.PPSn
{
	[TestClass]
	public class FilterTests
	{
		private void TestFilter(string expected, string filterExpression)
		{
			var f = PpsDataFilterExpression.Parse(filterExpression);
			var result = f.ToString();
			Console.Write("Input====> ");
			Console.WriteLine(filterExpression);
			Console.Write("BaseType=> ");
			Console.WriteLine("{0}", f.Type);
			Console.Write("result===> ");
			Console.WriteLine(result);
			Console.Write("expected=> ");
			Console.WriteLine(expected);

			Assert.AreEqual(expected, result);
		}

		[TestMethod]
		public void TestFilter01()
			=> TestFilter("text", "text");

		[TestMethod]
		public void TestFilter02()
			=> TestFilter("and(a b)", "a  b");

		[TestMethod]
		public void TestFilter03()
			=> TestFilter("or(a b)", "or(a  b)");

		[TestMethod]
		public void TestFilter04()
		{
			// test: ToString
			TestFilter("#..2010#", "#01.01.2010~01.01.2011#");
			TestFilter("#.01.2010#", "#01.01.2010~01.02.2010#");
			TestFilter("#01.01.2010#", "#01.01.2010~02.01.2010#");
			TestFilter("#01.01.2010~31.12.2010#", "#01.01.2010~31.12.2010#");
		}

		[TestMethod]
		public void TestFilter05de()
		{
			var cultureInfo = Thread.CurrentThread.CurrentUICulture;
			try
			{
				Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("de-DE");
				TestFilter("#..2010#", "#2010#");
				TestFilter("#.09.2010#", "#.09.2010#");
				TestFilter($"#.09.2010#", "#09.2010#");
				TestFilter("#20.09.2010#", "#20.09.2010#");
				TestFilter($"#20.09.{DateTime.Now.Year}#", "#20.09.#");
				TestFilter($"#20.{DateTime.Now.Month:00}.{DateTime.Now.Year}#", "#20.#");
				TestFilter($"#{DateTime.Now.Day:00}.{DateTime.Now.Month:00}.{DateTime.Now.Year}#", "##");

				TestFilter($"##", "#31.02.2010#");
			}
			finally
			{
				Thread.CurrentThread.CurrentUICulture = cultureInfo;
			}
		}

		[TestMethod]
		public void TestFilter05es()
		{
			var cultureInfo = Thread.CurrentThread.CurrentUICulture;
			try
			{
				Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("es-ES");
				TestFilter("#//2010#", "#2010#");
				TestFilter("#/09/2010#", "#/09/2010#");
				TestFilter($"#/09/2010#", "#09/2010#");
				TestFilter("#20/09/2010#", "#20/09/2010#");
				TestFilter($"#20/09/{DateTime.Now.Year}#", "#20/09/#");
				TestFilter($"#20/{DateTime.Now.Month:00}/{DateTime.Now.Year}#", "#20/#");
				TestFilter($"#{DateTime.Now.Day:00}/{DateTime.Now.Month:00}/{DateTime.Now.Year}#", "##");

				TestFilter($"##", "#31/02/2010#");
			}
			finally
			{
				Thread.CurrentThread.CurrentUICulture = cultureInfo;
			}
		}

		[TestMethod]
		public void TestFilter05us()
		{
			var cultureInfo = Thread.CurrentThread.CurrentUICulture;
			try
			{
				Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("en-US");
				TestFilter("#//2010#", "#2010#");
				TestFilter("#9//2010#", "#9//2010#");
				TestFilter($"#9//2010#", "#9/2010#");
				TestFilter("#9/20/2010#", "#09/20/2010#");
				TestFilter($"#9/20/{DateTime.Now.Year}#", "#9/20/#");
				TestFilter($"#{DateTime.Now.Month}/20/{DateTime.Now.Year}#", "#20/#");
				TestFilter($"#9/{DateTime.Now.Day}/{DateTime.Now.Year}#", "#9/#");
				TestFilter($"#{DateTime.Now.Month}/{DateTime.Now.Day}/{DateTime.Now.Year}#", "##");

				TestFilter($"##", "#02/31/2010#");
			}
			finally
			{
				Thread.CurrentThread.CurrentUICulture = cultureInfo;
			}
		}

		[TestMethod]
		public void TestFilter06()
			=> TestFilter("#20", "#20");

		[TestMethod]
		public void TestFilter07()
			=> TestFilter("Name:(A)", "Name:(A)");
		
		private static Tuple<bool, string, int, int, string> SplitRegEx(string line)
		{
			var  regAttributeLine = new Regex(@"(?<r>[\+\-])(?<n>\w+)(\:(?<c>\d*)(\:(?<u>\d*))?)?\=(?<v>.*)", RegexOptions.Singleline | RegexOptions.Compiled);
			var m = regAttributeLine.Match(line);
			if (!m.Success)
				return null;

			var isRemoved = m.Groups["r"].Value[0] != '-';
			var tagName = m.Groups["n"].Value;
			var classification = String.IsNullOrEmpty(m.Groups["c"].Value) ? -1 :Int32.Parse(m.Groups["c"].Value) ;
			var userId = String.IsNullOrEmpty(m.Groups["u"].Value) ? -1 : Int32.Parse(m.Groups["u"].Value);
			var value = m.Groups["v"].Value;

			return new Tuple<bool, string, int, int, string>(isRemoved, tagName, classification, userId, value);
		}

		[TestMethod]
		public void TestTagRex01()
		{
			var t1 = SplitRegEx("+tag1=");
			Assert.AreEqual(true, t1.Item1);
			Assert.AreEqual("tag1", t1.Item2);
			Assert.AreEqual(-1, t1.Item3);
			Assert.AreEqual(-1, t1.Item4);
			Assert.AreEqual(String.Empty, t1.Item5);

			t1 = SplitRegEx("+tag1:1:2=v1");
			Assert.AreEqual(true, t1.Item1);
			Assert.AreEqual("tag1", t1.Item2);
			Assert.AreEqual(1, t1.Item3);
			Assert.AreEqual(2, t1.Item4);
			Assert.AreEqual("v1", t1.Item5);
			
			t1 = SplitRegEx("-tag1:3=");
			Assert.AreEqual(false, t1.Item1);
			Assert.AreEqual("tag1", t1.Item2);
			Assert.AreEqual(3, t1.Item3);
			Assert.AreEqual(-1, t1.Item4);
			Assert.AreEqual(String.Empty, t1.Item5);
			
			t1 = SplitRegEx("-tag1::4=v2.");
			Assert.AreEqual(false, t1.Item1);
			Assert.AreEqual("tag1", t1.Item2);
			Assert.AreEqual(-1, t1.Item3);
			Assert.AreEqual(4, t1.Item4);
			Assert.AreEqual("v2.", t1.Item5);
		}
	}
}
