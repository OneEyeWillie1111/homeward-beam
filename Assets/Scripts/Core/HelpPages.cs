namespace Abduction.Core
{
    /// <summary>In-game Help menu pages (title, body).</summary>
    public static class HelpPages
    {
        public static readonly (string Title, string Body)[] Pages =
        {
            ("Goal", GameData.Intro + "\n\nEach deck has a glowing teleporter pad: stand on it and tap USE to beam down. Your role sets how many decks: Stargazer 10, Abductee 20, Specimen 35, Doomed 50. The Coiled Sovereign guards the machine home on the last deck."),
            ("Controls", "Put your thumb on the bottom-centre of the screen, over your head: a big joystick appears under it. Push up to walk, down to back up, left/right to turn. You slide along walls.\nRight-edge buttons: ATTACK hits the alien in front of you. USE opens the hatch or locker ahead, beams you down on a teleporter, or uses the held item (food heals). SCAN maps the area and points to the teleporter. DROP/FLEE is the small top button.\nThe map in the corner fills in as you explore: you are the arrow, cyan is a teleporter, gold is the machine home, red dots are aliens you've seen."),
            ("Fighting", "Walk into an alien, or face it and tap ATTACK, to fight. Fights are turn-based: each ATTACK, USE or FLEE (not from the boss) is one turn and the alien strikes back after it. SCAN reads the alien for free. Movement pauses until the fight ends.\nThe enemy's health bar appears above the buttons."),
            ("Health", "WAR (red bar) is your body. SPIRIT (blue bar) is your mind. Both run 0-99.\nWar aliens (red eyes) hurt War; spirit aliens (cyan eyes) hurt Spirit. If either bar empties you are re-cloned on deck 1 with weaker maximums."),
            ("Ring inventory", "Your 8 items sit in the ring in the top-right corner; the centre shows what you hold. Tap a slot to hold it; DROP leaves the held item.\nRed ring = equipped weapon (used by ATTACK). Blue ring = worn armor (best armor, automatic). Gold = the item you're holding.\nWeapons hurt war aliens fully; psionic items hurt spirit aliens fully and cost 1 Spirit."),
            ("Aliens", "Weakest to strongest: Brood Drudge & Shade Thrall, Husk Stalker & Pale Vessel, Mantis Lancer & Mantis Oracle, Half-Scale Sentinel & Gilded Herald, the Grays, and the Serpent Lords (Saurians). Deeper decks bring stronger castes."),
        };
    }
}
