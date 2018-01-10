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
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Neo.IronLua;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.UI
{
	#region -- class PpsPaneCollection --------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	internal sealed class PpsPaneCollection : IList, IReadOnlyList<IPpsWindowPane>, INotifyCollectionChanged
	{
		public event NotifyCollectionChangedEventHandler CollectionChanged;

		private readonly List<IPpsWindowPane> panes = new List<IPpsWindowPane>();

		public IEnumerator<IPpsWindowPane> GetEnumerator()
			=> panes.GetEnumerator();

		public bool Contains(IPpsWindowPane pane)
			=> panes.Contains(pane);

		public int IndexOf(IPpsWindowPane pane)
			=> panes.IndexOf(pane);

		public void AddPane(IPpsWindowPane pane)
		{
			panes.Add(pane);
			CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, pane));
		} // proc AddPane

		public void RemovePane(IPpsWindowPane pane)
		{
			panes.Remove(pane);
			CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, pane));
		} // proc AddPane

		/// <summary></summary>
		/// <param name="arguments"></param>
		/// <param name="compatibleAllowed"></param>
		/// <returns></returns>
		public Tuple<PpsWindowPaneCompareResult, IPpsWindowPane> FindPaneByArguments(Type paneType, LuaTable arguments, bool compatibleAllowed = false)
		{
			IPpsWindowPane compatiblePane = null;

			foreach (var p in panes)
			{
				if (p.GetType() == paneType)
				{
					var r = p.CompareArguments(arguments);
					if (r == PpsWindowPaneCompareResult.Same)
						return new Tuple<PpsWindowPaneCompareResult, IPpsWindowPane>(r, p);
					else if (r == PpsWindowPaneCompareResult.Reload && compatiblePane == null)
						compatiblePane = p;
				}
			}

			return compatiblePane == null ?
				new Tuple<PpsWindowPaneCompareResult, IPpsWindowPane>(PpsWindowPaneCompareResult.Incompatible, null) :
				new Tuple<PpsWindowPaneCompareResult, IPpsWindowPane>(PpsWindowPaneCompareResult.Reload, compatiblePane);
		} // func FindPaneByArguments

		public int Count => panes.Count;

		public IPpsWindowPane this[int index] => panes[index];

		#region -- IList members ----------------------------------------------------------

		int IList.Add(object value) { throw new NotSupportedException(); }
		void IList.Insert(int index, object value) { throw new NotSupportedException(); }
		void IList.Clear() { throw new NotSupportedException(); }
		void IList.Remove(object value) { throw new NotSupportedException(); }
		void IList.RemoveAt(int index) { throw new NotSupportedException(); }

		void ICollection.CopyTo(Array array, int index)
		{
			for (var i = 0; i < panes.Count; i++)
				array.SetValue(panes[i], index + i);
		} // func ICollection.CopyTo

		bool IList.Contains(object value)
			=> Contains(value as IPpsWindowPane);

		int IList.IndexOf(object value)
			=> IndexOf(value as IPpsWindowPane);

		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();

		object IList.this[int index]
		{
			get { return this[index]; }
			set { throw new NotSupportedException(); }
		} // func IList.this

		bool IList.IsFixedSize => false;
		bool IList.IsReadOnly => true;
		bool ICollection.IsSynchronized => false;
		object ICollection.SyncRoot => null;

		#endregion
	} // class PpsPaneCollection

	#endregion

	#region -- class PpsMainWindow ------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public partial class PpsMainWindow : IPpsWindowPaneManager
	{
		private readonly PpsPaneCollection panes = new PpsPaneCollection();

		#region -- Pane Manager  ------------------------------------------------------

		#region -- ActivatePane, ActivateNextPane -------------------------------------

		/// <summary>Activates the pane, if the pane is in the current pane manager.</summary>
		/// <param name="pane"></param>
		/// <returns></returns>
		public bool ActivatePane(IPpsWindowPane pane)
		{
			if (pane == null)
				return false;

			if (pane.PaneManager == this)
			{
				var r = Activate();

				SetValue(CurrentPaneKey, pane);
				IsNavigatorVisible = false;

				return r;
			}
			else
				return Environment.ActivatePane(pane);
		} // func ActivatePane

		public bool ActivateNextPane(bool forward)
		{
			var currentPane = CurrentPane;
			if (currentPane == null)
				return false;

			// get index
			var index = panes.IndexOf(currentPane);
			if (forward)
			{
				index++;
				if (index >= panes.Count)
					index = 0;
			}
			else
			{
				index--;
				if (index < 0)
					index = panes.Count - 1;
			}

			if (currentPane == panes[index])
				return false;

			return ActivatePane(panes[index]);
		} // func ActivateNextPane

		#endregion

		#region -- OpenPaneAsync ------------------------------------------------------

		private PpsOpenPaneMode GetDefaultPaneMode(dynamic arguments)
		{
			if (arguments.mode != null)
				return Procs.ChangeType<PpsOpenPaneMode>(arguments.mode);

			return Environment.GetOptionalValue<bool>("NewPaneMode", false) ? PpsOpenPaneMode.NewPane : PpsOpenPaneMode.ReplacePane;
		} // func GetDefaultPaneMode

		private async Task<IPpsWindowPane> LoadPaneInternAsync(Type paneType, LuaTable arguments)
		{
			arguments = arguments ?? new LuaTable();

			// Build the new pane
			var newPane = this.CreateEmptyPane(paneType);
			var oldPane = CurrentPane;

			try
			{
				// add pane and show it, progress handling should be done by the Load
				panes.AddPane(newPane);
				ActivatePane(newPane);

				// load the pane
				await newPane.LoadAsync(arguments);

				return newPane;
			}
			catch
			{
				ActivatePane(oldPane);
				newPane.Dispose();
				throw;
			}

			// Hide Navigator and show the pane
		} // proc LoadPaneInternAsync

		/// <summary>Lua friendly implementation of OpenPane.</summary>
		/// <param name="arguments"></param>
		public IPpsWindowPane LoadPane(LuaTable arguments = null)
			=> OpenPaneAsync(typeof(PpsGenericWpfWindowPane), PpsOpenPaneMode.Default, arguments).AwaitTask();

		/// <summary>Lua friendly implementation of OpenPane.</summary>
		/// <param name="arguments"></param>
		public IPpsWindowPane LoadMask(LuaTable arguments = null)
			=> OpenPaneAsync(typeof(PpsGenericMaskWindowPane), PpsOpenPaneMode.Default, arguments).AwaitTask();
		
		/// <summary>Loads a new current pane.</summary>
		/// <param name="paneType">Type of the pane to load.</param>
		/// <param name="newPaneMode"></param>
		/// <param name="arguments">Argument set for the pane</param>
		/// <returns></returns>
		public async Task<IPpsWindowPane> OpenPaneAsync(Type paneType, PpsOpenPaneMode newPaneMode = PpsOpenPaneMode.Default, LuaTable arguments = null)
		{
			try
			{
				if (newPaneMode == PpsOpenPaneMode.Default)
					newPaneMode = (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) ? PpsOpenPaneMode.NewMainWindow : GetDefaultPaneMode(arguments);

				switch (newPaneMode)
				{
					case PpsOpenPaneMode.NewMainWindow:
					case PpsOpenPaneMode.NewSingleWindow:
						return await Environment.OpenPaneAsync(paneType, newPaneMode, arguments);
					case PpsOpenPaneMode.ReplacePane:

						// replace pane => will close all panes an open an new one
						if (await UnloadPanesAsync())
							return await LoadPaneInternAsync(paneType, arguments);
						return null;

					default:
						var pane = FindOpenPane(paneType, arguments);
						if (pane == null)
							return await LoadPaneInternAsync(paneType, arguments);
						else
						{
							ActivatePane(pane);
							return pane;
						}
				}
			}
			catch (Exception e)
			{
				await Environment.ShowExceptionAsync(ExceptionShowFlags.None, e, "Die Ansicht konnte nicht geladen werden.");
				return null;
			}
		} // proc OpenPaneAsync

		#endregion

		#region -- UnloadPaneAsync ----------------------------------------------------

		private void Remove(IPpsWindowPane pane)
			=> UnloadPaneAsync(pane).AwaitTask();

		public async Task<bool> UnloadPaneAsync(IPpsWindowPane pane)
		{
			if (pane == null)
				throw new ArgumentNullException(nameof(pane));

			if (await pane.UnloadAsync())
			{
				if (CurrentPane == pane)
				{
					if (panes.Count > 1)
						ActivateNextPane(true);
					else
						SetValue(CurrentPaneKey, null);
				}

				panes.RemovePane(pane);
				pane.Dispose();

				return true;
			}
			else
				return false;
		} // func UnloadPaneAsync

		/// <summary>Unloads the current pane, to a empty pane.</summary>
		/// <returns></returns>
		public async Task<bool> UnloadPanesAsync()
		{
			if (panes.Count == 0)
				return true;
			else
			{
				for (var i = panes.Count - 1; i >= 0; i--)
				{
					if (!await UnloadPaneAsync(panes[i]))
						return false;
				}
				return true;
			}
		} // proc UnloadPaneAsync

		#endregion

		#region -- FindOpenPane -------------------------------------------------------

		public IPpsWindowPane FindOpenPane(Type paneType, LuaTable arguments)
		{
			var r = panes.FindPaneByArguments(paneType, arguments, false);
			return r.Item1 == PpsWindowPaneCompareResult.Same
				? r.Item2
				: null;
		} // func FindOpenPane

		#endregion

		Type IPpsWindowPaneManager.GetPaneType(PpsWellknownType wellknownType)
			=> Environment.GetPaneType(wellknownType);

		#endregion
		
		/// <summary>Returns the current view of the pane as a wpf control.</summary>
		public IPpsWindowPane CurrentPane => (IPpsWindowPane)GetValue(CurrentPaneProperty);
		/// <summary>List with the current open panes.</summary>
		public IReadOnlyList<IPpsWindowPane> Panes => panes;
		IEnumerable<IPpsWindowPane> IPpsWindowPaneManager.Panes => panes;
	} // class PpsMainWindow

	#endregion
}
