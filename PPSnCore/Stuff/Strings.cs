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
using System.Runtime.InteropServices;
using System.Security;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.Stuff
{
	/// <summary></summary>
	public static partial class ProcsPps
	{
		#region -- SecureStringCompare ------------------------------------------------

		/// <summary></summary>
		/// <param name="ss1"></param>
		/// <param name="ss2"></param>
		/// <returns></returns>
		public unsafe static bool SecureStringCompare(SecureString ss1, SecureString ss2)
		{
			if (ss1 == null || ss2 == null)
				return false;

			var bstr1 = IntPtr.Zero;
			var bstr2 = IntPtr.Zero;
			try
			{
				bstr1 = Marshal.SecureStringToBSTR(ss1);
				bstr2 = Marshal.SecureStringToBSTR(ss2);
				var length1 = *((int*)bstr1.ToPointer() - 1);
				var length2 = *((int*)bstr2.ToPointer() - 1);
				
				if (length1 == length2)
				{
					var c1 = (char*)bstr1.ToPointer();
					var c2 = (char*)bstr2.ToPointer();
					var c1end = (char*)((byte*)bstr1.ToPointer() + length1);
					var c2end = (char*)((byte*)bstr2.ToPointer() + length1);

					var r = true;
					while (c1 < c1end)
					{
						r = r && *c1 == *c2;
						c1++;
						c2++;
					}

					return r;
				}
				else
					return false;
			}
			finally
			{
				if (bstr2 != IntPtr.Zero)
					Marshal.ZeroFreeBSTR(bstr2);
				if (bstr1 != IntPtr.Zero)
					Marshal.ZeroFreeBSTR(bstr1);
			}
		} // func SecureStringCompare

		#endregion

		#region -- ToString -----------------------------------------------------------

		private static string ReplaceNewLine(string fmt)
			=> fmt.Replace("\\n", Environment.NewLine);

		/// <summary>Format to an string.</summary>
		/// <param name="value"></param>
		/// <param name="fmt"></param>
		/// <returns></returns>
		public static string ToString(this object value, string fmt)
			=> ToString(value, fmt, CultureInfo.CurrentCulture);

		/// <summary>Format to an string.</summary>
		/// <param name="value"></param>
		/// <param name="fmt"></param>
		/// <param name="cultureInfo"></param>
		/// <returns></returns>
		public static string ToString(this object value, string fmt, CultureInfo cultureInfo)
		{
			if (value == null)
				return null;
			else if (fmt == null)
				return Convert.ToString(value, cultureInfo);
			else
			{
				if (fmt.StartsWith("{}"))
					return String.Format(ReplaceNewLine(fmt.Substring(2)), value);
				else
				{
					switch (Type.GetTypeCode(value.GetType()))
					{
						case TypeCode.SByte:
						case TypeCode.Byte:
						case TypeCode.Int16:
						case TypeCode.UInt16:
						case TypeCode.Int32:
						case TypeCode.UInt32:
						case TypeCode.Int64:
						case TypeCode.UInt64:
							return value.ChangeType<long>().ToString(fmt, cultureInfo);
						case TypeCode.DateTime:
							return ((DateTime)value).ToString(fmt, cultureInfo);
						case TypeCode.Decimal:
							return ((decimal)value).ToString(fmt, cultureInfo);
						case TypeCode.Double:
							return ((double)value).ToString(fmt, cultureInfo);
						case TypeCode.Single:
							return ((float)value).ToString(fmt, cultureInfo);

						default:
							return value is IFormattable f
								? f.ToString(fmt, cultureInfo)
								: value.ToString();
					}
				}
			}
		} // func ToString

		#endregion
	} // class ProcsPps
}
