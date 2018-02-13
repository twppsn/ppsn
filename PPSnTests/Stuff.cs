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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

				Assert.IsTrue(ProcsPps.SecureStringCompare(ss1, ss2), "Same strings were not equal");

				ss1.Dispose();
				ss2.Dispose();
			}

			fixed (char* s1 = strs[1])
			fixed (char* s2 = strs[1])
			{
				var ss1 = new SecureString(s1, strs[1].Length);
				var ss2 = new SecureString(s2, strs[1].Length);

				Assert.IsTrue(ProcsPps.SecureStringCompare(ss1, ss2), "Empty strings were not equal");

				ss1.Dispose();
				ss2.Dispose();
			}


			fixed (char* s1 = strs[0])
			fixed (char* s2 = strs[1])
			{
				var ss1 = new SecureString(s1, strs[0].Length);
				var ss2 = new SecureString(s2, strs[1].Length);

				Assert.IsFalse(ProcsPps.SecureStringCompare(ss1, ss2), "String and empty string were equal");

				ss1.Dispose();
				ss2.Dispose();
			}

			fixed (char* s1 = strs[0])
			fixed (char* s2 = strs[2])
			{
				var ss1 = new SecureString(s1, strs[0].Length);
				var ss2 = new SecureString(s2, strs[2].Length);

				Assert.IsFalse(ProcsPps.SecureStringCompare(ss1, ss2), "String and Substring were equal");

				ss1.Dispose();
				ss2.Dispose();
			}

			fixed (char* s1 = strs[0])
			fixed (char* s2 = strs[2])
			{
				var ss1 = new SecureString(s1, strs[0].Length);
				SecureString ss2 = null;

				Assert.IsFalse(ProcsPps.SecureStringCompare(ss1, ss2), "String and null were equal");

				ss1.Dispose();
			}

			fixed (char* s1 = strs[3])
			fixed (char* s2 = strs[3])
			{
				var ss1 = new SecureString(s1, strs[3].Length);
				var ss2 = new SecureString(s2, strs[3].Length); ;

				Assert.IsTrue(ProcsPps.SecureStringCompare(ss1, ss2), "Long strings were not equal");

				ss1.Dispose();
				ss2.Dispose();
			}

			fixed (char* s1 = strs[4])
			fixed (char* s2 = strs[4])
			{
				var ss1 = new SecureString(s1, strs[4].Length);
				var ss2 = new SecureString(s2, strs[4].Length); ;

				Assert.IsTrue(ProcsPps.SecureStringCompare(ss1, ss2), "Full-ASCII strings were not equal");

				ss1.Dispose();
				ss2.Dispose();
			}
		}

		[TestMethod]
		public void DbPasswordStoreTest()
		{
			for (var i = 0; i < 100; i++)
			{
				var pwd = PpsEnvironment.GenerateSqlitePassword();
				Console.WriteLine("Password: {0}", pwd);
				var encrypted = PpsEnvironment.EncryptSqlitePassword(pwd);
				var pwd2 = PpsEnvironment.DecryptSqlitePassword(encrypted);
				Assert.AreEqual(pwd, pwd2);
			}
		}
	}
}
