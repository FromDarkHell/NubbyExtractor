

using ImageMagick;
using NubbyExtractor;
using ShellProgressBar;
using System.Text.Json;

internal class Program
{
    private static void Main(string[] args)
    {
        var nubbyPath = new DirectoryInfo(args[0]);

        NubbyParser parser = new NubbyParser(nubbyPath);

        var nubbyItems = parser.getNubbyItems();
        var nubbyPerks = parser.getNubbyPerks();
        Console.WriteLine($"Loaded {parser.getTranslationData().Count} translation data...");
        
        Console.WriteLine($"Loaded {nubbyItems.Count} items...");
        Console.WriteLine($"Loaded {nubbyPerks.Count} perks...");

        string itemString = JsonSerializer.Serialize(
            nubbyItems,
            new JsonSerializerOptions {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
                Converters = {
                    new NubbyTextConverter(parser.getTranslationData()),
                    new ImageMagicColorConverter()
                }
            }
        );

        string perkString = JsonSerializer.Serialize(
            nubbyPerks,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
                Converters = { 
                    new NubbyTextConverter(parser.getTranslationData()),
                    new ImageMagicColorConverter()
                }
            }
        );

        var exportDirectory = new DirectoryInfo(Path.Join(nubbyPath.FullName, "exports"));
        if (!exportDirectory.Exists) exportDirectory.Create();

        var itemFile = Path.Join(exportDirectory.FullName, "items.json");
        File.WriteAllText(itemFile, itemString);

        var perkFile = Path.Join(exportDirectory.FullName, "perks.json");
        File.WriteAllText(perkFile, perkString);

        // Time to write out all the sprites
        var spriteDirectory = new DirectoryInfo(Path.Join(exportDirectory.FullName, "sprites"));
        if (!spriteDirectory.Exists) spriteDirectory.Create();

        using (var pbar = new ProgressBar(nubbyPerks.Count, "Exporting perk sprites"))
        {
            foreach (var nubbyPerk in nubbyPerks)
            {
                pbar.Tick($"Exporting {nubbyPerk.ObjectName}");

                var gameObject = nubbyPerk.GameObject;
                var sprite = gameObject?.Sprite;
                if (sprite == null) continue;

                var spritePath = Path.Combine(spriteDirectory.FullName, $"{sprite.Name.Content}.gif");

                var spriteFrames = NubbyUtil.loadFramesFromSprite(sprite);
                using MagickImageCollection collection = new MagickImageCollection(spriteFrames);
                collection.Coalesce();
                collection.Write(new FileInfo(spritePath));
            }
        }

        using (var pbar = new ProgressBar(nubbyItems.Count, "Exporting item sprites"))
        {
            foreach (var nubbyItem in nubbyItems)
            {
                pbar.Tick($"Exporting {nubbyItem.ObjectName}");

                var gameObject = nubbyItem.gameObject;
                var sprite = gameObject?.Sprite;
                if (sprite == null) continue;

                var spritePath = Path.Combine(spriteDirectory.FullName, $"{sprite.Name.Content}.gif");

                var spriteFrames = NubbyUtil.loadFramesFromSprite(sprite);
                using MagickImageCollection collection = new MagickImageCollection(spriteFrames);
                collection.Coalesce();
                collection.Write(new FileInfo(spritePath));
            }
        }    
    }
}