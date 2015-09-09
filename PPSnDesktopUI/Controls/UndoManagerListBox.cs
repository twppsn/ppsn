using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace TecWare.PPSn.Controls
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class UndoManagerListBox : ListBox
	{
		private static readonly DependencyProperty FooterTextProperty = DependencyProperty.Register("FooterText", typeof(string), typeof(UndoManagerListBox));
		private static readonly DependencyProperty IsRedoListProperty = DependencyProperty.Register("IsRedoList", typeof(bool), typeof(UndoManagerListBox), new PropertyMetadata(false));
		private int curStackPosition = -1;

		protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
		{
			base.OnItemsChanged(e);
			if (e.Action == NotifyCollectionChangedAction.Reset)
			{
				curStackPosition = -1;
				// Q+D
				if (HasItems)
				{
					ItemContainerGenerator.StatusChanged += OnItemContainerGeneratorStatusChanged;
				}
			}
		}

		protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
		{
			base.OnPreviewMouseDown(e);
			e.Handled = true;
			if (e.LeftButton == MouseButtonState.Pressed)
				StartUndoRedoAction();
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);
			ChangeSelectionFromMouse(e);
		}

		private void OnItemContainerGeneratorStatusChanged(object sender, EventArgs e)
		{
			if (ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
			{
				ItemContainerGenerator.StatusChanged -= OnItemContainerGeneratorStatusChanged;
				ChangeSelectionFromIndex(0);
			}
		}

		private void StartUndoRedoAction()
		{
			ClosePopUp();
			DoUndoRedoAction();
		}

		private void DoUndoRedoAction()
		{
			// from  0 to curStackPosition
			MessageBox.Show("ToDo");
		}
		
		private void ClosePopUp()
		{
			var parent = LogicalTreeHelper.GetParent(this);
			// search from Treeview
			while (!(parent is Popup) && (parent != null))
			{
				parent = LogicalTreeHelper.GetParent(parent);
			}
			if (parent is Popup)
			{
				((Popup)parent).IsOpen = false;
			}
			else
			{
				throw new ArgumentException("Parent (popup) not found!");
			}
		}

		private void ChangeSelectionFromMouse(MouseEventArgs e)
		{
			var itemIndex = GetItemIndexUnderMouse(e);
			if (itemIndex < 0 || itemIndex == curStackPosition)
				return;

			ChangeSelectionFromIndex(itemIndex);
		} // proc ChangeSelectionFromMouse

		private void ChangeSelectionFromIndex(int itemIndex)
		{
			// unselect items above
			for (int i = itemIndex + 1; i <= curStackPosition; i++)
			{
				var lbi = (ListBoxItem)(ItemContainerGenerator.ContainerFromIndex(i));
				lbi.IsSelected = false;
			}
			// select items below
			for (int i = curStackPosition + 1; i <= itemIndex; i++)
			{
				var lbi = (ListBoxItem)(ItemContainerGenerator.ContainerFromIndex(i));
				lbi.IsSelected = true;
			}
			// notice
			curStackPosition = itemIndex;
			// finally update footer
			FooterText = CalcFooterText();
        } // proc ChangeSelectionFromIndex

		private string CalcFooterText()
		{
			var s1 = curStackPosition == 0 ? "Aktion" : "Aktionen";
			var s2 = IsRedoList ? "wiederholen" : "rückgängig machen";
			return String.Format("{0} {1} {2}", curStackPosition + 1, s1, s2);
		} // func CalcFooterText

		private int GetItemIndexUnderMouse(MouseEventArgs e)
		{
			var dependencyObject = InputHitTest(e.GetPosition(this)) as DependencyObject;
			while ((dependencyObject != null) && !(dependencyObject is ListBox))
			{
				if (dependencyObject is ListBoxItem)
				{
					return ItemContainerGenerator.IndexFromContainer(dependencyObject);
                }
				dependencyObject = VisualTreeHelper.GetParent(dependencyObject) as DependencyObject;
			}
			return -1;
		} // func GetItemIndexUnderMouse

		/// <summary></summary>
		public string FooterText { get { return (string)GetValue(FooterTextProperty); } set { SetValue(FooterTextProperty, value); } }
		/// <summary></summary>
		public bool IsRedoList { get { return (bool)GetValue(IsRedoListProperty); } set { SetValue(IsRedoListProperty, value); } }
	} // class UndoManagerListBox
}
