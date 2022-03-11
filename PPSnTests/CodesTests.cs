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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TecWare.PPSn.Core.UI.Barcodes.Encoding;

namespace PPSnTests
{
	[TestClass]
	public class CodesTests
	{
		private static void TestCode128(string payload, byte[] expected)
		{
			var actual = Code128.Encode(payload, 0, payload.Length);
			Assert.AreEqual(expected.Length, actual.Length, "Length");
			for (var i = 0; i < expected.Length; i++)
				Assert.AreEqual(expected[i], actual[i]);
		}

		[TestMethod]
		public void Code128Simple()
		{
			TestCode128("PJJ123C", new byte[] { 103, 48, 42, 42, 17, 18, 19, 35, 54, 106 });
			TestCode128("1234", new byte[] { 105, 12, 34, 82, 106 });
			TestCode128("12347", new byte[] { 105, 12, 34, 101, 23, 65, 106 });
			TestCode128("FA12", new byte[] { 103, 38, 33, 17, 18, 21, 106 });
			TestCode128("FA12A", new byte[] { 103, 38, 33, 17, 18, 33, 83, 106 });
			TestCode128("FA123465", new byte[] { 103, 38, 33, 99, 12, 34, 65, 82, 106 });
			TestCode128("FA123465A", new byte[] { 103, 38, 33, 99, 12, 34, 65, 101, 33, 23, 106 });
			TestCode128("FA1234655", new byte[] { 103, 38, 33, 17, 99, 23, 46, 55, 91, 106 });
			TestCode128("FA1234655A4455", new byte[] { 103, 38, 33, 17, 99, 23, 46, 55, 101, 33, 99, 44, 55, 34, 106 });
			TestCode128("FA\t1234", new byte[] { 103, 38, 33, 73, 99, 12, 34, 56, 106 });
			TestCode128("A\tBb\ta", new byte[] { 103, 33, 73, 34, 98, 66, 73, 98, 65, 72, 106 });
		}
		[TestMethod]
		public void Code128Path()
		{
			var s = Code128.ToPath("PJJ123C");
			Assert.AreEqual("M0,0H2V34H0ZM3,0H5V34H3ZM7,0H9V34H7ZM11,0H13V34H11ZM15,0H17V34H15ZM18,0H20V34H18ZM22,0H24V34H22ZM26,0H28V34H26ZM30,0H32V34H30ZM33,0H34V34H33ZM36,0H37V34H36ZM39,0H41V34H39ZM44,0H45V34H44ZM47,0H48V34H47ZM51,0H53V34H51ZM55,0H56V34H55ZM59,0H60V34H59ZM62,0H64V34H62ZM66,0H67V34H66ZM69,0H71V34H69ZM73,0H74V34H73ZM77,0H78V34H77ZM80,0H82V34H80ZM85,0H86V34H85ZM88,0H89V34H88ZM92,0H94V34H92ZM96,0H97V34H96ZM99,0H101V34H99ZM103,0H104V34H103ZM106,0H107V34H106ZM110,0H112V34H110Z", s);
		}
	}
}
