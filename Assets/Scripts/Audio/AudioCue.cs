/// <summary>A sound the game can play. Fired by presenters when the player sees the moment, never by rules.</summary>
public enum AudioCue
{
    UiPress,
    CardThrow,
    BlockImpact,
    EnemyHit,
    EnemyDefeat,
    PerfectBlock,
    PlayerHurt,
    ShieldHit,
    TriggerHop,
    CoinLand,
    HealLand,
    ItemPickUp,
    ItemSlap,
    RewardCard,
    Victory,
    Defeat
}

/// <summary>How one cue plays: its clip under Resources, bus, level, pitch spread and pile-up limits.</summary>
public readonly struct AudioCueSpec
{
    public readonly string Clip;
    public readonly AudioBus Bus;
    public readonly float Volume;
    public readonly float PitchSpread;
    public readonly float MinGapSeconds;
    public readonly int MaxVoices;

    public AudioCueSpec(string clip, AudioBus bus, float volume, float pitchSpread, float minGapSeconds, int maxVoices)
    {
        Clip = clip;
        Bus = bus;
        Volume = volume;
        PitchSpread = pitchSpread;
        MinGapSeconds = minGapSeconds;
        MaxVoices = maxVoices;
    }
}

public static class AudioCues
{
    public const string ResourceFolder = "Audio/Sfx/";

    public static AudioCueSpec Spec(AudioCue cue) => cue switch
    {
        AudioCue.UiPress => new AudioCueSpec("ui_press", AudioBus.Ui, .7f, .03f, .04f, 2),
        AudioCue.CardThrow => new AudioCueSpec("card_throw", AudioBus.Sfx, .6f, .05f, .03f, 3),
        AudioCue.BlockImpact => new AudioCueSpec("block_impact", AudioBus.Sfx, .85f, .04f, .04f, 3),
        AudioCue.EnemyHit => new AudioCueSpec("enemy_hit", AudioBus.Sfx, .9f, .05f, .04f, 3),
        AudioCue.EnemyDefeat => new AudioCueSpec("enemy_defeat", AudioBus.Sfx, .9f, .03f, .06f, 2),
        AudioCue.PerfectBlock => new AudioCueSpec("perfect_block", AudioBus.Sfx, .85f, 0f, .15f, 1),
        AudioCue.PlayerHurt => new AudioCueSpec("player_hurt", AudioBus.Sfx, 1f, .03f, .1f, 2),
        AudioCue.ShieldHit => new AudioCueSpec("shield_hit", AudioBus.Sfx, .85f, .03f, .1f, 2),
        AudioCue.TriggerHop => new AudioCueSpec("trigger_hop", AudioBus.Sfx, .65f, 0f, .03f, 3),
        AudioCue.CoinLand => new AudioCueSpec("coin_land", AudioBus.Sfx, .55f, .06f, .03f, 3),
        AudioCue.HealLand => new AudioCueSpec("heal_land", AudioBus.Sfx, .55f, .04f, .05f, 2),
        AudioCue.ItemPickUp => new AudioCueSpec("item_pick_up", AudioBus.Ui, .6f, .05f, .05f, 1),
        AudioCue.ItemSlap => new AudioCueSpec("item_slap", AudioBus.Sfx, .75f, .05f, .05f, 2),
        AudioCue.RewardCard => new AudioCueSpec("reward_card", AudioBus.Sfx, .9f, 0f, .2f, 1),
        AudioCue.Victory => new AudioCueSpec("run_victory", AudioBus.Sfx, 1f, 0f, 1f, 1),
        AudioCue.Defeat => new AudioCueSpec("run_defeat", AudioBus.Sfx, 1f, 0f, 1f, 1),
        _ => default
    };
}
