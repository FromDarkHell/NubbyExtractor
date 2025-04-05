using System.Text.Json.Serialization;
using ImageMagick.Colors;
using UndertaleModLib.Models;
using static NubbyExtractor.Definitions.NubbyPerk;

namespace NubbyExtractor.Definitions
{
    public class NubbyPerk(int id, NubbyText perkName, UndertaleGameObject? gameObject, string triggerID, NubbyPerkTier perkTier, NubbyPerkType perkType, NubbyPerkPool perkPool, ColorRGB perkEffectColor, NubbyPerkAltDescription altPerkDescVal, NubbyText perkDescription)
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

        public enum NubbyPerkPool
        {
            Unobtainable = 0,
            Capsule = 1,
        }

        public enum NubbyPerkAltDescription
        {
            None = 0,
            Disable = 1,
        }

        // See: gml_Object_obj_PerkMGMT_Draw_64
        // *maybe* eventually automate this, but meh
        private Dictionary<NubbyPerkAltDescription, string[]> altDescriptionTextIDs = new() {
            { NubbyPerkAltDescription.Disable, ["altdesc_disable", "\n"] }
        };

        public int ID { get { return id; } }
        public NubbyText PerkName { get { return perkName; } }

        public string TriggerID { get { return triggerID; } }
        public NubbyText TriggerText { get { return new NubbyText(triggerID); } }

        public NubbyPerkTier PerkTier { get { return perkTier; } }
        public NubbyPerkType PerkType { get { return perkType; } }
        public NubbyPerkPool PerkPool { get { return perkPool; } }
        public ColorRGB PerkEffectColor { get { return perkEffectColor; } }

        public NubbyPerkAltDescription AltPerkDescVal { get { return altPerkDescVal; } }
        public NubbyText? AltPerkDescText
        {
            get
            {
                if (altPerkDescVal == NubbyPerkAltDescription.None) return null;

                return new NubbyText(altDescriptionTextIDs[altPerkDescVal]);
            }
        }

        public NubbyText PerkDescription { get { return perkDescription; } }

        public string? ObjectName { get { return gameObject?.Name.Content; } }

        public string? SpriteName { get { return gameObject?.Sprite?.Name.Content; } }

        [JsonIgnore]
        public UndertaleGameObject? GameObject { get { return gameObject; } }
    }

}