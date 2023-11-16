using Mirror;
using RuntimeInspectorNamespace;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.PlayerLoop;
using UnityEngine.SceneManagement;

public class BuffDebuffHandler : NetworkBehaviourNonAlloc
{
    [SerializeField] Entity player; 
    [SerializeField] BuffDebuffHandler buffDebuffHandler;

    #region Adding & Refreshing Buffs


    //Adds permanent buff
    public void AddPermanentBuff(int i)
    {
        AddOrRefreshBuff(new Buff((BuffApplication)player.passiveAbilities[i].ability.permanentBuffSO), player);
    }


    //for friendly HoTS / DoTs
    private IEnumerator BuffOverTime(Buff buff, Entity caster)
    {
        var index = GetBuffIndexByName(buff.name, caster.name);
        while (buff.active && index != -1)
        {
            if (buff.damage != 0) player.damageHandler.DealDamageAt(caster, buff.damage * player.buffInfo[index].currentStacks, AbilitySO.AbilitySchool.None, buff.name, index, "buff");
            if (buff.healing != 0) player.healingHandler.DealHealingAt(caster, buff.healing * player.buffInfo[index].currentStacks, AbilitySO.AbilitySchool.None, buff.name, index, "buff");

            // deal with proximity healing using overlap sphere
            if (buff.proximityDamageOrHealing != BuffApplication.Proximity.None)
            {
                HandleProximityOutput(buff, caster, index);
            }

            yield return new WaitForSeconds(buff.Tickrate(caster));
            index = GetBuffIndexByName(buff.name, caster.name);
        }
    }

    //Proximity HoTs/DoTs for buffs. For Auras / ticking aoe 
    private void HandleProximityOutput(Buff buff, Entity caster, int index)
    {
        var col = Physics.OverlapSphere(transform.position, buff.pulseRange);
        foreach (var t in col)
            if (t.GetComponentInParent<Entity>())
            {
                var entity = t.GetComponentInParent<Entity>();
                var tname = entity.name;
                if (player.unfriendlyPlayers.Contains(tname) && buff.proximityDamageOrHealing == BuffApplication.Proximity.Damage)
                    entity.damageHandler.DealDamageAt(caster, buff.pulsingAoeDamageOrHealing * player.buffInfo[index].currentStacks, AbilitySO.AbilitySchool.None, buff.name, index, "buff");
                else if (!player.unfriendlyPlayers.Contains(tname) && buff.proximityDamageOrHealing == BuffApplication.Proximity.Healing)
                    entity.healingHandler.DealHealingAt(caster, buff.pulsingAoeDamageOrHealing * player.buffInfo[index].currentStacks, AbilitySO.AbilitySchool.None, buff.name, index, "buff");
            }
    }


    //For setting buffs active - Referenced inside of AddOrRefreshBuffs 
    public void ApplyBuffOverTime(Buff buff, Entity caster)
    {
        if (!buff.active)
        {
            buff.active = true;
            StartCoroutine(BuffOverTime(buff, caster));
        }
        else
        {
            buff.active = true;
        }
    }


    //For adding a buff if it's not already in the list by the caster otherwise refreshes the duration & stacks / adds stacks
    public void AddOrRefreshBuff(Buff buff, Entity caster, int customHealing = 0, bool alreadySpread = false, int customStacks = 0,
    BuffApplication.AddStacksCondition addStacksCondition = BuffApplication.AddStacksCondition.None)
    {
        // add buff if it's new, refresh if it's already up
        var index = GetBuffIndexByName(buff.name, caster.name);
        UpdateStatsBuffsPassives(caster, buff.name, AbilitySO.AbilitySchool.None, 0, index, "buff", "buff");
        //var shieldIndex = GetShieldIndexByName(buff.name, caster.name);
        // if the buff is still currently active...
        if (index != -1)
        {
            // grab that particular buff
            // as we are unable to directly modify buffs, we must create a temporary one, modify it and set it to the current buff.
            var b = player.buffInfo[index];
            b.buff = buff;

            // add stacks to the buff struct so we can eventually proc effects at higher stacks, etc.
            if (b.buff.data.addStacksCondition == addStacksCondition)
            {
                if (b.currentStacks + b.buff.data.addStacksAmount <= b.buff.maxStacks)
                    b.currentStacks += b.buff.data.addStacksAmount;
                else
                    b.currentStacks = b.buff.maxStacks;
            }

            // overwrite the current buff with the temporary one we just created
            player.buffInfo[index] = b;
        }
        else
        {
            // create a new buff, assign its data based on given parameters in the scriptable object and add it to the buff list.
            var b = new BuffInfo
            {
                currentStacks = customStacks != 0 ? customStacks : buff.beginningStacks,
                buff = buff,
                buffedBy = caster.name,
                shieldAmount = buff.data.shields
            };

            //if (buff.data.shields > 0) AddShield(buff, caster);
            //b.shieldAmount = buff.data.shields;

            b.buff.active = false;

            player.buffInfo.Add(b);

            ApplyBuffOverTime(b.buff, caster);
        }
    }

    //Takes the buff removal condition and removes a stack if it's the same as the buff's removal condition
    public void BuffStackRemoval(Entity caster, Entity target, BuffApplication.BuffRemovalCondition buffRemovalCondition, string abilityName = null)
    {
        if (caster == null) return;

        if (caster != target)
        {
            for (int i = 0; i < target.buffInfo.Count; i++)
            {
                if (buffRemovalCondition == target.buffInfo[i].buff.buffRemovalCondition)
                {
                    if (buffRemovalCondition == BuffApplication.BuffRemovalCondition.SpecificSpell)
                    {
                        foreach (var ability in target.buffInfo[i].buff.specificAbilitiesForRemoval)
                            if (ability.abilityName == abilityName)
                            {
                                if (caster.buffInfo[i].currentStacks <= 1)
                                {
                                    ProcessBuffStackRemoval(caster, target, i);
                                    i--;
                                }
                                else
                                    ProcessBuffStackRemoval(caster, target, i);
                            }
                    }
                    else
                    {
                        ProcessBuffStackRemoval(caster, target, i);
                    }
                }
            }
        }

        for (int i = 0; i < caster.buffInfo.Count; i++)
        {
            if (buffRemovalCondition == caster.buffInfo[i].buff.buffRemovalCondition)
            {
                if (buffRemovalCondition == BuffApplication.BuffRemovalCondition.SpecificSpell)
                {
                    foreach (var ability in caster.buffInfo[i].buff.specificAbilitiesForRemoval)
                        if (ability.abilityName == abilityName)
                        {
                            if (caster.buffInfo[i].currentStacks <= 1)
                            {
                                ProcessBuffStackRemoval(caster, caster, i);
                                i--;
                            }
                            else
                                ProcessBuffStackRemoval(caster, caster, i);
                        }
                }
                else
                {
                    ProcessBuffStackRemoval(caster, caster, i);
                }
            }
        }
        //foreach(var buffInfo in caster.buffInfo)
        //{
        //    int i = caster.buffDebuffHandler.GetBuffIndexByName(buffInfo.buff.name);

        //    if (buffRemovalCondition == buffInfo.buff.buffRemovalCondition)
        //    {
        //        if (buffRemovalCondition == BuffApplication.BuffRemovalCondition.SpecificSpell)
        //        {
        //            foreach (var ability in buffInfo.buff.specificAbilitiesForRemoval)
        //                if (ability.abilityName == abilityName)
        //                    ProcessBuffStackRemoval(caster, caster, i);
        //        }
        //        else
        //        {
        //            ProcessBuffStackRemoval(caster, caster, i);
        //        }
        //    }
        //}

    }

    //Checks if it should remove buff stacks and bounce or just remove buff stacks
    private void ProcessBuffStackRemoval(Entity caster, Entity target, int buffIndex)
    {
        if (target.buffInfo[buffIndex].buff.bounceBuffToClosestFriendly)
        {
            RemoveBuffStacksAndBounce(caster, target, buffIndex);
        }
        else
        {
            RemoveBuffStacks(target, buffIndex);
        }
    }


    //Removes 1 stack unless there's 1 stack left then it removes the buff
    private void RemoveBuffStacks(Entity caster, int buffIndex)
    {
        if (caster.buffInfo[buffIndex].currentStacks > 1)
        {
            var b = caster.buffInfo[buffIndex];
            b.currentStacks = caster.buffInfo[buffIndex].currentStacks - 1;
            caster.buffInfo[buffIndex] = b;
        }
        else
        {
            caster.GetComponent<BuffDebuffHandler>().RemoveBuff(buffIndex);
        }
    }


    //Uses a heal ability and then bounces to nearest party member if there is 1 otherwise just removes the buff
    private void RemoveBuffStacksAndBounce(Entity caster, Entity target, int buffIndex)
    {
        var colliders = Physics.OverlapSphere(target.transform.position, target.buffInfo[buffIndex].buff.range);
        var closestMember = FindClosestFriendlyMember(target, colliders, buffIndex);

        target.buffInfo[buffIndex].buff.healAppliedOnBounce.UseAbility(caster, target);

        if (closestMember != null)
        {
            BounceBuffToClosestMember(target, buffIndex, closestMember);
        }
        else
        {
            target.GetComponent<Entity>().buffDebuffHandler.RemoveBuff(buffIndex);
        }
    }

    
    //Takes buff from caster and applies it to the nearest party member
    private void BounceBuffToClosestMember(Entity caster, int buffIndex, Entity closestMember)
    {
        var buff = caster.buffInfo[buffIndex].buff.data;

        // Create the bounce visual effect
        var visual = Instantiate(buff.bounceEffect.gameObject, caster.transform.position, Quaternion.identity);
        SceneManager.MoveGameObjectToScene(visual, caster.gameObject.scene);
        visual.GetComponent<BounceEffect>().caster = caster;
        visual.GetComponent<BounceEffect>().spellTarget = closestMember;
        NetworkServer.Spawn(visual);

        if (caster.buffInfo[buffIndex].currentStacks > 1)
        {
            // Add the buff to the closest member with one less stack
            closestMember.buffDebuffHandler.AddOrRefreshBuff(new Buff(caster.buffInfo[buffIndex].buff.data), closestMember, customStacks: caster.buffInfo[buffIndex].currentStacks - 1);
        }

        caster.GetComponent<BuffDebuffHandler>().RemoveBuff(buffIndex);
    }


    //Removes ALL stacks of a buff
    public void RemoveBuff(int i)
    {
        player.buffInfo.Remove(player.buffInfo[i]);
    }

    // called by client |||||||   For right clicking buffs off in your UI
    [Command]
    public void ManualRemoveBuff(int i)
    {
        player.buffInfo.Remove(player.buffInfo[i]);
    }


    #endregion Adding & Refreshing Buffs


    #region Adding & Refreshing Debuffs



    // see buffs above, it's identical.
    public void AddOrRefreshDebuff(Debuff debuff, Entity caster, float customDamage = 0, bool alreadySpread = false, int customStacks = 0,
        BuffApplication.AddStacksCondition addStacksCondition = BuffApplication.AddStacksCondition.None)
    {
        //float ticks = debuff.buffTime / (debuff.tickrate / (1 + Haste/100));
        //float duration = (debuff.tickrate / (1 + Haste / 100)) * ticks;
        if (player.ImmuneToCC(debuff.ccType.ToString())) return;

        var index = GetDebuffIndexByName(debuff.name, caster.name);

        if (index != -1)
        {
            var d = player.debuffInfo[index];

            d.debuff = debuff;
            var ccApplied = player.lossOfControl.DRHandler(d);
            if (ccApplied)
            {
                caster.GetComponent<PlayerMatchmaking>().AddPlayerStats("ccDone", 1);
                GetComponent<PlayerMatchmaking>().AddPlayerStats("ccReceived", 1);
                
            }

            if (d.debuff.data.addStacksCondition == addStacksCondition)
            {
                if (d.currentStacks + d.debuff.data.addStacksAmount <= d.debuff.maxStacks)
                    d.currentStacks += d.debuff.data.addStacksAmount;
                else
                    d.currentStacks = d.debuff.maxStacks;
            }

            d.dotDamage = customDamage * d.currentStacks;
            player.debuffInfo[index] = d;
            //debuffs[index] = debuff;
        }
        else
        {
            var d = new DebuffInfo
            {
                currentStacks = customStacks != 0 ? customStacks : debuff.beginningStacks,
                debuff = debuff,
                debuffedBy = caster.name,
                alreadySpread = alreadySpread
            };
            d.debuff.active = false;
            d.dotDamage = customDamage * d.currentStacks;
            var ccApplied = player.lossOfControl.DRHandler(d);
            if (ccApplied)
            {
                caster.GetComponent<PlayerMatchmaking>().AddPlayerStats("ccDone", 1);
                GetComponent<PlayerMatchmaking>().AddPlayerStats("ccReceived", 1);
                ;
            }
            //d.debuff.buffTimeSet = lossOfControl.DRBuffTimeChange(d.debuff);
            player.debuffInfo.Add(d);
            ApplyDebuffOverTime(d.debuff, debuff.abilitySchool, caster);
        }
    }


    private IEnumerator DebuffOverTime(Debuff debuff, AbilitySO.AbilitySchool abilityType, Entity caster)
    {
        var index = GetDebuffIndexByName(debuff.name, caster.name);

        if (debuff.ccType != AbilitySO.CCType.None) yield break;
        while (debuff.active && index != -1)
        {
            if (debuff.damage != 0) player.damageHandler.DealDamageAt(caster, debuff.damage * player.debuffInfo[index].currentStacks, abilityType, debuff.name, index, "debuff");
            if (debuff.healing != 0) player.healingHandler.DealHealingAt(caster, debuff.healing * player.debuffInfo[index].currentStacks, abilityType, debuff.name, index, "debuff");
            yield return new WaitForSeconds(debuff.Tickrate(caster));
            index = GetDebuffIndexByName(debuff.name, caster.name);
        }
    }

    //Bool "debuff.active / buff.active" I created in the debuffs struct. It's passively set to true whenever the buff exists.
    //I set it to false in AddOrRefreshDebuff RIGHT before I call this function and then it instantly goes back to true
    //and since it's always true after you activate it the coroutine never gets called again which means 
    //no multiple coroutines for 1 debuff. The only way a new coroutine can be started is if the initial one 
    //ended and you are adding a brand new debuff to buffs

    public void ApplyDebuffOverTime(Debuff debuff, AbilitySO.AbilitySchool abilityType, Entity caster)
    {
        if (!debuff.active)
        {
            debuff.active = true;
            StartCoroutine(DebuffOverTime(debuff, abilityType, caster));
        }
        else
        {
            debuff.active = true;
        }
    }


    public void DebuffStackRemoval(Entity caster, Entity target, BuffApplication.BuffRemovalCondition buffRemovalCondition, string abilityName = null)
    {
        if (caster == null) return;

        for (var i = 0; i < target.debuffInfo.Count; i++)
        {
            if (buffRemovalCondition == target.debuffInfo[i].debuff.debuffRemovalCondition)
            {
                ProcessDebuffStackRemoval(caster, target, i);
            }
        }
    }

    private void ProcessDebuffStackRemoval(Entity caster, Entity target, int debuffIndex)
    {
        if (target.debuffInfo[debuffIndex].debuff.bounceDebuffToClosestEnemy)
            RemoveDebuffStacksAndBounce(caster, debuffIndex);
        if (target.debuffInfo[debuffIndex].debuff.dealDamageOnDamage)
            RemoveDebuffStacksAndDamage(caster, target, debuffIndex);
        else
            RemoveDebuffStacks(caster, debuffIndex);
    }

    private void RemoveDebuffStacks(Entity caster, int debuffIndex)
    {
        if (caster.debuffInfo[debuffIndex].currentStacks > 1)
        {
            var d = caster.debuffInfo[debuffIndex];
            d.currentStacks = caster.debuffInfo[debuffIndex].currentStacks - 1;
            caster.debuffInfo[debuffIndex] = d;
        }
        else
        {
            caster.GetComponent<BuffDebuffHandler>().RemoveDebuff(debuffIndex);
        }
    }

    private void RemoveDebuffStacksAndDamage(Entity caster, Entity target, int debuffIndex)
    {
        if (target.debuffInfo[debuffIndex].debuffedBy == caster.name)
        {
            caster.stackCounter++;
            if (caster.stackCounter >= 5)
            {
                foreach (var ability in target.debuffInfo[debuffIndex].debuff.data.cooldownReducee)
                {
                    var abilityIndex = caster.GetAbilityIndexByName(ability.name);
                    caster.cdReduceIndex = abilityIndex;
                    var abil = caster.abilities[caster.cdReduceIndex];
                    abil.cooldownEnd -= target.debuffInfo[debuffIndex].debuff.data.cooldownReduceAmount;
                    caster.abilities[caster.cdReduceIndex] = abil;
                }
                caster.stackCounter = 0;
            }
            target.debuffInfo[debuffIndex].debuff.damageAppliedOnHit.UseAbility(caster, target);
            target.GetComponent<Entity>().buffDebuffHandler.RemoveDebuffStacks(target, debuffIndex);
        }
        
    }


    //Need to Add Bouncing if we have a debuff that bounces
    private void RemoveDebuffStacksAndBounce(Entity caster, int debuffIndex)
    {
        var colliders = Physics.OverlapSphere(caster.transform.position, caster.debuffInfo[debuffIndex].debuff.range);
        var closestMember = FindClosestFriendlyMember(caster, colliders, debuffIndex);

        caster.debuffInfo[debuffIndex].debuff.damageAppliedOnHit.UseAbility(caster, caster);

        if (closestMember != null)
        {
            BounceDebuffToClosestMember(caster, debuffIndex, closestMember);
        }
        else
        {
            caster.GetComponent<Entity>().buffDebuffHandler.RemoveDebuff(debuffIndex);
        }
    }

    private void BounceDebuffToClosestMember(Entity caster, int debuffIndex, Entity closestMember)
    {
        var debuff = caster.debuffInfo[debuffIndex].debuff.data;

        // Create the bounce visual effect
        var visual = Instantiate(debuff.bounceEffect.gameObject, caster.transform.position, Quaternion.identity);
        SceneManager.MoveGameObjectToScene(visual, caster.gameObject.scene);
        visual.GetComponent<BounceEffect>().caster = caster;
        visual.GetComponent<BounceEffect>().spellTarget = closestMember;
        NetworkServer.Spawn(visual);

        if (caster.debuffInfo[debuffIndex].currentStacks > 1)
        {
            // Add the buff to the closest member with one less stack
            closestMember.buffDebuffHandler.AddOrRefreshDebuff(new Debuff(caster.debuffInfo[debuffIndex].debuff.data), closestMember,
                customStacks: caster.debuffInfo[debuffIndex].currentStacks - 1);
        }

        caster.GetComponent<BuffDebuffHandler>().RemoveDebuff(debuffIndex);
    }


    public void RemoveDebuff(int i)
    {
        player.debuffInfo.Remove(player.debuffInfo[i]);
    }

    // called by client
    [Command]
    public void ManualRemoveDebuff(int i)
    {
        player.debuffInfo.Remove(player.debuffInfo[i]);
    }

    #endregion Adding & Refreshing Debuffs


    #region Check Buffs For Dmg/Heal Increases

    public float CheckBuffsDamage(Entity entity, float damageDone)
    {
        // check localPlayer's buffs
        for (int i = 0; i < player.buffInfo.Count; i++)
        {


            // does the player have a buff that provides a strict damage increase?
            if (player.buffInfo[i].buff.damageDoneIncrease > 0f)
            {
                damageDone = damageDone * (1 + player.buffInfo[i].buff.damageDoneIncrease * player.buffInfo[i].currentStacks / 100f);
            }
            // does the player have a buff that provides a strict damage reduction?
            if (player.buffInfo[i].buff.damageDoneReduction > 0f)
            {
                damageDone = damageDone * (1 - player.buffInfo[i].buff.damageDoneIncrease * player.buffInfo[i].currentStacks / 100f);
            }
        }

        foreach (var item in entity.buffInfo)
        {
            // does the enemy have a damage taken increase buff?
            if (item.buff.damageTakenIncrease > 0f) damageDone = damageDone * (1 + item.buff.damageTakenIncrease * item.currentStacks / 100f);
            // does the enemy have a damage taken reduction buff?
            if (item.buff.damageTakenReduction > 0f) damageDone = damageDone * (1 + item.buff.damageTakenIncrease * item.currentStacks / 100);
        }

        return damageDone;
    }



    public float CheckBuffsHealing(Entity entity, float healingDone, string abilityName)
    {
        {
            // check localPlayer's buffs
            foreach (var item in player.buffInfo)
            {
                if (item.buff.healingDoneIncrease > 0f) healingDone = healingDone * (1 + item.buff.healingDoneIncrease * item.currentStacks / 100f);
                if (item.buff.healingDoneReduction > 0f) healingDone = healingDone * (1 - item.buff.healingDoneIncrease * item.currentStacks / 100f);
            }

            foreach (var item in entity.buffInfo)
            {
                if (item.buff.healingTakenIncrease > 0f) healingDone = healingDone * (1 + item.buff.healingTakenIncrease * item.currentStacks / 100f);
                if (item.buff.healingTakenReduction > 0f) healingDone = healingDone * (1 + item.buff.healingTakenIncrease * item.currentStacks / 100);
            }

            foreach (var item in entity.passiveAbilities.Where(item => item.ability.increaseOutputForSpecificAbilities))
                if (item.ability.requireTargetToHaveSpecificBuff)
                {
                    foreach (var itemzz in entity.buffInfo)
                        if (itemzz.buff.data.abilityName == item.ability.requiredBuffForProc.abilityName)
                            healingDone = healingDone + healingDone * item.ability.outputIncreaseAsPercentage / 100;
                }
                else
                {
                    foreach (var itemz in item.ability.specificAbilitiesForOutputIncrease)
                        if (itemz.abilityName == abilityName)
                            healingDone = healingDone + healingDone * item.ability.outputIncreaseAsPercentage / 100;
                }

            return healingDone;
        }
    }

    #endregion Check Buffs For Dmg/Heal Increases


    #region Add & Remove Abilities

    // some talents will cause abilities to be 'modified'.
    // in practice, instead of modifying, we have a 2nd ability that replaces the first.
    [Command]
    public void AddAbility(string wantedTalentName, string addType)
    {
        foreach (var talent in player.allTalents.Where(talent => talent.abilityName == wantedTalentName))
            switch (addType)
            {
                case "original":
                    player.abilities.Add(new Ability(talent.originalSpell));
                    break;
                case "altered":
                    player.abilities.Add(new Ability(talent.alteredSpell));
                    break;
            }
    }

    // read above
    [Command]
    public void RemoveAbility(string wantedTalentName, string removeType)
    {
        foreach (var talent in player.allTalents.Where(talent => talent.abilityName == wantedTalentName))
            switch (removeType)
            {
                case "original":
                    {
                        var original = player.abilities.Find(x => x.abilityName == talent.originalSpell.abilityName);
                        player.abilities.Remove(original);
                        break;
                    }
                case "altered":
                    {
                        var altered = player.abilities.Find(x => x.abilityName == talent.alteredSpell.abilityName);
                        player.abilities.Remove(altered);
                        break;
                    }
            }
    }

    #endregion


    #region Find Closest Friendly

    private Entity FindClosestFriendlyMember(Entity caster, Collider[] colliders, int buffIndex)
    {
        Entity closestMember = null;
        float closestDistance = Mathf.Infinity;

        foreach (var collision in colliders)
        {
            var currentEntity = collision.GetComponentInParent<Entity>();
            if (currentEntity && currentEntity.transform.root.name != caster.name &&
                !caster.unfriendlyPlayers.Contains(currentEntity.transform.root.name))
            {
                var currentDistance = Utils.ClosestDistance(caster, currentEntity);
                if (currentDistance < closestDistance)
                {
                    closestDistance = currentDistance;
                    closestMember = currentEntity;
                }
            }
        }

        return closestDistance < caster.buffInfo[buffIndex].buff.range ? closestMember : null;
    }

    #endregion Find Closest Friendly


    #region Buff & Debuff Cleanup

    public void CleanupBuffs()
    {
        for (var i = 0; i < player.buffInfo.Count; ++i)
        {
            //var shieldIndex = GetShieldIndexByName(buffInfo[i].buff.name, buffInfo[i].buffedBy);
            // if buff expires OR the buff was a shield and it was broken, remove it.
            if (player.buffInfo[i].buff.BuffTimeRemaining() != 0 /*&& (shieldIndex == -1 || !(shields[shieldIndex].currentShield <= 0))*/) continue;
            BuffExpiryLogic(player.buffInfo[i]);
            player.buffInfo.RemoveAt(i);
            //if (shieldIndex != -1)
            //    shields.RemoveAt(shieldIndex);
            --i;
        }
    }


    // same as above
    public void CleanupDebuffs()
    {
        for (var i = 0; i < player.debuffInfo.Count; ++i)
        {
            //if debuff expires, remove it.
            if (player.debuffInfo[i].debuff.DebuffTimeRemaining() == 0)
            {
                DebuffExpiryLogic(player.debuffInfo[i]);
                player.debuffInfo.RemoveAt(i);
                --i;
            }

            // test for CC threshhold. if the damage taken during that CC > allowed damage, break the CC.
            else if (player.debuffInfo[i].debuff.ccType != AbilitySO.CCType.None && player.debuffInfo[i].debuff.damageThresholdInCrowdControl != 0)
            {
                if (player.debuffInfo[i].damageTaken >= player.debuffInfo[i].debuff.damageThresholdInCrowdControl)
                {
                    player.debuffInfo.RemoveAt(i);
                    --i;
                }
            }
        }



    }

    // does the debuff spread to other targets on expiry?
    public void DebuffExpiryLogic(DebuffInfo debuffInfo)
    {
        if (!debuffInfo.debuff.spreadToAoeTargetsOnExpiry) return;
        var col = Physics.OverlapSphere(transform.position, debuffInfo.debuff.spreadBuffDebuffOnExpiryRange);
        var overlapSphereTargetList = new List<Entity>();
        foreach (var collision in col)
            if (collision.GetComponentInParent<Entity>() && !overlapSphereTargetList.Contains(collision.GetComponentInParent<Entity>())
                                                         && collision.transform.root.name != Player.localPlayer.name && player.unfriendlyPlayers.Contains(collision.transform.root.name))
                overlapSphereTargetList.Add(collision.GetComponentInParent<Entity>());

        foreach (var target in overlapSphereTargetList)
        {
            Player.onlinePlayers.TryGetValue(debuffInfo.debuffedBy, out var p);

            if (!debuffInfo.alreadySpread) target.buffDebuffHandler.AddOrRefreshDebuff(new Debuff(debuffInfo.debuff.data), p, debuffInfo.dotDamage, true);
        }
    }

    // does the buff spread to other friendlies on expiry?
    public void BuffExpiryLogic(BuffInfo buffInfo)
    {
        // Check if the buff should spread to AOE targets on expiry
        if (buffInfo.buff.spreadToAoeTargetsOnExpiry)
        {
            // Get all entities within a certain range of the current entity
            var colliders = Physics.OverlapSphere(transform.position, buffInfo.buff.spreadBuffDebuffOnExpiryRange);
            var targetList = new List<Entity>();

            // Add entities that are not the local player and are unfriendly to the target list
            foreach (var collider in colliders)
            {
                var target = collider.GetComponentInParent<Entity>();
                if (target != null && !targetList.Contains(target) && collider.transform.root.name != Player.localPlayer.name && player.unfriendlyPlayers.Contains(collider.transform.root.name))
                {
                    targetList.Add(target);
                }
            }

            // Add the buff to the targets in the target list
            foreach (var target in targetList)
            {
                Player.onlinePlayers.TryGetValue(buffInfo.buffedBy, out var player);
                if (!buffInfo.alreadySpread)
                {
                    target.buffDebuffHandler.AddOrRefreshBuff(new Buff(buffInfo.buff.data), player, 0, true);
                }
            }
        }

        // Check if the buff suspends health
        if (buffInfo.buff.suspendHealth)
        {
            // Calculate the change in health and apply it
            var healthChange = Mathf.Abs(buffInfo.damageSuspended - buffInfo.healingSuspended);
            if (buffInfo.damageSuspended > buffInfo.healingSuspended)
            {
                player.Health -= healthChange;
            }
            else
            {
                player.Health += healthChange;
            }
        }
    }

    #endregion Buff & Debuff Cleanup


    #region Update Buffs & Debuffs After Cast


    // add to player stats, remove buffs, update passives
    public void UpdateStatsBuffsPassives(Entity caster, string abilityName, AbilitySO.AbilitySchool abilityType, float finalOutput, int index, string buffOrDebuff, string damageOrHealing)
    {
        // should any buffs be applied to the caster?
        CheckForBuffApplications(caster, abilityType);
        CheckForBuffRemovals(caster, abilityName, damageOrHealing);
        
        // should any of the enemy's debuff stacks be increased?
        CheckBuffStackChanges(abilityName, caster);


        DealDamageOnDebuffHit(caster, abilityName, damageOrHealing);


        UpdateDebuffVariables(finalOutput, caster);
        UpdateBuffVariablesAndPassives(caster, abilityName, finalOutput, index, buffOrDebuff, damageOrHealing);
        UpdateArenaStatistics(caster, finalOutput);
    }

    public void DealDamageOnDebuffHit(Entity caster, string abilityName, string damageOrHealing)
    {
        CheckForDebuffRemovals(caster, player, abilityName, damageOrHealing);
        CheckDebuffStackChanges(abilityName, caster);

    }


    //Freezes health and calculated damage taken vs healing taken and once the buff expires causes the calculated amount to happen
    public void SuspendUpdateBuffInfo(string damageOrHealing = "", float amount = 0)
    {
        for (var i = 0; i < player.buffInfo.Count; i++)
        {
            var currentBuff = player.buffInfo[i];
            if (currentBuff.buff.suspendHealth)
            {
                if (damageOrHealing == "damage")
                {
                    currentBuff.damageSuspended += amount;  
                }
                else if (damageOrHealing == "healing")
                {
                    currentBuff.healingSuspended += amount;
                }
                player.buffInfo[i] = currentBuff;
            }
        }
    }

    //Adds buff stacks if there's a buff that is stackable and the spell cast adds stacks of it
    public void CheckBuffStackChanges(string abilityName, Entity caster)
    {
        foreach (var item in from item in player.buffInfo from abil in item.buff.data.stackAddingAbilities where abil.name == abilityName select item)
            AddOrRefreshBuff(item.buff, caster, customStacks: item.currentStacks + item.buff.data.addStacksAmount, 
                addStacksCondition: BuffApplication.AddStacksCondition.OnAbilityCasted);
    }

    //Adds debuff stacks if there's a debuff that is stackable and the spell cast adds stacks of it
    public void CheckDebuffStackChanges(string abilityName, Entity caster)
    {
        foreach (var item in from item in player.debuffInfo from abil in item.debuff.data.stackAddingAbilities where abil.name == abilityName select item)
            AddOrRefreshDebuff(item.debuff, caster, customStacks: item.currentStacks + item.debuff.data.addStacksAmount, 
                addStacksCondition: BuffApplication.AddStacksCondition.OnAbilityLanded);
    }

    // needs more work.
    // checks to see if any buffs or debuffs should be applied when damage is done
    public void CheckForBuffApplications(Entity caster, AbilitySO.AbilitySchool abilityType)
    {
        foreach (var debuff in from item in player.buffInfo
                               where item.buff.applyDebuffOnMeleeHitAgainstMe && abilityType == AbilitySO.AbilitySchool.Physical
                               let rngCheck = Random.Range(0f, 100f)
                               where rngCheck <= item.buff.applyDebuffOnMeleeProcPercentage
                               from DebuffSO debuff in item.buff.data.debuffs
                               select debuff)
            caster.buffDebuffHandler.AddOrRefreshDebuff(new Debuff(debuff), caster);
    }

    //Checks for damage done or taken to remove a buff
    public void DamageCheckForBuffRemoval(Entity caster, Entity target, string abilityName)
    {
        BuffStackRemoval(caster, target, BuffApplication.BuffRemovalCondition.DamageTaken);
        //caster.buffDebuffHandler.BuffStackRemoval(caster, target, BuffApplication.BuffRemovalCondition.SpecificSpell, abilityName);
        caster.buffDebuffHandler.BuffStackRemoval(caster, target, BuffApplication.BuffRemovalCondition.AnySpell, abilityName);
        BuffStackRemoval(caster, target, BuffApplication.BuffRemovalCondition.SpecificSpell, abilityName);
        // should any player buffs be removed when damage is dealt?
        caster.buffDebuffHandler.BuffStackRemoval(caster, target, BuffApplication.BuffRemovalCondition.DamageDone);

        if (caster.GetAbilityIndexByName(abilityName) != -1)
        {
            if (caster.abilities[caster.GetAbilityIndexByName(abilityName)].CastTime(caster) > 0)
            {
                caster.buffDebuffHandler.BuffStackRemoval(caster, target, BuffApplication.BuffRemovalCondition.CastTimeGreaterThanX);
            }

        }
    }


    //Checks for Healing done or taken to remove a buff
    public void HealingCheckForBuffRemoval(Entity caster, Entity target, string abilityName)
    {
        BuffStackRemoval(caster, target, BuffApplication.BuffRemovalCondition.HealingTaken);
        caster.buffDebuffHandler.BuffStackRemoval(caster, target, BuffApplication.BuffRemovalCondition.SpecificSpell, abilityName);
        caster.buffDebuffHandler.BuffStackRemoval(caster, target, BuffApplication.BuffRemovalCondition.AnySpell, abilityName);
        // should any player buffs be removed when damage is dealt?
        caster.buffDebuffHandler.BuffStackRemoval(caster, target, BuffApplication.BuffRemovalCondition.HealingDone);

        if (caster.GetAbilityIndexByName(abilityName) != -1)
        {
            if (caster.abilities[caster.GetAbilityIndexByName(abilityName)].CastTime(caster) > 0)
            {
                caster.buffDebuffHandler.BuffStackRemoval(caster, target, BuffApplication.BuffRemovalCondition.CastTimeGreaterThanX);
            }

        }
    }

    public void CheckForBuffRemovals(Entity caster, string abilityName, string damageOrHealing)
    {
        if (damageOrHealing == "damage")
            DamageCheckForBuffRemoval(caster, player, abilityName);
        else if (damageOrHealing == "healing")
            HealingCheckForBuffRemoval(caster, player, abilityName);
    }


    //Checks all damage done related passives, if you do 5,000 damage and have a passive up that 
    //every 5000 damage fires an extra spell this will check for that condition
    //Checks for atonement damage to healing as well
    public void UpdatePassiveAbilityDamageDone(Entity caster, string abilityName, float finalOutput, int index, string buffOrDebuff)
    {

        caster.passiveHandler.UpdatePassiveAbilityVariables(player, finalOutput, "Atonement", abilityName);

        // update passive variables based on actions taken (e.g. when damage is done, update all passives that require damage done to proc an effect).
        caster.passiveHandler.UpdatePassiveAbilityVariables(player, 0, "AbilityUse", abilityName);
        caster.passiveHandler.UpdatePassiveAbilityVariables(player, finalOutput, caster.IsEnemy(player) ? "LastEnemyTarget" : "LastFriendlyTarget", abilityName);
        caster.passiveHandler.UpdatePassiveAbilityVariables(player, finalOutput, "DamageDone", abilityName);
        caster.passiveHandler.UpdatePassiveAbilityVariables(caster, finalOutput, "OutputToTargetBelowHealthPercentage", abilityName, player.HealthPercent());

        // index is only passed as non -1 if it's a buff/debuff.
        if (player.IsBot(player.name)) return;
        if (index == -1) return;
        player.passiveHandler.UpdatePassiveAbilityVariables(caster, finalOutput, "BuffOrDebuffTick", buffOrDebuff == "buff" 
            ? player.buffInfo[index].buff.name : player.debuffInfo[index].debuff.name);
    }

    //Checks all damage done related passives, if you do 5,000 healing and have a passive up that 
    //every 5000 healing fires an extra spell this will check for that condition
    public void UpdatePassiveAbilityHealingDone(Entity caster, string abilityName, float finalOutput, int index, string buffOrDebuff)
    {
        caster.passiveHandler.UpdatePassiveAbilityVariables(player, 0, "AbilityUse", abilityName);
        caster.passiveHandler.UpdatePassiveAbilityVariables(player, finalOutput, caster.IsEnemy(player) ? "LastEnemyTarget" : "LastFriendlyTarget", abilityName);
        caster.passiveHandler.UpdatePassiveAbilityVariables(player, finalOutput, "HealingDone", abilityName);
        caster.passiveHandler.UpdatePassiveAbilityVariables(caster, finalOutput, "OutputToTargetBelowHealthPercentage", abilityName, player.HealthPercent());

        // index is only passed as non -1 if it's a buff/debuff.
        if (index == -1) return;
        player.passiveHandler.UpdatePassiveAbilityVariables(caster, finalOutput, "BuffOrDebuffTick", buffOrDebuff == "buff" ? player.buffInfo[index].buff.name : player.debuffInfo[index].debuff.name);
    }


    //Sends buff passives for main check
    public void UpdateBuffVariablesAndPassives(Entity caster, string abilityName, float finalOutput, int index, string buffOrDebuff, string damageOrHealing)
    {
        if (damageOrHealing == "damage")
            UpdatePassiveAbilityDamageDone(caster, abilityName, finalOutput, index, buffOrDebuff);
        else if (damageOrHealing == "healing")
            UpdatePassiveAbilityHealingDone(caster, abilityName, finalOutput, index, buffOrDebuff);

    }

    public void UpdateArenaStatistics(Entity caster, float finalDamage)
    {
        if (player.CheckForCCBreak(player, finalDamage))
        {
            caster.GetComponent<PlayerMatchmaking>().AddPlayerStats("ccBreaks", 1);
        }
        // if player is in arena, count this damage towards their final damage total.
        caster.GetComponent<PlayerMatchmaking>().AddPlayerStats("damageDone", finalDamage);
    }

    public void UpdateDebuffVariables(float damageTaken, Entity caster, string abilityName = "")
    {
        for (var i = 0; i < player.debuffInfo.Count; i++)
        {
            if (player.debuffInfo[i].debuff.ccType != AbilitySO.CCType.None)
            {
                var d = player.debuffInfo[i];
                d.damageTaken += damageTaken;
                player.debuffInfo[i] = d;
            }

            if (player.debuffInfo[i].debuff.data.addStacksCondition == BuffApplication.AddStacksCondition.OnAbilityLanded)
            {
                foreach (var ability in player.debuffInfo[i].debuff.data.stackAddingAbilities)
                {
                    if (ability.name == abilityName)
                    {
                        AddOrRefreshDebuff(player.debuffInfo[i].debuff, caster, customStacks: player.debuffInfo[i].currentStacks + player.debuffInfo[i].debuff.data.addStacksAmount,
                        addStacksCondition: BuffApplication.AddStacksCondition.OnAbilityLanded);
                    }

                }
            }
        }

    }


    //Checks for damage done or taken to remove a buff
    public void DamageCheckForDebuffRemoval(Entity caster, Entity target, string abilityName)
    {

        DebuffStackRemoval(caster, target, BuffApplication.BuffRemovalCondition.DamageTaken);
        DebuffStackRemoval(caster, target, BuffApplication.BuffRemovalCondition.DamageDone);
        foreach (var debuff in target.debuffInfo)
        {
            if (debuff.debuff.debuffRemovalCondition == BuffApplication.BuffRemovalCondition.SpecificSpell)
            {
                foreach (var ability in debuff.debuff.specificAbilitiesForRemoval)
                    if (abilityName == ability.name)
                    {
                        DebuffStackRemoval(caster, target, BuffApplication.BuffRemovalCondition.SpecificSpell, abilityName);
                    }
            }
        }
        //DebuffStackRemoval(player, BuffApplication.BuffRemovalCondition.SpecificSpell, abilityName);

        //if (caster.GetAbilityIndexByName(abilityName) != -1)
        //{
        //    if (caster.abilities[caster.GetAbilityIndexByName(abilityName)].CastTime(caster) > 0)
        //    {
        //        caster.buffDebuffHandler.DebuffStackRemoval(caster, BuffApplication.BuffRemovalCondition.CastTimeGreaterThanX);
        //    }

        //}
    }


    //Checks for Healing done or taken to remove a buff
    public void HealingCheckForDebuffRemoval(Entity caster, Entity target, string abilityName)
    {
        DebuffStackRemoval(caster, target, BuffApplication.BuffRemovalCondition.HealingTaken);
        caster.buffDebuffHandler.DebuffStackRemoval(caster, target, BuffApplication.BuffRemovalCondition.SpecificSpell, abilityName);
        caster.buffDebuffHandler.DebuffStackRemoval(caster, target, BuffApplication.BuffRemovalCondition.AnySpell, abilityName);
        // should any player buffs be removed when damage is dealt?
        caster.buffDebuffHandler.DebuffStackRemoval(caster, target, BuffApplication.BuffRemovalCondition.HealingDone);

        if (caster.GetAbilityIndexByName(abilityName) != -1)
        {
            if (caster.abilities[caster.GetAbilityIndexByName(abilityName)].CastTime(caster) > 0)
            {
                caster.buffDebuffHandler.DebuffStackRemoval(caster, target, BuffApplication.BuffRemovalCondition.CastTimeGreaterThanX);
            }

        }
    }

    public void CheckForDebuffRemovals(Entity caster, Entity target, string abilityName, string damageOrHealing)
    {
        if (damageOrHealing == "damage")
            DamageCheckForDebuffRemoval(caster, target, abilityName);
        else if (damageOrHealing == "healing")
            HealingCheckForDebuffRemoval(caster, target, abilityName);
    }

    #endregion Update Buffs & Debuffs After Cast


    #region Get By Name

    public int GetBuffIndexByName(string buffName, string casterName = null)
    {
        for (var i = 0; i < player.buffInfo.Count; ++i)
        {
            if (casterName != null)
            {
                if (player.buffInfo[i].buffedBy == casterName && player.buffInfo[i].buff.name == buffName)
                    return i;
            }
            else
                if (player.buffInfo[i].buff.name == buffName)
                return i;
        }
        return -1;


        //Keeping around for non-generic only indexer

        //for (var i = 0; i < player.buffInfo.Count; ++i)
        //    if (player.buffInfo[i].buffedBy == casterName && player.buffInfo[i].buff.name == buffName)
        //        return i;
        //return -1;
    }

    public int GetDebuffIndexByName(string debuffName, string casterName)
    {
        for (var i = 0; i < player.debuffInfo.Count; ++i)
            if (player.debuffInfo[i].debuffedBy == casterName && player.debuffInfo[i].debuff.name == debuffName)
                return i;
        return -1;
    }

    #endregion Get By Name


    #region Permanent Buffs/Debuffs/Passives

    // some buffs in our game should be automatically applied to all party members if they are in range of the buffed player.
    // this checks to see if any should be applied
    public void PermanentBuffLogic()
    {
        for (var i = 0; i < player.buffInfo.Count; i++)
        {
            var temp = player.buffInfo[i].buff.name;
            if (player.buffInfo[i].buff.buffAoeAlways && player.buffInfo[i].buffedBy == name && !Player.localPlayer.playerFunctions.party.NotInParty())
                foreach (var member in Player.localPlayer.playerFunctions.party.members)
                {
                    Player.onlinePlayers.TryGetValue(member, out var player);
                    var buffAlreadyExists = false;
                    if (Utils.ClosestDistance(player, player) < player.buffInfo[i].buff.buffAoeRange)
                    {
                        foreach (var memberBuff in player.buffInfo)
                            if (memberBuff.buff.name == temp)
                                buffAlreadyExists = true;
                        if (!buffAlreadyExists)
                            //player.buffs.Add(new Buffs(buffs[i].data));
                            //player.buffedBy.Add(this.name);
                            player.buffDebuffHandler.AddOrRefreshBuff(new Buff(player.buffInfo[i].buff.data), player);
                    }
                    else
                    {
                        for (var j = 0; j < player.buffInfo.Count; j++)
                            if (player.buffInfo[j].buff.name == temp)
                                player.buffInfo.Remove(player.buffInfo[j]);
                        //player.buffedBy.Remove(player.buffedBy[j]);
                        //player.buffStackSize.Remove(player.buffStackSize[j]);
                    }
                }
        }
    }

    public void PermanentPassiveLogic()
    {
        for (var i = 0; i < player.passiveAbilities.Count; i++)
        {
            if (player.passiveAbilities[i].ability.procType == PassiveAbilitySO.ProcType.Permanent)
            {
                var alreadyExists = false;
                foreach (var buff in player.buffInfo.Where(buff => buff.buff.name == player.passiveAbilities[i].ability.permanentBuffSO.abilityName))
                    alreadyExists = true;
                if (!alreadyExists) buffDebuffHandler.AddPermanentBuff(i);
            }

            if (player.passiveAbilities[i].ability.procType == PassiveAbilitySO.ProcType.CooldownReduction)
            {
                for (int j = 0; j < player.abilities.Count; j++)
                {
                    if (player.abilities[j].data.abilityName == player.passiveAbilities[i].ability.cooldownReductionSO.name)
                    {
                        if (player.abilities[j].cooldownReduction == 0)
                        {
                            Ability tempAbility = new Ability(player.passiveAbilities[i].ability.cooldownReductionSO, 5);
                            //tempAbility.cooldownEnd = NetworkTime.time + passiveAbilities[i].ability.data.cooldown;
                            player.abilities[j] = tempAbility;
                        }
                    }
                }
            }
        }
    }




    #endregion Permanent Buffs/Debuffs/Passives

}














////So buff.active becomes true the second a buff gets applied and the index
////will check for when the buff falls off, neither of those checks conflict
////with any timers or refreshing the buff or debuff and thus the dots / hots work perfectly now

//public void CheckBuffRemoval(Entity caster, BuffApplication.BuffRemovalCondition buffRemovalCondition, string abilityName = null)
//{
//    if (caster != null)
//        for (var i = 0; i < caster.buffInfo.Count; i++)
//        {           
//            if (buffRemovalCondition == caster.buffInfo[i].buff.buffRemovalCondition)
//            {
//                if (caster.buffInfo[i].buff.bounceBuffToClosestFriendly)
//                {
//                    BounceBuffs(i);
//                }
//                else if (caster.buffInfo[i].currentStacks > 1)
//                {
//                    var b = caster.buffInfo[i];
//                    b.currentStacks = caster.buffInfo[i].currentStacks - 1;
//                    caster.buffInfo[i] = b;
//                }
//                else
//                {
//                    caster.GetComponent<BuffDebuffHandler>().RemoveBuff(i);
//                    i--;
//                }
//            }
//        }



//    // find the closest party member, bounce the buff to them and remove it from the original player
//    void BounceBuffs(int buffIndex)
//    {
//        // Get all colliders within the bounce range
//        var colliders = Physics.OverlapSphere(caster.transform.position, caster.buffInfo[buffIndex].buff.range);
//        Entity closestMember = null;
//        AbilitySO buff = caster.buffInfo[buffIndex].buff.data;
//        float closestDistance = 1000f;

//        // Apply the heal effect on the caster
//        caster.buffInfo[buffIndex].buff.healAppliedOnBounce.UseAbility(caster, caster);

//        // Find the closest friendly member to bounce the buff to
//        foreach (var collision in colliders)
//        {
//            var currentEntity = collision.GetComponentInParent<Entity>();
//            if (currentEntity && currentEntity.transform.root.name != caster.name &&
//                !caster.unfriendlyPlayers.Contains(currentEntity.transform.root.name))
//            {
//                var currentDistance = Utils.ClosestDistance(caster, currentEntity);
//                if (currentDistance < closestDistance)
//                {
//                    closestDistance = currentDistance;
//                    closestMember = currentEntity;
//                }
//            }
//        }

//        // If a closest member was found and is within range, bounce the buff to that member
//        if (closestMember != null && closestDistance < caster.buffInfo[buffIndex].buff.range)
//        {
//            // If there are still stacks of the buff left, bounce it to the closest member
//            if (caster.buffInfo[buffIndex].currentStacks > 1)
//            {
//                // Create the bounce visual effect
//                var visual = Instantiate(buff.bounceEffect.gameObject, caster.transform.position, Quaternion.identity);
//                SceneManager.MoveGameObjectToScene(visual, caster.gameObject.scene);
//                visual.GetComponent<BounceEffect>().caster = caster;
//                visual.GetComponent<BounceEffect>().spellTarget = closestMember;
//                NetworkServer.Spawn(visual);

//                // Add the buff to the closest member with one less stack
//                closestMember.buffDebuffHandler.AddOrRefreshBuff(new Buff(caster.buffInfo[buffIndex].buff.data), closestMember, customStacks: caster.buffInfo[buffIndex].currentStacks - 1);
//            }
//            else
//            {
//                // Remove the buff from the caster if there are no more stacks left
//                caster.GetComponent<BuffDebuffHandler>().RemoveBuff(buffIndex);
//                return;
//            }

//            // Remove the buff from the caster after bouncing it
//            caster.GetComponent<BuffDebuffHandler>().RemoveBuff(buffIndex);
//        }
//        else
//        {
//            // Remove the buff from the caster if no closest member was found or is within range
//            caster.GetComponent<Player>().buffDebuffHandler.RemoveBuff(buffIndex);
//        }
//    }
//}



//private void BounceBuffs(Entity caster, int buffIndex)
//{
//    // Get all colliders within the bounce range
//    var colliders = Physics.OverlapSphere(caster.transform.position, caster.buffInfo[buffIndex].buff.range);
//    Entity closestMember = null;
//    AbilitySO buff = caster.buffInfo[buffIndex].buff.data;
//    float closestDistance = 1000f;

//    // Apply the heal effect on the caster
//    caster.buffInfo[buffIndex].buff.healAppliedOnBounce.UseAbility(caster, caster);

//    // Find the closest friendly member to bounce the buff to
//    foreach (var collision in colliders)
//    {
//        var currentEntity = collision.GetComponentInParent<Entity>();
//        if (currentEntity && currentEntity.transform.root.name != caster.name &&
//            !caster.unfriendlyPlayers.Contains(currentEntity.transform.root.name))
//        {
//            var currentDistance = Utils.ClosestDistance(caster, currentEntity);
//            if (currentDistance < closestDistance)
//            {
//                closestDistance = currentDistance;
//                closestMember = currentEntity;
//            }
//        }
//    }

//    // If a closest member was found and is within range, bounce the buff to that member
//    if (closestMember != null && closestDistance < caster.buffInfo[buffIndex].buff.range)
//    {
//        // If there are still stacks of the buff left, bounce it to the closest member
//        if (caster.buffInfo[buffIndex].currentStacks > 1)
//        {
//            // Create the bounce visual effect
//            var visual = Instantiate(buff.bounceEffect.gameObject, caster.transform.position, Quaternion.identity);
//            SceneManager.MoveGameObjectToScene(visual, caster.gameObject.scene);
//            visual.GetComponent<BounceEffect>().caster = caster;
//            visual.GetComponent<BounceEffect>().spellTarget = closestMember;
//            NetworkServer.Spawn(visual);

//            // Add the buff to the closest member with one less stack
//            closestMember.buffDebuffHandler.AddOrRefreshBuff(new Buff(caster.buffInfo[buffIndex].buff.data), closestMember, customStacks: caster.buffInfo[buffIndex].currentStacks - 1);
//        }
//        else
//        {
//            // Remove the buff from the caster if there are no more stacks left
//            caster.GetComponent<BuffDebuffHandler>().RemoveBuff(buffIndex);
//            return;
//        }

//        // Remove the buff from the caster after bouncing it
//        caster.GetComponent<BuffDebuffHandler>().RemoveBuff(buffIndex);
//    }
//    else
//    {
//        // Remove the buff from the caster if no closest member was found or is within range
//        caster.GetComponent<Player>().buffDebuffHandler.RemoveBuff(buffIndex);
//    }
//}