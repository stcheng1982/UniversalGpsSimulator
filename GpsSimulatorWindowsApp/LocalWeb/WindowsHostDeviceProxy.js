(function () {
  const _WEBVIEW_HOSTDEVICE_OBJECT_KEY = 'HostDevice';
  const _WEBMOCK_HOSTDEVICE_INFORMATION = {
    "DeviceIdentifier": "WINDOWS_WEBMOCK_DEVICE_IDENTIFIER",
    "MachineName": "WINDOWS_MACHINE_NAME",
    "OSVersion": navigator.platform,
    "TimeZoneName": "LocalTimeZone",
  };

  class WindowsHostDeviceProxy {

    constructor() {
    }

    get isHostConnected() {
      return !!(chrome && chrome.webview && chrome.webview.hostObjects && chrome.webview.hostObjects[_WEBVIEW_HOSTDEVICE_OBJECT_KEY]);
    }

    getHostDeviceInformation() {
      if (!this.isHostConnected) {
        return Promise.resolve(Object.assign({}, _WEBMOCK_HOSTDEVICE_INFORMATION));
      }

      return chrome.webview.hostObjects[_WEBVIEW_HOSTDEVICE_OBJECT_KEY].GetDeviceInformationAsJson()
        .then(jsonText => {
          return JSON.parse(jsonText);
        })
        .catch(err => {
          console.error('getHostDeviceInformation', err);
          return null;
        });
    }

    getHostUserSessionInformation() {
      if (!this.isHostConnected) {
        return Promise.resolve(null);
      }

      return chrome.webview.hostObjects[_WEBVIEW_HOSTDEVICE_OBJECT_KEY].GetUserSessionInformation()
        .then(jsonText => {
          return JSON.parse(jsonText);
        })
        .catch(err => {
          console.error('getHostUserSessionInformation', err);
          return null;
        });

    }
  }

  createNamespace("TF").WindowsHostDeviceProxy = WindowsHostDeviceProxy;
  createNamespace("tf").windowsHostDeviceProxy = new TF.WindowsHostDeviceProxy();
})();
