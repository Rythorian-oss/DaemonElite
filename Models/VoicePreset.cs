// >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>

// DaemonElite: (Voice Changer) 
// Copyright: (C) 2026 Justin Linwood Ross

// >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
namespace DaemonElite.Models
{
    public sealed record VoicePreset
    {
        public required string Name { get; init; }
        public required string Category { get; init; }
        public required string Glyph { get; init; }
        public float PitchFactor { get; init; } = 1f;
        public float ReverbMix { get; init; }
        public float ReverbTime { get; init; }
        public int EchoDelay { get; init; }
        public float EchoFeedback { get; init; }
        public float Distortion { get; init; }
        public float TremoloRate { get; init; }
        public float TremoloDepth { get; init; }

        public string Description
        {
            get
            {
                var effects = new List<string>();
                if (Math.Abs(PitchFactor - 1f) > .01f) effects.Add($"Pitch {PitchFactor:F2}x");
                if (ReverbMix > .01f) effects.Add($"Reverb {ReverbMix * 100:F0}%");
                if (EchoDelay > 0) effects.Add($"Echo {EchoDelay}ms");
                if (Distortion > .01f) effects.Add($"Drive {Distortion * 100:F0}%");
                if (TremoloRate > .01f) effects.Add($"Tremolo {TremoloRate:F1}Hz");
                return effects.Count == 0 ? "Clean signal / no processing" : string.Join("  •  ", effects);
            }
        }

        public static IReadOnlyList<VoicePreset> GetBuiltInPresets() =>
        [
            new() { Name = "Normal", Category = "STANDARD", Glyph = "N", PitchFactor = 1f },
            new() { Name = "Deep Male", Category = "HUMAN", Glyph = "D", PitchFactor = .65f, ReverbMix = .08f, ReverbTime = .3f },
            new() { Name = "Female", Category = "HUMAN", Glyph = "F", PitchFactor = 1.4f },
            new() { Name = "Child", Category = "HUMAN", Glyph = "C", PitchFactor = 1.7f },
            new() { Name = "Chipmunk", Category = "FUN", Glyph = "C", PitchFactor = 2.2f },
            new() { Name = "Robot", Category = "SYNTHETIC", Glyph = "R", PitchFactor = .85f, TremoloRate = 25f, TremoloDepth = .9f, Distortion = .15f },
            new() { Name = "Alien", Category = "SYNTHETIC", Glyph = "A", PitchFactor = 1.3f, ReverbMix = .35f, ReverbTime = 1.2f, EchoDelay = 120, EchoFeedback = .3f },
            new() { Name = "Demon", Category = "MONSTER", Glyph = "D", PitchFactor = .45f, Distortion = .35f, ReverbMix = .2f, ReverbTime = .8f },
            new() { Name = "Giant", Category = "MONSTER", Glyph = "G", PitchFactor = .55f, ReverbMix = .15f, ReverbTime = .6f },
            new() { Name = "Radio", Category = "EFFECT", Glyph = "R", PitchFactor = 1f, Distortion = .1f, EchoDelay = 50, EchoFeedback = .1f },
            new() { Name = "Underwater", Category = "EFFECT", Glyph = "U", PitchFactor = .9f, ReverbMix = .5f, ReverbTime = 2f, EchoDelay = 200, EchoFeedback = .4f },
            new() { Name = "Telephone", Category = "EFFECT", Glyph = "T", PitchFactor = 1f, Distortion = .2f, EchoDelay = 30, EchoFeedback = .05f },
            new() { Name = "Cathedral", Category = "AMBIENT", Glyph = "C", PitchFactor = 1f, ReverbMix = .6f, ReverbTime = 3.5f },
            new() { Name = "Stadium", Category = "AMBIENT", Glyph = "S", PitchFactor = 1f, ReverbMix = .4f, ReverbTime = 2f, EchoDelay = 150, EchoFeedback = .25f },
            new() { Name = "Tiny", Category = "FUN", Glyph = "T", PitchFactor = 1.9f, EchoDelay = 40, EchoFeedback = .1f }
        ];
    }
}