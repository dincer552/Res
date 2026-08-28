// TrainingSlotPlanner: lock training positions before filling the remaining cup XI.
// This module is intentionally independent from match-strength scoring.
(function () {
  const MAP = {
    'Kısa Paslar': { K: 'winger', OM: 'playmaking', SF: 'scoring', STP: 'defending', KL: 'goalkeeping' },
    'Oyun Kurma': { OM: 'playmaking', K: 'playmaking', SF: 'playmaking', STP: 'defending', KL: 'goalkeeping' },
    'Kanat': { K: 'winger', STP: 'winger', OM: 'winger', SF: 'winger', KL: 'goalkeeping' },
    'Golcülük': { SF: 'scoring', OM: 'scoring', K: 'scoring', STP: 'defending', KL: 'goalkeeping' },
    'Defans': { STP: 'defending', K: 'defending', OM: 'defending', SF: 'defending', KL: 'goalkeeping' },
    'Kalecilik': { KL: 'goalkeeping' }
  };

  const roleCount = (formation) => {
    const m = String(formation || '').match(/(\d+)-(\d+)-(\d+)/);
    if (!m) return { STP: 3, OM: 4, SF: 3, K: 0, KL: 1 };
    const d = Number(m[1]), mid = Number(m[2]), f = Number(m[3]);
    // Midfield is represented by K + OM; when exact side allocation is unavailable,
    // preserve the existing planner's convention: one wing slot per side and the rest OM.
    return { KL: 1, STP: d, K: Math.min(2, mid), OM: Math.max(0, mid - Math.min(2, mid)), SF: f };
  };

  function skill(p, key) {
    return Number(p?.[key] ?? p?.skills?.[key] ?? 0);
  }

  function trainingScore(p, role, trainingType) {
    const skillKey = MAP[trainingType]?.[role];
    if (!skillKey) return -Infinity;
    const main = skill(p, skillKey);
    const passing = skill(p, 'passing');
    const age = Number(p?.age ?? 99);
    const ageBonus = age <= 17 ? 24 : age <= 18 ? 20 : age <= 19 ? 16 : age <= 20 ? 13 : age <= 21 ? 10 : age <= 22 ? 7 : age <= 23 ? 4 : age <= 25 ? 0 : -(age - 25) * 2;
    const natural = String(p?.position ?? p?.Position ?? '').toUpperCase();
    const roleMatch = (role === 'K' && natural === 'K') || (role === 'STP' && natural === 'STP') || (role === 'OM' && natural === 'OM') || (role === 'SF' && natural === 'SF') || (role === 'KL' && natural === 'KL');
    return main * 10 + passing * 1.5 + ageBonus + (roleMatch ? 12 : 0);
  }

  function plan({ players, formation, trainingType, leagueLineupIds = [] }) {
    const counts = roleCount(formation);
    const league = new Set(leagueLineupIds.map(Number));
    const used = new Set();
    const slots = [];
    for (const role of ['KL', 'STP', 'K', 'OM', 'SF']) {
      for (let i = 0; i < (counts[role] || 0); i++) {
        // KL/STP may need to be reused to complete the XI; actual training slots are
        // defined by the selected training type and therefore only those roles are locked.
        const trainable = !!MAP[trainingType]?.[role];
        slots.push({ role, training: trainable });
      }
    }
    const locked = [];
    for (const slot of slots) {
      if (!slot.training) continue;
      const candidates = players.filter(p => {
        const id = Number(p?.playerId ?? p?.PlayerId);
        if (!id || used.has(id) || league.has(id)) return false;
        return trainingScore(p, slot.role, trainingType) > -Infinity;
      }).sort((a, b) => trainingScore(b, slot.role, trainingType) - trainingScore(a, slot.role, trainingType));
      const pick = candidates[0];
      if (pick) {
        const id = Number(pick.playerId ?? pick.PlayerId);
        used.add(id);
        locked.push({ role: slot.role, player: pick, score: trainingScore(pick, slot.role, trainingType) });
      }
    }
    return { counts, locked, usedPlayerIds: [...used] };
  }

  window.TrainingSlotPlanner = { plan, trainingScore, roleCount, MAP };
})();
