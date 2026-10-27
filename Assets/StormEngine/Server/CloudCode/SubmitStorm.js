/**
 * STORM ENGINE - SubmitStorm (Unity Cloud Code, JavaScript)
 *
 * Server-authoritative validation of weekly tournament runs. The client sends a replayable storm:
 * its frozen build, equipped style items and every boost activation. Tournament rules are enforced
 * here: this week's condition, fresh boosts and capped style bonuses. This script replays the storm with the
 * same content data and the same fixed-step simulation as the client (StormSim.cs) and only
 * writes the score it computes itself. Players are denied direct leaderboard writes by the
 * access policy in Server/AccessControl, so this is the only way onto the boards.
 *
 * The CONTENT block is generated. After changing balance in Content.cs run
 * "Storm Engine > Export Content To Cloud Code" and deploy this script again.
 */
const { LeaderboardsApi } = require("@unity-services/leaderboards-2.0");

const CONTENT = /*<content>*/{"version":3,"balance":{"lightningPerElecLog":0.3,"cloudStabilizerPerLog":0.15,"anomalyThreshold":0.4,"anomalyBase":3,"anomalyPerLevel":0.25,"megaCounts":[3,5,7],"megaMults":[1.5,2,3],"perfectCounts":[3,4],"perfectMults":[1.25,1.5]},"stats":[{"id":"BasePressure","base":1,"phase":"Atmosphere"},{"id":"Temperature","base":1,"phase":"Atmosphere"},{"id":"Humidity","base":1,"phase":"Atmosphere"},{"id":"Wind","base":1,"phase":"Atmosphere"},{"id":"Cloud","base":1,"phase":"Atmosphere"},{"id":"Electricity","base":1,"phase":"Atmosphere"},{"id":"GlobalPower","base":1,"phase":"Output"},{"id":"PhenoAll","base":1,"phase":"Phenomena"},{"id":"PhenoThunderstorm","base":1,"phase":"Phenomena"},{"id":"PhenoHeatStorm","base":1,"phase":"Phenomena"},{"id":"PhenoMonsoon","base":1,"phase":"Phenomena"},{"id":"PhenoCyclone","base":1,"phase":"Phenomena"},{"id":"PhenoSupercell","base":1,"phase":"Phenomena"},{"id":"PhenoOvercharge","base":1,"phase":"Phenomena"},{"id":"PhenoAnomaly","base":1,"phase":"Phenomena"},{"id":"StormIntensity","base":2.5,"phase":"Output"},{"id":"StormDuration","base":12,"phase":"Output"},{"id":"StormChargeSpeed","base":1,"phase":"Meta"},{"id":"LightningRate","base":0.5,"phase":"Output"},{"id":"ChainPerStrike","base":0.06,"phase":"Output"},{"id":"MegaStormPower","base":1,"phase":"Meta"},{"id":"BoostDuration","base":1,"phase":"Meta"},{"id":"BoostCooldown","base":1,"phase":"Meta"},{"id":"BoostPower","base":1,"phase":"Meta"},{"id":"OfflineEfficiency","base":0,"phase":"Meta"},{"id":"OfflineCapHours","base":0,"phase":"Meta"},{"id":"CoreGain","base":1,"phase":"Meta"},{"id":"ResearchGain","base":1,"phase":"Meta"},{"id":"ModuleSlots","base":2,"phase":"Meta"},{"id":"InstabilityRate","base":0,"phase":"Output"},{"id":"Resonance","base":0,"phase":"Meta"},{"id":"EnergyGain","base":1,"phase":"Meta"}],"milestones":[25,50,100,150,200,300,400,500,600,700,800,900,1000],"machines":[{"id":"pump","maxLevel":0,"milestoneStat":"BasePressure","milestoneMult":1.5,"effects":[{"stat":"BasePressure","op":"Flat","mode":"Scaled","base":0,"per":1,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"hydro","maxLevel":0,"milestoneStat":"Humidity","milestoneMult":1.5,"effects":[{"stat":"Humidity","op":"Add","mode":"Scaled","base":0,"per":0.05,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"condenser","maxLevel":0,"milestoneStat":"Cloud","milestoneMult":1.5,"effects":[{"stat":"Cloud","op":"Add","mode":"Scaled","base":0,"per":0.05,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"tesla","maxLevel":0,"milestoneStat":"Electricity","milestoneMult":1.5,"effects":[{"stat":"Electricity","op":"Add","mode":"Scaled","base":0,"per":0.05,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"furnace","maxLevel":0,"milestoneStat":"Temperature","milestoneMult":1.5,"effects":[{"stat":"Temperature","op":"Add","mode":"Scaled","base":0,"per":0.05,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"chamber","maxLevel":30,"milestoneStat":"BasePressure","milestoneMult":0,"effects":[{"stat":"StormDuration","op":"Flat","mode":"Scaled","base":0,"per":0.5,"from":"BasePressure","cond":"None","condIndex":-1},{"stat":"StormChargeSpeed","op":"Add","mode":"Scaled","base":0,"per":0.04,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"turbine","maxLevel":0,"milestoneStat":"Wind","milestoneMult":1.5,"effects":[{"stat":"Wind","op":"Add","mode":"Scaled","base":0,"per":0.05,"from":"BasePressure","cond":"None","condIndex":-1}]}],"boosts":[{"id":"cloud_burst","target":"Cloud","factor":2,"duration":20,"cooldown":45,"unlockMachine":"condenser","unlockLevel":10,"extraStat":"BasePressure","extraFactor":0},{"id":"arc_overload","target":"Electricity","factor":2.5,"duration":15,"cooldown":50,"unlockMachine":"tesla","unlockLevel":10,"extraStat":"LightningRate","extraFactor":1.5},{"id":"heat_surge","target":"Temperature","factor":2,"duration":30,"cooldown":60,"unlockMachine":"furnace","unlockLevel":10,"extraStat":"BasePressure","extraFactor":0},{"id":"jetstream","target":"Wind","factor":3,"duration":10,"cooldown":40,"unlockMachine":"turbine","unlockLevel":10,"extraStat":"BasePressure","extraFactor":0}],"phenomena":[{"id":"thunderstorm","stat":"PhenoThunderstorm","baseMult":1.18,"alpha":0.2,"perk":"LightningRate","perkValue":0.5,"special":false,"ingredients":[{"cat":1,"threshold":4},{"cat":3,"threshold":4},{"cat":4,"threshold":4}]},{"id":"heat_storm","stat":"PhenoHeatStorm","baseMult":1.21,"alpha":0.2,"perk":"ExtraDuration","perkValue":3,"special":false,"ingredients":[{"cat":0,"threshold":10},{"cat":2,"threshold":2}]},{"id":"monsoon","stat":"PhenoMonsoon","baseMult":1.24,"alpha":0.2,"perk":"RampingIntensity","perkValue":0.04,"special":false,"ingredients":[{"cat":1,"threshold":14},{"cat":3,"threshold":8},{"cat":2,"threshold":3}]},{"id":"cyclone","stat":"PhenoCyclone","baseMult":1.3,"alpha":0.2,"perk":"ChainBoost","perkValue":0.5,"special":false,"ingredients":[{"cat":2,"threshold":14},{"cat":1,"threshold":8}]},{"id":"supercell","stat":"PhenoSupercell","baseMult":1.48,"alpha":0.2,"perk":"IntensityMult","perkValue":1.5,"special":false,"ingredients":[{"cat":0,"threshold":30},{"cat":1,"threshold":30},{"cat":2,"threshold":30}]},{"id":"overcharge","stat":"PhenoOvercharge","baseMult":1.36,"alpha":0.2,"perk":"LightningRate","perkValue":1,"special":false,"ingredients":[{"cat":4,"threshold":40},{"cat":3,"threshold":16}]},{"id":"anomaly","stat":"PhenoAnomaly","baseMult":3,"alpha":0,"perk":"Anomaly","perkValue":0,"special":true,"ingredients":[]}],"modules":[{"id":"atmo_condenser","maxLevel":10,"effects":[{"stat":"Humidity","op":"Mult","mode":"Linear","base":1.3,"per":0.1,"from":"BasePressure","cond":"None","condIndex":-1},{"stat":"Cloud","op":"Mult","mode":"Linear","base":1.3,"per":0.1,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"tesla_array","maxLevel":10,"effects":[{"stat":"Electricity","op":"Mult","mode":"Linear","base":1.5,"per":0.15,"from":"BasePressure","cond":"None","condIndex":-1},{"stat":"LightningRate","op":"Flat","mode":"Linear","base":0.25,"per":0.05,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"thermal_core","maxLevel":10,"effects":[{"stat":"Temperature","op":"Mult","mode":"Linear","base":1.5,"per":0.15,"from":"BasePressure","cond":"None","condIndex":-1},{"stat":"BasePressure","op":"Convert","mode":"Linear","base":0.1,"per":0.01,"from":"Temperature","cond":"None","condIndex":-1}]},{"id":"jet_accelerator","maxLevel":10,"effects":[{"stat":"Wind","op":"Mult","mode":"Linear","base":1.5,"per":0.15,"from":"BasePressure","cond":"None","condIndex":-1},{"stat":"StormChargeSpeed","op":"Add","mode":"Linear","base":0.2,"per":0.02,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"barometric_lens","maxLevel":10,"effects":[{"stat":"BasePressure","op":"Mult","mode":"Linear","base":2,"per":0.25,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"capacitor_bank","maxLevel":10,"effects":[{"stat":"StormDuration","op":"Flat","mode":"Linear","base":4,"per":0.5,"from":"BasePressure","cond":"None","condIndex":-1},{"stat":"ChainPerStrike","op":"Flat","mode":"Linear","base":0.02,"per":0.005,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"heat_exchanger","maxLevel":10,"effects":[{"stat":"PhenoHeatStorm","op":"Mult","mode":"Linear","base":2,"per":0.2,"from":"BasePressure","cond":"None","condIndex":-1},{"stat":"Wind","op":"Convert","mode":"Linear","base":0.25,"per":0.025,"from":"Temperature","cond":"None","condIndex":-1}]},{"id":"rain_collector","maxLevel":10,"effects":[{"stat":"PhenoMonsoon","op":"Mult","mode":"Linear","base":2,"per":0.2,"from":"BasePressure","cond":"None","condIndex":-1},{"stat":"OfflineEfficiency","op":"Flat","mode":"Linear","base":0.1,"per":0.01,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"cyclone_core","maxLevel":10,"effects":[{"stat":"PhenoCyclone","op":"Mult","mode":"Linear","base":2,"per":0.2,"from":"BasePressure","cond":"None","condIndex":-1},{"stat":"Wind","op":"Mult","mode":"Linear","base":1.25,"per":0.05,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"overcharge_relay","maxLevel":10,"effects":[{"stat":"PhenoOvercharge","op":"Mult","mode":"Linear","base":2,"per":0.2,"from":"BasePressure","cond":"None","condIndex":-1},{"stat":"ChainPerStrike","op":"Flat","mode":"Linear","base":0.03,"per":0.005,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"supercell_processor","maxLevel":10,"effects":[{"stat":"PhenoAll","op":"Mult","mode":"Linear","base":1.2,"per":0.04,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"chaos_reactor","maxLevel":10,"effects":[{"stat":"GlobalPower","op":"Mult","mode":"Linear","base":2,"per":0.25,"from":"BasePressure","cond":"None","condIndex":-1},{"stat":"InstabilityRate","op":"Flat","mode":"Linear","base":0.1,"per":-0.004,"from":"BasePressure","cond":"None","condIndex":-1}]}],"research":[{"id":"engine_efficiency","maxLevel":500,"effects":[{"stat":"GlobalPower","op":"Add","mode":"Scaled","base":0,"per":0.25,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"automation","maxLevel":1,"effects":[]},{"id":"module_bay","maxLevel":3,"effects":[{"stat":"ModuleSlots","op":"Flat","mode":"Scaled","base":0,"per":1,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"long_storms","maxLevel":5,"effects":[{"stat":"StormDuration","op":"Flat","mode":"Scaled","base":0,"per":2,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"quick_charge","maxLevel":5,"effects":[{"stat":"StormChargeSpeed","op":"Add","mode":"Scaled","base":0,"per":0.1,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"hydro_mastery","maxLevel":10,"effects":[{"stat":"Humidity","op":"AddEff","mode":"Power","base":1.1,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"cloud_mastery","maxLevel":10,"effects":[{"stat":"Cloud","op":"AddEff","mode":"Power","base":1.1,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"charge_mastery","maxLevel":10,"effects":[{"stat":"Electricity","op":"AddEff","mode":"Power","base":1.1,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"thermal_mastery","maxLevel":10,"effects":[{"stat":"Temperature","op":"AddEff","mode":"Power","base":1.1,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"aero_mastery","maxLevel":10,"effects":[{"stat":"Wind","op":"AddEff","mode":"Power","base":1.1,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"offline_capacitor","maxLevel":5,"effects":[{"stat":"OfflineCapHours","op":"Flat","mode":"Scaled","base":0,"per":2,"from":"BasePressure","cond":"None","condIndex":-1},{"stat":"OfflineEfficiency","op":"Flat","mode":"Scaled","base":0,"per":0.05,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"cooldown_tuning","maxLevel":5,"effects":[{"stat":"BoostCooldown","op":"Mult","mode":"Power","base":0.94,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"core_extraction","maxLevel":5,"effects":[{"stat":"CoreGain","op":"Add","mode":"Scaled","base":0,"per":0.2,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"boost_amplifier","maxLevel":5,"effects":[{"stat":"BoostPower","op":"Add","mode":"Scaled","base":0,"per":0.1,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"head_start","maxLevel":5,"effects":[]},{"id":"synergy_resonance","maxLevel":5,"effects":[{"stat":"Resonance","op":"Flat","mode":"Scaled","base":0,"per":0.03,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"pressure_conversion","maxLevel":5,"effects":[{"stat":"BasePressure","op":"Convert","mode":"Scaled","base":0,"per":0.05,"from":"Temperature","cond":"None","condIndex":-1}]}],"weekly":[{"id":"extreme_gradient","effects":[{"stat":"Temperature","op":"Mult","mode":"Linear","base":1.4,"per":0,"from":"BasePressure","cond":"None","condIndex":-1},{"stat":"LightningRate","op":"Mult","mode":"Linear","base":1.1,"per":0,"from":"BasePressure","cond":"None","condIndex":-1},{"stat":"Humidity","op":"Mult","mode":"Linear","base":0.85,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"saturated_atmosphere","effects":[{"stat":"Humidity","op":"Mult","mode":"Linear","base":1.5,"per":0,"from":"BasePressure","cond":"None","condIndex":-1},{"stat":"Cloud","op":"Mult","mode":"Linear","base":1.2,"per":0,"from":"BasePressure","cond":"None","condIndex":-1},{"stat":"Wind","op":"Mult","mode":"Linear","base":0.8,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"high_wind_season","effects":[{"stat":"Wind","op":"Mult","mode":"Linear","base":1.5,"per":0,"from":"BasePressure","cond":"None","condIndex":-1},{"stat":"PhenoCyclone","op":"Mult","mode":"Linear","base":1.5,"per":0,"from":"BasePressure","cond":"None","condIndex":-1},{"stat":"Temperature","op":"Mult","mode":"Linear","base":0.85,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"electrical_instability","effects":[{"stat":"Electricity","op":"Mult","mode":"Linear","base":1.6,"per":0,"from":"BasePressure","cond":"None","condIndex":-1},{"stat":"ChainPerStrike","op":"Mult","mode":"Linear","base":1.5,"per":0,"from":"BasePressure","cond":"None","condIndex":-1},{"stat":"Cloud","op":"Mult","mode":"Linear","base":0.85,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"monsoon_season","effects":[{"stat":"PhenoMonsoon","op":"Mult","mode":"Linear","base":2,"per":0,"from":"BasePressure","cond":"None","condIndex":-1},{"stat":"Humidity","op":"Mult","mode":"Linear","base":1.3,"per":0,"from":"BasePressure","cond":"None","condIndex":-1},{"stat":"Electricity","op":"Mult","mode":"Linear","base":0.8,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"heat_dome","effects":[{"stat":"PhenoHeatStorm","op":"Mult","mode":"Linear","base":2,"per":0,"from":"BasePressure","cond":"None","condIndex":-1},{"stat":"Temperature","op":"Mult","mode":"Linear","base":1.3,"per":0,"from":"BasePressure","cond":"None","condIndex":-1},{"stat":"Cloud","op":"Mult","mode":"Linear","base":0.8,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"still_air","effects":[{"stat":"StormDuration","op":"Flat","mode":"Linear","base":4,"per":0,"from":"BasePressure","cond":"None","condIndex":-1},{"stat":"BoostPower","op":"Mult","mode":"Linear","base":1.25,"per":0,"from":"BasePressure","cond":"None","condIndex":-1},{"stat":"Wind","op":"Mult","mode":"Linear","base":0.8,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"supercell_outbreak","effects":[{"stat":"PhenoSupercell","op":"Mult","mode":"Linear","base":1.5,"per":0,"from":"BasePressure","cond":"None","condIndex":-1},{"stat":"StormIntensity","op":"Mult","mode":"Linear","base":1.1,"per":0,"from":"BasePressure","cond":"None","condIndex":-1},{"stat":"Electricity","op":"Mult","mode":"Linear","base":0.9,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]}],"tournament":{"styleCap":0.1},"styles":[{"id":"skin_ocean","slot":0,"effects":[{"stat":"Humidity","op":"Mult","mode":"Linear","base":1.1,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"skin_emerald","slot":0,"effects":[{"stat":"Cloud","op":"Mult","mode":"Linear","base":1.1,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"skin_royal","slot":0,"effects":[{"stat":"Electricity","op":"Mult","mode":"Linear","base":1.15,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"skin_midnight","slot":0,"effects":[{"stat":"Wind","op":"Mult","mode":"Linear","base":1.15,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"skin_candy","slot":0,"effects":[{"stat":"Temperature","op":"Mult","mode":"Linear","base":1.15,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"skin_gold","slot":0,"effects":[{"stat":"BasePressure","op":"Mult","mode":"Linear","base":1.2,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"skin_chaos","slot":0,"effects":[{"stat":"GlobalPower","op":"Mult","mode":"Linear","base":1.15,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"skin_steam","slot":0,"effects":[{"stat":"BasePressure","op":"Mult","mode":"Linear","base":1.3,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"skin_crystal","slot":0,"effects":[{"stat":"Electricity","op":"Mult","mode":"Linear","base":1.3,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"skin_rocket","slot":0,"effects":[{"stat":"Wind","op":"Mult","mode":"Linear","base":1.3,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"skin_magma","slot":0,"effects":[{"stat":"Temperature","op":"Mult","mode":"Linear","base":1.25,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"skin_blossom","slot":0,"effects":[{"stat":"Humidity","op":"Mult","mode":"Linear","base":1.25,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"skin_cyber","slot":0,"effects":[{"stat":"Electricity","op":"Mult","mode":"Linear","base":1.25,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"cloud_candy","slot":1,"effects":[{"stat":"Cloud","op":"Mult","mode":"Linear","base":1.1,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"cloud_toxic","slot":1,"effects":[{"stat":"StormChargeSpeed","op":"Mult","mode":"Linear","base":1.15,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"cloud_inferno","slot":1,"effects":[{"stat":"Temperature","op":"Mult","mode":"Linear","base":1.15,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"cloud_glacier","slot":1,"effects":[{"stat":"Humidity","op":"Mult","mode":"Linear","base":1.15,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"cloud_void","slot":1,"effects":[{"stat":"PhenoAll","op":"Mult","mode":"Linear","base":1.1,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"cloud_tempest","slot":1,"effects":[{"stat":"StormDuration","op":"Mult","mode":"Linear","base":1.2,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"cloud_ash","slot":1,"effects":[{"stat":"Temperature","op":"Mult","mode":"Linear","base":1.12,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"cloud_petal","slot":1,"effects":[{"stat":"Humidity","op":"Mult","mode":"Linear","base":1.12,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"cloud_synth","slot":1,"effects":[{"stat":"Electricity","op":"Mult","mode":"Linear","base":1.12,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"bolt_electric","slot":2,"effects":[{"stat":"LightningRate","op":"Mult","mode":"Linear","base":1.1,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"bolt_plasma","slot":2,"effects":[{"stat":"ChainPerStrike","op":"Mult","mode":"Linear","base":1.15,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"bolt_emerald","slot":2,"effects":[{"stat":"Electricity","op":"Mult","mode":"Linear","base":1.1,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"bolt_crimson","slot":2,"effects":[{"stat":"StormIntensity","op":"Mult","mode":"Linear","base":1.1,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"bolt_prism","slot":2,"effects":[{"stat":"LightningRate","op":"Mult","mode":"Linear","base":1.2,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"bolt_hammer","slot":2,"effects":[{"stat":"ChainPerStrike","op":"Mult","mode":"Linear","base":1.25,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"bolt_lava","slot":2,"effects":[{"stat":"StormIntensity","op":"Mult","mode":"Linear","base":1.1,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"bolt_blossom","slot":2,"effects":[{"stat":"LightningRate","op":"Mult","mode":"Linear","base":1.12,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"bolt_neon","slot":2,"effects":[{"stat":"ChainPerStrike","op":"Mult","mode":"Linear","base":1.12,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"world_desert","slot":3,"effects":[{"stat":"Temperature","op":"Mult","mode":"Linear","base":1.1,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"world_snow","slot":3,"effects":[{"stat":"Wind","op":"Mult","mode":"Linear","base":1.1,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"world_tropic","slot":3,"effects":[{"stat":"Humidity","op":"Mult","mode":"Linear","base":1.15,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"world_night","slot":3,"effects":[{"stat":"Electricity","op":"Mult","mode":"Linear","base":1.15,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"world_alien","slot":3,"effects":[{"stat":"GlobalPower","op":"Mult","mode":"Linear","base":1.1,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"world_volcano","slot":3,"effects":[{"stat":"Temperature","op":"Mult","mode":"Linear","base":1.2,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"world_sakura","slot":3,"effects":[{"stat":"Humidity","op":"Mult","mode":"Linear","base":1.2,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"world_neon","slot":3,"effects":[{"stat":"Electricity","op":"Mult","mode":"Linear","base":1.2,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"title_thunder","slot":4,"effects":[{"stat":"CoreGain","op":"Mult","mode":"Linear","base":1.05,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"title_scorcher","slot":4,"effects":[{"stat":"CoreGain","op":"Mult","mode":"Linear","base":1.05,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"title_rainmaker","slot":4,"effects":[{"stat":"CoreGain","op":"Mult","mode":"Linear","base":1.05,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"title_whisperer","slot":4,"effects":[{"stat":"CoreGain","op":"Mult","mode":"Linear","base":1.05,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"title_architect","slot":4,"effects":[{"stat":"CoreGain","op":"Mult","mode":"Linear","base":1.08,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"title_overcharger","slot":4,"effects":[{"stat":"CoreGain","op":"Mult","mode":"Linear","base":1.08,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"title_chaos","slot":4,"effects":[{"stat":"CoreGain","op":"Mult","mode":"Linear","base":1.1,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"title_veteran","slot":4,"effects":[{"stat":"CoreGain","op":"Mult","mode":"Linear","base":1.05,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"title_legend","slot":4,"effects":[{"stat":"CoreGain","op":"Mult","mode":"Linear","base":1.1,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"title_champion","slot":4,"effects":[{"stat":"CoreGain","op":"Mult","mode":"Linear","base":1.1,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"title_founder","slot":4,"effects":[{"stat":"CoreGain","op":"Mult","mode":"Linear","base":1.05,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"title_firelord","slot":4,"effects":[{"stat":"CoreGain","op":"Mult","mode":"Linear","base":1.05,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"title_zen","slot":4,"effects":[{"stat":"CoreGain","op":"Mult","mode":"Linear","base":1.05,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]},{"id":"title_neonghost","slot":4,"effects":[{"stat":"CoreGain","op":"Mult","mode":"Linear","base":1.05,"per":0,"from":"BasePressure","cond":"None","condIndex":-1}]}]}/*</content>*/;

const BOARD_ALLTIME = "storm_alltime";
const BOARD_WEEKLY = "storm_weekly";
const BOARD_CLASS_PREFIX = "storm_class_";
const TOLERANCE = 1e-6;
const TICKS_PER_SECOND = 10;
const DT = 0.1;
const MAX_CLOCK_SKEW_SECONDS = 600;

module.exports = async ({ params, context, logger }) => {
  let submission;
  try {
    submission = JSON.parse(params.submission);
  } catch (e) {
    return { ok: false, error: "malformed submission" };
  }

  const nowUnix = Math.floor(Date.now() / 1000);
  const check = validate(submission, nowUnix);
  if (!check.ok) {
    logger.warning("Rejected storm submission", { "player.id": context.playerId, "reason": check.error });
    return { ok: false, error: check.error };
  }

  const boards = [BOARD_ALLTIME];
  if (check.cls !== "squall") boards.push(BOARD_CLASS_PREFIX + check.cls);
  if (submission.week === weekOf(nowUnix)) boards.push(BOARD_WEEKLY);

  // Titles are cosmetic; only well-formed ids are stored so metadata stays tidy.
  const title = typeof params.title === "string" && /^title_[a-z_]{1,24}$/.test(params.title) ? params.title : null;
  const metadata = title ? Object.assign({}, check.summary, { ttl: title }) : check.summary;

  // Service-level client: only Cloud Code may write scores.
  const leaderboards = new LeaderboardsApi(context);
  const written = [];
  const failed = [];
  for (const id of boards) {
    try {
      await leaderboards.addLeaderboardPlayerScore(context.projectId, id, context.playerId, {
        score: check.score,
        metadata: metadata
      });
      written.push(id);
    } catch (err) {
      failed.push(id);
      logger.error("Failed to write leaderboard score", { "leaderboard.id": id, "error.message": err.message });
    }
  }

  return { ok: failed.length === 0, score: check.score, cls: check.cls, boards: written, failed: failed, error: failed.length ? "some boards failed" : null };
};

module.exports.params = {
  submission: { type: "String", required: true },
  summary: { type: "String", required: false },
  title: { type: "String", required: false }
};

// ---------------------------------------------------------------------------------------------
// Validation and deterministic replay (mirrors StormSubmission.cs, Build.cs, Formula.cs, StormSim.cs)
// ---------------------------------------------------------------------------------------------

const STAT = {};
CONTENT.stats && CONTENT.stats.forEach((s, i) => { STAT[s.id] = i; });
const NSTAT = CONTENT.stats ? CONTENT.stats.length : 0;
const ATMOSPHERE = ["BasePressure", "Temperature", "Humidity", "Wind", "Cloud", "Electricity"].map(n => STAT[n]);
const OUTPUT = ["GlobalPower", "StormIntensity", "StormDuration", "LightningRate", "ChainPerStrike", "InstabilityRate"].map(n => STAT[n]);

function indexOf(list, id) {
  for (let i = 0; i < list.length; i++) if (list[i].id === id) return i;
  return -1;
}

function weekOf(unix) {
  const days = Math.floor(unix / 86400);
  return Math.floor((days - 4) / 7);
}

function conditionIndex(week, count) {
  return count <= 0 ? -1 : ((week % count) + count) % count;
}

function ticksOf(seconds) {
  return Math.round(seconds * TICKS_PER_SECOND);
}

function milestonesReached(level) {
  let n = 0;
  for (const m of CONTENT.milestones) if (level >= m) n++;
  return n;
}

function valueAt(e, level) {
  switch (e.mode) {
    case "Scaled": return e.per * level;
    case "Power": return Math.pow(e.base, level);
    default: return e.base + e.per * (level - 1);
  }
}

function createMods(build) {
  const ms = {
    byStat: [], conversions: [], meta: [],
    boostUnlocked: [], boostDuration: [], boostCooldown: [],
    chaos: false, chaosLevel: 0, slots: 2
  };
  for (let i = 0; i < NSTAT; i++) ms.byStat.push([]);
  const add = m => { if (m.op === "Convert") ms.conversions.push(m); else ms.byStat[m.stat].push(m); };
  const addEffect = (e, level) => add({
    stat: STAT[e.stat], op: e.op, value: valueAt(e, level), from: STAT[e.from], cond: e.cond, condIndex: e.condIndex
  });

  CONTENT.machines.forEach((m, i) => {
    const level = build.machines[i];
    if (level <= 0) return;
    m.effects.forEach(e => addEffect(e, level));
    if (m.milestoneMult > 0) {
      const n = milestonesReached(level);
      if (n > 0) add({ stat: STAT[m.milestoneStat], op: "Mult", value: Math.pow(m.milestoneMult, n), cond: "None", condIndex: -1 });
    }
  });

  build.equipped.forEach(mod => {
    const level = build.moduleLevels[mod];
    if (level <= 0) return;
    CONTENT.modules[mod].effects.forEach(e => addEffect(e, level));
    if (CONTENT.modules[mod].id === "chaos_reactor") {
      ms.chaos = true;
      ms.chaosLevel = level;
    }
  });

  CONTENT.research.forEach((r, i) => {
    const level = build.research[i];
    if (level > 0) r.effects.forEach(e => addEffect(e, level));
  });

  if (build.weekly >= 0 && build.weekly < CONTENT.weekly.length)
    CONTENT.weekly[build.weekly].effects.forEach(e => addEffect(e, 1));

  // Style items: direct multipliers, capped by the tournament rules (same order as the client).
  (build.style || []).forEach(item => {
    if (item < 0 || item >= CONTENT.styles.length) return;
    CONTENT.styles[item].effects.forEach(e => {
      let value = valueAt(e, 1);
      if (build.styleCap > 0) {
        if (e.op === "Mult") value = Math.min(value, 1 + build.styleCap);
        else if (e.op === "Add" || e.op === "Flat") value = Math.min(value, build.styleCap);
      }
      add({ stat: STAT[e.stat], op: e.op, value: value, from: STAT[e.from], cond: e.cond, condIndex: e.condIndex });
    });
  });

  for (let i = 0; i < NSTAT; i++)
    if (CONTENT.stats[i].phase === "Meta") ms.meta[i] = accumulate(ms, i, null, null);
  ms.slots = Math.round(ms.meta[STAT.ModuleSlots].value);

  const boostPower = ms.meta[STAT.BoostPower].value;
  const durMult = ms.meta[STAT.BoostDuration].value;
  const cdMult = ms.meta[STAT.BoostCooldown].value;
  CONTENT.boosts.forEach((b, i) => {
    const mi = indexOf(CONTENT.machines, b.unlockMachine);
    ms.boostUnlocked[i] = mi >= 0 && build.machines[mi] >= b.unlockLevel;
    ms.boostDuration[i] = b.duration * durMult;
    ms.boostCooldown[i] = b.cooldown * cdMult;
    if (!ms.boostUnlocked[i]) return;
    add({ stat: STAT[b.target], op: "Mult", value: 1 + (b.factor - 1) * boostPower, cond: "BoostActive", condIndex: i });
    if (b.extraFactor > 0)
      add({ stat: STAT[b.extraStat], op: "Mult", value: 1 + (b.extraFactor - 1) * boostPower, cond: "BoostActive", condIndex: i });
  });
  return ms;
}

function conditionMet(m, ctx, snap) {
  switch (m.cond) {
    case "None": return true;
    case "StormActive": return !!ctx && ctx.storm;
    case "BoostActive": return !!ctx && m.condIndex >= 0 && m.condIndex < ctx.boostActive.length && ctx.boostActive[m.condIndex];
    case "PhenoActive": return !!snap && m.condIndex >= 0 && m.condIndex < snap.phenoActive.length && snap.phenoActive[m.condIndex];
    default: return false;
  }
}

function accumulate(ms, stat, ctx, snap) {
  const c = { base: CONTENT.stats[stat].base, add: 0, addEff: 1, mult: 1, value: 0 };
  for (const m of ms.byStat[stat]) {
    if (!conditionMet(m, ctx, snap)) continue;
    switch (m.op) {
      case "Flat": c.base += m.value; break;
      case "Add": c.add += m.value; break;
      case "AddEff": c.addEff *= m.value; break;
      case "Mult": c.mult *= m.value; break;
    }
  }
  finalize(c);
  return c;
}

function finalize(c) {
  c.value = c.base * (1 + c.add * c.addEff) * c.mult;
}

function newSnapshot() {
  const n = CONTENT.phenomena.length;
  return {
    stat: new Array(NSTAT).fill(0), calc: new Array(NSTAT), pre: new Array(6).fill(0),
    phenoStrength: new Array(n).fill(1), phenoActive: new Array(n).fill(false),
    activePhenos: 0, dominant: -1, synergy: 1, power: 0, boostsActive: 0
  };
}

function catValue(snap, cat) {
  return snap.stat[cat + 1];
}

function evaluate(ms, ctx, s) {
  const B = CONTENT.balance;
  for (let i = 0; i < NSTAT; i++) {
    if (CONTENT.stats[i].phase !== "Meta") continue;
    const m = ms.meta[i];
    s.calc[i] = { base: m.base, add: m.add, addEff: m.addEff, mult: m.mult, value: m.value };
    s.stat[i] = m.value;
  }

  for (const id of ATMOSPHERE) {
    s.calc[id] = accumulate(ms, id, ctx, null);
    s.pre[id] = s.calc[id].add * s.calc[id].addEff;
  }
  for (const conv of ms.conversions) {
    if (conv.from > 5 || conv.stat > 5) continue;
    s.calc[conv.stat].add += conv.value * s.pre[conv.from];
  }
  for (const id of ATMOSPHERE) {
    finalize(s.calc[id]);
    s.stat[id] = s.calc[id].value;
  }

  // Phenomena.
  s.calc[STAT.PhenoAll] = accumulate(ms, STAT.PhenoAll, ctx, null);
  s.stat[STAT.PhenoAll] = s.calc[STAT.PhenoAll].value;
  const all = s.stat[STAT.PhenoAll];
  let activeCount = 0;
  CONTENT.phenomena.forEach((def, p) => {
    const stat = STAT[def.stat];
    s.calc[stat] = accumulate(ms, stat, ctx, null);
    s.stat[stat] = s.calc[stat].value;
    let active;
    let raw;
    if (def.special) {
      active = ctx.storm && ms.chaos && ctx.instability >= B.anomalyThreshold;
      raw = B.anomalyBase + B.anomalyPerLevel * Math.max(0, ms.chaosLevel - 1);
    } else {
      let ratio = Number.MAX_VALUE;
      for (const ing of def.ingredients) ratio = Math.min(ratio, catValue(s, ing.cat) / ing.threshold);
      active = ratio >= 1;
      raw = active ? def.baseMult * (1 + def.alpha * Math.log(ratio)) : 1;
    }
    s.phenoActive[p] = active;
    s.phenoStrength[p] = active ? raw * all * s.stat[stat] : 1;
    if (active) activeCount++;
  });
  const resonance = ms.meta[STAT.Resonance].value;
  if (activeCount > 1 && resonance > 0) {
    const f = 1 + resonance * (activeCount - 1);
    for (let p = 0; p < s.phenoActive.length; p++) if (s.phenoActive[p]) s.phenoStrength[p] *= f;
  }
  let synergy = 1;
  let dominant = -1;
  let best = 0;
  for (let p = 0; p < s.phenoActive.length; p++) {
    if (!s.phenoActive[p]) continue;
    synergy *= s.phenoStrength[p];
    if (s.phenoStrength[p] > best) {
      best = s.phenoStrength[p];
      dominant = p;
    }
  }
  s.activePhenos = activeCount;
  s.synergy = synergy;
  s.dominant = dominant;

  for (const id of OUTPUT) s.calc[id] = accumulate(ms, id, ctx, s);
  const elec = s.stat[STAT.Electricity];
  if (elec > 1) {
    const lr = s.calc[STAT.LightningRate];
    lr.base += B.lightningPerElecLog * Math.log10(elec);
    finalize(lr);
  }
  const cloud = s.stat[STAT.Cloud];
  if (cloud > 1) {
    const ir = s.calc[STAT.InstabilityRate];
    ir.mult /= 1 + B.cloudStabilizerPerLog * Math.log10(cloud);
    finalize(ir);
  }
  for (const id of OUTPUT) s.stat[id] = s.calc[id].value;

  let power = s.stat[STAT.BasePressure];
  for (let cat = 0; cat < 5; cat++) power *= catValue(s, cat);
  power *= s.synergy * s.stat[STAT.GlobalPower];
  s.power = power;

  let boosts = 0;
  for (let i = 0; i < ctx.boostActive.length; i++) if (ctx.boostActive[i] && ms.boostUnlocked[i]) boosts++;
  s.boostsActive = boosts;
}

function tierMult(count, counts, mults) {
  let m = 1;
  for (let i = 0; i < counts.length; i++) if (count >= counts[i]) m = mults[i];
  return m;
}

/** Replays a storm. Returns null when a boost activation was not legal. */
function replay(setup, events) {
  const B = CONTENT.balance;
  const ms = createMods(setup.build);
  const nb = CONTENT.boosts.length;
  const np = CONTENT.phenomena.length;
  const ctx = { storm: true, boostActive: new Array(nb).fill(false), instability: 0 };
  const active = setup.active.slice();
  const cooldown = setup.cooldown.slice();
  const snap = newSnapshot();
  const r = { score: 0, strikes: 0, peakChain: 1, classScore: new Array(np + 1).fill(0), phenoSeen: new Array(np).fill(false), ticks: 0, collapsed: false };

  let tick = 0;
  let chain = 1;
  let instability = 0;
  let accumulator = 0;
  let extended = false;
  let finished = false;
  let legal = true;

  const refresh = () => {
    for (let i = 0; i < nb; i++) ctx.boostActive[i] = active[i] > 0;
    ctx.instability = instability;
  };
  refresh();
  evaluate(ms, ctx, snap);
  let duration = Math.max(1, ticksOf(snap.stat[STAT.StormDuration]));

  const canActivate = b => !finished && b >= 0 && b < nb && ms.boostUnlocked[b] && active[b] === 0 && cooldown[b] === 0;

  const step = pending => {
    for (const b of pending) {
      if (!canActivate(b)) { legal = false; continue; }
      active[b] = Math.max(1, ticksOf(ms.boostDuration[b]));
    }
    refresh();
    evaluate(ms, ctx, snap);

    const dom = snap.dominant;
    const perk = dom >= 0 ? CONTENT.phenomena[dom].perk : "None";
    const perkValue = dom >= 0 ? CONTENT.phenomena[dom].perkValue : 0;
    let lightning = snap.stat[STAT.LightningRate];
    let chainPer = snap.stat[STAT.ChainPerStrike];
    let intensity = snap.stat[STAT.StormIntensity];
    switch (perk) {
      case "LightningRate": lightning *= 1 + perkValue; break;
      case "ChainBoost": chainPer *= 1 + perkValue; break;
      case "IntensityMult": intensity *= perkValue; break;
      case "RampingIntensity": intensity *= 1 + perkValue * (tick * DT); break;
      case "Anomaly": intensity *= 2; break;
      case "ExtraDuration":
        if (!extended) {
          duration += ticksOf(perkValue);
          extended = true;
        }
        break;
    }

    let mega = tierMult(snap.activePhenos, B.megaCounts, B.megaMults);
    mega = 1 + (mega - 1) * snap.stat[STAT.MegaStormPower];
    const perfect = tierMult(snap.boostsActive, B.perfectCounts, B.perfectMults);

    accumulator += lightning * DT;
    const strikes = Math.floor(accumulator);
    accumulator -= strikes;
    r.strikes += strikes;
    chain += strikes * chainPer;

    const total = intensity * chain * mega * perfect;
    const gained = snap.power * total * DT;
    r.score += gained;
    r.classScore[dom + 1] += gained;
    r.peakChain = Math.max(r.peakChain, chain);
    for (let p = 0; p < np; p++) if (snap.phenoActive[p]) r.phenoSeen[p] = true;

    if (ms.chaos) {
      instability += snap.stat[STAT.InstabilityRate] * DT;
      if (instability >= 1) {
        instability = 1;
        r.collapsed = true;
        finished = true;
      }
    }

    for (let b = 0; b < nb; b++) {
      if (active[b] > 0) {
        active[b]--;
        if (active[b] === 0) cooldown[b] = ticksOf(ms.boostCooldown[b]);
      } else if (cooldown[b] > 0) {
        cooldown[b]--;
      }
    }

    tick++;
    if (tick >= duration) finished = true;
  };

  const ordered = events.slice().sort((a, b) => a.tick - b.tick);
  let e = 0;
  let guard = 0;
  while (!finished && guard++ < 100000) {
    const pending = [];
    while (e < ordered.length && ordered[e].tick === tick) pending.push(ordered[e++].boost);
    if (e < ordered.length && ordered[e].tick < tick) { legal = false; e++; }
    step(pending);
  }
  if (e < ordered.length) legal = false;

  let bestClass = 0;
  for (let i = 1; i < r.classScore.length; i++) if (r.classScore[i] > r.classScore[bestClass]) bestClass = i;
  r.classIndex = bestClass - 1;
  r.ticks = tick;
  r.legal = legal;
  return r;
}

function classId(index) {
  return index < 0 ? "squall" : CONTENT.phenomena[index].id;
}

/** Rebuilds the storm setup exactly like StormSubmission.TryCreateSetup. */
function createSetup(sub) {
  const build = {
    machines: new Array(CONTENT.machines.length).fill(0),
    moduleLevels: new Array(CONTENT.modules.length).fill(0),
    equipped: [],
    research: new Array(CONTENT.research.length).fill(0),
    weekly: -1,
    style: [-1, -1, -1, -1, -1],
    styleCap: CONTENT.tournament.styleCap
  };
  const isInt = v => typeof v === "number" && Number.isInteger(v);

  for (const [id, level] of Object.entries(sub.machines || {})) {
    const i = indexOf(CONTENT.machines, id);
    if (i < 0) return { error: "unknown machine " + id };
    const max = CONTENT.machines[i].maxLevel;
    if (!isInt(level) || level < 0 || level > 100000 || (max > 0 && level > max)) return { error: "bad level for " + id };
    build.machines[i] = level;
  }
  for (const [id, level] of Object.entries(sub.research || {})) {
    const i = indexOf(CONTENT.research, id);
    if (i < 0) return { error: "unknown research " + id };
    if (!isInt(level) || level < 0 || level > CONTENT.research[i].maxLevel) return { error: "bad level for " + id };
    build.research[i] = level;
  }
  for (const [id, level] of Object.entries(sub.modules || {})) {
    const i = indexOf(CONTENT.modules, id);
    if (i < 0) return { error: "unknown module " + id };
    if (!isInt(level) || level < 1 || level > CONTENT.modules[i].maxLevel) return { error: "bad level for " + id };
    if (build.equipped.indexOf(i) >= 0) return { error: "duplicate module " + id };
    build.moduleLevels[i] = level;
    build.equipped.push(i);
  }

  const weekly = conditionIndex(sub.week, CONTENT.weekly.length);
  const expected = weekly >= 0 ? CONTENT.weekly[weekly].id : null;
  if ((sub.weekly || null) !== expected) return { error: "weekly condition does not match week" };
  build.weekly = weekly;

  if (sub.style !== undefined && !Array.isArray(sub.style)) return { error: "bad style" };
  for (const id of sub.style || []) {
    const i = indexOf(CONTENT.styles, id);
    if (i < 0) return { error: "unknown style " + id };
    const slot = CONTENT.styles[i].slot;
    if (build.style[slot] >= 0) return { error: "two styles in one slot" };
    build.style[slot] = i;
  }

  const probe = createMods(build);
  if (build.equipped.length > probe.slots) return { error: "too many modules" };

  const nb = CONTENT.boosts.length;
  const setup = { build: build, active: new Array(nb).fill(0), cooldown: new Array(nb).fill(0) };
  for (const b of sub.boosts || []) {
    const i = indexOf(CONTENT.boosts, b.id);
    if (i < 0) return { error: "unknown boost " + b.id };
    const maxActive = ticksOf(probe.boostDuration[i]);
    const maxCooldown = ticksOf(probe.boostCooldown[i]);
    if (!isInt(b.a) || !isInt(b.c) || b.a < 0 || b.a > maxActive + 1 || b.c < 0 || b.c > maxCooldown + 1) return { error: "bad boost timer " + b.id };
    if (b.a > 0 && !probe.boostUnlocked[i]) return { error: "locked boost " + b.id };
    // Every tournament run starts with fresh boosts.
    if (b.a !== 0 || b.c !== 0) return { error: "tournament runs start with fresh boosts" };
  }

  const events = [];
  for (const ev of sub.events || []) {
    if (!Array.isArray(ev) || ev.length !== 2) return { error: "bad event" };
    const i = indexOf(CONTENT.boosts, ev[1]);
    if (i < 0 || !isInt(ev[0]) || ev[0] < 0) return { error: "bad event" };
    events.push({ tick: ev[0], boost: i });
  }
  return { setup: setup, events: events };
}

/** Compact build description stored with each leaderboard entry ("Build View"). */
function buildSummary(setup, r) {
  const ms = createMods(setup.build);
  const snap = newSnapshot();
  evaluate(ms, { storm: false, boostActive: new Array(CONTENT.boosts.length).fill(false), instability: 0 }, snap);
  const round2 = v => Math.round(v * 100) / 100;
  const cats = [];
  for (let c = 0; c < 5; c++) cats.push(round2(catValue(snap, c)));
  return {
    cls: classId(r.classIndex),
    cat: cats,
    mods: setup.build.equipped.map(m => CONTENT.modules[m].id),
    phen: CONTENT.phenomena.filter((p, i) => r.phenoSeen[i]).map(p => p.id),
    wk: setup.build.weekly >= 0 ? CONTENT.weekly[setup.build.weekly].id : null,
    chain: round2(r.peakChain),
    strikes: r.strikes,
    sec: r.ticks / TICKS_PER_SECOND
  };
}

function validate(sub, nowUnix) {
  if (!sub || typeof sub !== "object") return { ok: false, error: "malformed submission" };
  if (sub.v !== CONTENT.version) return { ok: false, error: "content version mismatch - update the game" };
  if (typeof sub.score !== "number" || !isFinite(sub.score) || sub.score < 0) return { ok: false, error: "bad score" };
  if (typeof sub.week !== "number" || sub.week > weekOf(nowUnix)) return { ok: false, error: "storm is from the future" };
  if (typeof sub.t === "number" && sub.t > nowUnix + MAX_CLOCK_SKEW_SECONDS) return { ok: false, error: "client clock is ahead" };

  const built = createSetup(sub);
  if (built.error) return { ok: false, error: built.error };

  const r = replay(built.setup, built.events);
  if (!r.legal) return { ok: false, error: "illegal boost timing" };
  const diff = Math.abs(r.score - sub.score) / Math.max(1, Math.abs(sub.score));
  if (diff > TOLERANCE) return { ok: false, error: "score mismatch" };
  const cls = classId(r.classIndex);
  if (cls !== sub.cls) return { ok: false, error: "class mismatch" };
  return { ok: true, score: r.score, cls: cls, summary: buildSummary(built.setup, r) };
}

// Test hook: the Node harness in Server/Tests evaluates this file with STORM_TEST defined.
if (typeof STORM_TEST !== "undefined") {
  STORM_TEST.validate = validate;
  STORM_TEST.replay = replay;
  STORM_TEST.createSetup = createSetup;
  STORM_TEST.weekOf = weekOf;
  STORM_TEST.content = CONTENT;
}
