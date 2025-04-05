using ImageMagick.Colors;
using System.Text.Json.Serialization;
using UndertaleModLib.Models;

namespace NubbyExtractor.Definitions
{
    public class NubbySupervisor(int id, NubbyText name, NubbyText descriptionText, UndertaleSprite? sprite, int cost, ColorRGB colorOne, ColorRGB colorTwo)
    {
        public int ID { get { return id; } }
        public NubbyText Name { get { return name; } }
        public NubbyText Description { get { return descriptionText; } }

        public int Cost { get { return cost; } }

        public ColorRGB ColorOne { get { return colorOne; } }
        public ColorRGB ColorTwo { get { return colorTwo; } }
        
        [JsonIgnore]
        public UndertaleSprite? Sprite { get { return sprite; } }
        public string? SpriteName { get { return sprite?.Name.Content; } }
    }
}