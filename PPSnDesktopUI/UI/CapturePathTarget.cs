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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.UI
{
	#region -- class PpsCapturePathTarget ---------------------------------------------

	/// <summary></summary>
	public sealed class PpsCapturePathTarget : IPpsCaptureTarget
	{
		private readonly string path;
		private readonly string prefix;

		/// <summary></summary>
		/// <param name="path"></param>
		/// <param name="prefix"></param>
		public PpsCapturePathTarget(string path, string prefix)
		{
			this.path = path ?? throw new ArgumentNullException(nameof(path));
			this.prefix = prefix;
		} // ctor

		public async Task AppendAsync(object capture)
		{
			await Task.Run(() =>
			{
				var fileName = Path.Combine(path, (prefix ?? DateTime.Now.ToString("yyyy-MM-dd_HHmmss")) + ".jpg");
				using (var dst = new FileStream(Procs.GetUniqueFileName(fileName), FileMode.CreateNew))
				{
					var encoder = new JpegBitmapEncoder();
					encoder.Frames.Add(BitmapFrame.Create((BitmapSource)capture));
					encoder.Save(dst);
					dst.Flush();
				}
			});
		} // proc AppendAsync
	} // class PpsCapturePathTarget

	#endregion
}
