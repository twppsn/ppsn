using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace TecWare.PPSn.Controls
{
	#region -- class PpsCalendar -------------------------------------------------------
	class PpsCalendar : Calendar, IPpsNullableControl
	{
		#region -- Ctor ----------------------------------------------------------------
		public PpsCalendar() 
		{
			SelectionMode = CalendarSelectionMode.MultipleRange; // Wir wollen alle Urlaubsblöcke anzeigen
		} // ctor
		#endregion

		#region -- IsNullable Property -------------------------------------------------
		public bool CanClear => IsEnabled;

		public bool IsNullable => true;

		public void Clear()
		{
			BlackoutDates.Clear();
			if (SelectedDate.HasValue)
				SelectedDate = null;
			SelectedDates.Clear();
		} // proc Clear
		#endregion
	} // class PpsCalendar
	#endregion
}
