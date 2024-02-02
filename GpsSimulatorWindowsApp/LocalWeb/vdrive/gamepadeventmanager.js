(function () {

  const _BUTTONS_PRESSED_PUBSUB_KEY = '_BUTTONS_PRESSED_PUBSUB_KEY';
  const _BUTTONS_RELEASED_PUBSUB_KEY = '_BUTTONS_RELEASED_PUBSUB_KEY';

  const _MIN_THUMBSTICK_PUSH_THRESHOLD = 0.5;
  const _MAX_THUMBSTICK_OFFSET = 1.0;

  function composeButtonCodesHash(buttonCodes) {
    if (!Array.isArray(buttonCodes) || buttonCodes.length === 0) {
      return '';
    }

    return buttonCodes.sort().join('+');
  }

  class GamepadButtonCodes {
    constructor() {
    }

    static get A() { return 'A' }

    static get B() { return 'B' }

    static get X() { return 'X' }

    static get Y() { return 'Y' }

    static get BACK() { return 'BACK' }

    static get START() { return 'START' }

    static get DP_UP() { return 'DP_UP' }

    static get DP_DOWN() { return 'DP_DOWN' }

    static get DP_LEFT() { return 'DP_LEFT' }

    static get DP_RIGHT() { return 'DP_RIGHT' }

    static get L_SHOULDER() { return 'L_SHOULDER' }

    static get R_SHOULDER() { return 'R_SHOULDER' }

    static get L_THUMB() { return 'L_THUMB' }

    static get R_THUMB() { return 'R_THUMB' }
  }

  class GamepadEventManager {

    constructor() {
      const self = this;

      self._buttonStateMap = new Map(); // key: ButtonCode, value: true | false (pressed | released)
      self._buttonsToPressedSubsMap = new Map(); // key: hash str of ButtonCode list, value: [subs]
      self._buttonsToReleasedSubsMap = new Map(); // key: hash str of ButtonCode list, value: [subs]

      self._leftThumbstickValue = { X: 0, Y: 0 };
      self._rightThumbstickValue = { X: 0, Y: 0 };

    }

    get buttonStateMap() {
      return this._buttonStateMap;
    }

    get leftThumbstickValue() {
      return this._leftThumbstickValue;
    }

    get rightThumbstickValue() {
      return this._rightThumbstickValue;
    }

    get leftThumbstickPushedLeft() {
      const leftThumbstickX = this._leftThumbstickValue.X;
      return leftThumbstickX < -_MIN_THUMBSTICK_PUSH_THRESHOLD;
    }

    get leftThumbstickPushedRight() {
      const leftThumbstickX = this._leftThumbstickValue.X;
      return leftThumbstickX > _MIN_THUMBSTICK_PUSH_THRESHOLD;
    }

    get leftThumbstickPushedUp() {
      const leftThumbstickY = this._leftThumbstickValue.Y;
      return leftThumbstickY > _MIN_THUMBSTICK_PUSH_THRESHOLD;
    }

    get leftThumbstickPushedDown() {
      const leftThumbstickY = this._leftThumbstickValue.Y;
      return leftThumbstickY < -_MIN_THUMBSTICK_PUSH_THRESHOLD;
    }

    get rightThumbstickPushedLeft() {
      const rightThumbstickX = this._rightThumbstickValue.X;
      return rightThumbstickX < -_MIN_THUMBSTICK_PUSH_THRESHOLD;
    }

    get rightThumbstickPushedRight() {
      const rightThumbstickX = this._rightThumbstickValue.X;
      return rightThumbstickX > _MIN_THUMBSTICK_PUSH_THRESHOLD;
    }

    get rightThumbstickPushedUp() {
      const rightThumbstickY = this._rightThumbstickValue.Y;
      return rightThumbstickY > _MIN_THUMBSTICK_PUSH_THRESHOLD;
    }

    get rightThumbstickPushedDown() {
      const rightThumbstickY = this._rightThumbstickValue.Y;
      return rightThumbstickY < -_MIN_THUMBSTICK_PUSH_THRESHOLD;
    }

    subscribeToButtonsPressed(buttonCodes, callback) {
      const self = this;

      if (!Array.isArray(buttonCodes) || buttonCodes.length === 0) {
        return null;
      }

      const hashbuttonCodes = composeButtonCodesHash(buttonCodes);
      if (!self._buttonsToPressedSubsMap.has(hashbuttonCodes)) {
        self._buttonsToPressedSubsMap.set(hashbuttonCodes, []);
      }

      const subscription = PubSub.subscribe(_BUTTONS_PRESSED_PUBSUB_KEY, (msg, data) =>
      {
        if (data && data.buttonCodesHash === hashbuttonCodes) {
          callback(data);
        }
      });

      const subs = self._buttonsToPressedSubsMap.get(hashbuttonCodes);
      subs.push(subscription);
      return subscription;
    }

    unsubscribeFromButtonsPressed(buttonCodes, subscription) {
      const self = this;

      if (!Array.isArray(buttonCodes) || buttonCodes.length === 0) {
        return;
      }

      const hashbuttonCodes = composeButtonCodesHash(buttonCodes);
      if (!self._buttonsToPressedSubsMap.has(hashbuttonCodes)) {
        return;
      }

      const subs = self._buttonsToPressedSubsMap.get(hashbuttonCodes);
      const index = subs.findIndex(sub => sub === subscription);
      if (index >= 0) {
        PubSub.unsubscribe(subscription);
        subs.splice(index, 1);
      }
    }

    raiseButtonsPressedEvent(buttonCodesHash, buttonCodes) {
      const self = this;

      PubSub.publish(_BUTTONS_PRESSED_PUBSUB_KEY, {
        buttonCodesHash: buttonCodesHash,
        buttonCodes: buttonCodes
      });
    }

    subscribeToButtonsReleased(buttonCodes, callback) {
      const self = this;

      if (!Array.isArray(buttonCodes) || buttonCodes.length === 0) {
        return null;
      }

      const hashbuttonCodes = composeButtonCodesHash(buttonCodes);
      if (!self._buttonsToReleasedSubsMap.has(hashbuttonCodes)) {
        self._buttonsToReleasedSubsMap.set(hashbuttonCodes, []);
      }

      const subscription = PubSub.subscribe(_BUTTONS_RELEASED_PUBSUB_KEY, (msg, data) =>
      {
        if (data && data.buttonCodesHash === hashbuttonCodes) {
          callback(data);
        }
      });

      const subs = self._buttonsToReleasedSubsMap.get(hashbuttonCodes);
      subs.push(subscription);
      return subscription;
    }

    unsubscribeFromButtonsReleased(buttonCodes, subscription) {
      const self = this;

      if (!Array.isArray(buttonCodes) || buttonCodes.length === 0) {
        return;
      }

      const hashbuttonCodes = composeButtonCodesHash(buttonCodes);
      if (!self._buttonsToReleasedSubsMap.has(hashbuttonCodes)) {
        return;
      }

      const subs = self._buttonsToReleasedSubsMap.get(hashbuttonCodes);
      const index = subs.indexOf(subscription);
      if (index >= 0) {
        subs.splice(index, 1);
      }
    }

    raiseButtonsReleasedEvent(buttonCodesHash, buttonCodes) {
      const self = this;

      PubSub.publish(_BUTTONS_RELEASED_PUBSUB_KEY, {
        buttonCodesHash: buttonCodesHash,
        buttonCodes: buttonCodes
      });
    }

    isButtonPressed(buttonCode) {
      if (!this._buttonStateMap.has(buttonCode)) {
        return false;
      }
      return this._buttonStateMap.get(buttonCode) === true;
    }

    isButtonReleased(buttonCode) {
      if (!this._buttonStateMap.has(buttonCode)) {
        return true;
      }
      return this._buttonStateMap.get(buttonCode) === false;
    }

    handleGamepadStates(gamepadStates) {
      const self = this;

      if (gamepadStates) {
        // A
        const buttonAPressed = !!gamepadStates.ButtonAPressed;
        self.handleGamepadButtonState(GamepadButtonCodes.A, buttonAPressed);

        // B
        const buttonBPressed = !!gamepadStates.ButtonBPressed;
        self.handleGamepadButtonState(GamepadButtonCodes.B, buttonBPressed);

        // X
        const buttonXPressed = !!gamepadStates.ButtonXPressed;
        self.handleGamepadButtonState(GamepadButtonCodes.X, buttonXPressed);

        // Y
        const buttonYPressed = !!gamepadStates.ButtonYPressed;
        self.handleGamepadButtonState(GamepadButtonCodes.Y, buttonYPressed);

        // DP_UP
        const dpadUpPressed = !!gamepadStates.DPadUpPressed;
        self.handleGamepadButtonState(GamepadButtonCodes.DP_UP, dpadUpPressed);

        // DP_DOWN
        const dpadDownPressed = !!gamepadStates.DPadDownPressed;
        self.handleGamepadButtonState(GamepadButtonCodes.DP_DOWN, dpadDownPressed);

        // DP_LEFT
        const dpadLeftPressed = !!gamepadStates.DPadLeftPressed;
        self.handleGamepadButtonState(GamepadButtonCodes.DP_LEFT, dpadLeftPressed);

        // DP_RIGHT
        const dpadRightPressed = !!gamepadStates.DPadRightPressed;
        self.handleGamepadButtonState(GamepadButtonCodes.DP_RIGHT, dpadRightPressed);

        // L_THUMB
        const lThumbPressed = !!gamepadStates.LeftThumbstick.Pressed;
        self.handleGamepadButtonState(GamepadButtonCodes.L_THUMB, lThumbPressed);
        self._leftThumbstickValue.X = gamepadStates.LeftThumbstick.X;
        self._leftThumbstickValue.Y = gamepadStates.LeftThumbstick.Y;

        // R_THUMB
        const rThumbPressed = !!gamepadStates.RightThumbstick.Pressed;
        self.handleGamepadButtonState(GamepadButtonCodes.R_THUMB, rThumbPressed);
        self._rightThumbstickValue.X = gamepadStates.RightThumbstick.X;
        self._rightThumbstickValue.Y = gamepadStates.RightThumbstick.Y;

        // BACK
        const backButtonPressed = !!gamepadStates.ButtonBackPressed;
        self.handleGamepadButtonState(GamepadButtonCodes.BACK, backButtonPressed);

        // START
        const startButtonPressed = !!gamepadStates.ButtonStartPressed;
        self.handleGamepadButtonState(GamepadButtonCodes.START, startButtonPressed);

        // L_SHOULDER
        const lShoulderPressed = !!gamepadStates.LeftShoulderPressed;
        self.handleGamepadButtonState(GamepadButtonCodes.L_SHOULDER, lShoulderPressed);

        // R_SHOULDER
        const rShoulderPressed = !!gamepadStates.RightShoulderPressed;
        self.handleGamepadButtonState(GamepadButtonCodes.R_SHOULDER, rShoulderPressed);

      }
    }

    handleGamepadButtonState(buttonCode, isPressed) {
      const self = this;

      if (isPressed) {
        self.markButtonAsPressed(buttonCode);
      }
      else {
        self.markButtonAsReleased(buttonCode);
      }
    }

    markButtonAsPressed(buttonCode) {
      const self = this;
      if (!self._buttonStateMap.has(buttonCode)) {
        self._buttonStateMap.set(buttonCode, false); // Add default entry
      }

      const prevButtonState = self._buttonStateMap.get(buttonCode);
      self._buttonStateMap.set(buttonCode, true);
      if (prevButtonState !== true) {
        // check and fire event if needed
        for (let [buttonCodesHash, subs] of self._buttonsToPressedSubsMap) {
          const buttonCodes = buttonCodesHash.split('+');
          if (buttonCodes.indexOf(buttonCode) >= 0) {
            const allButtonsPressed = buttonCodes.every(kc => self.isButtonPressed(kc));
            if (allButtonsPressed) {
              self.raiseButtonsPressedEvent(buttonCodesHash, buttonCodes);
            }
          }
        }

      }
    }

    markButtonAsReleased(buttonCode) {
      const self = this;
      if (!self._buttonStateMap.has(buttonCode)) {
        self._buttonStateMap.set(buttonCode, false); // Add default entry
      }

      const prevButtonState = self._buttonStateMap.get(buttonCode);
      self._buttonStateMap.set(buttonCode, false);
      if (prevButtonState !== false) {
        // check and fire event if needed
        for (let [buttonCodesHash, subs] of self._buttonsToReleasedSubsMap) {
          const buttonCodes = buttonCodesHash.split('+');
          if (buttonCodes.indexOf(buttonCode) >= 0) {
            const buttonsReleased = buttonCodes.filter(kc => self.isButtonReleased(kc));
            if (buttonsReleased.length === 1 && buttonsReleased[0] === buttonCode) {
              self.raiseButtonsReleasedEvent(buttonCodesHash, buttonCodes);
            }
          }
        }
      }

    }


    dumpCurrentButtonStates() {
      const self = this;

      const buttonStates = [];
      for (let [buttonCode, isPressed] of self._buttonStateMap) {
        buttonStates.push({
          button: buttonCode,
          isPressed: isPressed
        });
      }

      console.info('Current Button states: ', buttonStates);
      return buttonStates;
    }
  }

  window.GamepadButtonCodes = GamepadButtonCodes;
  window.GamepadEventManager = GamepadEventManager;
  window.gamepadEvents = new GamepadEventManager();
})();
