using System.Text.Json.Serialization;
using UndertaleModLib.Models;
using static NubbyExtractor.Definitions.NubbyItem;

namespace NubbyExtractor.Definitions
{
    public class NubbyItem(int id, NubbyText nameText, NubbyText descriptionText, int itemLevel, NubbyItemType itemType, NubbyItemTier itemRarity, NubbyItemPool itemPool, string? generalEffect, int offsetPrice, int upgradeID, string? mainTriggerID, string? altTriggerID, Dictionary<NubbyLevelWeighting, int>? levelWeighting, UndertaleGameObject? gameObject)
    {
        public static readonly Dictionary<NubbyItemTier, int> basePrices = new()
        {
            { NubbyItemTier.COMMON, 5 },

            { NubbyItemTier.RARE, 10 },
            { NubbyItemTier.ULTRA_RARE, 10 },
        };

        public enum NubbyItemType
        {
            Item = 0,
            UpgradedItem = 1,
            CorruptedItem = 2,
            UpgradedCorruptedItem = 3,
            Food = 4,
            UpgradedFood = 5
        };

        public enum NubbyItemTier
        {
            COMMON = 0,
            RARE = 1,
            ULTRA_RARE = 2,
        };

        public enum NubbyItemPool
        {
            Unobtainable = 0,
            Shop = 1,
            BlackMarket = 2,
            Cafe = 3,
        };

        public enum NubbyLevelWeighting
        {
            // Early-Game is any rounds <= 10
            EARLY = 0,

            // Mid-Game is > 10 and <=50
            MID = 1,

            // Late-Game is > 50
            LATE = 2,
        }

        public int Id { get { return id; } }
        public NubbyText NameText { get { return nameText; } }
        public NubbyText DescriptionText { get { return descriptionText; } }

        public int ItemLevel { get { return itemLevel; } }
        public NubbyItemType ItemType { get { return itemType; } }
        public NubbyItemTier ItemRarity { get { return itemRarity; } }
        public NubbyItemPool ItemPool { get { return itemPool; } }
        public string? GeneralEffect { get { return generalEffect; } }

        public int Price
        {
            get
            {
                return (basePrices[itemRarity] + offsetPrice);
            }
        }

        public int SellPrice
        {
            get
            {
                return (int)Math.Round(Price / 2.0);
            }
        }

        public int OffsetPrice { get { return offsetPrice; } }
        public int UpgradeID { get { return upgradeID; } }
        public string? MainTriggerID { get { return mainTriggerID; } }
        public NubbyText? MainTriggerText { get { return new NubbyText(mainTriggerID); } }
        public string? AltTriggerID { get { return altTriggerID; } }
        public NubbyText? AltTriggerText { get { return new NubbyText(altTriggerID); } }

        public string? ObjectName { get { return gameObject?.Name.Content; } }

        public string? SpriteName { get { return gameObject?.Sprite?.Name.Content; } }

        public Dictionary<NubbyLevelWeighting, int>? LevelWeighting { get { return levelWeighting; } }

        [JsonIgnore]
        public UndertaleGameObject? gameObject = gameObject;
    }

}