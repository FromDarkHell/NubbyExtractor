using UndertaleModLib;
using CsvHelper;
using System.Globalization;
using CsvHelper.Configuration.Attributes;
using CsvHelper.Configuration;
using UndertaleModLib.Decompiler;
using Underanalyzer.Decompiler;
using Underanalyzer.Decompiler.AST;
using static NubbyExtractor.NubbyItem;
using static NubbyExtractor.NubbyPerk;

using System.Diagnostics;
using UndertaleModLib.Models;
using System.Text.Json.Serialization;
using ImageMagick.Colors;

namespace NubbyExtractor
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
                return (int) Math.Round(Price / 2.0);
            }
        }

        public int OffsetPrice { get { return offsetPrice; } }
        public int UpgradeID { get { return upgradeID; } }
        public string? MainTriggerID { get { return mainTriggerID; } }
        public string? AltTriggerID { get { return altTriggerID; } }

        public string? ObjectName { get { return gameObject?.Name.Content; } }

        public string? SpriteName { get { return gameObject?.Sprite?.Name.Content; }}

        public Dictionary<NubbyLevelWeighting, int>? LevelWeighting { get { return levelWeighting; } }

        [JsonIgnore]
        public UndertaleGameObject? gameObject = gameObject;
    }

    public class NubbyPerk(int id, NubbyText perkName, UndertaleGameObject? gameObject, string triggerID, NubbyPerkTier perkTier, NubbyPerkType perkType, bool inPerkPool, ColorRGB perkEffectColor, int altPerkDescVal, NubbyText perkDescription)
    {
        public enum NubbyPerkTier
        {
            Unused = -1,
            Common = 0,
            Rare = 1,
            UltraRare = 2,
        }
        
        public enum NubbyPerkType
        {
            _UNK1 = 0, 
            _UNK2 = 1,
        }

        public int ID { get { return id; } }
        public NubbyText PerkName { get { return perkName; } }
        
        public string TriggerID { get { return triggerID; } }
        public NubbyText TriggerText { get { return new NubbyText(triggerID); } }

        public NubbyPerkTier PerkTier { get { return perkTier; } }
        public NubbyPerkType PerkType { get { return perkType; } }
        public bool InPerkPool { get { return inPerkPool; } }
        public ColorRGB PerkEffectColor { get { return perkEffectColor; } }
        public int AltPerkDescVal { get { return altPerkDescVal; } }
        public NubbyText PerkDescription { get { return perkDescription; } }

        public string? ObjectName { get { return gameObject?.Name.Content; } }

        public string? SpriteName { get { return gameObject?.Sprite?.Name.Content; } }

        [JsonIgnore]
        public UndertaleGameObject? GameObject { get { return gameObject; } }
    }

    public class NubbyParser
    {
        private class NubbyTranslation
        {
            public static CsvConfiguration csvConfiguration = new CsvConfiguration(CultureInfo.InvariantCulture) { 
                HasHeaderRecord = false, 
                BadDataFound = null 
            };

            [Index(0)]
            public required string Key { get; set; }

            [Index(1)]
            public required string Value { get; set; }
        }

        private UndertaleData gmData;
        private Dictionary<string, string> translationMapping;

        private List<NubbyItem>? items;
        private List<NubbyPerk>? perks;

        private GlobalDecompileContext decompileContext;

        public UndertaleData getData() => gmData;
        public Dictionary<string, string> getTranslationData() => translationMapping;
        public List<NubbyItem> getNubbyItems() => items!;
        public List<NubbyPerk> getNubbyPerks() => perks!;

        public NubbyParser(DirectoryInfo installationDirectory)
        {
            var files = installationDirectory.GetFiles();
            var dataFile = files.First((x) => x.Name == "data.win" || x.Name == "data.ios" || x.Name == "data.droid" || x.Name == "data.unx");

            using (FileStream fs = dataFile.OpenRead())
            {
                gmData = UndertaleIO.Read(fs);
                decompileContext = new GlobalDecompileContext(gmData);
            }

            var translationFile = files.First((x) => x.Name == "NNF_Full_LocalizationEN.csv");

            translationMapping = [];

            using (var translationReader = new StreamReader(translationFile.OpenRead()))
            using (var csv = new CsvReader(translationReader, NubbyTranslation.csvConfiguration))
            {
                var records = csv.GetRecords<NubbyTranslation>();
                foreach(var record in records)
                {
                    if(!translationMapping.ContainsKey(record.Key)) translationMapping.Add(record.Key, record.Value);
                }
            }

            initializeItems();
            initializePerks();
        }
    
        private void initializePerks()
        {
            perks = [];

            var perkManagementCall = gmData.Code.ByName("gml_Object_obj_PerkMGMT_Create_0");
            var perkManagementCode = new DecompileContext(decompileContext, perkManagementCall, gmData.ToolInfo.DecompilerSettings).DecompileToAST() as BlockNode;

            var functionCalls = perkManagementCode?.Children.Where((x) => x is FunctionCallNode).Cast<FunctionCallNode>() ?? [];

            var perkInitCalls = functionCalls.Where((x) => x.FunctionName == "gml_Script_scr_Init_Perk");
            foreach (FunctionCallNode perkInitCall in perkInitCalls)
            {
                var arguments = perkInitCall.Arguments;

                var perkID = (int)NubbyUtil.parseVariableNode(arguments[0]);

                var perkNameCall = (FunctionCallNode)arguments[1];
                var perkNameText = new NubbyText(perkNameCall);

                UndertaleGameObject? gameObject = null;
                if (arguments[2] is AssetReferenceNode)
                {
                    var assetReferenceNode = (AssetReferenceNode)arguments[2];
                    Debug.Assert(assetReferenceNode.AssetType == Underanalyzer.AssetType.Object);
                    gameObject = gmData.GameObjects[assetReferenceNode.AssetId];
                }

                string? perkTrigger = NubbyUtil.parseVariableNode(arguments[3]) as string;

                int perkTier = (int)NubbyUtil.parseVariableNode(arguments[4]);
                int perkType = (int)NubbyUtil.parseVariableNode(arguments[5]);

                short inPerkItemPoolVal = (short)NubbyUtil.parseVariableNode(arguments[6]);

                bool inPerkItemPool = false;
                if (inPerkItemPoolVal <= 0) inPerkItemPool = false;
                else inPerkItemPool = true;

                int perkFxColor = (int)NubbyUtil.parseVariableNode(arguments[7]);

                byte red = Convert.ToByte((perkFxColor >> 16) & 0xFF);
                byte green = Convert.ToByte((perkFxColor >> 8) & 0xFF);
                byte blue = Convert.ToByte((perkFxColor) & 0xFF);

                ColorRGB perkFXRGB = new ColorRGB(red, green, blue);

                int altPerkDescVal = (int)NubbyUtil.parseVariableNode(arguments[8]);

                var perkDescCall = (FunctionCallNode)arguments[9];
                var perkDescText = new NubbyText(perkDescCall);

                NubbyPerk nubbyPerk = new NubbyPerk(
                    perkID,
                    perkNameText,
                    gameObject,

                    perkTrigger!,
                    (NubbyPerkTier)perkTier,
                    (NubbyPerkType)perkType,
                    inPerkItemPool,
                    perkFXRGB,
                    altPerkDescVal,
                    perkDescText
                );

                perks.Add(nubbyPerk);
            }
        }

        private void initializeItems()
        {
            // Now it's time to start parsing the code loaded from the UndertaleData
            items = [];

            var itemManagementCall = gmData.Code.ByName("gml_Object_obj_ItemMGMT_Create_0");
            var itemManagementCode = new DecompileContext(decompileContext, itemManagementCall, gmData.ToolInfo.DecompilerSettings).DecompileToAST() as BlockNode;

            // TODO: Parse the variables out especially for PriceCOMN and PriceRARE

            var functionCalls = itemManagementCode?.Children.Where((x) => x is FunctionCallNode).Cast<FunctionCallNode>() ?? [];

            Dictionary<string, dynamic> itemPropertyList = new Dictionary<string, dynamic>()
            {
                { "ItemType", new List<int>() },
                { "ItemTier", new List<int>() },
                { "GeneralEffect", new List<string>() },
                { "OffsetPrice", new List<int>() },
                { "MutantTrig", new List<string>() },

                { "PriceRARE", basePrices[NubbyItemTier.RARE] },
                { "PriceCOMN", basePrices[NubbyItemTier.COMMON] },
            };

            var itemInitCalls = functionCalls.Where((x) => x.FunctionName == "gml_Script_scr_Init_Item").ToList();
            var itemInitExtCalls = functionCalls.Where((x) => x.FunctionName == "gml_Script_scr_Init_ItemExt").ToList();

            for (int i = 0; i < itemInitCalls.Count(); i++)
            {
                FunctionCallNode itemInitCall = itemInitCalls[i];
                var arguments = itemInitCall.Arguments;

                var itemID = (int)NubbyUtil.parseVariableNode(arguments[0], itemPropertyList);

                var itemNameCall = (FunctionCallNode)arguments[1];
                var itemNameText = new NubbyText(itemNameCall);

                var assetReferenceNode = (AssetReferenceNode)arguments[2];
                Debug.Assert(assetReferenceNode.AssetType == Underanalyzer.AssetType.Object);
                UndertaleGameObject gameObject = gmData.GameObjects[assetReferenceNode.AssetId];


                var itemLevel = (int)NubbyUtil.parseVariableNode(arguments[3], itemPropertyList);
                var itemType = (int)NubbyUtil.parseVariableNode(arguments[4], itemPropertyList);
                var itemTier = (int)NubbyUtil.parseVariableNode(arguments[5], itemPropertyList);
                var _UNK0001 = (int)NubbyUtil.parseVariableNode(arguments[6], itemPropertyList);

                var generalEffect = (string?)NubbyUtil.parseVariableNode(arguments[7], itemPropertyList);
                var itemPool = (int)NubbyUtil.parseVariableNode(arguments[8], itemPropertyList);

                var offsetPrice = (int)NubbyUtil.parseVariableNode(arguments[9], itemPropertyList);

                var upgradeID = (int)NubbyUtil.parseVariableNode(arguments[10], itemPropertyList);

                var itemTriggerID = (string?)NubbyUtil.parseVariableNode(arguments[11], itemPropertyList);
                var mutantTriggerID = (string?)NubbyUtil.parseVariableNode(arguments[12], itemPropertyList);

                var itemDescriptionNode = (FunctionCallNode)arguments[13];
                var itemDescriptionText = new NubbyText(itemDescriptionNode);

                FunctionCallNode itemInitExtCall = itemInitExtCalls[i];
                Dictionary<NubbyLevelWeighting, int> levelWeighting = new Dictionary<NubbyLevelWeighting, int>()
                {
                    { NubbyLevelWeighting.EARLY, (int)NubbyUtil.parseVariableNode(itemInitExtCall.Arguments[0]) },
                    { NubbyLevelWeighting.MID, (int)NubbyUtil.parseVariableNode(itemInitExtCall.Arguments[1]) },
                    { NubbyLevelWeighting.LATE, (int)NubbyUtil.parseVariableNode(itemInitExtCall.Arguments[2]) }
                };

                var nubbyItem = new NubbyItem(
                    itemID,
                    itemNameText,
                    itemDescriptionText,
                    itemLevel,
                    (NubbyItemType)itemType,
                    (NubbyItemTier)itemTier,
                    (NubbyItemPool)itemPool,
                    generalEffect!,
                    offsetPrice,
                    upgradeID,
                    itemTriggerID!,
                    mutantTriggerID!,

                    levelWeighting,

                    gameObject
                );

                (itemPropertyList["ItemType"] as List<int>)!.Add(itemType);
                (itemPropertyList["ItemTier"] as List<int>)!.Add(itemTier);
                (itemPropertyList["GeneralEffect"] as List<string>)!.Add(generalEffect!);
                (itemPropertyList["OffsetPrice"] as List<int>)!.Add(offsetPrice);
                (itemPropertyList["MutantTrig"] as List<string>)!.Add(mutantTriggerID!);

                items.Add(nubbyItem);
            }

        }
    }
}
