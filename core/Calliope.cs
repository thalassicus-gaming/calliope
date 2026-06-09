// Calliope.cs
// Document Version 0.3.0

using Lua;
using Lua.Standard;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Calliope {

  // ============================================================
  //  Data Classes
  // ============================================================

  public class ShuffleOption {
    public string text = "";
    public double repeatChance = 1.0;
    public Dictionary<string, bool>   allStatus     = new();
    public Dictionary<string, bool>   anyStatus     = new();
    public Dictionary<string, double> statusWeights = new();
    public Dictionary<string, bool>   setStatus     = new();
  }

  public class ShuffleDef {
    public Dictionary<string, ShuffleOption> options           = new();
    public Dictionary<string, double>        persistentWeights = new();
  }

  public class ButtonAction {
    public string text = "";
    public Dictionary<string, bool> setStatus = new();
  }

  public class ButtonDef {
    public string text     = "";
    public int position;
    public Dictionary<string, bool> allStatus = new();
    public Dictionary<string, bool> anyStatus = new();
    public Dictionary<string, bool> setStatus = new();
    public ButtonAction tap  = new();
    public ButtonAction hold = new();
  }

  public class ButtonPressCommand {
    public string buttonName = "";
    public bool   isHold     = false;
  }

  // ============================================================
  //  Shuffle Engine
  // ============================================================

  public static class ShuffleEvaluator {

    static readonly Regex shuffleTag = new Regex(@"\{shuffle\.(\w+)\}");
    static readonly Regex peopleTag  = new Regex(@"\{people\[(\d+)\]\.(\w+)\}");

    // Seeds persistentWeights from each option's base weight.
    // Called lazily on first evaluation of a shuffle.
    public static void InitializeWeights(ShuffleDef shuffleDef) {
      foreach (var pair in shuffleDef.options) {
        double baseWeight = pair.Value.statusWeights.TryGetValue("base", out var baseValue)
          ? baseValue : 1.0;
        shuffleDef.persistentWeights[pair.Key] = baseWeight;
      }
    }

    // Returns 0 if the option is ineligible under current status,
    // otherwise returns its effective weight (persistent * status multipliers).
    static double CalculateWeight(
        ShuffleOption option,
        Dictionary<string, bool> status,
        double persistentWeight) {
      // allStatus: every condition must match. Missing keys are treated as false.
      foreach (var condition in option.allStatus) {
        status.TryGetValue(condition.Key, out var allFlag);
        if (allFlag != condition.Value) return 0;
      }
      // anyStatus: at least one condition must match.
      if (option.anyStatus.Count > 0) {
        bool anyConditionMet = false;
        foreach (var condition in option.anyStatus) {
          status.TryGetValue(condition.Key, out var anyFlag);
          if (anyFlag == condition.Value) { anyConditionMet = true; break; }
        }
        if (!anyConditionMet) return 0;
      }
      // Apply statusWeights multipliers for active flags.
      double weight = persistentWeight;
      foreach (var condition in option.statusWeights) {
        if (condition.Key == "base") continue;
        if (status.TryGetValue(condition.Key, out var statusFlag) && statusFlag)
          weight *= condition.Value;
      }
      return weight;
    }

    // Selects an option, redistributes persistent weights, applies setStatus,
    // and returns the resolved text.
    public static string Evaluate(string shuffleName, StoryState storyState) {
      if (!storyState.shuffle.TryGetValue(shuffleName, out var shuffleDef)) return "";
      if (shuffleDef.persistentWeights.Count == 0) InitializeWeights(shuffleDef);

      // Build eligible weights for this evaluation.
      var eligibleWeights = new Dictionary<string, double>();
      foreach (var pair in shuffleDef.options) {
        double weight = CalculateWeight(pair.Value, storyState.status, shuffleDef.persistentWeights[pair.Key]);
        if (weight > 0) eligibleWeights[pair.Key] = weight;
      }
      if (eligibleWeights.Count == 0) return "";

      string selectedOptionKey  = Tools.GetRandomWeighted(eligibleWeights);
      ShuffleOption selectedOption = shuffleDef.options[selectedOptionKey];

      // Redistribute persistent weights using per-option repeatChance.
      double selectedBaseWeight = selectedOption.statusWeights.TryGetValue("base", out var selectedBase)
        ? selectedBase : 1.0;
      double totalBaseWeight = 0;
      foreach (var pair in shuffleDef.options)
        totalBaseWeight += pair.Value.statusWeights.TryGetValue("base", out var optionBase)
          ? optionBase : 1.0;

      double lostWeight              = shuffleDef.persistentWeights[selectedOptionKey] * (1.0 - selectedOption.repeatChance);
      double redistributeDenominator = totalBaseWeight - selectedBaseWeight;
      shuffleDef.persistentWeights[selectedOptionKey] *= selectedOption.repeatChance;

      if (redistributeDenominator > 0) {
        foreach (var pair in eligibleWeights) {
          if (pair.Key == selectedOptionKey) continue;
          double optionBaseWeight = shuffleDef.options[pair.Key].statusWeights.TryGetValue("base", out var currentBase)
            ? currentBase : 1.0;
          shuffleDef.persistentWeights[pair.Key] += lostWeight * optionBaseWeight / redistributeDenominator;
        }
      }

      // Apply this option's status side effects.
      foreach (var condition in selectedOption.setStatus)
        storyState.status[condition.Key] = condition.Value;

      return ResolveText(selectedOption.text, storyState);
    }

    // Replaces {shuffle.x} and {people[x].property} tags in a text template.
    // {people[x].property} is stubbed until the character system exists.
    public static string ResolveText(string template, StoryState storyState) {
      template = shuffleTag.Replace(template, match => Evaluate(match.Groups[1].Value, storyState));
      template = peopleTag.Replace(template, match => "Alex");
      return template;
    }
  }

  // ============================================================
  //  Story State
  // ============================================================

  public class StoryState {
    public Dictionary<string, bool>       status         = new();
    public Dictionary<string, ShuffleDef> shuffle        = new();
    public Dictionary<string, ButtonDef>  buttons        = new();
    public Queue<ButtonPressCommand>       pendingActions = new(); // stub for autonomous NPC behavior

    // Submits a command to the simulation and returns the resolved output text.
    public string Apply(ButtonPressCommand command) {
      return HandleButtonPress(command.buttonName, command.isHold);
    }

    // Validates and applies a button press, returning the resolved output text.
    private string HandleButtonPress(string buttonName, bool isHold) {
      if (!IsButtonVisible(buttonName)) return "";
      var button = buttons[buttonName];
      foreach (var condition in button.setStatus)
        status[condition.Key] = condition.Value;
      ButtonAction action = isHold ? button.hold : button.tap;
      foreach (var condition in action.setStatus)
        status[condition.Key] = condition.Value;
      return ShuffleEvaluator.ResolveText(action.text, this);
    }

    // Returns whether a button's visibility conditions are currently met.
    public bool IsButtonVisible(string buttonName) {
      if (!buttons.TryGetValue(buttonName, out var button)) return false;
      foreach (var condition in button.allStatus) {
        status.TryGetValue(condition.Key, out var allFlag);
        if (allFlag != condition.Value) return false;
      }
      if (button.anyStatus.Count > 0) {
        bool anyConditionMet = false;
        foreach (var condition in button.anyStatus) {
          status.TryGetValue(condition.Key, out var anyFlag);
          if (anyFlag == condition.Value) { anyConditionMet = true; break; }
        }
        if (!anyConditionMet) return false;
      }
      return true;
    }

    // ============================================================
    //  Data Loading
    // ============================================================

    public static async Task<StoryState> Parse(string lua) {
      var luaState = LuaState.Create();
      luaState.OpenMathLibrary();
      var results = await luaState.DoStringAsync(lua);
      var root = results[0].Read<LuaTable>();
      return ParseStoryState(root);
    }

    // --- Value helpers ---

    static string GetString(LuaTable sourceTable, string key, string defaultValue = "") {
      var value = sourceTable[key];
      return value.TryRead<string>(out var newString) ? newString : defaultValue;
    }

    static double GetDouble(LuaTable sourceTable, string key, double defaultValue = 0.0) {
      var value = sourceTable[key];
      return value.TryRead<double>(out var newDouble) ? newDouble : defaultValue;
    }

    static int GetInt(LuaTable sourceTable, string key, int defaultValue = 0) {
      var value = sourceTable[key];
      return value.TryRead<int>(out var newInt) ? newInt : defaultValue;
    }

    static Dictionary<string, bool> LuaToBoolDict(LuaTable sourceTable) {
      var newDict = new Dictionary<string, bool>();
      foreach (var pair in sourceTable) {
        if (pair.Key.TryRead<string>(out var newKey) && pair.Value.TryRead<bool>(out var newValue))
          newDict[newKey] = newValue;
      }
      return newDict;
    }

    static Dictionary<string, bool> GetBoolDict(LuaTable sourceTable, string key) {
      return sourceTable[key].TryRead<LuaTable>(out var subTable) ? LuaToBoolDict(subTable) : new();
    }

    static Dictionary<string, double> LuaToDoubleDict(LuaTable sourceTable) {
      var newDict = new Dictionary<string, double>();
      foreach (var pair in sourceTable) {
        if (pair.Key.TryRead<string>(out var newKey) && pair.Value.TryRead<double>(out var newValue))
          newDict[newKey] = newValue;
      }
      return newDict;
    }

    static Dictionary<string, double> GetDoubleDict(LuaTable sourceTable, string key) {
      return sourceTable[key].TryRead<LuaTable>(out var subTable) ? LuaToDoubleDict(subTable) : new();
    }

    // --- Parsers ---

    static ButtonAction ParseButtonAction(LuaTable sourceTable) {
      return new ButtonAction {
        text      = GetString(sourceTable, "text"),
        setStatus = GetBoolDict(sourceTable, "setStatus"),
      };
    }

    static ButtonDef ParseButtonDef(LuaTable sourceTable) {
      var button = new ButtonDef {
        text      = GetString(sourceTable, "text"),
        position  = GetInt(sourceTable, "position", 0),
        allStatus = GetBoolDict(sourceTable, "allStatus"),
        anyStatus = GetBoolDict(sourceTable, "anyStatus"),
        setStatus = GetBoolDict(sourceTable, "setStatus"),
      };
      if (sourceTable["tap"].TryRead<LuaTable>(out var tapTable))
        button.tap = ParseButtonAction(tapTable);
      if (sourceTable["hold"].TryRead<LuaTable>(out var holdTable))
        button.hold = ParseButtonAction(holdTable);
      return button;
    }

    static ShuffleOption ParseShuffleOption(LuaTable sourceTable) {
      return new ShuffleOption {
        text          = GetString(sourceTable, "text"),
        repeatChance  = GetDouble(sourceTable, "repeatChance", 1.0),
        allStatus     = GetBoolDict(sourceTable, "allStatus"),
        anyStatus     = GetBoolDict(sourceTable, "anyStatus"),
        statusWeights = GetDoubleDict(sourceTable, "statusWeights"),
        setStatus     = GetBoolDict(sourceTable, "setStatus"),
      };
    }

    static ShuffleDef ParseShuffleDef(LuaTable sourceTable) {
      var shuffle = new ShuffleDef();
      if (!sourceTable["options"].TryRead<LuaTable>(out var optionsTable)) return shuffle;
      foreach (var pair in optionsTable) {
        if (pair.Key.TryRead<string>(out var newKey) && pair.Value.TryRead<LuaTable>(out var optionTable))
          shuffle.options[newKey] = ParseShuffleOption(optionTable);
      }
      return shuffle;
    }

    static StoryState ParseStoryState(LuaTable sourceTable) {
      var state = new StoryState();
      if (sourceTable["status"].TryRead<LuaTable>(out var statusTable))
        state.status = LuaToBoolDict(statusTable);
      if (sourceTable["shuffle"].TryRead<LuaTable>(out var shuffleTable)) {
        foreach (var pair in shuffleTable) {
          if (pair.Key.TryRead<string>(out var key) && pair.Value.TryRead<LuaTable>(out var shuffleDefTable))
            state.shuffle[key] = ParseShuffleDef(shuffleDefTable);
        }
      }
      if (sourceTable["buttons"].TryRead<LuaTable>(out var buttonsTable)) {
        foreach (var pair in buttonsTable) {
          if (pair.Key.TryRead<string>(out var newKey) && pair.Value.TryRead<LuaTable>(out var buttonTable))
            state.buttons[newKey] = ParseButtonDef(buttonTable);
        }
      }
      return state;
    }
  }

}