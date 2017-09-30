namespace RoofDrain.Models
{
    using System;

    /// <summary>
    /// The state of a single roof puddle
    /// </summary>
    public class RoofPuddleState
    {
        /// <summary>
        /// The name of the roof puddle
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The last drain check-in time
        /// </summary>
        public DateTimeOffset LastDrainObservationTime { get; set; }

        /// <summary>
        /// If true, the drain was activated at the last observation time
        /// </summary>
        public bool DrainedAtLastObservationTime { get; set; }

        /// <summary>
        /// The estimated number of gallons remaining on the roof
        /// </summary>
        public double EstimatedGallonsRemaining { get; set; }

        /// <summary>
        /// The temperature of the roof
        /// </summary>
        public double Temperature { get; set; }
    }
}
