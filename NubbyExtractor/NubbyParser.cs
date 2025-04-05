using UndertaleModLib;
using CsvHelper;
using System.Globalization;
using CsvHelper.Configuration.Attributes;
using CsvHelper.Configuration;
using UndertaleModLib.Decompiler;
using Underanalyzer.Decompiler;
using Underanalyzer.Decompiler.AST;
using static NubbyExtractor.Definitions.NubbyItem;
using static NubbyExtractor.Definitions.NubbyPerk;
using static NubbyExtractor.Definitions.NubbySupervisor;

using System.Diagnostics;
using UndertaleModLib.Models;
using ImageMagick.Colors;
using NubbyExtractor.Definitions;

namespace NubbyExtractor
{
    public class NubbyParser
    {
        private class NubbyTranslation
        {
            public static CsvConfiguration csvConfiguration = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = false,
                BadDataFound = null
            };

            [Index(0)]
            public required string Key { get; set; }

            [Index(1)]
            public required string Value { get; set; }
        }

        private UndertaleData gmData;
        private GlobalDecompileContext decompileContext;

        private Dictionary<string, string> translationMapping;
        private List<NubbyItem>? items;
        private List<NubbyPerk>? perks;
        private List<NubbySupervisor>? supervisors;

        public UndertaleData getData() => gmData;
        public Dictionary<string, string> getTranslationData() => translationMapping;
        public List<NubbyItem> getNubbyItems() => items!;
        public List<NubbyPerk> getNubbyPerks() => perks!;
        public List<NubbySupervisor> getNubbySupervisors() => supervisors!;

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
                foreach (var record in records)
                {
                    if (!translationMapping.ContainsKey(record.Key)) translationMapping.Add(record.Key, record.Value);
                }
            }

            initializeItems();
            initializePerks();
            initializeSupervisors();
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

                int perkItemPool = (int)NubbyUtil.parseVariableNode(arguments[6]);

                int perkFxColor = (int)NubbyUtil.parseVariableNode(arguments[7]);

                ColorRGB perkFXRGB = NubbyUtil.integerToRGB(perkFxColor);

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
                    (NubbyPerkPool)perkItemPool,
                    perkFXRGB,
                    (NubbyPerkAltDescription)altPerkDescVal,
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

                Debug.Assert(
                    (int)NubbyUtil.parseVariableNode(itemInitExtCall.Arguments[0]) == itemID
                );

                Dictionary<NubbyLevelWeighting, int> levelWeighting = new Dictionary<NubbyLevelWeighting, int>()
                {
                    { NubbyLevelWeighting.EARLY, (int)NubbyUtil.parseVariableNode(itemInitExtCall.Arguments[1]) },
                    { NubbyLevelWeighting.MID, (int)NubbyUtil.parseVariableNode(itemInitExtCall.Arguments[2]) },
                    { NubbyLevelWeighting.LATE, (int)NubbyUtil.parseVariableNode(itemInitExtCall.Arguments[3]) }
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

        private void initializeSupervisors()
        {
            supervisors = [];

            var supervisorManagementCall = gmData.Code.ByName("gml_Object_obj_SupervisorMGMT_Create_0");
            var supervisorManagementCode = new DecompileContext(decompileContext, supervisorManagementCall, gmData.ToolInfo.DecompilerSettings).DecompileToAST() as BlockNode;

            var assignmentCalls = supervisorManagementCode?.Children.Where((x) => x is AssignNode).Cast<AssignNode>() ?? [];

            Dictionary<int, Dictionary<string, dynamic?>> supervisorMapping = [];

            foreach (AssignNode assignmentCall in assignmentCalls)
            {
                var assignedVariable = assignmentCall.Variable;
                if(assignedVariable is VariableNode)
                {
                    var variableNode = (VariableNode)assignedVariable;
                    if(variableNode.ConditionalValue == "SuperVisorName" || variableNode.ConditionalValue == "SuperVisorDesc" || variableNode.ConditionalValue == "SVSprite" || variableNode.ConditionalValue == "SuperVisorCol1" || variableNode.ConditionalValue == "SuperVisorCol2" || variableNode.ConditionalValue == "SVCost")
                    {
                        var supervisorIndex = (int)NubbyUtil.parseVariableNode(variableNode.ArrayIndices![0]);
                        if (!supervisorMapping.ContainsKey(supervisorIndex)) supervisorMapping[supervisorIndex] = [];

                        supervisorMapping[supervisorIndex][variableNode.ConditionalValue] = NubbyUtil.parseVariableNode(
                            assignmentCall.Value!, 
                            gameData: gmData
                        );
                    }
                }
            }

            supervisors = [.. supervisorMapping.Select((x) => new NubbySupervisor(
                    id: x.Key,
                    name: new NubbyText(x.Value["SuperVisorName"]),
                    descriptionText: new NubbyText(x.Value["SuperVisorDesc"]),
                    sprite: x.Value["SVSprite"],
                    cost: x.Value["SVCost"],
                    colorOne: NubbyUtil.integerToRGB(x.Value["SuperVisorCol1"]),
                    colorTwo: NubbyUtil.integerToRGB(x.Value["SuperVisorCol2"])
                ))];
        }
    }
}
