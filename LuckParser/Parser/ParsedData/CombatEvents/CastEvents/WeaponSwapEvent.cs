﻿namespace LuckParser.Parser.ParsedData.CombatEvents
{
    public class WeaponSwapEvent : AbstractCastEvent
    {
        // Swaps
        public int SwappedTo { get; protected set; }

        public WeaponSwapEvent(CombatItem evtcItem, AgentData agentData, SkillData skillData, long offset) : base(evtcItem, agentData, skillData, offset)
        {
            SwappedTo = (int)evtcItem.DstAgent;
            Skill = skillData.Get(SkillItem.WeaponSwapId);
            ExpectedDuration = 50;
            ActualDuration = 50;
        }
    }
}
