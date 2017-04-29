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
using System.Net;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.Server.Data
{
	#region -- class PpsUserIdentity ----------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Special Identity, for the system user.</summary>
	public abstract class PpsUserIdentity : IIdentity, IEquatable<IIdentity>, IDisposable
	{
		#region -- class PpsSystemIdentity ----------------------------------------------

		public sealed class PpsSystemIdentity : PpsUserIdentity
		{
			private const string name = "system";

			internal PpsSystemIdentity()
			{
			} // ctor

			public override bool Equals(IIdentity other)
				=> other is PpsSystemIdentity;

			protected override PpsCredentials GetCredentialsFromIdentityCore(IIdentity identity)
				=> Equals(identity) ? new PpsIntegratedCredentials(WindowsIdentity.GetCurrent(), false) : null;

			public override bool IsAuthenticated => true;
			public override string Name => name;
		} // class PpsSystemIdentity

		#endregion

		#region -- class PpsBasicIdentity -----------------------------------------------

		private sealed class PpsBasicIdentity : PpsUserIdentity
		{
			private readonly string userName;
			private readonly byte[] passwordHash;

			public PpsBasicIdentity(string userName, byte[] passwordHash)
			{
				this.userName = userName ?? throw new ArgumentNullException(nameof(userName));
				this.passwordHash = passwordHash ?? throw new ArgumentNullException(nameof(passwordHash));
			} // ctor

			protected override void Dispose(bool disposing)
			{
				Array.Clear(passwordHash, 0, passwordHash.Length);
				base.Dispose(disposing);
			} // proc Dispose

			public override bool Equals(IIdentity other)
			{
				if (other is HttpListenerBasicIdentity basicIdentity)
				{
					if (String.Compare(userName, other.Name, StringComparison.OrdinalIgnoreCase) != 0)
						return false;
					return ProcsDE.PasswordCompare(basicIdentity.Password, passwordHash);
				}
				else if (other is PpsBasicIdentity checkSql)
				{
					if (String.Compare(userName, checkSql.Name, StringComparison.OrdinalIgnoreCase) != 0)
						return false;
					return Procs.CompareBytes(passwordHash, checkSql.passwordHash);
				}
				else
					return false;
			} // func Equals

			protected override PpsCredentials GetCredentialsFromIdentityCore(IIdentity identity)
				=> identity is HttpListenerBasicIdentity p ? new PpsUserCredentials(p) : null;

			public override string Name => userName;
			public override bool IsAuthenticated => passwordHash != null;
		} // class PpsBasicIdentity

		#endregion

		#region -- class PpsWindowsIdentity ---------------------------------------------

		private sealed class PpsWindowsIdentity : PpsUserIdentity
		{
			private readonly WindowsIdentity identity;

			public PpsWindowsIdentity(string userName)
			{
				this.identity = new WindowsIdentity(userName);
			} // ctor

			protected override void Dispose(bool disposing)
			{
				if (disposing)
					identity.Dispose();
				base.Dispose(disposing);
			} // proc Dispose

			public override bool Equals(IIdentity other)
			{
				if (other is WindowsIdentity checkWindows && checkWindows.IsAuthenticated)
					return identity.User == checkWindows.User;
				else if (other is PpsWindowsIdentity checkWin)
					return identity.User == checkWin.identity.User;
				else
					return false;
			} // func Equals

			protected override PpsCredentials GetCredentialsFromIdentityCore(IIdentity identity)
				=> identity is WindowsIdentity w ? new PpsIntegratedCredentials(w, true) : null;

			public override string Name => identity.Name;
			public override bool IsAuthenticated => identity.IsAuthenticated;
		} // class PpsWindowsIdentity

		#endregion

		private PpsUserIdentity()
		{
		} // ctor

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			Dispose(true);
		} // proc Dispose

		protected virtual void Dispose(bool disposing)
		{
		} // proc Dispose

		public override int GetHashCode()
			=> Name.GetHashCode();

		public override bool Equals(object obj)
		{
			if (Object.ReferenceEquals(this, obj))
				return true;
			else if (obj is IIdentity i)
				return Equals(i);
			else
				return false;
		} // func Equals

		public abstract bool Equals(IIdentity other);

		internal PpsCredentials GetCredentialsFromIdentity(IIdentity identity)
		{
			if (identity == null)
				throw new ArgumentNullException(nameof(identity));

			return GetCredentialsFromIdentityCore(identity) ?? throw new ArgumentException("Identity from type {identity.GetType().Name} is not compatible.", nameof(identity));
		} // func GetCredentialsFromIdentity

		protected abstract PpsCredentials GetCredentialsFromIdentityCore(IIdentity identity);

		/// <summary>des</summary>
		public string AuthenticationType => "des";
		/// <summary>Immer <c>true</c></summary>
		public abstract bool IsAuthenticated { get; }
		/// <summary>des\system</summary>
		string IIdentity.Name => "des\\" + Name;
		/// <summary>system</summary>
		public abstract string Name { get; }

		/// <summary>Singleton</summary>
		public static PpsUserIdentity System { get; } = new PpsSystemIdentity();

		internal static PpsUserIdentity CreateBasicIdentity(string userName, byte[] passwordHash)
			=> new PpsBasicIdentity(userName, passwordHash);

		internal static PpsUserIdentity CreateIntegratedIdentity(string userName)
		{
			var p = userName.IndexOf('\\');
			return p == -1
				? new PpsWindowsIdentity(userName)
				: new PpsWindowsIdentity(userName.Substring(p + 1) + "@" + userName.Substring(0, p));
		} // func CreateIntegratedIdentity
	} // class PpsUserIdentity

	#endregion

	#region -- class PpsCredentials -----------------------------------------------------

	public abstract class PpsCredentials : IDisposable
	{
		public void Dispose()
		{
			Dispose(true);
		} // proc Dispose

		protected virtual void Dispose(bool disposing) { }
	} // class PpsCredentials

	#endregion

	#region -- class PpsIntegratedCredentials -------------------------------------------

	public sealed class PpsIntegratedCredentials : PpsCredentials
	{
		private readonly WindowsIdentity identity;

		internal PpsIntegratedCredentials(WindowsIdentity identity, bool doClone)
		{
			if (identity == null)
				throw new ArgumentNullException(nameof(identity));

			this.identity = doClone ? (WindowsIdentity)identity.Clone() : identity;
		} // ctor

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			if (disposing)
				identity.Dispose();
		} // proc Dispose

		public IDisposable Impersonate()
			=> identity.Impersonate();
	} // class PpsIntegratedCredentials

	#endregion

	#region -- class PpsUserCredentials -------------------------------------------------

	public sealed class PpsUserCredentials : PpsCredentials
	{
		private readonly string userName;
		private readonly SecureString password;

		internal unsafe PpsUserCredentials(HttpListenerBasicIdentity identity)
		{
			if (identity == null)
				throw new ArgumentNullException(nameof(identity));
			if (String.IsNullOrEmpty(identity.Password))
				throw new ArgumentNullException(nameof(identity) + ".Password");

			// copy the arguments
			this.userName = identity.Name;
			var passwordPtr = Marshal.StringToHGlobalUni(identity.Password);
			try
			{
				this.password = new SecureString((char*)passwordPtr.ToPointer(), identity.Password.Length);
			}
			finally
			{
				Marshal.ZeroFreeGlobalAllocUnicode(passwordPtr);
			}
		} // ctor

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			if (disposing)
				password?.Dispose();
		} // proc Dispose

		public string UserName => userName;
		public SecureString Password => password;
	} // class PpsUserCredentials

	#endregion

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
	public interface IPpsPrivateDataContext : IPropertyReadOnlyDictionary, IDisposable
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

		/// <summary>Creates a transaction to manipulate data.</summary>
		/// <param name="dataSource"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		PpsDataTransaction CreateTransaction(string dataSourceName, bool throwException = true);
		/// <summary></summary>
		/// <param name="dataSource"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		PpsDataTransaction CreateTransaction(PpsDataSource dataSource, bool throwException = true);

		/// <summary>Creates the credentials for to user for external tasks (like database connections).</summary>
		/// <returns></returns>
		PpsCredentials GetNetworkCredential();
		/// <summary>Creates the credentials for the local computer (e.g. file operations).</summary>
		/// <returns></returns>
		PpsIntegratedCredentials GetLocalCredentials();

		/// <summary>UserId of the user in the main database.</summary>
		long UserId { get; }
		/// <summary>Name (display info) of the current user.</summary>
		string UserName { get; }
		/// <summary>Returns the current identity.</summary>
		PpsUserIdentity Identity { get; }
	} // interface IPpsPrivateDataContext

	#endregion

	#region -- interface IPpsColumnDescription ------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Description for a column.</summary>
	public interface IPpsColumnDescription : IDataColumn
	{
		/// <summary>Inherited properties.</summary>
		IPpsColumnDescription Parent { get; }
	} // interface IPpsProviderColumnDescription

	#endregion

	#region -- class PpsColumnDescriptionAttributes -------------------------------------

	public class PpsColumnDescriptionAttributes<T> : IPropertyEnumerableDictionary
		where T : IPpsColumnDescription
	{
		private readonly T owner;

		public PpsColumnDescriptionAttributes(T owner)
		{
			this.owner = owner;
		} // ctor

		public virtual IEnumerator<PropertyValue> GetEnumerator()
		{
			if (owner.Parent != null)
			{
				foreach (var p in owner.Parent.Attributes)
					yield return p;
			}
		} // func GetEnumerator

		public virtual bool TryGetProperty(string name, out object value)
		{
			if (owner.Parent != null)
				return owner.Parent.Attributes.TryGetProperty(name, out value);

			value = null;
			return false;
		} // func TryGetProperty

		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();

		public T Owner => owner;
	} // class PpsColumnDescriptionAttributes

	#endregion

	#region -- class PpsColumnDescription -----------------------------------------------

	public class PpsColumnDescription : IPpsColumnDescription
	{
		private readonly IPpsColumnDescription parent;
		private readonly IPropertyEnumerableDictionary attributes;

		private readonly string name;
		private readonly Type dataType;

		public PpsColumnDescription(IPpsColumnDescription parent, string name, Type dataType)
		{
			this.parent = parent;
			this.attributes = CreateAttributes();

			this.name = name;
			this.dataType = dataType;
		} // ctor

		protected virtual IPropertyEnumerableDictionary CreateAttributes()
			=> new PpsColumnDescriptionAttributes<IPpsColumnDescription>(this);

		public string Name => name;
		public Type DataType => dataType;

		public IPpsColumnDescription Parent => parent;
		public IPropertyEnumerableDictionary Attributes => attributes;
	} // class PpsColumnDescription

	public static class PpsColumnDescriptionHelper
	{
		private sealed class PpsDataColumnDescription : IPpsColumnDescription
		{
			private readonly IPpsColumnDescription parent;
			private readonly IDataColumn column;

			public PpsDataColumnDescription(IPpsColumnDescription parent, IDataColumn column)
			{
				this.parent = parent;
				this.column = column;
			} // ctor

			public string Name => column.Name;
			public Type DataType => column.DataType;

			public IPpsColumnDescription Parent => parent;
			public IPropertyEnumerableDictionary Attributes => column.Attributes;
		} // class PpsDataColumnDescription

		public static T GetColumnDescriptionImplementation<T>(this IPpsColumnDescription columnDescription)
			where T : class
		{
			var t = columnDescription as T;

			if (t == null && columnDescription.Parent != null)
				return GetColumnDescriptionImplementation<T>(columnDescription.Parent);

			return t;
		} // func GetColumnDescriptionImplementation

		public static IPpsColumnDescription ToColumnDescription(this IDataColumn column, IPpsColumnDescription parent = null)
			=> new PpsDataColumnDescription(parent, column);
	} // class PpsColumnDescriptionHelper

	#endregion

	#region -- interface IPpsSelectorToken ----------------------------------------------

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

	#endregion
}
