using StickAndPuck.App.Core;

namespace StickAndPuck.App.Intermission
{
    /// <summary>The trailing talk's state for one match (<see cref="TrailingTalk"/>).</summary>
    internal sealed class TalkState
    {
        /// <summary>Gets or sets the club being asked, or None while no talk is open.</summary>
        public Maybe<Side> Asking { get; set; }

        /// <summary>Gets or sets the period the open talk is for.</summary>
        public int NextPeriod { get; set; }

        /// <summary>Gets or sets how many goals the asked club is behind.</summary>
        public int Deficit { get; set; }

        /// <summary>Gets or sets the answer the stick points at.</summary>
        public TalkChoice Highlight { get; set; } = TalkChoice.Push;

        /// <summary>Gets or sets the seconds the talk has been open.</summary>
        public float Shown { get; set; }

        /// <summary>Gets or sets a value indicating whether shoot or pass was down last step.</summary>
        public bool LastAnswer { get; set; }

        /// <summary>Gets or sets the answer the coach is replying to, shown for a moment after it.</summary>
        public Maybe<TalkChoice> Said { get; set; }

        /// <summary>Gets or sets the seconds the reply still shows.</summary>
        public float SaidFor { get; set; }

        /// <summary>Gets each club's answer, by side.</summary>
        public TalkChoice[] Chosen { get; } = new TalkChoice[2];

        /// <summary>Gets the period each club's answer holds for, by side.</summary>
        public int[] ForPeriod { get; } = new int[2];
    }
}
