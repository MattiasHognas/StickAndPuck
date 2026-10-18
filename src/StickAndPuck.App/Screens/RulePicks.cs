using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Save;
using StickAndPuck.App.Season.Schedule;

namespace StickAndPuck.App.Screens
{
    /// <summary>Which rules a match starts with: the arcade entries always play <see cref="RuleSet.Arcade"/>;
    /// a season or playoff game plays the rules stored in the season save, which its first game stores
    /// from the player's choice; every other game plays the player's choice from settings.json.
    /// SAP_RULES=shl|arcade|nhl picks a preset as it stands for headless runs and is never saved.</summary>
    internal static class RulePicks
    {
        /// <summary>Gets the player's rule choice, under SAP_RULES.</summary>
        public static RuleChoice Choice(AppState app)
        {
            string name = Env.Get("SAP_RULES");
            if (name is { Length: > 0 })
            {
                string trimmed = name.Trim();
                return Enum.TryParse(trimmed, ignoreCase: true, out RulePreset p)
                    ? RuleChoice.Of(p)
                    : throw new InvalidOperationException(
                        $"SAP_RULES={name}: use shl, arcade or nhl"
                    );
            }
            return app.Screens.Prefs.Rules;
        }

        /// <summary>Gets the rules a match of this plan plays.</summary>
        public static RuleSet For(AppState app, MatchPlan plan)
        {
            if (plan.Mode.TryGet(out IGameMode mode) && Fixed(mode).TryGet(out RuleSet fixedRules))
            {
                return fixedRules;
            }

            if (!plan.Season || !app.Season.Current.TryGet(out SeasonState season))
            {
                return Choice(app).Resolve();
            }

            if (season.Rules.TryGet(out RuleSet stored))
            {
                return stored;
            }

            RuleSet rules = Choice(app).Resolve();
            season.Rules = rules;
            if (app.Season.SavePath.Length > 0)
            {
                SeasonSave.Write(season, app.Season.SavePath);
            }

            Console.WriteLine($"SAP: screens: season stores {rules.Name} rules");
            return rules;
        }

        /// <summary>Gets the rules a mode is fixed to, or None when the player picks: the arcade entries play Arcade.</summary>
        public static Maybe<RuleSet> Fixed(IGameMode mode)
        {
            return mode is ArcadeMode ? RuleSet.Arcade : Maybe.None<RuleSet>();
        }
    }
}
