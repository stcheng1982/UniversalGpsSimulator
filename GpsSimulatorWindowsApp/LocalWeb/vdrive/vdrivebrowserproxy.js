(function () {
  const _WEBVIEW_PROXY_OBJECT_KEY = 'VirtualDrivingBrowserProxy';

  class VirtualDrivingWebViewProxy {

    constructor() {
    }

    get isHostConnected() {
      return !!(chrome && chrome.webview && chrome.webview.hostObjects && chrome.webview.hostObjects[_WEBVIEW_PROXY_OBJECT_KEY]);
    }

    getHostDeviceInformation() {
      if (!this.isHostConnected) {
        return Promise.resolve(Object.assign({}, {}));
      }

      return chrome.webview.hostObjects[_WEBVIEW_PROXY_OBJECT_KEY].GetDeviceInformationAsJson()
        .then(jsonText => {
          return JSON.parse(jsonText);
        })
        .catch(err => {
          console.error('getHostDeviceInformation', err);
          return null;
        });
    }

    loadRouteDataFromLocalStorage(routeName) {
      if (!this.isHostConnected) {
        return Promise.resolve(null);
      }

      return chrome.webview.hostObjects[_WEBVIEW_PROXY_OBJECT_KEY].LoadRouteDataFromLocalStorage(routeName)
        .then(routeJsonData => {
          return routeJsonData;
        })
        .catch(err => {
          console.error('loadRouteDataFromLocalStorage', err);
          return null;
        });
    }

    publishGeolocationOfDrivingVehicle(geolocation) {
      if (!this.isHostConnected) {
        console.warn('publishGeolocationOfDrivingVehicle: webview host is not connected');
        return;
      }

      chrome.webview.hostObjects[_WEBVIEW_PROXY_OBJECT_KEY].PublishGeolocationOfDrivingVehicle(
        geolocation.longitude,
        geolocation.latitude,
        geolocation.speed,
        geolocation.heading,
        geolocation.time
      );
    }

    markFullConductDrivingAsCompleted() {
      if (!this.isHostConnected) {
        console.warn('markFullConductDrivingAsCompleted: webview host is not connected');
        return;
      }

      chrome.webview.hostObjects[_WEBVIEW_PROXY_OBJECT_KEY].MarkFullConductDrivingAsCompleted();
    }
  }

  window.VirtualDrivingWebViewProxy = VirtualDrivingWebViewProxy;
})();
