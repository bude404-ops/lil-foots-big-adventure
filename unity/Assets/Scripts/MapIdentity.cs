using UnityEngine;

namespace LilFoots {
    /// <summary>
    /// BUILD IDENTITY (BudE Sept 27 PM: "fix the pipeline so it only sends the fixed
    /// updated versions"): every shipped scene carries a proof of WHAT it is — which map
    /// data file built it, from which commit, on which forge run. The smoke gate asserts
    /// this matches the MAP_DATA/BUILD_SHA the dispatcher asked for, so a stale or wrong
    /// map can NEVER reach the live link silently again.
    /// </summary>
    public class MapIdentity : MonoBehaviour {
        [HideInInspector] public string dataFile = "unknown";   // e.g. map_region1_spine.json
        [HideInInspector] public string buildSha = "local";     // git head the forge built from
        [HideInInspector] public string buildStamp = "local";   // forge run identity (run-NNN)
        [HideInInspector] public int plats, tokens, bumps;      // counts actually placed

        public string Describe() { return dataFile + " @ " + buildSha.Substring(0, Mathf.Min(7, buildSha.Length)) + " (" + buildStamp + ")"; }
    }
}
