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
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using System.Threading.Tasks;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Server;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.Server.Data
{
	#region -- class PpsUserIdentity --------------------------------------------------

	/// <summary>Special Identity, for the system user.</summary>
	public abstract class PpsUserIdentity : IIdentity, IEquatable<IIdentity>, IDisposable
	{
		#region -- class PpsSystemIdentity --------------------------------------------

		/// <summary>System identity</summary>
		public sealed class PpsSystemIdentity : PpsUserIdentity
		{
			private const string name = "system";

			internal PpsSystemIdentity()
			{
			} // ctor

			/// <summary></summary>
			/// <param name="other"></param>
			/// <returns></returns>
			public override bool Equals(IIdentity other)
				=> other is PpsSystemIdentity;

			/// <summary></summary>
			/// <param name="identity"></param>
			/// <returns></returns>
			protected override PpsCredentials GetCredentialsFromIdentityCore(IIdentity identity)
				=> Equals(identity) ? new PpsIntegratedCredentials(WindowsIdentity.GetCurrent(), false) : null;

			/// <summary></summary>
			public override bool IsAuthenticated => true;
			/// <summary></summary>
			public override string Name => "des\\" + name;
		} // class PpsSystemIdentity

		#endregion

		#region -- class PpsBasicIdentity ---------------------------------------------

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

		#region -- class PpsWindowsIdentity -------------------------------------------

		private sealed class PpsWindowsIdentity : PpsUserIdentity
		{
			private readonly SecurityIdentifier identityId;
			private readonly NTAccount identityAccount;

			public PpsWindowsIdentity(string domainName, string userName)
			{
				this.identityAccount = new NTAccount(domainName, userName);
				this.identityId = (SecurityIdentifier)identityAccount.Translate(typeof(SecurityIdentifier));
			} // ctor

			public override bool Equals(IIdentity other)
			{
				if (other is WindowsIdentity checkWindows && checkWindows.IsAuthenticated)
					return identityId == checkWindows.User;
				else if (other is PpsWindowsIdentity checkWin)
					return identityId == checkWin.identityId;
				else
					return false;
			} // func Equals

			protected override PpsCredentials GetCredentialsFromIdentityCore(IIdentity identity)
				=> identity is WindowsIdentity w ? new PpsIntegratedCredentials(w, true) : null;

			public override string Name => identityAccount.ToString();
			public override bool IsAuthenticated => false;
		} // class PpsWindowsIdentity

		#endregion

		private PpsUserIdentity()
		{
		} // ctor

		/// <summary></summary>
		public void Dispose()
		{
			GC.SuppressFinalize(this);
			Dispose(true);
		} // proc Dispose

		/// <summary></summary>
		/// <param name="disposing"></param>
		protected virtual void Dispose(bool disposing)
		{
		} // proc Dispose

		/// <summary></summary>
		/// <returns></returns>
		public override int GetHashCode()
			=> Name.GetHashCode();

		/// <summary></summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public override bool Equals(object obj)
		{
			if (ReferenceEquals(this, obj))
				return true;
			else if (obj is IIdentity i)
				return Equals(i);
			else
				return false;
		} // func Equals

		/// <summary></summary>
		/// <param name="other"></param>
		/// <returns></returns>
		public abstract bool Equals(IIdentity other);

		/// <summary></summary>
		/// <param name="identity"></param>
		/// <returns></returns>
		public PpsCredentials GetCredentialsFromIdentity(IIdentity identity)
		{
			if (identity == null)
				throw new ArgumentNullException(nameof(identity));

			return GetCredentialsFromIdentityCore(identity) ?? throw new ArgumentException($"Identity from type {identity.GetType().Name} is not compatible.", nameof(identity));
		} // func GetCredentialsFromIdentity

		/// <summary></summary>
		/// <param name="identity"></param>
		/// <returns></returns>
		protected abstract PpsCredentials GetCredentialsFromIdentityCore(IIdentity identity);

		/// <summary>des</summary>
		public string AuthenticationType => "des";
		/// <summary>Immer <c>true</c></summary>
		public abstract bool IsAuthenticated { get; }
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
				? new PpsWindowsIdentity(null, userName)
				: new PpsWindowsIdentity(userName.Substring(0, p), userName.Substring(p + 1));
		} // func CreateIntegratedIdentity
	} // class PpsUserIdentity

	#endregion

	#region -- class PpsCredentials ---------------------------------------------------

	/// <summary>Generice pps credentials</summary>
	public abstract class PpsCredentials : IDisposable
	{
		/// <summary></summary>
		public void Dispose()
		{
			Dispose(true);
		} // proc Dispose

		/// <summary></summary>
		/// <param name="disposing"></param>
		protected virtual void Dispose(bool disposing) { }
	} // class PpsCredentials

	#endregion

	#region -- class PpsIntegratedCredentials -----------------------------------------

	/// <summary>LDAP credentials</summary>
	public sealed class PpsIntegratedCredentials : PpsCredentials
	{
		private readonly WindowsIdentity identity;

		internal PpsIntegratedCredentials(WindowsIdentity identity, bool doClone)
		{
			if (identity == null)
				throw new ArgumentNullException(nameof(identity));

			this.identity = doClone ? (WindowsIdentity)identity.Clone() : identity;
		} // ctor

		/// <summary></summary>
		/// <param name="disposing"></param>
		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			if (disposing)
				identity.Dispose();
		} // proc Dispose

		/// <summary></summary>
		/// <returns></returns>
		public IDisposable Impersonate()
			=> WindowsIdentity.GetCurrent().User != identity.User
				? identity.Impersonate()
				: null;

		/// <summary></summary>
		public string Name => identity.Name;
	} // class PpsIntegratedCredentials

	#endregion

	#region -- class PpsUserCredentials -----------------------------------------------

	/// <summary>Basic user credentials.</summary>
	public sealed class PpsUserCredentials : PpsCredentials
	{
		private readonly string userName;
		private readonly SecureString password;
		
		/// <summary></summary>
		/// <param name="userName"></param>
		/// <param name="password"></param>
		public PpsUserCredentials(string userName, SecureString password)
		{
			this.userName = userName ?? throw new ArgumentNullException(nameof(userName));
			this.password = password ?? throw new ArgumentNullException(nameof(password));
		} // ctor

		internal unsafe PpsUserCredentials(HttpListenerBasicIdentity identity)
		{
			if (identity == null)
				throw new ArgumentNullException(nameof(identity));

			if (String.IsNullOrEmpty(identity.Name))
				throw new ArgumentException($"{nameof(identity)}.{nameof(HttpListenerBasicIdentity.Name)} is null or empty.");

			if (String.IsNullOrEmpty(identity.Password))
				throw new ArgumentException($"{nameof(identity)}.{nameof(HttpListenerBasicIdentity.Password)} is null or empty.");

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
			this.password.MakeReadOnly();
		} // ctor

		/// <summary></summary>
		/// <param name="disposing"></param>
		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			if (disposing)
				password?.Dispose();
		} // proc Dispose

		/// <summary></summary>
		public string UserName => userName;
		/// <summary></summary>
		public SecureString Password => password;
	} // class PpsUserCredentials

	#endregion

	#region -- interface IPpsConnectionHandle -----------------------------------------

	/// <summary>Represents a connection of a data source</summary>
	public interface IPpsConnectionHandle : IDisposable
	{
		/// <summary></summary>
		event EventHandler Disposed;

		/// <summary>Enforces the connection.</summary>
		/// <returns></returns>
		Task<bool> EnsureConnectionAsync(bool throwException = true);

		/// <summary>Is the connection still active.</summary>
		bool IsConnected { get; }

		/// <summary>DataSource of the current connection.</summary>
		PpsDataSource DataSource { get; }
	} // interface IPpsConnectionHandle

	#endregion

	#region -- interface IPpsPrivateDataContext ---------------------------------------

	/// <summary>Hold's the connection and context data for one user.</summary>
	public interface IPpsPrivateDataContext : IDEAuthentificatedUser, IPropertyReadOnlyDictionary, IDisposable
	{
		/// <summary>Returns a pooled connection for a datasource</summary>
		/// <param name="source"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		Task<IPpsConnectionHandle> EnsureConnectionAsync(PpsDataSource source, bool throwException = true);

		/// <summary>Creates a selector for a view.</summary>
		/// <param name="select">Name of the view</param>
		/// <param name="columns">Column definition.</param>
		/// <param name="filter">Filter rules</param>
		/// <param name="order">Order rules</param>
		/// <param name="throwException">Should the method throw on an exception on failure.</param>
		/// <returns></returns>
		Task<PpsDataSelector> CreateSelectorAsync(string select, PpsDataColumnExpression[] columns = null, PpsDataFilterExpression filter = null, PpsDataOrderExpression[] order = null, bool throwException = true);

		/// <summary>Creates a transaction to manipulate data.</summary>
		/// <param name="dataSourceName"></param>
		/// <param name="throwException">Should the method throw on an exception on failure.</param>
		/// <returns></returns>
		Task<PpsDataTransaction> CreateTransactionAsync(string dataSourceName, bool throwException = true);
		/// <summary>Creates a transaction to manipulate data.</summary>
		/// <param name="dataSource">Datasource specified as object.</param>
		/// <param name="throwException">Should the method throw on an exception on failure.</param>
		/// <returns></returns>
		Task<PpsDataTransaction> CreateTransactionAsync(PpsDataSource dataSource, bool throwException = true);

		/// <summary>Creates the credentials for to user for external tasks (like database connections).</summary>
		/// <returns></returns>
		PpsCredentials GetNetworkCredential();
		/// <summary>Creates the credentials for the local computer (e.g. file operations).</summary>
		/// <returns></returns>
		PpsIntegratedCredentials GetLocalCredentials();

		/// <summary>>Determines whether the current user belongs to the specified security token.</summary>
		/// <param name="securityToken"></param>
		/// <returns></returns>
		bool TryDemandToken(string securityToken);

		/// <summary>UserId of the user in the main database.</summary>
		long UserId { get; }
		/// <summary>Name (display info) of the current user.</summary>
		string UserName { get; }
		/// <summary>Returns the current identity.</summary>
		new PpsUserIdentity Identity { get; }
	} // interface IPpsPrivateDataContext

	#endregion

	#region -- interface IPpsColumnDescription ----------------------------------------

	/// <summary>Description for a column.</summary>
	public interface IPpsColumnDescription : IDataColumn
	{
		/// <summary>Returns a specific column implemenation.</summary>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		T GetColumnDescription<T>() where T : IPpsColumnDescription;
	} // interface IPpsProviderColumnDescription

	#endregion

	#region -- class PpsColumnDescription ---------------------------------------------

	/// <summary></summary>
	[DebuggerDisplay("{DebuggerDisplay,nq}")]
	public class PpsColumnDescription : IPpsColumnDescription
	{
		private readonly IPpsColumnDescription parent;
		private readonly IPropertyEnumerableDictionary attributes;

		private readonly string name;
		private readonly Type dataType;

		/// <summary></summary>
		/// <param name="parent"></param>
		/// <param name="name"></param>
		/// <param name="dataType"></param>
		public PpsColumnDescription(IPpsColumnDescription parent, string name, Type dataType)
		{
			this.parent = parent;
			this.attributes = CreateAttributes();

			this.name = name ?? throw new ArgumentNullException(nameof(name));
			this.dataType = dataType ?? throw new ArgumentNullException(nameof(dataType));
		} // ctor

		/// <summary></summary>
		/// <returns></returns>
		protected virtual IPropertyEnumerableDictionary CreateAttributes()
			=> parent == null ? PropertyDictionary.EmptyReadOnly : parent.Attributes;

		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		public T GetColumnDescription<T>() where T : IPpsColumnDescription
			=> this.GetColumnDescriptionParentImplementation<T>(parent);

		private string DebuggerDisplay
			=> $"Column: {Name} : {DataType.Name}";

		/// <summary></summary>
		public string Name => name;
		/// <summary></summary>
		public Type DataType => dataType;

		/// <summary></summary>
		public IPpsColumnDescription Parent => parent;
		/// <summary></summary>
		public IPropertyEnumerableDictionary Attributes => attributes;
	} // class PpsColumnDescription

	/// <summary></summary>
	public static class PpsColumnDescriptionHelper
	{
		#region -- class PpsColumnDescriptionAttributes -------------------------------

		private sealed class PpsColumnDescriptionAttributes : IPropertyEnumerableDictionary
		{
			private readonly IPropertyEnumerableDictionary self;
			private readonly IPropertyEnumerableDictionary parent;
			private readonly List<string> emittedProperties = new List<string>();

			public PpsColumnDescriptionAttributes(IPropertyEnumerableDictionary self, IPropertyEnumerableDictionary parent)
			{
				this.self = self;
				this.parent = parent;
			} // ctor

			private bool PropertyEmitted(string name)
			{
				var idx = emittedProperties.BinarySearch(name, StringComparer.OrdinalIgnoreCase);
				if (idx >= 0)
					return true;
				emittedProperties.Insert(~idx, name);
				return false;
			} // func PropertyEmitted

			public IEnumerator<PropertyValue> GetEnumerator()
			{
				foreach (var c in self)
				{
					if (!PropertyEmitted(c.Name))
						yield return c;
				}

				if (parent != null)
				{
					foreach (var c in parent)
					{
						if (!PropertyEmitted(c.Name))
							yield return c;
					}
				}
			} // func GetEnumerator

			public bool TryGetProperty(string name, out object value)
			{
				if (self.TryGetProperty(name, out value))
					return true;
				else if (parent != null && parent.TryGetProperty(name, out value))
					return true;

				value = null;
				return false;
			} // func TryGetProperty

			IEnumerator IEnumerable.GetEnumerator()
				=> GetEnumerator();
		} // class PpsColumnDescriptionAttributes

		#endregion

		#region -- class PpsDataColumnDescription -------------------------------------

		private sealed class PpsDataColumnDescription : IPpsColumnDescription
		{
			private readonly IPpsColumnDescription parent;
			private readonly IDataColumn column;

			public PpsDataColumnDescription(IPpsColumnDescription parent, IDataColumn column)
			{
				this.parent = parent;
				this.column = column;
			} // ctor

			public T GetColumnDescription<T>()
				where T : IPpsColumnDescription
				=> this.GetColumnDescriptionParentImplementation<T>(parent);

			public string Name => column.Name;
			public Type DataType => column.DataType;

			public IPropertyEnumerableDictionary Attributes
				=> parent == null ? column.Attributes : new PpsColumnDescriptionAttributes(column.Attributes, parent.Attributes);
		} // class PpsDataColumnDescription

		#endregion

		/// <summary></summary>
		/// <param name="properties"></param>
		/// <param name="parent"></param>
		/// <returns></returns>
		public static IPropertyEnumerableDictionary GetColumnDescriptionParentAttributes(IPropertyEnumerableDictionary properties, IPpsColumnDescription parent)
			=> new PpsColumnDescriptionAttributes(properties, parent?.Attributes);

		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="this"></param>
		/// <param name="parent"></param>
		/// <returns></returns>
		public static T GetColumnDescriptionParentImplementation<T>(this IPpsColumnDescription @this, IPpsColumnDescription parent)
			where T : IPpsColumnDescription
		{
			if (parent == null)
				return default(T);
			else if (typeof(T).IsAssignableFrom(@this.GetType()))
				return (T)(IPpsColumnDescription)@this;
			else if (parent != null)
				return parent.GetColumnDescription<T>();
			else
				return default(T);
		} // func GetColumnDescriptionParentImplementation

		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="columnDescription"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static bool TryGetColumnDescriptionImplementation<T>(this IPpsColumnDescription columnDescription, out T value)
			where T : IPpsColumnDescription
			=> (value = columnDescription.GetColumnDescription<T>()) != null;

		/// <summary></summary>
		/// <param name="column"></param>
		/// <param name="parent"></param>
		/// <returns></returns>
		public static IPpsColumnDescription ToColumnDescription(this IDataColumn column, IPpsColumnDescription parent = null)
			=> new PpsDataColumnDescription(parent, column);
	} // class PpsColumnDescriptionHelper

	#endregion

	#region -- interface IPpsSelectorToken --------------------------------------------

	/// <summary></summary>
	public interface IPpsSelectorToken : IDataColumns
	{
		/// <summary>Create a real request to the datasource to retrieve the data.</summary>
		/// <param name="connection"></param>
		/// <param name="alias"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		PpsDataSelector CreateSelector(IPpsConnectionHandle connection, string alias = null, bool throwException = true);

		/// <summary>Get the defintion for a column from the native column name.</summary>
		/// <param name="selectorColumn">Get the column information for the result column.</param>
		/// <returns></returns>
		IPpsColumnDescription GetFieldDescription(string selectorColumn);

		/// <summary>Name of the selector.</summary>
		string Name { get; }

		/// <summary>Attached datasource</summary>
		PpsDataSource DataSource { get; }
	} // interface IPpsSelectorToken

	#endregion
	
	#region -- class PpsPrivateDataContextHelper --------------------------------------

	/// <summary>Simple helpder for Data Context</summary>
	public static class PpsPrivateDataContextHelper
	{
		/// <summary>Create a selector.</summary>
		/// <param name="ctx"></param>
		/// <param name="select"></param>
		/// <param name="columns"></param>
		/// <param name="filter"></param>
		/// <param name="order"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		public static Task<PpsDataSelector> CreateSelectorAsync(this IPpsPrivateDataContext ctx, string select, string columns = null, string filter = null, string order = null, bool throwException = true)
		{
			return ctx.CreateSelectorAsync(
				select,
				PpsDataColumnExpression.Parse(columns).ToArray(),
				PpsDataFilterExpression.Parse(filter),
				PpsDataOrderExpression.Parse(order).ToArray(),
				throwException
			);
		} // func CreateSelectorAsync

		/// <summary>Create a selector.</summary>
		/// <param name="ctx"></param>
		/// <param name="table"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		public static Task<PpsDataSelector> CreateSelectorAsync(this IPpsPrivateDataContext ctx, LuaTable table, bool throwException = true)
		{
			return ctx.CreateSelectorAsync(
				table.GetOptionalValue("select", table.GetOptionalValue("name", (string)null)),
				PpsDataColumnExpression.Parse(table.GetMemberValue("columns")).ToArray(),
				PpsDataFilterExpression.Parse(table.GetMemberValue("filter")),
				PpsDataOrderExpression.Parse(table.GetMemberValue("order")).ToArray(),
				throwException
			);
		} // func CreateSelectorAsync
	} // class PpsPrivateDataContextHelper

	#endregion
}
