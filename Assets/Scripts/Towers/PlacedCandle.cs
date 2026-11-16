using System;
using UnityEngine;

// Added by TowerBuilder to every candle it places, so it can be found again by cell
// and rebuilt as the same type once it burns out. When the candle goes out it puffs smoke
// and starts to smoulder (see Smoulder) so the stub stays findable in the dark.
public class PlacedCandle : MonoBehaviour
{
    public int CatalogIndex { get; private set; }
    public Vector3Int Cell { get; private set; }
    public FlickerLight Flicker { get; private set; }

    public bool IsBurnedOut => Flicker != null && Flicker.IsBurnedOut;

    public event Action<PlacedCandle> BurnedOut;

    public void Init(int catalogIndex, Vector3Int cell)
    {
        CatalogIndex = catalogIndex;
        Cell = cell;
        Flicker = GetComponent<FlickerLight>();
        if (Flicker != null) Flicker.BurnedOut += OnBurnedOut;
        if (GetComponent<CandleExposure>() == null) gameObject.AddComponent<CandleExposure>();
    }

    void OnDestroy()
    {
        if (Flicker != null) Flicker.BurnedOut -= OnBurnedOut;
    }

    void OnBurnedOut()
    {
        CandleFx.BurnOut(gameObject);
        gameObject.AddComponent<Smoulder>();
        BurnedOut?.Invoke(this);
    }
}
