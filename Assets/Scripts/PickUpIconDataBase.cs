using System.Collections.Generic;
using UnityEngine;

public class PickupIconDatabase : MonoBehaviour
{
    public static PickupIconDatabase Instance { get; private set; }

    [System.Serializable]
    public class PickupIcon
    {
        public ItemType itemType;
        public Sprite sprite;
    }

    [Header("HUD Pickup Icons")]
    [SerializeField]
    private List<PickupIcon> pickupIcons = new List<PickupIcon>();

    private Dictionary<ItemType, Sprite> iconLookup;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        iconLookup = new Dictionary<ItemType, Sprite>();

        foreach (PickupIcon icon in pickupIcons)
        {
            if (!iconLookup.ContainsKey(icon.itemType))
            {
                iconLookup.Add(icon.itemType, icon.sprite);
            }
            else
            {
                Debug.LogWarning($"Duplicate icon for {icon.itemType}");
            }
        }
    }

    public Sprite GetIcon(ItemType itemType)
    {
        if (iconLookup.TryGetValue(itemType, out Sprite sprite))
        {
            return sprite;
        }

        Debug.LogWarning($"No HUD icon assigned for {itemType}");
        return null;
    }
}