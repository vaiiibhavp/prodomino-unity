using DominoTemplate.DragAndDrop;
using ProDomino.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using Timba.Patterns;
using UnityEngine;
using UnityEngine.UI;
using static HelperSharedLibrary.Enums;

namespace ProDomino.GameModes
{
    public class ConcentrateBoard : MonoBehaviour
    {
        [SerializeField] ExtendedDeckController extendedDeckController;
        [SerializeField] private Transform boneyardTileContainer;
        public Transform BoneyardTileContainer => boneyardTileContainer;
        [SerializeField] List<Transform> tileContainers = new List<Transform>();
        public List<Transform> TileContainers => tileContainers;

        /*private int totalTilesInBoneyard;
        public int TotalTilesInBoneyard
        {
            get => totalTilesInBoneyard;
            set => totalTilesInBoneyard = value;
        }*/

        private RectTransform rectTransform;
        private DictionaryService dictionaryService;
        private CanvasGroup canvasGroup;

        internal Image[] TileImages => tileContainers?.Select(x => x?.GetComponent<Image>())?.ToArray();

        void Awake()
        {
            rectTransform = this.GetComponent<RectTransform>();
            canvasGroup = this.GetComponent<CanvasGroup>();

            dictionaryService = ServiceLocator.Instance.GetService<DictionaryService>();
        }

        public void RemoveSlotFromBoneyard(Transform slot)
        {
            if (tileContainers.Contains(slot))
            {
                tileContainers.Remove(slot);
            }
        }

        public void InitializeSlotsFromConcentrate()
        {
            tileContainers.Clear();

            for (int i = 0; i < boneyardTileContainer.childCount; i++)
            {
                Transform aux = boneyardTileContainer.GetChild(i);
                tileContainers.Add(aux);

                aux.GetComponent<CustomButtonUI>().SetButtonInteractable(false, auxAlpha: 0);
                //aux.GetComponent<Image>().enabled = showSlots;
                //aux.GetChild(0).gameObject.SetActive(showSlots);

                //boneyardTileContainer.GetChild(i).gameObject.SetActive(showSlots); //GetChild
            }
        }

        public void EnableAllConcentrateSlots()
        {
            for (int i = 0; i < tileContainers.Count; i++)
            {
                tileContainers[i].GetComponent<CustomButtonUI>().SetButtonInteractable(true);
            }
        }

        public void DisableAllConcentrateSlots()
        {
            for (int i = 0; i < tileContainers.Count; i++)
            {
                tileContainers[i].GetComponent<CustomButtonUI>().SetButtonInteractable(false, auxAlpha: 1);
            }
        }

        public void Select_BoneyardTileForPlayer(Transform container)
        {
            SelectBoneyardTile(container);
        }

        public void Select_BoneyardTileForPlayer_FromHost(string playerID, int indexSelected, int tileID, Action callback)
        {
            canvasGroup.interactable = false;
            EnableContainerButtons(false);

            Debug.Log(">++ Index: " + indexSelected + " Tile ID: " + tileID);

            //Transform container = tileContainers[indexSelected];
            Transform container = boneyardTileContainer.GetChild(indexSelected);

            if (container.childCount == 2)
            {
                Debug.Log(">++ Enter A");
                RemoveSlotFromBoneyard(container);

                DragHandler dragHandler = container.GetChild(1).GetComponent<DragHandler>();

                dragHandler.gameObject.SetActive(true);

                container.GetComponent<CustomButtonUI>().SetButtonInteractable(false);
                container.GetComponent<Image>().enabled = false;
                container.GetChild(0).gameObject.SetActive(false);

                dragHandler.transform.SetParent(this.transform);

                extendedDeckController.TakeSpecificIndexTileFromBoneyard_FromHost(playerID, tileID, dragHandler, callback); //CONTINUE
            }
            else
            {
                Debug.Log(">++ Enter B");
            }
        }

        public int Select_BoneyardTileForPlayerAndReturnIndex(Transform container)
        {
            return container.GetSiblingIndex();
            //SelectBoneyardTile(container);
        }

        public bool SelectedRandomBoneyardTile()
        {
            if (tileContainers.Count > 0)
            {
                Transform randomContainer = tileContainers[UnityEngine.Random.Range(0, tileContainers.Count)];

                Debug.Log("Tile taken from si hay 01");

                return SelectBoneyardTile(randomContainer);
            }
            else
            {
                Debug.Log("The tileContainers list is empty.");
            }

            return false;
        }

        private bool SelectBoneyardTile(Transform container)
        {
            canvasGroup.interactable = false;
            EnableContainerButtons(false);

            Debug.Log("Tile taken from si hay 02");

            if (container.childCount >= 2) // Added the outline object
            {
                Debug.Log("Tile taken from si hay 03");

                RemoveSlotFromBoneyard(container);
                //tileContainers.Remove(container);

                DragHandler dragHandler = container.GetComponentInChildren<DragHandler>(true);

                dragHandler.gameObject.SetActive(true);

                container.GetComponent<CustomButtonUI>().SetButtonInteractable(false);
                container.GetComponent<Image>().enabled = false;
                container.GetChild(0).gameObject.SetActive(false);

                dragHandler.transform.SetParent(this.transform);
                return extendedDeckController.TakeSpecificIndexTileFromBoneyard(dragHandler);
            }

            return false;
        }

        public void ShowBoneyard(bool enableTilesButtons = true)
        {
            rectTransform.transform.localPosition = new Vector3(0, 0, 0);
            canvasGroup.interactable = true;
            EnableContainerButtons(enableTilesButtons);
        }

        public void HideBoneyard()
        {
            canvasGroup.interactable = false;
            EnableContainerButtons(false);
            rectTransform.transform.localPosition = new Vector3(0, -1100, 0);
        }

        private void EnableContainerButtons(bool isEnable)
        {
            foreach (Transform aux in tileContainers)
            {
                aux.GetComponent<CustomButtonUI>().SetButtonInteractableWithAlphaFull(isEnable);
            }
        }

        internal void OverrideTilesSkin(string skinID)
        {
            var tileImages = TileImages;
            if (tileImages is null or { Length: 0 })
            {
                Debug.LogWarning("No tile images found in the Boneyard.");
                return;
            }

            var backTile = dictionaryService.GetSprite(CosmeticType.Tiles.ToString(), $"{skinID}_{Consts.CollectionKeys.Back}");
            if (backTile is null)
            {
                Debug.LogWarning($"Back tile sprite for skin ID '{skinID}' not found.");
                return;
            }

            foreach (var tile in tileImages)
            {
                if (tile)
                    tile.sprite = backTile;
                else
                    Debug.LogWarning("Tile image is null in the Boneyard.");
            }
        }
    }
}
