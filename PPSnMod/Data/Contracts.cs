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
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using Neo.IronLua;

namespace TecWare.PPSn.Server.Data
{
	#region -- interface IPpsConnectionHandle -------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Represents a connection of a data source</summary>
	public interface IPpsConnectionHandle : IDisposable
	{
		event EventHandler Disposed;

		/// <summary>Enforces the connection.</summary>
		/// <returns></returns>
		bool EnsureConnection(bool throwException = true);

		/// <summary>Is the connection still active.</summary>
		bool IsConnected { get; }

		/// <summary>DataSource of the current connection.</summary>
		PpsDataSource DataSource { get; }
	} // interface IPpsConnectionHandle

	#endregion

	#region -- interface IPpsPrivateDataContext -----------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Hold's the connection and context data for one user.</summary>
	public interface IPpsPrivateDataContext : IDisposable
	{
		/// <summary>Creates a selector for a view.</summary>
		/// <param name="name">Name of the view</param>
		/// <param name="filter">Filter rules</param>
		/// <param name="order">Order rules</param>
		/// <param name="throwException">Should the method throw on an exception on failure.</param>
		/// <returns></returns>
		PpsDataSelector CreateSelector(string name, string filter = null, string order = null, bool throwException = true);
		/// <summary>Create selector for a view (lua tables based).</summary>
		/// <param name="table">Same arguments, like the c# version.</param>
		/// <returns></returns>
		PpsDataSelector CreateSelector(LuaTable table);

		///// <summary>Creates a transaction to manipulate data.</summary>
		///// <param name="dataSource"></param>
		///// <param name="throwException"></param>
		///// <returns></returns>
		//PpsDataTransaction CreateTransaction(string dataSource, bool throwException = true);

		//PpsDataTransaction CreateTransaction(PpsDataSource dataSource);

		/// <summary>Name of the current user.</summary>
		string UserName { get; }

		/// <summary></summary>
		PpsUserIdentity User { get; }
		/// <summary></summary>
		NetworkCredential AlternativeCredential { get; }
		/// <summary></summary>
		WindowsIdentity SystemIdentity { get; }
	} // interface IPpsPrivateDataContext

	#endregion

	#region -- class PpsUserIdentity ----------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Special Identity, for the system user.</summary>
	public sealed class PpsUserIdentity : IIdentity
	{
		private readonly string name;

		private PpsUserIdentity()
		{
			this.name = "system";
		} // ctor

	public PpsUserIdentity(string name)
		{
			if (String.IsNullOrEmpty(name) || String.Compare(name, "system", StringComparison.OrdinalIgnoreCase) == 0)
				throw new ArgumentNullException("name");

			this.name = name;
		} // ctor

		/// <summary>des</summary>
		public string AuthenticationType => "des";
		/// <summary>Immer <c>true</c></summary>
		public bool IsAuthenticated => true;
		/// <summary>des\system</summary>
		string IIdentity.Name => "des\\" + name;
		/// <summary>system</summary>
		public string Name => name;

		/// <summary>Singleton</summary>
		public static PpsUserIdentity System { get; } = new PpsUserIdentity();
	} // class PpsUserIdentity

	#endregion

	public interface IPpsColumnDescription
	{
		string Name { get; }
		int MaxLength { get; }
		Type DataType { get; }
		bool IsIdentity { get; }
	} // interface IPpsProviderColumnDescription

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public interface IPpsSelectorToken
	{
		PpsDataSelector CreateSelector(IPpsConnectionHandle connection, bool throwException = true);

		/// <summary>Get the defintion for a column from the native column name.</summary>
		/// <param name="selectorColumn"></param>
		/// <returns></returns>
		IPpsColumnDescription GetFieldDescription(string selectorColumn);

		string Name { get; }
		PpsDataSource DataSource { get; }

		IEnumerable<IPpsColumnDescription> Columns { get; }
	} // interface IPpsSelectorToken
}
