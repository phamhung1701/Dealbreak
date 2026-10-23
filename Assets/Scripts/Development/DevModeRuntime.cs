#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
#else
using UnityEngine;
#endif

// Session-only activation; PlayerPrefs stores option defaults, never an enabled state.
public static class DevModeRuntime
{
    const string Prefix = "CardPG.DevMode.";
    const string EnabledKey = Prefix + "Enabled";
    const string InfiniteHealthKey = Prefix + "InfiniteHealth";
    const string InfiniteMoneyKey = Prefix + "InfiniteMoney";
    const string UnlimitedShopOffersKey = Prefix + "UnlimitedShopOffers";
    static bool _enabled;

    public static bool Available => Application.isEditor || Debug.isDebugBuild;
    public static bool Enabled
    {
        get
        {
#if UNITY_EDITOR
            return Available && (_enabled || PlayerPrefs.GetInt(EnabledKey, 0) != 0);
#elif DEVELOPMENT_BUILD
            return Available && _enabled;
#else
            return false;
#endif
        }
    }
    public static bool InfiniteHealth => Enabled && PlayerPrefs.GetInt(InfiniteHealthKey, 1) != 0;
    public static bool InfiniteMoney => Enabled && PlayerPrefs.GetInt(InfiniteMoneyKey, 1) != 0;
    // (owner, 2026-10-23) Dev mode always stocks every Gear, Trick and service at a Black Market.
    public static bool UnlimitedShopOffers => Enabled;

    public static void Configure(bool enabled, bool infiniteHealth, bool infiniteMoney, bool unlimitedShopOffers)
    {
        PlayerPrefs.SetInt(InfiniteHealthKey, infiniteHealth ? 1 : 0);
        PlayerPrefs.SetInt(InfiniteMoneyKey, infiniteMoney ? 1 : 0);
        PlayerPrefs.SetInt(UnlimitedShopOffersKey, unlimitedShopOffers ? 1 : 0);
        PlayerPrefs.Save();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        _enabled = enabled && Available;
#else
        _enabled = false;
#endif
#if UNITY_EDITOR
        PlayerPrefs.SetInt(EnabledKey, _enabled ? 1 : 0);
        PlayerPrefs.Save();
#endif
    }

    public static void Disable()
    {
        _enabled = false;
#if UNITY_EDITOR
        PlayerPrefs.SetInt(EnabledKey, 0);
        PlayerPrefs.Save();
#endif
    }
    /// <summary>Dev "lose now": waits for attack presentation, then asks combat to resolve a defeat.</summary>
    public static bool TryForceDefeat()
    {
        var presentation = AttackCardPresentationUI.Instance;
        if (presentation != null && presentation.IsBusy) return false;
        return CombatManager.Instance != null && CombatManager.Instance.ForceDefeatForDevelopment();
    }

    public static void ResetSession()
    {
        _enabled = false;
#if UNITY_EDITOR
        PlayerPrefs.SetInt(EnabledKey, 0);
        PlayerPrefs.Save();
#endif
    }
}
