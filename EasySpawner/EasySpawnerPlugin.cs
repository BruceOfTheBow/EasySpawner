using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

using BepInEx;
using BepInEx.Logging;

using EasySpawner.UI;

using HarmonyLib;

using UnityEngine;

using static EasySpawner.EasySpawnerConfig;

namespace EasySpawner
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class EasySpawnerPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "comfy.ComfySpawner";
        public const string PluginName = "ComfySpawner";
        public const string PluginVersion = "1.7.1";

        private static AssetBundle menuAssetBundle;
        public static GameObject menuPrefab;
        public static GameObject menuGameObject;

        public static EasySpawnerMenu menu = new();
        public static EasySpawnerConfig config = new();

        public static List<string> prefabNames = new();
        private static List<string> playerNames = new();

        //List of each set of gameobjects spawned. Each list correlates to a spawn action
        public static Stack<List<GameObject>> spawnActions = new();

        public const string assetBundleName = "EasySpawnerAssetBundle";
        public const string favouritesFileName = "cooley.easyspawner.favouriteItems.txt";

        public static readonly string[] recipeNameFilterList =
            new string[] { "Recipe_PotionHealthMinor", "Recipe_PotionStaminaMinor" };

        public static readonly string[] itemNameFilterList =
                new string[] { "$item_fishingbait" };

    static ManualLogSource _logger;
        Harmony _harmony;

        public void Awake() {
          _logger = Logger;
          _logger.LogInfo("Easy spawner: Easy spawner loaded plugin");

          BindConfig(Config);

          _harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), harmonyInstanceId: PluginGuid);
        }

        public void OnDestroy() {
          StopAllCoroutines();
          DestroyMenu();

          _harmony?.UnpatchSelf();
        }

        void Update()
        {
            if (Player.m_localPlayer)
            {
                if (ToggleMenuShortcut.Value.IsDown())
                {
                    if (!menuGameObject)
                        CreateMenu();
                    else
                        menuGameObject.SetActive(!menuGameObject.activeSelf);
                }
                else if (menuGameObject)
                {
                    if (SpawnPrefabShortcut.Value.IsDown())
                    {
                        _logger.LogInfo("Spawn hotkey pressed");
                        menu.SpawnButton.onClick.Invoke();
                    }
                    else if (UndoSpawnPrefabShortcut.Value.IsDown() && spawnActions.Count > 0)
                    {
                        UndoSpawn();
                    }
                }
            }
        }

        public static AssetBundle GetAssetBundleFromResources(string fileName)
        {
            Assembly execAssembly = Assembly.GetExecutingAssembly();
            string resourceName = execAssembly.GetManifestResourceNames().Single(str => str.EndsWith(fileName));
            using (Stream stream = execAssembly.GetManifestResourceStream(resourceName))
            {
                return AssetBundle.LoadFromStream(stream);
            }
        }

        public static void LoadAsset(string assetFileName)
        {
            if (menuAssetBundle)
            {
                _logger.LogInfo("Easy spawner: menu asset bundle already loaded");
                return;
            }

            menuAssetBundle = GetAssetBundleFromResources(assetFileName);

            if (menuAssetBundle)
                _logger.LogInfo("Easy spawner: menu asset bundle loaded");
            else
                _logger.LogInfo("Easy spawner: menu asset bundle failed to load");
        }

        public static string GetFavouritesFilePath()
        {
            return Path.Combine(Paths.ConfigPath, favouritesFileName);
        }

        /// <summary>
        /// Load favourite prefabs from file, into PrefabState dictionary
        /// </summary>
        public static void LoadFavourites()
        {
            _logger.LogInfo("Easy spawner: load favourite Items from file: " + GetFavouritesFilePath());
            if (!File.Exists(GetFavouritesFilePath())) return;

            using (StreamReader file = File.OpenText(GetFavouritesFilePath()))
            {
                while (!file.EndOfStream)
                {
                    string prefabName = file.ReadLine();

                    if (prefabName == null || !EasySpawnerMenu.PrefabStates.ContainsKey(prefabName))
                    {
                        _logger.LogInfo("Easy spawner: favourite prefab '"+ prefabName + "' not found");
                        continue;
                    }

                    EasySpawnerMenu.PrefabStates[prefabName].isFavourite = true;
                }
            }
        }

        /// <summary>
        /// Saves favourite prefabs to file, from PrefabState dictionary
        /// </summary>
        public static void SaveFavourites()
        {
            _logger.LogInfo("Easy spawner: save favourite Items to file: " + GetFavouritesFilePath());
            using (StreamWriter file = File.CreateText(GetFavouritesFilePath()))
            {
                foreach (KeyValuePair<string, PrefabState> pair in EasySpawnerMenu.PrefabStates)
                {
                    if(!pair.Value.isFavourite) continue;

                    file.WriteLine(pair.Key);
                }
            }
        }

        private void CreateMenu()
        {
            _logger.LogInfo("Easy spawner: Loading menu prefab");

            if (!menuAssetBundle)
            {
                _logger.LogInfo("EasySpawner: Asset bundle not loaded");
                return;
            }

            if (!menuPrefab)
                menuPrefab = menuAssetBundle.LoadAsset<GameObject>("EasySpawnerMenu");

            if (!menuPrefab)
            {
                _logger.LogInfo("Easy spawner: Loading menu prefab failed");
                return;
            }
            _logger.LogInfo("Easy spawner: Successfully loaded menu prefab");

            //Add script to make menu mouse draggable as assetbundle cannot contain script
            if (!menuPrefab.GetComponent<UIElementDragger>())
                menuPrefab.AddComponent<UIElementDragger>();

            menuGameObject = Instantiate(menuPrefab);

            //Attach menu to Valheims UI gameobject
            var uiGO = GameObject.Find("IngameGui(Clone)");

            if (!uiGO)
            {
                _logger.LogInfo("Easy spawner: Couldnt find UI gameobject");
                return;
            }

            menuGameObject.transform.SetParent(uiGO.transform);
            menuGameObject.transform.localScale = new Vector3(1f, 1f, 1f);
            menuGameObject.transform.localPosition = new Vector3(-650, 0, 0);

            playerNames = GetPlayerNames();

            menu.CreateMenu(menuGameObject);
            Style.ApplyAll(menuGameObject, menu);

            //Attach CheckForNewPlayersCoroutine to UIElementdragger on the menu Gameobject so if it gets destroyed it also stops the coroutine
            menuGameObject.GetComponent<UIElementDragger>().StartCoroutine(CheckForNewPlayersCoroutine());
        }

        public static void SpawnPrefab(string prefabName, Player player, int amount = 1, int level = 1, bool pickup = false, bool ignoreStackSize = false)
        {
            _logger.LogInfo("Easy spawner: Trying to spawn " + prefabName);
            GameObject prefab = ZNetScene.instance.GetPrefab(prefabName);
            if (!prefab)
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, prefabName + " does not exist");
                _logger.LogInfo("Easy spawner: spawning " + prefabName + " failed");
            }
            else
            {
                List<GameObject> spawnedObjects = new List<GameObject>();

                //If prefab is an npc/enemy
                if (prefab.GetComponent<Character>())
                {
                    for (int i = 0; i < amount; i++)
                    {
                        GameObject spawnedChar = Instantiate(prefab, player.transform.position + player.transform.forward * 2f + Vector3.up, Quaternion.identity);
                        Character character = spawnedChar.GetComponent<Character>();
                        if (level > 1)
                            character.SetLevel(level);
                        spawnedObjects.Add(spawnedChar);
                    }
                }
                //if prefab is an item
                else if (prefab.GetComponent<ItemDrop>())
                {
                    ItemDrop itemPrefab = prefab.GetComponent<ItemDrop>();
                    if (itemPrefab.m_itemData.IsEquipable())
                    {
                        if (ignoreStackSize)
                        {
                            itemPrefab.m_itemData.m_stack = amount;
                            amount = 1;
                        }
                        itemPrefab.m_itemData.m_quality = level;
                        itemPrefab.m_itemData.m_durability = itemPrefab.m_itemData.GetMaxDurability();
                        for (int i = 0; i < amount; i++)
                        {
                            spawnedObjects.Add(SpawnItem(pickup, prefab, player));
                        }
                    }
                    else
                    {
                        int noOfStacks = 1;
                        int lastStack = 0;
                        if (ignoreStackSize)
                        {
                            itemPrefab.m_itemData.m_stack = amount;
                        }
                        else
                        {
                            int maxStack = itemPrefab.m_itemData.m_shared.m_maxStackSize;

                            //Some items maxStackSize incorrectly set to 0
                            if (maxStack < 1)
                                maxStack = 1;

                            itemPrefab.m_itemData.m_stack = maxStack;
                            noOfStacks = amount / maxStack;
                            lastStack = amount % maxStack;
                        }

                        for (int i = 0; i < noOfStacks; i++)
                        {
                            spawnedObjects.Add(SpawnItem(pickup, prefab, player));
                        }
                        if (lastStack != 0)
                        {
                            itemPrefab.m_itemData.m_stack = lastStack;
                            spawnedObjects.Add(SpawnItem(pickup, prefab, player));
                        }
                    }
                    itemPrefab.m_itemData.m_stack = 1;
                    itemPrefab.m_itemData.m_quality = 1;
                }
                else
                {
                    for (int i = 0; i < amount; i++)
                    {
                        spawnedObjects.Add(Instantiate(prefab, player.transform.position + player.transform.forward * 2f, Quaternion.identity));
                    }
                }

                spawnActions.Push(spawnedObjects);
                Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, "Spawning object " + prefabName);
                _logger.LogInfo("Easy spawner: Spawned " + amount + " " + prefabName);
            }
        }

        private static GameObject SpawnItem(bool pickup, GameObject prefab, Player player) {
            ItemDrop itemDrop = prefab.GetComponent<ItemDrop>();
            List<Recipe> recipes = ObjectDB.instance.m_recipes;
            long crafterId = 0L;
            string crafterName = "";
            bool shouldIncludeCrafterTags = recipes.Exists(recipe => {
                if (!recipe.m_item || recipeNameFilterList.Contains(recipe.name) ) {
                    return false;
                }
                return recipe.m_item.Equals(itemDrop);
            });

            if ((shouldIncludeCrafterTags || itemDrop.m_itemData.IsEquipable()) && !itemNameFilterList.Contains(itemDrop.m_itemData.m_shared.m_name)) {
                crafterId = Player.m_localPlayer.GetPlayerID();
                crafterName = Player.m_localPlayer.GetPlayerName();
                itemDrop.m_itemData.m_crafterID = crafterId;
                itemDrop.m_itemData.m_crafterName = crafterName;
            }

            if (pickup) {
                Player.m_localPlayer.GetInventory().AddItem(
                        itemDrop.name,
                        itemDrop.m_itemData.m_stack,
                        itemDrop.m_itemData.m_quality,
                        itemDrop.m_itemData.m_variant,
                        crafterId,
                        crafterName
                );
                return null;
            } else {
                itemDrop.m_itemData.m_dropPrefab = prefab;
                Vector3 position = player.transform.position + player.transform.forward * 2f + Vector3.up;
                ItemDrop dropped = ItemDrop.DropItem(itemDrop.m_itemData, 1, position, Quaternion.identity);
                return dropped.gameObject;
            }
        }

        private void UndoSpawn()
        {
            _logger.LogInfo("Easyspawner: Undo spawn action");

            if (spawnActions.Count <= 0)
                return;

            List<GameObject> spawnedGameObjects = spawnActions.Pop();

            _logger.LogInfo("Easyspawner: Destroying " + spawnedGameObjects.Count + " objects");
            string objectName = "objects";

            foreach (GameObject GO in spawnedGameObjects)
            {
                if (GO != null)
                {
                    objectName = GO.name.Remove(GO.name.Length - 7);
                    ZNetView zNetV = GO.GetComponent<ZNetView>();
                    if (zNetV && zNetV.IsValid() && zNetV.IsOwner())
                        zNetV.Destroy();
                }
            }

            Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Undo spawn of " + spawnedGameObjects.Count + " " + objectName);
            _logger.LogInfo("Easyspawner: Spawn undone");
        }

        private List<string> GetPlayerNames()
        {
            List<string> newPlayerNames = new List<string>();
            if (ZNet.instance)
            {
                foreach (ZNet.PlayerInfo player in ZNet.instance.GetPlayerList())
                {
                    newPlayerNames.Add(player.m_name);
                }
            }
            return newPlayerNames;
        }

        private void PlayerListChanged()
        {
            _logger.LogInfo("EasySpawner: Player list changed, updating player dropdown");
            playerNames = GetPlayerNames();
            menu.RebuildPlayerDropdown();
        }

        //Coroutine to check if list of player names has changed every 3 seconds while menu gameobject exists
        private IEnumerator CheckForNewPlayersCoroutine()
        {
            _logger.LogInfo("EasySpawner: Starting check for new players coroutine");
            while (menuGameObject)
            {
                yield return new WaitForSeconds(3);

                if (menuGameObject.activeSelf && ZNet.instance && Player.m_localPlayer)
                {
                    List<string> newPlayerNames = GetPlayerNames();
                    if (newPlayerNames.Count != playerNames.Count)
                    {
                        PlayerListChanged();
                        continue;
                    }

                    foreach (string name in newPlayerNames)
                    {
                        if (!playerNames.Contains(name))
                        {
                            PlayerListChanged();
                            break;
                        }
                    }
                }
            }
            _logger.LogInfo("EasySpawner: Stopping check for new players coroutine");
        }

        public static void DestroyMenu()
        {
            _logger.LogInfo("Easy spawner: Easy spawner unloading assets");

            if (menuGameObject)
            {
                menu.Destroy();
                Destroy(menuGameObject);
            }

            menuPrefab = null;

            if (menuAssetBundle)
                menuAssetBundle.Unload(true);

            _logger.LogInfo("Easy spawner: Easy spawner unloaded assets");
        }
    }
}