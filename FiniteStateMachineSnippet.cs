using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gaia;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;


    #region State Machine


    public void StateMachineLogic()
    {
        switch (state)
        {
            case "IDLE":
            case "MOVING":
                {
                    if (spectating)
                    {
                        playerMovement.Locomotion();
                    }
                    else if (UIManager.MyInstance.isInputEnabled && !disableMovement) // if typing, dont allow char to move
                    {
                        playerMovement.GetInputs();
                        playerMovement.Locomotion();
                    }
                    else if (!UIManager.MyInstance.isInputEnabled)
                    {
                        if (!controller.isGrounded) playerMovement.Locomotion();
                    }

                    //TargetHandler();
                    break;
                }
            case "CASTING":
                {
                    if (UIManager.MyInstance.isInputEnabled) playerMovement.GetInputs();
                    //TargetHandler();
                    playerMovement.Locomotion();
                    break;
                }
            case "DEAD":
                break;
            case "CROWDCONTROLLED":
                {
                    if (UIManager.MyInstance.isInputEnabled)
                        playerMovement.GetInputs();


                    if (Rooted)
                        if (!controller.isGrounded)
                            playerMovement.Locomotion();
                    if (Knockedback)
                    {
                    }

                    if (Silenced) playerMovement.Locomotion();

                    if (Subdued) playerMovement.Locomotion();
                    //TargetHandler();
                    break;
                }
            default:
                Debug.LogError("invalid state:" + state);
                break;
        }
    }

    // FINITE STATE MACHINE - SERVER
    // the server always sits in one of few possible states ("CASTING", "IDLE", etc). This function runs every frame
    // the state machine checks for any of the above Event() bools to return true and forces the server into a new state when that happens.
    [Server]
    private string UpdateServer_IDLE()
    {
        buffDebuffHandler.BuffStackRemoval(this, this, BuffApplication.BuffRemovalCondition.StandingStill);
        playerMovement.procStandingTimer += Time.deltaTime;

        // events sorted by priority (e.g. target doesn't matter if we died)

        if (EventDied())
        {
            // we died.
            OnDeath();
            return "DEAD";
        }

        if (EventCancelAction())
        {
            // the only thing that we can cancel is the target


            ServerSpellTarget = null;
            //ServerTarget = null;
            return "IDLE";
        }

        if (EventMoveStart())
        {
            // cancel casting (if any)
            if (currentAbility == -1) return "MOVING";
            var ability = abilities[currentAbility];
            // set the target of the spell. this allows the player to switch their active target and still have the spell hit the original target.
            if (!ability.InstantCast(this))
                CancelCastSkill();

            return "MOVING";
        }

        // player tried using a skill
        if (EventSkillRequest())
        {
            var ability = abilities[currentAbility];
            // set the target of the spell. this allows the player to switch their active target and still have the spell hit the original target.
            if ((!playerMovement.IsMoving() && !ability.InstantCast(this)) || ability.InstantCast(this))
            {
                // user wants to cast a skill.
                // check self (alive, mana, weapon etc.) and target and distance
                if (tempModifiedTargetServer != "NoMod" && !string.IsNullOrEmpty(tempModifiedTargetServer))
                    ServerSpellTarget = ability.data.CorrectedTarget(this, ability.data.GetAlternateTarget(this, tempModifiedTargetServer));
                else
                    ServerSpellTarget = ability.data.CorrectedTarget(this, ServerVisualTarget);
                if (CastCheckAllServer(ability))
                {
                    // start casting and cancel movement in any case
                    // (player might move into attack range * 0.8 but as soon as we
                    //  are close enough to cast, we fully commit to the cast.)
                    StartCastSkill(ability);
                    return "CASTING";
                }

                // checks failed. reset the attempted current skill.
                CancelCastSkill();
                currentAbility = -1;
                return "IDLE";
            }
        }

        if (EventSkillFinished())
        {
        }

        if (EventMoveEnd())
        {
        }

        if (EventRespawn())
        {
        }

        if (EventTargetDied())
        {
        }

        if (EventTargetDisappeared())
        {
        }

        if (EventCCd()) return "CROWDCONTROLLED";
        if (EventDuelEnded()) playerFunctions.EndDuel();
        playerMovement.old_Pos = transform.position;


        ServerSpellTarget = null;
        // no Events() happened, stay idle.
        return "IDLE";
    }


    [Server]
    private string UpdateServer_MOVING()
    {
        buffDebuffHandler.BuffStackRemoval(this, this, BuffApplication.BuffRemovalCondition.Moving);
        playerMovement.procStandingTimer = 0f;
        // events sorted by priority (e.g. target doesn't matter if we died)
        if (EventDied())
        {
            // we died.
            OnDeath();
            return "DEAD";
        }

        // finished moving. do whatever we did before.
        if (EventMoveEnd()) return "IDLE";
        if (EventCancelAction())
        {
            // cancel casting (if any) and continue moving
            CancelCastSkill();
            return "MOVING";
        }

        if (EventSkillRequest())
        {
            var ability = abilities[currentAbility];
            var ps = GetComponent<CustomAbilitiesCore>();

            {
                if ((!playerMovement.IsMoving() && !ability.InstantCast(this)) || ability.InstantCast(this) /*&& ability.type == AbilitySO.spellType.spell*/)
                {
                    if (tempModifiedTargetServer != "NoMod" && !string.IsNullOrEmpty(tempModifiedTargetServer))
                        ServerSpellTarget = ability.data.CorrectedTarget(this, ability.data.GetAlternateTarget(this, tempModifiedTargetServer));
                    else
                        ServerSpellTarget = ability.data.CorrectedTarget(this, ServerVisualTarget);
                    if (CastCheckAllServer(ability))
                    {
                        StartCastSkill(ability);
                        return "CASTING";
                    }
                }
                else
                {
                    CancelCastSkill();
                    currentAbility = -1;
                    currentInstantAbility = -1;
                    return "MOVING";
                }
            }
        }

        if (EventSkillFinished())
        {
        }

        if (EventMoveStart())
        {
        }

        if (EventRespawn())
        {
        }

        if (EventTargetDied())
        {
        }

        if (EventTargetDisappeared())
        {
        }

        if (EventCCd())
            //if (isMoving() && EventCCd())
            //    return "MOVING";
            //else
            return "CROWDCONTROLLED";
        if (EventDuelEnded()) playerFunctions.EndDuel();
        // used to check movement; compare old position to current position.
        playerMovement.old_Pos = transform.position;

        ServerSpellTarget = null;
        return "MOVING";
    }

    [Server]
    private string UpdateServer_CASTING()
    {
        var ability = abilities[currentAbility];
        var usableTargets = abilities[currentAbility].usableTargets;
        if (EventDied())
        {
            OnDeath();
            return "DEAD";
        }

        if (EventInterrupted())
        {
            interrupted = false;

            // check what spell school got interrupted and set the relevant interrupt timer.
            var castedAbilityType = ability.abilitySchool;
            var timer = CastedAbilityType(ability);
            interruptedEnd = NetworkTime.time + timer;
            // all abilities in our 'abilities' synclist with the above school is also interrupted for the timer duration.
            for (var i = 0; i < abilities.Count; i++)
            {
                var abil = abilities[i];
                if (abil.abilitySchool != castedAbilityType) continue;
                abil.interruptEnd = NetworkTime.time + timer;
                abilities[i] = abil;
            }

            ServerSpellTarget = null;
            return "IDLE";
        }

        if (EventMoveStart())
        {
            if (!ability.InstantCast(this) && playerMovement.IsMoving())
            {
                CancelCastSkill();
                return "MOVING";
            }

            if (!ability.InstantCast(this) || !playerMovement.IsMoving()) return "MOVING";
            //CastedAnimations(ability);
            FinishCastSkill(ability);
            currentAbility = -1;
            currentInstantAbility = -1;
            return "MOVING";
        }

        if (EventTargetStealthed())
        {
            CancelCastSkill();
            return "IDLE";
        }

        if (EventCancelAction())
        {
            // cancel casting
            CancelCastSkill();
            return "IDLE";
        }

        if (EventTargetDied())
            // cancel if the target matters for this skill
            if (abilities[currentAbility].cancelIfTargetDied)
            {
                CancelCastSkill();
                return "IDLE";
            }

        if (EventSkillFinished())
        {
            // apply the skill after casting is finished
            //CastedAnimations(ability);
            FinishCastSkill(ability);

            //UpdatePassiveAbilityVariables(this, 0, "AbilityUse", ability.abilityName);

            // clear current skill for now
            currentAbility = -1;
            currentInstantAbility = -1;

            ServerSpellTarget = null;


            // go back to IDLE
            return "IDLE";
        }

        if (EventMoveEnd())
        {
        }

        if (EventRespawn())
        {
        }

        if (EventSkillRequest())
        {
        }

        if (EventCCd()) return Rooted ? "CASTING" : "CROWDCONTROLLED";

        if (EventDuelEnded()) playerFunctions.EndDuel();
        playerMovement.old_Pos = transform.position;
        return "CASTING"; // nothing interesting happened
    }


    [Server]
    private string UpdateServer_CROWDCONTROLLED()
    {
        if (EventMoveStart())
        {
            if (Stunned)
            {
            }

            if (Disoriented)
            {
            }

            if (Disabled)
            {
            }

            if (Rooted)
            {
            }

            if (Knockedback) return "MOVING";
            if (Silenced) return "MOVING";
            if (Subdued) return "MOVING";
        }

        if (EventDied())
        {
            // we died.
            OnDeath();
            return "DEAD";
        }

        if (!EventCCd()) return "IDLE";
        if (EventCancelAction())
        {
            CancelCastSkill();
            return "CROWDCONTROLLED";
        }

        if (EventSkillRequest())
        {
            var ability = abilities[currentAbility];
            // set the target of the spell. this allows the player to switch their active target and still have the spell hit the original target.
            if ((!playerMovement.IsMoving() && ability.CastTime(this) != 0) || ability.CastTime(this) == 0)
            {
                // user wants to cast a skill.
                // check self (alive, mana, weapon etc.) and target and distance
                if (tempModifiedTargetServer != "NoMod" && !string.IsNullOrEmpty(tempModifiedTargetServer))
                    ServerSpellTarget = ability.data.CorrectedTarget(this, ability.data.GetAlternateTarget(this, tempModifiedTargetServer));
                else
                    ServerSpellTarget = ability.data.CorrectedTarget(this, ServerVisualTarget);
                if (CastCheckAllServer(ability))
                {
                    // start casting and cancel movement in any case
                    // (player might move into attack range * 0.8 but as soon as we
                    //  are close enough to cast, we fully commit to the cast.)
                    StartCastSkill(ability);
                    return "CASTING";
                }

                // checks failed. reset the attempted current skill.
                currentAbility = -1;
                return "CROWDCONTROLLED";
            }
        }

        if (EventSkillFinished())
        {
        }

        if (EventMoveEnd()) return "CROWDCONTROLLED";
        if (EventRespawn())
        {
        } // don't care

        if (EventTargetDied())
        {
        } // don't care

        if (EventTargetDisappeared())
        {
        } // don't care

        if (EventDuelEnded()) playerFunctions.EndDuel();
        playerMovement.old_Pos = transform.position;
        return "CROWDCONTROLLED";
    }


    [Server]
    private string UpdateServer_DEAD()
    {
        if (EventReturnedToFullHealth()) return "IDLE";
        if (EventMoveStart())
        {
            //cannot move while dead
            Debug.LogWarning("Player " + name + " moved while dead. ");
            return "DEAD";
        }

        if (EventMoveEnd())
        {
        }

        if (EventSkillFinished())
        {
        }

        if (EventDied())
        {
        }

        if (EventCancelAction())
        {
        }

        if (EventTargetDisappeared())
        {
        }

        if (EventTargetDied())
        {
        }

        if (EventSkillRequest())
        {
        }

        return "DEAD";
    }

    #endregion State Machine

    #region Events

    // no idea how this works
    private bool EventCancelAction()
    {
        var result = cancelActionRequested;
        cancelActionRequested = false; // reset
        return result;
    }

    public bool EventIdle()
    {
        return !playerMovement.IsMovingClient() && playerMovement.inputs.x == 0 && playerMovement.inputs.y == 0 && !playerMovement.jumping;
    }

    public bool EventCasting()
    {
        return currentAbility != -1 && abilities[currentAbility].Casting(this) && !abilities[currentAbility].channeled
               && abilities[currentAbility].animationType == AbilitySO.AnimationType.Projectile;
    }

    public bool EventSupportCasting()
    {
        return currentAbility != -1 && abilities[currentAbility].Casting(this) &&
               abilities[currentAbility].animationType == AbilitySO.AnimationType.Support;
    }

    public bool EventAoECasting()
    {
        return currentAbility != -1 && abilities[currentAbility].Casting(this) &&
               abilities[currentAbility].animationType == AbilitySO.AnimationType.Aoe;
    }

    public bool EventWalking()
    {
        return (playerMovement.IsMovingClient() && !playerMovement.run) || (Movespeed < 5.25f && playerMovement.IsMovingClient());
    }

    public bool EventDied()
    {
        return Health == 0 && !playerFunctions.isDueling;
    }

    // to be used in the future when we have mechanics that cause target loss (e.g. vanish, invis-like effects).
    public bool EventTargetDisappeared()
    {
        return ServerVisualTarget == null;
    }

    public bool EventTargetDied()
    {
        return ServerVisualTarget != null && ServerVisualTarget.Health == 0;
    }

    // is the player trying to use an ability?
    public bool EventSkillRequest()
    {
        return 0 <= currentAbility && currentAbility < abilities.Count;
    }

    public bool EventDuelEnded()
    {
        return Health <= 0 && playerFunctions.isDueling;
    }

    public bool EventSkillFinished()
    {
        return 0 <= currentAbility && currentAbility < abilities.Count && abilities[currentAbility].CastTimeRemaining() == 0;
    }

    public bool EventReticleFinished()
    {
        return true;
    }

    // only fire when started moving
    public bool EventMoveStart()
    {
        return state != "MOVING" && playerMovement.IsMoving();
    }

    // only fire when stopped moving
    private bool EventMoveEnd()
    {
        return state == "MOVING" && !playerMovement.IsMoving();
    }

    // for resurrect?
    private bool EventReturnedToFullHealth()
    {
        return /*state == "DEAD" && */Health > 0;
    }

    public bool EventCCd()
    {
        return Stunned || Disabled || Disoriented || Knockedback || Rooted || Silenced || Subdued;
    }

    public bool EventHardCCd()
    {
        return Stunned || Disoriented || Disabled || Rooted;
    }

    private bool EventInterrupted()
    {
        return interrupted;
    }

    public bool EventImmuneToMagical()
    {
        return buffInfo.Any(item => item.buff.immunityToMagical);
    }

    private bool EventRespawn()
    {
        var result = respawnRequested;
        respawnRequested = false; // reset
        return result;
    }

    private bool EventTargetStealthed()
    {
        return ServerVisualTarget != null && ServerVisualTarget.Stealthed;
    }

    #endregion Events
}