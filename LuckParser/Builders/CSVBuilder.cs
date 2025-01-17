using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using LuckParser.Parser;
using LuckParser.Models;
using LuckParser.EIData;
using LuckParser.Parser.ParsedData.CombatEvents;

namespace LuckParser.Builders
{
    public class CSVBuilder
    {
        readonly ParsedLog _log;
        readonly List<PhaseData> _phases;
        readonly Target _legacyTarget;

        readonly Statistics _statistics;
        readonly StreamWriter _sw;
        readonly string _delimiter;

        readonly string[] _uploadResult;
       
        public CSVBuilder(StreamWriter sw, string delimiter, ParsedLog log, string[] uploadresult)
        {
            _log = log;
            _sw = sw;
            _delimiter = delimiter;
            _phases = log.FightData.GetPhases(log);

            _statistics = log.Statistics;

            _uploadResult = uploadresult;
            _legacyTarget = log.FightData.Logic.Targets.Find(x => x.ID == log.FightData.ID);
            if (_legacyTarget == null)
            {
                _legacyTarget = new Target(GeneralHelper.UnknownAgent);
            }
        }
        private void WriteCell(string content)
        {
            _sw.Write(content + _delimiter, Encoding.GetEncoding(1252));
        }
        private void WriteCells(List<string> content)
        {
            foreach (string cont in content)
            {
                _sw.Write(cont + _delimiter, Encoding.GetEncoding(1252));
            }
        }
        private void WriteCells(string[] content)
        {
            foreach (string cont in content)
            {
                _sw.Write(cont + _delimiter, Encoding.GetEncoding(1252));
            }
        }
        private void NewLine()
        {
            _sw.Write("\r\n", Encoding.GetEncoding(1252));
        }
        private void WriteLine(string[] content)
        {
            foreach (string cont in content)
            {
                _sw.Write(cont + _delimiter, Encoding.GetEncoding(1252));
            }
            NewLine();
        }
        //Creating CSV---------------------------------------------------------------------------------
        public void CreateCSV()
        {       
            //header
            WriteLine(new [] { "Elite Insights Version", Application.ProductVersion });
            WriteLine(new [] { "ARC Version", _log.LogData.BuildVersion});
            WriteLine(new [] { "Fight ID", _log.FightData.ID.ToString() });
            WriteLine(new [] { "Recorded By", _log.LogData.PoVName });
            WriteLine(new [] { "Time Start", _log.LogData.LogStart });
            WriteLine(new [] { "Time End", _log.LogData.LogEnd });
            if (Properties.Settings.Default.UploadToDPSReports || Properties.Settings.Default.UploadToDPSReportsRH || Properties.Settings.Default.UploadToRaidar)
            {
                WriteLine(new[] { "Links", _uploadResult[0], _uploadResult[1], _uploadResult[2] });
            }
            else
            {
                NewLine();
            }
            NewLine();
            NewLine();
            NewLine();
            //Boss card
            WriteLine(new [] { "Boss", _log.FightData.Name });
            WriteLine(new [] { "Success", _log.FightData.Success.ToString() });
            WriteLine(new [] { "Total Boss Health", _legacyTarget.GetHealth(_log.CombatData).ToString() });
            List<HealthUpdateEvent> hpUpdates = _log.CombatData.GetHealthUpdateEvents(_legacyTarget.AgentItem);
            double hpLeft = hpUpdates.Count > 0
                ? hpUpdates.Last().HPPercent
                : 100.0;
            WriteLine(new [] { "Final Boss Health", (_legacyTarget.GetHealth(_log.CombatData) * hpLeft).ToString() });
            WriteLine(new [] { "Boss Health Burned %", (100.0 - hpLeft).ToString() });
            WriteLine(new [] { "Duration", _log.FightData.DurationString });

            //DPSStats
            CreateDPSTable(0);

            //DMGStatsBoss
            CreateBossDMGStatsTable(0);

            //DMGStats All
            CreateDmgStatsTable(0);

            //Defensive Stats
            CreateDefTable(0);

            //Support Stats
            CreateSupTable(0);

            // boons
            CreateUptimeTable(_statistics.PresentBoons, 0);

            //boonGenSelf
            CreateGenSelfTable(_statistics.PresentBoons, 0);

            // boonGenGroup
            CreateGenGroupTable(_statistics.PresentBoons, 0);

            // boonGenOGroup
            CreateGenOGroupTable(_statistics.PresentBoons, 0);

            //  boonGenSquad
            CreateGenSquadTable(_statistics.PresentBoons, 0);

            //Offensive Buffs stats
            // boons
            CreateUptimeTable(_statistics.PresentOffbuffs, 0);

            //boonGenSelf
            CreateGenSelfTable(_statistics.PresentOffbuffs, 0);

            // boonGenGroup
            CreateGenGroupTable(_statistics.PresentOffbuffs, 0);

            // boonGenOGroup
            CreateGenOGroupTable(_statistics.PresentOffbuffs, 0);

            //  boonGenSquad
            CreateGenSquadTable(_statistics.PresentOffbuffs, 0);

            //Defensive Buffs stats
            // boons
            CreateUptimeTable(_statistics.PresentDefbuffs, 0);

            //boonGenSelf
            CreateGenSelfTable(_statistics.PresentDefbuffs, 0);

            // boonGenGroup
            CreateGenGroupTable(_statistics.PresentDefbuffs, 0);

            // boonGenOGroup
            CreateGenOGroupTable(_statistics.PresentDefbuffs, 0);

            //  boonGenSquad
            CreateGenSquadTable(_statistics.PresentDefbuffs, 0);

            //Mechanics
            CreateMechanicTable(0);

            //Mech List
            CreateMechList(0);

            //Condi Uptime
            CreateCondiUptime(0);
            //Condi Gen
            CreateCondiGen(0);
            //Boss boons
            CreateBossBoonUptime(0);
        }
        private void CreateDPSTable(int phaseIndex)
        {
            PhaseData phase = _phases[phaseIndex];
            WriteLine(new[] { "Sub Group", "Profession","Role","Name","Account","WepSet1_1","WepSet1_2","WepSet2_1","WepSet2_2",
                "Boss DPS","Boss DMG","Boss Power DPS","Boss Power DMG","Boss Condi DPS","Boss Condi DMG",
                "All DPS","All DMG","All Power DPS","All Power DMG","All Condi DPS","All Condi DMG",
                "Times Downed", "Time Died","Percent Alive"});

            int count = 0;
            foreach (Player player in _log.PlayerList)
            {
                Statistics.FinalDPS dps = player.GetDPSAll(_log, phaseIndex);
                Statistics.FinalDefenses defense = player.GetDefenses(_log, phaseIndex);
                Statistics.FinalDPS dpsBoss = player.GetDPSTarget(_log, phaseIndex, _legacyTarget);
                string deathString = defense.DeadCount.ToString();
                string deadthTooltip = "";
                if (defense.DeadCount > 0)
                {
                    TimeSpan deathDuration = TimeSpan.FromMilliseconds(defense.DeadDuration);
                    deadthTooltip = deathDuration.TotalSeconds + " seconds dead, " + (100.0 - Math.Round((deathDuration.TotalMilliseconds / phase.DurationInMS) * 100, 1)) + "% Alive";
                }
                string[] wep = player.GetWeaponsArray(_log);
                string build = "";
                if (player.Condition > 0)
                {
                    build += " Condi:" + player.Condition;
                }
                if (player.Concentration > 0)
                {
                    build += " Concentration:" + player.Concentration;
                }
                if (player.Healing > 0)
                {
                    build += " Healing:" + player.Healing;
                }
                if (player.Toughness > 0)
                {
                    build += " Toughness:" + player.Toughness;
                }
                WriteLine(new [] { player.Group.ToString(), player.Prof,build,player.Character, player.Account ,wep[0],wep[1],wep[2],wep[3],
                dpsBoss.Dps.ToString(),dpsBoss.Damage.ToString(),dpsBoss.PowerDps.ToString(),dpsBoss.PowerDamage.ToString(),dpsBoss.CondiDps.ToString(),dpsBoss.CondiDamage.ToString(),
                dps.Dps.ToString(),dps.Damage.ToString(),dps.PowerDps.ToString(),dps.PowerDamage.ToString(),dps.CondiDps.ToString(),dps.CondiDamage.ToString(),
                defense.DownCount.ToString(), deathString, deadthTooltip});
                count++;
            }
            while (count < 15)//so each graph has equal spacing
            {
                NewLine();
                count++;
            }
        }
        private void CreateBossDMGStatsTable(int phaseIndex)
        {
            //generate dmgstats table=
            WriteLine(new [] { "Sub Group", "Profession", "Name" ,
                "Critical%","Critical hits","Critical DMG",
                "Scholar%","Scholar hits","Scholar DMG","Scholar % increase",
                "Moving%","Moving Hits","Moving DMG","Moving % increase",
                "Flanking%","Flanking hits",
                "Glancing%","Glancing Hits",
                "Blind%","Blind Hits",
                "Total Hits",
                "Hits to Interupt","Hits Invulned","Time wasted","Time saved","Weapon Swaps"});
            int count = 0;
            foreach (Player player in _log.PlayerList)
            {
                if (player.IsFakeActor)
                {
                    continue;
                }
                Statistics.FinalStatsAll stats = player.GetStatsAll(_log, phaseIndex);
                Statistics.FinalStats statsBoss = player.GetStatsTarget(_log, phaseIndex, _legacyTarget);
                Dictionary<string, List<Statistics.DamageModifierData>> damageMods = player.GetDamageModifierData(_log, _legacyTarget);
                Statistics.DamageModifierData scholar = new Statistics.DamageModifierData(0, 0, 0, 0);
                Statistics.DamageModifierData moving = new Statistics.DamageModifierData(0, 0, 0, 0);
                if (damageMods.TryGetValue("Scholar Rune", out var schoDict))
                {
                    scholar = schoDict[phaseIndex];
                }
                if (damageMods.TryGetValue("Moving Bonus", out var moveDict))
                {
                    moving = moveDict[phaseIndex];
                }

                WriteLine(new [] { player.Group.ToString(), player.Prof, player.Character,
                Math.Round((double)(statsBoss.CriticalCount) / statsBoss.CritableDirectDamageCount * 100,1).ToString(), statsBoss.CriticalCount.ToString(),statsBoss.CriticalDmg.ToString(),
                Math.Round((double)(scholar.HitCount) / scholar.TotalHitCount * 100,1).ToString(),scholar.HitCount.ToString(),scholar.DamageGain.ToString(),Math.Round(100.0 * (scholar.TotalDamage / (double)(scholar.TotalDamage - scholar.DamageGain) - 1.0), 3).ToString(),
                Math.Round((double)(moving.HitCount) / moving.TotalHitCount * 100,1).ToString(),moving.HitCount.ToString(),moving.DamageGain.ToString(),Math.Round(100.0 * (moving.TotalDamage / (double)(moving.TotalDamage - moving.DamageGain) - 1.0), 3).ToString(),
                Math.Round(statsBoss.FlankingCount / (double)statsBoss.DirectDamageCount * 100,1).ToString(),statsBoss.FlankingCount.ToString(),
                Math.Round(statsBoss.GlanceCount / (double)statsBoss.DirectDamageCount * 100,1).ToString(),statsBoss.GlanceCount.ToString(),
                Math.Round(statsBoss.Missed / (double)statsBoss.DirectDamageCount * 100,1).ToString(),statsBoss.Missed.ToString(),
                statsBoss.DirectDamageCount.ToString(),
                statsBoss.Interrupts.ToString(),statsBoss.Invulned.ToString(),stats.TimeWasted.ToString(),stats.TimeSaved.ToString(),stats.SwapCount.ToString() });
                count++;
            }
            while (count < 15)//so each graph has equal spacing
            {
                NewLine();
                count++;
            }
        }
        private void CreateDmgStatsTable(int phaseIndex)
        {
            //generate dmgstats table
            WriteLine(new [] { "Sub Group", "Profession", "Name" ,
                "Critical%","Critical hits","Critical DMG",
                "Scholar%","Scholar hits","Scholar DMG","Scholar % increase",
                "Moving%","Moving Hits","Moving DMG","Moving % increase",
                "Flanking%","Flanking hits",
                "Glancing%","Glancing Hits",
                "Blind%","Blind Hits",
                "Total Hits",
                "Hits to Interupt","Hits Invulned","Time wasted","Time saved","Weapon Swaps"});
            int count = 0;
            foreach (Player player in _log.PlayerList)
            {
                if (player.IsFakeActor)
                {
                    continue;
                }
                Statistics.FinalStatsAll stats = player.GetStatsAll(_log, phaseIndex);
                Dictionary<string, List<Statistics.DamageModifierData>> damageMods = player.GetDamageModifierData(_log, null);
                Statistics.DamageModifierData scholar = new Statistics.DamageModifierData(0,0,0,0);
                Statistics.DamageModifierData moving = new Statistics.DamageModifierData(0, 0, 0, 0);
                if (damageMods.TryGetValue("Scholar Rune", out var schoDict ))
                {
                    scholar = schoDict[phaseIndex];
                }
                if (damageMods.TryGetValue("Moving Bonus", out var moveDict))
                {
                    moving = moveDict[phaseIndex];
                }

                WriteLine(new [] { player.Group.ToString(), player.Prof, player.Character,
                Math.Round((double)(stats.CriticalCount) / stats.CritableDirectDamageCount * 100,1).ToString(), stats.CriticalCount.ToString(),stats.CriticalDmg.ToString(),
                Math.Round((double)(scholar.HitCount) / scholar.TotalHitCount * 100,1).ToString(),scholar.HitCount.ToString(),scholar.DamageGain.ToString(),Math.Round(100.0 * (scholar.TotalDamage / (double)(scholar.TotalDamage - scholar.DamageGain) - 1.0), 3).ToString(),
                Math.Round((double)(moving.HitCount) / moving.TotalHitCount * 100,1).ToString(),moving.HitCount.ToString(),moving.DamageGain.ToString(),Math.Round(100.0 * (moving.TotalDamage / (double)(moving.TotalDamage - moving.DamageGain) - 1.0), 3).ToString(),
                Math.Round(stats.FlankingCount / (double)stats.DirectDamageCount * 100,1).ToString(),stats.FlankingCount.ToString(),
                Math.Round(stats.GlanceCount / (double)stats.DirectDamageCount * 100,1).ToString(),stats.GlanceCount.ToString(),
                Math.Round(stats.Missed / (double)stats.DirectDamageCount * 100,1).ToString(),stats.Missed.ToString(),
                stats.DirectDamageCount.ToString(),
                stats.Interrupts.ToString(),stats.Invulned.ToString(),stats.TimeWasted.ToString(),stats.TimeSaved.ToString(),stats.SwapCount.ToString() });
                count++;
            }
            while (count < 15)//so each graph has equal spacing
            {
                NewLine();
                count++;
            }
        }
        private void CreateDefTable(int phaseIndex)
        {
            //generate defstats table
            WriteLine(new [] { "Sub Group", "Profession", "Name" ,
                "DMG Taken","DMG Barrier","Blocked","Invulned","Evaded","Dodges" });
            int count = 0;
            foreach (Player player in _log.PlayerList)
            {
                if (player.IsFakeActor)
                {
                    continue;
                }
                Statistics.FinalDefenses defenses = player.GetDefenses(_log, phaseIndex);

                WriteLine(new [] { player.Group.ToString(), player.Prof, player.Character,
                defenses.DamageTaken.ToString(),defenses.DamageBarrier.ToString(),defenses.BlockedCount.ToString(),defenses.InvulnedCount.ToString(),defenses.EvadedCount.ToString(),defenses.DodgeCount.ToString() });
                count++;
            }
            while (count < 15)//so each graph has equal spacing
            {
                NewLine();
                count++;
            }
        }
        private void CreateSupTable(int phaseIndex)
        {
            //generate supstats table
            WriteLine(new [] { "Sub Group", "Profession", "Name" ,
                "Condi Cleanse","Condi Cleanse time", "Condi Cleanse Self","Condi Cleanse time self", "Boon Strips","Boon Strips time","Resurrects","Time Resurecting" });
            int count = 0;
            foreach (Player player in _log.PlayerList)
            {
                if (player.IsFakeActor)
                {
                    continue;
                }
                Statistics.FinalSupport support = player.GetSupport(_log, phaseIndex);

                WriteLine(new[] { player.Group.ToString(), player.Prof, player.Character,
                support.CondiCleanse.ToString(),support.CondiCleanseTime.ToString(), support.CondiCleanseSelf.ToString(), support.CondiCleanseTimeSelf.ToString(), support.BoonStrips.ToString(), support.BoonStripsTime.ToString(), support.Resurrects.ToString(),support.ResurrectTime.ToString() });
                count++;
            }
            while (count < 15)//so each graph has equal spacing
            {
                NewLine();
                count++;
            }
        }
        private void CreateUptimeTable(List<Boon> listToUse, int phaseIndex)
        {
            //generate Uptime Table table
            PhaseData phase = _phases[phaseIndex];
            long fightDuration = phase.DurationInMS;

            WriteCells(new [] { "Name", "Avg Boons" });
            foreach (Boon boon in listToUse)
            {
                WriteCell(boon.Name);

            }
            NewLine();

            int count = 0;
            foreach (Player player in _log.PlayerList)
            {
                if (player.IsFakeActor)
                {
                    continue;
                }
                Dictionary<long, Statistics.FinalBuffs> uptimes = player.GetBuffs(_log, phaseIndex, Statistics.BuffEnum.Self);

                WriteCell(player.Character);
                WriteCell(player.GetStatsAll(_log, phaseIndex).AvgBoons.ToString());
                foreach (Boon boon in listToUse)
                {
                    if (uptimes.TryGetValue(boon.ID, out var value))
                    {
                        if (boon.Type == Boon.BoonType.Duration)
                        {
                            WriteCell(value.Uptime + "%");
                        }
                        else if (boon.Type == Boon.BoonType.Intensity)
                        {
                            WriteCell(value.ToString());
                        }

                    }
                    else
                    {
                        WriteCell("0");
                    }
                }
                NewLine();
                count++;
            }
            while (count < 15)//so each graph has equal spacing
            {
                NewLine();
                count++;
            }
        }
        private void CreateGenSelfTable(List<Boon> listToUse, int phaseIndex)
        {
            //generate Uptime Table table
            WriteCell("Name");
            foreach (Boon boon in listToUse)
            {
                WriteCell(boon.Name);
                WriteCell(boon.Name + " Overstack");
            }
            NewLine();

            int count = 0;
            foreach (Player player in _log.PlayerList)
            {
                if (player.IsFakeActor)
                {
                    continue;
                }
                Dictionary<long, Statistics.FinalBuffs> uptimes = player.GetBuffs(_log, phaseIndex, Statistics.BuffEnum.Self);

                WriteCell(player.Character);
                foreach (Boon boon in listToUse)
                {
                    string rate = "0";
                    string overstack = "0";
                    if (uptimes.TryGetValue(boon.ID, out var uptime))
                    {
                        if (uptime.Generation > 0 || uptime.Overstack > 0)
                        {
                            if (boon.Type == Boon.BoonType.Duration)
                            {
                                rate = uptime.Generation.ToString() + "%";
                                overstack = uptime.Overstack.ToString() + "%";
                            }
                            else if (boon.Type == Boon.BoonType.Intensity)
                            {
                                rate = uptime.Generation.ToString();
                                overstack = uptime.Overstack.ToString();
                            }
                        }
                    }                
                    WriteCell(rate);
                    WriteCell(overstack);
                }
                NewLine();
                count++;
            }
            while (count < 15)//so each graph has equal spacing
            {
                NewLine();
                count++;
            }
        }
        private void CreateGenGroupTable(List<Boon> listToUse, int phaseIndex)
        {
            //generate Uptime Table table
            WriteCell("Name");
            foreach (Boon boon in listToUse)
            {
                WriteCell(boon.Name);
                WriteCell(boon.Name + " Overstack");
            }
            NewLine();

            int count = 0;
            foreach (Player player in _log.PlayerList)
            {
                if (player.IsFakeActor)
                {
                    continue;
                }
                Dictionary<long, Statistics.FinalBuffs> boons = player.GetBuffs(_log, phaseIndex, Statistics.BuffEnum.Group);

                WriteCell(player.Character);
                foreach (Boon boon in listToUse)
                {
                    string rate = "0";
                    string overstack = "0";
                    if (boons.TryGetValue(boon.ID, out var uptime))
                    {
                        if (uptime.Generation > 0 || uptime.Overstack > 0)
                        {
                            if (boon.Type == Boon.BoonType.Duration)
                            {
                                rate = uptime.Generation.ToString() + "%";
                                overstack = uptime.Overstack.ToString() + "%";
                            }
                            else if (boon.Type == Boon.BoonType.Intensity)
                            {
                                rate = uptime.Generation.ToString();
                                overstack = uptime.Overstack.ToString();
                            }

                        }
                    }
                    WriteCell(rate);
                    WriteCell(overstack);
                }
                NewLine();
                count++;
            }
            while (count < 15)//so each graph has equal spacing
            {
                NewLine();
                count++;
            }
        }
        private void CreateGenOGroupTable(List<Boon> listToUse, int phaseIndex)
        {
            //generate Uptime Table table
            WriteCell("Name");
            foreach (Boon boon in listToUse)
            {
                WriteCell(boon.Name);
                WriteCell(boon.Name + " Overstack");
            }
            NewLine();

            int count = 0;
            foreach (Player player in _log.PlayerList)
            {
                if (player.IsFakeActor)
                {
                    continue;
                }
                Dictionary<long, Statistics.FinalBuffs> boons = player.GetBuffs(_log, phaseIndex, Statistics.BuffEnum.OffGroup);

                WriteCell(player.Character);
                foreach (Boon boon in listToUse)
                {
                    string rate = "0";
                    string overstack = "0";
                    if (boons.TryGetValue(boon.ID, out var uptime))
                    {
                        if (uptime.Generation > 0 || uptime.Overstack > 0)
                        {
                            if (boon.Type == Boon.BoonType.Duration)
                            {
                                rate = uptime.Generation.ToString() + "%";
                                overstack = uptime.Overstack.ToString() + "%";
                            }
                            else if (boon.Type == Boon.BoonType.Intensity)
                            {
                                rate = uptime.Generation.ToString();
                                overstack = uptime.Overstack.ToString();
                            }

                        }
                    }
                    WriteCell(rate);
                    WriteCell(overstack);
                }
                NewLine();
                count++;
            }
            while (count < 15)//so each graph has equal spacing
            {
                NewLine();
                count++;
            }
        }
        private void CreateGenSquadTable(List<Boon> listToUse, int phaseIndex)
        {
            //generate Uptime Table table
            WriteCell("Name");
            foreach (Boon boon in listToUse)
            {
                WriteCell(boon.Name);
                WriteCell(boon.Name + " Overstack");
            }
            NewLine();

            int count = 0;
            foreach (Player player in _log.PlayerList)
            {
                if (player.IsFakeActor)
                {
                    continue;
                }
                Dictionary<long, Statistics.FinalBuffs> boons = player.GetBuffs(_log, phaseIndex, Statistics.BuffEnum.Squad);
                WriteCell(player.Character);
                foreach (Boon boon in listToUse)
                {
                    string rate = "0";
                    string overstack = "0";
                    if (boons.TryGetValue(boon.ID, out var uptime))
                    {
                        if (uptime.Generation > 0 || uptime.Overstack > 0)
                        {
                            if (boon.Type == Boon.BoonType.Duration)
                            {
                                rate = uptime.Generation.ToString() + "%";
                                overstack = uptime.Overstack.ToString() + "%";
                            }
                            else if (boon.Type == Boon.BoonType.Intensity)
                            {
                                rate = uptime.Generation.ToString();
                                overstack = uptime.Overstack.ToString();
                            }

                        }
                    }
                    WriteCell(rate);
                    WriteCell(overstack);
                }
                NewLine();
                count++;
            }
            while (count < 15)//so each graph has equal spacing
            {
                NewLine();
                count++;
            }
        }
        private void CreateMechanicTable(int phaseIndex)
        {
            HashSet<Mechanic> presMech = _log.MechanicData.GetPresentPlayerMechs(_log, phaseIndex);
            //Dictionary<string, HashSet<Mechanic>> presEnemyMech = log.MechanicData.getPresentEnemyMechs(phaseIndex);
            PhaseData phase = _phases[phaseIndex];
            //List<AbstractMasterPlayer> enemyList = log.MechanicData.getEnemyList(phaseIndex);
            int countLines = 0;
            if (presMech.Count > 0)
            {
                WriteCell("Name");
                foreach (Mechanic mech in presMech)
                {
                    WriteCell("\""+mech.Description+"\"");
                }
                NewLine();

                foreach (Player p in _log.PlayerList)
                {
                    WriteCell(p.Character);
                    foreach (Mechanic mech in presMech)
                    {
                        int count = _log.MechanicData.GetMechanicLogs(_log, mech).Count(x => x.Actor.InstID == p.InstID && phase.InInterval(x.Time));
                        WriteCell(count.ToString());
                    }
                    NewLine();
                    countLines++;

                }

            }
            while (countLines < 15)//so each graph has equal spacing
            {
                NewLine();
                countLines++;
            }
        }
        private void CreateMechList(int phaseIndex)
        {
            MechanicData mData = _log.MechanicData;
            List<MechanicEvent> mLogs = new List<MechanicEvent>();
            foreach (List<MechanicEvent> mLs in mData.GetAllMechanics(_log))
            {
                mLogs.AddRange(mLs);
            }
            mLogs = mLogs.OrderBy(x => x.Time).ToList();
            int count = 0;
            WriteCell("Time");
            foreach (MechanicEvent m in mLogs)
            {
                WriteCell((m.Time / 1000.0).ToString());
            }
            NewLine();
            count++;
            WriteCell("Player");
            foreach (MechanicEvent m in mLogs)
            {
                WriteCell(m.Actor.Character);
            }
            NewLine();
            count++;
            WriteCell("Mechanic");
            foreach (MechanicEvent m in mLogs)
            {
                WriteCell("\"" + m.Description + "\"");
            }
            NewLine();
            count++;
            while (count < 15)//so each graph has equal spacing
            {
                NewLine();
                count++;
            }
        }
        private void CreateCondiUptime(int phaseIndex)
        {
            Target boss = _legacyTarget;
            long fightDuration = _phases[phaseIndex].DurationInMS;
            Dictionary<long, Statistics.FinalTargetBuffs> conditions = _legacyTarget.GetBuffs(_log, phaseIndex);

            WriteCell("Name");
            WriteCell("Avg");
            foreach (Boon boon in _statistics.PresentConditions)
            {
                WriteCell(boon.Name);
            }

            NewLine();
            int count = 0;
            WriteCell(boss.Character);
            WriteCell(Math.Round(_legacyTarget.GetAverageConditions(_log, phaseIndex), 1).ToString());
            foreach (Boon boon in _statistics.PresentConditions)
            {
                if (conditions.TryGetValue(boon.ID, out var uptime))
                {
                    if (boon.Type == Boon.BoonType.Duration)
                    {
                        WriteCell(uptime.Uptime.ToString() + "%");
                    }
                    else
                    {
                        WriteCell(uptime.Uptime.ToString());
                    }
                }
                else
                {
                    WriteCell("0");
                }
            }
            count++;

            while (count < 15)//so each graph has equal spacing
            {
                NewLine();
                count++;
            }
        }
        private void CreateBossBoonUptime(int phaseIndex)
        {
            Target boss = _legacyTarget;
            Dictionary<long, Statistics.FinalTargetBuffs> conditions = _legacyTarget.GetBuffs(_log, phaseIndex);
            WriteCell("Name");
            WriteCell("Avg");
            foreach (Boon boon in _statistics.PresentBoons)
            {
                WriteCell(boon.Name);
            }

            NewLine();
            int count = 0;
            WriteCell(boss.Character);
            foreach (Boon boon in _statistics.PresentBoons)
            {
                if (conditions.TryGetValue(boon.ID, out var uptime))
                {
                    if (boon.Type == Boon.BoonType.Duration)
                    {
                        WriteCell(uptime.Uptime.ToString() + "%");
                    }
                    else
                    {
                        WriteCell(uptime.Uptime.ToString());
                    }
                }
                else
                {
                    WriteCell("0");
                }
            }
            count++;

            while (count < 15)//so each graph has equal spacing
            {
                NewLine();
                count++;
            }
        }
        private void CreateCondiGen(int phaseIndex)
        {
            Dictionary<long, Statistics.FinalTargetBuffs> conditions = _legacyTarget.GetBuffs(_log, phaseIndex);
            //bool hasBoons = false;
            int count = 0;
            WriteCell("Name");
            foreach (Boon boon in _statistics.PresentConditions)
            {
                WriteCell(boon.Name);
                WriteCell(boon.Name + " Overstack");
            }
            NewLine();
            foreach (Player player in _log.PlayerList)
            {
                if (player.IsFakeActor)
                {
                    continue;
                }
                WriteCell(player.Character);
                foreach (Boon boon in _statistics.PresentConditions)
                {
                    if (conditions.TryGetValue(boon.ID, out var uptime))
                    {
                        if (boon.Type == Boon.BoonType.Duration)
                        {
                            WriteCell(conditions[boon.ID].Generated[player].ToString() + "%");
                            WriteCell(conditions[boon.ID].Overstacked[player].ToString() + "%");
                        }
                        else
                        {
                            WriteCell(conditions[boon.ID].Generated[player].ToString());
                            WriteCell(conditions[boon.ID].Overstacked[player].ToString());
                        }
                    }
                    else
                    {
                        WriteCell("0");
                        WriteCell("0");
                    }
                }
                NewLine();
                count++;
            }


            while (count < 15)//so each graph has equal spacing
            {
                NewLine();
                count++;
            }
        }
    }
}
