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
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Neo.IronLua;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.UI
{
	#region -- class PpsPaneCollection ------------------------------------------------

	internal sealed class PpsPaneCollection : IList, IReadOnlyList<PpsWindowPaneHost>, INotifyCollectionChanged
	{
		public event NotifyCollectionChangedEventHandler CollectionChanged;

		private readonly List<PpsWindowPaneHost> panes = new List<PpsWindowPaneHost>();

		#region -- IReadOnlyList<IPpsWindowPane> members ------------------------------

		public IEnumerator<PpsWindowPaneHost> GetEnumerator()
			=> panes.GetEnumerator();

		public bool Contains(PpsWindowPaneHost pane)
			=> panes.Contains(pane);

		public int IndexOf(PpsWindowPaneHost pane)
			=> panes.IndexOf(pane);

		public void AddPane(PpsWindowPaneHost pane)
		{
			panes.Add(pane);
			CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, pane));
		} // proc AddPane

		public void RemovePane(PpsWindowPaneHost pane)
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
					var r = p.CurrentPane.CompareArguments(arguments);
					if (r == PpsWindowPaneCompareResult.Same)
						return new Tuple<PpsWindowPaneCompareResult, IPpsWindowPane>(r, p.CurrentPane);
					else if (r == PpsWindowPaneCompareResult.Reload && compatiblePane == null)
						compatiblePane = p.CurrentPane;
				}
			}

			return compatiblePane == null ?
				new Tuple<PpsWindowPaneCompareResult, IPpsWindowPane>(PpsWindowPaneCompareResult.Incompatible, null) :
				new Tuple<PpsWindowPaneCompareResult, IPpsWindowPane>(PpsWindowPaneCompareResult.Reload, compatiblePane);
		} // func FindPaneByArguments

		public int Count => panes.Count;

		public PpsWindowPaneHost this[int index] => panes[index];

		#endregion

		#region -- IList members ------------------------------------------------------

		int IList.Add(object value) 
			=> throw new NotSupportedException();

		void IList.Insert(int index, object value) 
			=> throw new NotSupportedException();

		void IList.Clear() 
			=> throw new NotSupportedException();

		void IList.Remove(object value) 
			=> throw new NotSupportedException(); 

		void IList.RemoveAt(int index) 
			=> throw new NotSupportedException(); 

		void ICollection.CopyTo(Array array, int index)
		{
			for (var i = 0; i < panes.Count; i++)
				array.SetValue(panes[i], index + i);
		} // func ICollection.CopyTo

		bool IList.Contains(object value)
			=> Contains(value as PpsWindowPaneHost);

		int IList.IndexOf(object value)
			=> IndexOf(value as PpsWindowPaneHost);

		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();

		object IList.this[int index]
		{
			get => this[index];
			set => throw new NotSupportedException();
		} // func IList.this

		bool IList.IsFixedSize => false;
		bool IList.IsReadOnly => true;
		bool ICollection.IsSynchronized => false;
		object ICollection.SyncRoot => null;

		#endregion
	} // class PpsPaneCollection

	#endregion

	#region -- class PpsMainWindow ----------------------------------------------------

	/// <summary></summary>
	public partial class PpsMainWindow : IPpsWindowPaneManager
	{
#pragma warning disable IDE1006 // Naming Styles
		private readonly static DependencyPropertyKey currentPaneHostPropertyKey = DependencyProperty.RegisterReadOnly(nameof(CurrentPaneHost), typeof(PpsWindowPaneHost), typeof(PpsMainWindow), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnCurrentPaneHostChanged)));
		internal readonly static DependencyProperty CurrentPaneHostProperty = currentPaneHostPropertyKey.DependencyProperty;

		private readonly static DependencyPropertyKey paneHostsPropertyKey = DependencyProperty.RegisterReadOnly(nameof(PaneHosts), typeof(PpsPaneCollection), typeof(PpsMainWindow), new FrameworkPropertyMetadata(null));
		internal readonly static DependencyProperty PaneHostsProperty = paneHostsPropertyKey.DependencyProperty;

		private readonly static PropertyDescriptor hasPaneSideBarPropertyDescriptor = DependencyPropertyDescriptor.FromProperty(PpsWindowPaneHost.HasPaneSideBarProperty, typeof(PpsWindowPaneHost));
#pragma warning restore IDE1006 // Naming Styles

		private readonly PpsPaneCollection paneHosts = new PpsPaneCollection();

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
				return ActivatePaneHost(FindPaneHost(pane, true));
			else
				return Environment.ActivatePane(pane);
		} // func ActivatePane

		private bool ActivatePaneHost(PpsWindowPaneHost paneHost)
		{
			if (paneHost == null)
				return false;

			var r = Activate();
			if (paneHost != null)
			{
				SetValue(currentPaneHostPropertyKey, paneHost);
				IsNavigatorVisible = false;
			}
			else
				r = false;

			return r;
		} // func ActivatePaneHost

		public bool ActivateNextPane(bool forward)
		{
			var currentPaneHost = CurrentPaneHost;
			if (currentPaneHost == null)
				return false;

			// get index
			var index = paneHosts.IndexOf(currentPaneHost);
			if (forward)
			{
				index++;
				if (index >= paneHosts.Count)
					index = 0;
			}
			else
			{
				index--;
				if (index < 0)
					index = paneHosts.Count - 1;
			}

			if (currentPaneHost == paneHosts[index])
				return false;

			return ActivatePaneHost(paneHosts[index]);
		} // func ActivateNextPane

		#endregion

		#region -- OpenPaneAsync ------------------------------------------------------

		private PpsOpenPaneMode GetDefaultPaneMode(dynamic arguments)
		{
			if (arguments.mode != null)
				return Procs.ChangeType<PpsOpenPaneMode>(arguments.mode);

			return Environment.GetOptionalValue("NewPaneMode", false) ? PpsOpenPaneMode.NewPane : PpsOpenPaneMode.ReplacePane;
		} // func GetDefaultPaneMode

		private async Task<IPpsWindowPane> LoadPaneInternAsync(Type paneType, LuaTable arguments)
		{
			arguments = arguments ?? new LuaTable();

			var oldPaneHost = CurrentPaneHost;

			// Build the new pane host
			var newPaneHost = new PpsWindowPaneHost();
	
			try
			{
				// add pane and show it, progress handling should be done by the Load
				paneHosts.AddPane(newPaneHost);
				ActivatePaneHost(newPaneHost);

				// load the pane
				await newPaneHost.LoadAsync(this, paneType, arguments);

				return newPaneHost.CurrentPane;
			}
			catch
			{
				ActivatePaneHost(oldPaneHost);
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

		public Task<bool> UnloadPaneAsync(IPpsWindowPane pane)
		{
			if (pane == null)
				throw new ArgumentNullException(nameof(pane));

			return UnloadPaneHostAsync(FindPaneHost(pane, true), null);
		} // func UnloadPaneAsync

		private async Task<bool> UnloadPaneHostAsync(PpsWindowPaneHost paneHost, bool? commit)
		{
			if (paneHost == null)
				throw new ArgumentNullException(nameof(paneHost));

			if (await paneHost.UnloadAsync(commit))
			{
				if (CurrentPaneHost == paneHost)
				{
					if (paneHosts.Count > 1)
						ActivateNextPane(true);
					else
						SetValue(currentPaneHostPropertyKey, null);
				}

				paneHosts.RemovePane(paneHost);

				return true;
			}
			else
				return false;
		}

		/// <summary>Unloads the current pane, to a empty pane.</summary>
		/// <returns></returns>
		public async Task<bool> UnloadPanesAsync()
		{
			if (paneHosts.Count == 0)
				return true;
			else
			{
				for (var i = paneHosts.Count - 1; i >= 0; i--)
				{
					if (!await UnloadPaneHostAsync(paneHosts[i], null))
						return false;
				}
				return true;
			}
		} // proc UnloadPaneAsync

		#endregion

		#region -- FindOpenPane -------------------------------------------------------

		private PpsWindowPaneHost FindPaneHost(IPpsWindowPane pane, bool throwException)
		{
			if (pane != null)
			{
				foreach (var p in paneHosts)
				{
					if (p.CurrentPane == pane)
						return p;
				}
			}

			if (throwException)
			{
				if (pane == null)
					throw new ArgumentNullException(nameof(pane));
				else
					throw new ArgumentOutOfRangeException(nameof(pane), "Pane is not a member of this PaneManager.");
			}
			else
			return null;
		} // func FindPaneHost

		public IPpsWindowPane FindOpenPane(Type paneType, LuaTable arguments)
		{
			var r = paneHosts.FindPaneByArguments(paneType, arguments, false);
			return r.Item1 == PpsWindowPaneCompareResult.Same
				? r.Item2
				: null;
		} // func FindOpenPane

		#endregion

		private static void OnCurrentPaneHostChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsMainWindow)d).OnCurrentPaneHostChanged((PpsWindowPaneHost)e.NewValue, (PpsWindowPaneHost)e.OldValue);

		private void OnCurrentPaneHostChanged(PpsWindowPaneHost newValue, PpsWindowPaneHost oldValue)
		{
			if (oldValue != null)
				hasPaneSideBarPropertyDescriptor.RemoveValueChanged(oldValue, OnCurrentPaneHostSideBarChanged);
			if (newValue != null)
				hasPaneSideBarPropertyDescriptor.AddValueChanged(newValue, OnCurrentPaneHostSideBarChanged);

			RefreshSideIsVisibleProperty();
		} // proc OnCurrentPaneHostChanged

		private void OnCurrentPaneHostSideBarChanged(object sender, EventArgs e)
			=> RefreshSideIsVisibleProperty();

		Type IPpsWindowPaneManager.GetPaneType(PpsWellknownType wellknownType)
			=> Environment.GetPaneType(wellknownType);

		#endregion
		
		/// <summary>Returns the current view of the pane as a wpf control.</summary>
		internal PpsWindowPaneHost CurrentPaneHost => (PpsWindowPaneHost)GetValue(CurrentPaneHostProperty);
		/// <summary>List with the current open panes.</summary>
		//internal IReadOnlyList<PpsWindowPaneHost> PaneHosts => paneHosts;
		internal PpsPaneCollection PaneHosts => (PpsPaneCollection)GetValue(PaneHostsProperty);

		IEnumerable<IPpsWindowPane> IPpsWindowPaneManager.Panes
			=> paneHosts.Select(c => c.CurrentPane);
	} // class PpsMainWindow

	#endregion
}
