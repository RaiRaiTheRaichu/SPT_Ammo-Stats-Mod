using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using System.Reflection;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace AmmoStats
{
    public record ModMetadata : IModMetadata
    {
        public string ModGuid { get; init; } = "com.rairaitheraichu.ammostats";
        public string Name { get; init; } = "AmmoStats";
        public string Author { get; init; } = "RaiRaiTheRaichu";
        public List<string>? Contributors { get; init; }
        public SemanticVersioning.Version Version { get; init; } = new("4.1.0");
        public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.0");
        public List<string>? Incompatibilities { get; init; }
        public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
        public string? Url { get; init; } = "https://github.com/RaiRaiTheRaichu/SPT_Ammo-Stats-Mod";
        public string? License { get; init; } = "Apache License V2.0";
        public bool HasPrepatcher { get; init; } = false;
    }
    [Injectable(TypePriority = OnLoadOrder.Preload + 1)]
    public class AmmoStatsMod(
        TemplateTable templateTable,
        ModHelper modHelper,
        LocaleTable localeTable,
        ISptLogger<AmmoStatsMod> logger) : IOnLoad
    {
        private readonly ISptLogger<AmmoStatsMod> _logger = logger;
        private readonly char OS_SEPARATOR = System.IO.Path.DirectorySeparatorChar;
        private ConfigType ModConfig = new ConfigType();
        private Dictionary<MongoId, AmmoDictionary> AmmoStatDictionary = new();

        
        internal class AmmoDictionary
        {
            public double BulletDamage { get; set; }
            public int BulletPenetration { get; set; }
            public int BulletArmorTier { get; set; }
            public double BulletProjectiles { get; set; }
            public List<MongoId> AmmoBoxes { get; set; }
        }
        

        public Task OnLoadAsync(CancellationToken cancellationToken)
        {
            // Load config
            var modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
            ModConfig = modHelper.GetJsonDataFromFile<ConfigType>(modPath + OS_SEPARATOR + "config", "config.jsonc");
            
            Dictionary<MongoId, TemplateItem> itemDatabase = templateTable.Items;

            var ammo = itemDatabase.Values.Where(item => item.Properties.AmmoType == "bullet" || item.Properties.AmmoType == "buckshot");

            foreach (TemplateItem bullet in ammo)
            {
                if (bullet.Id == "677ae5df4be46b83620bf055")    // Skipping
                    continue;

                AmmoStatDictionary.Add(bullet.Id, new AmmoDictionary());

                AmmoStatDictionary[bullet.Id].BulletDamage = (double)bullet.Properties?.Damage;
                AmmoStatDictionary[bullet.Id].BulletPenetration = (int)bullet.Properties?.PenetrationPower;

                if (bullet.Properties.AmmoType.ToLower() == "buckshot")
                    AmmoStatDictionary[bullet.Id].BulletProjectiles = (double)bullet.Properties?.BuckshotBullets;
                else
                    AmmoStatDictionary[bullet.Id].BulletProjectiles = 1;


                AmmoStatDictionary[bullet.Id].BulletArmorTier = CalculateArmorLevel((int)bullet.Properties?.PenetrationPower);


                AmmoStatDictionary[bullet.Id].AmmoBoxes = itemDatabase.Where(item =>
                    item.Value.Properties?.StackSlots?.FirstOrDefault()?
                        .Properties?.Filters?.FirstOrDefault()?.Filter?.Contains(bullet.Id) ?? false)
                   .Select(item => item.Key)
                   .ToList();
            }

            cancellationToken.ThrowIfCancellationRequested();
            
            if (ModConfig.enableBulletColoredIcons || ModConfig.enableBoxesColoredIcons)
                ApplyBackgroundColors();

            if (ModConfig.bulletStats.GetAnyTrue())
                ApplyLocaleChanges();
            

            return Task.CompletedTask;
        }

        
        internal int CalculateArmorLevel(int penetrationPower)
        {
            int penetrationTier = 1;

            while (penetrationTier <= 6)
            {
                double armorStrength = penetrationTier * 10;

                if (armorStrength >= penetrationPower + 15) break;
                if (armorStrength <= penetrationPower - 15) 
                {
                    penetrationTier++;
                    continue;
                }

                double penChance = 0.0;
                if (armorStrength >= penetrationPower)
                    penChance = 0.4 * Math.Pow(armorStrength - penetrationPower - 15.0, 2);
                else
                    penChance = 100.0 + penetrationPower / (0.9 * armorStrength - penetrationPower);

                if (penChance >= 50.0)
                {
                    penetrationTier++;
                    continue;
                }

                break;
            }
            return penetrationTier - 1;
        }

        internal void ApplyBackgroundColors()
        {
            Dictionary<MongoId, TemplateItem> itemDatabase = templateTable.Items;

            bool hexColors = ModConfig.enableExtendedBackgroundColors && GetColorPlugin();
            Dictionary<int, string> ColorProfile = hexColors ? ModConfig.colorProfiles[ModConfig.colorProfile] : ModConfig.backgroundColors;

            //_logger.Info($"[AmmoStats] Applying background colors.");

            foreach (var bulletItem in AmmoStatDictionary)
            {
                string color = ColorProfile[bulletItem.Value.BulletArmorTier];

                if (ModConfig.enableBulletColoredIcons)
                    itemDatabase[bulletItem.Key].Properties.BackgroundColor = color;

                if (ModConfig.enableBoxesColoredIcons)
                {
                    foreach (var boxItem in AmmoStatDictionary[bulletItem.Key].AmmoBoxes)
                    {
                        itemDatabase[boxItem].Properties.BackgroundColor = color;
                    }
                }
            }
        }

        internal void ApplyLocaleChanges()
        {
            var bulletStats = ModConfig.bulletStats;

            string separator = "";
            if (ModConfig.separator == ConfigType.SeparatorEnum.oneline) separator = " | ";
            else if (ModConfig.separator == ConfigType.SeparatorEnum.newline) separator = "\n";

            foreach (var localeEntry in ModConfig.localeList)
            {
                foreach (var bulletEntry in AmmoStatDictionary)
                {
                    if (localeTable.Global.TryGetValue(localeEntry.Key, out var lazyloadedValue))
                    {
                        string newStatString = "";
                        int stringsToAdd = ModConfig.bulletStats.Amount();
                        if (bulletStats.addDamage)
                        {
                            newStatString += $"{localeEntry.Value.Damage}: {bulletEntry.Value.BulletDamage}";
                            if (bulletEntry.Value.BulletProjectiles > 1)
                            {
                                newStatString += $" * {bulletEntry.Value.BulletProjectiles} ({bulletEntry.Value.BulletDamage * bulletEntry.Value.BulletProjectiles})";
                            }
                            --stringsToAdd;
                            if (stringsToAdd > 0) newStatString += separator;
                        }

                        if (bulletStats.addPen)
                        {
                            newStatString += $"{localeEntry.Value.Penetration}: {bulletEntry.Value.BulletPenetration}";
                            --stringsToAdd;
                            if (stringsToAdd > 0) newStatString += separator;
                        }

                        if (bulletStats.addEffectArmorLv)
                        {
                            newStatString += $"{localeEntry.Value.TextEffectArmorLv}: ";
                            if (bulletEntry.Value.BulletArmorTier > 0)
                                newStatString += $"{bulletEntry.Value.BulletArmorTier}";
                            else newStatString += localeEntry.Value.EffectNone;
                        }

                        lazyloadedValue.AddTransformer(lazyloadedLocaleData =>
                        {
                            string oldDescription = lazyloadedLocaleData[$"{bulletEntry.Key} Description"];

                            if (ModConfig.mode == ConfigType.ModeEnum.prepend)
                                lazyloadedLocaleData[$"{bulletEntry.Key} Description"] = newStatString + $"\n\n" + oldDescription;
                            else if (ModConfig.mode == ConfigType.ModeEnum.append)
                                lazyloadedLocaleData[$"{bulletEntry.Key} Description"] += $"\n\n{newStatString}";

                            return lazyloadedLocaleData;
                        });

                        if (ModConfig.showPenInName)
                        {
                            lazyloadedValue.AddTransformer(lazyloadedLocaleData =>
                            {
                                lazyloadedLocaleData[$"{bulletEntry.Key} Name"] += $" ({bulletEntry.Value.BulletArmorTier})";
                                return lazyloadedLocaleData;
                            });
                        }
                    }
                }
            }
        }

        internal bool GetColorPlugin()
        {
            var modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
            var bepinexPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(modPath, @"..\..\..\..\BepInEx\"));

            var file = Directory.GetFiles(bepinexPath, "RaiRai.ColorConverterAPI.dll", SearchOption.AllDirectories);
            _logger.Info($"[AmmoStats] ColorConverterAPI Plugin detected?: {file.Any()}");
            return file.Any();
        }
        
    }
}
