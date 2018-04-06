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
using System.Collections;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Controls
{
	/// <summary>Designs for CommandButton</summary>
	public enum CommandbarMode
	{
		/// <summary>this is for Round Buttons</summary>
		Circle,
		/// <summary>this is for Rectangular Buttons</summary>
		Rectangle
	}

	/// <summary>This Control maps PpsUICommandButtons in a Template</summary>
	public class PpsCommandbarControl : ItemsControl
	{
		/// <summary>PpsUICommandCollection of the available Commands</summary>
		public static DependencyProperty CommandsProperty = DependencyProperty.Register(nameof(Commands), typeof(PpsUICommandCollection), typeof(PpsCommandbarControl));

		/// <summary>PpsUICommandCollection of the available Commands</summary>
		public PpsUICommandCollection Commands { get => (PpsUICommandCollection)GetValue(CommandsProperty); private set => SetValue(CommandsProperty, value); }

		/// <summary>Sets the Style for the UI</summary>
		public static DependencyProperty ModeProperty = DependencyProperty.Register(nameof(Mode), typeof(CommandbarMode), typeof(PpsCommandbarControl), new PropertyMetadata(CommandbarMode.Circle));
		/// <summary>Sets the Style for the UI</summary>
		public CommandbarMode Mode { get => (CommandbarMode)GetValue(ModeProperty); set => SetValue(ModeProperty, value); }

		/// <summary>standard constructor</summary>
		public PpsCommandbarControl()
		{
			var commands = new PpsUICommandCollection();
			commands.CollectionChanged += CommandsChanged;
			Commands = commands;
		}

		protected override IEnumerator LogicalChildren
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
		}


		private void CommandsChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			switch (e.Action)
			{
				case NotifyCollectionChangedAction.Add:
					var addindex = e.NewStartingIndex;
					foreach (var cmd in e.NewItems)
						if (addindex < 0)
							Items.Add(new ContentPresenter()
							{
								Content = cmd,
								ContentTemplateSelector = new PpsCommandbarDataTemplateSelector()
							});
						else
						{
							Items.Insert(addindex, new ContentPresenter()
							{
								Content = cmd,
								ContentTemplateSelector = new PpsCommandbarDataTemplateSelector()
							});
							addindex++;
						}
					break;
				case NotifyCollectionChangedAction.Remove:
					var removeindex = e.OldStartingIndex;
					foreach (var cmd in e.OldItems)
					{
						var itm = Items[removeindex];
						if (((ContentPresenter)itm).Content == cmd)
							Items.Remove(itm);
						else
							throw new ArgumentException("Commands failed to synchronize.");

						removeindex++;
					}
					break;
				case NotifyCollectionChangedAction.Reset:
					Items.Clear();
					foreach (var cmd in Commands)
						Items.Add(new ContentPresenter()
						{
							Content = cmd,
							ContentTemplateSelector = new PpsCommandbarDataTemplateSelector()
						});
					break;
				default:
					throw new ArgumentException("Commands changed in an invalid action.");
			} // switch (e.Action)
		} // proc CommandsChanged
	} // class CommandbarControl

	/// <summary>This DataTemplateSelector selects the Template according to the type of CommandButton</summary>
	public class PpsCommandbarDataTemplateSelector : DataTemplateSelector
	{
		/// <summary>Selects the Template according to the type of CommandButton</summary>
		/// <param name="item">Heir of PpsUICommandButton</param>
		/// <param name="container"></param>
		/// <returns>the Template for the Button</returns>
		public override DataTemplate SelectTemplate(object item, DependencyObject container)
		{
			var element = container as ContentPresenter;
			var commandbar = (PpsCommandbarControl)element.Parent;
			if (item == null)
				return (DataTemplate)element.FindResource("PpsUICommandbarSeparatorTemplate");
			else
			{
				// set the DataContext to the DataContext of the CommandbarControl - neccessary because PpsUICommandButton is not in the Logical Tree
				/*((PpsUICommandButton)item).SetBinding(PpsUICommandButton.DataContextProperty, new Binding()
				{
					Source = commandbar,
					Path = new PropertyPath("DataContext")
				});*/

				// set the Template
				var template = element.TryFindResource(item.GetType().Name + Enum.GetName(typeof(CommandbarMode), commandbar.Mode) + "Template");
				if (template is DataTemplate)
					return (DataTemplate)template;
				return (DataTemplate)element.FindResource("PpsUICommandbarDefaultButton" + Enum.GetName(typeof(CommandbarMode), commandbar.Mode) + "Template");
			}
		} // func SelectTemplate
	} // class PpsCommandbarDataTemplateSelector
}
