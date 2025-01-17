﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml;
using LuckParser.Models;
using LuckParser.Parser;
using LuckParser.Builders.JsonModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using static LuckParser.Builders.JsonModels.JsonStatistics;
using static LuckParser.Builders.JsonModels.JsonBuffsUptime;
using static LuckParser.Builders.JsonModels.JsonBuffsGeneration;
using static LuckParser.Builders.JsonModels.JsonTargetBuffs;
using static LuckParser.Builders.JsonModels.JsonRotation;
using static LuckParser.Builders.JsonModels.JsonBuffDamageModifierData;
using static LuckParser.Builders.JsonModels.JsonMechanics;
using LuckParser.EIData;
using LuckParser.Parser.ParsedData.CombatEvents;
using LuckParser.Parser.ParsedData;

namespace LuckParser.Builders
{
    public class RawFormatBuilder
    {

        readonly ParsedLog _log;
        readonly List<PhaseData> _phases;

        private readonly string[] _uploadLink;
        //
        private readonly Dictionary<string, JsonLog.SkillDesc> _skillDesc = new Dictionary<string, JsonLog.SkillDesc>();
        private readonly Dictionary<string, JsonLog.BuffDesc> _buffDesc = new Dictionary<string, JsonLog.BuffDesc>();
        private readonly Dictionary<string, JsonLog.DamageModDesc> _damageModDesc = new Dictionary<string, JsonLog.DamageModDesc>();
        private readonly Dictionary<string, HashSet<long>> _personalBuffs = new Dictionary<string, HashSet<long>>();
       
        public RawFormatBuilder(ParsedLog log, string[] UploadString)
        {
            _log = log;
            _phases = log.FightData.GetPhases(log);

            _uploadLink = UploadString;
        }

        public JsonLog CreateJsonLog()
        {
            var log = new JsonLog();

            SetGeneral(log);
            SetTargets(log);
            SetPlayers(log);
            SetPhases(log);
            SetMechanics(log);

            return log;
        }

        public void CreateJSON(StreamWriter sw)
        {
            var log = CreateJsonLog();
            DefaultContractResolver contractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy()
            };

            var serializer = new JsonSerializer
            {
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = contractResolver
            };
            JsonTextWriter writer = new JsonTextWriter(sw)
            {
                Formatting = Properties.Settings.Default.IndentJSON ? Newtonsoft.Json.Formatting.Indented : Newtonsoft.Json.Formatting.None
            };
            serializer.Serialize(writer, log);
            writer.Close();
        }

        public void CreateXML(StreamWriter sw)
        {

            var log = CreateJsonLog();

            DefaultContractResolver contractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy()
            };
            JsonSerializerSettings settings = new JsonSerializerSettings()
            {
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = contractResolver
            };
            Dictionary<string, JsonLog> root = new Dictionary<string, JsonLog>()
            {
                {"log", log }
            };
            string json = JsonConvert.SerializeObject(root, settings);

            XmlDocument xml = JsonConvert.DeserializeXmlNode(json);
            XmlTextWriter xmlTextWriter = new XmlTextWriter(sw)
            {
                Formatting = Properties.Settings.Default.IndentXML ? System.Xml.Formatting.Indented : System.Xml.Formatting.None
            };

            xml.WriteTo(xmlTextWriter);
            xmlTextWriter.Close();
        }

        private void SetGeneral(JsonLog log)
        {
            log.TriggerID = _log.FightData.ID;
            log.FightName = _log.FightData.Name;
            log.FightIcon = _log.FightData.Logic.IconUrl;
            log.EliteInsightsVersion = Application.ProductVersion;
            log.ArcVersion = _log.LogData.BuildVersion;
            log.RecordedBy = _log.LogData.PoVName;
            log.TimeStart = _log.LogData.LogStart;
            log.TimeEnd = _log.LogData.LogEnd;
            log.Duration = _log.FightData.DurationString;
            log.Success = _log.FightData.Success;
            log.SkillMap = _skillDesc;
            log.BuffMap = _buffDesc;
            log.DamageModMap = _damageModDesc;
            log.PersonalBuffs = _personalBuffs;
            log.UploadLinks = _uploadLink;
        }

        private void SetMechanics(JsonLog log)
        {
            MechanicData mechanicData = _log.MechanicData;
            var mechanicLogs = new List<MechanicEvent>();
            foreach (var mLog in mechanicData.GetAllMechanics(_log))
            {
                mechanicLogs.AddRange(mLog);
            }
            if (mechanicLogs.Any())
            {
                log.Mechanics = new List<JsonMechanics>();
                Dictionary<string, List<JsonMechanic>> dict = new Dictionary<string, List<JsonMechanic>>();
                foreach (MechanicEvent ml in mechanicLogs)
                {
                    JsonMechanic mech = new JsonMechanic
                    {
                        Time = ml.Time,
                        Actor = ml.Actor.Character
                    };
                    if (dict.TryGetValue(ml.InGameName, out var list))
                    {
                        list.Add(mech);
                    }
                    else
                    {
                        dict[ml.InGameName] = new List<JsonMechanic>()
                        {
                            mech
                        };
                    }
                }
                foreach (var pair in dict)
                {
                    log.Mechanics.Add(new JsonMechanics()
                    {
                        Name = pair.Key,
                        MechanicsData = pair.Value
                    });
                }
            }
        }

        private void SetTargets(JsonLog log)
        {
            log.Targets = new List<JsonTarget>();
            foreach (Target target in _log.FightData.Logic.Targets)
            {
                JsonTarget jsTarget = new JsonTarget
                {
                    Id = target.ID,
                    Name = target.Character,
                    Toughness = target.Toughness,
                    Healing = target.Healing,
                    Concentration = target.Concentration,
                    Condition = target.Condition,
                    TotalHealth = target.GetHealth(_log.CombatData),
                    AvgBoons = target.GetAverageBoons(_log),
                    AvgConditions = target.GetAverageConditions(_log),
                    DpsAll = target.GetDPSAll(_log).Select(x => new JsonDPS(x)).ToArray(),
                    Buffs = BuildTargetBuffs(target.GetBuffs(_log), target),
                    HitboxHeight = target.HitboxHeight,
                    HitboxWidth = target.HitboxWidth,
                    Damage1S = BuildTotal1SDamage(target),
                    Rotation = BuildRotation(target.GetCastLogs(_log, 0, _log.FightData.FightDuration)),
                    FirstAware = (int)(_log.FightData.ToFightSpace(target.FirstAwareLogTime)),
                    LastAware = (int)(_log.FightData.ToFightSpace(target.LastAwareLogTime)),
                    Minions = BuildMinions(target),
                    TotalDamageDist = BuildDamageDist(target, null),
                    TotalDamageTaken = BuildDamageTaken(target),
                    BoonsStates = BuildBuffStates(target.GetBoonGraphs(_log)[ProfHelper.NumberOfBoonsID]),
                    ConditionsStates = BuildBuffStates(target.GetBoonGraphs(_log)[ProfHelper.NumberOfConditionsID]),
                    HealthPercents = _log.CombatData.GetHealthUpdateEvents(target.AgentItem).Select(x => new double[2] { x.Time, x.HPPercent }).ToList()
                };
                double hpLeft = 0.0;
                if (_log.FightData.Success)
                {
                    hpLeft = 0;
                }
                else
                {
                    List<HealthUpdateEvent> hpUpdates = _log.CombatData.GetHealthUpdateEvents(target.AgentItem);
                    if (hpUpdates.Count > 0)
                    {
                        hpLeft = hpUpdates.Last().HPPercent;
                    }
                }
                jsTarget.HealthPercentBurned = 100.0 - hpLeft;
                jsTarget.FinalHealth = (int)Math.Round(target.GetHealth(_log.CombatData) * hpLeft / 100.0);
                log.Targets.Add(jsTarget);
            }
        }

        private void SetPlayers(JsonLog log)
        {
            log.Players = new List<JsonPlayer>();

            foreach (var player in _log.PlayerList)
            {
                log.Players.Add(new JsonPlayer
                {
                    Name = player.Character,
                    Account = player.Account,
                    Condition = player.Condition,
                    Concentration = player.Concentration,
                    Healing = player.Healing,
                    Toughness = player.Toughness,
                    HitboxHeight = player.HitboxHeight,
                    HitboxWidth = player.HitboxWidth,
                    Weapons = player.GetWeaponsArray(_log).Select(w => w ?? "Unknown").ToArray(),
                    Group = player.Group,
                    Profession = player.Prof,
                    Damage1S = BuildTotal1SDamage(player),
                    TargetDamage1S = BuildTarget1SDamage(player),
                    DpsAll = player.GetDPSAll(_log).Select(x => new JsonDPS(x)).ToArray(),
                    DpsTargets = BuildDPSTarget(player),
                    StatsAll = player.GetStatsAll(_log).Select(x => new JsonStatsAll(x)).ToArray(),
                    StatsTargets = BuildStatsTarget(player),
                    Defenses = player.GetDefenses(_log).Select(x => new JsonDefenses(x)).ToArray(),
                    Rotation = BuildRotation(player.GetCastLogs(_log, 0, _log.FightData.FightDuration)),
                    Support = player.GetSupport(_log).Select(x => new JsonSupport(x)).ToArray(),
                    BuffUptimes = BuildPlayerBuffUptimes(player.GetBuffs(_log, Statistics.BuffEnum.Self), player),
                    SelfBuffs = BuildPlayerBuffGenerations(player.GetBuffs(_log, Statistics.BuffEnum.Self)),
                    GroupBuffs = BuildPlayerBuffGenerations(player.GetBuffs(_log, Statistics.BuffEnum.Group)),
                    OffGroupBuffs = BuildPlayerBuffGenerations(player.GetBuffs(_log, Statistics.BuffEnum.OffGroup)),
                    SquadBuffs = BuildPlayerBuffGenerations(player.GetBuffs(_log, Statistics.BuffEnum.Squad)),
                    BuffUptimesActive = BuildPlayerBuffUptimes(player.GetActiveBuffs(_log, Statistics.BuffEnum.Self), player),
                    SelfBuffsActive = BuildPlayerBuffGenerations(player.GetActiveBuffs(_log, Statistics.BuffEnum.Self)),
                    GroupBuffsActive = BuildPlayerBuffGenerations(player.GetActiveBuffs(_log, Statistics.BuffEnum.Group)),
                    OffGroupBuffsActive = BuildPlayerBuffGenerations(player.GetActiveBuffs(_log, Statistics.BuffEnum.OffGroup)),
                    SquadBuffsActive = BuildPlayerBuffGenerations(player.GetActiveBuffs(_log, Statistics.BuffEnum.Squad)),
                    DamageModifiers = BuildDamageModifiers(player.GetDamageModifierData(_log, null)),
                    DamageModifiersTarget = BuildDamageModifiersTarget(player),
                    Minions = BuildMinions(player),
                    TotalDamageDist = BuildDamageDist(player, null),
                    TargetDamageDist = BuildDamageDist(player),
                    TotalDamageTaken = BuildDamageTaken(player),
                    DeathRecap = BuildDeathRecap(player.GetDeathRecaps(_log)),
                    Consumables = BuildConsumables(player),
                    BoonsStates = BuildBuffStates(player.GetBoonGraphs(_log)[ProfHelper.NumberOfBoonsID]),
                    ConditionsStates = BuildBuffStates(player.GetBoonGraphs(_log)[ProfHelper.NumberOfConditionsID]),
                    ActiveTimes = _phases.Select(x => x.GetPlayerActiveDuration(player, _log)).ToList(),
                });
            }
        }

        private List<int>[] BuildTotal1SDamage(AbstractMasterActor p)
        {
            List<int>[] list = new List<int>[_phases.Count];
            for (int i = 0; i < _phases.Count; i++)
            {
                list[i] = p.Get1SDamageList(_log, i, _phases[i], null);
            }
            return list;
        }

        private List<int>[][] BuildTarget1SDamage(Player p)
        {
            List<int>[][] tarList = new List<int>[_log.FightData.Logic.Targets.Count][];
            for (int j = 0; j < _log.FightData.Logic.Targets.Count; j++)
            {
                Target target = _log.FightData.Logic.Targets[j];
                List<int>[] list = new List<int>[_phases.Count];
                for (int i = 0; i < _phases.Count; i++)
                {
                    list[i] = p.Get1SDamageList(_log, i, _phases[i], target);
                }
                tarList[j] = list;
            }
            return tarList;
        }

        private JsonDPS[][] BuildDPSTarget(Player p)
        {
            JsonDPS[][] res = new JsonDPS[_log.FightData.Logic.Targets.Count][];
            int i = 0;
            foreach (Target tar in _log.FightData.Logic.Targets)
            {
                res[i++] = p.GetDPSTarget(_log, tar).Select(x => new JsonDPS(x)).ToArray();
            }
            return res;
        }

        private JsonStats[][] BuildStatsTarget(Player p)
        {
            JsonStats[][] res = new JsonStats[_log.FightData.Logic.Targets.Count][];
            int i = 0;
            foreach (Target tar in _log.FightData.Logic.Targets)
            {
                res[i++] = p.GetStatsTarget(_log, tar).Select(x => new JsonStats(x)).ToArray();
            }
            return res;
        }

        private List<JsonDeathRecap> BuildDeathRecap(List<Statistics.DeathRecap> recaps)
        {
            if (recaps == null)
            {
                return null;
            }
            List<JsonDeathRecap> res = new List<JsonDeathRecap>();
            foreach (Statistics.DeathRecap recap in recaps)
            {
                res.Add(new JsonDeathRecap(recap));
            }
            return res;
        }

        private List<JsonBuffDamageModifierData> BuildDamageModifiers(Dictionary<string, List<Statistics.DamageModifierData>> extra)
        {
            Dictionary<int, List<JsonBuffDamageModifierItem>> dict = new Dictionary<int, List<JsonBuffDamageModifierItem>>();
            foreach (string key in extra.Keys)
            {
                int iKey = key.GetHashCode();
                string nKey = "d" + iKey;
                if (!_damageModDesc.ContainsKey(nKey))
                {
                    _damageModDesc[nKey] = new JsonLog.DamageModDesc(_log.DamageModifiers.DamageModifiersByName[key]);
                }
                dict[iKey] = extra[key].Select(x => new JsonBuffDamageModifierItem(x)).ToList();
            }
            List<JsonBuffDamageModifierData> res = new List<JsonBuffDamageModifierData>();
            foreach (var pair in dict)
            {
                res.Add(new JsonBuffDamageModifierData()
                {
                    Id = pair.Key,
                    DamageModifiers = pair.Value
                });
            }
            return res;
        }

        private List<JsonBuffDamageModifierData>[] BuildDamageModifiersTarget(Player p)
        {
            List<JsonBuffDamageModifierData>[] res = new List<JsonBuffDamageModifierData>[_log.FightData.Logic.Targets.Count];
            for (int i = 0; i < _log.FightData.Logic.Targets.Count; i++)
            {
                Target tar = _log.FightData.Logic.Targets[i];
                res[i] = BuildDamageModifiers(p.GetDamageModifierData(_log, tar));
            }
            return res;
        }

        private List<JsonConsumable> BuildConsumables(Player player)
        {
            List<Statistics.Consumable> input = player.GetConsumablesList(_log, 0, _log.FightData.FightDuration);
            List<JsonConsumable> res = new List<JsonConsumable>();
            foreach (var food in input)
            {
                if (!_buffDesc.ContainsKey("b" + food.Buff.ID))
                {
                    _buffDesc["b" + food.Buff.ID] = new JsonLog.BuffDesc(food.Buff);
                }
                res.Add(new JsonConsumable(food));
            }
            return input.Count > 0 ? res : null;
        }

        private List<int[]> BuildBuffStates(BoonsGraphModel bgm)
        {
            if (bgm == null || bgm.BoonChart.Count == 0)
            {
                return null;
            }
            List<int[]> res = bgm.BoonChart.Select(x => new int[2] { (int)x.Start, x.Value }).ToList();
            return res.Count > 0 ? res : null;
        }

        private List<JsonDamageDist>[][] BuildDamageDist(AbstractMasterActor p)
        {
            List<JsonDamageDist>[][] res = new List<JsonDamageDist>[_log.FightData.Logic.Targets.Count][];
            for (int i = 0; i < _log.FightData.Logic.Targets.Count; i++)
            {
                Target target = _log.FightData.Logic.Targets[i];
                res[i] = BuildDamageDist(p, target);
            }
            return res;
        }

        private List<JsonDamageDist>[][] BuildDamageDist(Minions p)
        {
            List<JsonDamageDist>[][] res = new List<JsonDamageDist>[_log.FightData.Logic.Targets.Count][];
            for (int i = 0; i < _log.FightData.Logic.Targets.Count; i++)
            {
                Target target = _log.FightData.Logic.Targets[i];
                res[i] = BuildDamageDist(p, target);
            }
            return res;
        }

        private List<JsonDamageDist>[] BuildDamageDist(AbstractMasterActor p, Target target)
        {
            List<JsonDamageDist>[] res = new List<JsonDamageDist>[_phases.Count];
            for (int i = 0; i < _phases.Count; i++)
            {
                PhaseData phase = _phases[i];
                res[i] = BuildDamageDist(p.GetJustPlayerDamageLogs(target, _log, phase));
            }
            return res;
        }

        private List<JsonDamageDist>[] BuildDamageTaken(AbstractMasterActor p)
        {
            List<JsonDamageDist>[] res = new List<JsonDamageDist>[_phases.Count];
            for (int i = 0; i < _phases.Count; i++)
            {
                PhaseData phase = _phases[i];
                res[i] = BuildDamageDist(p.GetDamageTakenLogs(null, _log, phase.Start, phase.End));
            }
            return res;
        }

        private List<JsonDamageDist>[] BuildDamageDist(Minions p, Target target)
        {
            List<JsonDamageDist>[] res = new List<JsonDamageDist>[_phases.Count];
            for (int i = 0; i < _phases.Count; i++)
            {
                PhaseData phase = _phases[i];
                res[i] = BuildDamageDist(p.GetDamageLogs(target, _log, phase.Start, phase.End));
            }
            return res;
        }

        private List<JsonDamageDist> BuildDamageDist(List<AbstractDamageEvent> dls)
        {
            List<JsonDamageDist> res = new List<JsonDamageDist>();
            Dictionary<SkillItem, List<AbstractDamageEvent>> dict = dls.GroupBy(x => x.Skill).ToDictionary(x => x.Key, x => x.ToList());
            foreach (KeyValuePair<SkillItem, List<AbstractDamageEvent>> pair in dict)
            {
                if (pair.Value.Count == 0)
                {
                    continue;
                }
                SkillItem skill = pair.Key;
                bool indirect = pair.Value.Exists( x => x is NonDirectDamageEvent);
                if (indirect)
                {
                    if (!_buffDesc.ContainsKey("b" + pair.Key))
                    {
                        if (_log.Boons.BoonsByIds.TryGetValue(pair.Key.ID, out Boon buff))
                        {
                            _buffDesc["b" + pair.Key] = new JsonLog.BuffDesc(buff);
                        }
                        else
                        {
                            Boon auxBoon = new Boon(skill.Name, pair.Key.ID, skill.Icon);
                            _buffDesc["b" + pair.Key] = new JsonLog.BuffDesc(auxBoon);
                        }
                    }
                }
                else
                {
                    if (!_skillDesc.ContainsKey("s" + pair.Key))
                    {
                        _skillDesc["s" + pair.Key] = new JsonLog.SkillDesc(skill);
                    }
                }
                List<AbstractDamageEvent> filteredList = pair.Value.Where(x => !x.HasDowned).ToList();
                if (filteredList.Count == 0)
                {
                    continue;
                }
                string prefix = indirect ? "b" : "s";
                res.Add(new JsonDamageDist(filteredList, indirect, pair.Key.ID));
            }
            return res;
        }

        private List<JsonMinions> BuildMinions(AbstractMasterActor master)
        {
            List<JsonMinions> mins = new List<JsonMinions>();
            foreach (Minions minions in master.GetMinions(_log).Values)
            {
                List<int> totalDamage = new List<int>();
                List<int> totalShieldDamage = new List<int>();
                List<int>[] totalTargetDamage = new List<int>[_log.FightData.Logic.Targets.Count];
                List<int>[] totalTargetShieldDamage = new List<int>[_log.FightData.Logic.Targets.Count];
                foreach (PhaseData phase in _phases)
                {
                    int tot = 0;
                    int shdTot = 0;
                    foreach(AbstractDamageEvent de in minions.GetDamageLogs(null, _log, phase.Start, phase.End))
                    {
                        tot += de.Damage;
                        shdTot = de.ShieldDamage;
                    }
                    totalDamage.Add(tot);
                    totalShieldDamage.Add(shdTot);
                }
                for (int i = 0; i < _log.FightData.Logic.Targets.Count; i++)
                {
                    Target tar = _log.FightData.Logic.Targets[i];
                    List<int> totalTarDamage = new List<int>();
                    List<int> totalTarShieldDamage = new List<int>();
                    foreach (PhaseData phase in _phases)
                    {
                        int tot = 0;
                        int shdTot = 0;
                        foreach (AbstractDamageEvent de in minions.GetDamageLogs(tar, _log, phase.Start, phase.End))
                        {
                            tot += de.Damage;
                            shdTot = de.ShieldDamage;
                        }
                        totalTarDamage.Add(tot);
                        totalTarShieldDamage.Add(shdTot);
                    }
                    totalTargetDamage[i] = totalTarDamage;
                    totalTargetShieldDamage[i] = totalTarShieldDamage;
                }
                JsonMinions min = new JsonMinions()
                {
                    Name = minions.Character,
                    Rotation = BuildRotation(minions.GetCastLogs(_log, 0, _log.FightData.FightDuration)),
                    TotalDamageDist = BuildDamageDist(minions, null),
                    TargetDamageDist = BuildDamageDist(minions),
                    TotalDamage = totalDamage,
                    TotalShieldDamage = totalShieldDamage,
                    TotalTargetShieldDamage = totalTargetShieldDamage,
                    TotalTargetDamage = totalTargetDamage,
                };
                mins.Add(min);
            }
            return mins;
        }

        private List<JsonRotation> BuildRotation(List<AbstractCastEvent> cls)
        {
            Dictionary<long, List<JsonSkill>> dict = new Dictionary<long, List<JsonSkill>>();
            foreach (AbstractCastEvent cl in cls)
            {
                SkillItem skill = cl.Skill;
                string skillName = skill.Name;
                if (!_skillDesc.ContainsKey("s" + cl.SkillId))
                {
                    _skillDesc["s" + cl.SkillId] = new JsonLog.SkillDesc(skill);
                }
                JsonSkill jSkill = new JsonSkill(cl);
                if (dict.TryGetValue(cl.SkillId, out var list))
                {
                    list.Add(jSkill);
                }
                else
                {
                    dict[cl.SkillId] = new List<JsonSkill>()
                    {
                        jSkill
                    };
                }
            }
            List<JsonRotation> res = new List<JsonRotation>();
            foreach (var pair in dict)
            {
                res.Add(new JsonRotation()
                {
                    Id = pair.Key,
                    Skills = pair.Value
                });
            }
            return res;
        }

        private void SetPhases(JsonLog log)
        {
            log.Phases = new List<JsonPhase>();

            foreach (var phase in _phases)
            {
                JsonPhase phaseJson = new JsonPhase(phase);
                foreach (Target tar in phase.Targets)
                {
                    phaseJson.Targets.Add(_log.FightData.Logic.Targets.IndexOf(tar));
                }
                log.Phases.Add(phaseJson);
                for (int j = 1; j < _phases.Count; j++)
                {
                    PhaseData curPhase = _phases[j];
                    if (curPhase.Start < phaseJson.Start || curPhase.End > phaseJson.End ||
                         (curPhase.Start == phaseJson.Start && curPhase.End == phaseJson.End))
                    {
                        continue;
                    }
                    if (phaseJson.SubPhases == null)
                    {
                        phaseJson.SubPhases = new List<int>();
                    }
                    phaseJson.SubPhases.Add(j);
                }
            }
        }

        private List<JsonTargetBuffs> BuildTargetBuffs(List<Dictionary<long, Statistics.FinalTargetBuffs>> statBoons, Target target)
        {
            var boons = new List<JsonTargetBuffs>();

            foreach (var pair in statBoons[0])
            {
                if (!_buffDesc.ContainsKey("b" + pair.Key))
                {
                    _buffDesc["b" + pair.Key] = new JsonLog.BuffDesc(_log.Boons.BoonsByIds[pair.Key]);
                }
                List<JsonTargetBuffsData> data = new List<JsonTargetBuffsData>();
                for (int i = 0; i < _phases.Count; i++)
                {
                    JsonTargetBuffsData value = new JsonTargetBuffsData(statBoons[i][pair.Key]);
                    data.Add(value);
                }
                JsonTargetBuffs jsonBuffs = new JsonTargetBuffs()
                {
                    States = BuildBuffStates(target.GetBoonGraphs(_log)[pair.Key]),
                    BuffData = data,
                    Id = pair.Key
                };
                boons.Add(jsonBuffs);
            }

            return boons;
        }

        private List<JsonBuffsGeneration> BuildPlayerBuffGenerations(List<Dictionary<long, Statistics.FinalBuffs>> statUptimes)
        {
            var uptimes = new List<JsonBuffsGeneration>();
            foreach (var pair in statUptimes[0])
            {
                Boon buff = _log.Boons.BoonsByIds[pair.Key];
                if (!_buffDesc.ContainsKey("b" + pair.Key))
                {
                    _buffDesc["b" + pair.Key] = new JsonLog.BuffDesc(buff);
                }
                List<JsonBuffsGenerationData> data = new List<JsonBuffsGenerationData>();
                for (int i = 0; i < _phases.Count; i++)
                {
                    data.Add(new JsonBuffsGenerationData(statUptimes[i][pair.Key]));
                }
                JsonBuffsGeneration jsonBuffs = new JsonBuffsGeneration()
                {
                    BuffData = data,
                    Id = pair.Key
                };
                uptimes.Add(jsonBuffs);
            }

            if (!uptimes.Any()) return null;

            return uptimes;
        }

        private List<JsonBuffsUptime> BuildPlayerBuffUptimes(List<Dictionary<long, Statistics.FinalBuffs>> statUptimes, Player player)
        {
            var uptimes = new List<JsonBuffsUptime>();
            foreach (var pair in statUptimes[0])
            {
                Boon buff = _log.Boons.BoonsByIds[pair.Key];
                if (!_buffDesc.ContainsKey("b" + pair.Key))
                {
                    _buffDesc["b" + pair.Key] = new JsonLog.BuffDesc(buff);
                }
                if (buff.Nature == Boon.BoonNature.GraphOnlyBuff && buff.Source == Boon.ProfToEnum(player.Prof))
                {
                    if (player.GetBoonDistribution(_log, 0).GetUptime(pair.Key) > 0)
                    {
                        if (_personalBuffs.TryGetValue(player.Prof, out var list) && !list.Contains(pair.Key))
                        {
                            list.Add(pair.Key);
                        }
                        else
                        {
                            _personalBuffs[player.Prof] = new HashSet<long>()
                                {
                                    pair.Key
                                };
                        }
                    }
                }
                List<JsonBuffsUptimeData> data = new List<JsonBuffsUptimeData>();
                for (int i = 0; i < _phases.Count; i++)
                {
                    data.Add(new JsonBuffsUptimeData(statUptimes[i][pair.Key]));
                }
                JsonBuffsUptime jsonBuffs = new JsonBuffsUptime()
                {
                    States = BuildBuffStates(player.GetBoonGraphs(_log)[pair.Key]),
                    BuffData = data,
                    Id = pair.Key
                };
                uptimes.Add(jsonBuffs);
            }

            if (!uptimes.Any()) return null;

            return uptimes;
        }
    }
}