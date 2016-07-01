using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
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

		public void Dispose()		{		}

		public bool EnsureConnection(bool throwException = true)
			=> true;

		public PpsDataSource DataSource => dataSource;
		public IPpsPrivateDataContext PrivateUserData => privateUserData;
		public bool IsConnected => true;
	} // class PpsSysConnectionHandle

	#endregion

	#region -- class PpsSysDataSource -------------------------------------------------

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
				var selector = CreateSelector(null, false); // call head
				var columns = selector as IDataColumns;
				if (columns != null)
				{
					columnDescriptions = new IPpsColumnDescription[columns.Columns.Count];
					for (var i = 0; i < columnDescriptions.Length; i++)
						columnDescriptions[i] = selector.GetFieldDescription(columns.Columns[i].Name);;
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

		public PpsSysDataSource(IServiceProvider sp, string name)
			: base(sp, name)
		{
			this.application = sp.GetService<PpsApplication>(true);
		} // ctor

		public override IPpsConnectionHandle CreateConnection(IPpsPrivateDataContext privateUserData, bool throwException = true)
			=> new PpsSysConnectionHandle(this, privateUserData);

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

		public override PpsDataTransaction CreateTransaction(IPpsConnectionHandle connection)
		{
			throw new NotSupportedException();
		} // proc CreateTransaction

		public override string Type => "Sys";
	} // class PpsSysDataSource

	#endregion
}
