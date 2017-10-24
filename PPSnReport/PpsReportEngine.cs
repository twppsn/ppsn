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
using Neo.IronLua;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.Reporting
{
	#region -- struct PpsReportErrorInfo ----------------------------------------------

	/// <summary></summary>
	internal struct PpsReportErrorInfo
	{
		private readonly string fileName;
		private readonly int lineNumber;
		private readonly string extendedInfo;

		public PpsReportErrorInfo(string fileName, int lineNumber, string extendedInfo)
		{
			this.fileName = fileName;
			this.lineNumber = lineNumber;
			this.extendedInfo = extendedInfo;
		} // ctor

		public string FileName => fileName;
		public int LineNumber => lineNumber;
		public string ExtendedInfo => extendedInfo;
	} // struct PpsReportErrorInfo

	#endregion
	
	#region -- class PpsReportRunInfo -------------------------------------------------

	/// <summary>Report parameter block.</summary>
	public sealed class PpsReportRunInfo
	{
		private readonly string reportName;
		private readonly PropertyDictionary arguments;

		public PpsReportRunInfo(string reportName, PropertyDictionary parentProperties = null)
		{
			this.reportName = reportName ?? throw new ArgumentNullException(nameof(reportName));
			this.arguments = new PropertyDictionary(parentProperties);
		} // ctor

		/// <summary>Name of the report (without the file extension).</summary>
		public string ReportName => reportName;

		/// <summary>Arguments that will be passed to the report (names that start with 'd:' will passed as directive, 't:' are trackers).</summary>
		public PropertyDictionary Arguments => arguments;

		/// <summary>Do not use the system time for the report generation.</summary>
		public DateTime? UseDate { get; set; } = null;
		/// <summary>Run extra imposition pass, given that the style sets up imposition.</summary>
		public bool? Arrange { get; set; } = null;
		/// <summary>Run the report in a safe environment (only no escapes allowed).</summary>
		public bool NoEscapes { get; set; } = false;
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
	
		/// <summary>Gets called for every log line, that is not parsed.</summary>
		public Action<string> DebugOutput { get; set; } = null;
	} // class PpsReportRunInfo

	#endregion

	#region -- class PpsReportException -----------------------------------------------

	/// <summary>Report generation failed.</summary>
	public class PpsReportException : Exception
	{
		private readonly string reportName;
		private readonly string logFileName;
		private readonly int exitCode;
		private readonly PpsReportErrorInfo? info;

		internal PpsReportException(string reportName, string logFileName, int exitCode, string messageText, PpsReportErrorInfo? info, Exception innerException)
			: base(messageText, innerException)
		{
			this.reportName = reportName;
			this.logFileName = logFileName;
			this.exitCode = exitCode;
			this.info = info;
		} // ctor

		/// <summary>Base report file name.</summary>
		public string ReportName => reportName;
		/// <summary>Position of the log file.</summary>
		public string LogFileName => logFileName;
		/// <summary>ExitCode of the process.</summary>
		public int ExitCode => exitCode;
		/// <summary>Extend error info, e.g. code snippets.</summary>
		public string ExtendedInfo => info.HasValue ? info.Value.ExtendedInfo : null;
		/// <summary>Filename where the error occured.</summary>
		public string FileName => info.HasValue ? info.Value.FileName : null;
		/// <summary>Line number</summary>
		public int LineNumber => info.HasValue ? info.Value.LineNumber : 0;
	} // class PpsReportException

	#endregion

	#region -- class PpsReportEngine --------------------------------------------------

	/// <summary>Basic implementation of the LuaTex/ConTeXt reporting engine.</summary>
	public sealed class PpsReportEngine
	{
		private readonly static Regex texErrorLineParse = new Regex(@"tex\serror\son\sline\s(\d+)\sin\sfile\s(.*)\:\s\!\s(.*)", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);
		private readonly static Regex texFileMatch = new Regex(@"(.*?)(\.(\w{2})(-(\w{2}))?)?\.tex$", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

		private const string contextExe = "context.exe";
		private const string contextPath64 = @"texmf-win64\bin";
		private const string contextPath32 = @"texmf-win32\bin";

		#region -- class LuaPipeServer ------------------------------------------------

		/// <summary></summary>
		private sealed class LuaPipeServer : PpsDataServerProtocolBase
		{
			#region -- enum InputState ------------------------------------------------

			private enum InputState
			{
				Parse,
				BeginCollect,
				ErrorCollect
			} // enum InputState

			#endregion

			// base info
			private readonly Process process;

			// streams
			private readonly StreamWriter inputStream;
			private readonly StreamReader outputStream;
			private readonly StreamReader errorStream;

			// error info
			private readonly StringBuilder errorPipeText = new StringBuilder();
			private string lastExceptionMessage = null;
			private PpsReportErrorInfo? currentErrorInfo;
			private Exception currentInnerException = null;

			#region -- Ctor/Dtor ------------------------------------------------------

			public LuaPipeServer(Process process)
			{
				this.process = process ?? throw new ArgumentNullException(nameof(process));
				
				this.outputStream = process.StandardOutput;
				this.errorStream = process.StandardError;
				this.inputStream = process.StandardInput;
			} // ctor

			protected override void Dispose(bool disposing)
			{
				try
				{
					process?.Dispose();
				}
				finally
				{
					base.Dispose(disposing);
				}
			} // proc Dispose

			#endregion

			#region -- Process Pipes --------------------------------------------------

			public async Task ProcessErrorStreamAsync()
			{
				string line;
				while ((line = await errorStream.ReadLineAsync()) != null)
					errorPipeText.AppendLine(line);
			} // proc ProcessErrorStreamAsync

			protected override async Task<LuaTable> PopPacketCoreAsync()
			{
				var state = InputState.Parse;

				var emitEmitLineBefore = false;
				var categorySeperatorPos = -1;
				var exceptionSetted = false; // we collect only the first exception
				var errorInfo = new StringBuilder();

				string line;
				while ((line = await outputStream.ReadLineAsync()) != null)
				{
					var pos = line.IndexOfAny(new char[] { '|', '>' });
					switch (state)
					{
						case InputState.Parse:
							if (pos != -1)
							{
								var commandPrefix = line.Substring(0, pos).TrimEnd();
								var lineData = line.Substring(pos + 1);
								if (commandPrefix == "datacmd")
								{
									return LuaTable.FromLson(lineData); // return parsed data
								}
								else if (commandPrefix == "tex error") // tex-error found
								{
									var m = texErrorLineParse.Match(lineData);
									if (m.Success) // tex error as expected
									{
										if (!exceptionSetted)
										{
											lastExceptionMessage = m.Groups[3].Value;

											currentErrorInfo = new PpsReportErrorInfo(
												 fileName: m.Groups[2].Value,
												 lineNumber: Int32.Parse(m.Groups[1].Value),
												 extendedInfo: null
											);
										}

										categorySeperatorPos = pos;
										state = InputState.BeginCollect; // start collect code block
										errorInfo.Clear();
									}
									else if (!exceptionSetted) // unexpected format
									{
										lastExceptionMessage = lineData.Trim();
										currentErrorInfo = null;
									}

									OnDebugOutput(line);
								}
								else // unknown prefix -> ignore
									OnDebugOutput(line);
							}
							else // this line can not parsed -> ignore
								OnDebugOutput(line);
							break;
						case InputState.BeginCollect:
							if (pos != categorySeperatorPos)
							{
								if (!String.IsNullOrWhiteSpace(line)) // jump over empty lines
								{
									emitEmitLineBefore = false;
									state = InputState.ErrorCollect;
									goto case InputState.ErrorCollect;
								}
							}
							else
							{
								state = InputState.Parse;
								goto case InputState.Parse;
							}
							break;
						case InputState.ErrorCollect:
							if (pos != categorySeperatorPos) // collect lines
							{
								if (String.IsNullOrWhiteSpace(line))
									emitEmitLineBefore = true;
								else
								{
									if (emitEmitLineBefore) // new line handling
									{
										errorInfo.AppendLine();
										emitEmitLineBefore = false;
									}

									// append line
									errorInfo.AppendLine(line);
								}

								OnDebugOutput(line);
							}
							else // prefix line -> parse normal
							{
								if (!exceptionSetted)
								{
									currentErrorInfo = new PpsReportErrorInfo(currentErrorInfo.Value.FileName, currentErrorInfo.Value.LineNumber, errorInfo.ToString());
									exceptionSetted = true;
								}
								state = InputState.Parse;
								goto case InputState.Parse;
							}
							break;
						default:
							throw new InvalidOperationException();
					}
				}

				return null;
			} // func PopPacketCoreAsync

			protected override Task PushPacketCoreAsync(LuaTable t)
			{
				var lineData = t.ToLson(false); // create data
				return inputStream.WriteLineAsync(lineData);
			} // proc PushPacketCoreAsync
			
			public void OnProcessException(Exception exception)
				=> currentInnerException = exception;

			#endregion

			#region -- TryGetError ----------------------------------------------------

			public bool TryGetError(out string messageText, out PpsReportErrorInfo? info, out Exception innerException)
			{
				if (HasErrorInfo)
				{
					// set last exception text
					messageText = String.IsNullOrEmpty(lastExceptionMessage) ? "Report generation failed." : lastExceptionMessage;

					// combine extended info with pipe error
					if (errorPipeText.Length > 0)
					{
						if (currentErrorInfo.HasValue)
						{
							if (currentErrorInfo.Value.ExtendedInfo != null)
							{
								info = new PpsReportErrorInfo(currentErrorInfo.Value.FileName, currentErrorInfo.Value.LineNumber,
									currentErrorInfo.Value.ExtendedInfo + Environment.NewLine + Environment.NewLine + "Error:" + Environment.NewLine + errorPipeText.ToString()
								);
							}
							else
							{
								info = new PpsReportErrorInfo(currentErrorInfo.Value.FileName, currentErrorInfo.Value.LineNumber, errorPipeText.ToString());
							}
						}
						else
							info = new PpsReportErrorInfo(null, -1, errorPipeText.ToString());
					}
					else
						info = currentErrorInfo;

					innerException = currentInnerException;
					return true;
				}
				else
				{
					messageText = null;
					info = null;
					innerException = null;
					return false;
				}
			} // func TryGetError

			#endregion

			private void OnDebugOutput(string text)
				=> DebugOutput?.Invoke(text);

			public Action<string> DebugOutput { get; set; } = null;

			public bool HasErrorInfo => lastExceptionMessage != null || errorPipeText.Length > 0;
		} // class LuaPipeServer

		#endregion

		private readonly DirectoryInfo contextPath;
		private readonly DirectoryInfo reportBase;
		private readonly DirectoryInfo reportLogPath;

		private readonly List<DirectoryInfo> reportSources = new List<DirectoryInfo>();
		private string fontDirectory;
		private int cleanBaseDirectory = 1440; // in min (0 or neg. turns it off)
		private bool zipLogFiles = true;
		private bool storeSuccessLogs = false;
		
		private readonly PpsDataServerProviderBase provider;

		private Dictionary<string, SemaphoreSlim> reportLocks = new Dictionary<string, SemaphoreSlim>();

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary>Create the report engine.</summary>
		/// <param name="contextPath"></param>
		/// <param name="reportBase"></param>
		/// <param name="provider"></param>
		/// <param name="fontDirectory"></param>
		public PpsReportEngine(string contextPath, string reportBase, PpsDataServerProviderBase provider, string reportLogPath = null)
		{
			DirectoryInfo GetLogPath()
			{
				if (reportLogPath == null)
				{
					var di = new DirectoryInfo(Path.GetFullPath(Path.Combine(reportBase, ".logs")));
					if (!di.Exists)
						di.Create();
					return di;
				}
				else if (!Path.IsPathRooted(reportLogPath))
					return new DirectoryInfo(Path.GetFullPath(Path.Combine(reportBase, reportLogPath)));
				else
					return new DirectoryInfo(Path.GetFullPath(reportLogPath));
			} // func GetLogPath

			void CheckPath(DirectoryInfo path, string name)
			{
				if (!path.Exists)
					throw new DirectoryNotFoundException($"Could not locate [{name}]: {path})");
			} // proc CheckPath

			this.contextPath = new DirectoryInfo(Path.GetFullPath(contextPath ?? throw new ArgumentNullException(nameof(contextPath))));
			this.reportBase = new DirectoryInfo(Path.GetFullPath(reportBase ?? throw new ArgumentNullException(nameof(reportBase))));
			this.reportLogPath = GetLogPath();
			FontDirectory = null;

			this.provider = provider ?? throw new ArgumentNullException(nameof(provider));

			// check paths
			CheckPath(this.contextPath, nameof(contextPath));
			CheckPath(this.reportBase, nameof(reportBase));
			CheckPath(this.reportLogPath, nameof(reportLogPath));
			
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

		#region -- CoreRunContextAsync ------------------------------------------------

		private bool TryResolvePath(string path, out string fullPath, out string binPath)
		{
			fullPath = Path.GetFullPath(Path.Combine(contextPath.FullName, path));
			binPath = Path.GetFullPath(Path.Combine(fullPath, contextExe));
			return File.Exists(binPath);
		} // proc ResolvePath

		private async Task<(int exitCode, LuaPipeServer pipeServer)> CoreRunContextAsync(string commandLine, Action<string> debugOutput)
		{
			// find context exe (try first 64bit)
			if (!TryResolvePath(contextPath64, out var fullPath, out var binPath)
				&& !TryResolvePath(contextPath32, out fullPath, out binPath))
				throw new ArgumentException($"Could not locate context.exe ('{contextPath}' -> '{contextPath64}' or '{contextPath32}').");

			// prepare startup
			var psi = new ProcessStartInfo(binPath, commandLine)
			{
				// set report directory
				WorkingDirectory = reportBase.FullName,
				// redirect output
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardError = true,
				RedirectStandardInput = true,
				RedirectStandardOutput = true
			};

			// extend environment
			psi.Environment.Add("PATH", fullPath + ";" + Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User));
			psi.Environment.Add("OSFONTDIR", fontDirectory);

			// run process
			using (var ps = Process.Start(psi))
			{
				var pipeServer = new LuaPipeServer(ps);
				pipeServer.DebugOutput = debugOutput;
				using (var session = new PpsDataServer(pipeServer, provider, pipeServer.OnProcessException))
				{
					await Task.WhenAll(
						session.ProcessMessagesAsync(),
						pipeServer.ProcessErrorStreamAsync(),
						Task.Run(new Action(ps.WaitForExit))
					);

					return (ps.ExitCode, pipeServer);
				}
			}
		} // proc CoreRunContextAsync

		#endregion

		#region -- BuildCacheFilesAsync -----------------------------------------------

		/// <summary>Generate context cache files.</summary>
		/// <returns></returns>
		public async Task BuildCacheFilesAsync()
		{
			// execute command
			var (exitCode, pipeServer) = await CoreRunContextAsync("--generate", null);

			// build exception
			if(exitCode != 0 || pipeServer.HasErrorInfo)
			{
				var sb = new StringBuilder($"Generate cache faild with code {exitCode}.");

				if(pipeServer.TryGetError(out var messageText, out var info, out var innerException))
				{
					sb.AppendLine(messageText);
					if (info.HasValue)
						sb.AppendLine(info.Value.ExtendedInfo);
				}

				throw new Exception(sb.ToString());
			}
		} // proc BuildCacheFilesAsync

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

		private string ResolveReportFileByName(string reportName, string language)
		{
			// report name:
			//   name.tex
			//   name.de.tex
			//   name.de-DE.tex

			var firstSep = reportName.LastIndexOfAny(new char[] { '/', '\\' });

			IEnumerable<FileInfo> GetFullQualified()
			{
				var path = reportName.Substring(firstSep);
				var name = reportName.Substring(firstSep + 1);
				
				return new DirectoryInfo(Path.Combine(reportBase.FullName, path)).EnumerateFiles(name + "*.tex", SearchOption.TopDirectoryOnly);
			} // func GetFullQualified

			IEnumerable<FileInfo> GetNotQualified()
			{
				var pattern = reportName + "*.tex";
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
				var m = texFileMatch.Match(fi.Name);
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
				throw new PpsReportException(reportName, null, 1, "Report file not found.", null, null);

			var fullName = currentMatch.FullName;
			var p = reportBase.FullName.Length;
			if (fullName[p] == '\\' || fullName[p] == '/')
				p++;

			return currentMatch.FullName.Substring(p).Replace('\\', '/');
		} // proc ResolveReportFileByName

		private bool TryGeneratePattern(IEnumerable<PropertyValue> list, int ofs, out string result)
		{
			var sb = new StringBuilder();
			var first = true;
			foreach (var kv in list)
			{
				if (first)
					first = false;
				else
					sb.Append(',');

				var n = kv.Name.Substring(2);
				if (kv.Value == null) // append key only
					sb.Append(n);
				else // append key value
				{
					sb.Append(n)
						.Append('=')
						.Append(kv.Value.ToString());
				}
			}
			result = sb.ToString();
			return !first;
		} // func TryGeneratePattern

		private string RunReportExCommandLine(string resultSession, PpsReportRunInfo args, out string resolvedReportName)
		{
			var commandLine = new StringBuilder(
				"--nonstopmode " +
				"--interface=en " + // how numbers are formatted in the source
				$"--result={resultSession} "
			);

			// append switches
			if (args.UseDate.HasValue)
				commandLine.Append("--nodates=\"")
					.Append(args.UseDate.Value.ToString("yyyy-MM-dd HH:mm"))
					.Append("\"");
			if (args.Arrange.HasValue)
				commandLine.Append(args.Arrange.Value ? "--arrange" : "--noarrange ");
			if (args.NoEscapes)
				commandLine.Append("--paranoid ");

			// parse arguments directives
			if (TryGeneratePattern(args.Arguments.Where(c => c.Name.StartsWith("d:")), 2, out var directives))
				commandLine.Append("--directives=\"").Append(directives).Append("\"");

			// parse trackers
			if (TryGeneratePattern(args.Arguments.Where(c => c.Name.StartsWith("t:")), 2, out var trackers))
				commandLine.Append("--trackers=\"").Append(directives).Append("\"");

			// append file
			commandLine.Append(resolvedReportName = ResolveReportFileByName(args.ReportName, args.Language));

			// append parameters
			foreach (var kv in args.Arguments.Where(c => (c.Name.Length < 2 || c.Name[1] != ':') && c.Value != null))
			{
				commandLine.Append("--")
					.Append(kv.Name)
					.Append("=\"")
					.Append(kv.Value.ChangeType<string>())
					.Append("\"");
			}

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

		private async Task<(FileInfo resultFileInfo, FileInfo logFileInfo)> RunReportExPurgeTempFilesAsync(string resultSession, bool deleteResult, bool purgeAll)
		{
			var logFileInfo = (FileInfo)null;
			var resultFileInfo = (FileInfo)null;
			if (purgeAll)
			{
				var dtDeleteOlderThan = cleanBaseDirectory > 0 ? DateTime.UtcNow.AddMinutes(-cleanBaseDirectory) : (DateTime?)null;
				foreach (var fi in reportBase.EnumerateFiles().Where(c =>
						(c.Attributes & (FileAttributes.ReadOnly | FileAttributes.System | FileAttributes.Hidden)) == (FileAttributes)0
						&& c.Name[0] != '.'
					)
				)
				{
					// compare the session key
					if (fi.Name.StartsWith(resultSession, StringComparison.OrdinalIgnoreCase))
					{
						if (String.Compare(fi.Extension, ".log", StringComparison.OrdinalIgnoreCase) == 0) // is this the log-file
							logFileInfo = fi;
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
				logFileInfo = new FileInfo(Path.Combine(reportBase.FullName, resultSession + ".log"));
				if (!logFileInfo.Exists)
					logFileInfo = null;
			}

			return (resultFileInfo, logFileInfo);
		} // func RunReportExPurgeTempFiles

		private async Task<string> RunReportExMoveLogFileAsync(FileInfo fileInfo, string resolvedReportName, bool containsError)
		{
			if (!storeSuccessLogs && !containsError)
			{
				await DeleteSecureAsync(fileInfo);
				return null;
			}
			else
			{
				// build log file name
				var logFileName = resolvedReportName.Replace('/', '.') + (containsError ? ".err" : ".log") + (zipLogFiles ? ".gz" : String.Empty);
				var targetFile = new FileInfo(Path.Combine(reportLogPath.FullName, logFileName));

				// copy or move current
				if (zipLogFiles)
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
			[nameof(PpsReportRunInfo.Arrange)] = (a, v) => a.Arrange = v.ChangeType<bool>(),
			[nameof(PpsReportRunInfo.DeleteTempFiles)] = (a, v) => a.DeleteTempFiles = v.ChangeType<bool>(),
			[nameof(PpsReportRunInfo.Language)] = (a, v) => a.Language = v.ChangeType<string>(),
			[nameof(PpsReportRunInfo.NoEscapes)] = (a, v) => a.NoEscapes = v.ChangeType<bool>(),
			[nameof(PpsReportRunInfo.UseDate)] = (a, v) => a.UseDate = v.ChangeType<DateTime>(),
			[nameof(PpsReportRunInfo.DebugOutput)] = (a, v) => a.DebugOutput = v as Action<string>,
		};

		/// <summary>Create the report file.</summary>
		/// <param name="args"></param>
		/// <returns>The report target file and a log file.</returns>
		public Task<string> RunReportAsync(LuaTable table)
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

			return RunReportExAsync(args);
		} // func RunReportAsync

		/// <summary>Create the report file.</summary>
		/// <param name="args"></param>
		/// <returns>The report target file and a log file.</returns>
		public async Task<string> RunReportExAsync(PpsReportRunInfo args)
		{
			// build command line, put the result unter an different name
			var resultSession = "_" + Guid.NewGuid().ToString("N");
			var commandLine = RunReportExCommandLine(resultSession, args, out var resolvedReportName);

			using (await LockReportFileAsync(resolvedReportName))
			{
				// run context
				var (exitCode, pipeServer) = await CoreRunContextAsync(commandLine, args.DebugOutput);
				var hasError = pipeServer.TryGetError(out var messageText, out var messageInfo, out var innerException);

				// purge generated files in the root folder should not exist any file
				var result = await RunReportExPurgeTempFilesAsync(resultSession, hasError || exitCode != 0, args.DeleteTempFiles);
				var logFileName = await RunReportExMoveLogFileAsync(result.logFileInfo, resolvedReportName, hasError);

				// raise exception
				if (hasError)
					throw new PpsReportException(resolvedReportName, logFileName, exitCode, messageText, messageInfo, innerException);
				else if (exitCode != 0)
					throw new PpsReportException(resolvedReportName, logFileName, exitCode, $"Unknown error: {exitCode}", null, innerException);

				// build result
				return result.resultFileInfo.FullName;
			}
		} // proc RunReportExAsync

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

		/// <summary>Defined Environment path.</summary>
		public string ContextPath => contextPath.FullName;
		/// <summary>Defined report base path</summary>
		public string BasePath => reportBase.FullName;
		/// <summary>Defined log path</summary>
		public string LogPath => reportLogPath.FullName;

		/// <summary>Set the font path.</summary>
		public string FontDirectory { get=>fontDirectory; set => fontDirectory = Path.GetFullPath(value ?? DefaultFontPath); }

		/// <summary>Is it allowed to clean other files than the session files (in min).</summary>
		public int CleanBaseDirectoryAfter { get => cleanBaseDirectory; set => cleanBaseDirectory = value; }
		/// <summary>Zip resulting log files, to save space.</summary>
		public bool ZipLogFiles { get => zipLogFiles; set => zipLogFiles = value; }
		/// <summary>Store also logs with the result success.</summary>
		public bool StoreSuccessLogs { get => storeSuccessLogs; set => storeSuccessLogs = value; }

		private static string DefaultFontPath => Path.Combine(Environment.SystemDirectory, @"..\Fonts");
	} // class PpsReportEngine

	#endregion
}
