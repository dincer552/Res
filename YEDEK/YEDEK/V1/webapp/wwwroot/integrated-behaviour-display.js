(() => {
  // Integrated plan already has the player selection. This layer only makes
  // the individual-order result visible on the pitch; it never changes the XI.
  const asId = p => Number(p?.playerId ?? p?.PlayerId ?? 0);

  async function ensureCupBehaviours() {
    const plan = window.__integratedPlan;
    const cup = plan?.cup;
    const lineup = cup?.result?.lineup;
    if (!Array.isArray(lineup) || lineup.length !== 11) return;
    if (cup.__behaviourLoading || cup.__behaviourLoaded) return;

    cup.__behaviourLoading = true;
    try {
      const teamResponse = await fetch('/api/team', { cache: 'no-store' });
      if (!teamResponse.ok) return;
      const team = await teamResponse.json();
      const ids = new Set(lineup.map(asId));
      const cupPlayers = (team.players || []).filter(p => ids.has(asId(p)));
      if (cupPlayers.length !== 11) return;

      const opponentRatings = cup.opponentRatings || {};
      const response = await fetch('/api/recommend', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        cache: 'no-store',
        body: JSON.stringify({
          players: cupPlayers,
          opponent: {
            teamName: 'Kupa Rakibi',
            ratings: opponentRatings,
            preferredFormation: cup.formation
          },
          simulations: 1000,
          isHome: true
        })
      });
      if (!response.ok) return;
      const result = await response.json();
      const returnedLineup = Array.isArray(result?.lineup) ? result.lineup : [];
      if (returnedLineup.length !== 11) return;

      // /api/recommend returns the individual order directly on each lineup
      // player. Do not expect a separate behaviourProfile field.
      const behaviourByPlayer = new Map();
      returnedLineup.forEach(p => {
        const id = asId(p);
        if (id) behaviourByPlayer.set(id, p.behaviour || 'Normal');
      });

      lineup.forEach(p => {
        const behaviour = behaviourByPlayer.get(asId(p));
        if (behaviour) p.behaviour = behaviour;
      });
      cup.__behaviourLoaded = true;
      cup.result.cupIndividualOrders = true;

      if (document.getElementById('ownLineupTitle')?.textContent === 'Kupa Kadrosu' && typeof window.renderPitch === 'function') {
        window.renderPitch('#ownPitch', lineup, false);
      }
    } catch (_) {
      // Individual-order display must never break the already calculated XI.
    } finally {
      cup.__behaviourLoading = false;
    }
  }

  function refresh() {
    const plan = window.__integratedPlan;
    if (!plan?.cup?.result?.lineup?.length) return;
    ensureCupBehaviours();
  }

  const observer = new MutationObserver(() => {
    if (window.__integratedPlan?.cup?.result?.lineup?.length) refresh();
  });
  observer.observe(document.body, { childList: true, subtree: true });

  setTimeout(refresh, 800);
})();
