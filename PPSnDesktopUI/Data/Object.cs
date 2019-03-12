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
using System.IO;
using System.Threading.Tasks;
using Neo.IronLua;
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Data
{
	#region -- interface IPpsDataStream -----------------------------------------------

	/// <summary>Open data as an stream.</summary>
	public interface IPpsDataStream
	{
		/// <summary>Open an stream on the data.</summary>
		/// <param name="access"></param>
		/// <param name="expectedLength"></param>
		/// <returns></returns>
		Stream OpenStream(FileAccess access, long expectedLength = -1);
	} // interface IPpsDataStream

	#endregion

	#region -- interface IPpsDataObject -----------------------------------------------

	/// <summary>Defines a data access for UI-Controls.</summary>
	public interface IPpsDataObject : IDisposable
	{
		/// <summary>Gets call if the data was changed.</summary>
		event EventHandler DataChanged;
		/// <summary>Method to disable access to the data, from the data it self.</summary>
		Func<IDisposable> DisableUI { get; set; }

		/// <summary>Commit the data to the local changes.</summary>
		/// <returns></returns>
		Task CommitAsync();
		
		/// <summary>Is the data changable.</summary>
		bool IsReadOnly { get; }

		/// <summary>Access data</summary>
		object Data { get; }
	} // interface IPpsDataObject

	#endregion

	#region -- interface IPpsDataInfo -------------------------------------------------

	/// <summary></summary>
	public interface IPpsDataInfo
	{
		/// <summary></summary>
		/// <returns></returns>
		Task<IPpsDataObject> LoadAsync();

		/// <summary>Create a pane for the object.</summary>
		/// <param name="paneManager"></param>
		/// <param name="newPaneMode"></param>
		/// <param name="arguments"></param>
		/// <returns></returns>
		Task<IPpsWindowPane> OpenPaneAsync(IPpsWindowPaneManager paneManager = null, PpsOpenPaneMode newPaneMode = PpsOpenPaneMode.Default, LuaTable arguments = null);

		/// <summary>Name of the object.</summary>
		string Name { get; }
		/// <summary></summary>
		string MimeType { get; }
	} // interface IPpsDataInfo

	#endregion

	#region -- interface IPpsDataSetProvider ------------------------------------------

	/// <summary></summary>
	public interface IPpsDataSetProvider
	{
		/// <summary></summary>
		/// <param name="datasetName"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		PpsDataSetDefinition TryGetDataSetDefinition(string datasetName, bool throwException = false);
	} // interface IPpsDataSetProvider

	#endregion
}
