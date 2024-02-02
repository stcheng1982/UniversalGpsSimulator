(function () {

  const _DEFAULT_MAP_ZOOM = 15;
  const _DEFAULT_MAP_CENTER = [ -73.94164, 42.8122];
  const _MAX_SPEEDOMETER_VALUE = 180; // km/s

  const countDownSoundAudio = new Audio('../res/audio/mixkit-simple-countdown-3secs.mp3');
  const steeringSoundAudio = new Audio('../res/audio/steering-sound-effect.mp3');
  steeringSoundAudio.loop = true;
  const accelerationSoundAudio = new Audio('../res/audio/car-accelerate-sound.mp3');
  // accelerationSoundAudio.loop = true;
  let accelerationSoundAudioPlaying = false;

  const vehicleGeolocationMarkerSymbol = {
    type: "simple-marker", // autocasts as new SimpleMarkerSymbol()
    size: 3,
    color: [0, 0, 0],
    outline: null
  };

  // Define the symbology used to display the route
  const routeSymbol = {
    type: "simple-line", // autocasts as SimpleLineSymbol()
    color: [0, 0, 255, 0.4],
    width: 3
  };


  const vehicleGeolocationGraphicPopupTemplate = {
    title: "Vehicle Geolocation",
    content: [{
      // Pass in the fields to display
      type: "fields",
      fieldInfos: [{
        fieldName: "longitude",
        label: "Longitude"
      }, {
        fieldName: "latitude",
        label: "Latitude"
      }, {
        fieldName: "speed",
        label: "Speed (km/h)"
      }, {
        fieldName: "heading",
        label: "Heading (degree)"
      }, {
        fieldName: "time",
        label: "Time"
      }]
    }]
  };

  let _mainVehicleAccelerationStateChangedSubscription = null;
  let _mainVehicleChangingDirectionStateSubscription = null;
  let _mainVehicleLocationSubscription = null;
  let _mainVehicleFullConductRouteCompletedSubscription = null;


  const _MAP_ORIENTATION_KEYCODES = [ KeyCodes.LEFT_CTRL, KeyCodes.N ];
  const _MAP_ORIENTATION_BUTTONS = [ GamepadButtonCodes.L_SHOULDER ];
  let _mapOrientationKeyPressedSubscription = null;
  let _mapOrientationGamepadButtonPressedSubscription = null;

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

  function createSpeedometerItems(count) {
    const items = [];
    for (let i = 0; i < count; ++i) {
      items.push(i + 1);
    }

    return items;
  }

  function VirtualDriveViewModel() {
    const self = this;

    self.webviewProxy = new window.VirtualDrivingWebViewProxy();
    self.mapView = null;
    self.sceneView = null;
    self.basemapGallery = null;
    self.searchWidget = null;

    self.obShowBasemapGallery = ko.observable(false);
    self.obShowInstructionPanel = ko.observable(true);

    self.obShow3DMapView = ko.observable(false);
    self.obOrientation = ko.observable(0);
    self.obInstructionPanelExpanded = ko.observable(true);

    self.allPointsLayer = null;
    self.drivingRouteLayer = null;
    self.prevPoint = { x: 0, y: 0 };
    self.gameEngine = null;
    self.mainVehicle = null;

    self.obDrivingStarted = ko.observable(false);
    self.obShowSpeedometer = ko.observable(false);
    self.obSpeedometerSelectedCount = ko.observable(0);
    self.obSpeedometerValue = ko.observable(0);
    self.obSpeedometerDisplayValue = ko.pureComputed(function () {
      const speedDisplayValue = self.obSpeedometerValue().toFixed(1);
      return `${speedDisplayValue} Km/s`;
    }, self);
    self.obIsTurningLeft = ko.observable(false);
    self.obIsTurningRight = ko.observable(false);


    self.obSpeedometerItems = ko.observableArray(createSpeedometerItems(50));


    self.toggleBasemapGallery = self.toggleBasemapGallery.bind(self);
    self.toggleInstructionPanelVisibility = self.toggleInstructionPanelVisibility.bind(self);

    self.startVirtualDriving = self.startVirtualDriving.bind(self);
    self.stopVirtualDriving = self.stopVirtualDriving.bind(self);
    self.toggleMapOrientation = self.toggleMapOrientation.bind(self);
    self.toggle3DView = self.toggle3DView.bind(self);
    self.expandCollapseInstructionPanel = self.expandCollapseInstructionPanel.bind(self);

    self.obCountDownVisible = ko.observable(false);
    self.obCountDownText = ko.observable('');
  }

  VirtualDriveViewModel.prototype.initializeDrivingGameEngine = function() {
    const self = this;
    self.gameEngine = new window.VirtualDrivingGameEngine({
      fps: 15,
      is3DMode: false,
      mapView: this.mapView,
      searchWidget: this.searchWidget,
      mapToNorth: false,
    });

    self.mainVehicle = new window.drivingObject({
      id: 'my-car',
      name: 'My Car',
      speed: 0,
      heading: 0,
      lon: -73.94164,
      lat: 42.8122,
    });
    self.gameEngine.addGameObject(self.mainVehicle);
    console.info('Virtual Driving GameEngine initialized. ', self.gameEngine);

  };

  VirtualDriveViewModel.prototype.showCountDownScreen = async function () {
    const self = this;
    let counter = 3;
    self.obCountDownText(`${counter}`)
    self.obCountDownVisible(true);
    countDownSoundAudio.play();

    while (counter > 0) {
      await new Promise(res => setTimeout(res, 1000));
      --counter;
      self.obCountDownText(`${counter}`);
    }

    self.obCountDownVisible(false);
    self.obCountDownText('');

  };

  VirtualDriveViewModel.prototype.toggleBasemapGallery = function() {
    const self = this;

    const isBasemapGalleryVisible = self.obShowBasemapGallery();
    const newBasemapGalleryVisible = !isBasemapGalleryVisible;

    if (newBasemapGalleryVisible) {
      self.obShowInstructionPanel(false);
    }

    self.obShowBasemapGallery(newBasemapGalleryVisible);
    self.basemapGallery.visible = newBasemapGalleryVisible;
  };

  VirtualDriveViewModel.prototype.toggleInstructionPanelVisibility = function() {
    const self = this;
    const isInstructionPanelVisible = self.obShowInstructionPanel();
    const newInstructionPanelVisible = !isInstructionPanelVisible;

    if (newInstructionPanelVisible) {
      self.obShowBasemapGallery(false);
    }

    self.obShowInstructionPanel(newInstructionPanelVisible);
  };

  VirtualDriveViewModel.prototype.expandCollapseInstructionPanel = function() {
    const self = this;
    self.obInstructionPanelExpanded(!self.obInstructionPanelExpanded());
  };

  VirtualDriveViewModel.prototype.showHideInstructionPanel = function(visible)
  {
    const self = this;
    // const instructionPanel = document.getElementById('instructionPanel');
    // instructionPanel.style.display = visible ? 'flex' : 'none';
    self.obShowInstructionPanel(visible);
  };

  VirtualDriveViewModel.prototype.startVirtualDriving = function(startupSettings) {
    const self = this;
    if (self.obDrivingStarted()) {
      return;
    }

    let drivingRouteDataPromise = Promise.resolve(null);
    if (startupSettings.drivingRoute) {
      drivingRouteDataPromise = self.webviewProxy.loadRouteDataFromLocalStorage(startupSettings.drivingRoute.selectedRouteName)
        .then(routeJsonData => {
          let routeData = null;
          if (routeJsonData) {
            try {
              routeData = JSON.parse(routeJsonData);
            }
            catch (err) {
              console.error('Failed to parse route data: ', err);
              routeData = null;
            }
          }

          if (!routeData) {
            alert('Failed to load Driving Route data');
          }

          return routeData;
        });
    }

    drivingRouteDataPromise.then(routeData => {
      // Update Driving Route's data
      self.clearDrivingRouteOnMap();
      if (routeData) {
        startupSettings.drivingRoute.data = routeData;
      } else {
        startupSettings.drivingRoute = null;
      }

      if (startupSettings.drivingRoute) {
        self.drawDrivingRouteOnMap(routeData); // draw driving route on map view before start virtual driving
        _mainVehicleFullConductRouteCompletedSubscription = self.mainVehicle.subscribeFullConductRouteCompletedNotification(() => {
          // Notify WebViewProxy to stop virtual driving
          self.webviewProxy.markFullConductDrivingAsCompleted();
        });
      }

      console.debug('startVirtualDriving: ', startupSettings);
      self.showHideInstructionPanel(false);

      self.allPointsLayer.graphics.removeAll(); // clear all previous geolocation points

      self.resetMapAndVehicleRotation(false); // set mapToNorth is false before start

      self.showCountDownScreen().then(() => {
        self.gameEngine.start(startupSettings);
        self.obDrivingStarted(true);

        _mainVehicleLocationSubscription = self.mainVehicle.subscribeGeolocationNotification((evtData) => {
          self.updateVehicleGeolocation(evtData);
        });

        _mainVehicleAccelerationStateChangedSubscription = self.mainVehicle.subscribeAccelerationStateChangeNotification((evtData) => {
          self.handleVehicleAccelerationStateChanged(evtData);
        });

        _mainVehicleChangingDirectionStateSubscription = self.mainVehicle.subscribeDirectionChangingStateNotification(evtData => {
          self.handleVehicleDirectionChangingState(evtData);
        });

        self.subscribeMapOrientationInputEvents();
      });
    });

  };

  VirtualDriveViewModel.prototype.stopVirtualDriving = function() {
    const self = this;
    if (!self.obDrivingStarted()) {
      return;
    }

    self.unsubscribeMapOrientationInputEvents();
    self.mainVehicle.unsubscribeDirectionChangingStateNotification(_mainVehicleChangingDirectionStateSubscription);
    self.mainVehicle.unsubscribeAccelerationStateChangeNotification(_mainVehicleAccelerationStateChangedSubscription);
    self.mainVehicle.unsubscribeGeolocationNotification(_mainVehicleLocationSubscription);
    if (_mainVehicleFullConductRouteCompletedSubscription) {
       self.mainVehicle.unsubscribeFullConductRouteCompletedNotification(_mainVehicleFullConductRouteCompletedSubscription);
       _mainVehicleFullConductRouteCompletedSubscription = null;
    }

    self.gameEngine.stop();
    self.obDrivingStarted(false);
    self.obShowSpeedometer(false);
    self.obIsTurningLeft(false);
    self.obIsTurningRight(false);


    setTimeout(() => {
      self.resetMapAndVehicleRotation(true) // reset map view orientation

      if (self.obShow3DMapView()) {
        self.toggle3DView(); // switch back to 2D view
      }
    }, 1000);

    self.showHideInstructionPanel(true);
  };

  VirtualDriveViewModel.prototype.resetMapAndVehicleRotation = function(mapToNorth) {
    const self = this;

    self.gameEngine.mapToNorth = mapToNorth;

    if (mapToNorth) {
      this.gameEngine.mapView.rotation = 0;
      this.obOrientation(0);
    }
    else {
      const vehicleHeading = (this.mainVehicle.heading + 360) % 360;
      const mapRotation = -vehicleHeading;
      this.obOrientation(mapRotation);
      this.gameEngine.mapView.rotation = mapRotation;
    }

    this.mainVehicle.renderHeading(mapToNorth);

  };

  VirtualDriveViewModel.prototype.toggle3DView = function() {
    const self = this;

    if (!self.obDrivingStarted() || !self.sceneView) {
      return;
    }

    const is3DMode = self.obShow3DMapView();
    self.obShow3DMapView(!is3DMode);

    const mainVehicleMarkerElm = document.getElementById('mainVehicleMarker');
    if (self.obShow3DMapView()) {
      mainVehicleMarkerElm.style.display = 'none';
    }
    else {
      mainVehicleMarkerElm.style.display = '';
    }
  };

  VirtualDriveViewModel.prototype.toggleMapOrientation = function() {
    const self = this;

    if (!self.obDrivingStarted() || !self.gameEngine) {
      return;
    }

    if (self.obShow3DMapView()) {
      return; // Skip map orientation if 3D mode
    }

    self.gameEngine.mapToNorth = !self.gameEngine.mapToNorth;

    self.mainVehicle.renderHeading(self.gameEngine.mapToNorth); // force main vehicle to updating display angle
  };

  VirtualDriveViewModel.prototype.handleKeyboardInput = function(evt) {
    const self = this;
    if (!self.obDrivingStarted()) {
      return;
    }

    const keyStates = evt && evt.keyStates;
    if (Array.isArray(keyStates) && keyStates.length > 0) {
      // const jsonStr = JSON.stringify(keyStates);
      // console.debug('keyboard input: ', jsonStr);
      window.keyboardEvents.handleKeyboardStates(keyStates);
    }
  };

  VirtualDriveViewModel.prototype.handleGamepadInput = function(evt) {
    const self = this;

    if (!self.gameEngine.controlByGamepad) {
      return; // Skip gamepad input if ControlMethod is not Gamepad
    }

    if (evt && evt.gamepad) {
      // const jsonStr = JSON.stringify(evt.gamepad);
      // console.debug('Gamepad states: ', jsonStr);
      window.gamepadEvents.handleGamepadStates(evt.gamepad);
    }

  };

  VirtualDriveViewModel.prototype.subscribeMapOrientationInputEvents = function() {
    const self = this;
    _mapOrientationKeyPressedSubscription = window.keyboardEvents.subscribeToKeysPressed(_MAP_ORIENTATION_KEYCODES, (evtData) => {
      self.toggleMapOrientation();
    });

    _mapOrientationGamepadButtonPressedSubscription = window.gamepadEvents.subscribeToButtonsPressed(_MAP_ORIENTATION_BUTTONS, (evtData) => {
      self.toggleMapOrientation();
    });
  };

  VirtualDriveViewModel.prototype.unsubscribeMapOrientationInputEvents = function() {
    const self = this;
    if (_mapOrientationKeyPressedSubscription) {
      window.keyboardEvents.unsubscribeFromKeysPressed(_MAP_ORIENTATION_KEYCODES, _mapOrientationKeyPressedSubscription);
    }

    if (_mapOrientationGamepadButtonPressedSubscription) {
      window.gamepadEvents.unsubscribeFromButtonsPressed(_MAP_ORIENTATION_BUTTONS, _mapOrientationGamepadButtonPressedSubscription);
    }
  };

  VirtualDriveViewModel.prototype.handleVehicleAccelerationStateChanged = function(evtData) {
    const self = this;

    if (!evtData) {
      return;
    }

    const {isAccelerating, speed } = evtData;

    // if (isAccelerating) {
    //   if (!accelerationSoundAudioPlaying) {
    //     accelerationSoundAudio.currentTime = 0;
    //     accelerationSoundAudio.play();
    //     accelerationSoundAudioPlaying = true;
    //   }
    // }
    // else {
    //   accelerationSoundAudio.pause();
    //   accelerationSoundAudio.currentTime = 0;
    //   accelerationSoundAudioPlaying = false;
    // }

    const selectedCount = self.obSpeedometerItems().length * (speed / _MAX_SPEEDOMETER_VALUE);
    self.obSpeedometerSelectedCount(selectedCount)
    self.obShowSpeedometer(isAccelerating);
    self.obSpeedometerValue(speed);
  };

  VirtualDriveViewModel.prototype.handleVehicleDirectionChangingState = function(evtData) {
    const self = this;

    if (!evtData) {
      return;
    }

    const { isChanging, direction } = evtData;
    if (isChanging) {
      steeringSoundAudio.currentTime = 0;
      steeringSoundAudio.play();
    }
    else {
      steeringSoundAudio.pause();
      steeringSoundAudio.currentTime = 0;
    }

    const isTurningLeft = direction === 'left';
    const isTurningRight = direction === 'right';
    self.obIsTurningLeft(isTurningLeft);
    self.obIsTurningRight(isTurningRight);
  };

  VirtualDriveViewModel.prototype.updateVehicleGeolocation = function(geolocation) {
    const self = this;

    if (!geolocation) {
      return;
    }

    // Adjust Mapview based on vehicle geolocation
    self.adjustMapViewByVehicleGeolocation(geolocation);

    // console.debug('updateVehicleGeolocation', geolocation);
    self.webviewProxy.publishGeolocationOfDrivingVehicle(geolocation);

    self.updateGameEngineState(geolocation);

    self.addPointOfVehicleGeolocation(geolocation);
  };

  VirtualDriveViewModel.prototype.adjustMapViewByVehicleGeolocation = function (geolocation) {
    if (!this.obDrivingStarted()) {
      return;
    }

    // Update 2d view
    if (this.gameEngine.mapToNorth) {
      if (this.gameEngine.mapView.rotation !== 0) {
        this.gameEngine.mapView.rotation = 0;
        this.obOrientation(0);
      }

      this.gameEngine.mapView.goTo({
        center: [geolocation.longitude, geolocation.latitude],
      }, {
        animate: true,
        duration: 1000,
        easing: 'linear',
      });
    }
    else {
      const mapRotation = -geolocation.heading;
      this.gameEngine.mapView.goTo({
        center: [geolocation.longitude, geolocation.latitude],
        rotation: mapRotation,
      }, {
        animate: true,
        duration: 1000,
        easing: 'linear',
      }).finally(() => {
        this.obOrientation(mapRotation);
      });

    }

    // Update 3d view
    // const camera = this.sceneView.camera.clone();
    // camera.position.longitude = geolocation.longitude;
    // camera.position.latitude = geolocation.latitude;
    // // camera.position.z = 100;
    // camera.heading = geolocation.heading;
    // camera.tilt = 90;

    // this.sceneView.goTo({
    //   target: this.mapView.center,
    //   scale: 700,
    //   heading: geolocation.heading,
    //   tilt: 90,
    // }, {
    //   animate: true,
    //   duration: 1000,
    //   easing: 'linear',
    // });

  };

  VirtualDriveViewModel.prototype.updateGameEngineState = function(vehicleGeolocation) {
    const self = this;
    try {
      const fpsValue = self.gameEngine.actualFps;
      const vehicleAcceleration = self.mainVehicle.acceleration.toFixed(1);
      const vehicleDeacceleration = self.mainVehicle.deacceleration.toFixed(1);
      const vehicleSpeed = vehicleGeolocation.speed.toFixed(2);
      const vehicleHeadaing = vehicleGeolocation.heading.toFixed(0);
      const vehicleLongitude = vehicleGeolocation.longitude.toFixed(6);
      const vehicleLatitude = vehicleGeolocation.latitude.toFixed(6);
      const msg = `[<b>FPS:</b> ${fpsValue}] [Vehicle (<b>Lon:</b> ${vehicleLongitude}, <b>Lat:</b> ${vehicleLatitude}, <b>Speed:</b> ${vehicleSpeed} km/h, <b>Heading:</b> ${vehicleHeadaing}, <b>Acc:</b> ${vehicleAcceleration} m/s^2, <b>Decc:</b> ${vehicleDeacceleration} m/s^2)] `;
      document.getElementById('dbgMessage').innerHTML = msg;
    }
    catch (err) {
      console.error('updateGameEngineState: ', err);
    }
  };

  VirtualDriveViewModel.prototype.addPointOfVehicleGeolocation = function (geolocation) {
    const self = this;

    // create a new point graphic based on geolocation
    const point = new arcgis.Point({
      type: "point",
      longitude: geolocation.longitude,
      latitude: geolocation.latitude,
    });
    const simplePointGraphic = new arcgis.Graphic({
      symbol: vehicleGeolocationMarkerSymbol,
      geometry: point,
      attributes: {
        longitude: geolocation.longitude,
        latitude: geolocation.latitude,
        speed: geolocation.speed,
        heading: geolocation.heading,
        time: new Date(geolocation.time),
      },
      popupTemplate: vehicleGeolocationGraphicPopupTemplate,
    });

    self.allPointsLayer.graphics.add(simplePointGraphic);
  };

  VirtualDriveViewModel.prototype.drawDrivingRouteOnMap = function (routeData) {
    const self = this;

    if (!routeData || !routeData.routePolyline) {
      return;
    }

    const routePolyline = arcgis.Polyline.fromJSON(routeData.routePolyline);
    const routeGraphic = new arcgis.Graphic({
      geometry: routePolyline,
      symbol: routeSymbol,
      attributes: { type: "route" }
    });

    self.drivingRouteLayer.graphics.add(routeGraphic);
  };

  VirtualDriveViewModel.prototype.clearDrivingRouteOnMap = function () {
    const self = this;
    self.drivingRouteLayer.graphics.removeAll();
  };

  window.rootViewModel = new VirtualDriveViewModel();

  require([
    "esri/config",
    "esri/Map",
    "esri/views/MapView",
    "esri/views/SceneView",
    "esri/layers/SceneLayer",
    "esri/widgets/BasemapGallery",
    "esri/widgets/Search",
    "esri/Graphic",
    "esri/layers/GraphicsLayer",
    "esri/geometry/Point",
    "esri/geometry/Polyline",
    "esri/geometry/Extent",
    "esri/geometry/SpatialReference",
    "esri/geometry/support/geodesicUtils"
  ], function (
    esriConfig,
    Map,
    MapView,
    SceneView,
    SceneLayer,
    BasemapGallery,
    Search,
    Graphic,
    GraphicsLayer,
    Point,
    Polyline,
    Extent,
    SpatialReference,
    geodesicUtils) {

    esriConfig.apiKey = "AAPKff73321f58474e949c0abba328e521a3pZHeWItbg9ksrdv-Dl3kYKBtkmjLeI5wB7I2YuMKSBvGLWG1oavQRKaqegaSL_38";
    window.arcgis = {};
    window.arcgis.Map = Map;
    window.arcgis.MapView = MapView;
    window.arcgis.BasemapGallery = BasemapGallery;
    window.arcgis.SceneView = SceneView;
    window.arcgis.Graphic = Graphic;
    window.arcgis.Point = Point;
    window.arcgis.Polyline = Polyline;
    window.arcgis.Extent = Extent;
    window.arcgis.SpatialReference = SpatialReference;
    window.arcgis.geodesicUtils = geodesicUtils;

    const map = new Map({
      basemap: "streets-navigation-vector" //Basemap layer service
    });

    const view = new MapView({
      map: map,
      center: _DEFAULT_MAP_CENTER, //Longitude, latitude
      zoom: _DEFAULT_MAP_ZOOM,
      container: "viewDiv"
    });

    view.on("key-down", function (event) {
      event && event.stopPropagation();
    });

    // Add Search widget
    const searchWidget = new Search({
      view: view
    });
    // Adds the search widget below other elements in
    // the top left corner of the view
    view.ui.add(searchWidget, {
      position: "top-left",
      index: 0
    });

    // Add basemap gallery
    const basemapGallery = new BasemapGallery({
      view: view,
      container: "basemapGalleryContainer",
      visible: false,
    });
    view.ui.add(basemapGallery, "top-right");

    // Add graphics layer
    const allPointsLayer = new GraphicsLayer();
    const drivingRouteLayer = new GraphicsLayer();
    drivingRouteLayer.id = 'drivingRouteLayer';
    map.add(allPointsLayer);
    map.add(drivingRouteLayer);


    // Add SceneView
    const buildings3DObjects = new SceneLayer({
      url:
        "https://services.arcgis.com/V6ZHFr6zdgNZuVG0/arcgis/rest/services/SF_BLDG_WSL1/SceneServer",
      renderer: {
        type: "simple",
        symbol: {
          type: "mesh-3d",
          symbolLayers: [
            {
              type: "fill",
              material: {
                color: [255, 237, 204],
                colorMixMode: "replace"
              },
              edges: {
                type: "solid",
                color: [133, 108, 62, 0.5],
                size: 1
              }
            }
          ]
        }
      }
    });
    buildings3DObjects.renderer = {
      type: "simple",
      symbol: {
        type: "mesh-3d",
        symbolLayers: [
          {
            type: "fill",
            material: {
              color: [255, 237, 204],
              colorMixMode: "replace"
            },
            edges: {
              type: "solid",
              color: [133, 108, 62, 0.5],
              size: 1
            }
          }
        ]
      }
    };

    const sceneView = new SceneView({
      // An instance of Map or WebScene
      map: new Map({
        // basemap: "streets"
        basemap: "arcgis-light-gray",
        ground: "world-elevation",
        layers: [buildings3DObjects]
      }),

      // The id of a DOM element (may also be an actual DOM element)
      container: "view3DDiv",
      camera: { // Sets the initial camera position
        position: {
          longitude: _DEFAULT_MAP_CENTER[0],
          latitude: _DEFAULT_MAP_CENTER[1],
          z: 1,
        },
        heading: 0,
        tilt: 90,
        scale: 700,
        qualityProfile: "high",
      }
    });

    rootViewModel.sceneView = sceneView;

    // Load Knockout MVVM
    rootViewModel.mapView = view;
    rootViewModel.basemapGallery = basemapGallery;
    rootViewModel.searchWidget = searchWidget;
    rootViewModel.allPointsLayer = allPointsLayer;
    rootViewModel.drivingRouteLayer = drivingRouteLayer;
    ko.applyBindings(window.rootViewModel);

    rootViewModel.initializeDrivingGameEngine();

    if (rootViewModel.webviewProxy.isHostConnected) {
      rootViewModel.webviewProxy.getHostDeviceInformation()
        .then(deviceInfo => {
          console.info('webviewProxy -> deviceInfo: ', deviceInfo);
        });
    }

  });

})();
