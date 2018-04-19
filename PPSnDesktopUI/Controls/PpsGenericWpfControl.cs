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
using System.Windows.Input;
using Neo.IronLua;
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Controls
{
	#region -- class PpsGenericWpfControl ---------------------------------------------

	/// <summary>Base control for the wpf generic pane.</summary>
	public class PpsGenericWpfControl : ContentControl, IServiceProvider
	{
		/// <summary></summary>
		public static DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(PpsGenericWpfControl), new FrameworkPropertyMetadata("Pane title"));
		/// <summary></summary>
		public static DependencyProperty SubTitleProperty = DependencyProperty.Register(nameof(SubTitle), typeof(string), typeof(PpsGenericWpfControl), new FrameworkPropertyMetadata(null));

		/// <summary>Has this pane a sidebar on left side.</summary>
		public static readonly DependencyProperty HasSideBarProperty = DependencyProperty.Register(nameof(HasSideBar), typeof(bool), typeof(PpsGenericWpfControl), new FrameworkPropertyMetadata(false));
		private static readonly DependencyPropertyKey commandsPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Commands), typeof(PpsUICommandCollection), typeof(PpsGenericWpfControl), new FrameworkPropertyMetadata(null));
		
		/// <summary>Command list.</summary>
		public static readonly DependencyProperty CommandsProperty = commandsPropertyKey.DependencyProperty;

		private readonly PpsProgressStack progressStack;

		// for corrent Binding this Command must be a Property - not a Field
		// todo: change, Interface and flags for the current options
		private static readonly RoutedCommand setCharmCommand = new RoutedCommand("SetCharm", typeof(PpsGenericWpfControl));
		
		public RoutedCommand SetCharmCommand { get { return setCharmCommand; } }

		#region -- Ctor/Dtor --------------------------------------------------------------

		/// <summary></summary>
		public PpsGenericWpfControl()
			: base()
		{
			// initialize commands
			var commands = new PpsUICommandCollection();
			commands.CollectionChanged += Commands_CollectionChanged;
			SetValue(commandsPropertyKey, commands);

			progressStack = new PpsProgressStack(Dispatcher);

			Focusable = false;

			CommandBindings.Add(
				new CommandBinding(setCharmCommand,
					(sender, e) =>
					{
						((dynamic)Pane.PaneManager).CharmObject = ((LuaTable)((LuaTable)this.DataContext)["Arguments"])["Object"] ?? ((LuaTable)this.DataContext)["Object"]; // must be dynamic - type PpsMainWindow would need a reference to PPSnDesktop which is forbidden
					},
					(sender, e) =>
					{
						e.CanExecute = true;
					}
				)
			);

			// set the initial Object for the CharmBar
			DataContextChanged += (sender, e) =>
			{
				if ((DataContext != null) && (Pane != null))
					((dynamic)Pane.PaneManager).CharmObject = ((LuaTable)((LuaTable)this.DataContext)["Arguments"])["Object"] ?? ((LuaTable)this.DataContext)["Object"]; // must be dynamic - type PpsMainWindow would need a reference to PPSnDesktop which is forbidden
			};
		} // ctor

		#endregion

		#region -- Command Handling -------------------------------------------------------

		private void Commands_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			// todo: add command bar collection in logical tree?
			switch (e.Action)
			{
				case NotifyCollectionChangedAction.Add:
					if (e.NewItems[0] != null)
						AddLogicalChild(e.NewItems[0]);
					break;
				case NotifyCollectionChangedAction.Remove:
					if (e.OldItems[0] != null)
						RemoveLogicalChild(e.OldItems[0]);
					break;
				case NotifyCollectionChangedAction.Reset:
					break;
				default:
					throw new InvalidOperationException();
			}
		} // proc Commands_CollectionChanged

		/// <summary></summary>
		protected override System.Collections.IEnumerator LogicalChildren
		{
			get
			{
				// enumerate normal children
				var e = base.LogicalChildren;
				while (e.MoveNext())
					yield return e.Current;
								
				// enumerate commands
				foreach (var cmd in Commands)
				{
					if (cmd != null)
						yield return cmd;
				}
			}
		} // prop LogicalChildren

		#endregion

		/// <summary></summary>
		/// <param name="serviceType"></param>
		/// <returns></returns>
		public object GetService(Type serviceType)
			=> serviceType.IsAssignableFrom(GetType())
				? this
				: Pane?.GetService(serviceType);

		/// <summary>Access to the owning pane.</summary>
		public PpsGenericWpfWindowPane Pane
		{
			get
			{
				if (DataContext is PpsGenericWpfWindowPane pane)
					return pane;
				return null;
			}
		} // prop Pane

		/// <summary>Title of the window pane</summary>
		public string Title { get { return (string)GetValue(TitleProperty); } set { SetValue(TitleProperty, value); } }
		/// <summary>SubTitle of the window pane</summary>
		public string SubTitle { get { return (string)GetValue(SubTitleProperty); } set { SetValue(SubTitleProperty, value); } }

		/// <summary>pane with SideBar?</summary>
		[
		DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)
		]
		public bool HasSideBar { get { return (bool)GetValue(HasSideBarProperty); } set { SetValue(HasSideBarProperty, value); } }

		/// <summary>ProgressStack of the pane</summary>
		[
		DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)
		]
		public PpsProgressStack ProgressStack => progressStack;

		/// <summary>List of commands for the main toolbar.</summary>
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
		public PpsUICommandCollection Commands => (PpsUICommandCollection)GetValue(CommandsProperty);
	} // class PpsGenericWpfControl

	#endregion
}
