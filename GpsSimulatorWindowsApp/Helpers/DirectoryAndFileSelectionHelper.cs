using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GpsSimulatorWindowsApp.Helpers
{
	public static class DirectoryAndFileSelectionHelper
	{
		public static string? PromptForDirectorySelection(string? initialDirectory = null)
		{
			var dialog = new System.Windows.Forms.FolderBrowserDialog();
			if (!string.IsNullOrEmpty(initialDirectory) && System.IO.Directory.Exists(initialDirectory))
			{
				dialog.SelectedPath = initialDirectory;
			}
			else
			{
				// Set initial directory to desktop
				dialog.RootFolder = Environment.SpecialFolder.Desktop;
			}

			var result = dialog.ShowDialog();
			if (result == System.Windows.Forms.DialogResult.OK)
			{
				return dialog.SelectedPath;
			}

			return null;
		}

	}
}
