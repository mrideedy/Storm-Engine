// Cross-checks the Cloud Code validator (SubmitStorm.js) against storms recorded by the C# client.
//
// Usage: node run.js <vectors.json> [content.json]
//   vectors.json  - storms exported by the C# harness (submission + expected score/class)
//   content.json  - optional content export to embed instead of the one already in the script
const fs = require("fs");
const path = require("path");
const vm = require("vm");

const scriptPath = path.join(__dirname, "..", "..", "Assets", "StormEngine", "Server", "CloudCode", "SubmitStorm.js");
let source = fs.readFileSync(scriptPath, "utf8");

if (process.argv[3]) {
  const content = fs.readFileSync(process.argv[3], "utf8").trim();
  source = source.replace(/\/\*<content>\*\/[\s\S]*?\/\*<\/content>\*\//, "/*<content>*/" + content + "/*</content>*/");
}

const writes = [];
class FakeLeaderboardsApi {
  constructor(context) { this.context = context; }
  async addLeaderboardPlayerScore(projectId, leaderboardId, playerId, body) {
    writes.push({ leaderboardId, playerId, score: body.score, metadata: body.metadata });
    return { status: 200 };
  }
}

const STORM_TEST = {};
const sandbox = {
  module: { exports: {} },
  require: name => {
    if (name === "@unity-services/leaderboards-2.0") return { LeaderboardsApi: FakeLeaderboardsApi };
    throw new Error("Unexpected require " + name);
  },
  STORM_TEST,
  console,
  Date
};
vm.createContext(sandbox);
vm.runInContext(source, sandbox, { filename: "SubmitStorm.js" });

const vectors = JSON.parse(fs.readFileSync(process.argv[2], "utf8"));
let pass = 0;
let fail = 0;
let maxDiff = 0;
for (const v of vectors) {
  const res = STORM_TEST.validate(v.submission, v.now);
  if (!res.ok) {
    fail++;
    if (fail < 10) console.log("REJECTED", res.error, "expected", v.expectedScore, v.expectedClass);
    continue;
  }
  const diff = Math.abs(res.score - v.expectedScore) / Math.max(1, v.expectedScore);
  maxDiff = Math.max(maxDiff, diff);
  if (res.cls !== v.expectedClass) {
    fail++;
    console.log("CLASS MISMATCH", res.cls, v.expectedClass);
    continue;
  }
  pass++;
}
console.log(`Replay parity: ${pass}/${vectors.length} accepted, max relative score difference ${maxDiff.toExponential(2)}`);

// Tampering must be rejected.
const tampered = [
  ["inflated score", s => { s.score *= 1.01; }],
  ["wrong class", s => { s.cls = s.cls === "supercell" ? "cyclone" : "supercell"; }],
  ["impossible machine level", s => { s.machines.pump = 100001; }],
  ["unknown module", s => { s.modules.hyperdrive = 3; }],
  ["future week", s => { s.week += 5; }],
  ["wrong weekly condition", s => { s.weekly = "not_a_condition"; }],
  ["old content version", s => { s.v = s.v - 1; }],
  ["boost fired twice", s => { if (s.events.length) s.events.push([s.events[0][0] + 1, s.events[0][1]]); else s.score = -1; }],
  ["unknown style item", s => { s.style = (s.style || []).concat(["skin_hacked"]); }],
  ["two skins at once", s => { s.style = ["skin_steam", "skin_crystal"]; }],
  ["boost timers carried in", s => { s.boosts[0].c = 5; }]
];
let caught = 0;
for (const [name, mutate] of tampered) {
  let rejected = 0;
  let tried = 0;
  for (const v of vectors.slice(0, 60)) {
    const s = JSON.parse(JSON.stringify(v.submission));
    mutate(s);
    tried++;
    const res = STORM_TEST.validate(s, v.now);
    if (!res.ok) rejected++;
  }
  console.log(`Tamper "${name}": rejected ${rejected}/${tried}`);
  if (rejected === tried) caught++;
}

// End-to-end call of the Cloud Code entry point with a fake context.
(async () => {
  const v = vectors[vectors.length - 1];
  const realNow = Date.now;
  sandbox.Date.now = () => v.now * 1000;
  const logger = { warning: () => {}, error: (m, a) => console.log("logger.error", m, a) };
  const out = await sandbox.module.exports({
    params: { submission: JSON.stringify(v.submission), title: "title_thunder" },
    context: { projectId: "p", playerId: "player-1", accessToken: "t", serviceToken: "s" },
    logger
  });
  // A malformed title must be dropped rather than stored.
  const before = writes.length;
  const bad = await sandbox.module.exports({
    params: { submission: JSON.stringify(v.submission), title: "<script>" },
    context: { projectId: "p", playerId: "player-2", accessToken: "t", serviceToken: "s" },
    logger
  });
  Date.now = realNow;
  const titled = writes.slice(0, before).every(w => w.metadata && w.metadata.ttl === "title_thunder");
  const untitled = bad.ok && writes.slice(before).every(w => w.metadata && w.metadata.ttl === undefined);
  console.log("Entry point result:", JSON.stringify({ ok: out.ok, cls: out.cls, boards: out.boards }));
  console.log("Leaderboard writes:", writes.slice(0, before).map(w => w.leaderboardId).join(", "));
  console.log(`Title metadata: stored=${titled} malformed dropped=${untitled}`);
  const allGood = fail === 0 && caught === tampered.length && out.ok && titled && untitled;
  console.log(allGood ? "ALL CLOUD CODE CHECKS PASSED" : "CLOUD CODE CHECKS FAILED");
  process.exit(allGood ? 0 : 1);
})();
