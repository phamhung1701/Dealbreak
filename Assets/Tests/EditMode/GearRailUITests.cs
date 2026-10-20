using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class GearRailUITests
{
    readonly List<Object> _created = new();
    CardManager _cards;
    GearRailUI _rail;
    RectTransform _slots;
    TMP_Text _capacity;
    TMP_Text _tooltip;
    TMP_Text _detail;
    GameObject _tooltipPanel;
    GameObject _detailPanel;
    Button _close;

    [SetUp]
    public void SetUp()
    {
        _cards = Create("Cards").AddComponent<CardManager>();
        var host = Create("Rail");
        host.SetActive(false);
        _rail = host.AddComponent<GearRailUI>();
        _slots = CreateUI("Slots").GetComponent<RectTransform>();
        _capacity = CreateUI("Capacity").AddComponent<TextMeshProUGUI>();
        _tooltipPanel = CreateUI("Tooltip");
        _tooltip = _tooltipPanel.AddComponent<TextMeshProUGUI>();
        _tooltipPanel.SetActive(false);
        _detailPanel = CreateUI("Detail");
        _detail = _detailPanel.AddComponent<TextMeshProUGUI>();
        _detailPanel.SetActive(false);
        _close = CreateUI("Close").AddComponent<Button>();
        _rail.source = _cards;
        _rail.slotsRoot = _slots;
        _rail.capacityLabel = _capacity;
        _rail.tooltipPanel = _tooltipPanel;
        _rail.tooltipText = _tooltip;
        _rail.detailPanel = _detailPanel;
        _rail.detailText = _detail;
        _rail.detailCloseButton = _close;
        host.SetActive(true);
        InvokeRail("OnEnable"); // EditMode does not consistently run MonoBehaviour lifecycle callbacks.
        _rail.Refresh();
    }

    [TearDown]
    public void TearDown()
    {
        if (_rail) InvokeRail("OnDisable");
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i]) Object.DestroyImmediate(_created[i]);
        _created.Clear();
    }

    [Test]
    public void EmptyInventory_ShowsFiveBlankSlots()
    {
        Assert.That(_slots.childCount, Is.EqualTo(CardManager.BASE_GEAR_CAPACITY));
        for (int i = 0; i < _slots.childCount; i++)
        {
            var slot = _slots.GetChild(i).GetComponent<GearIconSlotUI>();
            Assert.That(_slots.GetChild(i).gameObject.activeSelf, Is.True);
            Assert.That(slot.Gear, Is.Null);
            Assert.That(string.IsNullOrEmpty(slot.GetComponentInChildren<TMP_Text>(true).text), Is.True);
        }
        Assert.That(_rail.transform.GetComponentsInChildren<Button>(true), Is.Empty, "The rail has no merge buttons.");
    }

    [Test]
    public void DroppingGearOnAMatchingCopyMergesIntoThatSlot()
    {
        GameplayInputGate.Clear();
        var common = Gear("common", "Common", "C", "Common detail");
        common.canonicalId = "family-common";
        common.tier = 1;
        var next = Gear("rare", "Rare", "R", "Rare detail");
        next.canonicalId = "family-rare";
        next.tier = 2;
        next.upgradeFromId = "family-common";
        var other = Gear("other", "Other", "O", "Other detail");
        _cards.gearCatalog.AddRange(new[] { common, next, other });
        Assert.That(_cards.BuyGear(common, 0), Is.True);
        Assert.That(_cards.BuyGear(other, 0), Is.True);
        Assert.That(_cards.BuyGear(common, 0), Is.True);
        long dragged = _cards.GetGearInstanceAt(0).Id;
        long target = _cards.GetGearInstanceAt(2).Id;

        Assert.That(ItemDropRules.CanDropGear(dragged, ItemDropTarget.OnGear(dragged), out _), Is.False,
            "A Gear cannot merge with itself.");
        Assert.That(ItemDropRules.CanDropGear(dragged, ItemDropTarget.OnGear(target), out string preview), Is.True);
        Assert.That(preview, Does.Contain("RARE"));
        Assert.That(_cards.ownedGear.Count, Is.EqualTo(3), "The preview changes nothing.");
        Assert.That(ItemDropRules.DropGear(dragged, ItemDropTarget.OnGear(target)), Is.True);
        Assert.That(_cards.ownedGear, Is.EqualTo(new[] { other, next }), "The merged Gear lands in the slot it was dropped on.");
        Assert.That(_slots.GetChild(1).GetComponent<GearIconSlotUI>().Gear, Is.SameAs(next));
    }

    [Test]
    public void DroppingGearOnADifferentFamilyDoesNotMerge()
    {
        GameplayInputGate.Clear();
        var firstGear = Gear("one", "One", "1", "One detail");
        firstGear.canonicalId = "family-one";
        firstGear.tier = 1;
        var secondGear = Gear("two", "Two", "2", "Two detail");
        secondGear.canonicalId = "family-two";
        secondGear.tier = 1;
        _cards.gearCatalog.AddRange(new[] { firstGear, secondGear });
        Assert.That(_cards.BuyGear(firstGear, 0), Is.True);
        Assert.That(_cards.BuyGear(secondGear, 0), Is.True);
        long first = _cards.GetGearInstanceAt(0).Id;
        long second = _cards.GetGearInstanceAt(1).Id;
        Assert.That(ItemDropRules.CanDropGear(first, ItemDropTarget.OnGear(second), out _), Is.False);
        Assert.That(ItemDropRules.DropGear(first, ItemDropTarget.OnGear(second)), Is.False);
        Assert.That(_cards.ownedGear.Count, Is.EqualTo(2));
    }

    [Test]
    public void Acquisition_ShowsOnlyIconUntilHoveredOrClicked()
    {
        var gear = Gear("relic-1", "Hidden Name", "★", "Detail body");
        Assert.That(_cards.BuyGear(gear, 0), Is.True);
        Assert.That(_capacity.text, Does.Contain("1/5"));
                Assert.That(_slots.childCount, Is.EqualTo(CardManager.BASE_GEAR_CAPACITY));
        Assert.That(Enumerable.Range(0, CardManager.BASE_GEAR_CAPACITY)
            .All(i => _slots.GetChild(i).gameObject.activeSelf), Is.True,
            "The rail keeps a stable five-slot inventory footprint.");
        Assert.That(Enumerable.Range(1, CardManager.BASE_GEAR_CAPACITY - 1)
            .Select(i => _slots.GetChild(i).GetComponent<GearIconSlotUI>().Gear),
            Is.All.Null, "Unowned capacity remains represented by quiet empty slots.");
        var slot = _slots.GetChild(0).GetComponent<GearIconSlotUI>();
        Assert.That(slot.Gear, Is.SameAs(gear));
        Assert.That(slot.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("★"));
        Assert.That(_capacity.text, Does.Not.Contain("Hidden Name"));
        Assert.That(_tooltipPanel.activeSelf, Is.False);
        Assert.That(_detailPanel.activeSelf, Is.False);

        slot.OnPointerEnter(null);
        Assert.That(_tooltipPanel.activeSelf, Is.True);
        Assert.That(_tooltip.text, Does.Contain("Hidden Name"));
        slot.OnPointerExit(null);
        Assert.That(_tooltipPanel.activeSelf, Is.False);
        slot.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
        Assert.That(_detailPanel.activeSelf, Is.True);
        Assert.That(_detail.text, Does.Contain("Detail body"));
        _close.onClick.Invoke();
        Assert.That(_detailPanel.activeSelf, Is.False);
    }

    [Test]
    public void SelectingGearShowsAdjacentDetailsUpdatesSelectionAndEmptySlotsStayBlank()
    {
        var first = Gear("first-detail", "First Name", "A", "First description");
        var second = Gear("second-detail", "Second Name", "B", "Second description");
        Assert.That(_cards.BuyGear(first, 0), Is.True);
        Assert.That(_cards.BuyGear(second, 0), Is.True);
        InvokeRail("Refresh");
        var firstSlot = _slots.GetChild(0).GetComponent<GearIconSlotUI>();
        var secondSlot = _slots.GetChild(1).GetComponent<GearIconSlotUI>();
        firstSlot.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
        Assert.That(firstSlot.IsDetailSelected, Is.True);
        Assert.That(_detailPanel.activeSelf, Is.True);
        Assert.That(_detail.text, Does.Contain("First Name").And.Contain("First description"));
        var detailRect = _detailPanel.GetComponent<RectTransform>();
        Assert.That(detailRect.rect.width, Is.LessThanOrEqualTo(240f), "Artifact information remains a compact side card, not a modal.");
        Assert.That(detailRect.rect.height, Is.LessThanOrEqualTo(220f));

        secondSlot.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
        Assert.That(firstSlot.IsDetailSelected, Is.False);
        Assert.That(secondSlot.IsDetailSelected, Is.True);
        Assert.That(_detail.text, Does.Contain("Second Name").And.Contain("Second description"));
        string selectedDescription = _detail.text;
        _slots.GetChild(2).GetComponent<GearIconSlotUI>()
            .OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
        Assert.That(_detail.text, Is.EqualTo(selectedDescription));
        Assert.That(_slots.GetChild(2).GetComponent<GearIconSlotUI>().Gear, Is.Null);

        secondSlot.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
        Assert.That(_detailPanel.activeSelf, Is.False);
        Assert.That(secondSlot.IsDetailSelected, Is.False);
    }

    [Test]
    public void EnemyTargetingClosesGearDetails()
    {
        Assert.That(_cards.BuyGear(Gear("target-a", "Target A", "A", "Readable description"), 0), Is.True);
        Assert.That(_cards.BuyGear(Gear("target-b", "Target B", "B", "Other description"), 0), Is.True);
        var slot = _slots.GetChild(0).GetComponent<GearIconSlotUI>();
        slot.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
        Assert.That(_detailPanel.activeSelf, Is.True);
        Assert.That(_detail.fontSize, Is.GreaterThanOrEqualTo(18f));
        slot.OnPointerEnter(null);
        Assert.That(_tooltipPanel.activeSelf, Is.False, "Selected details must not be covered by a duplicate hover tooltip.");
        _cards.BeginEnemyTargeting(_ => { }, _ => true);
        Assert.That(_detailPanel.activeSelf, Is.False);
        Assert.That(slot.IsDetailSelected, Is.False);
        _cards.EndEnemyTargeting();
    }

    [Test]
    public void Reset_ClearsSlotsAndOpenDetail_ThenAcquisitionReusesSlot()
    {
        var first = Gear("first", "First", "A", "First detail");
        var second = Gear("second", "Second", "B", "Second detail");
        _cards.BuyGear(first, 0);
        var slot = _slots.GetChild(0).GetComponent<GearIconSlotUI>();
        slot.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
        _cards.Reset();
        Assert.That(_capacity.text, Does.Contain("0/5"));
        Assert.That(slot.gameObject.activeSelf, Is.True, "The same slot remains visible as an empty inventory slot.");
        Assert.That(slot.Gear, Is.Null);
        Assert.That(_detailPanel.activeSelf, Is.False);
        _cards.BuyGear(second, 0);
        Assert.That(_slots.childCount, Is.EqualTo(CardManager.BASE_GEAR_CAPACITY));
        Assert.That(slot.Gear, Is.SameAs(second));
        Assert.That(slot.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("B"));
    }

    void InvokeRail(string method) => typeof(GearRailUI)
        .GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        .Invoke(_rail, null);

    GameObject Create(string name)
    {
        var go = new GameObject(name);
        _created.Add(go);
        return go;
    }

    GameObject CreateUI(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        _created.Add(go);
        return go;
    }

    GearData Gear(string id, string name, string icon, string description)
    {
        var gear = ScriptableObject.CreateInstance<GearData>();
        gear.id = id;
        gear.displayName = name;
        gear.icon = icon;
        gear.description = description;
        _created.Add(gear);
        return gear;
    }
}
