(function() {

  function toRadians(degrees) {
    return degrees * (Math.PI / 180);
  }

  function toDegrees(radians) {
    return radians * (180 / Math.PI);
  }

  function calculateHeadingByPoints(p1, p2) {
    const deltaX = p2.x - p1.x;
    const deltaY = p2.y - p1.y;

    const radians = Math.atan2(deltaY, deltaX);
    // Convert radians to degrees
    let deltaHeading = toDegrees(radians);

    // Adjust heading to match the ArcGIS style
    deltaHeading = (90 - deltaHeading + 360) % 360;

    // const deltaDistance = Math.sqrt(deltaX * deltaX + deltaY * deltaY);

    return deltaHeading;
  }

  class RouteCalculationHelper {
    constructor() {

    }

    // Generate lookup data for finding a point on a route by distance from start point
    generatePointByDistanceLookupData(routeFeature) {
      const lookupData = {};
      const routeLine = routeFeature.geometry; // Polyline
      const routePaths = routeLine.paths; // Array of paths
      const pathNodesOnLine = [];
      const spatialRef = routeFeature.geometry.spatialReference;

      // Iterate through each path
      let point1 = {
        x: 0,
        y: 0,
        spatialReference: spatialRef,
      };
      let point2 = {
        x: 0,
        y: 0,
        spatialReference: spatialRef,
      };
      let accumulatedDistance = 0;
      const pathCount = routePaths.length;

      pathNodesOnLine.push({
        distance: 0,
        point: {
          x: routePaths[0][0][0],
          y: routePaths[0][0][1],
          spatialReference: spatialRef,
        }
      })
      for (let i = 0; i < pathCount; i++) {
        const path = routePaths[i];

        // Iterate through each node in the path
        for (let j = 0; j < path.length - 1; j++) {
          const node = path[j];
          const nextNode = path[j + 1];

          // Calculate distance between nodes
          point1.x = node[0];
          point1.y = node[1];
          point2.x = nextNode[0];
          point2.y = nextNode[1];
          const distance = arcgis.geometryEngine.distance(point1, point2, 'meters');
          accumulatedDistance += distance;

          // Calculate the direction from the current node to the next node
          const heading = calculateHeadingByPoints(point1, point2);

          // Add node to lookup data
          const prevNode = pathNodesOnLine[pathNodesOnLine.length - 1];
          prevNode.toHeading = heading;
          pathNodesOnLine.push({
            distance: accumulatedDistance,
            fromHeading: heading, // from previous node to current node
            toHeading: null,
            point: {
              x: nextNode[0],
              y: nextNode[1],
            }
          });

        }
      }

      // Set the heading for the first and last nodes
      pathNodesOnLine[0].fromHeading = pathNodesOnLine[0].toHeading;
      pathNodesOnLine[pathNodesOnLine.length - 1].toHeading = pathNodesOnLine[pathNodesOnLine.length - 1].fromHeading;

      // Set the lookup data's properties
      lookupData.routeLengthInMeters = accumulatedDistance;
      lookupData.spatialReference = spatialRef;
      lookupData.nodesOnPath = pathNodesOnLine;

      return lookupData;
    }

    // Find a point on a route by distance(meters) from start point
    findNodeByDistance(lookupData, distance) {
      const nodesOnPath = lookupData.nodesOnPath;
      const nodeCount = nodesOnPath.length;
      let node = null;

      if (distance <= 0) {
        return Object.assign({}, nodesOnPath[0]);
      }

      if (distance >= lookupData.routeLengthInMeters) {
        return Object.assign({}, nodesOnPath[nodeCount - 1]);
      }

      // Find the node that is closest to the specified distance using binary search
      let leftIndex = 0;
      let rightIndex = nodeCount - 1;
      let midIndex = 0;
      let midDistance = 0;
      while (leftIndex + 1 < rightIndex) {
        midIndex = Math.floor((leftIndex + rightIndex) / 2);
        midDistance = nodesOnPath[midIndex].distance;

        if (midDistance < distance) {
          leftIndex = midIndex;
        }
        else if (midDistance > distance) {
          rightIndex = midIndex;
        }
        else {
          leftIndex = rightIndex = midIndex; // midIndex node is exactly the targe node
          break;
        }
      }

      // Check left-side node, if left-side's distance > target distance, we shift both leftIndex and rightIndex to left(by 1)
      if (nodesOnPath[leftIndex].distance > distance) {
        leftIndex--;
        rightIndex--;
      }

      if (leftIndex === rightIndex) {
        node = nodesOnPath[leftIndex];
      }
      else {
        // Generate the target point (x, y) based on linear interpolation of distance (based on leftNode and rightNode distance)
        const leftNode = nodesOnPath[leftIndex];
        const rightNode = nodesOnPath[rightIndex];
        const leftDistance = leftNode.distance;
        const rightDistance = rightNode.distance;
        const leftPoint = leftNode.point;
        const rightPoint = rightNode.point;
        const targetDistance = distance - leftDistance;
        const distanceRatio = targetDistance / (rightDistance - leftDistance);
        const targetX = leftPoint.x + (rightPoint.x - leftPoint.x) * distanceRatio;
        const targetY = leftPoint.y + (rightPoint.y - leftPoint.y) * distanceRatio;

        // Generate a temporary node for the target point
        node = {
          distance: distance,
          point: {
            x: targetX,
            y: targetY,
            spatialReference: lookupData.spatialReference,
          },
          fromHeading: leftNode.toHeading,
          toHeading: rightNode.fromHeading,
        };
      }

      return Object.assign({}, node);
    }

    getFirstNodeOnRoute(lookupData) {
      return Object.assign({}, lookupData.nodesOnPath[0]);
    }
  }


  // RouteCalculation class for defining constants
  class RouteDrivingPlanGenerator {
    constructor() {

      const self = this;

      self._accelaration = 2.788888; // m/s^2
      self._deceleration = 2.788888 * 2; // m/s^2
      self._globalMaxSpeed = 15; // m/s
      self._globalTurnSpeed = 1.2; // m/s
      self._maxAngleChangeInSegment = 10; // degrees
    }

    get accelaration() {
      return this._accelaration;
    }

    get deceleration() {
      return this._deceleration;
    }

    get globalMaxSpeed() {
      return this._globalMaxSpeed;
    }

    get globalTurnSpeed() {
      return this._globalTurnSpeed;
    }

    get maxAngleChangeInSegment() {
      return this._maxAngleChangeInSegment;
    }

    // Update the key parameters for computing driving plan
    updateDrivingParameters(parameters) {
      if (!parameters) {
        return;
      }

      const { acceleration, deceleration, maxSpeed, turnSpeed, maxAngleChangeInSegment } = parameters;

      this._accelaration = typeof(acceleration) === 'number' && acceleration > 1 ? acceleration : this._accelaration;
      this._deceleration = typeof(deceleration) === 'number' && deceleration > 1 ? deceleration : this._deceleration;
      this._globalMaxSpeed = typeof(maxSpeed) === 'number' && maxSpeed > 1 ? maxSpeed : this._globalMaxSpeed;
      this._globalTurnSpeed = typeof(turnSpeed) === 'number' && turnSpeed > 0.5 ? turnSpeed : this._globalTurnSpeed;
      this._maxAngleChangeInSegment = typeof(maxAngleChangeInSegment) === 'number' && maxAngleChangeInSegment > 0 ? maxAngleChangeInSegment : this._maxAngleChangeInSegment;
    }

    // Given a DrivingRoute's RouteData, try generating segments (with plan) and get a series of points on the route
    // From start time(0) and increment by given interval(in seconds) each iteration
    // Return an array of points
    generateGpsEventsOnRouteByTime(routeLookupData, interval) {
      if (!routeLookupData) {
        return null;
      }

      if (!interval || interval < 1) {
        console.error('Invalid interval, must >= 1 sec');
        return null;
      }

      const self = this;
      const spatialRef = routeLookupData.spatialReference;
      const segments = self.generateDrivingSegmentsWithPlan(routeLookupData);
      const segmentCount = segments.length;

      const minDrivingTime = segments[0].plan.startTime;
      const maxDrivingTime = segments[segmentCount - 1].plan.endTime;
      const gpsEvents = [];

      let eventTime = new Date();
      let drivingTime = 0;
      while (drivingTime <= maxDrivingTime) {
        const movingPoint = self.calculateMovingPointBySegementsAndDrivingTime(segments, drivingTime, spatialRef);
        if (movingPoint) {
          const gpsEvent = {
            longitude: movingPoint.longitude,
            latitude: movingPoint.latitude,
            heading: movingPoint.heading,
            speed: movingPoint.speed * 3.6, // km/h
            time: eventTime,
          }

          gpsEvents.push(gpsEvent);
        }

        drivingTime += interval;
        eventTime = new Date(eventTime.getTime() + interval * 1000);
      }

      return gpsEvents;
    }

    // Given computed segments(with plan) and a driving time (from start point time 0), find the point on the route with interpolated distance
    calculateMovingPointBySegementsAndDrivingTime(segments, drivingTime, spatialRef) {
      if (!segments || segments.length === 0) {
        return null;
      }

      const minDrivingTime = segments[0].plan.startTime;
      const maxDrivingTime = segments[segments.length - 1].plan.endTime;
      if (drivingTime < minDrivingTime || drivingTime > maxDrivingTime) {
        return null;
      }

      const self = this;
      const segmentCount = segments.length;
      let targetSegment = null;

      // Find the segment that contains the target time
      for (let i = 0; i < segmentCount; i++) {
        const segment = segments[i];
        if (drivingTime >= segment.plan.startTime && drivingTime <= segment.plan.endTime) {
          targetSegment = segment;
          break;
        }
      }

      if (!targetSegment) {
        return null;
      }

      // Calculate the distance from start point of the segment
      const timeOnSegment = drivingTime - targetSegment.plan.startTime;
      const [ distanceOnSegment, speedOnSegment ] = self.calculateDistanceAndSpeedOnSegmentByTime(targetSegment, timeOnSegment);
      if (distanceOnSegment === null || speedOnSegment === null) {
        throw new Error(`Failed to calculate distance or speed on segment ${targetSegment}, time: ${timeOnSegment}`);
      }

      // Find the node pair on targetSegment where the distanceOnSegment is between the two nodes
      const nodesOnSegment = targetSegment.nodes;
      const nodeCount = nodesOnSegment.length;

      let distanceOfFirstNode = nodesOnSegment[0].distance;
      let node1 = null;
      let node2 = null;
      for (let i = 0; i < nodeCount - 1; i++) {
        node1 = nodesOnSegment[i];
        node2 = nodesOnSegment[i + 1];
        const node1DistanceOnSegment = node1.distance - distanceOfFirstNode;
        const node2DistanceOnSegment = node2.distance - distanceOfFirstNode;

        if (distanceOnSegment >= node1DistanceOnSegment && distanceOnSegment <= node2DistanceOnSegment) {
          break;
        }
      }

      if (!node1 || !node2) {
        throw new Error(`Failed to find node pair for distance ${distanceOnSegment} on segment ${targetSegment}`);
      }

      // Interpolate the target point
      const node1DistanceOnSegment = node1.distance - distanceOfFirstNode;
      const node2DistanceOnSegment = node2.distance - distanceOfFirstNode;
      const distanceRatio = (distanceOnSegment - node1DistanceOnSegment) / (node2DistanceOnSegment - node1DistanceOnSegment);
      const node1Point = node1.point;
      const node2Point = node2.point;
      const targetX = node1Point.x + (node2Point.x - node1Point.x) * distanceRatio;
      const targetY = node1Point.y + (node2Point.y - node1Point.y) * distanceRatio;

      // generate a arcgis Point object with the targetX and targetY as X, Y coords
      const targetPoint = new arcgis.Point({
        x: targetX,
        y: targetY,
        spatialReference: spatialRef,
      });

      const targetHeading = node1.toHeading;
      const targetSpeed = speedOnSegment;

      const movingPoint = {
        longitude: targetPoint.longitude,
        latitude: targetPoint.latitude,
        heading: targetHeading,
        speed: targetSpeed, // m/s
      };

      return movingPoint;
    }

    // Given a segment and time on the segment, calculate the distance on the segment
    calculateDistanceAndSpeedOnSegmentByTime(segment, timeOnSegment) {
      const self = this;
      const segmentPlan = segment.plan;
      const acceleration = segmentPlan.acceleration;
      const deceleration = segmentPlan.deceleration;
      const accelerationTime = segmentPlan.accelerationTime;
      const constantSpeedTime = segmentPlan.constantSpeedTime;
      const decelerationTime = segmentPlan.decelerationTime;
      const accelerationDistance = segmentPlan.accelerationDistance;
      const constantSpeedDistance = segmentPlan.constantSpeedDistance;
      const startSpeed = segmentPlan.startSpeed;
      const endSpeed = segmentPlan.endSpeed;
      const maxSpeed = segmentPlan.maxSpeed;

      let speed = null;
      let distance = null;
      if (timeOnSegment <= accelerationTime) {
        // Acceleration
        distance = timeOnSegment * (startSpeed + maxSpeed) / 2;
        speed = startSpeed + timeOnSegment * acceleration;
      }
      else if (timeOnSegment <= accelerationTime + constantSpeedTime) {
        // Constant speed
        distance = accelerationDistance + (timeOnSegment - accelerationTime) * maxSpeed;
        speed = maxSpeed;
      }
      else if (timeOnSegment <= accelerationTime + constantSpeedTime + decelerationTime) {
        // Deceleration
        distance = accelerationDistance + constantSpeedDistance + (timeOnSegment - accelerationTime - constantSpeedTime) * (maxSpeed + endSpeed) / 2;
        speed = maxSpeed - (timeOnSegment - accelerationTime - constantSpeedTime) * deceleration;
      }
      else {
        // Invalid time
        return null;
      }

      return [ distance, speed ];
    }

    // Given a DrivingRoute LookupData, try generating a series of segments based on existing path nodes
    // If the heading change is too large, we put the previous and next path of the node into separate segments
    // Return an array of segments
    generateDrivingSegmentsWithPlan(lookupData) {
      const self = this;
      const nodesOnPath = lookupData.nodesOnPath;
      const nodeCount = nodesOnPath.length;
      const segments = [];

      if (nodeCount <= 1) {
        return segments;
      }

      // Iterate through each adjacent node pair(as a segment component), if its heading change from previous segment part(in current segment) is not too large,
      // we add the node pair to the current segment, otherwise we create a new segment
      let currentSegment = null;

      for (let i = 0; i < nodeCount - 1; i++) {
        const currentNode = nodesOnPath[i];
        const nextNode = nodesOnPath[i + 1];
        const componentDistance = nextNode.distance - currentNode.distance;

        const headingChange = Math.abs(currentNode.toHeading - currentNode.fromHeading);
        if (headingChange > self.maxAngleChangeInSegment) {
          // Save the current segment
          if (currentSegment) {
            segments.push(currentSegment);
          }

          // Create a new segment
          currentSegment = {
            nodes: [currentNode, nextNode],
            distance: componentDistance,
          };
        }
        else {
          // Add the node pair to the current segment
          if (!currentSegment) {
            currentSegment = {
              nodes: [currentNode, nextNode],
              distance: componentDistance,
            };
          }
          else {
            currentSegment.nodes.push(nextNode);
            currentSegment.distance += componentDistance;
          }
        }
      }

      // Save the last segment
      if (currentSegment) {
        segments.push(currentSegment);
      }

      // Go through each segment and generate the driving plan for it
      const globalMaxSpeed = self.globalMaxSpeed;
      const globalTurnSpeed = self.globalTurnSpeed;
      let accumulatedTime = 0;

      for (let i = 0; i < segments.length; i++) {
        const isFirstSegment = i === 0;
        const isLastSegment = i === segments.length - 1;
        const segment = segments[i];
        const segmentDistance = segment.distance;
        const segmentNodes = segment.nodes;
        const startNode = segmentNodes[0];
        const endNode = segmentNodes[segmentNodes.length - 1];
        const startSpeed = isFirstSegment ? 0 : globalTurnSpeed;
        const endSpeed = isLastSegment ? 0 : globalTurnSpeed;

        let planOfSegment = null;
        let maxSpeed = globalMaxSpeed;
        while (!planOfSegment && maxSpeed > 0) {
          planOfSegment = self.computeDrivingPlanOnSegment(segmentDistance, startSpeed, endSpeed, maxSpeed);
          if (!planOfSegment) {
            maxSpeed = maxSpeed / 2; // Reduce the max speed and try again
          }
        }

        if (!planOfSegment) {
          throw new Error(`Failed to generate driving plan for segment ${i}`);
        }

        // update startTime and endTime for the segment
        const startTime = accumulatedTime;
        const endTime = accumulatedTime + planOfSegment.totalTime;
        planOfSegment.startTime = startTime;
        planOfSegment.endTime = endTime;

        accumulatedTime = endTime; // Update accumulated time

        segment.plan = planOfSegment;
      }

      return segments;
    }

    // Given a straight line distance(meters), speed(meters/second) at the start and end points and max speed reached when passing the line
    // And using the global acceleration and deceleration parameters
    // compute the driving plan for the line, including time(seconds) for acceleration, constant speed and deceleration
    // Return an object with the following properties:
    //   distance: the distance of the line
    //   startSpeed: the speed at the start point
    //   endSpeed: the speed at the end point
    //   maxSpeed: the max speed reached when passing the line
    //   accelerationTime: the time(seconds) for acceleration
    //   constantSpeedTime: the time(seconds) for constant speed
    //   decelerationTime: the time(seconds) for deceleration
    //   totalTime: the total time(seconds) for the line
    computeDrivingPlanOnSegment(distance, startSpeed, endSpeed, maxSpeed) {
      const self = this;
      const plan = {};

      plan.acceleration = self.accelaration;
      plan.deceleration = self.deceleration;
      // Set the distance
      plan.totalDistance = distance;
      // Set the start speed
      plan.startSpeed = startSpeed;
      // Set the end speed
      plan.endSpeed = endSpeed;
      // Set the max speed
      plan.maxSpeed = maxSpeed;

      // Calculate the acceleration time
      const { accelerationTime, accelerationDistance} = self._calculateAccelerationTime(startSpeed, maxSpeed);
      if (accelerationTime <= 0 || accelerationDistance <= 0 || accelerationDistance > distance) {
        // Invalid plan
        return null;
      }

      // Calculate the deceleration time
      const { decelerationTime, decelerationDistance } = self._calculateDecelerationTime(endSpeed, maxSpeed);
      if (decelerationTime <= 0 || decelerationDistance <= 0 || decelerationDistance > distance) {
        // Invalid plan
        return null;
      }

      if (accelerationDistance + decelerationDistance > distance) {
        // Invalid plan
        return null;
      }

      // Calculate the constant speed time
      const constantSpeedDistance = distance - accelerationDistance - decelerationDistance;
      const constantSpeedTime = constantSpeedDistance / maxSpeed;

      // Calculate the total time
      const totalTime = accelerationTime + constantSpeedTime + decelerationTime;
      plan.totalTime = totalTime;

      // If plan is valid, we populate the plan object and return it
      plan.accelerationTime = accelerationTime;
      plan.accelerationDistance = accelerationDistance;
      plan.decelerationTime = decelerationTime;
      plan.decelerationDistance = decelerationDistance;
      plan.constantSpeedTime = constantSpeedTime;
      plan.constantSpeedDistance = constantSpeedDistance;


      return plan;
    }

    // Calculate the time(seconds) for acceleration
    // return both the time and moved distance
    _calculateAccelerationTime(startSpeed, maxSpeed) {
      const self = this;
      const acceleration = self.accelaration;
      const time = (maxSpeed - startSpeed) / acceleration;
      const movedDistance = (startSpeed + maxSpeed) * time / 2;
      return {
        accelerationTime: time,
        accelerationDistance: movedDistance,
      };
    }

    // Calculate the time(seconds) for deceleration
    // return both the time and moved distance
    _calculateDecelerationTime(endSpeed, maxSpeed) {
      const self = this;
      const deceleration = self.deceleration;
      const time = (maxSpeed - endSpeed) / deceleration;
      const movedDistance = (maxSpeed + endSpeed) * time / 2;
      return {
        decelerationTime: time,
        decelerationDistance: movedDistance,
      };
    }



  }

  window.routeDrivingPlanGenerator = new RouteDrivingPlanGenerator();
  window.routeCalculator = new RouteCalculationHelper();
})();
