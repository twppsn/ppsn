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
		}
		
		public static string GeneratePassword(int length, char[] validChars)
		{
			var ret = String.Empty;

			using (var secureRandomNumberGenerator = System.Security.Cryptography.RandomNumberGenerator.Create())
				while (ret.Length < length)
				{
					var buffer = new byte[128];
					secureRandomNumberGenerator.GetBytes(buffer);
					foreach (char chr in buffer)
						if (ret.Length < length && validChars.Contains(chr))
							ret += chr;
				}

			return ret;
		}

		public static string StringCypher(string input)
		{
			var ch = 'r';
			var ret = String.Empty;
			for (var i = 0; i < input.Length; i++)
			{
				var ch_ = (char)(input[i] ^ ch);
				ret += ch_;
				ch = ch_;
			}
			return ret;
		}

		public static string StringDecypher(string input)
		{
			var ret = String.Empty;

			if (String.IsNullOrWhiteSpace(input))
				return ret;

			for (var i = input.Length - 1; i > 0; i--)
				ret = (char)(input[i] ^ input[i - 1]) + ret;

			ret = ((char)(input[0] ^ 'r')) + ret;
			return ret;
		}
	}
}
