using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using TecWare.PPSn.Stuff;

namespace TecWare.PPSn
{
	[TestClass]
	public class StuffTests
	{

		[TestMethod]
		public unsafe void CompareSecureString()
		{
			var strs = new string[] { "Hallo Welt", "Hallo Welt" };

			fixed (char* s1 = strs[0])
			fixed(char* s2 = strs[1])
			{
				var ss1 = new SecureString(s1, strs[0].Length);
				var ss2 = new SecureString(s2, strs[1].Length);

				Assert.IsTrue(PpsProcs.SecureStringCompare(ss1, ss2));

				ss1.Dispose();
				ss2.Dispose();
			}
		}
	}
}
