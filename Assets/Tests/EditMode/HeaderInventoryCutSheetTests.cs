using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// HUD slice S7: the header inventory rows in the Cut Sheet look (visual-design 3b). Slots keep an
// untilted root as the hit area and drop target; the tilt, art, shadow, ring and discs live on
// children that never take raycasts.
public sealed class HeaderInventoryCutSheetTests
{
    readonly List<Object> _created = new();
    CardManager _cards;

    [SetUp]
    public void SetUp()
    {
        Assert.That(DealbreakUIStyle.Theme, Is.Not.Null, "The Cut Sheet path draws from the theme asset.");
        _cards = Create("Cards").AddComponent<CardManager>();
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i]) Object.DestroyImmediate(_created[i]);
        _created.Clear();
        CutSheetMotion.ReduceMotionOverride = null;
    }

    [Test]
    public void EmptyGearSlots_ShowTheDashedOutline_OccupiedOnesDoNot()
    {
        var rail = Rail(out var slots);
        Assert.That(_cards.BuyGear(Gear("dash-a", 1), 0), Is.True);
        rail.Refresh();

        Assert.That(slots.childCount, Is.EqualTo(CardManager.BASE_GEAR_CAPACITY));
        for (int i = 0; i < slots.childCount; i++)
        {
            var sticker = slots.GetChild(i).GetComponent<GearIconSlotUI>().Sticker;
            Assert.That(sticker, Is.Not.Null);
            bool empty = i > 0;
            Assert.That(sticker.Dashed.enabled, Is.EqualTo(empty), $"slot {i}");
            if (empty)
            {
                Assert.That(sticker.Dashed.sprite, Is.SameAs(DealbreakUIStyle.Theme.slotDashedSprite));
                Assert.That(sticker.Dashed.color, Is.EqualTo(DealbreakUIStyle.Theme.paper), "Paper at full strength.");
                Assert.That(sticker.Shadow.Depth, Is.EqualTo(0f), "An empty slot has no shadow.");
            }
        }
        var tier = slots.GetChild(0).GetComponent<GearIconSlotUI>().Sticker;
        Assert.That(tier.BadgeShown(0), Is.False, "(owner 2026-10-28) No tier number disc on Gear.");
    }

    [Test]
    public void EmptyTrickSlots_ShowTheDashedOutlineOnlyInsideBackpackCapacity()
    {
        var actions = Actions(CardManager.BASE_BACKPACK_CAPACITY + 2, out var buttons);
        Refresh(actions);

        for (int i = 0; i < buttons.Length; i++)
        {
            bool inside = i < _cards.BackpackCapacity;
            Assert.That(buttons[i].gameObject.activeSelf, Is.EqualTo(inside), $"slot {i}");
            bool dashed = buttons[i].TryGetComponent(out CutSheetSlotUI sticker) && sticker.Dashed.enabled;
            Assert.That(dashed, Is.EqualTo(inside), $"slot {i}: dashed only for BackpackCapacity slots");
        }
    }

    [Test]
    public void Tilt_LivesOnTheFace_AndTheRootRectAndRotationAreUnchanged()
    {
        var rail = Rail(out var slots);
        rail.Refresh();
        for (int i = 0; i < slots.childCount; i++)
        {
            var root = (RectTransform)slots.GetChild(i);
            var sticker = root.GetComponent<GearIconSlotUI>().Sticker;
            Assert.That(root.localRotation, Is.EqualTo(Quaternion.identity), $"slot {i} root stays untilted");
            Assert.That(root.sizeDelta, Is.EqualTo(new Vector2(rail.slotSize, rail.slotSize)), $"slot {i} root keeps its size");
            Assert.That(sticker.Face.parent, Is.SameAs(root));
            Assert.That(Mathf.DeltaAngle(0f, sticker.Face.localEulerAngles.z),
                Is.EqualTo(CutSheetMetrics.SlotTilt(i)).Within(.001f), $"slot {i} reads the fixed table");
            Assert.That(Mathf.Abs(CutSheetMetrics.SlotTilt(i)), Is.LessThanOrEqualTo(CutSheetMetrics.SlotTiltMax));
            Assert.That(sticker.Badges.localRotation, Is.EqualTo(Quaternion.identity), "Discs sit on an untilted parent.");
        }
    }

    [Test]
    public void SlotParts_AreNeverRaycastTargets_OnlyTheRootTakesClicks()
    {
        var rail = Rail(out var slots);
        var texture = new Texture2D(8, 8); _created.Add(texture);
        var sprite = Sprite.Create(texture, new Rect(0, 0, 8, 8), new Vector2(.5f, .5f)); _created.Add(sprite);
        var gear = Gear("ray-a", 1);
        gear.iconSprite = sprite;
        Assert.That(_cards.BuyGear(gear, 0), Is.True);
        rail.Refresh();
        var actions = Actions(CardManager.BASE_BACKPACK_CAPACITY, out var buttons);
        Refresh(actions);

        var roots = new List<Transform>();
        for (int i = 0; i < slots.childCount; i++) roots.Add(slots.GetChild(i));
        foreach (var button in buttons) roots.Add(button.transform);
        foreach (var root in roots)
        {
            Assert.That(root.GetComponent<Image>().raycastTarget, Is.True, $"{root.name} root is the hit area");
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
                if (graphic.gameObject != root.gameObject)
                    Assert.That(graphic.raycastTarget, Is.False, $"{root.name}/{graphic.name}");
        }
        var sticker = slots.GetChild(0).GetComponent<GearIconSlotUI>().Sticker;
        Assert.That(sticker.Shadow.Depth, Is.EqualTo(CutSheetMetrics.ShadowSlot), "The icon's own alpha, 3px down-right.");
        Assert.That(sticker.Shadow.Image.sprite, Is.SameAs(sprite));
        Assert.That(ItemIconUI.Shown(slots.GetChild(0)), Is.SameAs(sprite), "The art is found through the root.");
    }

    [Test]
    public void TrickChargesAndStack_AreDiscsOnTheSlotsUntiltedParent()
    {
        var trick = ScriptableObject.CreateInstance<ConsumableData>(); _created.Add(trick);
        trick.id = "s7_charges";
        trick.displayName = "Charged";
        trick.icon = "C";
        trick.uses = 3;
        _cards.Configure(null, null, null, System.Array.Empty<GearData>(), System.Array.Empty<CardEnhancementData>(), new[] { trick });
        Assert.That(_cards.AddConsumable(trick), Is.True);
        var actions = Actions(CardManager.BASE_BACKPACK_CAPACITY, out var buttons);
        Refresh(actions);

        var sticker = buttons[0].GetComponent<CutSheetSlotUI>();
        Assert.That(sticker.BadgeShown(0), Is.True, "Charges show on the slot.");
        var disc = sticker.Badge(0);
        Assert.That(disc.IsChildOf(sticker.Badges), Is.True);
        Assert.That(disc.IsChildOf(sticker.Face), Is.False, "Not on the tilted face.");
        Assert.That(sticker.Badges.parent, Is.SameAs(buttons[0].transform));
        Assert.That(sticker.Badges.localRotation, Is.EqualTo(Quaternion.identity));
        Assert.That(sticker.Dashed.enabled, Is.False);
        var glyph = buttons[0].GetComponentInChildren<TMP_Text>();
        Assert.That(glyph.transform.IsChildOf(sticker.Face), Is.True, "The glyph prints on the tilted face.");
        Assert.That(glyph.text, Is.EqualTo("C"), "The count is no longer folded into the glyph.");
    }

    [Test]
    public void HeadingStrips_AreTagSpritesInPaperForGearAndKraftForTricks_WithInkLabels()
    {
        var theme = DealbreakUIStyle.Theme;
        var strips = new[] { ("Gear Panel", theme.paper), ("Tricks Panel", theme.kraft) };
        foreach (var (name, tint) in strips)
        {
            var parent = CreateUI(name + " Host");
            var heading = CreateUI("Heading", parent).gameObject.AddComponent<TextMeshProUGUI>();
            heading.text = "Tricks";
            var count = CreateUI("Count", parent).gameObject.AddComponent<TextMeshProUGUI>();
            var slot = CreateUI("Slot", parent);
            var panel = InventoryPanelUI.Install(name, parent, new[] { heading.rectTransform, count.rectTransform, slot },
                new TMP_Text[] { heading, count }, tint, theme.inkPrint, theme.tagSprite);

            var strip = panel.HeadingStrip;
            Assert.That(strip.sprite, Is.SameAs(theme.tagSprite), name);
            Assert.That(strip.color, Is.EqualTo(tint), name);
            Assert.That(strip.raycastTarget, Is.False);
            Assert.That(strip.GetComponentInChildren<CutSheetShadow>(true), Is.Null, "The strip has no shadow.");
            foreach (var label in new TMP_Text[] { heading, count })
            {
                Assert.That(label.color, Is.EqualTo(theme.inkPrint), $"{name}: ink, never red text on kraft");
                Assert.That(label.fontSize, Is.EqualTo(InventoryPanelUI.StripTextSize));
                Assert.That(label.fontStyle & FontStyles.UpperCase, Is.EqualTo(FontStyles.UpperCase));
                if (theme.labelFont) Assert.That(label.font, Is.SameAs(theme.labelFont));
            }
        }
    }

    [Test]
    public void DiscNumbers_StayCourierInk_AfterTheHierarchyIsRestyled()
    {
        var theme = DealbreakUIStyle.Theme;
        var trick = Trick("s7_restyle", 3);
        Assert.That(_cards.AddConsumable(trick), Is.True);
        var actions = Actions(CardManager.BASE_BACKPACK_CAPACITY, out var buttons);
        Refresh(actions);
        var sticker = buttons[0].GetComponent<CutSheetSlotUI>();

        // Restyling the whole HUD (DealbreakUIRuntime.RefreshStyles) runs after the slots are dressed.
        DealbreakUIStyle.ApplyHierarchy(actions.transform, new HashSet<Object>());
        Refresh(actions);

        var label = sticker.BadgeLabel(0);
        Assert.That(sticker.BadgeShown(0), Is.True);
        if (theme.numericFont) Assert.That(label.font, Is.SameAs(theme.numericFont), "Courier Prime Bold");
        Assert.That(label.color, Is.EqualTo(theme.inkPrint));
        Assert.That(label.fontSize, Is.EqualTo(CutSheetSlotUI.DiscText));
        if (label.font) Assert.That(label.fontSharedMaterial, Is.SameAs(label.font.material), "No outline material.");
        Assert.That(buttons[0].TryGetComponent(out EditorialButton style) && style.enabled, Is.False,
            "The neo-punk button styler (which recolours child labels) is off on a dressed slot.");
        var frame = buttons[0].transform.Find("Editorial Frame");
        Assert.That(frame && frame.gameObject.activeSelf, Is.False, "The old charcoal square does not draw.");
    }

    [Test]
    public void EveryDressingGraphic_LiesInsideTheSlotRect()
    {
        var rail = Rail(out var slots);
        Assert.That(_cards.BuyGear(Gear("inside-a", 1), 0), Is.True);
        rail.Refresh();
        var trick = Trick("s7_inside", 3);
        Assert.That(_cards.AddConsumable(trick, 2), Is.True);
        var actions = Actions(CardManager.BASE_BACKPACK_CAPACITY, out var buttons);
        Refresh(actions);

        var roots = new List<RectTransform>();
        for (int i = 0; i < slots.childCount; i++) roots.Add((RectTransform)slots.GetChild(i));
        foreach (var button in buttons) roots.Add((RectTransform)button.transform);
        var corners = new Vector3[4];
        foreach (var root in roots)
        {
            var sticker = root.GetComponent<CutSheetSlotUI>();
            sticker.OnSelect(null); // the ring is shown, as on focus
            Assert.That(sticker.RingShown, Is.True);
            var bounds = root.rect;
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(false))
            {
                if (graphic.gameObject == root.gameObject || !graphic.enabled) continue;
                // A shadow sits offset down-right from its source; it must stay inside too.
                graphic.rectTransform.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    Vector2 local = root.InverseTransformPoint(corner);
                    Assert.That(local.x, Is.InRange(bounds.xMin - .01f, bounds.xMax + .01f), $"{root.name}/{graphic.name} x");
                    Assert.That(local.y, Is.InRange(bounds.yMin - .01f, bounds.yMax + .01f), $"{root.name}/{graphic.name} y");
                }
            }
        }
    }

    [Test]
    public void Hover_LiftsTheFaceUpLeft_AndTheShadowStays()
    {
        var rail = Rail(out var slots);
        Assert.That(_cards.BuyGear(Gear("hover-a", 1), 0), Is.True);
        rail.Refresh();
        var sticker = slots.GetChild(0).GetComponent<GearIconSlotUI>().Sticker;
        var shadowBefore = sticker.Shadow.transform.parent.localPosition;

        sticker.OnPointerEnter(null);

        Assert.That(sticker.Face.offsetMin, Is.EqualTo(CutSheetMetrics.HoverFaceOffset));
        Assert.That(sticker.Face.offsetMax, Is.EqualTo(CutSheetMetrics.HoverFaceOffset));
        Assert.That(sticker.RingShown, Is.True);
        Assert.That(sticker.Shadow.transform.parent.localPosition, Is.EqualTo(shadowBefore));
        sticker.OnPointerExit(null);
        Assert.That(sticker.Face.offsetMin, Is.EqualTo(Vector2.zero));
        Assert.That(sticker.RingShown, Is.False);
    }

    [Test]
    public void TrickRow_ReadsTheTiltTableFromItsOffset()
    {
        var actions = Actions(CardManager.BASE_BACKPACK_CAPACITY, out var buttons);
        Refresh(actions);
        for (int i = 0; i < buttons.Length; i++)
        {
            var face = buttons[i].GetComponent<CutSheetSlotUI>().Face;
            Assert.That(Mathf.DeltaAngle(0f, face.localEulerAngles.z),
                Is.EqualTo(CutSheetMetrics.SlotTilt(i + CutSheetMetrics.TrickRowTiltOffset)).Within(.001f), $"Trick slot {i}");
            Assert.That(buttons[i].transform.localRotation, Is.EqualTo(Quaternion.identity));
        }
    }

    [TestCase("Common")]
    [TestCase("Rare")]
    [TestCase("Epic")]
    [TestCase("Legendary")]
    public void BetterGear_WearsItsTierAuraColourOnItsFace_CommonNone(string rarity)
    {
        var slot = AuraSlot(rarity, out var sticker, out var fx);
        if (rarity == "Common")
        {
            Assert.That(fx == null || fx.Layer == null, Is.True, "Common gets no aura.");
            return;
        }
        Assert.That(fx, Is.Not.Null);
        Assert.That(fx.CurrentTier, Is.EqualTo(ItemTierFxUI.Of(slot.Gear)), "The shop's tier source.");
        Assert.That(fx.Layer.gameObject.activeSelf, Is.True);
        Assert.That(fx.Layer.IsChildOf(sticker.Face), Is.True, "The aura tilts with the sticker.");
        Assert.That(fx.Layer.GetComponentsInChildren<TMP_Text>(true), Is.Empty, "No named tag in the header.");
        var theme = DealbreakUIStyle.Theme;
        var aura = rarity switch { "Rare" => theme.rareAura, "Epic" => theme.epicAura, _ => theme.legendaryAura };
        Assert.That(ItemTierFxUI.AuraColor(fx.CurrentTier), Is.EqualTo(aura));
        var corners = new Vector3[4];
        var root = (RectTransform)slot.transform;
        int lit = 0;
        foreach (var graphic in fx.Layer.GetComponentsInChildren<Graphic>(true))
        {
            Assert.That(graphic.raycastTarget, Is.False, graphic.name);
            var c = graphic.color;
            if (c.a > 0f) lit++;
            Assert.That(new Vector3(c.r, c.g, c.b), Is.EqualTo(new Vector3(aura.r, aura.g, aura.b)),
                $"{rarity}: every aura part is the tier's theme colour");
            if (c.a <= 0f) continue;
            graphic.rectTransform.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                Vector2 local = root.InverseTransformPoint(corner);
                Assert.That(root.rect.Contains(local), Is.True, $"{rarity}/{graphic.name} stays inside the masked slot");
            }
        }
        Assert.That(lit, Is.GreaterThan(0));
        Assert.That(fx.Layer.GetComponentsInChildren<Image>(true), Has.Length.EqualTo(ItemTierFxUI.PartCount(fx.CurrentTier)),
            "Rare one frame; Epic double frame and a wire; Legendary double frame, halo, two wires, eight sparks.");
        Assert.That(sticker.BadgeShown(0), Is.False);
    }

    [TestCase("Rare")]
    [TestCase("Epic")]
    [TestCase("Legendary")]
    public void ReduceMotion_LeavesOnlyTheStaticFrame(string rarity)
    {
        CutSheetMotion.ReduceMotionOverride = true;
        AuraSlot(rarity, out _, out var fx);
        var parts = fx.Layer.GetComponentsInChildren<Image>(true);
        // The static structure alone tells the tier: Rare 4 bars, Epic 8 (double frame), Legendary 12 (plus the halo).
        int structure = ItemTierFxUI.StaticBars(fx.CurrentTier);
        Assert.That(structure, Is.EqualTo(rarity switch { "Rare" => 4, "Epic" => 8, _ => 12 }));
        for (int i = 0; i < parts.Length; i++)
        {
            bool shown = i < structure;
            Assert.That(parts[i].color.a > 0f, Is.EqualTo(shown), $"{rarity} part {i}: {(shown ? "structure shows" : "no wires or sparks")}");
        }
        var alphas = new float[parts.Length];
        for (int i = 0; i < parts.Length; i++) alphas[i] = parts[i].color.a;
        fx.Tick();
        for (int i = 0; i < parts.Length; i++)
            Assert.That(parts[i].color.a, Is.EqualTo(alphas[i]), $"{rarity} part {i}: no breathing or pulse");
        Assert.That(parts[0].color.a, Is.GreaterThanOrEqualTo(.85f - .001f), "The outer frame reads strongly.");
        if (rarity == "Legendary")
            Assert.That(parts[8].color.a, Is.EqualTo(.25f).Within(.01f), "The halo is a faint band.");
    }

    GearIconSlotUI AuraSlot(string rarity, out CutSheetSlotUI sticker, out ItemTierFxUI fx)
    {
        var rail = Rail(out var slots);
        var texture = new Texture2D(8, 8); _created.Add(texture);
        var sprite = Sprite.Create(texture, new Rect(0, 0, 8, 8), new Vector2(.5f, .5f)); _created.Add(sprite);
        var gear = Gear("aura-" + rarity, 1);
        gear.rarity = rarity;
        gear.iconSprite = sprite;
        Assert.That(_cards.BuyGear(gear, 0), Is.True);
        rail.Refresh();
        var slot = slots.GetChild(0).GetComponent<GearIconSlotUI>();
        sticker = slot.Sticker;
        sticker.Face.TryGetComponent(out fx);
        if (fx) fx.Tick();
        return slot;
    }

    // ---- Fixtures.

    ConsumableData Trick(string id, int uses)
    {
        var trick = ScriptableObject.CreateInstance<ConsumableData>(); _created.Add(trick);
        trick.id = id;
        trick.displayName = id;
        trick.icon = "T";
        trick.uses = uses;
        _cards.Configure(null, null, null, System.Array.Empty<GearData>(), System.Array.Empty<CardEnhancementData>(), new[] { trick });
        return trick;
    }

    GearRailUI Rail(out RectTransform slots)
    {
        var host = Create("Rail");
        host.SetActive(false);
        var rail = host.AddComponent<GearRailUI>();
        slots = CreateUI("Slots");
        rail.source = _cards;
        rail.slotsRoot = slots;
        rail.slotSize = 100f;
        rail.capacityLabel = CreateUI("Capacity").gameObject.AddComponent<TextMeshProUGUI>();
        host.SetActive(true);
        return rail;
    }

    ActionButtonsUI Actions(int count, out Button[] buttons)
    {
        var root = Create("Actions", typeof(RectTransform));
        root.SetActive(false);
        var actions = root.AddComponent<ActionButtonsUI>();
        buttons = new Button[count];
        for (int i = 0; i < count; i++)
        {
            var slot = Create($"Trick Slot {i}", typeof(RectTransform), typeof(Image), typeof(Button));
            slot.transform.SetParent(root.transform, false);
            ((RectTransform)slot.transform).sizeDelta = new Vector2(100, 100);
            buttons[i] = slot.GetComponent<Button>();
            buttons[i].targetGraphic = slot.GetComponent<Image>();
            var label = Create($"Trick Label {i}", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(slot.transform, false);
        }
        actions.backpackSlotButtons = buttons;
        return actions;
    }

    void Refresh(ActionButtonsUI actions) => typeof(ActionButtonsUI)
        .GetMethod("RefreshBackpackSlots", BindingFlags.Instance | BindingFlags.NonPublic)
        .Invoke(actions, new object[] { _cards, null });

    GearData Gear(string id, int tier)
    {
        var gear = ScriptableObject.CreateInstance<GearData>();
        gear.id = id;
        gear.displayName = id;
        gear.icon = "G";
        gear.tier = tier;
        _created.Add(gear);
        return gear;
    }

    GameObject Create(string name, params System.Type[] components)
    {
        var go = new GameObject(name, components);
        _created.Add(go);
        return go;
    }

    RectTransform CreateUI(string name, Transform parent = null)
    {
        var go = Create(name, typeof(RectTransform));
        if (parent) go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }
}
