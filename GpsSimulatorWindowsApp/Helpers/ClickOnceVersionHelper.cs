using GpsSimulatorWindowsApp.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace GpsSimulatorWindowsApp.Helpers
{
	public class ClickOnceVersionHelper
	{
		public static string GetVersionSummary()
		{
			try
			{
				ClickOnceDeployment cod = new ClickOnceDeployment(Assembly.GetExecutingAssembly().GetName().Name);

				// Check if we are network deployed
				if (cod.IsNetworkDeployed())
				{
					// Get the version as a string in the form 1.0.0.0
					string VersionString = cod.GetVersionString();

					return VersionString;
				}
				else
				{
					return "N/A";
				}
			}
			catch (Exception ex)
			{
				LogHelper.Error(ex);
				return "N/A";
			}
		}
	}

	public class ClickOnceDeployment
	{
		readonly string Application;

		public ClickOnceDeployment(string assemblyName)
		{
			Application = assemblyName;
		}

		/// <summary>
		/// Check to see if the manifest file exists, 
		/// if so, assume we are network deployed
		/// </summary>
		/// <returns>True / False</returns>
		public bool IsNetworkDeployed()
		{
			string? manifestFilePath = GetManifestPath();

			// And empty path is an error so assume not network deployed
			if (string.IsNullOrEmpty(manifestFilePath))
			{
				return false;
			}

			return File.Exists(manifestFilePath);
		}

		/// <summary>
		/// Get the version from the manifest and return as a string
		/// </summary>
		/// <returns>The version as a string (or null)</returns>
		public string? GetVersionString()
		{
			string? ManifestPath = GetManifestPath();
			if (string.IsNullOrEmpty(ManifestPath))
			{
				return null;
			}

			return GetVersionData(ManifestPath);
		}

		/// <summary>
		/// Get the version data from the manifest file
		/// </summary>
		/// <param name="manifestFilePath"></param>
		/// <returns>The version in string format</returns>
		private string GetVersionData(string manifestFilePath)
		{
			string versionNumber = "";
			if (!String.IsNullOrEmpty(manifestFilePath) && File.Exists(manifestFilePath))
			{
				string manifestContent = File.ReadAllText(manifestFilePath);
				int assembyIdentityStart = manifestContent.IndexOf("asmv1:assemblyIdentity");
				string versionText = "version=\"";
				int versionStart = manifestContent.IndexOf(versionText, assembyIdentityStart);
				int numberStart = versionStart + versionText.Length;
				int numberEnd = manifestContent.IndexOf("\"", numberStart);
				versionNumber = manifestContent.Substring(numberStart, numberEnd - numberStart);
			}

			return versionNumber;
		}

		/// <summary>
		/// Build the manifest file path for a network deployed application
		/// </summary>
		/// <returns>The full file path</returns>
		public string? GetManifestPath()
		{
			string baseDir = AppContext.BaseDirectory;
			string? assemblyName = string.IsNullOrEmpty(Application) ? Assembly.GetExecutingAssembly().GetName().Name : Application;

			if (!String.IsNullOrEmpty(assemblyName))
			{
				string manifestFilePath = Path.Combine(baseDir, $"{assemblyName}.exe.manifest");
				return manifestFilePath;
			}
			else
			{
				return null;
			}
		}
	}
}
