(function(){

  const _DEFAULT_GAMEENGINE_OPTIONS = {
    fps: 20,
    is3DMode: false,
    mapToNorth: false,
  };

  class DrivingGameEngine {

    constructor(options) {

      options = Object.assign({}, _DEFAULT_GAMEENGINE_OPTIONS, options);

      this._lastFpsUpdatedOn = Date.now();
      this._framesInRecentSecond = 0;
      this._actualFps = 0;
      this._fps = options.fps;
      this._then = Date.now();
      this._frames = 0;
      this._gameObjects = [];
      this._isRunning = false;
      this._is3DMode = options.is3DMode;
      this._mapView = options.mapView;
      this._searchWidget = options.searchWidget;
      this._scenenView = options.sceneView;
      this._mapToNorth = options.mapToNorth;
      this._controlByGamepad = false;

      if (this.fps <=0 || this.fps > 60) {
        throw new Error('fps must be between 0 and 60');
      }

      if (!this._is3DMode && (!this._mapView || !(this._mapView instanceof arcgis.MapView))) {
        throw new Error('[2D] MapView is required and must be an instance of arcgis.MapView');
      }

      if (this._is3DMode && (!this._sceneView || !(this._sceneView instanceof arcgis.SceneView))) {
        throw new Error('[3D] SceneView is required and must be an instance of arcgis.SceneView');
      }

      // check if ths._view is arcgis MapView or SceneView
    }

    get fps() {
      return this._fps;
    }

    get actualFps() {
      return this._actualFps;
    }

    get frames() {
      return this._frames;
    }

    get framesInRecentSecond() {
      return this._framesInRecentSecond;
    }

    get is3DMode() {
      return !!this._is3DMode;
    }

    get mapView() {
      return this._mapView;
    }

    get searchWidget() {
      return this._searchWidget;
    }

    get mapToNorth() {
      return this._mapToNorth;
    }

    set mapToNorth(value) {
      if (typeof value !== 'boolean') {
        return;
      }

      if (this._mapToNorth === value) {
        return;
      }

      this._mapToNorth = value;
    }

    get controlByGamepad() {
      return this._controlByGamepad;
    }

    get isRunning() {
      return this._isRunning;
    }

    addGameObject(gameObject) {
      if (!gameObject) {
        return;
      }

      this._gameObjects.push(gameObject);
      const view = this.is3DMode ? this._scenenView : this._mapView;
      gameObject.addToGameEngine(this, this.is3DMode, view);
    }

    removeGameObject(gameObject) {
      if (!gameObject) {
        return;
      }

      gameObject.removeFromGameEngine();
      this._gameObjects.splice(this._gameObjects.indexOf(gameObject), 1);
    }

    update() {
      for (let i = 0; i < this._gameObjects.length; i++) {
        this._gameObjects[i].update();
      }
    }

    render() {
      for (let i = 0; i < this._gameObjects.length; i++) {
        this._gameObjects[i].render(this.mapToNorth);
      }
    }

    start(startupSettings) {
      if (this._isRunning) {
        return;
      }

      // Close any popup on MapView
      if (this.mapView.popup) {
        this.mapView.popup.close();
      }

      if (this.searchWidget) {
        this.searchWidget.visible = false;
      }

      // Update startup settings
      let drivingProfile = null;
      let drivingRoute = null;
      this._controlByGamepad = false; // Default to keyboard
      if (startupSettings) {
        // Set FPS
        if (startupSettings.fps && startupSettings.fps > 0) {
          this._fps = startupSettings.fps;
        }

        // Set control method
        this._controlByGamepad = startupSettings.controlMethod === 'Gamepad';

        // Set driving profile parameters
        if (startupSettings.drivingProfile) {
          drivingProfile = Object.assign({}, startupSettings.drivingProfile);
        }

        if (startupSettings.drivingRoute) {
          drivingRoute = Object.assign({}, startupSettings.drivingRoute);
        }
      }

      this._isRunning = true;
      for (let gameObj of this._gameObjects) {
        gameObj.startDriving(drivingProfile, drivingRoute);
      }

      const now = Date.now();
      this._lastFpsUpdatedOn = now;
      this._then = now;
      this._loop();
    }

    stop() {
      if (!this._isRunning) {
        return;
      }

      this._isRunning = false;
      for (let gameObj of this._gameObjects) {
        gameObj.stopDriving();
      }

      if (this.searchWidget) {
        this.searchWidget.visible = true;
      }

    }

    _loop() {
      if (!this._isRunning) {
        return;
      }

      // calculate elapsed time since last loop
      const now = Date.now();
      const elapsed = now - this._then;
      if (elapsed > (1000 / this._fps)) {
        ++this._frames;
        ++this._framesInRecentSecond;

        // update game objects
        this.update();
        this.render();

        // reset then
        this._then = now;

        // calculate fps
        if (now >= this._lastFpsUpdatedOn + 1000) {
          this._actualFps = this._framesInRecentSecond;
          this._framesInRecentSecond = 0;
          this._lastFpsUpdatedOn = now;
        }
      }

      // use requestAnimationFrame to call _loop() recursively
      if (this._isRunning) {
        requestAnimationFrame(() => this._loop());
      }
    }
  }

  window.VirtualDrivingGameEngine = DrivingGameEngine;
})();
