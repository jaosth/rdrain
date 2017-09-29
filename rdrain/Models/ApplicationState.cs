namespace RoofDrain.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// The full application state
    /// </summary>
    public class ApplicationState
    {
        /// <summary>
        /// The roof state
        /// </summary>
        public List<RoofPuddleState> RoofPuddleStates { get; set; }

        /// <summary>
        /// The weather state
        /// </summary>
        public List<WeatherStationState> WeatherStationStates { get; set; }
    }
}
