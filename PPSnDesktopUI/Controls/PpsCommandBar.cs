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
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using TecWare.DE.Stuff;
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Controls
{
	#region -- enum PpsCommandBarMode -------------------------------------------------

	/// <summary>Designs for CommandButton</summary>
	public enum PpsCommandBarMode
	{
		/// <summary>this is for Round Buttons</summary>
		Circle,
		/// <summary>this is for Rectangular Buttons</summary>
		Rectangle,
		/// <summary>this is for Buttons which defines its DisplayMode self</summary>
		Mixed
	} // enum PpsCommandBarMode

	#endregion

	#region -- class CommandBarTemplateKey --------------------------------------------

	/// <summary></summary>
	internal sealed class PpsCommandBarTemplateKey : ResourceKey
	{
		private readonly PpsCommandBarMode mode;
		private readonly Type commandBarItemType;

		public PpsCommandBarTemplateKey(PpsCommandBarMode mode, Type commandBarItemType)
		{
			this.mode = mode;
			this.commandBarItemType = commandBarItemType ?? typeof(PpsUICommandSeperator);
		} // ctor

		public override int GetHashCode()
			=> mode.GetHashCode() ^ (commandBarItemType?.GetHashCode() ?? 0);

		public override bool Equals(object obj)
			=> obj is PpsCommandBarTemplateKey key
				? key.mode == mode && key.commandBarItemType == commandBarItemType
				: false;

		public override string ToString()
			=> $"{mode}:{commandBarItemType?.Name ?? "Separator"}";

		public override object ProvideValue(IServiceProvider serviceProvider) 
			=> this;

		public PpsCommandBarMode Mode => mode;
		public Type CommandBarItemType => commandBarItemType;

		public override Assembly Assembly => typeof(PpsCommandBar).Assembly;
	} // class PpsCommandBarTemplateKey

	#endregion

	#region -- class PpsCommandBar ----------------------------------------------------

	/// <summary>This Control maps PpsUICommandButtons in a Template</summary>
	[
	ContentProperty(nameof(Commands)),
	TemplatePart(Name = "PART_ItemsControl", Type = typeof(ItemsControl))
	]
	public class PpsCommandBar : Control
	{
		#region -- class PpsCommandBarTemplateSelector --------------------------------

		private sealed class PpsCommandBarTemplateSelector : DataTemplateSelector
		{
			private readonly PpsCommandBar parent;

			public PpsCommandBarTemplateSelector(PpsCommandBar parent)
				=> this.parent = parent;

			public override DataTemplate SelectTemplate(object item, DependencyObject container)
				=> parent.TryFindResource(new PpsCommandBarTemplateKey(parent.Mode, item?.GetType())) as DataTemplate;
		} // class PpsCommandBarTemplateSelector

		#endregion

		#region -- Commands - Property ------------------------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		private static readonly DependencyPropertyKey commandsPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Commands), typeof(PpsUICommandCollection), typeof(PpsCommandBar), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty CommandsProperty = commandsPropertyKey.DependencyProperty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		/// <summary>Current command collection.</summary>
		public PpsUICommandCollection Commands { get => (PpsUICommandCollection)GetValue(CommandsProperty); }

		#endregion

		#region -- BasicCommands, ExternalCommands - Property -------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty BasicCommandsProperty = DependencyProperty.Register(nameof(BasicCommands), typeof(PpsUICommandCollection), typeof(PpsCommandBar), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnExternalCommandsChanged)));
		public static readonly DependencyProperty ExternalCommandsProperty = DependencyProperty.Register(nameof(ExternalCommands), typeof(PpsUICommandCollection), typeof(PpsCommandBar), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnExternalCommandsChanged)));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		private static void OnExternalCommandsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsCommandBar)d).OnExternalCommandsChanged((PpsUICommandCollection)e.NewValue, (PpsUICommandCollection)e.OldValue);

		/// <summary></summary>
		/// <param name="newValue"></param>
		/// <param name="oldValue"></param>
		/// <returns></returns>
		protected virtual void OnExternalCommandsChanged(PpsUICommandCollection newValue, PpsUICommandCollection oldValue)
		{
			CommandsView.RemoveCommands(oldValue);
			CommandsView.AppendCommands(newValue);
		} // proc OnExternalCommandsChanged

		/// <summary>External defined command collection, they are merged with the main commands.</summary>
		public PpsUICommandCollection BasicCommands { get => (PpsUICommandCollection)GetValue(BasicCommandsProperty); set => SetValue(BasicCommandsProperty, value); }
		/// <summary>External defined command collection, they are merged with the main commands.</summary>
		public PpsUICommandCollection ExternalCommands { get => (PpsUICommandCollection)GetValue(ExternalCommandsProperty); set => SetValue(ExternalCommandsProperty, value); }

		#endregion

		#region -- CommandsView - Property --------------------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		private static readonly DependencyPropertyKey commandsViewPropertyKey = DependencyProperty.RegisterReadOnly(nameof(CommandsView), typeof(PpsUICommandsView), typeof(PpsCommandBar), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty CommandsViewProperty = commandsViewPropertyKey.DependencyProperty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		/// <summary>Combination of command collections.</summary>
		public PpsUICommandsView CommandsView => (PpsUICommandsView)GetValue(CommandsViewProperty);

		#endregion

		#region -- Mode - Property ----------------------------------------------------

		/// <summary>Sets the Style for the UI</summary>
		public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(nameof(Mode), typeof(PpsCommandBarMode), typeof(PpsCommandBar), 
			new FrameworkPropertyMetadata(
				PpsCommandBarMode.Circle, 
				FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, 
				new PropertyChangedCallback(OnModeChanged)
			)
		);

		private static void OnModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsCommandBar)d).OnModeChanged((PpsCommandBarMode)e.NewValue, (PpsCommandBarMode)e.OldValue);

		/// <summary></summary>
		/// <param name="newValue"></param>
		/// <param name="oldValue"></param>
		protected void OnModeChanged(PpsCommandBarMode newValue, PpsCommandBarMode oldValue)
			=> RefreshItems();

		/// <summary>Sets the Style for the UI</summary>
		public PpsCommandBarMode Mode { get => (PpsCommandBarMode)GetValue(ModeProperty); set => SetValue(ModeProperty, value); }

		#endregion

		private ItemsControl itemsControl = null;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary>standard constructor</summary>
		public PpsCommandBar()
		{
			SetValue(commandsPropertyKey, new PpsUICommandCollection
			{
				AddLogicalChildHandler = AddLogicalChild,
				RemoveLogicalChildHandler = RemoveLogicalChild
			});

			SetValue(commandsViewPropertyKey, new PpsUICommandsView(Commands));

			KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Once);
			KeyboardNavigation.SetDirectionalNavigation(this, KeyboardNavigationMode.Cycle);

		} // ctor

		static PpsCommandBar()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsCommandBar), new FrameworkPropertyMetadata(typeof(PpsCommandBar)));
		}

		#endregion

		#region -- Templates ----------------------------------------------------------

		/// <summary></summary>
		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();

			itemsControl = (ItemsControl)GetTemplateChild("PART_ItemsControl");
			itemsControl.ItemTemplateSelector = new PpsCommandBarTemplateSelector(this);
		} // proc OnApplyTemplate

		private void RefreshItems()
		{
			// call ItemContainerGenerator.Refresh()
			if (itemsControl != null)
				itemsControl.ItemTemplateSelector = new PpsCommandBarTemplateSelector(this);
		} // proc RefreshItems

		#endregion

		/// <summary></summary>
		protected override IEnumerator LogicalChildren
			=> Procs.CombineEnumerator(base.LogicalChildren, Commands.GetEnumerator());
	} // class PpsCommandBar

	#endregion
}
