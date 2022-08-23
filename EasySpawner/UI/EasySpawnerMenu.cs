﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using UnityEngine;
using UnityEngine.UI;

using static EasySpawner.EasySpawnerConfig;

namespace EasySpawner.UI
{
    public class EasySpawnerMenu
    {
        public Dropdown PlayerDropdown;
        public InputField SearchField;
        public InputField AmountField;
        public InputField LevelField;
        public Toggle PutInInventoryToggle;
        public Toggle IgnoreStackSizeToggle;
        public Toggle FavouritesOnlyToggle;
        public Button SpawnButton;
        public Text SpawnText;
        public Text UndoText;
        public Text CloseText;
        public ScrollRect PrefabScrollView;
        public PrefabItem[] PrefabItems;
        public Queue<PrefabItem> PrefabItemPool = new Queue<PrefabItem>();
        public string SelectedPrefabName;
        public static Dictionary<string, PrefabState> PrefabStates = new Dictionary<string, PrefabState>();
        public List<string> SearchItems = new List<string>();

        public void CreateMenu(GameObject menuGameObject)
        {
            PlayerDropdown = menuGameObject.transform.Find("PlayerDropdown").GetComponent<Dropdown>();
            PrefabScrollView = menuGameObject.transform.Find("PrefabScrollView").GetComponent<ScrollRect>();

            SearchField = menuGameObject.transform.Find("SearchInputField").GetComponent<InputField>();
            SearchField.onValueChanged.AddListener(delegate { RebuildPrefabDropdown(); });

            AmountField = menuGameObject.transform.Find("AmountInputField").GetComponent<InputField>();
            LevelField = menuGameObject.transform.Find("LevelInputField").GetComponent<InputField>();

            PutInInventoryToggle = menuGameObject.transform.Find("PutInInventoryToggle").GetComponent<Toggle>();
            IgnoreStackSizeToggle = menuGameObject.transform.Find("IgnoreStackSizeToggle").GetComponent<Toggle>();
            FavouritesOnlyToggle = menuGameObject.transform.Find("FavouritesOnlyToggle").GetComponent<Toggle>();
            FavouritesOnlyToggle.onValueChanged.AddListener(delegate { RebuildPrefabDropdown(); });

            SpawnButton = menuGameObject.transform.Find("SpawnButton").GetComponent<Button>();
            SpawnButton.onClick.AddListener(SpawnButtonPress);

            Transform hotkeyTexts = menuGameObject.transform.Find("HotkeyText");
            SpawnText = hotkeyTexts.Find("SpawnText").GetComponent<Text>();
            SpawnText.horizontalOverflow = HorizontalWrapMode.Overflow;
            UndoText = hotkeyTexts.Find("UndoText").GetComponent<Text>();
            UndoText.horizontalOverflow = HorizontalWrapMode.Overflow;
            CloseText = hotkeyTexts.Find("CloseText").GetComponent<Text>();
            CloseText.horizontalOverflow = HorizontalWrapMode.Overflow;

            //Set hotkey texts
            EasySpawnerConfig config = EasySpawnerPlugin.config;
            SpawnText.text = $"Spawn: {SpawnPrefabShortcut.Value}";
            UndoText.text = $"Undo: {UndoSpawnPrefabShortcut.Value}";
            CloseText.text = $"Toggle: {ToggleMenuShortcut.Value}";

            UpdateMenuSize(null, null);

            UIWidth.SettingChanged += UpdateMenuSize;

            //Initial player dropdown
            PlayerDropdown.ClearOptions();
            RebuildPlayerDropdown();

            //Create prefab dropdown pool
            PrefabItems = new PrefabItem[20];
            PrefabItem template = PrefabScrollView.content.GetChild(0).gameObject.AddComponent<PrefabItem>();
            template.gameObject.SetActive(false);

            for (int i = 0; i < 20; i++)
            {
                GameObject option = UnityEngine.Object.Instantiate(template.gameObject, PrefabScrollView.content);
                PrefabItem item = option.GetComponent<PrefabItem>();
                item.Init(SelectPrefab, FavouritePrefab);
                PoolPrefabItem(item);
                PrefabItems[i] = item;
            }

            PrefabScrollView.onValueChanged.AddListener(UpdateItemPrefabPool);
            RebuildPrefabDropdown();
        }

        private void UpdateMenuSize(object sender, EventArgs e)
        {
            RectTransform menuRect = (RectTransform) EasySpawnerPlugin.menuGameObject.transform;
            menuRect.sizeDelta = new Vector2(UIWidth.Value, menuRect.sizeDelta.y);
        }

        public void PoolPrefabItem(PrefabItem item)
        {
            item.Pool();
            PrefabItemPool.Enqueue(item);
        }

        public void PoolAllPrefabItems()
        {
            foreach (PrefabItem item in PrefabItems)
            {
                if (PrefabItemPool.Contains(item))
                    continue;

                PoolPrefabItem(item);
            }
        }

        public void UpdateItemPrefabPool(Vector2 slider)
        {
            PrefabScrollView.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,(SearchItems.Count - 1) * 20f);
            Rect scrollRect = PrefabScrollView.GetComponent<RectTransform>().rect;
            Vector2 scrollPos = PrefabScrollView.content.anchoredPosition;

            // search for items that are out of visible scroll rect
            foreach (PrefabItem item in PrefabItems)
            {
                if (PrefabItemPool.Contains(item))
                    continue;

                float posY = item.rectTransform.anchoredPosition.y;

                if (posY > -scrollPos.y + 20 || posY < -scrollPos.y - scrollRect.height - 20)
                    PoolPrefabItem(item);
            }

            int startIndex = Mathf.Max(0, Mathf.CeilToInt((scrollPos.y - 20) / 20));
            int maxItems = Mathf.CeilToInt((scrollRect.height + 40) / 20);

            for (int i = startIndex; i < Mathf.Min(startIndex + maxItems, SearchItems.Count); i++)
            {
                if (PrefabItems.Any(x => x.posIndex == i))
                    continue;

                if (PrefabItemPool.Count > 0)
                {
                    PrefabItem item = PrefabItemPool.Dequeue();
                    item.rectTransform.anchoredPosition = new Vector2(0, -i * 20 - 10f);
                    item.posIndex = i;
                    item.SetName(SearchItems[i]);
                    item.toggle.SetIsOnWithoutNotify(SelectedPrefabName == item.GetName());
                    item.SetFavouriteOn(PrefabStates[item.GetName()].isFavourite, true);
                    item.gameObject.SetActive(true);
                }
            }
        }

        private void SelectPrefab(PrefabItem caller)
        {
            foreach (PrefabItem prefabItem in PrefabItems)
            {
                // Disable all other prefabItems, without calling this method recursively
                prefabItem.toggle.SetIsOnWithoutNotify(prefabItem == caller);
            }

            SelectedPrefabName = caller != null ? caller.GetName() : null;
        }

        private void FavouritePrefab(bool favourite, PrefabItem caller)
        {
            string name = caller.GetName();
            PrefabStates[name].isFavourite = favourite;

            EasySpawnerPlugin.SaveFavourites();
            RebuildPrefabDropdown();
        }

        public void RebuildPlayerDropdown()
        {
            if (PlayerDropdown && ZNet.instance && Player.m_localPlayer)
            {
                PlayerDropdown.ClearOptions();
                PlayerDropdown.options.Add(new Dropdown.OptionData(Player.m_localPlayer.GetPlayerName()));

                foreach (ZNet.PlayerInfo player in ZNet.instance.GetPlayerList())
                {
                    if (player.m_name != Player.m_localPlayer.GetPlayerName())
                        PlayerDropdown.options.Add(new Dropdown.OptionData(player.m_name));
                }
            }
            else
            {
                Debug.Log("EasySpawner: Cannot rebuild player dropdown");
            }
        }

        private void SpawnButtonPress()
        {
            if (SelectedPrefabName == null)
                return;
            string prefabName = SelectedPrefabName;
            bool pickup = PutInInventoryToggle.isOn;
            bool ignoreStackSize = IgnoreStackSizeToggle.isOn;
            Player player = Player.m_localPlayer;

            if (!Int32.TryParse(AmountField.text, out int amount) || amount < 1)
                amount = 1;
            if (!Int32.TryParse(LevelField.text, out int level) || level < 1)
                level = 1;

            //If not local player selected in player dropdown then get the Player object for selected player, if player not found then player will stay as local player
            if (!PlayerDropdown.options[PlayerDropdown.value].text.Equals(player.GetPlayerName()))
            {
                pickup = false;//Cannot place items in other players inventories

                foreach (Player playerCheck in Player.GetAllPlayers())
                {
                    if (playerCheck.GetPlayerName().Equals(PlayerDropdown.options[PlayerDropdown.value].text))
                    {
                        player = playerCheck;
                        break;
                    }
                }
            }

            EasySpawnerPlugin.SpawnPrefab(prefabName, player, amount, level, pickup, ignoreStackSize);
        }

        //Update dropdown options using new search parameters
        public void RebuildPrefabDropdown()
        {
            SelectPrefab(null);
            SearchItems.Clear();

            Parallel.ForEach(EasySpawnerPlugin.prefabNames, name =>
            {
                bool isSearched = name.IndexOf(SearchField.text, StringComparison.OrdinalIgnoreCase) >= 0;
                string localizedName = PrefabStates[name].localizedName;
                isSearched = isSearched || localizedName.Length > 0 && localizedName.IndexOf(SearchField.text, StringComparison.OrdinalIgnoreCase) >= 0;
                PrefabStates[name].isSearched = isSearched;
            });

            // Add favourite items to the list
            foreach (string name in EasySpawnerPlugin.prefabNames)
            {
                PrefabState prefabState = PrefabStates[name];
                if (prefabState.isSearched && prefabState.isFavourite)
                    SearchItems.Add(name);
            }

            // Add non favourite items to the list
            if (!FavouritesOnlyToggle.isOn)
            {
                foreach (string name in EasySpawnerPlugin.prefabNames)
                {
                    PrefabState prefabState = PrefabStates[name];
                    if (prefabState.isSearched && !prefabState.isFavourite)
                        SearchItems.Add(name);
                }
            }

            PoolAllPrefabItems();
            UpdateItemPrefabPool(new Vector2(PrefabScrollView.horizontalScrollbar.value, PrefabScrollView.verticalScrollbar.value));
        }

        public void Destroy()
        {
            PrefabScrollView.onValueChanged.RemoveAllListeners();
            SearchField.onValueChanged.RemoveAllListeners();
            SpawnButton.onClick.RemoveAllListeners();
            PrefabItems = null;
            PrefabItemPool = new Queue<PrefabItem>();
            UIWidth.SettingChanged -= UpdateMenuSize;
        }
    }
}
