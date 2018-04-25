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
		Rectangle
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
			this.commandBarItemType = commandBarItemType;
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

		public override Assembly Assembly => typeof(PpsCommandBarControl).Assembly;
	} // class PpsCommandBarTemplateKey

	#endregion

	#region -- class PpsCommandBarControl ---------------------------------------------

	/// <summary>This Control maps PpsUICommandButtons in a Template</summary>
	[
	ContentProperty(nameof(Commands)),
	TemplatePart(Name = "PART_ItemsControl", Type = typeof(ItemsControl))
	]
	public class PpsCommandBarControl : Control
	{
		#region -- class PpsCommandBarControlTemplateSelector -------------------------

		private sealed class PpsCommandBarControlTemplateSelector : DataTemplateSelector
		{
			private readonly PpsCommandBarControl parent;

			public PpsCommandBarControlTemplateSelector(PpsCommandBarControl parent)
				=> this.parent = parent;

			public override DataTemplate SelectTemplate(object item, DependencyObject container)
				=> parent.TryFindResource(new PpsCommandBarTemplateKey(parent.Mode, item?.GetType())) as DataTemplate;
		} // class PpsCommandBarControlTemplateSelector

		#endregion

		/// <summary></summary>
		public static readonly DependencyProperty CommandsProperty = DependencyProperty.Register(nameof(Commands), typeof(PpsUICommandCollection), typeof(PpsCommandBarControl), new FrameworkPropertyMetadata(null, new CoerceValueCallback(OnCommandsCoerceValue)));

		/// <summary>Sets the Style for the UI</summary>
		public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(nameof(Mode), typeof(PpsCommandBarMode), typeof(PpsCommandBarControl), new PropertyMetadata(PpsCommandBarMode.Circle, new PropertyChangedCallback(OnModeChanged)));

		private readonly PpsUICommandCollection defaultCollection;
		private ItemsControl itemsControl = null;
		
		/// <summary>standard constructor</summary>
		public PpsCommandBarControl()
		{
			defaultCollection = new PpsUICommandCollection
			{
				AddLogicalChildHandler = AddLogicalChild,
				RemoveLogicalChildHandler = RemoveLogicalChild
			};

			SetValue(CommandsProperty, defaultCollection);
		} // ctor

		/// <summary></summary>
		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();

			itemsControl = (ItemsControl)GetTemplateChild("PART_ItemsControl");
			itemsControl.ItemTemplateSelector = new PpsCommandBarControlTemplateSelector(this);
		} // proc OnApplyTemplate

		private void RefreshItems()
		{
			// call ItemContainerGenerator.Refresh()
			if (itemsControl != null)
				itemsControl.ItemTemplateSelector = new PpsCommandBarControlTemplateSelector(this);
		} // proc RefreshItems

		private static object OnCommandsCoerceValue(DependencyObject d, object baseValue)
			=> ((PpsCommandBarControl)d).OnCommandCoerceValue((PpsUICommandCollection)baseValue);

		/// <summary></summary>
		/// <param name="baseValue"></param>
		/// <returns></returns>
		protected virtual PpsUICommandCollection OnCommandCoerceValue(PpsUICommandCollection baseValue)
			=> baseValue ?? defaultCollection;

		private static void OnModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsCommandBarControl)d).OnModeChanged((PpsCommandBarMode)e.NewValue, (PpsCommandBarMode)e.OldValue);

		/// <summary></summary>
		/// <param name="newValue"></param>
		/// <param name="oldValue"></param>
		protected void OnModeChanged(PpsCommandBarMode newValue, PpsCommandBarMode oldValue)
			=> RefreshItems();

		/// <summary></summary>
		protected override IEnumerator LogicalChildren
			=> Commands == defaultCollection 
				? Procs.CombineEnumerator(base.LogicalChildren, defaultCollection.GetEnumerator()) 
				: base.LogicalChildren;

		/// <summary>Current Commands collection.</summary>
		public PpsUICommandCollection Commands { get => (PpsUICommandCollection)GetValue(CommandsProperty); set => SetValue(CommandsProperty, value); }
		/// <summary>Sets the Style for the UI</summary>
		public PpsCommandBarMode Mode { get => (PpsCommandBarMode)GetValue(ModeProperty); set => SetValue(ModeProperty, value); }
		
		static PpsCommandBarControl()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsCommandBarControl), new FrameworkPropertyMetadata(typeof(PpsCommandBarControl)));
		}
	} // class PpsCommandBarControl

	#endregion
}
