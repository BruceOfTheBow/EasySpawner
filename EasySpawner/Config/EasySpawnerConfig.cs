using BepInEx.Configuration;

using UnityEngine;

namespace EasySpawner {
  public class EasySpawnerConfig {
    public static ConfigEntry<KeyboardShortcut> ToggleMenuShortcut { get; private set; }
    public static ConfigEntry<KeyboardShortcut> SpawnPrefabShortcut { get; private set; }
    public static ConfigEntry<KeyboardShortcut> UndoSpawnPrefabShortcut { get; private set; }

    public static ConfigEntry<float> UIWidth { get; private set; }

    public static void BindConfig(ConfigFile config) {
      ToggleMenuShortcut =
          config.Bind(
              "Hotkeys",
              "ToggleMenuShortcut",
              new KeyboardShortcut(KeyCode.Slash),
              "Keyboard shortcut to toggle the EasySpawner menu.");

      SpawnPrefabShortcut =
          config.Bind(
              "Hotkeys",
              "SpawnPrefabShortcut",
              new KeyboardShortcut(KeyCode.Equals),
              "Keyboard shortcut to spawn the selected prefab.");

      UndoSpawnPrefabShortcut =
          config.Bind(
              "Hotkeys",
              "UndoSpawnPrefabShortcut",
              new KeyboardShortcut(KeyCode.Z, KeyCode.LeftControl),
              "Keyboard shortcut to undo the last spawn prefab action.");

      UIWidth =
          config.Bind(
              "UI",
              "MenuWidth",
              450f,
              new ConfigDescription("Width of the menu", new AcceptableValueRange<float>(50f, 900f)));
    }
  }
}
