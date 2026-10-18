namespace StickAndPuck.App.Core
{
    /// <summary>How icing is called.</summary>
    public enum IcingCall
    {
        /// <summary>Hybrid (SHL, NHL): the race to the end-zone dot decides; the defence wins a tie.</summary>
        Hybrid,

        /// <summary>Touch: icing stands only when a defender touches the puck first; an attacker getting there first waves it off.</summary>
        Touch,

        /// <summary>No icing.</summary>
        Off,
    }
}
