using System.Linq;
using NUnit.Framework;
using UnityEngine;

// Headliner generation reads Resources/HeadlinerCatalog. These golden sequences were recorded from the
// pre-catalog code, so a reordered or edited pool shows up as a seed-breaking change.
public sealed class HeadlinerCatalogTests
{
    [TestCase("GOLDEN-A", "JackHearts:30/6/10:Guarded | JackDiamonds:30/6/10:DoubleStrike | JackSpades:30/6/10:Guarded | JackClubs:30/6/10:Silence | QueenSpades:30/6/15:HeavySwing | QueenClubs:30/6/15:Silence | QueenHearts:30/6/15:Silence | QueenDiamonds:30/6/15:Guarded | KingDiamonds:30/6/20:FullCoverageBlock+HeavySwing | KingClubs:30/6/20:Oppression+HeavySwing | KingHearts:30/6/20:Guarded+HeavySwing | KingSpades:30/6/20:Withering+FullCoverageBlock")]
    [TestCase("GOLDEN-B", "JackDiamonds:30/6/10:Regeneration | JackClubs:30/6/10:Withering | JackSpades:30/6/10:Withering | JackHearts:30/6/10:Withering | QueenHearts:30/6/15:Oppression | QueenClubs:30/6/15:FullCoverageBlock | QueenDiamonds:30/6/15:Regeneration | QueenSpades:30/6/15:DoubleStrike | KingSpades:30/6/20:Guarded+DoubleStrike | KingHearts:30/6/20:Withering+DoubleStrike | KingClubs:30/6/20:Guarded+HeavySwing | KingDiamonds:30/6/20:Guarded+Desperation")]
    [TestCase("CARDPG", "JackSpades:30/6/10:Withering | JackHearts:30/6/10:Oppression | JackClubs:30/6/10:Guarded | JackDiamonds:30/6/10:Silence | QueenClubs:30/6/15:Regeneration | QueenSpades:30/6/15:FullCoverageBlock | QueenHearts:30/6/15:HeavySwing | QueenDiamonds:30/6/15:Withering | KingHearts:30/6/20:Guarded+DoubleStrike | KingClubs:30/6/20:Silence+Desperation | KingDiamonds:30/6/20:Guarded+HeavySwing | KingSpades:30/6/20:Oppression+FullCoverageBlock")]
    public void HeadlinerSequence_MatchesPreCatalogGeneration(string seed, string expected)
    {
        var host = new GameObject("Headliner Golden");
        try
        {
            var run = host.AddComponent<RunManager>();
            run._randomContext = new RunRandomContext(seed);
            run.BuildHeadlinerDeck();
            string actual = string.Join(" | ", run.headlinerDeck.Select(h =>
                $"{h.sourceCard.rank}{h.sourceCard.suit}:{h.maxHp}/{h.baseAttack}/{h.creditsReward}:" +
                string.Join("+", h.abilities.Select(a => a.effect))));
            Assert.That(actual, Is.EqualTo(expected));
            run.Reset();
        }
        finally { Object.DestroyImmediate(host); }
    }

    [Test]
    public void Catalog_NamesEveryFaceCardAndDescribesEveryPooledAbility()
    {
        var catalog = HeadlinerCatalog.Load();
        Assert.That(catalog, Is.Not.Null);
        Assert.That(catalog.names.Select(n => (n.rank, n.suit)).Distinct().Count(), Is.EqualTo(12));
        Assert.That(catalog.names.All(n => !string.IsNullOrWhiteSpace(n.displayName)), Is.True);
        var pooled = catalog.jackAndQueenAbilities.Concat(catalog.kingHardCounters).Concat(catalog.kingHardCounterPartners)
            .Concat(catalog.kingDefensiveAbilities).Concat(catalog.kingOffensiveAbilities).Distinct();
        foreach (var effect in pooled)
            Assert.That(catalog.abilityText.Any(text => text.effect == effect && !string.IsNullOrWhiteSpace(text.description)),
                Is.True, effect.ToString());
    }
}
