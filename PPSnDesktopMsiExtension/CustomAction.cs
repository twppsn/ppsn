using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Web;
using System.Xml;
using Microsoft.Deployment.WindowsInstaller;

namespace PPSnDesktopMsiExtension
{
	public static class CustomActions
	{
		private static void ValidateShell(Session session, string targetPath, string shellName, string shellUri)
		{
			var shellDirectory = new DirectoryInfo(Path.GetFullPath(Path.Combine(targetPath, "env", shellName)));

			if (shellDirectory.Exists)
				session.Log($"Shell Directory exists: {shellDirectory.FullName}");
			else
			{
				session.Log($"Shell directory not exists. Creating: {shellDirectory.FullName}");
				shellDirectory.Create();
			}

			var fi = new FileInfo(Path.Combine(shellDirectory.FullName, "info.xml"));
			if (fi.Exists)
				session.Log("ShellInfo exists. Nothing.");
			else
			{
				session.Log("ShellInfo not exists. Creating: info.xml");

				using (var xml = XmlWriter.Create(fi.FullName))
				{
					xml.WriteStartDocument();
					xml.WriteStartElement("ppsn");
					xml.WriteAttributeString("uri", shellUri);
					xml.WriteEndElement();
					xml.WriteEndDocument();
				}
			}
		} // func ValidateShell

		[CustomAction]
		public static ActionResult RunApp(Session session)
		{
			session.Log("Begin RunApp");

			var applicationPath = session["WixShellExecTarget"];
			if (String.IsNullOrEmpty(applicationPath))
			{
				session.Log("WixShellExecTarget is empty.");
				return ActionResult.Failure;
			}

			applicationPath = session.Format(applicationPath);
			if (!File.Exists(applicationPath))
			{
				session.Log($"Application is missing: '{applicationPath}'");
				return ActionResult.Failure;
			}
			else
				session.Log($"WixShellExecTarget: {applicationPath}");

			var shellName = session["SHELLNAME"];
			if (!String.IsNullOrEmpty(shellName))
			{
				var shellUri = session["SHELLURI"];
				if (String.IsNullOrEmpty(shellUri))
				{
					var sourceDirectory = session["SourceDir"];
					session.Log("SourceDir: " + sourceDirectory);
					if (sourceDirectory.StartsWith("http://")
						|| sourceDirectory.StartsWith("https://"))
					{
						shellUri = new Uri(new Uri(sourceDirectory, UriKind.Absolute), "../").ToString();
					}
				}

				if (!String.IsNullOrEmpty(shellUri))
				{
					session.Log($"Enforce shell '{shellName}' with '{shellUri}'.");

					var targetPath = session.GetTargetPath("ppsn");
					session.Log("TargetPath: " + targetPath);

					ValidateShell(session, targetPath, shellName, shellUri);
				}
			}

			// execute application
			if (String.IsNullOrEmpty(shellName))
			{
				session.Log($"RunApp: {applicationPath}");
				Process.Start(applicationPath);
			}
			else
			{
				session.Log($"RunApp: {applicationPath} -a{shellName}");
				Process.Start(applicationPath, "-a" + shellName);
			}

			return ActionResult.Success;
		} // func RunApp
	} // class CustomActions
}
