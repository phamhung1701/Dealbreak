using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class DealbreakUIOverhaulTests
{
    [Test]
    public void ThemeHasLicensedFontSourcesPrintAssetsAndSuitFallbacks()
    {
        var theme=AssetDatabase.LoadAssetAtPath<DealbreakUITheme>("Assets/Resources/DealbreakUITheme.asset");
        Assert.That(theme,Is.Not.Null);
        foreach(var font in new[]{theme.headingFont,theme.bodyFont,theme.numericFont})
        {
            Assert.That(font.sourceFontFile,Is.Not.Null);Assert.That(font.material.mainTexture,Is.Not.Null);
            foreach(char symbol in "♥♦♣♠")Assert.That(font.HasCharacter(symbol,true,true),Is.True,$"{font.name}: {symbol}");
        }
        foreach(var texture in new[]{theme.paperTexture,theme.slateTexture,theme.darkTexture,theme.dryAccents,theme.cleanAccents})
            Assert.That(texture,Is.Not.Null);
        Assert.That(theme.inkAccentMaterial.shader.name,Is.EqualTo("UI/Dealbreak Ink Accent"));
    }
    [TestCase("Assets/Scenes/Game.unity")]
    [TestCase("Assets/Scenes/MainMenu.unity")]
    public void AuthoredScreensHaveNativeFramesAndCompleteDetailBindings(string path)
    {
        var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;
        if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
        try
        {
            var roots=scene.GetRootGameObjects();
            foreach(var obj in roots.SelectMany(r=>r.GetComponentsInChildren<Transform>(true)))
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(obj.gameObject),Is.Zero,obj.name);
            foreach(var frame in roots.SelectMany(r=>r.GetComponentsInChildren<EditorialFrame>(true)))
            {Assert.That(frame.GetComponent<CanvasRenderer>(),Is.Not.Null);Assert.That(frame.raycastTarget,Is.False);}
            var details=roots.SelectMany(r=>r.GetComponentsInChildren<ContextualTooltipUI>(true)).Single();
            Assert.That(details.panel&&details.title&&details.body&&details.scroll,Is.True);
            if(path.Contains("Game"))
            {
                var runtime=roots.SelectMany(r=>r.GetComponentsInChildren<DealbreakUIRuntime>(true)).Single(r=>r.actions);
                Assert.That(runtime.combatGroups.All(g=>g),Is.True);Assert.That(runtime.inventoryGroups.All(g=>g),Is.True);
                Assert.That(runtime.handCount,Is.Not.Null);
                Assert.That(runtime.market.creditsText,Is.Not.Null);
                Assert.That(runtime.actions.GetComponent<RunStatusUI>().timingLabel.gameObject.activeSelf,Is.False);
                Assert.That(runtime.handCount.rectTransform.anchorMax.y,Is.LessThan(runtime.actions.handField.transform.parent.GetComponent<RectTransform>().anchorMin.y));
            }
        }
        finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
    }
}
