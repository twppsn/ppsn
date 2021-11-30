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
using System.Threading.Tasks;
using System.Xml.Linq;
using TecWare.DE.Server;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.Server.Data
{
	#region -- class PpsDataSource ----------------------------------------------------

	/// <summary>Abstract class for a data source configuration item.</summary>
	public abstract class PpsDataSource : DEConfigLogItem
	{
		/// <summary></summary>
		/// <param name="sp"></param>
		/// <param name="name"></param>
		protected PpsDataSource(IServiceProvider sp, string name)
			: base(sp, name)
		{
			Application = sp.GetService<PpsApplication>(true);
		} // ctor

		/// <summary>Create a connection.</summary>
		/// <param name="throwException">If the connection could not created it throws an exception.</param>
		/// <returns></returns>
		public abstract IPpsConnectionHandle CreateConnection(bool throwException = true);

		/// <summary>Create a selector token for the view name.</summary>
		/// <param name="name">Name of the selector.</param>
		/// <param name="sourceDescription">Description for the token.</param>
		/// <returns>Selector</returns>
		public virtual Task<IPpsSelectorToken> CreateSelectorTokenAsync(string name, XElement sourceDescription)
			=> throw new NotImplementedException();

		/// <summary>Create a synchronization session for the client.</summary>
		/// <param name="connection"></param>
		/// <param name="lastSyncronizationStamp"></param>
		/// <param name="leaveConnectionOpen"></param>
		/// <returns></returns>
		public virtual PpsDataSynchronization CreateSynchronizationSession(IPpsConnectionHandle connection, long lastSyncronizationStamp, bool leaveConnectionOpen)
			=> throw new NotImplementedException();

		/// <summary>Create a data manipulation session.</summary>
		/// <param name="connection"></param>
		/// <returns></returns>
		public virtual PpsDataTransaction CreateTransaction(IPpsConnectionHandle connection)
			=> throw new NotImplementedException();

		/// <summary></summary>
		/// <param name="connection"></param>
		/// <param name="selectorName"></param>
		/// <returns></returns>
		public virtual PpsDataSelector CreateSelector(IPpsConnectionHandle connection, string selectorName)
		{
			// support own view definitions
			var view = Application.GetViewDefinition(selectorName, false);
			if (view.SelectorToken.DataSource == this)
				return view.SelectorToken.CreateSelector(connection);
			else // return nothing
				return null;
		} // func CreateSelector

		/// <summary>Returns a native column description.</summary>
		/// <param name="columnName"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		public virtual IPpsColumnDescription GetColumnDescription(string columnName, bool throwException = false)
			=> null;
		
		/// <summary>Create a data set.</summary>
		/// <param name="dataSetName"></param>
		/// <param name="config"></param>
		/// <param name="configurationStamp"></param>
		/// <returns></returns>
		public PpsDataSetServerDefinition CreateDataSetDefinition(string dataSetName, XElement config, DateTime configurationStamp)
			=> new PpsDataSetServerDefinition(this, dataSetName, config, configurationStamp);

		/// <summary></summary>
		/// <param name="dataset"></param>
		/// <param name="tableName"></param>
		/// <param name="config"></param>
		/// <returns></returns>
		public virtual PpsDataTableServerDefinition CreateTableDefinition(PpsDataSetServerDefinition dataset, string tableName, XElement config)
			=> new PpsDataTableServerDefinition(dataset, tableName, config);

		/// <summary>Base type of the data source.</summary>
		public abstract string Type { get; }

		/// <summary>Application object.</summary>
		public PpsApplication Application { get; }

		/// <summary></summary>
		/// <param name="authentificatedUser"></param>
		/// <param name="testUser"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		public static bool EnsureEqualUser(IDEAuthentificatedUser authentificatedUser, IDEAuthentificatedUser testUser, bool throwException)
		{
			if (testUser != null && authentificatedUser != null && !authentificatedUser.Info.Equals(testUser.Info))
			{
				if (throwException)
					throw new ArgumentException("Authentificated user does not match.");
				return false;
			}
			else
				return true;
		} // proc EnsureEqualUser
	} // class PpsDataSource

	#endregion
}
