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
		/// <summary></summary>
		/// <param name="source"></param>
		/// <param name="name"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		PpsDataSelector CreateSelector(PpsDataSource source, string name, bool throwException = true);

		PpsDataSelector CreateSelector(IPpsSelectorToken selectorToken, bool throwException = true);

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

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public interface IPpsSelectorToken
	{
		PpsDataSelector CreateSelector(IPpsConnectionHandle connection, bool throwException = true);

		PpsDataSource DataSource { get; }
	} // interface IPpsSelectorToken

}
