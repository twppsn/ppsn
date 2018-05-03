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
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Linq;
using TecWare.DE.Data;
using TecWare.DE.Server;
using TecWare.DE.Stuff;
using TecWare.PPSn.Server.Data;

namespace TecWare.PPSn.Server
{
	#region -- class PpsSysConnectionHandle -------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsSysConnectionHandle : IPpsConnectionHandle
	{
		/// <summary></summary>
		public event EventHandler Disposed { add { } remove { } }

		private readonly PpsSysDataSource dataSource;
		private readonly IPpsPrivateDataContext privateUserData;

		internal PpsSysConnectionHandle(PpsSysDataSource dataSource, IPpsPrivateDataContext privateUserData)
		{
			this.dataSource = dataSource;
			this.privateUserData = privateUserData;
		} // ctor

		public void Dispose() { }

		public Task<bool> EnsureConnectionAsync(bool throwException = true)
			=> Task.FromResult(true);

		public PpsDataSource DataSource => dataSource;
		public IPpsPrivateDataContext PrivateUserData => privateUserData;
		public bool IsConnected => true;
	} // class PpsSysConnectionHandle

	#endregion

	#region -- class PpsSysDataSource -------------------------------------------------

	/// <summary></summary>
	public sealed class PpsSysDataSource : PpsDataSource
	{
		#region -- class PpsSysMethodSelectorToken --------------------------------------

		private sealed class PpsSysMethodSelectorToken : IPpsSelectorToken
		{
			private readonly PpsSysDataSource dataSource;
			private readonly string name;
			private readonly string path;
			private readonly MethodInfo method;

			private IPpsColumnDescription[] columnDescriptions;

			public PpsSysMethodSelectorToken(PpsSysDataSource dataSource, string name, string path, MethodInfo method)
			{
				this.dataSource = dataSource;
				this.name = name;
				this.path = path;
				this.method = method;
			} // ctor

			public void InitializeColumns()
			{
				var selector = CreateSelector(dataSource.systemConnection, false); // call head
				if (selector is IDataColumns columns)
				{
					columnDescriptions = new IPpsColumnDescription[columns.Columns.Count];
					for (var i = 0; i < columnDescriptions.Length; i++)
						columnDescriptions[i] = selector.GetFieldDescription(columns.Columns[i].Name);
				}
				else
					columnDescriptions = null;
			} // proc InitializeColumnsAsync

			public PpsDataSelector CreateSelector(IPpsConnectionHandle connection, bool throwException = true)
			{
				PpsDataSelector ret = null;
				if (String.IsNullOrEmpty(path))
					ret = InvokeCreateSelector((PpsSysConnectionHandle)connection, dataSource.application, throwException);
				else
				{
					if (!dataSource.application.FirstChildren<DEConfigItem>(
						c => String.Compare(c.Name, path, StringComparison.OrdinalIgnoreCase) == 0,
						c => ret = InvokeCreateSelector((PpsSysConnectionHandle)connection, c, throwException)
					))
					{
						if (throwException)
							throw new ArgumentOutOfRangeException($"Path '{path}' not found.");
						return null;
					}
				}
				return ret;
			} // func CreateSelector

			private PpsDataSelector InvokeCreateSelector(PpsSysConnectionHandle connection, DEConfigItem c, bool throwException)
			{
				try
				{
					var parameterInfo = method.GetParameters();
					var args = new object[parameterInfo.Length];

					for (var i = 0; i < parameterInfo.Length; i++)
					{
						var parameterType = parameterInfo[i].ParameterType;
						if (parameterType == typeof(IPpsPrivateDataContext))
							args[i] = connection?.PrivateUserData;
						else if (parameterType == typeof(PpsDataSource) || parameterType == typeof(PpsSysDataSource))
							args[i] = connection?.DataSource;
						else
							throw new NotImplementedException();
					}

					return (PpsDataSelector)method.Invoke(c, args);
				}
				catch (TargetInvocationException)
				{
					if (throwException)
						throw;
					return null;
				}
			} // proc InvokeCreateSelector

			public IPpsColumnDescription GetFieldDescription(string selectorColumn)
			{
				var description = (IPpsColumnDescription)dataSource.application.GetFieldDescription(name + "." + selectorColumn, false);
				if (description == null)
					description = Array.Find(columnDescriptions, c => String.Compare(c.Name, selectorColumn, StringComparison.OrdinalIgnoreCase) == 0);
				return description;
			} // func GetFieldDescription

			public IEnumerable<IPpsColumnDescription> Columns
				=> columnDescriptions;

			public PpsDataSource DataSource => dataSource;
			public string Name => name;
		} // class PpsSysMethodSelectorToken

		#endregion

		private readonly PpsApplication application;
		private readonly PpsSysConnectionHandle systemConnection;

		/// <summary></summary>
		/// <param name="sp"></param>
		/// <param name="name"></param>
		public PpsSysDataSource(IServiceProvider sp, string name)
			: base(sp, name)
		{
			this.application = sp.GetService<PpsApplication>(true);
			this.systemConnection = new PpsSysConnectionHandle(this, null);
		} // ctor

		/// <summary></summary>
		/// <param name="privateUserData"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		public override IPpsConnectionHandle CreateConnection(IPpsPrivateDataContext privateUserData, bool throwException = true)
			=> new PpsSysConnectionHandle(this, privateUserData);

		/// <summary></summary>
		/// <param name="name"></param>
		/// <param name="sourceDescription"></param>
		/// <returns></returns>
		public override Task<IPpsSelectorToken> CreateSelectorTokenAsync(string name, XElement sourceDescription)
		{
			var viewType = sourceDescription.GetAttribute("type", "view");
			if (viewType != "view")
				throw new DEConfigurationException(sourceDescription, "Only 'view' is allowed on @type.");

			// [applicationNode]/function
			var viewDescription = sourceDescription.Value;

			var sep = viewDescription.LastIndexOf('/');
			var path = sep == -1 ? String.Empty : viewDescription.Substring(0, sep);
			var methodName = sep == -1 ? viewDescription : viewDescription.Substring(sep + 1);
			MethodInfo method = null;
			if (path.Length == 0)
				method = application.GetType().GetMethod(methodName);
			else
			{
				application.FirstChildren<DEConfigItem>(
					c => String.Compare(c.Name, path, StringComparison.OrdinalIgnoreCase) == 0,
					c =>
					{
						method = c.GetType().GetMethod(methodName);
					});
			}
			if (method == null)
				throw new FormatException($"Sys view format '{viewDescription}' not resolved.");

			var selector = new PpsSysMethodSelectorToken(this, name, path, method);
			return Task.Run<IPpsSelectorToken>(() => { selector.InitializeColumns(); return selector; });
		} // proc CreateSelectorTokenAsync

		/// <summary></summary>
		/// <param name="connection"></param>
		/// <returns></returns>
		public override PpsDataTransaction CreateTransaction(IPpsConnectionHandle connection)
		{
			throw new NotSupportedException();
		} // proc CreateTransaction

		/// <summary></summary>
		/// <param name="privateUserData"></param>
		/// <param name="lastSynchronization"></param>
		/// <returns></returns>
		public override PpsDataSynchronization CreateSynchronizationSession(IPpsPrivateDataContext privateUserData, DateTime lastSynchronization)
			=> new PpsDataSynchronization(application, CreateConnection(privateUserData, true), lastSynchronization);

		/// <summary></summary>
		public override string Type => "Sys";
	} // class PpsSysDataSource

	#endregion
}
