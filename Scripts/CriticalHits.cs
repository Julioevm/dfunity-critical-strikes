// Project:         Critical Hits mod for Daggerfall Unity (http://www.dfworkshop.net)
// Copyright:       Copyright (C) 2021 Reaven
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Author:          Reaven
// Version:			0.3

using DaggerfallConnect;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.MagicAndEffects.MagicEffects;
using UnityEngine;
using System;

namespace CriticalHits
{
    public class CriticalHits : MonoBehaviour
    {
        static Mod mod;
        static int criticalDamageBase;
        static int playerDivideBy;
        static int enemyDivideBy;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            var go = new GameObject("CriticalHits");
            go.AddComponent<CriticalHits>();
        }

        void Awake()
        {
            ModSettings settings = mod.GetSettings();
            criticalDamageBase = settings.GetValue<int>("criticalHits", "criticalDamageBase");
            playerDivideBy = settings.GetValue<int>("criticalHits", "playerChance");
            enemyDivideBy = settings.GetValue<int>("criticalHits", "enemyChance");
            InitMod();
            mod.IsReady = true;
        }

        private static void InitMod()
        {
            Debug.Log("Begin mod init: Critical Hits");
            FormulaHelper.RegisterOverride(mod, "CalculateAttackDamage", (Func<DaggerfallEntity, DaggerfallEntity, bool, int, DaggerfallUnityItem, int>)CalculateAttackDamage);
            FormulaHelper.RegisterOverride(mod, "CalculateSkillsToHit", (Func<DaggerfallEntity, DaggerfallEntity, int>)CalculateSkillsToHit);
            #if UNITY_EDITOR
                Debug.LogFormat("criticalDamageBase: {0}", criticalDamageBase);
                Debug.LogFormat("playerDivideBy: {0}", playerDivideBy);
                Debug.LogFormat("enemyDivideBy: {0}", enemyDivideBy);
            #endif
        }

        public static int CalculateAttackDamage(DaggerfallEntity attacker, DaggerfallEntity target, bool isEnemyFacingAwayFromPlayer, int weaponAnimTime, DaggerfallUnityItem weapon)
        {

            if (attacker == null || target == null)
                return 0;

            int damageModifiers = 0;
            int damage = 0;
            int chanceToHitMod = 0;
            int backstabChance = 0;
            PlayerEntity player = GameManager.Instance.PlayerEntity;
            short skillID = 0;
            bool critSuccess = false;
            int critBonusDamage = 0;
            int critHitAddi = 0;

            // Choose whether weapon-wielding enemies use their weapons or weaponless attacks.
            // In classic, weapon-wielding enemies use the damage values of their weapons
            // instead of their weaponless values.
            // For some enemies this gives lower damage than similar-tier monsters
            // and the weaponless values seems more appropriate, so here
            // enemies will choose to use their weaponless attack if it is more damaging.
            EnemyEntity AIAttacker = attacker as EnemyEntity;
            if (AIAttacker != null && weapon != null)
            {
                int weaponAverage = (weapon.GetBaseDamageMin() + weapon.GetBaseDamageMax()) / 2;
                int noWeaponAverage = (AIAttacker.MobileEnemy.MinDamage + AIAttacker.MobileEnemy.MaxDamage) / 2;

                if (noWeaponAverage > weaponAverage)
                {
                    // Use hand-to-hand
                    weapon = null;
                }
            }

            if (weapon != null)
            {
                // If the attacker is using a weapon, check if the material is high enough to damage the target
                if (target.MinMetalToHit > (WeaponMaterialTypes)weapon.NativeMaterialValue)
                {
                    if (attacker == player)
                    {
                        DaggerfallUI.Instance.PopupMessage(TextManager.Instance.GetLocalizedText("materialIneffective"));
                    }
                    return 0;
                }
                // Get weapon skill used
                skillID = weapon.GetWeaponSkillIDAsShort();
            }
            else
            {
                skillID = (short)DFCareer.Skills.HandToHand;
            }

            chanceToHitMod = attacker.Skills.GetLiveSkillValue(skillID);

            // Handle critical hit damage


            critSuccess = IsCriticalStrike(attacker);

            if (critSuccess)
            {
                int criticalStrikeSkill = attacker.Skills.GetLiveSkillValue(DFCareer.Skills.CriticalStrike);
                critBonusDamage = GetPlayerCritDamage(criticalStrikeSkill);
                critHitAddi = (criticalStrikeSkill / 4);
                chanceToHitMod += critHitAddi;

                #if UNITY_EDITOR
                     Debug.LogFormat("1. critical strike bonus damage: {0}", critBonusDamage);
                     Debug.LogFormat("2. critical strike bonus toHit: {0}", critHitAddi);
                     Debug.LogFormat("3. Final toHit: {0}", chanceToHitMod);
                #endif
            }


            if (attacker == player)
            {
                // Apply swing modifiers
                FormulaHelper.ToHitAndDamageMods swingMods = FormulaHelper.CalculateSwingModifiers(GameManager.Instance.WeaponManager.ScreenWeapon);
                damageModifiers += swingMods.damageMod;
                chanceToHitMod += swingMods.toHitMod;

                // Apply proficiency modifiers
                FormulaHelper.ToHitAndDamageMods proficiencyMods = FormulaHelper.CalculateProficiencyModifiers(attacker, weapon);
                damageModifiers += proficiencyMods.damageMod;
                chanceToHitMod += proficiencyMods.toHitMod;

                // Apply racial bonuses
                FormulaHelper.ToHitAndDamageMods racialMods = FormulaHelper.CalculateRacialModifiers(attacker, weapon, player);
                damageModifiers += racialMods.damageMod;
                chanceToHitMod += racialMods.toHitMod;

                backstabChance = FormulaHelper.CalculateBackstabChance(player, null, isEnemyFacingAwayFromPlayer);
                chanceToHitMod += backstabChance;
            }

            // Choose struck body part
            int struckBodyPart = FormulaHelper.CalculateStruckBodyPart();

            // Get damage for weaponless attacks
            if (skillID == (short)DFCareer.Skills.HandToHand)
            {
                if (attacker == player || (AIAttacker != null && AIAttacker.EntityType == EntityTypes.EnemyClass))
                {
                    if (FormulaHelper.CalculateSuccessfulHit(attacker, target, chanceToHitMod, struckBodyPart))
                    {
                        damage = FormulaHelper.CalculateHandToHandAttackDamage(attacker, target, damageModifiers, attacker == player);

                        damage = FormulaHelper.CalculateBackstabDamage(damage, backstabChance);
                    }
                }
                else if (AIAttacker != null) // attacker is a monster
                {
                    // Handle multiple attacks by AI
                    int minBaseDamage = 0;
                    int maxBaseDamage = 0;
                    int attackNumber = 0;
                    while (attackNumber < 3) // Classic supports up to 5 attacks but no monster has more than 3
                    {
                        if (attackNumber == 0)
                        {
                            minBaseDamage = AIAttacker.MobileEnemy.MinDamage;
                            maxBaseDamage = AIAttacker.MobileEnemy.MaxDamage;
                        }
                        else if (attackNumber == 1)
                        {
                            minBaseDamage = AIAttacker.MobileEnemy.MinDamage2;
                            maxBaseDamage = AIAttacker.MobileEnemy.MaxDamage2;
                        }
                        else if (attackNumber == 2)
                        {
                            minBaseDamage = AIAttacker.MobileEnemy.MinDamage3;
                            maxBaseDamage = AIAttacker.MobileEnemy.MaxDamage3;
                        }

                        int reflexesChance = 50 - (10 * ((int)player.Reflexes - 2));

                        int hitDamage = 0;
                        if (DFRandom.rand() % 100 < reflexesChance && minBaseDamage > 0 && FormulaHelper.CalculateSuccessfulHit(attacker, target, chanceToHitMod, struckBodyPart))
                        {
                            hitDamage = UnityEngine.Random.Range(minBaseDamage, maxBaseDamage + 1);
                            // Apply special monster attack effects
                            if (hitDamage > 0)
                                FormulaHelper.OnMonsterHit(AIAttacker, target, hitDamage);

                            damage += hitDamage;
                        }

                        // Apply bonus damage only when monster has actually hit, or they will accumulate bonus damage even for missed attacks and zero-damage attacks
                        if (hitDamage > 0)
                            damage += FormulaHelper.GetBonusOrPenaltyByEnemyType(attacker, target);

                        ++attackNumber;
                    }
                }
            }
            // Handle weapon attacks
            else if (weapon != null)
            {
                // Apply weapon material modifier.
                chanceToHitMod += FormulaHelper.CalculateWeaponToHit(weapon);

                // Mod hook for adjusting final hit chance mod and adding new elements to calculation. (no-op in DFU)
                chanceToHitMod = FormulaHelper.AdjustWeaponHitChanceMod(attacker, target, chanceToHitMod, weaponAnimTime, weapon);

                if (FormulaHelper.CalculateSuccessfulHit(attacker, target, chanceToHitMod, struckBodyPart))
                {
                    damage = FormulaHelper.CalculateWeaponAttackDamage(attacker, target, damageModifiers, weaponAnimTime, weapon);

                    damage = FormulaHelper.CalculateBackstabDamage(damage, backstabChance);
                }

                // Handle poisoned weapons
                if (damage > 0 && weapon.poisonType != Poisons.None)
                {
                    FormulaHelper.InflictPoison(attacker, target, weapon.poisonType, false);
                    weapon.poisonType = Poisons.None;
                }
            }

            #if UNITY_EDITOR
                Debug.LogFormat("Standard Damage = {0}", damage);
            #endif

            if (critSuccess)
            {
                damage += critBonusDamage;
                #if UNITY_EDITOR
                    Debug.LogFormat("Critical damage = {0}", damage);
                #endif
            }

            damage = Mathf.Max(0, damage);

            FormulaHelper.DamageEquipment(attacker, target, damage, weapon, struckBodyPart);

            // Apply Ring of Namira effect
            if (target == player)
            {
                DaggerfallUnityItem[] equippedItems = target.ItemEquipTable.EquipTable;
                DaggerfallUnityItem item = null;
                if (equippedItems.Length != 0)
                {
                    if (IsRingOfNamira(equippedItems[(int)EquipSlots.Ring0]) || IsRingOfNamira(equippedItems[(int)EquipSlots.Ring1]))
                    {
                        IEntityEffect effectTemplate = GameManager.Instance.EntityEffectBroker.GetEffectTemplate(RingOfNamiraEffect.EffectKey);
                        effectTemplate.EnchantmentPayloadCallback(EnchantmentPayloadFlags.None,
                            targetEntity: AIAttacker.EntityBehaviour,
                            sourceItem: item,
                            sourceDamage: damage);
                    }
                }
            }

            return damage;
        }



        public static int CalculateSkillsToHit(DaggerfallEntity attacker, DaggerfallEntity target)
        {
            PlayerEntity player = GameManager.Instance.PlayerEntity;
            int chanceToHitMod = 0;

            // Apply dodging modifier.
            // This modifier is bugged in classic and the attacker's dodging skill is used rather than the target's.
            // DF Chronicles says the dodging calculation is (dodging / 10), but it actually seems to be (dodging / 4).
            // Apply dodging modifier.
            chanceToHitMod -= (target.Skills.GetLiveSkillValue(DFCareer.Skills.Dodging) / 2); // Changing 4 to a 2, so 100 dodge will give -50 to hit chance, very powerful.

            // Removed additional chance to hit from DFU

            return chanceToHitMod;
        }

        private static bool IsCriticalStrike(DaggerfallEntity attacker)
        {
            PlayerEntity player = GameManager.Instance.PlayerEntity;
            int attackerLuckBonus = (int)Mathf.Floor(attacker.Stats.LiveLuck / 10);
            short criticalStrikeSkill = attacker.Skills.GetLiveSkillValue(DFCareer.Skills.CriticalStrike);
            int divider = (attacker == player) ? playerDivideBy : enemyDivideBy;

            int criticalChance = (criticalStrikeSkill / divider) + attackerLuckBonus;

            #if UNITY_EDITOR
                Debug.LogFormat("{0} Critical chance: {1}. Skill: {2}/{3} + {4}", attacker.Name, criticalChance, criticalStrikeSkill, divider ,attackerLuckBonus);
            #endif
            return Dice100.SuccessRoll(criticalChance); // Player has a 25% chance of critting at level 100. 32% with 75 luck, and 45% with 100 luck.
        }

        private static int GetPlayerCritDamage(int playerSkill)
        {
            int critDamage = 0;

            if (playerSkill < 98)
            {
                critDamage = Mathf.RoundToInt(playerSkill / 25);
            }
            else
            {
                critDamage = 6;
            }

            return critDamage * criticalDamageBase;
        }

        private static bool IsRingOfNamira(DaggerfallUnityItem item)
        {
            return item != null && item.ContainsEnchantment(DaggerfallConnect.FallExe.EnchantmentTypes.SpecialArtifactEffect, (int)ArtifactsSubTypes.Ring_of_Namira);
        }
    }
}