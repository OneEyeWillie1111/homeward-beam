namespace Abduction.Core
{
    /// <summary>In-game Help menu pages (title, body).</summary>
    public static class HelpPages
    {
        public static readonly (string Title, string Body)[] Pages =
        {
            ("Goal", GameData.Intro + "\n\nEach deck has a glowing teleporter pad: stand on it and tap BEAM to go down. Your role sets how many decks: Stargazer 10, Abductee 20, Specimen 35, Doomed 50. The Coiled Sovereign guards the machine home on the last deck."),
            ("Moving", "FORWARD / BACK step one tile. TURN L / TURN R face a new direction.\nOPEN opens the hatch or supply locker in front of you.\nThe map in the corner fills in as you explore: you are the arrow, cyan is a teleporter, gold is the machine home, red dots are aliens you've seen."),
            ("Fighting", "Walk into an alien to fight. Each turn: ATTACK with your equipped weapon, USE the held item (food heals), or FLEE (not from the boss). The alien strikes back after every action.\nThe enemy's health bar appears at the top of the view."),
            ("Health", "WAR (red bar) is your body. SPIRIT (blue bar) is your mind. Both run 0-99.\nWar aliens (red eyes) hurt War; spirit aliens (cyan eyes) hurt Spirit. If either bar empties you are re-cloned on deck 1 with weaker maximums."),
            ("Ring inventory", "Your 8 items orbit your head. Tap a slot (or < RING / RING >) to hold it.\nRed ring = equipped weapon (used by ATTACK). Blue ring = worn armor (best armor, automatic). Gold = the item you're holding.\nWeapons hurt war aliens fully; psionic items hurt spirit aliens fully and cost 1 Spirit."),
            ("Aliens", "Weakest to strongest: Brood Drudge & Shade Thrall, Husk Stalker & Pale Vessel, Mantis Lancer & Mantis Oracle, Half-Scale Sentinel & Gilded Herald, the Grays, and the Serpent Lords (Saurians). Deeper decks bring stronger castes."),
        };
    }
}
