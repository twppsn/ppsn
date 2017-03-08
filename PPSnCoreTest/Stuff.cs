﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
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
			var extralong = new StringBuilder();
			for (var i = 0; i < 1024; i++)
				extralong.Append("a");

			var everyChar = new StringBuilder();
			for (var i = 0; i < 256; i++)
				everyChar.Append((char)i);

			var strs = new string[] { "Hallo Welt", "", "Hallo Wel", extralong.ToString(), everyChar.ToString() };

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

			fixed (char* s1 = strs[3])
			fixed (char* s2 = strs[3])
			{
				var ss1 = new SecureString(s1, strs[3].Length);
				var ss2 = new SecureString(s2, strs[3].Length); ;

				Assert.IsTrue(PpsProcs.SecureStringCompare(ss1, ss2), "Long strings were not equal");

				ss1.Dispose();
				ss2.Dispose();
			}

			fixed (char* s1 = strs[4])
			fixed (char* s2 = strs[4])
			{
				var ss1 = new SecureString(s1, strs[4].Length);
				var ss2 = new SecureString(s2, strs[4].Length); ;

				Assert.IsTrue(PpsProcs.SecureStringCompare(ss1, ss2), "Full-ASCII strings were not equal");

				ss1.Dispose();
				ss2.Dispose();
			}
		}
	}
}
