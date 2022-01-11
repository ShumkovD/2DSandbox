using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class MyUtility
{
    public static bool PointToRectCentral(Vector2 point, Vector2 rectCentral, Vector2 r)
    {
        if (rectCentral.x - r.x * 0.5f < point.x && point.x < rectCentral.x + r.x * 0.5f &&
           rectCentral.y - r.y * 0.5f < point.y && point.y < rectCentral.y + r.y * 0.5f)
            return true;
        return false;
    }

    public static bool PointIsInsideOfRect(Vector2 lb, Vector2 ru, Vector3 point)
    {
        if (lb.x < point.x && ru.x > point.x &&
           lb.y < point.y && ru.y > point.y)
            return true;
        return false;
    }

    public static bool PointIsInsideOfRect(Vector2Int lb, Vector2Int ru, Vector2Int point)
    {
        if (lb.x <= point.x && ru.x > point.x &&
           lb.y <= point.y && ru.y > point.y)
            return true;
        return false;
    }

}
