// test
// Nightlife.cs
// Document Version 0.3.0

using Godot;
using Calliope;
using System.Collections.Generic;

public partial class Nightlife : Control {

  [Export] private RichTextLabel outputLabel;
  [Export] private Container     buttonGrid;
	[Export] private string dataPath = "res://core/data.lua";

  private StoryState              storyState;
  private Timer                   holdTimer;
  private Dictionary<int, Button> buttonMap    = new();
  private int                     holdPosition = -1;

  private const float holdThreshold = 0.3f;

  public async override void _Ready() {
    string lua = FileAccess.GetFileAsString(dataPath);
    storyState = await StoryState.Parse(lua);
    SetupHoldTimer();
    BuildButtonMap();
    RefreshUI();
  }

  // ============================================================
  //  Setup
  // ============================================================

  void SetupHoldTimer() {
    holdTimer          = new Timer();
    holdTimer.WaitTime = holdThreshold;
    holdTimer.OneShot  = true;
    holdTimer.Timeout += OnHoldTimerTimeout;
    AddChild(holdTimer);
  }

  void BuildButtonMap() {
    for (int slotIndex = 0; slotIndex < buttonGrid.GetChildCount(); slotIndex++) {
      if (buttonGrid.GetChild(slotIndex) is Button uiButton) {
        int capturedIndex    = slotIndex;
        uiButton.ButtonDown += () => OnButtonDown(capturedIndex);
        uiButton.ButtonUp   += () => OnButtonUp(capturedIndex);
        buttonMap[slotIndex] = uiButton;
      }
    }
  }

  // ============================================================
  //  UI Updates
  // ============================================================

  void RefreshUI() {
    foreach (var uiButton in buttonMap.Values)
      uiButton.Visible = false;

    foreach (var pair in storyState.buttons) {
      if (storyState.IsButtonVisible(pair.Key)) {
        if (buttonMap.TryGetValue(pair.Value.position, out var uiButton)) {
          uiButton.Text    = pair.Value.text;
          uiButton.Visible = true;
        }
      }
    }
  }

  void ShowOutput(string outputText) {
		outputLabel.AppendText(outputText + "\n\n");
  }

  // ============================================================
  //  Input Handling
  // ============================================================

  void OnButtonDown(int slotIndex) {
    holdPosition = slotIndex;
    holdTimer.Start();
  }

  void OnButtonUp(int slotIndex) {
    if (holdTimer.TimeLeft > 0) {
      holdTimer.Stop();
      FireButtonAction(slotIndex, isHold: false);
    }
  }

  void OnHoldTimerTimeout() {
    if (holdPosition >= 0)
      FireButtonAction(holdPosition, isHold: true);
  }

  void FireButtonAction(int slotIndex, bool isHold) {
    foreach (var pair in storyState.buttons) {
      if (pair.Value.position == slotIndex && storyState.IsButtonVisible(pair.Key)) {
        var command = new ButtonPressCommand { buttonName = pair.Key, isHold = isHold };
        ShowOutput(storyState.Apply(command));
        RefreshUI();
        holdPosition = -1;
        return;
      }
    }
  }

}
