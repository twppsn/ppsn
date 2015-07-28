using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using TecWare.PPSn.UI;

namespace TecWare.PPSn
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsMainEnvironment : PpsEnvironment
	{
		public PpsMainEnvironment(Uri remoteUri, ResourceDictionary mainResources)
			: base(remoteUri, mainResources)
		{
		} // ctor

		public void CreateMainWindow()
		{
			Dispatcher.Invoke(() =>
				{
					// find a free window index
					var freeIndex = 0;
					while (GetWindow(freeIndex) != null)
						freeIndex++;

					var window = new PpsMainWindow(freeIndex);
					window.Show();
				});
		} // proc CreateMainWindow

		public PpsMainWindow GetWindow(int index)
		{
			foreach (var c in Application.Current.Windows)
			{
				var w = c as PpsMainWindow;
				if (w != null && w.WindowIndex == index)
					return w;
			}
			return null;
		} // func GetWindow
	} // class PpsMainEnvironment
}
