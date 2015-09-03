using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace TecWare.PPSn.Controls
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsTreeListView : TreeView
	{
		protected override DependencyObject GetContainerForItemOverride()
		{
			return new PpsTreeListViewItem();
		}

		protected override bool IsItemItsOwnContainerOverride(object item)
		{
			return item is PpsTreeListViewItem;
		}
	} // class PpsTreeListView

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsTreeListViewItem : TreeViewItem
	{
		public PpsTreeListViewItem()
		{
			Loaded += OnLoaded;
		}

		protected override DependencyObject GetContainerForItemOverride()
		{
			return new PpsTreeListViewItem();
		}

		protected override bool IsItemItsOwnContainerOverride(object item)
		{
			return item is PpsTreeListViewItem;
		}

		protected override void OnExpanded(RoutedEventArgs e)
		{
			base.OnExpanded(e);
			Test();
		}

		protected override void OnCollapsed(RoutedEventArgs e)
		{
			base.OnCollapsed(e);
			Test();
		}

		private void OnLoaded(object sender, RoutedEventArgs e)
		{
			Test();
		}

		private void Test()
		{
			//var parent = Parent as DependencyObject;
			var parent = VisualTreeHelper.GetParent(this);
			while (!(parent is PpsTreeListView) && (parent != null))
			{
				parent = VisualTreeHelper.GetParent(parent);
			}
			if (parent is PpsTreeListView)
			{
				AlternationExtensions.SetAlternationIndexRecursively((ItemsControl)parent, 0);
			}
		}

		private static class AlternationExtensions
		{
			private static readonly MethodInfo SetAlternationIndexMethod;

			static AlternationExtensions()
			{
				SetAlternationIndexMethod = typeof(ItemsControl).GetMethod("SetAlternationIndex", BindingFlags.Static | BindingFlags.NonPublic);
			}

			public static int SetAlternationIndexRecursively(ItemsControl control, int firstAlternationIndex)
			{
				var alternationCount = control.AlternationCount;
				if (alternationCount == 0)
				{
					return 0;
				}

				foreach (var item in control.Items)
				{
					var container = control.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
					if (container != null)
					{
						var nextAlternation = firstAlternationIndex++ % alternationCount;
						SetAlternationIndexMethod.Invoke(null, new object[] { container, nextAlternation });
						if (container.IsExpanded)
						{
							firstAlternationIndex = SetAlternationIndexRecursively(container, firstAlternationIndex);
						}
					}
				}
				return firstAlternationIndex;
			}
		}
	} // class PpsTreeListViewItem

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsTreeViewLevelToIndentConverter : IValueConverter
	{
		private const double indentSize = 19.0;

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var level = 0;
			if (value is DependencyObject)
			{
				var parent = VisualTreeHelper.GetParent(value as DependencyObject);
				while (!(parent is PpsTreeListView) && (parent != null))
				{
					if (parent is PpsTreeListViewItem)
						level++;
					parent = VisualTreeHelper.GetParent(parent);
				}
			}
			return new Thickness(level * indentSize, 0, 0, 0);
		}
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new System.NotImplementedException();
		}
	} // converter PpsTreeViewLevelToIndentConverter

}
