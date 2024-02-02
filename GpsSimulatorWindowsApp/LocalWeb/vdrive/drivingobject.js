(function() {

  const _DEFAULT_DELTA_ACCERATION_PER_SECOND = 5.0; // 5 m/s^2 per second
  const _DEFAULT_DEACCELERATION = 18; // 1 m/s^2 per second

  const _DEFAULT_MAX_MPS_SPEED = 37.777777;
  const _DEFAULT_MAX_ACCELERATION = 3.78;
  const _DEFAULT_DRAG_COEFFICIENT = 0.095; // 0.095;
  const _DEFAULT_MASS = 1000; // kg
  const _DEFAULT_DELTA_ANGLE_PER_SECOND = 15; // 15 degree per second

  const _ACCELERATION_STATE_CHANGE_PUBSUB_KEY = '_ACCELERATION_STATE_CHANGE_PUBSUB_KEY';
  const _DIRECTION_CHANGING_STATE_PUBSUB_KEY = '_DIRECTION_CHANGING_STATE_PUBSUB_KEY';
  const _GEOLOCATION_PUBSUB_KEY = '_GEOLOCATION_PUBSUB_KEY';
  const _FULLCONDUCT_ROUTE_COMPLETED_PUBSUB_KEY = '_FULLCONDUCT_ROUTE_COMPLETED_PUBSUB_KEY';


  const _DEFAULT_DRIVING_OBJECT_OPTIONS = {
    id: '',
    name: '',
  };

  const arrowSymbol = {
    type: "picture-marker",  // autocasts as new PictureMarkerSymbol()
    url: "../res/icon-arrow-north.png",
    width: "32px",
    height: "32px"
  };

  function toRadians(degrees) {
    return degrees * (Math.PI / 180);
  }

  function toDegrees(radians) {
    return radians * (180 / Math.PI);
  }

  function createDrivingObjectGraphic(drivingObject) {
    const point = {
      type: "point",
      longitude: drivingObject.longitude,
      latitude: drivingObject.latitude,
    };

    const pointGraphic = new arcgis.Graphic({
      geometry: point,
      symbol: arrowSymbol,
      attributes : {
        id: drivingObject.id,
        name: drivingObject.name,
      }
    });

    return pointGraphic;
  }


  class DrivingObject {
    constructor(options) {

      options = Object.assign({}, _DEFAULT_DRIVING_OBJECT_OPTIONS, options);

      this._id = options.id;
      this._name = options.name;

      // Driving profile arguments fields
      this._maxAcceleration = 0; // m/s^2
      this._maxDeceleration = 0; // m/s^2
      this._maxSpeed = 0; // m/s
      this._dragCoefficient = 0;
      this._mass = 0; // kg
      this._deltaAnglePerSecond = 0; //degrees

      // Driving Route fields
      this._drivingRouteData = null;
      this._usingFullConductRouteData = false;
      this._drivingRouteCompleted = false;

      // Driving state fields
      this._deltaX = 0;
      this._deltaY = 0;
      this._point = null;
      this._heading = 0;
      this._displayAngle = 0;
      this._speed = 0;
      this._acceleration = 0;
      this._deacceleration = 0;
      this._isDriving = false;
      this._isAccelerating = false;
      this._isDecelerating = false;
      this._steeringDirection = null; // null | 'left' | 'right'
      this._totalDistanceDrived = 0;

      this._needRefreshHeading = false;
      this._lastUpdate = Date.now();

      // Game Engine and Map fields
      this._gameEngine = null;
      this._mapView = null;
      this._sceneView = null;
      this._vehicleMarkerElement = null;


      this._accelerationStateSubscriptionSet = new Set();
      this._directionChangingStateSubscriptionSet = new Set();
      this._geolocationSubscriptionSet = new Set();
      this._fullconductRouteCompletedSubscriptionSet = new Set();
      this._lastGeolocationPublishTime = Date.now();
    }

    get gameEngine() {
      return this._gameEngine;
    }

    get id() {
      return this._id;
    }

    get name() {
      return this._name;
    }

    get drivingRouteData() {
      return this._drivingRouteData;
    }

    get usingFullConductRouteData() {
      return this._usingFullConductRouteData;
    }

    get drivingRouteCompleted() {
      return this._drivingRouteCompleted;
    }

    get maxAcceleration() {
      return this._maxAcceleration;
    }

    get maxDeceleration() {
      return this._maxDeceleration;
    }

    get maxSpeed() {
      return this._maxSpeed;
    }

    get dragCoefficient() {
      return this._dragCoefficient;
    }

    get mass() {
      return this._mass;
    }

    get deltaAnglePerSecond() {
      return  this._deltaAnglePerSecond;
    }

    get longitude() {
      if (!this._point) {
        return null;
      }
      return this._point.longitude;
    }

    get latitude() {
      if (!this._point) {
        return null;
      }
      return this._point.latitude;
    }

    get heading() {
      return this._heading;
    }

    get speed() {
      return this._speed;
    }

    get acceleration() {
      return this._acceleration;
    }

    get deacceleration() {
      return this._deacceleration;
    }

    get vehicleElement() {
      return this._vehicleMarkerElement;
    }

    get isDriving() {
      return this._isDriving;
    }

    get isAccelerating() {
      return this._isAccelerating;
    }

    get isDecelerating() {
      return this._isDecelerating;
    }

    get steeringDirection() {
      return this._steeringDirection;
    }

    get totalDistanceDrived() {
      return this._totalDistanceDrived;
    }

    get lastUpdate() {
      return this._lastUpdate;
    }

    addToGameEngine(gameEngine, is3DMode, view) {
      this._gameEngine = gameEngine;

      // Create a point graphic based on the current location and Id
      // const objectGraphic = createDrivingObjectGraphic(this);
      if (this.gameEngine.is3DMode) {
        this._sceneView = view;
        throw new Error('3D mode is not implemented yet');
      } else {
        this._mapView = view;
        this._vehicleMarkerElement = document.getElementById('mainVehicleMarker');
        // this._vehicleMarkerElement.style.display = '';
      }

    }

    removeFromGameEngine() {
      if (this.gameEngine.is3DMode) {
        throw new Error('3D mode is not implemented yet');
      } else {
        if (this._vehicleMarkerElement) {
          // this._vehicleMarkerElement.style.display = 'none';
          this._vehicleMarkerElement = null;
        }
      }

      this._gameEngine = null;
    }

    updateDrivingControlArguments(drivingProfile) {
      const self = this;

      if (!drivingProfile) {
        self._maxAcceleration = _DEFAULT_MAX_ACCELERATION;
        self._maxDeceleration = _DEFAULT_DEACCELERATION;
        self._maxSpeed = _DEFAULT_MAX_MPS_SPEED;
        self._dragCoefficient = _DEFAULT_DRAG_COEFFICIENT;
        self._mass = _DEFAULT_MASS;
        self._deltaAnglePerSecond = _DEFAULT_DELTA_ANGLE_PER_SECOND;
      }
      else {
        self._maxAcceleration = drivingProfile.Acceleration;
        self._maxDeceleration =  drivingProfile.Deceleration;
        self._maxSpeed = drivingProfile.MaxSpeed;
        self._dragCoefficient = drivingProfile.DragCoefficient;
        self._mass = drivingProfile.Mass;
        self._deltaAnglePerSecond = drivingProfile.DeltaAnglePerSecond;
      }

      console.debug(`[Update Driving Control Arguments] MaxAcc: ${self.maxAcceleration}, MaxDecc: ${self.maxDeceleration}, MaxSpeed: ${self.maxSpeed}, DragCoefficient: ${self.dragCoefficient}, Mass: ${self.mass}, DeltaAnglePerSecond: ${self.deltaAnglePerSecond}`);
    }

    startDriving(drivingProfile, drivingRoute) {
      if (this._isDriving) {
        return;
      }

      const mapCenter = this.gameEngine.mapView.center;
      let initialPoint = new arcgis.Point({ x: mapCenter.longitude, y: mapCenter.latitude }); // start from current map center;
      let initialHeading = 0;

      if (drivingRoute) {
        this._drivingRouteData = drivingRoute.data;
        this._usingFullConductRouteData = drivingRoute.routeConductType === 'FullConduct';
        this._drivingRouteCompleted = false;
        const firstNode = routeCalculator.getFirstNodeOnRoute(drivingRoute.data.lookupData);
        const firstPoint = new arcgis.Point(firstNode.point);
        initialPoint = new arcgis.Point({ x: firstPoint.longitude, y: firstPoint.latitude});
        initialHeading = firstNode.toHeading;
      } else {
        this._drivingRouteData = null;
        this._usingFullConductRouteData = false;
        this._drivingRouteCompleted = false;
      }

      this.updateDrivingControlArguments(drivingProfile);

      this._point = initialPoint
      this._heading = initialHeading;
      this._displayAngle = 0;
      this._acceleration = 0;
      this._deacceleration = 0;
      this._speed = 0;
      this._isAccelerating = false;
      this._isDecelerating = false;
      this._totalDistanceDrived = 0;
      this._deltaX = 0;
      this._deltaY = 0;
      this._steeringDirection = null;

      // Update map center to the first node on the route
      this.gameEngine.mapView.center = initialPoint;
      this.renderHeading(this.gameEngine.mapToNorth);


      // Update vehicle marker visibility
      this._vehicleMarkerElement.style.display = '';
      this._isDriving = true;
    }

    stopDriving() {
      if (!this._isDriving) {
        return;
      }

      this._vehicleMarkerElement.style.display = 'none';
      this._speed = 0;
      this._acceleration = 0;
      this._deacceleration = 0;
      this._isAccelerating = false;
      this._isDecelerating = false;

      this._isDriving = false;
    }

    subscribeGeolocationNotification(callbackFunc) {
      if (callbackFunc && typeof (callbackFunc) === 'function')
      {
        const subscription = PubSub.subscribe(_GEOLOCATION_PUBSUB_KEY, (msg, data) =>
        {
          callbackFunc(data);
        });

        this._geolocationSubscriptionSet.add(subscription);
        return subscription;
      }

      throw new Error('[subscribeGeolocationNotification] Invalid callback function ', callbackFunc);
    }

    unsubscribeGeolocationNotification(subscription) {
      if (subscription && this._geolocationSubscriptionSet.has(subscription))
      {
        PubSub.unsubscribe(subscription);
        this._geolocationSubscriptionSet.delete(subscription);
      }
    }

    publishNewGeolocationData(geolocation) {
      if (!geolocation) return;

			PubSub.publish(_GEOLOCATION_PUBSUB_KEY, geolocation);
    }

    subscribeAccelerationStateChangeNotification(callbackFunc) {
      if (callbackFunc && typeof (callbackFunc) === 'function')
      {
        const subscription = PubSub.subscribe(_ACCELERATION_STATE_CHANGE_PUBSUB_KEY, (msg, data) =>
        {
          callbackFunc(data);
        });

        this._accelerationStateSubscriptionSet.add(subscription);
        return subscription;
      }

      throw new Error('[subscribeAccelerationStateChangeNotification] Invalid callback function ', callbackFunc);
    }

    unsubscribeAccelerationStateChangeNotification(subscription) {
      if (subscription && this._accelerationStateSubscriptionSet.has(subscription))
      {
        PubSub.unsubscribe(subscription);
        this._accelerationStateSubscriptionSet.delete(subscription);
      }
    }

    publishAccelerationStateChange() {

      const isAccelerating = this.isAccelerating || this.isDecelerating;
      const speedInKmph = this.speed * 3.6;
      const accelerationState = {
        isAccelerating: isAccelerating,
        speed: speedInKmph,
      };
      PubSub.publish(_ACCELERATION_STATE_CHANGE_PUBSUB_KEY, accelerationState);
    }

    subscribeDirectionChangingStateNotification(callbackFunc) {
      if (callbackFunc && typeof (callbackFunc) === 'function')
      {
        const subscription = PubSub.subscribe(_DIRECTION_CHANGING_STATE_PUBSUB_KEY, (msg, data) =>
        {
          callbackFunc(data);
        });

        this._directionChangingStateSubscriptionSet.add(subscription);
        return subscription;
      }

      throw new Error('[subscribeDirectionChangingStateNotification] Invalid callback function ', callbackFunc);
    }

    unsubscribeDirectionChangingStateNotification(subscription) {
      if (subscription && this._directionChangingStateSubscriptionSet.has(subscription))
      {
        PubSub.unsubscribe(subscription);
        this._directionChangingStateSubscriptionSet.delete(subscription);
      }
    }

    publishDirectionChangingState(steeringDirection) {
      const dirChangingState = {
        isChanging: !!steeringDirection,
        direction: steeringDirection,
      };

      PubSub.publish(_DIRECTION_CHANGING_STATE_PUBSUB_KEY, dirChangingState);
    }

    subscribeFullConductRouteCompletedNotification(callbackFunc) {
      if (callbackFunc && typeof (callbackFunc) === 'function')
      {
        const subscription = PubSub.subscribe(_FULLCONDUCT_ROUTE_COMPLETED_PUBSUB_KEY, (msg, data) =>
        {
          callbackFunc(data);
        });

        this._fullconductRouteCompletedSubscriptionSet.add(subscription);
        return subscription;
      }

      throw new Error('[subscribeFullConductRouteCompletedNotification] Invalid callback function ', callbackFunc);
    }

    unsubscribeFullConductRouteCompletedNotification(subscription) {
      if (subscription && this._fullconductRouteCompletedSubscriptionSet.has(subscription))
      {
        PubSub.unsubscribe(subscription);
        this._fullconductRouteCompletedSubscriptionSet.delete(subscription);
      }
    }

    publishFullConductRouteCompleted() {
      PubSub.publish(_FULLCONDUCT_ROUTE_COMPLETED_PUBSUB_KEY, null);
    }

    updateHeadingByDirectionInput(secondsElapsed) {
      if (this.usingFullConductRouteData === true) {
        return [null, null]; // we don't allow  manual direction change if using full conduct route data
      }

      let deltaAngle = 0;
      const prevSteeringDirection = this.steeringDirection;

      let newSteeringDirection = null;
      if (this.gameEngine.controlByGamepad) {
        // Determine deltaAngle by gamepad input states
        const isDpadLeftPressed = window.gamepadEvents.isButtonPressed(GamepadButtonCodes.DP_LEFT);
        const isDpadRightPressed = window.gamepadEvents.isButtonPressed(GamepadButtonCodes.DP_RIGHT);

        const isLeftThumbstickPushedLeft = window.gamepadEvents.leftThumbstickPushedLeft;
        const isLeftThumbstickPushedRight = window.gamepadEvents.leftThumbstickPushedRight;

        if ((isDpadLeftPressed && isDpadRightPressed) || (isDpadLeftPressed && isLeftThumbstickPushedRight) || (isDpadRightPressed && isLeftThumbstickPushedLeft)) {
          deltaAngle = 0; // Do not adjust direction if both left+right Dpad buttons pressed
        }
        else if (isDpadLeftPressed || isLeftThumbstickPushedLeft) {
          deltaAngle = -this.deltaAnglePerSecond * secondsElapsed;
          newSteeringDirection = 'left';
        }
        else if (isDpadRightPressed || isLeftThumbstickPushedRight) {
          deltaAngle = this.deltaAnglePerSecond * secondsElapsed;
          newSteeringDirection = 'right';
        }
        else {
          deltaAngle = 0;
        }
      }
      else {
        // Determine deltaAngle by keyboard input states
        const isLeftKeyPressed = window.keyboardEvents.isKeyPressed(KeyCodes.LEFT);
        const isRightKeyPressed = window.keyboardEvents.isKeyPressed(KeyCodes.RIGHT);
        // const isUpKeyPressed = window.keyboardEvents.isKeyPressed(KeyCodes.UP);

        if (isLeftKeyPressed && isRightKeyPressed) {
          deltaAngle = 0; // Do not adjust direction if both left+right keys pressed
        }
        else if (isLeftKeyPressed) {
          deltaAngle = -this.deltaAnglePerSecond * secondsElapsed;
          newSteeringDirection = 'left';
        }
        else if (isRightKeyPressed) {
          deltaAngle = this.deltaAnglePerSecond * secondsElapsed;
          newSteeringDirection = 'right';
        }
        else {
          deltaAngle = 0;
        }
      }

      this._steeringDirection = newSteeringDirection;
      if (deltaAngle !== 0) {
        if (deltaAngle < -this.deltaAnglePerSecond) {
          deltaAngle = -this.deltaAnglePerSecond;
        }

        if (deltaAngle > this.deltaAnglePerSecond) {
          deltaAngle = this.deltaAnglePerSecond;
        }

        this._heading += deltaAngle;
        this._needRefreshHeading = true;
      }

      return [prevSteeringDirection, this.steeringDirection];
    }

    updateAccelerationBySpeedInput(secondsElapsed) {
      let deltaAcceleration = 0;
      let deltaDeacceleration = 0;
      let releaseAcceleration = false;
      let releaseDeacceleration = false;
      const prevAccelerationState = this.isAccelerating;
      const prevDecelerationState = this.isDecelerating;

      if (this.gameEngine.controlByGamepad) {
        const isButtonAPressed = window.gamepadEvents.isButtonPressed(GamepadButtonCodes.A);
        const isButtonBPressed = window.gamepadEvents.isButtonPressed(GamepadButtonCodes.B);

        const isRightThumbstickPushedUp = window.gamepadEvents.rightThumbstickPushedUp;
        const isRightThumbstickPushedDown = window.gamepadEvents.rightThumbstickPushedDown;

        if (isButtonAPressed || isRightThumbstickPushedUp) {
          deltaAcceleration += _DEFAULT_DELTA_ACCERATION_PER_SECOND * secondsElapsed;
          this._isAccelerating = true;
        }
        else {
          deltaAcceleration = 0;
          releaseAcceleration = true;
          this._isAccelerating = false;
        }

        if (isButtonBPressed || isRightThumbstickPushedDown) {
          deltaDeacceleration = this.maxDeceleration * secondsElapsed;
          this._isDecelerating = true;
        }
        else {
          deltaDeacceleration = 0;
          releaseDeacceleration = true;
          this._isDecelerating = false;
        }
      }
      else {
        const isAKeyPressed = window.keyboardEvents.isKeyPressed(KeyCodes.A);
        const isBKeyPressed = window.keyboardEvents.isKeyPressed(KeyCodes.B);
        const isDKeyPressed = window.keyboardEvents.isKeyPressed(KeyCodes.D);

        if (isAKeyPressed) {
          deltaAcceleration += _DEFAULT_DELTA_ACCERATION_PER_SECOND * secondsElapsed;
          this._isAccelerating = true;
        }
        else {
          deltaAcceleration = 0;
          releaseAcceleration = true;
          this._isAccelerating = false;
        }

        if (isBKeyPressed || isDKeyPressed) {
          deltaDeacceleration = this.maxDeceleration * secondsElapsed;
          this._isDecelerating = true;
        }
        else {
          deltaDeacceleration = 0;
          releaseDeacceleration = true;
          this._isDecelerating = false;
        }
      }

      if (!releaseAcceleration) {
        this._acceleration += deltaAcceleration;

        if (this._acceleration > this.maxAcceleration) {
          this._acceleration = this.maxAcceleration;
        }
      }
      else {
        this._acceleration = 0; // reset acceleration to 0
      }

      if (!releaseDeacceleration) {
        this._deacceleration = deltaDeacceleration;
      }
      else {
        this._deacceleration = 0; // reset deacceleration to 0
      }

      return [prevAccelerationState, this.isAccelerating, prevDecelerationState, this.isDecelerating];
    }

    calculateAccumulatedMovement() {

      const radians = Math.atan2(this._deltaY, this._deltaX);
      // Convert radians to degrees
      let accumulatedHeading = toDegrees(radians);

      // Adjust heading to match the ArcGIS style
      accumulatedHeading = (90 - accumulatedHeading + 360) % 360;

      const accumulatedDistance = Math.sqrt(this._deltaX * this._deltaX + this._deltaY * this._deltaY);

      return {
        heading: accumulatedHeading,
        distance: accumulatedDistance,
      };
    }

    update() {
      if (!this._isDriving) {
        return;
      }

      const now = Date.now();
      const timeElapsed = now - this._lastUpdate;
      const secondsElapsed = timeElapsed / 1000;

      // Try updating heading based on steer angle (direction input)
      const [prevSteeringDir, curSteeringDir] = this.updateHeadingByDirectionInput(secondsElapsed);

      // Try updating acceleration based on speed input
      const [prevAccState, curAccState, prevDeccState, curDeccState] = this.updateAccelerationBySpeedInput(secondsElapsed);

      // Update speed based on acceleration
      const prevSpeed = this.speed;
      this._speed += (this.acceleration - this.deacceleration) * secondsElapsed;
      if (this._speed > this.maxSpeed) {
        this._speed = this.maxSpeed;
      }

      this._speed -= this.dragCoefficient * prevSpeed * secondsElapsed;
      if (this._speed < 0) {
        this._speed = 0;
      }

      if (this._speed > 0) {
        // Update position based on acceleration and speed and angle of steering
        const metersMoved = this._speed * secondsElapsed + 1/2 * this._acceleration * secondsElapsed * secondsElapsed; // speed is in meters per second
        // Update total distance drived
        this._totalDistanceDrived += metersMoved;

        const normalizedHeading = (this._heading + 360) % 360;

        // Accumulate deltaX and deltaY
        const normalizedAngleRadius = toRadians(90 - normalizedHeading);
        const xOffsetInMeters = Math.cos(normalizedAngleRadius) * metersMoved;
        const yOffsetInMeters = Math.sin(normalizedAngleRadius) * metersMoved;

        this._deltaX += xOffsetInMeters;
        this._deltaY += yOffsetInMeters;
      }

      // Update geolocation every 1 second based on accumulated distance and heading
      if (this._lastGeolocationPublishTime + 1000 <= now) {

        let newLocation = null;

        if (this.usingFullConductRouteData === true) {
          // Update geolocation based on route data and total distance drived
          const routeData = this.drivingRouteData;
          const routeDataLookup = routeData.lookupData;
          const nodeOnRoute = window.routeCalculator.findNodeByDistance(routeDataLookup, this.totalDistanceDrived);
          if (nodeOnRoute) {
            newLocation = new arcgis.Point(nodeOnRoute.point);
            this._point.longitude = newLocation.longitude;
            this._point.latitude = newLocation.latitude;

            // Also update current driving object heading based on route node
            this._heading = nodeOnRoute.fromHeading;
            this._needRefreshHeading = true;
          }

          if (this.totalDistanceDrived >= routeDataLookup.routeLengthInMeters) {
            // Driving route completed
            this._drivingRouteCompleted = true;
            // Raise event to virtual driving host to stop driving
            this.publishFullConductRouteCompleted();
          }
        }
        else {
          // Update geolocation based on accumulated movement (deltaX, deltaY)
          // Calculate accumulated movement
          const accumulatedMove = this.calculateAccumulatedMovement();
          if (accumulatedMove.distance > 0) {
            newLocation = arcgis.geodesicUtils.pointFromDistance(
              // new arcgis.Point({ x: this._point.longitude, y: this._point.latitude }),
              this._point,
              accumulatedMove.distance,
              accumulatedMove.heading
            );

            this._point.longitude = newLocation.longitude;
            this._point.latitude = newLocation.latitude;
          }

          // Reset accumulated x and y offset
          this._deltaX = 0;
          this._deltaY = 0;
        }

        // Publish new geolocation data
        const speedInKmph = this.speed * 3.6;
        const evtHeading = (this._heading + 360) % 360;
        const evtData = {
          longitude: this._point.longitude,
          latitude: this._point.latitude,
          speed: speedInKmph,
          heading: evtHeading,
          time: now,
        };

        this.publishNewGeolocationData(evtData);
        this._lastGeolocationPublishTime = now;
      }

      // Publish acceleration state if vehicle is accelerating or accelerating stopped
      if ((curAccState === true || prevAccState === true) || (curDeccState === true || prevDeccState === true)) {
        this.publishAccelerationStateChange();
      }

      // Publish direction changing state if vehicle is changing direction or direction changing stopped
      if (curSteeringDir !== prevSteeringDir) {
        this.publishDirectionChangingState(curSteeringDir);
      }

      // Update last update time
      this._lastUpdate = now;
    }


    renderHeading(mapToNorth) {

      const targetGraphicAngle = mapToNorth ? this._heading % 360 : 0;

      if (this._displayAngle !== targetGraphicAngle) {
        this._displayAngle = targetGraphicAngle;
        const markerAngle = (parseInt(targetGraphicAngle) + 360) % 360;
        this._vehicleMarkerElement.style.transform = `rotate(${markerAngle}deg)`;
      }
    }

    /**
     * view could be either an Arcgis MapView or SceneView
     * @param {*} view
     */
    render(mapToNorth) {

      if (this._needRefreshHeading) {
        this.renderHeading(mapToNorth);
        this._needRefreshHeading = false;
      }

    }
  }

  window.drivingObject = DrivingObject;
})();
