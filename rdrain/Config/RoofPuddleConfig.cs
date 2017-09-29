namespace RoofDrain.Config
{
    /// <summary>
    /// Configuration for a drained roof puddle
    /// </summary>
    public class RoofPuddleConfig
    {
        /// <summary>
        /// The name of the puddle/drain
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The area of the puddle
        /// </summary>
        public double AreaSquareFeet { get; set; }

        /// <summary>
        /// The rate of water removal
        /// </summary>
        public double DrainRateGallonsPerMinute { get; set; }
    }
}
