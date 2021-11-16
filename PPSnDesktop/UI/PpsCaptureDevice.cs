using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace TecWare.PPSn.UI
{
	#region -- class PpsCaptureService ------------------------------------------------

	[
	PpsService(typeof(PpsDpcService))
	]
	internal class PpsCaptureService : IPpsCaptureService
	{
		public Task<object> CaputureAsync(object owner, PpsCaptureDevice device)
		{
			return Task.FromResult<object>(PpsCameraDialog.TakePicture(owner as DependencyObject));
		} // func CaputureAsync
	} // PpsCaptureService

	#endregion
}
