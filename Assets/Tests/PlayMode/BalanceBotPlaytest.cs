using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Balance playtest bot: plays whole runs in the real Game scene with normal HP and a simple
/// heuristic policy, recording through GameplayBalanceTelemetry into its own folder
/// (persistentDataPath/BalanceTelemetryBot), never the owner's stats or run save.
/// Explicit, so the regression gate never runs it. Bot results measure the content against
/// this policy, not against a human player.
/// </summary>
[Explicit("Balance playtest bot; run on demand.")]
public sealed class BalanceBotPlaytest
{
    const int Runs = 20;
    const float Speed = 8f;
    const int StuckFrames = 900;
    static readonly FieldInfo StoreField = typeof(SaveGameService).GetField("StoreOverride", BindingFlags.Static | BindingFlags.NonPublic);

    object _previousStore;
    string _saveDirectory;
    bool _previousEnabled;
    System.Random _botRandom;
    int _hpOverride;

    // Decisions read 30 HP rules; a measurement run (HP override) plays as if at 18/30 throughout.
    (int hp, int max) PolicyHealth(CombatManager combat) =>
        _hpOverride > 0 ? (18, 30) : (combat.player.currentHealth, combat.player.maxHealth);

    static string OutputDirectory => Path.Combine(Application.persistentDataPath, "BalanceTelemetryBot");

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = 1;
        yield return UnloadRun();
        StoreField.SetValue(null, _previousStore);
        GameplayBalanceTelemetry.Enabled = _previousEnabled;
        GameplayBalanceTelemetry.ExportDirectoryOverride = null;
        FrontendLaunchContext.Clear();
        GameplayInputGate.Clear();
        if (_saveDirectory != null && Directory.Exists(_saveDirectory)) Directory.Delete(_saveDirectory, true);
    }

    static IEnumerator UnloadRun()
    {
        yield return SceneManager.LoadSceneAsync(GameFlowService.MainMenuSceneName);
        var persistent = new[]
        {
            RunManager.Instance != null ? RunManager.Instance.gameObject : null,
            CombatManager.Instance != null ? CombatManager.Instance.gameObject : null,
            CardManager.Instance != null ? CardManager.Instance.gameObject : null
        };
        foreach (var root in persistent.Where(root => root != null).Distinct())
            UnityEngine.Object.Destroy(root);
        yield return null;
        GameplayInputGate.Clear();
    }

    [UnityTest, Timeout(int.MaxValue)]
    public IEnumerator PlayRuns()
    {
        _previousStore = StoreField.GetValue(null);
        _saveDirectory = Path.Combine(Application.temporaryCachePath, "DealbreakBalanceBot", Guid.NewGuid().ToString("N"));
        StoreField.SetValue(null, new RunSaveStore(Path.Combine(_saveDirectory, "active-run.json")));
        _previousEnabled = GameplayBalanceTelemetry.Enabled;
        GameplayBalanceTelemetry.Enabled = true;
        GameplayBalanceTelemetry.ExportDirectoryOverride = OutputDirectory;
        Directory.CreateDirectory(OutputDirectory);
        string summaryPath = Path.Combine(OutputDirectory, "bot-runs.csv");
        if (!File.Exists(summaryPath))
            File.WriteAllText(summaryPath, "seed,outcome,district,headliners,hp,maxHp,credits,gear,tricks,deck,encounters,shops,incidents,turns,lastNode,lastEnemies,note,realSeconds\n");

        // bot-config.txt: line 1 the run count, optional line 2 a max-HP override (0 for none). With the override the
        // runs measure what each District costs (like the soak test) instead of where a 30 HP run ends.
        string config = Path.Combine(OutputDirectory, "bot-config.txt");
        var lines = File.Exists(config) ? File.ReadAllLines(config) : Array.Empty<string>();
        int runs = lines.Length > 0 && int.TryParse(lines[0].Trim(), out int configured) ? configured : Runs;
        int hpOverride = lines.Length > 1 && int.TryParse(lines[1].Trim(), out int hpValue) ? hpValue : 0;
        _hpOverride = hpOverride;
        // Optional line 3 replays an earlier batch's seeds (its stamp, e.g. 10232045) for a like-for-like comparison.
        string stamp = lines.Length > 2 && !string.IsNullOrWhiteSpace(lines[2]) ? lines[2].Trim() : DateTime.UtcNow.ToString("MMddHHmm");
        for (int runIndex = 0; runIndex < runs; runIndex++)
        {
            string seed = $"{(hpOverride > 0 ? "BOTHP" : "BOT")}-{stamp}-{runIndex:00}";
            _botRandom = new System.Random(seed.GetHashCode() & 0x7fffffff);
            var result = new RunResult { seed = seed };
            var realStart = DateTime.UtcNow;
            yield return SceneManager.LoadSceneAsync(GameFlowService.MainMenuSceneName);
            yield return null;
            FrontendLaunchContext.RequestRun(seed);
            yield return SceneManager.LoadSceneAsync(GameFlowService.GameSceneName);
            yield return null;
            if (hpOverride > 0) CombatManager.Instance.ConfigurePlayer(hpOverride);
            RunManager.Instance.OnShowGearDiscardChoice += ResolveGearDiscardChoice;
            yield return PlayOneRun(result);
            if (RunManager.Instance != null) RunManager.Instance.OnShowGearDiscardChoice -= ResolveGearDiscardChoice;
            Time.timeScale = 1;
            if (GameplayBalanceTelemetry.Current != null && GameplayBalanceTelemetry.Current.outcome == "in_progress")
                GameplayBalanceTelemetry.Finish(result.outcome);
            result.realSeconds = (float)(DateTime.UtcNow - realStart).TotalSeconds;
            File.AppendAllText(summaryPath, result.ToCsv());
            Debug.Log($"BALANCE_BOT {seed} {result.outcome} district={result.district} hp={result.hp}/{result.maxHp} {result.note}");
            yield return UnloadRun();
        }
    }

    sealed class RunResult
    {
        public string seed, outcome = "unknown", note = "", lastNode = "", lastEnemies = "";
        public int district, headliners, hp, maxHp, credits, gear, tricks, deck, encounters, shops, incidents, turns;
        public float realSeconds;
        public string ToCsv() => string.Join(",", seed, outcome, district, headliners, hp, maxHp, credits, gear, tricks, deck,
            encounters, shops, incidents, turns, lastNode, '"' + lastEnemies + '"', '"' + note.Replace('"', '\'') + '"', realSeconds.ToString("0")) + "\n";
    }

    void ResolveGearDiscardChoice(string title, string description, IReadOnlyList<CardInstance> choices, Action<int> resolve)
    {
        // Keep the strongest cards: give up the lowest-ranked choice.
        var pick = choices.OrderBy(card => (int)card.Rank).First();
        resolve(pick.Id);
    }

    IEnumerator PlayOneRun(RunResult result)
    {
        string lastSignature = null;
        int sameFrames = 0;
        for (int frame = 0; frame < 400000; frame++)
        {
            Time.timeScale = Speed;
            var run = RunManager.Instance;
            var cards = CardManager.Instance;
            var combat = CombatManager.Instance;
            Capture(result, run, cards, combat);
            if (run.IsRunCompleted) { result.outcome = "victory"; yield break; }
            if (combat.player.IsDefeated || GameplayBalanceTelemetry.Current?.outcome == "defeat")
            {
                result.outcome = "defeat";
                yield break;
            }

            string signature = Signature(run, cards, combat);
            if (signature == lastSignature)
            {
                if (++sameFrames > StuckFrames)
                {
                    result.outcome = "bot_stuck";
                    result.note = $"gate={GameplayInputGate.Reasons} state={combat.currentState} node={run.ActiveNode?.kind} event={run.ActiveEvent?.id} hammer={run.IsPendingHammerReward}";
                    yield break;
                }
            }
            else { lastSignature = signature; sameFrames = 0; }

            if (!combat.IsResolvingAction)
            {
                var reasons = GameplayInputGate.Reasons;
                if (reasons == GameplayInputBlockReason.None) Act(result, run, cards, combat);
                else if (reasons == GameplayInputBlockReason.ConsumableReward && run.PendingConsumableReward != null)
                {
                    if (!run.ClaimPendingConsumableReward(0)) run.DeclinePendingConsumableReward();
                }
                else if (reasons == GameplayInputBlockReason.GearChoice && run.IsPendingHammerReward && run.ActiveEvent != null)
                    Hammer(run);
            }
            yield return null;
        }
        result.outcome = "frame_budget";
    }

    static string Signature(RunManager run, CardManager cards, CombatManager combat) =>
        $"{run.CurrentMapIndex}|{run.ActiveNode?.id}|{run.CompletedNodeCount}|{combat.currentState}|{cards.HandCount}|{cards.credits}|" +
        $"{combat.player.currentHealth}|{combat.pendingDamage}|{cards.BackpackSlotsUsed}|{cards.ownedGear.Count}|" +
        $"{string.Join(".", combat.Enemies.Select(e => e.currentHp))}|{run.PendingConsumableReward != null}|{run.PendingEnemyDrop != null}|{run.ActiveEvent?.id}";

    static void Capture(RunResult result, RunManager run, CardManager cards, CombatManager combat)
    {
        result.district = run.CurrentMapIndex + 1;
        result.headliners = run.headlinerIndex;
        result.hp = combat.player.currentHealth;
        result.maxHp = combat.player.maxHealth;
        result.credits = cards.credits;
        result.gear = cards.ownedGear.Count;
        result.tricks = cards.BackpackSlotsUsed;
        result.deck = cards.ownedCards.Count;
        if (run.ActiveNode != null) result.lastNode = run.ActiveNode.kind.ToString();
        if (combat.Enemies.Count > 0)
            result.lastEnemies = string.Join(" ", combat.Enemies.Select(e => $"{e.type?.enemyName}:{e.currentHp}/{e.maxHp}hp:{e.EffectiveAttack}atk"));
    }

    void Act(RunResult result, RunManager run, CardManager cards, CombatManager combat)
    {
        if (run.PendingConsumableReward != null)
        {
            if (!run.ClaimPendingConsumableReward(0)) run.DeclinePendingConsumableReward();
            return;
        }
        if (run.PendingEnemyDrop != null)
        {
            if (!run.ClaimPendingEnemyDrop()) run.LeavePendingEnemyDrop();
            return;
        }
        if (combat.Enemies.Count > 0 && combat.Enemies.Any(e => !e.IsDefeated))
        {
            if (combat.currentState == GameState.PlayerTurn) { PlayerTurn(result, cards, combat); return; }
            if (combat.currentState == GameState.EnemyAttacking) { Defend(cards, combat); return; }
        }
        if (run.ActiveNode == null)
        {
            ChooseNode(result, run, cards, combat);
            return;
        }
        if (run.ActiveNode.kind == MapNodeType.Shop) { Shop(run, cards, combat); return; }
        if (run.ActiveEvent != null || run.IsPendingHammerReward) Incident(result, run, cards);
    }

    // ---------------------------------------------------------------- combat

    void PlayerTurn(RunResult result, CardManager cards, CombatManager combat)
    {
        if (UseTricks(cards, combat)) return;
        var hand = cards.hand.ToList();
        var enemies = combat.Enemies.Where(e => !e.IsDefeated).ToList();
        if (hand.Count == 0) { combat.Recover(); result.turns++; return; }

        var actions = new List<CardInstance[]>();
        foreach (var card in hand) actions.Add(new[] { card });
        foreach (var group in hand.GroupBy(card => card.Rank).Where(g => g.Count() >= 2))
        {
            var ranked = group.ToArray();
            actions.Add(ranked);
            if (ranked.Length > 2) actions.Add(ranked.Take(2).ToArray());
        }
        foreach (var ace in hand.Where(card => card.MatchesRank(CardData.Rank.Ace)))
            foreach (var other in hand.Where(card => card != ace))
                actions.Add(new[] { ace, other });

        CardInstance[] best = null;
        EnemyRuntime bestTarget = null;
        double bestScore = double.MinValue;
        foreach (var target in enemies)
            foreach (var action in actions)
            {
                if (!combat.CanPlayCards(action, target)) continue;
                int damage = combat.PreviewActionTotalDamage(action, target);
                bool kills = damage >= target.currentHp && target.ShieldCharges == 0;
                int spent = action.Sum(c => (int)c.Rank);
                // Kill the most dangerous enemy cheaply; a Shield eats a whole hit, so strip it with the
                // cheapest card; otherwise push damage into the enemy closest to dying.
                double score = kills
                    ? 1000 + target.EffectiveAttack * 10 - action.Length * 15 - spent * 3
                    : target.ShieldCharges > 0
                        ? 50 + target.EffectiveAttack - spent * 2 - (action.Length - 1) * 20
                        : Math.Min(damage, target.currentHp) - (action.Length - 1) * 6 - target.currentHp * 0.05 + target.EffectiveAttack;
                if (score > bestScore) { bestScore = score; best = action; bestTarget = target; }
            }
        if (best == null)
        {
            if (!combat.PassPlayerTurn()) combat.Recover();
            result.turns++;
            return;
        }
        if (combat.TryPlayCards(best, bestTarget)) result.turns++;
    }

    void Defend(CardManager cards, CombatManager combat)
    {
        int incoming = combat.IncomingDamageAfterModifiers;
        var (hp, maxHp) = PolicyHealth(combat);
        var target = combat.Enemies.FirstOrDefault(e => !e.IsDefeated);
        var hand = cards.hand.OrderBy(card => (int)card.Rank).ToList();
        if (incoming <= 0 || target == null || hand.Count == 0) { combat.TakeRemainingDamage(); return; }

        bool lethal = incoming >= hp;
        bool low = hp - incoming < maxHp * 0.35f;
        int keep = lethal ? 0 : low ? 1 : 3;
        var chosen = new List<CardInstance>();
        int blocked = 0;
        // Small hits are cheaper in HP than in cards; a cheap card that covers most of a big hit is a good trade.
        if (!lethal && !low)
        {
            var cheap = incoming < 4 ? null
                : hand.Where(card => combat.CalculateCardDefense(card) >= incoming * 0.75)
                    .OrderBy(card => combat.CalculateCardDefense(card)).FirstOrDefault();
            if (cheap == null || hand.Count <= keep) { combat.TakeRemainingDamage(); return; }
            chosen.Add(cheap);
        }
        else
        {
            foreach (var card in hand.OrderByDescending(card => combat.CalculateCardDefense(card)))
            {
                if (hand.Count - chosen.Count <= keep) break;
                var proposal = chosen.Append(card).ToList();
                if (!combat.CanDefendWithCards(proposal, target)) continue;
                chosen = proposal;
                blocked += combat.CalculateCardDefense(card);
                if (incoming - blocked < hp * 0.5) break;
            }
        }
        if (chosen.Count == 0 || !combat.TryDefendWithCards(chosen, target)) combat.TakeRemainingDamage();
    }

    bool UseTricks(CardManager cards, CombatManager combat)
    {
        var enemies = combat.Enemies.Where(e => !e.IsDefeated).ToList();
        for (int slot = 0; slot < cards.BackpackSlotsUsed; slot++)
        {
            var trick = cards.GetConsumableAtSlot(slot);
            if (trick == null || !cards.CanUseConsumableAtSlot(slot, combat)) continue;
            var hand = cards.hand.ToList();
            switch (trick.effectType)
            {
                case ConsumableEffectType.Heal:
                    if (combat.player.maxHealth - combat.player.currentHealth >= trick.healAmount &&
                        cards.UseConsumableAtSlot(slot, combat)) return true;
                    break;
                case ConsumableEffectType.DirectEnemyDamage:
                    var victim = enemies.Where(e => combat.CanUseConsumableDamage(trick.damageAmount, e))
                        .OrderByDescending(e => e.currentHp <= trick.damageAmount).ThenByDescending(e => e.EffectiveAttack).FirstOrDefault();
                    if (victim != null && cards.UseConsumableAtSlot(slot, combat, victim)) return true;
                    break;
                case ConsumableEffectType.ApplyEnhancement:
                    var modTarget = hand.Where(c => c.Enhancement == null && cards.CanTargetConsumableAtSlot(slot, c.Id))
                        .OrderByDescending(c => (int)c.Rank).FirstOrDefault();
                    if (modTarget != null && cards.UseEnhancementConsumableAtSlot(slot, modTarget.Id)) return true;
                    break;
                case ConsumableEffectType.DuplicateCard:
                    var cloned = hand.Where(c => cards.CanTargetConsumableAtSlot(slot, c.Id)).OrderByDescending(c => (int)c.Rank).FirstOrDefault();
                    if (cloned != null && cards.UseDeckMutationConsumableAtSlot(slot, new[] { cloned.Id })) return true;
                    break;
                case ConsumableEffectType.DestroyCards:
                    var cut = hand.Where(c => cards.CanTargetConsumableAtSlot(slot, c.Id)).OrderBy(c => (int)c.Rank).FirstOrDefault();
                    if (cut != null && cards.ownedCards.Count > 30 && cards.UseDeckMutationConsumableAtSlot(slot, new[] { cut.Id })) return true;
                    break;
                case ConsumableEffectType.ChangeSuit:
                    var recolour = hand.Where(c => c.Suit != trick.targetSuit && cards.CanTargetConsumableAtSlot(slot, c.Id))
                        .OrderByDescending(c => (int)c.Rank).FirstOrDefault();
                    if (recolour != null && cards.UseDeckMutationConsumableAtSlot(slot, new[] { recolour.Id })) return true;
                    break;
            }
        }
        return false;
    }

    // ---------------------------------------------------------------- route, Black Market, Incidents

    void ChooseNode(RunResult result, RunManager run, CardManager cards, CombatManager combat)
    {
        var open = run.currentPath.Where(n => n.accessible && !n.completed).ToList();
        if (open.Count == 0) return;
        var health = PolicyHealth(combat);
        float hpRatio = health.hp / (float)Math.Max(1, health.max);
        int credits = cards.credits;
        double Score(PathNode node) => node.kind switch
        {
            MapNodeType.Shop => credits >= 20 ? 6 : credits >= 10 ? 3 : 0.5,
            MapNodeType.Event => hpRatio < 0.45f ? 5 : 3,
            MapNodeType.Combat => hpRatio < 0.45f ? 1 : 4,
            MapNodeType.Elite => hpRatio >= 0.8f && run.CurrentMapIndex >= 1 ? 4.5 : 0.2,
            MapNodeType.Risk => hpRatio >= 0.7f ? 3.5 : 0.1,
            MapNodeType.Headliner => 2,
            _ => 1
        };
        var next = open.OrderByDescending(Score).ThenBy(n => n.id).First();
        if (next.kind is MapNodeType.Combat or MapNodeType.Elite or MapNodeType.Headliner) result.encounters++;
        if (next.kind == MapNodeType.Shop) result.shops++;
        if (next.kind is MapNodeType.Event or MapNodeType.Risk) result.incidents++;
        run.OnPathChosen(next.id);
    }

    static int RarityRank(string rarity) => rarity switch { "Legendary" => 4, "Epic" => 3, "Rare" => 2, _ => 1 };

    void Shop(RunManager run, CardManager cards, CombatManager combat)
    {
        var offers = run.GetCurrentShopOffers().Where(o => !o.purchased && run.CanPurchaseShopOffer(o.StableId)).ToList();
        var supply = offers.FirstOrDefault(o => o.kind == ShopOfferKind.Supply);
        if (supply != null && cards.HandCount < 5 && cards.credits >= 3 && run.PurchaseSupply(supply.StableId, 1)) return;
        var health = PolicyHealth(combat);
        float hpRatio = health.hp / (float)Math.Max(1, health.max);
        var heal = offers.FirstOrDefault(o => o.kind == ShopOfferKind.Consumable && o.consumable != null &&
            o.consumable.effectType == ConsumableEffectType.Heal);
        if (heal != null && hpRatio < 0.6f && run.PurchaseShopOffer(heal.StableId)) return;
        var gear = offers.Where(o => o.kind == ShopOfferKind.Gear && o.gear != null)
            .OrderByDescending(o => RarityRank(o.gear.rarity)).ThenByDescending(o => o.price).FirstOrDefault();
        if (gear != null && run.PurchaseShopOffer(gear.StableId)) return;
        var trick = offers.Where(o => o.kind is ShopOfferKind.Consumable or ShopOfferKind.Enhancement)
            .OrderBy(o => o.price).FirstOrDefault();
        if (trick != null && cards.credits - trick.price >= 5 && run.PurchaseShopOffer(trick.StableId)) return;
        if (supply != null && cards.HandCount < 7 && cards.credits >= 6 && run.PurchaseSupply(supply.StableId, 1)) return;
        run.OnShopDone();
    }

    void Incident(RunResult result, RunManager run, CardManager cards)
    {
        if (run.IsPendingHammerReward) { Hammer(run); return; }
        var incident = run.ActiveEvent;
        var available = Enumerable.Range(0, incident.choices?.Length ?? 0).Where(run.CanChooseEventOption).ToList();
        if (available.Count == 0)
        {
            if (!run.LeaveBlockedEvent()) result.note = "incident with no available choice: " + incident.id;
            return;
        }
        var health = PolicyHealth(CombatManager.Instance);
        float hpRatio = health.hp / (float)Math.Max(1, health.max);
        double ChoiceScore(int index)
        {
            var option = incident.choices[index];
            double score = _botRandom.NextDouble() * 0.5;
            if (option.effects != null)
                foreach (var effect in option.effects)
                    score += effect.type switch
                    {
                        RunEffectType.Heal => effect.amount * (hpRatio < 0.6f ? 1.5 : 0.3),
                        RunEffectType.GainMaxHealth => effect.amount * 1.2,
                        RunEffectType.GainCredits => effect.amount * 0.5,
                        RunEffectType.LoseCredits or RunEffectType.LoseCreditsUpTo => -effect.amount * 0.4,
                        RunEffectType.LoseHealth or RunEffectType.LoseHealthNonLethal => -effect.amount * (hpRatio < 0.5f ? 2.0 : 0.8),
                        RunEffectType.DrawCards => effect.amount * 2.0,
                        RunEffectType.GainConsumable => 3,
                        RunEffectType.RevealMapNode => 0.5,
                        RunEffectType.RandomGearEnhancementOrNothing => 3,
                        _ => 0
                    };
            if (option.interaction is RunEventChoiceInteraction.DiscardTwoForRandomEnhancement) score += 2;
            return score;
        }
        int choice = available.OrderByDescending(ChoiceScore).First();
        var interaction = incident.choices[choice].interaction;
        var hand = cards.hand.OrderBy(c => (int)c.Rank).ToList();
        bool done = interaction switch
        {
            RunEventChoiceInteraction.Immediate => run.ChooseEventOption(choice),
            RunEventChoiceInteraction.DiscardTwoForRandomEnhancement =>
                hand.Count >= 2 && run.ChooseEventDiscardAndReward(choice, hand.Take(2).Select(c => c.Id).ToList()),
            RunEventChoiceInteraction.DiscardCardsForTotalValue => DiscardForValue(run, choice, hand),
            _ => false
        };
        if (!done)
        {
            // Fall back to any immediate choice, then LEAVE.
            foreach (int other in available)
                if (incident.choices[other].interaction == RunEventChoiceInteraction.Immediate && run.ChooseEventOption(other)) return;
            run.LeaveBlockedEvent();
        }
    }

    // The Hammer offers Mods (taken as Mod Tricks); take the first one there is room for, else decline.
    static void Hammer(RunManager run)
    {
        var incident = run.ActiveEvent;
        for (int i = 0; incident?.choices != null && i < incident.choices.Length; i++)
            foreach (var mod in run.GetEventEnhancementsForChoice(i))
                if (string.IsNullOrEmpty(run.GetEventModUnavailableReason(i, mod)) && run.ChooseEventMod(i, mod)) return;
        run.DeclineHammerReward();
    }

    static bool DiscardForValue(RunManager run, int choice, List<CardInstance> hand)
    {
        var picked = new List<int>();
        foreach (var card in hand)
        {
            picked.Add(card.Id);
            if (run.CanChooseEventDiscardForTotalValue(choice, picked)) return run.ChooseEventDiscardForTotalValue(choice, picked);
        }
        return false;
    }
}
