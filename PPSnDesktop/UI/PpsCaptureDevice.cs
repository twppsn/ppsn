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
	PpsService(typeof(IPpsCaptureService))
	]
	internal class PpsCaptureService : IPpsCaptureService
	{
		public Task<object> CaptureAsync(object owner, PpsCaptureDevice device, IPpsCaptureTarget target)
		{
			switch (device)
			{
				case PpsCaptureDevice.Camera:
					return Task.FromResult(TakePicture(owner, target));
				default:
					throw new NotSupportedException();
			}
		} // func CaputureAsync

		private object TakePicture(object owner, IPpsCaptureTarget target)
		{
			var result = PpsCameraDialog.TakePicture(owner as DependencyObject, target);
			if (result == null)
				return null;
			else if (target != null)
				return target;
			else
				return result;
		} // func TakePicture

		public bool IsSupported(PpsCaptureDevice device)
			=> device == PpsCaptureDevice.Camera;
	} // PpsCaptureService

	#endregion
}
