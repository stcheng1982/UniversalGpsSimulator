(function () {

  const _KEYS_PRESSED_PUBSUB_KEY = '_KEYS_PRESSED_PUBSUB_KEY';
  const _KEYS_RELEASED_PUBSUB_KEY = '_KEYS_RELEASED_PUBSUB_KEY';

  function composeKeyCodesHash(keyCodes) {
    if (!Array.isArray(keyCodes) || keyCodes.length === 0) {
      return '';
    }

    return keyCodes.sort().join('+');
  }

  class KeyCodes {
    constructor() {
    }

    static get A() { return 'A' }

    static get B() { return 'B' }

    static get D() { return 'D' }

    static get N() { return 'N' }

    static get LEFT_CTRL() { return 'LeftControl' }

    static get LEFT() { return 'Left' }

    static get RIGHT() { return 'Right' }

    static get UP() { return 'Up' }

    static get DOWN() { return 'Down' }
  }

  class KeyboardEventManager {

    constructor() {
      const self = this;

      self._keyStateMap = new Map(); // key: keyCode, value: true | false (pressed | released)
      self._keysToPressedSubsMap = new Map(); // key: hash str of keycode list, value: [subs]
      self._keysToReleasedSubsMap = new Map(); // key: hash str of keycode list, value: [subs]
    }

    get keyStateMap() {
      return this._keyStateMap;
    }

    subscribeToKeysPressed(keyCodes, callback) {
      const self = this;

      if (!Array.isArray(keyCodes) || keyCodes.length === 0) {
        return null;
      }

      const hashKeyCodes = composeKeyCodesHash(keyCodes);
      if (!self._keysToPressedSubsMap.has(hashKeyCodes)) {
        self._keysToPressedSubsMap.set(hashKeyCodes, []);
      }

      const subscription = PubSub.subscribe(_KEYS_PRESSED_PUBSUB_KEY, (msg, data) =>
      {
        if (data && data.keyCodesHash === hashKeyCodes) {
          callback(data);
        }
      });

      const subs = self._keysToPressedSubsMap.get(hashKeyCodes);
      subs.push(subscription);
      return subscription;
    }

    unsubscribeFromKeysPressed(keyCodes, subscription) {
      const self = this;

      if (!Array.isArray(keyCodes) || keyCodes.length === 0) {
        return;
      }

      const hashKeyCodes = composeKeyCodesHash(keyCodes);
      if (!self._keysToPressedSubsMap.has(hashKeyCodes)) {
        return;
      }

      const subs = self._keysToPressedSubsMap.get(hashKeyCodes);
      const index = subs.findIndex(sub => sub === subscription);
      if (index >= 0) {
        PubSub.unsubscribe(subscription);
        subs.splice(index, 1);
      }
    }

    raiseKeysPressedEvent(keyCodesHash, keyCodes) {
      const self = this;

      PubSub.publish(_KEYS_PRESSED_PUBSUB_KEY, {
        keyCodesHash: keyCodesHash,
        keyCodes: keyCodes
      });
    }

    subscribeToKeysReleased(keyCodes, callback) {
      const self = this;

      if (!Array.isArray(keyCodes) || keyCodes.length === 0) {
        return null;
      }

      const hashKeyCodes = composeKeyCodesHash(keyCodes);
      if (!self._keysToReleasedSubsMap.has(hashKeyCodes)) {
        self._keysToReleasedSubsMap.set(hashKeyCodes, []);
      }

      const subscription = PubSub.subscribe(_KEYS_RELEASED_PUBSUB_KEY, (msg, data) =>
      {
        if (data && data.keyCodesHash === hashKeyCodes) {
          callback(data);
        }
      });

      const subs = self._keysToReleasedSubsMap.get(hashKeyCodes);
      subs.push(subscription);
      return subscription;
    }

    unsubscribeFromKeysReleased(keyCodes, subscription) {
      const self = this;

      if (!Array.isArray(keyCodes) || keyCodes.length === 0) {
        return;
      }

      const hashKeyCodes = composeKeyCodesHash(keyCodes);
      if (!self._keysToReleasedSubsMap.has(hashKeyCodes)) {
        return;
      }

      const subs = self._keysToReleasedSubsMap.get(hashKeyCodes);
      const index = subs.indexOf(subscription);
      if (index >= 0) {
        subs.splice(index, 1);
      }
    }

    raiseKeysReleasedEvent(keyCodesHash, keyCodes) {
      const self = this;

      PubSub.publish(_KEYS_RELEASED_PUBSUB_KEY, {
        keyCodesHash: keyCodesHash,
        keyCodes: keyCodes
      });
    }

    isKeyPressed(keyCode) {
      if (!this._keyStateMap.has(keyCode)) {
        return false;
      }
      return this._keyStateMap.get(keyCode) === true;
    }

    isKeyReleased(keyCode) {
      if (!this._keyStateMap.has(keyCode)) {
        return true;
      }
      return this._keyStateMap.get(keyCode) === false;
    }

    handleKeyboardStates(keyStates) {
      const self = this;

      if (Array.isArray(keyStates) && keyStates.length > 0) {

        // self.gameEngine.handleKeyboardInput(keyStates);
        for (let keyState of keyStates) {
          const keyCode = keyState.key;
          const isPressed = keyState.IsPressed;
          const isReleased = keyState.IsReleased;

          if (isPressed) {
            self.markKeyAsPressed(keyCode);
          }

          if (isReleased) {
            self.markKeyAsReleased(keyCode);
          }
        }
      }
    }

    markKeyAsPressed(keyCode) {
      const self = this;
      if (!self._keyStateMap.has(keyCode)) {
        self._keyStateMap.set(keyCode, false); // Add default entry
      }

      const prevKeyState = self._keyStateMap.get(keyCode);
      self._keyStateMap.set(keyCode, true);
      if (prevKeyState !== true) {
        // check and fire event if needed
        for (let [keyCodesHash, subs] of self._keysToPressedSubsMap) {
          const keyCodes = keyCodesHash.split('+');
          if (keyCodes.indexOf(keyCode) >= 0) {
            const allKeysPressed = keyCodes.every(kc => self.isKeyPressed(kc));
            if (allKeysPressed) {
              // console.debug('All keys pressed: ', keyCodes)
              self.raiseKeysPressedEvent(keyCodesHash, keyCodes);
            }
          }
        }

      }
    }

    markKeyAsReleased(keyCode) {
      const self = this;
      if (!self._keyStateMap.has(keyCode)) {
        self._keyStateMap.set(keyCode, false); // Add default entry
      }

      const prevKeyState = self._keyStateMap.get(keyCode);
      self._keyStateMap.set(keyCode, false);
      if (prevKeyState !== false) {
        // check and fire event if needed
        for (let [keyCodesHash, subs] of self._keysToReleasedSubsMap) {
          const keyCodes = keyCodesHash.split('+');
          if (keyCodes.indexOf(keyCode) >= 0) {
            const keysReleased = keyCodes.filter(kc => self.isKeyReleased(kc));
            if (keysReleased.length === 1 && keysReleased[0] === keyCode) {
              self.raiseKeysReleasedEvent(keyCodesHash, keyCodes);
            }
          }
        }
      }

    }


    dumpCurrentKeyStates() {
      const self = this;

      const keyStates = [];
      for (let [keyCode, isPressed] of self._keyStateMap) {
        keyStates.push({
          key: keyCode,
          isPressed: isPressed
        });
      }

      console.info('Current key states: ', keyStates);
      return keyStates;
    }
  }

  window.KeyCodes = KeyCodes;
  window.KeyboardEventManager = KeyboardEventManager;
  window.keyboardEvents = new KeyboardEventManager();
})();
