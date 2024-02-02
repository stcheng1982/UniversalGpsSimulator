(function () {

  const _DEVICE_POINTS_LAYER_ID_PREFIX = 'devicePoints_';
  const esriApiKey = "AAPKff73321f58474e949c0abba328e521a3pZHeWItbg9ksrdv-Dl3kYKBtkmjLeI5wB7I2YuMKSBvGLWG1oavQRKaqegaSL_38";

  let _basemapGallery = null;

  function getExtentByGraphics(graphics) {
    if (!graphics || graphics.length == 0) {
      return null
    }

    let extent = null;
    graphics.forEach(function (graphic) {
      if (extent != null) {
        if (graphic.geometry.type === 'point') {
          (graphic.geometry.x > extent.xmax) && (extent.xmax = graphic.geometry.x);
          (graphic.geometry.x < extent.xmin) && (extent.xmin = graphic.geometry.x);
          (graphic.geometry.y > extent.ymax) && (extent.ymax = graphic.geometry.y);
          (graphic.geometry.y < extent.ymin) && (extent.ymin = graphic.geometry.y);
        }
        else {
          extent = extent.union(graphic.geometry.extent)
        }
      } else {
        if (graphic.geometry.type === 'point') {
          extent = new arcgis.Extent();
          extent.xmin = extent.xmax = graphic.geometry.x;
          extent.ymin = extent.ymax = graphic.geometry.y;
        } else {
          extent = graphic.geometry.extent.clone();
        }
      }
    })

    return extent;
  }

  function createDeviceCurrentLocationGraphic(deviceId, longitude, latitude) {
    const point = { //Create a point
      type: "point",
      longitude: longitude,
      latitude: latitude,
    };
    const simpleMarkerSymbol = {
      type: "simple-marker",
      color: [226, 119, 40],  // Orange
      width: 1,
      outline: {
        color: [255, 255, 255], // White
        width: 1
      }
    };

    const pointAttributes = { type: 'current', deviceId: deviceId };
    const pointGraphic = new arcgis.Graphic({
      geometry: point,
      symbol: simpleMarkerSymbol,
      attributes: pointAttributes,
    });

    return pointGraphic;
  }

  function createDevicePlaybackLocationGraphic(deviceId, longitude, latitude) {
    const pointSymbol = {
      type: "simple-marker",
      outline: { style: "dot", width: 0 },
      size: 4,
      xoffset: 0,
      yoffset: 0,
      color: [0, 0, 0, 1]
    };

    const point = {
      type: "point",
      longitude: longitude,
      latitude: latitude,
    };

    const pointGraphic = new arcgis.Graphic({
      geometry: point,
      symbol: pointSymbol,
      attributes: { type: 'PlaybackPoint', deviceId: deviceId },
    });

    return pointGraphic;
  }

  function createDevicePlaybackPathGraphic(deviceId, pointCoords) {
    const linePaths = pointCoords.slice();
    const polyline = {
      type: "polyline",
      paths: linePaths
    };
    const simpleLineSymbol = {
      type: "simple-line",
      color: [128, 128, 128],
      width: 1
    };

    const polylineGraphic = new arcgis.Graphic({
      geometry: polyline,
      symbol: simpleLineSymbol,
      attributes: { type: 'PlaybackPath', deviceId: deviceId },
    });

    return polylineGraphic;
  }

  function AppViewModel() {
    const self = this;

    // UI properties
    self.obIsBasemapGalleryVisible = ko.observable(false);

    // Data properties
    self.obLongitude = ko.observable(-73.94164);
    self.obLatitude = ko.observable(42.8122);
    self.obDistanceToPrevPoint = ko.observable('0.00');
    self.obBearingFromPrevPoint = ko.observable('0.0');
    self.obSpeed = ko.observable('');
    self.obSentOn = ko.observable(new Date());
    self.mapView = null;
    self.devicePointsLayerMap = new Map();
    self.currentDevicePointsLayer = null;
    self.obSelectedDeviceId = ko.observable('');
    self.prevPoint = { x: 0, y: 0 };

    self.toggleBasemapGallery = self.toggleBasemapGallery.bind(self);
    self.focusToCurrentLocationOfSelectedDevice = self.focusToCurrentLocationOfSelectedDevice.bind(self);
    self.focusToCurrentLocationOfAllDevices = self.focusToCurrentLocationOfAllDevices.bind(self);
    self.zoomToPathsOfAllDevices = self.zoomToPathsOfAllDevices.bind(self);
  }

  AppViewModel.prototype.selectDeviceItem = function(deviceId) {
    const self = this;

    const previousSelectedDeviceId = self.obSelectedDeviceId();

    // find the previous selected device's current Location Graphic
    const previousSelectedDeviceCurrentLocationGraphic = self.currentDevicePointsLayer.graphics.items.find(g => g.attributes.deviceId === previousSelectedDeviceId);
    if (previousSelectedDeviceCurrentLocationGraphic) {
      // remove the border of the previous selected device's current Location Graphic symbol
      const copiedSymbol = previousSelectedDeviceCurrentLocationGraphic.symbol.clone();
      copiedSymbol.outline.width = 0;
      previousSelectedDeviceCurrentLocationGraphic.set('symbol', copiedSymbol);

      // find the PlaybackPath line graphic of the previous selected device and reset line's symbol width and color
      const previousSelectedDevicePLaybackLayer = self.devicePointsLayerMap.get(previousSelectedDeviceId);
      if (previousSelectedDevicePLaybackLayer) {
        const previousSelectedDevicePlaybackPathGraphic = previousSelectedDevicePLaybackLayer.graphics.items.find(
          g => g.attributes.type === 'PlaybackPath' && g.attributes.deviceId === previousSelectedDeviceId);
        if (previousSelectedDevicePlaybackPathGraphic) {
          const copiedSymbol = previousSelectedDevicePlaybackPathGraphic.symbol.clone();
          copiedSymbol.width = 1;
          copiedSymbol.color = [128, 128, 128];
          previousSelectedDevicePlaybackPathGraphic.set('symbol', copiedSymbol);
        }
      }
    }



    // find the current selected device's current Location Graphic
    const currentSelectedDeviceCurrentLocationGraphic = self.currentDevicePointsLayer.graphics.items.find(g => g.attributes.deviceId === deviceId);
    if (currentSelectedDeviceCurrentLocationGraphic) {
      // add the border of the current selected device's current Location Graphic symbol (border color is darkblue)
      const copiedSymbol = currentSelectedDeviceCurrentLocationGraphic.symbol.clone();
      copiedSymbol.outline.width = 2;
      copiedSymbol.outline.color = [0, 0, 139];
      currentSelectedDeviceCurrentLocationGraphic.set('symbol', copiedSymbol);

      // find the PlaybackPath line graphic of the current selected device and reset line's symbol width and color
      const currentSelectedDevicePLaybackLayer = self.devicePointsLayerMap.get(deviceId);
      if (currentSelectedDevicePLaybackLayer) {
        const currentSelectedDevicePlaybackPathGraphic = currentSelectedDevicePLaybackLayer.graphics.items.find(
          g => g.attributes.type === 'PlaybackPath' && g.attributes.deviceId === deviceId);
        if (currentSelectedDevicePlaybackPathGraphic) {
          const copiedSymbol = currentSelectedDevicePlaybackPathGraphic.symbol.clone();
          copiedSymbol.width = 2;
          copiedSymbol.color = [0, 0, 139];
          currentSelectedDevicePlaybackPathGraphic.set('symbol', copiedSymbol);
        }
      }

      self.obSelectedDeviceId(deviceId);
    }
    else {
      // No matched device found, we clear the current selected device id
      self.obSelectedDeviceId('');
    }

  };

  AppViewModel.prototype.toggleDevicePlaybackLayerVisibility = function (deviceId, visible) {
    const self = this;

    if (!deviceId) {
      return;
    }

    // find device's playback layer
    const devicePlaybackLayer = self.devicePointsLayerMap.get(deviceId);
    if (devicePlaybackLayer) {
      devicePlaybackLayer.visible = visible;
    }

    // find device's current Location point
    if (self.currentDevicePointsLayer) {
      const deviceCurrentLocationGraphic = self.currentDevicePointsLayer.graphics.items.find(g => g.attributes.deviceId === deviceId);
      if (deviceCurrentLocationGraphic) {
        deviceCurrentLocationGraphic.visible = visible;
      }
    }
  };

  AppViewModel.prototype.recreateDevicePointsLayers = function (deviceIds) {
    const self = this;

    if (self.devicePointsLayerMap.size > 0) {
      for (let [deviceId, devicePointsLayer] of self.devicePointsLayerMap) {
        if (devicePointsLayer) {
          self.mapView.map.remove(devicePointsLayer);
        }

        const deviceCurrentPointGraphic = self.currentDevicePointsLayer.graphics.items.find(g => g.attributes.deviceId === deviceId);
        if (deviceCurrentPointGraphic) {
          self.currentDevicePointsLayer.remove(deviceCurrentPointGraphic);
        }
      }

      self.devicePointsLayerMap.clear(); // clear all Points layer mapping
    }

    if (!deviceIds || deviceIds.length === 0) {
      return;
    }

    const currentDevicePointsLayer = self.mapView.map.remove(self.currentDevicePointsLayer); // remove currentDevicePointsLayer from the map first
    for (let deviceId of deviceIds) {

      // create device's points layer
      const devicePointsLayerId = `${_DEVICE_POINTS_LAYER_ID_PREFIX}${deviceId}`;
      const devicePointsLayer = new arcgis.GraphicsLayer({ id: devicePointsLayerId });
      self.mapView.map.add(devicePointsLayer);
      self.devicePointsLayerMap.set(deviceId, devicePointsLayer);

      // create device's current location graphic
      const deviceCurrentLocationGraphic = createDeviceCurrentLocationGraphic(deviceId, 0, 0);
      currentDevicePointsLayer.add(deviceCurrentLocationGraphic);
    }

    self.mapView.map.add(currentDevicePointsLayer); // Add currentDevicePointsLayer to the top
    self.selectDeviceItem(deviceIds[0]); // Select the first device
  };

  AppViewModel.prototype.updateCurrentLocationOfDevices = function (deviceItems, sentOnValue) {
    try {
      const self = this,
        geodesicUtils = arcgis.geodesicUtils;

      if (!deviceItems || deviceItems.length === 0) {
        return;
      }

      // Compute distance and angle based on previous and new point

      // Update new point observables
      const selectedDeviceItem = deviceItems.find(d => d.deviceId === self.obSelectedDeviceId());
      if (selectedDeviceItem) {
        self.obLongitude(selectedDeviceItem.longitude);
        self.obLatitude(selectedDeviceItem.latitude);
        self.obSpeed(selectedDeviceItem.speed);
      }

      const sentOn = new Date(sentOnValue);
      self.obSentOn(sentOn);

      if (self.currentDevicePointsLayer) {
        for (let deviceItem of deviceItems) {
          const deviceLocationGraphic = self.currentDevicePointsLayer.graphics.items.find(g => g.attributes.deviceId === deviceItem.deviceId);
          if (deviceLocationGraphic) {
            const newLocationOfDevice = { //Create a point
              type: "point",
              longitude: deviceItem.longitude,
              latitude: deviceItem.latitude
            };

            deviceLocationGraphic.set("geometry", newLocationOfDevice);
          }
        }
      }
    }
    catch (err) {
      console.error(err);
    }
  };

  AppViewModel.prototype.toggleBasemapGallery = function () {
    const self = this;

    const isBasemapGalleryVisible = self.obIsBasemapGalleryVisible();
    const newBasemapGalleryVisible = !isBasemapGalleryVisible;
    self.obIsBasemapGalleryVisible(newBasemapGalleryVisible);
    _basemapGallery.visible = newBasemapGalleryVisible;
  };

  AppViewModel.prototype.focusToCurrentLocationOfSelectedDevice = function () {
    const self = this;
    const selectedDeviceId = self.obSelectedDeviceId();

    if (selectedDeviceId && self.currentDevicePointsLayer) {
      const currentLocationGraphicOfSelectedDevice = self.currentDevicePointsLayer.graphics.items.find(g => g.attributes.type === 'current' && g.attributes.deviceId === selectedDeviceId);
      if (currentLocationGraphicOfSelectedDevice) {
        self.mapView.goTo({
          center: currentLocationGraphicOfSelectedDevice.geometry,
          zoom: self.mapView.zoom,
        });
      }
    }
  };

  AppViewModel.prototype.focusToCurrentLocationOfAllDevices = function () {
    const self = this;

    if (self.currentDevicePointsLayer) {
      const visibleCurrentLocationGraphics = self.currentDevicePointsLayer.graphics.items.filter(g => g.attributes.type === 'current' && g.visible);
      if (visibleCurrentLocationGraphics.length > 0) {
        const extent = getExtentByGraphics(visibleCurrentLocationGraphics);
        if (extent) {
          const extentWithBuffer = extent.expand(1.05);
          self.mapView.goTo(extentWithBuffer);
        }
      }
    }
  };

  AppViewModel.prototype.zoomToPathsOfAllDevices = function() {
    const self = this;

    try {
      const allDevicePointsLayers = [...self.devicePointsLayerMap.values()];
      const pathGraphicsOfVisibleDeviceLayers = [];
      for (let devicePointsLayer of allDevicePointsLayers) {
        if (devicePointsLayer.visible) {
          const pathGraphics = devicePointsLayer.graphics.items.filter(g => g.attributes.type === 'PlaybackPath');
          pathGraphicsOfVisibleDeviceLayers.push(...pathGraphics);
        }
      }

      if (pathGraphicsOfVisibleDeviceLayers.length > 0) {
        self.mapView.goTo(pathGraphicsOfVisibleDeviceLayers).then(() => {
          const mapExtent = self.mapView.extent;
          return self.mapView.goTo(mapExtent.expand(1.05));
        });
      }
    }
    catch (err) {
      console.error('zoomToPathsOfAllDevices', err);
    }
  };

  AppViewModel.prototype.loadPlaybackPointsOfSingleDevice = function (deviceId, pointCoords) {
    const self = this;

    if (!deviceId || !pointCoords || pointCoords.length === 0) {
      return;
    }

    try {
      const devicePlaybackLayer = self.devicePointsLayerMap.get(deviceId);
      if (devicePlaybackLayer) {

        devicePlaybackLayer.visible = true; // reset to visible on initial loading of all playback event points
        devicePlaybackLayer.graphics.removeAll();
        console.info(`clear old points on PointsLayer of device '${deviceId}', and gonna load ${pointCoords.length} Playback Points`);

         // Add all point Graphics
         const pointGraphics = [];
         for (let coord of pointCoords) {
           const pointGraphic = createDevicePlaybackLocationGraphic(deviceId, coord[0], coord[1]);
           pointGraphics.push(pointGraphic);
         }

         devicePlaybackLayer.addMany(pointGraphics);

         // Add a polyline for the path
         const polylineGraphic = createDevicePlaybackPathGraphic(deviceId, pointCoords);
         devicePlaybackLayer.add(polylineGraphic);

         // Focus to first point
         if (self.currentDevicePointsLayer) {
           const firstPoint = pointGraphics[0].geometry.clone();
           const deviceCurrentLocationGraphic = self.currentDevicePointsLayer.graphics.items.find(g => g.attributes.deviceId === deviceId);
           if (deviceCurrentLocationGraphic) {
            deviceCurrentLocationGraphic.visible = true;
            deviceCurrentLocationGraphic.set("geometry", firstPoint);
            //  self.mapView.goTo(locationGraphic);
           }
         }
      }
    }
    catch (err) {
      console.error(err);
    }
  };

  window.rootViewModel = new AppViewModel();

  require([
    "esri/config",
    "esri/Map",
    "esri/views/MapView",
    "esri/widgets/BasemapGallery",
    "esri/Graphic",
    "esri/layers/GraphicsLayer",
    "esri/geometry/Point",
    "esri/geometry/Polyline",
    "esri/geometry/Extent",
    "esri/geometry/support/geodesicUtils"
  ], function (
    esriConfig,
    Map,
    MapView,
    BasemapGallery,
    Graphic,
    GraphicsLayer,
    Point,
    Polyline,
    Extent,
    geodesicUtils) {

    esriConfig.apiKey = esriApiKey;
    window.arcgis = {};
    window.arcgis.BasemapGallery = BasemapGallery;
    window.arcgis.Graphic = Graphic;
    window.arcgis.Point = Point;
    window.arcgis.Polyline = Polyline;
    window.arcgis.Extent = Extent;
    window.arcgis.GraphicsLayer = GraphicsLayer;
    window.arcgis.geodesicUtils = geodesicUtils;

    const lon = rootViewModel.obLongitude();
    const lat = rootViewModel.obLatitude();

    const map = new Map({
      basemap: "streets-navigation-vector" //Basemap layer service
    });

    const view = new MapView({
      map: map,
      center: [lon, lat], //Longitude, latitude
      zoom: 16,
      container: "viewDiv"
    });

     // Add basemap gallery
     _basemapGallery = new BasemapGallery({
      view: view,
      container: "basemapGalleryContainer",
      visible: false,
    });
    view.ui.add(_basemapGallery, "top-right");

    // Add graphics layers
    const currentDevicePointsLayer = new GraphicsLayer({id: 'currentDevicePointsLayer'});
    map.add(currentDevicePointsLayer);

    // Load Knockout MVVM
    rootViewModel.mapView = view;
    rootViewModel.currentDevicePointsLayer = currentDevicePointsLayer;
    ko.applyBindings(window.rootViewModel);
  });

})();
