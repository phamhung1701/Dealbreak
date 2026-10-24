using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The playable characters (owner, 2026-10-24): six portraits from the painted character pack,
/// each with one simple perk, Balatro-deck style. Creates or updates their data assets, registers
/// them in RunSaveContent, and lays out the character row on the run setup screen.
/// </summary>
public static class DealbreakCharacterAuthoring
{
    const string ArtFolder = "Assets/Art/Characters", DataFolder = "Assets/Data/Characters";

    struct Spec
    {
        public string Id, Name, Description, GearRarity;
        public int MaxHealth, Credits, HandSize, GearSlots, TrickSlots;
    }

    // Character-select order. Portraits come from Tools/PrintArt/make_portraits.py.
    static readonly Spec[] Roster =
    {
        new() { Id = "chr_rook", Name = "Rook", Description = "+10 max HP.", MaxHealth = 10 },
        new() { Id = "chr_dex", Name = "Dex", Description = "+1 Gear slot.", GearSlots = 1 },
        new() { Id = "chr_nova", Name = "Nova", Description = "+1 Trick slot.", TrickSlots = 1 },
        new() { Id = "chr_scarlet", Name = "Scarlet", Description = "+1 hand size.", HandSize = 1 },
        new() { Id = "chr_glitch", Name = "Glitch", Description = "Starts with a random Common Gear.", GearRarity = "Common" },
        new() { Id = "chr_velvet", Name = "Velvet", Description = "Starts with 15 Credits.", Credits = 15 },
    };

    [MenuItem("Dealbreak/Characters/Create Character Assets")]
    public static void CreateCharacters()
    {
        if (!AssetDatabase.IsValidFolder(DataFolder)) AssetDatabase.CreateFolder("Assets/Data", "Characters");
        var characters = Roster.Select(spec =>
        {
            string path = $"{DataFolder}/{spec.Name}.asset";
            var data = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
            if (!data) { data = ScriptableObject.CreateInstance<CharacterData>(); AssetDatabase.CreateAsset(data, path); }
            data.id = spec.Id;
            data.displayName = spec.Name;
            data.description = spec.Description;
            data.portrait = ImportPortrait($"{ArtFolder}/{spec.Name}.png");
            data.maxHealthBonus = spec.MaxHealth;
            data.startingCredits = spec.Credits;
            data.startingGearRarity = spec.GearRarity ?? string.Empty;
            data.handSizeBonus = spec.HandSize;
            data.gearSlotBonus = spec.GearSlots;
            data.trickSlotBonus = spec.TrickSlots;
            EditorUtility.SetDirty(data);
            return data;
        }).ToArray();

        var registry = SaveContentCatalog.Load();
        registry.characters = characters;
        EditorUtility.SetDirty(registry);
        AssetDatabase.SaveAssets();
    }

    static Sprite ImportPortrait(string path)
    {
        if (AssetImporter.GetAtPath(path) is TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // The run setup card, top to bottom in 1080p reference pixels from the centre (y up): the title,
    // the character row, then the seed row and the buttons, each moved down to make room.
    [MenuItem("Dealbreak/UI/Apply Character Select Layout")]
    public static void ApplyCharacterSelectLayout()
    {
        const string path = "Assets/Scenes/MainMenu.unity";
        var scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            var setup = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<RunSetupUI>(true)).First();
            var card = setup.transform.Find("RunSetupCard");
            Move(card, "Title", 380);
            Move(card, "SeedLabel", -80);
            Move(card, "SeedInput", -132);
            Move(card, "RandomSeedButton", -132);
            Move(card, "SeedPreviewLabel", -198);
            Move(card, "StartRunButton", -280);
            Move(card, "BackButton", -370);

            var picker = DealbreakUIStyle.Child(card, "CharacterPicker", typeof(CharacterPickerUI));
            picker.anchorMin = picker.anchorMax = new Vector2(.5f, .5f);
            picker.pivot = new Vector2(.5f, 1f);
            picker.anchoredPosition = new Vector2(0, 320);
            picker.sizeDelta = new Vector2(1320, 320);
            picker.SetSiblingIndex(card.Find("Title").GetSiblingIndex() + 1);
            setup.characterPicker = picker.GetComponent<CharacterPickerUI>();
            EditorUtility.SetDirty(setup);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    static void Move(Transform card, string name, float y)
    {
        var rect = (RectTransform)card.Find(name);
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
    }
}
