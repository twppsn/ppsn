using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TecWare.PPSn
{
	internal partial class NativeMethods
	{
		public static Point MakePoints(IntPtr lparam)
		{
			unchecked
			{
				var lp = lparam.ToInt32();
				return new Point(
					(ushort)lp,
					(ushort)(lp >> 16)
				);
			}
		} // func GetLParam
	} // class NativeMethods
}
