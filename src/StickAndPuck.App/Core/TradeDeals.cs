namespace StickAndPuck.App.Core
{
    /// <summary>Applies a season's trades (<see cref="Trade"/>) to the clubs, so a season's match setups, its
    /// simulated games and its replays all play the rosters as traded.</summary>
    public static class TradeDeals
    {
        /// <summary>The clubs with the trades made, in order. Each player takes the roster slot of the one he was
        /// traded for (the same position, so the lines keep their shape) and wears his new number. A trade
        /// whose clubs or players are not there is skipped, and so is one already made, so applying the
        /// trades to clubs that already have them changes nothing.</summary>
        public static IReadOnlyList<ClubData> Apply(
            IReadOnlyList<ClubData> clubs,
            IEnumerable<Trade> trades
        )
        {
            List<ClubData> list = [.. clubs];
            bool changed = false;
            foreach (Trade t in trades)
            {
                int a = list.FindIndex(c => string.Equals(c.Id, t.Club, StringComparison.Ordinal));
                int b = list.FindIndex(c =>
                    string.Equals(c.Id, t.OtherClub, StringComparison.Ordinal)
                );
                if (a < 0 || b < 0 || a == b)
                {
                    continue;
                }

                int sa = Slot(list[a], t.Number);
                int sb = Slot(list[b], t.OtherNumber);
                if (sa < 0 || sb < 0)
                {
                    continue; // already made, or not these rosters
                }

                PlayerData leaving = list[a].Roster[sa];
                PlayerData coming = list[b].Roster[sb];
                if (leaving.Position != coming.Position)
                {
                    continue;
                }

                list[a] = With(list[a], sa, coming with { Number = t.OtherNewNumber });
                list[b] = With(list[b], sb, leaving with { Number = t.NewNumber });
                changed = true;
            }

            return changed ? list : clubs;
        }

        /// <summary>Gets the roster slot of a number, or -1.</summary>
        private static int Slot(ClubData club, int number)
        {
            for (int i = 0; i < club.Roster.Count; i++)
            {
                if (club.Roster[i].Number == number)
                {
                    return i;
                }
            }

            return -1;
        }

        private static ClubData With(ClubData club, int slot, PlayerData player)
        {
            List<PlayerData> roster = [.. club.Roster];
            roster[slot] = player;
            return club with { Roster = roster };
        }
    }
}
