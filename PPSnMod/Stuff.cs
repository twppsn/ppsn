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
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Deployment.WindowsInstaller;
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

		public readonly static XName xnStoredProcedure = PpsNamespace + "procedure";

		public readonly static XName xnView = PpsNamespace + "view";
		public readonly static XName xnSource = PpsNamespace + "source";
		public readonly static XName xnJoin = PpsNamespace + "join";
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
		[Obsolete]
		public readonly static XName xnWpfAction = PpsNamespace + "action";
		[Obsolete]
		public readonly static XName xnWpfTheme = PpsNamespace + "theme";
		[Obsolete]
		public readonly static XName xnWpfTemplate = PpsNamespace + "template";
		[Obsolete]
		public readonly static XName xnWpfWpfSource = PpsNamespace + "wpfSource";
		[Obsolete]
		public readonly static XName xnWpfCode = PpsNamespace + "code";
		[Obsolete]
		public readonly static XName xnWpfCondition = PpsNamespace + "condition";

		public readonly static XName xnStyle = PpsNamespace + "style";
		public readonly static XName xnStyleGeometry = PpsNamespace + "geometry";
		public readonly static XName xnStyleColor = PpsNamespace + "color";

		public readonly static XName xnClientOptions = PpsNamespace + "clientOptions";
		public readonly static XName xnClientOptionValue = PpsNamespace + "value";
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		#region -- WriteProperty ------------------------------------------------------

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

		#region -- Sql-Helper ---------------------------------------------------------

		#region -- class DataRecordValueStream ----------------------------------------
		
		// ist nur sinnvoll für Long binary, Image
		// todo: Verallgemeinerung?
		private sealed class DataRecordValueStream : Stream
		{
			private readonly IDataRecord r;
			private readonly int columnIndex;
			private long dataOffset = 0;

			public DataRecordValueStream(IDataRecord r, int columnIndex)
			{
				this.r = r ?? throw new ArgumentNullException(nameof(r));
				this.columnIndex = columnIndex;
			} // ctor

			public override void Flush() { }

			public override int Read(byte[] buffer, int offset, int count)
			{
				var readed = (int)r.GetBytes(columnIndex, dataOffset, buffer, offset, count);
				dataOffset += readed;
				return readed;
			} // func Read

			public override long Seek(long offset, SeekOrigin origin)
				=> throw new NotSupportedException();

			public override void SetLength(long value)
				=> throw new NotSupportedException();

			public override void Write(byte[] buffer, int offset, int count)
				=> throw new NotSupportedException();

			public override bool CanRead => true;
			public override bool CanWrite => false;
			public override bool CanSeek => false;

			public override long Position { get => dataOffset; set => throw new NotSupportedException(); }
			public override long Length => throw new NotSupportedException();
		} // class DataRecordValueStream

		#endregion

		#region -- class PropertyReadOnlyDictionaryRecord -----------------------------

		private sealed class PropertyReadOnlyDictionaryRecord : IPropertyReadOnlyDictionary
		{
			private readonly IDataRecord record;

			public PropertyReadOnlyDictionaryRecord(IDataRecord record)
				=> this.record = record ?? throw new ArgumentNullException(nameof(record));

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

		#endregion

		/// <summary>Execute the command and raise a exception with the command text included.</summary>
		/// <param name="command"></param>
		public static void ExecuteNonQueryEx(this DbCommand command)
		{
			try
			{
				command.ExecuteNonQuery();
			}
			catch (Exception e)
			{
				throw new CommandException(command, e);
			}
		} // proc ExecuteNonQueryEx

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

		private static void SetValueWithSize(DbParameter parameter, int length, object value)
		{
			if (parameter.Size == 0)
				parameter.Size = length;
			parameter.Value = value;
		} // proc SetValueWithSize

		/// <summary>Set the DbParameter value, <c>null</c> will be converted to <c>DbNull</c>.</summary>
		/// <param name="parameter"></param>
		/// <param name="value"></param>
		/// <param name="parameterType"></param>
		/// <param name="defaultValue"></param>
		public static void SetValue(this DbParameter parameter, object value, Type parameterType, object defaultValue)
		{
			switch (parameter.DbType)
			{
				case DbType.Xml:
				case DbType.String:
				case DbType.AnsiString:
					if (value == null)
						SetValueWithSize(parameter, 1, defaultValue);
					else if (value == DBNull.Value)
						SetValueWithSize(parameter, 1, DBNull.Value);
					else // this is only needed for SACommand, but should not harm other provider
					{
						var t = value.ChangeType<string>();
						SetValueWithSize(parameter, t != null && t.Length > 0 ? t.Length : 1, t);
					}
					break;
				case DbType.Binary:
					if (value == null)
						SetValueWithSize(parameter, 1, defaultValue);
					if (value == DBNull.Value)
						SetValueWithSize(parameter, 1, DBNull.Value);
					else
					{
						var b = value is string t
							? Encoding.Default.GetBytes(t)
							: value.ChangeType<byte[]>();
						SetValueWithSize(parameter, b != null && b.Length > 0 ? b.Length : 1, b);
					}
					break;
				default:
					parameter.Value = value == null
						? defaultValue
						: value != DBNull.Value ? Procs.ChangeType(value, parameterType) : value;
					break;
			}
		} // proc SetValue

		/// <summary></summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public static Type GetDataTypeFromDbType(DbType type)
		{
			switch (type)
			{
				case DbType.AnsiString:
				case DbType.String:
				case DbType.AnsiStringFixedLength:
				case DbType.StringFixedLength:
				case DbType.Xml:
					return typeof(string);
				case DbType.Binary:
					return typeof(byte[]);

				case DbType.Boolean:
					return typeof(bool);

				case DbType.Decimal:
				case DbType.Currency:
				case DbType.VarNumeric:
					return typeof(decimal);
				case DbType.Double:
					return typeof(double);
				case DbType.Single:
					return typeof(float);

				case DbType.DateTime:
				case DbType.Date:
				case DbType.Time:
					return typeof(DateTime);
				case DbType.DateTime2:
				case DbType.DateTimeOffset:
					return typeof(DateTimeOffset);

				case DbType.Guid:
					return typeof(Guid);

				case DbType.SByte:
					return typeof(sbyte);
				case DbType.Int16:
					return typeof(short);
				case DbType.Int32:
					return typeof(int);
				case DbType.Int64:
					return typeof(long);

				case DbType.Byte:
					return typeof(byte);
				case DbType.UInt16:
					return typeof(ushort);
				case DbType.UInt32:
					return typeof(uint);
				case DbType.UInt64:
					return typeof(ulong);
				case DbType.Object:
					return typeof(object);
				default:
					throw new ArgumentOutOfRangeException(nameof(type), type, "Invalid DbType.");
			}
		} // func GetDataTypeFromDbType

		/// <summary></summary>
		/// <param name="param"></param>
		/// <returns></returns>
		public static Type GetDataType(this DbParameter param)
			=> GetDataTypeFromDbType(param.DbType);

		/// <summary>Return a byte stream from a value.</summary>
		/// <param name="r"></param>
		/// <param name="columnIndex"></param>
		/// <returns></returns>
		public static Stream GetValueStream(this IDataRecord r, int columnIndex)
			=> new DataRecordValueStream(r, columnIndex);

		#endregion

		#region -- Common Scope -------------------------------------------------------

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
				value = default;
				return false;
			}
		} // func TryGetGlobal

		#endregion

		#region -- GetVersionCode -----------------------------------------------------

		private static long GetPart(int value, int pos)
		{
			if (value < 0)
				value = 0;
			else if (value > 0xFFFF)
				throw new ArgumentOutOfRangeException(nameof(value), value, "Version is invalid.");

			return ((long)value) << pos;
		} // func GetPart

		/// <summary>Create a version code, from a version</summary>
		/// <param name="version"></param>
		/// <returns></returns>
		public static long GetVersionCode(this Version version)
			=> GetPart(version.Major, 48) | GetPart(version.Minor, 32) | GetPart(version.Build, 16) | GetPart(version.Revision, 0);

		#endregion

		#region -- Drawing Helper -----------------------------------------------------

		/// <summary>Parse a rectangle.</summary>
		/// <param name="value"></param>
		/// <param name="rectangle"></param>
		/// <returns></returns>
		public static bool TryParseRectangle(string value, out RectangleF rectangle)
		{
			if (value == null)
				goto fail;

			var values = value.Split(',');
			if (values.Length == 4
				&& Single.TryParse(values[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var x)
				&& Single.TryParse(values[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var y)
				&& Single.TryParse(values[2], NumberStyles.Number, CultureInfo.InvariantCulture, out var w)
				&& Single.TryParse(values[3], NumberStyles.Number, CultureInfo.InvariantCulture, out var h)
			)
			{
				rectangle = new RectangleF(x, y, x + w, y + h);
				return true;
			}
			fail:
			rectangle = RectangleF.Empty;
			return false;
		} // func TryParseRectangle


		private static bool TryParseColorPart(string[] values, int idx, out byte result)
		{
			var p = values[idx].Trim();
			if (String.IsNullOrEmpty(p))
			{
				result = 0;
				return true;
			}
			else if (p.IndexOf('.') >= 0 && Double.TryParse(p, NumberStyles.Number, CultureInfo.InvariantCulture, out var f))
			{
				result = unchecked((byte)Convert.ToInt32(f * 255));
				return true;
			}
			else if (Int32.TryParse(p, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
			{
				result = unchecked((byte)i);
				return true;
			}
			else
			{
				result = 0;
				return false;
			}
		} // func TryParseColorPart

		private static bool TryParseColorHexPart(string value, int ofs, out byte result)
		{
			var p = value.Substring(ofs, 2);
			if (Byte.TryParse(p, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result))
				return true;
			else
			{
				result = 0;
				return false;
			}
		} // func TryParseColorHexPart

		/// <summary>Parse a color from a string.</summary>
		/// <param name="value"></param>
		/// <param name="color"></param>
		/// <returns></returns>
		public static bool TryParseColor(string value, out Color color)
		{
			color = Color.Transparent;

			if (String.IsNullOrWhiteSpace(value))
				return false;

			value = value.Trim();
			if (value[0] == '#') // html-format
			{
				switch (value.Length)
				{
					case 9: // ARGB
						if (TryParseColorHexPart(value, 1, out var a)
							&& TryParseColorHexPart(value, 3, out var r)
							&& TryParseColorHexPart(value, 5, out var g)
							&& TryParseColorHexPart(value, 7, out var b))
						{
							color = Color.FromArgb(a, r, g, b);
							return true;
						}
						return false;
					case 7: // RGB
						if (TryParseColorHexPart(value, 1, out r)
							&& TryParseColorHexPart(value, 3, out g)
							&& TryParseColorHexPart(value, 5, out b))
						{
							color = Color.FromArgb(255, r, g, b);
							return true;
						}
						return false;
					default:
						return false;
				}
			}
			else
			{
				var values = value.Split(new char[] { ',', ';' });
				switch (values.Length)
				{
					case 4: // ARGB
						if (TryParseColorPart(values, 0, out var a)
							&& TryParseColorPart(values, 1, out var r)
							&& TryParseColorPart(values, 2, out var g)
							&& TryParseColorPart(values, 3, out var b))
						{
							color = Color.FromArgb(a, r, g, b);
							return true;
						}
						return false;
					case 3: // RGB
						if (TryParseColorPart(values, 0, out r)
							&& TryParseColorPart(values, 1, out g)
							&& TryParseColorPart(values, 2, out b))
						{
							color = Color.FromArgb(255, r, g, b);
							return true;
						}
						return false;
					default:
						return false;
				}
			}
		} // func TryParseColor

		/// <summary>Parse a color from a string.</summary>
		/// <param name="value"></param>
		/// <param name="defaultColor"></param>
		/// <returns></returns>
		public static Color ParseColor(string value, Color defaultColor)
			=> TryParseColor(value, out var cl) ? cl : defaultColor;

		/// <summary></summary>
		/// <param name="color"></param>
		/// <param name="alpha"></param>
		/// <returns></returns>
		public static Color ChangeColorOpacity(Color color, int alpha)
			=> Color.FromArgb(alpha, color.R, color.G, color.B);

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
			: base(GetCleanMessageText(innerException.Message), innerException)
		{
			this.commandText = command.CommandText;
		} // ctor

		/// <summary>Give error code of inner exception.</summary>
		public override int ErrorCode => InnerException is DbException dbx ? dbx.ErrorCode : -1;

		/// <summary>Command text</summary>
		public string CommandText => commandText;

		private static string GetCleanMessageText(string message)
		{
			if (message.StartsWith("RAISERROR ", StringComparison.OrdinalIgnoreCase))
			{
				var pos = message.IndexOf(':');
				return pos > 0 ? message.Substring(pos + 1).Trim() : message;
			}
			else
				return message;
		} // func GetCleanMessageText

	} // class CommandException

	#endregion
}
