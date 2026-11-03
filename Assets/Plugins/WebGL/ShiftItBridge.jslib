// Publishes the running game's state onto the page so an automated session can PLAY the real
// WebGL build rather than mash keys blind.
//
// Without this, a browser harness can send Shift presses but cannot see a fuse, a shield or a dud,
// so it dies within a few passes and the "ten full runs" it reports are nothing like play. With
// it, the driver reads the same facts a player reads off the screen and makes the same decisions.
//
// It only ever WRITES a string to the page. Nothing here can affect the game.
mergeInto(LibraryManager.library, {

  ShiftItReportState: function (ptr) {
    var s = UTF8ToString(ptr);
    window.shiftItState = s;
    if (!window.shiftItLog) { window.shiftItLog = []; }
  },

  ShiftItReportRunEnd: function (passes, score, reasonPtr) {
    if (!window.shiftItRuns) { window.shiftItRuns = []; }
    window.shiftItRuns.push({
      passes: passes,
      score: score,
      reason: UTF8ToString(reasonPtr)
    });
  }
});
