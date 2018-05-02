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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TecWare.PPSn.Controls;

namespace TecWare.PPSn
{
	[TestClass]
	public class PpsTextBoxTests
	{
		private static readonly string[] singleLineText = new string[] { "anytext", "any\atext" };
		private static readonly string[] twoLineText = new string[] { "any\ntext", "any\r\ntext", "any\n\rtext" };
		private static readonly string[] threeLineText = new string[] { "any\n\ntext", "any\r\n\r\ntext", "any\n\r\n\rtext" };
		private static readonly string[] integerText = new string[] { "0","1", "1234", $"1{CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator}234", Int32.MaxValue.ToString() };
		private static readonly string[] integerNegativeText = new string[] { "0", "-1", "-1234", $"-1{CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator}234", Int32.MinValue.ToString() };
		private static readonly string[] decimalText = new string[] { "0,0", "0,1", "1,234", $"1{CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator}234,1"};
		private static readonly string[] decimalNegativeText = new string[] { "0,0", "-0,1", "-1,234", $"-1{CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator}234,1" };
		private static readonly string[] numberText = new string[] { "007" };

		#region ---- Input is matching InputType ----------------------------------------

		[TestMethod]
		public void MatchingNone()
		{
			foreach (var input in singleLineText.Union(twoLineText).Union(threeLineText))
			{
				var error = String.Empty;

				var output = PpsTextBox.NeatlyCleanText(PpsTextBoxInputType.None, input, 1, out error);

				Assert.AreEqual(input, output);
				Assert.IsTrue(String.IsNullOrEmpty(error));
			}
		}

		[TestMethod]
		public void MatchingSingleLine()
		{
			foreach (var input in singleLineText)
			{
				var error = String.Empty;

				var output = PpsTextBox.NeatlyCleanText(PpsTextBoxInputType.SingleLine, input, 1, out error);

				Assert.AreEqual(input, output);
				Assert.IsTrue(String.IsNullOrEmpty(error));
			}
		}

		[TestMethod]
		public void MatchingMultiLine()
		{
			foreach (var input in singleLineText)
			{
				var error = String.Empty;

				var output = PpsTextBox.NeatlyCleanText(PpsTextBoxInputType.MultiLine, input, 1, out error);

				Assert.AreEqual(input, output,"Single line");
				Assert.IsTrue(String.IsNullOrEmpty(error));
			}

			foreach (var input in singleLineText.Union(twoLineText))
			{
				var error = String.Empty;

				var output = PpsTextBox.NeatlyCleanText(PpsTextBoxInputType.MultiLine, input, 2, out error);

				Assert.AreEqual(input, output, "Two lines");
				Assert.IsTrue(String.IsNullOrEmpty(error));
			}

			foreach (var input in  singleLineText.Union(twoLineText).Union(threeLineText) )
			{
				var error = String.Empty;

				var output = PpsTextBox.NeatlyCleanText(PpsTextBoxInputType.MultiLine, input, 3, out error);

				Assert.AreEqual(input, output, "Three lines");
				Assert.IsTrue(String.IsNullOrEmpty(error));
			}
		}

		[TestMethod]
		public void MatchingInteger()
		{
			foreach (var input in integerText)
			{
				var error = String.Empty;

				var output = PpsTextBox.NeatlyCleanText(PpsTextBoxInputType.Integer, input, 1, out error);

				Assert.AreEqual(input, output);
				Assert.IsTrue(String.IsNullOrEmpty(error));
			}
		}

		[TestMethod]
		public void MatchingIntegerNegative()
		{
			foreach (var input in integerText.Union(integerNegativeText))
			{
				var error = String.Empty;

				var output = PpsTextBox.NeatlyCleanText(PpsTextBoxInputType.IntegerNegative, input, 1, out error);

				Assert.AreEqual(input, output);
				Assert.IsTrue(String.IsNullOrEmpty(error));
			}
		}

		[TestMethod]
		public void MatchingDecimal()
		{
			foreach (var input in integerText.Union(decimalText))
			{
				var error = String.Empty;

				var output = PpsTextBox.NeatlyCleanText(PpsTextBoxInputType.Decimal, input, 1, out error);

				Assert.AreEqual(input, output);
				Assert.IsTrue(String.IsNullOrEmpty(error));
			}
		}

		[TestMethod]
		public void MatchingDecimalNegative()
		{
			foreach (var input in integerText.Union(integerNegativeText).Union(decimalText).Union(decimalNegativeText))
			{
				var error = String.Empty;
				
				var output = PpsTextBox.NeatlyCleanText(PpsTextBoxInputType.DecimalNegative, input, 1, out error);

				Assert.AreEqual(input, output);
				Assert.IsTrue(String.IsNullOrEmpty(error));
			}
		}

		[TestMethod]
		public void MatchingNumber()
		{
			foreach (var input in integerText.Union(numberText))
			{
				var error = String.Empty;

				var output = PpsTextBox.NeatlyCleanText(PpsTextBoxInputType.Number, input, 1, out error);

				Assert.AreEqual(input, output);
				Assert.IsTrue(String.IsNullOrEmpty(error));
			}
		}

		#endregion Input is matching InputType

		#region ---- Input mismatches InputType -----------------------------------------

		[TestMethod]
		public void MisMatchingSingleLine()
		{
			foreach (var input in twoLineText.Union(threeLineText))
			{
				var error = String.Empty;

				var output = PpsTextBox.NeatlyCleanText(PpsTextBoxInputType.SingleLine, input, 1, out error);

				Assert.AreEqual(input.Replace("\n", String.Empty).Replace("\r",String.Empty), output);
				Assert.IsFalse(String.IsNullOrEmpty(error), "No Information returned.");
			}
		}

		[TestMethod]
		public void MisMatchingMultiLine1()
		{
			foreach (var input in twoLineText.Union(threeLineText))
			{
				var error = String.Empty;

				var output = PpsTextBox.NeatlyCleanText(PpsTextBoxInputType.MultiLine, input, 1, out error);

				Assert.AreEqual(input.Replace("\n", String.Empty).Replace("\r", String.Empty), output);
				Assert.IsFalse(String.IsNullOrEmpty(error), "No Information returned.");
			}
		}

		[TestMethod]
		public void MisMatchingMultiLine2()
		{
			foreach (var input in threeLineText)
			{
				var error = String.Empty;

				var output = PpsTextBox.NeatlyCleanText(PpsTextBoxInputType.MultiLine, input, 2, out error);

				Assert.AreEqual(input.Where(c=>c=='\n').Count()-1, output.Where(c => c == '\n').Count());
				if (input.Where(c => c == '\r').Count()>0)
					Assert.AreEqual(input.Where(c => c == '\r').Count() - 1, output.Where(c => c == '\r').Count());
				Assert.IsFalse(String.IsNullOrEmpty(error), "No Information returned.");
			}
		}

		[TestMethod]
		public void MisMatchingNumberField()
		{
			foreach (var input in singleLineText.Union(twoLineText).Union(threeLineText))
				foreach(var inputType in new PpsTextBoxInputType[] { PpsTextBoxInputType.Decimal, PpsTextBoxInputType.DecimalNegative,PpsTextBoxInputType.Integer,PpsTextBoxInputType.IntegerNegative,PpsTextBoxInputType.Number})
			{
				var error = String.Empty;

				var output = PpsTextBox.NeatlyCleanText(PpsTextBoxInputType.Integer, input, 1, out error);

				Assert.AreEqual("0", output);
				Assert.IsFalse(String.IsNullOrEmpty(error), "No Information returned.");
			}
		}

		[TestMethod]
		public void MisMatchingInteger()
		{
			foreach (var input in integerNegativeText.Union(decimalText).Union(decimalNegativeText))
			{
				var error = String.Empty;

				var output = PpsTextBox.NeatlyCleanText(PpsTextBoxInputType.Integer, input, 1, out error);

				// lazy clean
				var cinput = input.Replace("-", String.Empty);
				cinput = cinput.Replace(",", String.Empty);
				while (cinput.Length>1&&cinput[0] == '0')
					cinput= cinput.Remove(0, 1);

				Assert.AreEqual(cinput, output);
				if (input != "0")
				Assert.IsFalse(String.IsNullOrEmpty(error), "No Information returned.");
			}
		}

		[TestMethod]
		public void MisMatchingIntegerNegative()
		{
			foreach (var input in decimalText)
			{
				var error = String.Empty;

				var output = PpsTextBox.NeatlyCleanText(PpsTextBoxInputType.IntegerNegative, input, 1, out error);

				// lazy clean
				var cinput = input.Replace(",", String.Empty);
				while (cinput.Length > 1 && cinput[0] == '0')
					cinput = cinput.Remove(0, 1);

				Assert.AreEqual(cinput, output);
				if (input != "0")
					Assert.IsFalse(String.IsNullOrEmpty(error), "No Information returned.");
			}
		}

		[TestMethod]
		public void MisMatchingDecimal()
		{
			foreach (var input in decimalNegativeText)
			{
				var error = String.Empty;

				var output = PpsTextBox.NeatlyCleanText(PpsTextBoxInputType.Decimal, input, 1, out error);

				// lazy clean
				var cinput = input.Replace("-", String.Empty);
				while (cinput.Length > 1 && cinput[1]!=',' && cinput[0] == '0')
					cinput = cinput.Remove(0, 1);

				Assert.AreEqual(cinput, output);
				if (input != "0" && input != "0,0")
					Assert.IsFalse(String.IsNullOrEmpty(error), "No Information returned.");
			}
		}

		[TestMethod]
		public void MisMatchingNumber()
		{
			foreach (var input in integerNegativeText.Union(decimalText).Union(decimalNegativeText))
			{
				var error = String.Empty;

				var output = PpsTextBox.NeatlyCleanText(PpsTextBoxInputType.Number, input, 1, out error);

				// lazy clean
				var cinput = input.Replace("-", String.Empty);
				cinput = cinput.Replace(",", String.Empty);

				Assert.AreEqual(cinput, output);
				if (input != "0" && input != "0,0")
					Assert.IsFalse(String.IsNullOrEmpty(error), "No Information returned.");
			}
		}


		#endregion Input mismatches InputType
	}
}
