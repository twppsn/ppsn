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
			var strs = new string[] { "Hallo Welt", "", "Hallo Wel" };

			fixed (char* s1 = strs[0])
			fixed (char* s2 = strs[0])
			{
				var ss1 = new SecureString(s1, strs[0].Length);
				var ss2 = new SecureString(s2, strs[0].Length);

				Assert.IsTrue(PpsProcs.SecureStringCompare(ss1, ss2), "Same strings were not equal");

				ss1.Dispose();
				ss2.Dispose();
			}

			fixed (char* s1 = strs[1])
			fixed (char* s2 = strs[1])
			{
				var ss1 = new SecureString(s1, strs[1].Length);
				var ss2 = new SecureString(s2, strs[1].Length);

				Assert.IsTrue(PpsProcs.SecureStringCompare(ss1, ss2), "Empty strings were not equal");

				ss1.Dispose();
				ss2.Dispose();
			}


			fixed (char* s1 = strs[0])
			fixed (char* s2 = strs[1])
			{
				var ss1 = new SecureString(s1, strs[0].Length);
				var ss2 = new SecureString(s2, strs[1].Length);

				Assert.IsFalse(PpsProcs.SecureStringCompare(ss1, ss2), "String and empty string were equal");

				ss1.Dispose();
				ss2.Dispose();
			}

			fixed (char* s1 = strs[0])
			fixed (char* s2 = strs[2])
			{
				var ss1 = new SecureString(s1, strs[0].Length);
				var ss2 = new SecureString(s2, strs[2].Length);

				Assert.IsFalse(PpsProcs.SecureStringCompare(ss1, ss2), "String and Substring were equal");

				ss1.Dispose();
				ss2.Dispose();
			}

			fixed (char* s1 = strs[0])
			fixed (char* s2 = strs[2])
			{
				var ss1 = new SecureString(s1, strs[0].Length);
				SecureString ss2 = null;

				Assert.IsFalse(PpsProcs.SecureStringCompare(ss1, ss2), "String and null were equal");

				ss1.Dispose();
			}
		}
	}
}
