using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TecWare.DE.Stuff;

namespace PPSnDesktopUnitTest
{
	[TestClass]
	public class DESCoreStrings
	{
		[TestMethod]
		public void TestStringEscape()
		{
			Assert.AreEqual("Hello\\nWorld\\nHow are\\tyou!", Procs.EscapeSpecialChars("Hello\nWorld\r\nHow are\tyou!"));
		}

		[TestMethod]
		public void TestStringUnescape()
		{
			Assert.AreEqual("Hello\nWorld\nHow are\tyou!", Procs.UnescapeSpecialChars("Hello\\nWorld\\nHow are\\tyou!"));
		}
	} // class DESCoreStrings
}
