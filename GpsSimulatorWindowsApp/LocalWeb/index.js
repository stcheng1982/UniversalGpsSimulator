(function () {
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
    self.allPointsLayer = null;
    self.currentPointLayer = null;
    self.prevPoint = { x: 0, y: 0 };

    self.toggleBasemapGallery = self.toggleBasemapGallery.bind(self);
    self.focusToCurrentLocationOfSelectedDevice = self.focusToCurrentLocationOfSelectedDevice.bind(self);
    self.focusToAllPlaybackPointsOfSelectedDevice = self.focusToAllPlaybackPointsOfSelectedDevice.bind(self);
  }

  AppViewModel.prototype.updateCurrentLocation = function (lon, lat, speed, sentOnValue) {
    try {
      const self = this,
        geodesicUtils = arcgis.geodesicUtils;

      // Compute distance and angle based on previous and new point
      if (self.prevPoint && geodesicUtils) {
        const { distance, azimuth } = geodesicUtils.geodesicDistance(
          new arcgis.Point({ x: self.prevPoint.x, y: self.prevPoint.y }),
          new arcgis.Point({ x: lon, y: lat }),
          "meters"
        );

        self.obDistanceToPrevPoint(!!distance ? distance.toFixed(2) : '0');
        self.obBearingFromPrevPoint(!!azimuth ? azimuth.toFixed(1) : '0');

        // console.info(`[Prev - Cur] Distance: ${distance}, Angle: ${azimuth}`);
      }

      // Update previous point
      self.prevPoint.x = lon;
      self.prevPoint.y = lat;

      // Update new point observables
      self.obLongitude(lon);
      self.obLatitude(lat);
      self.obSpeed(speed);

      const sentOn = new Date(sentOnValue);
      self.obSentOn(sentOn);

      if (self.currentPointLayer) {
        const locationGraphic = self.currentPointLayer.graphics.items.find(g => g.attributes.type === 'current');
        if (locationGraphic) {
          const newPoint = { //Create a point
            type: "point",
            x: lon, // -118.80657463861,
            y: lat, // 34.0005930608889
          };
          locationGraphic.set("geometry", newPoint);

          //rootViewModel.mapView.goTo(locationGraphic);

          //const animationOptions = {
          //  duration: 1000, // The duration of the animation in milliseconds
          //  easing: "linear" // The easing function to use for the animation
          //};
          //rootViewModel.mapView.goTo({ center: newPoint }, animationOptions);
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

    if (self.currentPointLayer) {
      const locationGraphic = self.currentPointLayer.graphics.items.find(g => g.attributes.type === 'current');
      if (locationGraphic) {
        self.mapView.goTo(locationGraphic);
      }
    }
  };

  AppViewModel.prototype.focusToAllPlaybackPointsOfSelectedDevice = function () {
    const self = this;

    if (self.allPointsLayer) {
      const playbackGraphics = self.allPointsLayer.graphics.items.filter(g => g.visible);
      if (playbackGraphics && playbackGraphics.length > 0) {

        const extent = getExtentByGraphics(playbackGraphics);
        if (extent) {
          const extentWithBuffer = extent.expand(1.02);
          self.mapView.goTo(extentWithBuffer);
        }
      }
    }
  };

  AppViewModel.prototype.loadAllPlaybackLocations = function (pointCoords) {
    const self = this;

    const pointSymbol = {
      type: "simple-marker",
      outline: { style: "dot", width: 0 },
      size: 3,
      xoffset: 0,
      yoffset: 0,
      color: [0, 0, 0, 1]
    };

    try {
      if (self.allPointsLayer) {

        self.allPointsLayer.graphics.removeAll();
        console.info(`clear old points on allPointsLayer, and gonna add ${pointCoords.length} new points`);
        if (Array.isArray(pointCoords) && pointCoords.length > 0) {

          // Add all point Graphics
          const pointGraphics = [];
          for (let coord of pointCoords) {
            const point = {
              type: "point",
              x: coord.x,
              y: coord.y,
            };

            const pointGraphic = new arcgis.Graphic({
              geometry: point,
              symbol: pointSymbol,
            });

            pointGraphics.push(pointGraphic);
          }

          self.allPointsLayer.addMany(pointGraphics);

          // Add a polyline for the path
          const linePaths = pointCoords.map(coord => [coord.x, coord.y]);
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
            symbol: simpleLineSymbol
          });
          self.allPointsLayer.add(polylineGraphic);

          // Focus to first point
          const firstPoint = pointCoords[0];
          Object.assign(self.prevPoint, firstPoint);

          if (self.currentPointLayer) {
            const locationGraphic = self.currentPointLayer.graphics.items.find(g => g.attributes.type === 'current');
            if (locationGraphic) {
              const initialPoint = {
                type: "point",
                x: firstPoint.x,
                y: firstPoint.y,
              };
              locationGraphic.set("geometry", initialPoint);
              self.mapView.goTo(locationGraphic);
            }
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
    const allPointsLayer = new GraphicsLayer();
    const currentPointLayer = new GraphicsLayer();
    map.add(allPointsLayer);
    map.add(currentPointLayer);

    // Add current point
    const point = { //Create a point
      type: "point",
      longitude: lon,
      latitude: lat,
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

    const pointAttributes = { type: 'current' };
    const pointGraphic = new Graphic({
      geometry: point,
      symbol: simpleMarkerSymbol,
      attributes: pointAttributes,
    });
    currentPointLayer.add(pointGraphic);
    view.goTo(pointGraphic);


    // Load Knockout MVVM
    rootViewModel.mapView = view;
    rootViewModel.allPointsLayer = allPointsLayer;
    rootViewModel.currentPointLayer = currentPointLayer;
    ko.applyBindings(window.rootViewModel);

  });

})();
