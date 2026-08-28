// Cup lineup policy used by the integrated lineup planner.
// The league XI is not globally banned from the cup: for training-ineligible
// positions (for example GK/CD during Short Passing), league players may be reused.
// For training positions, prefer players who did NOT play in the league XI.
(function () {
  const CUP_REUSE_ALLOWED = new Set(['GK', 'CD', 'WB']);

  function selectCupCandidates(players, roles, trainingScore, positionScore, natural, leagueLineup, opponentBias) {
    const leagueIds = new Set((leagueLineup || []).map(p => Number(p.playerId)));
    const eligible = players.filter(p => !p.injured && !p.suspended);
    const used = new Set();
    const out = new Array(roles.length).fill(null);

    const candidates = eligible.flatMap(player => roles.map((role, index) => {
      const isLeaguePlayer = leagueIds.has(Number(player.playerId));
      const training = Number(trainingScore(player, role) || 0);
      const base = Number(positionScore(player, role) || 0);
      const opponent = Number(opponentBias?.(player, role) || 0);
      const reusePenalty = !CUP_REUSE_ALLOWED.has(role) && isLeaguePlayer ? -1000 : 0;
      return { player, role, index, isLeaguePlayer, score: training + base + opponent + reusePenalty };
    })).filter(x => x.score > 0).sort((a, b) => b.score - a.score);

    for (const c of candidates) {
      const id = Number(c.player.playerId);
      if (out[c.index] || used.has(id)) continue;
      if (!natural(c.player, c.role) && c.role !== 'GK') continue;
      out[c.index] = c.player;
      used.add(id);
    }

    // Fallback: for training positions, use a non-league player first.
    // For GK/CD/WB, league players are explicitly allowed.
    for (let i = 0; i < roles.length; i++) {
      if (out[i]) continue;
      const role = roles[i];
      let pool = eligible.filter(p => !used.has(Number(p.playerId)));
      if (!CUP_REUSE_ALLOWED.has(role)) {
        const nonLeague = pool.filter(p => !leagueIds.has(Number(p.playerId)));
        if (nonLeague.length) pool = nonLeague;
      }
      pool.sort((a, b) => {
        const na = natural(a, role) ? 1 : 0;
        const nb = natural(b, role) ? 1 : 0;
        return nb - na || Number(positionScore(b, role) || 0) - Number(positionScore(a, role) || 0);
      });
      if (pool[0]) {
        out[i] = pool[0];
        used.add(Number(pool[0].playerId));
      }
    }

    return out.every(Boolean) ? out : null;
  }

  window.HattrickCupLineupPolicy = { selectCupCandidates, CUP_REUSE_ALLOWED };
})();
