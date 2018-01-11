namespace RoofDrain.Models
{
    using System;

    /// <summary>
    /// The full application state
    /// </summary>
    public class RoofDrainState
    {
        /// <summary>
        /// The last time this status was updated
        /// </summary>
        public DateTimeOffset Updated { get; set; }

        /// <summary>
        /// The current temperature reading in celsius
        /// </summary>
        public double CurrentTemperature { get; set; }

        /// <summary>
        /// If true, the drain is frozen
        /// </summary>
        public bool IsFrozen { get; set; }

        /// <summary>
        /// The last time the drain primed
        /// </summary>
        public DateTimeOffset TimeOfLastPrime { get; set; }

        /// <summary>
        /// The last time the drain drained
        /// </summary>
        public DateTimeOffset TimeOfLastDrain { get; set; }

        /// <summary>
        /// The next time the drain was primed
        /// </summary>
        public DateTimeOffset TimeOfNextPrime { get; set; }

        /// <summary>
        /// If true, the drain is draining
        /// </summary>
        public bool IsDraining { get; set; }

        /// <summary>
        /// A status message
        /// </summary>
        public string Message { get; set; }
    }
}
