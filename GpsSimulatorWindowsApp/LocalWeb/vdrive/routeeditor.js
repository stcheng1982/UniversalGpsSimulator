(function() {

  // Esri API Key
  const esriApiKey = "AAPKff73321f58474e949c0abba328e521a3pZHeWItbg9ksrdv-Dl3kYKBtkmjLeI5wB7I2YuMKSBvGLWG1oavQRKaqegaSL_38";
  // Point the URL to esri online services
  const geocodingUrl = "http://geocode-api.arcgis.com/arcgis/rest/services/World/GeocodeServer";
  const routeUrl = "https://route-api.arcgis.com/arcgis/rest/services/World/Route/NAServer/Route_World";
  const _DEFAULT_MAP_CENTER = [ -73.94164, 42.8122 ];

  class RouteEditorMapState
  {
    static Idle = 0;
    static AddingStops = 1;
    static EditingStop = 2;
  }

  // Define the symbology used to display a debug point
  const debugPointSymbol = {
    type: "simple-marker", // autocasts as new SimpleMarkerSymbol()
    style: "circle",
    size: 6,
    color: [255, 255, 255],
    outline: {
      // autocasts as new SimpleLineSymbol()
      width: 1,
      color: [0, 0, 0]
    }
  };

  // Define the symbology used to display the stops
  const stopSymbol = {
    type: "simple-marker", // autocasts as new SimpleMarkerSymbol()
    style: "cross",
    size: 10,
    outline: {
      // autocasts as new SimpleLineSymbol()
      width: 2
    }
  };

  // Define the symbology used to display the route
  const routeSymbol = {
    type: "simple-line", // autocasts as SimpleLineSymbol()
    color: [0, 0, 255, 0.4],
    width: 3
  };

  const routePointSymbol = {
    type: "simple-marker", // autocasts as new SimpleMarkerSymbol()
    style: "circle",
    size: 16,
    color: [226, 119, 40],
    outline: {
      // autocasts as new SimpleLineSymbol()
      width: 1,
      // set to orange to make points stand out
      color: [255, 255, 255],
    }
  };

  let _basemapGallery = null;
  let _map = null;
  let _mapView = null;
  let _routeLayer = null;
  let _routeLayerView = null;
  let _routeLineGraphic = null;
  let _routePointGraphic = null;
  let _routeLookupData = null;

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

  async function solveRoute(stopGraphics) {
    if (!Array.isArray(stopGraphics) || stopGraphics.length < 2) {
      return null;
    }

    try {
          // Setup the route parameters
      const routeParams = new arcgis.RouteParameters({
        // An authorization string used to access the routing service
        apiKey: esriApiKey,
        stops: new arcgis.FeatureSet(),
        outSpatialReference: {
          // autocasts as new SpatialReference()
          wkid: 3857
        }
      });

      // Execute the route if 2 or more stops are input
      routeParams.stops.features.push(...stopGraphics);
      const solveResult = await arcgis.route.solve(routeUrl, routeParams);
      return solveResult;
    }
    catch (err) {
      console.error(err);
      return null;
    }
  }

  async function reverseGeocode(mapPoint) {
    const locatorParams = {
      apiKey: esriApiKey,
      location: mapPoint
    };

    try {
      const response = await  arcgis.locator.locationToAddress(geocodingUrl, locatorParams);
      console.debug('reverseGeocode response: ', response);
      const result = {
        address: response.address,
        location: response.location
      };
      return result;
    }
    catch (err) {
      console.error(err);
      return null;
    }
  }

  class RouteEditorViewModel {

    #webviewProxy = null;

    constructor() {
      const self = this;

      self.#webviewProxy = new window.RouteEditorWebViewProxy();

      // common properties
      self.obShowLoadingIndicator = ko.observable(false);
      self.obIsBasemapGalleryVisible = ko.observable(false);

      // Current Route properties
      self.obShowRouteInfoPanel = ko.observable(false);
      self.obRoutePanelExpanded = ko.observable(true);

      self.obCurrentRouteName = ko.observable('');
      self.obCurrentRouteStops = ko.observableArray([]);
      self.obCurrentRouteTotalKMs = ko.observable(0);
      self.obCurrentRouteTotalMeters = ko.computed(() => self.obCurrentRouteTotalKMs() * 1000);
      self.obRoutePointDistanceFromStart = ko.observable(0); // meters
      self.obRoutePointHeading = ko.observable(0); // degrees

      // Route Editor properties
      self.obMapState = ko.observable(RouteEditorMapState.Idle);
      self.obIsAddingStops = ko.computed(() => self.obMapState() === RouteEditorMapState.AddingStops);
      self.obIsEditingStop = ko.computed(() => self.obMapState() === RouteEditorMapState.EditingStop && !!self.obEditingStopItem());
      self.obEditingStopItem = ko.observable(null);

      // event handlers
      self.toggleBasemapGallery = self.toggleBasemapGallery.bind(self);
      self.toggleRouteInfoPanelVisibility = self.toggleRouteInfoPanelVisibility.bind(self);
      self.toggleRouteInfoPanelExpand = self.toggleRouteInfoPanelExpand.bind(self);

      self.zoomToRouteAreaClicked = self.zoomToRouteAreaClicked.bind(self);
      self.addStopsButtonClicked = self.addStopsButtonClicked.bind(self);
      self.copyStopInfoButtonClicked = self.copyStopInfoButtonClicked.bind(self);
      self.editStopButtonClicked = self.editStopButtonClicked.bind(self);
      self.removeStopButtonClicked = self.removeStopButtonClicked.bind(self);
      self.stopItemMouseOver = self.stopItemMouseOver.bind(self);
      self.stopItemMouseOut = self.stopItemMouseOut.bind(self);

      self.routePointDistanceChanged = self.routePointDistanceChanged.bind(self);
    }

    get webviewProxy() {
      return this.#webviewProxy;
    }

    get map() {
      return _map;
    }

    get mapView() {
      return _mapView;
    }

    get routeLayer() {
      return _routeLayer;
    }

    get routeLookupData() {
      return _routeLookupData;
    }

    get routeLineGraphic() {
      return _routeLineGraphic;
    }

    get routePointGraphic() {
      return _routePointGraphic;
    }

    showLoadingIndicator() {
      const self = this;
      self.obShowLoadingIndicator(true);
    }

    hideLoadingIndicator() {
      const self = this;
      self.obShowLoadingIndicator(false);
    }

    async initializeMapView() {
      const self = this;

      self.showLoadingIndicator();
      try {
        // Add Search widget
        const searchWidget = new arcgis.Search({
          view: _mapView
        });

        // Adds the search widget below other elements in
        // the TOP RIGHT corner of the view
        _mapView.ui.add(searchWidget, {
          position: "top-left",
          index: 0
        });

        // Add basemap gallery
        _basemapGallery = new arcgis.BasemapGallery({
          view: _mapView,
          container: "basemapGalleryContainer",
          visible: false,
        });
        _mapView.ui.add(_basemapGallery, "top-right");

        // Adds a graphic when the user clicks the map. If 2 or more points exist, route is solved.
        _mapView.on("click", (event) => {
          self.mapViewClicked(event);
        });

        await _mapView.when();

      }
      catch (err) {
        console.error(err);
      }
      finally {
        self.hideLoadingIndicator();
      }
    }

    mapViewClicked(event) {
      const self = this;
      console.debug('mapViewClicked: ', event);

      if (self.obIsAddingStops()) {
        self.addStop(event);
      }
      else if (self.obIsEditingStop()) {
        self.editStop(event);
      }

    }

    toggleBasemapGallery() {
      const self = this;

      const isBasemapGalleryVisible = self.obIsBasemapGalleryVisible();
      const newBasemapGalleryVisible = !isBasemapGalleryVisible;

      if (newBasemapGalleryVisible) {
        self.obShowRouteInfoPanel(false);
      }

      self.obIsBasemapGalleryVisible(newBasemapGalleryVisible);
      _basemapGallery.visible = newBasemapGalleryVisible;
    }

    toggleRouteInfoPanelVisibility() {
      const self = this;
      const isRouteInfoPanelVisible = self.obShowRouteInfoPanel();
      const newRouteInfoPanelVisible = !isRouteInfoPanelVisible;

      if (newRouteInfoPanelVisible) {
        self.obIsBasemapGalleryVisible(false);
      }

      self.obShowRouteInfoPanel(newRouteInfoPanelVisible);
    }

    toggleRouteInfoPanelExpand() {
      const self = this;

      self.obRoutePanelExpanded(!self.obRoutePanelExpanded());
    }

    zoomToRouteAreaClicked() {
      const self = this;

      if (self.obCurrentRouteStops().length === 0 || !_routeLineGraphic) {
        return;
      }

      return self.mapView.goTo(_routeLineGraphic.geometry.extent).then(() => {
        const expandExtent = _routeLineGraphic.geometry.extent.clone();
        self.mapView.goTo(expandExtent.expand(1.2));
      });
    }

    addStopsButtonClicked() {
      const self = this;

      if (self.obIsAddingStops()) {
        self.obMapState(RouteEditorMapState.Idle);
      }
      else {
        if (self.obIsEditingStop()) {
          self.obEditingStopItem(null); // cancel editing
        }

        self.obMapState(RouteEditorMapState.AddingStops);
      }

    }

    copyStopInfoButtonClicked(stopItem) {
      // console.info('copyStopInfo: ', stopItem);
      const copyText = `Longitude: ${stopItem.longitude.toFixed(6)}, Latitude: ${stopItem.latitude.toFixed(6)}, Address: ${stopItem.address}`;
      navigator.clipboard.writeText(copyText);
    }

    editStopButtonClicked(stopItem) {
      const self = this;

      if (self.obIsEditingStop()) {
        if (self.obEditingStopItem() === stopItem) {
          self.obEditingStopItem(null); // cancel editing
          self.obMapState(RouteEditorMapState.Idle);
        }
        else {
          self.obEditingStopItem(stopItem);
        }
      }
      else {
        self.obMapState(RouteEditorMapState.EditingStop);
        self.obEditingStopItem(stopItem);
      }

    }

    removeStopButtonClicked(stopItem) {
      const self = this;

      if (!stopItem) {
        return;
      }

      if (!confirm('Are you sure you want to remove this stop?')) {
        return;
      }

      const stopGraphic = stopItem.graphic;
      if (stopGraphic) {
        _routeLayer.remove(stopGraphic);
      }

      self.obCurrentRouteStops.remove(stopItem);

      self.refreshCurrentRouteOnMap();
    }

    async stopItemMouseOver(stopItem) {
      const stopGraphic = stopItem.graphic;

      _routeLayerView = await _mapView.whenLayerView(_routeLayer);
      stopItem.layerHighlight = _routeLayerView.highlight(stopGraphic);
    }

    async stopItemMouseOut(stopItem) {
      // const stopGraphic = stopItem.graphic;
      if (stopItem.layerHighlight) {
        stopItem.layerHighlight.remove();
        stopItem.layerHighlight = null;
      }
    }

    async addStop(event) {
      const self = this;

      // Get address for location and add the stop into list
      const geocodeResult = await reverseGeocode(event.mapPoint);
      if (!geocodeResult) {
        alert('Failed to get address for location');
        return;
      }

      const { address, location } = geocodeResult;

      // Add a Point Graphic at the reverse geocoded location (as the stop location)
      const stopGraphic = new arcgis.Graphic({
        geometry: location,
        symbol: stopSymbol,
        attributes: { type: "stop" }
      });

      _routeLayer.add(stopGraphic);

      // Add a viewModel item for the new stop item
      const stopItem = {
        longitude: event.mapPoint.longitude,
        latitude: event.mapPoint.latitude,
        address: address,
        graphic: stopGraphic,
      };

      self.obCurrentRouteStops.push(stopItem);
      self.refreshCurrentRouteOnMap();
    }


    async editStop(event) {
      const self = this,
        editingStopItem = self.obEditingStopItem();

      if (!event || !editingStopItem) {
        return;
      }

      // Get address for location and add the stop into list
      const geocodeResult = await reverseGeocode(event.mapPoint);
      if (!geocodeResult) {
        alert('Failed to get address for location');
        return;
      }

      const { address, location } = geocodeResult;
      const stopGraphic = editingStopItem.graphic;
      stopGraphic.geometry = location; // update the stop location
      editingStopItem.longitude = location.longitude; // update the stop longitude
      editingStopItem.latitude = location.latitude; // update the stop latitude
      editingStopItem.address = address; // update the stop address

      // refresh the route path on map
      self.refreshCurrentRouteOnMap();
    }


    async refreshCurrentRouteOnMap() {
      const self = this;

      if (self.obCurrentRouteStops().length >= 2) {

        self.showLoadingIndicator();
        try {
          const stopGraphics = self.obCurrentRouteStops().map(s => s.graphic);
          const solveResult = await solveRoute(stopGraphics);
          if (solveResult) {
            self.showRoutePath(solveResult);
          }
          else {
            alert('Failed to solve route');
            self.showRoutePath(null);
          }
        }
        catch (err) {
          console.error(err);
        }
        finally {
          self.hideLoadingIndicator();
        }
      }
      else {
          self.showRoutePath(null);
      }
    }

    showRoutePath(data) {
      const self = this;
      // const existingRouteGraphics = _routeLayer.graphics.filter(g => g.attributes.type === 'route');
      if (_routeLineGraphic) {
        _routeLayer.remove(_routeLineGraphic);
      }

      _routeLineGraphic = null;
      _routePointGraphic.visible = false; // hide the route point graphic first
      _routeLookupData = null;
      self.obRoutePointDistanceFromStart(0);
      self.obRoutePointHeading(0);

      if (!data) {
        return;
      }

      _routeLineGraphic = data.routeResults[0].route;
      console.debug('[showRoutePath] routeLineGraphic: ', _routeLineGraphic);
      _routeLookupData = routeCalculator.generatePointByDistanceLookupData(_routeLineGraphic);

      // const totalKMs = routeGraphic.attributes.Total_Kilometers;
      const shapeLengthInMeters = _routeLineGraphic.attributes.Shape_Length;
      const shapeLengthKMs = shapeLengthInMeters / 1000;

      self.obCurrentRouteTotalKMs(shapeLengthKMs);

      // Update _routePointGraphic to the first node's point
      const firstNode = _routeLookupData.nodesOnPath[0];
      const initialPoint = _routePointGraphic.geometry.clone();
      initialPoint.x = firstNode.point.x;
      initialPoint.y = firstNode.point.y;
      initialPoint.spatialReference = _routeLookupData.spatialReference;
      _routePointGraphic.geometry = initialPoint; // update the route point graphic location
      _routePointGraphic.visible = true;

      self.obRoutePointDistanceFromStart(0);
      self.obRoutePointHeading(firstNode.fromHeading);

      // Update the route path graphic
      _routeLineGraphic.symbol = routeSymbol;
      _routeLineGraphic.attributes = Object.assign(_routeLineGraphic.attributes, { type: "route" });
      _routeLayer.add(_routeLineGraphic);
    }

    routePointDistanceChanged() {
      const self = this;
      if (!_routeLookupData) {
        return;
      }

      // console.debug('routePointDistanceChanged: distance from start: ', this.obRoutePointDistanceFromStart());
      const distanceInMeters = self.obRoutePointDistanceFromStart();
      const node = window.routeCalculator.findNodeByDistance(_routeLookupData, distanceInMeters);
      if (!node) {
        return;
      }

      const point = _routePointGraphic.geometry.clone();
      point.x = node.point.x;
      point.y = node.point.y;
      point.spatialReference = _routeLookupData.spatialReference;
      _routePointGraphic.geometry = point; // update the route point graphic location
      self.obRoutePointHeading(node.fromHeading);
    }

    async loadCurrentSelectedRoute(selectedRouteName) {
      const self = this;

      try {

        if (!selectedRouteName) {
          return;
        }

        const routeDataJSONStr = await self.webviewProxy.loadRouteDataFromLocalStorage(selectedRouteName);
        if (!routeDataJSONStr) {
          self.closeCurrentEditingRoute(true);
          self.obCurrentRouteName(selectedRouteName);
          return;
        }

        const routeData = JSON.parse(routeDataJSONStr);
        self.obCurrentRouteName(selectedRouteName);
        if (routeData.name !== selectedRouteName) {
          routeData.name = selectedRouteName;
        }

        self.restoreRouteFromSavedData(routeData);

        await self.zoomToRouteAreaClicked(); // zoom to stops
      }
      catch (err) {
        console.error(err);
      }
    }

    async saveCurrentEditingRoute() {
      const self = this;

      if (!_routeLineGraphic || !_routeLookupData) {
        alert('No Valid Route found');
        return;
      }

      let routeName = self.obCurrentRouteName();
      if (!routeName) {
        routeName = prompt('Please enter Route Name:');
        if (!routeName) {
          alert('Route Name is invalid');
          return;
        }

        self.obCurrentRouteName(routeName);
      }

      const previewImageDataUrl = await self.capturePreviewImageOfCurrentRoute();
      const exportedRouteData = self.exportCurrentRoute();
      const routeDataJSONStr = JSON.stringify(exportedRouteData);
      await self.webviewProxy.saveRouteDataToLocalStorage(routeName, routeDataJSONStr, previewImageDataUrl);

      alert('Route saved successfully');
    }

    renameCurrentEditingRoute(newRouteName) {
      const self = this;

      if (!newRouteName) {
        return;
      }

      const currentRouteName = self.obCurrentRouteName();
      if (!currentRouteName || currentRouteName === newRouteName) {
        return;
      }

      self.obCurrentRouteName(newRouteName);
      console.debug('renameCurrentEditingRoute: ', currentRouteName, ' -> ', newRouteName);
    }

    async capturePreviewImageOfCurrentRoute() {
      const self = this;
      // first zoom to the route area
      await self.zoomToRouteAreaClicked();

      // then capture the image
      const captureOptions = {
        quality: 95,
        x: 80,
        y: 50,
        width: self.mapView.width - 160,
        height: self.mapView.height - 100,
      };
      const screenshot = await self.mapView.takeScreenshot(captureOptions);
      const imageDataUrl = screenshot.dataUrl;
      return imageDataUrl;
    }

    exportCurrentRoute() {
      const self = this;
      const routeData = {
        name: self.obCurrentRouteName(),
        totalDistanceInMeters: self.obCurrentRouteTotalMeters(),
        stops: self.obCurrentRouteStops().map(s => {
          return {
            longitude: s.longitude,
            latitude: s.latitude,
            address: s.address,
          };
        }),
        routePolyline: _routeLineGraphic.geometry.toJSON(),
        lookupData: _routeLookupData,
        spatialReference: _routeLookupData.spatialReference.toJSON(),
      };

      return routeData;
    }

    closeCurrentEditingRoute(force = false) {
      const self = this;

      if (force !== true && !!self.obCurrentRouteName()) {
        if (!confirm('Are you sure you want to close the route in editing?')) {
          return;
        }
      }

      self.obCurrentRouteName(null);
      self.obCurrentRouteStops([]);
      self.obCurrentRouteTotalKMs(0);

      // clear existing route
      const stopGraphics = _routeLayer.graphics.filter(g => g.attributes.type === 'stop');
      const routeGraphics = _routeLayer.graphics.filter(g => g.attributes.type === 'route');
      if (stopGraphics.length > 0) {
        _routeLayer.removeMany(stopGraphics);
      }

      if (routeGraphics.length > 0) {
        _routeLayer.removeMany(routeGraphics);
      }

      _routeLineGraphic = null;
      _routePointGraphic.visible = false; // hide the route point graphic first
      _routeLookupData = null;
    }

    restoreRouteFromSavedData(routeData) {
      const self = this;

      console.debug('routeData of selectedRouteName: ', routeData);
      const spatialRef = arcgis.SpatialReference.fromJSON(routeData.spatialReference);

      // clear existing route
      self.closeCurrentEditingRoute(true);

      // restore route properties
      self.obCurrentRouteName(routeData.name);
      self.obCurrentRouteTotalKMs(routeData.totalDistanceInMeters / 1000);

      // restore route stops
      for (let s of routeData.stops) {
        const stopGraphic = new arcgis.Graphic({
          geometry: {
            type: "point",
            longitude: s.longitude,
            latitude: s.latitude,
            spatialReference: spatialRef,
          },
          symbol: stopSymbol,
          attributes: { type: "stop" }
        });

        const stopItem = {
          longitude: s.longitude,
          latitude: s.latitude,
          address: s.address,
          graphic: stopGraphic,
        };

        _routeLayer.add(stopGraphic);
        self.obCurrentRouteStops.push(stopItem);
      }

      // restore the route Polyline
      const routePolyline = arcgis.Polyline.fromJSON(routeData.routePolyline);
      _routeLineGraphic = new arcgis.Graphic({
        geometry: routePolyline,
        symbol: routeSymbol,
        attributes: { type: "route" }
      });

      _routeLayer.add(_routeLineGraphic);

      // restore the route lookup data
      _routeLookupData = routeData.lookupData;
      _routeLookupData.spatialReference = spatialRef;

      // Update _routePointGraphic to the first node's point
      const firstNode = _routeLookupData.nodesOnPath[0];
      const initialPoint = _routePointGraphic.geometry.clone();
      initialPoint.x = firstNode.point.x;
      initialPoint.y = firstNode.point.y;
      initialPoint.spatialReference = spatialRef;
      _routePointGraphic.geometry = initialPoint; // update the route point graphic location
      _routePointGraphic.visible = true;

    }

    async generateGpsEventsByCurrentDrivingRouteAndPlan(drivingPlanParameters) {
      if (!drivingPlanParameters) {
        alert('No driving plan arguments found');
        return;
      }

      const self = this;
      if (!self.obCurrentRouteName()) {
        alert('No route is being edited');
        return;
      }

      const routeName = self.obCurrentRouteName();
      const routeLookupData = self.routeLookupData;
      if (!routeLookupData) {
        alert('No Route Lookup Data found!');
        return;
      }

      const gpsEventInterval = drivingPlanParameters.intervalInSeconds;
      if (!gpsEventInterval || isNaN(gpsEventInterval) || gpsEventInterval < 1) {
        gpsEventInterval = 1; // 1 event per second by default
      }


      try {
        window.routeDrivingPlanGenerator.updateDrivingParameters(drivingPlanParameters);

        const gpsEvents = window.routeDrivingPlanGenerator.generateGpsEventsOnRouteByTime(routeLookupData, gpsEventInterval);
        await self.webviewProxy.saveGpsEventsOfVirtualDrivingRoutePLan(routeName, gpsEvents);
      }
      catch (err) {
        console.error(err);
        await self.webviewProxy.saveGpsEventsOfVirtualDrivingRoutePLan(routeName, null);
      }
    }

    drawDebugPoints(points) {
      if (!Array.isArray(points) || points.length === 0) {
        return;
      }

      const graphics = points.map(p => {
        return new arcgis.Graphic({
          geometry: {
            type: "point",
            longitude: p.x,
            latitude: p.y,
            spatialReference: { wkid: 102100 },
          },
          symbol: debugPointSymbol,
          attributes: { type: "debugPoint" }
        });
      });

      _routeLayer.addMany(graphics);
    }

    drawDebugExtent(extent) {
      if (!extent) {
        return;
      }

      const graphic = new arcgis.Graphic({
        geometry: extent,
        symbol: debugPointSymbol,
        attributes: { type: "debugPoint" }
      });

      _routeLayer.add(graphic);
    }

    clearDebugPoints() {
      const debugPoints = _routeLayer.graphics.filter(g => g.attributes.type === 'debugPoint');
      _routeLayer.removeMany(debugPoints);
    }
  }


  window.routeEditorVM = new RouteEditorViewModel();

  require([
    "esri/config",
    "esri/geometry/SpatialReference",
    "esri/Map",
    "esri/views/MapView",
    "esri/widgets/BasemapGallery",
    "esri/Graphic",
    "esri/layers/GraphicsLayer",
    "esri/rest/route",
    "esri/rest/support/RouteParameters",
    "esri/rest/locator",
    "esri/rest/support/FeatureSet",
    "esri/widgets/Search",
    "esri/geometry/geometryEngine",
    "esri/geometry/Point",
    "esri/geometry/Polyline",
    "esri/geometry/Extent",
  ], function(
    esriConfig,
    SpatialReference,
    Map,
    MapView,
    BasemapGallery,
    Graphic,
    GraphicsLayer,
    route,
    RouteParameters,
    locator,
    FeatureSet,
    Search,
    geometryEngine,
    Point,
    Polyline,
    Extent) {

    esriConfig.apiKey = esriApiKey;

    window.arcgis = {};
    window.arcgis.SpatialReference = SpatialReference;
    window.arcgis.BasemapGallery = BasemapGallery;
    window.arcgis.Search = Search;
    window.arcgis.locator = locator;
    window.arcgis.route = route;
    window.arcgis.RouteParameters = RouteParameters;
    window.arcgis.FeatureSet = FeatureSet;
    window.arcgis.Graphic = Graphic;
    window.arcgis.GraphicsLayer = GraphicsLayer;
    window.arcgis.geometryEngine = geometryEngine;
    window.arcgis.Point = Point;
    window.arcgis.Polyline = Polyline;
    window.arcgis.Extent = Extent;

    // The stops and route result will be stored in this layer
    _routeLayer = new GraphicsLayer();
    _routePointGraphic = new Graphic({
      symbol: routePointSymbol,
      attributes: { type: "pointOnRoute" },
      geometry: {
        type: "point",
        x: 0,
        y: 0,
        spatialReference: { wkid: 102100 }
      },
      visible: false,
    });
    _routeLayer.add(_routePointGraphic);

    _map = new Map({
      basemap: "streets-navigation-vector",
      layers: [_routeLayer] // Add the route layer to the map
    });

    _mapView = new MapView({
      container: "viewDiv", // Reference to the scene div created in step 5
      map: _map, // Reference to the map object created before the scene
      center: _DEFAULT_MAP_CENTER,
      zoom: 13
    });

    var rootViewModel = window.routeEditorVM;
    ko.applyBindings(rootViewModel);


    rootViewModel.initializeMapView();

  });
})();
