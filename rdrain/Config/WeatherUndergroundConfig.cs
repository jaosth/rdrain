namespace RoofDrain.Config
{
    /// <summary>
    /// Configuration for a drained roof puddle
    /// </summary>
    public class WeatherUndergroundConfig
    {
        /// <summary>
        /// The name of the puddle/drain
        /// </summary>
        public string[] Stations { get; set; }

        /// <summary>
        /// The area of the puddle
        /// </summary>
        public string ApiKey { get; set; }
    }
}
