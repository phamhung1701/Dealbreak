using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// HUD slice S16: the pending enemy drop and LEAVE IT sit at the top of the stage, centred; they show
/// only while RunManager holds a drop; claim and leave go through RunManager; the slap is instant
/// under Reduce Motion.
/// </summary>
public sealed class EnemyDropLayoutTests
{
    // The stage spans x .06-.88 (hud.md section C); the drop row is centred in it.
    const float StageCentreX = .47f;

    readonly List<Object> _created = new();
    ConsumableData _trick;
    int _trickUses;
    RunManager _run;
    CardManager _cards;
    EnemyDropUI _ui;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        CutSheetMotion.ReduceMotionOverride = null;
        _trick = AssetDatabase.LoadAssetAtPath<ConsumableData>("Assets/Data/Tricks/EnergyBar.asset");
        Assert.That(_trick, Is.Not.Null);
        _trickUses = _trick.uses;
        _cards = Make<CardManager>("Drop Layout Cards");
        _cards.Configure(null, null, null, System.Array.Empty<GearData>(),
            System.Array.Empty<CardEnhancementData>(), new[] { _trick });
        _run = Make<RunManager>("Drop Layout Run");
        var canvas = Make<Canvas>("Drop Layout Canvas");
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        EnemyDropUI.Install(canvas);
        _ui = canvas.GetComponentInChildren<EnemyDropUI>(true);
        Assert.That(_ui, Is.Not.Null);
    }

    [TearDown]
    public void TearDown()
    {
        GameplayInputGate.Clear();
        CutSheetMotion.ReduceMotionOverride = null;
        if (_trick != null) _trick.uses = _trickUses;
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) Object.DestroyImmediate(_created[i]);
        _created.Clear();
    }

    [Test]
    public void DropRow_SitsAtTheTopOfTheStage_Centred()
    {
        var row = _ui.Row;
        Assert.That(row, Is.Not.Null);
        Assert.That(row.parent, Is.SameAs(_ui.transform));
        Assert.That(row.anchorMin.y, Is.EqualTo(.74f).Within(.01f));
        Assert.That(row.anchorMax.y, Is.EqualTo(.82f).Within(.01f));
        Assert.That((row.anchorMin.x + row.anchorMax.x) * .5f, Is.EqualTo(StageCentreX).Within(.01f),
            "The row is centred on the stage.");

        var token = _ui.Token;
        var leave = (RectTransform)_ui.LeaveButton.transform;
        Assert.That(token.parent, Is.SameAs(row));
        Assert.That(leave.parent, Is.SameAs(row));
        foreach (var rect in new[] { token, leave })
        {
            Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(.5f, .5f)), rect.name);
            Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(.5f, .5f)), rect.name);
            Assert.That(rect.anchoredPosition.y, Is.EqualTo(0f).Within(.01f), rect.name + " sits on the row's centre line.");
        }
        Assert.That(token.anchoredPosition.x, Is.LessThan(leave.anchoredPosition.x), "The token is left of LEAVE IT.");
        float left = token.anchoredPosition.x - token.sizeDelta.x * .5f;
        float right = leave.anchoredPosition.x + leave.sizeDelta.x * .5f;
        Assert.That(left + right, Is.EqualTo(0f).Within(.01f), "The token and LEAVE IT are centred as a pair.");
        Assert.That(right - left, Is.LessThan(EnemyDropUI.TokenSize + EnemyDropUI.LeaveWidth + 60f));
        Assert.That(token.sizeDelta, Is.EqualTo(new Vector2(150f, 150f)), "The item sticker is 150px.");
    }

    [Test]
    public void LeaveIt_IsACutSheetPaperButton()
    {
        Assert.That(_ui.LeaveButton.TryGetComponent(out EditorialButton style), Is.True);
        Assert.That(style.cutSheet, Is.True);
        Assert.That(style.role, Is.EqualTo(EditorialButton.Role.Paper));
    }

    [Test]
    public void Drop_ShowsOnlyWhileRunManagerHoldsOne()
    {
        _ui.Bind(_run);
        Assert.That(_ui.Showing, Is.False, "No pending drop, no token.");
        Assert.That(_ui.LeaveButton.gameObject.activeSelf, Is.False, "No pending drop, no LEAVE IT.");

        SetPendingDrop(_trick);
        Assert.That(_ui.Showing, Is.True);
        Assert.That(_ui.LeaveButton.gameObject.activeSelf, Is.True);
        Assert.That(_ui.Token.anchoredPosition, Is.EqualTo(_ui.TokenHome));

        Assert.That(_run.LeavePendingEnemyDrop(), Is.True);
        Assert.That(_ui.Showing, Is.False, "The token goes once the drop is resolved.");
        Assert.That(_ui.LeaveButton.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void LeaveIt_CallsRunManagerLeave()
    {
        SetPendingDrop(_trick);
        _ui.LeaveButton.onClick.Invoke();
        Assert.That(_run.PendingEnemyDrop, Is.Null, "LEAVE IT resolves the drop through RunManager.");
        Assert.That(_cards.GetConsumableCount(_trick), Is.Zero, "Leaving it does not add it.");
        Assert.That(_ui.Showing, Is.False);
    }

    [Test]
    public void ClickingTheToken_WithRoom_CallsRunManagerClaim()
    {
        SetPendingDrop(_trick);
        ConsumableData claimed = null;
        _run.OnEnemyDropClaimed += drop => claimed = drop;
        _ui.Click();
        Assert.That(claimed, Is.SameAs(_trick), "The claim goes through RunManager.ClaimPendingEnemyDrop.");
        Assert.That(_run.PendingEnemyDrop, Is.Null);
        Assert.That(_cards.GetConsumableCount(_trick), Is.EqualTo(1));
        Assert.That(_ui.Showing, Is.False);
    }

    [Test]
    public void Slap_IsInstantUnderReduceMotion()
    {
        CutSheetMotion.ReduceMotionOverride = true;
        SetPendingDrop(_trick);
        Assert.That(_ui.Showing, Is.True);
        Assert.That(_ui.IsSlapping, Is.False, "Under Reduce Motion the drop is simply there.");
        Assert.That(_ui.Token.localScale, Is.EqualTo(Vector3.one));
        Assert.That(_ui.LeaveButton.transform.localScale, Is.EqualTo(Vector3.one));
    }

    [Test]
    public void Slap_PlaysOnArrival_AndSettlesWithin200ms()
    {
        CutSheetMotion.ReduceMotionOverride = false;
        SetPendingDrop(_trick);
        Assert.That(_ui.IsSlapping, Is.True, "The drop slaps in when it arrives.");
        Assert.That(_ui.Token.localScale.x, Is.GreaterThan(1f));
        Assert.That(_ui.LeaveButton.transform.localScale, Is.EqualTo(_ui.Token.localScale), "Token and LEAVE IT slap together.");
        Assert.That(EnemyDropUI.SlapInSeconds, Is.EqualTo(.12f).Within(.0001f));
        Assert.That(EnemyDropUI.SlapSettleSeconds, Is.EqualTo(.08f).Within(.0001f));

        _ui.TickSlap(Time.unscaledTime + .2f + .001f);
        Assert.That(_ui.IsSlapping, Is.False);
        Assert.That(_ui.Token.localScale, Is.EqualTo(Vector3.one));
        Assert.That(_ui.LeaveButton.transform.localScale, Is.EqualTo(Vector3.one));
    }

    // Holds a drop as the full-backpack path does, then binds so the layer reads it.
    void SetPendingDrop(ConsumableData drop)
    {
        typeof(RunManager).GetField("_pendingEnemyDrop", System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic).SetValue(_run, drop);
        _ui.Bind(null);
        _ui.Bind(_run);
    }

    T Make<T>(string name) where T : Component
    {
        var gameObject = new GameObject(name);
        _created.Add(gameObject);
        return gameObject.AddComponent<T>();
    }
}
