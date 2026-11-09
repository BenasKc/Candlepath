using UnityEngine;

// A burned-out candle stays on its cell as a wax puddle (FlickerLight's last stage).
// Clicking the puddle: left = clean it away (frees the cell for a new candle), right = relight the candle.
// Wax rewards/costs come later with the wax economy.
public static class WaxPuddle
{
    // Returns true if the click landed on a puddle, so the caller skips its normal click handling
    public static bool TryClick(BuildGrid grid, Vector3Int cell, bool leftClick)
    {
        GameObject obj = grid.GetAt(cell);
        if (obj == null) return false;

        var candle = obj.GetComponent<FlickerLight>();
        if (candle == null || !candle.IsOut) return false;

        if (leftClick)
        {
            grid.Free(cell, obj);
            candle.CleanUp();
        }
        else
        {
            candle.Relight();
        }
        return true;
    }
}