namespace RoofDrain.Models
{
    /// <summary>
    /// What the roof drain POSTS to update the state
    /// </summary>
    public class RoofDrainPostBody
    {
        /// <summary>
        /// The current temperature reading in celsius
        /// </summary>
        public double CurrentTemperature { get; set; }

        /// <summary>
        /// If true, the drain is frozen
        /// </summary>
        public bool IsFrozen { get; set; }

        /// <summary>
        /// The current time on the drain device
        /// </summary>
        public int CurrentTime { get; set; }

        /// <summary>
        /// The last time the drain primed
        /// </summary>
        public int TimeOfLastPrime { get; set; }

        /// <summary>
        /// The last time the drain drained
        /// </summary>
        public int TimeOfLastDrain { get; set; }

        /// <summary>
        /// The next time the drain was primed
        /// </summary>
        public int TimeOfNextPrime { get; set; }

        /// <summary>
        /// If true, the drain is draining
        /// </summary>
        public bool IsDraining { get; set; }

        /// <summary>
        /// The message from the drain
        /// </summary>
        public string Message { get; set; }
    }
}
