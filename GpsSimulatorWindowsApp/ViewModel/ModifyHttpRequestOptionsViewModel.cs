using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GpsSimulatorWindowsApp.DataType;
using GpsSimulatorWindowsApp.Dialogs;
using GpsSimulatorWindowsApp.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;

namespace GpsSimulatorWindowsApp.ViewModel
{
	public class ModifyHttpRequestOptionsViewModel : ObservableObject
	{
		private readonly HttpMethod[] _allowedRequestMethods =
		{
			HttpMethod.Get,
			HttpMethod.Post,
		};

		private HttpMethod _selectedRequestMethod;
		private string _requestHeadersValue;
		private string _requestBodyValue;
		private string _eventListJsonPath;
		private string _eventLongitudePropertyName;
		private string _eventLatitudePropertyName;
		private string _eventSpeedPropertyName;
		private string _eventHeadingPropertyName;
		private string _eventTimePropertyName;

		private Action _applyAction;
		private Action _cancelAction;

		public ModifyHttpRequestOptionsViewModel(
			HttpRequestOptionsForWebGpsEventSource webGpsResourceRequestOptions,
			Action applyAction,
			Action cancelAction)
		{
			SelectedRequestMethod = new HttpMethod(webGpsResourceRequestOptions.RequestMethod);
			RequestHeadersValue = webGpsResourceRequestOptions.RequestHeaders != null ? JsonSerializer.Serialize(webGpsResourceRequestOptions.RequestHeaders) : string.Empty;
			RequestBodyValue = webGpsResourceRequestOptions.RequestBody ?? string.Empty;

			var eventJsonQuerySettings = webGpsResourceRequestOptions.GpsEventsJsonQuerySettings ?? WebGpsEventsJsonQuerySettings.DefaultJsonQuerySettings;
			EventListJsonPath = eventJsonQuerySettings.JsonPathOfGpsEventList;
			EventLongitudePropertyName = eventJsonQuerySettings.LongitudeJsonPropertyName;
			EventLatitudePropertyName = eventJsonQuerySettings.LatitudeJsonPropertyName;
			EventSpeedPropertyName = eventJsonQuerySettings.SpeedJsonPropertyName;
			EventHeadingPropertyName = eventJsonQuerySettings.HeadingJsonPropertyName;
			EventStartTimePropertyName = eventJsonQuerySettings.StartTimeJsonPropertyName;

			_applyAction = applyAction;
			_cancelAction = cancelAction;

			ApplyRequestOptionsChangeCommand = new RelayCommand(ApplyRequestOptionsChange);
			CancelRequestOptionsChangeCommand = new RelayCommand(CancelRequestOptionsChange);
		}

		public HttpMethod[] AllowedRequestMethods => _allowedRequestMethods;

		public HttpMethod SelectedRequestMethod
		{
			get => _selectedRequestMethod;
			set
			{
				SetProperty(ref _selectedRequestMethod, value, nameof(SelectedRequestMethod));
				OnPropertyChanged(nameof(IsRequestBodyEnabled));
			}
		}

		public string RequestHeadersValue
		{
			get => _requestHeadersValue;
			set => SetProperty(ref _requestHeadersValue, value, nameof(RequestHeadersValue));
		}

		public string RequestBodyValue
		{
			get => _requestBodyValue;
			set => SetProperty(ref _requestBodyValue, value, nameof(RequestBodyValue));
		}

		public bool IsRequestBodyEnabled => SelectedRequestMethod == HttpMethod.Post;

		public string EventListJsonPath
		{
			get => _eventListJsonPath;
			set
			{
				SetProperty(ref _eventListJsonPath, value, nameof(EventListJsonPath));
			}
		}

		public string EventLongitudePropertyName
		{
			get => _eventLongitudePropertyName;
			set
			{
				SetProperty(ref _eventLongitudePropertyName, value, nameof(EventLongitudePropertyName));
			}
		}

		public string EventLatitudePropertyName
		{
			get => _eventLatitudePropertyName;
			set
			{
				SetProperty(ref _eventLatitudePropertyName, value, nameof(EventLatitudePropertyName));
			}
		}

		public string EventSpeedPropertyName
		{
			get => _eventSpeedPropertyName;
			set
			{
				SetProperty(ref _eventSpeedPropertyName, value, nameof(EventSpeedPropertyName));
			}
		}

		public string EventHeadingPropertyName
		{
			get => _eventHeadingPropertyName;
			set
			{
				SetProperty(ref _eventHeadingPropertyName, value, nameof(EventHeadingPropertyName));
			}
		}

		public string EventStartTimePropertyName
		{
			get => _eventTimePropertyName;
			set
			{
				SetProperty(ref _eventTimePropertyName, value, nameof(EventStartTimePropertyName));
			}
		}

		public IRelayCommand ApplyRequestOptionsChangeCommand { get; private set; }

		public IRelayCommand CancelRequestOptionsChangeCommand { get; private set; }

		public HttpRequestOptionsForWebGpsEventSource GetLatestRequestOptions()
		{
			var requestOptions = new HttpRequestOptionsForWebGpsEventSource
			{
				RequestMethod = SelectedRequestMethod.Method,
				RequestHeaders = JsonSerializer.Deserialize<Dictionary<string, string>>(RequestHeadersValue),
				RequestBody = RequestBodyValue,
				GpsEventsJsonQuerySettings = new WebGpsEventsJsonQuerySettings
				{
					JsonPathOfGpsEventList = EventListJsonPath,
					LongitudeJsonPropertyName = EventLongitudePropertyName,
					LatitudeJsonPropertyName = EventLatitudePropertyName,
					SpeedJsonPropertyName = EventSpeedPropertyName,
					HeadingJsonPropertyName = EventHeadingPropertyName,
					StartTimeJsonPropertyName = EventStartTimePropertyName,
				},
			};

			return requestOptions;
		}

		private string? ValidateRequestOptions()
		{
			try
			{
				// Check request headers value (if not empty)
				if (!string.IsNullOrEmpty(RequestHeadersValue))
				{
					var headersDict = JsonSerializer.Deserialize<Dictionary<string, string>>(RequestHeadersValue);
					if (headersDict == null)
					{
						return "Invalid JSON dictionary!";
					}
				}

				// For HTTP POST method, check request body value
				if (SelectedRequestMethod == HttpMethod.Post)
				{
					if (string.IsNullOrEmpty(RequestBodyValue))
					{
						return "Request body cannot be empty!";
					}

					var jdoc = JsonDocument.Parse(RequestBodyValue);
				}

				// Check the event list JSON query settings
				if (string.IsNullOrEmpty(EventListJsonPath) || !EventListJsonPath.StartsWith("/"))
				{
					return "Event List Json Path must be a path starts with '/'";
				}

				if (string.IsNullOrEmpty(EventLongitudePropertyName))
				{
					return "Event Longitude property name cannot be empty!";
				}

				if (string.IsNullOrEmpty(EventLatitudePropertyName))
				{
					return "Event Latitude property name cannot be empty!";
				}

				if (string.IsNullOrEmpty(EventSpeedPropertyName))
				{
					return "Event Speed property name cannot be empty!";
				}

				if (string.IsNullOrEmpty(EventHeadingPropertyName))
				{
					return "Event Heading property name cannot be empty!";
				}

				if (string.IsNullOrEmpty(EventStartTimePropertyName))
				{
					return "Event Start Time property name cannot be empty!";
				}

				return null;
			}
			catch (JsonException ex)
			{
				return "Invalid JSON content!";
			}
			catch (Exception ex)
			{
				return "Invalid Request Options!";
			}
		}

		private void CancelRequestOptionsChange()
		{
			_cancelAction?.Invoke();
		}

		private void ApplyRequestOptionsChange()
		{
			var errorMessage = ValidateRequestOptions();
			if (string.IsNullOrEmpty(errorMessage))
			{
				_applyAction?.Invoke();
			}
			else
			{
				MessageBox.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}
	}
}
