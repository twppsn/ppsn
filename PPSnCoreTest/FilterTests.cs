using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
	}
}
