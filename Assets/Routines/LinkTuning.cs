using System.Globalization;
using UnityEngine;
using toio;

// Lets the web app's address tune how robots cope with a slow or bursty Bluetooth link, so it
// can be adjusted on a given laptop without rebuilding. Examples:
//   .../swarmRobots_SR/?lag=0.18          assume commands take 180 ms to act (default 0.13)
//   .../swarmRobots_SR/?deadreckon=0      turn off dead reckoning (compare against before)
//   .../swarmRobots_SR/?maxage=0.8        replay up to 0.8 s past an old position report
// Parameters can be combined with &. The values in use are printed to the browser console.
public static class LinkTuning
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void ReadFromAddress()
    {
        string url = Application.absoluteURL;
        double v;
        if (TryGet(url, "lag", out v) && v >= 0 && v <= 1) { CubeHandle.lag = v; }
        if (TryGet(url, "maxage", out v) && v >= 0 && v <= 3) { CubeHandle.maxPositionAge = v; }
        if (TryGet(url, "deadreckon", out v)) { CubeHandle.deadReckoning = v != 0; }
        Debug.Log("[LinkTuning] lag=" + CubeHandle.lag.ToString(CultureInfo.InvariantCulture)
            + " deadreckon=" + (CubeHandle.deadReckoning ? 1 : 0)
            + " maxage=" + CubeHandle.maxPositionAge.ToString(CultureInfo.InvariantCulture));
    }

    static bool TryGet(string url, string key, out double value)
    {
        value = 0;
        if (string.IsNullOrEmpty(url)) { return false; }
        int q = url.IndexOf('?');
        if (q < 0) { return false; }
        string query = url.Substring(q + 1);
        int hash = query.IndexOf('#');
        if (hash >= 0) { query = query.Substring(0, hash); }
        foreach (var part in query.Split('&'))
        {
            int eq = part.IndexOf('=');
            if (eq <= 0) { continue; }
            if (part.Substring(0, eq).ToLowerInvariant() != key) { continue; }
            return double.TryParse(part.Substring(eq + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
        return false;
    }
}
