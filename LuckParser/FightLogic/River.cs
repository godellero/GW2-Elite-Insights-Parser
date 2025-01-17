﻿using LuckParser.EIData;
using LuckParser.Parser;
using LuckParser.Parser.ParsedData;
using LuckParser.Parser.ParsedData.CombatEvents;
using System;
using System.Collections.Generic;
using System.Linq;
using static LuckParser.Parser.ParseEnum.TrashIDS;

namespace LuckParser.Logic
{
    public class River : RaidLogic
    {
        public River(ushort triggerID) : base(triggerID)
        {
            MechanicList.AddRange( new List<Mechanic>
            {
                new HitOnPlayerMechanic(48272, "Bombshell", new MechanicPlotlySetting("circle","rgb(255,125,0)"),"Bomb Hit", "Hit by Hollowed Bomber Exlosion", "Hit by Bomb", 0 ),
                new HitOnPlayerMechanic(47258, "Timed Bomb", new MechanicPlotlySetting("square","rgb(255,125,0)"),"Stun Bomb", "Stunned by Mini Bomb", "Stun Bomb", 0, (de, log) => !de.To.HasBuff(log, 1122, de.Time)),
            }
            );
            GenericFallBackMethod = FallBackMethod.None;
            Extension = "river";
            IconUrl = "https://wiki.guildwars2.com/images/thumb/7/7b/Gold_River_of_Souls_Trophy.jpg/220px-Gold_River_of_Souls_Trophy.jpg";
        }

        protected override CombatReplayMap GetCombatMapInternal()
        {
            return new CombatReplayMap("https://i.imgur.com/YBtiFnH.png",
                            (4145, 1603),
                            (-12201, -4866, 7742, 2851),
                            (-21504, -12288, 24576, 12288),
                            (19072, 15484, 20992, 16508));
        }

        protected override List<ParseEnum.TrashIDS> GetTrashMobsIDS()
        {
            return new List<ParseEnum.TrashIDS>
            {
                Enervator,
                HollowedBomber,
                RiverOfSouls,
                SpiritHorde1,
                SpiritHorde2,
                SpiritHorde3
            };
        }

        public override void CheckSuccess(CombatData combatData, AgentData agentData, FightData fightData, HashSet<AgentItem> playerAgents)
        {
            base.CheckSuccess(combatData, agentData, fightData, playerAgents);
            if (!fightData.Success)
            {
                Target desmina = Targets.Find(x => x.ID == (ushort)ParseEnum.TargetIDS.Desmina);
                if (desmina == null)
                {
                    throw new InvalidOperationException("Main target of the fight not found");
                }
                ExitCombatEvent ooc = combatData.GetExitCombatEvents(desmina.AgentItem).LastOrDefault();
                if (ooc != null)
                {
                    long time = 0;
                    foreach (Mob mob in TrashMobs.Where(x => x.ID == (ushort)SpiritHorde3))
                    {
                        DespawnEvent dspwnHorde = combatData.GetDespawnEvents(mob.AgentItem).LastOrDefault();
                        if (dspwnHorde != null)
                        {
                            time = Math.Max(dspwnHorde.Time, time);
                        }
                    }
                    DespawnEvent dspwn = combatData.GetDespawnEvents(desmina.AgentItem).LastOrDefault();
                    if (time != 0 && dspwn == null && time <= fightData.ToFightSpace(desmina.LastAwareLogTime))
                    {
                        fightData.SetSuccess(true, fightData.ToLogSpace(time));
                    }
                }
            }
        }

        public override void SpecialParse(FightData fightData, AgentData agentData, List<CombatItem> combatData)
        {
            // The walls spawn at the start of the encounter, we fix it by overriding their first aware to the first velocity change event
            List<AgentItem> riverOfSouls = agentData.GetAgentsByID((ushort)RiverOfSouls);
            bool sortCombatList = false;
            foreach (AgentItem riverOfSoul in riverOfSouls)
            {
                CombatItem firstMovement = combatData.FirstOrDefault(x => x.IsStateChange == ParseEnum.StateChange.Velocity && x.SrcInstid == riverOfSoul.InstID && x.LogTime <= riverOfSoul.LastAwareLogTime);
                if (firstMovement != null)
                {
                    // update start
                    riverOfSoul.FirstAwareLogTime = firstMovement.LogTime - 10;
                    foreach (CombatItem c in combatData)
                    {
                        if (c.SrcInstid == riverOfSoul.InstID && c.LogTime < riverOfSoul.FirstAwareLogTime && (c.IsStateChange == ParseEnum.StateChange.Position || c.IsStateChange == ParseEnum.StateChange.Rotation))
                        {
                            sortCombatList = true;
                            c.OverrideTime(riverOfSoul.FirstAwareLogTime);
                        }
                    }
                }
                else
                {
                    // otherwise remove the agent from the pool
                    agentData.RemoveAgent(riverOfSoul);
                }
            }
            // make sure the list is still sorted by time after overrides
            if (sortCombatList)
            {
                combatData.Sort((x, y) => x.LogTime.CompareTo(y.LogTime));
            }
            ComputeFightTargets(agentData, combatData);
        }

        public override void ComputePlayerCombatReplayActors(Player p, ParsedLog log, CombatReplay replay)
        {
            // TODO bombs dual following circle actor (one growing, other static) + dual static circle actor (one growing with min radius the final radius of the previous, other static). Missing buff id
        }

        public override void ComputeMobCombatReplayActors(Mob mob, ParsedLog log, CombatReplay replay)
        {
            Target desmina = Targets.Find(x => x.ID == (ushort)ParseEnum.TargetIDS.Desmina);
            if (desmina == null)
            {
                throw new InvalidOperationException("Main target of the fight not found");
            }
            int start = (int)replay.TimeOffsets.start;
            int end = (int)replay.TimeOffsets.end;
            switch (mob.ID)
            {
                case (ushort)HollowedBomber:
                    List<AbstractCastEvent> bomberman = mob.GetCastLogs(log, 0, log.FightData.FightDuration).Where(x => x.SkillId == 48272).ToList();
                    foreach (AbstractCastEvent bomb in bomberman)
                    {
                        int startCast = (int)bomb.Time;
                        int endCast = startCast + bomb.ActualDuration;
                        int expectedEnd = Math.Max(startCast + bomb.ExpectedDuration, endCast);
                        replay.Actors.Add(new CircleActor(true, 0, 480, (startCast, endCast), "rgba(180,250,0,0.3)", new AgentConnector(mob)));
                        replay.Actors.Add(new CircleActor(true, expectedEnd, 480, (startCast, endCast), "rgba(180,250,0,0.3)", new AgentConnector(mob)));
                    }
                    break;
                case (ushort)RiverOfSouls:
                    if (replay.Rotations.Count > 0)
                    {
                        replay.Actors.Add(new FacingRectangleActor((start, end), new AgentConnector(mob), replay.PolledRotations, 160, 390, "rgba(255,100,0,0.5)"));
                    }
                    break;
                case (ushort)Enervator:
                // TODO Line actor between desmina and enervator. Missing skillID
                case (ushort)SpiritHorde1:
                case (ushort)SpiritHorde2:
                case (ushort)SpiritHorde3:
                    break;
                default:
                    throw new InvalidOperationException("Unknown ID in ComputeAdditionalData");
            }

        }

        public override string GetFightName() {
            return "River of Souls";
        }
    }
}
