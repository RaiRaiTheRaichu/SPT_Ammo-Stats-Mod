using SemanticVersioning;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmmoStats
{
    public class ConfigType
    {
        public enum ModeEnum
        {
            prepend,
            append
        };

        public enum SeparatorEnum
        {
            newline,
            oneline
        }

        public ModeEnum mode { get; set; }
        public SeparatorEnum separator { get; set; }
        public ConfigLocaleEditType bulletStats { get; set; }
        public bool showPenInName { get; set; }
        public bool enableBulletColoredIcons { get; set; }
        public bool enableBoxesColoredIcons { get; set; }
        public Dictionary<int, string> backgroundColors { get; set; }
        public bool enableExtendedBackgroundColors { get; set; }
        public string colorProfile { get; set; }
        public Dictionary<string, Dictionary<int, string>> colorProfiles { get; set; }
        public Dictionary<string, ConfigLocaleEntryType> localeList { get; set; }
    }

    public class ConfigLocaleEditType
    {
        public bool addDamage { get; set; }
        public bool addPen { get; set; }
        public bool addEffectArmorLv { get; set; }

        public bool GetAnyTrue()
        {
            return addDamage || addPen || addEffectArmorLv;
        }

        public int Amount()
        {
            return (addDamage ? 1 : 0) + (addPen ? 1 : 0) + (addEffectArmorLv ? 1 : 0);
        }
    }

    public class ConfigLocaleEntryType
    {
        public string Damage { get; set; }
        public string Penetration { get; set; }
        public string TextEffectArmorLv { get; set; }
        public string EffectNone { get; set; }
        public string Pellets { get; set; }
    }
}