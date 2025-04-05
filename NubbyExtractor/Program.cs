

using CsvHelper;
using ImageMagick;
using NubbyExtractor;
using NubbyExtractor.Definitions;
using ShellProgressBar;
using System.Text.Json;
using System.Text.RegularExpressions;
using UndertaleModLib.Models;

internal class Program
{
    private static void exportDescriptionSprites(string description, IEnumerable<UndertaleSprite> allSprites, DirectoryInfo spriteDirectory)
    {
        var matches = Regex.Matches(description, @"\[([A-Za-z0-9_]*),[0-9]+\]");
        foreach (Match match in matches)
        {
            var spriteName = match.Groups[1].Value;

            var sprite = allSprites.First((x) => x.Name.Content == spriteName);

            exportSprite(sprite, spriteDirectory);
        }
    }

    private static void Main(string[] args)
    {
        var nubbyPath = new DirectoryInfo(args[0]);

        NubbyParser parser = new NubbyParser(nubbyPath);

        JsonSerializerOptions serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            Converters = {
                        new NubbyTextConverter(parser.getTranslationData()),
                        new ImageMagicColorConverter()
                }
        };

        var nubbyItems = parser.getNubbyItems();
        var nubbyPerks = parser.getNubbyPerks();
        var nubbySupervisors = parser.getNubbySupervisors();

        Console.WriteLine($"Loaded {parser.getTranslationData().Count} translation data...");

        Console.WriteLine($"Loaded {nubbyItems.Count} items...");
        Console.WriteLine($"Loaded {nubbyPerks.Count} perks...");
        Console.WriteLine($"Loaded {nubbySupervisors.Count} supervisors...");

        var exportDirectory = new DirectoryInfo(Path.Join(nubbyPath.FullName, "exports"));
        if (!exportDirectory.Exists) exportDirectory.Create();

        File.WriteAllText(
            Path.Join(exportDirectory.FullName, "items.json"),
            JsonSerializer.Serialize(
                nubbyItems,
                serializerOptions
            )
        );

        File.WriteAllText(
            Path.Join(exportDirectory.FullName, "perks.json"),
            JsonSerializer.Serialize(
                nubbyPerks,
                serializerOptions
            )
        );

        File.WriteAllText(
            Path.Join(exportDirectory.FullName, "supervisors.json"),
            JsonSerializer.Serialize(
                nubbySupervisors,
                serializerOptions
            )
        );

        // Time to write out all the sprites
        var spriteDirectory = new DirectoryInfo(Path.Join(exportDirectory.FullName, "sprites"));
        if (!spriteDirectory.Exists) spriteDirectory.Create();

        var allSprites = parser.getData().Sprites;

        using (var pbar = new ProgressBar(nubbySupervisors.Count, "Exporting supervisor sprites"))
        {
            foreach (var supervisor in nubbySupervisors)
            {
                pbar.Tick($"Checking {supervisor.ID}");

                if (supervisor.Sprite != null) exportSprite(supervisor.Sprite, spriteDirectory);
                
                var description = supervisor.Description.ToString(parser.getTranslationData());
                exportDescriptionSprites(description, allSprites, spriteDirectory);
            }
        }

        using (var pbar = new ProgressBar(nubbyItems.Count + nubbyPerks.Count, "Checking/exporting misc sprites"))
        {

            foreach (var nubbyItem in nubbyItems)
            {
                pbar.Tick($"Checking {nubbyItem.ObjectName}");
                var description = nubbyItem.DescriptionText.ToString(parser.getTranslationData());

                exportDescriptionSprites(description, allSprites, spriteDirectory);
            }

            foreach (var nubbyPerk in nubbyItems)
            {
                pbar.Tick($"Checking {nubbyPerk.ObjectName}");

                var description = nubbyPerk.DescriptionText.ToString(parser.getTranslationData());
                exportDescriptionSprites(description, allSprites, spriteDirectory);
            }
        }

        using (var pbar = new ProgressBar(nubbyPerks.Count, "Exporting perk sprites"))
        {
            foreach (var nubbyPerk in nubbyPerks)
            {
                pbar.Tick($"Exporting {nubbyPerk.ObjectName}");

                var gameObject = nubbyPerk.GameObject;
                var sprite = gameObject?.Sprite;
                if (sprite == null) continue;

                exportSprite(sprite, spriteDirectory);
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

                exportSprite(sprite, spriteDirectory);
            }
        }
    }

    private static void exportSprite(UndertaleSprite sprite, DirectoryInfo spriteDirectory)
    {
        var spritePath = Path.Combine(spriteDirectory.FullName, $"{sprite.Name.Content}.gif");

        var spriteFrames = NubbyUtil.loadFramesFromSprite(sprite);
        using MagickImageCollection collection = new MagickImageCollection(spriteFrames);
        collection.Coalesce();
        collection.Write(new FileInfo(spritePath));
    }
}