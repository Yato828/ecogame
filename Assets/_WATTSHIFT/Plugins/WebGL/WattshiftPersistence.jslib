mergeInto(LibraryManager.library, {
  $WattshiftPersistenceState: { busy: false, queue: [], registered: false },
  Wattshift_SyncPersistentData__deps: ['$WattshiftPersistenceState'],
  Wattshift_SyncPersistentData: function(receiverPtr, ticket) {
    var receiver = UTF8ToString(receiverPtr);
    var state = WattshiftPersistenceState;
    state.queue.push({receiver: receiver, ticket: ticket});
    if (state.busy) return;
    var flush = function() {
      state.busy = true;
      var waiting = state.queue.splice(0);
      var finish = function(error) {
        state.busy = false;
        waiting.forEach(function(item) { SendMessage(item.receiver, 'OnStorageSync', item.ticket + (error ? ':error' : ':ok')); });
        if (state.queue.length && !state.busy) flush();
      };
      try { FS.syncfs(false, finish); } catch (error) { finish(error); }
    };
    flush();
  },
  Wattshift_RegisterPageLifecycle__deps: ['$WattshiftPersistenceState'],
  Wattshift_RegisterPageLifecycle: function(receiverPtr) {
    var state = WattshiftPersistenceState;
    if (state.registered) return;
    state.registered = true;
    var receiver = UTF8ToString(receiverPtr);
    document.addEventListener('visibilitychange', function() {
      if (document.hidden) SendMessage(receiver, 'FlushFromPage', '');
    });
    window.addEventListener('pagehide', function() { SendMessage(receiver, 'FlushFromPage', ''); });
  }
});
