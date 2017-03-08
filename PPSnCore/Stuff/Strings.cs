using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace TecWare.PPSn.Stuff
{
	public static partial class PpsProcs
	{
		public unsafe static bool SecureStringCompare(SecureString ss1, SecureString ss2)
		{
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
		}
	}
}
