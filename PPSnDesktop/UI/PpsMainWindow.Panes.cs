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
	#region -- enum PpsOpenPaneMode -----------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public enum PpsOpenPaneMode
	{
		Default,
		ReplacePane,
		NewPane,
		NewMainWindow,
		NewSingleWindow
	} // enum PpsOpenPaneMode

	#endregion

	#region -- class PpsPaneCollection --------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsPaneCollection : IList, IReadOnlyList<IPpsWindowPane>, INotifyCollectionChanged
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
	public partial class PpsMainWindow
	{
		private readonly PpsPaneCollection panes = new PpsPaneCollection();

		public bool Activate(IPpsWindowPane paneToActivate)
		{
			var r = base.Activate();

			SetValue(CurrentPaneKey, paneToActivate);
			IsNavigatorVisible = false;

			return r;
		} // func Activate

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

			return Activate(panes[index]);
		} // func ActivateNextPane

		private void Remove(IPpsWindowPane paneToRemove)
		{
			var r = UnloadPaneAsync(paneToRemove);
		} // proc Remove

		/// <summary>Initialize a empty pane.</summary>
		/// <param name="paneType"></param>
		/// <returns></returns>
		private IPpsWindowPane CreateEmptyPane(Type paneType)
		{
			var ti = paneType.GetTypeInfo();
			var ctorBest = (ConstructorInfo)null;
			var ctoreBestParamLength = -1;

			// search for the longest constructor
			foreach (var ci in ti.GetConstructors())
			{
				var pi = ci.GetParameters();
				if (ctoreBestParamLength < pi.Length)
				{
					ctorBest = ci;
					ctoreBestParamLength = pi.Length;
				}
			}
			if (ctorBest == null)
				throw new ArgumentException($"'{ti.Name}' has no constructor.");

			// create the argument set
			var parameterInfo = ctorBest.GetParameters();
			var paneArguments = new object[parameterInfo.Length];

			for (int i = 0; i < paneArguments.Length; i++)
			{
				var pi = parameterInfo[i];
				var tiParam = pi.ParameterType.GetTypeInfo();
				if (tiParam.IsAssignableFrom(typeof(PpsMainEnvironment)))
					paneArguments[i] = Environment;
				else if (tiParam.IsAssignableFrom(typeof(PpsMainWindow)))
					paneArguments[i] = this;
				else if (pi.HasDefaultValue)
					paneArguments[i] = pi.DefaultValue;
				else
					throw new ArgumentException($"Unsupported argument '{pi.Name}' for type '{ti.Name}'.");
			}

			// activate the pane
			return (IPpsWindowPane)Activator.CreateInstance(paneType, paneArguments);
		} // func CreateEmptyPane

		public PpsLuaTask LoadPane(LuaTable arguments)
			=> Environment.RunTask(LoadPaneAsync(typeof(PpsGenericWpfWindowPane), arguments));

		public PpsLuaTask LoadMask(LuaTable arguments)
			=> Environment.RunTask(LoadPaneAsync(typeof(PpsGenericMaskWindowPane), arguments));

		/// <summary>Loads a new current pane.</summary>
		/// <param name="paneType">Type of the pane to load.</param>
		/// <param name="arguments">Argument set for the pane</param>
		/// <returns></returns>
		public Task LoadPaneAsync(Type paneType, LuaTable arguments)
			=> LoadPaneAsync(paneType, PpsOpenPaneMode.Default, arguments);

		/// <summary>Loads a new current pane.</summary>
		/// <param name="paneType">Type of the pane to load.</param>
		/// <param name="newPaneMode"></param>
		/// <param name="arguments">Argument set for the pane</param>
		/// <returns></returns>
		public async Task LoadPaneAsync(Type paneType, PpsOpenPaneMode newPaneMode, LuaTable arguments)
		{
			try
			{
				if (newPaneMode == PpsOpenPaneMode.Default)
					newPaneMode = (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) ? PpsOpenPaneMode.NewMainWindow : GetDefaultPaneMode(arguments);
				
				switch (newPaneMode)
				{
					case PpsOpenPaneMode.NewMainWindow:
					case PpsOpenPaneMode.NewSingleWindow:
						{
							var loadedPane =
							(
								from w in Environment.GetWindows()
								let r = w.Panes.FindPaneByArguments(paneType, arguments, false)
								where w != this && r.Item1 == PpsWindowPaneCompareResult.Same
								select new Tuple<PpsMainWindow, IPpsWindowPane>(w, r.Item2)
							).FirstOrDefault();

							if (loadedPane == null)
							{
								var newWindow = await Environment.CreateMainWindowAsync();
								await newWindow.LoadPaneInternAsync(paneType, arguments);
							}
							else
								await Dispatcher.InvokeAsync(() => loadedPane.Item1.Activate(loadedPane.Item2));
						}
						break;

					case PpsOpenPaneMode.ReplacePane:

						// replace pane => will close all panes an open an new one
						if (await UnloadPanesAsync())
							await LoadPaneInternAsync(paneType, arguments);

						break;

					default:
						{
							var loadedPane = Panes.FirstOrDefault(c => c.GetType() == paneType && c.CompareArguments(arguments) == PpsWindowPaneCompareResult.Same);
							if (loadedPane != null)
								await Dispatcher.InvokeAsync(() => Activate(loadedPane));
							else
								await LoadPaneInternAsync(paneType, arguments);
						}
						break;
				}
			}
			catch (Exception e)
			{
				await Environment.ShowExceptionAsync(ExceptionShowFlags.None, e, "Die Ansicht konnte nicht geladen werden.");
			}
		} // proc StartPaneAsync

		private PpsOpenPaneMode GetDefaultPaneMode(dynamic arguments)
		{
			if (arguments.mode != null)
				return Procs.ChangeType<PpsOpenPaneMode>(arguments.mode);

			return Environment.GetOptionalValue<bool>("NewPaneMode", false) ? PpsOpenPaneMode.NewPane : PpsOpenPaneMode.ReplacePane;
		} // func GetDefaultPaneMode

		private async Task LoadPaneInternAsync(Type paneType, LuaTable arguments)
		{
			arguments = arguments ?? new LuaTable();

			// Build the new pane
			var newPane = CreateEmptyPane(paneType);

			try
			{
				// load the pane>
				await newPane.LoadAsync(arguments);
				await Dispatcher.InvokeAsync(() => panes.AddPane(newPane));
			}
			catch
			{
				newPane.Dispose();
				throw;
			}

			// Hide Navigator and show the pane
			await Dispatcher.InvokeAsync(() =>
			{
				Activate(newPane);
			});
		} // proc LoadPaneInternAsync

		public async Task<bool> UnloadPaneAsync(IPpsWindowPane pane)
		{
			if (await pane.UnloadAsync())
			{
				await Dispatcher.InvokeAsync(() =>
				{
					if (CurrentPane == pane)
					{
						if (panes.Count > 1)
							ActivateNextPane(true);
						else
							SetValue(CurrentPaneKey, null);
					}

					panes.RemovePane(pane);
				});
				pane.Dispose();
				ShowSideBarBackground();
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

		private async Task StartLoginAsync()
		{
			await LoadPaneAsync(typeof(Panes.PpsLoginPane), null);
		} // proc StartLogin

		/// <summary>Returns the current view of the pane as a wpf control.</summary>
		public IPpsWindowPane CurrentPane => (IPpsWindowPane)GetValue(CurrentPaneProperty);
		/// <summary>List with the current open panes.</summary>
		public PpsPaneCollection Panes => panes;
		/// <summary>show the SideBarBackground</summary>
		public bool ShowPaneSideBar => CurrentPane?.HasSideBar ?? false;

	} // class PpsMainWindow

	#endregion
}
