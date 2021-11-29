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
using System.Globalization;
using System.Linq;
using System.Net;
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
			{
				if (other is PpsSystemIdentity)
					return true;
				else if (other is WindowsIdentity windowsIdentity)
					return WindowsIdentity.GetCurrent().User == windowsIdentity.User;
				else
					return false;
			} // func Equals

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
				if (other is HttpListenerBasicIdentity http)
				{
					if (String.Compare(userName, other.Name, StringComparison.OrdinalIgnoreCase) != 0)
						return false;
					return ProcsDE.PasswordCompare(http.Password, passwordHash);
				}
				else if (other is PpsBasicIdentity basic)
				{
					if (String.Compare(userName, basic.Name, StringComparison.OrdinalIgnoreCase) != 0)
						return false;
					return Procs.CompareBytes(passwordHash, basic.passwordHash);
				}
				else
					return false;
			} // func Equals

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
			Dispose(true);
			GC.SuppressFinalize(this);
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

		/// <summary>User that created this connection.</summary>
		IDEUser User { get; }
		/// <summary>DataSource of the current connection.</summary>
		PpsDataSource DataSource { get; }
	} // interface IPpsConnectionHandle

	#endregion

	#region -- interface IPpsColumnDescription ----------------------------------------

	/// <summary>Description for a column.</summary>
	public interface IPpsColumnDescription : IDataColumn
	{
		/// <summary>Returns a specific column implementation.</summary>
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

			public PpsColumnDescriptionAttributes(IPropertyEnumerableDictionary self, IPropertyEnumerableDictionary parent)
			{
				this.self = self ?? throw new ArgumentNullException(nameof(self));
				this.parent = parent;
			} // ctor

			private bool PropertyEmitted(List<string> emittedProperties, string name)
			{
				var idx = emittedProperties.BinarySearch(name, StringComparer.OrdinalIgnoreCase);
				if (idx >= 0)
					return true;
				emittedProperties.Insert(~idx, name);
				return false;
			} // func PropertyEmitted

			public IEnumerator<PropertyValue> GetEnumerator()
			{
				var emittedProperties = new List<string>();

				foreach (var c in self)
				{
					if (!PropertyEmitted(emittedProperties, c.Name))
						yield return c;
				}

				if (parent != null)
				{
					foreach (var c in parent)
					{
						if (!PropertyEmitted(emittedProperties, c.Name))
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

		#region -- class PpsAttributeComparer -----------------------------------------

		private sealed class PpsAttributeComparer : IComparer<string>
		{
			private PpsAttributeComparer()
			{
			} // ctor

			public int Compare(string x, string y)
			{
				SplitAttributeName(x, out var xc, out var xn);
				SplitAttributeName(y, out var yc, out var yn);

				var r = String.Compare(xc, yc, StringComparison.OrdinalIgnoreCase);
				if (r == 0)
					return String.Compare(xn, yn, StringComparison.OrdinalIgnoreCase);
				else
					return r;
			} // func Compare

			public static IComparer<string> Comparer { get; } = new PpsAttributeComparer();
		} // class PpsAttributeComparer

		#endregion

		#region -- class PpsAttributeSelector -----------------------------------------

		private sealed class PpsAttributeSelector
		{
#pragma warning disable IDE1006 // Naming Styles
			private const int ALL = 1;
			private const int ROOT = 2;
			private const int NONE = -1;
#pragma warning restore IDE1006 // Naming Styles

			private readonly int emitType = 0; // -1 emit none, 0 check rules, 1 emit all, 2 emit root
			private readonly Dictionary<string, List<Func<string, bool>>> expressions = new Dictionary<string, List<Func<string, bool>>>(StringComparer.OrdinalIgnoreCase);

			public PpsAttributeSelector(string expression)
			{
				if (expression == "*" || expression == null)
					emitType = ROOT;
				else if (expression.Length == 0 || expression == ".")
					emitType = NONE;
				else if (expression == "*.*")
					emitType = ALL;
				else
				{
					foreach (var _expr in expression.Split(','))
					{
						var expr = _expr.Trim();

						if (String.IsNullOrEmpty(expr))
							continue;

						SplitAttributeName(expr, out var attributeClass, out var attributeName);
						if (attributeClass == "*")
							continue; // invalid filter
						else
							AddExpression(attributeClass ?? String.Empty, Procs.GetFilerFunction(attributeName, false));
					}
				}
			} // ctor

			private void AddExpression(string attributeClass, Func<string, bool> func)
			{
				if (!expressions.TryGetValue(attributeClass, out var fn))
					expressions[attributeClass] = fn = new List<Func<string, bool>>();
				fn.Add(func);
			} // proc AddExpression

			public bool IsSelected(string name)
			{
				if (IsEmitNone)
					return false;
				else if (IsEmitAll)
					return true;
				else if (IsEmitRoot && name.IndexOf('.') == -1)
					return true;
				else
				{
					SplitAttributeName(name, out var attributeClass, out var attributeName);
					return expressions.TryGetValue(attributeClass ?? String.Empty, out var fn) && fn != null && fn.Count > 0 && fn.Any(f => f(attributeName));
				}
			} // func IsSelected

			public bool IsSelected(PropertyValue property)
				=> IsSelected(property.Name);

			public bool IsEmitAll => emitType == ALL;
			public bool IsEmitRoot => emitType == ROOT;
			public bool IsEmitNone => emitType == NONE;
		} // class PpsAttributeSelector

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
				return default;
			else if (typeof(T).IsAssignableFrom(@this.GetType()))
				return (T)@this;
			else if (parent != null)
				return parent.GetColumnDescription<T>();
			else
				return default;
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

		/// <summary>Get attributes of this field.</summary>
		/// <param name="column"></param>
		/// <param name="attributeSelector">String selector for attribute names.</param>
		/// <returns></returns>
		public static IEnumerable<PropertyValue> GetAttributes(this IPpsColumnDescription column, string attributeSelector)
		{
			var sel = new PpsAttributeSelector(attributeSelector);
			if (sel.IsEmitNone)
				return Array.Empty<PropertyValue>();
			else
			{
				return sel.IsEmitAll
				  ? column.Attributes
				  : column.Attributes.Where(sel.IsSelected);
			}
		} // func GetAttributes

		/// <summary>Split a attribute name in his parts.</summary>
		/// <param name="name"></param>
		/// <param name="attributeClass"></param>
		/// <param name="attributeName"></param>
		public static void SplitAttributeName(string name, out string attributeClass, out string attributeName)
		{
			var groupSeperator = name.IndexOf('.');
			if (groupSeperator == -1)
			{
				attributeClass = null;
				attributeName = name;
			}
			else
			{
				attributeClass = name.Substring(0, groupSeperator);
				attributeName = name.Substring(groupSeperator + 1);
			}
		} // func GetAttributeClass

		/// <summary>Return a attribute comparer</summary>
		public static IComparer<string> AttributeNameComparer => PpsAttributeComparer.Comparer;
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
}
