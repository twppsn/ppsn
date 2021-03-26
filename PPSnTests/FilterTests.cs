#region -- copyright --
//
// Licensed under the EUPL, Version 1.1 or - as soon they will be approved by the
// European Commission - subsequent versions of the EUPL(the "Licence"); You may
// not use this work except in compliance with the Licence.
//
// You may obtain a copy of the Licence at:
// http://ec.europa.eu/idabc/eupl
//
// Unless required by applicable law or agreed to in writing, software distributed
// under the Licence is distributed on an "AS IS" basis, WITHOUT WARRANTIES OR
// CONDITIONS OF ANY KIND, either express or implied. See the Licence for the
// specific language governing permissions and limitations under the Licence.
//
#endregion
using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TecWare.PPSn.Data;

namespace TecWare.PPSn
{
	[TestClass]
	public class FilterTests
	{
		private sealed class Props
		{
			public string Text { get; set; }
			public long Int64 { get; set; }
		}

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

		private void TestDateFilter(string expectedText, DateTime from, DateTime to, string valueExpression, CultureInfo cultureInfo, CultureInfo cultureInfo2 = null)
		{
			var r = PpsDataFilterDateTimeValue.ParseDateTime(valueExpression, 0, valueExpression.Length, cultureInfo);

			Assert.AreEqual(from, r.From);
			Assert.AreEqual(to, r.To);

			Assert.AreEqual(expectedText, r.ToString(cultureInfo2 ?? cultureInfo));
		} //proc TestDateFilter

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
			var cultureInfo = CultureInfo.GetCultureInfo("de-DE");
			TestDateFilter("#2010#", new DateTime(2010, 1, 1), new DateTime(2011, 1, 1), "2010", cultureInfo);
			TestDateFilter("#2010#", new DateTime(2010, 1, 1), new DateTime(2011, 1, 1), "..2010", cultureInfo);
			TestDateFilter("#2010#", new DateTime(2010, 1, 1), new DateTime(2011, 1, 1), "01.01.2010~31.12.2010", cultureInfo);
			TestDateFilter("#01.10.2010#", new DateTime(2010, 10, 1), new DateTime(2010, 10, 2), "01.10.2010", cultureInfo);

			TestDateFilter("#01.2010#", new DateTime(2010, 1, 1), new DateTime(2010, 2, 1), "01.2010", cultureInfo);
			TestDateFilter("#30.04.2010#", new DateTime(2010, 4, 30), new DateTime(2010, 5, 1), "31.04.2010", cultureInfo);
			TestDateFilter("#2010~2012#", new DateTime(2010, 1, 1), new DateTime(2013, 1, 1), "2010~2012", cultureInfo);
			TestDateFilter("#2010~2012#", new DateTime(2010, 1, 1), new DateTime(2013, 1, 1), "01.01.2010~31.12.2012", cultureInfo);
			
			TestDateFilter("#02.2010~12.2012#", new DateTime(2010, 2, 1), new DateTime(2013, 1, 1), "02.2010~12.2012", cultureInfo);
			TestDateFilter("#01.01.2010~30.11.2010#", new DateTime(2010, 1, 1), new DateTime(2010, 12, 1), "01.01.2010~31.11.2010", cultureInfo);

			TestDateFilter("#01.01.2010T10:22~30.11.2010T20:15#", new DateTime(2010, 1, 1, 10, 22, 0), new DateTime(2010, 11, 30, 20, 16, 0), "01.01.2010T10:22~31.11.2010T20:15", cultureInfo);
			TestDateFilter("#01.01.2010T10:22:12~30.11.2010T20:15:30#", new DateTime(2010, 1, 1, 10, 22, 12), new DateTime(2010, 11, 30, 20, 15, 31), "01.01.2010T10:22:12~31.11.2010T20:15:30", cultureInfo);
			TestDateFilter("#01.01.2010T10:22#", new DateTime(2010, 1, 1, 10, 22, 0), new DateTime(2010, 1, 1, 10, 23, 0), "01.01.2010T10:22", cultureInfo);
			TestDateFilter("#01.01.2010T10:22:00#", new DateTime(2010, 1, 1, 10, 22, 0), new DateTime(2010, 1, 1, 10, 22, 1), "01.01.2010T10:22:00", cultureInfo);
			TestDateFilter("#01.01.2010T10:22:12#", new DateTime(2010, 1, 1, 10, 22, 12), new DateTime(2010, 1, 1, 10, 22, 13), "01.01.2010T10:22:12", cultureInfo);

			TestDateFilter("#~#", DateTime.MinValue, DateTime.MaxValue, "~", cultureInfo);
			TestDateFilter("#0#", DateTime.MaxValue, DateTime.MinValue, "0", cultureInfo);
			//TestDateFilter(String.Format(cultureInfo, "#{0:d}#", DateTime.Now), DateTime.Now.Date, DateTime.Now.Date.AddDays(1), "", cultureInfo);
		}

		[TestMethod]
		public void TestFilter05inv()
		{
			var cultureInfo = CultureInfo.InvariantCulture;

			TestDateFilter("#2010#", new DateTime(2010, 1, 1), new DateTime(2011, 1, 1), "2010", cultureInfo);
			TestDateFilter("#2010#", new DateTime(2010, 1, 1), new DateTime(2011, 1, 1), "//2010", cultureInfo);
			TestDateFilter("#2010#", new DateTime(2010, 1, 1), new DateTime(2011, 1, 1), "01/01/2010~12/31/2010", cultureInfo);
			TestDateFilter("#10/01/2010#", new DateTime(2010, 10, 1), new DateTime(2010, 10, 2), "10/01/2010", cultureInfo);

			TestDateFilter("#01/2010#", new DateTime(2010, 1, 1), new DateTime(2010, 2, 1), "01/2010", cultureInfo);
			TestDateFilter("#04/30/2010#", new DateTime(2010, 4, 30), new DateTime(2010, 5, 1), "04/31/2010", cultureInfo);
			TestDateFilter("#2010~2012#", new DateTime(2010, 1, 1), new DateTime(2013, 1, 1), "2010~2012", cultureInfo);
			TestDateFilter("#2010~2012#", new DateTime(2010, 1, 1), new DateTime(2013, 1, 1), "01/01/2010~12/31/2012", cultureInfo);

			TestDateFilter("#02/2010~12/2012#", new DateTime(2010, 2, 1), new DateTime(2013, 1, 1), "02/2010~12/2012", cultureInfo);
			TestDateFilter("#01/01/2010~11/30/2010#", new DateTime(2010, 1, 1), new DateTime(2010, 12, 1), "01/01/2010~11/31/2010", cultureInfo);
		}

		[TestMethod]
		public void TestFilter05deTOinv()
		{
			var cultureInfo = CultureInfo.GetCultureInfo("de-DE");
			var cultureInfo2 = CultureInfo.InvariantCulture;

			TestDateFilter("#2010#", new DateTime(2010, 1, 1), new DateTime(2011, 1, 1), "2010", cultureInfo, cultureInfo2);
			TestDateFilter("#2010#", new DateTime(2010, 1, 1), new DateTime(2011, 1, 1), "..2010", cultureInfo, cultureInfo2);
			TestDateFilter("#2010#", new DateTime(2010, 1, 1), new DateTime(2011, 1, 1), "01./01.2010~31.12.2010", cultureInfo, cultureInfo2);
			TestDateFilter("#10/01/2010#", new DateTime(2010, 10, 1), new DateTime(2010, 10, 2), "01.10.2010", cultureInfo, cultureInfo2);

			TestDateFilter("#01/2010#", new DateTime(2010, 1, 1), new DateTime(2010, 2, 1), "01.2010", cultureInfo, cultureInfo2);
			TestDateFilter("#04/30/2010#", new DateTime(2010, 4, 30), new DateTime(2010, 5, 1), "31.04.2010", cultureInfo, cultureInfo2);
			TestDateFilter("#2010~2012#", new DateTime(2010, 1, 1), new DateTime(2013, 1, 1), "2010~2012", cultureInfo, cultureInfo2);
			TestDateFilter("#2010~2012#", new DateTime(2010, 1, 1), new DateTime(2013, 1, 1), "01.01.2010~31.12.2012", cultureInfo, cultureInfo2);

			TestDateFilter("#02/2010~12/2012#", new DateTime(2010, 2, 1), new DateTime(2013, 1, 1), "02.2010~12.2012", cultureInfo, cultureInfo2);
			TestDateFilter("#01/01/2010~11/30/2010#", new DateTime(2010, 1, 1), new DateTime(2010, 12, 1), "01.01.2010~31.11.2010", cultureInfo, cultureInfo2);
		}

		[TestMethod]
		public void TestFilter05invTOde()
		{
			var cultureInfo = CultureInfo.InvariantCulture;
			var cultureInfo2 = CultureInfo.GetCultureInfo("de-DE");

			TestDateFilter("#2010#", new DateTime(2010, 1, 1), new DateTime(2011, 1, 1), "2010", cultureInfo, cultureInfo2);
			TestDateFilter("#2010#", new DateTime(2010, 1, 1), new DateTime(2011, 1, 1), "//2010", cultureInfo, cultureInfo2);
			TestDateFilter("#2010#", new DateTime(2010, 1, 1), new DateTime(2011, 1, 1), "01/01/2010~12/31/2010", cultureInfo, cultureInfo2);
			TestDateFilter("#01.10.2010#", new DateTime(2010, 10, 1), new DateTime(2010, 10, 2), "10/01/2010", cultureInfo, cultureInfo2);

			TestDateFilter("#01.2010#", new DateTime(2010, 1, 1), new DateTime(2010, 2, 1), "01/2010", cultureInfo, cultureInfo2);
			TestDateFilter("#30.04.2010#", new DateTime(2010, 4, 30), new DateTime(2010, 5, 1), "04/31/2010", cultureInfo, cultureInfo2);
			TestDateFilter("#2010~2012#", new DateTime(2010, 1, 1), new DateTime(2013, 1, 1), "2010~2012", cultureInfo, cultureInfo2);
			TestDateFilter("#2010~2012#", new DateTime(2010, 1, 1), new DateTime(2013, 1, 1), "01/01/2010~12/31/2012", cultureInfo, cultureInfo2);

			TestDateFilter("#02.2010~12.2012#", new DateTime(2010, 2, 1), new DateTime(2013, 1, 1), "02/2010~12/2012", cultureInfo, cultureInfo2);
			TestDateFilter("#01.01.2010~30.11.2010#", new DateTime(2010, 1, 1), new DateTime(2010, 12, 1), "01/01/2010~11/31/2010", cultureInfo, cultureInfo2);
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

		[TestMethod]
		public void TestInExpr()
		{
			var f = PpsDataFilterExpression.Parse("Int64:(1 2 3 4 5)");
			var p = PpsDataFilterVisitorLambda.CompileTypedFilter<Props>(f);
			var f2 = PpsDataFilterExpression.Parse("or(or(Int64:=1 Int64:=2 Int64:=3 Int64:=4 Int64:=2) Int64:=5)").Reduce();
		}

		[TestMethod]
		public void TestInExpr2()
		{
			var f = PpsDataFilterExpression.Parse("Nr:(01 02 13 24 35)");

			var t = new TextSqlVisitor(new Tuple<string, Type>("Nr", typeof(string)));
			var r = t.CreateFilter(f);
			Assert.AreEqual("Nr IN ('01','02','13','24','35')", r);

			var t2 = new TextSqlVisitor(new Tuple<string, Type>("Nr", typeof(long)));
			var r2 = t2.CreateFilter(f);
			Assert.AreEqual("Nr IN (1,2,13,24,35)", r2);
		}

		private sealed class TextSqlVisitor : PpsDataFilterVisitorSql
		{
			private readonly Tuple<string, Type>[] columns;

			public TextSqlVisitor(params Tuple<string, Type>[] columns)
			{
				this.columns = columns;
			} // ctor

			protected override Tuple<string, Type> LookupColumn(string columnToken)
				=> columns.FirstOrDefault(c => String.Compare(c.Item1, columnToken, StringComparison.OrdinalIgnoreCase) == 0);

			protected override string LookupNativeExpression(string key)
				=> null;
		}

		[TestMethod]
		public void TestSqlVisitor()
		{
			var t = new TextSqlVisitor(
				new Tuple<string, Type>("DD", typeof(DateTime))
			).CreateFilter(PpsDataFilterExpression.Combine(
				PpsDataFilterExpression.Compare("DD", PpsDataFilterCompareOperator.Equal, new DateTime(2010, 5, 26))
			));

			Assert.AreEqual("DD BETWEEN '26.05.2010 00:00:00' AND '26.05.2010 23:59:59'", t);
		}
	}
}
