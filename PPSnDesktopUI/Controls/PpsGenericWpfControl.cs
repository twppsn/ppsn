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
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Neo.IronLua;
using TecWare.DE.Stuff;
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Controls
{
	#region -- class PpsGenericWpfControl ---------------------------------------------

//	/// <summary>Base control for the wpf generic pane.</summary>
//	public class PpsGenericWpfControl : ContentControl, IServiceProvider
//	{
//#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
//		public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(PpsGenericWpfControl), new FrameworkPropertyMetadata("Pane title"));

//		public static readonly DependencyProperty SubTitleProperty = DependencyProperty.Register(nameof(SubTitle), typeof(string), typeof(PpsGenericWpfControl), new FrameworkPropertyMetadata(null));
//		public static readonly DependencyProperty ImageProperty = DependencyProperty.Register(nameof(Image), typeof(object), typeof(PpsGenericWpfControl), new FrameworkPropertyMetadata(null));

//		private static readonly DependencyPropertyKey commandsPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Commands), typeof(PpsUICommandCollection), typeof(PpsGenericWpfControl), new FrameworkPropertyMetadata(null));
//		public static readonly DependencyProperty CommandsProperty = commandsPropertyKey.DependencyProperty;

//		public static readonly DependencyProperty HasSideBarProperty = DependencyProperty.Register(nameof(HasSideBar), typeof(bool), typeof(PpsGenericWpfControl), new FrameworkPropertyMetadata(false));

//#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
		
//		#region -- Ctor/Dtor --------------------------------------------------------------

//		/// <summary></summary>
//		public PpsGenericWpfControl()
//			: base()
//		{
//			// initialize commands
//			var commands = new PpsUICommandCollection
//			{
//				AddLogicalChildHandler = AddLogicalChild,
//				RemoveLogicalChildHandler = RemoveLogicalChild
//			};
//			SetValue(commandsPropertyKey, commands);

//			Focusable = false;
//		} // ctor

//		#endregion

//		/// <summary></summary>
//		protected override System.Collections.IEnumerator LogicalChildren
//			=> Procs.CombineEnumerator(base.LogicalChildren, Commands?.GetEnumerator());

//		/// <summary></summary>
//		/// <param name="serviceType"></param>
//		/// <returns></returns>
//		public object GetService(Type serviceType)
//			=> serviceType.IsAssignableFrom(GetType())
//				? this
//				: Pane?.GetService(serviceType);

//		/// <summary>Access to the owning pane.</summary>
//		public PpsGenericWpfWindowPane Pane
//		{
//			get
//			{
//				if (DataContext is PpsGenericWpfWindowPane pane)
//					return pane;
//				return null;
//			}
//		} // prop Pane

//		/// <summary>Title of the window pane</summary>
//		public string Title { get => (string)GetValue(TitleProperty);  set => SetValue(TitleProperty, value); } 
//		/// <summary>SubTitle of the window pane</summary>
//		public string SubTitle { get => (string)GetValue(SubTitleProperty);  set => SetValue(SubTitleProperty, value); }
//		/// <summary>SubTitle of the window pane</summary>
//		public object Image { get => GetValue(ImageProperty); set => SetValue(ImageProperty, value); }
//		/// <summary>Has this control a sidebar element.</summary>
//		public bool HasSideBar { get => BooleanBox.GetBool(GetValue(HasSideBarProperty)); set => SetValue(HasSideBarProperty, BooleanBox.GetObject(value)); }
	
//		/// <summary>List of commands for the main toolbar.</summary>
//		[DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
//		public PpsUICommandCollection Commands => (PpsUICommandCollection)GetValue(CommandsProperty);
//	} // class PpsGenericWpfControl

	#endregion
}
