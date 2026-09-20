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
    public class BoneyardManager : MonoBehaviour
    {
        [SerializeField] ExtendedDeckController extendedDeckController;
        [SerializeField] private Transform boneyardTileContainer;
        public Transform BoneyardTileContainer => boneyardTileContainer;
        [SerializeField] List<Transform> tileContainers = new List<Transform>();

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

        public void InitializeSlotsFromBoneyard()
        {
            tileContainers.Clear();

            for (int i = 0; i < boneyardTileContainer.childCount; i++)
            {
                Transform aux = boneyardTileContainer.GetChild(i);
                tileContainers.Add(aux);

                aux.GetComponent<CustomButtonUI>().SetButtonInteractable(true);
                aux.GetComponent<Image>().enabled = true;
                aux.GetChild(0).gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// Helper that make sure the boneyard doesn't use invalid references
        /// </summary>
        private void BoneyardContainerFixer()
        {
            var invalidContainers = tileContainers?.Where(x => !x.GetComponentInChildren<DragHandler>(true))?.ToArray();

            if (invalidContainers is not null and { Length: > 0 })
                Debug.LogError("Boneyard is using invalid containers as possible targets");

            // Force ignore container that could stop the game.
            // This is a temporarily solution
            if (invalidContainers.Length > 0)
                tileContainers = tileContainers.Except(invalidContainers).ToList();
        }

        public void Select_BoneyardTileForPlayer(Transform container, bool instantMove = false)
        {
            SelectBoneyardTile(container, instantMove);
        }

        public int GetBoneyardTilesCount()
        {
            BoneyardContainerFixer();
            return tileContainers.Count;
        }

        public int? GetRandomBoneyardTileIndex()
        {
            if (tileContainers is null)
            {
                Debug.LogWarning("tileContainers is null.");
                return null;
            }

            BoneyardContainerFixer();
            if (tileContainers.Count > 0)
            {
                Transform randomContainer = tileContainers[UnityEngine.Random.Range(0, tileContainers.Count)];
                return randomContainer.GetSiblingIndex();
            } 
            else
                Debug.Log("The tileContainers list is empty.");

            return null;
        }

        public void Select_BoneyardTileForPlayer_FromHost(string playerID, int indexSelected, int tileID, Action callback)
        {
            canvasGroup.interactable = false;
            EnableContainerButtons(false);

            Debug.Log(">++ Index: " + indexSelected + " Tile ID: " + tileID);

            //Transform container = tileContainers[indexSelected];
            Transform container = default(Transform);

            if (boneyardTileContainer.childCount > indexSelected)
                container = boneyardTileContainer.GetChild(indexSelected);
            else
                Debug.LogWarning($"Trying to get child of index {indexSelected}, but boneyard only has {BoneyardTileContainer.childCount} children");

            // If container is valid and has enough children
            if (container != null && container.childCount >= 3)
            {
                Debug.Log(">++ Enter A");
                RemoveSlotFromBoneyard(container);

                DragHandler dragHandler = container.GetComponentInChildren<DragHandler>(true);
                if (dragHandler == null)
                {
                    Debug.LogError($"Host:: DragHandler component not found in the container with name <b>{container.name}</b> ({container.GetInstanceID()}).");
                    return;
                }

                dragHandler.gameObject.SetActive(true);

                container.GetComponent<CustomButtonUI>().SetButtonInteractable(false);
                container.GetComponent<Image>().enabled = false;
                container.GetChild(0).gameObject.SetActive(false);

                dragHandler.transform.SetParent(this.transform);

                extendedDeckController.ControlAllHands();
                extendedDeckController.TakeSpecificIndexTileFromBoneyard_FromHost(playerID, tileID, dragHandler, callback); //CONTINUE
            }

            // If container is null or doesn't have enough children
            else
                Debug.LogError($"Select_BoneyardTileForPlayer_FromHost: container has value?: {container != null}, child count: {(container != null ? container.childCount.ToString() : "N/A")}");
        }

        private bool SelectBoneyardTile(Transform container, bool instantMove = false)
        {
            canvasGroup.interactable = false;
            EnableContainerButtons(false);

            Debug.Log("Tile taken from si hay 02");

            if (container.childCount >= 3) // Added the outline object
            {
                Debug.Log("Tile taken from si hay 03");

                RemoveSlotFromBoneyard(container);
                //tileContainers.Remove(container);

                DragHandler dragHandler = container.GetComponentInChildren<DragHandler>(true);
                if (dragHandler == null)
                {
                    Debug.LogError($"Local:: DragHandler component not found in the container with name <b>{container.name}</b> ({container.GetInstanceID()}).");
                    return false;
                }

                dragHandler.gameObject.SetActive(true);

                container.GetComponent<CustomButtonUI>().SetButtonInteractable(false);
                container.GetComponent<Image>().enabled = false;
                container.GetChild(0).gameObject.SetActive(false);

                dragHandler.transform.SetParent(this.transform);
                return extendedDeckController.TakeSpecificIndexTileFromBoneyard(dragHandler, instantMove);
            }

            return false;
        }

        public int Select_BoneyardTileForPlayerAndReturnIndex(Transform container)
        {
            if (container.TryGetComponent<CustomButtonUI>(out var customButtonUI))
                customButtonUI.SetButtonInteractable(false);
            else
                Debug.LogError("CustomButtonUI component not found on the container.");

            if (container.TryGetComponent<Image>(out var image))
                image.enabled = false;
            else
                Debug.LogError("Image component not found on the container.");

            if (container.childCount > 0)
                container.GetChild(0).gameObject.SetActive(false);
            else
                Debug.LogError("Container has no children to disable.");

            return container.GetSiblingIndex();
            //SelectBoneyardTile(container);
        }

        public Transform GetRandomBoneyardTile()
        {
            BoneyardContainerFixer();
            if (tileContainers.Count > 0)
            {
                return tileContainers[UnityEngine.Random.Range(0, tileContainers.Count)];
            }
            else
            {
                Debug.Log("The tileContainers list is empty.");
            }

            return null;
        }
        
        public bool SelectedRandomBoneyardTile()
        {
            BoneyardContainerFixer();
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


        public void ShowBoneyard(bool enableTilesButtons = true, bool enableInteractivity = true)
        {
            rectTransform.transform.localPosition = new Vector3(0, 0, 0);
            canvasGroup.interactable = enableInteractivity;
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
            BoneyardContainerFixer();
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
