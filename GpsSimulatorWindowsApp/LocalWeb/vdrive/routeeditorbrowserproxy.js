(function () {
  const _WEBVIEW_PROXY_OBJECT_KEY = 'RouteEditorWebViewProxy';

  class RouteEditorWebViewProxy {

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

    getCurrentSelectedRouteName() {
      if (!this.isHostConnected) {
        return Promise.resolve(null);
      }

      return chrome.webview.hostObjects[_WEBVIEW_PROXY_OBJECT_KEY].GetCurrentSelectedRouteName()
        .then(routeName => {
          return routeName;
        })
        .catch(err => {
          console.error('getCurrentSelectedRouteName', err);
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

    saveRouteDataToLocalStorage(routeName, routeJsonData, previewImageDataUrl) {
      if (!this.isHostConnected) {
        return Promise.resolve(false);
      }

      return chrome.webview.hostObjects[_WEBVIEW_PROXY_OBJECT_KEY].SaveRouteDataToLocalStorage(routeName, routeJsonData, previewImageDataUrl)
        .then(saveError => {
          if (saveError) {
            console.error('saveRouteDataToLocalStorage', saveError);
          }
          return !saveError;
        })
        .catch(err => {
          console.error('saveRouteDataToLocalStorage', err);
          return false;
        });
    }

    saveGpsEventsOfVirtualDrivingRoutePLan(routeName, gpsEvents) {
      if (!this.isHostConnected) {
        return Promise.resolve(false);
      }

      // convert gps events to simulation history events
      const simulationHistoryEvents = gpsEvents.map(gpsEvent => {
        return {
          Lat: gpsEvent.latitude,
          Lon: gpsEvent.longitude,
          Speed: Math.round(gpsEvent.speed),
          Heading: Math.floor(gpsEvent.heading),
          StartTime: gpsEvent.time.toJSON(),
        };
      });

      return chrome.webview.hostObjects[_WEBVIEW_PROXY_OBJECT_KEY].SaveGpsEventsOfVirtualDrivingRoutePLan(routeName, JSON.stringify(simulationHistoryEvents))
        .then(saveError => {
          if (saveError) {
            console.error('SaveGpsEventsOfVirtualDrivingRoutePLan', saveError);
          }
          return !saveError;
        })
        .catch(err => {
          console.error('SaveGpsEventsOfVirtualDrivingRoutePLan', err);
          return false;
        });
    }

  }

  window.RouteEditorWebViewProxy = RouteEditorWebViewProxy;
})();
