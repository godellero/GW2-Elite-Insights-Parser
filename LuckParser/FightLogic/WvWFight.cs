using LuckParser.EIData;
using LuckParser.Parser;
using LuckParser.Parser.ParsedData;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LuckParser.Logic
{
    public class WvWFight : FightLogic
    {
        public WvWFight(ushort triggerID) : base(triggerID)
        {
            Extension = "wvw";
            Mode = ParseMode.WvW;
            IconUrl = "https://wiki.guildwars2.com/images/3/35/WvW_Rank_up.png";
        }

        protected override HashSet<ushort> GetUniqueTargetIDs()
        {
            return new HashSet<ushort>();
        }

        public override List<PhaseData> GetPhases(ParsedLog log, bool requirePhases)
        {
            List<PhaseData> phases = GetInitialPhase(log);
            Target mainTarget = Targets.Find(x => x.ID == (ushort)ParseEnum.TargetIDS.WorldVersusWorld);
            if (mainTarget == null)
            {
                throw new InvalidOperationException("Main target of the fight not found");
            }
            phases[0].Targets.Add(mainTarget);
            if (!requirePhases)
            {
                return phases;
            }
            /*phases.Add(new PhaseData(phases[0].Start + 1, phases[0].End)
            {
                Name = "Detailed Full Fight"
            });
            foreach (Target tar in Targets)
            {
                if (tar != mainTarget)
                {
                    phases[1].Targets.Add(tar);
                }
            }*/
            return phases;
        }
        public override string GetFightName()
        {
            return "World vs World";
        }

        public override void CheckSuccess(CombatData combatData, AgentData agentData, FightData fightData, HashSet<AgentItem> playerAgents)
        {
            fightData.SetSuccess(true, fightData.FightEndLogTime);
        }

        public override void SpecialParse(FightData fightData, AgentData agentData, List<CombatItem> combatData)
        {
            AgentItem dummyAgent = agentData.AddCustomAgent(combatData.First().LogTime, combatData.Last().LogTime, AgentItem.AgentType.NPC, "Enemy Players", "", TriggerID);
            ComputeFightTargets(agentData, combatData);
            List<AgentItem> aList = agentData.GetAgentByType(AgentItem.AgentType.EnemyPlayer).ToList();
            /*foreach (AgentItem a in aList)
            {
                TrashMobs.Add(new Mob(a));
            }*/
            Dictionary<ulong, AgentItem> enemyPlayerDicts = aList.GroupBy(x => x.Agent).ToDictionary(x => x.Key, x => x.ToList().First());
            foreach (CombatItem c in combatData)
            {
                if (c.IsStateChange == ParseEnum.StateChange.None && 
                    c.IsActivation == ParseEnum.Activation.None && 
                    c.IsBuffRemove == ParseEnum.BuffRemove.None &&
                    ((c.IsBuff != 0 && c.Value == 0) || (c.IsBuff == 0)))
                {
                    if (enemyPlayerDicts.TryGetValue(c.SrcAgent, out AgentItem src))
                    {
                        c.OverrideSrcValues(dummyAgent.Agent, dummyAgent.InstID);
                    }
                    if (enemyPlayerDicts.TryGetValue(c.DstAgent, out AgentItem dst))
                    {
                        c.OverrideDstValues(dummyAgent.Agent, dummyAgent.InstID);
                    }
                }
            }
        }
    }
}
