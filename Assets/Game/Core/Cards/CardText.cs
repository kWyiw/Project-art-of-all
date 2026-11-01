using System.Collections.Generic;

namespace ProjectArtOfAll.Core.Cards
{
    /// <summary>
    /// Card face text. Kept in the pure kernel so the HUD, the headless simulator and the
    /// tests all describe a card the same way (and so "卡面文案" can be asserted in checks).
    /// </summary>
    public static class CardText
    {
        private static readonly CardTag[] TagOrder =
        {
            CardTag.Weapon,
            CardTag.Tool,
            CardTag.Waste,
            CardTag.Food,
            CardTag.Companion,
            CardTag.Trap,
            CardTag.Estate,
            CardTag.Craft,
            CardTag.Resource
        };

        public static string TagName(CardTag tag)
        {
            switch (tag)
            {
                case CardTag.Weapon:
                    return "武器";
                case CardTag.Tool:
                    return "工具";
                case CardTag.Waste:
                    return "废料";
                case CardTag.Food:
                    return "食物";
                case CardTag.Companion:
                    return "伙伴";
                case CardTag.Trap:
                    return "陷阱";
                case CardTag.Estate:
                    return "地产";
                case CardTag.Craft:
                    return "合成";
                case CardTag.Resource:
                    return "资源";
                default:
                    return "无";
            }
        }

        public static string TagList(CardTag tags)
        {
            if (tags == CardTag.None)
            {
                return "无标签";
            }

            List<string> names = new List<string>();
            foreach (CardTag tag in TagOrder)
            {
                if ((tags & tag) != 0)
                {
                    names.Add(TagName(tag));
                }
            }

            return names.Count == 0 ? "无标签" : string.Join(" · ", names);
        }

        /// <summary>「3 秒」/「被动」。180 tick = 3 秒。</summary>
        public static string Seconds(int ticks)
        {
            if (ticks <= 0)
            {
                return "被动";
            }

            float seconds = ticks / 60f;
            return seconds >= 10f
                ? string.Concat(((int)(seconds + 0.5f)).ToString(), " 秒")
                : string.Concat(seconds.ToString("0.##"), " 秒");
        }

        public static string Cooldown(CardDefinition card)
        {
            if (card == null)
            {
                return string.Empty;
            }

            return card.IsActive ? Seconds(card.CooldownTicks) : "被动";
        }

        /// <summary>「1d6」「2d20」「Xd4」「3」。</summary>
        public static string Amount(EffectAmount amount)
        {
            switch (amount.Kind)
            {
                case AmountKind.Dice:
                    return string.Concat(
                        amount.Count.ToString(),
                        "d",
                        amount.Faces.ToString());
                case AmountKind.ValueScaledDice:
                    return string.Concat("Xd", amount.Faces.ToString());
                default:
                    return amount.Constant.ToString();
            }
        }

        public static string TargetName(EffectTarget target)
        {
            switch (target)
            {
                case EffectTarget.Enemy:
                    return string.Empty;
                case EffectTarget.Self:
                case EffectTarget.SelfCard:
                    return "自身 ";
                case EffectTarget.RandomOwnCard:
                    return "随机我方牌 ";
                case EffectTarget.RandomEnemyCard:
                    return "随机敌方牌 ";
                case EffectTarget.AdjacentOwnCards:
                    return "相邻我方牌 ";
                default:
                    return string.Empty;
            }
        }

        public static string Effect(CardEffect effect)
        {
            if (effect == null)
            {
                return string.Empty;
            }

            string amount = Amount(effect.Amount);
            string body;
            switch (effect.Operation)
            {
                case EffectOp.DealDamage:
                    body = string.Concat(amount, " 伤害");
                    break;
                case EffectOp.GainShield:
                    body = string.Concat(amount, " 护盾");
                    break;
                case EffectOp.ApplyPoison:
                    body = string.Concat(amount, " 层中毒");
                    break;
                case EffectOp.ApplyBurn:
                    body = string.Concat(amount, " 层燃烧");
                    break;
                case EffectOp.ApplyRegeneration:
                    body = string.Concat(amount, " 层再生");
                    break;
                case EffectOp.Heal:
                    body = string.Concat("治疗 ", amount);
                    break;
                case EffectOp.Charge:
                    body = string.Concat("充能 ", Seconds(effect.DurationTicks));
                    break;
                case EffectOp.Haste:
                    body = string.Concat("加速 ", Seconds(effect.DurationTicks));
                    break;
                case EffectOp.Slow:
                    body = string.Concat("缓速 ", Seconds(effect.DurationTicks));
                    break;
                case EffectOp.MultiCast:
                    body = string.Concat(
                        "额外施放 ×",
                        effect.CardCount.ToString());
                    break;
                case EffectOp.DestroySelf:
                    body = "销毁自身";
                    break;
                case EffectOp.ModifyValue:
                    body = string.Concat("价值 +", amount);
                    break;
                case EffectOp.ModifyDiceCount:
                    body = string.Concat("骰数 +", amount);
                    break;
                case EffectOp.ModifyShieldBonus:
                    body = string.Concat("护盾获得 +", amount);
                    break;
                default:
                    body = effect.Operation.ToString();
                    break;
            }

            return string.Concat(TargetName(effect.Target), body);
        }

        public static string Aura(AuraDefinition aura)
        {
            if (aura == null)
            {
                return string.Empty;
            }

            if (aura.Stat == AuraStat.MaxRoll)
            {
                return string.Concat(AuraTargetName(aura), "骰值必定取最大值");
            }

            string stat = aura.Stat == AuraStat.MultiCast
                ? "额外施放"
                : aura.Stat == AuraStat.DiceCount
                    ? "骰数"
                    : "骰值";
            string amount = aura.Amount >= 0
                ? string.Concat("+", aura.Amount.ToString())
                : aura.Amount.ToString();
            return string.Concat(AuraTargetName(aura), stat, " ", amount);
        }

        private static string AuraTargetName(AuraDefinition aura)
        {
            string tag = aura.RequiredTag == CardTag.None
                ? string.Empty
                : TagName(aura.RequiredTag);
            switch (aura.Target)
            {
                case AuraTarget.Self:
                    return "自身 ";
                case AuraTarget.AdjacentLeft:
                    return string.Concat("左邻", tag);
                case AuraTarget.AdjacentRight:
                    return string.Concat("右邻", tag);
                case AuraTarget.AllAdjacent:
                    return string.Concat("相邻", tag);
                case AuraTarget.AllByTag:
                    return string.Concat("全场", tag);
                default:
                    return string.Empty;
            }
        }

        public static string TriggerName(PassiveTrigger trigger)
        {
            switch (trigger)
            {
                case PassiveTrigger.BattleStart:
                    return "战斗开始时";
                case PassiveTrigger.OwnCardCast:
                    return "己方施放";
                case PassiveTrigger.AnyCardCast:
                    return "任一方施放";
                case PassiveTrigger.SlowApplied:
                    return "缓速命中时";
                case PassiveTrigger.HasteApplied:
                    return "加速命中时";
                case PassiveTrigger.ChargeApplied:
                    return "充能命中时";
                case PassiveTrigger.BurnApplied:
                    return "燃烧命中时";
                case PassiveTrigger.DamageDealt:
                    return "造成伤害后";
                case PassiveTrigger.DamageTaken:
                    return "受到伤害后";
                case PassiveTrigger.ShieldGained:
                    return "获得护盾后";
                case PassiveTrigger.CardSold:
                    return "被出售时";
                default:
                    return "触发时";
            }
        }

        public static string Passive(PassiveDefinition passive)
        {
            if (passive == null)
            {
                return string.Empty;
            }

            string tag = passive.CastTag == CardTag.None
                ? string.Empty
                : TagName(passive.CastTag);
            return string.Concat(
                TriggerName(passive.Trigger),
                tag,
                " → ",
                Effect(passive.Effect));
        }

        /// <summary>一行卡面摘要：尺寸 · 冷却 · 标签。</summary>
        public static string Meta(CardDefinition card)
        {
            if (card == null)
            {
                return string.Empty;
            }

            return string.Concat(
                card.SlotSize.ToString(),
                "c · ",
                Cooldown(card),
                " · ",
                TagList(card.Tags));
        }

        /// <summary>效果正文，逗号分隔；没有效果时返回空串。</summary>
        public static string Description(CardDefinition card)
        {
            if (card == null)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            foreach (CardEffect effect in card.ActiveEffects)
            {
                parts.Add(Effect(effect));
            }

            foreach (AuraDefinition aura in card.Auras)
            {
                parts.Add(string.Concat("光环：", Aura(aura)));
            }

            foreach (PassiveDefinition passive in card.Passives)
            {
                parts.Add(string.Concat("被动：", Passive(passive)));
            }

            return string.Join("，", parts);
        }
    }
}
