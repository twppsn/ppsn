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
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.Reporting
{
	#region -- struct PpsReportErrorInfo ----------------------------------------------

	/// <summary>Report error information</summary>
	public struct PpsReportErrorInfo
	{
		private readonly string message;
		private readonly bool isWarning;

		internal PpsReportErrorInfo(string message, bool isWarning)
		{
			this.message = message;
			this.isWarning = isWarning;
		} // ctor

		/// <summary>Message of the report</summary>
		public string Message => message;
		/// <summary>Is this message only a warning.</summary>
		public bool IsWarning => isWarning;
	} // struct PpsReportErrorInfo

	#endregion

	#region -- class PpsReportRunInfo -------------------------------------------------

	/// <summary>Report parameter block.</summary>
	public sealed class PpsReportRunInfo
	{
		private readonly string reportName;
		private readonly PropertyDictionary arguments;

		/// <summary></summary>
		/// <param name="reportName"></param>
		/// <param name="parentProperties"></param>
		public PpsReportRunInfo(string reportName, PropertyDictionary parentProperties = null)
		{
			this.reportName = reportName ?? throw new ArgumentNullException(nameof(reportName));
			this.arguments = new PropertyDictionary(parentProperties);
		} // ctor

		/// <summary>Name of the report (without the file extension).</summary>
		public string ReportName => reportName;

		/// <summary>Arguments that will be passed to the report.</summary>
		public PropertyDictionary Arguments => arguments;

		/// <summary>Do not use the system time for the report generation.</summary>
		public DateTime? UseDate { get; set; } = null;
		/// <summary>Sets the language code.</summary>
		public string Language { get; set; } = "de-DE";
		/// <summary>Only the base language.</summary>
		public string LanguagePartOnly
		{
			get
			{
				if (Language == null)
					return null;
				else if (Procs.TrySplitLanguage(Language, out var languagePart, out var _))
					return languagePart;
				else
					return Language;
			}
		} // prop LanguagePartOnly
		  /// <summary>Remove all files, that will be created during this session.</summary>
		public bool DeleteTempFiles { get; set; } = true;
		/// <summary>Generation runs.</summary>
		public int Runs { get; set; } = 2;

		/// <summary>Gets called for every log line, that is not parsed.</summary>
		public Action<string> DebugOutput { get; set; } = null;
	} // class PpsReportRunInfo

	#endregion

	#region -- class PpsReportException -----------------------------------------------

	/// <summary>Report generation failed.</summary>
	public class PpsReportException : Exception
	{
		private readonly string reportName;
		private readonly string reportSource;
		private readonly string logFileName;
		private readonly int exitCode;
		private readonly PpsReportErrorInfo[] messages;

		internal PpsReportException(string reportName, string reportSource, string logFileName, int exitCode, string messageText, PpsReportErrorInfo[] messages, Exception innerException)
			: base(messageText, innerException)
		{
			this.reportName = reportName;
			this.reportSource = reportSource;
			this.logFileName = logFileName;
			this.exitCode = exitCode;
			this.messages = messages;
		} // ctor

		/// <summary>Base report file name.</summary>
		public string ReportName => reportName;
		/// <summary>Position of the log file.</summary>
		public string LogFileName => logFileName;
		/// <summary>ExitCode of the process.</summary>
		public int ExitCode => exitCode;
		/// <summary>Filename where the error occured.</summary>
		public string FileName => reportSource;
		/// <summary>Detailed messages.</summary>
		public PpsReportErrorInfo[] Messages => messages;
	} // class PpsReportException

	#endregion

	#region -- class PpsReportEngine --------------------------------------------------

	/// <summary>Basic implementation of the LuaTex/ConTeXt reporting engine.</summary>
	public sealed class PpsReportEngine
	{
		/// <summary></summary>
		public static readonly XNamespace ReportDataNamespace = "http://tecware-gmbh.de/dev/des/2015/ppsn/reportData";
		/// <summary></summary>
		public static readonly XName DataElement = ReportDataNamespace + "data";
		/// <summary></summary>
		public static readonly XName ListElement = ReportDataNamespace + "list";
		/// <summary></summary>
		public static readonly XName ListColumnElement = ReportDataNamespace + "column";
		/// <summary></summary>
		public static readonly XName ListFilterElement = ReportDataNamespace + "filter";
		/// <summary></summary>
		public static readonly XName ListOrderElement = ReportDataNamespace + "order";
		/// <summary></summary>
		public static readonly XName DataSetElement = ReportDataNamespace + "dataset";
		/// <summary></summary>
		public static readonly XName ExecuteElement = ReportDataNamespace + "execute";
		/// <summary></summary>
		public static readonly XName ExecuteParameterElement = ReportDataNamespace + "parameter";

		private const string speedataEnginePath = @"bin\sp.exe";
		private readonly static Regex xreportFileMatch = new Regex(@"(.*?)(\.(\w{2})(-(\w{2}))?)?\.xreport", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

		#region -- class PpsReportData ------------------------------------------------

		private sealed class PpsReportData : IDisposable
		{
			private readonly PpsDataServerProviderBase provider;
			private readonly TextWriter tw;
			private readonly string reportFileName;
			private readonly PropertyDictionary arguments;

			private bool isClosed = false;
			
			public PpsReportData(PpsDataServerProviderBase provider, StreamWriter tw, string reportFileName, PropertyDictionary arguments)
			{
				this.provider = provider;
				this.tw = tw;
				this.reportFileName = reportFileName;
				this.arguments = arguments;
			} // ctor

			public void Dispose()
			{
				if (!isClosed)
					tw.Close();
			} // proc Dispose

			private async Task EmitListElementAsync(XmlWriter xml, XElement xInfo)
			{
				// parse parameter
				var listName = xInfo.GetAttribute("select", null);
				var columns = (
					from x in xInfo.Elements(ListColumnElement)
					select new PpsDataColumnExpression(x.GetAttribute("name", null), x.GetAttribute("alias", null))
				).ToArray();

				var filter = PpsDataFilterExpression.Parse(xInfo.GetNode(ListFilterElement, null), variables: arguments);
				var order = PpsDataOrderExpression.Parse(xInfo.GetNode(ListOrderElement, null)).ToArray();

				// access list
				var columnList = (string[])null;
				foreach (var row in await provider.GetListAsync(listName, columns, filter, order))
				{
					await xml.WriteStartElementAsync(null, "r", null);

					// generate column list header
					if (columnList == null)
					{
						columnList = new string[row.Columns.Count];
						for (var i = 0; i < columnList.Length; i++)
							columnList[i] = row.Columns[i].Name;
					}

					// write data
					for (var i = 0; i < columnList.Length; i++)
					{
						await xml.WriteStartElementAsync(null, columnList[i], null);
						await xml.WriteStringAsync(row[i].ChangeType<string>());
						await xml.WriteEndElementAsync();
					}

					await xml.WriteEndElementAsync();
				}
			} // proc EmitListElementAsync
			
			private Task EmitDataSetElementAsync(XmlWriter xml, XElement xInfo)
			{
				throw new NotImplementedException();
			} // proc EmitDataSetElementAsync

			private Task EmitExecuteElementAsync(XmlWriter xml, XElement xInfo) => throw new NotImplementedException();

			public async Task ProcessDataAsync(bool indent)
			{
				// process data to the report layout, to find data description block
				var xData = await FindReportDataAsync(reportFileName, DataElement);

				if (xData == null || !xData.HasElements) // empty data tag, write only a root tag
					await tw.WriteLineAsync("<data/>");
				else
				{
					using (var xml = XmlWriter.Create(tw, new XmlWriterSettings() { Async = true, Indent = indent, NewLineHandling = indent ? NewLineHandling.Entitize : NewLineHandling.None }))
					{
						await xml.WriteStartElementAsync(null, "data", null);

						foreach(var xEmitInfo in xData.Elements())
						{
							var elementName = xEmitInfo.GetAttribute("element", null);
							if (String.IsNullOrEmpty(elementName))
								throw new ArgumentNullException("@element", "Element name is missing.");

							await xml.WriteStartElementAsync(null, elementName, null);

							if (xEmitInfo.Name == ListElement)
								await EmitListElementAsync(xml, xEmitInfo);
							else if (xEmitInfo.Name == DataSetElement)
								await EmitDataSetElementAsync(xml, xEmitInfo); 
							else if (xEmitInfo.Name == ExecuteElement)
								await EmitExecuteElementAsync(xml, xEmitInfo);
							else
								throw new ArgumentOutOfRangeException("dataElement", xEmitInfo.Name.LocalName, "Unknown data element.");

							await xml.WriteEndElementAsync();
						}

						await xml.WriteEndElementAsync();
						await xml.FlushAsync();
					}
				}
				
				// close inputStream
				isClosed = true;
				tw.Close();
			} // proc ProcessDataAsync
		} // class PpsReportData

		#endregion

		private readonly DirectoryInfo engineBase;
		private readonly DirectoryInfo reportBase;
		private readonly DirectoryInfo reportLogPath;
		private readonly DirectoryInfo reportWorkingPath;

		private readonly List<DirectoryInfo> reportSources = new List<DirectoryInfo>();
		private readonly PpsDataServerProviderBase provider;

		private Dictionary<string, SemaphoreSlim> reportLocks = new Dictionary<string, SemaphoreSlim>();

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary>Create the report engine.</summary>
		/// <param name="engineBase"></param>
		/// <param name="reportBase"></param>
		/// <param name="provider"></param>
		/// <param name="reportWorkingPath"></param>
		/// <param name="reportLogPath"></param>
		public PpsReportEngine(string engineBase, string reportBase, PpsDataServerProviderBase provider, string reportWorkingPath = null, string reportLogPath = null)
		{
			DirectoryInfo GetDefaultPath(string currentPath, string def)
			{
				if (currentPath == null)
				{
					var di = new DirectoryInfo(Path.GetFullPath(Path.Combine(reportBase, def)));
					if (!di.Exists)
					{
						di.Create();
						di.Refresh();
					}
					return di;
				}
				else if (!Path.IsPathRooted(currentPath))
					return new DirectoryInfo(Path.GetFullPath(Path.Combine(reportBase, currentPath)));
				else
					return new DirectoryInfo(Path.GetFullPath(currentPath));
			} // func GetLogPath

			void CheckPath(DirectoryInfo path, string name)
			{
				if (!path.Exists)
					throw new DirectoryNotFoundException($"Could not locate [{name}]: {path})");
			} // proc CheckPath

			this.engineBase = new DirectoryInfo(Path.GetFullPath(engineBase ?? throw new ArgumentNullException(nameof(engineBase))));
			this.reportBase = new DirectoryInfo(Path.GetFullPath(reportBase ?? throw new ArgumentNullException(nameof(reportBase))));
			this.reportLogPath = GetDefaultPath(reportLogPath, ".logs");
			this.reportWorkingPath = GetDefaultPath(reportWorkingPath, ".work");

			this.provider = provider ?? throw new ArgumentNullException(nameof(provider));

			// check paths
			CheckPath(this.engineBase, nameof(engineBase));
			CheckPath(this.reportBase, nameof(reportBase));
			CheckPath(this.reportLogPath, nameof(reportLogPath));
			CheckPath(this.reportWorkingPath, nameof(reportWorkingPath));

			// check report sources
			var sourceFile = new FileInfo(Path.Combine(this.reportBase.FullName, ".sources"));
			if (sourceFile.Exists)
			{
				DirectoryInfo GetDirectoryFromLine(string line)
				{
					if (String.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
						return null;

					var di = new DirectoryInfo(Path.GetFullPath(Path.Combine(this.reportBase.FullName, line)));
					return di.Exists ? di : null;
				} // func GetDirectoryFromLine

				reportSources.AddRange(
					from line in File.ReadAllLines(sourceFile.FullName)
					let di = GetDirectoryFromLine(line)
					where di != null
					select di
				);
			}
			else
			{
				reportSources.AddRange(
					from di in this.reportBase.EnumerateDirectories()
					where (di.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0 && di.Name[0] != '.'
					orderby di.Name
					select di
				);
			}
		} // ctor

		#endregion

		#region -- CoreRunEngineAsync -------------------------------------------------

		private bool TryResolvePath(string path, out string binPath)
		{
			binPath = Path.GetFullPath(Path.Combine(engineBase.FullName, path));
			return File.Exists(binPath);
		} // proc ResolvePath

		private async Task<int> CoreRunEngineAsync(string commandLine, Action<string> debugOutput)
		{
			// find context exe (try first 64bit)
			if (!TryResolvePath(speedataEnginePath, out var binPath))
				throw new ArgumentException($"Could not locate sp.exe ('{engineBase.FullName}' -> '{speedataEnginePath}').");

			// prepare startup
			var psi = new ProcessStartInfo(binPath, commandLine)
			{
				// set report directory
				WorkingDirectory = reportWorkingPath.FullName,
				// redirect output
				UseShellExecute = false,
				CreateNoWindow = true
			};

			// activate redirect
			if (debugOutput != null)
			{
				psi.StandardErrorEncoding = Encoding.UTF8;
				psi.StandardOutputEncoding = Encoding.UTF8;
				psi.RedirectStandardError = true;
				psi.RedirectStandardOutput = true;
			}

			// run process
			using (var ps = Process.Start(psi))
			{
				// function to process output streams to console
				async Task ProcessOutputStream(TextReader tr, bool isError)
				{
					string line;
					while ((line = await tr.ReadLineAsync()) != null)
						debugOutput(line);
				} // proc ProcessOutputStream

				// wait for all tasks
				var tasks = new List<Task>(3)
				{
					Task.Run(new Action(ps.WaitForExit))
				};
				if (debugOutput != null)
				{
					tasks.Add(ProcessOutputStream(ps.StandardOutput, false));
					tasks.Add(ProcessOutputStream(ps.StandardError, true));
				}

				await Task.WhenAll(tasks.ToArray());

				return ps.ExitCode;
			}
		} // proc CoreRunEngineAsync

		#endregion

		#region -- RunReportAsync -----------------------------------------------------

		#region -- enum ResolveMatchScore ---------------------------------------------

		[Flags]
		private enum ResolveMatchScore
		{
			None = 0,
			Name = 1,
			Language = 2,
			Country = 4,
			All = Name | Language | Country
		} // enum ResolveMatchScore

		#endregion

		private (string reportPath, string reportFileName, string resolvedReportName) ResolveReportFileByName(string reportName, string language)
		{
			// report name:
			//   name.xreport
			//   name.de.xreport
			//   name.de-DE.xreport

			var firstSep = reportName.LastIndexOfAny(new char[] { '/', '\\' });

			IEnumerable<FileInfo> GetFullQualified()
			{
				var path = reportName.Substring(firstSep);
				var name = reportName.Substring(firstSep + 1);

				return new DirectoryInfo(Path.Combine(reportBase.FullName, path)).EnumerateFiles(name + "*.xreport", SearchOption.TopDirectoryOnly);
			} // func GetFullQualified

			IEnumerable<FileInfo> GetNotQualified()
			{
				var pattern = reportName + "*.xreport";
				foreach (var di in reportSources)
				{
					foreach (var fi in di.EnumerateFiles(pattern, SearchOption.TopDirectoryOnly))
						yield return fi;
				}
			} // func GetNotQualified

			if (reportName.Contains("../")
				|| reportName.Contains("..\\"))
				throw new ArgumentException("Path is not allowed.");

			var reportFiles = firstSep == -1 ? GetNotQualified() : GetFullQualified();
			var matchName = firstSep == -1 ? reportName : reportName.Substring(firstSep + 1);

			var currentMatch = (FileInfo)null;
			var currentScore = ResolveMatchScore.None;
			if (!Procs.TrySplitLanguage(language, out var matchLanguage, out var matchCountry))
			{
				matchLanguage = null;
				matchCountry = null;
			}

			foreach (var fi in reportFiles)
			{
				var m = xreportFileMatch.Match(fi.Name);
				var currentName = m.Groups[1].Value;
				if (String.Compare(currentName, matchName, StringComparison.OrdinalIgnoreCase) != 0)
					continue; // wrong name

				var tmpScore = ResolveMatchScore.Name;

				// test language part
				var currentLanguage = m.Groups[3].Value;
				if (String.IsNullOrEmpty(currentLanguage) && String.IsNullOrEmpty(matchLanguage)
					|| String.Compare(currentLanguage, matchLanguage, StringComparison.OrdinalIgnoreCase) == 0)
					tmpScore |= ResolveMatchScore.Language;

				var currentCountry = m.Groups[5].Value;
				if (String.IsNullOrEmpty(currentCountry) && String.IsNullOrEmpty(matchCountry)
					|| String.Compare(currentCountry, matchCountry, StringComparison.OrdinalIgnoreCase) == 0)
					tmpScore |= ResolveMatchScore.Country;

				if (currentScore < tmpScore)
				{
					currentScore = tmpScore;
					currentMatch = fi;

					if (currentScore == ResolveMatchScore.All) // perfect
						break;
				}
			}

			// check result
			if (currentMatch == null)
				throw new PpsReportException(reportName, null, null, 1, "Report file not found.", null, null);

			var fullName = currentMatch.FullName;
			var p = reportBase.FullName.Length;
			if (fullName[p] == '\\' || fullName[p] == '/')
				p++;

			return (currentMatch.Directory.FullName, currentMatch.Name, currentMatch.FullName.Substring(p).Replace('\\', '/'));
		} // proc ResolveReportFileByName

		private string RunReportExCommandLine(string resultSession, PpsReportRunInfo args, out string resolvedReportName, out string fullReportFileName)
		{
			string EscapeValue(object v)
			{
				var str = v.ToString();
				if (str.IndexOf(' ') >= 0)
					return "\"" + str + "\"";
				return v.ToString();
			} // EscapeValue

			// resolve report
			var r = ResolveReportFileByName(args.ReportName, args.Language);
			resolvedReportName = r.resolvedReportName;
			fullReportFileName = Path.Combine(r.reportPath, r.reportFileName);

			var commandLine = new StringBuilder(
				$"--jobname=\"{resultSession}\" " +
				"--systemfonts " +
				$"--runs={args.Runs} " +
				$"--layout={EscapeValue(r.reportFileName)} " + // layout file
				$"--data=\"{resultSession}.xml\" " + // data is piped via stdin
				$"--mainlanguage={args.Language.Replace('-', '_')} " +
				$"--extra-dir={EscapeValue(r.reportPath)};{reportBase.FullName} "
			);
			
			// append arguments
			foreach (var arg in args.Arguments)
			{
				if (arg.Name.StartsWith("c:")) // command line option
				{
					var commandLineSwitch = arg.Name.Substring(2);
					commandLine.Append("--").Append(commandLineSwitch);
					if (arg.Value != null)
					{
						commandLine.Append('=')
							.Append(EscapeValue(arg.Value))
							.Append(' ');
					}
				}
				else
				{
					commandLine.Append("--var=")
						.Append(EscapeValue($"{arg.Name}={arg.Value.ChangeType<string>()}"))
						.Append(' ');
				}
			}

			// trim end
			if (commandLine[commandLine.Length - 1] == ' ')
				commandLine.Remove(commandLine.Length - 1, 1);
			

			return commandLine.ToString();
		} // proc RunReportExParameters

		private static async Task DeleteSecureAsync(FileInfo fi)
		{
			try
			{
				await Task.Run(new Action(fi.Delete));
			}
			catch { }
		} // proc DeleteSecureAsync

		private async Task<(FileInfo resultFileInfo, FileInfo statusFileInfo, FileInfo logFileInfo)> RunReportExPurgeTempFilesAsync(string resultSession, bool deleteResult, bool purgeAll)
		{
			var logFileInfo = (FileInfo)null;
			var resultFileInfo = (FileInfo)null;
			var statusFileInfo = (FileInfo)null;
			if (purgeAll)
			{
				var dtDeleteOlderThan = CleanBaseDirectoryAfter > 0 ? DateTime.UtcNow.AddMinutes(-CleanBaseDirectoryAfter) : (DateTime?)null;
				foreach (var fi in reportWorkingPath.EnumerateFiles().Where(c =>
						(c.Attributes & (FileAttributes.ReadOnly | FileAttributes.System | FileAttributes.Hidden)) == (FileAttributes)0
						&& c.Name[0] != '.'
					)
				)
				{
					// compare the session key
					if (fi.Name.StartsWith(resultSession, StringComparison.OrdinalIgnoreCase))
					{
						if (String.Compare(fi.Extension, ".protocol", StringComparison.OrdinalIgnoreCase) == 0) // is this the log-file
							logFileInfo = fi;
						else if (String.Compare(fi.Extension, ".status", StringComparison.OrdinalIgnoreCase) == 0) // status xml-file
							statusFileInfo = fi;
						else if (String.Compare(fi.Extension, ".pdf", StringComparison.OrdinalIgnoreCase) == 0) // this is our expected result
							resultFileInfo = fi;
						else
							await DeleteSecureAsync(fi);
					}
					else if (dtDeleteOlderThan.HasValue && fi.LastWriteTimeUtc < dtDeleteOlderThan.Value)
						await DeleteSecureAsync(fi);
				}
			}
			else
			{
				logFileInfo = new FileInfo(Path.Combine(reportWorkingPath.FullName, resultSession + ".protocol"));
				if (!logFileInfo.Exists)
					logFileInfo = null;
			}

			return (resultFileInfo, statusFileInfo, logFileInfo);
		} // func RunReportExPurgeTempFiles

		private async Task<string> RunReportExMoveLogFileAsync(FileInfo fileInfo, string resolvedReportName, bool containsError)
		{
			if (!StoreSuccessLogs && !containsError)
			{
				await DeleteSecureAsync(fileInfo);
				return null;
			}
			else
			{
				// build log file name
				var logFileName = resolvedReportName.Replace('/', '.') + (containsError ? ".err" : ".log") + (ZipLogFiles ? ".gz" : String.Empty);
				var targetFile = new FileInfo(Path.Combine(reportLogPath.FullName, logFileName));

				// copy or move current
				if (ZipLogFiles)
				{
					using (var src = fileInfo.OpenRead())
					using (var dst = new GZipStream(targetFile.Create(), CompressionMode.Compress, false))
					{
						await src.CopyToAsync(dst);
					}

					await DeleteSecureAsync(fileInfo);
				}
				else
				{
					if (targetFile.Exists)
						targetFile.Delete();
					fileInfo.MoveTo(targetFile.FullName);
				}

				return targetFile.FullName;
			}
		} // func RunReportExMoveLogFileAsync

		/// <summary>Create the report file.</summary>
		/// <param name="reportName">Name of the report (without the file extension).</param>
		/// <param name="arguments">Arguments that will be passed to the report.</param>
		/// <returns>The report target file and a log file.</returns>
		public Task<string> RunReportAsync(string reportName, params KeyValuePair<string, object>[] arguments)
		{
			var ri = new PpsReportRunInfo(reportName);
			if ((arguments?.Length ?? 0) > 0)
				ri.Arguments.AddRange(arguments);

			return RunReportExAsync(ri);
		} // func RunReportAsync

		private readonly static Dictionary<string, Action<PpsReportRunInfo, object>> runReportArgsSetter = new Dictionary<string, Action<PpsReportRunInfo, object>>()
		{
			[nameof(PpsReportRunInfo.DeleteTempFiles)] = (a, v) => a.DeleteTempFiles = v.ChangeType<bool>(),
			[nameof(PpsReportRunInfo.Language)] = (a, v) => a.Language = v.ChangeType<string>(),
			[nameof(PpsReportRunInfo.UseDate)] = (a, v) => a.UseDate = v.ChangeType<DateTime>(),
			[nameof(PpsReportRunInfo.Runs)] = (a, v) => a.Runs = v.ChangeType<int>(),
			[nameof(PpsReportRunInfo.DebugOutput)] = (a, v) => a.DebugOutput = v as Action<string>,
		};

		private static PpsReportRunInfo GetReportRunInfoFromTable(LuaTable table)
		{
			var args = new PpsReportRunInfo(table.GetMemberValue("name") as string);

			foreach (var kv in table.Members)
			{
				if (kv.Key == "name" || kv.Value == null)
					continue;

				if (runReportArgsSetter.TryGetValue(kv.Key, out var setter))
					setter(args, kv.Value);
				else
					args.Arguments.SetProperty(kv.Key, kv.Value);
			}

			return args;
		} // func GetReportRunInfoFromTable

		/// <summary>Create the report file.</summary>
		/// <param name="table"></param>
		/// <returns>The report target file and a log file.</returns>
		public Task<string> RunReportAsync(LuaTable table)
			=> RunReportExAsync(GetReportRunInfoFromTable(table));

		/// <summary></summary>
		/// <param name="table"></param>
		/// <returns></returns>
		public Task<string> RunDataAsync(LuaTable table)
			=> RunDataExAsync(GetReportRunInfoFromTable(table));

		private static async Task<(bool hasError, PpsReportErrorInfo[] messages)> ParseErrorInfoAsync(FileInfo statusFileInfo)
		{
			var hasError = false;

			if (statusFileInfo != null)
			{
				var xStatus = XDocument.Load(statusFileInfo.FullName);

				var errors = new List<PpsReportErrorInfo>();
				foreach (var xCur in from x in xStatus.Root.Elements() where x.Name == "Warning" && x.Name == "Error" select x)
				{
					var isWarning = xCur.Name == "Warning";
					errors.Add(new PpsReportErrorInfo(xCur.Value, isWarning));
					if (!isWarning)
						hasError = true;
				}

				await DeleteSecureAsync(statusFileInfo);

				return (hasError, errors.ToArray());
			}
			else
				hasError = true;
			return (hasError, null);
		} // func ParseErrorInfoAsync

		/// <summary>Create the report file.</summary>
		/// <param name="args"></param>
		/// <returns>The report target file and a log file.</returns>
		public async Task<string> RunReportExAsync(PpsReportRunInfo args)
		{
			// build command line, put the result unter an different name
			var resultSession = "_" + Guid.NewGuid().ToString("N");
			var commandLine = RunReportExCommandLine(resultSession, args, out var resolvedReportName, out var fullReportFileName);

			using (await LockReportFileAsync(resolvedReportName))
			{
				// write data.xml
				var dataXml = new FileInfo(Path.Combine(reportWorkingPath.FullName, resultSession + ".xml"));
				if (dataXml.Exists)
					dataXml.Delete();
				using (var tw = new StreamWriter(dataXml.OpenWrite(), Encoding.UTF8, 4096, false))
				using (var data = new PpsReportData(provider, tw, fullReportFileName, args.Arguments))
					await data.ProcessDataAsync(false);

				// run context
				var exitCode = await CoreRunEngineAsync(commandLine, args.DebugOutput);

				// purge generated files in the root folder should not exist any file
				var (resultFileInfo, statusFileInfo, logFileInfo) = await RunReportExPurgeTempFilesAsync(resultSession, exitCode != 0, args.DeleteTempFiles);

				// read status file for exceptions
				var (hasError, messages) = await ParseErrorInfoAsync(statusFileInfo);
				var logFileName = logFileInfo == null ? null : await RunReportExMoveLogFileAsync(logFileInfo, resolvedReportName, hasError);

				// raise exception
				if (hasError)
					throw new PpsReportException(resolvedReportName, resolvedReportName, logFileName, exitCode, String.Join(Environment.NewLine, from c in messages where !c.IsWarning select c.Message), messages, null);
				else if (exitCode != 0)
					throw new PpsReportException(resolvedReportName, resolvedReportName, logFileName, exitCode, $"Unknown error: {exitCode}", null, null);

				// build result
				return resultFileInfo.FullName;
			}
		} // proc RunReportExAsync

		/// <summary>Create the report data.</summary>
		/// <param name="args"></param>
		/// <returns>The report target file and a log file.</returns>
		public async Task<string> RunDataExAsync(PpsReportRunInfo args)
		{
			// build command line, put the result unter an different name
			var (reportPath, reportFileName, resolvedReportName) = ResolveReportFileByName(args.ReportName, args.Language);
			var fullReportFileName = Path.Combine(reportPath, reportFileName);

			var resultFileInfo = new FileInfo(Path.ChangeExtension(Path.Combine(reportWorkingPath.FullName, resolvedReportName.Replace('/', '_')), ".xdata"));
			if (resultFileInfo.Exists)
				resultFileInfo.Delete();

			using (await LockReportFileAsync(resolvedReportName))
			using (var tw = new StreamWriter(resultFileInfo.OpenWrite(), Encoding.UTF8, 4096, false))
			using (var data = new PpsReportData(provider, tw, fullReportFileName, args.Arguments))
			{
				// write xml
				await data.ProcessDataAsync(true);

				// build result
				return resultFileInfo.FullName;
			}
		} // proc RunDataExAsync

		#endregion

		#region -- Lock File for reporting --------------------------------------------

		private async Task<IDisposable> LockReportFileAsync(string reportName)
		{
			while (true)
			{
				SemaphoreSlim semaphoreToWait = null;

				lock (reportLocks)
				{
					if (!reportLocks.TryGetValue(reportName, out semaphoreToWait))
					{
						reportLocks[reportName] = new SemaphoreSlim(0, 1);
						return new DisposableScope(() => UnlockReportFile(reportName));
					}
				}

				await semaphoreToWait.WaitAsync();
			}
		} // func LockReportFileAsync

		private void UnlockReportFile(string reportName)
		{
			lock (reportLocks)
			{
				var ev = reportLocks[reportName];
				reportLocks.Remove(reportName);
				ev.Release();
				ev.Dispose();
			}
		} // proc UnlockReportFile

		#endregion

		#region -- Parse report extra data --------------------------------------------

		/// <summary>Find a special extension node.</summary>
		/// <param name="reportFileName"></param>
		/// <param name="name"></param>
		/// <returns></returns>
		public static async Task<XElement> FindReportDataAsync(string reportFileName, XName name)
		{
			using (var xml = XmlReader.Create(reportFileName, new XmlReaderSettings() { Async = true, IgnoreComments = true, IgnoreWhitespace = true }))
			{
				// move to content
				if (await xml.MoveToContentAsync() != XmlNodeType.Element)
					throw new ArgumentException();

				// we expect a layout element
				if (xml.IsEmptyElement || xml.LocalName != "Layout")
					return null;

				await xml.ReadStartElementAsync("Layout");
				while (xml.NodeType == XmlNodeType.Element)
				{
					if (xml.LocalName == name.LocalName && xml.NamespaceURI == name.NamespaceName) //read element
						return await Task.Run(() => (XElement)XNode.ReadFrom(xml));
					else
						await xml.SkipAsync();
				}
				await xml.ReadEndElementAsync();
			}
			return null;
		} // func FindReportDataAsync

		#endregion

		/// <summary>Defined Environment path.</summary>
		public string EnginePath => engineBase.FullName;
		/// <summary>Defined report base path</summary>
		public string BasePath => reportBase.FullName;
		/// <summary>Defined log path</summary>
		public string LogPath => reportLogPath.FullName;
		/// <summary>Working path.</summary>
		public string WorkingPath => reportWorkingPath.FullName;

		/// <summary>Is it allowed to clean other files than the session files (in min).</summary>
		public int CleanBaseDirectoryAfter { get; set; } = 1440;

		/// <summary>Zip resulting log files, to save space.</summary>
		public bool ZipLogFiles { get; set; } = true;

		/// <summary>Store also logs with the result success.</summary>
		public bool StoreSuccessLogs { get; set; } = false;

		private static string DefaultFontPath => Path.Combine(Environment.SystemDirectory, @"..\Fonts");
	} // class PpsReportEngine

	#endregion
}
