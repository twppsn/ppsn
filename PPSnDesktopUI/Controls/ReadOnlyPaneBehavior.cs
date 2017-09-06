using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace TecWare.PPSn.Controls
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PPSnReadOnlyPaneBehavior
	{
		//From MSDN:
		//Note, however, that designating a property as inheritable does have some performance considerations.
		//In cases where that property does not have an established local value, or a value obtained through styles, templates, or data binding,
		//an inheritable property provides its assigned property values to all child elements in the logical tree
		public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.RegisterAttached(
			"IsReadOnly",
			typeof(bool), 
			typeof(PPSnReadOnlyPaneBehavior),
			new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits, ReadOnlyPropertyChanged));

		public static bool GetIsReadOnly(DependencyObject o)
		{
			return (bool)o.GetValue(IsReadOnlyProperty);
		}

		public static void SetIsReadOnly(DependencyObject o, bool value)
		{
			o.SetValue(IsReadOnlyProperty, value);
		}

		private static void ReadOnlyPropertyChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
		{
			if (o is TextBox)
			{
				((TextBox)o).IsReadOnly = (bool)e.NewValue;
			}
		}
	} // class PPSnReadOnlyPaneBehavior
}
