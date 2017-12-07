using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace TecWare.PPSn
{
	internal class UnsafeNativeMethods
	{
		[DllImport("Kernel32.dll", EntryPoint = "RtlZeroMemory", SetLastError = false)]
		public static extern void ZeroMemory(IntPtr dest, int size);
		[DllImport("kernel32.dll", SetLastError = false)]
		public static extern void CopyMemory(IntPtr dest, IntPtr src, int count);
	}
}
