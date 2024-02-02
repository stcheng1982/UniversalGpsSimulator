using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GpsSimulatorWindowsApp.DataType;
using GpsSimulatorWindowsApp.Dialogs;
using GpsSimulatorWindowsApp.Helpers;
using GpsSimulatorWindowsApp.WebViewHost;
using Microsoft.Identity.Client;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace GpsSimulatorWindowsApp.ViewModel
{
	public class EditDrivingRouteViewModel : ObservableObject, IDisposable
	{
		private const string RouteEditorWebViewProxyKey = "RouteEditorWebViewProxy";
		private const string RouteEditorPageUrl = "https://web.local/vdrive/routeeditor.html";

		private WebView2 _routeEditorWebView;
		private RouteEditorWebViewProxy _routeEditorWebViewProxy;

		private Action _closeViewAction;
		private bool _webViewLoaded = false;
		private string? _selectedRouteName = null;
		private bool _isRenamingRoute = false;

		private bool _disposed;

		public EditDrivingRouteViewModel(
			VirtualDrivingRoute? selectedRoute,
			Action closeAction)
		{
			_routeEditorWebViewProxy = new RouteEditorWebViewProxy(this);

			_closeViewAction = closeAction;

			// Initialize Driving Route properties
			var drivingRoutes = VirtualDrivingDataHelper.FindAllDrivingRoutesInLocalStorage();
			DrivingRouteNames = new ObservableCollection<string>(drivingRoutes.Select(r => r.Name));
			SelectedRouteName = selectedRoute?.Name;


			// Initialize commands
			CloseRoutesEditorCommand = new RelayCommand(CloseRouteEditingView);
			EditRouteNameCommand = new RelayCommand(EditRouteName);
			SaveRouteCommand = new RelayCommand(SaveRoute);
			DeleteRouteCommand = new RelayCommand(DeleteRoute);
			GenerateDrivingGpsEventsCommand = new RelayCommand(GenerateDrivingGpsEventsBySelectedRoute);
			CreateRouteCommand = new RelayCommand(CreateNewRoute);

			// Bind event handlers
			_routeEditorWebViewProxy.DrivingRouteSaved += HandleDrivingRouteSavedEvent;
			_routeEditorWebViewProxy.AutoDrivingGpsEventsGenerated += HandleGeneratedAutoDrivingGpsEvents;
		}

		public ObservableCollection<string> DrivingRouteNames { get; private set; }

		public bool WebViewLoaded
		{
			get => _webViewLoaded;
			set
			{
				SetProperty(ref _webViewLoaded, value, nameof(WebViewLoaded));
				OnPropertyChanged(nameof(HasSelectedRoute));
			}
		}

		public string? SelectedRouteName
		{
			get => _selectedRouteName;
			set
			{
				SetProperty(ref _selectedRouteName, value, nameof(SelectedRouteName));
				OnPropertyChanged(nameof(HasSelectedRoute));

				if (!string.IsNullOrEmpty(value) || !_isRenamingRoute)
				{
					// When user select a route, trigger loading action in WebView
					// Unless user is renaming a route
					TriggerLoadingRouteActionInWebView();
				}
			}
		}

		public bool HasSelectedRoute
		{
			get => WebViewLoaded && !string.IsNullOrEmpty(SelectedRouteName);
		}

		public IRelayCommand CloseRoutesEditorCommand { get; private set; }

		public IRelayCommand EditRouteNameCommand { get; private set; }

		public IRelayCommand SaveRouteCommand { get; private set; }

		public IRelayCommand DeleteRouteCommand { get; private set; }

		public IRelayCommand GenerateDrivingGpsEventsCommand { get; private set; }

		public IRelayCommand CreateRouteCommand { get; private set; }

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}

			// Release managed resource
			_routeEditorWebViewProxy.AutoDrivingGpsEventsGenerated -= HandleGeneratedAutoDrivingGpsEvents;
			_routeEditorWebViewProxy.DrivingRouteSaved -= HandleDrivingRouteSavedEvent;
			_routeEditorWebView?.Dispose();

			_disposed = true;
		}


		public async Task InitializeRouteEditorWebView2Async(WebView2 webView)
		{
			_routeEditorWebView = webView;
			await webView.EnsureCoreWebView2Async();

			// Mapping Virtual Host
			var localWebDir = System.IO.Path.Combine(AppContext.BaseDirectory, "LocalWeb");
			webView.CoreWebView2.SetVirtualHostNameToFolderMapping("web.local", localWebDir, CoreWebView2HostResourceAccessKind.Allow);

			// TODO: might need to include compile the following cache cleaning code for Debug build
			await webView.CoreWebView2.Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.CacheStorage | CoreWebView2BrowsingDataKinds.DiskCache);

			// Inject hosted objects
			webView.CoreWebView2.AddHostObjectToScript(RouteEditorWebViewProxyKey, _routeEditorWebViewProxy);

			// Load Esri Route edit page
			webView.CoreWebView2.Navigate(RouteEditorPageUrl);
			webView.CoreWebView2.NavigationCompleted += (o, e) =>
			{
				// Mark WebView loaded
				WebViewLoaded = true;
			};
		}

		private void CloseRouteEditingView()
		{
			_closeViewAction?.Invoke();
		}

		private void HandleDrivingRouteSavedEvent(object? sender, string savedRouteName)
		{
			
		}

		private void HandleGeneratedAutoDrivingGpsEvents(object? sender, List<HistoryGpsEvent> gpsEvents)
		{
			try
			{
				if (gpsEvents == null || gpsEvents.Count == 0)
				{
					MessageBox.Show("No GPS Event generated.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
					return;
				}

				// Prompt to save GPS Events
				TrySavingGeneratedAutoDrivingGpsEventsAsync(gpsEvents);

			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		private void EditRouteName()
		{
			var oldRouteName = SelectedRouteName;
			var newRouteName = oldRouteName;
			var inputRouteNameDialog = new InputNameDialog() { WindowStartupLocation = WindowStartupLocation.CenterScreen };
			var inputRouteNameVM = new InputNamePromptViewModel(
				"Rename Route",
				"Route Name",
				newRouteName,
				"Apply",
				"Cancel",
				(name) =>
				{
					var nameValidationError = VirtualDrivingDataHelper.ValidateRouteName(name);
					if (!string.IsNullOrEmpty(nameValidationError))
					{
						return nameValidationError;
					}

					if (VirtualDrivingDataHelper.RouteNameExistsInLocalStorage(name))
					{
						return $"Route Name '{name}' already exists";
					}

					return null;
				},
				() =>
				{
					inputRouteNameDialog.DialogResult = true;
					inputRouteNameDialog.Close();
				},
				() =>
				{
					inputRouteNameDialog.DialogResult = false;
					inputRouteNameDialog.Close();
				});

			inputRouteNameDialog.DataContext = inputRouteNameVM;
			var dialogResult = inputRouteNameDialog.ShowDialog();
			if (true != dialogResult)
			{
				return;
			}

			_isRenamingRoute = true;
			newRouteName = inputRouteNameVM.InputValue;
			VirtualDrivingDataHelper.RenameRouteInLocalStorage(oldRouteName, newRouteName);
			TriggerRenameRouteActionInWebView(newRouteName);

			DrivingRouteNames.Remove(oldRouteName);
			DrivingRouteNames.Add(newRouteName);
			SelectedRouteName = newRouteName;

			_isRenamingRoute = false;
		}

		private void SaveRoute()
		{
			TriggerSaveRouteActionInWebView();
		}

		private void DeleteRoute()
		{
			var routeName = SelectedRouteName;
			if (string.IsNullOrEmpty(routeName))
			{
				return;
			}

			var confirmDelete = MessageBox.Show($"Are you sure to delete route '{routeName}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
			if (confirmDelete == MessageBoxResult.Yes)
			{
				TriggerCloseEditingRouteInWebView();
				VirtualDrivingDataHelper.DeleteRouteFromLocalStorage(routeName);
				SelectedRouteName = null;
				DrivingRouteNames.Remove(routeName);
			}
		}

		private void GenerateDrivingGpsEventsBySelectedRoute()
		{
			var routeName = SelectedRouteName;
			if (string.IsNullOrEmpty(routeName))
			{
				return;
			}

			var parametersInputDialog = new AutoDrivingGpsEventsGenerationSettingsDialog();
			var parametersInputVM = new AutoDrivingGpsEventsGenerationParametersViewModel(
				new NaiveAutoDrivingPlanParameters(),
				() =>
				{
					parametersInputDialog.DialogResult = true;
					parametersInputDialog.Close();
				},
				() =>
				{
					parametersInputDialog.DialogResult = false;
					parametersInputDialog.Close();
				});
			
			parametersInputDialog.DataContext = parametersInputVM;
			var dialogResult = parametersInputDialog.ShowDialog();
			if (true != dialogResult)
			{
				return;
			}

			var parameters = parametersInputVM.GetAppliedDrivingPLanParameters();
			TriggerAutoDrivingGpsEventsGenerationActionInWebView(parameters);
		}

		private void CreateNewRoute()
		{
			var newRouteName = "New Route";
			var inputRouteNameDialog = new InputNameDialog() { WindowStartupLocation = WindowStartupLocation.CenterScreen };
			var inputRouteNameVM = new InputNamePromptViewModel(
				"Create New Route",
				"Route Name",
				newRouteName,
				"Create",
				"Cancel",
				(name) =>
				{
					var nameValidationError = VirtualDrivingDataHelper.ValidateRouteName(name);
					if (!string.IsNullOrEmpty(nameValidationError))
					{
						return nameValidationError;
					}

					if (VirtualDrivingDataHelper.RouteNameExistsInLocalStorage(name))
					{
						return $"Route Name '{name}' already exists";
					}

					return null;
				},
				() =>
				{
					inputRouteNameDialog.DialogResult = true;
					inputRouteNameDialog.Close();
				},
				() =>
				{
					inputRouteNameDialog.DialogResult = false;
					inputRouteNameDialog.Close();
				});
			
			inputRouteNameDialog.DataContext = inputRouteNameVM;
			var dialogResult = inputRouteNameDialog.ShowDialog();
			if (true != dialogResult)
			{
				return;
			}

			newRouteName = inputRouteNameVM.InputValue;
			DrivingRouteNames.Add(newRouteName);
			SelectedRouteName = newRouteName;

			TriggerLoadingRouteActionInWebView();
		}

		private void TriggerLoadingRouteActionInWebView()
		{
			if (_routeEditorWebView?.CoreWebView2 == null)
			{
				return; // WebView is still initializing
			}

			var rounteName = SelectedRouteName;
			var loadCurrentSelectedRouteScript = $@"
window.routeEditorVM.loadCurrentSelectedRoute('{rounteName}');
";
			_routeEditorWebView.Dispatcher.Invoke(async () =>
			{
				_routeEditorWebView.CoreWebView2?.ExecuteScriptAsync(loadCurrentSelectedRouteScript);
			});
		}

		private void TriggerSaveRouteActionInWebView()
		{
			var saveCurrentRouteScript = $@"
window.routeEditorVM.saveCurrentEditingRoute();
";
			_routeEditorWebView.Dispatcher.Invoke(async () =>
			{
				_routeEditorWebView.CoreWebView2?.ExecuteScriptAsync(saveCurrentRouteScript);
			});
		}

		private void TriggerCloseEditingRouteInWebView()
		{
			var closeEditingRouteScript = $@"
window.routeEditorVM.closeCurrentEditingRoute(true);
";
			_routeEditorWebView.Dispatcher.Invoke(async () =>
			{
				_routeEditorWebView.CoreWebView2?.ExecuteScriptAsync(closeEditingRouteScript);
			});
		}

		private void TriggerRenameRouteActionInWebView(string newRouteName)
		{
			var renameEditingRouteScript = $@"
window.routeEditorVM.renameCurrentEditingRoute('{newRouteName}');
";
			_routeEditorWebView.Dispatcher.Invoke(async () =>
			{
				_routeEditorWebView.CoreWebView2?.ExecuteScriptAsync(renameEditingRouteScript);
			});
		}

		private void TriggerAutoDrivingGpsEventsGenerationActionInWebView(NaiveAutoDrivingPlanParameters parameters)
		{
			var parametersJsonStr = $@"{{
	""acceleration"": {parameters.Acceleration},
	""deceleration"": {parameters.Deceleration},
	""maxSpeed"": {parameters.MaxSpeed},
	""turnSpeed"": {parameters.TurnSpeed},
	""maxAngleChangeInSegment"": {parameters.MaxAngleChangeInSegment},
	""intervalInSeconds"": {parameters.GpsEventsIntervalInSeconds},
}}";
			var renameEditingRouteScript = $@"
window.routeEditorVM.generateGpsEventsByCurrentDrivingRouteAndPlan({parametersJsonStr});
";
			_routeEditorWebView.Dispatcher.Invoke(async () =>
			{
				_routeEditorWebView.CoreWebView2?.ExecuteScriptAsync(renameEditingRouteScript);
			});
		}

		private async Task TrySavingGeneratedAutoDrivingGpsEventsAsync(List<HistoryGpsEvent> gpsEvents)
		{
			try
			{
				var saveEventsDirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "VirtualDrivingHistoryData");
				//if (!Directory.Exists(saveEventsDirectoryPath))
				//{
				//	Directory.CreateDirectory(saveEventsDirectoryPath);
				//}

				var saveEventsFileName = ComposeDrivingHistoryEventsFileName(gpsEvents);

				saveEventsDirectoryPath = DirectoryAndFileSelectionHelper.PromptForDirectorySelection(saveEventsDirectoryPath);
				if (string.IsNullOrEmpty(saveEventsDirectoryPath))
				{
					return;
				}

				var saveEventsFullPath = Path.Combine(saveEventsDirectoryPath, saveEventsFileName);
				var (exported, error) = await ExcelAndCsvDataHelper.SaveHistoryGpsEventsAsCsvFile(saveEventsFullPath, gpsEvents).ConfigureAwait(false);
				if (exported)
				{
					if (File.Exists(saveEventsFullPath))
					{
						var startInfo = new ProcessStartInfo()
						{
							Arguments = saveEventsDirectoryPath,
							FileName = "explorer.exe",
						};

						Process.Start(startInfo);
					}
				}
				else
				{
					MessageBox.Show($"Failed to save AutoGenerated Virtual Driving GPS Events. Error: {error}");
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Failed to save AutoGenerated Virtual Driving GPS Events. Error: {ex.Message}");
			}
		}

		private static string ComposeDrivingHistoryEventsFileName(List<HistoryGpsEvent> historyEvents)
		{
			var firstEvent = historyEvents.First();
			var lastEvent = historyEvents.Last();

			var startEventTimeAbbreviation = firstEvent.StartTime.ToString("yyyyMMddHHmmss");
			var endEventTimeAbbreviation = lastEvent.StartTime.ToString("yyyyMMddHHmmss");
			var fileName = $"AutoGeneratedVirtualDrivingHistory_{startEventTimeAbbreviation}_{endEventTimeAbbreviation}.csv";
			return fileName;
		}
	}
}
