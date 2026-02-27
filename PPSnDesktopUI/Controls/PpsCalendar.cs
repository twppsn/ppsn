using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Controls
{
	#region -- class PpsCalendar -------------------------------------------------------

	public class PpsCalendar : Calendar, IPpsNullableControl
	{
		#region -- Ctor ----------------------------------------------------------------
	
		public PpsCalendar() 
		{
		} // ctor

		#endregion

		#region -- IsNullable Property -------------------------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty IsNullableProperty = PpsTextBox.IsNullableProperty.AddOwner(typeof(PpsCalendar), new FrameworkPropertyMetadata(true));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		public void Clear()
		{
			if (SelectedDate.HasValue)
				SelectedDate = null;
			SelectedDates.Clear();
		} // proc Clear

		bool IPpsNullableControl.CanClear => IsEnabled && SelectedDates.Count > 0;

		/// <summary>Is the field nullable.</summary>
		public bool IsNullable { get => BooleanBox.GetBool(GetValue(IsNullableProperty)); set => SetValue(IsNullableProperty, BooleanBox.GetObject(value)); }

		#endregion

		static PpsCalendar()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsCalendar), new FrameworkPropertyMetadata(typeof(PpsCalendar)));
		} // sctor
	} // class PpsCalendar

	#endregion
}
