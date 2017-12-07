using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TecWare.PPSn.Data;

namespace PPSnDesktopUnitTest
{
	[TestClass]
	public class FormattedStringParserTests
	{
		private sealed class FmtBlock : PpsFormattedStringValue.ExpressionBlock
		{
			private readonly string code;
			private readonly string fmt;

			public FmtBlock(string code, string fmt)
			{
				this.code = code;
				this.fmt = fmt;
			}

			public override string ToString(PpsLuaRowEnvironment env)
				=> code + "::" + fmt;
		}


		private void AssertExpr(string expr, params string[] expected)
		{
			var spans = PpsFormattedStringValue.ParseTemplate(expr, (c, f) => new FmtBlock(c, f));
			for (var i = 0; i < expected.Length; i++)
			{
				if (i < spans.Length)
				{
					var e = expected[i];
					var a = spans[i].ToString(null);
					Assert.AreEqual(e, a, $"Span[{i}] failed.");
				}
				else
					Assert.Fail("Not enough spans.");
			}
			if (expected.Length < spans.Length)
				Assert.Fail("More expected span.");
		} // proc Assert

		[TestMethod]
		public void TestParser()
		{
			//AssertExpr("Hallo Welt", "Hallo Welt");
			//AssertExpr("Hall% Welt", "Hall% Welt");
			AssertExpr("Hall%% zahl %2::N0 %%Welt", "Hall", "zahl %2::N0", "Welt");
		}
	}
}
