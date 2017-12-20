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
using System.Data;
using System.Data.Common;
using System.Xml;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Server;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.Server
{
	/// <summary>Helper function collection.</summary>
	public static class PpsStuff
	{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public readonly static XNamespace PpsNamespace = "http://tecware-gmbh.de/dev/des/2015/ppsn";

		public readonly static XName xnRegister = PpsNamespace + "register";
		public readonly static XName xnField = PpsNamespace + "field";
		public readonly static XName xnFieldAttribute = PpsNamespace + "attribute";
		public readonly static XName xnEnvironment = PpsNamespace + "environment";
		public readonly static XName xnCode = PpsNamespace + "code";

		public readonly static XName xnView = PpsNamespace + "view";
		public readonly static XName xnSource = PpsNamespace + "source";
		public readonly static XName xnFilter = PpsNamespace + "filter";
		public readonly static XName xnOrder = PpsNamespace + "order";
		public readonly static XName xnAttribute = PpsNamespace + "attribute";

		public readonly static XName xnColumn = PpsNamespace + "column";

		public readonly static XName xnDataSet = PpsNamespace + "dataset";
		public readonly static XName xnTable = PpsNamespace + "table";
		public readonly static XName xnRelation = PpsNamespace + "relation";
		public readonly static XName xnAutoTag = PpsNamespace + "autoTag";
		public readonly static XName xnMeta = PpsNamespace + "meta";

		public readonly static XName xnWpf = PpsNamespace + "wpf";
		public readonly static XName xnWpfAction = PpsNamespace + "action";
		public readonly static XName xnWpfTheme = PpsNamespace + "theme";
		public readonly static XName xnWpfTemplate = PpsNamespace + "template";
		public readonly static XName xnWpfWpfSource = PpsNamespace + "wpfSource";
		public readonly static XName xnWpfCode = PpsNamespace + "code";
		public readonly static XName xnWpfCondition = PpsNamespace + "condition";

		public readonly static XName xnReports = PpsNamespace + "reports";
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		#region -- WriteProperty --------------------------------------------------------

		/// <summary></summary>
		/// <param name="xml"></param>
		/// <param name="attributes"></param>
		/// <param name="propertyName"></param>
		/// <param name="targetPropertyName"></param>
		public static void WriteProperty(this XmlWriter xml, IPropertyReadOnlyDictionary attributes, string propertyName, string targetPropertyName = null)
		{
			var value = attributes.GetProperty<string>(propertyName, null);
			if (value == null)
				return;

			xml.WriteAttributeString(targetPropertyName ?? propertyName, value);
		} // proc WriteProperty

		/// <summary></summary>
		/// <param name="xml"></param>
		/// <param name="x"></param>
		/// <param name="attributeName"></param>
		/// <param name="targetAttributeName"></param>
		public static void WriteAttributeString(this XmlWriter xml, XElement x, XName attributeName, string targetAttributeName = null)
		{
			var attr = x?.Attribute(attributeName);
			if (attr != null)
				xml.WriteAttributeString(targetAttributeName ?? attributeName.LocalName, attr.Value);
		} // proc WriteAttributeString

		/// <summary>Convert the xml node to an property dictionary.</summary>
		/// <param name="attributes"></param>
		/// <param name="wellKnownProperties"></param>
		/// <returns></returns>
		public static IPropertyReadOnlyDictionary ToPropertyDictionary(this IEnumerable<XElement> attributes, params KeyValuePair<string, Type>[] wellKnownProperties)
		{
			var props = new PropertyDictionary();
			foreach (var x in attributes)
			{
				var propertyName = x.GetAttribute<string>("name", null);
				if (String.IsNullOrEmpty(propertyName))
					throw new ArgumentException("@name is missing.");

				Type dataType;
				var wellKnownPropertyIndex = Array.FindIndex(wellKnownProperties, c => String.Compare(c.Key, propertyName, StringComparison.OrdinalIgnoreCase) == 0);
				if (wellKnownPropertyIndex == -1)
					dataType = LuaType.GetType(x.GetAttribute("dataType", "string"));
				else
					dataType = wellKnownProperties[wellKnownPropertyIndex].Value;

				props.SetProperty(propertyName, dataType, x.Value);
			}
			return props;
		} // func IPropertyReadOnlyDictionary

		#endregion

		#region -- Sql-Helper -----------------------------------------------------------
		
		private sealed class PropertyReadOnlyDictionaryRecord : IPropertyReadOnlyDictionary
		{
			private readonly IDataRecord record;

			public PropertyReadOnlyDictionaryRecord(IDataRecord record)
				=> this.record = record;

			public bool TryGetProperty(string name, out object value)
			{
				for (var i = 0; i < record.FieldCount; i++)
				{
					if (String.Compare(record.GetName(i), name, StringComparison.OrdinalIgnoreCase) == 0)
					{
						value = record.GetValue(i);
						return true;
					}
				}
				value = null;
				return false;
			} // func TryGetProperty
		} // class PropertyReadOnlyDictionaryRecord

		/// <summary>Execute the command and raise a exception with the command text included.</summary>
		/// <param name="command"></param>
		/// <param name="commandBehavior"></param>
		/// <returns></returns>
		public static DbDataReader ExecuteReaderEx(this DbCommand command, CommandBehavior commandBehavior = CommandBehavior.Default)
		{
			try
			{
				return command.ExecuteReader(commandBehavior);
			}
			catch (Exception e)
			{
				throw new CommandException(command, e);
			}
		} // func ExecuteReaderEx

		/// <summary></summary>
		/// <param name="record"></param>
		/// <returns></returns>
		public static IPropertyReadOnlyDictionary ToDictionary(this IDataRecord record)
			=> new PropertyReadOnlyDictionaryRecord(record);

		/// <summary>Set the DbParameter value, <c>null</c> will be converted to <c>DbNull</c>.</summary>
		/// <param name="parameter"></param>
		/// <param name="value"></param>
		/// <param name="parameterType"></param>
		public static void SetValue(this DbParameter parameter, object value, Type parameterType)
		{
			parameter.Value = value == null
				? (object)DBNull.Value
				: (value != DBNull.Value ? Procs.ChangeType(value, parameterType) : value);
		} // proc SetValue

		#endregion

		#region -- Common Scope ---------------------------------------------------------

		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="scope"></param>
		/// <param name="ns"></param>
		/// <param name="variable"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static bool TryGetGlobal<T>(this IDECommonScope scope, object ns, object variable, out T value)
		{
			if (scope.TryGetGlobal(ns, variable, out var v))
			{
				value = (T)v;
				return true;
			}
			else
			{
				value = default(T);
				return false;
			}
		} // func TryGetGlobal

		#endregion
	} // class PpsStuff

	#region -- class CommandException -------------------------------------------------

	/// <summary>Command exception.</summary>
	public class CommandException : DbException
	{
		private readonly string commandText;

		/// <summary></summary>
		/// <param name="command"></param>
		/// <param name="innerException"></param>
		public CommandException(DbCommand command, Exception innerException)
			: base(innerException.Message, innerException)
		{
			this.commandText = command.CommandText;
		} // ctor

		/// <summary>Command text</summary>
		public string CommandText => commandText;
	} // class CommandException

	#endregion
}
