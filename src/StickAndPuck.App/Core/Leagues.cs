namespace StickAndPuck.App.Core
{
    /// <summary>The league a headless run loads its clubs from: SAP_LEAGUE=nhl plays the scenes and the
    /// screens with the 32 NHL clubs (an NHL season, its standings and its playoffs); unset or shl
    /// keeps the SHL.</summary>
    public static class Leagues
    {
        /// <summary>Gets the league SAP_LEAGUE names, the SHL when it is unset.</summary>
        public static League FromEnvironment()
        {
            string name = Env.Get("SAP_LEAGUE").Trim();
            return name.Length == 0 ? League.Shl
                : Enum.TryParse(name, ignoreCase: true, out League league) ? league
                : throw new InvalidOperationException($"SAP_LEAGUE={name}: use shl or nhl");
        }
    }
}
