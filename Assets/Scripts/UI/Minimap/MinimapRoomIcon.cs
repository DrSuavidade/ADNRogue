using UnityEngine;
using UnityEngine.UI;
using Geneforge.Gameplay.Map;

namespace Geneforge.UI.Minimap
{
    public class MinimapRoomIcon : MonoBehaviour
    {
        [Header("Visual Elements")]
        [SerializeField] private Image background;
        [SerializeField] private Image roomTypeIcon;

        [Header("Settings")]
        [SerializeField] private Color colorUnvisited = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        [SerializeField] private Color colorVisited = new Color(0.4f, 0.4f, 0.4f, 0.8f);
        [SerializeField] private Color colorActive = new Color(1f, 1f, 1f, 1f);

        private RoomInstance room;
        public RoomInstance Room => room;
        private bool isVisited;
        private bool isActive;

        public RectTransform rectTransform => GetComponent<RectTransform>();

        public void Initialize(RoomInstance roomInstance)
        {
            room = roomInstance;
            UpdateVisuals();
        }

        public void SetState(bool visited, bool active)
        {
            isVisited = visited;
            isActive = active;
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            if (room == null)
            {
                Debug.LogWarning("[MinimapRoomIcon] UpdateVisuals: room is NULL!");
                return;
            }

            Debug.Log($"[MinimapRoomIcon] UpdateVisuals for {room.name}");

            // Update background color based on state
            if (background != null)
            {
                if (isActive) background.color = colorActive;
                else if (isVisited) background.color = colorVisited;
                else background.color = colorUnvisited;

                Debug.Log($"[MinimapRoomIcon] Color set to: {background.color} (Active:{isActive}, Visited:{isVisited})");

                // USAR A SPRITE DA PLANTA DA SALA (em vez de ícone de tipo)
                if (room.MinimapIcon != null)
                {
                    background.sprite = room.MinimapIcon;
                    Debug.Log($"[MinimapRoomIcon] Applied room sprite: {room.MinimapIcon.name}");
                }
                else if (MinimapUI.Instance != null)
                {
                    // Fallback: usar ícone de tipo se não houver planta
                    background.sprite = MinimapUI.Instance.GetIconForType(room.RoomType);
                    Debug.LogWarning($"[MinimapRoomIcon] Room {room.name} has NO MinimapIcon! Using fallback type icon.");
                }
                else
                {
                    Debug.LogError($"[MinimapRoomIcon] Room {room.name} has NO sprite and MinimapUI.Instance is NULL!");
                }
            }
            else
            {
                Debug.LogError("[MinimapRoomIcon] background Image is NULL!");
            }

            // Se ainda usares o Type_Icon separado, esconde-o
            if (roomTypeIcon != null)
            {
                roomTypeIcon.gameObject.SetActive(false);
            }
        }
    }
}
