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
using AForge.Video.DirectShow;

namespace TecWare.PPSn.Controls
{
	/// <summary>This Class adds a German Translation to the Enum of CameraControlProperty</summary>
	public static class CameraControlPropertyExtensions
	{
		private static string ToGermanString(this CameraControlProperty property)
		{
			switch (property)
			{
				case CameraControlProperty.Exposure:
					return "Belichtung";
				case CameraControlProperty.Focus:
					return "Fokus";
				case CameraControlProperty.Iris:
					return "Blende";
				case CameraControlProperty.Pan:
					return "Schwenkung";
				case CameraControlProperty.Roll:
					return "Rotation";
				case CameraControlProperty.Tilt:
					return "Neigung";
				case CameraControlProperty.Zoom:
					return "Vergrößerung";
				default:
					return Enum.GetName(typeof(CameraControlProperty), property);
			}
		}

		/// <summary>Returns the Name of the Enum Item</summary>
		/// <param name="property">Property to print</param>
		/// <param name="toGerman">default false, if true the German string is returned</param>
		/// <returns>Coded Name as a string</returns>
		public static string ToString(this CameraControlProperty property, bool toGerman = false)
		{
			if (toGerman)
				return ToGermanString(property);
			return Enum.GetName(typeof(CameraControlProperty), property);
		}
	}

	class PpsCameraPane
	{
		#region -- IPpsWindowPane -----------------------------------------------------



		/// <summary>the Pane has hardware handles to dispose</summary>
		public void Dispose()
		{
			//CameraEnum.Dispose();
			//PaneManager.SiteBar?.UpdateObjectInfo(null);
		} // proc Dispose


		#endregion

	}
}
