using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Trades;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Season.DressingRoom
{
    /// <summary>The trade deadline (W135) in the dressing room news: the offers still on the table, then for a few
    /// games each trade made ("TRADE WITH LHF: ... IN, ... OUT") and each offer turned down.</summary>
    internal sealed class TradeNews : IDressingRoomSource
    {
        /// <summary>The news priority: above the rivalry line and the placeholder season news.</summary>
        public const int Priority = 6;

        /// <summary>How many games a trade stays in the news.</summary>
        public const int Games = 5;

        /// <inheritdoc/>
        public void Fill(DressingRoomContext context, DressingRoomReport report)
        {
            SeasonTrades trades = context.Season.Trades;
            int open = trades.Open.Count;
            if (open > 0)
            {
                report.AddNews(
                    new DressingRoomEntry(
                        open == 1
                            ? Text.Get(K.TradeNewsOffer)
                            : Text.Format(K.TradeNewsOffers, open),
                        Priority + 1,
                        Highlight: true
                    )
                );
            }

            int played = context.Season.PlayedBy(context.UserClub).Count();
            foreach (TradeOffer offer in trades.Offers.Where(o => played - o.Game < Games))
            {
                switch (offer.Answer)
                {
                    case TradeAnswer.Accepted:
                        report.AddNews(
                            new DressingRoomEntry(
                                Text.Format(
                                    K.TradeNewsDone,
                                    offer.GetName.ToUpperInvariant(),
                                    context
                                        .Clubs.FirstOrNone(c =>
                                            string.Equals(
                                                c.Id,
                                                offer.Club,
                                                StringComparison.Ordinal
                                            )
                                        )
                                        .Select(c => c.ShortName)
                                        .Or(offer.Club)
                                        .ToUpperInvariant(),
                                    offer.GiveName.ToUpperInvariant()
                                ),
                                Priority,
                                Highlight: true
                            )
                        );
                        break;
                    case TradeAnswer.Declined:
                        report.AddNews(
                            new DressingRoomEntry(
                                Text.Format(K.TradeNewsDeclined, offer.GiveName.ToUpperInvariant()),
                                Priority - 1
                            )
                        );
                        break;
                    case TradeAnswer.Open:
                        break;
                    default:
                        throw new NotSupportedException("no trade news for " + offer.Answer);
                }
            }
        }
    }
}
